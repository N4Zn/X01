package com.eduxplore.posebench

import android.app.Activity
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.os.Bundle
import android.util.Log
import android.widget.ScrollView
import android.widget.TextView
import com.google.mediapipe.framework.image.BitmapImageBuilder
import com.google.mediapipe.tasks.core.BaseOptions
import com.google.mediapipe.tasks.core.Delegate
import com.google.mediapipe.tasks.vision.core.RunningMode
import com.google.mediapipe.tasks.vision.poselandmarker.PoseLandmarker
import java.io.File
import kotlin.concurrent.thread

/**
 * Đo thời gian suy luận MediaPipe Pose Landmarker trên K02.
 * Ảnh thử: adb push ... /sdcard/Android/data/com.eduxplore.posebench/files/test.jpg
 * Kết quả: logcat tag POSEBENCH + hiển thị trên màn hình.
 */
class MainActivity : Activity() {
    private lateinit var tv: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        tv = TextView(this).apply { textSize = 14f; setPadding(24, 24, 24, 24) }
        setContentView(ScrollView(this).apply { addView(tv) })
        thread { runBench() }
    }

    private fun out(s: String) {
        Log.i("POSEBENCH", s)
        runOnUiThread { tv.append(s + "\n") }
    }

    private fun runBench() {
        val img = File(getExternalFilesDir(null), "test.jpg")
        if (!img.exists()) { out("Thiếu ${img.absolutePath}"); return }
        val src = BitmapFactory.decodeFile(img.absolutePath)
        val cfg = File(getExternalFilesDir(null), "bench.txt")
        // dòng: model,delegate,numPoses,width   (vd: lite,GPU,2,640)
        val cases = if (cfg.exists()) cfg.readLines().filter { it.isNotBlank() }
        else listOf("lite,CPU,2,640", "lite,GPU,2,640", "full,CPU,2,640", "full,GPU,2,640")
        out("Máy: ${android.os.Build.MODEL}  ảnh ${src.width}x${src.height}")
        for (c in cases) {
            val (model, del, np, w) = c.trim().split(",")
            try { runCase(src, model, del, np.toInt(), w.toInt()) }
            catch (e: Throwable) { out("[$c] LỖI: $e") }
        }
        out("XONG")
    }

    private operator fun <T> List<T>.component4() = this[3]

    private fun runCase(src: Bitmap, model: String, del: String, numPoses: Int, width: Int) {
        val bmp = Bitmap.createScaledBitmap(src, width, width * src.height / src.width, true)
        val t0 = System.nanoTime()
        val base = BaseOptions.builder()
            .setModelAssetPath("pose_landmarker_$model.task")
            .setDelegate(if (del == "GPU") Delegate.GPU else Delegate.CPU).build()
        val lm = PoseLandmarker.createFromOptions(
            this,
            PoseLandmarker.PoseLandmarkerOptions.builder()
                .setBaseOptions(base).setRunningMode(RunningMode.IMAGE)
                .setNumPoses(numPoses).setMinPoseDetectionConfidence(0.6f).build()
        )
        val initMs = (System.nanoTime() - t0) / 1e6
        val mp = BitmapImageBuilder(bmp).build()
        var first = 0.0
        val times = ArrayList<Double>()
        var poses = 0
        for (i in 0 until 16) {
            val s = System.nanoTime()
            val r = lm.detect(mp)
            val ms = (System.nanoTime() - s) / 1e6
            poses = r.landmarks().size
            if (i == 0) first = ms else times.add(ms)
        }
        lm.close()
        times.sort()
        out("[%s %s np=%d w=%d] init=%.0fms first=%.0fms med=%.0fms min=%.0f max=%.0f  => %.1f fps, poses=%d"
            .format(model, del, numPoses, width, initMs, first, times[times.size / 2], times.first(), times.last(),
                1000.0 / times[times.size / 2], poses))
    }
}
