package com.eduxplore.control;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.os.CountDownTimer;
import android.util.Log;
import android.view.Display;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import com.eduxplore.control.ui.UiUtil;

import java.lang.reflect.Method;

/**
 * "Calib" — icon riêng trong Launcher thật của K02 (ô lưới "Calib khi có", xem CLAUDE.md), TÁCH
 * RIÊNG khỏi ControlActivity (không lồng trong menu chọn game) — đúng yêu cầu "1 icon riêng".
 *
 * Vai trò giống hệt ControlActivity ở chỗ "entry point native trên display 0, tự khởi động
 * Unity trên display phụ" — nhưng KHÔNG dùng chung code/Activity với ControlActivity để tránh
 * đụng vào luồng Start/Pause/Stop đã test kỹ trên máy thật (xem CalibControlBridge.cs cho lý do
 * đầy đủ). Unity chạy scene "CalibScene" (Assets/Game/Scripts/UIScripts/Calib/), toàn bộ UI vẽ
 * mục tiêu/xác nhận nằm bên đó — Activity này chỉ có 3 nút: Bắt đầu calib / Lưu / Thoát + 1 dòng
 * trạng thái, vì giáo viên đang đứng CẠNH TABLET lúc bấm (đã đặt xong 5 trụ trên sàn, quay lại).
 */
public class CalibActivity extends Activity {

    private static final String TAG = "CalibActivity";
    private static final String UNITY_PLAYER_ACTIVITY_CLASS = "com.unity3d.player.UnityPlayerActivity";
    private static final String CALIB_SCENE_NAME = "CalibScene";
    private static final String UNITY_GAME_OBJECT = "CalibControlBridge";

    private static CalibActivity sInstance;

    private TextView statusText;
    private Button startBtn, saveBtn;
    private CountDownTimer countdownTimer;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sInstance = this;
        setImmersive();
        buildUi();
        startUnityOnSecondaryDisplay();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) setImmersive();
    }

    private void setImmersive() {
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    @Override
    public void onBackPressed() {
        onExitClicked();
    }

    // ── Khởi động Unity trên display phụ — bản rút gọn của
    // ControlActivity.findSecondaryDisplay()/onStartClicked(), KHÔNG dùng chung code (xem
    // javadoc đầu file). Luôn khởi động ngay lúc mở Activity (khác ControlActivity phải chờ
    // giáo viên chọn game trước) vì Calib chỉ có đúng 1 "scene" cố định. ──────────────────────
    private Display findSecondaryDisplay() {
        DisplayManager dm = (DisplayManager) getSystemService(DISPLAY_SERVICE);
        if (dm == null) return null;
        for (Display d : dm.getDisplays()) {
            if (d.getDisplayId() == Display.DEFAULT_DISPLAY) continue;
            if ((d.getFlags() & Display.FLAG_PRESENTATION) != 0) return d;
        }
        return null;
    }

    private void startUnityOnSecondaryDisplay() {
        Display secondary = findSecondaryDisplay();
        Intent intent = new Intent();
        intent.setClassName(getPackageName(), UNITY_PLAYER_ACTIVITY_CLASS);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        intent.putExtra(ControlActivity.EXTRA_SCENE_NAME, CALIB_SCENE_NAME);
        intent.putExtra(ControlActivity.EXTRA_GAME_NAME, (String) null);

        if (secondary == null) {
            Log.i(TAG, "startUnityOnSecondaryDisplay: không có display phụ — chạy display 0 (fallback 1-display, chỉ dùng lúc dev)");
            startActivity(intent);
            return;
        }
        try {
            Bundle options = ActivityOptions.makeBasic().setLaunchDisplayId(secondary.getDisplayId()).toBundle();
            startActivity(intent, options);
        } catch (SecurityException e) {
            Log.e(TAG, "startUnityOnSecondaryDisplay: SecurityException khi setLaunchDisplayId — " + e.getMessage(), e);
            startActivity(intent);
        }
    }

    // ── UI — 100% code, cùng phong cách ClassManagementActivity/ControlActivity (nền sáng,
    // token màu trong colors.xml của module này). ──────────────────────────────────────────
    private void buildUi() {
        int pad = UiUtil.dp(this, 24);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(pad, pad, pad, pad);
        root.setBackgroundColor(UiUtil.ContextColor(this, R.color.bg));

        root.addView(UiUtil.label(this, "Hiệu chỉnh vùng tương tác", 24f, R.color.text, true));

        TextView instructions = UiUtil.label(this,
                "Bước 1: Đặt 5 trụ xốp tròn vào đúng 5 vòng tròn vàng đang chiếu trên sàn (4 góc + giữa).\n" +
                "Bước 2: Quay lại đây, bấm \"Bắt đầu calib\" — giữ nguyên trụ, không di chuyển trong lúc đo.\n" +
                "Bước 3: Xem kết quả trên máy chiếu — chấm đỏ trùng vòng vàng là tốt, bấm \"Lưu\".",
                15f, R.color.text_dim, false);
        LinearLayout.LayoutParams instrLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        instrLp.topMargin = UiUtil.dp(this, 12);
        instrLp.bottomMargin = UiUtil.dp(this, 20);
        instructions.setLayoutParams(instrLp);
        root.addView(instructions);

        LinearLayout statusCard = new LinearLayout(this);
        statusCard.setOrientation(LinearLayout.VERTICAL);
        statusCard.setPadding(UiUtil.dp(this, 16), UiUtil.dp(this, 16), UiUtil.dp(this, 16), UiUtil.dp(this, 16));
        statusCard.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, R.color.panel), 12,
                UiUtil.ContextColor(this, R.color.border), 1, this));
        statusText = UiUtil.label(this, "Sẵn sàng — bấm \"Bắt đầu calib\" khi đã đặt xong 5 trụ.", 16f, R.color.text, false);
        statusCard.addView(statusText);
        LinearLayout.LayoutParams statusLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        statusLp.bottomMargin = UiUtil.dp(this, 20);
        statusCard.setLayoutParams(statusLp);
        root.addView(statusCard);

        LinearLayout btnRow = new LinearLayout(this);
        btnRow.setOrientation(LinearLayout.HORIZONTAL);

        startBtn = makeButton("Bắt đầu calib", R.color.accent);
        startBtn.setOnClickListener(v -> onStartClicked());
        btnRow.addView(startBtn, buttonLp());

        saveBtn = makeButton("Lưu", R.color.good);
        saveBtn.setEnabled(false);
        saveBtn.setOnClickListener(v -> onSaveClicked());
        LinearLayout.LayoutParams saveLp = buttonLp();
        saveLp.leftMargin = UiUtil.dp(this, 12);
        btnRow.addView(saveBtn, saveLp);

        Button exitBtn = makeButton("Thoát", R.color.idle);
        exitBtn.setOnClickListener(v -> onExitClicked());
        LinearLayout.LayoutParams exitLp = buttonLp();
        exitLp.leftMargin = UiUtil.dp(this, 12);
        btnRow.addView(exitBtn, exitLp);

        root.addView(btnRow);

        ScrollView scroll = new ScrollView(this);
        scroll.addView(root);
        setContentView(scroll);
    }

    private Button makeButton(String text, int colorRes) {
        Button b = new Button(this);
        b.setText(text);
        b.setAllCaps(false);
        b.setTextSize(16f);
        b.setTextColor(UiUtil.ContextColor(this, R.color.panel));
        b.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, colorRes), 10, 0, 0, this));
        b.setPadding(UiUtil.dp(this, 8), UiUtil.dp(this, 14), UiUtil.dp(this, 8), UiUtil.dp(this, 14));
        return b;
    }

    private LinearLayout.LayoutParams buttonLp() {
        return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
    }

    // ── Nút bấm — gửi lệnh sang Unity qua CalibControlBridge (xem javadoc đầu file) ──────────

    private void onStartClicked() {
        startBtn.setEnabled(false);
        saveBtn.setEnabled(false);
        statusText.setText("Đang đo...");
        sendToUnity("OnStartCalibRequested", "");
    }

    private void onSaveClicked() {
        sendToUnity("OnSaveRequested", "");
    }

    private void onExitClicked() {
        sendToUnity("OnExitRequested", "");
        // KHÔNG finish/destroy UnityPlayerActivity — Unity tự kill() cả process khi Activity
        // của nó bị destroy (xem CLAUDE.md "Stop/Start — KHÔNG destroy UnityPlayerActivity").
        // Chỉ thoát Activity của MÌNH, giống backToMenu() của ControlActivity.
        finish();
    }

    private static void sendToUnity(String method, String message) {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            Method m = unityPlayerClass.getMethod("UnitySendMessage", String.class, String.class, String.class);
            m.invoke(null, UNITY_GAME_OBJECT, method, message);
        } catch (Exception e) {
            Log.e(TAG, "sendToUnity(" + method + ") failed: " + e);
        }
    }

    // ── Gọi từ Unity (CalibControlBridge.PushXxx) — static, resolve qua UnitySendMessage
    // reflection nên PHẢI đúng chữ ký (kiểu tham số) khớp với phía Java gọi qua CallStatic. ────

    public static void OnCalibListening(int durationMs) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> a.startCountdown(durationMs));
    }

    public static void OnCalibResult(boolean success, int found, int expected, float maxResidualPx, String failReason) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> {
            a.startBtn.setEnabled(true);
            a.startBtn.setText("Bắt đầu calib lại");
            if (success) {
                a.saveBtn.setEnabled(true);
                a.statusText.setText("Đã đo xong (" + found + "/" + expected + " điểm, sai số tối đa " +
                        Math.round(maxResidualPx) + "px). Xem máy chiếu để xác nhận rồi bấm Lưu.");
            } else {
                a.saveBtn.setEnabled(false);
                a.statusText.setText("Chưa đạt (" + found + "/" + expected + " điểm): " + failReason);
            }
        });
    }

    public static void OnCalibSaved() {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> {
            a.saveBtn.setEnabled(false);
            a.statusText.setText("Đã lưu calib thành công. Có thể dọn trụ khỏi sàn rồi bấm Thoát.");
        });
    }

    private void startCountdown(int durationMs) {
        if (countdownTimer != null) countdownTimer.cancel();
        countdownTimer = new CountDownTimer(durationMs, 200) {
            @Override public void onTick(long millisUntilFinished) {
                statusText.setText("Đang đo... còn " + (millisUntilFinished / 1000f) + "s — giữ nguyên trụ.");
            }
            @Override public void onFinish() {
                statusText.setText("Đang xử lý kết quả...");
            }
        }.start();
    }

    @Override
    protected void onDestroy() {
        if (sInstance == this) sInstance = null;
        if (countdownTimer != null) countdownTimer.cancel();
        super.onDestroy();
    }
}
