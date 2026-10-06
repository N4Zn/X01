package com.faceattendance.app

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import org.opencv.android.Utils
import org.opencv.core.Mat
import java.io.File
import java.io.FileOutputStream
import java.io.FileWriter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** One enrolled reference sample: an embedding plus the face-crop photo it came from
 * (photo is null for test-gallery entries, which have no real photo). */
data class Sample(val embedding: FloatArray, val photo: String?)

data class MatchResult(val name: String?, val sim: Float, val runnerUpName: String?, val runnerUpSim: Float)

data class PersonLogEntry(var first: Long, var last: Long, var count: Int, var snapshot: Bitmap?)

/** Holds enrolled face sample clusters, per-person cooldown state, aggregated recent-checkin
 * log and the attendance CSV log. */
class AttendanceStore(private val context: Context) {

    companion object {
        // A single averaged centroid per person doesn't help separate near-identical faces
        // (e.g. twins) - a small cluster of real samples does, at least somewhat. The first
        // PERMANENT_SAMPLES collected are kept forever; beyond that only the ROLLING_SAMPLES
        // most recent are kept. 2026-10-05: ROLLING = 0 (trước 5) — ảnh thêm sau có thể sai ánh sáng/nhầm
        // người; trần = 3 mẫu lấy lúc enroll, quá trần thì addSample từ chối (xem addSample).
        const val PERMANENT_SAMPLES = 3
        const val ROLLING_SAMPLES = 0
        const val MAX_SAMPLES_PER_PERSON = PERMANENT_SAMPLES + ROLLING_SAMPLES
        const val RECENT_PEOPLE_SHOWN = 5

        /** Lớp học sinh test (các bạn đã enroll từ trước khi có danh sách lớp thật). */
        const val DEV_CLASS = "Dev"
        const val CLASS_5_TUOI = "5 tuổi"
        /** Danh sách 5 tuổi ban đầu (tên thật, giới tính) — chỉ dùng để khởi tạo 1 lần ở migrateLegacyRosterIfNeeded(). */
        val DEFAULT_5_TUOI = listOf(
            "Ngô Quốc An" to "nam", "Nguyễn Đăng Bách" to "nam", "Nguyễn Ngọc Bảo Châu" to "nam",
            "Nguyễn Phương Linh" to "nu", "Nguyễn Hà Linh Phương" to "nu", "Phạm Minh Ngọc" to "nam",
            "Đào Khánh Ngọc" to "nu", "Trần Bảo Vy" to "nu", "Trương Thảo Vy" to "nu",
            "Nguyễn Anh Khôi" to "nam", "Doãn Minh Trí" to "nam", "Đinh Nguyễn Cát Tường" to "nam",
            "Tạ Ngọc Khuê" to "nu", "Vũ Đình Khánh" to "nam", "Nguyễn Hòa Vũ" to "nam", "Đặng Tâm Như" to "nu"
        )
    }

    private val enrolled = LinkedHashMap<String, MutableList<Sample>>()
    // "nam" or "nu" - picks the honorific ("anh"/"chi") used by the attendance greeting TTS.
    private val genders = HashMap<String, String>()
    // Which "lớp" (class) each enrolled person belongs to - null until assigned via the
    // class-management UI. Independent of enrolled.json's Sample/embedding data, so adding
    // this never touches the recognition path (FaceDatabase/bestMatch only reads name+samples).
    private val classNames = HashMap<String, String>()
    // Nickname ("tên thường gọi") shown in the class roster UI - mầm non students mostly go by
    // a home name, not their full legal name. Independent of the enrollment key (which stays the
    // real/legal name everywhere else - recognition, CSV log, file paths) so nothing downstream
    // needs to change; defaults to the real name's last 2 syllables (see defaultAliasFor) but the
    // teacher can override it freely.
    private val aliasNames = HashMap<String, String>()
    // Ngày sinh, dạng ISO "yyyy-MM-dd" - null cho tới khi giáo viên nhập qua panel "Quản lý lớp".
    // Tiền đề để chuẩn hoá năng lực mầm non theo THÁNG tuổi (xem ageInMonthsOf()) thay vì theo
    // năm - 3 tuổi 1 tháng và 3 tuổi 11 tháng không nên bị coi là cùng 1 nhóm.
    private val birthdates = HashMap<String, String>()
    // Known class names, including ones with zero students yet (created via "Thêm lớp mới") -
    // a plain list separate from classNames' values so an empty class still shows up.
    private val knownClasses = LinkedHashSet<String>()
    // Names that came from importTestGallery() rather than a real enroll() - excluded from
    // persistence so the fake 200-person test gallery never leaks into the production save file.
    private val testGalleryNames = HashSet<String>()
    private val lastLogged = HashMap<String, Long>()
    // Shared path: both FaceAttendance and EduXplore game read/write this file.
    private val sharedDir = File("/sdcard/EduXplore").also { it.mkdirs() }
    // Nhật ký điểm danh + ảnh snapshot cũng nằm ở /sdcard/EduXplore để sống qua lần gỡ/cài lại app
    // (getExternalFilesDir bị Android xoá cùng app). File cũ được chép sang lúc mở app (migrateAppFilesToSharedDir).
    private val logFile = File(sharedDir, "attendance_log.csv")
    private val enrolledFile = File(sharedDir, "enrolled.json")
    private val classesFile = File(sharedDir, "classes.json")
    // Ảnh mẫu nằm CÙNG phân vùng với enrolled.json (/sdcard/EduXplore/enrolled_photos/<tên>/<tên>_<thời gian>.jpg)
    // để backup 1 thư mục /sdcard/EduXplore là đủ. Ảnh cũ ở getExternalFilesDir được chuyển sang
    // đây lúc mở app (migratePhotosToSharedDir).
    private val enrolledPhotosDir = File(sharedDir, "enrolled_photos")
    private val snapshotDir = File(sharedDir, "snapshots")
    private val timeFmt = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US)
    private val displayTimeFmt = SimpleDateFormat("HH:mm:ss", Locale.US)

    // Per-person aggregate (first/last check-in time, count, latest thumbnail) instead of a
    // flat event list - with a 10s cooldown a busy person would otherwise add a new line every
    // 10s forever. recentNames tracks display order, most-recent last, capped at RECENT_PEOPLE_SHOWN.
    val personLog = LinkedHashMap<String, PersonLogEntry>()
    val recentNames = mutableListOf<String>()

    // roster.json — xem khối chú thích 'roster.json' bên dưới. Phải khai báo TRƯỚC init {}.
    private val rosterFile = File(sharedDir, "roster.json")
    private val migrationMarker = File(sharedDir, ".roster_migration_v1")
    private val rosterPrefs = context.getSharedPreferences("roster_sync", Context.MODE_PRIVATE)

    /** Lỗi nạp roster.json lần mở gần nhất (null = ổn hoặc không có file). */
    var rosterImportError: String? = null
        private set
    /** Số học sinh MỚI được thêm từ roster.json lần mở gần nhất. */
    var rosterAddedCount = 0
        private set

    init {
        enrolledPhotosDir.mkdirs()
        snapshotDir.mkdirs()
        loadPersisted()
        migratePhotosToSharedDir()
        migrateAppFilesToSharedDir()
        migrateLegacyRosterIfNeeded()
        importRosterIfChanged()
    }

    private fun loadPersisted() {
        // classes.json đọc TRƯỚC: thứ tự lớp (và lớp rỗng) theo file này; enrolled.json chỉ bổ sung lớp còn thiếu.
        if (classesFile.exists()) {
            try {
                val arr = org.json.JSONArray(classesFile.readText())
                for (i in 0 until arr.length()) knownClasses.add(arr.getString(i))
            } catch (e: Exception) {
                android.util.Log.e("FaceAttendance", "Failed to load classes.json", e)
            }
        }
        if (!enrolledFile.exists()) return
        try {
            val arr = org.json.JSONArray(enrolledFile.readText())
            for (i in 0 until arr.length()) {
                val obj = arr.getJSONObject(i)
                val name = obj.getString("name")
                val samples = mutableListOf<Sample>()
                if (obj.has("samples")) {
                    val samplesArr = obj.getJSONArray("samples")
                    for (j in 0 until samplesArr.length()) {
                        val sObj = samplesArr.getJSONObject(j)
                        val embArr = sObj.getJSONArray("embedding")
                        val emb = FloatArray(embArr.length()) { embArr.getDouble(it).toFloat() }
                        val photo = if (sObj.isNull("photo")) null else sObj.getString("photo")
                        samples.add(Sample(emb, photo))
                    }
                } else {
                    // backward-compat with the earlier single-embedding-per-person format
                    val embArr = obj.getJSONArray("embedding")
                    val emb = FloatArray(embArr.length()) { embArr.getDouble(it).toFloat() }
                    samples.add(Sample(emb, null))
                }
                enrolled[name] = samples
                genders[name] = if (obj.has("gender")) obj.getString("gender") else "nam"
                if (obj.has("className") && !obj.isNull("className")) {
                    val cls = obj.getString("className")
                    classNames[name] = cls
                    knownClasses.add(cls)
                }
                if (obj.has("alias") && !obj.isNull("alias")) {
                    aliasNames[name] = obj.getString("alias")
                }
                if (obj.has("birthdate") && !obj.isNull("birthdate")) {
                    birthdates[name] = obj.getString("birthdate")
                }
            }
            val totalSamples = enrolled.values.sumOf { it.size }
            android.util.Log.e("FaceAttendance", "Loaded ${enrolled.size} persisted enrollments ($totalSamples samples)")
        } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "Failed to load persisted enrollments", e)
        }
    }

    private fun persist() {
        val arr = org.json.JSONArray()
        for ((name, samples) in enrolled) {
            if (name in testGalleryNames) continue
            val obj = org.json.JSONObject()
            obj.put("name", name)
            obj.put("gender", genders[name] ?: "nam")
            obj.put("className", classNames[name])
            obj.put("alias", aliasNames[name])
            obj.put("birthdate", birthdates[name])
            val samplesArr = org.json.JSONArray()
            for (s in samples) {
                val sObj = org.json.JSONObject()
                val embArr = org.json.JSONArray()
                for (v in s.embedding) embArr.put(v.toDouble())
                sObj.put("embedding", embArr)
                sObj.put("photo", s.photo)
                samplesArr.put(sObj)
            }
            obj.put("samples", samplesArr)
            arr.put(obj)
        }
        val json = arr.toString()
        try {
            enrolledFile.parentFile?.mkdirs()
            enrolledFile.writeText(json)
        } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "persist failed: ${e.message}", e)
        }
        writeRoster()
        // Game merged into the same APK/package (Track A) — FaceDatabase.load() in the game's
        // runtime already checks context.getExternalFilesDir(null)/enrolled.json first, which
        // is the SAME directory this class also writes to via context.getExternalFilesDir(null)
        // — same UID, no cross-app mirroring needed anymore (used to write into 3 separate
        // hardcoded game package dirs back when FA/Game were separate APKs).
    }

    private fun persistClasses() {
        val arr = org.json.JSONArray()
        for (c in knownClasses) arr.put(c)
        try {
            classesFile.parentFile?.mkdirs()
            classesFile.writeText(arr.toString())
        } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "persistClasses failed: ${e.message}", e)
        }
        writeRoster()
    }

    // ──────────────────────────  roster.json (sửa/xem danh sách lớp bằng tay)  ──────────────────────────
    //
    // /sdcard/EduXplore/roster.json = bản DỄ ĐỌC/DỄ SỬA của danh sách lớp + học sinh (không có embedding/ảnh,
    // khác enrolled.json 45KB+). Định dạng:
    //   {"version":1,"classes":[{"name":"5 tuổi","students":[
    //       {"name":"Ngô Quốc An","alias":"Quốc An","gender":"nam","birthdate":"2020-05-01"}, ...]}]}
    // • App GHI file này sau MỖI thay đổi (persist) -> `adb pull` ra xem/sửa.
    // • App NẠP file này mỗi lần mở AttendanceStore NẾU file khác lần app ghi gần nhất (so mtime) -> `adb push` vào là
    //   có hiệu lực ở lần mở Quản lý lớp kế tiếp. Nạp = THÊM/CẬP NHẬT (lớp, học sinh mới, alias, giới tính, ngày sinh,
    //   chuyển lớp); KHÔNG BAO GIỜ xoá học sinh/ảnh/enroll — muốn xoá thì dùng app. name = khoá, không đổi tên qua file.
    // • "gender": nam|nu (nhận cả Nam/Nữ). "alias"/"birthdate" tuỳ chọn. Field thừa (vd "samples") bị bỏ qua.
    // • Lỗi cú pháp -> không nạp, giữ nguyên file (app KHÔNG ghi đè khi đang lỗi), lỗi ở [rosterImportError].

    private fun writeRoster() {
        if (rosterImportError != null) return // file đang lỗi cú pháp -> không đè mất bản người dùng đang sửa
        try {
            val classes = org.json.JSONArray()
            for (c in knownClasses) {
                val studs = org.json.JSONArray()
                for ((name, samples) in enrolled) {
                    if (name in testGalleryNames || classNames[name] != c) continue
                    val o = org.json.JSONObject()
                    o.put("name", name)
                    aliasNames[name]?.let { o.put("alias", it) }
                    o.put("gender", genders[name] ?: "nam")
                    birthdates[name]?.let { o.put("birthdate", it) }
                    o.put("samples", samples.size) // chỉ để xem; bỏ qua khi nạp
                    studs.put(o)
                }
                classes.put(org.json.JSONObject().put("name", c).put("students", studs))
            }
            val root = org.json.JSONObject().put("version", 1).put("classes", classes)
            rosterFile.parentFile?.mkdirs()
            rosterFile.writeText(root.toString(2))
            rosterPrefs.edit().putLong("mtime", rosterFile.lastModified()).apply()
        } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "writeRoster failed: ${e.message}", e)
        }
    }

    /** Áp 1 roster JSON vào bộ nhớ (chưa persist). Trả về true nếu có gì thay đổi. */
    private fun applyRosterJson(root: org.json.JSONObject): Boolean {
        val classes = root.getJSONArray("classes")
        var changed = false
        for (i in 0 until classes.length()) {
            val co = classes.getJSONObject(i)
            val cname = co.getString("name").trim()
            if (cname.isEmpty()) continue
            if (knownClasses.add(cname)) changed = true
            val studs = co.optJSONArray("students") ?: continue
            for (j in 0 until studs.length()) {
                val so = studs.getJSONObject(j)
                val name = so.getString("name").trim()
                if (name.isEmpty()) continue
                val isNew = name !in enrolled
                if (isNew) { enrolled[name] = mutableListOf(); rosterAddedCount++; changed = true }
                if (classNames[name] != cname) { classNames[name] = cname; changed = true }
                if (so.has("alias") && !so.isNull("alias")) {
                    val a = so.getString("alias").trim()
                    val want = if (a.isEmpty() || a == name) null else a
                    if (aliasNames[name] != want) { if (want == null) aliasNames.remove(name) else aliasNames[name] = want; changed = true }
                } else if (isNew) {
                    defaultAliasFor(name).takeIf { it != name }?.let { aliasNames[name] = it }
                }
                if (so.has("gender") && !so.isNull("gender")) {
                    val g = when (so.getString("gender").trim().lowercase()) {
                        "nu", "nữ", "female", "f" -> "nu"
                        "nam", "male", "m" -> "nam"
                        else -> null
                    }
                    if (g != null && genders[name] != g) { genders[name] = g; changed = true }
                } else if (isNew) genders[name] = "nam"
                if (so.has("birthdate") && !so.isNull("birthdate")) {
                    parseBirthdate(so.getString("birthdate"))?.let { if (birthdates[name] != it) { birthdates[name] = it; changed = true } }
                }
            }
        }
        return changed
    }

    /** "yyyy-MM-dd" hoặc "dd/MM/yyyy" -> ISO; sai định dạng -> null (bỏ qua). */
    private fun parseBirthdate(raw: String): String? {
        val t = raw.trim()
        Regex("""(\d{4})-(\d{1,2})-(\d{1,2})""").matchEntire(t)?.let {
            return "%04d-%02d-%02d".format(it.groupValues[1].toInt(), it.groupValues[2].toInt(), it.groupValues[3].toInt())
        }
        Regex("""(\d{1,2})/(\d{1,2})/(\d{4})""").matchEntire(t)?.let {
            return "%04d-%02d-%02d".format(it.groupValues[3].toInt(), it.groupValues[2].toInt(), it.groupValues[1].toInt())
        }
        return null
    }

    private fun importRosterIfChanged() {
        rosterImportError = null
        rosterAddedCount = 0
        if (!rosterFile.exists()) return
        if (rosterFile.lastModified() == rosterPrefs.getLong("mtime", -1L)) return // chính app vừa ghi, không có gì mới
        try {
            val changed = applyRosterJson(org.json.JSONObject(rosterFile.readText()))
            android.util.Log.i("FaceAttendance", "roster.json nạp xong: changed=$changed, thêm mới=$rosterAddedCount")
            persist()          // ghi enrolled.json + (qua writeRoster) chuẩn hoá lại roster.json, cập nhật mtime đã biết
            persistClasses()
        } catch (e: Exception) {
            rosterImportError = e.message ?: e.toString()
            android.util.Log.e("FaceAttendance", "roster.json lỗi, không nạp: $e")
        }
    }

    /** Dọn 1 lần dữ liệu mock cũ (2026-10-05): bỏ 3 lớp giả Mầm/Chồi/Lá + 2 lớp rác "a"/"cây" cùng các học
     * sinh giả (không có mẫu enroll) trong đó; ai đã enroll thật (có mẫu) thì chuyển vào lớp "Dev" (học sinh
     * test); tạo lớp "5 tuổi" với 16 học sinh (chưa có ảnh) nếu chưa có roster.json riêng. Có marker file nên
     * chỉ chạy 1 lần/máy. */
    private fun migrateLegacyRosterIfNeeded() {
        if (migrationMarker.exists()) return
        val legacy = setOf("Lớp Mầm", "Lớp Chồi", "Lớp Lá", "a", "cây")
        for (name in enrolled.keys.toList()) {
            if (name in testGalleryNames) continue
            val cls = classNames[name]
            if (cls != null && cls !in legacy) continue
            if (enrolled[name].isNullOrEmpty()) {
                if (cls == null) continue // placeholder chưa gán lớp: không phải mock của mình, để yên
                enrolled.remove(name); genders.remove(name); aliasNames.remove(name)
                birthdates.remove(name); classNames.remove(name)
            } else {
                classNames[name] = DEV_CLASS
            }
        }
        val others = knownClasses.filter { it !in legacy && it != DEV_CLASS && it != CLASS_5_TUOI }
        knownClasses.clear()
        knownClasses.add(CLASS_5_TUOI)
        knownClasses.add(DEV_CLASS)
        knownClasses.addAll(others)
        if (!rosterFile.exists()) {
            val studs = org.json.JSONArray()
            for ((name, gender) in DEFAULT_5_TUOI) studs.put(org.json.JSONObject().put("name", name).put("gender", gender))
            val cls = org.json.JSONArray().put(org.json.JSONObject().put("name", CLASS_5_TUOI).put("students", studs))
            applyRosterJson(org.json.JSONObject().put("classes", cls))
        }
        persist()
        persistClasses()
        try { migrationMarker.writeText("1") } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "không ghi được marker migration: $e")
        }
    }

    // ──────────────────────────  Lớp (class) management  ─────────────────────────

    @Synchronized fun classNameOf(name: String): String? = classNames[name]

    /** Known class names in the order they were first seen/created - includes classes with
     * zero students (created via addClass but nobody enrolled into them yet). */
    @Synchronized fun allClassNames(): List<String> = knownClasses.toList()

    @Synchronized fun addClass(name: String) {
        if (knownClasses.add(name)) persistClasses()
    }

    @Synchronized fun renameClass(oldName: String, newName: String) {
        if (oldName == newName || newName.isBlank()) return
        if (knownClasses.remove(oldName)) knownClasses.add(newName)
        var changed = false
        for (person in classNames.keys.toList()) {
            if (classNames[person] == oldName) { classNames[person] = newName; changed = true }
        }
        persistClasses()
        if (changed) persist()
    }

    @Synchronized fun setClassName(name: String, className: String) {
        classNames[name] = className
        knownClasses.add(className)
        persist()
        persistClasses()
    }

    /** Real enrolled students belonging to one class, name-sorted. */
    @Synchronized fun studentsInClass(className: String): List<String> =
        realEnrolledNames().filter { classNames[it] == className }

    // ──────────────────────────  Tên thường gọi (alias)  ──────────────────────────

    /** Nickname shown in the roster UI. Falls back to the real (enrollment) name if no alias
     * was ever set - so callers can always just display aliasOf(name) unconditionally. */
    @Synchronized fun aliasOf(name: String): String = aliasNames[name]?.takeIf { it.isNotBlank() } ?: name

    @Synchronized fun setAlias(name: String, alias: String) {
        val trimmed = alias.trim()
        if (trimmed.isEmpty() || trimmed == name) aliasNames.remove(name) else aliasNames[name] = trimmed
        persist()
    }

    /** Default nickname suggestion when a teacher enters the real name: the last 2 syllables
     * (e.g. "Nguyễn Khánh Vy" -> "Khánh Vy"), or the whole name if it's 1-2 syllables already. */
    @Synchronized fun defaultAliasFor(realName: String): String {
        val parts = realName.trim().split(Regex("\\s+")).filter { it.isNotEmpty() }
        return if (parts.size <= 2) parts.joinToString(" ") else parts.takeLast(2).joinToString(" ")
    }

    /** Renames an enrolled person across every map keyed by name. Returns false (no-op) if
     * newName is blank, unchanged, or already taken by someone else. */
    @Synchronized fun renameEnrollment(oldName: String, newName: String): Boolean {
        val trimmed = newName.trim()
        if (trimmed.isEmpty() || trimmed == oldName) return false
        if (trimmed in enrolled) return false
        val samples = enrolled.remove(oldName) ?: return false
        // Ảnh lưu theo tên -> đổi tên thì chuyển ảnh sang thư mục/tên file mới (lỗi thì giữ đường dẫn cũ).
        for (i in samples.indices) {
            val s = samples[i]
            val src = s.photo?.let { File(it) }?.takeIf { it.exists() } ?: continue
            try {
                val dst = newPhotoFile(trimmed)
                if (src.renameTo(dst) || (src.copyTo(dst, overwrite = false).length() == src.length() && src.delete())) {
                    samples[i] = Sample(s.embedding, dst.absolutePath)
                }
            } catch (e: Exception) {
                android.util.Log.e("FaceAttendance", "rename photo failed (${s.photo}): $e")
            }
        }
        photoDirFor(oldName).takeIf { it.isDirectory && it.list().isNullOrEmpty() }?.delete()
        enrolled[trimmed] = samples
        genders[trimmed] = genders.remove(oldName) ?: "nam"
        classNames.remove(oldName)?.let { classNames[trimmed] = it }
        aliasNames.remove(oldName)?.let { aliasNames[trimmed] = it }
        birthdates.remove(oldName)?.let { birthdates[trimmed] = it }
        lastLogged.remove(oldName)?.let { lastLogged[trimmed] = it }
        personLog.remove(oldName)?.let { personLog[trimmed] = it }
        val idx = recentNames.indexOf(oldName)
        if (idx >= 0) recentNames[idx] = trimmed
        persist()
        return true
    }

    /** Creates a name-only "chưa có ảnh" placeholder enrollment (zero samples) if it doesn't
     * already exist - lets a teacher pre-register a class roster by name before anyone has
     * actually been photographed yet (shows up as "Cần ảnh" like any other unphotographed
     * student), and is also how seeded/demo rosters get created. No-op if already enrolled. */
    @Synchronized fun ensurePlaceholder(name: String) {
        if (name in enrolled) return
        enrolled[name] = mutableListOf()
        persist()
    }

    /** Tên an toàn cho tên thư mục/file (bỏ ký tự cấm của hệ file, giữ nguyên dấu tiếng Việt). */
    private fun safeFileName(name: String): String =
        name.trim().replace(Regex("""[\\/:*?"<>|]"""), "_").ifEmpty { "_" }

    private fun photoDirFor(name: String) = File(enrolledPhotosDir, safeFileName(name))

    /** File ảnh mới của `name`: <tên>_<yyyyMMdd_HHmmss_SSS>.jpg — không trùng, đọc tên là biết của ai. */
    private fun newPhotoFile(name: String): File {
        val stamp = SimpleDateFormat("yyyyMMdd_HHmmss_SSS", Locale.US).format(Date())
        val dir = photoDirFor(name).also { it.mkdirs() }
        var f = File(dir, "${safeFileName(name)}_$stamp.jpg")
        var n = 1
        while (f.exists()) f = File(dir, "${safeFileName(name)}_${stamp}_${n++}.jpg")
        return f
    }

    /** Saves a face-crop Mat as a JPEG under enrolled_photos/<name>/ and returns its path. */
    private fun saveSamplePhoto(name: String, faceCrop: Mat?): String? {
        if (faceCrop == null || faceCrop.empty()) return null
        val file = newPhotoFile(name)
        val bmp = Bitmap.createBitmap(faceCrop.cols(), faceCrop.rows(), Bitmap.Config.ARGB_8888)
        Utils.matToBitmap(faceCrop, bmp)
        FileOutputStream(file).use { out -> bmp.compress(Bitmap.CompressFormat.JPEG, 90, out) }
        return file.absolutePath
    }

    private fun deleteSamplePhoto(path: String?) {
        if (path != null) File(path).delete()
    }

    /** Chuyển ảnh mẫu còn nằm ở thư mục cũ (getExternalFilesDir) sang /sdcard/EduXplore/enrolled_photos,
     * cập nhật đường dẫn trong enrolled.json. Chạy mỗi lần mở, không làm gì nếu đã chuyển hết;
     * ảnh không chuyển được (file mất, lỗi ghi) giữ nguyên đường dẫn cũ — KHÔNG xoá gì. */
    /** Chép attendance_log.csv + snapshots từ thư mục riêng của app (mất khi cài lại) sang
     * /sdcard/EduXplore nếu đích chưa có. Không xoá nguồn. */
    private fun migrateAppFilesToSharedDir() {
        try {
            val oldDir = context.getExternalFilesDir(null) ?: return
            val oldLog = File(oldDir, "attendance_log.csv")
            if (oldLog.exists() && !logFile.exists()) oldLog.copyTo(logFile)
            val oldSnaps = File(oldDir, "snapshots")
            if (oldSnaps.isDirectory) {
                snapshotDir.mkdirs()
                oldSnaps.listFiles()?.forEach { f ->
                    val dst = File(snapshotDir, f.name)
                    if (f.isFile && !dst.exists()) f.copyTo(dst)
                }
            }
        } catch (e: Exception) {
            android.util.Log.w("AttendanceStore", "migrateAppFilesToSharedDir failed", e)
        }
    }

    private fun migratePhotosToSharedDir() {
        var changed = false
        val root = enrolledPhotosDir.absolutePath + File.separator
        for ((name, samples) in enrolled) {
            if (name in testGalleryNames) continue
            for (i in samples.indices) {
                val s = samples[i]
                val old = s.photo ?: continue
                if (old.startsWith(root)) continue
                val src = File(old)
                if (!src.exists()) continue
                try {
                    val dst = newPhotoFile(name)
                    src.copyTo(dst, overwrite = false)
                    if (dst.length() != src.length()) { dst.delete(); continue }
                    samples[i] = Sample(s.embedding, dst.absolutePath)
                    changed = true
                    src.delete()
                } catch (e: Exception) {
                    android.util.Log.e("FaceAttendance", "migrate photo failed ($old): $e")
                }
            }
        }
        if (changed) persist()
    }

    /** Đọc lại enrolled.json trên đĩa và so với bộ nhớ cho 1 học sinh: đủ số mẫu, đúng đường dẫn ảnh,
     * và mọi file ảnh được tham chiếu đều còn. `expectGone` = học sinh phải không còn trong file. */
    private fun verifyOnDisk(name: String, expectGone: Boolean = false): Boolean {
        return try {
            val arr = org.json.JSONArray(enrolledFile.readText())
            var found: org.json.JSONObject? = null
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                if (o.getString("name") == name) { found = o; break }
            }
            if (expectGone) return found == null
            val mem = enrolled[name] ?: return found == null
            val disk = found ?: return false
            val ds = disk.getJSONArray("samples")
            if (ds.length() != mem.size) return false
            for (j in 0 until ds.length()) {
                val so = ds.getJSONObject(j)
                val p = if (so.isNull("photo")) null else so.getString("photo")
                if (p != mem[j].photo) return false
                if (p != null && !File(p).exists()) return false
            }
            true
        } catch (e: Exception) {
            android.util.Log.e("FaceAttendance", "verifyOnDisk($name) failed: $e")
            false
        }
    }

    /** Adds one embedding to a person's cluster - used both for fresh/supplementary enrollment
     * and for samples confirmed via the ambiguous-match dialog. */
    @Synchronized fun addSample(name: String, embedding: FloatArray, faceCrop: Mat?): Boolean {
        val samples = enrolled.getOrPut(name) { mutableListOf() }
        // Đã đủ trần (hiện 3 = chỉ các mẫu lúc lấy mẫu) thì TỪ CHỐI, không tự thay/xoá mẫu cũ:
        // muốn đổi ảnh thì xoá bớt ở panel học sinh trước. Không có chỗ nào tự thêm mẫu khi nhận diện.
        if (samples.size >= MAX_SAMPLES_PER_PERSON) return false
        val photo = saveSamplePhoto(name, faceCrop)
        samples.add(Sample(embedding, photo))
        testGalleryNames.remove(name)
        persist()
        return true
    }

    /** Xoá 1 mẫu (embedding + file ảnh) rồi kiểm tra lại enrolled.json trên đĩa. Học sinh KHÔNG bị
     * xoá dù hết mẫu — chỉ chuyển sang "cần ảnh"; xoá học sinh phải dùng deleteEnrollment().
     * Trả về true nếu dữ liệu trên đĩa đã khớp (embedding và ảnh đã mất cùng nhau). */
    @Synchronized fun deleteSample(name: String, index: Int): Boolean {
        val samples = enrolled[name] ?: return false
        if (index !in samples.indices) return false
        val removed = samples.removeAt(index)
        deleteSamplePhoto(removed.photo)
        persist()
        if (verifyOnDisk(name)) return true
        persist() // thử ghi lại 1 lần
        return verifyOnDisk(name).also {
            if (!it) android.util.Log.e("FaceAttendance", "deleteSample($name,$index): enrolled.json không khớp sau khi xoá")
        }
    }

    @Synchronized fun samplesOf(name: String): List<Sample> = enrolled[name]?.toList() ?: emptyList()

    /** "nam" or "nu" - only meaningful the first time a name is enrolled; ignored on
     * supplementary enrollment of an existing name (gender doesn't change). */
    @Synchronized fun setGender(name: String, gender: String) {
        if (name !in genders) {
            genders[name] = gender
            persist()
        }
    }

    @Synchronized fun genderOf(name: String): String = genders[name] ?: "nam"

    /** Sửa lại giới tính đã có - khác setGender() (chỉ set được LẦN ĐẦU, cố ý không cho ghi đè
     * lúc enroll bổ sung). Dùng cho panel sửa thông tin học sinh trong "Quản lý lớp". */
    @Synchronized fun updateGender(name: String, gender: String) {
        genders[name] = gender
        persist()
    }

    // ──────────────────────────  Ngày sinh / tuổi theo tháng  ──────────────────────────

    /** ISO "yyyy-MM-dd", null nếu giáo viên chưa nhập. */
    @Synchronized fun birthdateOf(name: String): String? = birthdates[name]

    @Synchronized fun setBirthdate(name: String, isoDate: String) {
        birthdates[name] = isoDate
        persist()
    }

    /** Tuổi theo THÁNG tại thời điểm gọi hàm - null nếu chưa có ngày sinh. Mầm non 3 tuổi 1
     * tháng và 3 tuổi 11 tháng phát triển khác nhau nhiều, nên chuẩn hoá năng lực cần độ phân
     * giải theo tháng, không phải theo năm (xem CLAUDE.md, phần "Năng lực theo môn" Giai đoạn 0). */
    @Synchronized fun ageInMonthsOf(name: String): Int? {
        val iso = birthdates[name] ?: return null
        return try {
            val parts = iso.split("-")
            val today = java.util.Calendar.getInstance()
            val birth = java.util.Calendar.getInstance().apply {
                clear()
                set(parts[0].toInt(), parts[1].toInt() - 1, parts[2].toInt())
            }
            var months = (today.get(java.util.Calendar.YEAR) - birth.get(java.util.Calendar.YEAR)) * 12
            months += today.get(java.util.Calendar.MONTH) - birth.get(java.util.Calendar.MONTH)
            if (today.get(java.util.Calendar.DAY_OF_MONTH) < birth.get(java.util.Calendar.DAY_OF_MONTH)) months--
            if (months < 0) null else months
        } catch (e: Exception) {
            null
        }
    }

    @Synchronized fun representativePhoto(name: String): String? = enrolled[name]?.firstOrNull { it.photo != null }?.photo

    /** Xoá hẳn 1 học sinh: embedding, toàn bộ ảnh (cả thư mục ảnh của bạn đó), lớp, alias, ngày sinh.
     * Trả về true nếu enrolled.json trên đĩa đã không còn bạn này. */
    @Synchronized fun deleteEnrollment(name: String): Boolean {
        val samples = enrolled.remove(name) ?: emptyList()
        for (s in samples) deleteSamplePhoto(s.photo)
        photoDirFor(name).takeIf { it.isDirectory && it.list().isNullOrEmpty() }?.delete()
        genders.remove(name)
        aliasNames.remove(name)
        birthdates.remove(name)
        classNames.remove(name)
        lastLogged.remove(name)
        personLog.remove(name)
        recentNames.remove(name)
        persist()
        return verifyOnDisk(name, expectGone = true)
    }

    @Synchronized fun enrolledCount() = enrolled.size

    /** Real (non test-gallery) enrolled names, for a management/delete UI. */
    @Synchronized fun realEnrolledNames(): List<String> = enrolled.keys.filter { it !in testGalleryNames }.sorted()

    /** The attendance CSV file, for sharing/export. Null if nothing has been logged yet. */
    @Synchronized fun logFileIfExists(): File? = if (logFile.exists()) logFile else null

    /**
     * Test-only helper: bulk-loads a JSON array of {"name": str, "embedding": [floats]}
     * (see gen_test_gallery.py, which computes these with the same SFace model from real
     * LFW photos) to stress-test matching/margin behavior at a large gallery size without
     * needing hundreds of real people to physically enroll. Not persisted (see testGalleryNames).
     */
    @Synchronized fun importTestGallery(json: String): Int {
        val arr = org.json.JSONArray(json)
        var count = 0
        for (i in 0 until arr.length()) {
            val obj = arr.getJSONObject(i)
            val name = obj.getString("name")
            val embArr = obj.getJSONArray("embedding")
            val embedding = FloatArray(embArr.length()) { embArr.getDouble(it).toFloat() }
            enrolled[name] = mutableListOf(Sample(embedding, null))
            testGalleryNames.add(name)
            count++
        }
        return count
    }

    /**
     * Each person's score is the max similarity across their whole sample cluster, not a
     * single centroid - a cluster covering more real appearances discriminates better. Returns
     * both the best and runner-up person's name+score so callers can apply a margin check
     * (false-positive risk scales with gallery size) and, when the two are too close to call
     * (e.g. identical twins), offer the runner-up as the other candidate to confirm between.
     */
    @Synchronized fun bestMatch(embedding: FloatArray, engine: FaceEngine): MatchResult {
        if (enrolled.isEmpty()) return MatchResult(null, -1f, null, -1f)
        var bestName: String? = null
        var bestSim = -1f
        var runnerName: String? = null
        var runnerSim = -1f
        for ((name, samples) in enrolled) {
            var personBest = -1f
            for (s in samples) {
                val sim = engine.cosineSim(s.embedding, embedding)
                if (sim > personBest) personBest = sim
            }
            if (personBest > bestSim) {
                runnerName = bestName
                runnerSim = bestSim
                bestName = name
                bestSim = personBest
            } else if (personBest > runnerSim) {
                runnerName = name
                runnerSim = personBest
            }
        }
        return MatchResult(bestName, bestSim, runnerName, runnerSim)
    }

    @Synchronized fun canLog(name: String, cooldownMs: Long): Boolean {
        val last = lastLogged[name] ?: 0L
        return System.currentTimeMillis() - last > cooldownMs
    }

    @Synchronized fun logAttendance(name: String, faceCrop: Mat?, confidence: Float) {
        val now = System.currentTimeMillis()
        lastLogged[name] = now
        val ts = timeFmt.format(Date(now))

        val entry = personLog.getOrPut(name) { PersonLogEntry(now, now, 0, null) }
        entry.last = now
        entry.count++
        if (faceCrop != null && !faceCrop.empty()) {
            val bmp = Bitmap.createBitmap(faceCrop.cols(), faceCrop.rows(), Bitmap.Config.ARGB_8888)
            Utils.matToBitmap(faceCrop, bmp)
            entry.snapshot = Bitmap.createScaledBitmap(bmp, 90, 90, true)
            val snapFile = File(snapshotDir, "${name}_${ts.replace(Regex("[ :]"), "")}.jpg")
            FileOutputStream(snapFile).use { out -> bmp.compress(Bitmap.CompressFormat.JPEG, 90, out) }
        }

        recentNames.remove(name)
        recentNames.add(name)
        while (recentNames.size > RECENT_PEOPLE_SHOWN) recentNames.removeAt(0)

        val isNew = !logFile.exists()
        FileWriter(logFile, true).use { w ->
            if (isNew) w.append("timestamp,name,confidence\n")
            w.append("$ts,$name,${"%.3f".format(confidence)}\n")
        }
    }

    fun loadPhotoBitmap(path: String?, sizePx: Int): Bitmap? {
        if (path == null || !File(path).exists()) return null
        val bmp = BitmapFactory.decodeFile(path) ?: return null
        val scaled = Bitmap.createScaledBitmap(bmp, sizePx, sizePx, true)
        if (scaled !== bmp) bmp.recycle() // ảnh gốc to, không trả sớm thì chiếm RAM tới lần GC sau
        return scaled
    }
}
