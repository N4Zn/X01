# K02 cấu hình thiết bị: HDMI mirror, USB root, Audio

> **TL;DR**: Ba fix cấp firmware, làm riêng cho từng máy K02 (mất khi flash ROM): neutralize mirror_hdmi.sh, cấp quyền USB bằng root lúc boot, max volume + tắt Safe Media Volume. File nằm ở NativePlugins/K02DeviceConfig/.
> **Đọc khi**: cài/khôi phục máy K02 mới, lỗi HDMI méo/dialog USB/âm lượng.
> **Cập nhật**: 2026-10-04 (tách nguyên văn từ CLAUDE.md gốc, chưa sửa nội dung)

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

### USB permission (lidar CP2102 + camera) — cấp bằng root lúc boot (2026-10-03, K02 #2 đã verify)

**Vấn đề**: Android 10 KHÔNG lưu quyền USB qua reboot, và sự kiện `USB_DEVICE_ATTACHED` lúc boot
đến trước khi app chạy (không giao được cho app) → sau mỗi reboot app hiện dialog "Allow USB
access" cho lidar + camera. Mục "mặc định" trong `/data/system/users/0/usb_device_manager.xml`
KHÔNG giúp gì cho `requestPermission()` trên Android 10.

**Không làm được từ trong app**: `su` của K02 chỉ cho **root/shell** gọi — app gọi `su` bị
`not allowed` (đã giả lập bằng `su 10104 /system/xbin/su 0 id`). Cú pháp `su` ở đây là
`su 0 <cmd>`, KHÔNG phải `su -c`. Nên cấp quyền phải do root chạy **từ ngoài app**.

**Cách đã triển khai** (per-device, như fix HDMI ở trên — nằm trên firmware, mất khi flash lại ROM):
- [`NativePlugins/K02DeviceConfig/usbgrant/UsbGrant.dex`](NativePlugins/K02DeviceConfig/usbgrant/UsbGrant.dex)
  (nguồn `UsbGrant.java`, build: `javac -source 8` + `d8 --min-api 22`) — chạy bằng root qua
  `app_process`, gọi hidden API `IUsbManager.grantDevicePermission(device, uid)` bằng reflection.
  Độc lập APK nên **không cần build lại app** khi đổi bản.
- Hook boot = chính `/data/local/tmp/mirror_hdmi.sh` (service OEM `mirror_hdmi`, oneshot, root,
  chạy ở `boot_completed`+8s — cùng hook đã dùng cho fix HDMI). Bản mới ở
  [`mirror_hdmi.sh.usbgrant`](NativePlugins/K02DeviceConfig/mirror_hdmi.sh.usbgrant): giữ phần HDMI
  no-op, rồi lấy uid `com.EduXplore.X01a` qua `pm list packages -U`, chạy `UsbGrant.dex` retry tối đa
  40 lần × 2s cho tới khi thấy lidar (`4292:60000`). Log: `/data/local/tmp/usbgrant_log.txt`
  (đọc cần `adb root`). **Đã reboot kiểm chứng trên K02 #2: không còn dialog, nhận cả lidar + cam.**
- **Cài cho 1 máy K02 mới** (adb, Git Bash cần `MSYS_NO_PATHCONV=1`; `adb root` phải bật):
  ```bash
  adb root
  MSYS_NO_PATHCONV=1 adb push NativePlugins/K02DeviceConfig/usbgrant/UsbGrant.dex /data/local/tmp/UsbGrant.dex
  MSYS_NO_PATHCONV=1 adb push NativePlugins/K02DeviceConfig/mirror_hdmi.sh.usbgrant /data/local/tmp/mirror_hdmi.sh
  adb shell chmod 755 /data/local/tmp/mirror_hdmi.sh
  adb reboot
  ```
  Nếu package khác `X01a`, sửa biến `PKG` trong script. Muốn cấp thử ngay không cần reboot:
  `adb shell "su 0 sh -c 'CLASSPATH=/data/local/tmp/UsbGrant.dex app_process /system/bin UsbGrant <uid>'"`.
- **Giới hạn**: quyền chỉ sống trong bộ nhớ → **rút/cắm lại cáp USB lidar/camera lúc máy đang chạy
  thì mất quyền** (dialog hiện lại) cho tới lần reboot sau, hoặc chạy tay lệnh trên. Script chạy 1 lần
  lúc boot, chưa theo dõi hotplug.
- File script/dex đang thuộc user `shell` (không `chown root` được lúc triển khai) — vẫn chạy đúng
  vì init chạy bằng root, nhưng ai có adb shell đều sửa được.
- **K02 #1** (serial `PNH6ZHMNZDEAYDLB`) KHÔNG làm gì — user nói máy đó không hỏi quyền sau reboot từ
  lâu (dùng X01b + `MyNativeApp` đọc lidar qua `ttyUSB2`, setup khác hẳn); chỉ dùng tham khảo.

**Sửa kèm theo (cần build lại APK Unity mới có hiệu lực)**:
- **ID camera**: camera hiện tại `cb07:1bcf` bị ROM K02 báo sai thành **`vid=1410 pid=2848`** (serial
  "Web Camera"); đã thêm vào `res/xml/usb_device_filter.xml` của cả `unityplugin-release.aar` +
  `faceenroll-release.aar` + nguồn FaceEnroll (trước chỉ có ID camera cũ `742/6733`, `22595/30852`).
  Backup aar cũ: `NativePlugins/K02DeviceConfig/backups/*.bak_20261003`.
- **`LidarUsbBridge.java`**: bỏ cơ chế chờ auto-grant 6×500ms/sự kiện cắm — giờ check `hasPermission`
  trước, có quyền thì mở đọc luôn, chưa có mới hiện dialog ngay. Đã build lại + copy
  `lidarlib-release.aar` (`gradlew :lidarlib:assembleRelease` trong `NativePlugins/LidarNativeAndroidLib`,
  cần `JAVA_HOME` = `C:\Program Files\Android\Android Studio\jbr`).

### Audio K02 — max hết + tắt Safe Media Volume (2026-10-03, K02 #2)

Yêu cầu: mọi đường audio (HDMI, loa, USB, BT...) đều max, tắt cảnh báo an toàn tai nghe.
- `adb shell settings put global audio_safe_volume_state 2` (INACTIVE) rồi **reboot** — dịch vụ audio
  chỉ đọc lúc khởi động (trước reboot `dumpsys audio` vẫn báo ACTIVE).
- Mức theo từng thiết bị lưu trong `settings system`: `volume_<stream>_<device>` (stream: system,
  ring, music, alarm, notification, bluetooth_sco, system_enforced, dtmf, tts, accessibility; device:
  speaker, hdmi, usb_headset, headset, bluetooth_a2dp, ...) = 15 (`volume_voice*` = 7). Chỉ ghi vào
  settings thì phải **reboot** mới áp dụng; đang chạy thì dùng `service call audio 10 i32 <stream> i32 15
  i32 0 s16 com.android.shell` (setStreamVolume; `10` đúng cho Android 10 — mã `7` thì không ra gì).
  Đã verify sau reboot: mọi stream × mọi device = 15/15, `SAFE_MEDIA_VOLUME_INACTIVE`.
- HDMI không có gain riêng ở mixer phần cứng (`tinymix`) — âm lượng HDMI chỉ chỉnh bằng phần mềm.
