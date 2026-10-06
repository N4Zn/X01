# UnityFacePluginAndroidLib (nhận mặt trong game)

Bản sao SOURCE của module `unityplugin` -> `Assets/Plugins/Android/unityplugin-release.aar`
(gốc: `D:\X_projects\FaceRecognition\android\unityplugin`, nhánh FR). Chỉ lưu source Kotlin để có lịch sử trong repo.

**Không build được trong repo này** vì cần: Java wrapper UVC `com.serenegiant.usb.*` (`app/src/main/java/com/serenegiant`)
và các `.so` đã build sẵn (`src/main/jniLibs/arm64-v8a`, libuvc/libUVCCamera/libopencv_java4...) của project gốc.
Sửa ở đây thì chép lại về project gốc (hoặc ngược lại), build `./gradlew :unityplugin:assembleRelease`, copy aar vào `Assets/Plugins/Android/`.

Vùng nhận diện: đọc `/sdcard/EduXplore/face_zones.json` (`FaceZones.kt`, bản sao của FaceEnroll — sửa cả hai).
Cắt và dò riêng từng vùng playLeft/playRight; chưa có file thì dùng khung cắt `frtest_config.json` + chia đôi khung như cũ.
