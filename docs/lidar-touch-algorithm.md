# Thuật toán nhận diện chạm LiDAR (D200), độ trễ, và option C1

> **TL;DR**: Điểm chạm = "cụm ≥ 3 điểm trong cửa sổ 300 ms, phân tích mỗi 100 ms, lấy điểm Y lớn nhất". KHÔNG có đếm "n vòng quay" — chỉ có cửa sổ thời gian 300 ms. Độ trễ điển hình ~100 ms, tệ nhất ~300–400 ms (vật nhỏ/xa). Có thể đổi sang RPLIDAR C1 bằng `"lidar_model": "c1"` trong `lidar_config.json`.
> **Đọc khi**: chỉnh độ nhạy/độ trễ chạm, so sánh cảm biến, thêm loại LiDAR mới, nghi ngờ chạm "ma"/chạm trễ.
> **Cập nhật**: 2026-10-19 (đối chiếu code `NativePlugins/LidarUnity/src/` — nếu native đổi, cập nhật cả `Tools/lidar_bench/touch_pipeline.py`)

Code liên quan: `NativePlugins/LidarUnity/src/{native-lib.cpp,liblidar.cpp}`, `Assets/Game/Scripts/UIScripts/Common/LidarTouchBridge.cs`, `NativePlugins/LidarNativeAndroidLib/.../LidarUsbBridge.java`. Hiệu chỉnh vùng sàn/máy chiếu: `docs/lidar-calib.md`. Tool đo trên laptop: `Tools/lidar_bench/README.md`.

## 1. Đường đi của 1 điểm chạm (D200, đang chạy trên K02)

```
D200 ─UART 230400─▶ CP2102 ─USB bulk (Java, buffer 4096)─▶ pipe ─▶ [T1 read] ─RingBuffer─▶ [T2 parse] ─▶ [T3 lọc vùng] ─▶ g_points_in_area_touch
                                                                                                                       │ (mỗi ≥100 ms)
Unity Update() ◀─Lidar_PollTouch (queue 32)◀─ sendTouch (throttle) ◀─ convert_to_1024x600 ◀─ toe point ◀─ [T4 gom cụm] ◀┘
   └─▶ ApplyCalib (affine) ─▶ RaycastAll ─▶ pointerDown/Up/Click
```

| # | Bước | Code | Chi tiết |
|---|---|---|---|
| 1 | Giải gói | `lidar_AnalysisOne` / `lidar_frame_Parse` | Gói 47 byte `54 2C`, CRC8, 12 điểm/gói. Bỏ gói lệch (`diff > speed·12/4000·1.5`). Góc nội suy `start + i·(end-start)/11`, cộng `offset_angle`. `x = -d·sin a`, `y = d·cos a` |
| 2 | Lọc vùng | `point_is_in_area` | Chỉ giữ góc **90°–270°** (nửa mặt phẳng phía sau), `0 < d ≤ 4000 mm`, và `x ∈ [-half_x+shift_x_floor, half_x+shift_x_floor]`, `y ∈ [ymax-hight_floor, ymax]` |
| 3 | Đệm điểm | `ld14p_parse_xypoint2touch_fnc` | Điểm đạt vùng được đẩy vào `g_points_in_area_touch` kèm thời gian đến. **Không** khử nền, **không** lọc theo độ tin cậy/intensity |
| 4 | Tick | `ld14p_consumer_thread_function` | Cứ ≥ **100 ms** (`CLUSTER_ANALYSIS_INTERVAL`) chạy 1 lượt phân tích. Sao chép đệm **trước**, rồi mới xoá điểm cũ hơn **300 ms** (`MAX_POINT_LIFE_SEC`) |
| 5 | Cổng tổng số điểm | cùng hàm | Chỉ phân tích khi `số điểm trong đệm > nums_point_report` (máy chiếu #2: 2 → cần ≥ 3 điểm) |
| 6 | Gom cụm | `cluster_points` | Gom tham lam: điểm vào cụm đầu tiên có 1 điểm cách ≤ **30 mm** (`DISTANCE_THRESHOLD`); không gộp cụm về sau |
| 7 | Chọn cụm | `find_toe_points(…, 3)` | Cụm phải có **≥ 3 điểm** (hardcode). Mỗi cụm đạt → 1 "toe point" = điểm có **Y lớn nhất** trong cụm |
| 8 | Đổi toạ độ | `convert_to_1024x600` | mm → ref 1024×600 (Unity đổi sang pixel thật). Lưu ý: `shift_x_floor` bị cộng 2 lần (trong `G_XMIN` và trong biểu thức) — đã có từ bản gốc, đừng "sửa" kẻo lệch calib đang chạy |
| 9 | Throttle | `sendTouch` | Cùng vị trí (≤ 100 px ref) không báo lại trước **150 ms**. Vị trí khác nhau không chặn nhau |
| 10 | Unity | `Update()` | Rút queue mỗi frame → `ApplyCalib` → bắn `PointerEventData` (down+up+click liền nhau, chỉ tap) |

### Trả lời câu hỏi "qua mấy vòng mới nhận, cụm phải đạt gì"

- **Không có đếm số vòng quay.** "Nhiều vòng" chỉ xuất hiện gián tiếp: cửa sổ tích điểm 300 ms ≈ 3 vòng ở 10 Hz, nên 1 vật nhỏ/xa chỉ trúng 1 điểm/vòng vẫn gom đủ 3 điểm sau ~3 vòng.
- Tiêu chí nhận cụm = **(a)** tổng điểm trong vùng > `nums_point_report` **và (b)** cụm ≥ 3 điểm, các điểm cách nhau chuỗi ≤ 30 mm. Với `nums_point_report = 2`, (a) thừa vì (b) đã kéo theo (a). Chỉ có tác dụng khi đặt ≥ 3.
- Bộ lọc "ổn định phải xuất hiện trong cả 5 lần quét" (`SCAN_BUFFER_SIZE`) và `find_position`/`POINT_NUMBER_OK = 10` **là code chết** (`LidarProcessor.cpp`, `liblidar.cpp:find_position`): không được gọi trong pipeline thật (`lidar_processing_thread` không bao giờ được tạo; `find_position` đã comment). Nhiều khả năng ký ức "qua một số vòng" đến từ đây.

### Hệ quả cần biết

1. **Chạm "ma" sau khi rời**: đệm giữ điểm đến ~300 ms (+ tới 100 ms do sao chép trước khi xoá) → sau khi chân rời vẫn còn tới ~3–4 tick báo lại vị trí cũ (bị throttle còn ~1 báo/200 ms).
2. **Giữ nguyên vị trí vẫn báo liên tục** ~5 lần/giây (throttle 150 ms + tick 100 ms ⇒ cách tick thứ 2). Game không nhận "thả" (không có touch-up thật).
3. **Vật tĩnh trong vùng (tường, ghế) = chạm liên tục** — không có khử nền. Calib phải thu hẹp vùng (`half_x/ymax`) để loại.
4. `nums_point_report`, `window`, `tick`, `min_cluster`, `cluster_mm` có thể thử không cần build native: `Tools/lidar_bench/lidar_analyze.py latency … --window 0.2 --tick 0.05` (xem README tool).

## 2. Độ trễ D200 — phân rã (từ code; xác nhận bằng mô phỏng cùng pipeline)

Từ lúc chân chạm sàn → `pointerDown` trong Unity:

| Thành phần | Điển hình | Tệ nhất | Nguồn |
|---|---|---|---|
| Chờ tia quét trúng vật | ~50 ms | 100 ms (1 vòng @10 Hz) | tốc độ quay |
| Chờ đủ ≥ 3 điểm trong cụm | 0 (vật ≥ 3 điểm/vòng) | +100…200 ms (vật nhỏ/xa, cần 2–3 vòng) | `min_cluster=3` + cửa sổ 300 ms |
| Chờ tick phân tích | ~50 ms | 100 ms | `CLUSTER_ANALYSIS_INTERVAL` |
| USB bulk + pipe + 3 thread (sleep 200 µs) | vài ms (ước tính, chưa đo) | ~16 ms (ước: latency timer CP2102) | `LidarUsbBridge`, `native-lib.cpp` |
| Chờ frame Unity | ~8 ms | 17 ms @60 fps | `Update()` |
| **Tổng** | **~110–120 ms** | **~235 ms** (vật đủ điểm) **~400 ms** (vật nhỏ/xa) | |

Throttle 150 ms **không** làm trễ lần báo đầu, chỉ chặn lặp.

## 3. Option RPLIDAR C1 cho K02

**Bật**: thêm `"lidar_model": "c1"` vào `/sdcard/EduXplore/lidar_config.json`, **đóng hẳn app mở lại** (baud + lệnh quét chỉ gửi lúc kết nối USB). Thiếu key / `"d200"` = hành vi cũ, không đổi gì với máy đang chạy.

```
MSYS_NO_PATHCONV=1 adb pull /sdcard/EduXplore/lidar_config.json   # rồi thêm dòng "lidar_model": "c1"
MSYS_NO_PATHCONV=1 adb push lidar_config.json /sdcard/EduXplore/lidar_config.json
```

Khác biệt kỹ thuật (C1 = Slamtec RPLIDAR C1, đã dò thật trên COM6: model 0x41, FW 1.02, chế độ `Standard` 0x81 và `DenseBoost` 0x85, đều 200 µs/mẫu = 5000 mẫu/s):

| | D200 (LD14P) | C1 |
|---|---|---|
| Baud | 230400 | **460800** (Java `initCp2102` đặt theo model) |
| Khởi động | tự phát dữ liệu | **phải gửi lệnh**: `A5 25` (stop), xả, `A5 20` (quét Standard) — `LidarUsbBridge.startC1Scan`; descriptor phản hồi 7 byte bị Java bỏ trước khi vào pipe |
| Định dạng | gói 47 byte + CRC8, 12 điểm | node 5 byte (không CRC; đồng bộ bằng 2 bit kiểm tra) — `rplidar_ParseByte` |
| Mật độ | ~4000 điểm/s, ~0,9°/điểm | 5000 mẫu/s, đo thật ~4300 điểm hợp lệ/s, ~0,70°/điểm |
| USB | CP2102 VID `10C4` PID `EA60` | **cùng VID/PID** ⇒ không tự nhận loại được, phải đặt `lidar_model` |

Phần còn lại (lọc vùng, gom cụm, throttle, calib, Unity) **dùng chung nguyên xi**. Đường C1 trong native chỉ thay bước 1 (giải gói); mọi tham số ở mục 1 giữ nguyên.

**Chưa kiểm chứng trên K02** (chỉ chạy được trên laptop): luồng Java gửi lệnh qua bulk OUT, hướng quay/mốc 0° của C1 so với D200 (nếu chạm bị lật/xoay: chỉnh `offset_angle`; lật gương thì hiện chưa có tuỳ chọn — báo để thêm), `lidar_config.json` mới cho C1 (hình học vùng sàn giữ nguyên nếu lắp cùng vị trí). `liblidar_unity.so` và `lidarlib-release.aar` đã build lại nhưng **chưa build APK / chưa cài K02**.

## 4. So sánh C1 vs D200

### 4.1 Đo thật trên C1 (laptop, COM6, 2026-10-19, cảnh tĩnh trong phòng làm việc)

| Chỉ số | C1 Standard | C1 DenseBoost |
|---|---|---|
| Tốc độ quay | 10,2 Hz (p95 chu kỳ 103 ms) | 10,3 Hz |
| Điểm hợp lệ/s | ~4270 | ~4260 |
| Điểm/vòng | ~418 | ~414 |
| Bước góc | 0,70° | 0,70° |
| Độ lặp lại (σ qua các vòng, trung vị) | 0–1 m: 1,9 mm · 1–2 m: 1,2 mm · 2–3 m: 4,0 mm · 3–4,5 m: 13 mm | 2,0 · 1,3 · 4,8 · 14 mm |
| Tỉ lệ ô góc có phản xạ ổn định | 94% | 94% |

Hai chế độ cho kết quả gần như trùng ⇒ native dùng **Standard** (đơn giản hơn, có quality). "Độ lặp lại" là độ nhiễu khi đo cùng 1 bề mặt, **không phải sai số tuyệt đối** (cần trụ có khoảng cách thước đo: bước 4 ở README tool).

### 4.2 D200: chưa đo thật

Chưa có D200 nối vào laptop trong phiên này. Số liệu danh định từ code: 4000 điểm/s (`lidar_measure_freq_`), 10 Hz, 12 điểm/gói, ~0,9°/điểm, bỏ qua mọi điểm `d = 0`. Tool đã có driver D200 nhưng **chưa chạy với phần cứng thật** (chỉ kiểm thử bằng mô phỏng) — nếu cắm được D200 vào cổng COM thứ hai, chạy `lidar_view.py --model d200 --port COMx` để xác nhận trước.

### 4.3 Mô phỏng cùng pipeline, cùng nhiễu (chỉ khác mật độ điểm)

Cấu hình: nhiễu σ = 5 mm cho cả hai, rớt 2%, window 300 ms, tick 100 ms, min_cluster 3, 40 lần/ô, vật đứng yên xuất hiện ở pha ngẫu nhiên. Trễ = từ lúc vật xuất hiện (chân lý trong mô phỏng) → báo chạm đầu tiên. Bảng đầy đủ: `python Tools/lidar_bench/sim_sweep.py`.

| Vật | Khoảng cách | Điểm/vòng C1 / D200 | Trễ TB (p95) C1 | Trễ TB (p95) D200 |
|---|---|---|---|---|
| Trụ Ø50 | 1 m | 4,0 / 3,2 | 101 (148) ms | 92 (157) ms |
| Trụ Ø50 | 1,5 m | 2,7 / 2,1 | 106 (162) ms | 157 (234) ms |
| Trụ Ø50 | ≥ 2 m | ≤ 2,0 / ≤ 1,6 | ~309 (441) ms | ~292 (357) ms |
| Chân Ø100 | 2 m | 4,0 / 3,2 | 109 (175) ms | 287 (334) ms |
| Chân Ø100 | ≥ 2,5 m | ≤ 3,2 / ≤ 2,5 | ~301 (348) ms | ~287 (334) ms |

Đọc kết quả: (1) khi vật cho **≥ 3 điểm/vòng** cả hai ≈ 100 ms (sai khác < 15 ms là nhiễu thống kê); (2) khi vật **< 3 điểm/vòng** phải gom nhiều vòng ⇒ nhảy lên ~300 ms. C1 dày hơn ~25% nên giữ được ~100 ms **xa hơn** (trụ Ø50: tới ~1,5 m so với ~1 m của D200; chân Ø100: tới ~2 m so với ~1,5 m) — đây là lợi thế độ trễ thật duy nhất của C1 ở K02 (cả hai vẫn rơi về ~300 ms khi vật quá nhỏ/xa). (3) Không ô nào bị hụt (vật đứng yên, 1,5 s), nhưng vật **di chuyển** sẽ nhoè vì ngưỡng 30 mm — chưa mô phỏng.
Mô phỏng là mô hình, không phải đo thật; nhiễu D200 thật có thể khác 5 mm.

## 5. Việc tiếp theo
- Đo D200 thật bằng tool (cùng 1 trụ, cùng vị trí, 30 s mỗi cảm biến), điền mục 4.2.
- Cài bản mới lên K02, test `"lidar_model": "c1"`: chạm có ăn, hướng đúng, không "ma", log `adb logcat -s FloorGame_Main LidarUsbBridge` thấy `Lidar_SetModel → C1`.
- Quyết định có thêm mục chọn loại LiDAR trên ControlActivity (hiện chỉ sửa file JSON).
