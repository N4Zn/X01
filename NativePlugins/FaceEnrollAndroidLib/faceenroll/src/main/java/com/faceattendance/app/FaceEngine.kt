package com.faceattendance.app

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
    // 5 points in this fixed order: rightEye, leftEye, nose, rightMouth, leftMouth (subject-anatomical)
    val landmarks: List<PointF>,
    val score: Float,
    // Raw 15-value FaceDetectorYN row, kept around only to feed FaceRecognizerSF.alignCrop(),
    // which expects exactly this format (not just the parsed landmarks above).
    val rawRow: FloatArray
)

data class PoseEstimate(val yawOffset: Float, val rollDeg: Float) {
    fun isFrontal(yawTolerance: Float, rollToleranceDeg: Float) =
        kotlin.math.abs(yawOffset) <= yawTolerance && kotlin.math.abs(rollDeg) <= rollToleranceDeg
}

/**
 * YuNet detection -> FaceRecognizerSF (OpenCV Zoo's MobileFaceNet/SFace) alignment + embedding.
 * Replaces the earlier AuraFace/ResNet100 (ONNX Runtime) pipeline, which measured ~2.8-4s per
 * embedding on the G350's CPU/NNAPI - far too slow for live attendance. SFace is a MobileFaceNet
 * (same weight class as the "buffalo_s"/lightweight ArcFace variants), Apache-2.0 licensed,
 * distributed by OpenCV itself (same source as the YuNet model already in use), ~37MB vs
 * AuraFace's 260MB. alignCrop()/feature() are OpenCV's own calibrated implementation for this
 * exact model, so no more hand-rolled 5-point affine template.
 */
class FaceEngine(context: Context) {

    private val detector: FaceDetectorYN
    private val recognizer: FaceRecognizerSF

    init {
        // Confirmed via a one-off diagnostic (removed): OPENCV backend only exposes CPU,
        // and TIMVX (the VeriSilicon/NPU backend) reports zero available targets on this
        // board/AAR combo - no NPU path available through OpenCV's DNN module here.
        val yunetFile = copyAssetToFile(context, "face_detection_yunet.onnx")
        detector = FaceDetectorYN.create(yunetFile.absolutePath, "", Size(320.0, 320.0), 0.7f)

        val sfaceFile = copyAssetToFile(context, "face_recognition_sface.onnx")
        recognizer = FaceRecognizerSF.create(sfaceFile.absolutePath, "")
    }

    private fun copyAssetToFile(context: Context, assetName: String): File {
        val outFile = File(context.filesDir, assetName)
        if (outFile.exists() && outFile.length() > 0) return outFile
        context.assets.open(assetName).use { input ->
            FileOutputStream(outFile).use { output -> input.copyTo(output) }
        }
        return outFile
    }

    /** Detects the closest (largest-in-frame) face in a BGR Mat. Returns null if none found. */
    fun detectLargestFace(bgr: Mat): DetectedFace? = detectFaces(bgr, 1).firstOrNull()

    /**
     * Detects up to [maxFaces] faces in a BGR Mat, keeping the ones closest to the camera.
     * There's no depth sensor, so "closest" is approximated by bounding-box area (a closer
     * face projects larger in frame) - people farther back with a merely higher YuNet
     * confidence score no longer displace a nearer person the way a pure score-sort would.
     */
    fun detectFaces(bgr: Mat, maxFaces: Int = 2): List<DetectedFace> {
        detector.setInputSize(Size(bgr.cols().toDouble(), bgr.rows().toDouble()))
        val facesMat = Mat()
        detector.detect(bgr, facesMat)
        if (facesMat.rows() == 0) return emptyList()

        val rowBuf = FloatArray(15)
        val all = ArrayList<DetectedFace>(facesMat.rows())
        for (r in 0 until facesMat.rows()) {
            facesMat.get(r, 0, rowBuf)
            val rect = RectF(rowBuf[0], rowBuf[1], rowBuf[0] + rowBuf[2], rowBuf[1] + rowBuf[3])
            // Fixed YuNet landmark order: re, le, nt, rcm, lcm
            val pts = listOf(
                PointF(rowBuf[4], rowBuf[5]),
                PointF(rowBuf[6], rowBuf[7]),
                PointF(rowBuf[8], rowBuf[9]),
                PointF(rowBuf[10], rowBuf[11]),
                PointF(rowBuf[12], rowBuf[13])
            )
            all.add(DetectedFace(rect, pts, rowBuf[14], rowBuf.copyOf()))
        }
        facesMat.release()
        return all.sortedByDescending { it.rect.width() * it.rect.height() }.take(maxFaces)
    }

    /** Rough yaw/roll from the 5 landmarks, no extra model needed (same heuristic as the Python test). */
    fun estimatePose(face: DetectedFace): PoseEstimate {
        val re = face.landmarks[0]
        val le = face.landmarks[1]
        val nt = face.landmarks[2]
        val eyeDist = hypot((re.x - le.x).toDouble(), (re.y - le.y).toDouble()).toFloat()
        if (eyeDist < 1e-3f) return PoseEstimate(0f, 0f)
        val eyeMidX = (re.x + le.x) / 2f
        val yawOffset = (nt.x - eyeMidX) / eyeDist
        val rollDeg = Math.toDegrees(atan2((le.y - re.y).toDouble(), (le.x - re.x).toDouble())).toFloat()
        return PoseEstimate(yawOffset, rollDeg)
    }

    /** Aligns the face using OpenCV's own calibrated crop for this recognizer (no manual template). */
    fun alignFace(bgr: Mat, face: DetectedFace): Mat {
        val faceBox = Mat(1, 15, CvType.CV_32F)
        faceBox.put(0, 0, face.rawRow)
        val aligned = Mat()
        recognizer.alignCrop(bgr, faceBox, aligned)
        faceBox.release()
        return aligned
    }

    /** Runs SFace on the aligned crop, returns an L2-normalized embedding. */
    fun getEmbedding(alignedBgr: Mat): FloatArray {
        val tStart = System.nanoTime()
        val featureMat = Mat()
        recognizer.feature(alignedBgr, featureMat)
        val tEnd = System.nanoTime()
        android.util.Log.e("FacePerf", "    sface_feature=${"%.1f".format((tEnd - tStart) / 1_000_000.0)}ms")

        val dim = featureMat.cols()
        val raw = FloatArray(dim)
        featureMat.get(0, 0, raw)
        featureMat.release()

        var norm = 0f
        for (v in raw) norm += v * v
        norm = kotlin.math.sqrt(norm)
        return FloatArray(raw.size) { i -> raw[i] / norm }
    }

    fun cosineSim(a: FloatArray, b: FloatArray): Float {
        var dot = 0f
        for (i in a.indices) dot += a[i] * b[i]
        return dot
    }

    fun close() {
        // FaceRecognizerSF/FaceDetectorYN native objects are released by their finalizers;
        // nothing else to close now that ONNX Runtime is no longer used directly.
    }
}
