package com.faceattendance.app

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.PointF
import android.graphics.RectF
import android.util.AttributeSet
import android.view.View

data class FaceOverlayItem(
    val rect: RectF,
    val landmarks: List<PointF>,
    val label: String,
    val color: Int
)

/** Draws detection boxes, landmarks and status labels (one per detected face) on top of the camera preview. */
class OverlayView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    private var items: List<FaceOverlayItem> = emptyList()

    // Source frame size the coordinates above are expressed in, used to scale to view size.
    private var sourceWidth: Int = 1
    private var sourceHeight: Int = 1

    private val boxPaint = Paint().apply {
        style = Paint.Style.STROKE
        strokeWidth = 5f
    }
    private val dotPaint = Paint().apply {
        style = Paint.Style.FILL
        color = Color.MAGENTA
    }
    private val textPaint = Paint().apply {
        textSize = 22f
        isFakeBoldText = true
        style = Paint.Style.FILL
    }
    private val textBgPaint = Paint().apply {
        color = Color.parseColor("#AA000000")
        style = Paint.Style.FILL
    }

    /** Single-face convenience overload (kept for callers that only ever track one face). */
    fun update(rect: RectF?, pts: List<PointF>, text: String, color: Int, srcW: Int, srcH: Int) {
        update(
            if (rect == null) emptyList() else listOf(FaceOverlayItem(rect, pts, text, color)),
            srcW, srcH
        )
    }

    fun update(faces: List<FaceOverlayItem>, srcW: Int, srcH: Int) {
        items = faces
        sourceWidth = srcW.coerceAtLeast(1)
        sourceHeight = srcH.coerceAtLeast(1)
        postInvalidate()
    }

    // --- Vùng nhận diện (toạ độ chuẩn hoá 0..1, xem FaceZones) ---
    // zoneSetup=false: chỉ làm tối phần NGOÀI hình chữ nhật enroll (hướng dẫn đứng vào đâu).
    // zoneSetup=true: vẽ cả 3 vùng, vùng đang chọn nét đậm.
    private var zones: FaceZones? = null
    private var zoneSetup = false
    private var zoneSelected = 0 // 0 = trái, 1 = phải, 2 = chữ nhật enroll

    fun setZones(z: FaceZones?, setup: Boolean = false, selected: Int = 0) {
        zones = z; zoneSetup = setup; zoneSelected = selected
        postInvalidate()
    }

    private val zoneDimPaint = Paint().apply { color = Color.parseColor("#99000000"); style = Paint.Style.FILL }
    private val zoneLinePaint = Paint().apply { style = Paint.Style.STROKE; isAntiAlias = true }
    private val zoneTextPaint = Paint().apply { textSize = 26f; isFakeBoldText = true; isAntiAlias = true }

    private fun drawZones(canvas: Canvas) {
        val z = zones ?: return
        val w = width.toFloat(); val h = height.toFloat()
        // Preview là ImageView fitCenter 16:9 — vẽ vùng theo cùng phép co giãn như khung mặt (xem onDraw).
        val e = z.enroll
        if (!zoneSetup) {
            // Tối phần NGOÀI vùng: 4 hình chữ nhật quanh vùng (không dùng Path INVERSE_WINDING — bị ngược trên một số máy).
            val l = e.x * w; val t = e.y * h; val r = (e.x + e.w) * w; val b = (e.y + e.h) * h
            canvas.drawRect(0f, 0f, w, t, zoneDimPaint)
            canvas.drawRect(0f, b, w, h, zoneDimPaint)
            canvas.drawRect(0f, t, l, b, zoneDimPaint)
            canvas.drawRect(r, t, w, b, zoneDimPaint)
            zoneLinePaint.color = Color.WHITE; zoneLinePaint.strokeWidth = 4f
            canvas.drawRect(l, t, r, b, zoneLinePaint)
            return
        }
        fun rect(zr: ZoneRect, sel: Boolean, color: Int, title: String) {
            zoneLinePaint.color = color; zoneLinePaint.strokeWidth = if (sel) 8f else 3f
            canvas.drawRect(zr.x * w, zr.y * h, (zr.x + zr.w) * w, (zr.y + zr.h) * h, zoneLinePaint)
            zoneTextPaint.color = color
            canvas.drawText(title, zr.x * w + 10f, zr.y * h + 30f, zoneTextPaint)
        }
        rect(z.playLeft, zoneSelected == 0, Color.rgb(0, 200, 255), "TRÁI (game)")
        rect(z.playRight, zoneSelected == 1, Color.rgb(255, 200, 0), "PHẢI (game)")
        rect(e, zoneSelected == 2, Color.rgb(120, 255, 120), "ENROLL (thêm từ camera)")
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        drawZones(canvas)
        if (items.isEmpty()) return
        val tStart = System.nanoTime()

        val scaleX = width.toFloat() / sourceWidth
        val scaleY = height.toFloat() / sourceHeight

        for (item in items) {
            val scaledRect = RectF(
                item.rect.left * scaleX, item.rect.top * scaleY,
                item.rect.right * scaleX, item.rect.bottom * scaleY
            )
            boxPaint.color = item.color
            canvas.drawRect(scaledRect, boxPaint)

            for (p in item.landmarks) {
                canvas.drawCircle(p.x * scaleX, p.y * scaleY, 4f, dotPaint)
            }

            if (item.label.isNotEmpty()) {
                // Multi-line labels (long strings) wrap below the box instead of overflowing off-screen.
                val lines = item.label.split(" | ")
                val lineHeight = textPaint.textSize + 6f
                var textY = (scaledRect.top - lineHeight * lines.size - 4f).coerceAtLeast(lineHeight)
                val maxWidth = lines.maxOf { textPaint.measureText(it) }
                textBgPaint.let {
                    canvas.drawRect(
                        scaledRect.left, textY - textPaint.textSize,
                        scaledRect.left + maxWidth + 10f, textY + lineHeight * (lines.size - 1) + 6f,
                        it
                    )
                }
                textPaint.color = item.color
                for (line in lines) {
                    canvas.drawText(line, scaledRect.left + 5f, textY, textPaint)
                    textY += lineHeight
                }
            }
        }
        val tEnd = System.nanoTime()
        android.util.Log.e("FacePerf", "  onDraw (boxes+landmarks+labels, n=${items.size})=${"%.2f".format((tEnd - tStart) / 1_000_000.0)}ms")
    }
}
