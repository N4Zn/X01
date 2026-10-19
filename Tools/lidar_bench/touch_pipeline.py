"""Bản port Python của pipeline nhận diện điểm chạm D200 đang chạy trên K02.

Nguồn gốc (đối chiếu khi sửa native, hai bên PHẢI khớp nhau):
  NativePlugins/LidarUnity/src/liblidar.cpp  — point_is_in_area, cluster_points, find_toe_points,
                                               convert_to_1024x600, sendTouch (throttle)
  NativePlugins/LidarUnity/src/native-lib.cpp — ld14p_parse_xypoint2touch_fnc (đệm điểm),
                                               ld14p_consumer_thread_function (tick 100 ms)
KHÔNG port: LidarProcessor.cpp / find_position() — code chết, không được gọi trong pipeline thật.

Dùng cho: viewer trên laptop, đo độ trễ offline từ file ghi, thử tham số (window/tick/min_cluster/nums)
cho C1 mà không cần build lại native.
"""
from __future__ import annotations

import json
import math
from dataclasses import dataclass, field, replace
from typing import List, Optional, Tuple

from lidar_io import Pt

REF_W, REF_H = 1024, 600


@dataclass
class Cfg:
    # — lidar_config.json (hình học vùng sàn). Mặc định = máy chiếu #2 (docs/lidar-calib.md) —
    half_x: int = 1190
    hight_floor: int = 1380
    ymax: int = -800
    shift_x_floor: int = -110
    shift_y: int = -15
    shift_x: int = -35
    offset_angle: float = -5
    nums_point_report: int = 2
    # — hằng số cứng trong native (mặc định = giá trị đang chạy) —
    window_s: float = 0.30      # MAX_POINT_LIFE_SEC (native-lib.cpp:43)
    tick_s: float = 0.10        # CLUSTER_ANALYSIS_INTERVAL (native-lib.cpp:44)
    cluster_mm: float = 30.0    # DISTANCE_THRESHOLD (liblidar.h)
    min_cluster: int = 3        # find_toe_points(clusters, 3) (native-lib.cpp)
    throttle_s: float = 0.150   # TOUCH_MIN_INTERVAL_MS (liblidar.cpp)
    merge_px: float = 100.0     # SAME_TOUCH_MERGE_RADIUS (liblidar.cpp)
    max_range_mm: float = 4000.0  # point_is_in_area: distance > 4000 bị loại

    @staticmethod
    def from_json(path: str) -> "Cfg":
        with open(path, encoding="utf-8") as f:
            d = json.load(f)
        c = Cfg()
        for k in ("half_x", "hight_floor", "ymax", "shift_x_floor", "shift_y", "shift_x",
                  "offset_angle", "nums_point_report"):
            if k in d:
                setattr(c, k, d[k])
        return c

    # vùng sàn (mm) trong hệ toạ độ native, sau khi cộng offset_angle
    @property
    def xmin(self): return -self.half_x + self.shift_x_floor
    @property
    def xmax(self): return self.half_x + self.shift_x_floor
    @property
    def ymin(self): return self.ymax - self.hight_floor


@dataclass
class Touch:
    t_tick: float                 # thời điểm tick phát hiện (giây, cùng trục với Pt.t)
    t_first: float                # điểm đầu tiên (cũ nhất) của cụm gây ra touch này
    mm: Tuple[float, float]       # toe point (x, y) mm
    ref: Tuple[int, int]          # toạ độ 1024x600 gửi cho Unity
    n_cluster: int
    sent: bool                    # False = bị throttle 150 ms/100 px chặn

    @property
    def latency(self) -> float:
        return self.t_tick - self.t_first


def to_xy(angle_raw: float, dist: float, offset: float) -> Tuple[float, float, float]:
    a = angle_raw + offset
    if a >= 360.0:
        a -= 360.0
    elif a < 0.0:
        a += 360.0
    r = math.radians(a)
    return -dist * math.sin(r), dist * math.cos(r), a


def convert_to_ref(cfg: Cfg, x: float, y: float) -> Tuple[int, int]:
    """convert_to_1024x600() — lưu ý native cộng shift_x_floor 2 lần (1 trong xmin, 1 trong biểu thức); giữ nguyên."""
    px = int(((x - (cfg.xmin + cfg.shift_x_floor)) / (2 * cfg.half_x)) * REF_W + cfg.shift_x)
    py = int((REF_H - (((y - cfg.ymin) / cfg.hight_floor) * REF_H)) + cfg.shift_y)
    return px, py


def cluster_points(pts: List[Tuple[float, float, float]], thr_mm: float):
    """Gom cụm tham lam y như native: điểm vào cụm ĐẦU TIÊN có 1 điểm cách ≤ thr, không gộp cụm về sau."""
    thr2 = thr_mm * thr_mm
    clusters: List[list] = []
    for p in pts:
        for c in clusters:
            hit = False
            for q in c:
                dx, dy = p[0] - q[0], p[1] - q[1]
                if dx * dx + dy * dy <= thr2:
                    hit = True
                    break
            if hit:
                c.append(p)
                break
        else:
            clusters.append([p])
    return clusters


class TouchPipeline:
    """Cấp điểm theo thứ tự thời gian bằng feed(); gọi advance(now) trước mỗi feed để chạy các tick."""

    def __init__(self, cfg: Cfg):
        self.cfg = cfg
        self.buf: List[Tuple[float, float, float]] = []  # (x, y, t) điểm trong vùng
        self.recent: List[Tuple[int, int, float]] = []   # (refx, refy, t) lần gửi gần nhất, cho throttle
        self.next_tick: Optional[float] = None
        self.last_clusters: List[list] = []
        self.last_toes: List[Tuple[float, float, int]] = []
        self.n_in_area = 0
        self.n_total = 0

    # point_is_in_area()
    def in_area(self, x: float, y: float, a: float, dist: float) -> bool:
        c = self.cfg
        if 0 <= a <= 90 or 270 <= a <= 360:
            return False
        if dist == 0 or dist > c.max_range_mm:
            return False
        return c.xmin <= x <= c.xmax and c.ymin <= y <= c.ymax

    def feed(self, p: Pt) -> bool:
        self.n_total += 1
        x, y, a = to_xy(p.angle, p.dist, self.cfg.offset_angle)
        if not self.in_area(x, y, a, p.dist):
            return False
        self.buf.append((x, y, p.t))
        self.n_in_area += 1
        return True

    def advance(self, now: float) -> List[Touch]:
        out: List[Touch] = []
        if self.next_tick is None:
            self.next_tick = now + self.cfg.tick_s
            return out
        while self.next_tick <= now:
            out += self._tick(self.next_tick)
            self.next_tick += self.cfg.tick_s
        return out

    def _tick(self, T: float) -> List[Touch]:
        c = self.cfg
        # native sao chép bộ đệm TRƯỚC rồi mới xoá điểm quá 300 ms → cụm có thể chứa điểm cũ hơn window
        pts = [p for p in self.buf if p[2] <= T]
        self.buf = [p for p in self.buf if p[2] >= T - c.window_s]
        self.last_clusters, self.last_toes = [], []
        touches: List[Touch] = []
        if len(pts) <= c.nums_point_report:
            return touches
        clusters = cluster_points(pts, c.cluster_mm)
        self.last_clusters = clusters
        for cl in clusters:
            if len(cl) < c.min_cluster:
                continue
            toe = cl[0]
            for q in cl:
                if q[1] > toe[1]:
                    toe = q
            ref = convert_to_ref(c, toe[0], toe[1])
            self.last_toes.append((toe[0], toe[1], len(cl)))
            sent = self._throttle(ref, T)
            touches.append(Touch(T, min(q[2] for q in cl), (toe[0], toe[1]), ref, len(cl), sent))
        return touches

    # sendTouch(): mỗi vị trí có mốc thời gian riêng
    def _throttle(self, ref: Tuple[int, int], T: float) -> bool:
        c = self.cfg
        self.recent = [r for r in self.recent if T - r[2] < c.throttle_s]
        for r in self.recent:
            dx, dy = r[0] - ref[0], r[1] - ref[1]
            if dx * dx + dy * dy <= c.merge_px * c.merge_px:
                return False
        self.recent.append((ref[0], ref[1], T))
        return True


def run_offline(pts: List[Pt], cfg: Cfg) -> Tuple[List[Touch], TouchPipeline]:
    """Phát lại toàn bộ điểm qua pipeline → danh sách touch (cả bị throttle, cờ .sent)."""
    pl = TouchPipeline(cfg)
    touches: List[Touch] = []
    for p in pts:
        touches += pl.advance(p.t)
        pl.feed(p)
    if pts:
        touches += pl.advance(pts[-1].t + cfg.tick_s)
    return touches, pl


def episodes(touches: List[Touch], gap_s: float = 0.6) -> List[List[Touch]]:
    """Nhóm touch (đã gửi) thành đợt chạm: cách nhau > gap_s coi là đợt mới."""
    eps: List[List[Touch]] = []
    for t in touches:
        if not t.sent:
            continue
        if eps and t.t_tick - eps[-1][-1].t_tick <= gap_s:
            eps[-1].append(t)
        else:
            eps.append([t])
    return eps
