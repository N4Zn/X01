# Thử chụp ảnh camera IP (Hikvision) trực tiếp trên K02

Cập nhật: 2026-10-21. Thử `SnapshotClient` (`NativePlugins/UnityFacePluginAndroidLib/.../SnapshotClient.java`) bằng `app_process`, không cần build aar.

## 0. Mạng K02 ↔ camera (làm lại sau mỗi lần reboot / rút cáp, vì chưa có script boot)
```
adb shell "su 0 ip addr add 192.168.2.20/24 dev eth0"
adb shell "su 0 ip rule add to 192.168.2.0/24 lookup main pref 5000"
```
Luật thứ hai bắt buộc: Android coi Wi-Fi là mạng mặc định nên gói tới `192.168.2.x` mặc định đi qua `wlan0`
(ping `-I eth0` vẫn chạy nên dễ nhầm là mạng ổn). Camera: IP `192.168.2.64`, mask `255.255.255.0`, gateway `192.168.2.1`.

## 1. Build
```
sh Tools/ip_camera_test/build.sh        # -> Tools/ip_camera_test/out/snaptest.jar
```

## 2. Tạo file cấu hình (có mật khẩu, KHÔNG commit)
Chép `ipcamera.json.example` ra ngoài repo, điền mật khẩu, rồi:
```
adb push ipcamera.json /sdcard/EduXplore/ipcamera.json
```

## 3. Chạy (Git Bash cần `export MSYS_NO_PATHCONV=1`)
```
adb push Tools/ip_camera_test/out/snaptest.jar /data/local/tmp/snaptest.jar
adb shell "su 0 app_process -Djava.class.path=/data/local/tmp/snaptest.jar /data/local/tmp com.eduxplore.faceplugin.SnapshotTest 5"
adb pull /sdcard/EduXplore/snaps ./snaps
```
In ra từng ảnh: thời gian (ms), dung lượng, kích thước ảnh. Ảnh lưu ở `/sdcard/EduXplore/snaps/snap_N.jpg`.

Chưa điền mật khẩu (để trống) thì chỉ lấy thử thách Digest rồi dừng ("Cần mật khẩu (realm=...)") — dùng để kiểm tra đường mạng mà không gửi mật khẩu.
Sai mật khẩu thì dừng ngay, không thử lại: Hikvision khoá IP sau vài lần đăng nhập sai.
