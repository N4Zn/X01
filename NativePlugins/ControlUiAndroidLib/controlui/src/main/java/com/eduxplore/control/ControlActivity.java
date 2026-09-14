package com.eduxplore.control;

import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.util.Log;
import android.view.Display;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Entry point mới (HOME/launcher) — thay UnityPlayerActivity.
 *
 * UI dựng bằng Android View thuần qua XML layout (activity_control.xml) — KHÔNG dùng
 * WebView: đã xác nhận thực tế trên K02, Chromium không khởi tạo được GL context
 * ("FATAL: gpu::InitializeGLThreadSafe() failed"), lỗi driver GPU tầng hệ thống không sửa
 * được từ app. Android View dùng chung con đường render mà Unity đã chạy ổn định.
 *
 * 2 "màn hình" (menu chọn game / control lúc đang chơi) là 2 View con trong CÙNG Activity,
 * chuyển qua lại bằng visibility — không phải 2 Activity, để giữ state đơn giản.
 *
 * Không phụ thuộc biên dịch vào class Unity nào (com.unity3d.player.*) — dùng
 * Intent.setClassName() với string thay vì class reference (module .aar này được
 * unityLibrary include, không phải ngược lại, nên không chắc thấy class đó lúc compile).
 */
public class ControlActivity extends Activity {

    private static final String TAG = "ControlActivity";
    private static final String UNITY_PLAYER_ACTIVITY_CLASS = "com.unity3d.player.UnityPlayerActivity";

    /** Đọc ở phía Unity (ControlBridge.cs) qua Intent.getStringExtra() lúc khởi động. */
    public static final String EXTRA_SCENE_NAME = "com.eduxplore.control.SCENE_NAME";
    /** "name" (variant key) trong GameRegistry.GameEntry — nhiều game dùng CHUNG 1 scene
     *  nhưng khác bộ câu hỏi CSV theo key này (vd TestTongHopGame). BẮT BUỘC gửi kèm scene,
     *  nếu không QuestionPool sẽ không biết load CSV nào — xem ControlBridge.cs. */
    public static final String EXTRA_GAME_NAME = "com.eduxplore.control.GAME_NAME";

    private static final String GAME_REGISTRY_ASSET = "game_registry.json";

    /** name hiển thị/variant key (GameRegistry.GameEntry.name) + sceneName — đọc từ
     *  assets/game_registry.json, xuất tay từ GameRegistry.cs (Unity/C#, nguồn sự thật).
     *  ControlActivity chạy TRƯỚC khi Unity khởi động nên không đọc trực tiếp GameRegistry
     *  được — file JSON này cần cập nhật lại thủ công nếu GameRegistry.cs đổi danh sách game. */
    private static class GameItem {
        final String name;
        final String sceneName;
        GameItem(String name, String sceneName) { this.name = name; this.sceneName = sceneName; }
    }
    private final List<GameItem> games = new ArrayList<>();

    // GameObject Unity nhận lệnh Pause/Resume — xem GameControlBridge.cs (phải trùng tên).
    private static final String UNITY_GAME_OBJECT = "GameControlBridge";

    // Unity gọi ngược vào đây (UpdateReport, static) để cập nhật report — cần biết instance
    // ĐANG hiển thị vì Unity không có cách nào lấy Context/Activity của ControlActivity
    // (khác display/task với UnityPlayerActivity, UnityPlayer.currentActivity trả về chính
    // UnityPlayerActivity, không phải ControlActivity).
    private static ControlActivity sInstance;

    private View menuScreen, controlScreen;
    private TextView playingGameLabel, reportLabel;
    private Button startButton, pauseButton, stopButton;
    private String selectedScene = null;
    private String selectedGameName = null;
    private boolean paused = false;

    // true sau lần Start ĐẦU TIÊN — UnityPlayerActivity từ đó luôn sống nguyên (Stop không
    // còn destroy nó nữa, xem onStopClicked()), nên các lần Start SAU chỉ cần gửi lệnh nạp
    // game mới qua UnitySendMessage thay vì startActivity() lại từ đầu (Intent extra chỉ đọc
    // được 1 lần lúc cold-boot — xem ControlBridge.cs bên Unity).
    private boolean unityStarted = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sInstance = this;
        setImmersive();
        setContentView(R.layout.activity_control);

        menuScreen = findViewById(R.id.menu_screen);
        controlScreen = findViewById(R.id.control_screen);
        startButton = findViewById(R.id.start_button);
        playingGameLabel = findViewById(R.id.playing_game_label);
        reportLabel = findViewById(R.id.report_label);
        pauseButton = findViewById(R.id.pause_button);
        stopButton = findViewById(R.id.stop_button);

        loadGameRegistry();
        buildGameList();
        startButton.setOnClickListener(v -> onStartClicked());
        pauseButton.setOnClickListener(v -> onPauseClicked());
        stopButton.setOnClickListener(v -> onStopClicked());

        // Xem TaskRemovedWatcherService — dọn sạch cả 2 display khi 1 trong 2 task (Control/
        // Unity, khác display) bị user tắt qua Recents.
        startService(new Intent(this, TaskRemovedWatcherService.class));

        // WatchdogService (process riêng ":watchdog") — tự khởi động lại ControlActivity nếu
        // process chính chết bất ngờ (vd Unity tự kill() cả process lúc UnityPlayerActivity
        // destroy — bug "Stop thoát cả app"). TẠM TẮT theo yêu cầu user (chưa muốn auto-start
        // lại lúc test) — bật lại bằng cách bỏ comment dòng dưới khi cần.
        // startService(new Intent(this, WatchdogService.class));
    }

    /** Đọc assets/game_registry.json — export tay từ GameRegistry.cs (xem ghi chú ở field
     *  games). Lỗi/rỗng → games rỗng, màn Menu không có nút nào (không crash). */
    private void loadGameRegistry() {
        try (InputStream is = getAssets().open(GAME_REGISTRY_ASSET)) {
            BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8));
            StringBuilder sb = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) sb.append(line);

            JSONArray arr = new JSONArray(sb.toString());
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.getJSONObject(i);
                games.add(new GameItem(o.getString("name"), o.getString("sceneName")));
            }
            Log.i(TAG, "loadGameRegistry: đọc được " + games.size() + " game");
        } catch (Exception e) {
            Log.e(TAG, "loadGameRegistry lỗi: " + e.getMessage(), e);
        }
    }

    private void buildGameList() {
        LinearLayout gameList = findViewById(R.id.game_list);
        Button[] buttons = new Button[games.size()];
        for (int i = 0; i < games.size(); i++) {
            Button b = new Button(this);
            b.setText(games.get(i).name);
            b.setTextSize(18f);
            b.setAllCaps(false);
            b.setAlpha(0.5f);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.bottomMargin = 12;
            b.setLayoutParams(lp);
            final int idx = i;
            b.setOnClickListener(v -> selectGame(idx, buttons));
            gameList.addView(b);
            buttons[i] = b;
        }
    }

    private void selectGame(int idx, Button[] buttons) {
        GameItem item = games.get(idx);
        selectedScene = item.sceneName;
        selectedGameName = item.name;
        for (int i = 0; i < buttons.length; i++) {
            buttons[i].setAlpha(i == idx ? 1f : 0.5f);
        }
        startButton.setEnabled(true);
        Log.i(TAG, "selectGame: name=" + selectedGameName + " scene=" + selectedScene);
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

    /** ControlActivity là "home" — Back không thoát, giống LauncherBehaviour bên Unity. */
    @Override
    public void onBackPressed() {
        Log.i(TAG, "onBackPressed — ignored (ControlActivity is home)");
    }

    // -------------------------------------------------------------------
    // Tìm display phụ (máy chiếu) — ưu tiên display có FLAG_PRESENTATION, không phải
    // display mặc định (id 0). displayId KHÔNG ổn định qua các lần cắm/rút HDMI (đã xác
    // nhận thực tế ở giai đoạn phân tích) — luôn tra cứu lại lúc cần, không cache lâu dài.
    // -------------------------------------------------------------------
    private Display findSecondaryDisplay() {
        DisplayManager dm = (DisplayManager) getSystemService(DISPLAY_SERVICE);
        if (dm == null) return null;
        for (Display d : dm.getDisplays()) {
            if (d.getDisplayId() == Display.DEFAULT_DISPLAY) continue;
            if ((d.getFlags() & Display.FLAG_PRESENTATION) != 0) return d;
        }
        return null;
    }

    private void onStartClicked() {
        if (selectedScene == null) return;

        if (!unityStarted) {
            // Lần Start ĐẦU TIÊN — chưa có UnityPlayerActivity nào sống, phải khởi động thật.
            // Đọc scene/game qua Intent extra lúc cold-boot (ControlBridge.cs, chỉ đọc được 1 lần).
            Display secondary = findSecondaryDisplay();
            Intent intent = new Intent();
            intent.setClassName(getPackageName(), UNITY_PLAYER_ACTIVITY_CLASS);
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            intent.putExtra(EXTRA_SCENE_NAME, selectedScene);
            intent.putExtra(EXTRA_GAME_NAME, selectedGameName);

            if (secondary == null) {
                Log.i(TAG, "onStartClicked: không có display phụ — chạy display 0 (fallback 1-display)");
                startActivity(intent);
            } else {
                try {
                    Bundle options = ActivityOptions.makeBasic().setLaunchDisplayId(secondary.getDisplayId()).toBundle();
                    Log.i(TAG, "onStartClicked: khởi động UnityPlayerActivity lần đầu, displayId=" + secondary.getDisplayId()
                            + " scene=" + selectedScene);
                    startActivity(intent, options);
                } catch (SecurityException e) {
                    Log.e(TAG, "onStartClicked: SecurityException khi setLaunchDisplayId — " + e.getMessage(), e);
                    startActivity(intent);
                }
            }
            unityStarted = true;
        } else {
            // UnityPlayerActivity đã sống sẵn từ lần chơi trước (Stop không destroy nó nữa) —
            // chỉ cần lệnh nạp game mới, không khởi động lại Activity. Xem
            // GameControlBridge.OnLoadGameRequested (Unity) — payload "scene|gameName".
            Log.i(TAG, "onStartClicked: Unity đã sống sẵn — gửi lệnh nạp game mới scene=" + selectedScene);
            sendToUnity("OnLoadGameRequested", selectedScene + "|" + (selectedGameName != null ? selectedGameName : ""));
        }

        playingGameLabel.setText("Đang chơi: " + selectedGameName);
        reportLabel.setText("Thời gian: — \n— : —đ   ·   — : —đ");
        paused = false;
        pauseButton.setText("PAUSE");
        menuScreen.setVisibility(View.GONE);
        controlScreen.setVisibility(View.VISIBLE);
    }

    private void onPauseClicked() {
        paused = !paused;
        pauseButton.setText(paused ? "PLAY" : "PAUSE");
        sendToUnity(paused ? "OnPauseRequested" : "OnResumeRequested", "");
        Log.i(TAG, "onPauseClicked → paused=" + paused);
    }

    private void onStopClicked() {
        // KHÔNG destroy UnityPlayerActivity nữa (khác finishUnityTask() cũ đã xoá) — Unity tự
        // kill() cả process dùng chung khi Activity của nó bị destroy, hành vi engine không
        // sửa được từ code app (xem lịch sử bug "Stop thoát cả app"). Chỉ báo Unity dừng game
        // + che đen display máy chiếu — xem GameControlBridge.OnStopRequested.
        if (paused) sendToUnity("OnResumeRequested", ""); // tránh treo IsPaused cho lần chơi sau
        sendToUnity("OnStopRequested", "");
        backToMenu();
    }

    /** Quay lại màn Menu (chọn game) — dùng chung cho Stop (bấm tay) và OnGameEnded (game tự
     *  hết giờ/hết vòng, gọi từ Unity — xem GameControlBridge.PushGameEnded). */
    private void backToMenu() {
        menuScreen.setVisibility(View.VISIBLE);
        controlScreen.setVisibility(View.GONE);
    }

    // Gọi UnityPlayer.UnitySendMessage() qua reflection — tránh phụ thuộc biên dịch trực
    // tiếp vào com.unity3d.player.UnityPlayer (module .aar này được unityLibrary include,
    // không phải ngược lại, nên không chắc thấy class đó lúc compile; ở runtime thì luôn
    // có, cùng 1 ClassLoader trong APK). Giống hệt cách LidarUsbBridge.java đã làm.
    private static void sendToUnity(String method, String message) {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            Method m = unityPlayerClass.getMethod("UnitySendMessage", String.class, String.class, String.class);
            m.invoke(null, UNITY_GAME_OBJECT, method, message);
        } catch (Exception e) {
            Log.e(TAG, "sendToUnity(" + method + ") failed: " + e);
        }
    }

    /** Gọi từ Unity (GameControlBridge.PushReport, qua AndroidJavaClass.CallStatic — luôn
     *  reflection nên không có vấn đề phụ thuộc biên dịch chiều nào) để cập nhật report.
     *  Có thể gọi từ thread không phải main thread của Unity → phải runOnUiThread.
     *  leftName/rightName lấy từ GameSessionManager.GetDisplayName1/2 (Unity) — tên do
     *  PlayerRecognitionService nhận diện được, cập nhật liên tục trong ván. */
    public static void UpdateReport(String timeText, String leftName, String leftScoreText,
                                     String rightName, String rightScoreText) {
        ControlActivity activity = sInstance;
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            if (activity.reportLabel != null) {
                activity.reportLabel.setText("Thời gian: " + timeText + "\n"
                        + leftName + ": " + leftScoreText + "đ   ·   "
                        + rightName + ": " + rightScoreText + "đ");
            }
        });
    }

    /** Gọi từ Unity (GameControlBridge.PushGameEnded, lúc GameOver — hết giờ/hết vòng tự
     *  nhiên, KHÔNG phải do bấm Stop) — tự quay Menu để chọn game tiếp theo, coi như hết 1
     *  round, không cần user bấm Stop thủ công. Màn hình máy chiếu (display phụ) KHÔNG bị
     *  đụng tới — vẫn giữ nguyên hiển thị ScoreScene/kết quả game vừa xong bên đó, đúng ý đồ. */
    public static void OnGameEnded() {
        ControlActivity activity = sInstance;
        if (activity == null) return;
        activity.runOnUiThread(activity::backToMenu);
    }

    @Override
    protected void onDestroy() {
        if (sInstance == this) sInstance = null;
        super.onDestroy();
    }
}
