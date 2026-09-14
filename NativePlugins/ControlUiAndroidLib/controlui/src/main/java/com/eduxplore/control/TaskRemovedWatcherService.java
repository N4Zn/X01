package com.eduxplore.control;

import android.app.Service;
import android.content.Intent;
import android.os.IBinder;
import android.os.Process;
import android.util.Log;

/**
 * ControlActivity (display 0) và UnityPlayerActivity (display phụ) chạy trong CÙNG process
 * nhưng ở 2 task RIÊNG BIỆT — bắt buộc do launch cross-display cần FLAG_ACTIVITY_NEW_TASK.
 * Android chỉ dọn đúng 1 task khi user swipe tắt từ Recent Apps — task còn lại (game trên
 * máy chiếu) KHÔNG tự đóng theo, để lại màn hình phụ hiện game "mồ côi" mãi.
 *
 * Service.onTaskRemoved() là cách chính thức duy nhất để bắt sự kiện "task bị tắt qua
 * Recents" (Activity không có callback này) — dùng nó để kill cả process, dọn sạch cả 2
 * display cùng lúc thay vì chỉ 1 bên.
 *
 * Nút STOP (ControlActivity) KHÔNG còn đụng tới task Unity nữa (xem GameControlBridge.
 * OnStopRequested — chỉ dừng game + che đen display, giữ UnityPlayerActivity sống nguyên để
 * né bug Unity tự kill() process lúc Activity destroy), nên onTaskRemoved() ở đây giờ CHỈ
 * còn fire đúng 1 trường hợp: user thật sự swipe 1 trong 2 task qua Recents.
 */
public class TaskRemovedWatcherService extends Service {

    private static final String TAG = "TaskRemovedWatcher";

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onTaskRemoved(Intent rootIntent) {
        Log.i(TAG, "onTaskRemoved: 1 task bị tắt qua Recents — kill cả process để dọn sạch mọi display");
        super.onTaskRemoved(rootIntent);
        Process.killProcess(Process.myPid());
    }
}
