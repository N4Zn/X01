# LiDAR touch, tune config và Calib vùng tương tác

> **TL;DR**: LidarTouchBridge.cs bắn PointerEventData trong process Unity từ liblidar_unity.so; config calib nằm TRÊN THIẾT BỊ (lidar_config.json, không theo APK); Calib dùng 5 trụ xốp, người chọn cụm trên tablet.
> **Đọc khi**: LiDAR không nhận/lệch, sửa LidarTouchBridge, native liblidar.cpp, CalibActivity/CalibScene, tune config máy chiếu mới.
> **Cập nhật**: 2026-10-04 (tách nguyên văn từ CLAUDE.md gốc, chưa sửa nội dung)

### LiDAR touch — trong-process Unity, không qua OS injection

`LidarTouchBridge.cs` (Singleton, `Assets/Game/Scripts/UIScripts/Common/`) đọc dữ liệu qua
`liblidar_unity.so` (native plugin port từ `MyNativeApp_v4`, xem `NativePlugins/LidarUnity/`) rồi
tự bắn `PointerEventData` vào `EventSystem` — y hệt 1 cú chạm thật, KHÔNG qua Android
InputManager/AccessibilityService/`dispatchGesture`/`injectInputEvent` (không cần root/quyền OS
đặc biệt). USB bridge (`LidarUsbBridge.java`,
`Assets/Plugins/Android/lidarlib-release.aar`) đọc CP2102 UART, gọi
`UnitySendMessage("LidarTouchBridge", "OnUartFdReady", fd)`.

- **Nút cứng bật/tắt** (van an toàn — LiDAR có thể bắn touch rất nhanh/nhiều): `KeyCode.JoystickButton0`
  (xác nhận thực tế trên K02, KHÔNG phải `KeyCode.Return`). Mặc định OFF lúc khởi động.
- **Vòng tròn debug touch** (thay "Show taps" của Android — không dùng được trên K02):
  `showTouchIndicator`/`indicatorColor`/`indicatorDiameter` trên `LidarTouchBridge` — hiện đang
  màu đỏ, x1.5 kích thước gốc.
- **Config calib** (`half_x`, `hight_floor`, `ymax`, `shift_x/y`, `offset_angle`, ...) — file JSON
  editable trên máy KHÔNG cần rebuild: `/sdcard/EduXplore/lidar_config.json` (SharedStorage; bản cũ ở persistentDataPath được chép sang lần đầu), gọi
  `LidarTouchBridge.Instance.ReloadConfig()` hoặc restart app để áp dụng.
  - **Máy chiếu #1** (đã xác nhận hoạt động trước 2026-09-17): `{half_x:1130,
    hight_floor:1350, ymax:-600, shift_x_floor:1, shift_y:1, shift_x:1, offset_angle:-5,
    nums_point_report:2}`. **Bản đẩy lại 2026-10-03 (user xác nhận "work" trên K02 đang nối):
    `shift_x_floor/shift_y/shift_x` = 0** (không phải 1) — backup `lidar_config.json.projector1_20261003`.
  - **Máy chiếu #2 (đang dùng hiện tại, 2026-09-18, tune bằng `MyNativeApp_v3` — xem section
    riêng bên dưới)**: `{half_x:1190, hight_floor:1380, ymax:-800, shift_x_floor:-110,
    shift_y:-15, shift_x:-35, offset_angle:-5, nums_point_report:2}`. Khớp đúng backup mới nhất
    `lidar_config.json.new_20260918_v2` — không có gì mới hơn chưa ghi lại.
  - Backup cả 2 bộ (+ mọi lần đổi khác, gồm cả 1 bộ nháp `new_20260917_projector_2340x1400`
    không dùng) lưu tại `NativePlugins/K02DeviceConfig/backups/`.
  - **Config nằm trên TỪNG THIẾT BỊ, không trong APK/project** — nạp bản Unity mới / máy K02 mới
    thì file KHÔNG theo: app tự tạo bản MẶC ĐỊNH (`half_x 1000, hight_floor 0, ymax 1500, shift_* 0,
    offset_angle 0, nums_point_report 1` — backup `lidar_config.json.k02_pulled_20261003_default`),
    LiDAR "không nhận" vì điểm rơi lệch/ngoài màn hình (đã xảy ra thật 2026-10-03, đẩy đúng bộ
    máy chiếu vào là work). `interaction_area_calib.json` cũng mất, về chưa-calib. Phục hồi:
    `MSYS_NO_PATHCONV=1 adb push <file> /sdcard/EduXplore/lidar_config.json`
    (package K02 hiện là `X01a`; **Git Bash PHẢI có `MSYS_NO_PATHCONV=1` trước lệnh** `adb push/
    cat/exec-out` có đường dẫn `/sdcard/...`, không thì bị đổi thành `C:/Program Files/Git/sdcard`),
    rồi **đóng hẳn app mở lại** (config chỉ đọc lúc khởi động).
  - Khi LiDAR không nhận, kiểm tra theo thứ tự: (1) config có phải mặc định không; (2) nút cứng
    `JoystickButton0` đã bật LiDAR chưa (mặc định OFF); (3) K02 còn app `com.example.mynativeapp_v1`
    (MyNativeApp, `LidarService` foreground) chạy nền cùng có quyền USB CP2102 — nghi tranh cổng
    UART, chưa xác nhận (lần 2026-10-03 nguyên nhân là config).
- `GameControlBridge.OnPauseRequested/OnResumeRequested`/`OnStopRequested` đều gọi
  `LidarTouchBridge.SetTouchEnabled()` — dùng CHUNG cờ với nút cứng, nên nút cứng có thể ghi đè
  trạng thái Pause của control panel nếu bấm không đúng lúc (biết trước, chưa fix — hỏi trước
  khi đụng nếu cần tách riêng 2 cờ).

### Công cụ tune `lidar_config.json` — `MyNativeApp_v3` (2026-09-18)

**Project RIÊNG, NGOÀI repo này** — `D:\X_projects\DangLHb\MyNativeApp_v3` (Android Studio,
Gradle, KHÔNG phải Unity — tiền thân của `NativePlugins/LidarUnity` trong chính repo này, package
`com.example.mynativeapp_v1`, cài song song trên K02 không đụng gì tới app EduXplore chính).
Dùng để **tune nhanh** `half_x/hight_floor/ymax/shift_x_floor/shift_y/shift_x/offset_angle/
nums_point_report` bằng mắt — nhập số → Lưu → áp dụng NGAY (không cần khởi động lại) → sang màn
hình xem chấm hiện trực tiếp theo cảm biến — rồi báo số liệu để ghi vào `lidar_config.json` thật
của app chính (2 nơi lưu HOÀN TOÀN TÁCH BIỆT, không tự đồng bộ).

- `MainActivity` — form nhập tay 8 tham số, nút "Lưu" gọi thẳng `sendLidarConfig()` (native, áp
  dụng sống) + lưu `SharedPreferences`. Chọn cổng UART + "Connect" → mở `ThirdActivity`.
- `ThirdActivity` + `DotView` — màn hình full-screen vẽ chấm tại vị trí LiDAR phát hiện.
- **3 lần sửa thật trên K02 (2026-09-18)**, xem code + comment tiếng Việt tại chỗ để biết chi tiết:
  1. **Tắt hẳn vuốt** (`sendSwipe()` → no-op tuyệt đối, `liblidar.cpp`) — LiDAR bắt chuyển động
     lúc di chuyển đặt trụ bị phân tích nhầm thành vuốt, tiêm cả chuỗi MOVE thật vào
     `/dev/input/event3`, gây thao tác/lỗi giao diện ngoài ý muốn.
  2. **Chế độ CHỈ HIỂN THỊ** (mặc định BẬT, `g_display_only_mode`) — `sendTouch()` không còn ghi
     `/dev/input/event3` (không tạo touch OS thật nữa, tránh nhiễu hệ thống) — thay bằng
     `ThirdActivity` POLL native định kỳ (`nativeGetAllPoints()`, 60fps) để tự vẽ.
  3. **Tốc độ + vẽ đồng thời**: `CLUSTER_ANALYSIS_INTERVAL` 100ms→20ms (nghẽn cổ chai chính —
     tầng đọc UART/parse KHÔNG có delay nhân tạo, cửa sổ tích luỹ điểm `MAX_POINT_LIFE_SEC` 300ms
     giữ nguyên nên không mất điểm). Thêm hàng đợi lịch sử hiển thị RIÊNG (`g_touch_history`,
     tách biệt bộ đệm gom cụm) — tối đa 600 điểm, đầy thì tự xoá điểm CŨ NHẤT (FIFO), vẽ HẾT
     đồng thời mỗi khung hình thay vì 1 chấm nhảy giữa các vị trí. Chấm cũng chỉnh nhỏ lại 3 lần
     (`DOT_RADIUS` 10→3.3).
- **Kết quả tune 2026-09-18** (máy chiếu mới) đã ghi vào `lidar_config.json` thật — xem mục
  "Config calib" phía trên.

### Calib "vùng tương tác" — icon riêng bằng 5 trụ xốp (2026-09-17)

Icon "Calib" RIÊNG trong APK tổng — khớp ô lưới "Calib khi có" đã để sẵn trong Launcher thật của
K02 (xem đầu file), **KHÔNG** lồng trong menu chọn game của `ControlActivity`. Bù lệch/xoay/co
giãn do máy chiếu lắp đặt từng phòng khác nhau — giáo viên tự làm lại bất cứ lúc nào, không cần
hiểu gì về hình học cảm biến. Tách bạch 2 tầng calib:
- **Tầng vật lý cảm biến** (`lidar_config.json` — `half_x/hight_floor/offset_angle/...`, xem mục
  LiDAR touch phía trên) — kỹ thuật viên chỉnh 1 lần lúc lắp, KHÔNG đụng ở đây.
- **Tầng "vùng tương tác"** (mới, file riêng `interaction_area_calib.json`) — 1 phép affine áp
  SAU khi đã có toạ độ màn hình thô, giáo viên tự làm.

**UX**: giáo viên đặt **5 trụ xốp tròn tĩnh** (Ø~5cm, cao ~5cm — cao hơn vùng quét LiDAR) vào
4 góc + tâm vùng chiếu (theo 5 vòng tròn vàng CalibScene chiếu lên sàn), quay lại tablet bấm
**"Bắt đầu calib"** — không đứng lại lên từng điểm (khác bản nháp đầu, đứng 2 chân cho toạ độ
không chính xác). Trụ tĩnh hợp với bộ lọc ổn định của native (ưu tiên vật thể đứng yên, xem
`LidarProcessor.cpp`/`native-lib.cpp`) hơn hẳn chân người, và `find_toe_points()`
(`liblidar.cpp`) vốn đã trả về 1 sự kiện cho MỖI cụm đạt `min_cluster_size` (hardcode = 3, xem
`native-lib.cpp:355` — **giữ nguyên 3**, chưa hạ xuống 2; nếu 1 trụ ở góc xa không đủ 3 điểm ổn
định thì đây là chỗ cần hạ, đợi user báo lại trước khi sửa) — không cần sửa gì ở tầng native để
nhận cùng lúc 5 vị trí.

**Kiến trúc — 3 phần, TÁCH RIÊNG khỏi ControlActivity/GameControlBridge** (không đụng luồng
Start/Pause/Stop đã test kỹ trên máy thật):
- `CalibActivity.java` (`NativePlugins/ControlUiAndroidLib/controlui/.../CalibActivity.java`) —
  entry point riêng (display 0, MAIN/LAUNCHER thường, không HOME/DEFAULT), tự khởi động
  `UnityPlayerActivity` vào scene `CalibScene` trên display phụ ngay lúc mở (khác
  `ControlActivity` phải chờ chọn game). UI 100% code (giống `ClassManagementActivity`): 3 nút
  Bắt đầu calib / Lưu / Thoát + 1 dòng trạng thái — không dựng lại `findSecondaryDisplay()`/
  Unity-launch logic dùng chung với `ControlActivity` (chấp nhận trùng lặp nhỏ để tránh rủi ro
  đụng code đã test).
- `LidarTouchBridge.cs` (phần cuối file) — API capture: `StartCalibrationCapture()` mở cửa sổ
  lắng nghe 2.5s, gom điểm thô thành cụm (`ClusterPoints`), gán 5 cụm → 5 vai trò bằng vị trí
  tương đối so với trọng tâm chung (`AssignRoles` — KHÔNG dựa vào calib cũ, tránh gán sai khi máy
  chiếu đang lệch nặng), giải affine tối thiểu bình phương (`SolveAffine`, Cramer's rule thuần
  C#, không cần thư viện ngoài) rồi lưu vào `interaction_area_calib.json`
  (`CommitPendingCalibration()` — chưa lưu ngay lúc capture xong, chờ giáo viên xem bước xác
  nhận trực quan rồi mới bấm Lưu).
- `CalibSceneController.cs` + `CalibControlBridge.cs`
  (`Assets/Game/Scripts/UIScripts/Calib/`) — scene `CalibScene` (Unity, display máy chiếu),
  toàn bộ UI (Canvas, 5 vòng tròn mục tiêu, chấm xác nhận) dựng bằng code lúc runtime — GIỐNG
  `GameControlBridge.CreateBlankOverlay()` — nên file `.unity` chỉ cần đúng 1 GameObject gắn
  script, không cần dựng UI tay trong Editor. `CalibControlBridge` là bridge 2 chiều RIÊNG với
  `GameControlBridge` (GameObject tên `"CalibControlBridge"`, gọi ngược
  `AndroidJavaClass("com.eduxplore.control.CalibActivity")`) — cố ý KHÔNG tổng quát hoá
  `GameControlBridge.PushReport/PushGameEnded` (đang hardcode gọi `ControlActivity`) để dùng
  chung, vì đụng vào đó là đụng luồng Start/Pause/Stop đã test kỹ.

**Chưa làm/cần làm tiếp**:
- Wiring Launcher thật (`D:\X_projects\Launcher`, ngoài repo) — thêm hằng số
  `CALIB_COMPONENT` trỏ `CalibActivity`, gán vào ô lưới "Calib khi có", build lại + cài lại
  Launcher (chưa làm trong phiên này, project riêng ngoài repo).
- Scene `CalibScene.unity` được viết tay (không qua Unity Editor — không có quyền chạy Editor
  lúc code) dựa theo đúng format 1 scene tối giản có thật trong repo (`Assets/_Test/Animated.unity`
  cho phần boilerplate OcclusionCulling/RenderSettings/LightmapSettings/NavMeshSettings, và
  `TestTongHopGame.unity` cho format block MonoBehaviour) — **cần mở Unity Editor 1 lần để xác
  nhận scene load sạch, không lỗi Console**, trước khi build thật.
- Build `controlui-release.aar` đã chạy lại (`gradlew :controlui:assembleRelease`) và copy đè
  `Assets/Plugins/Android/controlui-release.aar` — nhưng CHƯA build/test APK tổng thật trên máy
  K02.
- Việc verify trên máy thật: 1 trụ Ø5cm ở góc xa ROI có đủ tạo ≥3 điểm LiDAR ổn định không (xem
  `min_cluster_size` ở trên); cửa sổ lắng nghe 2.5s đủ dài chưa.

**2 bug thật đã tìm + sửa qua debug logcat trực tiếp trên K02 (2026-09-17)**:
- **LiDAR touch không tự bật khi vào Calib** — mặc định OFF lúc khởi động app (van an toàn, nút
  cứng `JoystickButton0`), giáo viên vào Calib không biết phải bật nút đó trước → 0 điểm, luôn
  báo "chưa đạt" dù đặt đúng trụ. Đã fix: `CalibSceneController.BeginCapture()`/
  `CaptureSequentialStep()` tự gọi `LidarTouchBridge.SetTouchEnabled(true)` ngay trước khi đo,
  tắt lại (`false`) ngay sau khi đo xong/huỷ — giáo viên không cần biết cơ chế ẩn này.
- **Phòng nhỏ → quét dính tường** — log thật cho thấy toạ độ "ổn định" ghim gần biên xa ROI, Y
  nhảy khắp dải (đặc trưng quét trúng 1 mặt phẳng lớn/tường, không phải 1 trụ nhỏ). Vùng quét
  (`half_x`/`hight_floor`/`ymax` trong `lidar_config.json`) đang rộng hơn kích thước phòng thật.
  **Fix (theo đề xuất user)**: mỗi `CalibTarget` giờ có thêm `expectedRawMm` (Vector2? — toạ độ
  MM thật, đo trực tiếp ngoài đời bằng thước/laser theo hệ trục LiDAR, KHÔNG suy từ
  `lidar_config.json` vì chính config đó có thể đang lệch) — `LidarTouchBridge.FilterNearHints()`
  chỉ giữ điểm thô trong bán kính `HintWindowRadiusMm` (250mm) quanh gợi ý đó trước khi gom cụm,
  loại thẳng nhiễu ở xa (tường) mà không cần chỉnh lại `lidar_config.json` (chỉnh config đó sẽ
  ảnh hưởng CẢ game bình thường, không riêng calib). `MmToRawScreen()` mô phỏng lại đúng công
  thức `convert_to_1024x600()` của native bằng C# để quy đổi gợi ý mm → toạ độ màn hình thô,
  dùng `_config` (half_x/hight_floor/ymax/shift_x/shift_y) đang áp dụng — CHỈ dùng để khoanh
  vùng lọc, không dùng để tính calib (tính calib vẫn dựa 100% vào điểm LiDAR đo được thật).
  Target nào chưa có `expectedRawMm` (chưa đo) thì bỏ qua bộ lọc cho điểm đó, không lỗi.
  **Trạng thái đo** (`CalibSceneController.TargetHintsMm`) — ĐỦ 5/5, đo thật 2026-09-17:
  TopLeft(-1200,-1100), TopRight(900,-1100), BottomLeft(-1200,-2100), BottomRight(900,-2100),
  Center(-150,-1600) (= trung điểm 4 góc, không đo riêng). X trải rộng 2100mm, Y trải sâu
  1000mm — hình chữ nhật khá đều. Đo lại/chỉnh trực tiếp trong dictionary đó nếu sau này lắp
  lại máy chiếu/phòng khác. **Không còn dùng để lọc cứng** (xem 2 mục dưới) — chỉ còn tác dụng
  log so sánh ở chế độ 5-trụ-cùng-lúc.

### Calib — 3 bug thật tìm thêm qua debug logcat trực tiếp (2026-09-17, sau phiên đầu)

Sau khi build bản có `TargetHintsMm` đủ 5/5, test thật trên K02 vẫn "chưa đạt" — debug tiếp qua
logcat trực tiếp (không đoán) phát hiện 3 lớp vấn đề chồng lên nhau:

1. **Lọc cứng theo mm SAI hoàn toàn** — `MmToRawScreen()` dùng CHÍNH hình học `lidar_config.json`
   (đang lệch, đó LÀ lý do cần calib) để đoán "điểm mm này sẽ hiện ở đâu trên màn hình" — vòng
   lặp logic: dùng cái đang sai để lọc, ra cửa sổ lọc sai theo, loại nhầm sạch cụm đúng (xác nhận
   thật: lệch tới ~1900px). **Đã bỏ lọc cứng theo mm hoàn toàn.**
2. **"Chọn cụm nhiều điểm nhất" cũng sai** — phòng nhỏ, tường/vật tĩnh trong tầm quét phản xạ
   MẠNH và ỔN ĐỊNH hơn hẳn 1 trụ nhỏ, luôn thắng "nhiều điểm nhất" dù không phải trụ (xác nhận
   thật: 4/5 điểm đo ra cùng 1 vị trí bất kể trụ đặt đâu). **Đã bỏ tự chọn** — thay bằng đưa HẾT
   các cụm đạt ngưỡng (`MinSamplesPerCluster`, hạ từ 4→3 theo yêu cầu thật) lên máy chiếu **đánh
   số** (`CalibSceneController.DrawCandidateMarkers`, màu xanh dương), giáo viên tự chọn đúng số
   trên tablet (`CalibActivity.showCandidatePicker` → `OnCandidateChosen`) — không có thuật toán
   nào phân biệt "trụ" với "vật tĩnh" đáng tin, chỉ người đứng đó mới biết. Thêm lọc thô **góc
   phần tư màn hình** (`LidarTouchBridge.InExpectedQuadrant` — so với tâm màn hình, KHÔNG dùng
   mm) để loại bớt ứng viên rõ ràng sai phía trước khi đưa lên chọn, có dự phòng: nếu lọc rỗng
   hết thì bỏ qua bộ lọc (không chặn cứng lỡ hình học lệch nặng tới mức cả trụ thật cũng "sai
   phía"). `StartSinglePointCapture()` đổi tham số từ `Vector2? expectedMm` sang `string role`.
3. **Bug gốc rễ thật, ở NATIVE** — `sendTouch()` ([liblidar.cpp](NativePlugins/LidarUnity/src/liblidar.cpp))
   dùng **1 mốc thời gian throttle TOÀN CỤC** (`g_last_touch_call_ms`) cho mọi điểm — nghĩa là hễ
   đẩy xong 1 điểm thì KHOÁ 150ms tiếp theo cho MỌI vị trí khác luôn, không riêng vị trí vừa đẩy.
   Xác nhận qua log thật: 1 vật tĩnh (tường) ra tín hiệu ~400 lần/6s trong khi trụ chỉ ~40
   lần/6s (native VẪN "nhìn thấy" trụ ở tầng gom cụm, chỉ là không bao giờ thắng nổi suất đẩy 1
   lần/150ms khi phải cạnh tranh với tường). **Đã sửa**: đổi sang throttle theo TỪNG vị trí
   (`g_recent_sends`, gộp trong bán kính `SAME_TOUCH_MERGE_RADIUS=100px`) — các touch KHÁC NHAU
   không còn tranh giành 1 suất chung, chỉ CÙNG 1 vị trí mới bị giãn cách 150ms như thiết kế gốc
   (mục đích ban đầu: tránh dội SurfaceFlinger, KHÔNG phải để loại bớt điểm). Đây là sửa ảnh
   hưởng **CẢ game bình thường** (multi-touch 2 người chạm đồng thời), không riêng calib — đã
   rebuild `liblidar_unity.so` (`ninja` trong `NativePlugins/LidarUnity/build/arm64-v8a/`, NDK
   27.0.12077973 + CMake 3.22.1 có sẵn trên máy) và copy đè
   `Assets/Plugins/Android/libs/arm64-v8a/liblidar_unity.so` — **cần test lại cả touch thường
   (2 người chạm cùng lúc) lẫn calib** sau khi cài bản mới.

### Tự bật Lidar khi vào game (2026-10-05)
`LidarTouchBridge` lắng nghe `SceneManager.sceneLoaded`: scene nào không nằm trong `NonGameScenes` (Start/Menu/Home/Calib/CharacterSelect/TeamSelect/Score/FRTest) thì `SetTouchEnabled(true)`. Trước đây chỉ `MiniGameControllerBase` + `TestTongHopController` tự bật, game cũ khác để mặc định OFF nên không nhận chạm. Thêm scene không phải game mới → thêm tên vào `NonGameScenes`.
