# AGENTS.md — quy tắc cho MỌI agent (Android Studio / Gemini / Junie / Claude Code)

Trả lời bằng tiếng Việt. Đọc `CLAUDE.md` + `handoff/README.md` trước khi làm việc được giao.

## 1. Game ĐÓNG BĂNG — tuyệt đối không đụng, không hỏi
Danh sách (regex theo đường dẫn) ở `handoff/FROZEN.txt`: AddUp, NumberAddUp, PathFinder, TrainPath, Monopoly, RiverCross, Balloon (SoDem2/ChuCai2).
- KHÔNG sửa, KHÔNG đề xuất sửa, KHÔNG hỏi "có cần làm thêm Monopoly/Balloon... không?", KHÔNG nhắc trong báo cáo (trừ khi user tự hỏi).
- Các bảng/danh sách trong file việc có liệt kê game đóng băng chỉ để tham khảo → BỎ QUA dòng đó.
- Trước khi commit: `sh handoff/check-frozen.sh` phải thoát 0 (hook pre-commit cũng chặn). Lỡ sửa nhầm → `git checkout HEAD -- <file>`.
- Chỉ user mới mở lại (bằng cách xoá dòng trong FROZEN.txt).

## 2. Làm xong ghi vào đâu — KHÔNG hỏi lại
- Ghi kết quả vào mục **"## Kết quả"** cuối file việc `handoff/<ngày>-<việc>.md` (file đã sửa / chưa làm / chưa test / chỗ nghi ngờ).
- Đổi trạng thái dòng việc trong `handoff/README.md` sang `REVIEW`. KHÔNG tự đặt `DONE`.
- Thắc mắc/cần quyết định → ghi vào mục **"## Câu hỏi"** cuối file việc, rồi làm tiếp phần không bị chặn. Không kết thúc câu trả lời bằng câu hỏi chung chung.
- Tin nhắn cuối chỉ cần 1–3 dòng: "xong, xem handoff/<file> mục Kết quả".

## 3. Phạm vi
- Mỗi việc có `scope:` trong front matter của file việc (mẫu: `handoff/TEMPLATE.md`). CHỈ sửa đường dẫn trong scope; trước khi báo xong chạy `sh handoff/check-scope.sh handoff/<file việc>` phải thoát 0.
- Làm trên nhánh `agent/<tên-việc>` (worktree riêng: `sh handoff/new-agent-worktree.sh <tên-việc>`), KHÔNG làm trực tiếp trên nhánh của user.
- Chỉ sửa file thuộc việc được giao. Không `git checkout/merge/push main`, không xoá file ngoài phạm vi.
- Không sửa `CLAUDE.md`, `AGENTS.md`, `handoff/FROZEN.txt` (Claude Code/user quản lý). Muốn bổ sung kiến thức vào CLAUDE.md → ghi đề xuất vào mục "Kết quả".

## 4. File lớn
- File mới `.mp4/.mov/.onnx/.psd` tự vào Git LFS (`.gitattributes`). Không commit file > 50MB dạng thường; không xoá các dòng `-filter` trong `.gitattributes`.

## 5. Build `.aar` (Android modules)
Sửa mã trong `NativePlugins/{ControlUiAndroidLib,LidarNativeAndroidLib,FaceEnrollAndroidLib}` (kể cả `game_registry.json`) thì build + commit aar bằng **một lệnh duy nhất**, KHÔNG build/copy/commit tay:
```
sh Tools/build-aar.sh changed --commit
```
Script kiểm tra, build, copy vào `Assets/Plugins/Android/`, chỉ commit khi nội dung aar thật sự đổi, tự ghi kết quả. Không push. Hướng dẫn đầy đủ + định dạng báo cáo (phải dán nguyên văn output và `git log`/`git status`, không được tự tóm tắt sai): `handoff/build-aar.md`. Claude Code dùng `/build-aar`.
