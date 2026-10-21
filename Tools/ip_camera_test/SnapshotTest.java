package com.eduxplore.faceplugin;

import java.io.File;
import java.io.FileOutputStream;

import javax.net.SocketFactory;

/**
 * Chạy thử SnapshotClient trực tiếp trên K02 (qua app_process, không cần build aar/APK).
 * Đọc /sdcard/EduXplore/ipcamera.json, chụp N ảnh liên tiếp, in thời gian + kích thước, lưu JPEG.
 * Cách chạy: Tools/ip_camera_test/README.md
 *
 * args: [số ảnh=5] [thư mục lưu=/sdcard/EduXplore/snaps] [đường dẫn config]
 */
public final class SnapshotTest {
    public static void main(String[] args) throws Exception {
        int count = args.length > 0 ? Integer.parseInt(args[0]) : 5;
        String outDir = args.length > 1 ? args[1] : "/sdcard/EduXplore/snaps";
        String cfgPath = args.length > 2 ? args[2] : null;

        IpCameraConfig c = IpCameraConfig.load(cfgPath);
        SocketFactory sf = c.localIp.isEmpty() ? null : SnapshotClient.bindTo(c.localIp);
        System.out.println("camera " + c.host + ":" + c.port + " user=" + c.user + " path=" + c.snapshotPath
                + " localIp=" + (c.localIp.isEmpty() ? "(mặc định)" : c.localIp) + " passwordSet=" + !c.password.isEmpty());

        SnapshotClient cli = new SnapshotClient(c.host, c.port, c.user, c.password, sf, c.timeoutMs);
        new File(outDir).mkdirs();
        long total = 0;
        int ok = 0;
        for (int i = 1; i <= count; i++) {
            long t0 = System.nanoTime();
            try {
                byte[] jpg = cli.fetch(c.snapshotPath);
                long ms = (System.nanoTime() - t0) / 1_000_000;
                String dim = jpegSize(jpg);
                File f = new File(outDir, "snap_" + i + ".jpg");
                FileOutputStream fo = new FileOutputStream(f);
                try { fo.write(jpg); } finally { fo.close(); }
                System.out.println("#" + i + "  " + ms + " ms  " + jpg.length + " B  " + dim + "  -> " + f.getPath());
                total += ms;
                ok++;
            } catch (Exception e) {
                System.out.println("#" + i + "  LỖI sau " + (System.nanoTime() - t0) / 1_000_000 + " ms: " + e);
                if (String.valueOf(e.getMessage()).startsWith("HTTP 401") || String.valueOf(e.getMessage()).startsWith("Cần mật khẩu")) break;
            }
        }
        if (ok > 0) System.out.println("OK " + ok + "/" + count + ", trung bình " + (total / ok) + " ms/ảnh");
    }

    /** Đọc WxH từ marker SOFn của JPEG (không cần decode). */
    private static String jpegSize(byte[] b) {
        int i = 2;
        while (i + 9 < b.length) {
            if ((b[i] & 0xFF) != 0xFF) { i++; continue; }
            int m = b[i + 1] & 0xFF;
            if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC) {
                int h = ((b[i + 5] & 0xFF) << 8) | (b[i + 6] & 0xFF);
                int w = ((b[i + 7] & 0xFF) << 8) | (b[i + 8] & 0xFF);
                return w + "x" + h;
            }
            int len = ((b[i + 2] & 0xFF) << 8) | (b[i + 3] & 0xFF);
            i += 2 + len;
        }
        return "?x?";
    }
}
