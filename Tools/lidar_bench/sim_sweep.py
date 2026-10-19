"""Quét mô phỏng: độ trễ + tỉ lệ bắt hụt theo khoảng cách, C1 vs D200, cùng 1 pipeline chạm của K02.

Chỉ khác nhau ở tốc độ lấy mẫu/quay (C1 5000 điểm/s @10 Hz = 0,72°; D200 4000 điểm/s @6 Hz = 0,54°). Nhiễu khoảng cách
đặt NHƯ NHAU cho cả 2 (mặc định σ=5 mm) để so sánh thuần theo mật độ điểm — KHÔNG phải số đo thật của D200.
Có "chân lý": thời điểm vật xuất hiện biết trước nên đo được độ trễ từ lúc xuất hiện → báo chạm
(file ghi thật không có mốc này).

  python sim_sweep.py                 # in bảng Markdown
  python sim_sweep.py --trials 40 --sigma 5 --md out.md
"""
from __future__ import annotations

import argparse
import random
import statistics as st

import lidar_io
from lidar_io import SIM_SPEC, SimWorld, sim_points
from touch_pipeline import Cfg, run_offline

ROI = Cfg(half_x=1500, hight_floor=3800, ymax=-100, shift_x_floor=0, shift_y=0, shift_x=0, offset_angle=0, nums_point_report=2)


def trial(model: str, dist: float, radius: float, rng: random.Random, cfg: Cfg):
    ton = 2.0 + rng.random() * 0.1  # pha ngẫu nhiên so với vòng quay/tick
    world = SimWorld([(0.0, -dist, radius, ton, ton + 1.5, 1e6)], walls=(-2500, 2500, -4500, 800))
    pts = sim_points(model, 0.0, ton + 1.5, world, rng)
    touches, _ = run_offline(pts, cfg)
    for t in touches:
        if t.sent and t.t_tick >= ton:
            return (t.t_tick - ton) * 1000.0
    return None


def pct(v, p):
    v = sorted(v)
    return v[min(len(v) - 1, int(p * len(v)))]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--trials", type=int, default=30)
    ap.add_argument("--sigma", type=float, default=5.0)
    ap.add_argument("--radius", type=float, nargs="+", default=[25.0, 50.0], help="bán kính vật (mm): 25 = trụ Ø5, 50 = bàn chân ~Ø10")
    ap.add_argument("--dist", type=float, nargs="+", default=[500, 1000, 1500, 2000, 2500, 3000, 3500])
    ap.add_argument("--window", type=float)
    ap.add_argument("--tick", type=float)
    ap.add_argument("--md")
    a = ap.parse_args()
    cfg = ROI
    if a.window: cfg.window_s = a.window
    if a.tick: cfg.tick_s = a.tick
    for m in SIM_SPEC.values():
        m["sigma"] = a.sigma
        m["drop"] = 0.02

    lines = [f"Mô phỏng cùng nhiễu σ={a.sigma:g} mm, rớt điểm 2%, window={cfg.window_s * 1000:.0f} ms, tick={cfg.tick_s * 1000:.0f} ms, "
             f"min_cluster={cfg.min_cluster}, {a.trials} lần/ô.", ""]
    for r in a.radius:
        lines += [f"**Vật Ø{2 * r:g} mm**", "",
                  "| Khoảng cách | Điểm/vòng C1 | Điểm/vòng D200 | Trễ TB C1 (p95) ms | Trễ TB D200 (p95) ms | Hụt C1 | Hụt D200 |",
                  "|---|---|---|---|---|---|---|"]
        for d in a.dist:
            row = {}
            for m in ("c1", "d200"):
                rng = random.Random(7)
                lat, miss = [], 0
                for _ in range(a.trials):
                    v = trial(m, d, r, rng, cfg)
                    if v is None:
                        miss += 1
                    else:
                        lat.append(v)
                row[m] = (lat, miss)
            ppr = {m: (2 * r / d) * (180 / 3.14159265) / {"c1": 0.72, "d200": 0.54}[m] for m in row}  # xấp xỉ hình học

            def f(m):
                lat, miss = row[m]
                return f"{st.mean(lat):.0f} ({pct(lat, 0.95):.0f})" if lat else "—"
            lines.append(f"| {d / 1000:g} m | {ppr['c1']:.1f} | {ppr['d200']:.1f} | {f('c1')} | {f('d200')} | "
                         f"{100 * row['c1'][1] / a.trials:.0f}% | {100 * row['d200'][1] / a.trials:.0f}% |")
        lines.append("")
    out = "\n".join(lines)
    print(out)
    if a.md:
        open(a.md, "w", encoding="utf-8").write(out + "\n")


if __name__ == "__main__":
    main()
