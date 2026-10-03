import android.hardware.usb.UsbDevice;
import android.os.Bundle;
import android.os.IBinder;

import java.lang.reflect.Method;

/**
 * Chạy bằng root (app_process) để cấp quyền USB cho 1 app — Android 10 không lưu quyền USB qua
 * reboot và app không tự gọi su được ("not allowed"). Dùng: UsbGrant <uid> [vid:pid ...]
 * (không có vid:pid thì cấp mọi device đang cắm). In "GRANT_OK <n>" khi xong.
 */
public class UsbGrant {
    public static void main(String[] args) {
        try {
            int uid = Integer.parseInt(args[0]);
            Class<?> sm = Class.forName("android.os.ServiceManager");
            IBinder binder = (IBinder) sm.getMethod("getService", String.class).invoke(null, "usb");
            Class<?> stub = Class.forName("android.hardware.usb.IUsbManager$Stub");
            Object svc = stub.getMethod("asInterface", IBinder.class).invoke(null, binder);
            Class<?> iface = Class.forName("android.hardware.usb.IUsbManager");
            Method getDeviceList = iface.getMethod("getDeviceList", Bundle.class);
            Method grant = iface.getMethod("grantDevicePermission", UsbDevice.class, int.class);

            Bundle devices = new Bundle();
            getDeviceList.invoke(svc, devices);
            int n = 0;
            for (String key : devices.keySet()) {
                Object o = devices.get(key);
                if (!(o instanceof UsbDevice)) continue;
                UsbDevice d = (UsbDevice) o;
                boolean want = args.length == 1;
                for (int i = 1; i < args.length && !want; i++) {
                    want = args[i].equals(d.getVendorId() + ":" + d.getProductId());
                }
                if (want) {
                    grant.invoke(svc, d, uid);
                    n++;
                    System.out.println("granted " + key + " " + d.getVendorId() + ":" + d.getProductId());
                }
            }
            System.out.println("GRANT_OK " + n);
        } catch (Throwable e) {
            System.out.println("GRANT_FAIL " + e);
            e.printStackTrace(System.out);
        }
    }
}
