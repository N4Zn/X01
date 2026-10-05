# Launcher (Home K02) và Quản lý lớp (FaceEnroll)

> **TL;DR**: Launcher là project Kotlin riêng ngoài repo, 2 hero card trỏ ComponentName vào ControlActivity/ClassManagementActivity. Quản lý lớp nằm ở NativePlugins/FaceEnrollAndroidLib.
> **Đọc khi**: sửa Launcher (D:\X_projects\Launcher), ClassManagementActivity, enrolled.json/classes.json, hoặc đổi Activity entry point.
> **Cập nhật**: 2026-10-04 (2026-10-05: hết mock, lớp Dev/5 tuổi, roster.json, điểm/lịch sử thật; trước đó tách nguyên văn từ CLAUDE.md gốc)

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

### Hết mock, lớp Dev / 5 tuổi, roster.json, điểm thật (2026-10-05)

- **Mock đã xoá**: `seedDemoDataIfEmpty()` (3 lớp Mầm/Chồi/Lá ~35 bạn giả) và `mockScoreOf()` không còn. `AttendanceStore.migrateLegacyRosterIfNeeded()` dọn **1 lần/máy** (marker `/sdcard/EduXplore/.roster_migration_v1`): xoá lớp Mầm/Chồi/Lá + lớp rác `a`/`cây` và các bạn giả (0 mẫu enroll) trong đó; ai đã enroll thật (có mẫu, kể cả không có ảnh) → lớp **`Dev`** (học sinh test); tạo lớp **`5 tuổi`** 16 bạn (`DEFAULT_5_TUOI`, chưa có ảnh, alias = 2 từ cuối tên) nếu chưa có `roster.json`. Thứ tự lớp: `5 tuổi`, `Dev`. Migration chỉ chạy khi có 1 `AttendanceStore` được tạo (mở Quản lý lớp hoặc màn camera) — ControlActivity mở trước vẫn thấy dữ liệu cũ tới lúc đó.
- **`/sdcard/EduXplore/roster.json`** — bản dễ đọc/sửa của lớp + học sinh (khác `enrolled.json` chứa embedding): `{"version":1,"classes":[{"name":"5 tuổi","students":[{"name","alias","gender":"nam|nu","birthdate"}]}]}`. App GHI sau mỗi thay đổi (`adb pull` để xem); NẠP khi mở `AttendanceStore` nếu file khác lần app ghi gần nhất (so mtime) → `adb push` rồi mở lại Quản lý lớp. Nạp chỉ THÊM/CẬP NHẬT (lớp, học sinh mới, alias, giới tính, ngày sinh, chuyển lớp), **không bao giờ xoá** học sinh/ảnh/enroll (xoá bằng app); `name` là khoá, không đổi tên qua file; lỗi cú pháp → toast + không nạp + app không ghi đè file. Học sinh chưa gán lớp không xuất hiện trong file.
- **Điểm/lịch sử trong Quản lý lớp**: `ScoreBook.kt` đọc `class_rounds.jsonl` (ControlActivity ghi) — cùng công thức `ScoreStore.java` (50 round gần nhất/học phần; môn = TB học phần đã chơi; tổng = TB môn đã có điểm; chưa chơi = **"-"**). Sửa công thức ở 1 nơi thì sửa nơi kia. Môn/học phần lấy từ `game_registry.json` (asset của aar controlui, gộp chung APK). Danh sách/thẻ học sinh hiện cột "Điểm" ("-" nếu chưa có); panel chi tiết học sinh có 3 tab **Ảnh / Năng lực / Lịch sử** (giống ControlActivity). Nút thêm ảnh cho đúng bạn đó (Chụp ảnh mới / Từ ảnh có sẵn) nằm cuối panel chi tiết.

### Panel học sinh / ảnh mẫu (2026-10-05)
- Ảnh mẫu lưu ở `/sdcard/EduXplore/enrolled_photos/<tên>/<tên>_<yyyyMMdd_HHmmss_SSS>.jpg`, CÙNG thư mục với `enrolled.json`/`classes.json`/`roster.json` → backup cả `/sdcard/EduXplore`. Ảnh cũ ở `getExternalFilesDir/enrolled_photos` tự chuyển sang lúc mở app (`migratePhotosToSharedDir`); đổi tên thật thì ảnh được chuyển theo.
- Mẫu: tối đa **3** ảnh (permanent, chụp lúc lấy mẫu); `ROLLING_SAMPLES = 0` (trước 3 + 5) vì ảnh thêm sau dễ sai ánh sáng/nhầm người. Đủ 3 thì `addSample` TỪ CHỐI (toast), không tự thay/xoá — muốn đổi thì xoá bớt ở panel. Dữ liệu cũ đang >3 ảnh giữ nguyên, không tự cắt.
- Xoá hết ảnh KHÔNG xoá học sinh (chỉ thành "Cần ảnh"); chỉ nút "Xóa học sinh" (hỏi xác nhận) mới xoá, kèm ảnh + embedding + lớp/alias/ngày sinh.
- Sau xoá ảnh/học sinh, `AttendanceStore.verifyOnDisk` đọc lại `enrolled.json` để chắc embedding và ảnh đã mất cùng nhau (lệch → toast cảnh báo).
- Panel là `Dialog` cao cố định 92% màn, footer (Xóa/Chụp ảnh/Thêm ảnh/Đóng/Lưu) luôn hiện; Enter ở tên thường gọi → tên thật → mở chọn ngày sinh.
