package com.faceattendance.app

import android.util.Log
import org.json.JSONObject
import java.io.File

/** Hình chữ nhật chuẩn hoá 0..1 theo khung camera (đã lật gương, đúng như hình thấy trên màn). */
data class ZoneRect(val x: Float, val y: Float, val w: Float, val h: Float) {
    fun contains(nx: Float, ny: Float) = nx >= x && nx <= x + w && ny >= y && ny <= y + h
}

/**
 * 3 vùng nhận diện khuôn mặt, lưu 1 file chung /sdcard/EduXplore/face_zones.json (sống qua lần cài lại;
 * FaceEnroll process ":fa" và plugin nhận mặt trong game đều đọc). Chỉ mặt có TÂM nằm trong vùng mới được xử lý.
 *  - playLeft / playRight: 2 vùng người chơi trong game (nửa trái / nửa phải).
 *  - enroll: vùng chữ nhật ở màn camera "Thêm từ camera" (Quản lý lớp).
 * Bản sao cùng format nằm ở plugin game (FaceZones.kt trong unityplugin) — sửa 1 nơi thì sửa cả 2.
 */
data class FaceZones(val playLeft: ZoneRect, val playRight: ZoneRect, val enroll: ZoneRect) {

    /** Tâm (px, py) trong khung frameW x frameH có nằm trong hình chữ nhật enroll không. */
    fun enrollContains(px: Float, py: Float, frameW: Float, frameH: Float): Boolean =
        enroll.contains(px / frameW, py / frameH)

    /** Hình chữ nhật pixel của vùng enroll (để cắt ảnh trước khi dò mặt cho nhanh). */
    fun enrollBounds(frameW: Float, frameH: Float): android.graphics.RectF =
        android.graphics.RectF(
            enroll.x * frameW, enroll.y * frameH,
            (enroll.x + enroll.w) * frameW, (enroll.y + enroll.h) * frameH
        )

    fun toJson(): JSONObject = JSONObject().apply {
        put("version", 2)
        put("playLeft", rectJson(playLeft))
        put("playRight", rectJson(playRight))
        put("enroll", rectJson(enroll))
    }

    fun save(): Boolean = try {
        file.parentFile?.mkdirs()
        val tmp = File(file.parentFile, file.name + ".tmp")
        tmp.writeText(toJson().toString(2))
        if (!tmp.renameTo(file)) { file.writeText(tmp.readText()); tmp.delete() }
        true
    } catch (e: Exception) {
        Log.e(TAG, "save failed: ${e.message}")
        false
    }

    companion object {
        private const val TAG = "FaceZones"
        val file = File("/sdcard/EduXplore/face_zones.json")

        val DEFAULT = FaceZones(
            playLeft = ZoneRect(0.03f, 0.15f, 0.44f, 0.80f),
            playRight = ZoneRect(0.53f, 0.15f, 0.44f, 0.80f),
            enroll = ZoneRect(0.30f, 0.10f, 0.40f, 0.80f)
        )

        private fun rectJson(z: ZoneRect) =
            JSONObject().put("x", z.x.toDouble()).put("y", z.y.toDouble()).put("w", z.w.toDouble()).put("h", z.h.toDouble())

        private fun rectFrom(o: JSONObject?, d: ZoneRect): ZoneRect {
            if (o == null) return d
            val w = o.optDouble("w", d.w.toDouble()).toFloat().coerceIn(0.05f, 1f)
            val h = o.optDouble("h", d.h.toDouble()).toFloat().coerceIn(0.05f, 1f)
            val x = o.optDouble("x", d.x.toDouble()).toFloat().coerceIn(0f, 1f - w)
            val y = o.optDouble("y", d.y.toDouble()).toFloat().coerceIn(0f, 1f - h)
            return ZoneRect(x, y, w, h)
        }

        /** Bản cũ (version 1) lưu enroll là hình tròn {cx,cy,r}: đổi sang hình chữ nhật bao nó (khung 16:9). */
        private fun enrollFrom(o: JSONObject?): ZoneRect {
            if (o != null && !o.has("w") && o.has("r")) {
                val r = o.optDouble("r", 0.30).toFloat()
                val w = r * 2f * 9f / 16f; val h = r * 2f
                val cx = o.optDouble("cx", 0.5).toFloat(); val cy = o.optDouble("cy", 0.5).toFloat()
                return rectFrom(JSONObject().put("x", cx - w / 2).put("y", cy - r).put("w", w).put("h", h), DEFAULT.enroll)
            }
            return rectFrom(o, DEFAULT.enroll)
        }

        /** File chưa có / hỏng -> null (nơi gọi tự quyết dùng mặc định hay giữ hành vi cũ). */
        fun loadOrNull(): FaceZones? {
            if (!file.canRead()) return null
            return try {
                val o = JSONObject(file.readText())
                FaceZones(
                    rectFrom(o.optJSONObject("playLeft"), DEFAULT.playLeft),
                    rectFrom(o.optJSONObject("playRight"), DEFAULT.playRight),
                    enrollFrom(o.optJSONObject("enroll"))
                )
            } catch (ex: Exception) {
                Log.e(TAG, "parse failed: ${ex.message}")
                null
            }
        }

        fun load(): FaceZones = loadOrNull() ?: DEFAULT
    }
}
