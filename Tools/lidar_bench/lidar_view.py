"""Viewer thời gian thực cho C1 / D200 trên laptop + chạy thử pipeline chạm của K02.

  python lidar_view.py --model c1 --port COM6                 # C1
  python lidar_view.py --model d200 --port COM7               # D200
  python lidar_view.py --model sim-c1                         # mô phỏng, không cần phần cứng
  python lidar_view.py --replay captures/x.csv --speed 2      # phát lại file ghi
  python lidar_view.py --model c1 --port COM6 --probe         # chỉ hỏi thông tin thiết bị C1

Bên trái: cảnh nhìn từ trên xuống theo hệ toạ độ native (x ngang, y sâu; cảm biến ở gốc 0,0).
  xám = điểm ngoài vùng sàn, xanh = điểm trong vùng (được pipeline dùng), vàng = cụm ≥ min_cluster,
  đỏ ✕ = toe point (Y lớn nhất) → báo chạm. Khung xanh lá = vùng sàn theo lidar_config.json.
Bên phải: màn hình tham chiếu 1024x600 mà native gửi cho Unity (chấm chạm mờ dần sau 1.5 s).
Phím: r = ghi/dừng ghi CSV | p = tạm dừng | c = xoá lịch sử | +/- = thu phóng | q = thoát
"""
from __future__ import annotations

import argparse
import collections
import os
import time

import matplotlib
matplotlib.use("TkAgg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.animation import FuncAnimation  # noqa: E402
from matplotlib.patches import Rectangle  # noqa: E402

from lidar_io import C1Source, Recorder, make_source  # noqa: E402
from touch_pipeline import REF_H, REF_W, Cfg, TouchPipeline, to_xy  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", default="c1", choices=["c1", "d200", "sim-c1", "sim-d200"])
    ap.add_argument("--port")
    ap.add_argument("--baud", type=int)
    ap.add_argument("--mode", default="auto", choices=["auto", "standard", "dense"], help="chế độ quét C1")
    ap.add_argument("--config", help="lidar_config.json (mặc định: máy chiếu #2)")
    ap.add_argument("--replay")
    ap.add_argument("--speed", type=float, default=1.0)
    ap.add_argument("--range", type=float, default=3000.0, help="bán kính hiển thị (mm)")
    ap.add_argument("--record", help="ghi CSV ngay từ đầu (đường dẫn)")
    ap.add_argument("--probe", action="store_true", help="C1: in thông tin thiết bị rồi thoát")
    ap.add_argument("--screenshot", help="lưu PNG sau --shot-after giây rồi thoát")
    ap.add_argument("--shot-after", type=float, default=4.0)
    a = ap.parse_args()

    cfg = Cfg.from_json(a.config) if a.config else Cfg()

    if a.probe:
        import json
        print(json.dumps(C1Source(a.port, a.baud).probe(), indent=2, ensure_ascii=False))
        return

    src = make_source(a.model, a.port, a.baud, a.mode, a.replay, a.speed).start()
    pipe = TouchPipeline(cfg)
    rec = None
    paused = False
    rng = [a.range]

    scene_pts = collections.deque()        # (x, y, a, t, in_area) ~ 1 vòng gần nhất
    touches = collections.deque(maxlen=64)  # (refx, refy, t, sent)
    rev_times = collections.deque(maxlen=12)
    seen_rev = [-1]
    lat_hist = collections.deque(maxlen=20)
    rate = collections.deque(maxlen=50)    # (t, n) để tính điểm/s

    fig, (ax, ax2) = plt.subplots(1, 2, figsize=(13, 6.5), gridspec_kw={"width_ratios": [1.15, 1]})
    fig.canvas.manager.set_window_title(f"LiDAR bench — {src.model}")
    ax.set_aspect("equal"); ax.grid(alpha=.25)
    ax.set_xlabel("x (mm)"); ax.set_ylabel("y (mm)")
    ax.plot(0, 0, "k^", ms=9)
    ax.add_patch(Rectangle((cfg.xmin, cfg.ymin), cfg.xmax - cfg.xmin, cfg.ymax - cfg.ymin, fill=False, ec="green", lw=1.5))
    sc_out = ax.scatter([], [], s=4, c="#9aa", alpha=.6)
    sc_in = ax.scatter([], [], s=9, c="#1f77ff")
    sc_cl = ax.scatter([], [], s=16, c="#ffb000", zorder=3)
    sc_toe = ax.scatter([], [], s=90, c="red", marker="x", lw=2, zorder=4)
    ax2.set_xlim(0, REF_W); ax2.set_ylim(REF_H, 0); ax2.set_aspect("equal"); ax2.grid(alpha=.25)
    ax2.set_title("Màn hình tham chiếu 1024×600 (gửi cho Unity)")
    sc_tp = ax2.scatter([], [], s=140, c="red", alpha=.7)
    txt = ax.text(0.01, 0.99, "", transform=ax.transAxes, va="top", family="monospace", fontsize=8,
                  bbox=dict(fc="white", ec="#ccc", alpha=.85))

    def set_range():
        r = rng[0]
        ax.set_xlim(-r, r); ax.set_ylim(-r * 1.2, r * 0.4)
    set_range()

    def start_rec():
        nonlocal rec
        os.makedirs(os.path.join(HERE, "captures"), exist_ok=True)
        path = a.record if a.record else os.path.join(HERE, "captures", time.strftime(f"{a.model}_%Y%m%d_%H%M%S.csv"))
        a.record = None
        rec = Recorder(path, {"model": a.model.replace("sim-", ""), "port": a.port, "baud": a.baud or getattr(src, "baud", ""),
                              "scan_mode": getattr(src, "scan_mode", "")})
        print("REC →", path)

    def stop_rec():
        nonlocal rec
        if rec:
            rec.close(); print(f"REC xong ({rec.n} điểm)"); rec = None

    if a.record:
        start_rec()

    def on_key(e):
        nonlocal paused
        if e.key == "r":
            stop_rec() if rec else start_rec()
        elif e.key == "p":
            paused = not paused
        elif e.key == "c":
            scene_pts.clear(); touches.clear()
        elif e.key in ("+", "="):
            rng[0] = max(500, rng[0] / 1.3); set_range()
        elif e.key == "-":
            rng[0] = min(12000, rng[0] * 1.3); set_range()
        elif e.key == "q":
            plt.close(fig)
    fig.canvas.mpl_connect("key_press_event", on_key)

    def update(_):
        if src.error:
            txt.set_text("LỖI: " + src.error)
            return sc_out, sc_in, sc_cl, sc_toe, sc_tp, txt
        pts = src.drain()
        now = time.perf_counter()
        if rec:
            rec.write(pts)
        if not paused:
            for p in pts:
                for t in pipe.advance(p.t):
                    touches.append((t.ref[0], t.ref[1], p.t, t.sent))
                    if t.sent:
                        lat_hist.append(t.latency * 1000)
                ia = pipe.feed(p)
                x, y, ang = to_xy(p.angle, p.dist, cfg.offset_angle)
                scene_pts.append((x, y, p.t, ia))
                if p.rev != seen_rev[0]:
                    seen_rev[0] = p.rev; rev_times.append(p.t)
            for t in pipe.advance(now):
                touches.append((t.ref[0], t.ref[1], now, t.sent))
                if t.sent:
                    lat_hist.append(t.latency * 1000)
        if pts:
            rate.append((now, len(pts)))
        while scene_pts and now - scene_pts[0][2] > 0.35:
            scene_pts.popleft()
        while touches and now - touches[0][2] > 1.5:
            touches.popleft()

        out = [(p[0], p[1]) for p in scene_pts if not p[3]]
        inn = [(p[0], p[1]) for p in scene_pts if p[3]]
        sc_out.set_offsets(out or [[1e9, 1e9]])
        sc_in.set_offsets(inn or [[1e9, 1e9]])
        cl = [(q[0], q[1]) for c in pipe.last_clusters if len(c) >= cfg.min_cluster for q in c]
        sc_cl.set_offsets(cl or [[1e9, 1e9]])
        sc_toe.set_offsets([(t[0], t[1]) for t in pipe.last_toes] or [[1e9, 1e9]])
        sc_tp.set_offsets([(t[0], t[1]) for t in touches if t[3]] or [[-1e9, -1e9]])
        sc_tp.set_alpha(0.7)

        hz = ""
        if len(rev_times) >= 3:
            per = (rev_times[-1] - rev_times[0]) / (len(rev_times) - 1)
            hz = f"{1 / per:5.2f} Hz" if per > 0 else ""
        pps = ""
        if len(rate) >= 2 and rate[-1][0] > rate[0][0]:
            pps = f"{sum(n for _, n in rate) / (rate[-1][0] - rate[0][0]):6.0f}"
        lat = f"{sum(lat_hist) / len(lat_hist):5.0f} ms" if lat_hist else "—"
        s = src.stats
        txt.set_text(
            f"{src.model}  {'(PAUSED) ' if paused else ''}{'● REC ' if rec else ''}\n"
            f"quay: {hz}   điểm/s: {pps}\n"
            f"gói ok/lỗi: {s['frames']}/{s['bad_frames']}  điểm: {s['points']}\n"
            f"trong vùng sàn: {len(inn)}   cụm≥{cfg.min_cluster}: {len([c for c in pipe.last_clusters if len(c) >= cfg.min_cluster])}\n"
            f"trễ điểm đầu→báo (TB 20 gần nhất): {lat}\n"
            f"nums={cfg.nums_point_report} window={cfg.window_s * 1000:.0f}ms tick={cfg.tick_s * 1000:.0f}ms")
        return sc_out, sc_in, sc_cl, sc_toe, sc_tp, txt

    ani = FuncAnimation(fig, update, interval=40, blit=False, cache_frame_data=False)  # noqa: F841
    if a.screenshot:  # chụp PNG sau N giây rồi thoát (kiểm thử / đính kèm báo cáo)
        def shot():
            fig.savefig(a.screenshot, dpi=110)
            print("saved", a.screenshot)
            plt.close(fig)
        tm = fig.canvas.new_timer(interval=int(a.shot_after * 1000))
        tm.single_shot = True
        tm.add_callback(shot)
        tm.start()
    try:
        plt.show()
    finally:
        stop_rec()
        src.stop()


if __name__ == "__main__":
    main()
