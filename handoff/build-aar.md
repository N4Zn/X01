---
id: build-aar
status: STANDING   # việc lặp lại, không có trạng thái DONE
assignee: bất kỳ agent (Claude Code: /build-aar)
branch: nhánh hiện tại
scope:
  - Assets/Plugins/Android/controlui-release.aar
  - Assets/Plugins/Android/lidarlib-release.aar
  - Assets/Plugins/Android/faceenroll-release.aar
  - NativePlugins/ControlUiAndroidLib/**
  - NativePlugins/LidarNativeAndroidLib/**
  - NativePlugins/FaceEnrollAndroidLib/**
  - TODO.md
---

# Việc thường trực: kiểm tra & build aar

**Giao việc bằng 1 câu**: "build aar" (hoặc "build aar controlui"). Agent làm toàn bộ theo dưới đây, không hỏi lại.

1. `sh Tools/build-aar.sh changed --check-only` rồi `sh Tools/build-aar.sh changed`.
2. Lỗi compile trong module → sửa trong scope trên, build lại (tối đa 3 vòng), vẫn lỗi thì dừng và ghi nguyên văn lỗi vào mục "Kết quả" của file này.
3. `git add` chỉ file aar vừa build; commit local `aar: rebuild <module>`. Không push.
4. aar đổi chỉ vài byte (timestamp) → bỏ, không commit.
5. Nếu thay đổi cần test trên K02 → thêm 1 dòng vào `TODO.md`.

Người dùng còn lại: build APK trong Unity. Game đóng băng: không đụng (`AGENTS.md`).

## Kết quả (ghi đè mỗi lần chạy, 1–3 dòng)
_chưa chạy_
