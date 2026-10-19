"""Phân tích file CSV ghi từ lidar_view.py / lidar_capture.py.

  stats    FILE                    tốc độ quay, điểm/vòng, độ phân giải góc, tỉ lệ rớt điểm
  static   FILE --truth-dist MM    độ chính xác khi có 1 trụ đứng yên cách cảm biến MM (tâm trụ)
  latency  FILE [--config JSON]    chạy lại pipeline chạm D200 → độ trễ "điểm đầu → báo chạm"
  compare  A.csv B.csv [...]       bảng so sánh C1 vs D200 (stats + static + latency)

Mọi cờ pipeline (--window --tick --nums --min-cluster --cluster-mm) đều chỉnh được để xem C1
sẽ ra sao nếu đổi tham số, không cần build lại native.
"""
from __future__ import annotations

import argparse
import math
import statistics as st
import sys
from typing import Dict, List, Optional

from lidar_io import Pt, load_csv
from touch_pipeline import Cfg, episodes, run_offline


def _pct(v: List[float], p: float) -> float:
    if not v:
        return float("nan")
    v = sorted(v)
    k = (len(v) - 1) * p
    f, c = int(math.floor(k)), int(math.ceil(k))
    return v[f] + (v[c] - v[f]) * (k - f)


def _fmt(x, nd=1):
    if x is None or (isinstance(x, float) and math.isnan(x)):
        return "—"
    return f"{x:.{nd}f}" if isinstance(x, float) else str(x)


# ── stats ────────────────────────────────────────────────────────────────────
def calc_stats(pts: List[Pt]) -> Dict:
    if len(pts) < 10:
        return {}
    dur = pts[-1].t - pts[0].t
    revs: Dict[int, List[Pt]] = {}
    for p in pts:
        revs.setdefault(p.rev, []).append(p)
    keys = sorted(revs)[1:-1] or sorted(revs)  # bỏ vòng đầu/cuối (dở dang)
    t_first = [revs[k][0].t for k in keys]
    periods = [b - a for a, b in zip(t_first, t_first[1:])]
    per_rev = [len(revs[k]) for k in keys]
    steps = []
    for k in keys:
        a = [p.angle for p in revs[k]]
        steps += [(y - x) % 360 for x, y in zip(a, a[1:])]
    steps = [s for s in steps if 0 < s < 5]
    return {
        "duration_s": dur,
        "points": len(pts),
        "pts_per_s": len(pts) / dur if dur > 0 else float("nan"),
        "rev_hz": 1.0 / st.mean(periods) if periods else float("nan"),
        "rev_hz_std": (st.pstdev([1.0 / x for x in periods if x > 0]) if len(periods) > 1 else float("nan")),
        "rev_period_p95_ms": _pct(periods, 0.95) * 1000 if periods else float("nan"),
        "pts_per_rev": st.mean(per_rev) if per_rev else float("nan"),
        "ang_step_deg": st.median(steps) if steps else float("nan"),
        "max_dist_mm": max(p.dist for p in pts),
    }


# ── precision (độ lặp lại, KHÔNG cần biết giá trị thật) ──────────────────────
def calc_precision(pts: List[Pt], bands=((0, 1000), (1000, 2000), (2000, 3000), (3000, 4500))) -> Dict:
    """Cảnh tĩnh: với mỗi ô 1° lấy khoảng cách TB mỗi vòng, độ lệch chuẩn qua các vòng = độ lặp lại (mm).
    Bỏ ô có biên độ > 150 mm (mép vật, vật di chuyển) và ô có mặt < 50% số vòng."""
    revs = sorted({p.rev for p in pts})[1:-1]
    if len(revs) < 8:
        return {}
    rs = set(revs)
    cells: Dict[int, Dict[int, List[float]]] = {}
    for p in pts:
        if p.rev in rs:
            cells.setdefault(int(p.angle) % 360, {}).setdefault(p.rev, []).append(p.dist)
    out: Dict = {}
    per_band: Dict[tuple, List[float]] = {b: [] for b in bands}
    present, expected = 0, 0
    for a, byrev in cells.items():
        if len(byrev) < 0.5 * len(revs):
            continue
        vals = [st.mean(v) for v in byrev.values()]
        present += len(vals)
        expected += len(revs)
        if max(vals) - min(vals) > 150.0:
            continue
        m = st.mean(vals)
        for b in bands:
            if b[0] <= m < b[1]:
                per_band[b].append(st.pstdev(vals))
    for b, v in per_band.items():
        key = f"precision_{b[0] // 1000}-{b[1] / 1000:g}m"
        out[key + "_median_mm"] = st.median(v) if v else float("nan")
        out[key + "_p90_mm"] = _pct(v, 0.9) if v else float("nan")
        out[key + "_bins"] = len(v)
    out["stable_return_rate_pct"] = 100.0 * present / expected if expected else float("nan")
    return out


# ── static (độ chính xác với trụ đứng yên) ───────────────────────────────────
def _angular_groups(pts: List[Pt], gap_deg: float):
    """Gom điểm trong dải khoảng cách theo góc liền kề (có xử lý vòng 360°)."""
    s = sorted(pts, key=lambda p: p.angle)
    groups, cur = [], []
    for p in s:
        if cur and p.angle - cur[-1].angle > gap_deg:
            groups.append(cur)
            cur = []
        cur.append(p)
    if cur:
        groups.append(cur)
    if len(groups) > 1 and groups[0][0].angle + 360 - groups[-1][-1].angle <= gap_deg:
        groups[0] = groups.pop() + groups[0]
    return groups


def calc_static(pts: List[Pt], truth_dist: float, post_r: float = 25.0, band: float = 150.0,
                angle: Optional[float] = None, angle_tol: float = 10.0, min_pts: int = 3) -> Dict:
    near = truth_dist - post_r  # cảm biến thấy mặt trụ gần nhất
    sel = [p for p in pts if abs(p.dist - near) <= band]
    if angle is not None:
        sel = [p for p in sel if abs(((p.angle - angle) + 180) % 360 - 180) <= angle_tol]
    if not sel:
        return {"error": f"không có điểm nào trong dải {near - band:.0f}…{near + band:.0f} mm"}
    n_revs_total = len({p.rev for p in pts})
    groups = _angular_groups(sel, gap_deg=2.0)

    def width_of(g):
        return (g[-1].angle - g[0].angle) % 360
    # trụ nhỏ ⇒ nhóm hẹp (≤ 20°); tường/vật lớn cùng tầm bị loại. Trong số nhóm hẹp lấy nhóm có mặt ở nhiều vòng nhất.
    narrow = [g for g in groups if width_of(g) <= 20.0] or groups
    g = max(narrow, key=lambda g: (len({p.rev for p in g}), -width_of(g)))
    width = (g[-1].angle - g[0].angle) % 360
    ang_c = ((sum(((p.angle + 180) % 360) - 180 for p in g) / len(g)))  # tâm góc (không tính khi wrap 0°)
    ang_c = st.median([p.angle for p in g]) if width < 180 else ang_c
    per_rev: Dict[int, List[Pt]] = {}
    for p in g:
        per_rev.setdefault(p.rev, []).append(p)
    rev_keys = sorted(per_rev)
    all_revs = sorted({p.rev for p in pts})[1:-1]
    hits3 = sum(1 for k in all_revs if len(per_rev.get(k, [])) >= min_pts)
    hits1 = sum(1 for k in all_revs if len(per_rev.get(k, [])) >= 1)
    cent_x, cent_y, rngs = [], [], []
    for k in rev_keys:
        c = per_rev[k]
        xs = [-p.dist * math.sin(math.radians(p.angle)) for p in c]
        ys = [p.dist * math.cos(math.radians(p.angle)) for p in c]
        cent_x.append(st.mean(xs)); cent_y.append(st.mean(ys))
        rngs += [p.dist for p in c]
    jitter = math.sqrt(st.pvariance(cent_x) + st.pvariance(cent_y)) if len(cent_x) > 1 else float("nan")
    return {
        "target_angle_deg": ang_c,
        "target_width_deg": width,
        "n_groups_in_band": len(groups),
        "revs_total": len(all_revs),
        "pts_per_rev_on_target": st.mean([len(per_rev.get(k, [])) for k in all_revs]) if all_revs else float("nan"),
        "detect_rate_ge1_pct": 100.0 * hits1 / len(all_revs) if all_revs else float("nan"),
        "detect_rate_ge%d_pct" % min_pts: 100.0 * hits3 / len(all_revs) if all_revs else float("nan"),
        "range_mean_mm": st.mean(rngs),
        "range_bias_mm": st.mean(rngs) - near,
        "range_noise_std_mm": st.pstdev(rngs) if len(rngs) > 1 else float("nan"),
        "centroid_jitter_mm": jitter,
    }


# ── latency ──────────────────────────────────────────────────────────────────
def calc_latency(pts: List[Pt], cfg: Cfg) -> Dict:
    touches, pl = run_offline(pts, cfg)
    sent = [t for t in touches if t.sent]
    eps = episodes(touches)
    first_lat = [e[0].latency * 1000 for e in eps]
    all_lat = [t.latency * 1000 for t in sent]
    dur = (pts[-1].t - pts[0].t) if pts else 0
    return {
        "points_in_area_pct": 100.0 * pl.n_in_area / max(1, pl.n_total),
        "touches_sent": len(sent),
        "touches_throttled": len(touches) - len(sent),
        "episodes": len(eps),
        "first_report_latency_median_ms": st.median(first_lat) if first_lat else float("nan"),
        "first_report_latency_p95_ms": _pct(first_lat, 0.95),
        "first_report_latency_max_ms": max(first_lat) if first_lat else float("nan"),
        "any_report_latency_median_ms": st.median(all_lat) if all_lat else float("nan"),
        "report_rate_hz": len(sent) / dur if dur > 0 else float("nan"),
    }


# ── CLI ──────────────────────────────────────────────────────────────────────
def _cfg_from(a) -> Cfg:
    cfg = Cfg.from_json(a.config) if a.config else Cfg()
    if a.window is not None: cfg.window_s = a.window
    if a.tick is not None: cfg.tick_s = a.tick
    if a.nums is not None: cfg.nums_point_report = a.nums
    if a.min_cluster is not None: cfg.min_cluster = a.min_cluster
    if a.cluster_mm is not None: cfg.cluster_mm = a.cluster_mm
    if a.offset is not None: cfg.offset_angle = a.offset
    return cfg


def _print_dict(title: str, d: Dict):
    print(f"\n== {title}")
    for k, v in d.items():
        print(f"  {k:34s} {_fmt(v, 2)}")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def pipe_opts(p):
        p.add_argument("--config", help="lidar_config.json (mặc định: máy chiếu #2)")
        p.add_argument("--window", type=float, help="cửa sổ tích luỹ điểm, giây (mặc định 0.3)")
        p.add_argument("--tick", type=float, help="chu kỳ phân tích cụm, giây (mặc định 0.1)")
        p.add_argument("--nums", type=int, help="nums_point_report")
        p.add_argument("--min-cluster", type=int, help="số điểm tối thiểu/cụm (mặc định 3)")
        p.add_argument("--cluster-mm", type=float, help="ngưỡng gom cụm mm (mặc định 30)")
        p.add_argument("--offset", type=float, help="ghi đè offset_angle")

    def static_opts(p):
        p.add_argument("--truth-dist", type=float, help="khoảng cách thật cảm biến → TÂM trụ (mm)")
        p.add_argument("--post-radius", type=float, default=25.0)
        p.add_argument("--angle", type=float, help="góc thô của trụ (độ), nếu có nhiều vật cùng tầm")
        p.add_argument("--angle-tol", type=float, default=10.0)

    s = sub.add_parser("stats"); s.add_argument("files", nargs="+")
    s = sub.add_parser("static"); s.add_argument("file"); static_opts(s)
    s = sub.add_parser("latency"); s.add_argument("file"); pipe_opts(s)
    s = sub.add_parser("compare"); s.add_argument("files", nargs="+"); static_opts(s); pipe_opts(s)
    a = ap.parse_args(argv)

    if a.cmd == "stats":
        for f in a.files:
            meta, pts = load_csv(f)
            _print_dict(f"{f} ({meta.get('model', '?')})", {**calc_stats(pts), **calc_precision(pts)})
    elif a.cmd == "static":
        if a.truth_dist is None:
            sys.exit("--truth-dist bắt buộc")
        meta, pts = load_csv(a.file)
        _print_dict(f"{a.file} ({meta.get('model', '?')}) — static", calc_static(pts, a.truth_dist, a.post_radius, angle=a.angle, angle_tol=a.angle_tol))
    elif a.cmd == "latency":
        meta, pts = load_csv(a.file)
        _print_dict(f"{a.file} ({meta.get('model', '?')}) — latency", calc_latency(pts, _cfg_from(a)))
    elif a.cmd == "compare":
        cols, names = [], []
        for f in a.files:
            meta, pts = load_csv(f)
            row = {}
            row.update({"[stats] " + k: v for k, v in calc_stats(pts).items()})
            row.update({"[precision] " + k: v for k, v in calc_precision(pts).items()})
            if a.truth_dist is not None:
                row.update({"[static] " + k: v for k, v in calc_static(pts, a.truth_dist, a.post_radius, angle=a.angle, angle_tol=a.angle_tol).items()})
            row.update({"[latency] " + k: v for k, v in calc_latency(pts, _cfg_from(a)).items()})
            cols.append(row)
            names.append(f"{meta.get('model', '?')}:{f.replace(chr(92), '/').split('/')[-1]}")
        keys = []
        for r in cols:
            for k in r:
                if k not in keys:
                    keys.append(k)
        w = max(len(k) for k in keys) + 2
        print("".ljust(w) + "".join(n[:28].ljust(30) for n in names))
        for k in keys:
            print(k.ljust(w) + "".join(_fmt(r.get(k), 2).ljust(30) for r in cols))


if __name__ == "__main__":
    main()
