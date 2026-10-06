package com.eduxplore.faceplugin

import android.util.Log
import java.io.File

/** One enrolled person: name + list of L2-normalised SFace embeddings (FloatArray). */
data class EnrolledPerson(val name: String, val embeddings: List<FloatArray>)

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
        private const val MATCH_THRESHOLD  = 0.363f
        private const val MARGIN_THRESHOLD = 0.08f
    }

    private var persons: List<EnrolledPerson> = emptyList()

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
                if (embs.isNotEmpty()) list.add(EnrolledPerson(name, embs))
            }
            persons = list
            Log.i("FaceDB", "Loaded ${persons.size} persons")
            persons.size
        } catch (e: Exception) {
            Log.e("FaceDB", "Failed to load DB: ${e.javaClass.simpleName}: ${e.message}")
            0
        }
    }

    fun isEmpty() = persons.isEmpty()

    /**
     * Returns best match if sim >= MATCH_THRESHOLD and margin over runner-up >= MARGIN_THRESHOLD.
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
        return if (bestSim >= MATCH_THRESHOLD && margin >= MARGIN_THRESHOLD)
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
