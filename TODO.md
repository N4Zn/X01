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

- [ ] Panel live ControlActivity: bên trả lời SAI / không có điểm vẫn hiện tên (Combined: TestTongHop + Kit); bên đã nhận diện nhưng chưa có câu nào hiện 0đ·0c — (2026-10-05)

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
- [ ] Chơi lại SoSanhSo/SoSanhSo2Dau/TachSo2/CuaHangKem sau khi dọn ảnh trùng (`_shared`) — ảnh/FlyToStay đúng; ThuNghiem1 chưa có game.json nên chưa gom — (2026-10-08)

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

## FaceEnroll: tracking + chào + ngày sinh (2026-10-05)
- [ ] K02: 1 người đã nhận diện, người thứ 2 bước vào che người 1 → khung người 2 KHÔNG mang tên người 1 (log `lock ... dropped`); 2 người cùng khung → mỗi người giữ đúng tên, đổi chỗ trái/phải không nhảy tên. Lock giữ theo vị trí, KHÔNG kiểm chứng lại bằng embedding (tên không chớp tắt); mặt mới/đổi cỡ đột ngột = track mới => nhận diện lại. Báo "mặt mới" sau 4 lần nhận diện liên tiếp không ra ai (`unknownFramesToAnnounce`).
- [ ] K02: câu chào không còn mất đầu câu (GreetingTts: luồng im lặng keep-alive + lead 500ms khi warm / 2200ms khi lạnh). Nếu vẫn mất: tăng `WARM_LEAD_MS`.
- [ ] Quản lý lớp: bấm ô ngày sinh → hộp chọn ngày/tháng/năm: cuộn tới đâu LƯU ngay tới đó, bấm ra ngoài để đóng vẫn giữ; nút Lưu tổng của học sinh vẫn dùng được — (2026-10-05)

## Dữ liệu sống qua lần cài lại (2026-10-05)
- Gốc dữ liệu lâu dài = `/sdcard/EduXplore` (targetSdk 27 → không bị Android xoá khi gỡ app): lớp, roster, khuôn mặt + ảnh, điểm (`class_rounds.jsonl`), `game_settings.json` (đã có từ trước) + MỚI: lịch sử chơi `GameLogs/`, `lidar_config.json`, `interaction_area_calib.json`, `sheets_sync_config.json`, `attendance_log.csv`, `snapshots/` (qua `SharedStorage.cs` + `AttendanceStore`). File cũ trong thư mục riêng của app tự được CHÉP sang lần mở đầu (không xoá nguồn).
- [ ] K02: build APK, cài đè bản cũ → kiểm tra `/sdcard/EduXplore/GameLogs` có log cũ + calib/lidar_config còn nguyên; chơi 1 game → log mới nằm ở GameLogs; gỡ app rồi cài lại → mọi thứ vẫn còn.
- Cài bằng `adb install -r` (KHÔNG `uninstall` trước) nếu muốn giữ cả PlayerPrefs; PlayerPrefs chỉ còn là bản sao, nguồn thật của cài đặt là `game_settings.json`.
- Lưu ý: dữ liệu tồn tại nhưng app cài lại phải được cấp lại quyền Storage thì mới đọc được (Android 10, targetSdk 27: quyền runtime reset khi gỡ app).

## Vùng nhận diện + sửa thoát app Quản lý lớp (2026-10-06)
Code đã sửa, aar `faceenroll`/`controlui`/`unityplugin` đã build — CHƯA test trên K02, cần build lại APK trong Unity.
- [ ] Quản lý lớp -> "⚙ Vùng nhận diện" (hoặc ControlActivity -> Cài đặt -> "Mở cài đặt vùng"): hiện hình camera + 2 chữ nhật (TRÁI/PHẢI) + vòng tròn ENROLL; −/+ chỉnh X, Y, rộng, cao (bán kính), LƯU ghi `/sdcard/EduXplore/face_zones.json`.
- [ ] "Thêm từ camera": chỉ mặt có tâm trong vòng tròn được nhận/chụp; ngoài vòng tối đi và bị bỏ qua.
- [ ] Vào game 2 người: mỗi bên chỉ nhận mặt trong vùng của mình; sửa vùng xong vào chơi là áp dụng (plugin kiểm tra file mỗi 2s). Chưa có file thì hành vi cũ.
- [ ] Đối chiếu vị trí vòng tròn/khung với hình thật (preview là fitCenter 16:9, overlay co giãn theo view — có thể lệch vài %).
- [ ] Soak test Quản lý lớp: vào/ra màn camera nhiều lần, rút/cắm camera USB, vừa nhận diện vừa thêm HS. Lỗi Java ghi ở `/sdcard/EduXplore/crash_fa.txt`; crash native: `adb logcat -b crash`.
- Đã sửa (nghi vấn thoát app): callback camera bọc try/catch + bỏ frame sai kích thước + bắt RejectedExecution; đóng camera/onDisconnect an toàn; onDestroy chờ thread nhận diện dừng; `AttendanceStore` đồng bộ (@Synchronized, `samplesOf` trả bản sao); tái dùng buffer frame (đỡ RAM).
- Source plugin game nằm ở `NativePlugins/UnityFacePluginAndroidLib` (bản sao, build ở `D:\X_projects\FaceRecognition\android`).
- [ ] Thoát màn "Vùng nhận diện" bị tắt app (K02 #1, 09:33): process `:fa` chết SIGABRT ~0,5s sau khi MainActivity bị huỷ, không có backtrace (ROM không ghi tombstone), thử lại 2 lần không tái hiện. Nghi SurfaceTexture giả bị GC khi luồng preview native còn ghi (log `dequeueBuffer failed (No such device)`) hoặc lệnh cấp quyền USB bằng tay lúc đang mở camera. Đã sửa: giữ ref SurfaceTexture, `destroy()` đúng 1 lần, thả texture sau khi đóng camera. Cần build lại APK + soak test; nếu còn: `adb logcat -d -b all -v threadtime | grep -i "X01a\|am_proc_died\|Zygote"` ngay sau khi tắt.
- [ ] Logic khung mặt màn camera (2026-10-06, chưa test): lần đầu nhận ra = khung xanh + tên + confidence; giữ nguyên khi tracking (nới ngưỡng ghép track cho mặt đã có tên, mặt nghiêng/hụt detect vẫn vẽ khung cuối); verify nền = 3 lần khớp lại khi mặt thẳng rồi hiện ✓ và thôi nhận diện (quá 8 lần thử cũng chấp nhận; 2 lần liên tiếp ra người khác chắc chắn thì bỏ lock nhận lại); mất mặt quanh vị trí đó liên tục 1s mới coi là rời khung, vào lại nhận diện lại. Kiểm tra: nghiêng/quay đầu không nháy xám; ra khỏi khung >1s rồi vào lại thì nhận lại.

## K02 #1: hook boot (2026-10-06)
- `NativePlugins/K02DeviceConfig/k02_boot_hook.sh` -> `/data/local/tmp/your_script.sh` (hook sẵn trong `lidar_config.rc`): chặn thanh thông báo + tự cấp quyền USB (camera/lidar) cho `com.EduXplore.X01a` qua `UsbGrant.dex`, theo dõi mỗi 5s để cấp lại khi cài lại APK/rút cắm cáp. Hướng dẫn đầy đủ: `Hide_Notification_bar.txt`.
- Đã kiểm chứng sau reboot (`GRANT_OK 5`, `mDisabled1=0x3a50000`). Chưa test: cài lại APK không reboot, rút/cắm cáp lúc app chạy.

## Build / Tổng hợp Toán (2026-10-08)
- [ ] Unity: Tools → Build → Optimize Android Textures (áp dụng) (user tự chạy), kiểm tra ảnh không mờ/vỡ; đo lại thời gian build — (2026-10-08)
- [ ] Build APK Release (Tools/Build/Profile/Release) rồi test K02: game Tổng hợp Toán (Ôn tập chung) + không còn console lỗi — (2026-10-08)
- [ ] `GameRegistry.cs`: (0,17)/(0,18) bị gán 2 lần (SubNumber5/SubNumber vs SoSanhSoChuSo/SoSanhSo2DauChuSo) → đổi index; index trống nhóm 0: 5, 7, 12 — (2026-10-08)

