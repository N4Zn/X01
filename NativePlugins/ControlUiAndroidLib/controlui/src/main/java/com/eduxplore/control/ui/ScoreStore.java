package com.eduxplore.control.ui;

import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.FileInputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Kết quả chơi THẬT của từng học sinh — mỗi ván chơi xong ghi 1 dòng/người (số câu đúng / số câu đã
 *  trả lời trong ván đó, game nào, thuộc "học phần" nào). Điểm học phần của 1 người =
 *  Σ đúng / Σ đã chơi trong học phần đó, nhân 100 (xem {@link #scorePct}) — công thức tạm.
 *
 *  Lưu ở /sdcard/EduXplore/class_scores.json (cùng thư mục enrolled.json/classes.json, sống sót qua
 *  gỡ/cài lại app); không ghi được thì rơi về thư mục riêng của app. Chỉ lớp này đụng file đó. */
public final class ScoreStore {
    private static final String TAG = "ScoreStore";
    private static final String FILE_NAME = "class_scores.json";
    private static final int MAX_RECORDS = 5000;

    public static final class Record {
        public final long time;
        public final String name, game, phan;
        public final int correct, answered;
        public final float avgTime;
        public Record(long time, String name, String game, String phan, int correct, int answered, float avgTime) {
            this.time = time; this.name = name; this.game = game; this.phan = phan;
            this.correct = correct; this.answered = answered; this.avgTime = avgTime;
        }
    }

    private final File primary;
    private final File fallback;
    private final List<Record> records = new ArrayList<>();

    public ScoreStore(File fallbackDir) {
        this.primary = new File("/sdcard/EduXplore", FILE_NAME);
        this.fallback = fallbackDir != null ? new File(fallbackDir, FILE_NAME) : null;
    }

    /** Thang 100: số câu đúng / tổng số câu đã chơi. Chưa chơi câu nào → -1 (chưa có điểm). */
    public static int scorePct(int correct, int answered) {
        return answered <= 0 ? -1 : Math.round(100f * correct / answered);
    }

    public synchronized void load() {
        records.clear();
        File f = primary.exists() ? primary : (fallback != null && fallback.exists() ? fallback : null);
        if (f == null) return;
        try (InputStream is = new FileInputStream(f)) {
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[8192];
            int n;
            while ((n = is.read(buf)) > 0) bos.write(buf, 0, n);
            JSONArray arr = new JSONObject(bos.toString("UTF-8")).optJSONArray("records");
            if (arr == null) return;
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.getJSONObject(i);
                records.add(new Record(o.optLong("t"), o.optString("name"), o.optString("game"), o.optString("phan"),
                        o.optInt("correct"), o.optInt("answered"), (float) o.optDouble("avgTime", 0)));
            }
        } catch (Exception e) {
            Log.e(TAG, "đọc " + f + " lỗi: " + e);
        }
    }

    public synchronized void add(List<Record> newRecords) {
        if (newRecords == null || newRecords.isEmpty()) return;
        records.addAll(newRecords);
        while (records.size() > MAX_RECORDS) records.remove(0);
        save();
    }

    private void save() {
        try {
            JSONArray arr = new JSONArray();
            for (Record r : records) {
                JSONObject o = new JSONObject();
                o.put("t", r.time); o.put("name", r.name); o.put("game", r.game); o.put("phan", r.phan);
                o.put("correct", r.correct); o.put("answered", r.answered); o.put("avgTime", r.avgTime);
                arr.put(o);
            }
            byte[] bytes = new JSONObject().put("records", arr).toString().getBytes(StandardCharsets.UTF_8);
            if (!writeAtomic(primary, bytes) && (fallback == null || !writeAtomic(fallback, bytes))) {
                Log.e(TAG, "không ghi được class_scores.json ở đâu cả");
            }
        } catch (Exception e) {
            Log.e(TAG, "save lỗi: " + e);
        }
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

    // ── Truy vấn ──────────────────────────────────────────────────────────────

    /** học phần → {tổng đúng, tổng đã chơi} của 1 học sinh, theo thứ tự xuất hiện đầu tiên. */
    public synchronized Map<String, int[]> phanTotals(String studentName) {
        Map<String, int[]> out = new LinkedHashMap<>();
        for (Record r : records) {
            if (!r.name.equals(studentName)) continue;
            int[] t = out.get(r.phan);
            if (t == null) { t = new int[2]; out.put(r.phan, t); }
            t[0] += r.correct; t[1] += r.answered;
        }
        return out;
    }

    /** Số ván học sinh này đã chơi game `game`. */
    public synchronized int plays(String studentName, String game) {
        int n = 0;
        for (Record r : records) if (r.name.equals(studentName) && r.game.equals(game)) n++;
        return n;
    }

    /** Các dòng mới nhất (mới trước) của những học sinh trong `names`, tối đa `limit`. */
    public synchronized List<Record> recent(Set<String> names, int limit) {
        List<Record> out = new ArrayList<>();
        for (int i = records.size() - 1; i >= 0 && out.size() < limit; i--) {
            if (names.contains(records.get(i).name)) out.add(records.get(i));
        }
        return out;
    }

    public synchronized List<Record> all() { return Collections.unmodifiableList(new ArrayList<>(records)); }
}
