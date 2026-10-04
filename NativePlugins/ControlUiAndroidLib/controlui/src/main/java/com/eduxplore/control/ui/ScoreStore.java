package com.eduxplore.control.ui;

import android.util.Log;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Kết quả chơi THẬT của từng học sinh, lưu MỖI CÂU/ROUND 1 dòng (Unity đẩy từng round qua
 *  ControlActivity.OnRound). Từ đây tính điểm và hiện lịch sử chi tiết từng câu.
 *
 *  Điểm 1 học phần của 1 học sinh = số round đúng / số round đã chơi, chỉ lấy {@link #WINDOW} round gần nhất
 *  của học sinh đó trong học phần đó (xem {@link #phanScores}). Học phần/môn chưa chơi → không có điểm (-1),
 *  nơi hiển thị tự quyết định in "0" nhưng KHÔNG đưa vào trung bình.
 *
 *  Lưu ở /sdcard/EduXplore/class_rounds.jsonl (mỗi dòng 1 JSON, chỉ ghi nối cuối → rẻ dù có hàng chục nghìn
 *  round); không ghi được thì rơi về thư mục riêng của app. Chỉ lớp này đụng file đó.
 *  File cũ class_scores.json (tổng hợp theo ván, công thức cũ) bị XOÁ khi load — không còn dùng. */
public final class ScoreStore {
    private static final String TAG = "ScoreStore";
    private static final String FILE_NAME = "class_rounds.jsonl";
    private static final String ARCHIVE_NAME = "class_rounds_archive.jsonl";
    private static final String LEGACY_FILE_NAME = "class_scores.json";
    private static final int MAX_RECORDS = 20000;
    /** Số round gần nhất (của 1 học sinh, trong 1 học phần) được tính vào điểm. */
    public static final int WINDOW = 50;

    public static final class Record {
        public final long time, session;
        public final String name, game, phan, questionId, question, answer, correctAnswer;
        public final int round;
        public final boolean correct;
        public final float sec;
        public Record(long time, long session, String name, String game, String phan, int round, String questionId,
                      String question, String answer, String correctAnswer, boolean correct, float sec) {
            this.time = time; this.session = session; this.name = name; this.game = game; this.phan = phan;
            this.round = round; this.questionId = questionId; this.question = question; this.answer = answer;
            this.correctAnswer = correctAnswer; this.correct = correct; this.sec = sec;
        }
    }

    private final File primary, fallback;
    private final File legacyPrimary, legacyFallback;
    private final List<Record> records = new ArrayList<>();
    private final Map<String, List<Record>> byName = new HashMap<>();

    public ScoreStore(File fallbackDir) {
        this.primary = new File("/sdcard/EduXplore", FILE_NAME);
        this.fallback = fallbackDir != null ? new File(fallbackDir, FILE_NAME) : null;
        this.legacyPrimary = new File("/sdcard/EduXplore", LEGACY_FILE_NAME);
        this.legacyFallback = fallbackDir != null ? new File(fallbackDir, LEGACY_FILE_NAME) : null;
    }

    /** Thang 100: round đúng / round đã chơi. Chưa chơi round nào → -1 (chưa có điểm). */
    public static int scorePct(int correct, int played) {
        return played <= 0 ? -1 : Math.round(100f * correct / played);
    }

    public synchronized void load() {
        if (legacyPrimary.exists()) legacyPrimary.delete();
        if (legacyFallback != null && legacyFallback.exists()) legacyFallback.delete();
        records.clear();
        byName.clear();
        File f = primary.exists() ? primary : (fallback != null && fallback.exists() ? fallback : null);
        if (f == null) return;
        try (BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(f), StandardCharsets.UTF_8))) {
            String line;
            while ((line = br.readLine()) != null) {
                line = line.trim();
                if (line.isEmpty()) continue;
                try {
                    JSONObject o = new JSONObject(line);
                    index(new Record(o.optLong("t"), o.optLong("s"), o.optString("name"), o.optString("game"),
                            o.optString("phan"), o.optInt("r"), o.optString("qid"), o.optString("q"), o.optString("a"),
                            o.optString("c"), o.optBoolean("ok"), (float) o.optDouble("sec", 0)));
                } catch (Exception badLine) {
                    Log.w(TAG, "bỏ qua dòng hỏng trong " + f.getName());
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "đọc " + f + " lỗi: " + e);
        }
        if (records.size() > MAX_RECORDS) {
            int drop = records.size() - MAX_RECORDS;
            // Không bao giờ xoá lịch sử: phần cũ chuyển sang class_rounds_archive.jsonl (cùng thư mục), chỉ khi
            // chuyển xong mới cắt bớt file chính. Ghi archive lỗi → giữ nguyên, lần mở sau thử lại.
            if (archiveOld(f, records.subList(0, drop))) {
                List<Record> keep = new ArrayList<>(records.subList(drop, records.size()));
                records.clear(); byName.clear();
                for (Record r : keep) index(r);
                rewriteAll();
            }
        }
    }

    private static boolean archiveOld(File dataFile, List<Record> old) {
        StringBuilder sb = new StringBuilder();
        for (Record r : old) { String l = toLine(r); if (l != null) sb.append(l).append('\n'); }
        return appendTo(new File(dataFile.getParentFile(), ARCHIVE_NAME), sb.toString().getBytes(StandardCharsets.UTF_8));
    }

    private void index(Record r) {
        records.add(r);
        List<Record> l = byName.get(r.name);
        if (l == null) { l = new ArrayList<>(); byName.put(r.name, l); }
        l.add(r);
    }

    /** Thêm 1 round (gọi từ luồng nền). Ghi nối 1 dòng vào cuối file. */
    public synchronized void addRound(Record r) {
        index(r);
        appendLine(toLine(r));
    }

    private static String toLine(Record r) {
        try {
            JSONObject o = new JSONObject();
            o.put("t", r.time); o.put("s", r.session); o.put("name", r.name); o.put("game", r.game);
            o.put("phan", r.phan); o.put("r", r.round); o.put("qid", r.questionId); o.put("q", r.question); o.put("a", r.answer);
            o.put("c", r.correctAnswer); o.put("ok", r.correct); o.put("sec", r.sec);
            return o.toString();
        } catch (Exception e) {
            return null;
        }
    }

    private void appendLine(String line) {
        if (line == null) return;
        byte[] bytes = (line + "\n").getBytes(StandardCharsets.UTF_8);
        if (appendTo(primary, bytes)) return;
        if (fallback == null || !appendTo(fallback, bytes)) Log.e(TAG, "không ghi được class_rounds.jsonl ở đâu cả");
    }

    private static boolean appendTo(File target, byte[] bytes) {
        try {
            File dir = target.getParentFile();
            if (dir != null && !dir.exists() && !dir.mkdirs()) return false;
            try (FileOutputStream os = new FileOutputStream(target, true)) { os.write(bytes); }
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    private void rewriteAll() {
        StringBuilder sb = new StringBuilder();
        for (Record r : records) { String l = toLine(r); if (l != null) sb.append(l).append('\n'); }
        byte[] bytes = sb.toString().getBytes(StandardCharsets.UTF_8);
        if (!writeAtomic(primary, bytes) && (fallback == null || !writeAtomic(fallback, bytes))) {
            Log.e(TAG, "không ghi lại được class_rounds.jsonl");
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

    /** học phần → {round đúng, round đã chơi} của 1 học sinh, mỗi học phần chỉ tính {@link #WINDOW} round
     *  gần nhất; theo thứ tự học phần xuất hiện đầu tiên. */
    public synchronized Map<String, int[]> phanScores(String studentName) {
        Map<String, int[]> out = new LinkedHashMap<>();
        List<Record> l = byName.get(studentName);
        if (l == null) return out;
        Map<String, List<Record>> perPhan = new LinkedHashMap<>();
        for (Record r : l) {
            List<Record> p = perPhan.get(r.phan);
            if (p == null) { p = new ArrayList<>(); perPhan.put(r.phan, p); }
            p.add(r);
        }
        for (Map.Entry<String, List<Record>> e : perPhan.entrySet()) {
            List<Record> p = e.getValue();
            int[] t = new int[2];
            for (int i = Math.max(0, p.size() - WINDOW); i < p.size(); i++) {
                t[1]++;
                if (p.get(i).correct) t[0]++;
            }
            out.put(e.getKey(), t);
        }
        return out;
    }

    /** Số ván (phiên chơi) học sinh này đã chơi game `game`. */
    public synchronized int plays(String studentName, String game) {
        List<Record> l = byName.get(studentName);
        if (l == null) return 0;
        java.util.Set<Long> sessions = new java.util.HashSet<>();
        for (Record r : l) if (r.game.equals(game)) sessions.add(r.session);
        return sessions.size();
    }

    /** Mọi round của 1 học sinh, MỚI trước. */
    public synchronized List<Record> roundsOf(String studentName) {
        List<Record> out = new ArrayList<>();
        List<Record> l = byName.get(studentName);
        if (l == null) return out;
        for (int i = l.size() - 1; i >= 0; i--) out.add(l.get(i));
        return out;
    }
}
