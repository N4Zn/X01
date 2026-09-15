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

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
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
