package com.eduxplore.control.ui;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.text.Layout;
import android.widget.TextView;

/** TextView chữ có viền ngoài (vd chữ đen viền trắng cho tên game trên nền). Viền vẽ TRƯỚC, thân chữ vẽ
 *  đè lên nên chỉ nửa ngoài của nét viền lộ ra — không ăn vào thân chữ (giống paint-order: stroke fill). */
public class OutlinedTextView extends TextView {
    /** Bề dày nét viền theo cỡ chữ; 0.35 ≈ mức "dày (12)" trên bản xem thử (chữ 34 → nét 12). */
    private static final float STROKE_PER_TEXT_SIZE = 0.35f;

    private int outlineColor = 0xFFFFFFFF;

    public OutlinedTextView(Context ctx) {
        super(ctx);
        updatePad();
    }

    public void setOutlineColor(int color) { outlineColor = color; invalidate(); }

    @Override
    public void setTextSize(float sp) {
        super.setTextSize(sp);
        updatePad();
    }

    /** Chừa chỗ cho nửa ngoài của nét viền để không bị cắt ở mép view. */
    private void updatePad() {
        int pad = Math.round(getTextSize() * STROKE_PER_TEXT_SIZE / 2f);
        setPadding(pad, pad, pad, pad);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        Layout layout = getLayout();
        if (layout != null) {
            Paint p = getPaint();
            int savedColor = p.getColor();
            canvas.save();
            canvas.translate(getCompoundPaddingLeft(), getExtendedPaddingTop());
            p.setStyle(Paint.Style.STROKE);
            p.setStrokeJoin(Paint.Join.ROUND);
            p.setStrokeWidth(getTextSize() * STROKE_PER_TEXT_SIZE);
            p.setColor(outlineColor);
            layout.draw(canvas);
            p.setStyle(Paint.Style.FILL);
            p.setStrokeWidth(0f);
            p.setColor(savedColor);
            canvas.restore();
        }
        super.onDraw(canvas); // thân chữ (TextView tự đặt lại màu chữ)
    }
}
