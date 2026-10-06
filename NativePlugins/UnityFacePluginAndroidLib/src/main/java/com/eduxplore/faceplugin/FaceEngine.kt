package com.eduxplore.faceplugin

import android.content.Context
import android.graphics.PointF
import android.graphics.RectF
import org.opencv.core.CvType
import org.opencv.core.Mat
import org.opencv.core.Size
import org.opencv.objdetect.FaceDetectorYN
import org.opencv.objdetect.FaceRecognizerSF
import java.io.File
import java.io.FileOutputStream
import kotlin.math.atan2
import kotlin.math.hypot

data class DetectedFace(
    val rect: RectF,
    val landmarks: List<PointF>,
    val score: Float,
    val rawRow: FloatArray
)

data class PoseEstimate(val yawOffset: Float, val rollDeg: Float) {
    fun isFrontal(yawTol: Float, rollTolDeg: Float) =
        kotlin.math.abs(yawOffset) <= yawTol && kotlin.math.abs(rollDeg) <= rollTolDeg
}

/**
 * YuNet detection + SFace alignment/embedding — same pipeline as FaceAttendance.
 * Models are loaded from context.assets (put in Unity StreamingAssets/).
 */
class FaceEngine(context: Context) {

    companion object {
        const val YAW_TOLERANCE  = 0.60f
        const val ROLL_TOLERANCE = 45f
    }

    private val detector:   FaceDetectorYN
    private val recognizer: FaceRecognizerSF

    init {
        val yunet  = copyAsset(context, "face_detection_yunet.onnx")
        val sface  = copyAsset(context, "face_recognition_sface.onnx")
        detector   = FaceDetectorYN.create(yunet.absolutePath,  "", Size(320.0, 320.0), 0.7f)
        recognizer = FaceRecognizerSF.create(sface.absolutePath, "")
    }

    private fun copyAsset(context: Context, name: String): File {
        val out = File(context.filesDir, name)
        if (out.exists() && out.length() > 0) return out
        context.assets.open(name).use { i -> FileOutputStream(out).use { o -> i.copyTo(o) } }
        return out
    }

    /** Up to [max] faces, sorted largest-to-smallest (closest first). */
    fun detectFaces(bgr: Mat, max: Int = 2): List<DetectedFace> {
        detector.setInputSize(Size(bgr.cols().toDouble(), bgr.rows().toDouble()))
        val mat = Mat()
        detector.detect(bgr, mat)
        if (mat.rows() == 0) { mat.release(); return emptyList() }
        val buf = FloatArray(15)
        val all = ArrayList<DetectedFace>(mat.rows())
        for (r in 0 until mat.rows()) {
            mat.get(r, 0, buf)
            val rect = RectF(buf[0], buf[1], buf[0] + buf[2], buf[1] + buf[3])
            val pts  = listOf(
                PointF(buf[4], buf[5]), PointF(buf[6], buf[7]), PointF(buf[8], buf[9]),
                PointF(buf[10], buf[11]), PointF(buf[12], buf[13])
            )
            all.add(DetectedFace(rect, pts, buf[14], buf.copyOf()))
        }
        mat.release()
        return all.sortedByDescending { it.rect.width() * it.rect.height() }.take(max)
    }

    fun estimatePose(face: DetectedFace): PoseEstimate {
        val re = face.landmarks[0]; val le = face.landmarks[1]; val nt = face.landmarks[2]
        val eyeDist = hypot((re.x - le.x).toDouble(), (re.y - le.y).toDouble()).toFloat()
        if (eyeDist < 1e-3f) return PoseEstimate(0f, 0f)
        val yaw  = (nt.x - (re.x + le.x) / 2f) / eyeDist
        val roll = Math.toDegrees(atan2((le.y - re.y).toDouble(), (le.x - re.x).toDouble())).toFloat()
        return PoseEstimate(yaw, roll)
    }

    fun alignFace(bgr: Mat, face: DetectedFace): Mat {
        val box = Mat(1, 15, CvType.CV_32F)
        box.put(0, 0, face.rawRow)
        val aligned = Mat()
        recognizer.alignCrop(bgr, box, aligned)
        box.release()
        return aligned
    }

    fun getEmbedding(aligned: Mat): FloatArray {
        val feat = Mat()
        recognizer.feature(aligned, feat)
        val dim = feat.cols()
        val raw = FloatArray(dim)
        feat.get(0, 0, raw)
        feat.release()
        var norm = 0f
        for (v in raw) norm += v * v
        norm = kotlin.math.sqrt(norm)
        return FloatArray(raw.size) { raw[it] / norm }
    }
}
