package com.eduxplore.lidar;

import android.app.Activity;
import android.content.Intent;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbManager;
import android.os.Bundle;
import android.util.Log;

/**
 * Nhận USB_DEVICE_ATTACHED cho Lidar (CP2102) — Android tự cấp quyền không cần dialog vì
 * khớp {@code res/xml/lidar_usb_device_filter.xml}. Lưu device rồi báo LidarUsbBridge thử
 * kết nối ngay (idempotent — an toàn gọi lại nhiều lần).
 */
public class LidarUsbAttachActivity extends Activity {

    private static final String TAG = "LidarUsbAttachActivity";

    public static volatile UsbDevice grantedDevice = null;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        UsbDevice device = getIntent().getParcelableExtra(UsbManager.EXTRA_DEVICE);
        if (device != null) {
            Log.i(TAG, "USB_DEVICE_ATTACHED: VID=" + device.getVendorId()
                    + " PID=" + device.getProductId()
                    + " name=" + device.getProductName());
            grantedDevice = device;
        }

        LidarUsbBridge.start(getApplicationContext());

        finish();
    }
}
