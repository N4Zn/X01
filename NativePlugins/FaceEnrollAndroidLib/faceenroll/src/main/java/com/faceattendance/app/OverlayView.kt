package com.faceattendance.app

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.DashPathEffect
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
    private var sourceKnown = false

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
        sourceKnown = true
        postInvalidate()
    }

    // --- Vùng nhận diện (toạ độ chuẩn hoá 0..1, xem FaceZones) ---
    // zoneSetup=false: chỉ làm tối phần NGOÀI hình chữ nhật enroll (hướng dẫn đứng vào đâu).
    // zoneSetup=true: vẽ cả 3 vùng, vùng đang chọn nét đậm.
    private var zones: FaceZones? = null
    private var zoneSetup = false
    private var zoneSelected = 0 // 0 = trái, 1 = phải, 2 = chữ nhật enroll

    private var showStand = false

    /** Vòng tròn đứng chân (nét đứt xanh) ở chế độ không chỉnh vùng — bật ở các màn chụp ảnh. Chế độ chỉnh vùng luôn vẽ. */
    fun setShowStand(show: Boolean) { showStand = show; postInvalidate() }

    fun setZones(z: FaceZones?, setup: Boolean = false, selected: Int = 0) {
        zones = z; zoneSetup = setup; zoneSelected = selected
        postInvalidate()
    }

    /** Vùng hiển thị THẬT của hình camera trong view: preview là ImageView fitCenter nên màn khác tỉ lệ khung (vd 16:10) sẽ có
     * viền đen; khung mặt + vùng phải vẽ trong hình chữ nhật này, không phải cả view (trước đây vẽ lệch vài % so với vùng dò thật). */
    private fun imageRect(): RectF {
        val vw = width.toFloat(); val vh = height.toFloat()
        val ar = if (sourceKnown) sourceWidth.toFloat() / sourceHeight else 16f / 9f
        val iw: Float; val ih: Float
        if (vw / vh > ar) { ih = vh; iw = vh * ar } else { iw = vw; ih = vw / ar }
        val l = (vw - iw) / 2f; val t = (vh - ih) / 2f
        return RectF(l, t, l + iw, t + ih)
    }

    private val standPaint = Paint().apply {
        style = Paint.Style.STROKE; isAntiAlias = true
        color = Color.rgb(33, 150, 243)
        pathEffect = DashPathEffect(floatArrayOf(26f, 16f), 0f)
    }

    private fun drawStand(canvas: Canvas, img: RectF, highlight: Boolean) {
        val st = zones?.stand ?: return
        standPaint.strokeWidth = if (highlight) 9f else 6f
        canvas.drawCircle(img.left + st.cx * img.width(), img.top + st.cy * img.height(), st.r * img.height(), standPaint)
    }

    private val zoneDimPaint = Paint().apply { color = Color.parseColor("#99000000"); style = Paint.Style.FILL }
    private val zoneLinePaint = Paint().apply { style = Paint.Style.STROKE; isAntiAlias = true }
    private val zoneTextPaint = Paint().apply { textSize = 26f; isFakeBoldText = true; isAntiAlias = true }

    private fun drawZones(canvas: Canvas) {
        val z = zones ?: return
        val w = width.toFloat(); val h = height.toFloat()
        // Toạ độ vùng chuẩn hoá theo HÌNH camera (không phải cả view) — ánh xạ vào vùng hiển thị thật của hình.
        val img = imageRect()
        val iw = img.width(); val ih = img.height()
        val e = z.enroll
        if (!zoneSetup) {
            // Tối phần NGOÀI vùng: 4 hình chữ nhật quanh vùng (không dùng Path INVERSE_WINDING — bị ngược trên một số máy).
            val l = img.left + e.x * iw; val t = img.top + e.y * ih
            val r = img.left + (e.x + e.w) * iw; val b = img.top + (e.y + e.h) * ih
            canvas.drawRect(0f, 0f, w, t, zoneDimPaint)
            canvas.drawRect(0f, b, w, h, zoneDimPaint)
            canvas.drawRect(0f, t, l, b, zoneDimPaint)
            canvas.drawRect(r, t, w, b, zoneDimPaint)
            zoneLinePaint.color = Color.WHITE; zoneLinePaint.strokeWidth = 4f
            canvas.drawRect(l, t, r, b, zoneLinePaint)
            if (showStand) drawStand(canvas, img, false)
            return
        }
        fun rect(zr: ZoneRect, sel: Boolean, color: Int, title: String) {
            zoneLinePaint.color = color; zoneLinePaint.strokeWidth = if (sel) 8f else 3f
            val l = img.left + zr.x * iw; val t = img.top + zr.y * ih
            canvas.drawRect(l, t, img.left + (zr.x + zr.w) * iw, img.top + (zr.y + zr.h) * ih, zoneLinePaint)
            zoneTextPaint.color = color
            canvas.drawText(title, l + 10f, t + 30f, zoneTextPaint)
        }
        rect(z.playLeft, zoneSelected == 0, Color.rgb(0, 200, 255), "TRÁI (game)")
        rect(z.playRight, zoneSelected == 1, Color.rgb(255, 200, 0), "PHẢI (game)")
        rect(e, zoneSelected == 2, Color.rgb(120, 255, 120), "ENROLL (thêm từ camera)")
        drawStand(canvas, img, zoneSelected == 3)
        zoneTextPaint.color = Color.rgb(33, 150, 243)
        canvas.drawText("VỊ TRÍ ĐỨNG", img.left + z.stand.cx * iw - 80f, img.top + z.stand.cy * ih + 9f, zoneTextPaint)
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        drawZones(canvas)
        if (items.isEmpty()) return
        val tStart = System.nanoTime()

        val img = imageRect()
        val scaleX = img.width() / sourceWidth
        val scaleY = img.height() / sourceHeight
        val offX = img.left; val offY = img.top

        for (item in items) {
            val scaledRect = RectF(
                offX + item.rect.left * scaleX, offY + item.rect.top * scaleY,
                offX + item.rect.right * scaleX, offY + item.rect.bottom * scaleY
            )
            boxPaint.color = item.color
            canvas.drawRect(scaledRect, boxPaint)

            for (p in item.landmarks) {
                canvas.drawCircle(offX + p.x * scaleX, offY + p.y * scaleY, 4f, dotPaint)
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
