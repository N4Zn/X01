#ifndef LIDARPROCESSOR_H
#define LIDARPROCESSOR_H

#include <iostream>
#include <vector>
#include <cmath>
#include <algorithm>
#include <iomanip>
#include <chrono>
#include <numeric>
#include <set>
#include <unistd.h> // Cần cho usleep() nếu sử dụng trong thread

// =================================================================
// 1. CÁC HẰNG SỐ CẤU HÌNH (Cần điều chỉnh)
// =================================================================

const int PACKET_SIZE = 47;
const float CLUSTER_DISTANCE_THRESHOLD = 50.0f; // Ngưỡng gom nhóm (mm)
const int MAX_POINTS = 2000;                     // Giới hạn điểm tối đa
const float MAX_TIME = 0.1f;                     // 100ms (Tính bằng giây)
const int CONFIDENCE_THRESHOLD = 150;            // Ngưỡng độ tin cậy
const float HALF_X = 1000.0f;                    // ROI X +/- 1000mm
const float OFFSET_Y = 100.0f;                   // ROI Y bắt đầu từ -100mm
const float WIDTH_Y = 1500.0f;                   // ROI Y mở rộng 1500mm
const int SCAN_BUFFER_SIZE = 5;                  // Kích thước buffer quét để lọc ổn định
const unsigned int DETECT_RANGE = 3000;
const float OFFSET_ANGLE = 0;
const int POINTS_PER_PACKET = 12;
const int WIDTH_X = HALF_X * 2;

extern int uart_port_fd;
// =================================================================
// 2. CẤU TRÚC DỮ LIỆU
// =================================================================

// Cấu trúc Tọa độ Cartesian (X, Y)
struct CartesianPos {
    float sx; // Chiều ngang (mm)
    float sy; // Chiều sâu (mm)
};

struct ScreenPos {
    int x; // Tọa độ X trên màn hình
    int y; // Tọa độ Y trên màn hình
};
// Cấu trúc Điểm LiDAR hoàn chỉnh
struct LidarPoint {
    CartesianPos pos;
    unsigned char confidence;
    long long timestamp_ms; // Dấu thời gian (ms)
};

// Cấu trúc Grid Key cho Lọc Ổn định
struct GridKey {
    int gx;
    int gy;
    bool operator<(const GridKey& other) const {
        if (gx != other.gx) return gx < other.gx;
        return gy < other.gy;
    }
    bool operator==(const GridKey& other) const {
        return gx == other.gx && gy == other.gy;
    }
};

// =================================================================
// 3. KHAI BÁO HÀM GIẢ ĐỊNH (Cần triển khai trong .cpp)
// =================================================================

// Đọc dữ liệu thô từ cổng UART thực tế
std::vector<unsigned char> read_uart_data();

// Giải mã gói tin, xóa byte rác/gói tin đã xử lý khỏi buffer
std::vector<LidarPoint> process_uart_buffer(std::vector<unsigned char>& uart_buffer, long long current_ts);

// Chuyển đổi tọa độ LiDAR sang tọa độ Màn hình
ScreenPos lidar_to_screen(float cx, float cy, int screen_w, int screen_h);

// Hàm di chuyển con trỏ chuột
void move_mouse(int sx, int sy);

// Hàm lấy dấu thời gian hiện tại
long long get_current_timestamp();

// =================================================================
// 4. CÁC HÀM HỖ TRỢ CHUNG
// =================================================================

// Tính khoảng cách Euclidean
float distance_euclidean(const CartesianPos& p1, const CartesianPos& p2);

// Tạo Grid Key cho một điểm
GridKey _grid_key(const CartesianPos& p);


// =================================================================
// 5. LỚP XỬ LÝ CHÍNH (LidarProcessor)
// =================================================================

class LidarProcessor {
public:
    // Buffer chung cho tất cả các điểm đã giải mã (lọc theo MAX_TIME/MAX_POINTS)
    std::vector<LidarPoint> all_points; 
    
    // Buffer cho Lọc Ổn định: Chứa danh sách các điểm sau khi Lọc ROI
    std::vector<std::vector<CartesianPos>> last_scans; 
    
    // Buffer cho các điểm đại diện (toe points) đã được phát hiện
    std::vector<LidarPoint> centers; 

    // Constructor mặc định
    LidarProcessor() = default;

    // Hàm thực hiện Gom nhóm và Tìm Max Y
    std::vector<CartesianPos> cluster_and_max_y(const std::vector<CartesianPos>& stable_points);

    // Hàm xử lý chính (tương đương update_data/process_frame)
    void process_frame(std::vector<unsigned char>& uart_buffer, const std::vector<unsigned char>& new_uart_data);
};

#endif // LIDARPROCESSOR_H