package com.eduxplore.posebridge;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;

/**
 * Cấu hình camera IP, đọc từ /sdcard/EduXplore/ipcamera.json (cùng chỗ với face_zones.json, frtest_config.json).
 * Nằm TRÊN THIẾT BỊ, không trong APK/repo: có mật khẩu camera. Mẫu: Tools/ip_camera_test/ipcamera.json.example.
 *
 * {"source":"ip","host":"192.168.2.64","port":80,"user":"admin","password":"...",
 *  "snapshotPath":"/ISAPI/Streaming/channels/101/picture","localIp":"192.168.2.20","mirror":true,"timeoutMs":5000}
 *
 * source: "ip" = dùng camera IP (chụp ảnh HTTP); thiếu/"uvc" = webcam USB như cũ (không có file cũng là UVC).
 * localIp: bind socket vào IP của eth0 (eth0 do K02 cấu hình tay, Android không coi là Network nên không có Network.getSocketFactory()).
 * mirror: lật gương ngang như webcam cũ (vùng nhận diện trong face_zones.json căn theo hình lật); đổi thì phải căn lại vùng.
 * rotate180: camera lắp ngược (xoay vật lý 180°) -> xoay ảnh lại 180° NGAY sau khi giải mã, trước khi lật gương/dò mặt/hiển thị;
 *   mọi thứ phía sau (preview, nhận diện, ảnh mẫu, face_zones.json) coi như camera lắp xuôi. Mặc định false.
 */
public final class IpCameraConfig {
    public static final String DEFAULT_FILE = "/sdcard/EduXplore/ipcamera.json";

    public final String source;
    public final boolean mirror;
    public final boolean rotate180;
    public final String host;
    public final int port;
    public final String user;
    public final String password;
    public final String snapshotPath;
    public final String localIp;
    public final int timeoutMs;
    /** Luồng MJPEG sub-stream cho PoseTest (mặc định kênh 102: hình nhỏ, nhiều fps). Có thể ghi đè trong ipcamera.json. */
    public final String streamPath;

    private IpCameraConfig(JSONObject o) {
        streamPath = o.optString("streamPath", "/ISAPI/Streaming/channels/102/httpPreview");
        source = o.optString("source", "uvc");
        mirror = o.optBoolean("mirror", true);
        rotate180 = o.optBoolean("rotate180", false);
        host = o.optString("host", "192.168.2.64");
        port = o.optInt("port", 80);
        user = o.optString("user", "admin");
        password = o.optString("password", "");
        snapshotPath = o.optString("snapshotPath", SnapshotClient.DEFAULT_PATH);
        localIp = o.optString("localIp", "");
        timeoutMs = o.optInt("timeoutMs", 5000);
    }

    public boolean isIp() { return "ip".equalsIgnoreCase(source); }

    /** Mã cho Core.flip để đạt (xoay 180° nếu rotate180) rồi (lật gương ngang nếu mirror); null = giữ nguyên ảnh.
     * Xoay 180° = flip(-1); cộng thêm lật ngang thì ra lật dọc = flip(0). */
    public Integer flipCode() {
        if (rotate180) return mirror ? 0 : -1;
        return mirror ? 1 : null;
    }

    /** Không có file / file hỏng → null (coi như dùng webcam UVC). */
    public static IpCameraConfig loadOrNull() {
        try {
            File f = new File(DEFAULT_FILE);
            return f.canRead() ? load(DEFAULT_FILE) : null;
        } catch (Exception e) {
            return null;
        }
    }

    public static IpCameraConfig load(String path) throws IOException, JSONException {
        File f = new File(path == null ? DEFAULT_FILE : path);
        InputStream in = new FileInputStream(f);
        try {
            ByteArrayOutputStream bo = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = in.read(buf)) != -1) bo.write(buf, 0, n);
            return new IpCameraConfig(new JSONObject(bo.toString("UTF-8")));
        } finally {
            in.close();
        }
    }
}
