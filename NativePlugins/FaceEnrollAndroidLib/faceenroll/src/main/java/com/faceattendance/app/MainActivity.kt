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

        // Đến từ ClassManagementActivity — lớp đang chọn (gán cho người mới enroll), và chế độ
        // mở màn hình này: xem/quản lý ảnh 1 học sinh có sẵn, hoặc mở luôn picker chọn nhiều
        // ảnh (bulk add). Không có extra nào thì mở như màn camera bình thường.
        const val EXTRA_TARGET_CLASS = "target_class"
        const val EXTRA_MODE = "mode"
        const val EXTRA_STUDENT_NAME = "student_name"
        const val MODE_VIEW_STUDENT = "view_student"
        const val MODE_BULK_PICK = "bulk_pick"
        // "Cập nhật ảnh" từ panel chi tiết học sinh trong ClassManagementActivity — học sinh đã
        // biết tên rồi (EXTRA_STUDENT_NAME), nên bỏ qua hẳn showEnrollNameDialog() (hỏi tên/tên
        // gọi/giới tính cho người MỚI), add thẳng mẫu ảnh vào đúng tên đó. Camera: giáo viên tự
        // bấm nút chụp như bình thường (targetStudentName lệch hướng enrollNextFace tại chỗ).
        // Gallery: mở luôn picker nhiều ảnh, mỗi ảnh cũng add thẳng vào tên đó.
        const val MODE_ADD_SAMPLES_CAMERA = "add_samples_camera"
        const val MODE_ADD_SAMPLES_GALLERY = "add_samples_gallery"
        // Chỉnh 3 vùng nhận diện (2 vùng game + vùng chữ nhật enroll) trên hình camera thật, rồi Lưu.
        // Từ Quản lý lớp và từ tab Cài đặt của ControlActivity. Không nhận diện/chào/điểm danh ở chế độ này.
        const val MODE_ZONE_SETUP = "zone_setup"
    }

    // Vùng nhận diện: màn camera bình thường chỉ xử lý mặt có tâm trong vùng chữ nhật enroll.
    private var zones: FaceZones = FaceZones.DEFAULT
    private var zoneSetupMode = false
    @Volatile private var activityReleased = false

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
    private lateinit var photoPickerMultiLauncher: ActivityResultLauncher<String>
    // Lớp đang được gán cho bất kỳ ai mới enroll trong phiên này (null = không gán lớp),
    // đến từ ClassManagementActivity. pendingBulkPick nhớ việc bấm "Thêm hàng loạt" đang chờ
    // quyền READ_EXTERNAL_STORAGE, để biết bật picker đơn hay nhiều khi quyền vừa được cấp.
    private var targetClassName: String? = null
    // Khi != null: đang ở chế độ "cập nhật ảnh" cho 1 học sinh ĐÃ CÓ (từ panel chi tiết trong
    // ClassManagementActivity) — enrollNextFace() add thẳng mẫu vào tên này, bỏ qua hộp thoại đặt
    // tên/giới tính (đã biết cả rồi, không phải người mới).
    private var targetStudentName: String? = null
    private var pendingBulkPick = false

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

    // Supports up to MAX_FACES people being recognized at once. Mỗi khuôn mặt được gắn vào 1
    // FaceTrack bền vững qua các frame bằng IoU/khoảng cách tâm (KHÔNG còn sắp theo vị trí
    // trái->phải mỗi frame: cách cũ làm tên "nhảy" sang người khác khi thứ tự trái/phải đổi hoặc
    // người thứ 2 che người 1). Lock tên nằm trong track và được kiểm chứng lại bằng embedding
    // định kỳ / khi box thay đổi đột ngột.
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

    // Track ĐÃ có tên: giữ (vẽ khung xanh + tên, dùng lại vị trí cuối khi YuNet hụt mặt do nghiêng/che)
    // cho tới khi KHÔNG thấy mặt nào quanh vị trí đó liên tục chừng này ms thì coi là đã rời khung hình.
    private val lockPresenceGraceMs = 1000L
    // Track chưa có tên: hụt ngắn thôi là bỏ (không có gì để giữ).
    private val unlockedPresenceGraceMs = 500L
    // Verify nền sau khi đã có tên: cần chừng này lần khớp lại (mặt thẳng) thì hiện ✓ và thôi nhận diện.
    private val verifyFramesNeeded = 3
    // Quá chừng này lần thử (mặt thẳng) mà chưa đủ lần khớp, cũng không có người khác => chấp nhận luôn, thôi nhận diện.
    private val verifyMaxTries = 8

    private fun graceOf(t: FaceTrack) = if (t.lock != null) lockPresenceGraceMs else unlockedPresenceGraceMs

    private class ResolvedLock(val name: String, val sinceMs: Long, val sim: Float)
    private class FaceTrack(var rect: RectF, var lastSeenMs: Long) {
        // Confirmed-person lock: khi có, chỉ vẽ khung xanh + theo vị trí, không nhận diện lại.
        var lock: ResolvedLock? = null
        // Verify nền: verified=true => hiện ✓ và không chạy embedding nữa cho tới khi track bị xoá.
        var verified = false
        var verifyOk = 0
        var verifyBad = 0
        var verifyTries = 0
        var smoothed: Array<PointF>? = null
        // Số lần nhận diện (mặt frontal) liên tiếp mà không ra ai => đủ ngưỡng mới báo "mặt mới".
        var unknownCount = 0
    }
    private val tracks = ArrayList<FaceTrack>()
    // Cần chừng này lần nhận diện liên tiếp không ra ai (như người quen cũng cần vài frame mới chắc)
    // thì mới coi là khuôn mặt mới và cảnh báo.
    private val unknownFramesToAnnounce = 4

    private fun rectIou(a: RectF, b: RectF): Float {
        val iw = minOf(a.right, b.right) - maxOf(a.left, b.left)
        val ih = minOf(a.bottom, b.bottom) - maxOf(a.top, b.top)
        if (iw <= 0f || ih <= 0f) return 0f
        val inter = iw * ih
        val union = a.width() * a.height() + b.width() * b.height() - inter
        return if (union <= 0f) 0f else inter / union
    }

    /** Gắn mỗi mặt vào track theo VỊ TRÍ (IoU, greedy, cặp tốt nhất trước). Mặt không khớp (không
     * chồng đủ lên track nào, hoặc đổi cỡ đột ngột — dấu hiệu người khác chen vào/che) -> track mới,
     * chưa có tên, sẽ được nhận diện như người mới. Track không được gắn giữ lại tới hết
     * lockPresenceGraceMs rồi bị xoá cùng lock. Không dùng embedding để kiểm chứng lại lock: nhận
     * 1 lần là giữ tên tới khi mặt rời khung, tránh tên chớp tắt. */
    private fun assignTracks(faces: List<DetectedFace>, nowMs: Long): List<FaceTrack> {
        expireTracks(nowMs)
        class Cand(val f: Int, val t: Int, val score: Float)
        val cands = ArrayList<Cand>()
        for (fi in faces.indices) {
            val fr = faces[fi].rect
            for (ti in tracks.indices) {
                val track = tracks[ti]
                val tr = track.rect
                // Track đã có tên được nới lỏng: mặt nghiêng/quay làm box đổi cỡ và lệch, vẫn là người đó
                // (nếu không sẽ sinh track mới -> khung xám + nhận diện lại -> nháy).
                val locked = track.lock != null
                val sizeRatio = fr.width() / tr.width().coerceAtLeast(1f)
                val ratioRange = if (locked) 0.5f..2.0f else 0.67f..1.5f
                if (sizeRatio !in ratioRange) continue // đổi cỡ đột ngột => không phải cùng người
                val iou = rectIou(fr, tr)
                val dist = kotlin.math.hypot((fr.centerX() - tr.centerX()).toDouble(), (fr.centerY() - tr.centerY()).toDouble()).toFloat()
                val near = dist < (if (locked) 0.8f else 0.4f) * minOf(fr.width(), tr.width())
                if (iou >= (if (locked) 0.1f else 0.25f) || near) {
                    val closeness = 1f - (dist / maxOf(fr.width(), tr.width()).coerceAtLeast(1f)).coerceIn(0f, 1f)
                    cands.add(Cand(fi, ti, iou + 0.2f * closeness))
                }
            }
        }
        cands.sortByDescending { it.score }
        val result = arrayOfNulls<FaceTrack>(faces.size)
        val usedTracks = BooleanArray(tracks.size)
        for (c in cands) {
            if (result[c.f] != null || usedTracks[c.t]) continue
            result[c.f] = tracks[c.t]
            usedTracks[c.t] = true
        }
        return faces.indices.map { fi ->
            val track = result[fi]
            if (track != null) {
                track.rect = RectF(faces[fi].rect)
                track.lastSeenMs = nowMs
                track
            } else {
                FaceTrack(RectF(faces[fi].rect), nowMs).also { tracks.add(it) }
            }
        }
    }

    private fun expireTracks(nowMs: Long) {
        tracks.removeAll {
            val gone = nowMs - it.lastSeenMs > graceOf(it)
            if (gone && it.lock != null) Log.e("FaceAttendance", "track lock cleared (absent > ${graceOf(it)}ms): ${it.lock?.name}")
            gone
        }
    }

    private fun lockLabel(t: FaceTrack): String {
        val l = t.lock ?: return "..."
        return "${l.name} ${"%.2f".format(l.sim)}" + if (t.verified) " ✓" else ""
    }

    /** Khung cho track đã có tên nhưng frame này YuNet không thấy mặt (nghiêng/che): vẽ lại vị trí cuối,
     * vẫn xanh + tên, cho tới khi hết lockPresenceGraceMs. */
    private fun coastingItems(matched: Collection<FaceTrack>): List<FaceOverlayItem> =
        tracks.filter { it.lock != null && matched.none { m -> m === it } }
            .map { FaceOverlayItem(RectF(it.rect), emptyList(), lockLabel(it), Color.GREEN) }

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
    private val landmarkSmoothingAlpha = 0.4f

    private var fpsEma: Double? = null
    private val displayTimeFmt = SimpleDateFormat("HH:mm:ss", Locale.US)

    /** Logs full details of any uncaught exception on any thread before the process dies - the
     * default handler already ultimately kills the process the same way, this just makes sure
     * the reason is captured in logcat (the actual recovery is WatchdogReceiver, which works
     * independently of this and also covers native crashes that never reach here at all). */
    private fun installCrashLogger() = CrashLog.install("MainActivity")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        installCrashLogger()

        photoPickerLauncher = registerForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
            if (uri != null) enrollFromPhoto(uri)
        }
        photoPickerMultiLauncher = registerForActivityResult(ActivityResultContracts.GetMultipleContents()) { uris: List<Uri> ->
            if (uris.isNotEmpty()) enrollFromPhotosBulk(uris) else toastStatus("Chưa chọn ảnh nào")
        }

        targetClassName = intent.getStringExtra(EXTRA_TARGET_CLASS)
        val mode = intent.getStringExtra(EXTRA_MODE)

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
        zones = FaceZones.load()
        zoneSetupMode = mode == MODE_ZONE_SETUP
        overlayView.setZones(zones, zoneSetupMode)
        when (mode) {
            MODE_ZONE_SETUP -> setupZoneEditor()
            MODE_VIEW_STUDENT -> intent.getStringExtra(EXTRA_STUDENT_NAME)?.let { showStudentSamplesDialog(it) }
            MODE_BULK_PICK -> requestBulkPick()
            MODE_ADD_SAMPLES_CAMERA -> {
                targetStudentName = intent.getStringExtra(EXTRA_STUDENT_NAME)
                toastStatus("Chụp ảnh bổ sung cho ${targetStudentName ?: "?"} — bấm nút chụp khi thấy mặt rõ")
            }
            MODE_ADD_SAMPLES_GALLERY -> {
                targetStudentName = intent.getStringExtra(EXTRA_STUDENT_NAME)
                requestBulkPick()
            }
        }
        greetingTts = GreetingTts(this)
        greetingTts.startWeatherRefresh()
        if (!zoneSetupMode) greetingTts.startKeepAlive()
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
                    // Gallery picker was blocked waiting for this; launch it now — single or
                    // multi-select depending on which button asked for it.
                    if (pendingBulkPick) {
                        pendingBulkPick = false
                        photoPickerMultiLauncher.launch("image/*")
                    } else {
                        photoPickerLauncher.launch("image/*")
                    }
                }
            }
        }
    }

    /** "Thêm hàng loạt từ ảnh có sẵn" (từ ClassManagementActivity) — mở picker chọn nhiều ảnh
     * cùng lúc; mỗi ảnh chạy qua đúng pipeline enrollFromPhoto() một, nối tiếp nhau. */
    private fun requestBulkPick() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.READ_EXTERNAL_STORAGE)
                != PackageManager.PERMISSION_GRANTED) {
            pendingBulkPick = true
            ActivityCompat.requestPermissions(this,
                arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE), CAMERA_PERMISSION_REQUEST_CODE)
        } else {
            photoPickerMultiLauncher.launch("image/*")
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
        // Re-register so onAttach() fires for every attached device. Permission was already
        // granted the very first time this camera connected (persists across app restarts),
        // so requestPermission() below just re-confirms it silently and onConnect() fires
        // immediately — no dialog, no delay.
        if (!usbMonitorRegistered) {
            usbMonitorRegistered = true
            usbMonitor.register()
        }
    }

    override fun onStop() {
        // Release the USB camera so X01 (Unity FRTest) can open it when FA goes to background.
        // The VID whitelist in looksLikeUvcCamera() ensures reconnect in onStart() is fast
        // (permission already granted → onConnect fires without a dialog).
        releaseCamera()
        usbMonitor.unregister()
        usbMonitorRegistered = false
        super.onStop()
    }

    /** Đóng camera an toàn: lấy tham chiếu rồi null ngay (onDisconnect/onStop gọi lặp hay chồng nhau
     * không đóng 2 lần), tắt callback trước, và không để ngoại lệ nào thoát ra (đóng camera lỗi trên
     * thread USB/UI = chết cả process). */
    private fun releaseCamera() {
        val cam = uvcCamera ?: return
        uvcCamera = null
        try { cam.setFrameCallback(null, 0) } catch (e: Throwable) { Log.e("FaceAttendance", "setFrameCallback(null)", e) }
        try { cam.stopPreview() } catch (e: Throwable) { Log.e("FaceAttendance", "stopPreview", e) }
        // destroy() = close() + nativeDestroy(); trước đây nativeDestroy chỉ chạy nhờ lần gọi lồng
        // onDisconnect trong close() — giờ onDisconnect no-op nên gọi thẳng, đúng 1 lần.
        try { cam.destroy() } catch (e: Throwable) { Log.e("FaceAttendance", "camera destroy", e) }
        // Chỉ thả texture SAU khi camera đã đóng hẳn (thread preview không còn ghi vào nó).
        try { previewTexture?.release() } catch (e: Throwable) { Log.e("FaceAttendance", "texture release", e) }
        previewTexture = null
    }

    private var previewTexture: SurfaceTexture? = null

    // --- USBMonitor.OnDeviceConnectListener ---

    // Đúng 1 camera USB cố định trên máy (không phải app tiêu dùng phải đoán giữa nhiều webcam
    // lạ) — quyền đã cấp lần đầu thì Android tự nhớ lâu dài, requestPermission() trên device đã
    // có quyền không hiện dialog, connect ngay. Vì vậy coi MỌI device không rõ ràng thuộc nhóm
    // khác (hub/chuột/bàn phím/USB-serial như LiDAR) là ứng viên camera, xin quyền NGAY — không
    // cần đợi 6s + dò 2 tầng như trước (gây cảm giác app "đứng" mỗi lần mở).
    private val knownSerialVids = setOf(
        0x10C4,  // Silicon Labs CP210x (LiDAR UART)
        0x1A86,  // WinChipHead CH340/CH341
        0x0403,  // FTDI FT232
        0x067B,  // Prolific PL2303
        0x0B95   // ATEN USB serial
    )
    private val excludedCameraClasses = setOf(
        android.hardware.usb.UsbConstants.USB_CLASS_AUDIO,
        android.hardware.usb.UsbConstants.USB_CLASS_COMM,
        android.hardware.usb.UsbConstants.USB_CLASS_HID,
        android.hardware.usb.UsbConstants.USB_CLASS_PRINTER,
        android.hardware.usb.UsbConstants.USB_CLASS_MASS_STORAGE,
        android.hardware.usb.UsbConstants.USB_CLASS_HUB,
        android.hardware.usb.UsbConstants.USB_CLASS_CDC_DATA
    )

    private fun looksLikeUvcCamera(device: UsbDevice): Boolean {
        if (device.vendorId in knownSerialVids) return false
        if (device.interfaceCount == 0) return true // descriptor không đọc được — vẫn thử
        return (0 until device.interfaceCount).any { i ->
            device.getInterface(i).interfaceClass !in excludedCameraClasses
        }
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
        if (!looksLikeCamera) return
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
            // PHẢI giữ tham chiếu mạnh (field): biến cục bộ bị GC thu -> finalize() huỷ BufferQueue trong khi
            // thread preview native vẫn ghi vào đó (log thật: "Surface: dequeueBuffer failed (No such device)"
            // lặp liên tục) -> đóng camera sau đó dễ SIGABRT. UnityFaceBridge đã giữ tham chiếu từ lâu.
            val dummyTexture = SurfaceTexture(false)
            previewTexture = dummyTexture
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
        // Chạy trên thread nền của USBMonitor, và camera.destroy()/close() lại gọi ngược onDisconnect
        // đồng bộ: null tham chiếu TRƯỚC để lần gọi lồng là no-op, ngoại lệ không được thoát ra
        // (uncaught trên thread này = chết cả process). Xem UnityFaceBridge.onDisconnect (cùng lỗi đã gặp).
        val cam = uvcCamera ?: return
        uvcCamera = null
        try { cam.setFrameCallback(null, 0) } catch (e: Throwable) { Log.e("FaceAttendance", "disconnect: setFrameCallback", e) }
        try { cam.destroy() } catch (e: Throwable) { Log.e("FaceAttendance", "disconnect: destroy", e) }
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

    // Buffer dùng lại giữa các frame (callback luôn chạy trên 1 thread capture) — trước đây mỗi frame
    // cấp mới ~1.3MB ByteArray + 2 Mat native, máy RAM thấp (K02) dễ bị hệ thống kill vì thiếu bộ nhớ.
    private var frameBuf: ByteArray? = null
    private var yuvMat: Mat? = null
    private var bgrRawMat: Mat? = null
    private var badFrameCount = 0

    private val frameCallback = IFrameCallback { frame: ByteBuffer ->
        // Callback này chạy trên thread NATIVE của camera: mọi ngoại lệ lọt ra ngoài = chết cả
        // process (không có chỗ nào bắt). Vì vậy bọc toàn bộ, kể cả Error (OOM, UnsatisfiedLink...).
        var bgrPreview: Mat? = null
        var handedOff = false
        var claimed = false // đã đặt isProcessing=true ở frame NÀY (không thì không được đụng vào cờ)
        try {
            if (activityReleased) return@IFrameCallback
            val now = System.nanoTime()
            val sinceLastMs = if (lastFrameArrivalNs == 0L) 0.0 else (now - lastFrameArrivalNs) / 1_000_000.0
            lastFrameArrivalNs = now
            val t0 = System.nanoTime()

            val w = frameWidth; val h = frameHeight
            val expected = w * h * 3 / 2
            val size = frame.remaining()
            if (size != expected) {
                // Khung lệch kích thước (đổi định dạng giữa chừng/frame hỏng): bỏ, không đưa vào Mat.put.
                if (badFrameCount++ % 100 == 0) Log.e("FaceAttendance", "bad frame size=$size expected=$expected (${w}x$h), dropped=$badFrameCount")
                return@IFrameCallback
            }
            // frame's backing buffer is only valid until this callback returns, so the copy has to
            // happen here - everything after this point is safe to hand off to another thread.
            val data = frameBuf?.takeIf { it.size == size } ?: ByteArray(size).also { frameBuf = it }
            frame.get(data)
            val yuv = yuvMat?.takeIf { it.rows() == h + h / 2 && it.cols() == w }
                ?: Mat(h + h / 2, w, CvType.CV_8UC1).also { yuvMat?.release(); yuvMat = it }
            yuv.put(0, 0, data)
            val bgrRaw = bgrRawMat ?: Mat().also { bgrRawMat = it }
            Imgproc.cvtColor(yuv, bgrRaw, Imgproc.COLOR_YUV2BGR_NV21)
            val flipped = Mat()
            bgrPreview = flipped
            Core.flip(bgrRaw, flipped, 1)  // 1 = horizontal mirror
            val t1 = System.nanoTime()

            // The preview always refreshes here, on every frame the camera delivers - kept on this
            // thread since it's cheap (a color convert + bitmap copy), unlike detection below.
            updatePreviewBitmap(flipped)

            // Push the last known overlay result on every frame so the bounding box moves at
            // camera FPS rather than at detection FPS. Dropped frames still get this refresh.
            val snap = cachedOverlayItems; val sw = cachedOverlayW; val sh = cachedOverlayH
            runOnUiThread { overlayView.update(snap, sw, sh) }

            if (isProcessing) {
                droppedWhileBusy++
                Log.e("FacePerf", "DROPPED frame (busy) - arrival gap=${"%.1f".format(sinceLastMs)}ms, dropped_total=$droppedWhileBusy")
                return@IFrameCallback
            }
            isProcessing = true
            claimed = true
            // flipped is handed off here - released by the detection thread once it's done with
            // it, not by this (capture) thread, since this callback must return immediately.
            try {
                detectionExecutor.execute {
                    try {
                        processFrame(flipped)
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
                    } catch (e: Throwable) {
                        // Throwable (không chỉ Exception): OOM/Error trên thread executor cũng làm chết process.
                        Log.e("FaceAttendance", "frame processing error", e)
                    } finally {
                        isProcessing = false
                        flipped.release()
                    }
                }
                handedOff = true
            } catch (e: java.util.concurrent.RejectedExecutionException) {
                // Executor đã shutdown (activity đang đóng) đúng lúc frame cuối tới.
                isProcessing = false
            }
        } catch (e: Throwable) {
            Log.e("FaceAttendance", "frameCallback error", e)
            if (claimed && !handedOff) isProcessing = false
        } finally {
            if (!handedOff) bgrPreview?.release()
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
    private fun scaleFace(face: DetectedFace, scale: Float, offsetX: Float = 0f, offsetY: Float = 0f): DetectedFace {
        val r = face.rect
        val raw = face.rawRow.copyOf()
        for (i in 0..13) raw[i] *= scale  // x, y, w, h, 5 landmark pairs — skip score at [14]
        // Dò trên vùng đã cắt: dời toạ độ (x, y của box và 5 điểm landmark; w/h là độ dài nên không dời).
        raw[0] += offsetX; raw[1] += offsetY
        for (i in 0..4) { raw[4 + i * 2] += offsetX; raw[5 + i * 2] += offsetY }
        return DetectedFace(
            rect = RectF(r.left * scale + offsetX, r.top * scale + offsetY, r.right * scale + offsetX, r.bottom * scale + offsetY),
            landmarks = face.landmarks.map { PointF(it.x * scale + offsetX, it.y * scale + offsetY) },
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
        // Chỉ dò trong vùng chữ nhật enroll (cắt ảnh trước khi dò — nhanh hơn dò cả khung).
        // Chế độ chỉnh vùng thì dò cả khung để thấy mặt ở ngoài vùng.
        val bounds = if (zoneSetupMode) RectF(0f, 0f, width.toFloat(), height.toFloat())
                     else zones.enrollBounds(width.toFloat(), height.toFloat())
        val cropX = bounds.left.toInt().coerceIn(0, width - 2)
        val cropY = bounds.top.toInt().coerceIn(0, height - 2)
        val cropW = (bounds.right.toInt() - cropX).coerceIn(2, width - cropX)
        val cropH = (bounds.bottom.toInt() - cropY).coerceIn(2, height - cropY)
        val region = Mat(bgr, Rect(cropX, cropY, cropW, cropH)) // view vào bgr, không copy
        val small = Mat()
        Imgproc.resize(region, small, org.opencv.core.Size(cropW * detectScale.toDouble(), cropH * detectScale.toDouble()))
        region.release()
        val tDetectStart = System.nanoTime()
        // Lấy dư vài mặt rồi lọc theo vùng, để mặt to ở ngoài vùng không chiếm chỗ của mặt trong vùng.
        val facesSmall = engine.detectFaces(small, MAX_FACES + 2)
        val tDetectEnd = System.nanoTime()
        small.release()
        // Scale coordinates back to full-res space so the rest of the pipeline (alignFace,
        // overlay drawing) works on the original frame dimensions.
        val faces = facesSmall.map { scaleFace(it, 1f / detectScale, cropX.toFloat(), cropY.toFloat()) }
            .filter { zoneSetupMode || zones.enrollContains(it.rect.centerX(), it.rect.centerY(), width.toFloat(), height.toFloat()) }
            .let { if (zoneSetupMode) it else it.take(MAX_FACES) }
        if (zoneSetupMode) {
            // Chế độ chỉnh vùng: chỉ hiện khung mặt để canh, không nhận diện/chào/điểm danh.
            cachedOverlayItems = faces.map { FaceOverlayItem(it.rect, emptyList(), "", Color.LTGRAY) }
            cachedOverlayW = width; cachedOverlayH = height
            return
        }
        // Gắn mặt <-> track bền vững (thay cho sắp trái->phải theo slot, vốn làm tên nhảy người).
        val faceTracks = assignTracks(faces, nowMs)
        lastDetected = faces.firstOrNull()

        // Push bounding boxes immediately after detection — before recognition runs — so the
        // overlay updates at detection FPS rather than being blocked on the recognition pipeline.
        cachedOverlayItems = faces.mapIndexed { i, face ->
            val track = faceTracks[i]
            if (track.lock != null) FaceOverlayItem(face.rect, face.landmarks.map { android.graphics.PointF(it.x, it.y) }, lockLabel(track), android.graphics.Color.GREEN)
            else FaceOverlayItem(face.rect, face.landmarks.map { android.graphics.PointF(it.x, it.y) }, "...", android.graphics.Color.GRAY)
        } + coastingItems(faceTracks)
        cachedOverlayW = width; cachedOverlayH = height

        if (faces.isEmpty()) {
            lastDetected = null
            // Tracks (kèm lock) hết hạn khi không thấy mặt quanh vị trí đó liên tục lockPresenceGraceMs.
            expireTracks(nowMs)
            val coasting = coastingItems(emptyList())
            cachedOverlayItems = coasting; cachedOverlayW = width; cachedOverlayH = height
            if (coasting.isEmpty()) {
                // Idle 1s before next detection attempt (chỉ khi không còn track nào đang được giữ).
                nextDetectAllowedMs = nowMs + 1000
                Log.e("FacePerf", "  detect=${"%.1f".format((tDetectEnd - tDetectStart) / 1_000_000.0)}ms (no face — idle 1s)")
            }
            // Bug đã xác nhận: nhánh này trước đây KHÔNG cập nhật tvStatus, nên "faces in view"
            // bị đứng ở giá trị lần cuối có người (thường là 1) mãi mãi dù người đã rời khung
            // hình từ lâu — chỗ set text duy nhất nằm ở cuối processFrame(), không bao giờ chạy
            // tới khi faces rỗng vì hàm return sớm ở đây.
            runOnUiThread { tvStatus.text = "Enrolled: ${attendanceStore.enrolledCount()} | faces in view: 0" }
            return
        }

        // Handle enroll snapshot: capture embeddings for all unrecognized faces, then show
        // sequential name dialogs. Done before the display loop so we can return early.
        if (enrollSnapshotRequested) {
            enrollSnapshotRequested = false
            val toEnroll = mutableListOf<Pair<FloatArray, Mat>>()
            for ((i, face) in faces.withIndex()) {
                if (faceTracks[i].lock == null) {
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
        for ((slot, face) in faces.withIndex()) {
            val track = faceTracks[slot]
            var color: Int
            val label: String

            val lock = track.lock
            if (lock != null) {
                // Đã có tên: giữ khung xanh + tên + confidence theo vị trí. Chưa verified thì chạy verify
                // (vài lần khớp lại khi mặt thẳng, không ảnh hưởng hiển thị); xong là ✓ và không nhận diện nữa.
                color = Color.GREEN
                if (!track.verified) verifyLock(engine, bgr, face, track, lock)
                if (!greetedActive.containsKey(lock.name)) {
                    justLogged.add(lock.name to attendanceStore.genderOf(lock.name))
                }
                greetedActive[lock.name] = nowMs
                if (attendanceStore.canLog(lock.name, cooldownMs)) {
                    val crop = cropFace(bgr, face.rect)
                    attendanceStore.logAttendance(lock.name, crop, lock.sim)
                    crop.release()
                    runOnUiThread { refreshRecentPeoplePanel() }
                }
                label = lockLabel(track)
            } else {
                // Unknown track: run full recognition.
                val pose = engine.estimatePose(face)
                val goodPose = pose.isFrontal(yawTolerance, rollToleranceDeg)
                if (slot == 0) lastGoodPose = goodPose

                val tAlignStart = System.nanoTime()
                val embedding = run {
                    val aligned = engine.alignFace(bgr, face)
                    val e = engine.getEmbedding(aligned)
                    aligned.release()
                    e
                }
                val tAlignEnd = System.nanoTime()
                val tEmbedEnd = tAlignEnd

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
                    confirmed && tracks.any { it !== track && it.lock?.name == name } -> {
                        // Cùng 1 tên không thể ở 2 chỗ: track khác đã giữ tên này => mặt này không được lock.
                        color = Color.rgb(255, 165, 0)
                        label = "$name đã có người khác ${"%.2f".format(sim)}"
                    }
                    confirmed -> {
                        // Single-frame confirmation: set lock immediately, no voting needed.
                        track.lock = ResolvedLock(name!!, nowMs, sim)
                        track.unknownCount = 0
                        color = Color.GREEN
                        if (!greetedActive.containsKey(name)) {
                            justLogged.add(name to attendanceStore.genderOf(name))
                        }
                        greetedActive[name] = nowMs
                        if (attendanceStore.canLog(name, cooldownMs)) {
                            val crop = cropFace(bgr, face.rect)
                            attendanceStore.logAttendance(name, crop, sim)
                            crop.release()
                            runOnUiThread { refreshRecentPeoplePanel() }
                        }
                        label = lockLabel(track)
                    }
                    isAmbiguous -> {
                        color = Color.MAGENTA
                        label = "AMBIGUOUS ${"%.2f".format(sim)}/${"%.2f".format(runnerUpSim)} best=$name"
                    }
                    else -> {
                        // Như người quen: cần vài lần nhận diện liên tiếp không ra ai mới chắc là mặt mới.
                        track.unknownCount++
                        if (track.unknownCount >= unknownFramesToAnnounce) {
                            color = Color.RED
                            label = "Unknown ${"%.2f".format(sim)} best=$name"
                            newFaceLastSeenMs = nowMs
                            if (!newFaceAnnouncedThisAppearance) {
                                newFaceAnnouncedThisAppearance = true
                                announceNewFace()
                            }
                        } else {
                            color = Color.GRAY
                            label = "... ${track.unknownCount}/$unknownFramesToAnnounce"
                        }
                    }
                }
            }
            } // end else (full recognition)

            val rect = RectF(face.rect)
            val rawPts = face.landmarks.map { PointF(it.x, it.y) }
            val prev = track.smoothed
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
            track.smoothed = smoothed
            overlayItems.add(FaceOverlayItem(rect, smoothed.toList(), label, color))
        }

        overlayItems.addAll(coastingItems(faceTracks))
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

    /** Verify nền cho track đã có tên: mặt thẳng thì tính lại embedding, khớp đúng tên [verifyFramesNeeded] lần
     * => verified (hiện ✓, không nhận diện nữa). Mặt nghiêng/điểm thấp: bỏ qua, KHÔNG làm mất khung xanh.
     * Chỉ khi 2 lần liên tiếp ra NGƯỜI KHÁC chắc chắn mới bỏ lock (lock ban đầu sai) để nhận diện lại. */
    private fun verifyLock(engine: FaceEngine, bgr: Mat, face: DetectedFace, track: FaceTrack, lock: ResolvedLock) {
        if (!engine.estimatePose(face).isFrontal(yawTolerance, rollToleranceDeg)) return
        track.verifyTries++
        val aligned = engine.alignFace(bgr, face)
        val emb = engine.getEmbedding(aligned)
        aligned.release()
        val m = attendanceStore.bestMatch(emb, engine)
        val sameName = m.name == lock.name && m.sim > matchThreshold
        val otherPerson = m.name != null && m.name != lock.name && m.sim > matchThreshold &&
            (m.sim - m.runnerUpSim) >= marginThreshold
        Log.e("FaceMatch", "verify ${lock.name}: best=${m.name} sim=${"%.3f".format(m.sim)} ok=${track.verifyOk} bad=${track.verifyBad} tries=${track.verifyTries}")
        when {
            sameName -> { track.verifyOk++; track.verifyBad = 0 }
            otherPerson -> track.verifyBad++
        }
        if (track.verifyBad >= 2) {
            Log.e("FaceAttendance", "verify mismatch (${lock.name} vs ${m.name}) — lock dropped, recognize again")
            greetedActive.remove(lock.name)
            track.lock = null; track.verifyOk = 0; track.verifyBad = 0; track.verifyTries = 0
        } else if (track.verifyOk >= verifyFramesNeeded || track.verifyTries >= verifyMaxTries) {
            track.verified = true
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

    // Bug đã xác nhận (giơ tay 1 lát sau mới thấy hình): mỗi frame trước đây tự alloc 1 Bitmap
    // MỚI (Bitmap.createBitmap) rồi post qua runOnUiThread KHÔNG có cơ chế chặn dồn — nếu main
    // thread xử lý chậm hơn 1 nhịp, các Runnable setImageBitmap() xếp hàng lại và hiển thị chậm
    // dần theo, đúng cảm giác "trễ". Sửa 2 chỗ: (1) tái dùng 1 Bitmap cố định qua
    // Utils.matToBitmap(mat, bitmap) — bản OpenCV ghi thẳng vào bitmap có sẵn, không alloc mới
    // mỗi frame (đỡ áp lực GC); (2) previewUpdatePending chặn dồn — frame mới tới khi main
    // thread còn đang xử lý frame trước thì bỏ qua luôn (ưu tiên hình mới nhất, không xếp hàng
    // hình cũ) thay vì hiển thị "đuổi kịp" một loạt frame trễ.
    // 2 bitmap cố định xoay vòng (không phải 1) — viết vào buffer ĐANG KHÔNG hiển thị, main
    // thread luôn đọc buffer ĐÃ VIẾT XONG hoàn chỉnh, không có chuyện vừa đọc vừa ghi cùng lúc
    // trên đúng 1 bitmap (dễ rách hình nếu main thread render đúng lúc frame kế tiếp đang ghi đè).
    private val previewBitmaps = arrayOfNulls<Bitmap>(2)
    private var previewBitmapIndex = 0
    @Volatile private var previewUpdatePending = false

    private fun updatePreviewBitmap(bgr: Mat) {
        if (previewUpdatePending) return // main thread chưa tiêu thụ xong frame trước — bỏ frame này
        val t0 = System.nanoTime()
        val idx = previewBitmapIndex
        var bmp = previewBitmaps[idx]
        if (bmp == null || bmp.width != bgr.cols() || bmp.height != bgr.rows()) {
            bmp = Bitmap.createBitmap(bgr.cols(), bgr.rows(), Bitmap.Config.ARGB_8888)
            previewBitmaps[idx] = bmp
        }
        Utils.matToBitmap(bgr, bmp)
        previewBitmapIndex = 1 - idx
        val t1 = System.nanoTime()
        previewUpdatePending = true
        runOnUiThread {
            previewImage.setImageBitmap(bmp)
            previewUpdatePending = false
        }
        Log.e("FacePerf", "  updatePreviewBitmap (convert)=${"%.1f".format((t1 - t0) / 1_000_000.0)}ms")
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
    private fun enrollFromPhoto(uri: Uri, onDone: () -> Unit = {}) {
        toastStatus("Đang xử lý ảnh...")
        detectionExecutor.execute {
            val engine = faceEngine
            if (engine == null) {
                runOnUiThread { toastStatus("ENROLL TỪ ẢNH: FaceEngine chưa sẵn sàng"); onDone() }
                return@execute
            }
            try {
                val raw = contentResolver.openInputStream(uri)?.use { stream ->
                    android.graphics.BitmapFactory.decodeStream(stream)
                }
                if (raw == null) {
                    runOnUiThread { toastStatus("ENROLL TỪ ẢNH: không đọc được ảnh"); onDone() }
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
                    runOnUiThread { toastStatus("ENROLL TỪ ẢNH: không phát hiện khuôn mặt nào"); onDone() }
                } else {
                    val faceData = faces.map { face ->
                        val aligned = engine.alignFace(bgr, face)
                        val emb = engine.getEmbedding(aligned)
                        aligned.release()
                        emb to cropFace(bgr, face.rect)
                    }
                    bgr.release()
                    runOnUiThread { enrollNextFace(faceData, 0, onDone) }
                }
            } catch (e: Exception) {
                Log.e("FaceAttendance", "enrollFromPhoto error", e)
                runOnUiThread { toastStatus("ENROLL TỪ ẢNH: lỗi — ${e.message}"); onDone() }
            }
        }
    }

    /** "Thêm hàng loạt từ ảnh có sẵn" — chạy từng ảnh đã chọn qua đúng pipeline enrollFromPhoto()
     * một, nối tiếp nhau (không xử lý song song, để 2 dialog đặt tên không đè lên nhau khi
     * nhiều ảnh cùng có mặt). */
    private fun enrollFromPhotosBulk(uris: List<Uri>, index: Int = 0) {
        if (index >= uris.size) {
            toastStatus("Đã xử lý xong ${uris.size} ảnh")
            return
        }
        enrollFromPhoto(uris[index]) { enrollFromPhotosBulk(uris, index + 1) }
    }

    /** Sequential multi-person enrollment: shows a dialog for each unrecognized face in order.
     *  Called with index=0 from processFrame's snapshot handler and recurses until done.
     *  onAllDone fires once every face in this batch has been named or skipped — used by the
     *  bulk-from-gallery flow to chain on to the next picked photo. */
    private fun enrollNextFace(faces: List<Pair<FloatArray, Mat>>, index: Int, onAllDone: () -> Unit = {}) {
        if (index >= faces.size) {
            onAllDone()
            return
        }
        val target = targetStudentName
        if (target != null) {
            // Cập nhật ảnh cho học sinh ĐÃ CÓ (từ panel chi tiết) — đã biết tên/tên gọi/giới
            // tính rồi, add thẳng mẫu vào đúng tên đó, không hỏi lại showEnrollNameDialog().
            val (emb, crop) = faces[index]
            if (attendanceStore.addSample(target, emb, crop)) {
                toastStatus("Đã thêm ảnh cho $target (${attendanceStore.samplesOf(target).size} mẫu)")
            } else {
                toastStatus("$target đã đủ ${AttendanceStore.MAX_SAMPLES_PER_PERSON} ảnh — xóa bớt ở panel học sinh trước khi thêm")
            }
            enrollNextFace(faces, index + 1, onAllDone)
            return
        }
        showEnrollNameDialog(
            samples = listOf(faces[index]),
            personIndex = index,
            totalPersons = faces.size,
            onComplete = { enrollNextFace(faces, index + 1, onAllDone) }
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
            hint = "Tên thật..."
        }
        rightCol.addView(input)

        // Nickname ("tên thường gọi") - mầm non students mostly go by a home name. Auto-fills
        // from the real name's last 2 syllables as the teacher types it, but only until the
        // teacher edits this field directly - after that their own text always wins, even if
        // they then go back and change the real name (aliasManuallyEdited latch below).
        val aliasInput = EditText(this).apply {
            hint = "Tên thường gọi..."
            // Dismiss keyboard when the user taps Done/Enter
            imeOptions = android.view.inputmethod.EditorInfo.IME_ACTION_DONE
            setOnEditorActionListener { _, _, _ -> clearFocus(); false }
        }
        var aliasManuallyEdited = false
        var lastAutoAlias = ""
        input.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                if (aliasManuallyEdited) return
                lastAutoAlias = attendanceStore.defaultAliasFor(s?.toString() ?: "")
                aliasInput.setText(lastAutoAlias)
                aliasInput.setSelection(aliasInput.text.length)
            }
        })
        aliasInput.addTextChangedListener(object : android.text.TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: android.text.Editable?) {
                if (s?.toString() != lastAutoAlias) aliasManuallyEdited = true
            }
        })
        rightCol.addView(aliasInput)

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
                    val alias = aliasInput.text.toString().trim()
                    attendanceStore.setAlias(name, alias.ifEmpty { attendanceStore.defaultAliasFor(name) })
                    targetClassName?.let { attendanceStore.setClassName(name, it) }
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

    // ---- Chỉnh vùng nhận diện (MODE_ZONE_SETUP) ----
    private var editZones: FaceZones = FaceZones.DEFAULT
    private var zoneSel = 0 // 0 = trái (game), 1 = phải (game), 2 = chữ nhật enroll
    private val zoneRowLabels = ArrayList<TextView>()
    private val zoneRowValues = ArrayList<TextView>()
    private val zoneRowViews = ArrayList<LinearLayout>()
    private val zoneTabButtons = ArrayList<Button>()
    private val zoneRepeatHandler = Handler(Looper.getMainLooper())

    private fun dpi(v: Int) = (v * resources.displayMetrics.density).toInt()

    private fun selectedZone(z: FaceZones): ZoneRect = when (zoneSel) { 0 -> z.playLeft; 1 -> z.playRight; else -> z.enroll }

    private fun zoneParams(): List<Pair<String, Float>> {
        val r = selectedZone(editZones)
        return listOf("X (trái → phải)" to r.x, "Y (trên → dưới)" to r.y, "Rộng" to r.w, "Cao" to r.h)
    }

    private fun round2(v: Float) = Math.round(v * 100f) / 100f

    /** Đổi tham số thứ [param] của vùng đang chọn thêm [delta] (đơn vị 0..1), kẹp trong khung. */
    private fun adjustZone(param: Int, delta: Float) {
        val z = editZones
        val r = selectedZone(z)
        var x = r.x; var y = r.y; var w = r.w; var h = r.h
        when (param) { 0 -> x += delta; 1 -> y += delta; 2 -> w += delta; 3 -> h += delta }
        w = round2(w.coerceIn(0.05f, 1f)); h = round2(h.coerceIn(0.05f, 1f))
        x = round2(x.coerceIn(0f, 1f - w)); y = round2(y.coerceIn(0f, 1f - h))
        val nr = ZoneRect(x, y, w, h)
        editZones = when (zoneSel) { 0 -> z.copy(playLeft = nr); 1 -> z.copy(playRight = nr); else -> z.copy(enroll = nr) }
        refreshZoneEditor()
    }

    private fun refreshZoneEditor() {
        val params = zoneParams()
        for (i in zoneRowViews.indices) {
            if (i < params.size) {
                zoneRowViews[i].visibility = android.view.View.VISIBLE
                zoneRowLabels[i].text = params[i].first
                zoneRowValues[i].text = "${Math.round(params[i].second * 100)}%"
            } else {
                zoneRowViews[i].visibility = android.view.View.GONE
            }
        }
        for ((i, b) in zoneTabButtons.withIndex()) {
            b.backgroundTintList = android.content.res.ColorStateList.valueOf(if (i == zoneSel) Color.parseColor("#2196F3") else Color.parseColor("#616161"))
        }
        overlayView.setZones(editZones, true, zoneSel)
    }

    /** Nút +/- bấm 1 lần = 1 bước, giữ = tự lặp nhanh dần. */
    private fun stepButton(text: String, onStep: () -> Unit): Button = Button(this).apply {
        this.text = text
        textSize = 18f
        minWidth = dpi(48); minimumWidth = dpi(48)
        val repeat = object : Runnable {
            var n = 0
            override fun run() { onStep(); n++; zoneRepeatHandler.postDelayed(this, if (n > 10) 40L else 90L) }
        }
        setOnTouchListener { v, ev ->
            when (ev.actionMasked) {
                android.view.MotionEvent.ACTION_DOWN -> {
                    v.isPressed = true
                    onStep(); repeat.n = 0
                    zoneRepeatHandler.postDelayed(repeat, 400L)
                }
                android.view.MotionEvent.ACTION_UP, android.view.MotionEvent.ACTION_CANCEL -> {
                    v.isPressed = false
                    zoneRepeatHandler.removeCallbacks(repeat)
                }
            }
            true
        }
    }

    private fun setupZoneEditor() {
        editZones = FaceZones.load()
        // Ẩn mọi thứ của màn nhận diện/enroll — chỉ còn hình camera + khung vùng + bảng chỉnh.
        for (v in listOf<android.view.View>(btnEnroll.parent as android.view.View, btnManage.parent as android.view.View,
            recentPeoplePanel, tvGreeting, tvInfoPanel, tvFps)) v.visibility = android.view.View.GONE

        val panel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setBackgroundColor(Color.parseColor("#D9000000"))
            setPadding(dpi(12), dpi(10), dpi(12), dpi(10))
        }
        panel.addView(TextView(this).apply {
            text = "VÙNG NHẬN DIỆN"; setTextColor(Color.WHITE); textSize = 16f; typeface = android.graphics.Typeface.DEFAULT_BOLD
        })
        val tabs = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        for ((i, name) in listOf("TRÁI", "PHẢI", "ENROLL").withIndex()) {
            val b = Button(this).apply {
                text = name; textSize = 12f; setTextColor(Color.WHITE)
                setOnClickListener { zoneSel = i; refreshZoneEditor() }
            }
            tabs.addView(b, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
            zoneTabButtons.add(b)
        }
        panel.addView(tabs)
        for (p in 0 until 4) {
            val row = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL }
            val label = TextView(this).apply { setTextColor(Color.parseColor("#DDDDDD")); textSize = 13f }
            val value = TextView(this).apply {
                setTextColor(Color.WHITE); textSize = 15f; gravity = Gravity.CENTER; typeface = android.graphics.Typeface.DEFAULT_BOLD
            }
            row.addView(label, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
            row.addView(stepButton("−") { adjustZone(p, -0.01f) })
            row.addView(value, LinearLayout.LayoutParams(dpi(52), LinearLayout.LayoutParams.WRAP_CONTENT))
            row.addView(stepButton("+") { adjustZone(p, 0.01f) })
            panel.addView(row)
            zoneRowViews.add(row); zoneRowLabels.add(label); zoneRowValues.add(value)
        }
        val actions = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        actions.addView(Button(this).apply {
            text = "MẶC ĐỊNH"; textSize = 12f
            setOnClickListener { editZones = FaceZones.DEFAULT; refreshZoneEditor() }
        }, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        actions.addView(Button(this).apply {
            text = "LƯU"; textSize = 12f; setTextColor(Color.WHITE)
            backgroundTintList = android.content.res.ColorStateList.valueOf(Color.parseColor("#4CAF50"))
            setOnClickListener {
                toastStatus(if (editZones.save()) "Đã lưu vùng nhận diện — game áp dụng từ vòng chơi sau" else "LƯU THẤT BẠI (kiểm tra quyền bộ nhớ)")
            }
        }, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        panel.addView(actions)

        (findViewById<android.view.ViewGroup>(android.R.id.content).getChildAt(0) as android.widget.FrameLayout).addView(
            panel,
            android.widget.FrameLayout.LayoutParams(dpi(300), android.widget.FrameLayout.LayoutParams.WRAP_CONTENT, Gravity.END or Gravity.CENTER_VERTICAL).apply {
                rightMargin = dpi(12)
            }
        )
        tvStatus.text = "Chỉnh vùng: chọn TRÁI/PHẢI/ENROLL, bấm − + rồi LƯU. Nút X để thoát."
        refreshZoneEditor()
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

    private fun showStudentSamplesDialog(nameArg: String) {
        var name = nameArg
        val container = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(16, 16, 16, 16) }
        val grid = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        val scroll = HorizontalScrollView(this).apply { addView(grid) }
        val headerRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL }
        val headerText = TextView(this).apply {
            textSize = 14f
            layoutParams = LinearLayout.LayoutParams(0, android.view.ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        }
        fun refreshHeaderText() {
            val alias = attendanceStore.aliasOf(name)
            val realLine = if (alias != name) "\nTên thật: $name" else ""
            headerText.text = "$alias$realLine\n${attendanceStore.samplesOf(name).size} mẫu"
        }
        refreshHeaderText()
        headerRow.addView(headerText)
        headerRow.addView(TextView(this).apply {
            text = "Đổi tên gọi"
            setPadding(16, 8, 8, 8)
            setOnClickListener {
                val input = EditText(this@MainActivity).apply {
                    val alias = attendanceStore.aliasOf(name)
                    setText(alias); setSelection(alias.length)
                }
                AlertDialog.Builder(this@MainActivity)
                    .setTitle("Đổi tên thường gọi")
                    .setView(input)
                    .setPositiveButton("Lưu") { _, _ ->
                        attendanceStore.setAlias(name, input.text.toString())
                        refreshHeaderText()
                    }
                    .setNegativeButton("Hủy", null)
                    .show()
            }
        })
        headerRow.addView(TextView(this).apply {
            text = "Đổi tên thật"
            setPadding(16, 8, 8, 8)
            setOnClickListener {
                val input = EditText(this@MainActivity).apply { setText(name); setSelection(name.length) }
                AlertDialog.Builder(this@MainActivity)
                    .setTitle("Đổi tên thật học sinh")
                    .setView(input)
                    .setPositiveButton("Lưu") { _, _ ->
                        val newName = input.text.toString().trim()
                        if (attendanceStore.renameEnrollment(name, newName)) {
                            name = newName
                            refreshHeaderText()
                            toastStatus("Đã đổi tên thành $name")
                        } else if (newName.isNotEmpty()) {
                            toastStatus("Không đổi được tên — trùng với người khác?")
                        }
                    }
                    .setNegativeButton("Hủy", null)
                    .show()
            }
        })
        container.addView(headerRow)
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
        // Thứ tự quan trọng: cờ released (callback bỏ frame mới) -> đóng camera (hết frame mới) ->
        // chờ frame đang xử lý xong -> mới giải phóng engine/buffer. Trước đây shutdownNow() không chờ,
        // và frame cuối còn có thể execute() vào executor đã đóng.
        activityReleased = true
        super.onDestroy()
        healthLogHandler.removeCallbacksAndMessages(null)
        infoPanelHandler.removeCallbacksAndMessages(null)
        zoneRepeatHandler.removeCallbacksAndMessages(null)
        releaseCamera()
        usbMonitorRegistered = false
        try { usbMonitor.unregister() } catch (e: Throwable) { Log.e("FaceAttendance", "usb unregister", e) }
        try { usbMonitor.destroy() } catch (e: Throwable) { Log.e("FaceAttendance", "usb destroy", e) }
        detectionExecutor.shutdown()
        try {
            if (!detectionExecutor.awaitTermination(2, java.util.concurrent.TimeUnit.SECONDS)) {
                Log.w("FaceAttendance", "detectionExecutor chưa dừng sau 2s — frame đang chạy native, bỏ chờ")
                detectionExecutor.shutdownNow()
            }
        } catch (e: InterruptedException) {
            Thread.currentThread().interrupt()
        }
        yuvMat?.release(); yuvMat = null
        bgrRawMat?.release(); bgrRawMat = null
        faceEngine?.close()
        greetingTts.release()
        try { unregisterReceiver(debugTtsReceiver) } catch (e: IllegalArgumentException) { /* chưa đăng ký */ }
    }
}
