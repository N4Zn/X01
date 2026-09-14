#ifndef __LIBLOGS_H__
#define __LIBLOGS_H__

#include <iostream>
#include "android/log.h"

#define LOGI(tags, fmt, args...) __android_log_print(ANDROID_LOG_INFO,  tags, fmt, ##args)
#define LOGD(tags, fmt, args...) __android_log_print(ANDROID_LOG_DEBUG, tags, fmt, ##args)
#define LOGE(tags, fmt, args...) __android_log_print(ANDROID_LOG_ERROR, tags, fmt, ##args)

#endif