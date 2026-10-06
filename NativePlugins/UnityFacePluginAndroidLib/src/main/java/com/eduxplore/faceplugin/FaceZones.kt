package com.eduxplore.faceplugin

import android.util.Log
import org.json.JSONObject
import java.io.File

/** Hình chữ nhật chuẩn hoá 0..1 theo khung camera (đã lật gương, đúng như hình thấy trên màn). */
data class ZoneRect(val x: Float, val y: Float, val w: Float, val h: Float) {
    fun contains(nx: Float, ny: Float) = nx >= x && nx <= x + w && ny >= y && ny <= y + h
}

/** Hình tròn: cx theo chiều rộng khung, cy theo chiều cao, r theo CHIỀU CAO (để ra hình tròn thật). */
data class ZoneCircle(val cx: Float, val cy: Float, val r: Float)

/**
 * 3 vùng nhận diện khuôn mặt, lưu 1 file chung /sdcard/EduXplore/face_zones.json (sống qua lần cài lại;
 * FaceEnroll process ":fa" và plugin nhận mặt trong game đều đọc). Chỉ mặt có TÂM nằm trong vùng mới được xử lý.
 *  - playLeft / playRight: 2 vùng người chơi trong game (nửa trái / nửa phải).
 *  - enroll: vùng tròn ở màn camera "Thêm từ camera" (Quản lý lớp).
 * Bản sao cùng format nằm ở plugin game (FaceZones.kt trong unityplugin) — sửa 1 nơi thì sửa cả 2.
 */
data class FaceZones(val playLeft: ZoneRect, val playRight: ZoneRect, val enroll: ZoneCircle) {

    /** Tâm (px, py) trong khung frameW x frameH có nằm trong vòng tròn enroll không. */
    fun enrollContains(px: Float, py: Float, frameW: Float, frameH: Float): Boolean {
        val dx = px - enroll.cx * frameW
        val dy = py - enroll.cy * frameH
        val r = enroll.r * frameH
        return dx * dx + dy * dy <= r * r
    }

    /** Hình chữ nhật pixel bao quanh vòng tròn enroll (để cắt ảnh trước khi dò mặt cho nhanh). */
    fun enrollBounds(frameW: Float, frameH: Float): android.graphics.RectF {
        val r = enroll.r * frameH
        val cx = enroll.cx * frameW
        val cy = enroll.cy * frameH
        return android.graphics.RectF(
            (cx - r).coerceIn(0f, frameW), (cy - r).coerceIn(0f, frameH),
            (cx + r).coerceIn(0f, frameW), (cy + r).coerceIn(0f, frameH)
        )
    }

    fun toJson(): JSONObject = JSONObject().apply {
        put("version", 1)
        put("playLeft", rectJson(playLeft))
        put("playRight", rectJson(playRight))
        put("enroll", JSONObject().put("cx", enroll.cx.toDouble()).put("cy", enroll.cy.toDouble()).put("r", enroll.r.toDouble()))
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
            enroll = ZoneCircle(0.5f, 0.5f, 0.30f)
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

        /** File chưa có / hỏng -> null (nơi gọi tự quyết dùng mặc định hay giữ hành vi cũ). */
        fun loadOrNull(): FaceZones? {
            if (!file.canRead()) return null
            return try {
                val o = JSONObject(file.readText())
                val e = o.optJSONObject("enroll")
                FaceZones(
                    rectFrom(o.optJSONObject("playLeft"), DEFAULT.playLeft),
                    rectFrom(o.optJSONObject("playRight"), DEFAULT.playRight),
                    ZoneCircle(
                        (e?.optDouble("cx", 0.5) ?: 0.5).toFloat().coerceIn(0f, 1f),
                        (e?.optDouble("cy", 0.5) ?: 0.5).toFloat().coerceIn(0f, 1f),
                        (e?.optDouble("r", 0.30) ?: 0.30).toFloat().coerceIn(0.05f, 1f)
                    )
                )
            } catch (ex: Exception) {
                Log.e(TAG, "parse failed: ${ex.message}")
                null
            }
        }

        fun load(): FaceZones = loadOrNull() ?: DEFAULT
    }
}
