# TODO — việc cần làm (KHÔNG tự nạp vào phiên; chỉ user/Claude Code sửa, agent dùng handoff/)

Định dạng: `- [ ] việc — (ngày thêm)`. Xong thì tick `[x]` + ngày; `/logwork` dọn việc đã xong sang notes.

## Game settings (ControlActivity)
- [ ] Mở Unity compile các sửa đổi của agent (FloorZoneClearer, Base, LaneTrack, controller cũ) — (2026-10-04)
- [ ] Test K02: đổi từng tham số khi đang chơi, cold-boot, Start lần 2; tắt ô "chờ clear" thử 1 game Independent + 1 game cũ — (2026-10-04)
- [ ] Quyết: `SpawnFlowDisplay` có chia `spawnInterval` cho `FlowSpeed` không — (2026-10-04)
- [ ] B8: xoá `QuestionTimeout` + panel setting cũ ở MenuScene/HomeScene? — (2026-10-04)
- [ ] Ô số flow trong ControlActivity: gõ dở bị lưu sớm, số >5 không clamp hiển thị; seekbar ghi file mỗi nấc — (2026-10-04)
- [ ] Tab "Tư duy - Logic" (mục 5) đang trống, kiểm tra UI — (2026-10-04)

## Git / hạ tầng
- [ ] Merge `scorescene-mvp-tiachop` → `main` (cách 1: `commit-tree` hoặc merge --allow-unrelated-histories -X theirs; squash khi về sau) — (2026-10-04)
- [ ] Khôi phục lại `ProjectSettings/EditorBuildSettings.asset` nếu scene lại bị bỏ tick — (2026-10-04)
- [x] Tách `CLAUDE.md` 52KB → gốc 6.9KB + `docs/` + CLAUDE.md lồng (2026-10-04)
- [ ] ADR + `docs/contracts/` (JSON settings, UnitySendMessage) — (2026-10-04)
- [ ] Pre-push build check + CI nhẹ (check-frozen, build aar). `Tools/build-aar.sh` đã có (2026-10-04); Unity batch compile chưa có

## Calib / LiDAR (từ CLAUDE.md)
- [ ] Wiring Launcher: `CALIB_COMPONENT` trỏ `CalibActivity` — (2026-09-17)
- [ ] Mở Unity Editor xác nhận `CalibScene.unity` load sạch — (2026-09-17)
- [ ] Test thật: trụ Ø5cm ở góc xa có đủ ≥3 điểm; cửa sổ lắng nghe 2.5s đủ chưa; multi-touch sau sửa `sendTouch` — (2026-09-17)

## GenericGame
- [ ] Đăng ký `CuaHangKemTruocSau`/`ThuNghiem12` vào GameRegistry (import lại zip) — (2026-10-03)
- [ ] Test trong Editor: round/collect/mirror/icon grid; hộp thoại đăng ký registry — (2026-10-03)
- [ ] Tool so/reset marker Variant theo json mới; so schema `game_builder_v2.2_1003.html` — (2026-10-03)
- [ ] GenericGameBuilder: Safari chưa chơi thử được — (2026-10-04)

## Đóng băng (không làm cho tới khi user mở lại) — xem `handoff/FROZEN.txt`
