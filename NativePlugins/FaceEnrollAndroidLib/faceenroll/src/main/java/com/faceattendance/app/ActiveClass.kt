package com.faceattendance.app

import android.util.Log
import java.io.File

/**
 * Lớp đang dùng — MỘT lựa chọn chung cho Quản lý lớp, tab Lớp học của ControlActivity và nhận diện trong game.
 * Lưu /sdcard/EduXplore/active_class.txt (1 dòng = tên lớp). Nhận diện mặt (màn camera + plugin game) chỉ so khớp
 * với học sinh của lớp này. Bản sao cùng format: ControlActivity (ui/ActiveClass.java) và plugin game (FaceDatabase.kt) — sửa 1 nơi thì sửa cả 3.
 */
object ActiveClass {
    private const val TAG = "ActiveClass"
    val file = File("/sdcard/EduXplore/active_class.txt")

    fun read(): String? = try {
        if (file.canRead()) file.readText().trim().takeIf { it.isNotEmpty() } else null
    } catch (e: Exception) { Log.e(TAG, "read lỗi: ${e.message}"); null }

    fun write(className: String?) {
        if (className.isNullOrBlank()) return
        try {
            file.parentFile?.mkdirs()
            if (read() != className) file.writeText(className)
        } catch (e: Exception) { Log.e(TAG, "write lỗi: ${e.message}") }
    }
}
