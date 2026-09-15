package com.eduxplore.control.ui;

import android.content.Context;
import android.graphics.drawable.GradientDrawable;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.PopupWindow;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.List;

/** Factory helpers cho các mảnh UI lặp lại (chip, avatar tròn, thanh bar-cell) — Android View
 *  thuần, dựng bằng code thay vì XML vì phần lớn nội dung sinh động theo dữ liệu (giống cách
 *  buildGameList() cũ đã làm trong ControlActivity). */
public final class UiUtil {
    private UiUtil() {}

    public static int dp(Context ctx, float value) {
        return Math.round(value * ctx.getResources().getDisplayMetrics().density);
    }

    public static int sp(Context ctx, float value) {
        return Math.round(value * ctx.getResources().getDisplayMetrics().scaledDensity);
    }

    public static GradientDrawable pill(int bgColor, int strokeColor, float strokeWidthDp, Context ctx) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(bgColor);
        d.setCornerRadius(dp(ctx, 20));
        if (strokeColor != 0) d.setStroke(dp(ctx, strokeWidthDp), strokeColor);
        return d;
    }

    public static GradientDrawable roundedRect(int bgColor, float radiusDp, int strokeColor, float strokeWidthDp, Context ctx) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(bgColor);
        d.setCornerRadius(dp(ctx, radiusDp));
        if (strokeColor != 0) d.setStroke(dp(ctx, strokeWidthDp), strokeColor);
        return d;
    }

    public static GradientDrawable circle(int bgColor, int strokeColor, float strokeWidthDp, Context ctx) {
        GradientDrawable d = new GradientDrawable();
        d.setShape(GradientDrawable.OVAL);
        d.setColor(bgColor);
        if (strokeColor != 0) d.setStroke(dp(ctx, strokeWidthDp), strokeColor);
        return d;
    }

    /** Avatar tròn với chữ cái đầu — dùng cho tên học sinh. */
    public static TextView makeAvatar(Context ctx, String initials, float sizeDp) {
        TextView tv = new TextView(ctx);
        tv.setText(initials);
        tv.setTextColor(ContextColor(ctx, com.eduxplore.control.R.color.text_dim));
        tv.setTextSize(10f);
        tv.setTypeface(tv.getTypeface(), android.graphics.Typeface.BOLD);
        tv.setGravity(Gravity.CENTER);
        tv.setBackground(circle(ContextColor(ctx, com.eduxplore.control.R.color.panel2), 0, 0, ctx));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(dp(ctx, sizeDp), dp(ctx, sizeDp));
        tv.setLayoutParams(lp);
        return tv;
    }

    /** 1 hàng bar-cell (track + fill tỉ lệ value% + số) — dùng trong bảng năng lực Lớp học. */
    public static LinearLayout makeBarCell(Context ctx, int value, boolean isMin) {
        LinearLayout row = new LinearLayout(ctx);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout track = new LinearLayout(ctx);
        track.setOrientation(LinearLayout.HORIZONTAL);
        track.setBackground(roundedRect(ContextColor(ctx, com.eduxplore.control.R.color.idle_dim), 3, 0, 0, ctx));
        LinearLayout.LayoutParams trackLp = new LinearLayout.LayoutParams(0, dp(ctx, 6), 1f);
        track.setLayoutParams(trackLp);

        View fill = new View(ctx);
        fill.setBackground(roundedRect(isMin ? ContextColor(ctx, com.eduxplore.control.R.color.bad)
                : ContextColor(ctx, com.eduxplore.control.R.color.accent), 3, 0, 0, ctx));
        int v = Math.max(0, Math.min(100, value));
        LinearLayout.LayoutParams fillLp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, v);
        fill.setLayoutParams(fillLp);
        track.addView(fill);
        if (v < 100) {
            View rest = new View(ctx);
            LinearLayout.LayoutParams restLp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 100 - v);
            rest.setLayoutParams(restLp);
            track.addView(rest);
        }

        TextView val = new TextView(ctx);
        val.setText(String.valueOf(value));
        val.setTextSize(11f);
        val.setTextColor(ContextColor(ctx, com.eduxplore.control.R.color.text_dim));
        LinearLayout.LayoutParams valLp = new LinearLayout.LayoutParams(dp(ctx, 22), LinearLayout.LayoutParams.WRAP_CONTENT);
        valLp.gravity = Gravity.END;
        valLp.leftMargin = dp(ctx, 6);
        val.setLayoutParams(valLp);

        row.addView(track);
        row.addView(val);
        return row;
    }

    public static TextView label(Context ctx, String text, float sizeSp, int colorRes, boolean bold) {
        TextView tv = new TextView(ctx);
        tv.setText(text);
        tv.setTextSize(sizeSp);
        tv.setTextColor(ContextColor(ctx, colorRes));
        if (bold) tv.setTypeface(tv.getTypeface(), android.graphics.Typeface.BOLD);
        return tv;
    }

    public static int ContextColor(Context ctx, int colorRes) {
        return ctx.getResources().getColor(colorRes);
    }

    public interface OnPick { void onPick(int index); }

    /** Dropdown xổ xuống dưới `anchor` — dùng cho chọn Môn học / Lớp (thay lưới chip nhiều
     *  dòng/thanh cuộn ngang, tốn diện tích trên màn 1024x600). */
    public static void showDropdown(Context ctx, View anchor, List<String> labels, int selectedIndex,
                                     int selectedBg, OnPick onPick) {
        LinearLayout content = new LinearLayout(ctx);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setBackground(roundedRect(ContextColor(ctx, com.eduxplore.control.R.color.panel), 10,
                ContextColor(ctx, com.eduxplore.control.R.color.border), 1, ctx));
        content.setPadding(dp(ctx, 6), dp(ctx, 6), dp(ctx, 6), dp(ctx, 6));

        ScrollView scroll = new ScrollView(ctx);
        scroll.addView(content);

        final PopupWindow popup = new PopupWindow(scroll, dp(ctx, 200), dp(ctx, Math.min(260, 44 * labels.size() + 12)), true);
        popup.setElevation(dp(ctx, 12));

        for (int i = 0; i < labels.size(); i++) {
            final int idx = i;
            TextView item = new TextView(ctx);
            item.setText(labels.get(i));
            item.setTextSize(12.5f);
            item.setTypeface(item.getTypeface(), android.graphics.Typeface.BOLD);
            item.setPadding(dp(ctx, 10), dp(ctx, 9), dp(ctx, 10), dp(ctx, 9));
            boolean sel = i == selectedIndex;
            item.setTextColor(ContextColor(ctx, sel ? com.eduxplore.control.R.color.text : com.eduxplore.control.R.color.text_dim));
            if (sel) item.setBackground(roundedRect(selectedBg, 6, 0, 0, ctx));
            item.setOnClickListener(v -> { popup.dismiss(); onPick.onPick(idx); });
            content.addView(item);
        }

        popup.showAsDropDown(anchor, 0, dp(ctx, 6));
    }
}
