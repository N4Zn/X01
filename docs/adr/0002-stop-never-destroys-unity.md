# ADR 0002 — Stop không bao giờ destroy UnityPlayerActivity
- **Trạng thái**: chấp nhận (2026-09)
- **Bối cảnh**: Unity IL2CPP tự `Process.killProcess()` cả process (chung với ControlActivity) khi `UnityPlayerActivity` bị destroy; không chặn được từ code app.
- **Quyết định**: Stop = pause + che đen display máy chiếu; Start lần 2 gửi `OnLoadGameRequested` cho Unity đang sống (`ControlActivity.unityStarted`).
- **Hệ quả**: Intent extra chỉ đọc được lúc cold-boot; mọi cấu hình đổi sau phải đi qua UnitySendMessage (xem `docs/contracts/java-unity-bridge.md`).
