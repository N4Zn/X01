package com.eduxplore.control;

import android.app.Activity;
import android.app.ActivityOptions;
import android.app.Presentation;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.util.Log;
import android.view.Display;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.TextView;
import android.text.Editable;
import android.text.TextWatcher;
import android.text.InputType;

import com.eduxplore.control.ui.ClassRepo;
import com.eduxplore.control.ui.ScoreStore;
import com.eduxplore.control.ui.SettingsStore;
import com.eduxplore.control.ui.UiUtil;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Entry point (HOME/launcher) — thay UnityPlayerActivity.
 *
 * UI dựng bằng Android View thuần (KHÔNG WebView — Chromium không init được GL context trên
 * K02, xem ghi chú cũ). Bố cục 4 vùng cố định (top-left Môn học / top-right Lớp / action-zone
 * trái nhỏ / report-zone phải to) x 3 tiến trình (chọn / đang chơi / hết game) — xem
 * activity_control.xml cho khung, các hàm render* bên dưới dựng nội dung động theo state,
 * cùng ý tưởng với bản mockup HTML đã duyệt với giáo viên.
 *
 * Dữ liệu game/môn học lấy THẬT từ assets/game_registry.json (xuất tay từ GameRegistry.cs).
 * Lớp/học sinh lấy THẬT từ "Quản lý lớp" (ClassRepo — /sdcard/EduXplore/classes.json + enrolled.json).
 * Điểm/lịch sử là kết quả chơi thật (ScoreStore): hết mỗi ván ghi số câu đúng / số câu đã chơi của
 * từng người; điểm 1 học phần = Σ đúng / Σ đã chơi trong học phần đó × 100. Luồng Start/Pause/Stop/Unity 100% thật.
 */
public class ControlActivity extends Activity {

    private static final String TAG = "ControlActivity";
    private static final String UNITY_PLAYER_ACTIVITY_CLASS = "com.unity3d.player.UnityPlayerActivity";

    public static final String EXTRA_SCENE_NAME = "com.eduxplore.control.SCENE_NAME";
    public static final String EXTRA_GAME_NAME = "com.eduxplore.control.GAME_NAME";
    public static final String EXTRA_SETTINGS_JSON = "com.eduxplore.control.SETTINGS_JSON";

    private static final String GAME_REGISTRY_ASSET = "game_registry.json";
    private static final String UNITY_GAME_OBJECT = "GameControlBridge";

    private static ControlActivity sInstance;

    private static class GameItem {
        final String name, displayName, group, sceneName;
        final int category;
        GameItem(String name, String displayName, String group, String sceneName, int category) {
            this.name = name; this.displayName = displayName; this.group = group;
            this.sceneName = sceneName; this.category = category;
        }
        /** "" (chưa có scene thật) = game placeholder, hiện trong danh sách nhưng không bấm
         *  chạy được — xem GameRegistry.cs's IsImplemented, cùng quy ước "sceneName rỗng". */
        boolean isImplemented() { return sceneName != null && !sceneName.isEmpty(); }
    }

    private String[] categoryNames = new String[0];
    private final Map<Integer, List<GameItem>> gamesByCategory = new LinkedHashMap<>();
    private final List<String> allGameNames = new ArrayList<>();
    // name (định danh nội bộ, dùng để chọn/gửi Unity) -> displayName (tiếng Việt hiện lên UI).
    private final Map<String, String> displayNameByName = new java.util.HashMap<>();
    // "Học phần" của 1 game = nhóm chủ đề (group, vd "So sánh số") nếu có, không có group thì lấy tên môn.
    private final Map<String, String> phanByGame = new java.util.HashMap<>();
    private final Map<String, Integer> phanCategory = new java.util.HashMap<>(); // học phần -> chỉ số môn

    /** Tên hiển thị tiếng Việt cho 1 game, theo đúng định danh nội bộ (selectedGameName, ...).
     *  Không tìm thấy (game lạ/JSON thiếu) → hiện tạm chính định danh đó thay vì rỗng. */
    private String displayNameOf(String internalName) {
        if (internalName == null) return null;
        String d = displayNameByName.get(internalName);
        return d != null ? d : internalName;
    }

    // ── Views ────────────────────────────────────────────────────────────────
    private TextView categoryTrigger, classTrigger;
    private FrameLayout actionZone;
    private LinearLayout reportTabs;
    private LinearLayout panelRoster, panelCompetency, panelLive, panelSummary;
    private View panelHistory; // ScrollView trong XML — chỉ cần setVisibility, không cần LinearLayout
    private TextView gamePreviewStrip, notPlayedStrip;
    private LinearLayout rosterHeader, rosterBody, compChips, compScoreList, historyBody;
    private TextView compName, compFlags;
    private LinearLayout liveSideLeft, liveSideRight;
    private LinearLayout summaryCard;
    private ScrollView panelSettings;
    private LinearLayout settingsBody;

    // ── State ────────────────────────────────────────────────────────────────
    private String scene = "select"; // select | playing | ended
    private int domain = 0;
    private String classKey; // tên lớp đang chọn (null = chưa có lớp nào)
    private ClassRepo.Snapshot roster = new ClassRepo.Snapshot();
    private ScoreStore scoreStore;
    private SettingsStore settingsStore;
    private String selectedScene = null;      // sceneName Unity thật (Start dùng cái này)
    private String selectedGameName = null;   // tên game/variant đang chọn hoặc vừa chơi
    /** Danh sách game được dựng lại mỗi renderAll() (ScrollView mới) → nhớ vị trí cuộn để chọn game
     *  ở dưới không bị kéo về đầu. Đổi môn thì reset về 0. */
    private ScrollView gameListScroll = null;
    private int gameListScrollY = 0;
    private int compStudentIndex = 0;
    private String rosterSortKey = "score";
    /** Ô chọn kiểu điểm ở tab Lớp học (chạm để đổi vòng): 0 = Tất cả các môn, 1 = Môn đang chọn (mặc định),
     *  2 = Phần (học phần) của game đang chọn — kiểu 2 chỉ có khi đã chọn game. */
    private int rosterScope = 1;
    private TextView scopeBtn;
    private long sessionId = 0;               // mốc thời gian lần Start hiện tại — gom các round thành 1 ván
    private String historyStudent;            // học sinh đang xem ở tab Lịch sử (ClassRepo.Student.name)
    private final java.util.Set<Long> openSessions = new java.util.HashSet<>();
    private final java.util.concurrent.ExecutorService scoreExec = java.util.concurrent.Executors.newSingleThreadExecutor();
    private int rosterSortDir = -1;
    private boolean paused = false;
    /** Thời lượng ván cho màn Tổng kết: từ lúc bấm Start đến lúc kết thúc, trừ thời gian Pause. 0 = chưa có. */
    private long gameStartMs = 0, gameEndMs = 0, pausedAtMs = 0, pausedAccumMs = 0;
    private boolean unityStarted = false;
    // Group nào đang thu gọn trong danh sách chọn game (vd "Đếm", "Cộng") — mặc định tất cả
    // đang mở (set rỗng = không group nào bị collapse).
    private final java.util.Set<String> collapsedGroups = new java.util.HashSet<>();

    // Logo màn hình phụ (máy chiếu) lúc chưa chọn/chạy game — dùng Presentation (Dialog cho
    // 1 Display cụ thể), KHÔNG phải 1 Activity riêng: nhẹ hơn nhiều (không tốn task/back-stack/
    // khai báo manifest), API chính chủ Android dành đúng cho việc hiển thị nội dung phụ trên
    // display thứ 2 từ trong app đang chạy. Đóng lại đúng lúc Start lần đầu để Unity chiếm chỗ.
    private Presentation logoPresentation;

    // Report trực tiếp từ Unity (GameControlBridge.PushReport) — dùng cho panel_live.
    private volatile String liveLeftName = "—", liveRightName = "—", liveLeftScore = "0", liveRightScore = "0", liveTime = "—";

    // Breakdown từng người chơi THẬT trong ván đang chạy (GameControlBridge.PushPlayerBreakdown,
    // cùng nhịp 1s với PushReport) — thay cho roster mock cũ
    // vốn là danh sách CẢ LỚP với số liệu random, không liên quan ván đang chơi.
    private volatile List<LivePlayer> liveLeftPlayers = new ArrayList<>();
    private volatile List<LivePlayer> liveRightPlayers = new ArrayList<>();

    private static class LivePlayer {
        String name = "?";
        int correct, answered;
        float avgTime, avgCorrectTime;
    }

    // 2026-09-21: ĐÃ REVERT toàn bộ "xin quyền Camera+Lidar ngay lúc mở app" — thử thêm
    // requestPermissions(CAMERA) làm dòng ĐẦU TIÊN trong onCreate() (trước setContentView())
    // gây màn hình đen hoàn toàn ở CẢ 2 display, xác nhận qua phép thử A/B thực tế (cài lại bản
    // cũ không có đoạn này → hết đen, cài bản có đoạn này → đen lại, cùng 1 máy vừa reboot).
    // Chưa rõ cơ chế chính xác (nghi vấn: request permission quá sớm — trước setContentView(),
    // trước khi window/Presentation display phụ kịp dựng — làm hỏng luồng khởi tạo màn hình kép
    // tuỳ biến của K02), nhưng bằng chứng đủ rõ để revert ngay, không cần hiểu hết nguyên nhân
    // mới được phép an toàn trở lại. Camera vẫn được xin bình thường ở MainActivity
    // (FaceEnrollAndroidLib) như thiết kế gốc — chỉ mất phần "xin sớm ngay lúc mở app".

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sInstance = this;
        setImmersive();
        setContentView(R.layout.activity_control);

        categoryTrigger = findViewById(R.id.category_trigger);
        classTrigger = findViewById(R.id.class_trigger);
        actionZone = findViewById(R.id.action_zone);
        reportTabs = findViewById(R.id.report_tabs);
        panelRoster = findViewById(R.id.panel_roster);
        panelCompetency = findViewById(R.id.panel_competency);
        panelHistory = findViewById(R.id.panel_history);
        panelLive = findViewById(R.id.panel_live);
        panelSummary = findViewById(R.id.panel_summary);
        panelSettings = (ScrollView) findViewById(R.id.panel_settings);
        settingsBody = (LinearLayout) panelSettings.getChildAt(0);
        gamePreviewStrip = findViewById(R.id.game_preview_strip);
        notPlayedStrip = findViewById(R.id.notplayed_strip);
        rosterHeader = findViewById(R.id.roster_header);
        rosterBody = findViewById(R.id.roster_body);
        compChips = findViewById(R.id.comp_chips);
        compName = findViewById(R.id.comp_name);
        compFlags = findViewById(R.id.comp_flags);
        compScoreList = findViewById(R.id.comp_score_list);
        historyBody = findViewById(R.id.history_body);
        liveSideLeft = findViewById(R.id.live_side_left);
        liveSideRight = findViewById(R.id.live_side_right);
        summaryCard = findViewById(R.id.summary_card);

        loadGameRegistry();
        scoreStore = new ScoreStore(getExternalFilesDir(null));
        settingsStore = new SettingsStore(getExternalFilesDir(null));
        settingsStore.load();
        ((TextView) findViewById(R.id.comp_note)).setText(
                "Điểm học phần = số round đúng / số round đã chơi, tính trên 50 round gần nhất (thang 100). "
                + "Điểm môn = trung bình các học phần đã chơi; môn/học phần chưa chơi không tính vào trung bình.");

        // Ô chọn kiểu điểm của bảng học sinh: chạm 1 lần đổi sang kiểu kế tiếp (Tất cả → Môn → Phần → Tất cả ...).
        scopeBtn = new TextView(this);
        scopeBtn.setTextSize(13f);
        scopeBtn.setTypeface(scopeBtn.getTypeface(), android.graphics.Typeface.BOLD);
        scopeBtn.setTextColor(UiUtil.ContextColor(this, R.color.accent));
        scopeBtn.setGravity(Gravity.CENTER_VERTICAL);
        scopeBtn.setPadding(UiUtil.dp(this, 14), UiUtil.dp(this, 8), UiUtil.dp(this, 14), UiUtil.dp(this, 8));
        scopeBtn.setBackground(UiUtil.pill(UiUtil.ContextColor(this, R.color.accent_dim), UiUtil.ContextColor(this, R.color.accent), 1, this));
        LinearLayout.LayoutParams scopeLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        scopeLp.setMargins(UiUtil.dp(this, 12), UiUtil.dp(this, 10), UiUtil.dp(this, 12), UiUtil.dp(this, 4)); // cùng lề với các strip/header bên dưới
        scopeBtn.setLayoutParams(scopeLp);
        scopeBtn.setOnClickListener(v -> {
            int next = (rosterScope + 1) % 3;
            if (next == 2 && !phanScopeAvailable()) next = 0;
            rosterScope = next;
            renderRoster();
        });
        panelRoster.addView(scopeBtn, 0);

        buildReportTabs();
        renderAll(); // lớp/điểm nạp ở luồng nền trong onResume() rồi vẽ lại
        showLogoOnSecondaryDisplay();

        startService(new Intent(this, TaskRemovedWatcherService.class));
        // WatchdogService — tắt theo yêu cầu user lúc test, xem CLAUDE.md Track A.
        // startService(new Intent(this, WatchdogService.class));
    }

    /** Hiện logo EduXplore full-screen trên display phụ (máy chiếu) ngay lúc mở app, trước khi
     *  cô giáo chọn/chạy mini game nào — thay vì màn đen. Đóng lại đúng lúc Start lần đầu (xem
     *  onStartClicked) để Unity chiếm màn hình đó. Không có display phụ → bỏ qua, không lỗi. */
    private void showLogoOnSecondaryDisplay() {
        Display secondary = findSecondaryDisplay();
        if (secondary == null) return;
        try {
            ImageView iv = new ImageView(this);
            // logo_eduxplore_splash (KHÔNG phải logo_eduxplore) — đổi tên có chủ đích: trước đó
            // trùng tên với FaceEnrollAndroidLib's logo_eduxplore.png (bản NỀN TRONG SUỐT, dùng
            // cho mục đích khác trong module đó), 2 module cùng khai báo 1 resource name → lúc
            // Unity gộp hết các Android library module vào 1 app cuối cùng, symbol
            // R.drawable.logo_eduxplore bị TRÙNG, module nào merge sau thắng — hoá ra không phải
            // bản navy của ControlUiAndroidLib, mà là bản trong suốt của FaceEnrollAndroidLib,
            // khiến sửa file bao nhiêu lần cũng không thấy hiệu lực trên máy thật (đã xác nhận
            // qua test K02: sửa file/xoá alpha ở ĐÂY không đổi gì cả, vì app đang render file
            // KHÁC). Đổi tên để không còn đụng độ resource với module khác nữa.
            iv.setImageResource(R.drawable.logo_eduxplore_splash);
            iv.setScaleType(ImageView.ScaleType.CENTER_CROP);
            iv.setLayoutParams(new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            logoPresentation = new Presentation(this, secondary);
            logoPresentation.setContentView(iv);
            logoPresentation.show();
        } catch (Exception e) {
            Log.e(TAG, "showLogoOnSecondaryDisplay lỗi: " + e.getMessage(), e);
            logoPresentation = null;
        }
    }

    private void dismissLogoPresentation() {
        if (logoPresentation == null) return;
        try { logoPresentation.dismiss(); } catch (Exception ignored) { }
        logoPresentation = null;
    }

    // ── Đọc game_registry.json (categoryNames + games[{name,sceneName,category}]) ──────────
    private void loadGameRegistry() {
        try (InputStream is = getAssets().open(GAME_REGISTRY_ASSET)) {
            BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8));
            StringBuilder sb = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) sb.append(line);

            JSONObject root = new JSONObject(sb.toString());
            JSONArray catArr = root.getJSONArray("categoryNames");
            categoryNames = new String[catArr.length()];
            for (int i = 0; i < catArr.length(); i++) categoryNames[i] = catArr.getString(i);

            JSONArray gamesArr = root.getJSONArray("games");
            for (int i = 0; i < gamesArr.length(); i++) {
                JSONObject o = gamesArr.getJSONObject(i);
                String displayName = o.has("displayName") ? o.getString("displayName") : o.getString("name");
                String group = o.has("group") ? o.getString("group") : "";
                GameItem item = new GameItem(o.getString("name"), displayName, group, o.getString("sceneName"), o.getInt("category"));
                List<GameItem> bucket = gamesByCategory.get(item.category);
                if (bucket == null) { bucket = new ArrayList<>(); gamesByCategory.put(item.category, bucket); }
                bucket.add(item);
                allGameNames.add(item.name);
                displayNameByName.put(item.name, item.displayName);
                String phan = !group.isEmpty() ? group
                        : (item.category >= 0 && item.category < categoryNames.length ? categoryNames[item.category] : "Khác");
                phanByGame.put(item.name, phan);
                if (!phanCategory.containsKey(phan)) phanCategory.put(phan, item.category);
            }
            Log.i(TAG, "loadGameRegistry: " + categoryNames.length + " môn, " + allGameNames.size() + " game");
        } catch (Exception e) {
            Log.e(TAG, "loadGameRegistry lỗi: " + e.getMessage(), e);
            categoryNames = new String[]{"Khác"};
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        reloadRosterAsync();
    }

    /** Nạp lại lớp/học sinh + điểm đã lưu (giáo viên có thể vừa sửa lớp ở "Quản lý lớp" rồi quay lại). Đọc file
     *  ở luồng nền (enrolled.json có thể nặng), xong mới vẽ lại — đang chơi thì không vẽ chen vào màn live. */
    private void reloadRosterAsync() {
        new Thread(() -> {
            ClassRepo.Snapshot snap = ClassRepo.load();
            scoreStore.load();
            runOnUiThread(() -> {
                roster = snap;
                if (classKey == null || !roster.classes.contains(classKey))
                    classKey = roster.classes.isEmpty() ? null : roster.classes.get(0);
                if (!"playing".equals(scene)) renderAll();
            });
        }, "roster-load").start();
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
        Log.i(TAG, "onBackPressed — ignored (ControlActivity is home)");
    }

    // =========================================================================================
    // RENDER — cùng cấu trúc render*() như bản mockup HTML đã duyệt.
    // =========================================================================================

    private void renderAll() {
        renderTopBar();
        renderActionZone();
        renderReportZone();
    }

    private void renderTopBar() {
        categoryTrigger.setText(categoryNames.length > domain ? categoryNames[domain] : "—");
        categoryTrigger.setOnClickListener(v -> {
            List<String> labels = java.util.Arrays.asList(categoryNames);
            UiUtil.showDropdown(this, categoryTrigger, labels, domain,
                    UiUtil.ContextColor(this, R.color.accent_dim), idx -> {
                        domain = idx; selectedGameName = null; selectedScene = null;
                        gameListScrollY = 0;
                        renderAll();
                    });
        });

        findViewById(R.id.category_box).setOnClickListener(v -> categoryTrigger.performClick());
        findViewById(R.id.class_box).setOnClickListener(v -> classTrigger.performClick());

        List<String> classLabels = roster.classes;
        int classIdx = classKey == null ? -1 : classLabels.indexOf(classKey);
        if (classIdx < 0) {
            classTrigger.setText("—");
            classTrigger.setOnClickListener(null);
        } else {
            // Trường "Lớp" đã có label riêng rồi nên giá trị chỉ cần tên ngắn (Mầm/Chồi/Lá), không
            // lặp lại chữ "Lớp" trong value.
            String classLabel = classLabels.get(classIdx);
            classTrigger.setText(classLabel.startsWith("Lớp ") ? classLabel.substring(4) : classLabel);
            final int fClassIdx = classIdx;
            classTrigger.setOnClickListener(v -> UiUtil.showDropdown(this, classTrigger, classLabels, fClassIdx,
                    UiUtil.ContextColor(this, R.color.live_dim), idx -> {
                        classKey = classLabels.get(idx); compStudentIndex = 0;
                        renderAll();
                    }));
        }
    }

    // ── ACTION ZONE (trái, nhỏ) — nội dung đổi theo scene ───────────────────────────────────
    private void renderActionZone() {
        if (gameListScroll != null) gameListScrollY = gameListScroll.getScrollY();
        gameListScroll = null;
        actionZone.removeAllViews();
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setLayoutParams(new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        actionZone.addView(col);

        if ("select".equals(scene)) {
            LinearLayout eyebrowRow = new LinearLayout(this);
            eyebrowRow.setOrientation(LinearLayout.HORIZONTAL);
            eyebrowRow.addView(UiUtil.label(this, "CHỌN MINI GAME", 10f, R.color.text_faint, true));
            col.addView(eyebrowRow);

            ScrollView scroll = new ScrollView(this);
            LinearLayout.LayoutParams scrollLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f);
            scrollLp.topMargin = UiUtil.dp(this, 10);
            scrollLp.bottomMargin = UiUtil.dp(this, 10);
            scroll.setLayoutParams(scrollLp);
            LinearLayout list = new LinearLayout(this);
            list.setOrientation(LinearLayout.VERTICAL);
            scroll.addView(list);
            col.addView(scroll);
            gameListScroll = scroll;
            final int restoreY = gameListScrollY;
            if (restoreY > 0) {
                // Chờ layout xong (nội dung chưa đo lúc này) rồi mới cuộn về chỗ cũ, 1 lần.
                scroll.getViewTreeObserver().addOnPreDrawListener(new android.view.ViewTreeObserver.OnPreDrawListener() {
                    @Override public boolean onPreDraw() {
                        scroll.getViewTreeObserver().removeOnPreDrawListener(this);
                        scroll.scrollTo(0, restoreY);
                        return true;
                    }
                });
            }

            List<GameItem> items = gamesByCategory.get(domain);
            if (items == null || items.isEmpty()) {
                list.addView(UiUtil.label(this, "Chưa có mini game cho môn này.", 12.5f, R.color.text_faint, false));
            } else {
                // Gom theo group (giữ thứ tự xuất hiện đầu tiên của mỗi group trong danh sách
                // gốc — 1 group có thể có phần tử không liền kề nhau, vẫn gộp về đúng 1 mục).
                // Game không có group (group rỗng) hiện phẳng như cũ, không bọc trong mục nào.
                Map<String, List<GameItem>> groupBuckets = new LinkedHashMap<>();
                List<Object> sections = new ArrayList<>(); // phần tử: GameItem (phẳng) hoặc String (tên group)
                for (GameItem item : items) {
                    if (item.group == null || item.group.isEmpty()) {
                        sections.add(item);
                    } else {
                        if (!groupBuckets.containsKey(item.group)) {
                            groupBuckets.put(item.group, new ArrayList<>());
                            sections.add(item.group);
                        }
                        groupBuckets.get(item.group).add(item);
                    }
                }
                for (Object section : sections) {
                    if (section instanceof GameItem) {
                        list.addView(buildGameRow((GameItem) section, 0));
                    } else {
                        String groupName = (String) section;
                        List<GameItem> children = groupBuckets.get(groupName);
                        boolean expanded = !collapsedGroups.contains(groupName);
                        list.addView(buildGroupHeader(groupName, children.size(), expanded));
                        if (expanded) {
                            for (GameItem child : children) list.addView(buildGameRow(child, 14));
                        }
                    }
                }
            }

            TextView startBtn = new TextView(this);
            startBtn.setText("START");
            startBtn.setGravity(Gravity.CENTER);
            startBtn.setTextSize(13f);
            startBtn.setTypeface(startBtn.getTypeface(), android.graphics.Typeface.BOLD);
            boolean canStart = selectedGameName != null && selectedScene != null && !selectedScene.isEmpty();
            startBtn.setEnabled(canStart);
            startBtn.setAlpha(canStart ? 1f : 0.4f);
            startBtn.setTextColor(0xFF0B1710);
            startBtn.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, R.color.good), 9, 0, 0, this));
            startBtn.setPadding(0, UiUtil.dp(this, 13), 0, UiUtil.dp(this, 13));
            startBtn.setOnClickListener(v -> { if (canStart) onStartClicked(); });
            col.addView(startBtn);
        } else if ("playing".equals(scene)) {
            col.addView(UiUtil.label(this, "ĐANG CHẠY", 10f, R.color.text_faint, true));

            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, R.color.panel2), 10, UiUtil.ContextColor(this, R.color.border_soft), 1, this));
            card.setPadding(UiUtil.dp(this, 13), UiUtil.dp(this, 12), UiUtil.dp(this, 13), UiUtil.dp(this, 12));
            LinearLayout.LayoutParams cardLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            cardLp.topMargin = UiUtil.dp(this, 10);
            card.setLayoutParams(cardLp);
            card.addView(UiUtil.label(this, selectedGameName != null ? displayNameOf(selectedGameName) : "—", 16f, R.color.text, true));
            card.addView(UiUtil.label(this, "Đang chơi", 12f, R.color.text_dim, false));
            col.addView(card);

            View spacer = new View(this);
            spacer.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
            col.addView(spacer);

            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            TextView pauseBtn = simpleButton(paused ? "PLAY" : "PAUSE", R.color.panel2, R.color.text);
            TextView stopBtn = simpleButton("STOP", R.color.bad_dim, R.color.bad);
            LinearLayout.LayoutParams halfLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            halfLp.rightMargin = UiUtil.dp(this, 4);
            pauseBtn.setLayoutParams(halfLp);
            LinearLayout.LayoutParams halfLp2 = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            halfLp2.leftMargin = UiUtil.dp(this, 4);
            stopBtn.setLayoutParams(halfLp2);
            pauseBtn.setOnClickListener(v -> onPauseClicked());
            stopBtn.setOnClickListener(v -> onStopClicked());
            row.addView(pauseBtn);
            row.addView(stopBtn);
            col.addView(row);
        } else { // ended
            col.addView(UiUtil.label(this, "VỪA XONG", 10f, R.color.text_faint, true));

            LinearLayout banner = new LinearLayout(this);
            banner.setOrientation(LinearLayout.VERTICAL);
            banner.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, R.color.panel2), 10, UiUtil.ContextColor(this, R.color.border_soft), 1, this));
            banner.setPadding(UiUtil.dp(this, 13), UiUtil.dp(this, 12), UiUtil.dp(this, 13), UiUtil.dp(this, 12));
            LinearLayout.LayoutParams bannerLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            bannerLp.topMargin = UiUtil.dp(this, 10);
            banner.setLayoutParams(bannerLp);
            banner.addView(UiUtil.label(this, "Vừa chơi", 10f, R.color.text_faint, true));
            banner.addView(UiUtil.label(this, selectedGameName != null ? displayNameOf(selectedGameName) : "—", 15f, R.color.text, true));
            // "Đội trái/phải" chứ không phải liveLeftName/liveRightName — đó là tên NGƯỜI CUỐI
            // CÙNG được nhận diện, không đại diện được cho cả đội (đã sửa cùng lý do ở
            // renderSummary() — xem breakdown đầy đủ từng người ở nút "TỔNG KẾT" bên dưới).
            banner.addView(UiUtil.label(this, "Đội trái " + liveLeftScore + " – " + liveRightScore + " Đội phải", 11.5f, R.color.text_dim, false));
            col.addView(banner);

            View spacer = new View(this);
            spacer.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
            col.addView(spacer);

            TextView replayBtn = simpleButton("CHƠI LẠI", R.color.good, R.color.bg);
            replayBtn.setTextColor(0xFF0B1710);
            replayBtn.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
            replayBtn.setOnClickListener(v -> onStartClicked());
            col.addView(replayBtn);

            TextView otherBtn = simpleButton("CHỌN GAME KHÁC", R.color.panel2, R.color.text);
            LinearLayout.LayoutParams otherLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            otherLp.topMargin = UiUtil.dp(this, 8);
            otherBtn.setLayoutParams(otherLp);
            otherBtn.setOnClickListener(v -> { scene = "select"; selectedGameName = null; selectedScene = null; renderAll(); });
            col.addView(otherBtn);

            TextView sumBtn = simpleButton("TỔNG KẾT", R.color.panel2, R.color.text);
            LinearLayout.LayoutParams sumLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            sumLp.topMargin = UiUtil.dp(this, 8);
            sumBtn.setLayoutParams(sumLp);
            sumBtn.setOnClickListener(v -> showReportView("summary"));
            col.addView(sumBtn);
        }
    }

    /** 1 hàng game trong danh sách chọn — dùng chung cho game đứng riêng (indentDp=0) và game
     *  nằm trong 1 group (indentDp>0, thụt vào cho thấy quan hệ cha-con với header nhóm). Game
     *  chưa có scene thật (isImplemented()==false, placeholder) vẫn chọn/tô sáng được như bình
     *  thường — chỉ nút START bị khoá (xem canStart ở renderActionZone) nên bấm không chạy gì. */
    private TextView buildGameRow(GameItem item, int indentDp) {
        TextView row = new TextView(this);
        row.setText(item.displayName);
        row.setTextSize(13f);
        boolean sel = item.name.equals(selectedGameName);
        int baseColor = item.isImplemented() ? R.color.text_dim : R.color.text_faint;
        row.setTextColor(UiUtil.ContextColor(this, sel ? R.color.text : baseColor));
        row.setPadding(UiUtil.dp(this, 11 + indentDp), UiUtil.dp(this, 9), UiUtil.dp(this, 11), UiUtil.dp(this, 9));
        row.setBackground(sel
                ? UiUtil.roundedRect(UiUtil.ContextColor(this, R.color.accent_dim), 8, UiUtil.ContextColor(this, R.color.accent), 1, this)
                : UiUtil.roundedRect(android.graphics.Color.TRANSPARENT, 8, 0, 0, this));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = UiUtil.dp(this, 6);
        row.setLayoutParams(lp);
        row.setOnClickListener(v -> {
            selectedGameName = item.name;
            selectedScene = item.sceneName;
            renderAll();
        });
        return row;
    }

    /** Tiêu đề 1 nhóm chủ đề (vd "Đếm", "Cộng") — bấm để thu gọn/mở rộng (collapsedGroups). */
    private TextView buildGroupHeader(String groupName, int count, boolean expanded) {
        TextView header = new TextView(this);
        header.setText((expanded ? "▾ " : "▸ ") + groupName + " (" + count + ")");
        header.setTextSize(12.5f);
        header.setTypeface(header.getTypeface(), android.graphics.Typeface.BOLD);
        header.setTextColor(UiUtil.ContextColor(this, R.color.text));
        header.setPadding(UiUtil.dp(this, 8), UiUtil.dp(this, 9), UiUtil.dp(this, 8), UiUtil.dp(this, 9));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = UiUtil.dp(this, 4);
        header.setLayoutParams(lp);
        header.setOnClickListener(v -> {
            if (collapsedGroups.contains(groupName)) collapsedGroups.remove(groupName);
            else collapsedGroups.add(groupName);
            renderAll();
        });
        return header;
    }

    private TextView simpleButton(String text, int bgColorRes, int textColorRes) {
        TextView btn = new TextView(this);
        btn.setText(text);
        btn.setGravity(Gravity.CENTER);
        btn.setTextSize(13f);
        btn.setTypeface(btn.getTypeface(), android.graphics.Typeface.BOLD);
        btn.setTextColor(UiUtil.ContextColor(this, textColorRes));
        btn.setBackground(UiUtil.roundedRect(UiUtil.ContextColor(this, bgColorRes), 9, 0, 0, this));
        btn.setPadding(0, UiUtil.dp(this, 13), 0, UiUtil.dp(this, 13));
        return btn;
    }

    // ── REPORT ZONE (phải, to) ───────────────────────────────────────────────────────────────
    private String reportTab = "roster"; // roster | competency | history | summary

    private void buildReportTabs() {
        String[] tabs = {"roster:Lớp học", "competency:Năng lực", "history:Lịch sử", "settings:Cài đặt"};
        for (String t : tabs) {
            String[] p = t.split(":");
            TextView tab = new TextView(this);
            tab.setText(p[1]);
            tab.setTextSize(13f);
            tab.setTypeface(tab.getTypeface(), android.graphics.Typeface.BOLD);
            tab.setPadding(UiUtil.dp(this, 14), UiUtil.dp(this, 12), UiUtil.dp(this, 14), UiUtil.dp(this, 12));
            tab.setTag(p[0]);
            tab.setOnClickListener(v -> showReportView((String) v.getTag()));
            reportTabs.addView(tab);
        }
    }

    private void showReportView(String view) {
        reportTab = view;
        reportTabs.setVisibility("playing".equals(scene) ? View.GONE : View.VISIBLE);
        for (int i = 0; i < reportTabs.getChildCount(); i++) {
            TextView tab = (TextView) reportTabs.getChildAt(i);
            boolean active = tab.getTag().equals(view);
            tab.setTextColor(UiUtil.ContextColor(this, active ? R.color.text : R.color.text_faint));
        }
        panelRoster.setVisibility("roster".equals(view) ? View.VISIBLE : View.GONE);
        panelCompetency.setVisibility("competency".equals(view) ? View.VISIBLE : View.GONE);
        panelHistory.setVisibility("history".equals(view) ? View.VISIBLE : View.GONE);
        panelLive.setVisibility("live".equals(view) ? View.VISIBLE : View.GONE);
        panelSummary.setVisibility("summary".equals(view) ? View.VISIBLE : View.GONE);
        panelSettings.setVisibility("settings".equals(view) ? View.VISIBLE : View.GONE);
        if ("summary".equals(view)) renderSummary();
        if ("settings".equals(view)) renderSettings();
    }

    private void renderReportZone() {
        if ("playing".equals(scene)) { showReportView("live"); renderLive(); return; }
        showReportView(reportTab.equals("live") || reportTab.equals("summary") ? "roster" : reportTab);
        renderRoster();
        renderCompetencyChips();
        renderHistory();
    }

    // ── Số liệu học sinh THẬT (ClassRepo = lớp/học sinh, ScoreStore = kết quả chơi) ───────────
    private static class Row {
        final ClassRepo.Student s;
        final Map<String, int[]> phan; // học phần -> {tổng đúng, tổng đã chơi}
        Row(ClassRepo.Student s, Map<String, int[]> phan) { this.s = s; this.phan = phan; }
        boolean played() {
            for (int[] t : phan.values()) if (t[1] > 0) return true;
            return false;
        }
    }

    private List<Row> classRows() {
        List<Row> out = new ArrayList<>();
        for (ClassRepo.Student s : roster.inClass(classKey)) out.add(new Row(s, scoreStore.phanScores(s.name)));
        return out;
    }

    private List<Row> playedRows() {
        List<Row> out = new ArrayList<>();
        for (Row r : classRows()) if (r.played()) out.add(r);
        return out;
    }

    /** Điểm trung bình các học phần ĐÃ CHƠI thuộc môn `cat` (thang 100); -1 = chưa chơi học phần nào của môn. */
    private int categoryScore(Row r, int cat) {
        int sum = 0, n = 0;
        for (Map.Entry<String, int[]> e : r.phan.entrySet()) {
            Integer c = phanCategory.get(e.getKey());
            int sc = ScoreStore.scorePct(e.getValue()[0], e.getValue()[1]);
            if (c != null && c == cat && sc >= 0) { sum += sc; n++; }
        }
        return n == 0 ? -1 : Math.round(sum / (float) n);
    }

    /** Điểm tổng = trung bình các MÔN đã có điểm (môn chưa chơi không tính); -1 = chưa chơi gì. */
    private int totalScore(Row r) {
        int sum = 0, n = 0;
        for (int c = 0; c < categoryNames.length; c++) {
            int sc = categoryScore(r, c);
            if (sc >= 0) { sum += sc; n++; }
        }
        return n == 0 ? -1 : Math.round(sum / (float) n);
    }

    /** Điểm 1 học phần (50 round gần nhất); -1 = chưa chơi học phần đó. */
    private int phanScore(Row r, String phan) {
        int[] t = phan == null ? null : r.phan.get(phan);
        return t == null ? -1 : ScoreStore.scorePct(t[0], t[1]);
    }

    /** Học phần của game đang chọn (null = chưa chọn game). */
    private String currentPhan() {
        if (selectedGameName == null) return null;
        String p = phanByGame.get(selectedGameName);
        return p != null ? p : selectedGameName;
    }

    private boolean phanScopeAvailable() { return currentPhan() != null; }

    private String scopeLabel() {
        switch (rosterScope) {
            case 0: return "Tất cả các môn";
            case 2: return "Phần " + currentPhan();
            default: return "Môn " + (categoryNames.length > domain ? categoryNames[domain] : "—");
        }
    }

    /** Điểm theo kiểu đang chọn ở ô chọn; -1 = chưa có điểm (nơi hiển thị in "-", KHÔNG đưa vào trung bình). */
    private int scopeScore(Row r) {
        switch (rosterScope) {
            case 0: return totalScore(r);
            case 2: return phanScore(r, currentPhan());
            default: return categoryScore(r, domain);
        }
    }

    /** Môn có điểm thấp nhất trong các môn đã có điểm; -1 nếu chưa có môn nào. */
    private int weakestCategory(Row r) {
        int best = -1, bestScore = Integer.MAX_VALUE;
        for (int c = 0; c < categoryNames.length; c++) {
            int sc = categoryScore(r, c);
            if (sc >= 0 && sc < bestScore) { bestScore = sc; best = c; }
        }
        return best;
    }

    private TextView emptyNote(String text) {
        TextView tv = UiUtil.label(this, text, 12.5f, R.color.text_faint, false);
        tv.setPadding(0, UiUtil.dp(this, 14), 0, 0);
        return tv;
    }

    // ── Tab "Lớp học": Tên + 1 cột Điểm (theo ô chọn Tất cả/Môn/Phần) + [Lượt] ───────────────────
    private void renderRoster() {
        if (rosterScope == 2 && !phanScopeAvailable()) rosterScope = 1;
        scopeBtn.setText("Điểm theo:  " + scopeLabel() + "   ⟳");

        List<Row> all = classRows();
        List<Row> played = new ArrayList<>();
        for (Row r : all) if (r.played()) played.add(r);

        notPlayedStrip.setVisibility(View.GONE); // học sinh chưa chơi giờ vẫn nằm trong bảng (điểm "-")

        boolean showGameCol = "select".equals(scene) && selectedGameName != null;
        gamePreviewStrip.setVisibility(showGameCol ? View.VISIBLE : View.GONE);
        if (showGameCol) gamePreviewStrip.setText("Đang xem số lượt đã chơi " + displayNameOf(selectedGameName) + " — cột \"Lượt\".");

        rosterHeader.removeAllViews();
        rosterHeader.addView(headerCell("Học sinh", "name", new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.4f)));
        rosterHeader.addView(headerCell("Điểm", "score", new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.6f)));
        if (showGameCol) rosterHeader.addView(headerCell("Lượt", null, new LinearLayout.LayoutParams(UiUtil.dp(this, 46), ViewGroup.LayoutParams.WRAP_CONTENT)));

        // Hiện CẢ lớp (kể cả bạn chưa chơi gì). Sắp theo điểm thì bạn chưa có điểm (-1) luôn nằm cuối, bất kể chiều sort.
        List<Row> rows = new ArrayList<>(all);
        final int dir = rosterSortDir;
        java.util.Collections.sort(rows, (a, b) -> {
            if ("name".equals(rosterSortKey)) return a.s.display.compareTo(b.s.display) * dir;
            int sa = scopeScore(a), sb = scopeScore(b);
            if ((sa < 0) != (sb < 0)) return sa < 0 ? 1 : -1;
            int cmp = Integer.compare(sa, sb) * dir;
            return cmp != 0 ? cmp : a.s.display.compareTo(b.s.display);
        });

        rosterBody.removeAllViews();
        if (classKey == null) {
            rosterBody.addView(emptyNote("Chưa có lớp nào. Tạo lớp và thêm học sinh ở mục \"Quản lý lớp\"."));
            return;
        }
        if (all.isEmpty()) {
            rosterBody.addView(emptyNote("Lớp này chưa có học sinh. Thêm học sinh ở mục \"Quản lý lớp\"."));
            return;
        }
        for (Row s : rows) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(0, UiUtil.dp(this, 7), 0, UiUtil.dp(this, 7));
            row.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

            LinearLayout nameCell = new LinearLayout(this);
            nameCell.setOrientation(LinearLayout.HORIZONTAL);
            nameCell.setGravity(Gravity.CENTER_VERTICAL);
            nameCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.4f));
            TextView av = UiUtil.makeAvatar(this, initialsOf(s.s.display), 26);
            LinearLayout.LayoutParams avLp = (LinearLayout.LayoutParams) av.getLayoutParams();
            avLp.rightMargin = UiUtil.dp(this, 8);
            nameCell.addView(av);
            TextView nm = UiUtil.label(this, s.s.display, 13f, R.color.text, true);
            nameCell.addView(nm);
            row.addView(nameCell);

            // Chưa có điểm ở phạm vi đang chọn (-1) → thanh rỗng + "-" (chưa có điểm, không phải 0).
            int sc = scopeScore(s);
            boolean weakest = rosterScope == 1 && sc >= 0 && weakestCategory(s) == domain;
            View scoreCell = UiUtil.makeBarCell(this, sc, weakest);
            scoreCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.6f));
            row.addView(scoreCell);

            if (showGameCol) {
                int plays = scoreStore.plays(s.s.name, selectedGameName);
                TextView gameCell = UiUtil.label(this, plays > 0 ? String.valueOf(plays) : "—", 12f, R.color.accent, false);
                gameCell.setGravity(Gravity.END);
                gameCell.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 46), ViewGroup.LayoutParams.WRAP_CONTENT));
                row.addView(gameCell);
            }

            final int playedIdx = played.indexOf(s);
            if (playedIdx >= 0) row.setOnClickListener(v -> { // chưa chơi gì → chưa có gì để xem ở tab Năng lực
                compStudentIndex = playedIdx;
                showReportView("competency");
                renderCompetencyChips();
            });
            rosterBody.addView(row);

            View divider = new View(this);
            divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
            divider.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1)));
            rosterBody.addView(divider);
        }
    }

    private TextView headerCell(String text, String sortKey, LinearLayout.LayoutParams lp) {
        TextView th = new TextView(this);
        String arrow = sortKey != null && sortKey.equals(rosterSortKey) ? (rosterSortDir == 1 ? " ▴" : " ▾") : "";
        th.setText(text + arrow);
        th.setTextSize(10.5f);
        th.setTypeface(th.getTypeface(), android.graphics.Typeface.BOLD);
        th.setTextColor(UiUtil.ContextColor(this, sortKey != null && sortKey.equals(rosterSortKey) ? R.color.accent : R.color.text_faint));
        th.setLayoutParams(lp);
        if (sortKey != null) {
            th.setOnClickListener(v -> {
                if (sortKey.equals(rosterSortKey)) rosterSortDir *= -1;
                else { rosterSortKey = sortKey; rosterSortDir = "name".equals(sortKey) ? 1 : -1; }
                renderRoster();
            });
        }
        return th;
    }

    // ── Tab "Năng lực": điểm từng HỌC PHẦN đã chơi của 1 học sinh ───────────────────────────
    private void renderCompetencyChips() {
        List<Row> played = playedRows();
        if (compStudentIndex >= played.size()) compStudentIndex = 0;
        compChips.removeAllViews();
        for (int i = 0; i < played.size(); i++) {
            ClassRepo.Student s = played.get(i).s;
            boolean sel = i == compStudentIndex;
            LinearLayout chip = new LinearLayout(this);
            chip.setOrientation(LinearLayout.HORIZONTAL);
            chip.setGravity(Gravity.CENTER_VERTICAL);
            chip.setPadding(UiUtil.dp(this, 6), UiUtil.dp(this, 6), UiUtil.dp(this, 10), UiUtil.dp(this, 6));
            chip.setBackground(sel
                    ? UiUtil.pill(UiUtil.ContextColor(this, R.color.accent_dim), UiUtil.ContextColor(this, R.color.accent), 1, this)
                    : UiUtil.pill(UiUtil.ContextColor(this, R.color.panel2), 0, 0, this));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.bottomMargin = UiUtil.dp(this, 6);
            chip.setLayoutParams(lp);
            TextView av = UiUtil.makeAvatar(this, initialsOf(s.display), 20);
            LinearLayout.LayoutParams avLp = (LinearLayout.LayoutParams) av.getLayoutParams();
            avLp.rightMargin = UiUtil.dp(this, 8);
            chip.addView(av);
            chip.addView(UiUtil.label(this, s.display, 12f, sel ? R.color.text : R.color.text_dim, true));
            final int idx = i;
            chip.setOnClickListener(v -> { compStudentIndex = idx; renderCompetencyChips(); });
            compChips.addView(chip);
        }
        renderCompetency();
    }

    private void renderCompetency() {
        List<Row> played = playedRows();
        compScoreList.removeAllViews();
        if (played.isEmpty()) { compName.setText("—"); compFlags.setText(""); return; }
        Row r = played.get(Math.min(compStudentIndex, played.size() - 1));
        compName.setText(r.s.display);
        int total = totalScore(r);
        compFlags.setText(total < 0 ? "" : "Điểm trung bình các môn đã chơi: " + total);

        // Chỉ liệt kê học phần ĐÃ CHƠI; học phần điểm thấp nhất tô đỏ (chỉ khi có ≥2 học phần để so).
        List<Map.Entry<String, int[]>> entries = new ArrayList<>();
        for (Map.Entry<String, int[]> e : r.phan.entrySet()) if (e.getValue()[1] > 0) entries.add(e);
        int minScore = Integer.MAX_VALUE;
        for (Map.Entry<String, int[]> e : entries) minScore = Math.min(minScore, ScoreStore.scorePct(e.getValue()[0], e.getValue()[1]));

        for (Map.Entry<String, int[]> e : entries) {
            int sc = ScoreStore.scorePct(e.getValue()[0], e.getValue()[1]);
            boolean isMin = entries.size() > 1 && sc == minScore;
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(0, UiUtil.dp(this, 8), 0, UiUtil.dp(this, 8));

            // "So sánh số · 3/5" = học phần · số câu đúng / số câu đã chơi.
            TextView cname = UiUtil.label(this, e.getKey() + " · " + e.getValue()[0] + "/" + e.getValue()[1], 12.5f,
                    isMin ? R.color.bad : R.color.text_dim, isMin);
            cname.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 170), ViewGroup.LayoutParams.WRAP_CONTENT));
            row.addView(cname);

            LinearLayout barCell = UiUtil.makeBarCell(this, sc, isMin);
            barCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            row.addView(barCell);

            View divider = new View(this);
            divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
            LinearLayout.LayoutParams dLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1));
            compScoreList.addView(row);
            compScoreList.addView(divider, dLp);
        }
    }

    // ── Tab "Lịch sử": chọn 1 học sinh → các ván theo ngày → bấm 1 ván để xem TỪNG CÂU ────────
    private void renderHistory() {
        historyBody.removeAllViews();
        List<ClassRepo.Student> studs = roster.inClass(classKey);
        if (studs.isEmpty()) { historyBody.addView(emptyNote("Lớp này chưa có học sinh.")); return; }

        ClassRepo.Student picked = null;
        for (ClassRepo.Student st : studs) if (st.name.equals(historyStudent)) picked = st;
        if (picked == null) {
            for (ClassRepo.Student st : studs) if (picked == null && !scoreStore.roundsOf(st.name).isEmpty()) picked = st;
            if (picked == null) picked = studs.get(0);
            historyStudent = picked.name;
        }

        android.widget.HorizontalScrollView hs = new android.widget.HorizontalScrollView(this);
        hs.setHorizontalScrollBarEnabled(false);
        LinearLayout chips = new LinearLayout(this);
        chips.setOrientation(LinearLayout.HORIZONTAL);
        for (ClassRepo.Student st : studs) {
            boolean sel = st.name.equals(historyStudent);
            TextView chip = UiUtil.label(this, st.display, 12.5f, sel ? R.color.accent : R.color.text_dim, true);
            chip.setPadding(UiUtil.dp(this, 12), UiUtil.dp(this, 7), UiUtil.dp(this, 12), UiUtil.dp(this, 7));
            chip.setBackground(sel
                    ? UiUtil.pill(UiUtil.ContextColor(this, R.color.accent_dim), UiUtil.ContextColor(this, R.color.accent), 1, this)
                    : UiUtil.pill(UiUtil.ContextColor(this, R.color.panel2), 0, 0, this));
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.rightMargin = UiUtil.dp(this, 6);
            chip.setLayoutParams(lp);
            chip.setOnClickListener(v -> { historyStudent = st.name; renderHistory(); });
            chips.addView(chip);
        }
        hs.addView(chips);
        LinearLayout.LayoutParams hsLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        hsLp.bottomMargin = UiUtil.dp(this, 10);
        historyBody.addView(hs, hsLp);

        List<ScoreStore.Record> rounds = scoreStore.roundsOf(historyStudent); // mới trước
        if (rounds.isEmpty()) { historyBody.addView(emptyNote(picked.display + " chưa có câu nào được ghi lại.")); return; }

        // gom theo ván (session), giữ thứ tự mới → cũ
        Map<Long, List<ScoreStore.Record>> bySession = new LinkedHashMap<>();
        for (ScoreStore.Record r : rounds) {
            List<ScoreStore.Record> l = bySession.get(r.session);
            if (l == null) { l = new ArrayList<>(); bySession.put(r.session, l); }
            l.add(r);
        }
        java.text.SimpleDateFormat dayFmt = new java.text.SimpleDateFormat("EEEE dd/MM/yyyy", new java.util.Locale("vi"));
        java.text.SimpleDateFormat timeFmt = new java.text.SimpleDateFormat("HH:mm", java.util.Locale.US);
        String lastDay = null;
        for (Map.Entry<Long, List<ScoreStore.Record>> e : bySession.entrySet()) {
            final long sid = e.getKey();
            List<ScoreStore.Record> sr = e.getValue();               // mới → cũ
            ScoreStore.Record first = sr.get(sr.size() - 1);          // câu đầu tiên của ván
            String day = dayFmt.format(new java.util.Date(first.time));
            if (!day.equals(lastDay)) {
                lastDay = day;
                TextView dh = UiUtil.label(this, day, 12f, R.color.text_faint, true);
                dh.setPadding(0, UiUtil.dp(this, 10), 0, UiUtil.dp(this, 4));
                historyBody.addView(dh);
            }
            int ok = 0; float totalSec = 0;
            for (ScoreStore.Record r : sr) { if (r.correct) ok++; totalSec += r.sec; }
            boolean open = openSessions.contains(sid);

            LinearLayout head = new LinearLayout(this);
            head.setOrientation(LinearLayout.HORIZONTAL);
            head.setGravity(Gravity.CENTER_VERTICAL);
            head.setPadding(UiUtil.dp(this, 8), UiUtil.dp(this, 9), UiUtil.dp(this, 8), UiUtil.dp(this, 9));
            head.setBackground(UiUtil.pill(UiUtil.ContextColor(this, R.color.panel2), 0, 0, this));
            head.addView(cellText(timeFmt.format(new java.util.Date(first.time)), 48, R.color.text_faint));
            head.addView(cellText(displayNameOf(first.game), 0, R.color.text));
            TextView mark = cellText(ok + "/" + sr.size(), 52, ok * 2 >= sr.size() ? R.color.good : R.color.bad);
            mark.setGravity(Gravity.CENTER);
            head.addView(mark);
            TextView avg = cellText(String.format(java.util.Locale.US, "%.1fs", totalSec / sr.size()), 52, R.color.text_dim);
            avg.setGravity(Gravity.END);
            head.addView(avg);
            head.addView(cellText(open ? "▾" : "▸", 22, R.color.accent));
            head.setOnClickListener(v -> {
                if (!openSessions.remove(sid)) openSessions.add(sid);
                renderHistory();
            });
            LinearLayout.LayoutParams headLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            headLp.bottomMargin = UiUtil.dp(this, 4);
            historyBody.addView(head, headLp);

            if (!open) continue;
            for (int i = sr.size() - 1; i >= 0; i--) {                // cũ → mới = đúng thứ tự câu
                ScoreStore.Record r = sr.get(i);
                LinearLayout row = new LinearLayout(this);
                row.setOrientation(LinearLayout.HORIZONTAL);
                row.setPadding(UiUtil.dp(this, 14), UiUtil.dp(this, 7), UiUtil.dp(this, 8), UiUtil.dp(this, 7));

                LinearLayout left = new LinearLayout(this);
                left.setOrientation(LinearLayout.VERTICAL);
                left.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
                String q = r.question == null || r.question.isEmpty() ? "Câu hỏi media" : r.question;
                left.addView(UiUtil.label(this, "Câu " + r.round + ": " + q, 12.5f, R.color.text, true));
                left.addView(UiUtil.label(this, "Chọn: " + (r.answer == null || r.answer.isEmpty() ? "—" : r.answer),
                        12f, r.correct ? R.color.good : R.color.bad, false));
                if (!r.correct && r.correctAnswer != null && !r.correctAnswer.isEmpty())
                    left.addView(UiUtil.label(this, "Đúng: " + r.correctAnswer, 12f, R.color.text_dim, false));
                row.addView(left);

                LinearLayout right = new LinearLayout(this);
                right.setOrientation(LinearLayout.VERTICAL);
                right.setGravity(Gravity.END);
                right.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 64), ViewGroup.LayoutParams.WRAP_CONTENT));
                right.addView(UiUtil.label(this, r.correct ? "✓ Đúng" : "✗ Sai", 12f, r.correct ? R.color.good : R.color.bad, true));
                right.addView(UiUtil.label(this, String.format(java.util.Locale.US, "%.1fs", r.sec), 11.5f, R.color.text_dim, false));
                row.addView(right);
                historyBody.addView(row);

                View divider = new View(this);
                divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
                historyBody.addView(divider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1)));
            }
        }
    }

    private TextView cellText(String text, int widthDp, int colorRes) {
        TextView tv = UiUtil.label(this, text, 12.5f, colorRes, false);
        tv.setLayoutParams(widthDp > 0
                ? new LinearLayout.LayoutParams(UiUtil.dp(this, widthDp), ViewGroup.LayoutParams.WRAP_CONTENT)
                : new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        return tv;
    }

    // ── Scene "ended", bấm "Tổng kết": thẻ tóm tắt ván vừa xong ─────────────────────────────
    /** Tab "TỔNG KẾT" (nút riêng ở sidebar lúc "ended") — điểm TỔNG theo bên + breakdown từng
     *  người chơi thật. Trước đây dùng liveLeftName/liveRightName làm nhãn "Điểm X/Y" — SAI logic
     *  đã xác nhận qua báo cáo thực tế: đó là tên NGƯỜI CUỐI CÙNG được nhận diện ở mỗi bên, không
     *  đại diện được cho cả đội (1 bên có thể nhiều bạn thay phiên chơi). Đổi nhãn thành "Đội
     *  trái/phải" trung lập + thêm breakdown thật từng người (liveLeftPlayers/liveRightPlayers,
     *  cùng data với panel_live lúc đang chơi, UpdateLivePlayers() từ Unity) — đây cũng là nơi
     *  DUY NHẤT hiện breakdown chi tiết giờ, thay cho panel tương tự trước đặt nhầm bên màn chiếu
     *  (display phụ, Unity ScoreScene) — đã bỏ bên đó, kéo hết về tablet (display 0) theo đúng
     *  yêu cầu: giáo viên xem chi tiết trên tablet, màn chiếu chỉ cần tổng điểm cho học sinh. */
    private void renderSummary() {
        summaryCard.removeAllViews();
        summaryCard.addView(UiUtil.label(this, selectedGameName != null ? displayNameOf(selectedGameName) : "—", 19f, R.color.text, true));

        String[][] rows = {
                {"Thời lượng", durationText()},
                {"Điểm Đội trái / Đội phải", liveLeftScore + " – " + liveRightScore},
        };
        for (String[] r : rows) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            LinearLayout.LayoutParams rowLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            rowLp.topMargin = UiUtil.dp(this, 10);
            row.setLayoutParams(rowLp);
            TextView k = UiUtil.label(this, r[0], 13f, R.color.text_dim, false);
            k.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            TextView v = UiUtil.label(this, r[1], 13f, R.color.text, true);
            row.addView(k);
            row.addView(v);
            summaryCard.addView(row);
            View divider = new View(this);
            divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
            LinearLayout.LayoutParams dLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1));
            dLp.topMargin = UiUtil.dp(this, 10);
            summaryCard.addView(divider, dLp);
        }

        LinearLayout breakdownRow = new LinearLayout(this);
        breakdownRow.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams breakdownLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        breakdownLp.topMargin = UiUtil.dp(this, 4);
        breakdownRow.setLayoutParams(breakdownLp);
        breakdownRow.addView(buildSummaryPlayerColumn("Đội trái", liveLeftPlayers, R.color.live));
        View colGap = new View(this);
        colGap.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 16), 1));
        breakdownRow.addView(colGap);
        breakdownRow.addView(buildSummaryPlayerColumn("Đội phải", liveRightPlayers, R.color.bad));
        summaryCard.addView(breakdownRow);
    }

    /** "m:ss" của ván vừa xong; "—" nếu chưa có ván nào. Đang chơi thì tính tới hiện tại. */
    private String durationText() {
        if (gameStartMs <= 0) return "—";
        long end = gameEndMs > 0 ? gameEndMs : System.currentTimeMillis();
        long ms = Math.max(0, end - gameStartMs - pausedAccumMs);
        long sec = ms / 1000;
        return String.format(java.util.Locale.US, "%d:%02d", sec / 60, sec % 60);
    }

    private LinearLayout buildSummaryPlayerColumn(String label, List<LivePlayer> players, int accentColorRes) {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        TextView header = UiUtil.label(this, label, 12.5f, accentColorRes, true);
        LinearLayout.LayoutParams headerLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        headerLp.bottomMargin = UiUtil.dp(this, 6);
        header.setLayoutParams(headerLp);
        col.addView(header);

        if (players == null || players.isEmpty()) {
            col.addView(UiUtil.label(this, "(chưa nhận diện được ai)", 11.5f, R.color.text_faint, false));
            return col;
        }
        for (int i = 0; i < players.size(); i++) {
            LivePlayer p = players.get(i);
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.VERTICAL);
            LinearLayout.LayoutParams rowLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            rowLp.topMargin = UiUtil.dp(this, i == 0 ? 0 : 8);
            row.setLayoutParams(rowLp);
            // Số CÂU ĐÚNG, không gọi là "điểm": điểm đội = câu đúng x điểm/câu của game (có game >1 điểm/câu) nên tổng
            // câu đúng của các bạn không phải lúc nào cũng bằng điểm đội.
            row.addView(UiUtil.label(this, (i + 1) + ". " + p.name + " — " + p.correct + "/" + p.answered + " câu đúng", 12.5f, R.color.text, true));
            row.addView(UiUtil.label(this, "TB " + String.format(java.util.Locale.US, "%.1f", p.avgTime) + "s/câu"
                    + (p.correct > 0 ? " · đúng TB " + String.format(java.util.Locale.US, "%.1f", p.avgCorrectTime) + "s" : ""), 10.5f, R.color.text_faint, false));
            col.addView(row);
        }
        return col;
    }

    // ── Scene "playing": live team view — tên/điểm/breakdown từng người ĐỀU THẬT, từ
    //    UpdateReport()/UpdateLivePlayers() (GameControlBridge bên Unity, cùng nhịp 1s) ────────
    private void renderLive() {
        buildLiveSide(liveSideLeft, "Team Trái", liveLeftName, liveLeftScore, liveLeftPlayers, R.color.live);
        buildLiveSide(liveSideRight, "Team Phải", liveRightName, liveRightScore, liveRightPlayers, R.color.bad);
    }

    private void buildLiveSide(LinearLayout side, String teamLabel, String playingName, String scoreText,
                                List<LivePlayer> members, int accentColorRes) {
        side.removeAllViews();
        View topBorder = new View(this);
        topBorder.setBackgroundColor(UiUtil.ContextColor(this, accentColorRes));
        side.addView(topBorder, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 3)));

        LinearLayout nameRow = new LinearLayout(this);
        nameRow.setOrientation(LinearLayout.HORIZONTAL);
        nameRow.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams nameRowLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        nameRowLp.topMargin = UiUtil.dp(this, 14);
        TextView label = UiUtil.label(this, teamLabel + "  ·  Đang chơi: " + playingName, 14f, R.color.text, true);
        label.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        nameRow.addView(label);
        nameRow.setLayoutParams(nameRowLp);
        side.addView(nameRow);

        TextView score = UiUtil.label(this, scoreText, 36f, R.color.text, true);
        LinearLayout.LayoutParams scoreLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        scoreLp.topMargin = UiUtil.dp(this, 10);
        score.setLayoutParams(scoreLp);
        side.addView(score);
        TextView scoreLabel = UiUtil.label(this, "tổng điểm nhóm", 10.5f, R.color.text_faint, false);
        side.addView(scoreLabel);

        android.widget.HorizontalScrollView turnScroll = new android.widget.HorizontalScrollView(this);
        LinearLayout.LayoutParams turnScrollLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        turnScrollLp.topMargin = UiUtil.dp(this, 14);
        turnScroll.setLayoutParams(turnScrollLp);
        LinearLayout turnRow = new LinearLayout(this);
        turnRow.setOrientation(LinearLayout.HORIZONTAL);
        turnScroll.addView(turnRow);
        if (members.isEmpty()) {
            turnRow.addView(UiUtil.label(this, "(chưa nhận diện được ai)", 10.5f, R.color.text_faint, false));
        }
        for (LivePlayer m : members) {
            LinearLayout av = new LinearLayout(this);
            av.setOrientation(LinearLayout.VERTICAL);
            av.setGravity(Gravity.CENTER);
            LinearLayout.LayoutParams avLp = new LinearLayout.LayoutParams(UiUtil.dp(this, 46), ViewGroup.LayoutParams.WRAP_CONTENT);
            avLp.rightMargin = UiUtil.dp(this, 8);
            av.setLayoutParams(avLp);
            TextView circle = UiUtil.makeAvatar(this, initialsOf(m.name), 38);
            circle.setTextColor(UiUtil.ContextColor(this, R.color.text));
            circle.setBackground(UiUtil.circle(UiUtil.ContextColor(this, R.color.panel2), UiUtil.ContextColor(this, R.color.accent), 2, this));
            av.addView(circle);
            // "3đ · 5c" = 3 điểm / 5 câu đã trả lời — số liệu THẬT từ PlayerRecognitionService,
            // không phải tổng lượt chơi mock cả lớp như trước.
            TextView cnt = UiUtil.label(this, m.correct + "đ · " + m.answered + "c", 9f, R.color.text_faint, false);
            av.addView(cnt);
            TextView tname = UiUtil.label(this, m.name, 9.5f, R.color.text_dim, false);
            tname.setGravity(Gravity.CENTER);
            av.addView(tname);
            turnRow.addView(av);
        }
        side.addView(turnScroll);
    }

    /** 2 chữ cái đầu tên + họ, cùng quy ước với ClassManagementActivity.initialOf() (Kotlin) —
     *  viết riêng ở đây vì 2 file khác module/ngôn ngữ, không share trực tiếp được. */
    private static String initialsOf(String name) {
        if (name == null) return "?";
        String[] parts = name.trim().split("\\s+");
        if (parts.length == 0 || parts[0].isEmpty()) return "?";
        String first = parts[0].substring(0, 1).toUpperCase();
        if (parts.length > 1 && !parts[parts.length - 1].isEmpty()) {
            return first + parts[parts.length - 1].substring(0, 1).toUpperCase();
        }
        return first;
    }

    // =========================================================================================
    // Start/Pause/Stop — logic Unity THẬT (không đổi so với trước), chỉ đổi phần cập nhật UI.
    // =========================================================================================
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
        sessionId = System.currentTimeMillis(); // mỗi lần Start = 1 ván mới trong lịch sử
        gameStartMs = sessionId; gameEndMs = 0; pausedAtMs = 0; pausedAccumMs = 0;

        if (!unityStarted) {
            dismissLogoPresentation(); // nhường display phụ lại cho Unity
            Display secondary = findSecondaryDisplay();
            Intent intent = new Intent();
            intent.setClassName(getPackageName(), UNITY_PLAYER_ACTIVITY_CLASS);
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            intent.putExtra(EXTRA_SCENE_NAME, selectedScene);
            intent.putExtra(EXTRA_GAME_NAME, selectedGameName);
            try {
                intent.putExtra(EXTRA_SETTINGS_JSON, settingsStore.toJson().toString());
            } catch (Exception ignored) {}

            if (secondary == null) {
                Log.i(TAG, "onStartClicked: không có display phụ — chạy display 0 (fallback 1-display)");
                startActivity(intent);
            } else {
                try {
                    Bundle options = ActivityOptions.makeBasic().setLaunchDisplayId(secondary.getDisplayId()).toBundle();
                    startActivity(intent, options);
                } catch (SecurityException e) {
                    Log.e(TAG, "onStartClicked: SecurityException khi setLaunchDisplayId — " + e.getMessage(), e);
                    startActivity(intent);
                }
            }
            unityStarted = true;
        } else {
            sendToUnity("OnLoadGameRequested", selectedScene + "|" + (selectedGameName != null ? selectedGameName : ""));
        }

        paused = false;
        // Ván mới: bỏ số liệu người chơi của ván trước (không để lẫn vào bản ghi của ván này).
        liveLeftPlayers = new ArrayList<>();
        liveRightPlayers = new ArrayList<>();
        scene = "playing";
        renderAll();
    }

    private void onPauseClicked() {
        paused = !paused;
        if (paused) pausedAtMs = System.currentTimeMillis();
        else if (pausedAtMs > 0) { pausedAccumMs += System.currentTimeMillis() - pausedAtMs; pausedAtMs = 0; }
        sendToUnity(paused ? "OnPauseRequested" : "OnResumeRequested", "");
        renderActionZone();
    }

    private void onStopClicked() {
        if (paused) sendToUnity("OnResumeRequested", "");
        sendToUnity("OnStopRequested", "");
        backToMenu();
        paused = false;
    }

    /** Dùng chung cho Stop (bấm tay), OnGameEnded (game tự hết giờ), và nút "Bắt Đầu" trên
     *  ScoreScene (Unity). */
    private void backToMenu() {
        // ĐÃ BỎ startActivity(new Intent(this, ControlActivity.class)) từng có ở đây — comment
        // cũ ghi "cần cho chế độ 1 màn hình", tức từ TRƯỚC kiến trúc 2 display hiện tại.
        // ControlActivity (display 0) không bao giờ mất focus khi Unity (display 1) đổi trạng
        // thái, nên tự relaunch chính mình là thừa — nghi vấn cao nhất khiến "CHƠI LẠI" không
        // hiện ra được: lệnh này chen ngay giữa lúc renderAll() vừa vẽ xong màn "ended", làm
        // gián đoạn trước khi người dùng kịp thấy (đã xác nhận qua báo cáo thực tế trên K02:
        // hết game tự nhiên không hề thấy nút Chơi lại).
        // Ghi kết quả ván vừa xong (Stop tay / hết giờ tự nhiên đều qua đây) — chỉ khi đang "playing", để
        // OnGameEnded gọi thêm lần nữa (vd bấm nút trên ScoreScene) không ghi trùng.
        if ("playing".equals(scene) && gameStartMs > 0) {
            long now = System.currentTimeMillis();
            if (paused && pausedAtMs > 0) { pausedAccumMs += now - pausedAtMs; pausedAtMs = 0; }
            gameEndMs = now;
        }
        scene = "ended";
        // Các round cuối có thể còn nằm trong hàng đợi ghi — vẽ lại thêm 1 lần sau khi hàng đợi xong.
        scoreExec.execute(() -> runOnUiThread(() -> { if (!"playing".equals(scene)) renderReportZone(); }));
        Log.i(TAG, "backToMenu: scene=ended, renderAll()");
        renderAll();
    }

    private void renderSettings() {
        settingsBody.removeAllViews();
        settingsBody.addView(UiUtil.label(this, "CÀI ĐẶT CHUNG", 16f, R.color.text, true));
        
        // 1. Thời gian game: thanh trượt 10–600 + ô nhập số ở cuối (nhập ngoài dải vẫn dùng đúng số nhập, ≥ 1)
        settingsBody.addView(buildGameTimeSetting());

        // 2. Chờ chuyển round
        settingsBody.addView(buildSeekBarSetting("Chờ chuyển round (giây)", 0, 6, Math.max(0, Math.min(6, Math.round(settingsStore.roundEndDelay))), val -> {
            settingsStore.roundEndDelay = (float)val;
            settingsStore.save();
            syncSettingsToUnity();
        }));

        // 3. Tốc độ flow (Slider 0.1 - 5.0, step 0.1 + EditText)
        settingsBody.addView(buildFlowSpeedSetting());

        // 4. Âm lượng nhạc
        settingsBody.addView(buildVolumeSetting("Âm lượng nhạc nền", settingsStore.musicVolume, val -> {
            settingsStore.musicVolume = val;
            settingsStore.save();
            syncSettingsToUnity();
        }));

        // 5. Âm lượng hiệu ứng (SFX)
        settingsBody.addView(buildVolumeSetting("Âm lượng hiệu ứng (SFX)", settingsStore.sfxVolume, val -> {
            settingsStore.sfxVolume = val;
            settingsStore.save();
            syncSettingsToUnity();
        }));

        // 6. Chờ clear mới chuyển round
        settingsBody.addView(buildCheckboxSetting("Chờ clear mới chuyển round", settingsStore.waitForClear == 1, checked -> {
            settingsStore.waitForClear = checked ? 1 : 0;
            settingsStore.save();
            syncSettingsToUnity();
        }));

        // 7. Tên hiển thị trong game: tên thường gọi (mặc định) hay tên thật; cả 2 chỉ hiện 2 tiếng cuối.
        settingsBody.addView(buildCheckboxSetting("Hiện tên thật trong game (bỏ chọn = tên thường gọi)", settingsStore.showRealName == 1, checked -> {
            settingsStore.showRealName = checked ? 1 : 0;
            settingsStore.save();
            syncSettingsToUnity();
        }));

        // 8. Vùng nhận diện khuôn mặt (2 vùng người chơi trong game + vòng tròn "Thêm từ camera").
        // Chỉnh trên hình camera thật ở màn của FaceEnroll (cùng APK); lưu vào /sdcard/EduXplore/face_zones.json,
        // game áp dụng từ vòng chơi sau. Chỉnh khi KHÔNG chơi (camera USB chỉ 1 nơi mở được).
        settingsBody.addView(buildButtonSetting("Vùng nhận diện khuôn mặt (2 vùng chơi + vùng enroll)", "Mở cài đặt vùng", () -> {
            try {
                Intent i = new Intent();
                i.setClassName(getPackageName(), "com.faceattendance.app.MainActivity");
                i.putExtra("mode", "zone_setup");
                startActivity(i);
            } catch (Exception e) {
                Log.e(TAG, "mở cài đặt vùng nhận diện lỗi: " + e);
            }
        }));
    }

    /** Một dòng cài đặt dạng: nhãn + nút bấm (mở màn khác, không phải giá trị). */
    private View buildButtonSetting(String label, String buttonText, final Runnable onClick) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));
        TextView l = UiUtil.label(this, label, 13f, R.color.text, false);
        l.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        row.addView(l);
        android.widget.Button b = new android.widget.Button(this);
        b.setText(buttonText);
        b.setTextSize(13f);
        b.setAllCaps(false);
        b.setOnClickListener(v -> onClick.run());
        row.addView(b);
        return row;
    }

    private View buildSeekBarSetting(String label, int min, int max, int current, final UiUtil.OnPick onPick) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));

        LinearLayout top = new LinearLayout(this);
        top.setOrientation(LinearLayout.HORIZONTAL);
        top.addView(UiUtil.label(this, label, 13f, R.color.text_dim, false));
        View spacer = new View(this);
        spacer.setLayoutParams(new LinearLayout.LayoutParams(0, 1, 1f));
        top.addView(spacer);
        TextView valTxt = UiUtil.label(this, String.valueOf(current), 13f, R.color.accent, true);
        top.addView(valTxt);
        row.addView(top);

        SeekBar sb = new SeekBar(this);
        sb.setMax(max - min);
        sb.setProgress(current - min);
        sb.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                int val = min + progress;
                valTxt.setText(String.valueOf(val));
                if (fromUser) onPick.onPick(val);
            }
            @Override public void onStartTrackingTouch(SeekBar seekBar) {}
            @Override public void onStopTrackingTouch(SeekBar seekBar) {}
        });
        row.addView(sb);
        return row;
    }

    /** Gắn "chốt giá trị" cho ô nhập: nhấn Xong trên bàn phím hoặc rời ô thì mới áp (không áp từng phím gõ). */
    private void commitOnDoneOrBlur(final EditText et, final Runnable commit) {
        et.setSingleLine(true);
        et.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_DONE);
        et.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == android.view.inputmethod.EditorInfo.IME_ACTION_DONE) { commit.run(); v.clearFocus(); }
            return false;
        });
        et.setOnFocusChangeListener((v, hasFocus) -> { if (!hasFocus) commit.run(); });
    }

    /** Thời gian game: thanh trượt 10–600s + ô nhập số (≥ 1s, không giới hạn trên) — số nhập được dùng nguyên. */
    private View buildGameTimeSetting() {
        final int sbMin = 10, sbMax = 600;
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));
        row.addView(UiUtil.label(this, "Thời gian game (giây)", 13f, R.color.text_dim, false));

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.HORIZONTAL);
        controls.setGravity(Gravity.CENTER_VERTICAL);

        final SeekBar sb = new SeekBar(this);
        sb.setMax(sbMax - sbMin);
        sb.setProgress(Math.max(0, Math.min(sbMax - sbMin, settingsStore.gameTime - sbMin)));
        sb.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        final EditText et = new EditText(this);
        et.setText(String.valueOf(settingsStore.gameTime));
        et.setTextSize(13f);
        et.setInputType(InputType.TYPE_CLASS_NUMBER);
        et.setGravity(Gravity.CENTER);
        et.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 72), ViewGroup.LayoutParams.WRAP_CONTENT));

        sb.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                if (!fromUser) return;
                int val = sbMin + progress;
                et.setText(String.valueOf(val));
                settingsStore.gameTime = val;
                settingsStore.save();
                syncSettingsToUnity();
            }
            @Override public void onStartTrackingTouch(SeekBar seekBar) {}
            @Override public void onStopTrackingTouch(SeekBar seekBar) {}
        });
        commitOnDoneOrBlur(et, () -> {
            int val;
            try { val = Integer.parseInt(et.getText().toString().trim()); } catch (Exception e) { val = -1; }
            if (val < 1) { et.setText(String.valueOf(settingsStore.gameTime)); return; } // rỗng/sai → trả lại giá trị đang dùng
            sb.setProgress(Math.max(0, Math.min(sbMax - sbMin, val - sbMin)));
            if (val != settingsStore.gameTime) {
                settingsStore.gameTime = val;
                settingsStore.save();
                syncSettingsToUnity();
            }
        });
        controls.addView(sb);
        controls.addView(et);
        row.addView(controls);
        return row;
    }

    /** Tốc độ flow: thanh trượt 0.1–5.0 + ô nhập số; nhập ngoài dải vẫn dùng đúng số nhập (> 0). */
    private View buildFlowSpeedSetting() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));

        row.addView(UiUtil.label(this, "Tốc độ flow (thanh trượt 0.1x - 5.0x, ô số nhập tự do)", 13f, R.color.text_dim, false));

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.HORIZONTAL);
        controls.setGravity(Gravity.CENTER_VERTICAL);

        final SeekBar sb = new SeekBar(this);
        sb.setMax(49); // 0 to 49 -> 0.1 to 5.0
        sb.setProgress(Math.max(0, Math.min(49, Math.round((settingsStore.flowSpeed - 0.1f) * 10f))));
        sb.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        final EditText et = new EditText(this);
        et.setText(fmtFlow(settingsStore.flowSpeed));
        et.setTextSize(13f);
        et.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_FLAG_DECIMAL);
        et.setGravity(Gravity.CENTER);
        et.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 72), ViewGroup.LayoutParams.WRAP_CONTENT));

        sb.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                if (!fromUser) return;
                float val = 0.1f + progress / 10f;
                et.setText(fmtFlow(val));
                settingsStore.flowSpeed = val;
                settingsStore.save();
                syncSettingsToUnity();
            }
            @Override public void onStartTrackingTouch(SeekBar seekBar) {}
            @Override public void onStopTrackingTouch(SeekBar seekBar) {}
        });
        commitOnDoneOrBlur(et, () -> {
            float val;
            try { val = Float.parseFloat(et.getText().toString().trim().replace(',', '.')); } catch (Exception e) { val = -1f; }
            if (!(val > 0f)) { et.setText(fmtFlow(settingsStore.flowSpeed)); return; }
            sb.setProgress(Math.max(0, Math.min(49, Math.round((val - 0.1f) * 10f))));
            if (Math.abs(val - settingsStore.flowSpeed) > 1e-6f) {
                settingsStore.flowSpeed = val;
                settingsStore.save();
                syncSettingsToUnity();
            }
        });

        controls.addView(sb);
        controls.addView(et);
        row.addView(controls);
        return row;
    }

    private static String fmtFlow(float v) {
        String t = String.format(java.util.Locale.US, "%.2f", v);
        if (t.contains(".")) t = t.replaceAll("0+$", "").replaceAll("[.]$", "");
        return t.isEmpty() ? "0" : (t.indexOf('.') < 0 ? t + ".0" : t);
    }

    private View buildVolumeSetting(String label, float current, final VolumeCallback cb) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));

        LinearLayout top = new LinearLayout(this);
        top.setOrientation(LinearLayout.HORIZONTAL);
        top.addView(UiUtil.label(this, label, 13f, R.color.text_dim, false));
        View spacer = new View(this);
        spacer.setLayoutParams(new LinearLayout.LayoutParams(0, 1, 1f));
        top.addView(spacer);
        TextView valTxt = UiUtil.label(this, Math.round(current * 100) + "%", 13f, R.color.accent, true);
        top.addView(valTxt);
        row.addView(top);

        SeekBar sb = new SeekBar(this);
        sb.setMax(100);
        sb.setProgress(Math.round(current * 100));
        sb.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                valTxt.setText(progress + "%");
                if (fromUser) cb.onVolumeChanged(progress / 100f);
            }
            @Override public void onStartTrackingTouch(SeekBar seekBar) {}
            @Override public void onStopTrackingTouch(SeekBar seekBar) {}
        });
        row.addView(sb);
        return row;
    }

    private View buildCheckboxSetting(String label, boolean current, final CheckboxCallback cb) {
        CheckBox cbView = new CheckBox(this);
        cbView.setText(label);
        cbView.setChecked(current);
        cbView.setTextSize(13f);
        cbView.setTextColor(UiUtil.ContextColor(this, R.color.text));
        cbView.setPadding(0, UiUtil.dp(this, 12), 0, UiUtil.dp(this, 12));
        cbView.setOnCheckedChangeListener((buttonView, isChecked) -> cb.onCheckedChanged(isChecked));
        return cbView;
    }

    private interface VolumeCallback { void onVolumeChanged(float val); }
    private interface CheckboxCallback { void onCheckedChanged(boolean checked); }

    private void syncSettingsToUnity() {
        if (!unityStarted) return;
        try {
            String json = settingsStore.toJson().toString();
            sendToUnity("OnSettingsChanged", json);
        } catch (Exception e) {
            Log.e(TAG, "syncSettingsToUnity lỗi: " + e);
        }
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

    /** Gọi từ Unity (GameControlBridge.PushReport) — cập nhật report LIVE thật cho panel_live. */
    public static void UpdateReport(String timeText, String leftName, String leftScoreText,
                                     String rightName, String rightScoreText) {
        ControlActivity activity = sInstance;
        if (activity == null) return;
        activity.liveTime = timeText;
        activity.liveLeftName = leftName;
        activity.liveLeftScore = leftScoreText;
        activity.liveRightName = rightName;
        activity.liveRightScore = rightScoreText;
        activity.runOnUiThread(() -> {
            if ("playing".equals(activity.scene)) activity.renderLive();
            else if ("ended".equals(activity.scene) && activity.panelSummary != null
                    && activity.panelSummary.getVisibility() == View.VISIBLE) activity.renderSummary(); // số liệu về muộn sau Stop
        });
    }

    /** Gọi từ Unity (GameControlBridge.PushPlayerBreakdown, cùng nhịp 1s với UpdateReport) —
     *  điểm/số liệu THẬT từng người chơi đã nhận diện được (PlayerRecognitionService), thay cho
     *  roster mock cũ. JSON dạng {"players":[{"name","correct","answered","avgTime",
     *  "avgCorrectTime"}, ...]} — xem GameControlBridge.PushPlayerBreakdown() phía Unity. */
    public static void UpdateLivePlayers(String leftJson, String rightJson) {
        ControlActivity activity = sInstance;
        if (activity == null) return;
        activity.liveLeftPlayers = parseLivePlayers(leftJson);
        activity.liveRightPlayers = parseLivePlayers(rightJson);
        activity.runOnUiThread(() -> {
            if ("playing".equals(activity.scene)) activity.renderLive();
            else if ("ended".equals(activity.scene) && activity.panelSummary != null
                    && activity.panelSummary.getVisibility() == View.VISIBLE) activity.renderSummary(); // số liệu về muộn sau Stop
        });
    }

    private static List<LivePlayer> parseLivePlayers(String json) {
        List<LivePlayer> out = new ArrayList<>();
        if (json == null || json.isEmpty()) return out;
        try {
            JSONArray arr = new JSONObject(json).optJSONArray("players");
            if (arr != null) {
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject p = arr.getJSONObject(i);
                    LivePlayer lp = new LivePlayer();
                    lp.name = p.optString("name", "?");
                    lp.correct = p.optInt("correct", 0);
                    lp.answered = p.optInt("answered", 0);
                    lp.avgTime = (float) p.optDouble("avgTime", 0);
                    lp.avgCorrectTime = (float) p.optDouble("avgCorrectTime", 0);
                    out.add(lp);
                }
            }
        } catch (Exception e) {
            Log.e(TAG, "parseLivePlayers failed: " + e);
        }
        return out;
    }

    /** Gọi từ Unity (GameControlBridge.PushRound, qua PlayerRecognitionService.LogRound) MỖI KHI 1 học sinh trả
     *  lời xong 1 câu/round. JSON {"name","recognized","game","slot","round","questionId","question","answer",
     *  "correctAnswer","correct","sec"}. Chỉ lưu người đã nhận diện được. Ghi tuần tự ở 1 luồng nền (giữ đúng thứ tự). */
    public static void OnRound(String json) {
        final ControlActivity a = sInstance;
        if (a == null || json == null) return;
        try {
            JSONObject o = new JSONObject(json);
            if (!o.optBoolean("recognized", false)) return;
            String name = o.optString("name", "");
            if (name.isEmpty() || "?".equals(name)) return;
            String game = o.optString("game", "");
            if (!a.phanByGame.containsKey(game) && a.selectedGameName != null) game = a.selectedGameName;
            String phan = a.phanByGame.containsKey(game) ? a.phanByGame.get(game) : game;
            if (a.sessionId == 0) a.sessionId = System.currentTimeMillis();
            final ScoreStore.Record r = new ScoreStore.Record(System.currentTimeMillis(), a.sessionId, name, game, phan,
                    o.optInt("round"), o.optString("questionId"), o.optString("question"), o.optString("answer"),
                    o.optString("correctAnswer"), o.optBoolean("correct"), (float) o.optDouble("sec", 0));
            a.scoreExec.execute(() -> a.scoreStore.addRound(r));
        } catch (Exception e) {
            Log.e(TAG, "OnRound lỗi: " + e);
        }
    }

    /** Gọi từ Unity (GameControlBridge.PushGameEnded) lúc hết giờ/hết vòng tự nhiên. */
    public static void OnGameEnded() {
        ControlActivity activity = sInstance;
        if (activity == null) return;
        activity.runOnUiThread(activity::backToMenu);
    }

    @Override
    protected void onDestroy() {
        if (sInstance == this) sInstance = null;
        dismissLogoPresentation();
        super.onDestroy();
    }
}
