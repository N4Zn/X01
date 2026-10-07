package com.eduxplore.control.ui;

import android.util.Log;
import org.json.JSONObject;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;

/** Lưu trữ cài đặt game dưới dạng JSON.
 *  Lưu ở /sdcard/EduXplore/game_settings.json; fallback về thư mục riêng của app. */
public final class SettingsStore {
    private static final String TAG = "SettingsStore";
    private static final String FILE_NAME = "game_settings.json";

    public float musicVolume = 0.5f; // mặc định ban đầu 50% (khi chưa có game_settings.json)
    public float sfxVolume = 1.0f;
    public int gameTime = 100;
    public float roundEndDelay = 2.0f;
    public float flowSpeed = 1.0f;
    public int waitForClear = 1; // 1: bật, 0: tắt, -1: không đổi
    public int showRealName = 0; // 0: tên thường gọi (mặc định), 1: tên thật (cả 2 chỉ hiện 2 tiếng cuối)

    private final File primary;
    private final File fallback;

    public SettingsStore(File fallbackDir) {
        this.primary = new File("/sdcard/EduXplore", FILE_NAME);
        this.fallback = fallbackDir != null ? new File(fallbackDir, FILE_NAME) : null;
    }

    public synchronized void load() {
        File f = primary.exists() ? primary : (fallback != null && fallback.exists() ? fallback : null);
        if (f == null) return;
        try (FileInputStream is = new FileInputStream(f)) {
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = is.read(buf)) > 0) bos.write(buf, 0, n);
            JSONObject o = new JSONObject(bos.toString("UTF-8"));
            
            musicVolume = (float) o.optDouble("musicVolume", musicVolume);
            sfxVolume = (float) o.optDouble("sfxVolume", sfxVolume);
            gameTime = o.optInt("gameTime", gameTime);
            roundEndDelay = (float) o.optDouble("roundEndDelay", roundEndDelay);
            flowSpeed = (float) o.optDouble("flowSpeed", flowSpeed);
            waitForClear = o.optInt("waitForClear", waitForClear);
            showRealName = o.optInt("showRealName", showRealName);
        } catch (Exception e) {
            Log.e(TAG, "đọc " + f + " lỗi: " + e);
        }
    }

    public synchronized void save() {
        try {
            JSONObject o = toJson();
            byte[] bytes = o.toString().getBytes(StandardCharsets.UTF_8);
            if (!writeAtomic(primary, bytes) && (fallback == null || !writeAtomic(fallback, bytes))) {
                Log.e(TAG, "không ghi được game_settings.json ở đâu cả");
            }
        } catch (Exception e) {
            Log.e(TAG, "save lỗi: " + e);
        }
    }

    public JSONObject toJson() throws org.json.JSONException {
        JSONObject o = new JSONObject();
        o.put("musicVolume", musicVolume);
        o.put("sfxVolume", sfxVolume);
        o.put("gameTime", gameTime);
        o.put("roundEndDelay", roundEndDelay);
        o.put("flowSpeed", flowSpeed);
        o.put("waitForClear", waitForClear);
        o.put("showRealName", showRealName);
        return o;
    }

    private static boolean writeAtomic(File target, byte[] bytes) {
        try {
            File dir = target.getParentFile();
            if (dir != null && !dir.exists() && !dir.mkdirs()) return false;
            File tmp = new File(dir, target.getName() + ".tmp");
            try (FileOutputStream os = new FileOutputStream(tmp)) { os.write(bytes); }
            if (target.exists() && !target.delete()) return false;
            return tmp.renameTo(target);
        } catch (Exception e) {
            return false;
        }
    }
}
