#include <iostream>
#include <iomanip>
#include <cstdint>
#include <string.h>
#include <chrono>
#include <mutex>
#include <functional>
#include <cmath>
#include "touch_report.h"


#define H_RECTANGE 1000.0
#define W_RECTANGE 2000.0
#define H_OFFSET 2000.0
#define ORIGINAL_X 0.0
#define ORIGINAL_Y H_OFFSET


bool point_is_in_range(DataPoint dpoint){

    if((dpoint.y >= ORIGINAL_Y) && (dpoint.y <= ORIGINAL_Y + H_RECTANGE)){
        if((dpoint.x >= ORIGINAL_X - W_RECTANGE) && (dpoint.x <= ORIGINAL_X)){
            return true;
        }
    }
    return false;
}












