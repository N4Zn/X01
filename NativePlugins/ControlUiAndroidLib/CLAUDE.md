# ControlUiAndroidLib (ControlActivity, CalibActivity)

Chi tiết kiến trúc, Stop/Start, JSON settings, giao diện, lớp/điểm: **`docs/control-activity.md`**. Calib: `docs/lidar-calib.md`.
- Sau khi sửa Java/res/assets (kể cả `game_registry.json`): build lại aar bằng `sh Tools/build-aar.sh controlui` (tự copy vào `Assets/Plugins/Android/`). KHÔNG build tay rồi quên copy.
- Sửa tên hiển thị game: ở CẢ `GameRegistry.cs` và `game_registry.json`.
- Stop KHÔNG destroy UnityPlayerActivity.
