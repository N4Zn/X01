# CLAUDE.md — EduXplore 2.0

Derived from: `EduGame/edu-game` (EduXplore v1).
Unity 2022.3.62f1 · IL2CPP · Android primary target.

## Deployment context

**Floor projection setup** — projector chiếu xuống sàn nhà, người chơi đứng/nhảy tương tác.
- Landscape locked (LandscapeLeft)
- Multi-touch bắt buộc
- Screen never sleeps (`LauncherBehaviour.keepScreenAwake = true`)
- App là **Android HOME launcher** — không thoát được bằng Back button
  > **K02 dual-display (Track A, xem section riêng bên dưới)**: `ControlActivity` (entry point
  > mới trên K02) hiện là launcher thường (`MAIN`/`LAUNCHER`), **KHÔNG** đăng ký `HOME`/`DEFAULT`
  > — chủ động tắt theo yêu cầu lúc test. Vai trò HOME thật của K02 giờ do 1 **app Launcher riêng**
  > đảm nhiệm (`D:\X_projects\Launcher`, ngoài repo này — xem section "Launcher — Home launcher
  > thật của K02" bên dưới), gọi `ControlActivity` qua 1 trong 2 "hero card" của nó chứ không tự
  > đăng ký HOME. Muốn quay lại cách cũ (chính `ControlActivity` tự làm HOME): thêm `HOME`+
  > `DEFAULT` category vào intent-filter của nó trong
  > `NativePlugins/ControlUiAndroidLib/controlui/src/main/AndroidManifest.xml`.

## Điểm khác so với v1

| | v1 (edu-game) | v2 (eduXploreGame2.0) |
|---|---|---|
| Package | com.EduCompany.EduGame | com.EduXplore.EduXploreGame20 |
| Launcher | Không | Có (HOME + DEFAULT intent) |
| Scene đầu | StartScene → HomeScene → Menu | StartScene → MenuScene thẳng |
| Back button | Thoát app | Về MenuScene / noop ở menu |
| Screen sleep | Mặc định | Never sleep |

## Scene flow

```
StartScene (load CSV + TongHopConfig + AssetOverrides)
    └── MenuScene  ← màn hình chính, không thoát được
            ├── TestTongHopGame  (game chính — Choose + Matching)
            ├── AddUpGame
            ├── TrainPathGame
            └── ... (thêm dần)
```

## Key scripts

- `LauncherBehaviour.cs` — gắn vào MenuScene: chặn Back, giữ màn hình sáng, lock landscape
- `StartMainController.cs` — load data rồi LoadScene("MenuScene") thay vì HomeScene
- `AndroidManifest.xml` — `Assets/Plugins/Android/` — HOME + DEFAULT category

## Architecture (kế thừa từ v1)

**MVC per scene** · **Custom FSM** (`CustomFSMManager.cs`) · **Singleton pattern**
**GameSessionManager** — cross-scene data (mode, player names, scores)
**TongHopConfig** — JSON tunable config tại `StreamingAssets/TestTongHop/gameconfig.json`
**GameLogger** — 3-tier logging: Click → Round → Session → JSON file

## Canvas

Reference resolution: **1024×600**, ScreenSpaceOverlay.
> Nếu projector là 1920×1080, scale UI bằng `Canvas Scaler → Match Width Or Height = 0.5`.

## Build

- **Target SDK**: 34 · **Min SDK**: 22 · **IL2CPP**
- Keystore: `Keystore/edugame.keystore` (alias: `edugame`)
- Package: `com.EduXplore.EduXploreGame20`
- **QUAN TRỌNG**: Sync CSV sau khi sửa:
  ```
  Assets/MasterData/Csv/*.csv  →  Assets/StreamingAssets/MasterData/Csv/
  ```

## Track A — K02 dual-display + LiDAR touch (session "Dual display setup K02")

Deployment thứ 2 song song với luồng MenuScene gốc ở trên: tablet **K02** xuất HDMI ra máy
chiếu, dùng **2 display độc lập** (không mirror) + **LiDAR** làm nguồn touch sàn thay cho
tay chạm màn hình. Toàn bộ phần dưới đây chỉ áp dụng cho deployment K02 — không đụng tới
luồng MenuScene/StartScene gốc.

### Kiến trúc 2 Activity, 2 display

- **`ControlActivity`** (Java, `NativePlugins/ControlUiAndroidLib/`, đóng gói thành
  `Assets/Plugins/Android/controlui-release.aar`) — entry point mới trên K02, chạy **display 0**
  (màn hình tablet). Android View thuần (KHÔNG WebView — Chromium không init được GL context
  trên K02, lỗi driver GPU tầng hệ thống, đã xác nhận thực tế, không sửa được từ app). Menu chọn
  game + panel Pause/Stop/report lúc đang chơi, 2 View con trong CÙNG Activity (không phải 2
  Activity).
- **`UnityPlayerActivity`** — chạy **display phụ** (máy chiếu), khởi động qua
  `ActivityOptions.setLaunchDisplayId()` từ `ControlActivity.onStartClicked()`. Đã bỏ
  `MAIN`/`LAUNCHER` khỏi manifest của nó (`Assets/Plugins/Android/AndroidManifest.xml`) —
  `ControlActivity` là entry point duy nhất giờ.
- **`ControlBridge.cs`** (Unity, `Assets/Game/Scripts/UIScripts/Common/`) — đọc scene+tên game
  qua Intent extra lúc cold-boot (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`), set
  `GameSessionManager.SelectedGameName` rồi `SceneManager.LoadScene()` thẳng vào game đã chọn
  (không qua MenuScene). `LoadGame(sceneName, gameName)` là entry point dùng chung — cả cold-boot
  lẫn lúc Unity đã sống sẵn (xem bên dưới) đều gọi qua đây.

**Giao diện `ControlActivity` (2026-09-17)** — nền **sáng**, đồng bộ bảng màu với Launcher (xem
section riêng bên dưới) — toàn bộ màu đi qua token trong `res/values/colors.xml` của
`ControlUiAndroidLib` (`bg/panel/text/accent/good/live/bad/...`), đổi giá trị token là đổi hết cả
UI, không hardcode hex rải rác trong Java (2 chỗ hardcode còn lại — `0xFF0B1710`, chữ đen trên nút
START/Chơi lại nền xanh `good` — không cần đổi, không phụ thuộc theme sáng/tối). Có logo EduXplore
góc trên-trái top bar (`@drawable/logo_eduxplore_transparent` — bản NỀN TRONG SUỐT, khác
`@drawable/logo_eduxplore` bản NỀN ĐẶC/navy dùng full-bleed cho `showLogoOnSecondaryDisplay()` ở
máy chiếu — **đừng nhầm 2 file, dùng sai bản sẽ dính khung màu**). Chữ "Môn học"/"Lớp" và giá trị
đang chọn phóng to (21sp, trước là 12.5sp) vì đây là tiêu đề chính của màn hình.

**Tên game hiển thị ≠ định danh nội bộ** — `GameRegistry.cs`'s `GameEntry` giờ có 2 field tách
biệt: `name` (định danh nội bộ ổn định — khoá tra icon `Assets/Resources/GameIcons/`, khoá CSV
variant, giá trị gửi Unity qua `EXTRA_GAME_NAME`/`OnLoadGameRequested` — **không đổi khi sửa câu
chữ**) và `displayName` (tiếng Việt, hiện lên UI). Mirror `game_registry.json`
(`NativePlugins/ControlUiAndroidLib/controlui/src/main/assets/`) đã thêm field `displayName`
tương ứng — sửa tên hiển thị thì sửa Ở CẢ 2 FILE (C# + JSON) để không lệch. `ControlActivity.java`
dùng `item.displayName` để vẽ danh sách chọn game, nhưng vẫn giữ `selectedGameName`/`item.name`
(định danh nội bộ) cho toàn bộ logic chọn/gửi Unity — chỗ hiển thị tên game ở nơi khác
(pause card, banner, summary...) tra ngược qua `displayNameOf(selectedGameName)`.

### Launcher — Home launcher thật của K02 (`D:\X_projects\Launcher`, 2026-09-16/17)

**Project RIÊNG, KHÔNG nằm trong `eduXploreGame2.0`** — 1 app Android launcher (Kotlin, không
Unity) đăng ký `HOME`+`DEFAULT`, dự định là launcher mặc định thật của tablet K02 (khác
`ControlActivity`, chỉ là `MAIN`/`LAUNCHER` thường — xem ghi chú đầu file). Màn hình chính: 2
"hero card" lớn (Game, Quản lý lớp) + lưới icon nhỏ (Files, Cài đặt luôn hiện mặc định, Calib khi
có). Nền sáng (gradient xanh dương-cam-xanh lá), logo EduXplore, admin ẩn (nhấn góc trên-phải 5
lần → PIN `0000` → chọn app nào hiện/ẩn) — PIN hiện đang hardcode, đổi được ở
`MainActivity.ADMIN_PIN`.

- **2 hero card KHÔNG phải app riêng** — trỏ thẳng vào 2 Activity cụ thể **bằng ComponentName**
  trong CÙNG package `com.EduXplore.X01a` (kiến trúc 3-app-1-APK): Game → `ControlActivity`
  (label manifest đã đổi thành `"EduGame"`), Quản lý lớp → `ClassManagementActivity` (xem section
  trên). Hardcode ở `MainActivity.kt`'s `GAME_COMPONENT`/`ROSTER_COMPONENT` — **đổi Activity nào
  đó thành entry point mới (như đã làm với FA) thì PHẢI sửa 2 hằng số này + build lại + cài lại
  Launcher, không tự động theo**.
- **Pin lưu theo ComponentName cụ thể (`package/activity`), không phải theo package** —
  `PinnedAppsManager`/`AdminActivity` cố ý làm vậy vì 2 Activity khác nhau (Game, Quản lý lớp)
  chung 1 package, chỉ lưu theo package sẽ đè lẫn nhau. Hệ quả: **đổi `ROSTER_COMPONENT`/
  `GAME_COMPONENT` sang Activity khác → key cũ trong `pinned_components` (SharedPreferences)
  không tự migrate** — máy nào đã tick từ trước phải vào lại admin, **bỏ tick rồi tick lại 1
  lần**. Đã xảy ra thật 1 lần (2026-09-16): đổi `ROSTER_COMPONENT` xong quên cài lại Launcher +
  quên re-tick, hero card 2 vẫn mở nhầm Activity cũ.
- **Gotcha admin picker "kẹt pin không gỡ được"** (đã fix) — `AdminActivity.loadAllApps()` liệt
  kê app bằng `queryIntentActivities(MAIN+LAUNCHER)`, chỉ thấy Activity nào ĐANG có launcher
  intent-filter. Nếu 1 Activity đã pin từ trước bị gỡ intent-filter (như `MainActivity` của FA
  khi chuyển launcher-icon sang `ClassManagementActivity`) thì nó biến mất khỏi danh sách admin —
  **không có cách bỏ tick qua UI, kẹt vĩnh viễn**. Đã sửa: `loadAllApps()` giờ liệt kê thêm cả
  component đã pin nhưng không còn launcher-activity (đánh dấu `"(ẩn)"`, tra label qua
  `pm.getActivityInfo()`, fallback icon `pm.defaultActivityIcon` nếu app đã gỡ hẳn) — **bất kỳ
  Activity nào sau này bị đổi/gỡ launcher-icon đều vẫn gỡ pin được bình thường, không cần sửa gì
  thêm**.
- **Test bằng bản debug** (`com.launcher.eduxplore.debug`, package suffix riêng, không đụng
  launcher thật nếu có) — set làm Home để test: `adb shell cmd role add-role-holder
  android.app.role.HOME com.launcher.eduxplore.debug`. Kiểm tra Home hiện tại:
  `adb shell cmd package resolve-activity -a android.intent.action.MAIN -c
  android.intent.category.HOME --brief`.
- **Logo dùng chung**: `logo_eduxplore.png` bản NỀN TRONG SUỐT (tách nền từ
  `Assets/Game/Resources/ui/splash/Logo_Full.png` bằng color-key vì nền gốc phẳng 1 màu) — đã copy
  sang cả `ControlUiAndroidLib` (`logo_eduxplore_transparent.png`, xem trên) và
  `FaceEnrollAndroidLib` (`logo_eduxplore.png`, ghi đè tên trùng bản navy đặc cũ — module này
  KHÔNG có nhu cầu full-bleed navy nên ghi đè thẳng, khác `ControlUiAndroidLib` phải giữ 2 bản).

### "Quản lý lớp" — ClassManagementActivity (2026-09-16)

Entry point launcher của FA đổi từ `MainActivity` (màn camera) sang **`ClassManagementActivity`**
(mới) — danh sách lớp → danh sách học sinh trong lớp → xem/quản lý ảnh 1 học sinh. Build 100%
bằng code (LinearLayout lồng nhau, không RecyclerView/XML item layout), cùng phong cách
`showManageStudentsDialog()`/`showStudentSamplesDialog()` đã có sẵn trong `MainActivity.kt`.
`MainActivity` vẫn giữ nguyên toàn bộ pipeline camera/nhận diện/enroll — không viết lại, chỉ
điều khiển qua Intent extras từ `ClassManagementActivity`. Giao diện nền **sáng** (đồng bộ Launcher/
ControlActivity, 2026-09-17) + logo EduXplore góc trên-trái — màu hardcode trực tiếp trong Kotlin
(`cBg/cCard/cText/cAccent/...` khai ở đầu class), không qua `colors.xml` như `ControlUiAndroidLib`
(toàn bộ UI của Activity này vốn dựng 100% bằng code, không XML, nên không có chỗ để đặt token).
`MainActivity` (màn camera) vẫn giữ nguyên theme tối cũ — chưa đồng bộ, chưa ai yêu cầu.

- `EXTRA_TARGET_CLASS` (String) — lớp đang chọn; enroll người MỚI trong phiên này sẽ tự gán
  `className` = giá trị này (xem `showEnrollNameDialog()`'s Lưu action).
- `EXTRA_MODE` = `MODE_VIEW_STUDENT` + `EXTRA_STUDENT_NAME` — mở thẳng
  `showStudentSamplesDialog(name)` lúc `onCreate()` (màn "Cập nhật ảnh").
- `EXTRA_MODE` = `MODE_BULK_PICK` — mở thẳng picker chọn NHIỀU ảnh (`GetMultipleContents`,
  khác launcher đơn `GetContent` đã có) rồi chạy tuần tự qua đúng `enrollFromPhoto()` cho từng
  ảnh (`enrollFromPhotosBulk()`), nối chuỗi bằng callback `onDone`/`onAllDone` mới thêm vào
  `enrollFromPhoto()`/`enrollNextFace()` — không viết pipeline detect/embed mới.
- Không có extra nào → mở như màn camera bình thường (hành vi cũ, không đổi).

**Dữ liệu**: `enrolled.json` thêm field `"className"` (nullable, bỏ qua an toàn ở code cũ/game —
`FaceDatabase`/`bestMatch()` chỉ đọc `name`+`samples`, không đụng field này). Danh sách lớp
(kể cả lớp rỗng chưa ai) lưu riêng ở `/sdcard/EduXplore/classes.json`. Đổi tên lớp
(`AttendanceStore.renameClass`) và đổi tên học sinh (`AttendanceStore.renameEnrollment`, di
chuyển key qua mọi map: `enrolled`/`genders`/`classNames`/`lastLogged`/`personLog`/`recentNames`)
đều có sẵn.

> **Gotcha Launcher**: `MainActivity.kt` (Launcher app, `ROSTER_COMPONENT`) đã trỏ sang
> `ClassManagementActivity` thay vì `MainActivity` của FA — nhưng key lưu trong
> `PinnedAppsManager` là **component cụ thể** (`package/activity`), nên máy nào đã từng tick
> chọn "Quản lý lớp" từ trước (lúc còn trỏ `MainActivity`) cần **vào lại admin bỏ tick rồi tick
> lại** 1 lần để lưu đúng component mới — tự nó không migrate.

> **Chưa áp dụng cho bản standalone**: `ClassManagementActivity` mới chỉ có ở bản merge
> (`NativePlugins/FaceEnrollAndroidLib`) — bản gốc độc lập `D:\X_projects\FaceRecognition\android`
> chỉ được cập nhật phần dữ liệu (`AttendanceStore.kt`: className/classes.json/rename) +
> `MainActivity.kt` (intent extras, bulk picker) để 2 bản không lệch nhau, nhưng KHÔNG có màn
> hình "Quản lý lớp" riêng — app đó vẫn chỉ có màn camera như cũ.

### HDMI mirror lúc boot/launcher/FA — bug OEM, đã fix (2026-09-16)

Yêu cầu thực tế: lúc boot / ở launcher / mở app FA → máy chiếu cần **mirror** đúng nội dung
tablet, **full độ phân giải thật** (không phải 2 display tách biệt — đó chỉ cần khi vào game
Unity, xem `setLaunchDisplayId()` ở trên). Thiết bị K02 chạy **MediaTek MT8168**, build
`userdebug`/test-keys (`adb root` dùng được thẳng, có sẵn `/system/xbin/su` setuid — tự app
cũng gọi root runtime được nếu cần).

**Có 2 tầng clone HDMI độc lập, dễ nhầm là 1:**
1. **Tầng HAL** (`persist.vendor.sys.hdmi_hidl.clone=1`) — MediaTek's HDMI HAL tự mirror display
   chính ra HDMI, tự scale đúng theo độ phân giải thật, tự negotiate lại mỗi lần hotplug (rút/cắm
   cáp). Tầng này **tự nhường** khi có app claim riêng display 1 (lý do Unity vẫn tách màn đúng
   bình thường mà không cần code gì thêm để bật/tắt mirror — platform tự làm đúng cái mình cần).
   **Không đụng vào, không cần sửa gì ở đây.**
2. **Tầng Java OEM** (`/vendor/etc/init/mirror_hdmi.rc` → service `mirror_hdmi`, oneshot, trigger
   đúng 1 lần lúc `sys.boot_completed=1` → `sleep 8` → chạy
   `/data/local/tmp/mirror_hdmi.sh` → `app_process` load `/data/local/tmp/MirrorDisplay.dex` →
   gọi `SurfaceControl.setDisplayLayerStack()`/`setDisplayProjection()` trực tiếp) — đây là
   **nguồn gây bug**: nó hardcode `dst` rect theo giả định resolution ~1280×720, nhưng máy chiếu
   thật negotiate 1920×1080 → đè lên kết quả ĐÚNG của tầng HAL bằng 1 vùng méo/lệch góc. Vì chạy
   1 lần lúc boot, không re-trigger khi hotplug, nên rút/cắm lại cáp HDMI = né được override sai
   này, quay về đúng tầng HAL (đã xác nhận thực tế: rút/cắm cáp lúc đang ở launcher → 2 màn tự
   động sync đúng, full resolution).

**Fix đã áp dụng**: neutralize `/data/local/tmp/mirror_hdmi.sh` thành no-op (`exit 0`) — không
đụng gì tới `/vendor` (đang mount `ro` + overlay, không cần remount). File nằm trên `/data` nên
ghi trực tiếp được, và **sống sót qua reboot** (đã test lại sau reboot: script vẫn no-op, KHÔNG
tự phục hồi). Đã verify: sau reboot, `mirror_log.txt` (nơi `MirrorDisplay.dex` log lại mỗi lần
chạy) không bị ghi thêm gì mới → xác nhận override không còn chạy nữa.

- **Backup/restore**: bản gốc lưu tại
  [`NativePlugins/K02DeviceConfig/mirror_hdmi.sh.orig`](NativePlugins/K02DeviceConfig/mirror_hdmi.sh.orig)
  (bản OEM thật), bản đang deploy tại
  [`NativePlugins/K02DeviceConfig/mirror_hdmi.sh.noop`](NativePlugins/K02DeviceConfig/mirror_hdmi.sh.noop).
  Muốn phục hồi hành vi mirror gốc của OEM (không khuyến khích — có bug góc màn hình):
  ```bash
  adb root
  MSYS_NO_PATHCONV=1 adb push NativePlugins/K02DeviceConfig/mirror_hdmi.sh.orig /data/local/tmp/mirror_hdmi.sh
  adb shell chmod 755 /data/local/tmp/mirror_hdmi.sh
  ```
  Muốn áp lại fix (ví dụ sau khi flash lại firmware/OTA reset `/data`):
  ```bash
  adb root
  MSYS_NO_PATHCONV=1 adb push NativePlugins/K02DeviceConfig/mirror_hdmi.sh.noop /data/local/tmp/mirror_hdmi.sh
  adb shell chmod 755 /data/local/tmp/mirror_hdmi.sh
  adb reboot
  ```
  > Lưu ý: đây là sửa trực tiếp trên **firmware/OS của từng thiết bị K02** (không phải code
  > project), KHÔNG nằm trong APK/build Unity — flash lại ROM hoặc factory reset sẽ mất fix này,
  > phải làm lại bước push script trên cho từng máy.

### Stop/Start — KHÔNG destroy UnityPlayerActivity (gotcha quan trọng)

**Unity tự gọi `Process.killProcess()` cả process (dùng chung với `ControlActivity`) khi
`UnityPlayerActivity` bị destroy** — hành vi mặc định của Unity/IL2CPP trên Android, xác nhận
qua log `Process: Sending signal... SIG: 9` ngay sau khi Unity chạy xong quy trình quit của nó.
Không sửa được từ code app. Vì vậy:

- **Stop** (`GameControlBridge.OnStopRequested`) **KHÔNG BAO GIỜ** finish/destroy
  `UnityPlayerActivity` — chỉ pause game (timer/touch/nhạc) + che đen display máy chiếu bằng 1
  Canvas overlay riêng (`GameControlBridge.ShowBlankOverlay`). Activity vẫn sống nguyên, giống
  hệt việc bấm Home để app chạy nền.
- **Start lần 2 trở đi** không launch Activity mới — gửi `UnitySendMessage("GameControlBridge",
  "OnLoadGameRequested", "scene|gameName")` cho instance Unity đang sống, Unity tự
  `ControlBridge.LoadGame()` load scene mới. `ControlActivity.unityStarted` (boolean) theo dõi
  đã từng Start lần đầu chưa để quyết định startActivity() thật hay chỉ gửi message.
- **Hết giờ tự nhiên** (không bấm Stop) → `GameControlBridge.PushGameEnded()` (gọi từ
  `StateMachineEnter_GameOver` của `TestTongHopController`/`MiniGameControllerBase`) báo
  `ControlActivity.OnGameEnded()` tự quay Menu — coi như hết 1 round. Display máy chiếu KHÔNG bị
  đụng, vẫn hiện đúng ScoreScene/kết quả.
- `TaskRemovedWatcherService` — chỉ còn xử lý case user thật sự swipe 1 trong 2 task qua Recents
  (kill cả process dọn sạch cả 2 display). Không còn liên quan gì đến nút Stop nữa.
- `WatchdogService` (process riêng `:watchdog`, bind vào `HeartbeatService` để phát hiện process
  chính chết bất ngờ rồi tự relaunch `ControlActivity`) — code có sẵn nhưng **đang tắt**
  (`ControlActivity.onCreate()`, dòng `startService(new Intent(this, WatchdogService.class))` bị
  comment) theo yêu cầu user lúc test. Bật lại khi cần.

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
  editable trên máy KHÔNG cần rebuild: `Application.persistentDataPath/lidar_config.json`, gọi
  `LidarTouchBridge.Instance.ReloadConfig()` hoặc restart app để áp dụng. Giá trị calib thật đã
  xác nhận hoạt động: `{half_x:1130, hight_floor:1350, ymax:-600, shift_x_floor:1, shift_y:1,
  shift_x:1, offset_angle:-5, nums_point_report:2}`.
- `GameControlBridge.OnPauseRequested/OnResumeRequested`/`OnStopRequested` đều gọi
  `LidarTouchBridge.SetTouchEnabled()` — dùng CHUNG cờ với nút cứng, nên nút cứng có thể ghi đè
  trạng thái Pause của control panel nếu bấm không đúng lúc (biết trước, chưa fix — hỏi trước
  khi đụng nếu cần tách riêng 2 cờ).

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

### Report + Google Sheets sync (Track E)

- `GameControlBridge.PushReport(secondsLeft, leftName, leftScore, rightName, rightScore)` —
  đẩy report throttle 1 lần/giây từ `MiniGameControllerBase.Update()`/
  `TestTongHopController.Update()` sang `ControlActivity.UpdateReport()` (hiện tên + điểm TỪNG
  BÊN riêng, không gộp tổng).
- `SheetsSyncManager.cs` — gom log (`GameLogger`, `MiniGameControllerBase.LogRoundResult`,
  `PlayerRecognitionService`) gửi lên Google Sheet qua Apps Script Web App (không cần OAuth phía
  app), batch mỗi ~3s. Config: `Application.persistentDataPath/sheets_sync_config.json`
  (`webAppUrl`, `enabled`, `intervalSeconds`).
  > **Gotcha Apps Script**: sửa code trong editor **KHÔNG** tự cập nhật URL `/exec` đang chạy —
  > bắt buộc **Deploy → Manage deployments → Edit (bút chì) → New version → Deploy** thì code mới
  > mới thật sự chạy. Response log giờ có in body (`{"ok":true,"count":N}`) để kiểm tra ngay thay
  > vì phải mở Sheet thủ công.
- **Gotcha ghi tên game vào log**: PHẢI dùng `GameSessionManager.ResolveActiveGameName(fallback)`
  (không hardcode literal tên scene) ở MỌI nơi log game — nhiều game share chung 1
  scene/controller, hardcode sẽ ghi sai tên khi có > 1 variant (đã fix 14 chỗ hardcode kiểu
  `BeginGameSession("XxxGame")` rải rác ở các controller cũ không kế thừa
  `MiniGameControllerBase`, vd `AddNumberGameController`, `AddUpGameController`, ...).
- **Independent play mode** (`playMode`/`independentPlay = true`, vd Counting5): KHÔNG dùng được
  `GameLogger.BeginRound()`/`EndRound()` gốc (dựa vào 1 `_currentRound` dùng chung — 2 bên tự
  nhịp câu hỏi riêng nên có thể chồng thời gian nhau, bên này `BeginRound()` sẽ đè mất câu đang
  dở của bên kia). Dùng `GameLogger.LogIndependentRound()` /
  `GameManager.RecordIndependentAnswer()` thay thế — ghi trực tiếp, không qua `_currentRound`.

## Phase roadmap

- **Phase 1** ✅ — Dự án mới, launcher, vào thẳng MenuScene
- **Phase 2** — Refactor `PlayerSlot` (1P / 2P / 3-4P chung code)
- **Phase 3** — Game modes: Endless, Survival, Time Attack, Sudden Death
- **Phase 4** — 3-4 người cùng màn hình (multi-touch zones)
- **Phase 5** — Multi-device (LAN/Bluetooth)
