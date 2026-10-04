# LidarUnity (liblidar_unity.so, native C++)

Chi tiết LiDAR touch, config calib, bug throttle `sendTouch`, Calib: **`docs/lidar-calib.md`**.
- Build: `cd NativePlugins/LidarUnity/build/arm64-v8a && ninja` (NDK 27.0.12077973, CMake 3.22.1), copy .so vào `Assets/Plugins/Android/libs/arm64-v8a/`.
- Config calib nằm trên thiết bị (`lidar_config.json`), không theo APK.
