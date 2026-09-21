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
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import com.eduxplore.control.ui.MockData;
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
 * Dữ liệu roster/điểm năng lực/lịch sử hiện là MOCK (MockData.java) — chưa có hệ thống lớp
 * học + công thức tính điểm thật, xem CLAUDE.md. Luồng Start/Pause/Stop/Unity vẫn 100% thật.
 */
public class ControlActivity extends Activity {

    private static final String TAG = "ControlActivity";
    private static final String UNITY_PLAYER_ACTIVITY_CLASS = "com.unity3d.player.UnityPlayerActivity";

    public static final String EXTRA_SCENE_NAME = "com.eduxplore.control.SCENE_NAME";
    public static final String EXTRA_GAME_NAME = "com.eduxplore.control.GAME_NAME";

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

    // ── State ────────────────────────────────────────────────────────────────
    private String scene = "select"; // select | playing | ended
    private int domain = 0;
    private String classKey;
    private String selectedScene = null;      // sceneName Unity thật (Start dùng cái này)
    private String selectedGameName = null;   // tên game/variant đang chọn hoặc vừa chơi
    private int compStudentIndex = 0;
    private String rosterSortKey = "total";
    private int rosterSortDir = -1;
    private boolean paused = false;
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
    // cùng nhịp 1s với PushReport) — thay cho roster MockData.classData(...).playedStudents cũ
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
        MockData.generate(categoryNames.length, allGameNames);
        classKey = MockData.classDefs()[0].key;

        buildReportTabs();
        buildHistoryPanel();
        renderAll();
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
            }
            Log.i(TAG, "loadGameRegistry: " + categoryNames.length + " môn, " + allGameNames.size() + " game");
        } catch (Exception e) {
            Log.e(TAG, "loadGameRegistry lỗi: " + e.getMessage(), e);
            categoryNames = new String[]{"Khác"};
        }
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
                        renderAll();
                    });
        });

        MockData.ClassDef[] classDefs = MockData.classDefs();
        int classIdx = 0;
        List<String> classLabels = new ArrayList<>();
        for (int i = 0; i < classDefs.length; i++) {
            classLabels.add(classDefs[i].label);
            if (classDefs[i].key.equals(classKey)) classIdx = i;
        }
        // Trường "Lớp" đã có label riêng rồi nên giá trị chỉ cần tên ngắn (Mầm/Chồi/Lá), không
        // lặp lại chữ "Lớp" trong value.
        String classLabel = classDefs[classIdx].label;
        classTrigger.setText(classLabel.startsWith("Lớp ") ? classLabel.substring(4) : classLabel);
        final int fClassIdx = classIdx;
        classTrigger.setOnClickListener(v -> UiUtil.showDropdown(this, classTrigger, classLabels, fClassIdx,
                UiUtil.ContextColor(this, R.color.live_dim), idx -> {
                    classKey = classDefs[idx].key; compStudentIndex = 0;
                    renderAll();
                }));
    }

    // ── ACTION ZONE (trái, nhỏ) — nội dung đổi theo scene ───────────────────────────────────
    private void renderActionZone() {
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
        String[] tabs = {"roster:Lớp học", "competency:Năng lực", "history:Lịch sử"};
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
        if ("summary".equals(view)) renderSummary();
    }

    private void renderReportZone() {
        if ("playing".equals(scene)) { showReportView("live"); renderLive(); return; }
        showReportView(reportTab.equals("live") || reportTab.equals("summary") ? "roster" : reportTab);
        renderRoster();
        renderCompetencyChips();
    }

    // ── Tab "Lớp học": Tên + 1 cột môn đang chọn + [Lượt] + Tổng ────────────────────────────
    private void renderRoster() {
        MockData.ClassData cls = MockData.classData(classKey);
        int catCount = categoryNames.length;

        notPlayedStrip.setVisibility(cls.notPlayedNames.isEmpty() ? View.GONE : View.VISIBLE);
        if (!cls.notPlayedNames.isEmpty()) {
            notPlayedStrip.setText("Chưa chơi (" + cls.notPlayedNames.size() + "): "
                    + android.text.TextUtils.join(", ", cls.notPlayedNames));
        }

        boolean showGameCol = "select".equals(scene) && selectedGameName != null;
        gamePreviewStrip.setVisibility(showGameCol ? View.VISIBLE : View.GONE);
        if (showGameCol) gamePreviewStrip.setText("Đang xem số lượt đã chơi " + displayNameOf(selectedGameName) + " — cột \"Lượt\".");

        // header
        rosterHeader.removeAllViews();
        rosterHeader.addView(headerCell("Học sinh", "name", new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.4f)));
        String catLabel = domain < categoryNames.length ? categoryNames[domain] : "Môn";
        rosterHeader.addView(headerCell(catLabel, "cat", new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.3f)));
        if (showGameCol) rosterHeader.addView(headerCell("Lượt", null, new LinearLayout.LayoutParams(UiUtil.dp(this, 46), ViewGroup.LayoutParams.WRAP_CONTENT)));
        rosterHeader.addView(headerCell("Tổng", "total", new LinearLayout.LayoutParams(UiUtil.dp(this, 50), ViewGroup.LayoutParams.WRAP_CONTENT)));

        List<MockData.Student> rows = new ArrayList<>(cls.playedStudents);
        final int dir = rosterSortDir;
        java.util.Collections.sort(rows, (a, b) -> {
            int cmp;
            if ("name".equals(rosterSortKey)) cmp = a.name.compareTo(b.name);
            else if ("total".equals(rosterSortKey)) cmp = Integer.compare(a.total(catCount), b.total(catCount));
            else cmp = Integer.compare(a.scoreOr0(domain), b.scoreOr0(domain));
            return cmp * dir;
        });

        rosterBody.removeAllViews();
        for (MockData.Student s : rows) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(0, UiUtil.dp(this, 7), 0, UiUtil.dp(this, 7));
            row.setLayoutParams(new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

            LinearLayout nameCell = new LinearLayout(this);
            nameCell.setOrientation(LinearLayout.HORIZONTAL);
            nameCell.setGravity(Gravity.CENTER_VERTICAL);
            nameCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.4f));
            TextView av = UiUtil.makeAvatar(this, s.avatar, 26);
            LinearLayout.LayoutParams avLp = (LinearLayout.LayoutParams) av.getLayoutParams();
            avLp.rightMargin = UiUtil.dp(this, 8);
            nameCell.addView(av);
            TextView nm = UiUtil.label(this, s.name, 13f, R.color.text, true);
            nameCell.addView(nm);
            row.addView(nameCell);

            boolean isMin = s.weakestCategory(catCount) == domain;
            LinearLayout catCell = UiUtil.makeBarCell(this, s.scoreOr0(domain), isMin);
            catCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.3f));
            row.addView(catCell);

            if (showGameCol) {
                Integer plays = s.playsByGame.get(selectedGameName);
                TextView gameCell = UiUtil.label(this, plays != null ? String.valueOf(plays) : "—", 12f, R.color.accent, false);
                gameCell.setGravity(Gravity.END);
                gameCell.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 46), ViewGroup.LayoutParams.WRAP_CONTENT));
                row.addView(gameCell);
            }

            TextView totalCell = UiUtil.label(this, String.valueOf(s.total(catCount)), 14f, R.color.text, true);
            totalCell.setGravity(Gravity.END);
            totalCell.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 50), ViewGroup.LayoutParams.WRAP_CONTENT));
            row.addView(totalCell);

            row.setOnClickListener(v -> {
                compStudentIndex = cls.playedStudents.indexOf(s);
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

    // ── Tab "Năng lực": list điểm theo môn (không vẽ ngũ giác — nhiều môn sẽ rối) ───────────
    private void renderCompetencyChips() {
        MockData.ClassData cls = MockData.classData(classKey);
        if (compStudentIndex >= cls.playedStudents.size()) compStudentIndex = 0;
        compChips.removeAllViews();
        for (int i = 0; i < cls.playedStudents.size(); i++) {
            MockData.Student s = cls.playedStudents.get(i);
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
            TextView av = UiUtil.makeAvatar(this, s.avatar, 20);
            LinearLayout.LayoutParams avLp = (LinearLayout.LayoutParams) av.getLayoutParams();
            avLp.rightMargin = UiUtil.dp(this, 8);
            chip.addView(av);
            chip.addView(UiUtil.label(this, s.name, 12f, sel ? R.color.text : R.color.text_dim, true));
            final int idx = i;
            chip.setOnClickListener(v -> { compStudentIndex = idx; renderCompetencyChips(); });
            compChips.addView(chip);
        }
        renderCompetency();
    }

    private void renderCompetency() {
        MockData.ClassData cls = MockData.classData(classKey);
        if (cls.playedStudents.isEmpty()) { compName.setText("—"); compFlags.setText(""); compScoreList.removeAllViews(); return; }
        MockData.Student s = cls.playedStudents.get(compStudentIndex);
        int catCount = categoryNames.length;
        compName.setText(s.name);
        compFlags.setText("slow-sure".equals(s.flag) ? "Chậm mà chắc" : "fast-careless".equals(s.flag) ? "Nhanh, hay ẩu" : "");
        int weakest = s.weakestCategory(catCount);

        compScoreList.removeAllViews();
        for (int c = 0; c < catCount; c++) {
            boolean isMin = c == weakest;
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(0, UiUtil.dp(this, 8), 0, UiUtil.dp(this, 8));

            TextView cname = UiUtil.label(this, categoryNames[c], 12.5f, isMin ? R.color.bad : R.color.text_dim, isMin);
            cname.setLayoutParams(new LinearLayout.LayoutParams(UiUtil.dp(this, 120), ViewGroup.LayoutParams.WRAP_CONTENT));
            row.addView(cname);

            LinearLayout barCell = UiUtil.makeBarCell(this, s.scoreOr0(c), isMin);
            barCell.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
            row.addView(barCell);

            View divider = new View(this);
            divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
            LinearLayout.LayoutParams dLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1));
            compScoreList.addView(row);
            compScoreList.addView(divider, dLp);
        }
    }

    // ── Tab "Lịch sử" — mock tĩnh, build 1 lần lúc onCreate ─────────────────────────────────
    private void buildHistoryPanel() {
        String[][] rows = {
                {"10:24:41", "Minh An", "Counting5", "✓", "2.8s"},
                {"10:24:33", "Player_2", "Counting5", "✗", "6.2s"},
                {"10:21:07", "Bảo Ngọc", "Counting", "✓", "3.1s"},
                {"10:18:52", "Khánh Vy", "AddNumber5", "✓", "4.0s"},
                {"10:15:19", "Thảo My", "TestTongHop", "✗", "7.5s"},
        };
        for (String[] r : rows) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setPadding(0, UiUtil.dp(this, 8), 0, UiUtil.dp(this, 8));
            row.addView(cellText(r[0], 70, R.color.text_faint));
            row.addView(cellText(r[1], 0, R.color.text));
            row.addView(cellText(r[2], 0, R.color.text_dim));
            TextView mark = cellText(r[3], 30, "✓".equals(r[3]) ? R.color.good : R.color.bad);
            mark.setGravity(Gravity.CENTER);
            row.addView(mark);
            TextView rt = cellText(r[4], 60, R.color.text_dim);
            rt.setGravity(Gravity.END);
            row.addView(rt);
            historyBody.addView(row);
            View divider = new View(this);
            divider.setBackgroundColor(UiUtil.ContextColor(this, R.color.border_soft));
            historyBody.addView(divider, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, UiUtil.dp(this, 1)));
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
                {"Thời lượng", "—"},
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
            row.addView(UiUtil.label(this, (i + 1) + ". " + p.name + " — " + p.correct + " điểm", 12.5f, R.color.text, true));
            row.addView(UiUtil.label(this, p.answered + " câu · " + p.correct + " đúng · TB " + String.format(java.util.Locale.US, "%.1f", p.avgTime) + "s", 10.5f, R.color.text_faint, false));
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

        TextView score = UiUtil.label(this, scoreText, 30f, R.color.text, true);
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

        if (!unityStarted) {
            dismissLogoPresentation(); // nhường display phụ lại cho Unity
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
        scene = "playing";
        renderAll();
    }

    private void onPauseClicked() {
        paused = !paused;
        sendToUnity(paused ? "OnPauseRequested" : "OnResumeRequested", "");
        renderActionZone();
    }

    private void onStopClicked() {
        if (paused) sendToUnity("OnResumeRequested", "");
        sendToUnity("OnStopRequested", "");
        backToMenu();
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
        scene = "ended";
        Log.i(TAG, "backToMenu: scene=ended, renderAll()");
        renderAll();
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
