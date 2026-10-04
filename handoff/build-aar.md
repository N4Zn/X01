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
| Tình huống | Script in | Git |
|---|---|---|
| Không sửa gì | `Không module nào thay đổi — không cần build.` | sạch, không commit |
| Sửa nguồn Android | `COMMIT <hash>: aar: rebuild <module>` | 1–2 commit mới (aar + ghi kết quả) |
| Build lại nhưng aar y hệt | `nội dung aar KHÔNG đổi ... (không commit)` | sạch |
| Lỗi compile | `LỖI: ...` + 40 dòng log cuối | sửa trong scope, chạy lại, tối đa 3 vòng, vẫn lỗi → dừng, dán nguyên văn lỗi vào "Kết quả" |

## Báo cáo cho người dùng (BẮT BUỘC khớp thực tế)
Dán nguyên văn output của lệnh trên + `git log --oneline -3` + `git status --short`. Không viết "đã cập nhật/đã commit" nếu `git log` không có commit đó. Không ghi kết quả vào file việc khác (không phải `2026-10-04-game-settings.md`). Cần test trên K02 → thêm 1 dòng vào `TODO.md`.

Người dùng còn lại: build APK trong Unity. Game đóng băng: không đụng (`AGENTS.md`).

## Kết quả (script tự ghi đè mỗi lần chạy `--commit`)
_chưa chạy_
