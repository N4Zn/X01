package com.eduxplore.faceplugin

import android.util.Log
import java.io.File

/** One enrolled person: name + list of L2-normalised SFace embeddings (FloatArray). */
data class EnrolledPerson(val name: String, val embeddings: List<FloatArray>, val className: String? = null)

data class DbMatchResult(val name: String?, val sim: Float)

/**
 * Read-only face database.
 * Reads enrolled.json written by MainActivity's AttendanceStore — SAME app now (Track A merge),
 * so both read/write the exact same file, no per-package path juggling needed anymore.
 * JSON format: [{name, gender, samples:[{embedding:[floats], photo}]}]
 */
class FaceDatabase {

    companion object {
        const val DB_PATH = "/sdcard/EduXplore/enrolled.json"
        // Lớp đang dùng (ghi bởi ControlActivity + Quản lý lớp, xem FaceEnroll ActiveClass.kt). Nhận diện trong game chỉ so với học sinh lớp này.
        const val ACTIVE_CLASS_PATH = "/sdcard/EduXplore/active_class.txt"

        fun readActiveClass(): String? = try {
            val f = File(ACTIVE_CLASS_PATH)
            if (f.canRead()) f.readText().trim().takeIf { it.isNotEmpty() } else null
        } catch (e: Exception) { Log.e("FaceDB", "read active_class lỗi: ${e.message}"); null }
    }

    @Volatile private var persons: List<EnrolledPerson> = emptyList()

    fun load(): Int {
        val file = File(DB_PATH)
        Log.i("FaceDB", "load: exists=${file.exists()} canRead=${file.canRead()} path=${file.path}")
        if (!file.exists()) {
            Log.w("FaceDB", "enrolled.json not found at $DB_PATH")
            return 0
        }
        return try {
            val raw = file.readText()
            Log.i("FaceDB", "load: readText len=${raw.length} first20=${raw.take(20)}")
            val arr = org.json.JSONArray(raw)
            Log.i("FaceDB", "load: arr.length=${arr.length()}")
            val list = mutableListOf<EnrolledPerson>()
            for (i in 0 until arr.length()) {
                val obj = arr.getJSONObject(i)
                val name = obj.getString("name")
                val embs = mutableListOf<FloatArray>()
                if (obj.has("samples")) {
                    val sa = obj.getJSONArray("samples")
                    Log.i("FaceDB", "  person[$i] '$name': ${sa.length()} samples")
                    for (j in 0 until sa.length()) {
                        val sObj = sa.getJSONObject(j)
                        val embKey = when {
                            sObj.has("embedding")  -> "embedding"
                            sObj.has("embeddings") -> "embeddings"
                            else -> null
                        }
                        if (embKey == null) { Log.w("FaceDB", "  sample[$j] has no embedding key, keys=${sObj.keys().asSequence().toList()}"); continue }
                        val ea = sObj.getJSONArray(embKey)
                        embs.add(FloatArray(ea.length()) { ea.getDouble(it).toFloat() })
                        Log.i("FaceDB", "  sample[$j] embLen=${ea.length()}")
                    }
                } else if (obj.has("embedding")) {
                    val ea = obj.getJSONArray("embedding")
                    embs.add(FloatArray(ea.length()) { ea.getDouble(it).toFloat() })
                }
                val cls = if (obj.has("className") && !obj.isNull("className")) obj.getString("className") else null
                if (embs.isNotEmpty()) list.add(EnrolledPerson(name, embs, cls))
            }
            // Chỉ giữ học sinh của lớp đang dùng; lớp không có ai => rỗng => không nhận ra ai (game hiện Player 1/2).
            // Chưa có active_class.txt (máy chưa mở ControlActivity lần nào) => giữ tất cả như trước.
            val active = readActiveClass()
            persons = if (active == null) list else list.filter { it.className == active }
            Log.i("FaceDB", "Loaded ${persons.size}/${list.size} persons (lớp đang dùng: ${active ?: "—"})")
            persons.size
        } catch (e: Exception) {
            Log.e("FaceDB", "Failed to load DB: ${e.javaClass.simpleName}: ${e.message}")
            0
        }
    }

    fun isEmpty() = persons.isEmpty()

    /**
     * Returns best match if sim >= matchThreshold and margin over runner-up >= marginThreshold
     * (both from [FaceMatchConfig]).
     * Returns null name if no confident match.
     */
    fun bestMatch(embedding: FloatArray): DbMatchResult {
        if (persons.isEmpty()) return DbMatchResult(null, -1f)
        var bestName: String? = null
        var bestSim  = -1f
        var runnerSim = -1f
        for (p in persons) {
            var pBest = -1f
            for (emb in p.embeddings) {
                val s = cosineSim(emb, embedding)
                if (s > pBest) pBest = s
            }
            if (pBest > bestSim) {
                runnerSim = bestSim
                bestSim   = pBest
                bestName  = p.name
            } else if (pBest > runnerSim) {
                runnerSim = pBest
            }
        }
        val margin = bestSim - runnerSim
        val cfg = FaceMatchConfig.get()
        return if (bestSim >= cfg.matchThreshold && margin >= cfg.marginThreshold)
            DbMatchResult(bestName, bestSim)
        else
            DbMatchResult(null, bestSim)
    }

    private fun cosineSim(a: FloatArray, b: FloatArray): Float {
        var dot = 0f
        for (i in a.indices) dot += a[i] * b[i]
        return dot
    }
}
