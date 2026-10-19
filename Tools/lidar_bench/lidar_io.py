"""Driver đọc LiDAR trên laptop: RPLIDAR C1 (Slamtec) + D200 (giao thức LD14P, LDROBOT),
bộ mô phỏng (không cần phần cứng) và ghi/phát lại CSV.

Mọi nguồn đều cho ra cùng 1 kiểu điểm `Pt` (t, rev, angle, dist, q) để pipeline/viewer/analyze
không phụ thuộc loại cảm biến. `angle` là góc THÔ của cảm biến (độ, [0,360)), CHƯA cộng
offset_angle — pipeline cộng sau, đúng như native (liblidar.cpp: lidar_frame_Parse).
"""
from __future__ import annotations

import csv
import math
import queue
import random
import struct
import sys
import threading
import time
from dataclasses import dataclass
from typing import Iterable, List, Optional

for _s in (sys.stdout, sys.stderr):  # console Windows mặc định cp1252 → lỗi khi in tiếng Việt
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:  # noqa: BLE001
        pass

try:
    import serial  # pyserial
except ImportError:  # chỉ cần khi đọc cổng thật
    serial = None


@dataclass
class Pt:
    t: float      # giây, time.perf_counter() lúc khối byte chứa điểm này được đọc từ cổng
    rev: int      # số thứ tự vòng quay (tăng khi góc quay về 0)
    angle: float  # độ, thô
    dist: float   # mm
    q: int        # chất lượng/độ tin cậy (C1 standard: 0-63; D200: intensity 0-255; C1 dense: 0)


# ──────────────────────────────────────────────────────────────────────────────
# Nguồn dữ liệu (thread đọc + hàng đợi)
# ──────────────────────────────────────────────────────────────────────────────
class Source:
    model = "?"

    def __init__(self):
        self.q: "queue.Queue[Pt]" = queue.Queue()
        self.stats = {"bytes": 0, "frames": 0, "bad_frames": 0, "points": 0, "zero": 0}
        self.info = {}
        self.error: Optional[str] = None
        self._stop = threading.Event()
        self._th: Optional[threading.Thread] = None
        self._rev = 0
        self._last_angle = -1.0

    def start(self):
        self._th = threading.Thread(target=self._guard, daemon=True, name=f"{self.model}-reader")
        self._th.start()
        return self

    def stop(self):
        self._stop.set()
        if self._th:
            self._th.join(timeout=2)

    def drain(self) -> List[Pt]:
        out = []
        try:
            while True:
                out.append(self.q.get_nowait())
        except queue.Empty:
            return out

    def _guard(self):
        try:
            self._run()
        except Exception as e:  # noqa: BLE001 — báo lên UI thay vì chết lặng
            self.error = f"{type(e).__name__}: {e}"

    def _run(self):
        raise NotImplementedError

    def _emit(self, t: float, angle: float, dist: float, q: int):
        """Gán số vòng theo bước nhảy góc rồi đẩy vào hàng đợi."""
        if self._last_angle >= 0 and angle < self._last_angle - 180.0:
            self._rev += 1
        self._last_angle = angle
        self.stats["points"] += 1
        self.q.put(Pt(t, self._rev, angle, dist, q))


class SerialSource(Source):
    baud = 115200

    def __init__(self, port: str, baud: Optional[int] = None):
        super().__init__()
        if serial is None:
            raise RuntimeError("Thiếu pyserial: pip install pyserial")
        self.port = port
        if baud:
            self.baud = baud
        self.ser = None

    def _open(self):
        try:
            self.ser = serial.Serial(self.port, self.baud, timeout=0.02)
        except serial.SerialException as e:
            raise RuntimeError(
                f"Không mở được {self.port}: {e}. Cổng đang bị app khác giữ "
                "(Slamtec RoboStudio, MyNativeApp, terminal...) — đóng app đó rồi chạy lại.") from e

    def _read_chunk(self):
        n = self.ser.in_waiting
        data = self.ser.read(n if n > 0 else 1)
        t = time.perf_counter()
        self.stats["bytes"] += len(data)
        return data, t


# ── D200 / LD14P ─────────────────────────────────────────────────────────────
def _crc8_table():
    tab = []
    for i in range(256):
        c = i
        for _ in range(8):
            c = ((c << 1) ^ 0x4D) & 0xFF if c & 0x80 else (c << 1) & 0xFF
        tab.append(c)
    return tab


_CRC8 = _crc8_table()  # trùng bảng CrcTable trong liblidar.cpp (đa thức 0x4D)


def crc8(data: bytes) -> int:
    c = 0
    for b in data:
        c = _CRC8[c ^ b]
    return c


class D200Source(SerialSource):
    """Gói 47 byte: 54 2C | speed u16 | start u16(0.01°) | 12×(dist u16, intensity u8) | end u16 | ts u16 | crc8.
    Cùng giao thức native (liblidar.cpp), 230400 baud."""
    model = "d200"
    baud = 230400
    PKT = 47

    def _run(self):
        self._open()
        buf = bytearray()
        while not self._stop.is_set():
            data, t = self._read_chunk()
            if not data:
                continue
            buf += data
            while True:
                i = buf.find(b"\x54\x2c")
                if i < 0:
                    del buf[:-1]
                    break
                if i > 0:
                    del buf[:i]
                if len(buf) < self.PKT:
                    break
                pkt = bytes(buf[: self.PKT])
                if crc8(pkt[:-1]) != pkt[-1]:
                    self.stats["bad_frames"] += 1
                    del buf[:2]
                    continue
                del buf[: self.PKT]
                self.stats["frames"] += 1
                self._parse(pkt, t)

    def _parse(self, p: bytes, t: float):
        speed = struct.unpack_from("<H", p, 2)[0]
        start = struct.unpack_from("<H", p, 4)[0]
        end = struct.unpack_from("<H", p, 42)[0]
        # bộ lọc gói lệch giống native: diff <= speed*12/4000*1.5 (số nguyên, độ)
        diff_deg = ((end // 100 - start // 100) + 360) % 360
        if diff_deg > speed * 12 / 4000 * 1.5:
            self.stats["bad_frames"] += 1
            return
        diff2 = (end + 36000 - start) % 36000
        step = diff2 / 11.0
        for i in range(12):
            d, q = struct.unpack_from("<HB", p, 6 + i * 3)
            if d == 0:
                continue
            a = (start + i * step) / 100.0
            if a >= 360.0:
                a -= 360.0
            self._emit(t, a, float(d), q)


# ── RPLIDAR C1 ───────────────────────────────────────────────────────────────
class C1Source(SerialSource):
    """Slamtec RPLIDAR C1, 460800 baud. Tự dò chế độ quét: standard (A5 20, node 5 byte) rồi
    dense (A5 82, capsule 84 byte, ans-type 0x85). C1 tự quay motor, không cần DTR/PWM."""
    model = "c1"
    baud = 460800

    def __init__(self, port: str, baud: Optional[int] = None, mode: str = "auto", flip_y: bool = True, flip_x: bool = True):
        super().__init__(port, baud)
        self.mode = mode
        self.scan_mode = None
        # Trục Y của C1 ngược D200 → đổi về quy ước D200 (góc → 180° − góc), giống rplidar_ParseByte trên K02.
        self.flip_y = flip_y
        self.flip_x = flip_x  # đảo X: a → −a (làm trước đảo Y)

    # -- giao thức lệnh --
    def _send(self, cmd: int, payload: bytes = b""):
        pkt = bytes([0xA5, cmd])
        if payload:
            pkt += bytes([len(payload)]) + payload
            x = 0
            for b in pkt:
                x ^= b
            pkt += bytes([x])
        self.ser.write(pkt)

    def _read_exact(self, n: int, timeout: float) -> bytes:
        end = time.perf_counter() + timeout
        out = b""
        while len(out) < n and time.perf_counter() < end:
            out += self.ser.read(n - len(out))
        return out

    def _descriptor(self, timeout=1.0):
        """Tìm A5 5A rồi đọc 5 byte còn lại → (độ dài dữ liệu, send_mode, kiểu dữ liệu) hoặc None."""
        end = time.perf_counter() + timeout
        win = b""
        while time.perf_counter() < end:
            b = self.ser.read(1)
            if not b:
                continue
            win = (win + b)[-2:]
            if win == b"\xa5\x5a":
                rest = self._read_exact(5, 0.3)
                if len(rest) < 5:
                    return None
                ln = struct.unpack("<I", rest[:4])[0]
                return ln & 0x3FFFFFFF, ln >> 30, rest[4]
        return None

    def _request(self, cmd: int, payload: bytes = b"", timeout=1.0) -> Optional[bytes]:
        self.ser.reset_input_buffer()
        self._send(cmd, payload)
        d = self._descriptor(timeout)
        if not d:
            return None
        return self._read_exact(d[0], timeout)

    def _conf(self, typ: int, opt: bytes = b"") -> Optional[bytes]:
        r = self._request(0x84, struct.pack("<I", typ) + opt)
        return r[4:] if r and len(r) >= 4 else None

    def probe(self):
        """Hỏi thông tin/sức khoẻ/danh sách chế độ quét — dùng cho `lidar_view --probe`."""
        self._open()
        self._send(0x25)  # STOP
        time.sleep(0.1)
        self.ser.reset_input_buffer()
        info = self._request(0x50)
        if info and len(info) == 20:
            self.info["model_id"] = info[0]
            self.info["firmware"] = f"{info[2]}.{info[1]:02d}"
            self.info["hardware"] = info[3]
            self.info["serial"] = info[4:].hex()
        h = self._request(0x52)
        if h and len(h) == 3:
            self.info["health"] = {0: "good", 1: "warning", 2: "error"}.get(h[0], h[0])
        cnt = self._conf(0x70)
        modes = []
        if cnt and len(cnt) >= 2:
            for i in range(struct.unpack("<H", cnt[:2])[0]):
                o = struct.pack("<H", i)
                name = self._conf(0x7F, o)
                us = self._conf(0x71, o)
                mr = self._conf(0x74, o)
                at = self._conf(0x75, o)
                modes.append({
                    "id": i,
                    "name": name.rstrip(b"\x00").decode(errors="replace") if name else "?",
                    "us_per_sample": struct.unpack("<I", us[:4])[0] / 256.0 if us and len(us) >= 4 else None,
                    "max_range_m": struct.unpack("<I", mr[:4])[0] / 256.0 if mr and len(mr) >= 4 else None,
                    "ans_type": hex(at[0]) if at else None,
                })
        typ = self._conf(0x7C)
        self.info["typical_mode"] = struct.unpack("<H", typ[:2])[0] if typ and len(typ) >= 2 else None
        self.info["modes"] = modes
        self.ser.close()
        return self.info

    # -- quét --
    def _run(self):
        self._open()
        self._send(0x25)
        time.sleep(0.1)
        self.ser.reset_input_buffer()
        self._request(0x50)
        order = {"auto": ["standard", "dense"], "standard": ["standard"], "dense": ["dense"]}[self.mode]
        started = None
        for m in order:
            self.ser.reset_input_buffer()
            if m == "standard":
                self._send(0x20)
                want = 0x81
            else:
                self._send(0x82, b"\x00\x00\x00\x00\x00")
                want = 0x85
            d = self._descriptor(1.0)
            if d and d[2] == want:
                started = m
                break
            self._send(0x25)
            time.sleep(0.1)
        if not started:
            raise RuntimeError("C1 không trả lời lệnh quét (thử standard + dense). Kiểm tra baud 460800 / cáp.")
        self.scan_mode = started
        self.info["scan_mode"] = started
        buf = bytearray()
        prev_cap = None
        while not self._stop.is_set():
            data, t = self._read_chunk()
            if not data:
                continue
            buf += data
            if started == "standard":
                self._parse_standard(buf, t)
            else:
                prev_cap = self._parse_dense(buf, t, prev_cap)
        try:
            self._send(0x25)
        except Exception:  # noqa: BLE001
            pass

    def _orient(self, a: float) -> float:
        """Quy ước C1 → D200: đảo X (a → −a) rồi đảo Y (a → 180° − a), giống rplidar_ParseByte trên K02."""
        if self.flip_x:
            a = -a
        if self.flip_y:
            a = 180.0 - a
        return a % 360.0

    def _parse_standard(self, buf: bytearray, t: float):
        while len(buf) >= 5:
            b0 = buf[0]
            if (b0 & 1) == ((b0 >> 1) & 1) or not (buf[1] & 1):  # S và !S phải ngược nhau; C bit = 1
                del buf[0]
                self.stats["bad_frames"] += 1
                continue
            ang = ((buf[2] << 7) | (buf[1] >> 1)) / 64.0
            dist = (buf[3] | (buf[4] << 8)) / 4.0
            q = b0 >> 2
            del buf[:5]
            self.stats["frames"] += 1
            if dist > 0:
                self._emit(t, self._orient(ang), dist, q)
            else:
                self.stats["zero"] = self.stats.get("zero", 0) + 1  # node hợp lệ nhưng không có phản xạ (dist=0)

    def _parse_dense(self, buf: bytearray, t: float, prev):
        CAP = 84
        while len(buf) >= CAP:
            if (buf[0] >> 4) != 0xA or (buf[1] >> 4) != 0x5:
                del buf[0]
                continue
            want = (buf[0] & 0xF) | ((buf[1] & 0xF) << 4)
            x = 0
            for b in buf[2:CAP]:
                x ^= b
            if x != want:
                del buf[0]
                self.stats["bad_frames"] += 1
                continue
            start = (struct.unpack_from("<H", buf, 2)[0] & 0x7FFF) * 4  # q8
            cabins = struct.unpack_from("<40H", buf, 4)
            del buf[:CAP]
            self.stats["frames"] += 1
            if prev is not None:
                p_start, p_cabins = prev
                diff = start - p_start
                if p_start > start:
                    diff += 360 << 8
                inc = (diff << 8) // 40
                cur = p_start << 8
                for d in p_cabins:
                    a = (cur >> 10) / 64.0
                    cur += inc
                    if d > 0:
                        self._emit(t, self._orient(a), float(d), 0)
            prev = (start, cabins)
        return prev


# ──────────────────────────────────────────────────────────────────────────────
# Mô phỏng (kiểm thử pipeline khi chưa có phần cứng)
# ──────────────────────────────────────────────────────────────────────────────
SIM_SPEC = {  # tốc độ lấy mẫu (điểm/s), vòng/s, nhiễu khoảng cách σ (mm), tỉ lệ rớt điểm
    "c1": dict(fs=5000.0, hz=10.0, sigma=6.0, drop=0.02),
    "d200": dict(fs=4000.0, hz=6.0, sigma=10.0, drop=0.03),  # LD14P: 4000 mẫu/s @ 6 Hz = 667 điểm/vòng = 0,54°
}


class SimWorld:
    """Phòng hình chữ nhật + trụ tròn bật/tắt theo chu kỳ. Quy ước toạ độ giống native:
    x = -d·sin(a), y = d·cos(a)."""

    def __init__(self, targets=None, walls=(-2500, 2500, -3000, 800)):
        # target: (x, y, r, t_on, t_off, period) — bật trong [t_on, t_off) của mỗi chu kỳ; period=0 → luôn bật
        self.targets = targets if targets is not None else [(-150.0, -1000.0, 25.0, 1.0, 2.5, 4.0)]
        self.walls = walls

    def range_at(self, a_deg: float, t: float) -> float:
        dx, dy = -math.sin(math.radians(a_deg)), math.cos(math.radians(a_deg))
        best = 1e9
        x0, x1, y0, y1 = self.walls
        for lim, d, o in ((x0, dx, 0), (x1, dx, 0)):
            if abs(d) > 1e-9:
                s = lim / d
                if s > 0:
                    best = min(best, s)
        for lim in (y0, y1):
            if abs(dy) > 1e-9:
                s = lim / dy
                if s > 0:
                    best = min(best, s)
        for (cx, cy, r, ton, toff, per) in self.targets:
            if per > 0:
                ph = t % per
                if not (ton <= ph < toff):
                    continue
            b = dx * cx + dy * cy
            disc = b * b - (cx * cx + cy * cy - r * r)
            if disc >= 0:
                s = b - math.sqrt(disc)
                if s > 0:
                    best = min(best, s)
        return best


def sim_points(model: str, t0: float, t1: float, world: SimWorld, rng: random.Random, rev0: int = 0) -> List[Pt]:
    """Sinh điểm cho khoảng thời gian ảo [t0, t1) — dùng cho cả live (thời gian thật) lẫn offline."""
    sp = SIM_SPEC[model]
    dt = 1.0 / sp["fs"]
    out = []
    n0 = int(math.ceil(t0 / dt))
    n1 = int(math.ceil(t1 / dt))
    for n in range(n0, n1):
        t = n * dt
        rev_f = t * sp["hz"]
        a = (rev_f % 1.0) * 360.0
        if rng.random() < sp["drop"]:
            continue
        r = world.range_at(a, t)
        if r > 12000:
            continue
        r += rng.gauss(0.0, sp["sigma"])
        out.append(Pt(t, int(rev_f) + rev0, a, max(r, 1.0), 40))
    return out


class SimSource(Source):
    """Phát điểm mô phỏng theo thời gian thật. model: c1 | d200."""

    def __init__(self, model: str, world: Optional[SimWorld] = None, seed: int = 1):
        super().__init__()
        self.model = f"sim-{model}"
        self._m = model
        self.world = world or SimWorld()
        self.rng = random.Random(seed)

    def _run(self):
        t_start = time.perf_counter()
        t_prev = 0.0
        while not self._stop.is_set():
            time.sleep(0.01)
            t_now = time.perf_counter() - t_start
            for p in sim_points(self._m, t_prev, t_now, self.world, self.rng):
                self.stats["points"] += 1
                self.q.put(Pt(t_start + p.t, p.rev, p.angle, p.dist, p.q))
            t_prev = t_now


# ──────────────────────────────────────────────────────────────────────────────
# CSV ghi / phát lại
# ──────────────────────────────────────────────────────────────────────────────
class Recorder:
    def __init__(self, path: str, meta: dict):
        self.f = open(path, "w", newline="", encoding="utf-8")
        for k, v in meta.items():
            self.f.write(f"# {k}={v}\n")
        self.w = csv.writer(self.f)
        self.w.writerow(["t_ns", "rev", "angle", "dist", "q"])
        self.n = 0

    def write(self, pts: Iterable[Pt]):
        for p in pts:
            self.w.writerow([int(p.t * 1e9), p.rev, f"{p.angle:.3f}", f"{p.dist:.1f}", p.q])
            self.n += 1

    def close(self):
        self.f.close()


def load_csv(path: str):
    """→ (meta dict, list[Pt]); t chuyển về giây tương đối từ 0."""
    meta, pts = {}, []
    with open(path, newline="", encoding="utf-8") as f:
        rows = []
        for line in f:
            if line.startswith("#"):
                k, _, v = line[1:].strip().partition("=")
                meta[k.strip()] = v.strip()
            else:
                rows.append(line)
    rd = csv.DictReader(rows)
    for r in rd:
        pts.append(Pt(int(r["t_ns"]) / 1e9, int(r["rev"]), float(r["angle"]), float(r["dist"]), int(r["q"])))
    if pts:
        t0 = pts[0].t
        for p in pts:
            p.t -= t0
    return meta, pts


class ReplaySource(Source):
    """Phát lại CSV theo thời gian thật (hoặc nhanh hơn: speed>1)."""

    def __init__(self, path: str, speed: float = 1.0, loop: bool = True):
        super().__init__()
        self.meta, self.pts = load_csv(path)
        self.model = "replay-" + self.meta.get("model", "?")
        self.speed, self.loop = speed, loop

    def _run(self):
        if not self.pts:
            return
        dur = self.pts[-1].t
        while not self._stop.is_set():
            t_start = time.perf_counter()
            i = 0
            while i < len(self.pts) and not self._stop.is_set():
                now = (time.perf_counter() - t_start) * self.speed
                while i < len(self.pts) and self.pts[i].t <= now:
                    p = self.pts[i]
                    self.stats["points"] += 1
                    self.q.put(Pt(t_start + p.t / self.speed, p.rev, p.angle, p.dist, p.q))
                    i += 1
                time.sleep(0.005)
            if not self.loop:
                break
            time.sleep(0.2)


def make_source(model: str, port: Optional[str], baud: Optional[int] = None, mode: str = "auto",
                replay: Optional[str] = None, speed: float = 1.0, flip_y: bool = True, flip_x: bool = True) -> Source:
    if replay:
        return ReplaySource(replay, speed)
    if model in ("sim-c1", "sim-d200"):
        return SimSource(model.split("-")[1])
    if not port:
        raise SystemExit("Cần --port COMx (hoặc --model sim-c1 / --replay file.csv)")
    if model == "c1":
        return C1Source(port, baud, mode, flip_y, flip_x)
    if model == "d200":
        return D200Source(port, baud)
    raise SystemExit(f"Model không hợp lệ: {model}")
