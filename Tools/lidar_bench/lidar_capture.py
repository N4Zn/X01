"""Ghi CSV không cần cửa sổ đồ thị (chạy được qua SSH/CI, hoặc sinh dữ liệu mô phỏng offline).

  python lidar_capture.py --model c1 --port COM6 --seconds 30 --out captures/c1_1500mm.csv
  python lidar_capture.py --model d200 --port COM7 --seconds 30 --out captures/d200_1500mm.csv
  python lidar_capture.py --model sim-c1 --seconds 20 --out captures/sim_c1.csv     # ảo, không chờ
"""
from __future__ import annotations

import argparse
import os
import random
import sys
import time

from lidar_io import Recorder, SimWorld, make_source, sim_points


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", default="c1", choices=["c1", "d200", "sim-c1", "sim-d200"])
    ap.add_argument("--port")
    ap.add_argument("--baud", type=int)
    ap.add_argument("--no-flip-x", action="store_true", help="C1: KHÔNG đảo trục X (mặc định đảo cho giống D200)")
    ap.add_argument("--no-flip-y", action="store_true", help="C1: KHÔNG đảo trục Y (mặc định đảo cho giống D200)")
    ap.add_argument("--mode", default="auto", choices=["auto", "standard", "dense"])
    ap.add_argument("--seconds", type=float, default=20.0)
    ap.add_argument("--out", required=True)
    ap.add_argument("--sim-post", help="mô phỏng: trụ đứng yên 'x,y,r' (mm) thay cho trụ bật/tắt mặc định")
    a = ap.parse_args()
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    m = a.model.replace("sim-", "")

    if a.model.startswith("sim-"):  # offline: sinh thẳng theo thời gian ảo
        rec = Recorder(a.out, {"model": m, "sim": 1})
        targets = None
        if a.sim_post:
            x, y, r = (float(v) for v in a.sim_post.split(","))
            targets = [(x, y, r, 0.0, 0.0, 0.0)]
        rng, world = random.Random(1), SimWorld(targets)
        t = 0.0
        while t < a.seconds:
            rec.write(sim_points(m, t, t + 1.0, world, rng))
            t += 1.0
        rec.close(); print(f"{a.out}: {rec.n} điểm (mô phỏng)")
        return

    src = make_source(a.model, a.port, a.baud, a.mode, flip_y=not a.no_flip_y, flip_x=not a.no_flip_x).start()
    rec = Recorder(a.out, {"model": m, "port": a.port, "baud": a.baud or getattr(src, "baud", "")})
    t0 = time.perf_counter()
    try:
        while time.perf_counter() - t0 < a.seconds:
            time.sleep(0.05)
            if src.error:
                sys.exit("LỖI: " + src.error)
            rec.write(src.drain())
    finally:
        src.stop()
        rec.write(src.drain())
        rec.close()
    print(f"{a.out}: {rec.n} điểm, quét={getattr(src, 'scan_mode', '-')}, gói ok/lỗi={src.stats['frames']}/{src.stats['bad_frames']}, node dist=0: {src.stats.get('zero', 0)}, byte={src.stats['bytes']}")


if __name__ == "__main__":
    main()
