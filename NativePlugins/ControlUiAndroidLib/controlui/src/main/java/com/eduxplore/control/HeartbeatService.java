package com.eduxplore.control;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;

/**
 * Service rỗng, chạy trong process CHÍNH (cùng process với ControlActivity/UnityPlayerActivity)
 * — không làm gì cả, chỉ tồn tại để WatchdogService (process riêng :watchdog, xem
 * WatchdogService.java) bind vào và nhận biết khi nào process chính chết bất ngờ (crash,
 * hoặc Unity tự kill() cả process lúc UnityPlayerActivity bị destroy — xem lịch sử bug
 * "Stop thoát cả app"). Không tự làm gì để relaunch — logic đó nằm ở WatchdogService.
 */
public class HeartbeatService extends Service {
    private final IBinder binder = new Binder();

    @Override
    public IBinder onBind(Intent intent) {
        return binder;
    }
}
