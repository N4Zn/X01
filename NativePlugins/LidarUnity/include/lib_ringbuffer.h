#ifndef __LIB_RINGBUFFER_H__
#define __LIB_RINGBUFFER_H__

#include <iostream>
#include <atomic>
#include <thread>
#include <chrono>
#include <vector>
#include <mutex>
#include <condition_variable> // ⭐ Cần thiết cho Wait/Notify
#include <stddef.h>          // Cho size_t
#include "liblog.h"

//#define TAG "FloorGame_RingBuffer"


template <typename T>
class RingBuffer {
public:
    RingBuffer(size_t capacity)
        : capacity(capacity), front(0), back(0), size(0) {
        buffer.resize(capacity);
    }

    bool enqueue(const T& item) {
        std::lock_guard<std::mutex> lock(mtx);
        if (size == capacity) {
            front = (front + 1) % capacity;
        } else {
            ++size;
        }
        buffer[back] = item;
        back = (back + 1) % capacity;
        cv.notify_one();
        return true;
    }

    bool enqueueArray(const T* data, size_t length) {
        std::lock_guard<std::mutex> lock(mtx);
        bool did_enqueue = false;

        for (size_t i = 0; i < length; ++i) {
            if (size == capacity) {
                front = (front + 1) % capacity;
            } else {
                ++size;
            }
            buffer[back] = data[i];
            back = (back + 1) % capacity;
            did_enqueue = true;
        }
        
        // ⭐ BƯỚC 2: Thông báo sau khi ghi xong toàn bộ mảng
        if (did_enqueue) {
            cv.notify_one(); 
        }
        
        return true;
    }
    bool wait_and_dequeue(T& item) {
        // Sử dụng unique_lock cho phép chúng ta unlock và lock lại (trong wait)
        std::unique_lock<std::mutex> lock(mtx);
        
        // ⭐ CHẶN LUỒNG: Luồng sẽ ngủ (blocking) nếu hàng đợi trống (size == 0).
        // Luồng sẽ thức dậy khi cv.notify_one() được gọi VÀ size > 0.
        cv.wait(lock, [this] {
            return size > 0;
        });

        // Sau khi thức dậy, chúng ta đã giữ khóa và size > 0.
        // Thực hiện dequeue an toàn
        item = buffer[front];
        front = (front + 1) % capacity;
        --size;
        
        return true; // Luôn trả về true vì chúng ta chỉ thức dậy khi size > 0
    }

    bool dequeue(T& item) {
        std::lock_guard<std::mutex> lock(mtx);
        if (size == 0) return false;
        item = buffer[front];
        front = (front + 1) % capacity;
        --size;
        return true;
    }

    size_t getSize(){
        std::lock_guard<std::mutex> lock(mtx);
        return size;
    }

private:
    std::vector<T> buffer;
    size_t capacity;
    size_t front;
    size_t back;
    size_t size;
    std::mutex mtx;
    std::condition_variable cv;
};


#endif