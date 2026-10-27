package com.eduxplore.control.ui;

import android.util.Log;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;

/** Đọc/ghi bộ thông số LiDAR trên máy: /sdcard/EduXplore/lidar_config.json (bộ đang dùng, Unity đọc) và
 *  lidar_config_slot1..4.json (bộ đã lưu, CÙNG định dạng nên adb push/pull lẫn nhau được).
 *  Chỉ đụng 8 khoá hình học; các khoá còn lại (lidar_model, touch_mode, c1_flip_*, ...) giữ nguyên khi ghi. */
public final class LidarConfigStore {
    private static final String TAG = "LidarConfigStore";
    private static final String DIR = "/sdcard/EduXplore";
    private static final String ACTIVE = "lidar_config.json";
    public static final int SLOTS = 4;

    /** Khoá, nhãn, bước +/- , cho phép số thập phân. Mặc định = bộ máy chiếu #2 (khớp LidarTouchBridge.LidarConfigJson). */
    public static final class Field {
        public final String key, label;
        public final double step, def;
        public final boolean decimal;
        Field(String key, String label, double step, double def, boolean decimal) {
            this.key = key; this.label = label; this.step = step; this.def = def; this.decimal = decimal;
        }
    }

    public static final Field[] FIELDS = {
        new Field("half_x", "half_x — nửa bề rộng sàn (mm)", 10, 1190, false),
        new Field("hight_floor", "hight_floor — chiều sâu sàn (mm)", 10, 1380, false),
        new Field("ymax", "ymax — mép gần LiDAR (mm)", 10, -800, false),
        new Field("shift_x_floor", "shift_x_floor — dịch vùng sàn theo X (mm)", 10, -110, false),
        new Field("shift_y", "shift_y — dịch toạ độ Y", 5, -15, false),
        new Field("shift_x", "shift_x — dịch toạ độ X", 5, -35, false),
        new Field("offset_angle", "offset_angle — xoay (độ, nhập được 4.5)", 0.5, -5, true),
        new Field("nums_point_report", "nums_point_report — số điểm báo", 1, 2, false),
    };

    private static File active() { return new File(DIR, ACTIVE); }
    private static File slotFile(int slot) { return new File(DIR, "lidar_config_slot" + slot + ".json"); }

    public static boolean slotExists(int slot) { return slotFile(slot).exists(); }

    /** Bộ đang dùng; file chưa có hoặc hỏng → bộ mặc định. */
    public static synchronized JSONObject readActive() {
        JSONObject o = read(active());
        return o != null ? o : defaults();
    }

    public static synchronized JSONObject readSlot(int slot) { return read(slotFile(slot)); }

    public static synchronized boolean writeActive(JSONObject o) { return write(active(), o); }

    /** Lưu bộ đang dùng vào slot (ghi đè). */
    public static synchronized boolean saveToSlot(int slot) { return write(slotFile(slot), readActive()); }

    /** Nạp slot thành bộ đang dùng. false nếu slot trống/hỏng. */
    public static synchronized boolean loadFromSlot(int slot) {
        JSONObject o = readSlot(slot);
        return o != null && write(active(), o);
    }

    /** Ghi 1 khoá số (số nguyên ghi dạng nguyên để bản app cũ vẫn đọc được). */
    public static void put(JSONObject o, String key, double v) {
        try {
            if (v == Math.rint(v) && Math.abs(v) < 1e9) o.put(key, (long) v); else o.put(key, v);
        } catch (Exception e) { Log.e(TAG, "put " + key + ": " + e); }
    }

    public static double get(JSONObject o, Field f) { return o.optDouble(f.key, f.def); }

    public static String fmt(double v) {
        return v == Math.rint(v) ? String.valueOf((long) v) : String.valueOf(v);
    }

    /** Tóm tắt 1 dòng cho slot: "half_x 1300 · sâu 1600 · góc -5" hoặc "(trống)". */
    public static String summary(int slot) {
        JSONObject o = readSlot(slot);
        if (o == null) return "(trống)";
        return "half_x " + fmt(o.optDouble("half_x", 0)) + " · sâu " + fmt(o.optDouble("hight_floor", 0))
            + " · góc " + fmt(o.optDouble("offset_angle", 0));
    }

    private static JSONObject defaults() {
        JSONObject o = new JSONObject();
        for (Field f : FIELDS) put(o, f.key, f.def);
        return o;
    }

    private static JSONObject read(File f) {
        if (!f.exists()) return null;
        try (FileInputStream is = new FileInputStream(f)) {
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = is.read(buf)) > 0) bos.write(buf, 0, n);
            return new JSONObject(bos.toString("UTF-8"));
        } catch (Exception e) {
            Log.e(TAG, "đọc " + f + " lỗi: " + e);
            return null;
        }
    }

    private static boolean write(File f, JSONObject o) {
        try {
            File dir = f.getParentFile();
            if (dir != null && !dir.exists()) dir.mkdirs();
            try (FileOutputStream os = new FileOutputStream(f)) {
                os.write(o.toString(2).getBytes(StandardCharsets.UTF_8));
            }
            return true;
        } catch (Exception e) {
            Log.e(TAG, "ghi " + f + " lỗi: " + e);
            return false;
        }
    }
}
