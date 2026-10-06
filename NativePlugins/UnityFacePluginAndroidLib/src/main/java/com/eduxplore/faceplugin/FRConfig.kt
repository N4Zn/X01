package com.eduxplore.faceplugin

import android.content.Context
import android.util.Log
import org.json.JSONObject
import java.io.File

/**
 * Tunable settings for the FRTest recognition round, editable without rebuilding the AAR.
 * Reads/writes {externalFilesDir}/frtest_config.json.
 *
 * cropX/Y/W/H are expressed in on-screen pixels at the 1024x600 reference resolution
 * (matches the game's Canvas Scaler reference) and are scaled internally to the actual
 * camera frame size. Only faces whose center falls inside this rect are considered.
 */
data class FRConfig(
    val cropX: Float = 0f,
    val cropY: Float = 0f,
    val cropW: Float = 1024f,
    val cropH: Float = 600f,
    val refWidth: Float = 1024f,
    val refHeight: Float = 600f,
    val timeoutSec: Float = 4f,
    val defaultNameLeft: String = "Player_1",
    val defaultNameRight: String = "Player_2"
) {
    companion object {
        private const val TAG = "FRConfig"
        private const val FILE_NAME = "frtest_config.json"

        fun load(context: Context?): FRConfig {
            val default = FRConfig()
            // /sdcard/EduXplore sống qua lần cài lại app; getExternalFilesDir thì bị xoá. Ưu tiên bản chung,
            // không có thì rơi về chỗ cũ (và mẫu mặc định vẫn ghi ở chỗ cũ).
            val shared = File("/sdcard/EduXplore", FILE_NAME)
            val file = if (shared.canRead()) shared
                       else context?.getExternalFilesDir(null)?.let { File(it, FILE_NAME) }
            if (file == null || !file.canRead()) {
                Log.w(TAG, "config not found (path=${file?.path}) — using defaults, writing template")
                writeDefaultIfMissing(file, default)
                return default
            }
            return try {
                val obj = JSONObject(file.readText())
                val cfg = FRConfig(
                    cropX            = obj.optDouble("cropX", default.cropX.toDouble()).toFloat(),
                    cropY            = obj.optDouble("cropY", default.cropY.toDouble()).toFloat(),
                    cropW            = obj.optDouble("cropW", default.cropW.toDouble()).toFloat(),
                    cropH            = obj.optDouble("cropH", default.cropH.toDouble()).toFloat(),
                    refWidth         = obj.optDouble("refWidth", default.refWidth.toDouble()).toFloat(),
                    refHeight        = obj.optDouble("refHeight", default.refHeight.toDouble()).toFloat(),
                    timeoutSec       = obj.optDouble("timeoutSec", default.timeoutSec.toDouble()).toFloat(),
                    defaultNameLeft  = obj.optString("defaultNameLeft", default.defaultNameLeft),
                    defaultNameRight = obj.optString("defaultNameRight", default.defaultNameRight)
                )
                Log.i(TAG, "loaded: $cfg")
                cfg
            } catch (e: Exception) {
                Log.e(TAG, "parse failed: ${e.message} — using defaults")
                default
            }
        }

        private fun writeDefaultIfMissing(file: File?, cfg: FRConfig) {
            if (file == null || file.exists()) return
            try {
                val obj = JSONObject()
                obj.put("cropX", cfg.cropX)
                obj.put("cropY", cfg.cropY)
                obj.put("cropW", cfg.cropW)
                obj.put("cropH", cfg.cropH)
                obj.put("refWidth", cfg.refWidth)
                obj.put("refHeight", cfg.refHeight)
                obj.put("timeoutSec", cfg.timeoutSec)
                obj.put("defaultNameLeft", cfg.defaultNameLeft)
                obj.put("defaultNameRight", cfg.defaultNameRight)
                file.parentFile?.mkdirs()
                file.writeText(obj.toString(2))
                Log.i(TAG, "wrote default template to ${file.path}")
            } catch (e: Exception) {
                Log.w(TAG, "failed writing default template: ${e.message}")
            }
        }
    }
}
