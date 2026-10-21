package com.eduxplore.faceplugin;

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
 */
public final class IpCameraConfig {
    public static final String DEFAULT_FILE = "/sdcard/EduXplore/ipcamera.json";

    public final String source;
    public final boolean mirror;
    public final String host;
    public final int port;
    public final String user;
    public final String password;
    public final String snapshotPath;
    public final String localIp;
    public final int timeoutMs;

    private IpCameraConfig(JSONObject o) {
        source = o.optString("source", "uvc");
        mirror = o.optBoolean("mirror", true);
        host = o.optString("host", "192.168.2.64");
        port = o.optInt("port", 80);
        user = o.optString("user", "admin");
        password = o.optString("password", "");
        snapshotPath = o.optString("snapshotPath", SnapshotClient.DEFAULT_PATH);
        localIp = o.optString("localIp", "");
        timeoutMs = o.optInt("timeoutMs", 5000);
    }

    public boolean isIp() { return "ip".equalsIgnoreCase(source); }

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
