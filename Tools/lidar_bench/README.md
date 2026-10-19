# lidar_bench — kết nối, vẽ đồ thị và so sánh LiDAR C1 / D200 trên laptop

Python 3.11, `pip install -r requirements.txt` (pyserial + matplotlib). Giải thích thuật toán chạm: `docs/lidar-touch-algorithm.md`.

> **Cổng COM chỉ 1 app giữ được.** Đóng Slamtec RoboStudio / MyNativeApp / terminal serial trước khi chạy, nếu không báo `Access is denied`.

| File | Việc |
|---|---|
| `lidar_view.py` | Viewer thời gian thực + chạy pipeline chạm K02 (cụm, toe point, màn hình 1024×600), ghi CSV |
| `lidar_capture.py` | Ghi CSV không cần cửa sổ |
| `lidar_analyze.py` | `stats` · `static` · `latency` · `compare` trên file CSV |
| `sim_sweep.py` | Mô phỏng trễ/hụt theo khoảng cách, C1 vs D200 |
| `touch_pipeline.py` | Port Python của pipeline native (đối chiếu `liblidar.cpp`/`native-lib.cpp`) |
| `lidar_io.py` | Driver C1 (Standard + DenseBoost) và D200 (LD14P), mô phỏng, CSV |

## Dùng nhanh
```
python lidar_view.py --model c1 --port COM6 --probe          # thông tin C1: model, FW, các chế độ quét
python lidar_view.py --model c1 --port COM6 --range 4000     # đồ thị live (r = ghi CSV, p = dừng, +/- = zoom)
python lidar_view.py --model d200 --port COM7
python lidar_view.py --model sim-c1                          # không cần phần cứng
python lidar_view.py --replay captures/x.csv --speed 2
python lidar_view.py --config lidar_config.json ...          # vẽ đúng vùng sàn của máy chiếu (adb pull từ K02)
```
Đồ thị dùng đúng hệ toạ độ native (x ngang, y sâu, cảm biến ở gốc, đã cộng `offset_angle`). Khung xanh lá = vùng sàn.

## Quy trình đo so sánh C1 vs D200 (cùng 1 phòng, cùng 1 vị trí lắp)
Mỗi cảm biến lắp ĐÚNG chỗ như nhau (đo lần lượt, không lắp cùng lúc — tia sẽ nhiễu nhau).

1. **Tốc độ/mật độ/độ lặp lại** (cảnh tĩnh, không ai đi lại): `lidar_capture.py --model X --port COMn --seconds 30 --out captures/X_scene.csv` rồi `lidar_analyze.py stats captures/X_scene.csv`.
2. **Độ chính xác khoảng cách**: dựng 1 trụ Ø5 cm đứng yên, đo bằng thước/laser từ **tâm cảm biến tới tâm trụ** (mm), ghi 30 s ở 1, 2, 3 m:
   `lidar_capture.py … --out captures/X_2000.csv` → `lidar_analyze.py static captures/X_2000.csv --truth-dist 2000 --post-radius 25`
   Ra: sai số khoảng cách TB (bias), nhiễu, rung tâm cụm (mm), số điểm trên trụ/vòng, % vòng đạt ≥ 3 điểm (đúng tiêu chí nhận cụm).
3. **Độ trễ chạm**: ghi 30 s trong lúc 1 người bước vào – đứng 1 s – bước ra vùng sàn ~10 lần (dùng `--config lidar_config.json` của máy thật): `lidar_analyze.py latency captures/X_step.csv --config lidar_config.json`. Trễ = điểm đầu tiên của cụm → báo chạm đầu tiên mỗi đợt. Đây là trễ **của pipeline**; trễ chân→màn hình còn cộng ~(nửa vòng quay + frame Unity), không đo được bằng file ghi.
4. **Bảng so sánh**: `lidar_analyze.py compare captures/c1_2000.csv captures/d200_2000.csv --truth-dist 2000 --config lidar_config.json`.

Thử tham số không cần build native: thêm `--window 0.2 --tick 0.05 --nums 3 --min-cluster 2 --cluster-mm 40` vào `latency`/`compare`.

## Đã kiểm chứng gì
- C1: chạy thật trên COM6 (FW 1.02), cả Standard và DenseBoost; viewer live OK.
- D200: driver + pipeline mới chỉ chạy với **mô phỏng**, chưa có phần cứng. Lần đầu cắm D200, xem ô `gói ok/lỗi` trên viewer: số gói lỗi phải ≈ 0 (nếu lỗi nhiều → sai baud/cổng).
- `touch_pipeline.py` khớp native theo đọc code; chưa so từng sự kiện với logcat K02.
