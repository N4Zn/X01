package com.eduxplore.posebridge

import android.app.Activity
import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.os.SystemClock
import android.util.Log
import com.google.mediapipe.framework.image.BitmapImageBuilder
import com.google.mediapipe.tasks.core.BaseOptions
import com.google.mediapipe.tasks.core.Delegate
import com.google.mediapipe.tasks.vision.core.RunningMode
import com.google.mediapipe.tasks.vision.poselandmarker.PoseLandmarker
import java.util.Locale

/**
 * Nhận diện khung xương (MediaPipe Pose Landmarker Lite) cho game PoseTest trên K02.
 *
 * Nguồn ảnh: camera IP (ipcamera.json "source":"ip"). Thứ tự thử: luồng MJPEG sub-stream (1 kết nối, nhiều fps)
 * → chụp HTTP sub-stream → chụp HTTP kênh chính; mode nào chạy được thì giữ ([getSource]).
 *
 * Hai luồng:
 *  - "pose-source": đọc ảnh liên tục, giữ ảnh mới nhất kèm mốc thời gian ([getFrameId]/[getFrameJpeg]/[getFrameAgeMs]).
 *    Ảnh được trao NGUYÊN JPEG cho Unity (không giải mã/mã hoá lại) → preview mượt theo tốc độ luồng.
 *  - "pose-detect": lấy ảnh mới nhất chưa xử lý, giải mã nhỏ lại, xoay/lật theo cấu hình, suy luận, công bố kết quả kèm
 *    id/mốc thời gian CỦA ẢNH đó → Unity ghép đúng khung xương với đúng ảnh và nội suy giữa các kết quả.
 *
 * Toạ độ landmark chuẩn hoá 0..1 trên ảnh ĐÃ xoay/lật ([getFlip]: bit1 = xoay 180°, bit2 = lật gương); Unity hiển thị ảnh gốc
 * bằng uvRect tương ứng nên hai thứ trùng nhau.
 * Landmarker tạo MỘT lần (GPU delegate ~13s lần đầu) rồi giữ; [setActive] chỉ tạm dừng.
 *
 * Kết quả: [getResultJson] = {"seq":n,"fid":frameId,"ft":frameMs,"ms":infer,"decodeMs":d,"gpu":true,"poses":[{"p":[x,y,vis,...13 điểm]}]}
 *   13 điểm theo [KEYPOINTS]: mũi, vai T/P, khuỷu T/P, cổ tay T/P, hông T/P, đầu gối T/P, mắt cá T/P.
 */
object PoseBridge {
    private const val TAG = "PoseBridge"
    private const val MODEL = "pose_landmarker_lite.task"
    private const val INPUT_W = 384
    private const val NUM_POSES = 2

    @JvmField val KEYPOINTS = intArrayOf(0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28)

    private class Frame(val id: Int, val jpeg: ByteArray, val tMs: Long)

    @Volatile private var appContext: Context? = null
    @Volatile private var started = false
    @Volatile private var active = false
    @Volatile private var detectEnabled = true
    @Volatile private var status = "idle"
    @Volatile private var usingGpu = false
    @Volatile private var resultJson = ""
    @Volatile private var seq = 0
    @Volatile private var lastInferMs = 0L
    @Volatile private var source = "none"
    @Volatile private var sourceFps = 0f
    @Volatile private var flip = 0

    @Volatile private var latest: Frame? = null
    private var frameCounter = 0
    private var fetchThread: Thread? = null
    private var detectThread: Thread? = null

    // ── API cho Unity ─────────────────────────────────────────────────────────

    /** Idempotent: lần đầu tạo luồng + nạp model; các lần sau chỉ bật lại [active]. */
    @JvmStatic @Synchronized
    fun initialize(activity: Activity) {
        appContext = activity.applicationContext
        active = true
        if (started) return
        started = true
        status = "init"
        val cfg = IpCameraConfig.loadOrNull()
        flip = (if (cfg?.rotate180 == true) 1 else 0) or (if (cfg?.mirror != false) 2 else 0)
        fetchThread = Thread({ sourceLoop() }, "pose-source").apply { isDaemon = true; start() }
        detectThread = Thread({ detectLoop() }, "pose-detect").apply { isDaemon = true; start() }
    }

    @JvmStatic fun setActive(on: Boolean) { active = on }
    @JvmStatic fun setDetectionEnabled(on: Boolean) { detectEnabled = on }

    /** "init" | "loading" | "ready" | "error: ..." */
    @JvmStatic fun getStatus(): String = status
    @JvmStatic fun isGpu(): Boolean = usingGpu
    @JvmStatic fun getSeq(): Int = seq
    @JvmStatic fun getResultJson(): String = resultJson
    @JvmStatic fun getLastInferMs(): Long = lastInferMs
    @JvmStatic fun getSource(): String = source
    @JvmStatic fun getSourceFps(): Float = sourceFps
    @JvmStatic fun getFlip(): Int = flip
    @JvmStatic fun getNowMs(): Long = SystemClock.elapsedRealtime()

    @JvmStatic fun getFrameId(): Int = latest?.id ?: 0
    @JvmStatic fun getFrameJpeg(): ByteArray? = latest?.jpeg
    /** Tuổi (ms) của ảnh mới nhất tại thời điểm gọi. */
    @JvmStatic fun getFrameAgeMs(): Long { val f = latest ?: return -1; return SystemClock.elapsedRealtime() - f.tMs }

    @JvmStatic @Synchronized
    fun shutdown() {
        started = false
        active = false
        fetchThread?.interrupt(); detectThread?.interrupt()
        fetchThread = null; detectThread = null
        status = "idle"
    }

    // ── Luồng nguồn ảnh ───────────────────────────────────────────────────────

    private enum class Mode { STREAM, SNAP_SUB, SNAP_MAIN }

    private fun publish(jpeg: ByteArray) {
        latest = Frame(++frameCounter, jpeg, SystemClock.elapsedRealtime())
    }

    private fun sourceLoop() {
        val cfg = IpCameraConfig.loadOrNull()
        if (cfg == null || !cfg.isIp) {
            status = "error: cần /sdcard/EduXplore/ipcamera.json với \"source\":\"ip\""
            Log.e(TAG, status)
            return
        }
        val sockets = if (cfg.localIp.isEmpty()) null else SnapshotClient.bindTo(cfg.localIp)
        val client = SnapshotClient(cfg.host, cfg.port, cfg.user, cfg.password, sockets, cfg.timeoutMs)
        val modes = Mode.values()
        var mi = 0
        var backoff = 0
        while (started) {
            val mode = modes[mi]
            val r = try { runMode(mode, cfg, client) } catch (e: InterruptedException) { break }
            when (r) {
                RunResult.FATAL -> return
                RunResult.NEXT -> { if (mi < modes.size - 1) mi++; Log.w(TAG, "chuyển nguồn ảnh → ${modes[mi]}") }
                RunResult.RETRY -> { backoff = minOf(backoff + 1, 5) }
            }
            try { Thread.sleep(200L * (backoff + 1)) } catch (_: InterruptedException) { break }
        }
    }

    private enum class RunResult { RETRY, NEXT, FATAL }

    private fun fatal(e: Exception): Boolean {
        // Sai mật khẩu: dừng hẳn, tránh bị camera khoá IP.
        if (e.message?.startsWith("HTTP 401") == true || e.message?.startsWith("Cần mật khẩu") == true) {
            status = "error: camera sai mật khẩu (ipcamera.json)"
            Log.e(TAG, status)
            return true
        }
        return false
    }

    private fun subPath(p: String): String = if (p.contains("/channels/101/")) p.replace("/channels/101/", "/channels/102/") else p

    /** Chạy 1 mode đến khi lỗi. Trả NEXT nếu mode chưa từng ra được ảnh (thử mode kế), RETRY nếu đã từng chạy tốt. */
    private fun runMode(mode: Mode, cfg: IpCameraConfig, client: SnapshotClient): RunResult {
        var got = 0
        var fails = 0
        var winStart = SystemClock.elapsedRealtime()
        var winCount = 0
        fun tick() {
            got++; winCount++
            val now = SystemClock.elapsedRealtime()
            if (now - winStart >= 1000) { sourceFps = winCount * 1000f / (now - winStart); winCount = 0; winStart = now }
        }
        var stream: SnapshotClient.Stream? = null
        try {
            while (started) {
                if (!active) { stream?.close(); stream = null; Thread.sleep(50); continue }
                try {
                    when (mode) {
                        Mode.STREAM -> {
                            val st = stream ?: client.openStream(cfg.streamPath).also { stream = it; source = "stream"; Log.i(TAG, "mở luồng ${cfg.streamPath}") }
                            publish(st.readFrame()); tick()
                        }
                        Mode.SNAP_SUB, Mode.SNAP_MAIN -> {
                            // Còn ảnh chưa được xử lý thì chưa chụp thêm: luôn là ảnh mới nhất.
                            val path = if (mode == Mode.SNAP_SUB) subPath(cfg.snapshotPath) else cfg.snapshotPath
                            source = if (mode == Mode.SNAP_SUB) "snap-sub" else "snap-main"
                            publish(client.fetch(path)); tick()
                        }
                    }
                    fails = 0
                } catch (e: InterruptedException) {
                    throw e
                } catch (e: Exception) {
                    stream?.close(); stream = null
                    fails++
                    Log.w(TAG, "$mode lỗi (#$fails): $e")
                    if (fatal(e)) return RunResult.FATAL
                    if (got == 0 && fails >= 2) return RunResult.NEXT
                    if (got > 0 && fails >= 1) return RunResult.RETRY
                    Thread.sleep(minOf(2000L, 200L * fails))
                }
            }
        } finally {
            stream?.close()
        }
        return RunResult.RETRY
    }

    // ── Luồng suy luận ────────────────────────────────────────────────────────

    private fun createLandmarker(ctx: Context, gpu: Boolean): PoseLandmarker {
        val base = BaseOptions.builder()
            .setModelAssetPath(MODEL)
            .setDelegate(if (gpu) Delegate.GPU else Delegate.CPU)
            .build()
        return PoseLandmarker.createFromOptions(
            ctx,
            PoseLandmarker.PoseLandmarkerOptions.builder()
                .setBaseOptions(base)
                .setRunningMode(RunningMode.IMAGE)
                .setNumPoses(NUM_POSES)
                .setMinPoseDetectionConfidence(0.5f)
                .setMinPosePresenceConfidence(0.5f)
                .build()
        )
    }

    private fun detectLoop() {
        val ctx = appContext ?: return
        status = "loading"
        var landmarker: PoseLandmarker? = null
        try {
            val t0 = System.nanoTime()
            try {
                landmarker = createLandmarker(ctx, gpu = true)
                usingGpu = true
            } catch (e: Throwable) {
                Log.w(TAG, "GPU delegate lỗi, dùng CPU: $e")
                landmarker = createLandmarker(ctx, gpu = false)
                usingGpu = false
            }
            Log.i(TAG, "landmarker sẵn sàng (gpu=$usingGpu) sau ${(System.nanoTime() - t0) / 1_000_000}ms")
        } catch (e: Throwable) {
            status = "error: nạp model lỗi: $e"
            Log.e(TAG, status)
            return
        }
        if (status == "loading") status = "ready"

        val rotate = flip and 1 != 0
        val mirror = flip and 2 != 0
        var lastId = 0

        while (started) {
            try {
                val f = latest
                if (f == null || f.id == lastId || !active || !detectEnabled) { Thread.sleep(4); continue }
                lastId = f.id
                val d0 = System.nanoTime()
                val bmp = decodeScaled(f.jpeg, rotate, mirror) ?: continue
                val decodeMs = (System.nanoTime() - d0) / 1_000_000

                val s = System.nanoTime()
                val res = landmarker!!.detect(BitmapImageBuilder(bmp).build())
                val ms = (System.nanoTime() - s) / 1_000_000
                val poses = poseJson(res.landmarks())
                bmp.recycle()
                lastInferMs = ms

                resultJson = "{\"seq\":${seq + 1},\"fid\":${f.id},\"ft\":${f.tMs},\"ms\":$ms,\"decodeMs\":$decodeMs,\"gpu\":$usingGpu,\"poses\":[$poses]}"
                seq++
            } catch (e: InterruptedException) {
                break
            } catch (e: Throwable) {
                Log.w(TAG, "suy luận lỗi: $e")
                try { Thread.sleep(200) } catch (_: InterruptedException) { break }
            }
        }
        try { landmarker?.close() } catch (_: Throwable) {}
    }

    /** Giải mã JPEG (giảm tỉ lệ ngay khi giải mã) → xoay/lật theo cấu hình camera → [INPUT_W] px ngang, ARGB_8888. */
    private fun decodeScaled(jpeg: ByteArray, rotate180: Boolean, mirror: Boolean): Bitmap? {
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        BitmapFactory.decodeByteArray(jpeg, 0, jpeg.size, bounds)
        if (bounds.outWidth <= 0) return null
        var sample = 1
        while (bounds.outWidth / (sample * 2) >= INPUT_W) sample *= 2
        val src = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.size,
            BitmapFactory.Options().apply { inSampleSize = sample; inPreferredConfig = Bitmap.Config.ARGB_8888 }) ?: return null
        val m = Matrix()
        if (rotate180) m.postRotate(180f, src.width / 2f, src.height / 2f)
        if (mirror) m.postScale(-1f, 1f, src.width / 2f, src.height / 2f)
        val s = INPUT_W.toFloat() / src.width
        if (s < 0.999f) m.postScale(s, s)
        val out = Bitmap.createBitmap(src, 0, 0, src.width, src.height, m, true)
        if (out !== src) src.recycle()
        return out
    }

    private fun poseJson(all: List<List<com.google.mediapipe.tasks.components.containers.NormalizedLandmark>>): String {
        val sb = StringBuilder()
        for (lms in all) {
            if (lms.size < 29) continue
            if (sb.isNotEmpty()) sb.append(',')
            sb.append("{\"p\":[")
            for ((k, idx) in KEYPOINTS.withIndex()) {
                val l = lms[idx]
                if (k > 0) sb.append(',')
                sb.append(String.format(Locale.US, "%.3f,%.3f,%.2f", l.x(), l.y(), l.visibility().orElse(1f)))
            }
            sb.append("]}")
        }
        return sb.toString()
    }
}
