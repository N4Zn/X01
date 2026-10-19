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

    // Loại LiDAR: "d200" (mặc định, LD14P 230400 baud, tự phát dữ liệu) hoặc "c1" (Slamtec RPLIDAR C1,
    // 460800 baud, phải gửi lệnh quét). Cả hai dùng chung cầu USB CP2102 (VID/PID giống nhau) nên chỉ
    // phân biệt bằng cấu hình `lidar_model` trong lidar_config.json (xem LidarTouchBridge.cs).
    private static volatile boolean sModelC1 = false;
    private static final int C1_BAUD = 460800;
    private static final int D200_BAUD = 230400;

    private static volatile boolean sStarted = false;
    private static volatile boolean sConnected = false;
    private static volatile boolean sUsbPermissionPending = false;

    private static ParcelFileDescriptor sPipeRead;
    private static ParcelFileDescriptor sPipeWrite;
    private static Thread sReaderThread;

    /** Giữ cho tương thích bản cũ: D200. */
    public static void start(Context context) {
        start(context, "d200");
    }

    /** Gọi 1 lần từ Unity C# (LidarTouchBridge.Start()) — an toàn gọi lại nhiều lần. model: "d200" | "c1". */
    public static void start(Context context, String model) {
        sModelC1 = model != null && model.trim().equalsIgnoreCase("c1");
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

        // Không dựa vào sự kiện cắm: đã có quyền (hasPermission) thì mở đọc luôn, không hỏi.
        // Chưa có quyền (lần đầu / sau reboot vì Android 10 không lưu quyền USB qua boot) thì
        // mới xin qua dialog, ngay lập tức, không chờ auto-grant.
        if (!usbManager.hasPermission(cp2102)) {
            if (!sUsbPermissionPending) {
                sUsbPermissionPending = true;
                Log.i(TAG, "tryConnect: chưa có quyền USB — xin qua dialog");
                PendingIntent pi = PendingIntent.getBroadcast(context, 0,
                        new Intent(ACTION_USB_PERMISSION), PendingIntent.FLAG_IMMUTABLE);
                usbManager.requestPermission(cp2102, pi);
            }
            return;
        }
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
        UsbEndpoint bulkOut = null;
        for (int i = 0; i < intf.getEndpointCount(); i++) {
            UsbEndpoint ep = intf.getEndpoint(i);
            if (ep.getType() != UsbConstants.USB_ENDPOINT_XFER_BULK) continue;
            if (ep.getDirection() == UsbConstants.USB_DIR_IN) {
                if (bulkIn == null) bulkIn = ep;
            } else if (bulkOut == null) {
                bulkOut = ep;
            }
        }
        if (bulkIn == null) {
            Log.e(TAG, "tryConnect: không tìm thấy bulk IN endpoint");
            conn.close();
            return;
        }

        final boolean c1 = sModelC1;
        initCp2102(conn, c1 ? C1_BAUD : D200_BAUD);
        if (c1) {
            if (bulkOut == null) {
                Log.e(TAG, "tryConnect: C1 cần bulk OUT endpoint để gửi lệnh quét nhưng không có");
                conn.close();
                return;
            }
            startC1Scan(conn, bulkIn, bulkOut);
        }

        try {
            ParcelFileDescriptor[] pipe = ParcelFileDescriptor.createPipe();
            sPipeRead = pipe[0];
            sPipeWrite = pipe[1];

            final UsbDeviceConnection finalConn = conn;
            final UsbEndpoint finalBulkIn = bulkIn;
            sReaderThread = new Thread(() -> {
                byte[] buf = new byte[4096];
                // C1: 7 byte đầu là response descriptor (A5 5A ...) — bỏ đi, chỉ chuyển dòng node cho native.
                byte[] head = new byte[64];
                int headLen = 0;
                boolean skipDescriptor = c1;
                try (FileOutputStream fos = new FileOutputStream(sPipeWrite.getFileDescriptor())) {
                    while (sConnected) {
                        int n = finalConn.bulkTransfer(finalBulkIn, buf, buf.length, 200);
                        if (n <= 0) continue;
                        if (skipDescriptor) {
                            int take = Math.min(n, head.length - headLen);
                            System.arraycopy(buf, 0, head, headLen, take);
                            headLen += take;
                            int idx = indexOfDescriptor(head, headLen);
                            if (idx >= 0 && headLen >= idx + 7) {
                                skipDescriptor = false;
                                int rest = headLen - (idx + 7);
                                if (rest > 0) fos.write(head, idx + 7, rest);
                                if (take < n) fos.write(buf, take, n - take);
                            } else if (headLen >= head.length) {
                                // không thấy descriptor — chuyển nguyên, native tự đồng bộ theo bit hợp lệ
                                skipDescriptor = false;
                                fos.write(head, 0, headLen);
                                if (take < n) fos.write(buf, take, n - take);
                            }
                            continue;
                        }
                        fos.write(buf, 0, n);
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

    private static int indexOfDescriptor(byte[] b, int len) {
        for (int i = 0; i + 1 < len; i++) {
            if ((b[i] & 0xFF) == 0xA5 && (b[i + 1] & 0xFF) == 0x5A) return i;
        }
        return -1;
    }

    /**
     * RPLIDAR C1: STOP → xả hết dữ liệu cũ → SCAN chuẩn (A5 20). Chế độ Standard là node 5 byte,
     * native (rplidar_ParseByte trong liblidar.cpp) đồng bộ theo bit S/!S + C nên không cần checksum.
     * Motor C1 tự quay, không cần DTR/PWM. Đã kiểm chứng trên COM6 laptop: FW 1.02, Standard 5 kS/s ~10 Hz.
     */
    private static void startC1Scan(UsbDeviceConnection conn, UsbEndpoint bulkIn, UsbEndpoint bulkOut) {
        byte[] stop = { (byte) 0xA5, 0x25 };
        byte[] scan = { (byte) 0xA5, 0x20 };
        conn.bulkTransfer(bulkOut, stop, stop.length, 500);
        try { Thread.sleep(100); } catch (InterruptedException ignored) { }
        byte[] tmp = new byte[4096];
        // xả dữ liệu còn sót của lần quét trước (nếu app khởi động lại khi C1 đang quay)
        for (int i = 0; i < 20 && conn.bulkTransfer(bulkIn, tmp, tmp.length, 30) > 0; i++) { /* drain */ }
        int w = conn.bulkTransfer(bulkOut, scan, scan.length, 500);
        Log.i(TAG, "startC1Scan: gửi A5 20, bulkTransfer=" + w);
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
