cmake_minimum_required(VERSION 3.22.1)
project("liblidar_unity")

add_library(lidar_unity SHARED
        src/native-lib.cpp
        src/libuart.cpp
        src/liblidar.cpp
        src/touch_report.cpp
        src/LidarProcessor.cpp
)

target_link_libraries(lidar_unity
        android
        log)

include_directories(${CMAKE_SOURCE_DIR}/include)
