package com.faceattendance.app

import android.util.Log
import java.io.File
import java.io.PrintWriter
import java.io.StringWriter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Ghi exception chưa bắt (mọi thread) vào /sdcard/EduXplore/crash_fa.txt để còn bằng chứng sau khi
 * app thoát (logcat mất khi tắt máy). Crash native (SIGSEGV) và bị hệ thống kill vì thiếu RAM KHÔNG
 * vào được đây — loại đó chỉ thấy bằng `adb logcat -b crash` / `dumpsys activity exit-info`.
 */
object CrashLog {
    private const val MAX_BYTES = 200_000L
    private var installed = false

    @Synchronized
    fun install(tag: String) {
        if (installed) return
        installed = true
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            Log.e("FaceAttendance", "Uncaught exception on thread ${thread.name}", throwable)
            write(tag, thread.name, throwable)
            previous?.uncaughtException(thread, throwable)
        }
    }

    private fun write(tag: String, threadName: String, t: Throwable) {
        try {
            val f = File("/sdcard/EduXplore/crash_fa.txt")
            f.parentFile?.mkdirs()
            if (f.length() > MAX_BYTES) f.delete()
            val sw = StringWriter()
            t.printStackTrace(PrintWriter(sw))
            val ts = SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(Date())
            f.appendText("=== $ts [$tag] thread=$threadName ===\n$sw\n")
        } catch (_: Throwable) {
        }
    }
}
