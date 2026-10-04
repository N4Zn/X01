---
name: build-aar
description: Kiểm tra và build lại các module Android (.aar: controlui, lidarlib, faceenroll) rồi copy vào Assets/Plugins/Android, commit local. Dùng khi sửa NativePlugins/*AndroidLib hoặc game_registry.json, hoặc user bảo "build aar".
---

# /build-aar [controlui|lidarlib|faceenroll|all]  (mặc định: tự phát hiện module đã đổi)

Tự động hoá toàn bộ, không hỏi lại user giữa chừng. Không push, không đụng game đóng băng.

1. `sh Tools/build-aar.sh <module|changed> --check-only` — kiểm tra JDK, local.properties, JSON registry, check-frozen. Lỗi tiền điều kiện → sửa nếu là lỗi của mình, còn lại báo user.
2. `sh Tools/build-aar.sh <module|changed>` (timeout ≥ 10 phút). Thất bại → đọc `build-aar.log`, sửa lỗi compile trong scope module đó, build lại (tối đa 3 vòng); vẫn lỗi → dừng, báo nguyên văn lỗi.
3. Kiểm tra thêm khi module là `controlui`: `game_registry.json` khớp `GameRegistry.cs` (cùng danh sách `name`, 7 game đóng băng vẫn comment-out/không có trong JSON nếu đã vậy ở C#).
4. Chỉ `git add` đúng file aar vừa build (+ source module nếu chưa commit), commit local: `aar: rebuild <module> (<lý do 1 dòng>)`. aar vào LFS tự động (`.gitattributes`). Nếu aar không đổi nội dung (chỉ khác timestamp, vài byte) → `git checkout` bỏ, không commit.
5. Báo user 1–3 dòng: module nào build, kích thước, commit hash, và việc còn lại cho user ("build lại APK trong Unity"). Nếu có thay đổi cần test trên K02 thì ghi vào `TODO.md`.

Không làm: build `libvlc`/`unityplugin` (file ngoài), build `.so` native (xem `NativePlugins/LidarUnity/CLAUDE.md`), push.
