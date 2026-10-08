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

**Giao việc bằng 1 câu**: "build aar" (hoặc "build aar controlui"). Agent làm đúng các bước dưới, không hỏi lại.

## Cách làm (1 lệnh, script lo hết)
```
sh Tools/build-aar.sh changed --commit            # tự phát hiện module đổi; hoặc thay changed bằng controlui | lidarlib | faceenroll | all
```
- Không có `sh` trong PATH (Windows): dùng `"C:\Program Files\Gitin\sh.exe" Tools/build-aar.sh changed --commit`.
- Script tự: kiểm tra JDK/SDK/JSON registry/game đóng băng → build → kiểm tra cấu trúc aar → copy vào `Assets/Plugins/Android/` → so NỘI DUNG aar với HEAD (bỏ qua timestamp) → **khác thì commit local** `aar: rebuild <module>` (kèm nguồn module), **giống thì hoàn tác file** → ghi mục "Kết quả" bên dưới.
- Không build tay trong Android Studio/Gradle rồi tự copy; không `git add`/`commit` tay; không push.
- Chỉ xem trước: `sh Tools/build-aar.sh changed --check-only`.

## Kết quả mong đợi
_lần chạy 2026-10-08 13:53_ (module:controlui)

- controlui: aar đã đổi → commit `aar: rebuild controlui`.
