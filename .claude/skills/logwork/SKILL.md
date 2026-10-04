---
name: logwork
description: Kết thúc ngày làm việc của eduXploreGame2.0 — commit local, ghi nhật ký ngày vào notes/YYYY-MM.md, phân loại kiến thức vào docs/CLAUDE.md lồng, cập nhật TODO.md, rồi push nhánh hiện tại lên GitHub. Dùng khi user gõ /logwork hoặc nói "kết thúc ngày", "log công việc". Tuỳ chọn --no-push.
---

# /logwork — chốt ngày làm việc

Gọi skill = user đồng ý **push nhánh hiện tại** (không bao giờ `main`, không `--force`). `--no-push` thì bỏ bước 6. Trả lời tiếng Việt, báo cáo cuối ≤ 8 dòng.

## Bước 1 — Kiểm tra an toàn
- `git branch --show-current` — nếu là `main` thì DỪNG, hỏi user.
- `sh handoff/check-frozen.sh` phải thoát 0 (game đóng băng không được đổi). Nếu có việc đang DOING trong `handoff/README.md`, chạy thêm `sh handoff/check-scope.sh <file việc>`.
- File mới > 50MB không thuộc LFS → báo user, KHÔNG commit file đó.

## Bước 2 — Commit local
- `sh Tools/autosave.sh --force` để lưu mọi thứ đang dở dạng `wip:`.
- Nếu thay đổi chia được thành nhóm rõ (vd: Android / Unity / docs), ưu tiên commit riêng từng nhóm với message tiếng Việt không dấu hoặc có dấu ngắn gọn, thay vì 1 commit `wip:` duy nhất. Đừng gom thay đổi không liên quan của Unity tự sinh (vd `DOTweenSettings.asset`, `EditorBuildSettings.asset`) vào commit nghiệp vụ; hỏi user nếu nghi ngờ.

## Bước 3 — Gom việc đã làm
- Đọc `notes/YYYY-MM.md`, tìm dòng `HEAD:` của ngày gần nhất → mốc. `git log --oneline <mốc>..HEAD` (nếu chưa có mốc: `--since=midnight`).
- Cộng với những gì phiên hiện tại đã làm / quyết định (lý do "vì sao" chỉ có trong hội thoại — ghi đủ).

## Bước 4 — Viết nhật ký (cho user, không tự nạp vào phiên)
Thêm vào CUỐI `notes/YYYY-MM.md` (tạo file nếu sang tháng mới) mục:
```
## YYYY-MM-DD
**Đã làm** — gạch đầu dòng ngắn, mỗi dòng 1 việc, có tên file/hàm chính
**Quyết định** — và lý do
**Còn mở** — trỏ TODO.md
HEAD: <hash ngắn>
```
Nếu hôm nay đã có mục thì bổ sung, không tạo trùng.

## Bước 5 — Phân loại kiến thức + TODO
Với mỗi kiến thức mới đáng giữ, chọn nơi theo mức quan trọng:
- **Chi tiết một khu vực** (gotcha, cách làm, số liệu) → `CLAUDE.md` lồng trong thư mục đó, hoặc `docs/<chủ đề>.md`. Cập nhật dòng `Cập nhật: <ngày> @ <hash>` đầu file.
- **Thay đổi kiến trúc lớn / hợp đồng liên module / quyết định khó đảo ngược** → `docs/adr/NNNN-*.md` (10–15 dòng: bối cảnh, quyết định, hệ quả) VÀ 1 dòng trong changelog kiến trúc của `CLAUDE.md` gốc. Chỉ ghi version lớn; `CLAUDE.md` gốc giữ ≤ ~8KB, mỗi mục ≤ 5 dòng + link.
- **Việc dở / ý tưởng** → `TODO.md` (đúng nhóm; xong thì tick `[x]` + ngày).
- **Sở thích/phản hồi của user** → memory, không phải docs.
- Cảnh báo (không tự sửa): code một khu vực đổi nhưng docs khu vực đó chưa đổi.
Không chép lại thứ đã nằm trong code hoặc git log.

## Bước 6 — Commit ghi chú + push
- Commit riêng các file `notes/ TODO.md docs/ CLAUDE.md` : `log: YYYY-MM-DD`.
- Push: `git push origin <nhánh hiện tại>` (nhánh chưa có upstream → `-u`). Lỗi từ chối (non-fast-forward) → DỪNG, báo user, không force.
- Báo: số commit hôm nay, file đã ghi, hash HEAD, kết quả push, cảnh báo docs lỗi thời (nếu có).
