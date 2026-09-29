package com.eduxplore.control;

import android.app.Service;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.util.Log;

/**
 * Chạy trong process RIÊNG (android:process=":watchdog" — xem AndroidManifest.xml), tách biệt
 * hoàn toàn với process chính (ControlActivity + UnityPlayerActivity). Bind vào
 * HeartbeatService (rỗng, sống trong process chính) — Android tự gọi onServiceDisconnected()
 * ĐÚNG LÚC process chính chết BẤT NGỜ (crash hoặc bị OS kill), bất kể lý do gì, không cần biết
 * trước nguyên nhân. Nhờ đó bắt được đúng bug đã xác nhận: Unity tự gọi
 * Process.killProcess() cả process dùng chung khi UnityPlayerActivity bị destroy (bấm Stop) —
 * không sửa được từ code app, nên đứng ngoài process đó mà quan sát + tự khởi động lại
 * ControlActivity, KHÔNG cần đăng ký app này làm Home/Launcher của máy (yêu cầu của user —
 * chỉ muốn 1 service theo dõi, không muốn đổi Home).
 *
 * Không xung đột với TaskRemovedWatcherService (vẫn giữ nguyên, xử lý case khác: user chủ
 * động swipe 1 trong 2 task qua Recents) — watchdog này chỉ phản ứng khi process THỰC SỰ đã
 * chết, không quan tâm lý do.
 */
public class WatchdogService extends Service {

    private static final String TAG = "Watchdog";
    private static final long REBIND_DELAY_MS = 1500;

    private final Handler handler = new Handler(Looper.getMainLooper());
    private boolean bound = false;

    private final ServiceConnection connection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            bound = true;
            Log.i(TAG, "onServiceConnected: process chính còn sống (hoặc vừa khởi động lại)");
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            // Android chỉ gọi callback này khi process host của service bị chết ĐỘT NGỘT
            // (crash/bị kill) — không gọi khi tự unbindService() bình thường.
            bound = false;
            Log.w(TAG, "onServiceDisconnected: process chính vừa chết — tự khởi động lại ControlActivity");
            relaunchControlActivity();
            handler.postDelayed(WatchdogService.this::tryBind, REBIND_DELAY_MS);
        }
    };

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        tryBind();
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null; // watchdog không cần ai bind vào nó
    }

    private void tryBind() {
        if (bound) return;
        try {
            boolean ok = bindService(new Intent(this, HeartbeatService.class), connection, Context.BIND_AUTO_CREATE);
            if (!ok) {
                Log.w(TAG, "tryBind: bindService thất bại — thử lại sau " + REBIND_DELAY_MS + "ms");
                handler.postDelayed(this::tryBind, REBIND_DELAY_MS);
            }
        } catch (Exception e) {
            Log.e(TAG, "tryBind lỗi: " + e.getMessage(), e);
            handler.postDelayed(this::tryBind, REBIND_DELAY_MS);
        }
    }

    private void relaunchControlActivity() {
        try {
            Intent intent = new Intent(this, ControlActivity.class);
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
            startActivity(intent);
        } catch (Exception e) {
            Log.e(TAG, "relaunchControlActivity lỗi: " + e.getMessage(), e);
        }
    }
}
