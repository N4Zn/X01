#include <iostream>
#include <vector>
#include <cmath>
#include <algorithm>
#include <iomanip>
#include <chrono>
#include <numeric>
#include <set>

#include "LidarProcessor.h"


std::vector<unsigned char> read_uart_data() {
    // Kích thước gói tin hợp lệ
    std::vector<unsigned char> full_data;

#ifdef DEBUGING_TEST_PARSE    
    // Dữ liệu mẫu (Gói tin 47 byte hợp lệ với header 0x54 0x2C)
    static const unsigned char DUMMY_PACKET[PACKET_SIZE] = {
        0x54, 0x2C, 0x01, 0x00, 
        // Index 6: Góc Bắt đầu
        0x0C, 0x7B, 
        0x01, 
        // Index 9-44: 12 Điểm dữ liệu
        0x86, 0x05, 0xFF,
        0xA0, 0x01, 0xFF, 
        0xF0, 0x01, 0xFF,
        0x40, 0x02, 0xFF, 
        0x90, 0x02, 0xFF,
        0xE0, 0x02, 0xFF, 
        0x30, 0x03, 0xFF,
        0x80, 0x03, 0xFF, 
        0xD0, 0x03, 0xFF,
        0x20, 0x04, 0xFF, 
        0x70, 0x04, 0xFF,
        0xC0, 0x04, 0xFF, 
        // Index 45-46: Góc Kết thúc & Checksum
        0x0C, 0x7B, 0x00, 0x00
    };
    static const unsigned char REORDERED_DUMMY_PACKET[PACKET_SIZE] = {
        // Header (Index 0-3)
        0x54, 0x2C, 0x01, 0x00, 
        
        // Index 4-6: Góc Bắt đầu (Giữ nguyên 315 độ)
        0x0C, 0x7B, 0x01, 
        
        // Index 7-42: 12 Điểm dữ liệu ĐẢO VỊ TRÍ
        
        // Thay đổi: Điểm 12 lên đầu (0xC0, 0x04)
        0xC0, 0x04, 0xFF, // Điểm 12
        
        // Điểm 6 tiếp theo (0xE0, 0x02)
        0xE0, 0x02, 0xFF, // Điểm 6
        
        // Điểm 10 tiếp theo (0x20, 0x04)
        0x20, 0x04, 0xFF, // Điểm 10
        
        // Điểm 1 (0x86, 0x05)
        0x86, 0x05, 0xFF, // Điểm 1
        
        // Điểm 7 (0x30, 0x03)
        0x30, 0x03, 0xFF, // Điểm 7
        
        // Điểm 3 (0xF0, 0x01)
        0xF0, 0x01, 0xFF, // Điểm 3
        
        // Điểm 9 (0xD0, 0x03)
        0xD0, 0x03, 0xFF, // Điểm 9
        
        // Điểm 4 (0x40, 0x02)
        0x40, 0x02, 0xFF, // Điểm 4
        
        // Điểm 11 (0x70, 0x04)
        0x70, 0x04, 0xFF, // Điểm 11
        
        // Điểm 2 (0xA0, 0x01)
        0xA0, 0x01, 0xFF, // Điểm 2
        
        // Điểm 5 (0x90, 0x02)
        0x90, 0x02, 0xFF, // Điểm 5
        
        // Điểm 8 (0x80, 0x03)
        0x80, 0x03, 0xFF, // Điểm 8

        // Index 43-46: Góc Kết thúc & Checksum
        0x0C, 0x7B, 0x00, 0x00
    };
    
    // --- KHAI BÁO BYTE RÁC ---
    const int JUNK_BYTES = 5;
    const unsigned char JUNK_DATA[JUNK_BYTES] = {
        0xAA, 0xBB, 0xCC, 0xDD, 0xEE // 5 byte rác, không chứa 0x54, 0x2C
    };

    // 1. Tạo vector kết quả
    
    // 2. Thêm 5 byte rác vào đầu
    // full_data.insert(full_data.end(), JUNK_DATA, JUNK_DATA + JUNK_BYTES);

    // // 3. Thêm gói tin mẫu 47 byte vào sau
    // full_data.insert(full_data.end(), DUMMY_PACKET, DUMMY_PACKET + PACKET_SIZE);

    full_data.insert(full_data.end(), JUNK_DATA, JUNK_DATA + JUNK_BYTES);
    full_data.insert(full_data.end(), REORDERED_DUMMY_PACKET, REORDERED_DUMMY_PACKET + PACKET_SIZE);
    
    // Tổng kích thước trả về: 5 + 47 = 52 byte
    return full_data;
#else
    int MAX_READ_SIZE = 256;
    std::vector<unsigned char> read_buffer(MAX_READ_SIZE);
    ssize_t num_bytes = read(uart_port_fd, read_buffer.data(), MAX_READ_SIZE);

    if (num_bytes < 0) {
        // Có lỗi đọc, nhưng sẽ không dừng chương trình (Chỉ in lỗi)
        // std::cerr << "Loi khi doc tu UART. Error: " << errno << std::endl;
        return {}; 
    }
    
    // Trả về vector chứa chính xác số byte đã đọc
    if (num_bytes > 0) {
        read_buffer.resize(num_bytes);
        return read_buffer;
    } 
    
    return {};
#endif
}

/**
 * @brief Chuyển đổi 3 byte (Distance/Confidence) thành Tọa độ Descartes (X, Y).
 * @param raw_bytes_3p Con trỏ đến 3 byte dữ liệu (Distance (2 byte), Confidence (1 byte))
 * @param angle_deg Góc tương ứng của điểm (độ)
 * @param current_ts Timestamp hiện tại
 * @return LidarPoint đã tính toán, nếu không hợp lệ sẽ trả về confidence=0.
 */
LidarPoint compute_point(const unsigned char* raw_bytes_3p, float angle_deg, long long current_ts) {
    // Khởi tạo điểm mặc định (X=0, Y=0, confidence=0)
    LidarPoint lp = {{0.0f, 0.0f}, 0, 0}; 

    // Giải mã Little Endian: Distance (2 byte) + Confidence (1 byte)
    // Giả sử [0] là Low Byte, [1] là High Byte
    unsigned int distance = static_cast<unsigned int>(raw_bytes_3p[0]) | 
                            (static_cast<unsigned int>(raw_bytes_3p[1]) << 8);
    unsigned char confidence = raw_bytes_3p[2];

    // Lọc ngưỡng ban đầu (Distance quá nhỏ, quá lớn, Confidence quá thấp)
    if (distance == 0 || distance > DETECT_RANGE || confidence < CONFIDENCE_THRESHOLD) {
        // std::cout << " distance = " << distance 
        //           << " confidence = " << static_cast<int>(confidence) // Ép kiểu thành int
        //           << " (FILTERED - INITIAL)" << std::endl;
        return lp;
    }

    // In ra giá trị hợp lệ trước khi tính toán
    // std::cout << " distance = " << distance 
    //           << " confidence = " << static_cast<int>(confidence) // Ép kiểu thành int
    //           << std::endl;

    // Tính toán góc cuối cùng và chuyển sang Radian
    float final_angle = angle_deg + OFFSET_ANGLE;
    float angle_rad = final_angle * (M_PI / 180.0f);

    // std::cout << "final_angle = " << final_angle 
    //           << " angle_rad = " << angle_rad 
    //           << " angle_deg = " << angle_deg << std::endl;

    // Tính toán X, Y (mm)
    float dist_f = static_cast<float>(distance);

    // Áp dụng công thức Tọa độ Cực sang Descartes (Y: Cos, X: -Sin)
    lp.pos.sy = dist_f * std::cos(angle_rad);
    lp.pos.sx = -dist_f * std::sin(angle_rad);
    
    lp.confidence = confidence;
    lp.timestamp_ms = current_ts;
    
    // Lọc theo Vùng quan tâm (ROI)
    // Vùng ROI: X: [-HALF_X, HALF_X] ; Y: [-(OFFSET_Y + WIDTH_Y), -OFFSET_Y]
    if (lp.pos.sx >= -HALF_X && lp.pos.sx <= HALF_X && 
        lp.pos.sy < -OFFSET_Y && lp.pos.sy >= -(OFFSET_Y + WIDTH_Y)) 
    {
        std::cout << "Diem hop le" << " x = " << lp.pos.sx << " y = " << lp.pos.sy << std::endl;
        return lp; // Điểm hợp lệ
    }
    
    // Điểm không hợp lệ (ngoài ROI)
    std::cout << " Diem khong hop le" << " x = " << lp.pos.sx << " y = " << lp.pos.sy << std::endl;
    return {{0.0f, 0.0f}, 0, 0}; // Trả về điểm bị loại
}

/**
 * @brief Giải mã toàn bộ gói tin LiDAR 47 byte.
 * @param packet_data Con trỏ đến 47 byte dữ liệu thô.
 * @param current_ts Timestamp hiện tại
 * @return Vector các LidarPoint hợp lệ.
 */
std::vector<LidarPoint> parse_packet(const unsigned char* packet_data, long long current_ts) {
    std::vector<LidarPoint> points;
    
    // 1. Giải mã Angle Base và End Angle (Little Endian)
    // Packet Index 4:5 (Angle Base)
    unsigned int angle_base_raw = static_cast<unsigned int>(packet_data[4]) | 
                                 (static_cast<unsigned int>(packet_data[5]) << 8);
    float angle_base = static_cast<float>(angle_base_raw) / 100.0f;

    // Packet Index 42:43 (End Angle)
    unsigned int end_angle_raw = static_cast<unsigned int>(packet_data[42]) | 
                                 (static_cast<unsigned int>(packet_data[43]) << 8);
    float end_angle = static_cast<float>(end_angle_raw) / 100.0f;
    
    // 2. Lặp qua 12 điểm dữ liệu
    for (int i = 0; i < POINTS_PER_PACKET; ++i) {
        int offset = 7 + i * 3; // Dữ liệu điểm bắt đầu từ Index 7 (offset=6 là 1 byte trước)
        // std::cout << "Giai ma toa do cua diem thu " << i << std::endl;
        // 3 byte dữ liệu: [Distance_Low] [Distance_High] [Confidence]
        const unsigned char* raw_bytes_3p = &packet_data[offset];

        // Tính toán góc nội suy cho điểm thứ i
        float point_angle = angle_base + (end_angle - angle_base) * (static_cast<float>(i) / 11.0f);

        // Chuẩn hóa góc (0-360)
        point_angle = std::fmod(point_angle, 360.0f);
        if (point_angle < 0) point_angle += 360.0f;

        // Tính toán Tọa độ X, Y
        LidarPoint lp = compute_point(raw_bytes_3p, point_angle, current_ts);
        // std::cout << "Toa do cua diem " << i << " x = " << lp.pos.x << " y = " << lp.pos.y << std::endl;
        
        if (lp.confidence > 0) {
            points.push_back(lp);
        }
    }

    return points;
}

// Hàm giả định (cần bạn tự định nghĩa)
/**
 * @brief Mô hình hàm tìm và giải mã gói tin trong buffer UART.
 * @param uart_buffer Vector chứa dữ liệu byte thô từ UART.
 * @return Vector các LidarPoint từ gói tin đầu tiên hợp lệ tìm thấy.
 */
std::vector<LidarPoint> process_uart_buffer(std::vector<unsigned char>& uart_buffer, long long current_ts) {
    std::vector<LidarPoint> decoded_points;
    
    // Tìm Header 0x54 0x2C
    const unsigned char HEADER[] = {0x54, 0x2C};

    std::cout << "enter process_uart_buffer bufer size = " << uart_buffer.size() <<  std::endl;
    
    if (uart_buffer.size() < PACKET_SIZE) {
    std::cout << "DEBUG: Buffer qua nho, khong du goi tin." << std::endl;
    return decoded_points; // Trả về rỗng
    }
    for (size_t i = 0; i <= (uart_buffer.size() - PACKET_SIZE); ++i) {
        if (uart_buffer[i] == HEADER[0] && uart_buffer[i+1] == HEADER[1]) {
            // Đã tìm thấy header và buffer đủ lớn
            const unsigned char* packet_start = uart_buffer.data() + i;
            std ::cout << "Tim thay header cua goi tin" << std::endl;  
            // Giải mã gói tin
            decoded_points = parse_packet(packet_start, current_ts);
            
            // Xóa gói tin đã xử lý khỏi buffer (cần cẩn thận trong môi trường thực)
            std::cout << "Xoa data trong uart_buffer" << std::endl;
            uart_buffer.erase(uart_buffer.begin(), uart_buffer.begin() + i + PACKET_SIZE);
            if (decoded_points.empty()) {
                std::cout << "DEBUG: Tra ve vector rong." << std::endl;
            } else {
                std::cout << "DEBUG: Tra ve vector co " << decoded_points.size() << " diem." << std::endl;
            }
            return decoded_points; // Trả về và xử lý vòng lặp tiếp theo
        }
    }
    
    // Nếu buffer quá lớn mà không tìm thấy header, có thể cần cơ chế cắt bớt buffer
    // (ví dụ: nếu buffer > 2*PACKET_SIZE, xóa các byte đầu)
    
    if (decoded_points.empty()) {
        std::cout << "DEBUG: Tra ve vector rong." << std::endl;
    } else {
        std::cout << "DEBUG: Tra ve vector co " << decoded_points.size() << " diem." << std::endl;
    }
    return decoded_points; // Trống nếu không tìm thấy gói tin
}

/**
 * @brief Chuyển đổi tọa độ (x,y) từ LiDAR sang (sx,sy) pixel màn hình.
 * @param x Tọa độ X (LiDAR)
 * @param y Tọa độ Y (LiDAR)
 * @param screen_w Chiều rộng màn hình (pixel)
 * @param screen_h Chiều cao màn hình (pixel)
 * @return ScreenPos (sx, sy)
 */
ScreenPos lidar_to_screen(float x, float y, int screen_w, int screen_h) {
    ScreenPos sp;

    // Ánh xạ X: [-HALF_X, HALF_X] -> [0, screen_w]
    // sx = (x + HALF_X) / WIDTH_X * screen_w
    sp.x = static_cast<int>((x + HALF_X) / WIDTH_X * screen_w);
    
    // Ánh xạ Y (đảo ngược): [-OFFSET_Y - WIDTH_Y, -OFFSET_Y] -> [0, screen_h]
    // sy = -(y + OFFSET_Y) / WIDTH_Y * screen_h
    // (Ở đây giả sử SHIFT_Y = 0)
    sp.y = static_cast<int>(-(y + OFFSET_Y) / WIDTH_Y * screen_h);
    
    // Đảm bảo tọa độ nằm trong biên (Clamping)
    sp.x = std::max(0, std::min(screen_w - 1, sp.x));
    sp.y = std::max(0, std::min(screen_h - 1, sp.y));

    return sp;
}

long long get_current_timestamp() {
    using namespace std::chrono;
    return duration_cast<milliseconds>(
        system_clock::now().time_since_epoch()
    ).count();
}

float distance_euclidean(const CartesianPos& p1, const CartesianPos& p2) {
    float dx = p1.sx - p2.sx;
    float dy = p1.sy - p2.sy;
    return std::sqrt(dx * dx + dy * dy);
}

// Độ phân giải lưới: 50mm x 50mm
GridKey _grid_key(const CartesianPos& p) {
    return {
        (int)(p.sx / 50.0f),
        (int)(p.sy / 50.0f)
    };
}

std::vector<CartesianPos> LidarProcessor::cluster_and_max_y(
    const std::vector<CartesianPos>& stable_points) 
{
    if (stable_points.empty()) return {};

    std::vector<CartesianPos> max_y_points;
    std::vector<bool> visited(stable_points.size(), false);

    for (size_t i = 0; i < stable_points.size(); ++i) {
        if (visited[i]) continue;

        std::vector<size_t> current_cluster;
        current_cluster.push_back(i);
        visited[i] = true;

        CartesianPos max_y_pos = stable_points[i];
        
        // Mở rộng nhóm (BFS-like)
        for (size_t j = 0; j < current_cluster.size(); ++j) {
            size_t current_idx = current_cluster[j];

            for (size_t k = 0; k < stable_points.size(); ++k) {
                if (!visited[k]) {
                    if (distance_euclidean(stable_points[current_idx], stable_points[k]) <= CLUSTER_DISTANCE_THRESHOLD) {
                        
                        visited[k] = true;
                        current_cluster.push_back(k);

                        // Cập nhật điểm Y lớn nhất (toe point)
                        if (stable_points[k].sy > max_y_pos.sy) {
                            max_y_pos = stable_points[k];
                        }
                    }
                }
            }
        }
        
        // Chỉ chấp nhận cụm lớn hơn 3 điểm (len(cluster) >= 3)
        if (current_cluster.size() >= 3) {
             max_y_points.push_back(max_y_pos);
        }
    }

    return max_y_points;
}

void LidarProcessor::process_frame(std::vector<unsigned char>& uart_buffer, const std::vector<unsigned char>& new_uart_data) 
{
    long long now_ms = get_current_timestamp();
    float now_sec = (float)now_ms / 1000.0f;

    // --- 1. Cập nhật Buffer UART (Tương đương self.buffer.extend(data)) ---
    if (!new_uart_data.empty()) {
        uart_buffer.insert(uart_buffer.end(), new_uart_data.begin(), new_uart_data.end());
    }

    // --- 2. Xử lý Gói tin trong Buffer (Tương đương while True: idx = self.buffer.find...) ---
    std::vector<LidarPoint> pts;
    do {
        // Hàm này bóc tách gói tin, trả về điểm, và dọn dẹp uart_buffer
        pts = process_uart_buffer(uart_buffer, now_ms); 
        if (!pts.empty()) {
            this->all_points.insert(this->all_points.end(), pts.begin(), pts.end());
        }
    } while (!pts.empty());


    // --- 3. Lọc theo Thời gian (Tương đương cutoff = now - MAX_TIME) ---
    long long cutoff_ms = now_ms - (long long)(MAX_TIME * 1000.0f);
    
    this->all_points.erase(
        std::remove_if(this->all_points.begin(), this->all_points.end(), 
            [&](const LidarPoint& p) {
                return p.timestamp_ms < cutoff_ms;
            }), 
        this->all_points.end()
    );

    // --- 4. Lọc theo Số lượng (Tương đương len(self.all_points) > MAX_POINTS) ---
    if (this->all_points.size() > MAX_POINTS) {
        // Giữ MAX_POINTS điểm cuối cùng (mới nhất)
        this->all_points.erase(this->all_points.begin(), this->all_points.begin() + (this->all_points.size() - MAX_POINTS));
    }

    // --- 5. Lọc Confidence và ROI (Tương đương filtered_points = [p['pos'] for p in self.all_points if...]) ---
    std::vector<CartesianPos> filtered_points;
    for (const auto& p : this->all_points) {
        if (p.confidence >= CONFIDENCE_THRESHOLD && 
            p.pos.sx >= -HALF_X && p.pos.sx <= HALF_X && 
            p.pos.sy < -OFFSET_Y && p.pos.sy >= -(OFFSET_Y + WIDTH_Y)) 
        {
            filtered_points.push_back(p.pos);
        }
    }

    // --- 6. Lọc Ổn định (Stability Filtering) ---
    std::vector<CartesianPos> stable_points;
    if (!filtered_points.empty()) {
        // Cập nhật buffer quét cuối (Tương đương self.last_scans.append)
        this->last_scans.push_back(filtered_points); 
        
        // Giới hạn kích thước SCAN_BUFFER_SIZE (Giả sử bằng 5)
        const int SCAN_BUFFER_SIZE = 5; 
        if (this->last_scans.size() > SCAN_BUFFER_SIZE) {
            this->last_scans.erase(this->last_scans.begin()); // pop(0)
        }

        // Thực hiện Lọc Ổn định (Kiểm tra sự xuất hiện trong tất cả các lần quét gần nhất)
        const std::vector<CartesianPos>& latest_scan = this->last_scans.back();
        int required_count = this->last_scans.size(); // Yêu cầu phải xuất hiện trong tất cả N lần quét
        
        for (const auto& p : latest_scan) {
            GridKey key = _grid_key(p);
            int cnt = 0;
            
            for (const auto& scan : this->last_scans) {
                // Tối ưu hóa: Thay vì tính lại keys cho mỗi lần quét, ta dùng set
                std::set<GridKey> keys;
                for(const auto& pp : scan) { keys.insert(_grid_key(pp)); }

                if (keys.count(key)) {
                    cnt++;
                }
            }

            if (cnt >= required_count) {
                stable_points.push_back(p);
            }
        }
    }

    // --- 7. Gom nhóm (Clustering) và Tìm Max Y ---
    std::vector<CartesianPos> toe_positions;
    if (!stable_points.empty()) {
        toe_positions = this->cluster_and_max_y(stable_points);
    }

    // --- 8. Quản lý Toe Points (Tương đương self.centers) ---
    if (!toe_positions.empty()) {
        for (const auto& pos : toe_positions) {
            this->centers.push_back({pos, 0, now_ms}); // Confidence và timestamp được thêm vào
        }
    }

    // Lọc Toe Points theo thời gian (Tương đương cutoff = now - MAX_TIME)
    this->centers.erase(
        std::remove_if(this->centers.begin(), this->centers.end(), 
            [&](const LidarPoint& c) {
                return c.timestamp_ms < cutoff_ms;
            }), 
        this->centers.end()
    );

    // --- 9. Kết quả Cuối cùng và Di chuột ---
    if (!this->centers.empty()) {
        const int SCREEN_W = 1920; // Giả định
        const int SCREEN_H = 1080; // Giả định
        
        // Chỉ lấy điểm toe point mới nhất (Hoặc điểm Y max nhất nếu muốn)
        for (const auto& c : this->centers) {
            int sx, sy;
            ScreenPos screen_pos = lidar_to_screen(c.pos.sx, c.pos.sy, SCREEN_W, SCREEN_H);
            //send touch
        }
    }
}