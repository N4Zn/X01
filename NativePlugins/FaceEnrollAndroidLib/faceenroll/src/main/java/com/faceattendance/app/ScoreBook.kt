package com.faceattendance.app

import android.content.Context
import android.util.Log
import org.json.JSONObject
import java.io.File

/**
 * Điểm/lịch sử chơi THẬT của từng học sinh, CHỈ ĐỌC — cùng nguồn và cùng công thức với
 * `ControlUiAndroidLib/.../ui/ScoreStore.java` (ControlActivity), để màn "Quản lý lớp" hiện đúng
 * những gì ControlActivity hiện:
 *  • nguồn: `/sdcard/EduXplore/class_rounds.jsonl` (mỗi dòng 1 round; ControlActivity là bên GHI),
 *    không đọc được thì thử `getExternalFilesDir(null)` như ScoreStore;
 *  • điểm 1 học phần = round đúng / round đã chơi trên [WINDOW] round gần nhất (thang 100);
 *  • điểm môn = trung bình các học phần ĐÃ CHƠI của môn; điểm tổng = trung bình các môn ĐÃ CÓ ĐIỂM;
 *  • chưa chơi → -1 (UI in "-", KHÔNG phải 0, và không đưa vào trung bình).
 * Môn/học phần/tên hiển thị game lấy từ `assets/game_registry.json` — file này nằm ở aar controlui,
 * nhưng chạy trong cùng 1 APK nên assets được gộp; thiếu file (bản FA chạy riêng) thì không có
 * môn nào → mọi điểm là "-".
 *
 * Sửa công thức ở ScoreStore.java thì PHẢI sửa cả ở đây (2 aar riêng, không dùng chung code được).
 */
class ScoreBook(context: Context) {

    class Round(
        val time: Long, val session: Long, val name: String, val game: String, val phan: String,
        val round: Int, val question: String, val answer: String, val correctAnswer: String,
        val correct: Boolean, val sec: Float
    )

    var categoryNames: List<String> = emptyList()
        private set
    private val phanCategory = HashMap<String, Int>()      // học phần -> chỉ số môn
    private val displayNameByGame = HashMap<String, String>()
    private val byName = HashMap<String, MutableList<Round>>()

    init {
        loadRegistry(context)
        loadRounds(context)
    }

    private fun loadRegistry(context: Context) {
        try {
            val root = JSONObject(context.assets.open(REGISTRY_ASSET).bufferedReader(Charsets.UTF_8).use { it.readText() })
            val cats = root.getJSONArray("categoryNames")
            categoryNames = List(cats.length()) { cats.getString(it) }
            val games = root.getJSONArray("games")
            for (i in 0 until games.length()) {
                val o = games.getJSONObject(i)
                val name = o.getString("name")
                displayNameByGame[name] = if (o.has("displayName")) o.getString("displayName") else name
                val cat = o.getInt("category")
                val group = if (o.has("group")) o.getString("group") else ""
                val phan = if (group.isNotEmpty()) group else categoryNames.getOrElse(cat) { "Khác" }
                if (!phanCategory.containsKey(phan)) phanCategory[phan] = cat
            }
        } catch (e: Exception) {
            Log.w(TAG, "không đọc được $REGISTRY_ASSET: $e")
        }
    }

    private fun loadRounds(context: Context) {
        val primary = File("/sdcard/EduXplore", FILE_NAME)
        val fallback = File(context.getExternalFilesDir(null), FILE_NAME)
        val f = if (primary.exists()) primary else if (fallback.exists()) fallback else return
        try {
            f.bufferedReader(Charsets.UTF_8).useLines { lines ->
                for (raw in lines) {
                    val line = raw.trim()
                    if (line.isEmpty()) continue
                    try {
                        val o = JSONObject(line)
                        val r = Round(
                            o.optLong("t"), o.optLong("s"), o.optString("name"), o.optString("game"),
                            o.optString("phan"), o.optInt("r"), o.optString("q"), o.optString("a"),
                            o.optString("c"), o.optBoolean("ok"), o.optDouble("sec", 0.0).toFloat()
                        )
                        byName.getOrPut(r.name) { ArrayList() }.add(r)
                    } catch (badLine: Exception) {
                        // bỏ qua dòng hỏng, giống ScoreStore
                    }
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "đọc $f lỗi: $e")
        }
    }

    /** học phần -> [round đúng, round đã chơi] của 1 học sinh, mỗi học phần chỉ tính [WINDOW] round gần nhất. */
    fun phanScores(name: String): Map<String, IntArray> {
        val out = LinkedHashMap<String, IntArray>()
        val list = byName[name] ?: return out
        val perPhan = LinkedHashMap<String, MutableList<Round>>()
        for (r in list) perPhan.getOrPut(r.phan) { ArrayList() }.add(r)
        for ((phan, rs) in perPhan) {
            val t = IntArray(2)
            for (i in maxOf(0, rs.size - WINDOW) until rs.size) {
                t[1]++
                if (rs[i].correct) t[0]++
            }
            out[phan] = t
        }
        return out
    }

    fun categoryOfPhan(phan: String): Int? = phanCategory[phan]

    /** Điểm môn `cat` (thang 100) = trung bình các học phần đã chơi thuộc môn; -1 = chưa chơi học phần nào. */
    fun categoryScore(phan: Map<String, IntArray>, cat: Int): Int {
        var sum = 0
        var n = 0
        for ((p, t) in phan) {
            val sc = pct(t[0], t[1])
            if (phanCategory[p] == cat && sc >= 0) { sum += sc; n++ }
        }
        return if (n == 0) -1 else Math.round(sum / n.toFloat())
    }

    /** Điểm tổng = trung bình các MÔN đã có điểm (môn chưa chơi không tính); -1 = chưa chơi gì. */
    fun totalScore(name: String): Int {
        val phan = phanScores(name)
        var sum = 0
        var n = 0
        for (c in categoryNames.indices) {
            val sc = categoryScore(phan, c)
            if (sc >= 0) { sum += sc; n++ }
        }
        return if (n == 0) -1 else Math.round(sum / n.toFloat())
    }

    /** Mọi round của 1 học sinh, MỚI trước. */
    fun roundsOf(name: String): List<Round> = byName[name]?.asReversed() ?: emptyList()

    fun displayNameOf(game: String): String = displayNameByGame[game] ?: game

    companion object {
        private const val TAG = "ScoreBook"
        private const val FILE_NAME = "class_rounds.jsonl"
        private const val REGISTRY_ASSET = "game_registry.json"
        /** Số round gần nhất (1 học sinh, 1 học phần) được tính vào điểm — phải khớp ScoreStore.WINDOW. */
        const val WINDOW = 50

        /** Thang 100: round đúng / round đã chơi. Chưa chơi → -1 (chưa có điểm). */
        fun pct(correct: Int, played: Int): Int = if (played <= 0) -1 else Math.round(100f * correct / played)

        /** Điểm hiển thị: -1 → "-". */
        fun text(score: Int): String = if (score < 0) "-" else score.toString()
    }
}
