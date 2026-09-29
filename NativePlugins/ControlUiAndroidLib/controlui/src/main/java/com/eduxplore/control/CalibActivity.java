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
 * mục tiêu/xác nhận nằm bên đó.
 *
 * 2 CHẾ ĐỘ — giáo viên chọn tuỳ số trụ xốp sẵn có:
 * - "5 trụ (nhanh)": đặt đủ 5 trụ cùng lúc rồi đo 1 lần (CalibSceneController.BeginCapture).
 * - "1 trụ (từng điểm)": chỉ có 1 trụ — di chuyển trụ tuần tự qua từng vị trí, mỗi lần bấm
 *   "Đo điểm này" chỉ đo ĐÚNG 1 điểm rồi tự chuyển sang điểm kế tiếp
 *   (CalibSceneController.BeginSequential/CaptureSequentialStep).
 */
public class CalibActivity extends Activity {

    private static final String TAG = "CalibActivity";
    private static final String UNITY_PLAYER_ACTIVITY_CLASS = "com.unity3d.player.UnityPlayerActivity";
    private static final String CALIB_SCENE_NAME = "CalibScene";
    private static final String UNITY_GAME_OBJECT = "CalibControlBridge";

    private enum Mode { NONE, BATCH, SEQUENTIAL }

    private static CalibActivity sInstance;

    private TextView statusText;
    private Button modeBatchBtn, modeSequentialBtn, actionBtn, backBtn, saveBtn;
    private CountDownTimer countdownTimer;

    private Mode mode = Mode.NONE;
    private boolean sequentialDone = false; // đã đo đủ 5/5 điểm của phiên tuần tự (chờ Lưu hoặc làm lại từ đầu)

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
                "Có 5 trụ: chọn \"Calib 5 trụ (nhanh)\" — đặt cả 5 vào 4 góc + giữa rồi đo 1 lần.\n" +
                "Chỉ có 1 trụ: chọn \"Calib từng điểm\" — di chuyển trụ lần lượt qua từng vòng tròn, " +
                "mỗi lần đặt xong quay lại bấm \"Đo điểm này\".\n" +
                "Xem kết quả trên máy chiếu — chấm đỏ trùng vòng vàng là tốt, bấm \"Lưu\".",
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
        statusText = UiUtil.label(this, "Sẵn sàng — chọn chế độ calib bên dưới.", 16f, R.color.text, false);
        statusCard.addView(statusText);
        LinearLayout.LayoutParams statusLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        statusLp.bottomMargin = UiUtil.dp(this, 20);
        statusCard.setLayoutParams(statusLp);
        root.addView(statusCard);

        // Hàng 1: chọn chế độ — ẩn đi sau khi đã chọn 1 trong 2.
        LinearLayout modeRow = new LinearLayout(this);
        modeRow.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams modeRowLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        modeRowLp.bottomMargin = UiUtil.dp(this, 12);
        modeRow.setLayoutParams(modeRowLp);

        modeBatchBtn = makeButton("Calib 5 trụ (nhanh)", R.color.accent);
        modeBatchBtn.setOnClickListener(v -> onModeBatchClicked());
        modeRow.addView(modeBatchBtn, buttonLp());

        modeSequentialBtn = makeButton("Calib từng điểm (1 trụ)", R.color.live);
        modeSequentialBtn.setOnClickListener(v -> onModeSequentialClicked());
        LinearLayout.LayoutParams seqLp = buttonLp();
        seqLp.leftMargin = UiUtil.dp(this, 12);
        modeRow.addView(modeSequentialBtn, seqLp);

        root.addView(modeRow);

        // Hàng 2: hành động trong lúc đang calib — ẩn cho tới khi đã chọn chế độ.
        LinearLayout actionRow = new LinearLayout(this);
        actionRow.setOrientation(LinearLayout.HORIZONTAL);
        actionRow.setVisibility(View.GONE);

        actionBtn = makeButton("", R.color.accent);
        actionBtn.setOnClickListener(v -> onActionClicked());
        actionRow.addView(actionBtn, buttonLp());

        backBtn = makeButton("Lùi lại", R.color.idle);
        backBtn.setVisibility(View.GONE); // chỉ hiện ở chế độ tuần tự
        backBtn.setOnClickListener(v -> sendToUnity("OnStepBackRequested", ""));
        LinearLayout.LayoutParams backLp = buttonLp();
        backLp.leftMargin = UiUtil.dp(this, 12);
        actionRow.addView(backBtn, backLp);

        this.actionRow = actionRow;
        root.addView(actionRow);

        // Hàng ứng viên — dựng động khi Unity báo phát hiện NHIỀU vật cùng lúc (vd tường + trụ),
        // 1 nút cho mỗi số đã đánh trên máy chiếu. Ẩn hoàn toàn lúc bình thường.
        candidateRow = new LinearLayout(this);
        candidateRow.setOrientation(LinearLayout.HORIZONTAL);
        candidateRow.setVisibility(View.GONE);
        LinearLayout.LayoutParams candidateRowLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        candidateRowLp.topMargin = UiUtil.dp(this, 8);
        candidateRow.setLayoutParams(candidateRowLp);
        root.addView(candidateRow);

        LinearLayout btnRow = new LinearLayout(this);
        btnRow.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams btnRowLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        btnRowLp.topMargin = UiUtil.dp(this, 12);
        btnRow.setLayoutParams(btnRowLp);

        saveBtn = makeButton("Lưu", R.color.good);
        saveBtn.setEnabled(false);
        saveBtn.setOnClickListener(v -> onSaveClicked());
        btnRow.addView(saveBtn, buttonLp());

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

    private LinearLayout actionRow;
    private LinearLayout candidateRow;

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

    private void onModeBatchClicked() {
        mode = Mode.BATCH;
        modeRowVisible(false);
        actionRow.setVisibility(View.VISIBLE);
        backBtn.setVisibility(View.GONE);
        actionBtn.setText("Bắt đầu calib");
        actionBtn.setEnabled(true);
        saveBtn.setEnabled(false);
        statusText.setText("Đang đo...");
        sendToUnity("OnStartCalibRequested", "");
    }

    private void onModeSequentialClicked() {
        mode = Mode.SEQUENTIAL;
        sequentialDone = false;
        modeRowVisible(false);
        actionRow.setVisibility(View.VISIBLE);
        backBtn.setVisibility(View.VISIBLE);
        backBtn.setEnabled(false);
        actionBtn.setText("Đo điểm này");
        actionBtn.setEnabled(false); // bật lại khi Unity báo đã sẵn sàng ở điểm đầu tiên (OnSequentialStep)
        saveBtn.setEnabled(false);
        statusText.setText("Đang chuẩn bị...");
        sendToUnity("OnStartSequentialRequested", "");
    }

    private void modeRowVisible(boolean visible) {
        int v = visible ? View.VISIBLE : View.GONE;
        modeBatchBtn.setVisibility(v);
        modeSequentialBtn.setVisibility(v);
    }

    private void onActionClicked() {
        if (mode == Mode.BATCH) {
            actionBtn.setEnabled(false);
            saveBtn.setEnabled(false);
            statusText.setText("Đang đo...");
            sendToUnity("OnStartCalibRequested", "");
        } else if (mode == Mode.SEQUENTIAL) {
            if (sequentialDone) {
                // "Làm lại từ đầu" — huỷ kết quả cũ, bắt đầu lại phiên tuần tự từ điểm 1.
                sequentialDone = false;
                backBtn.setEnabled(false);
                saveBtn.setEnabled(false);
                actionBtn.setEnabled(false);
                statusText.setText("Đang chuẩn bị...");
                sendToUnity("OnStartSequentialRequested", "");
            } else {
                actionBtn.setEnabled(false);
                backBtn.setEnabled(false);
                sendToUnity("OnCaptureStepRequested", "");
            }
        }
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

    /// Kết quả CUỐI của cả phiên (sau khi đã giải affine xong) — dùng chung cho cả 2 chế độ.
    public static void OnCalibResult(boolean success, int found, int expected, float maxResidualPx, String failReason) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> {
            if (a.mode == Mode.SEQUENTIAL) {
                a.sequentialDone = true;
                a.backBtn.setVisibility(View.GONE);
                a.actionBtn.setText("Làm lại từ đầu");
            } else {
                a.actionBtn.setText("Bắt đầu calib lại");
            }
            a.actionBtn.setEnabled(true);
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

    /// Chỉ dùng ở chế độ tuần tự — Unity vừa chuyển sang chờ đo điểm thứ index (0-based).
    public static void OnSequentialStep(int index, int total, String role, String roleLabel) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> {
            a.actionBtn.setText("Đo điểm " + (index + 1) + "/" + total);
            a.actionBtn.setEnabled(true);
            a.backBtn.setEnabled(index > 0);
            a.statusText.setText("Điểm " + (index + 1) + "/" + total + ": " + roleLabel +
                    " — đặt trụ vào vòng tròn đang chiếu, giữ nguyên rồi bấm \"" + a.actionBtn.getText() + "\".");
        });
    }

    /// Kết quả đo 1 ĐIỂM ĐƠN trong phiên tuần tự — khác OnCalibResult (kết quả CUỐI cả phiên).
    public static void OnSequentialStepResult(boolean success, String failReason) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> {
            if (!success) {
                a.actionBtn.setEnabled(true); // cho bấm lại đúng điểm đang đo
                a.backBtn.setEnabled(true);
                a.statusText.setText("Chưa đạt: " + failReason + " — bấm lại để đo lại điểm này.");
            }
            // success=true: chờ OnSequentialStep tiếp theo (hoặc OnCalibResult nếu vừa xong điểm cuối)
            // tự cập nhật UI — không cần làm gì thêm ở đây.
        });
    }

    /// Phát hiện NHIỀU vật cùng lúc trong vùng quét (vd tường + trụ) — không có cách tự động
    /// phân biệt đáng tin (xem lịch sử bug ở LidarTouchBridge.StartSinglePointCapture), chuyển
    /// cho giáo viên tự chọn. encodedCounts: "n1;n2;n3;..." — số điểm/cụm từng ứng viên, thứ tự
    /// khớp đúng số đã đánh trên máy chiếu (DrawCandidateMarkers). Dựng 1 nút cho mỗi ứng viên.
    public static void OnCandidatesFound(String encodedCounts) {
        CalibActivity a = sInstance;
        if (a == null) return;
        a.runOnUiThread(() -> a.showCandidatePicker(encodedCounts));
    }

    private void showCandidatePicker(String encodedCounts) {
        actionRow.setVisibility(View.GONE);
        candidateRow.removeAllViews();

        String[] counts = encodedCounts.split(";");
        for (int i = 0; i < counts.length; i++) {
            final int index = i;
            Button btn = makeButton("Số " + (i + 1) + " (" + counts[i] + " điểm)", R.color.live);
            btn.setOnClickListener(v -> onCandidateChosen(index));
            LinearLayout.LayoutParams lp = buttonLp();
            if (i > 0) lp.leftMargin = UiUtil.dp(this, 8);
            candidateRow.addView(btn, lp);
        }
        candidateRow.setVisibility(View.VISIBLE);
        statusText.setText("Phát hiện " + counts.length + " vật trong vùng quét — xem số hiện trên sàn (máy chiếu), " +
                "chọn ĐÚNG số của trụ.");
    }

    private void onCandidateChosen(int index) {
        candidateRow.setVisibility(View.GONE);
        actionRow.setVisibility(View.VISIBLE);
        sendToUnity("OnCandidateChosen", String.valueOf(index));
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
