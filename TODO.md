# TODO — việc cần làm (KHÔNG tự nạp vào phiên; chỉ user/Claude Code sửa, agent dùng handoff/)

Định dạng: `- [ ] việc — (ngày thêm)`. Xong thì tick `[x]` + ngày; `/logwork` dọn việc đã xong sang notes.

## Game settings (ControlActivity)
- [ ] Mở Unity compile các sửa đổi của agent (FloorZoneClearer, Base, LaneTrack, controller cũ) — (2026-10-04)
- [ ] Test K02: đổi từng tham số khi đang chơi, cold-boot, Start lần 2; tắt ô "chờ clear" thử 1 game Independent + 1 game cũ — (2026-10-04)
- [ ] Quyết: `SpawnFlowDisplay` có chia `spawnInterval` cho `FlowSpeed` không — (2026-10-04)
- [ ] B8: xoá `QuestionTimeout` + panel setting cũ ở MenuScene/HomeScene? — (2026-10-04)
- [x] Ô số flow/thời gian game: chỉ áp khi Xong/rời ô, nhập ngoài dải dùng đúng số nhập (2026-10-05); còn: seekbar ghi file mỗi nấc
- [ ] Tab "Tư duy - Logic" (mục 5) đang trống, kiểm tra UI — (2026-10-04)

## Lớp / điểm (2026-10-05) — cần build APK trong Unity rồi test K02
- [ ] Build APK; mở Quản lý lớp 1 lần để chạy migration: còn đúng 2 lớp `5 tuổi` (16 bạn) + `Dev` (10 bạn); `adb pull /sdcard/EduXplore/roster.json` xem — (2026-10-05)
- [ ] Test sửa `roster.json` + `adb push` → mở lại Quản lý lớp thấy đổi; thử file sai cú pháp thấy toast lỗi — (2026-10-05)
- [ ] Test tab Lớp học (ControlActivity) hiện cả lớp, "-" cho bạn chưa chơi; Quản lý lớp tab Năng lực/Lịch sử khớp ControlActivity — (2026-10-05)
- [ ] Test cài đặt: chờ round 0 & 6s, flow nhập 0.05 / 8, thời gian game nhập 15 / 900; nhạc mặc định 50% trên máy sạch (xoá `game_settings.json` + PlayerPrefs) — (2026-10-05)
- [ ] Nhập ngày sinh/giới tính thật cho 16 bạn 5 tuổi (sửa roster.json) và thêm ảnh — (2026-10-05)

## Game: Counting / wait-clear / tutorial (2026-10-05) — cần build APK Unity rồi test K02
- [ ] Đếm đến 5 / Đếm đến 10 hiện câu hỏi (sửa `CountdownStyle.Apply` null material) — (2026-10-05)
- [ ] Chờ clear: Counting (độc lập) hiện "Go to START position!" từng bên sau mỗi câu; tắt ô "Chờ clear" thì không chặn; thử RoundEndDelay=0 — (2026-10-05)
- [ ] Mọi game vào thẳng "Start in…", không còn màn Hướng dẫn — (2026-10-05)

## Quản lý lớp — panel học sinh (2026-10-05) — cần build APK Unity rồi test K02
- [ ] Panel học sinh: thấy đủ nút Xóa/Chụp ảnh/Thêm ảnh/Lưu; Enter chuyển ô; sửa ngày sinh → Lưu → mở lại còn — (2026-10-05)
- [ ] Xoá hết ảnh 1 bạn → bạn vẫn còn trong lớp ("Cần ảnh"); mở `enrolled.json` thấy `samples` rỗng; thêm ảnh lại được — (2026-10-05)
- [ ] Ảnh mới nằm ở `/sdcard/EduXplore/enrolled_photos/<tên>/`; ảnh cũ tự chuyển sang; bạn đã đủ 3 ảnh thì thêm ảnh bị từ chối (toast) — (2026-10-05)
- [ ] ControlActivity: danh sách game dài, cuộn xuống chọn game ở dưới → giữ nguyên vị trí cuộn (đổi môn thì về đầu) — (2026-10-05)
- [ ] Lidar tự ON khi vào mọi game (kể cả game cũ không dùng Kit): vào từng game cũ, chạm sàn có ăn; Menu/Calib vẫn OFF (`LidarTouchBridge.NonGameScenes`) — (2026-10-05)
- [ ] Tab TỔNG KẾT (ControlActivity): Thời lượng hiện m:ss (trừ Pause); điểm 2 đội = điểm cuối thật (cả game cũ, trước đây 0–0); Stop tay giữa chừng không lệch round cuối; danh sách bạn hiện "X/Y câu đúng" — (2026-10-05)

## Git / hạ tầng
- [ ] Merge `scorescene-mvp-tiachop` → `main` (cách 1: `commit-tree` hoặc merge --allow-unrelated-histories -X theirs; squash khi về sau) — (2026-10-04)
- [ ] Khôi phục lại `ProjectSettings/EditorBuildSettings.asset` nếu scene lại bị bỏ tick — (2026-10-04)
- [x] Tách `CLAUDE.md` 52KB → gốc 6.9KB + `docs/` + CLAUDE.md lồng (2026-10-04)
- [x] ADR + `docs/contracts/` (2026-10-04)
- [ ] Pre-push build check local + Unity batch compile (CI nhẹ đã có: .github/workflows/check.yml; `Tools/check-docs.sh`; `Tools/build-aar.sh`)

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
- [ ] HaiQua: tạm bỏ (2026-10-05), mở lại sau khi update scene: bỏ `// DISABLED` ở `GameRegistry.cs`, thêm lại dòng json, xoá 2 dòng HaiQua ở `FROZEN.txt`, build lại aar — (2026-10-05)

## Điểm theo round + lịch sử chi tiết (2026-10-04)
- [ ] Compile Unity (PlayerRecognitionService/GameControlBridge/MiniGameControllerBase/GenericGameController) + build APK + test K02: round có về ControlActivity không, `class_rounds.jsonl` có ghi không.
- [ ] Kiểm tra nội dung câu hỏi ở Lịch sử với game generic thật (chữ/icon/"Câu hỏi media") và game cũ (TestTongHop... `LogRound` truyền `correctAnswer` rỗng nên không hiện dòng "Đúng:").
- [ ] Nút "Chơi lại đúng câu hỏi này" từ Lịch sử: `questionId` đã được lưu; còn thiếu phần Unity ép câu đầu tiên = id đó (GenericGame: pool theo id; game CSV: tra theo id) + message Java→Unity.
- [ ] Lịch sử trong app chưa đọc `class_rounds_archive.jsonl` (round cũ >20.000); thêm nếu cần xem lại — (2026-10-04)
- [ ] Sao lưu định kỳ `/sdcard/EduXplore/class_rounds*.jsonl` ra ngoài máy — (2026-10-04)

## Countdown + màu chữ (2026-10-05)
- [ ] Mở Unity: compile (CountdownStyle.cs + sửa MiniGameControllerBase/GenericGameController/10 View cũ), chơi DemQua xem có "Next in Ns" giữa round, kiểm tra chữ countdown trắng/cỡ 56 ở HaiQua, FamilyMember, TestTongHop, 1 game generic.
- [ ] Test màu chữ câu hỏi/đáp án: chọn màu trong web builder → Export → import Unity → chữ đúng màu (web: ô màu dưới ô cỡ chữ).
- [ ] PlanetOrderDisplay giữ countdown chữ tối trên nền trắng (cố ý); SaveTheAstronaut/LaneDash giữ số 3-2-1 to 120 (đã trắng) — đổi nếu muốn đồng bộ hẳn.
- [ ] Quyết `Editor/GameControlBridgeTester.cs` (giữ/bỏ) và xác nhận xoá scene `ThuNghiem12.unity` (đang `D` chưa commit) — (2026-10-05)
