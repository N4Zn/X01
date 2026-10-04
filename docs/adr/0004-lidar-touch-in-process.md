# ADR 0004 — LiDAR touch bắn PointerEventData trong process Unity
- **Trạng thái**: chấp nhận (2026-09)
- **Bối cảnh**: injection qua Android InputManager/Accessibility cần quyền OS đặc biệt/root và nhiễu hệ thống.
- **Quyết định**: `LidarTouchBridge.cs` đọc `liblidar_unity.so` rồi bắn `PointerEventData` vào EventSystem; nút cứng `JoystickButton0` bật/tắt, mặc định OFF.
- **Hệ quả**: config calib nằm trên thiết bị (`lidar_config.json`), không theo APK; calib 2 tầng (vật lý cảm biến vs "vùng tương tác"). Chi tiết: `docs/lidar-calib.md`.
