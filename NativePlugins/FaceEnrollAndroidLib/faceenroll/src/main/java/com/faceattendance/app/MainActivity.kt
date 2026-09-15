package com.faceattendance.app

import android.Manifest
import android.app.AlertDialog
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.PointF
import android.graphics.RectF
import android.graphics.SurfaceTexture
import android.hardware.usb.UsbDevice
import android.graphics.Matrix
import android.net.Uri
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import android.view.Gravity
import android.widget.Button
import android.widget.EditText
import android.widget.HorizontalScrollView
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.RadioButton
import android.widget.RadioGroup
import android.widget.TextView
import androidx.activity.result.ActivityResultLauncher
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import com.serenegiant.usb.IFrameCallback
import com.serenegiant.usb.USBMonitor
import com.serenegiant.usb.UVCCamera
import org.opencv.android.OpenCVLoader
import org.opencv.android.Utils
import org.opencv.core.Core
import org.opencv.core.CvType
import org.opencv.core.Mat
import org.opencv.core.Rect
import org.opencv.imgproc.Imgproc
import java.nio.ByteBuffer
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors

/**
 * NOTE on how this camera is actually reached: the webcam's video interface (Sonix
 * Technology, vendorId 0x0c45/3141, productId 0x636b/25451) is invisible to Android's
 * Camera2 (`dumpsys media.camera` -> 0 devices), invisible to AUSBC's pure-Kotlin UVC
 * device scan (its interface-class heuristic chokes on this device's descriptors - the
 * OS's own UsbDescriptorParser throws on it too), and OpenCV's Android VideoCapture has
 * no V4L2 backend compiled in even though /dev/video0 exists. What DOES work (confirmed
 * live on this exact board via com.shenyaocn.android.usbcamera, a libuvc-based UVC
 * viewer, streaming happily at ~42fps) is native libuvc reading the raw USB descriptors
 * itself instead of trusting Android's higher-level UsbInterface Java objects. So this
 * activity vendors saki4510t/UVCCamera's native layer (com.serenegiant.usb.USBMonitor +
 * UVCCamera, built from src/main/jni via ndk-build) instead of any pure-Java/OpenCV path.
 */
class MainActivity : AppCompatActivity(), USBMonitor.OnDeviceConnectListener {

    companion object {
        private const val CAMERA_PERMISSION_REQUEST_CODE = 1001
    }

    private lateinit var previewImage: ImageView
    private lateinit var overlayView: OverlayView
    private lateinit var tvStatus: TextView
    private lateinit var tvFps: TextView
    private lateinit var recentPeoplePanel: LinearLayout
    private lateinit var btnEnroll: Button
    private lateinit var btnEnrollPhoto: Button
    private lateinit var btnClose: Button
    private lateinit var btnManage: Button
    private lateinit var btnExport: Button
    private lateinit var tvGreeting: TextView
    private lateinit var tvInfoPanel: TextView

    private lateinit var photoPickerLauncher: ActivityResultLauncher<String>

    private var faceEngine: FaceEngine? = null
    private lateinit var attendanceStore: AttendanceStore
    private lateinit var greetingTts: GreetingTts

    private lateinit var usbMonitor: USBMonitor
    private var uvcCamera: UVCCamera? = null
    private var frameWidth = UVCCamera.DEFAULT_PREVIEW_WIDTH
    private var frameHeight = UVCCamera.DEFAULT_PREVIEW_HEIGHT

    // NOTE on why there's no vendor/product ID filter here: the first webcam this app was
    // built against (Sonix Technology 0x0c45/0x636b) got its USB descriptors misparsed by
    // Android's UsbManager, which reported completely different (vid=742/pid=6733) IDs to
    // apps - so matching on a hardcoded ID pair is fragile even for a single camera, and
    // breaks outright the moment a different webcam model is plugged in (each one gets its
    // own, possibly also-misreported, IDs). Instead: attempt to open *every* attached USB
    // device as a UVC camera and let UVCCamera.open()/startPreview() fail (caught below) for
    // whatever isn't actually a camera - works for any webcam without recalibration.

    // Supports up to MAX_FACES people being recognized at once. Faces are assigned to a
    // "slot" each frame by sorting left-to-right, so slot 0 = leftmost face, slot 1 =
    // next, etc. Vote history is tracked per slot so temporal smoothing still works
    // per-person even with several faces in frame simultaneously.
    private val MAX_FACES = 2
    // OpenCV's own recommendation for the SFace model (opencv_zoo demo.py):
    // cosine similarity >= 0.363 means same identity.
    private val matchThreshold = 0.363f
    // Minimum lead the best match must have over the runner-up to be trusted. Without this,
    // a large gallery (dozens-hundreds of people) makes it increasingly likely some other
    // enrolled person's embedding also clears matchThreshold by coincidence. This is also
    // what catches near-identical faces (e.g. identical twins) - two people whose embeddings
    // are this close trigger a manual confirmation instead of a silent (possibly wrong) pick.
    private val marginThreshold = 0.08f
    // Loosened as far as still makes sense: beyond this, SFace's own alignment/embedding
    // accuracy degrades regardless of whether YuNet still reports a detection.
    private val yawTolerance = 0.60f
    private val rollToleranceDeg = 45f
    private val cooldownMs = 15_000L

    @Volatile private var lastDetected: DetectedFace? = null
    @Volatile private var lastGoodPose = false

    // Snapshot-based enrollment: captures all unrecognized faces in a single frame when the
    // ENROLL button is pressed, then shows sequential name dialogs for each.
    @Volatile private var enrollSnapshotRequested = false

    // Detection idle throttle: when no face is found, skip detection for 1s to save CPU.
    private var nextDetectAllowedMs = 0L

    // Double-buffer for overlay: last computed items are pushed to the UI on every incoming
    // camera frame (including dropped ones), so the bounding box refreshes at camera FPS
    // even though detection itself runs at a lower rate.
    @Volatile private var cachedOverlayItems: List<FaceOverlayItem> = emptyList()
    @Volatile private var cachedOverlayW: Int = 640
    @Volatile private var cachedOverlayH: Int = 360

    // How long a slot must remain absent before its resolvedLock is cleared.
    // A short grace absorbs YuNet's occasional missed-detection frame (blink, motion blur)
    // without treating it as the person having actually left.
    private val lockPresenceGraceMs = 500L
    private val lastSeenMs = LongArray(MAX_FACES) { 0L }

    // Separate history for detecting sustained ambiguous pairs (two people too similar to auto-pick).
    private val ambiguousHistories = List(MAX_FACES) { ArrayDeque<Pair<String, String>?>() }
    private val ambiguousWindow = 5
    private val ambiguousMinAgree = 3

    private class ResolvedLock(val name: String, val sinceMs: Long)
    // Confirmed-person lock per slot: once set, only show tracking (green box) without
    // running recognition again. Cleared when the slot has been absent for > lockPresenceGraceMs.
    private val resolvedLocks = arrayOfNulls<ResolvedLock>(MAX_FACES)
    private val wasPresent = BooleanArray(MAX_FACES)

    // Greeting fires once per appearance in frame, not once per cooldownMs like attendance
    // logging. Keyed by NAME (not slot index) since slot assignment is re-sorted left-to-
    // right every frame and can shift if a second person enters/leaves; a single missed-
    // detection frame (common - blinking, motion blur) also shouldn't count as "left frame",
    // so a name only drops out after greetingAbsenceGraceMs of not being seen at all.
    private val greetedActive = HashMap<String, Long>()
    private val greetingAbsenceGraceMs = 2000L

    // Announces once per appearance of an unrecognized face (confidently "Unknown", not a
    // transient misdetection), same absence-grace pattern as greetedActive above - but a single
    // global flag instead of per-name, since an unrecognized face has no identity to key by.
    private var newFaceLastSeenMs = 0L
    private var newFaceAnnouncedThisAppearance = false
    private val newFaceAnnounceGraceMs = 3000L
    private val newFaceAnnounceText = "Hệ thống ghi nhận có khuôn mặt mới, vui lòng ấn vào màn hình để cập nhật danh sách."

    // Smooths the 5 display-only landmark dots over time per slot (EMA) to cut down per-frame
    // jitter from YuNet's landmark head, which is noticeably less precise than its own box
    // detection - most visible on the mouth-corner points. Recognition itself is unaffected:
    // alignFace()/getEmbedding() still run on the raw, unsmoothed detector output.
    private val smoothedLandmarks = arrayOfNulls<Array<PointF>>(MAX_FACES)
    private val landmarkSmoothingAlpha = 0.4f

    private var fpsEma: Double? = null
    private val displayTimeFmt = SimpleDateFormat("HH:mm:ss", Locale.US)

    /** Logs full details of any uncaught exception on any thread before the process dies - the
     * default handler already ultimately kills the process the same way, this just makes sure
     * the reason is captured in logcat (the actual recovery is WatchdogReceiver, which works
     * independently of this and also covers native crashes that never reach here at all). */
    private fun installCrashLogger() {
        val defaultHandler = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            Log.e("FaceAttendance", "Uncaught exception on thread ${thread.name}", throwable)
            defaultHandler?.uncaughtException(thread, throwable)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        installCrashLogger()

        photoPickerLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
            if (uri != null) enrollFromPhoto(uri)
        }

        setContentView(R.layout.activity_main)

        previewImage = findViewById(R.id.preview_image)
        overlayView = findViewById(R.id.overlay_view)
        tvStatus = findViewById(R.id.tv_status)
        tvFps = findViewById(R.id.tv_fps)
        recentPeoplePanel = findViewById(R.id.recent_people_panel)
        btnEnroll = findViewById(R.id.btn_enroll)
        btnEnrollPhoto = findViewById(R.id.btn_enroll_photo)
        btnClose = findViewById(R.id.btn_close)
        btnManage = findViewById(R.id.btn_manage)
        btnExport = findViewById(R.id.btn_export)
        tvGreeting = findViewById(R.id.tv_greeting)
        tvInfoPanel = findViewById(R.id.tv_info_panel)
        btnClose.setOnClickListener { finish() }
        btnEnroll.setOnClickListener { onEnrollClicked() }
        btnEnrollPhoto.setOnClickListener {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.READ_EXTERNAL_STORAGE)
                    != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(this,
                    arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE), CAMERA_PERMISSION_REQUEST_CODE)
            } else {
                photoPickerLauncher.launch("image/*")
            }
        }
        btnManage.setOnClickListener { showManageStudentsDialog() }
        btnExport.setOnClickListener { exportAttendanceCsv() }

        attendanceStore = AttendanceStore(this)
        greetingTts = GreetingTts(this)
        greetingTts.startWeatherRefresh()
        loadTestGalleryIfPresent()
        OpenCVLoader.initLocal()
        faceEngine = FaceEngine(this)

        tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | waiting for USB camera..."
        usbMonitor = USBMonitor(this, this)
        startHealthLog()
        startInfoPanelRefresh()

        // On BHSE/K02's vendor Android 10 build, AOSP's UsbUserSettingsManager silently
        // denies USB permission for USB_CLASS_VIDEO devices unless the app also holds the
        // runtime CAMERA permission. Only request CAMERA here; storage permissions are
        // granted via `adb shell pm grant` during device setup (see README) to avoid the
        // startup dialog on a kiosk device. READ_EXTERNAL_STORAGE is requested lazily when
        // the gallery picker is launched.
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.CAMERA), CAMERA_PERMISSION_REQUEST_CODE)
        }

        // Debug-only: lets `adb shell am broadcast -a com.faceattendance.app.DEBUG_TEST_TTS`
        // exercise the exact greeting/audio pipeline without needing a live face in front of
        // the camera. Harmless to leave registered - no-one else can trigger it without adb.
        registerReceiver(debugTtsReceiver, IntentFilter("com.faceattendance.app.DEBUG_TEST_TTS"))
    }

    private val debugTtsReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent) {
            Log.e("FaceAttendance", "DEBUG_TEST_TTS triggered")
            speakGreeting(listOf("Test" to "nam"))
        }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode != CAMERA_PERMISSION_REQUEST_CODE) return
        permissions.forEachIndexed { i, perm ->
            if (grantResults.getOrElse(i) { PackageManager.PERMISSION_DENIED } != PackageManager.PERMISSION_GRANTED) return@forEachIndexed
            when (perm) {
                Manifest.permission.CAMERA -> {
                    // Re-request USB permission for already-attached cameras now that CAMERA is granted.
                    for (device in usbMonitor.deviceList) {
                        if (looksLikeUvcCamera(device)) usbMonitor.requestPermission(device)
                    }
                }
                Manifest.permission.READ_EXTERNAL_STORAGE -> {
                    // Gallery picker was blocked waiting for this; launch it now.
                    photoPickerLauncher.launch("image/*")
                }
            }
        }
    }

    /**
     * Test-only: if a pre-generated gallery JSON (see gen_test_gallery.py) has been pushed
     * to this app's external files dir, load it to simulate hundreds of enrolled people -
     * lets margin-check behavior be validated without physically enrolling that many people.
     * `adb push test_gallery_200.json /sdcard/Android/data/com.faceattendance.app/files/`
     */
    private fun loadTestGalleryIfPresent() {
        val file = java.io.File(getExternalFilesDir(null), "test_gallery_200.json")
        if (!file.exists()) return
        try {
            val count = attendanceStore.importTestGallery(file.readText())
            Log.e("FaceAttendance", "Loaded test gallery: $count people")
        } catch (e: Exception) {
            Log.e("FaceAttendance", "Failed to load test gallery", e)
        }
    }

    private var usbMonitorRegistered = false

    override fun onStart() {
        super.onStart()
        // Always reset reconnect state and re-register so onAttach() fires for every attached
        // device. For the whitelisted camera (vid=742), permission is already granted → onConnect
        // fires immediately with no dialog, so reconnect is nearly instant.
        fallbackScheduled = false
        retryPermissionScheduled = false
        skippedDevices.clear()
        fallbackHandler.removeCallbacksAndMessages(null)
        if (!usbMonitorRegistered) {
            usbMonitorRegistered = true
            usbMonitor.register()
        }
    }

    override fun onStop() {
        // Release the USB camera so X01 (Unity FRTest) can open it when FA goes to background.
        // The VID whitelist in looksLikeUvcCamera() ensures reconnect in onStart() is fast
        // (permission already granted → onConnect fires without a dialog).
        uvcCamera?.setFrameCallback(null, 0)
        uvcCamera?.stopPreview()
        uvcCamera?.close()
        uvcCamera = null
        usbMonitor.unregister()
        usbMonitorRegistered = false
        super.onStop()
    }

    // --- USBMonitor.OnDeviceConnectListener ---

    /** USB_CLASS_VIDEO (14) on any interface is the standards-based way to recognize "this is
     * a UVC camera" without needing to know its vendor/product ID in advance - unlike device-
     * descriptor vid/pid (which got misparsed for the very first webcam this app supported),
     * this reads the *configuration* descriptor's per-interface class, a different, generally
     * more reliable code path. Logged either way so a logcat capture always shows exactly
     * what's attached and why it was/wasn't tried, even if this heuristic ever misses one.
     */
    // Cameras confirmed to work but that don't declare USB_CLASS_VIDEO in their Java-visible
    // interface descriptors. Whitelisting by VID bypasses the 6-second fallback delay.
    private val knownCameraVids = setOf(742)  // Rapoo cam (vid=742) — interfaceClasses=[]

    private fun looksLikeUvcCamera(device: UsbDevice): Boolean {
        if (device.vendorId in knownCameraVids) return true
        for (i in 0 until device.interfaceCount) {
            val iface = device.getInterface(i)
            if (iface.interfaceClass == android.hardware.usb.UsbConstants.USB_CLASS_VIDEO) return true
        }
        return false
    }

    // Devices seen but skipped (didn't look like a UVC camera) - retried as a last resort if
    // no camera is working after fallbackDelayMs, in case looksLikeUvcCamera() ever misses a
    // real camera whose interfaces Android's Java API doesn't report cleanly (this happened
    // for the very first webcam this app supported, via a different symptom).
    private val skippedDevices = mutableListOf<UsbDevice>()
    private val fallbackHandler = Handler(Looper.getMainLooper())
    private val fallbackDelayMs = 6000L
    private var fallbackScheduled = false
    // After the initial fallback fires, keep re-requesting permission for all skipped devices
    // every retryPermissionIntervalMs until a camera connects. The first APK install after
    // uninstall clears all USB device permissions, and on this K02 vendor ROM the permission
    // dialog for devices whose descriptors Android can't parse (interfaceClasses=[]) is
    // sometimes silently dropped or dismissed - periodic retry ensures the dialog reappears.
    private val retryPermissionIntervalMs = 15_000L
    private var retryPermissionScheduled = false

    private fun schedulePermissionRetry() {
        if (retryPermissionScheduled) return
        retryPermissionScheduled = true
        fallbackHandler.postDelayed(object : Runnable {
            override fun run() {
                if (uvcCamera != null) { retryPermissionScheduled = false; return }
                val unpermitted = skippedDevices.filter { !usbMonitor.hasPermission(it) }
                if (unpermitted.isNotEmpty()) {
                    Log.e("FaceAttendance", "Camera retry: requesting permission for ${unpermitted.size} device(s) still without permission")
                    runOnUiThread { tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | đang xin quyền camera lần nữa..." }
                    for (d in unpermitted) usbMonitor.requestPermission(d)
                }
                fallbackHandler.postDelayed(this, retryPermissionIntervalMs)
            }
        }, retryPermissionIntervalMs)
    }

    override fun onAttach(device: UsbDevice) {
        val ifaceClasses = (0 until device.interfaceCount).joinToString(",") { device.getInterface(it).interfaceClass.toString() }
        val looksLikeCamera = looksLikeUvcCamera(device)
        Log.e(
            "FaceAttendance",
            "USB attach: vid=${device.vendorId} pid=${device.productId} name=${device.deviceName} " +
                "interfaceClasses=[$ifaceClasses] looksLikeUvcCamera=$looksLikeCamera"
        )
        if (uvcCamera != null) {
            Log.e("FaceAttendance", "Already have a working camera, ignoring this attach")
            return
        }
        if (!looksLikeCamera) {
            // Requesting permission for every attached USB device (hubs, touch controllers,
            // etc.) wastes the permission dialog on the wrong device and confused -50
            // ("invalid device") failures with the real camera never even being tried - only
            // ask for devices that actually declare a Video interface, with the full list
            // as a fallback below if that ever turns out wrong for a given camera model.
            skippedDevices.add(device)
            if (!fallbackScheduled) {
                fallbackScheduled = true
                fallbackHandler.postDelayed({
                    if (uvcCamera == null) {
                        // Only retry devices that could plausibly be cameras — exclude anything
                        // whose every interface is a well-known non-camera USB class (HID,
                        // mass-storage, hub, CDC/serial, audio, printer). This prevents popping
                        // permission dialogs for mice, keyboards, USB dongles, serial adapters, etc.
                        val obviouslyNotCamera = setOf(
                            android.hardware.usb.UsbConstants.USB_CLASS_AUDIO,       // 1
                            android.hardware.usb.UsbConstants.USB_CLASS_COMM,        // 2 CDC comm
                            android.hardware.usb.UsbConstants.USB_CLASS_HID,         // 3
                            android.hardware.usb.UsbConstants.USB_CLASS_PRINTER,     // 7
                            android.hardware.usb.UsbConstants.USB_CLASS_MASS_STORAGE,// 8
                            android.hardware.usb.UsbConstants.USB_CLASS_HUB,         // 9
                            android.hardware.usb.UsbConstants.USB_CLASS_CDC_DATA     // 10 serial
                        )
                        // USB-to-serial adapters report class=255 (vendor-specific) rather than
                        // CDC, so the interface-class filter above misses them. Blacklist by VID.
                        val knownSerialVids = setOf(
                            0x10C4,  // Silicon Labs CP210x (vid=4292 decimal)
                            0x1A86,  // WinChipHead CH340/CH341 (vid=6790 decimal)
                            0x0403,  // FTDI FT232
                            0x067B,  // Prolific PL2303
                            0x0B95   // ATEN USB serial (vid=2965 decimal)
                        )
                        val candidates = skippedDevices.filter { dev ->
                            dev.vendorId !in knownSerialVids &&
                            // A device with no declared interfaces (interfaceCount==0, seen on some
                            // UVC cams that don't advertise their class in the Java API) is unknown
                            // — treat it as a candidate rather than silently skipping it.
                            (dev.interfaceCount == 0 || (0 until dev.interfaceCount).any { i ->
                                dev.getInterface(i).interfaceClass !in obviouslyNotCamera
                            })
                        }
                        Log.e("FaceAttendance", "No UVC camera found, fallback: ${candidates.size}/${skippedDevices.size} device(s) look plausibly camera-like")
                        for (d in candidates) usbMonitor.requestPermission(d)
                        if (candidates.isNotEmpty()) schedulePermissionRetry()
                    }
                }, fallbackDelayMs)
            }
            return
        }
        runOnUiThread { tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | camera found, requesting permission..." }
        usbMonitor.requestPermission(device)
    }

    override fun onDettach(device: UsbDevice) {}

    override fun onConnect(device: UsbDevice, ctrlBlock: USBMonitor.UsbControlBlock, createNew: Boolean) {
        if (uvcCamera != null) return
        Log.e("FaceAttendance", "USB onConnect, trying to open as UVCCamera: vid=${device.vendorId} pid=${device.productId} name=${device.deviceName}")
        try {
            val camera = UVCCamera()
            camera.open(ctrlBlock)
            try {
                // MJPEG instead of raw YUYV: at 1280x720, uncompressed YUYV needs ~55MB/s at
                // 30fps, well past what this USB link sustains, so the camera was silently
                // negotiating down to ~9-11fps. MJPEG's compressed frames need a fraction of
                // that bandwidth - libuvc decodes it back to YUV natively (libjpeg-turbo is
                // vendored in for exactly this) so PIXEL_FORMAT_NV21 below is unaffected.
                camera.setPreviewSize(1280, 720, UVCCamera.FRAME_FORMAT_MJPEG)
                frameWidth = 1280
                frameHeight = 720
            } catch (e: Exception) {
                Log.w("FaceAttendance", "1280x720 MJPEG not supported, falling back to YUYV", e)
                try {
                    camera.setPreviewSize(1280, 720, UVCCamera.DEFAULT_PREVIEW_MODE)
                    frameWidth = 1280
                    frameHeight = 720
                } catch (e2: Exception) {
                    Log.w("FaceAttendance", "1280x720 not supported at all, falling back to default", e2)
                    camera.setPreviewSize(UVCCamera.DEFAULT_PREVIEW_WIDTH, UVCCamera.DEFAULT_PREVIEW_HEIGHT, UVCCamera.DEFAULT_PREVIEW_MODE)
                    frameWidth = UVCCamera.DEFAULT_PREVIEW_WIDTH
                    frameHeight = UVCCamera.DEFAULT_PREVIEW_HEIGHT
                }
            }
            try {
                camera.setBacklightComp(70)
            } catch (e: Exception) {
                Log.w("FaceAttendance", "backlight comp not supported", e)
            }
            camera.setFrameCallback(frameCallback, UVCCamera.PIXEL_FORMAT_NV21)
            // UVCPreview::startPreview() (native) only spawns its capture thread when
            // mPreviewWindow is non-null - even though we only want the frame callback
            // and never render anything, we still have to hand it a SurfaceTexture.
            // Detached mode (API 19+) needs no GL context since we never consume it.
            val dummyTexture = SurfaceTexture(false)
            camera.setPreviewTexture(dummyTexture)
            camera.startPreview()
            uvcCamera = camera
            runOnUiThread { tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | camera streaming ${frameWidth}x${frameHeight}" }
        } catch (e: Exception) {
            Log.e("FaceAttendance", "Failed to open/start UVCCamera", e)
            runOnUiThread { tvStatus.text = "FAILED to start camera: ${e.message}" }
        }
    }

    override fun onDisconnect(device: UsbDevice, ctrlBlock: USBMonitor.UsbControlBlock) {
        uvcCamera?.destroy()
        uvcCamera = null
    }

    override fun onCancel(device: UsbDevice) {
        runOnUiThread { tvStatus.text = "USB permission denied for camera" }
    }

    @Volatile private var isProcessing = false
    private var lastFrameArrivalNs = 0L
    private var droppedWhileBusy = 0
    // Detection/recognition runs here, off the native capture callback thread entirely - the
    // UVC pipeline waits for onFrame() to return before it decodes/delivers the next frame, so
    // running processFrame() inline (even after reordering the preview update earlier) still
    // throttled capture itself to detection speed. This executor is what actually decouples them.
    private val detectionExecutor = Executors.newSingleThreadExecutor()

    private val frameCallback = IFrameCallback { frame: ByteBuffer ->
        val now = System.nanoTime()
        val sinceLastMs = if (lastFrameArrivalNs == 0L) 0.0 else (now - lastFrameArrivalNs) / 1_000_000.0
        lastFrameArrivalNs = now
        val t0 = System.nanoTime()
        // frame's backing buffer is only valid until this callback returns, so the copy has to
        // happen here - everything after this point is safe to hand off to another thread.
        val data = ByteArray(frame.remaining())
        frame.get(data)
        val yuv = Mat(frameHeight + frameHeight / 2, frameWidth, CvType.CV_8UC1)
        yuv.put(0, 0, data)
        val bgrRaw = Mat()
        Imgproc.cvtColor(yuv, bgrRaw, Imgproc.COLOR_YUV2BGR_NV21)
        yuv.release()
        val bgrPreview = Mat()
        Core.flip(bgrRaw, bgrPreview, 1)  // 1 = horizontal mirror
        bgrRaw.release()
        val t1 = System.nanoTime()

        // The preview always refreshes here, on every frame the camera delivers - kept on this
        // thread since it's cheap (a color convert + bitmap copy), unlike detection below.
        updatePreviewBitmap(bgrPreview)

        // Push the last known overlay result on every frame so the bounding box moves at
        // camera FPS rather than at detection FPS. Dropped frames still get this refresh.
        val snap = cachedOverlayItems; val sw = cachedOverlayW; val sh = cachedOverlayH
        runOnUiThread { overlayView.update(snap, sw, sh) }

        if (isProcessing) {
            droppedWhileBusy++
            bgrPreview.release()
            Log.e("FacePerf", "DROPPED frame (busy) - arrival gap=${"%.1f".format(sinceLastMs)}ms, dropped_total=$droppedWhileBusy")
            return@IFrameCallback
        }
        isProcessing = true
        // bgrPreview is handed off here - released by the detection thread once it's done with
        // it, not by this (capture) thread, since this callback must return immediately.
        detectionExecutor.execute {
            try {
                processFrame(bgrPreview)
                val t2 = System.nanoTime()
                val totalMs = (t2 - t0) / 1_000_000.0
                if (totalMs > 0) {
                    val instFps = 1000.0 / totalMs
                    fpsEma = if (fpsEma == null) instFps else 0.9 * fpsEma!! + 0.1 * instFps
                }
                Log.e(
                    "FacePerf",
                    "arrival_gap=${"%.1f".format(sinceLastMs)}ms  nv21_to_bgr=${"%.1f".format((t1 - t0) / 1_000_000.0)}ms  " +
                        "processFrame=${"%.1f".format((t2 - t1) / 1_000_000.0)}ms  TOTAL=${"%.1f".format(totalMs)}ms  " +
                        "fps_if_sustained=${"%.1f".format(1000.0 / totalMs)}"
                )
            } catch (e: Exception) {
                Log.e("FaceAttendance", "frame processing error", e)
            } finally {
                isProcessing = false
                bgrPreview.release()
            }
        }
    }

    /** Crops a padded region around a face box out of a BGR Mat - caller owns/releases the result. */
    private fun cropFace(bgr: Mat, rect: RectF, padRatio: Float = 0.3f): Mat {
        val padX = (rect.width() * padRatio).toInt()
        val padY = (rect.height() * padRatio).toInt()
        val x0 = (rect.left - padX).toInt().coerceIn(0, bgr.cols() - 1)
        val y0 = (rect.top - padY).toInt().coerceIn(0, bgr.rows() - 1)
        val x1 = (rect.right + padX).toInt().coerceIn(x0 + 1, bgr.cols())
        val y1 = (rect.bottom + padY).toInt().coerceIn(y0 + 1, bgr.rows())
        return Mat(bgr, Rect(x0, y0, x1 - x0, y1 - y0)).clone()
    }

    private fun matToBitmap(mat: Mat): Bitmap {
        val bmp = Bitmap.createBitmap(mat.cols(), mat.rows(), Bitmap.Config.ARGB_8888)
        Utils.matToBitmap(mat, bmp)
        return bmp
    }

    /** Scales a DetectedFace from a downsampled detection space back to the original frame space. */
    private fun scaleFace(face: DetectedFace, scale: Float): DetectedFace {
        val r = face.rect
        val raw = face.rawRow.copyOf()
        for (i in 0..13) raw[i] *= scale  // x, y, w, h, 5 landmark pairs — skip score at [14]
        return DetectedFace(
            rect = RectF(r.left * scale, r.top * scale, r.right * scale, r.bottom * scale),
            landmarks = face.landmarks.map { PointF(it.x * scale, it.y * scale) },
            score = face.score,
            rawRow = raw
        )
    }

    private fun processFrame(bgr: Mat) {
        val engine = faceEngine ?: return
        val width = bgr.cols()
        val height = bgr.rows()
        val nowMs = System.currentTimeMillis()

        val staleGreeted = greetedActive.entries.filter { nowMs - it.value > greetingAbsenceGraceMs }.map { it.key }
        for (name in staleGreeted) greetedActive.remove(name)
        if (nowMs - newFaceLastSeenMs > newFaceAnnounceGraceMs) newFaceAnnouncedThisAppearance = false

        // Skip detection entirely while in the post-no-face idle window.
        if (nowMs < nextDetectAllowedMs) {
            cachedOverlayItems = emptyList(); cachedOverlayW = width; cachedOverlayH = height
            return
        }

        // Detect at half resolution — YuNet's cost scales roughly with input pixel count, so
        // this gives ~3-4x speedup with negligible accuracy loss for faces seen at normal range.
        val detectScale = 0.5f
        val small = Mat()
        Imgproc.resize(bgr, small, org.opencv.core.Size(width * detectScale.toDouble(), height * detectScale.toDouble()))
        val tDetectStart = System.nanoTime()
        val facesSmall = engine.detectFaces(small, MAX_FACES)
        val tDetectEnd = System.nanoTime()
        small.release()
        // Scale coordinates back to full-res space so the rest of the pipeline (alignFace,
        // overlay drawing) works on the original frame dimensions.
        val faces = facesSmall.map { scaleFace(it, 1f / detectScale) }.sortedBy { it.rect.left }

        // Push bounding boxes immediately after detection — before recognition runs — so the
        // overlay updates at detection FPS rather than being blocked on the recognition pipeline.
        cachedOverlayItems = faces.take(MAX_FACES).mapIndexed { i, face ->
            val lock = if (i < MAX_FACES) resolvedLocks[i] else null
            if (lock != null) FaceOverlayItem(face.rect, face.landmarks.map { android.graphics.PointF(it.x, it.y) }, "${lock.name} ✓", android.graphics.Color.GREEN)
            else FaceOverlayItem(face.rect, face.landmarks.map { android.graphics.PointF(it.x, it.y) }, "...", android.graphics.Color.GRAY)
        }
        cachedOverlayW = width; cachedOverlayH = height

        if (faces.isEmpty()) {
            lastDetected = null
            // Release locks for slots that have been absent long enough.
            for (i in 0 until MAX_FACES) {
                if (nowMs - lastSeenMs[i] > lockPresenceGraceMs) {
                    if (resolvedLocks[i] != null) {
                        Log.e("FaceAttendance", "slot $i lock cleared (absent > ${lockPresenceGraceMs}ms)")
                    }
                    resolvedLocks[i] = null
                    ambiguousHistories[i].clear()
                }
                smoothedLandmarks[i] = null
                wasPresent[i] = false
            }
            // Idle 1s before next detection attempt.
            nextDetectAllowedMs = nowMs + 1000
            cachedOverlayItems = emptyList(); cachedOverlayW = width; cachedOverlayH = height
            Log.e("FacePerf", "  detect=${"%.1f".format((tDetectEnd - tDetectStart) / 1_000_000.0)}ms (no face — idle 1s)")
            return
        }

        // Handle enroll snapshot: capture embeddings for all unrecognized faces, then show
        // sequential name dialogs. Done before the display loop so we can return early.
        if (enrollSnapshotRequested) {
            enrollSnapshotRequested = false
            val toEnroll = mutableListOf<Pair<FloatArray, Mat>>()
            for ((i, face) in faces.withIndex()) {
                if (i < MAX_FACES && resolvedLocks[i] == null) {
                    val aligned = engine.alignFace(bgr, face)
                    val emb = engine.getEmbedding(aligned)
                    aligned.release()
                    toEnroll.add(emb to cropFace(bgr, face.rect))
                }
            }
            if (toEnroll.isEmpty()) {
                runOnUiThread { toastStatus("Không có người mới — tất cả đã được nhận diện") }
            } else {
                runOnUiThread { enrollNextFace(toEnroll, 0) }
            }
            return
        }

        val overlayItems = mutableListOf<FaceOverlayItem>()
        // Anyone whose logAttendance() actually fired (i.e. cleared cooldown) this frame -
        // both slots are checked before speaking, so two simultaneous check-ins get one
        // combined greeting instead of two overlapping ones.
        val justLogged = mutableListOf<Pair<String, String>>()
        for (slot in 0 until MAX_FACES) {
            if (slot >= faces.size) {
                // Slot absent this frame: release lock if gone long enough.
                if (nowMs - lastSeenMs[slot] > lockPresenceGraceMs) {
                    if (resolvedLocks[slot] != null) {
                        Log.e("FaceAttendance", "slot $slot lock cleared (absent)")
                        resolvedLocks[slot] = null
                        ambiguousHistories[slot].clear()
                    }
                }
                smoothedLandmarks[slot] = null
                wasPresent[slot] = false
                continue
            }
            val face = faces[slot]
            if (slot == 0) lastDetected = face
            lastSeenMs[slot] = nowMs
            wasPresent[slot] = true

            val lock = resolvedLocks[slot]
            var color: Int
            val label: String

            if (lock != null) {
                // Person already confirmed: just track — no embedding, no matching.
                color = Color.GREEN
                if (!greetedActive.containsKey(lock.name)) {
                    justLogged.add(lock.name to attendanceStore.genderOf(lock.name))
                }
                greetedActive[lock.name] = nowMs
                if (attendanceStore.canLog(lock.name, cooldownMs)) {
                    val crop = cropFace(bgr, face.rect)
                    attendanceStore.logAttendance(lock.name, crop)
                    crop.release()
                    runOnUiThread { refreshRecentPeoplePanel() }
                }
                label = "${lock.name} ✓"
            } else {
                // Unknown slot: run full recognition.
                val pose = engine.estimatePose(face)
                val goodPose = pose.isFrontal(yawTolerance, rollToleranceDeg)
                if (slot == 0) lastGoodPose = goodPose

                val tAlignStart = System.nanoTime()
                val aligned = engine.alignFace(bgr, face)
                val tAlignEnd = System.nanoTime()
                val embedding = engine.getEmbedding(aligned)
                val tEmbedEnd = System.nanoTime()
                aligned.release()

                Log.e(
                    "FacePerf",
                    "  slot=$slot detect=${"%.1f".format((tDetectEnd - tDetectStart) / 1_000_000.0)}ms  " +
                        "align=${"%.1f".format((tAlignEnd - tAlignStart) / 1_000_000.0)}ms  " +
                        "embed=${"%.1f".format((tEmbedEnd - tAlignEnd) / 1_000_000.0)}ms"
                )

            val poseLabel = "yaw=${"%+.2f".format(pose.yawOffset)} roll=${"%+.1f".format(pose.rollDeg)}deg"

            if (!goodPose) {
                color = Color.rgb(255, 165, 0)
                label = "TURN TO CAMERA | $poseLabel"
                ambiguousHistories[slot].clear()
            } else {
                val match = attendanceStore.bestMatch(embedding, engine)
                val name = match.name
                val sim = match.sim
                val runnerUpName = match.runnerUpName
                val runnerUpSim = match.runnerUpSim
                val margin = sim - runnerUpSim
                val isAmbiguous = name != null && sim > matchThreshold && margin < marginThreshold
                val confirmed = name != null && sim > matchThreshold && !isAmbiguous
                Log.e(
                    "FaceMatch",
                    "slot=$slot best=$name sim=${"%.3f".format(sim)} runnerUp=${"%.3f".format(runnerUpSim)} " +
                        "margin=${"%.3f".format(margin)} ambiguous=$isAmbiguous confirmed=$confirmed"
                )

                when {
                    attendanceStore.enrolledCount() == 0 -> {
                        color = Color.rgb(0, 200, 255)
                        label = "face OK - press ENROLL | $poseLabel"
                    }
                    confirmed -> {
                        // Single-frame confirmation: set lock immediately, no voting needed.
                        resolvedLocks[slot] = ResolvedLock(name!!, nowMs)
                        color = Color.GREEN
                        if (!greetedActive.containsKey(name)) {
                            justLogged.add(name to attendanceStore.genderOf(name))
                        }
                        greetedActive[name] = nowMs
                        if (attendanceStore.canLog(name, cooldownMs)) {
                            val crop = cropFace(bgr, face.rect)
                            attendanceStore.logAttendance(name, crop)
                            crop.release()
                            runOnUiThread { refreshRecentPeoplePanel() }
                        }
                        label = "$name ${"%.2f".format(sim)}"
                    }
                    isAmbiguous -> {
                        color = Color.MAGENTA
                        label = "AMBIGUOUS ${"%.2f".format(sim)}/${"%.2f".format(runnerUpSim)} best=$name"
                    }
                    else -> {
                        color = Color.RED
                        label = "Unknown ${"%.2f".format(sim)} best=$name"
                        newFaceLastSeenMs = nowMs
                        if (!newFaceAnnouncedThisAppearance) {
                            newFaceAnnouncedThisAppearance = true
                            announceNewFace()
                        }
                    }
                }
            }
            } // end else (full recognition)

            val rect = RectF(face.rect)
            val rawPts = face.landmarks.map { PointF(it.x, it.y) }
            val prev = smoothedLandmarks[slot]
            val rawMeanX = rawPts.sumOf { it.x.toDouble() }.toFloat() / rawPts.size
            val rawMeanY = rawPts.sumOf { it.y.toDouble() }.toFloat() / rawPts.size
            val smoothed: Array<PointF> = if (prev == null) {
                rawPts.toTypedArray()
            } else {
                val prevMeanX = prev.sumOf { it.x.toDouble() }.toFloat() / prev.size
                val prevMeanY = prev.sumOf { it.y.toDouble() }.toFloat() / prev.size
                val dist = kotlin.math.hypot((prevMeanX - rawMeanX).toDouble(), (prevMeanY - rawMeanY).toDouble())
                if (dist > rect.width() * 0.5) {
                    rawPts.toTypedArray() // different face (or first sighting) in this slot - don't blend across identities
                } else {
                    Array(rawPts.size) { i ->
                        PointF(
                            landmarkSmoothingAlpha * rawPts[i].x + (1 - landmarkSmoothingAlpha) * prev[i].x,
                            landmarkSmoothingAlpha * rawPts[i].y + (1 - landmarkSmoothingAlpha) * prev[i].y
                        )
                    }
                }
            }
            smoothedLandmarks[slot] = smoothed
            overlayItems.add(FaceOverlayItem(rect, smoothed.toList(), label, color))
        }

        cachedOverlayItems = overlayItems; cachedOverlayW = width; cachedOverlayH = height
        runOnUiThread {
            overlayView.update(overlayItems, width, height)
            tvFps.text = if (fpsEma != null) "FPS: ${"%.1f".format(fpsEma)}" else "FPS: --"
            tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | faces in view: ${faces.size}"
        }
        // Both slots are already resolved by this point in the frame, so 1 vs 2 simultaneous
        // check-ins gets a single combined greeting instead of two overlapping ones.
        if (justLogged.isNotEmpty()) {
            speakGreeting(justLogged)
        }
    }

    /** Builds and speaks the greeting for whoever just checked in, showing the sentence on
     * tvGreeting for the duration of playback (or ~4s if audio fails/no internet). */
    private fun speakGreeting(people: List<Pair<String, String>>) {
        val text = greetingTts.buildGreeting(people)
        Log.e("FaceAttendance", "Greeting: $text")
        runOnUiThread {
            tvGreeting.text = text
            tvGreeting.visibility = android.view.View.VISIBLE
        }
        greetingTts.speak(
            text,
            onDone = {
                tvGreeting.postDelayed({ tvGreeting.visibility = android.view.View.GONE }, 1500)
            }
        )
    }

    /** Fires once per appearance of an unrecognized face (see newFaceAnnouncedThisAppearance)
     * so whoever's watching the kiosk knows to tap the screen and enroll rather than standing
     * there un-greeted with no explanation. Shares the same banner/queue as speakGreeting()
     * since GreetingTts.speak() already serializes back-to-back calls. */
    private fun announceNewFace() {
        Log.e("FaceAttendance", "New face announcement")
        runOnUiThread {
            tvGreeting.text = newFaceAnnounceText
            tvGreeting.visibility = android.view.View.VISIBLE
        }
        greetingTts.speak(
            newFaceAnnounceText,
            onDone = {
                tvGreeting.postDelayed({ tvGreeting.visibility = android.view.View.GONE }, 1500)
            }
        )
    }

    private fun updatePreviewBitmap(bgr: Mat) {
        val t0 = System.nanoTime()
        val bmp = matToBitmap(bgr)
        val t1 = System.nanoTime()
        runOnUiThread { previewImage.setImageBitmap(bmp) }
        Log.e("FacePerf", "  updatePreviewBitmap (alloc+convert)=${"%.1f".format((t1 - t0) / 1_000_000.0)}ms")
    }

    private fun onEnrollClicked() {
        if (lastDetected == null) {
            toastStatus("ENROLL: không có người trong khung hình")
            return
        }
        enrollSnapshotRequested = true
        tvStatus.text = "Đang chụp..."
    }

    /**
     * Picks an image the user selected from the gallery, runs face detection on it, and if
     * exactly one face is found extracts its embedding and calls showEnrollNameDialog() — the
     * same dialog as the live-camera path.  Runs detection on a background thread so it doesn't
     * block the UI while decoding/processing a large photo.
     */
    private fun enrollFromPhoto(uri: Uri) {
        toastStatus("Đang xử lý ảnh...")
        detectionExecutor.execute {
            val engine = faceEngine
            if (engine == null) {
                runOnUiThread { toastStatus("ENROLL TỪ ẢNH: FaceEngine chưa sẵn sàng") }
                return@execute
            }
            try {
                val raw = contentResolver.openInputStream(uri)?.use { stream ->
                    android.graphics.BitmapFactory.decodeStream(stream)
                }
                if (raw == null) {
                    runOnUiThread { toastStatus("ENROLL TỪ ẢNH: không đọc được ảnh") }
                    return@execute
                }

                // Apply EXIF rotation — phone cameras store portrait shots as landscape+rotation
                // metadata; BitmapFactory.decodeStream ignores it, so faces come in sideways and
                // YuNet misses them. Read the orientation tag via a second stream open.
                val bitmap = run {
                    val degrees = try {
                        contentResolver.openInputStream(uri)?.use { s ->
                            val exif = androidx.exifinterface.media.ExifInterface(s)
                            when (exif.getAttributeInt(
                                androidx.exifinterface.media.ExifInterface.TAG_ORIENTATION,
                                androidx.exifinterface.media.ExifInterface.ORIENTATION_NORMAL)) {
                                androidx.exifinterface.media.ExifInterface.ORIENTATION_ROTATE_90  -> 90f
                                androidx.exifinterface.media.ExifInterface.ORIENTATION_ROTATE_180 -> 180f
                                androidx.exifinterface.media.ExifInterface.ORIENTATION_ROTATE_270 -> 270f
                                else -> 0f
                            }
                        } ?: 0f
                    } catch (e: Exception) { 0f }

                    if (degrees != 0f) {
                        val m = Matrix().apply { postRotate(degrees) }
                        val rotated = Bitmap.createBitmap(raw, 0, 0, raw.width, raw.height, m, true)
                        raw.recycle()
                        rotated
                    } else raw
                }

                // BitmapFactory gives ARGB_8888; convert to BGR for OpenCV models.
                val rgba = Mat()
                Utils.bitmapToMat(bitmap, rgba)
                bitmap.recycle()
                val bgr = Mat()
                Imgproc.cvtColor(rgba, bgr, Imgproc.COLOR_RGBA2BGR)
                rgba.release()

                val faces = engine.detectFaces(bgr, 10)
                if (faces.isEmpty()) {
                    bgr.release()
                    runOnUiThread { toastStatus("ENROLL TỪ ẢNH: không phát hiện khuôn mặt nào") }
                } else {
                    val faceData = faces.map { face ->
                        val aligned = engine.alignFace(bgr, face)
                        val emb = engine.getEmbedding(aligned)
                        aligned.release()
                        emb to cropFace(bgr, face.rect)
                    }
                    bgr.release()
                    runOnUiThread { enrollNextFace(faceData, 0) }
                }
            } catch (e: Exception) {
                Log.e("FaceAttendance", "enrollFromPhoto error", e)
                runOnUiThread { toastStatus("ENROLL TỪ ẢNH: lỗi — ${e.message}") }
            }
        }
    }

    /** Sequential multi-person enrollment: shows a dialog for each unrecognized face in order.
     *  Called with index=0 from processFrame's snapshot handler and recurses until done. */
    private fun enrollNextFace(faces: List<Pair<FloatArray, Mat>>, index: Int) {
        if (index >= faces.size) {
            return
        }
        showEnrollNameDialog(
            samples = listOf(faces[index]),
            personIndex = index,
            totalPersons = faces.size,
            onComplete = { enrollNextFace(faces, index + 1) }
        )
    }

    /** Shows a name/gender input dialog for one person. Displays their face crop so the user
     *  can see who they're naming. onComplete is called after OK or Skip (for sequential flow). */
    private fun showEnrollNameDialog(
        samples: List<Pair<FloatArray, Mat>>,
        personIndex: Int = 0,
        totalPersons: Int = 1,
        onComplete: (() -> Unit)? = null
    ) {
        val dp = resources.displayMetrics.density
        val inner = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            setPadding((24 * dp).toInt(), (12 * dp).toInt(), (24 * dp).toInt(), (8 * dp).toInt())
        }

        // Left: face preview
        val crop = samples.firstOrNull()?.second
        if (crop != null && !crop.empty()) {
            val sizePx = (120 * dp).toInt()
            inner.addView(ImageView(this).apply {
                setImageBitmap(matToBitmap(crop))
                layoutParams = LinearLayout.LayoutParams(sizePx, sizePx).also {
                    it.gravity = Gravity.CENTER_VERTICAL
                    it.rightMargin = (16 * dp).toInt()
                }
                scaleType = ImageView.ScaleType.FIT_CENTER
            })
        }

        // Right: inputs
        val rightCol = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            layoutParams = LinearLayout.LayoutParams(0, android.view.ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }

        if (totalPersons > 1) {
            rightCol.addView(TextView(this).apply {
                text = "Người ${personIndex + 1} / $totalPersons"
                setPadding(0, 0, 0, (4 * dp).toInt())
            })
        }

        val input = EditText(this).apply {
            hint = "Tên..."
            // Dismiss keyboard when the user taps Done/Enter
            imeOptions = android.view.inputmethod.EditorInfo.IME_ACTION_DONE
            setOnEditorActionListener { _, _, _ -> clearFocus(); false }
        }
        rightCol.addView(input)

        val genderGroup = RadioGroup(this).apply { orientation = RadioGroup.HORIZONTAL }
        val rbMale = RadioButton(this).apply { text = "Nam"; isChecked = true; id = 1001 }
        val rbFemale = RadioButton(this).apply { text = "Nữ"; id = 1002 }
        genderGroup.addView(rbMale); genderGroup.addView(rbFemale)
        rightCol.addView(genderGroup)

        inner.addView(rightCol)

        val negLabel = if (totalPersons > 1) "Bỏ qua" else "Huỷ"
        val dlg = AlertDialog.Builder(this)
            .setTitle(if (totalPersons > 1) "Đặt tên (${personIndex + 1}/$totalPersons)" else "Nhập tên")
            .setView(inner)
            .setCancelable(totalPersons <= 1)
            .setPositiveButton("Lưu") { _, _ ->
                val name = input.text.toString().trim()
                if (name.isNotEmpty()) {
                    val gender = if (genderGroup.checkedRadioButtonId == 1002) "nu" else "nam"
                    attendanceStore.setGender(name, gender)
                    for ((emb, c) in samples) {
                        attendanceStore.addSample(name, emb, c)
                        c.release()
                    }
                    toastStatus("Đã lưu: $name (${attendanceStore.samplesOf(name).size} mẫu)")
                } else {
                    for ((_, c) in samples) c.release()
                }
                onComplete?.invoke()
            }
            .setNegativeButton(negLabel) { _, _ ->
                for ((_, c) in samples) c.release()
                onComplete?.invoke()
            }
            .create()

        // Push dialog above keyboard instead of being covered by it
        dlg.window?.setSoftInputMode(
            android.view.WindowManager.LayoutParams.SOFT_INPUT_ADJUST_PAN or
            android.view.WindowManager.LayoutParams.SOFT_INPUT_STATE_VISIBLE
        )
        dlg.show()
    }

    /** Pops up when the same two people stay too close to call for several frames (e.g.
     * twins) - shows the live capture next to each candidate's enrollment photo so a human
     * can pick, instead of the system silently guessing. Blocks the calling (camera) thread
     * until the user responds, mirroring how the rest of frame processing is synchronous. */
    private fun showAmbiguousConfirmDialogBlocking(nameA: String, nameB: String, liveCrop: Bitmap): String? {
        val latch = CountDownLatch(1)
        var chosen: String? = null
        runOnUiThread {
            lateinit var dialog: AlertDialog

            fun column(title: String, bmp: Bitmap?, buttonText: String?, onClick: (() -> Unit)?): LinearLayout {
                val col = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    gravity = Gravity.CENTER
                    setPadding(24, 8, 24, 8)
                }
                val iv = ImageView(this).apply {
                    layoutParams = LinearLayout.LayoutParams(220, 220)
                    setImageBitmap(bmp ?: Bitmap.createBitmap(220, 220, Bitmap.Config.ARGB_8888))
                    scaleType = ImageView.ScaleType.CENTER_CROP
                }
                col.addView(iv)
                col.addView(TextView(this).apply { text = title; gravity = Gravity.CENTER; textSize = 14f })
                if (buttonText != null && onClick != null) {
                    col.addView(Button(this).apply { text = buttonText; setOnClickListener { onClick() } })
                }
                return col
            }

            val row = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
                setPadding(16, 16, 16, 16)
            }
            row.addView(column("Live capture", liveCrop, null, null))
            row.addView(column(nameA, attendanceStore.loadPhotoBitmap(attendanceStore.representativePhoto(nameA), 220), "This is $nameA") {
                chosen = nameA
                latch.countDown()
                dialog.dismiss()
            })
            row.addView(column(nameB, attendanceStore.loadPhotoBitmap(attendanceStore.representativePhoto(nameB), 220), "This is $nameB") {
                chosen = nameB
                latch.countDown()
                dialog.dismiss()
            })

            val scroll = HorizontalScrollView(this).apply { addView(row) }

            dialog = AlertDialog.Builder(this)
                .setTitle("Ambiguous match: $nameA vs $nameB - who is this?")
                .setView(scroll)
                .setNegativeButton("Neither / Skip") { _, _ -> latch.countDown() }
                .setOnCancelListener { latch.countDown() }
                .create()
            dialog.show()
        }
        latch.await()
        return chosen
    }

    private fun toastStatus(msg: String) {
        runOnUiThread { tvStatus.text = msg }
    }

    /** Lists real enrolled students (test-gallery entries excluded); tapping one shows their
     * individual sample photos with per-sample delete, plus a whole-person remove option. */
    private fun showManageStudentsDialog() {
        val names = attendanceStore.realEnrolledNames()
        if (names.isEmpty()) {
            toastStatus("No students enrolled yet")
            return
        }
        AlertDialog.Builder(this)
            .setTitle("Students (${names.size}) - tap to view/remove")
            .setItems(names.toTypedArray()) { _, which -> showStudentSamplesDialog(names[which]) }
            .setNegativeButton("Close", null)
            .show()
    }

    private fun showStudentSamplesDialog(name: String) {
        val container = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(16, 16, 16, 16) }
        val grid = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val scroll = HorizontalScrollView(this).apply { addView(grid) }
        container.addView(TextView(this).apply { text = "$name - ${attendanceStore.samplesOf(name).size} sample(s)"; textSize = 14f })
        container.addView(scroll)

        lateinit var dialog: AlertDialog

        fun rebuildGrid() {
            grid.removeAllViews()
            val samples = attendanceStore.samplesOf(name)
            if (samples.isEmpty()) {
                dialog.dismiss()
                return
            }
            samples.forEachIndexed { index, sample ->
                val cell = LinearLayout(this).apply {
                    orientation = LinearLayout.VERTICAL
                    gravity = Gravity.CENTER
                    setPadding(8, 8, 8, 8)
                }
                val bmp = attendanceStore.loadPhotoBitmap(sample.photo, 100)
                cell.addView(ImageView(this).apply {
                    layoutParams = LinearLayout.LayoutParams(100, 100)
                    setImageBitmap(bmp ?: Bitmap.createBitmap(100, 100, Bitmap.Config.ARGB_8888))
                    scaleType = ImageView.ScaleType.CENTER_CROP
                })
                val tag = if (index < AttendanceStore.PERMANENT_SAMPLES) "permanent" else "rolling"
                cell.addView(TextView(this).apply { text = tag; textSize = 10f })
                cell.addView(Button(this).apply {
                    text = "Delete"
                    setOnClickListener {
                        attendanceStore.deleteSample(name, index)
                        rebuildGrid()
                    }
                })
                grid.addView(cell)
            }
        }

        dialog = AlertDialog.Builder(this)
            .setView(container)
            .setPositiveButton("Remove all", null)
            .setNegativeButton("Close", null)
            .create()
        dialog.setOnShowListener {
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
                AlertDialog.Builder(this)
                    .setTitle("Remove $name?")
                    .setMessage("Removes all samples and photos for $name.")
                    .setPositiveButton("Remove") { _, _ ->
                        attendanceStore.deleteEnrollment(name)
                        toastStatus("Removed $name (total ${attendanceStore.enrolledCount()})")
                        dialog.dismiss()
                    }
                    .setNegativeButton("Cancel", null)
                    .show()
            }
        }
        rebuildGrid()
        dialog.show()
    }

    /** Shares the attendance CSV via Android's chooser (email, Drive, USB file manager,
     * whatever's installed) - there's no fixed destination system yet, so a share sheet is
     * the most general way to get the log off the board without needing school-specific
     * server/API integration. */
    /** Không dùng FileProvider share-sheet nữa — khai 1 <provider> riêng bị Android chặn (đụng
     *  FileProvider.androidlib có sẵn của Unity, lỗi "Colliding Attributes" lúc merge manifest).
     *  Xuất/nhập dữ liệu kiểu khác (qua màn ControlActivity) sẽ làm sau — tạm thời chỉ báo rõ
     *  đường dẫn file để lấy qua adb/file manager. */
    private fun exportAttendanceCsv() {
        val file = attendanceStore.logFileIfExists()
        if (file == null) {
            toastStatus("No attendance logged yet, nothing to export")
            return
        }
        toastStatus("Attendance log: ${file.absolutePath}")
    }

    private val healthLogHandler = Handler(Looper.getMainLooper())
    private val healthLogIntervalMs = 60_000L
    private val bootTimeMs = SystemClock.elapsedRealtime()

    /**
     * Periodic low-cost log line (uptime, JVM heap, dropped-frame count) so a multi-hour/
     * multi-day soak test leaves a trail to diagnose memory leaks or creeping slowdown from,
     * without having to babysit the board the whole time - just `adb logcat` afterwards.
     */
    private fun startHealthLog() {
        healthLogHandler.postDelayed(object : Runnable {
            override fun run() {
                val rt = Runtime.getRuntime()
                val usedMb = (rt.totalMemory() - rt.freeMemory()) / (1024 * 1024)
                val maxMb = rt.maxMemory() / (1024 * 1024)
                val uptimeMin = (SystemClock.elapsedRealtime() - bootTimeMs) / 60_000.0
                Log.e(
                    "FaceHealth",
                    "uptime=${"%.1f".format(uptimeMin)}min heapUsed=${usedMb}MB/${maxMb}MB " +
                        "enrolled=${attendanceStore.enrolledCount()} droppedFrames=$droppedWhileBusy " +
                        "cameraOpen=${uvcCamera != null}"
                )
                healthLogHandler.postDelayed(this, healthLogIntervalMs)
            }
        }, healthLogIntervalMs)
    }

    private val infoPanelHandler = Handler(Looper.getMainLooper())

    /** Top-left, always-on general info for anyone looking at the screen (not tied to a
     * specific recognized person): date/time, location, today's temperature range, current
     * conditions - refreshed every second so the clock stays live. */
    private fun startInfoPanelRefresh() {
        infoPanelHandler.postDelayed(object : Runnable {
            override fun run() {
                tvInfoPanel.text = "${greetingTts.dateTimeText()}\n${greetingTts.locationName()}\n" +
                    "Nhiệt độ trong ngày: ${greetingTts.dailyRangeText()}\n${greetingTts.weatherSummaryText()}"
                infoPanelHandler.postDelayed(this, 1000L)
            }
        }, 0L)
    }

    /** Rebuilds the top-right "recent check-ins" panel: up to RECENT_PEOPLE_SHOWN people,
     * most recent first, each with their latest snapshot and first/last/times. Must run on
     * the UI thread. */
    private fun refreshRecentPeoplePanel() {
        recentPeoplePanel.removeAllViews()
        for (name in attendanceStore.recentNames.asReversed()) {
            val entry = attendanceStore.personLog[name] ?: continue
            val snapshot = entry.snapshot ?: continue
            val row = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
                setPadding(0, 4, 0, 4)
            }
            row.addView(ImageView(this).apply {
                layoutParams = LinearLayout.LayoutParams(60, 60)
                setImageBitmap(snapshot)
                scaleType = ImageView.ScaleType.CENTER_CROP
            })
            val textCol = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL
                setPadding(8, 0, 0, 0)
            }
            textCol.addView(TextView(this).apply {
                text = "$name (x${entry.count})"
                setTextColor(Color.GREEN)
                textSize = 11f
            })
            textCol.addView(TextView(this).apply {
                text = "first ${displayTimeFmt.format(java.util.Date(entry.first))}"
                setTextColor(Color.WHITE)
                textSize = 10f
            })
            textCol.addView(TextView(this).apply {
                text = "last  ${displayTimeFmt.format(java.util.Date(entry.last))}"
                setTextColor(Color.WHITE)
                textSize = 10f
            })
            row.addView(textCol)
            recentPeoplePanel.addView(row)
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        healthLogHandler.removeCallbacksAndMessages(null)
        infoPanelHandler.removeCallbacksAndMessages(null)
        fallbackHandler.removeCallbacksAndMessages(null) // also cancels schedulePermissionRetry
        uvcCamera?.setFrameCallback(null, 0)
        uvcCamera?.stopPreview()
        uvcCamera?.close()
        uvcCamera = null
        usbMonitorRegistered = false
        usbMonitor.unregister()
        usbMonitor.destroy()
        faceEngine?.close()
        greetingTts.release()
        detectionExecutor.shutdownNow()
        unregisterReceiver(debugTtsReceiver)
    }
}
