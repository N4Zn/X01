#ifndef __LIB_TOUCH_HANDLE_H__
#define __LIB_TOUCH_HANDLE_H__

#include <iostream>
#include <liblidar.h>

struct DataPointInfo {
    bool reported;
    double x_in_used;
    double y_in_used;

};


bool point_is_in_range(DataPoint dpoint);

bool convert_realpoint_to_touchevent(DataPoint dpoint);

#endif