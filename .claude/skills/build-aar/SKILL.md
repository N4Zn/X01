---
name: build-aar
description: Kiểm tra và build lại các module Android (.aar: controlui, lidarlib, faceenroll) rồi copy vào Assets/Plugins/Android, commit local. Dùng khi sửa NativePlugins/*AndroidLib hoặc game_registry.json, hoặc user bảo "build aar".
---

# /build-aar [controlui|lidarlib|faceenroll|all]  (mặc định: tự phát hiện module đã đổi)

Tự động hoá toàn bộ, không hỏi lại user giữa chừng. Không push, không đụng game đóng băng.

1. `sh Tools/build-aar.sh <module|changed> --commit` (timeout ≥ 10 phút). Script tự kiểm tra, build, copy, so nội dung aar với HEAD, commit local `aar: rebuild <module>` khi có thay đổi thật, và ghi `handoff/build-aar.md`. Chỉ xem trước: `--check-only`.
2. Thất bại → đọc `build-aar.log`, sửa lỗi compile trong scope module đó, chạy lại (tối đa 3 vòng); vẫn lỗi → dừng, báo nguyên văn lỗi.
3. Khi module là `controlui`: kiểm tra `game_registry.json` khớp `GameRegistry.cs` (cùng danh sách `name`).
4. Báo user 1–3 dòng: module nào, commit hash (lấy từ `git log`), việc còn lại ("build lại APK trong Unity"). Cần test K02 → thêm vào `TODO.md`.

Không làm: build `libvlc`/`unityplugin` (file ngoài), build `.so` native (xem `NativePlugins/LidarUnity/CLAUDE.md`), push.
