# handoff/ — nơi bàn giao việc giữa các agent (Claude Code ↔ Android Studio agent)

**Đọc file này TRƯỚC khi làm.** Mỗi việc = 1 file `handoff/<yyyy-mm-dd>-<tên-việc>.md` (bối cảnh, hợp đồng, danh sách việc, blacklist).

## Quy ước
- Trạng thái: `TODO` → `DOING` (agent nhận việc) → `REVIEW` (làm xong, chờ review) → `DONE` (người/Claude Code review xong).
- Làm xong: đổi trạng thái sang `REVIEW` ở bảng dưới + thêm mục **"Kết quả"** cuối file việc: file đã sửa, phần chưa làm, chỗ nghi ngờ.
- Mỗi agent CHỈ sửa file việc của mình + đúng dòng của việc đó ở bảng dưới (tránh xung đột git).
- Game trong BLACKLIST của file việc: tuyệt đối không đụng.
- Ngôn ngữ: tiếng Việt.

## Bảng việc

| Việc | File | Trạng thái | Giao cho |
|---|---|---|---|
| Cài đặt game chuyển sang ControlActivity (khung Unity đã xong) | [2026-10-04-game-settings.md](2026-10-04-game-settings.md) | DONE (Claude Code đã review code 2026-10-04; còn: compile Unity + test K02) | Android Studio agent |

## Quy tắc tự động
Quy tắc chung cho mọi agent (game đóng băng, nơi ghi kết quả, không hỏi lại) nằm ở `../AGENTS.md`; danh sách game đóng băng: `FROZEN.txt`; kiểm tra: `sh handoff/check-frozen.sh`.

## Giao việc mới
1. Copy `TEMPLATE.md` → `handoff/<yyyy-mm-dd>-<tên>.md`, điền front matter (`scope:` là allowlist đường dẫn).
2. Tạo worktree + nhánh riêng: `sh handoff/new-agent-worktree.sh <tên>` rồi mở Android Studio ở thư mục đó.
3. Agent xong → `check-scope.sh` + `check-frozen.sh` thoát 0 → REVIEW. Claude Code review → merge nhánh `agent/<tên>` (squash) vào nhánh làm việc.
