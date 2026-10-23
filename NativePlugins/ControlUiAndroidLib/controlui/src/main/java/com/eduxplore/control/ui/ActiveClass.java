package com.eduxplore.control.ui;

import android.util.Log;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;

/**
 * Lớp đang dùng — chung cho tab Lớp học (ControlActivity), Quản lý lớp (FaceEnroll) và nhận diện mặt trong game.
 * Lưu /sdcard/EduXplore/active_class.txt (1 dòng = tên lớp). Cùng format với FaceEnroll ActiveClass.kt và plugin game FaceDatabase.kt.
 */
public final class ActiveClass {
    private static final String TAG = "ActiveClass";
    private static final File FILE = new File("/sdcard/EduXplore", "active_class.txt");

    private ActiveClass() {}

    /** Tên lớp đang dùng, null nếu chưa có file. */
    public static String read() {
        try {
            if (!FILE.canRead()) return null;
            try (FileInputStream is = new FileInputStream(FILE)) {
                byte[] buf = new byte[(int) Math.min(FILE.length(), 1024)];
                int n = is.read(buf);
                String s = n > 0 ? new String(buf, 0, n, StandardCharsets.UTF_8).trim() : "";
                return s.isEmpty() ? null : s;
            }
        } catch (Exception e) {
            Log.e(TAG, "read lỗi: " + e);
            return null;
        }
    }

    public static void write(String className) {
        if (className == null || className.trim().isEmpty()) return;
        try {
            if (className.equals(read())) return;
            File dir = FILE.getParentFile();
            if (dir != null && !dir.exists()) dir.mkdirs();
            try (FileOutputStream os = new FileOutputStream(FILE)) {
                os.write(className.getBytes(StandardCharsets.UTF_8));
            }
        } catch (Exception e) {
            Log.e(TAG, "write lỗi: " + e);
        }
    }
}
