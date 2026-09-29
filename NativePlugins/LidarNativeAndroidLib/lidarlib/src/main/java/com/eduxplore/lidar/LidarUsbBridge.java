package com.eduxplore.lidar;

import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.hardware.usb.UsbConstants;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbDeviceConnection;
import android.hardware.usb.UsbEndpoint;
import android.hardware.usb.UsbInterface;
import android.hardware.usb.UsbManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.ParcelFileDescriptor;
import android.util.Log;

import java.io.FileOutputStream;
import java.io.IOException;
import java.lang.reflect.Method;

/**
 * Đọc Lidar (CP2102) qua Android USB Host API — không cần root, không cần cp210x.ko.
 * Port từ MyNativeApp_v4/LidarService.openUsbDirect(), bỏ hết phần liên quan đến việc
 * bơm touch vào Android (dispatchGesture/injectInputEvent) — touch giờ xử lý ngay trong
 * process Unity (xem liblidar_unity.so + LidarTouchBridge.cs), không "inject" đi đâu cả.
 *
 * Cầu nối 1 chiều duy nhất sang native: khi có fd đọc dữ liệu sẵn sàng, báo cho Unity qua
 * UnitySendMessage — C# tự gọi Lidar_SetUartFd()/Lidar_Init() (P/Invoke trực tiếp vào
 * liblidar_unity.so, không qua Java/JNI ở đây nữa).
 */
public class LidarUsbBridge {

    private static final String TAG = "LidarUsbBridge";

    private static final int CP2102_VID = 0x10C4;
    private static final int CP2102_PID = 0xEA60;
    private static final String ACTION_USB_PERMISSION = "com.eduxplore.lidar.USB_PERMISSION";

    // GameObject Unity nhận callback — xem LidarTouchBridge.cs (phải trùng tên GameObject).
    private static final String UNITY_GAME_OBJECT = "LidarTouchBridge";

    private static volatile boolean sStarted = false;
    private static volatile boolean sConnected = false;
    private static volatile boolean sUsbPermissionPending = false;
    private static int sSilentRetries = 0;

    // "Grace period" trước khi tự xin quyền qua dialog — nhường chỗ cho cơ chế auto-grant
    // im lặng của Android (USB_DEVICE_ATTACHED + usb_device_filter.xml, xử lý qua
    // LidarUsbAttachActivity) chạy xong trước. Không có grace period này, tryConnect() gọi
    // ngay lúc app khởi động thắng cuộc đua và LUÔN hiện dialog xin quyền — kể cả khi user
    // đã tick "always" ở lần trước, vì đó là 2 cơ chế cấp quyền KHÁC NHAU (dialog vs.
    // auto-grant), tick "always" trên dialog không tắt được việc dialog tự hiện lại lần sau
    // nếu code cứ gọi requestPermission() ngay từ đầu mỗi lần app khởi động.
    private static final int MAX_SILENT_RETRIES = 6;
    private static final long SILENT_RETRY_DELAY_MS = 500;
    private static final Handler sHandler = new Handler(Looper.getMainLooper());

    private static ParcelFileDescriptor sPipeRead;
    private static ParcelFileDescriptor sPipeWrite;
    private static Thread sReaderThread;

    /** Gọi 1 lần từ Unity C# (LidarTouchBridge.Start()) — an toàn gọi lại nhiều lần. */
    public static void start(Context context) {
        Context appCtx = context.getApplicationContext();

        if (!sStarted) {
            sStarted = true;
            IntentFilter filter = new IntentFilter(ACTION_USB_PERMISSION);
            BroadcastReceiver receiver = new BroadcastReceiver() {
                @Override
                public void onReceive(Context c, Intent intent) {
                    if (ACTION_USB_PERMISSION.equals(intent.getAction())) {
                        sUsbPermissionPending = false;
                        tryConnect(appCtx);
                    }
                }
            };
            if (Build.VERSION.SDK_INT >= 33) {
                appCtx.registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED);
            } else {
                appCtx.registerReceiver(receiver, filter);
            }
        }

        if (!sConnected) tryConnect(appCtx);
    }

    private static void tryConnect(Context context) {
        if (sConnected) return;

        UsbManager usbManager = (UsbManager) context.getSystemService(Context.USB_SERVICE);
        if (usbManager == null) return;

        UsbDevice cp2102 = LidarUsbAttachActivity.grantedDevice;
        if (cp2102 == null) {
            for (UsbDevice dev : usbManager.getDeviceList().values()) {
                if (dev.getVendorId() == CP2102_VID && dev.getProductId() == CP2102_PID) {
                    cp2102 = dev;
                    break;
                }
            }
        }
        if (cp2102 == null) {
            Log.i(TAG, "tryConnect: chưa thấy Lidar (CP2102)");
            return;
        }

        if (!usbManager.hasPermission(cp2102)) {
            if (sSilentRetries < MAX_SILENT_RETRIES) {
                sSilentRetries++;
                Log.i(TAG, "tryConnect: chưa có quyền — chờ auto-grant im lặng (" + sSilentRetries + "/" + MAX_SILENT_RETRIES + ")");
                final Context ctxForRetry = context;
                sHandler.postDelayed(() -> tryConnect(ctxForRetry), SILENT_RETRY_DELAY_MS);
                return;
            }
            if (!sUsbPermissionPending) {
                sUsbPermissionPending = true;
                Log.i(TAG, "tryConnect: hết grace period, xin quyền USB qua dialog (fallback)");
                PendingIntent pi = PendingIntent.getBroadcast(context, 0,
                        new Intent(ACTION_USB_PERMISSION), PendingIntent.FLAG_IMMUTABLE);
                usbManager.requestPermission(cp2102, pi);
            }
            return;
        }
        sSilentRetries = 0;
        sUsbPermissionPending = false;
        LidarUsbAttachActivity.grantedDevice = null; // consumed

        UsbDeviceConnection conn = usbManager.openDevice(cp2102);
        if (conn == null) {
            Log.e(TAG, "tryConnect: không mở được device");
            return;
        }

        UsbInterface intf = cp2102.getInterface(0);
        conn.claimInterface(intf, true);
        UsbEndpoint bulkIn = null;
        for (int i = 0; i < intf.getEndpointCount(); i++) {
            UsbEndpoint ep = intf.getEndpoint(i);
            if (ep.getType() == UsbConstants.USB_ENDPOINT_XFER_BULK
                    && ep.getDirection() == UsbConstants.USB_DIR_IN) {
                bulkIn = ep;
                break;
            }
        }
        if (bulkIn == null) {
            Log.e(TAG, "tryConnect: không tìm thấy bulk IN endpoint");
            conn.close();
            return;
        }

        initCp2102(conn, 230400);

        try {
            ParcelFileDescriptor[] pipe = ParcelFileDescriptor.createPipe();
            sPipeRead = pipe[0];
            sPipeWrite = pipe[1];

            final UsbDeviceConnection finalConn = conn;
            final UsbEndpoint finalBulkIn = bulkIn;
            sReaderThread = new Thread(() -> {
                byte[] buf = new byte[4096];
                try (FileOutputStream fos = new FileOutputStream(sPipeWrite.getFileDescriptor())) {
                    while (sConnected) {
                        int n = finalConn.bulkTransfer(finalBulkIn, buf, buf.length, 200);
                        if (n > 0) fos.write(buf, 0, n);
                    }
                } catch (IOException e) {
                    Log.w(TAG, "USB reader kết thúc: " + e.getMessage());
                } finally {
                    finalConn.close();
                }
            }, "lidar-usb-reader");
            sReaderThread.setDaemon(true);
            sConnected = true;
            sReaderThread.start();

            Log.i(TAG, "tryConnect: CP2102 đã mở, pipe fd=" + sPipeRead.getFd());
            sendToUnity("OnUartFdReady", String.valueOf(sPipeRead.getFd()));
        } catch (IOException e) {
            Log.e(TAG, "tryConnect: tạo pipe lỗi: " + e.getMessage());
            conn.close();
        }
    }

    // Gọi UnityPlayer.UnitySendMessage() qua reflection — tránh phụ thuộc biên dịch trực
    // tiếp vào com.unity3d.player.UnityPlayer (module .androidlib này được unityLibrary
    // include, không phải ngược lại, nên không chắc thấy class đó lúc compile; ở runtime
    // thì luôn có, cùng 1 ClassLoader trong APK).
    private static void sendToUnity(String method, String message) {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            Method m = unityPlayerClass.getMethod("UnitySendMessage", String.class, String.class, String.class);
            m.invoke(null, UNITY_GAME_OBJECT, method, message);
        } catch (Exception e) {
            Log.e(TAG, "sendToUnity(" + method + ") failed: " + e);
        }
    }

    private static void initCp2102(UsbDeviceConnection conn, int baud) {
        conn.controlTransfer(0x41, 0x00, 0x0001, 0, null, 0, 1000);
        byte[] b = { (byte) (baud), (byte) (baud >> 8), (byte) (baud >> 16), (byte) (baud >> 24) };
        conn.controlTransfer(0x40, 0x1E, 0, 0, b, 4, 1000);
        conn.controlTransfer(0x41, 0x03, 0x0800, 0, null, 0, 1000);
    }
}
