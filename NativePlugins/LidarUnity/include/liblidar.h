#ifndef __LIBLIDAR_H__
#define __LIBLIDAR_H__

#include <iostream>

enum {
  PKG_HEADER = 0x54,
  PKG_VER_LEN = 0x2C,
  POINT_PER_PACK = 12,
};

constexpr float DISTANCE_THRESHOLD = 30.0f; // Dùng cho Clustering sau này
constexpr double THRESHOLD_SQUARED = DISTANCE_THRESHOLD * DISTANCE_THRESHOLD;

struct DataPoint {
  // Polar coordinate representation
  float angle;         // Angle ranges from 0 to 359 degrees
  uint16_t distance;   // Distance is measured in millimeters
  uint8_t intensity;  // Intensity is 0 to 255
  //! System time when first range was measured in nanoseconds
  uint64_t stamp;
  // Cartesian coordinate representation
  double x;
  double y;
};

struct touch_point_event_t {
    double x;
    double y;
    double x_avg;
    double y_avg;
    long last_time;
    uint16_t distance;
    uint16_t count;
    bool reported;
    double x_max_y; // MỚI: Tọa độ X của điểm có Y lớn nhất
    double y_max_y; // MỚI: Tọa độ Y lớn nhất
    uint64_t timestamp;    
};

struct LidarPoint_t {
    double x;
    double y;
    uint64_t timestamp;
};

typedef struct __attribute__((packed)) {
  uint16_t distance;
  uint8_t intensity;
} LidarPointStructDef;

typedef struct __attribute__((packed)) {
  uint8_t header;
  uint8_t ver_len;
  uint16_t speed;
  uint16_t start_angle;
  LidarPointStructDef point[POINT_PER_PACK];
  uint16_t end_angle;
  uint16_t timestamp;
  uint8_t crc8;
} LiDARFrameTypeDef;

uint8_t CalCRC8(const uint8_t *data, uint16_t data_len);
bool lidar_AnalysisOne(uint8_t byte);
size_t lidar_frame_Parse(DataPoint* dpoint);
/******
Kiểm tra dữ liệu điểm đo có hợp hệ không:
+ x = y = distance = 0 > coi là không hợp lệ
*****/
bool ld14p_xypoint_is_valid(DataPoint* dpoint);

bool point_is_in_area(DataPoint* dpoint);
void find_position(touch_point_event_t point);
void sendTouch(int x, int y);
void sendTouchRelease();
void sendSwipe(int x_start, int y_start, int x_end, int y_end);
void init_thread_check_point_disapear(void);
uint64_t GetTimestamp(void);
std::vector<std::vector<LidarPoint_t>> cluster_points(const std::vector<LidarPoint_t>& points);
std::vector<touch_point_event_t> find_toe_points(
    const std::vector<std::vector<LidarPoint_t>>& all_clusters,
    size_t min_cluster_size = 3
);
void report_touch_event(touch_point_event_t p_old);

void test_libdar_checkcrc();
#endif