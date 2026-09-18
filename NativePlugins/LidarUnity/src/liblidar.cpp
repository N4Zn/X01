#include <iostream>
#include <iomanip>
#include <cstdint>
#include <string.h>
#include <chrono>
#include <mutex>
#include <functional>
#include <cmath>
#include <thread>
#include <atomic>
#include <vector>
#include <algorithm>
#include "liblidar.h"

#include <fcntl.h>
#include <unistd.h>
#include <cstring>
#include "liblog.h"

// EduXplore Unity plugin — bản rút gọn từ MyNativeApp_v4/liblidar.cpp.
// Không còn ghi evdev (/dev/input) hay dispatchGesture/injectInputEvent — touch được
// đẩy thẳng vào Unity qua PushTouchPoint() (native-lib.cpp), Unity C# tự bắn PointerEvent
// vào EventSystem của chính nó. Không cần quyền OS đặc biệt nào (không root, không
// INJECT_EVENTS) vì không còn "inject" vào tiến trình khác nữa.

// Defined in native-lib.cpp — set via Lidar_SetTouchEnabled()
extern std::atomic<bool> g_touch_enabled;

// Defined in native-lib.cpp — nhận điểm touch đã tính toạ độ, đẩy vào queue cho Unity poll.
extern void PushTouchPoint(int x, int y);

// C++-level throttle — tránh đẩy CÙNG 1 touch vào queue quá dày (giữ nguyên nhịp gốc: 1 vị trí
// không báo lại nhanh hơn 150ms/lần, tránh dội SurfaceFlinger). ĐÃ SỬA (2026-09-17, xác nhận bug
// thật trên K02): bản gốc dùng 1 mốc thời gian TOÀN CỤC — nghĩa là báo xong 1 điểm thì KHOÁ luôn
// 150ms tiếp theo cho MỌI điểm khác, kể cả điểm ở vị trí hoàn toàn khác (vd 2 người chạm cùng lúc
// 2 chỗ, hoặc 1 vật tĩnh lớn — như tường trong phòng nhỏ — liên tục thắng suất báo, khiến vật nhỏ
// hơn/xa hơn (như trụ calib) gần như không bao giờ có cơ hội được đẩy sang Unity). Giờ mỗi vị trí
// (gộp theo bán kính SAME_TOUCH_MERGE_RADIUS) có mốc thời gian RIÊNG — các touch KHÁC nhau không
// còn tranh giành 1 suất chung nữa, chỉ CÙNG 1 touch mới bị giãn cách 150ms như cũ.
static constexpr int64_t TOUCH_MIN_INTERVAL_MS = 150;
static constexpr float SAME_TOUCH_MERGE_RADIUS = 100.0f; // px (ref 1024x600) — 2 điểm trong bán kính này coi là CÙNG 1 touch

struct RecentSend { int x, y; int64_t ts; };
static std::vector<RecentSend> g_recent_sends;
static std::mutex g_recent_sends_mutex;

static int64_t nowMs() {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}



#define TAG "FloorGame_LibLIDAR"

constexpr int X_OFFSET_MAX = 60;
constexpr int Y_OFFSET_MAX = 60;

// constexpr int AREA_X_MIN = -1500;
// constexpr int AREA_X_MAX = 0;
// constexpr int AREA_Y_MIN = 1400;
// constexpr int AREA_Y_MAX = 2500;

extern int G_HALF_X;
extern int G_HIGHT_FLOOR;
extern int G_YMAX;
extern int G_SHIFT_X_FLOOR ;
extern int G_SHIFT_Y;
extern int G_SHIFT_X;
extern float G_OFFSET_ANGLE;
extern int G_NUMS_POINT_REPORT;

int G_YMIN = G_YMAX - G_HIGHT_FLOOR;
int G_WIDTH_FLOOR = 2 * G_HALF_X;
int G_XMAX = G_HALF_X + G_SHIFT_X_FLOOR;
int G_XMIN = -G_HALF_X + G_SHIFT_X_FLOOR;

constexpr int MAX_EVENTS_SUPPORTED = 20;
constexpr int ITEM_DETECTED_NONE = -1;
constexpr int ITEM_TIMEOUT_MS = 1500;
constexpr int POINT_NUMBER_OK = 10;
constexpr int TOUCH_POINT_DISAPPEAR_TIMEOUT_MS = 300;


enum item_type_t{
    ITEM_TYPE_NONE = -1,
    ITEM_ADD_NEW,
    ITEM_UPDATE_EXIST,
};

LiDARFrameTypeDef datapkg_;


static const uint8_t CrcTable[256] = {
    0x00, 0x4d, 0x9a, 0xd7, 0x79, 0x34, 0xe3, 0xae, 0xf2, 0xbf, 0x68, 0x25,
    0x8b, 0xc6, 0x11, 0x5c, 0xa9, 0xe4, 0x33, 0x7e, 0xd0, 0x9d, 0x4a, 0x07,
    0x5b, 0x16, 0xc1, 0x8c, 0x22, 0x6f, 0xb8, 0xf5, 0x1f, 0x52, 0x85, 0xc8,
    0x66, 0x2b, 0xfc, 0xb1, 0xed, 0xa0, 0x77, 0x3a, 0x94, 0xd9, 0x0e, 0x43,
    0xb6, 0xfb, 0x2c, 0x61, 0xcf, 0x82, 0x55, 0x18, 0x44, 0x09, 0xde, 0x93,
    0x3d, 0x70, 0xa7, 0xea, 0x3e, 0x73, 0xa4, 0xe9, 0x47, 0x0a, 0xdd, 0x90,
    0xcc, 0x81, 0x56, 0x1b, 0xb5, 0xf8, 0x2f, 0x62, 0x97, 0xda, 0x0d, 0x40,
    0xee, 0xa3, 0x74, 0x39, 0x65, 0x28, 0xff, 0xb2, 0x1c, 0x51, 0x86, 0xcb,
    0x21, 0x6c, 0xbb, 0xf6, 0x58, 0x15, 0xc2, 0x8f, 0xd3, 0x9e, 0x49, 0x04,
    0xaa, 0xe7, 0x30, 0x7d, 0x88, 0xc5, 0x12, 0x5f, 0xf1, 0xbc, 0x6b, 0x26,
    0x7a, 0x37, 0xe0, 0xad, 0x03, 0x4e, 0x99, 0xd4, 0x7c, 0x31, 0xe6, 0xab,
    0x05, 0x48, 0x9f, 0xd2, 0x8e, 0xc3, 0x14, 0x59, 0xf7, 0xba, 0x6d, 0x20,
    0xd5, 0x98, 0x4f, 0x02, 0xac, 0xe1, 0x36, 0x7b, 0x27, 0x6a, 0xbd, 0xf0,
    0x5e, 0x13, 0xc4, 0x89, 0x63, 0x2e, 0xf9, 0xb4, 0x1a, 0x57, 0x80, 0xcd,
    0x91, 0xdc, 0x0b, 0x46, 0xe8, 0xa5, 0x72, 0x3f, 0xca, 0x87, 0x50, 0x1d,
    0xb3, 0xfe, 0x29, 0x64, 0x38, 0x75, 0xa2, 0xef, 0x41, 0x0c, 0xdb, 0x96,
    0x42, 0x0f, 0xd8, 0x95, 0x3b, 0x76, 0xa1, 0xec, 0xb0, 0xfd, 0x2a, 0x67,
    0xc9, 0x84, 0x53, 0x1e, 0xeb, 0xa6, 0x71, 0x3c, 0x92, 0xdf, 0x08, 0x45,
    0x19, 0x54, 0x83, 0xce, 0x60, 0x2d, 0xfa, 0xb7, 0x5d, 0x10, 0xc7, 0x8a,
    0x24, 0x69, 0xbe, 0xf3, 0xaf, 0xe2, 0x35, 0x78, 0xd6, 0x9b, 0x4c, 0x01,
    0xf4, 0xb9, 0x6e, 0x23, 0x8d, 0xc0, 0x17, 0x5a, 0x06, 0x4b, 0x9c, 0xd1,
    0x7f, 0x32, 0xe5, 0xa8};

uint8_t CalCRC8(const uint8_t *data, uint16_t data_len) {
  uint8_t crc = 0;
  while (data_len--) {
    crc = CrcTable[(crc ^ *data) & 0xff];
    data++;
  }
  return crc;
}

bool lidar_AnalysisOne(uint8_t byte) {
  static enum {
    HEADER,
    VER_LEN,
    DATA,
  } state = HEADER;
  static uint16_t count = 0;
  static uint8_t tmp[128] = {0};
  static uint16_t pkg_count = sizeof(LiDARFrameTypeDef);

//  LOGI(TAG, "state = %d, data = 0x%02x", state, byte);
  switch (state) {
    case HEADER:
      if (byte == PKG_HEADER) {
        tmp[count++] = byte;
        state = VER_LEN;
        // std::cout << "========= found header\n";
      }
      break;
    case VER_LEN:
      if (byte == PKG_VER_LEN) {
        tmp[count++] = byte;
        state = DATA;
        // std::cout << "========= found LEN\n";
      } else {
        state = HEADER;
        count = 0;
        // std::cout << "========= not found LEN => RESET\n";
        return false;
      }
      break;
    case DATA:
      tmp[count++] = byte;
      if (count >= pkg_count) {
        memcpy((uint8_t *)&datapkg_, tmp, pkg_count);
        uint8_t crc = CalCRC8((uint8_t *)&datapkg_, pkg_count - 1);
        state = HEADER;
        count = 0;
        /******* print for debug only ******* *
            int temp = crc;
            temp = 0x000000FF & temp;
            // std::cout << "=========== Check CRC:\n";
            std::cout << std::setw(2) << std::setfill('0') << std::hex << temp << ":";

            temp = datapkg_.crc8;
            temp = 0x000000FF & temp;
            std::cout << std::setw(2) << std::setfill('0') << std::hex << temp << "\n";
        ************************************ */
        if (crc == datapkg_.crc8) {
//            LOGI(TAG, "Found  frame");
            return true;
        } else {
//            LOGE(TAG, "CRC checked, frame error");
            return false;
        }
      }
      break;
    default:
      break;
  }

  return false;  
}

double RAD_TO_DEG = 180.0 / 3.14159265358979323846;
int lidar_measure_freq_ = 4000;
uint64_t last_pkg_timestamp_;

uint64_t GetTimestamp(void) {
  std::chrono::time_point<std::chrono::system_clock, std::chrono::nanoseconds> tp = 
    std::chrono::time_point_cast<std::chrono::nanoseconds>(std::chrono::system_clock::now());
  auto tmp = std::chrono::duration_cast<std::chrono::nanoseconds>(tp.time_since_epoch());
  return ((uint64_t)tmp.count());
}

size_t lidar_frame_Parse(DataPoint* dpoint) { 

    size_t size = 0;
    uint16_t speed_;
    uint16_t timestamp_;
    speed_ = datapkg_.speed;
    timestamp_ = datapkg_.timestamp;
    uint64_t current_pack_stamp = 0;
    // parse a package is success

    float diff = ((datapkg_.end_angle / 100 - datapkg_.start_angle / 100) + 360) % 360;
    /************ for debug only ******** *
    std::cout << "====================== check to calculator x/y point:\n";
    std::cout << std::setw(2) << std::setfill('0') << std::dec << (datapkg_.end_angle) << "-";
    std::cout << std::setw(2) << std::setfill('0') << std::dec << (datapkg_.start_angle) << "==>";
    std::cout << std::setw(2) << std::setfill('0') << std::fixed << diff << " vs ";
    std::cout << std::setw(2) << std::setfill('0') << std::dec << (datapkg_.speed) << "\n";

    ************************************* */
    if (diff <= ((double)datapkg_.speed * POINT_PER_PACK / lidar_measure_freq_ * 1.5)) {
        if (0 == last_pkg_timestamp_) {
            last_pkg_timestamp_ = GetTimestamp();
        } else {
            current_pack_stamp = GetTimestamp();
            int pkg_point_number = POINT_PER_PACK;
            double pack_stamp_point_step =  
                static_cast<double>(current_pack_stamp - last_pkg_timestamp_) / static_cast<double>(pkg_point_number - 1);
            
            uint32_t diff_2 = ((uint32_t)datapkg_.end_angle + 36000 - (uint32_t)datapkg_.start_angle) % 36000;
            
            // Chia cho (POINT_PER_PACK - 1) để lấy khoảng cách góc giữa các điểm
            constexpr int NUM_GAPS = 11; // Nếu POINT_PER_PACK = 12
            float angle_step_cent = (float)diff_2 / (float)NUM_GAPS; // Góc nhân 100
            
            float angle_start_cent = (float)datapkg_.start_angle; // Góc nhân 100
            
            // Bỏ qua đoạn code tính toán angle_start/angle_end/angle_diff/angle_per_point/angle_deg
            
            for (int i = 0; i < POINT_PER_PACK; i++) {
                dpoint->distance = datapkg_.point[i].distance;

                // 1. Tính Góc: Sử dụng logic tính góc đồng nhất
                float angle_cent = angle_start_cent + i * angle_step_cent;
                float angle_deg = angle_cent / 100.0f;
                
                if (angle_deg >= 360.0f) 
                {
                    angle_deg -= 360.0f;
                }

                float angle_deg_offset = angle_deg + G_OFFSET_ANGLE;
                
                // Chuẩn hóa góc sau khi offset về phạm vi [0, 360)
                if (angle_deg_offset >= 360.0f) {
                    angle_deg_offset -= 360.0f;
                } else if (angle_deg_offset < 0.0f) {
                    angle_deg_offset += 360.0f;
                }
                
                // Cập nhật dpoint->angle với giá trị đã tính
                dpoint->angle =angle_deg_offset;

                dpoint->intensity = datapkg_.point[i].intensity;
                dpoint->stamp = static_cast<uint64_t>(last_pkg_timestamp_ + (pack_stamp_point_step * i));

                // 2. Tính X/Y: Dùng góc độ chính xác đã tính
                float angle_rad = angle_deg_offset * M_PI / 180.0f;
                
                // Giữ nguyên phép lật X đã sửa (nếu cần)
                dpoint->y = dpoint->distance * cos(angle_rad);
                dpoint->x = -dpoint->distance * sin(angle_rad);

                dpoint++;
                size++;

                /***********************************
                // std::cout << std::setw(2) << std::setfill('0') << std::dec << dpoint.distance << ":";
                // std::cout << std::setw(2) << std::setfill('0') << std::fixed << dpoint.angle << ":";
                // std::cout << std::setw(2) << std::setfill('0') << std::fixed << angle_rad << "==> ";
                std::cout << std::setw(2) << std::setfill('0') << std::fixed << dpoint.x << " : ";
                std::cout << std::setw(2) << std::setfill('0') << std::fixed << dpoint.y << "\n";

                *********************************** */
                
            }
            last_pkg_timestamp_ = current_pack_stamp;
        }
    }
    return size;
}

bool ld14p_xypoint_is_valid(DataPoint* dpoint){
    if(dpoint == nullptr){
        return 0;
    }else{
        if((dpoint->x == 0) && (dpoint->y == 0)){
            return 0;
        }else{
            return 1;
        }
    }
}

long GetTimesdelta() {
    using namespace std::chrono;
    return duration_cast<milliseconds>(
        system_clock::now().time_since_epoch()
    ).count();
}

bool point_is_in_area(DataPoint* dpoint){
    bool ret = false;
    if(!dpoint){
        ret = false;
        goto exit;
    }

    // std::cout << "ThoNH------ check point_is_in_area\n";
    // std::cout << std::setw(2) << std::setfill('0') << std::fixed << dpoint->x << " / ";
    // std::cout << std::setw(2) << std::setfill('0') << std::fixed << dpoint->y << "\n";
    //Print debug only
    // if((dpoint->x >= AREA_X_MIN) && (dpoint->x <= AREA_X_MAX) && (dpoint->y >= AREA_Y_MIN) && (dpoint->y <= AREA_Y_MAX)){
    //     ret = true;
    // }
    // if((dpoint->x >= G_XMIN) && (dpoint->x <= G_XMAX) && (dpoint->y >= G_YMIN) && (dpoint->y <= G_YMAX)){
    //     ret = true;
    // }
    
    if((dpoint->angle >=0 && dpoint->angle <= 90) || (dpoint->angle >= 270 && dpoint->angle <=360))
        return false;
    if (dpoint->distance == 0 || dpoint->distance > 4000)
        return false;
    // LOGI(TAG, "point_is_in_area debug angle = %f, x = %f, y = %f", dpoint->angle, dpoint->x, dpoint->y);


    if (dpoint->x < G_XMIN) 
        return false;
    if (dpoint->x > G_XMAX)
        return false;
    if (dpoint->y < G_YMIN)
        return false;
    if (dpoint->y > G_YMAX)
        return false;

    // Nếu vượt qua tất cả các kiểm tra loại trừ, điểm nằm trong khu vực
    // LOGI(TAG, "Find point int the area = %f, x = %f, y = %f", dpoint->angle, dpoint->x, dpoint->y);
    return true;
    exit:
    return ret;
}

bool point_is_same_item(touch_point_event_t* p_old, touch_point_event_t* p_new){
    bool ret = false;
    if(!p_old || !p_new){
        return false;
    }

    double x_avg = p_old->x_avg/p_old->count;
    double y_avg = p_old->y_avg/p_old->count;
    double x_offset = fabs(x_avg - p_new->x);
    double y_offset = fabs(y_avg - p_new->y);
    if((x_offset <= X_OFFSET_MAX) && (y_offset <= Y_OFFSET_MAX)){
        ret = true;
    }
    return ret;
}

void remove_item(touch_point_event_t* p_old, int index){

    // LOGI(TAG, "remove item - %d", index);
    p_old->x = 0;
    p_old->y = 0;
    p_old->x_avg = 0;
    p_old->y_avg = 0;
    p_old->distance = 0;
    p_old->count = -1;
    p_old->last_time = 0;
    p_old->reported = false;
}

bool item_is_empty(touch_point_event_t* point){
    bool ret = false;
    if((point->x == 0) && (point->y == 0) && (point->distance == 0)){
        ret = true;
    }
    return ret;
}

constexpr int P100_WIDTH = 1024;
constexpr int P100_HEIGH = 600;

// Xuất toạ độ theo không gian tham chiếu 1024x600 CỐ ĐỊNH — KHÔNG phải pixel thật của
// display đích. Đây trùng với "Reference resolution" Canvas Scaler của toàn bộ UI Unity
// (xem CLAUDE.md). Unity C# (LidarTouchBridge) tự quy đổi sang Screen.width/height thật —
// nhờ vậy không cần biết/hardcode độ phân giải display lúc build native lib, và cùng 1
// binary chạy đúng trên cả màn tablet lẫn máy chiếu bất kể độ phân giải.
void convert_to_1024x600(touch_point_event_t point){
    int p100_x = 0, p100_y = 0;
    double x_avg = point.x_max_y;
    double y_avg = point.y_max_y;

    p100_x = (((x_avg - (G_XMIN + G_SHIFT_X_FLOOR))/G_WIDTH_FLOOR) * P100_WIDTH) + G_SHIFT_X;
    p100_y = (P100_HEIGH - (((y_avg - G_YMIN)/G_HIGHT_FLOOR) * P100_HEIGH)) + G_SHIFT_Y;
    LOGI(TAG, "Touch point (ref 1024x600) = %d, %d", p100_x, p100_y);
    sendTouch(p100_x, p100_y);
}


void report_touch_event(touch_point_event_t p_old){
    convert_to_1024x600(p_old);
}


touch_point_event_t item_arrays[10];
static int points_saved_count = 0;

void find_position(touch_point_event_t point)
{
    item_type_t item_type = ITEM_TYPE_NONE;
    int index_detected = ITEM_DETECTED_NONE;
    int i = 0;

    if(points_saved_count == 0){
        index_detected = 0;
        item_type = ITEM_ADD_NEW;
        goto add_or_update_item;
    }else{
        for(i = 0; i < MAX_EVENTS_SUPPORTED; i++){
            if(item_is_empty(&item_arrays[i]) == true){//Check phan tu mang co trong khong
                if(item_type == ITEM_TYPE_NONE){
                    index_detected = i;
                    item_type = ITEM_ADD_NEW;
                }
            }else{
                if(point_is_same_item(&item_arrays[i], &point) == true){//trung vi tri
                    item_type = ITEM_UPDATE_EXIST;
                    index_detected = i;
                    goto add_or_update_item;
                }
            }
        }
    }

add_or_update_item:
    // LOGI(TAG, "insert point to position(index/type) = %d/%d", index_detected, item_type);
    if((index_detected > ITEM_DETECTED_NONE) && (item_type > ITEM_TYPE_NONE)){//Chi xu ly diem hop le
        i = 0;

        long time_now = GetTimesdelta();
        // std::cout << "DebugTouchPoint: now = " << std::dec << time_now << std::endl;

        switch(item_type){
            case ITEM_ADD_NEW:
            {
                points_saved_count++;
                i = index_detected;
                long time_now = GetTimesdelta();
                // std::cout << std::dec << time_now << std::endl;
                // std::cout << "Debug Touch Point: time now: " << std::dec << time_now << std::endl;
                
                item_arrays[i].x = point.x;
                item_arrays[i].y = point.y;
                item_arrays[i].x_avg = point.x;
                item_arrays[i].y_avg = point.y;

                // KHỞI TẠO MAX Y: Lấy điểm đầu tiên làm điểm Y cao nhất
                item_arrays[i].x_max_y = point.x;
                item_arrays[i].y_max_y = point.y;

                item_arrays[i].count = 1;
                item_arrays[i].reported = false;
                item_arrays[i].last_time = time_now;
                break;
                
            }
            case ITEM_UPDATE_EXIST:
            {
                i = index_detected;
                // long time_now = GetTimesdelta();
                // std::cout << std::dec << time_now << std::endl;

                item_arrays[i].last_time = time_now;
                if(item_arrays[i].count <= POINT_NUMBER_OK){//So mau da du de report
                    item_arrays[i].count++;

                    item_arrays[i].x_avg += point.x;
                    item_arrays[i].y_avg += point.y;
                    // 🌟 LOGIC MỚI: TÌM VÀ CẬP NHẬT ĐIỂM CÓ Y CAO NHẤT
                    if (point.y > item_arrays[i].y_max_y) {
                        item_arrays[i].x_max_y = point.x;
                        item_arrays[i].y_max_y = point.y;
                    }
                }else{
                    if(item_arrays[i].reported == false){
                        report_touch_event(item_arrays[i]);
                        item_arrays[i].reported = true;
                    }
                }
                break;
            }
            default:
            {
                break;
            }
        }
    }
}

void touch_point_disapear_pthID_fnc() {
    while(true){
        int i = 0;
        long time_now = GetTimesdelta();

       // std::cout << "DebugTouchPoint: timeout happend, now = " << std::dec << time_now << std::endl;
        // LOGI(TAG, "timeout happend, now = %d", time_now);
//        sendTouch(300, 300);

        for(i = 0; i < MAX_EVENTS_SUPPORTED; i++){
            if(item_is_empty(&item_arrays[i]) == false){
                long delta = time_now - item_arrays[i].last_time;
//                std::cout << "DebugTouchPoint: i/delta/points_saved_count = " << std::dec << i << "/" << delta << "/" << points_saved_count << std::endl;
                // LOGI(TAG, "i/delta/points_saved_count = %d/%d/%d", i, delta, points_saved_count);
                if (delta >= ITEM_TIMEOUT_MS) {
                    if (points_saved_count > 0) {
                        points_saved_count--;
                    }
                    // Dù points_saved_count là gì, nếu mục này timeout, hãy xóa nó.
                    remove_item(&item_arrays[i], i);
                    
                    // Bỏ break để đảm bảo vòng lặp for kiểm tra tất cả các item còn lại.
                }
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(TOUCH_POINT_DISAPPEAR_TIMEOUT_MS));
    }
}

// ---------------------------------------------------------------------------
// Simple tap: DOWN + UP ngay lập tức tại vị trí LIDAR phát hiện
// ---------------------------------------------------------------------------

void sendTouchRelease() {}   // no-op — kept for linker (called from native-lib.cpp)

// Đẩy thẳng vào queue cho Unity C# poll mỗi frame — không còn evdev/JNI injection nào.
// Throttle theo TỪNG vị trí (xem giải thích ở khai báo g_recent_sends phía trên) — không còn
// dùng 1 mốc thời gian chung cho mọi điểm nữa.
void sendTouch(int x, int y) {
    if (!g_touch_enabled.load()) return;

    int64_t now = nowMs();
    std::lock_guard<std::mutex> lock(g_recent_sends_mutex);

    // Dọn các bản ghi đã quá hạn (>=150ms) — không cần nhớ nữa.
    g_recent_sends.erase(
        std::remove_if(g_recent_sends.begin(), g_recent_sends.end(),
            [now](const RecentSend& r) { return now - r.ts >= TOUCH_MIN_INTERVAL_MS; }),
        g_recent_sends.end());

    // Có bản ghi nào GẦN (x,y) vừa gửi trong 150ms qua không — nếu có, đây là CÙNG 1 touch vừa
    // báo rồi, bỏ qua lần này (đúng mục đích gốc: tránh dội SurfaceFlinger).
    for (const auto& r : g_recent_sends) {
        float dx = static_cast<float>(r.x - x);
        float dy = static_cast<float>(r.y - y);
        if (dx * dx + dy * dy <= SAME_TOUCH_MERGE_RADIUS * SAME_TOUCH_MERGE_RADIUS) return;
    }

    g_recent_sends.push_back({x, y, now});
    PushTouchPoint(x, y);
    LOGI(TAG, "Tap (%d,%d) → queue", x, y);
}

// Không triển khai — độ phân giải Lidar hiện tại chỉ đủ cho touch điểm rời rạc, và
// ld14p_consumer_thread_function() (pipeline đang chạy thật) không gọi hàm này ở đâu cả.
// Giữ lại rỗng chỉ để thoả mãn linker (liblidar.h vẫn khai báo sendSwipe()).
void sendSwipe(int x_start, int y_start, int x_end, int y_end) {}

/**
 * @brief Gom các điểm Lidar gần nhau thành các cụm.
 * Tương đương với hàm cluster_points trong Python.
 * * @param points Danh sách các điểm LidarPoint.
 * @return std::vector<std::vector<LidarPoint>> Danh sách các cụm, mỗi cụm là một danh sách các điểm.
 */
std::vector<std::vector<LidarPoint_t>> cluster_points(const std::vector<LidarPoint_t>& points) {
    std::vector<std::vector<LidarPoint_t>> clusters;

    for (const auto& p : points) {
        bool added = false;
        
        // Lặp qua tất cả các cụm hiện tại
        for (auto& cluster : clusters) {
            bool found_neighbor = false;
            
            // Kiểm tra xem điểm 'p' có đủ gần bất kỳ điểm nào trong cụm không
            for (const auto& cp : cluster) {
                double dx = p.x - cp.x;
                double dy = p.y - cp.y;
                
                // So sánh với bình phương ngưỡng để tránh tính căn bậc hai (sqrt)
                if (dx * dx + dy * dy <= THRESHOLD_SQUARED) {
                    found_neighbor = true;
                    break;
                }
            }
            
            if (found_neighbor) {
                // Nếu tìm thấy điểm lân cận, thêm điểm 'p' vào cụm này
                cluster.push_back(p);
                added = true;
                break; // Đã thêm vào một cụm, không cần kiểm tra các cụm khác
            }
        }
        
        // Nếu điểm 'p' không thuộc bất kỳ cụm nào, tạo cụm mới
        if (!added) {
            clusters.push_back({p});
        }
    }
    return clusters;
}

/**
 * @brief Lọc các cụm, tìm điểm Y lớn nhất (Toe Point) cho mỗi cụm
 * và cập nhật struct touch_point_event_t.
 * * @param all_clusters Danh sách các cụm (đầu ra của cluster_points).
 * @param min_cluster_size Kích thước cụm tối thiểu để xem xét (ví dụ: 3 điểm).
 * @return std::vector<touch_point_event_t> Danh sách các sự kiện Toe Point (Centers).
 */
std::vector<touch_point_event_t> find_toe_points(
    const std::vector<std::vector<LidarPoint_t>>& all_clusters,
    size_t min_cluster_size) 
{
    std::vector<touch_point_event_t> toe_events;

    for (const auto& cluster : all_clusters) {
        if (cluster.size() < min_cluster_size) {
            continue; // Bỏ qua cụm quá nhỏ
        }

        // Tìm điểm có Y lớn nhất (max y) trong cụm
        // Tương đương với: toe = max(cluster, key=lambda p: p[1]) trong Python
        const LidarPoint_t& toe_point = *std::max_element(
            cluster.begin(), cluster.end(), 
            [](const LidarPoint_t& a, const LidarPoint_t& b) {
                return a.y < b.y;
            }
        );
        
        // Tạo và điền dữ liệu vào struct touch_point_event_t
        touch_point_event_t event;
        // Điền tọa độ Y lớn nhất
        event.x_max_y = toe_point.x; 
        event.y_max_y = toe_point.y;
        event.timestamp = GetTimestamp();
        // event.timestamp = toe_point.timestamp;
        // LOGI(TAG, "New ToeEvent created at timestamp: %lu", event.timestamp);
        
        // Bạn có thể điền thêm các trường khác nếu cần (ví dụ: count = cluster.size())
        event.count = (uint16_t)cluster.size();

        toe_events.push_back(event);
    }
    
    return toe_events;
}




void init_thread_check_point_disapear(void){
    std::thread touch_point_disapear_pthID(touch_point_disapear_pthID_fnc);//Thread doc data tu sensor roi luu vao queue
    touch_point_disapear_pthID.detach();
}




/****************** For debug only ************* */
const uint8_t test_data[] = {0x54, 0x2c, 0x78, 0x08, 0xbb, 0x32, 0xc0, 0x00, 0xde, 0xb6, 0x00, 0xde, 0xac, 0x00, 0xdc, 0xac, 0x00, 0xde, 0xa9, 0x00, 0xde, 0xa6, 0x00, 0xde, 0xa5, 0x00, 0xde, 0xa4, 0x00, 0xde, 0xa2, 0x00, 0xde, 0xa1, 0x00, 0xde, 0xa0, 0x00, 0xde, 0xa0, 0x00, 0xde, 0x0e, 0x35, 0x20, 0x0e, 0x86};
// const uint8_t test_data[] = {0x00, 0xde, 0xa0, 0x00, 0xde, 0x0e, 0x35, 0x20, 0x0e, 0x86};

void test_libdar_checkcrc(void){
    uint8_t ret = 0;
    ret = CalCRC8(test_data, 46);
    std::cout << std::setw(2) << std::setfill('0') << std::hex << ret << " ";
}

/*********************************************** */

