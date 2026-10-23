# Pose (khung xương) trên K02 — kết quả đo & phương án

Cập nhật: 2026-10-23 @ f8a5f308. Nguồn mẫu: `D:\X_projects\AI_Feature\Bone\DatLV\nhan-dien-khung-xuong_9_xephanggon (1).html`
(MediaPipe Pose Landmarker Lite, `numPoses: 3`, delegate GPU, Web Worker; chia 3 vùng trái/giữa/phải, mỗi vùng 1 người).

## Chạy HTML trực tiếp trên K02: KHÔNG được
WebView hệ thống K02 = `com.android.webview 74.0.3729.186` (Android 10). `tasks-vision` cần module worker + WebGL2/wasm hiện đại → không chạy. Phải đi đường native.

## Đo thật (MediaPipe tasks-vision 0.10.14 Android, K02 MT8168, ảnh 640 px, chế độ IMAGE)
Công cụ: `Tools/pose_bench/` (app tối thiểu; ảnh thử đẩy vào `/sdcard/Android/data/com.eduxplore.posebench/files/test.jpg`, danh sách ca ở `bench.txt`, kết quả logcat tag `POSEBENCH`).

| Ca (lite) | 1 người | 2 người |
|---|---|---|
| CPU (XNNPACK) | 3.1 fps (322 ms) | 2.1 fps (483 ms) |
| **GPU (Mali-G52)** | 10.9 fps (92 ms) | **6.2 fps (161 ms)** |

- `full` chậm hơn ~25% (CPU 2.5 fps / GPU ~9.8 fps, 1 người) — không cần.
- GPU delegate: lần tạo đầu mất **~13 s** (biên dịch shader) → khởi tạo lúc vào game/nạp scene, không làm giữa vòng chơi.
- Đã đo lúc K02 gần như rảnh (không Unity chạy). Chưa đo khi Unity + LiDAR đang chạy chung GPU/CPU.
- Detector chỉ bắt được người thấy gần đủ thân; ảnh camera 3 người ngồi/cắt bàn chỉ ra 1 pose. Cần cam đặt sao cho thấy toàn thân.

## PoseTest (đã dựng 2026-10-23)
Game thử `PoseTest` (registry cat 8, scene `PoseTestGame`) — 2 người, mỗi người một nửa khung hình, 6 tư thế port từ HTML, nhất 3 điểm / nhì 2 điểm, log `*_PoseTest.json`.
- Native: `NativePlugins/PoseAndroidLib` (module `posebridge`, build bằng `sh Tools/build-aar.sh posebridge`) → `Assets/Plugins/Android/posebridge-release.aar`; `mainTemplate.gradle` khai `com.google.mediapipe:tasks-vision:0.10.14`.
- Unity: `Assets/Game/Scripts/UIScripts/PoseTest/` (`PoseBridgeClient`, `PoseSkeletonGraphic`, `PoseChecks`, `PoseTestController`); dựng scene: Tools → PoseTest → Build Scene.
- Nguồn ảnh: camera IP (`/sdcard/EduXplore/ipcamera.json` `source:"ip"`, áp mirror/rotate180); chưa hỗ trợ webcam UVC.
- Gửi 13/33 điểm: mũi, vai, khuỷu, cổ tay, hông, đầu gối, mắt cá. Cầu nối: `getStatus/getSeq/getResultJson/getPreviewJpeg` (xem `PoseBridge.kt`).
- Đo trên K02 (cảnh trống): GPU 6.4 kết quả/s, suy luận 90 ms; khởi tạo GPU ~14.5 s lần đầu mỗi lần mở app (vào lại game không phải chờ).
- Chạy nhanh bằng adb: `am start -n com.EduXplore.X01a/com.unity3d.player.UnityPlayerActivity --es com.eduxplore.control.SCENE_NAME PoseTestGame --es com.eduxplore.control.GAME_NAME PoseTest`.
- Chưa kiểm: người thật 2 đứng toàn thân, độ chính xác ngưỡng 6 tư thế, tải khi LiDAR + game thật cùng chạy.

### Preview mượt + khớp khung xương (2026-10-23)
- Nguồn ảnh theo thứ tự: MJPEG sub-stream `streamPath` (mặc định `/ISAPI/Streaming/channels/102/httpPreview`, đổi được trong `ipcamera.json`) → chụp HTTP kênh 102 → chụp kênh chính (`PoseBridge.getSource()`). Độ phân giải preview = độ phân giải sub-stream đặt trên camera (hạ ở web camera nếu muốn nhẹ hơn); suy luận luôn thu về 384 px ngang.
- Native trao nguyên JPEG cho Unity (không mã hoá lại), kèm mốc thời gian; mỗi kết quả khung xương mang `fid`/`ft` của ảnh đã dùng. Camera không xoay/lật ở native cho preview → Unity bù bằng `RawImage.uvRect` (`PoseBridgeClient.UvRect`); landmark tính trên ảnh đã xoay/lật nên trùng.
- Unity hiển thị video trễ `DisplayDelay` (≈ chu kỳ kết quả + độ trễ suy luận, tự đo) và nội suy khung xương giữa 2 kết quả quanh thời điểm đó (`DisplayPeople`); luật chơi vẫn dùng kết quả mới nhất (`People`).
- PoseTest tắt LiDAR touch (`LidarTouchBridge.NoTouchScenes`), dừng nhận diện mặt, giới hạn 30 fps.

## Phương án triển khai
1. **AAR `posebridge`** (Kotlin, trong process Unity như `unityplugin`): MediaPipe `PoseLandmarker`, GPU delegate, `numPoses=2`, 1 luồng nền, bỏ khung khi đang bận (chỉ xử lý khung mới nhất).
2. **Nguồn ảnh**: ảnh chụp HTTP từ camera IP (`SnapshotClient`, đã có) hoặc UVC; thu về ≤640 px trước khi đưa vào model.
3. **Cầu nối → Unity**: JSON landmark (33 điểm/người, x,y,visibility) qua `UnitySendMessage` mỗi lần có kết quả (~5–6 Hz).
4. **Logic game ở Unity (C#)**: chia vùng trái/phải theo x của hông, port 9 hàm nhận tư thế bằng hình học landmark từ HTML, xếp hạng theo thời gian hoàn thành. Vẽ khung xương bằng UI Unity, làm mượt/ngoại suy giữa 2 kết quả.
5. Dự phòng: nếu GPU delegate lỗi/tranh chấp với Unity → CPU 2.1 fps (vẫn đạt mục tiêu 1–2 fps cho 2 người).

## Việc còn lại trước khi coi là chắc
- Đo lại khi Unity chạy (MenuScene/game) + LiDAR đang bật; theo dõi nhiệt sau ≥10 phút.
- Thử với người thật đứng toàn thân (2 người) từ vị trí camera thật.
- Quyết định bảng vùng (trái/phải) có dùng `face_zones.json` chung không.
