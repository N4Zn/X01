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
        // most recent are kept (oldest rolling sample evicted first).
        const val PERMANENT_SAMPLES = 3
        const val ROLLING_SAMPLES = 5
        const val MAX_SAMPLES_PER_PERSON = PERMANENT_SAMPLES + ROLLING_SAMPLES
        const val RECENT_PEOPLE_SHOWN = 5
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
    private val logFile = File(context.getExternalFilesDir(null), "attendance_log.csv")
    // Shared path: both FaceAttendance and EduXplore game read/write this file.
    private val sharedDir = File("/sdcard/EduXplore").also { it.mkdirs() }
    private val enrolledFile = File(sharedDir, "enrolled.json")
    private val classesFile = File(sharedDir, "classes.json")
    // Điểm THẬT từng học sinh, ghi bởi Unity (PlayerRecognitionService.MergeStudentScoresToSharedFile,
    // luỹ kế qua nhiều ván/nhiều lần mở app) — đọc để phục vụ sort-theo-điểm + hiện điểm cá nhân
    // trong "Quản lý lớp" (ClassManagementActivity), đúng tính năng có ở bản HTML mockup nhưng
    // trước đây CHƯA nối được vì không có nguồn dữ liệu thật (roster khi đó toàn MockData).
    private val scoresFile = File(sharedDir, "student_scores.json")
    private val enrolledPhotosDir = File(context.getExternalFilesDir(null), "enrolled_photos")
    private val snapshotDir = File(context.getExternalFilesDir(null), "snapshots")
    private val timeFmt = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US)
    private val displayTimeFmt = SimpleDateFormat("HH:mm:ss", Locale.US)

    // Per-person aggregate (first/last check-in time, count, latest thumbnail) instead of a
    // flat event list - with a 10s cooldown a busy person would otherwise add a new line every
    // 10s forever. recentNames tracks display order, most-recent last, capped at RECENT_PEOPLE_SHOWN.
    val personLog = LinkedHashMap<String, PersonLogEntry>()
    val recentNames = mutableListOf<String>()

    data class StudentScore(val totalScore: Int, val totalAnswered: Int, val totalCorrect: Int, val lastPlayed: String)
    private val scores = HashMap<String, StudentScore>()

    init {
        enrolledPhotosDir.mkdirs()
        snapshotDir.mkdirs()
        loadPersisted()
        reloadScores()
    }

    /** File điểm do Unity ghi ĐỘC LẬP với vòng đời FA app (game chạy ở process/module khác,
     * ghi bất cứ lúc nào giáo viên đang chơi) — gọi lại hàm này mỗi khi mở/quay lại "Quản lý
     * lớp" (onResume()) để luôn thấy điểm mới nhất, không chỉ điểm lúc app khởi động. */
    fun reloadScores() {
        scores.clear()
        if (!scoresFile.exists()) return
        try {
            val root = org.json.JSONObject(scoresFile.readText())
            val arr = root.optJSONArray("scores") ?: return
            for (i in 0 until arr.length()) {
                val obj = arr.getJSONObject(i)
                val name = obj.optString("name", "")
                if (name.isEmpty()) continue
                scores[name] = StudentScore(
                    totalScore = obj.optInt("totalScore", 0),
                    totalAnswered = obj.optInt("totalAnswered", 0),
                    totalCorrect = obj.optInt("totalCorrect", 0),
                    lastPlayed = obj.optString("lastPlayed", "")
                )
            }
        } catch (e: Exception) {
            android.util.Log.w("AttendanceStore", "reloadScores lỗi: ${e.message}")
        }
    }

    /** null = học sinh này chưa có điểm nào (chưa từng được nhận diện lúc chơi game). */
    fun scoreOf(name: String): StudentScore? = scores[name]

    private fun loadPersisted() {
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
        if (classesFile.exists()) {
            try {
                val arr = org.json.JSONArray(classesFile.readText())
                for (i in 0 until arr.length()) knownClasses.add(arr.getString(i))
            } catch (e: Exception) {
                android.util.Log.e("FaceAttendance", "Failed to load classes.json", e)
            }
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
    }

    // ──────────────────────────  Lớp (class) management  ─────────────────────────

    fun classNameOf(name: String): String? = classNames[name]

    /** Known class names in the order they were first seen/created - includes classes with
     * zero students (created via addClass but nobody enrolled into them yet). */
    fun allClassNames(): List<String> = knownClasses.toList()

    fun addClass(name: String) {
        if (knownClasses.add(name)) persistClasses()
    }

    fun renameClass(oldName: String, newName: String) {
        if (oldName == newName || newName.isBlank()) return
        if (knownClasses.remove(oldName)) knownClasses.add(newName)
        var changed = false
        for (person in classNames.keys.toList()) {
            if (classNames[person] == oldName) { classNames[person] = newName; changed = true }
        }
        persistClasses()
        if (changed) persist()
    }

    fun setClassName(name: String, className: String) {
        classNames[name] = className
        knownClasses.add(className)
        persist()
        persistClasses()
    }

    /** Real enrolled students belonging to one class, name-sorted. */
    fun studentsInClass(className: String): List<String> =
        realEnrolledNames().filter { classNames[it] == className }

    // ──────────────────────────  Tên thường gọi (alias)  ──────────────────────────

    /** Nickname shown in the roster UI. Falls back to the real (enrollment) name if no alias
     * was ever set - so callers can always just display aliasOf(name) unconditionally. */
    fun aliasOf(name: String): String = aliasNames[name]?.takeIf { it.isNotBlank() } ?: name

    fun setAlias(name: String, alias: String) {
        val trimmed = alias.trim()
        if (trimmed.isEmpty() || trimmed == name) aliasNames.remove(name) else aliasNames[name] = trimmed
        persist()
    }

    /** Default nickname suggestion when a teacher enters the real name: the last 2 syllables
     * (e.g. "Nguyễn Khánh Vy" -> "Khánh Vy"), or the whole name if it's 1-2 syllables already. */
    fun defaultAliasFor(realName: String): String {
        val parts = realName.trim().split(Regex("\\s+")).filter { it.isNotEmpty() }
        return if (parts.size <= 2) parts.joinToString(" ") else parts.takeLast(2).joinToString(" ")
    }

    /** Renames an enrolled person across every map keyed by name. Returns false (no-op) if
     * newName is blank, unchanged, or already taken by someone else. */
    fun renameEnrollment(oldName: String, newName: String): Boolean {
        val trimmed = newName.trim()
        if (trimmed.isEmpty() || trimmed == oldName) return false
        if (trimmed in enrolled) return false
        val samples = enrolled.remove(oldName) ?: return false
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
    fun ensurePlaceholder(name: String) {
        if (name in enrolled) return
        enrolled[name] = mutableListOf()
        persist()
    }

    /** Saves a face-crop Mat as a JPEG under enrolled_photos/<name>/ and returns its path. */
    private fun saveSamplePhoto(name: String, faceCrop: Mat?): String? {
        if (faceCrop == null || faceCrop.empty()) return null
        val personDir = File(enrolledPhotosDir, name)
        personDir.mkdirs()
        val file = File(personDir, "${System.currentTimeMillis()}.jpg")
        val bmp = Bitmap.createBitmap(faceCrop.cols(), faceCrop.rows(), Bitmap.Config.ARGB_8888)
        Utils.matToBitmap(faceCrop, bmp)
        FileOutputStream(file).use { out -> bmp.compress(Bitmap.CompressFormat.JPEG, 90, out) }
        return file.absolutePath
    }

    private fun deleteSamplePhoto(path: String?) {
        if (path != null) File(path).delete()
    }

    /** Adds one embedding to a person's cluster - used both for fresh/supplementary enrollment
     * and for samples confirmed via the ambiguous-match dialog. */
    fun addSample(name: String, embedding: FloatArray, faceCrop: Mat?) {
        val samples = enrolled.getOrPut(name) { mutableListOf() }
        val photo = saveSamplePhoto(name, faceCrop)
        samples.add(Sample(embedding, photo))
        if (samples.size > MAX_SAMPLES_PER_PERSON) {
            val removed = samples.removeAt(PERMANENT_SAMPLES)
            deleteSamplePhoto(removed.photo)
        }
        testGalleryNames.remove(name)
        persist()
    }

    fun deleteSample(name: String, index: Int) {
        val samples = enrolled[name] ?: return
        if (index >= samples.size) return
        val removed = samples.removeAt(index)
        deleteSamplePhoto(removed.photo)
        if (samples.isEmpty()) enrolled.remove(name)
        persist()
    }

    fun samplesOf(name: String): List<Sample> = enrolled[name] ?: emptyList()

    /** "nam" or "nu" - only meaningful the first time a name is enrolled; ignored on
     * supplementary enrollment of an existing name (gender doesn't change). */
    fun setGender(name: String, gender: String) {
        if (name !in genders) {
            genders[name] = gender
            persist()
        }
    }

    fun genderOf(name: String): String = genders[name] ?: "nam"

    /** Sửa lại giới tính đã có - khác setGender() (chỉ set được LẦN ĐẦU, cố ý không cho ghi đè
     * lúc enroll bổ sung). Dùng cho panel sửa thông tin học sinh trong "Quản lý lớp". */
    fun updateGender(name: String, gender: String) {
        genders[name] = gender
        persist()
    }

    // ──────────────────────────  Ngày sinh / tuổi theo tháng  ──────────────────────────

    /** ISO "yyyy-MM-dd", null nếu giáo viên chưa nhập. */
    fun birthdateOf(name: String): String? = birthdates[name]

    fun setBirthdate(name: String, isoDate: String) {
        birthdates[name] = isoDate
        persist()
    }

    /** Tuổi theo THÁNG tại thời điểm gọi hàm - null nếu chưa có ngày sinh. Mầm non 3 tuổi 1
     * tháng và 3 tuổi 11 tháng phát triển khác nhau nhiều, nên chuẩn hoá năng lực cần độ phân
     * giải theo tháng, không phải theo năm (xem CLAUDE.md, phần "Năng lực theo môn" Giai đoạn 0). */
    fun ageInMonthsOf(name: String): Int? {
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

    fun representativePhoto(name: String): String? = enrolled[name]?.firstOrNull { it.photo != null }?.photo

    fun deleteEnrollment(name: String) {
        val samples = enrolled.remove(name) ?: emptyList()
        for (s in samples) deleteSamplePhoto(s.photo)
        genders.remove(name)
        aliasNames.remove(name)
        birthdates.remove(name)
        lastLogged.remove(name)
        persist()
    }

    fun enrolledCount() = enrolled.size

    /** Real (non test-gallery) enrolled names, for a management/delete UI. */
    fun realEnrolledNames(): List<String> = enrolled.keys.filter { it !in testGalleryNames }.sorted()

    /** The attendance CSV file, for sharing/export. Null if nothing has been logged yet. */
    fun logFileIfExists(): File? = if (logFile.exists()) logFile else null

    /**
     * Test-only helper: bulk-loads a JSON array of {"name": str, "embedding": [floats]}
     * (see gen_test_gallery.py, which computes these with the same SFace model from real
     * LFW photos) to stress-test matching/margin behavior at a large gallery size without
     * needing hundreds of real people to physically enroll. Not persisted (see testGalleryNames).
     */
    fun importTestGallery(json: String): Int {
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
    fun bestMatch(embedding: FloatArray, engine: FaceEngine): MatchResult {
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

    fun canLog(name: String, cooldownMs: Long): Boolean {
        val last = lastLogged[name] ?: 0L
        return System.currentTimeMillis() - last > cooldownMs
    }

    fun logAttendance(name: String, faceCrop: Mat?, confidence: Float) {
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
        return Bitmap.createScaledBitmap(bmp, sizePx, sizePx, true)
    }
}
