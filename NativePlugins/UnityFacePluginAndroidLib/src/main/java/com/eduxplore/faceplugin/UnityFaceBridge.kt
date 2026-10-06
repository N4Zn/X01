package com.eduxplore.faceplugin

import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.graphics.SurfaceTexture
import android.hardware.usb.UsbDevice
import android.hardware.usb.UsbManager
import android.util.Log
import com.serenegiant.usb.IFrameCallback
import com.serenegiant.usb.USBMonitor
import com.serenegiant.usb.UVCCamera
import org.opencv.android.OpenCVLoader
import org.opencv.core.Core
import org.opencv.core.CvType
import org.opencv.core.Mat
import org.opencv.core.Size
import org.opencv.imgproc.Imgproc
import java.nio.ByteBuffer
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * Bridge between the UVC camera + face recognition pipeline and Unity.
 *
 * Unity C# usage:
 *   var bridge = new AndroidJavaClass("com.eduxplore.faceplugin.UnityFaceBridge");
 *   bridge.CallStatic("initialize", activity);
 *   bridge.CallStatic("clearRoundVotes");       // start of each countdown
 *   bridge.CallStatic("setRecognitionEnabled", true/false);
 *   string json = bridge.CallStatic<string>("getConfirmedNames");  // poll during/after countdown
 *   bridge.CallStatic("shutdown");
 *
 * Recognition logic: single-frame confirm. The LAST person confidently recognised in each
 * slot during the active round is returned by getConfirmedNames(). clearRoundVotes() resets.
 */
object UnityFaceBridge : USBMonitor.OnDeviceConnectListener {

    private const val TAG       = "UnityFaceBridge"
    private const val MAX_FACES = 2

    // Cameras that don't advertise USB_CLASS_VIDEO but are confirmed UVC devices.
    // USB-to-serial adapters (LiDAR's CP2102 UART là 1 ví dụ, vid=0x10C4) báo class=255
    // (vendor-specific) thay vì CDC nên không lọc được qua interfaceClass — loại bằng VID biết
    // trước để không bao giờ thử mở nhầm chúng thành camera.
    private val knownSerialVids = setOf(0x10C4, 0x1A86, 0x0403, 0x067B, 0x0B95)
    private val excludedCameraClasses = setOf(
        android.hardware.usb.UsbConstants.USB_CLASS_AUDIO,
        android.hardware.usb.UsbConstants.USB_CLASS_COMM,
        android.hardware.usb.UsbConstants.USB_CLASS_HID,
        android.hardware.usb.UsbConstants.USB_CLASS_PRINTER,
        android.hardware.usb.UsbConstants.USB_CLASS_MASS_STORAGE,
        android.hardware.usb.UsbConstants.USB_CLASS_HUB,
        android.hardware.usb.UsbConstants.USB_CLASS_CDC_DATA
    )

    // ── Camera ───────────────────────────────────────────────────────────────
    private var usbMonitor:    USBMonitor?    = null
    private var uvcCamera:     UVCCamera?     = null
    private var previewSurface: android.graphics.SurfaceTexture? = null
    @Volatile private var frameWidth  = 0
    @Volatile private var frameHeight = 0

    // ── Latest frame for Unity display (RGBA, mirrored + vertically flipped) ──
    private val latestRgba = AtomicReference<ByteArray?>(null)
    @Volatile private var frameRgbaCount = 0

    // Latest detected face rects (from previous recognition frame) for box overlay
    private val latestFaceRects = AtomicReference<List<android.graphics.RectF>>(emptyList())

    // ── Recognition ──────────────────────────────────────────────────────────
    private var engine:   FaceEngine?   = null
    private var database: FaceDatabase? = null
    private var appContext: Context?    = null
    @Volatile private var config: FRConfig = FRConfig()
    // Per-slot recognition enable — lets a caller that only cares about ONE slot (e.g. a game
    // with independent per-player timing) avoid paying the align+embed+match cost for the
    // other slot just because it hasn't locked yet. setRecognitionEnabled() sets both together
    // for callers (FRTest) whose rounds are synchronized.
    private val slotEnabled = arrayOf(AtomicBoolean(false), AtomicBoolean(false))
    // Detection normally only runs when recognition is active. FRTest also wants live bounding
    // boxes drawn during its "question phase" when recognition itself is off — this flag lets
    // it opt into detection-for-display without forcing every other (headless, no preview) game
    // to pay that cost too. Default false: headless games never touch this, so it stays off.
    private val bboxEnabled   = AtomicBoolean(false)
    @Volatile private var isProcessing = false
    private var detectionExecutor = Executors.newSingleThreadExecutor()

    private const val DETECT_CANDIDATES = 6

    // Last confident recognition per slot this round (updated every frame, reset each round).
    // Single-frame confirm: no voting — the most recent confident match wins.
    private val lastRecognized = arrayOfNulls<String>(MAX_FACES)
    // Độ tin cậy (cosine sim, 0..1) của lần khoá lastRecognized[slot] tương ứng — song song với
    // mảng trên, cùng bị khoá/xoá tại cùng thời điểm. -1 = chưa nhận diện lần nào trong round này.
    private val lastConfidence = FloatArray(MAX_FACES) { -1f }

    @Volatile private var initialized = false

    // ── Init / shutdown ──────────────────────────────────────────────────────

    @JvmStatic
    fun initialize(activity: Activity) {
        if (!OpenCVLoader.initLocal()) {
            Log.e(TAG, "OpenCV init failed")
            return
        }
        if (detectionExecutor.isShutdown) {
            detectionExecutor = Executors.newSingleThreadExecutor()
        }
        engine      = FaceEngine(activity)
        appContext  = activity.applicationContext
        database    = FaceDatabase().also { db ->
            Log.i(TAG, "DB loaded: ${db.load()} persons")
        }
        config = FRConfig.load(appContext)

        // USB monitor MUST be registered on Android's main thread.
        // Unity's game thread has no Looper — sticky ACTION_USB_DEVICE_ATTACHED broadcast
        // is only delivered synchronously during registerReceiver() when called from a
        // thread that has a Looper. FA calls register() from onStart() (= main thread);
        // we replicate that by dispatching to the main thread here.
        activity.runOnUiThread {
            if (usbMonitor == null) {
                usbMonitor = USBMonitor(activity, this@UnityFaceBridge)
            }
            usbMonitor?.register()

            // K02 vendor ROM silently denies USB permission for video-class devices unless
            // the app holds CAMERA runtime permission (FA app comment, MainActivity.kt:248).
            // Also explicitly request for cameras already connected before register() was called.
            val hasCameraPermission = activity.checkSelfPermission(android.Manifest.permission.CAMERA) ==
                    PackageManager.PERMISSION_GRANTED
            Log.i(TAG, "registered on main thread (hasCameraPermission=$hasCameraPermission)")
            if (hasCameraPermission) {
                val usbMan = activity.getSystemService(Context.USB_SERVICE) as UsbManager
                // Same permissive path as onAttach() — a webcam already plugged in BEFORE the game
                // starts only ever goes through this cold-start scan, never onAttach().
                usbMan.deviceList.values.forEach { handleCandidateDevice(it) }
            } else {
                Log.w(TAG, "CAMERA permission not granted — USB camera may be silently denied")
            }
        }

        initialized = true
        Log.i(TAG, "initialized")
    }

    @JvmStatic
    fun shutdown() = shutdownInternal()

    private fun shutdownInternal() {
        initialized = false
        slotEnabled[0].set(false)
        slotEnabled[1].set(false)
        uvcCamera?.setFrameCallback(null, 0)
        uvcCamera?.stopPreview()
        uvcCamera?.close()
        uvcCamera = null
        latestRgba.set(null)
        previewSurface?.release()
        previewSurface = null
        // Unregister but do NOT destroy — preserve the instance for the next initialize() call.
        // unregisterReceiver() is safe to call from any thread.
        usbMonitor?.unregister()

        // shutdownNow() does NOT actually stop a processFrame() call already in flight — it's
        // blocked inside native OpenCV inference (detectFaces/alignFace/getEmbedding), which
        // ignores Thread.interrupt() entirely. Without waiting here, FaceRecognitionPlugin's
        // OnApplicationPause (Unity side) calls initialize() again moments later on app resume —
        // creating a NEW engine/database/executor generation while the OLD single thread is still
        // mid-inference on the old one. That's a real, frequently-reachable path (screen sleep,
        // a permission dialog, any transient pause), not just app exit — block (bounded by a
        // timeout as a safety net, since the call itself truly can't be cancelled) until the old
        // thread actually finishes before returning, so initialize() never races a zombie generation.
        detectionExecutor.shutdownNow()
        try {
            if (!detectionExecutor.awaitTermination(2, TimeUnit.SECONDS)) {
                Log.w(TAG, "detectionExecutor did not terminate within 2s — a zombie processFrame() may still be running")
            }
        } catch (e: InterruptedException) {
            Thread.currentThread().interrupt()
        }
        Log.i(TAG, "shutdown")
    }

    // ── Frame access (called from Unity Update) ──────────────────────────────

    @JvmStatic fun getLatestFrameRgba(): ByteArray? = latestRgba.get()
    @JvmStatic fun getFrameWidth():  Int = frameWidth
    @JvmStatic fun getFrameHeight(): Int = frameHeight
    @JvmStatic fun getFrameRgbaCount(): Int = frameRgbaCount

    // ── Recognition control ──────────────────────────────────────────────────

    @JvmStatic
    fun setRecognitionEnabled(enabled: Boolean) {
        slotEnabled[0].set(enabled)
        slotEnabled[1].set(enabled)
        Log.i(TAG, if (enabled) "Recognition ENABLED (native, both slots)" else "Recognition disabled (native, both slots)")
    }

    /**
     * Enables/disables recognition for just one slot (0=left, 1=right) — for callers that only
     * want the align+embed+match cost paid for the slot they're actually waiting on, not both.
     */
    @JvmStatic
    fun setSlotRecognitionEnabled(slot: Int, enabled: Boolean) {
        if (slot < 0 || slot >= MAX_FACES) return
        slotEnabled[slot].set(enabled)
        Log.i(TAG, "Recognition slot=$slot ${if (enabled) "ENABLED" else "disabled"} (native)")
    }

    /**
     * Opt in to running detection purely for bounding-box display even while recognition is
     * off (FRTest's question phase). Headless games (no camera preview) should never call
     * this — leaving it off skips detection entirely whenever recognition is also off.
     */
    @JvmStatic
    fun setBoundingBoxEnabled(enabled: Boolean) {
        bboxEnabled.set(enabled)
        Log.i(TAG, "Bounding box display ${if (enabled) "enabled" else "disabled"} (native)")
    }

    /** Reset at the start of each round's countdown. Reloads config so edits apply without a rebuild. */
    @JvmStatic
    fun clearRoundVotes() {
        synchronized(lastRecognized) { lastRecognized.fill(null); lastConfidence.fill(-1f) }
    }

    /**
     * Re-reads frtest_config.json from disk — split out of clearRoundVotes() because that call
     * fires on every fresh recognition window across ALL games (not just FRTest), and disk I/O
     * on Unity's main thread (this is a blocking JNI call) caused a visible stutter right at
     * the moment a new slot's recognition window opened. Only FRTest needs live-editable config
     * (crop/timeout tuning without a rebuild), so only it calls this explicitly.
     */
    @JvmStatic
    fun reloadConfig() {
        config = FRConfig.load(appContext)
    }

    /**
     * Clears just one slot's lock (0=left, 1=right) without touching the other — for games
     * with independent per-player timing (e.g. each side has its own countdown), where a full
     * clearRoundVotes() would wipe an already-locked result the other slot's caller is still
     * waiting to read.
     */
    @JvmStatic
    fun clearSlotVote(slot: Int) {
        if (slot < 0 || slot >= MAX_FACES) return
        synchronized(lastRecognized) { lastRecognized[slot] = null; lastConfidence[slot] = -1f }
    }

    @JvmStatic fun getTimeoutSec(): Float = config.timeoutSec
    @JvmStatic fun getDefaultNameLeft(): String = config.defaultNameLeft
    @JvmStatic fun getDefaultNameRight(): String = config.defaultNameRight

    /**
     * Returns the last confirmed names + confidence (cosine sim, 0..1) for each slot as JSON.
     * null name = nobody recognised yet this round for that slot (sim is -1 in that case).
     * Example: {"left":"Nguyen Van A","leftSim":0.412,"right":null,"rightSim":-1}
     */
    @JvmStatic
    fun getConfirmedNames(): String {
        val l: String?
        val r: String?
        val lSim: Float
        val rSim: Float
        synchronized(lastRecognized) {
            l = lastRecognized[0]
            r = lastRecognized[1]
            lSim = lastConfidence[0]
            rSim = lastConfidence[1]
        }
        val ls = if (l != null) "\"$l\"" else "null"
        val rs = if (r != null) "\"$r\"" else "null"
        return """{"left":$ls,"leftSim":$lSim,"right":$rs,"rightSim":$rSim}"""
    }

    // ── USBMonitor.OnDeviceConnectListener ───────────────────────────────────

    // Deployment thực tế: đúng 1 camera USB cố định trên máy (không phải app tiêu dùng phải
    // đoán giữa nhiều webcam lạ) — quyền USB đã cấp thì Android tự nhớ lâu dài (usbMonitor.
    // requestPermission() trên 1 device ĐÃ có quyền tự động không hiện dialog, connect ngay),
    // nên không cần chờ 6s "để chắc" mỗi lần mở app/game nữa — cứ thấy device có thể là camera
    // (không thuộc nhóm rõ ràng không phải camera) là xin quyền/mở NGAY.
    override fun onAttach(device: UsbDevice) = handleCandidateDevice(device)

    /** Shared by onAttach() (device plugged in after registration) and initialize()'s
     *  cold-start scan (device already attached before the game started). */
    private fun handleCandidateDevice(device: UsbDevice) {
        if (uvcCamera != null) return
        if (!looksLikeUvcCamera(device)) return
        Log.i(TAG, "candidate device vid=${device.vendorId} pid=${device.productId} name=${device.deviceName} — requesting permission")
        usbMonitor?.requestPermission(device)
    }

    override fun onConnect(device: UsbDevice, ctrl: USBMonitor.UsbControlBlock, createNew: Boolean) {
        if (uvcCamera != null) return
        try {
            val cam = UVCCamera()
            cam.open(ctrl)
            try {
                cam.setPreviewSize(1280, 720, UVCCamera.FRAME_FORMAT_MJPEG)
                frameWidth = 1280; frameHeight = 720
            } catch (e: Exception) {
                try {
                    cam.setPreviewSize(1280, 720, UVCCamera.DEFAULT_PREVIEW_MODE) // YUYV fallback
                    frameWidth = 1280; frameHeight = 720
                } catch (e2: Exception) {
                    cam.setPreviewSize(UVCCamera.DEFAULT_PREVIEW_WIDTH, UVCCamera.DEFAULT_PREVIEW_HEIGHT, UVCCamera.DEFAULT_PREVIEW_MODE)
                    frameWidth = UVCCamera.DEFAULT_PREVIEW_WIDTH
                    frameHeight = UVCCamera.DEFAULT_PREVIEW_HEIGHT
                }
            }
            cam.setFrameCallback(frameCallback, UVCCamera.PIXEL_FORMAT_NV21)
            // Keep a hard reference so GC never abandons the BufferQueue
            val st = android.graphics.SurfaceTexture(0)
            previewSurface = st
            cam.setPreviewTexture(st)
            cam.startPreview()
            uvcCamera = cam
            Log.i(TAG, "UVC camera started ${frameWidth}x${frameHeight}")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to open UVC camera", e)
        }
    }

    override fun onDisconnect(device: UsbDevice, ctrl: USBMonitor.UsbControlBlock) {
        // USBMonitor.UsbControlBlock#close() (invoked from UVCCamera.close()/destroy() below)
        // calls this SAME listener's onDisconnect() again, synchronously/reentrantly on this
        // thread, right after it nulls its own connection — see UsbControlBlock#close(). The
        // native UVCCamera/UVCPreview guards (mDeviceHandle, mIsRunning, mNativePtr) happen to
        // make a naive re-entry harmless today, but that's an implementation detail of a bundled
        // third-party native library, not a contract — null the reference FIRST so the reentrant
        // call is a guaranteed no-op regardless, and don't let any exception here escape onto
        // USBMonitor's dedicated background thread (an uncaught exception there kills the whole
        // process, same reasoning as onConnect()'s existing try/catch below).
        val cam = uvcCamera ?: return
        uvcCamera = null
        try {
            cam.destroy()
        } catch (e: Exception) {
            Log.e(TAG, "onDisconnect: camera teardown error", e)
        }
        latestRgba.set(null)
    }

    override fun onDettach(device: UsbDevice) {}
    override fun onCancel(device: UsbDevice) {}

    // ── Frame callback ────────────────────────────────────────────────────────

    @Volatile private var frameCallbackCount = 0

    private val frameCallback = IFrameCallback { frame: ByteBuffer ->
        val w = frameWidth; val h = frameHeight
        if (w == 0 || h == 0) { Log.w(TAG, "frameCallback: w=$w h=$h, skip"); return@IFrameCallback }

        val frameSize = frame.remaining()
        val expectedNv21 = w * h * 3 / 2
        frameCallbackCount++
        if (frameCallbackCount <= 3 || frameCallbackCount % 100 == 0) {
            Log.i(TAG, "frameCallback #$frameCallbackCount: size=$frameSize expected=$expectedNv21 ${w}x${h}")
        }

        // Khung lệch kích thước (định dạng đổi giữa chừng/frame hỏng): bỏ, không đưa vào Mat.put.
        if (frameSize != expectedNv21) return@IFrameCallback

        var bgrForRecog: Mat? = null
        try {
            val data = ByteArray(frameSize)
            frame.get(data)

            // YUV → BGR (NV12: U first, V second — matches uvc_yuyv2yuv420SP output)
            val yuv = Mat(h + h / 2, w, CvType.CV_8UC1)
            val putCount = yuv.put(0, 0, data)
            if (frameCallbackCount <= 3) Log.i(TAG, "yuv.put=$putCount expected=$expectedNv21")
            val bgrRaw = Mat()
            Imgproc.cvtColor(yuv, bgrRaw, Imgproc.COLOR_YUV2BGR_NV12)
            yuv.release()

            // Mirror horizontally (selfie-style); keep this Mat for recognition
            val bgr = Mat()
            Core.flip(bgrRaw, bgr, 1)
            bgrRaw.release()

            // Building the display frame (clone, draw boxes, flip, BGR→RGBA, 3.6MB copy) is real
            // native CPU work that ran unconditionally on EVERY camera frame regardless of scene
            // — headless games (no camera preview UI at all) were paying this cost the whole
            // time the camera ran, not just during active recognition. Only FRTest actually
            // shows this texture, so gate the whole block behind the same flag it already uses
            // to opt into bounding-box display.
            if (bboxEnabled.get()) {
                val bgrDisplay = bgr.clone()
                val rects = latestFaceRects.get()
                for (r in rects) {
                    Imgproc.rectangle(bgrDisplay,
                        org.opencv.core.Point(r.left.toDouble(), r.top.toDouble()),
                        org.opencv.core.Point(r.right.toDouble(), r.bottom.toDouble()),
                        org.opencv.core.Scalar(0.0, 255.0, 0.0), 4)
                }
                // Flip vertically: Unity's LoadRawTextureData reads bottom-to-top (GL convention)
                val bgrFlipped = Mat()
                Core.flip(bgrDisplay, bgrFlipped, 0)
                bgrDisplay.release()

                // BGR → RGBA for Unity texture
                val rgba = Mat()
                Imgproc.cvtColor(bgrFlipped, rgba, Imgproc.COLOR_BGR2RGBA)
                bgrFlipped.release()
                val rgbaBytes = ByteArray(w * h * 4)
                rgba.get(0, 0, rgbaBytes)
                rgba.release()
                latestRgba.set(rgbaBytes)
                if (frameCallbackCount <= 3) Log.i(TAG, "latestRgba set: ${rgbaBytes.size} bytes")

                frameRgbaCount++
                if (frameRgbaCount <= 5 || frameRgbaCount % 30 == 0) {
                    val px0 = if (rgbaBytes.size >= 4) "${rgbaBytes[0].toInt() and 0xFF},${rgbaBytes[1].toInt() and 0xFF},${rgbaBytes[2].toInt() and 0xFF},${rgbaBytes[3].toInt() and 0xFF}" else "?"
                    val dbg = "recog=[${slotEnabled[0].get()},${slotEnabled[1].get()}] proc=$isProcessing eng=${engine!=null} dbEmpty=${database?.isEmpty()}"
                    Log.i(TAG, "rgbaReady #$frameRgbaCount ${w}x${h} firstPx=[$px0] $dbg")
                }
            } else {
                latestRgba.set(null)
            }

            bgrForRecog = bgr
        } catch (e: Exception) {
            Log.e(TAG, "frameCallback error: ${e.message}", e)
            bgrForRecog?.release()
            return@IFrameCallback
        }

        // Detection only runs when something actually needs it: recognition itself (either
        // slot), or a game (FRTest) that opted into bounding-box display via
        // setBoundingBoxEnabled(). Headless games never set that flag, so once their
        // recognition window closes, detection stops entirely instead of running every frame
        // for boxes nobody reads. Which slots to actually run align+embed+match for is decided
        // per-slot inside processFrame — this flag only gates whether detection runs at all.
        val anySlotEnabled = slotEnabled[0].get() || slotEnabled[1].get()
        val needsDetection = anySlotEnabled || bboxEnabled.get()

        if (needsDetection && !isProcessing) {
            isProcessing = true
            try {
                detectionExecutor.execute {
                    try { processFrame(bgrForRecog!!, anySlotEnabled) }
                    finally { isProcessing = false; bgrForRecog.release() }
                }
            } catch (e: java.util.concurrent.RejectedExecutionException) {
                // Narrow window: executor was mid-shutdown (see shutdownInternal()) right as this
                // frame arrived. isProcessing was already flipped true above but the lambda that
                // resets it never got scheduled — without this catch, isProcessing would stay
                // stuck true forever and detection would silently stop for the rest of the
                // process's life (the outer if() above would never pass again), plus bgrForRecog
                // would leak. Reset both here instead.
                Log.w(TAG, "detectionExecutor rejected frame (shutting down) — dropping")
                isProcessing = false
                bgrForRecog.release()
            }
        } else {
            if (!needsDetection) latestFaceRects.set(emptyList())
            bgrForRecog.release()
        }
    }

    // face_zones.json: kiểm tra mtime tối đa 2s/lần, chỉ trên thread nhận diện (không đụng main thread
    // của Unity). Sửa vùng trong Quản lý lớp rồi vào chơi là áp dụng, không cần build lại.
    private var zonesCache: FaceZones? = null
    private var zonesMtime = -1L
    private var zonesCheckedMs = 0L

    private fun currentZones(): FaceZones? {
        val now = android.os.SystemClock.elapsedRealtime()
        if (now - zonesCheckedMs >= 2000L) {
            zonesCheckedMs = now
            val m = if (FaceZones.file.exists()) FaceZones.file.lastModified() else 0L
            if (m != zonesMtime) {
                zonesMtime = m
                zonesCache = if (m == 0L) null else FaceZones.loadOrNull()
                Log.i(TAG, "zones reloaded: $zonesCache")
            }
        }
        return zonesCache
    }

    /** Cắt vùng [l,t,r,b] (pixel khung đầy đủ) -> thu nhỏ 0.5 -> dò mặt -> trả toạ độ về khung đầy đủ.
     * Cắt TRƯỚC khi dò nên bộ dò chỉ xử lý đúng vùng đó (YuNet tốn theo số pixel đầu vào). */
    private fun detectInRegion(eng: FaceEngine, bgr: Mat, l: Float, t: Float, r: Float, b: Float): List<DetectedFace> {
        val fw = bgr.cols().toFloat(); val fh = bgr.rows().toFloat()
        val left = l.coerceIn(0f, fw - 2f); val top = t.coerceIn(0f, fh - 2f)
        val right = r.coerceIn(left + 2f, fw); val bottom = b.coerceIn(top + 2f, fh)
        val region = Mat(bgr, org.opencv.core.Rect(left.toInt(), top.toInt(), (right - left).toInt(), (bottom - top).toInt())) // view, không copy
        // Detect at half resolution — YuNet's cost scales roughly with input pixel count, so
        // this cuts detection time ~3-4x with negligible accuracy loss (same trick as the FA app).
        val detectScale = 0.5f
        val small = Mat()
        Imgproc.resize(region, small, Size(region.cols() * detectScale.toDouble(), region.rows() * detectScale.toDouble()))
        region.release()
        val facesSmall = eng.detectFaces(small, DETECT_CANDIDATES)
        small.release()
        return facesSmall.map { scaleAndOffsetFace(it, 1f / detectScale, left, top) }
    }

    private fun processFrame(bgr: Mat, doRecognition: Boolean) {
        val eng = engine ?: return
        val cfg = config

        // Crop to the configured region BEFORE detection — this actually shrinks the detector's
        // input (X01a-only optimization; config coords are on-screen pixels at refWidth x
        // refHeight, scaled to actual frame size). The FA app is untouched — it has no crop step.
        // Nếu có /sdcard/EduXplore/face_zones.json (chỉnh trong Quản lý lớp) thì dùng 2 vùng playLeft/
        // playRight: cắt và dò riêng từng vùng, mặt chỉ tính khi TÂM nằm trong vùng của bên đó.
        val frameW = bgr.cols().toFloat()
        val frameH = bgr.rows().toFloat()
        val zones = currentZones()
        val leftFace: DetectedFace?
        val rightFace: DetectedFace?
        val tDetectStart = System.nanoTime()
        if (zones != null) {
            // Cắt và dò RIÊNG từng vùng (không dò khoảng trống giữa 2 vùng, không dò ngoài vùng): chỉ mặt
            // có tâm trong vùng mới tính, 1 mặt lớn nhất mỗi bên.
            fun inZone(f: DetectedFace, z: ZoneRect) =
                z.contains((f.rect.left + f.rect.right) / 2f / frameW, (f.rect.top + f.rect.bottom) / 2f / frameH)
            fun detectZone(z: ZoneRect) = detectInRegion(eng, bgr, z.x * frameW, z.y * frameH, (z.x + z.w) * frameW, (z.y + z.h) * frameH)
                .filter { inZone(it, z) }.maxByOrNull { it.rect.width() * it.rect.height() }
            leftFace  = detectZone(zones.playLeft)
            rightFace = detectZone(zones.playRight)
        } else {
            // Chưa có face_zones.json: giữ hành vi cũ — khung cắt cfg (pixel trên màn refWidth x refHeight)
            // rồi chia trái/phải theo đường giữa khung.
            val scaleX = frameW / cfg.refWidth
            val scaleY = frameH / cfg.refHeight
            val cropLeft   = (cfg.cropX * scaleX).coerceIn(0f, frameW - 1f)
            val cropTop    = (cfg.cropY * scaleY).coerceIn(0f, frameH - 1f)
            val cropRight  = ((cfg.cropX + cfg.cropW) * scaleX).coerceIn(cropLeft + 1f, frameW)
            val cropBottom = ((cfg.cropY + cfg.cropH) * scaleY).coerceIn(cropTop + 1f, frameH)
            val candidates = detectInRegion(eng, bgr, cropLeft, cropTop, cropRight, cropBottom)
            val half = frameW / 2f
            leftFace  = candidates.filter { (it.rect.left + it.rect.right) / 2f < half }
                .maxByOrNull { it.rect.width() * it.rect.height() }
            rightFace = candidates.filter { (it.rect.left + it.rect.right) / 2f >= half }
                .maxByOrNull { it.rect.width() * it.rect.height() }
        }
        val tDetectEnd = System.nanoTime()
        val slots = listOf(leftFace, rightFace)

        latestFaceRects.set(slots.mapNotNull { it?.rect })
        if (leftFace != null || rightFace != null) {
            Log.i(TAG, "processFrame: left=${leftFace != null} right=${rightFace != null}")
        }

        if (!doRecognition) return

        val db = database ?: return
        if (db.isEmpty()) return

        // Do the expensive work (alignment, embedding, DB match) OUTSIDE the lock — only the
        // final array write is synchronized. Holding the lock across this would stall Unity's
        // per-frame getConfirmedNames() JNI call (main thread) for as long as the computation
        // takes, freezing the whole app instead of just delaying the name update.
        for (slot in 0 until MAX_FACES) {
            if (!slotEnabled[slot].get()) continue      // nobody is currently waiting on this slot
            if (lastRecognized[slot] != null) continue // already locked this round — stop re-recognizing

            val face = slots[slot] ?: continue
            val pose = eng.estimatePose(face)
            if (!pose.isFrontal(FaceEngine.YAW_TOLERANCE, FaceEngine.ROLL_TOLERANCE)) continue

            val tAlignStart = System.nanoTime()
            val aligned   = eng.alignFace(bgr, face)
            val tAlignEnd = System.nanoTime()
            val embedding = eng.getEmbedding(aligned)
            val tEmbedEnd = System.nanoTime()
            aligned.release()

            val match = db.bestMatch(embedding)
            val tMatchEnd = System.nanoTime()
            Log.i(TAG, "perf slot=$slot detect=${"%.1f".format((tDetectEnd - tDetectStart) / 1_000_000.0)}ms " +
                "align=${"%.1f".format((tAlignEnd - tAlignStart) / 1_000_000.0)}ms " +
                "embed=${"%.1f".format((tEmbedEnd - tAlignEnd) / 1_000_000.0)}ms " +
                "match=${"%.1f".format((tMatchEnd - tEmbedEnd) / 1_000_000.0)}ms")
            if (match.name != null) {
                synchronized(lastRecognized) {
                    if (lastRecognized[slot] == null) {
                        lastRecognized[slot] = match.name
                        lastConfidence[slot] = match.sim
                    }
                }
                Log.i(TAG, "slot $slot LOCKED → ${match.name} (sim=${"%.3f".format(match.sim)})")
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /**
     * Scales a DetectedFace from a downsampled+cropped detection space back to full-frame
     * coordinates: multiply by [scale] first, then shift by ([offsetX], [offsetY]) — the crop
     * region's top-left corner in full-frame pixels.
     */
    private fun scaleAndOffsetFace(face: DetectedFace, scale: Float, offsetX: Float, offsetY: Float): DetectedFace {
        val raw = face.rawRow.copyOf()
        raw[0] = raw[0] * scale + offsetX // box x
        raw[1] = raw[1] * scale + offsetY // box y
        raw[2] *= scale                   // box w — no offset, it's a delta
        raw[3] *= scale                   // box h — no offset, it's a delta
        for (i in 0..4) {                 // 5 landmark (x, y) pairs starting at index 4
            val xi = 4 + i * 2
            raw[xi]     = raw[xi]     * scale + offsetX
            raw[xi + 1] = raw[xi + 1] * scale + offsetY
        }
        return DetectedFace(
            rect = android.graphics.RectF(raw[0], raw[1], raw[0] + raw[2], raw[1] + raw[3]),
            landmarks = (0..4).map { i -> android.graphics.PointF(raw[4 + i * 2], raw[4 + i * 2 + 1]) },
            score = face.score,
            rawRow = raw
        )
    }

    /** Chỉ 1 camera USB cố định trên máy — thay vì đòi khớp đúng USB_CLASS_VIDEO/VID quen (dễ
     *  trượt nếu descriptor bị ROM misparse, đã gặp thật), coi MỌI device không rõ ràng thuộc
     *  nhóm khác (hub/chuột/bàn phím/LiDAR UART/...) là ứng viên camera — không cần chờ/dò 2 tầng. */
    private fun looksLikeUvcCamera(device: UsbDevice): Boolean {
        if (device.vendorId in knownSerialVids) return false
        if (device.interfaceCount == 0) return true // descriptor không đọc được — vẫn thử
        return (0 until device.interfaceCount).any { i ->
            device.getInterface(i).interfaceClass !in excludedCameraClasses
        }
    }
}
