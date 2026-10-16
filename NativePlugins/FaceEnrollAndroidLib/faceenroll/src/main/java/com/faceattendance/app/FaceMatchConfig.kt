package com.faceattendance.app

import android.os.SystemClock
import android.util.Log
import org.json.JSONObject
import java.io.File

/**
 * Hệ số nhận diện mặt, chỉnh được không cần build lại: /sdcard/EduXplore/face_match.json
 * (chưa có file thì tự ghi mẫu mặc định). Tự nạp lại khi file đổi, kiểm tra tối đa 3s/lần.
 * File này DÙNG CHUNG cho game (unityplugin) và FaceEnroll (faceenroll) — 2 bản FaceMatchConfig.kt
 * giống hệt nhau (chỉ khác package), sửa cả hai.
 *
 *  matchThreshold   cosine sim tối thiểu để coi là cùng người (SFace)
 *  marginThreshold  người xếp 1 phải hơn người xếp 2 ít nhất chừng này
 *  yawTolerance     độ lệch ngang tối đa của mặt (0 = nhìn thẳng)
 *  voteWindow / votesNeeded  (chỉ game) trong voteWindow frame gần nhất, có votesNeeded frame
 *                   cùng ra 1 người thì mới khoá tên
 */
object FaceMatchConfig {

    data class Values(
        val matchThreshold: Float = 0.38f,
        val marginThreshold: Float = 0.10f,
        val yawTolerance: Float = 0.40f,
        val rollToleranceDeg: Float = 45f,
        val voteWindow: Int = 5,
        val votesNeeded: Int = 2
    )

    private const val TAG = "FaceMatchConfig"
    private const val PATH = "/sdcard/EduXplore/face_match.json"
    private const val CHECK_INTERVAL_MS = 3000L

    @Volatile private var cached = Values()
    @Volatile private var lastCheckMs = -CHECK_INTERVAL_MS
    private var lastModified = -1L

    fun get(): Values {
        val now = SystemClock.elapsedRealtime()
        if (now - lastCheckMs >= CHECK_INTERVAL_MS) {
            lastCheckMs = now
            reload()
        }
        return cached
    }

    @Synchronized private fun reload() {
        try {
            val file = File(PATH)
            if (!file.exists()) {
                lastModified = -1L
                writeTemplate(file)
                return
            }
            val mod = file.lastModified()
            if (mod == lastModified) return
            lastModified = mod
            val d = Values()
            val o = JSONObject(file.readText())
            val window = o.optInt("voteWindow", d.voteWindow).coerceIn(1, 20)
            cached = Values(
                matchThreshold   = o.optDouble("matchThreshold", d.matchThreshold.toDouble()).toFloat(),
                marginThreshold  = o.optDouble("marginThreshold", d.marginThreshold.toDouble()).toFloat(),
                yawTolerance     = o.optDouble("yawTolerance", d.yawTolerance.toDouble()).toFloat(),
                rollToleranceDeg = o.optDouble("rollToleranceDeg", d.rollToleranceDeg.toDouble()).toFloat(),
                voteWindow       = window,
                votesNeeded      = o.optInt("votesNeeded", d.votesNeeded).coerceIn(1, window)
            )
            Log.i(TAG, "loaded: $cached")
        } catch (e: Exception) {
            Log.w(TAG, "load failed: ${e.message} — giữ giá trị hiện tại")
        }
    }

    private fun writeTemplate(file: File) {
        try {
            val d = Values()
            val o = JSONObject()
            o.put("matchThreshold", d.matchThreshold.toDouble())
            o.put("marginThreshold", d.marginThreshold.toDouble())
            o.put("yawTolerance", d.yawTolerance.toDouble())
            o.put("rollToleranceDeg", d.rollToleranceDeg.toDouble())
            o.put("voteWindow", d.voteWindow)
            o.put("votesNeeded", d.votesNeeded)
            file.parentFile?.mkdirs()
            file.writeText(o.toString(2))
            Log.i(TAG, "wrote default template to ${file.path}")
        } catch (e: Exception) {
            Log.w(TAG, "failed writing default template: ${e.message}")
        }
    }
}
