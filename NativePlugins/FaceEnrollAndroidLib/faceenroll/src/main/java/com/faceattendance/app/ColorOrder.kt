package com.faceattendance.app

import android.graphics.Bitmap
import org.opencv.android.Utils
import org.opencv.core.Mat
import org.opencv.imgproc.Imgproc

/**
 * `Utils.matToBitmap` coi Mat 3 kênh là RGB, còn Mat khung hình ở đây là "BGR".
 *
 * - Webcam UVC (đường cũ): MainActivity giải NV21 trong khi dữ liệu thực tế là NV12 (xem UnityFaceBridge dùng NV12) nên 2 kênh màu
 *   bị đảo đúng bằng phép đảo R/B của matToBitmap → hình hiển thị/ảnh mẫu lưu ra trông đúng. Giữ nguyên, không đụng.
 * - Camera IP: JPEG giải mã ra BGR đúng thứ tự (cùng quy ước với UnityFaceBridge), nên phải đổi sang RGB trước khi hiển thị/lưu,
 *   nếu không da người hiện xanh. [trueBgr] = true khi đang dùng camera IP.
 *
 * Chỉ dùng cho hiển thị/lưu ảnh; ảnh đưa vào dò mặt/trích đặc trưng KHÔNG đổi.
 */
object ColorOrder {
    @Volatile var trueBgr = false

    fun matToBitmap(mat: Mat, bmp: Bitmap) {
        if (trueBgr && mat.channels() == 3) {
            val rgb = Mat()
            try {
                Imgproc.cvtColor(mat, rgb, Imgproc.COLOR_BGR2RGB)
                Utils.matToBitmap(rgb, bmp)
            } finally {
                rgb.release()
            }
        } else {
            Utils.matToBitmap(mat, bmp)
        }
    }
}
