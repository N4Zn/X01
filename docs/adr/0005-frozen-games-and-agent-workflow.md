# ADR 0005 — Đóng băng 7 game và quy trình agent bằng máy kiểm tra
- **Trạng thái**: chấp nhận (2026-10-04)
- **Bối cảnh**: agent từng sửa game đã bỏ (AddUp, NumberAddUp, PathFinder, TrainPath, Monopoly, RiverCross, Balloon) vì quy tắc chỉ ghi bằng lời.
- **Quyết định**: quy tắc thành máy kiểm tra: `handoff/FROZEN.txt` + `check-frozen.sh` (pre-commit), `scope:` trong task + `check-scope.sh`, worktree/nhánh `agent/<task>`, `AGENTS.md` là nguồn quy tắc chung.
- **Hệ quả**: mở lại game = sửa `FROZEN.txt` và registry theo yêu cầu user. File lớn mới vào LFS (mp4/mov/onnx/psd/aar).
