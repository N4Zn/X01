using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/MenuScene";
    private const string ScenePath = "Assets/Game/Scenes/MenuScene/MenuScene.unity";
    private const string SpriteFolder = "Assets/Game/Textures/MenuScene";
    private const string TeamSpriteFolder = "Assets/Game/Textures/TeamSelectScene";

    // Design reference resolution
    private const float REF_W = 1024f;
    private const float REF_H = 600f;

    // Design colors
    private static readonly Color BG_COLOR = new Color(0.96f, 0.91f, 0.82f, 1f);         // warm beige
    private static readonly Color TEAM_BAR_COLOR = new Color(0.85f, 0.78f, 0.65f, 0.9f);  // tan
    private static readonly Color GRID_BG_COLOR = new Color(0.92f, 0.86f, 0.76f, 0.6f);   // light tan
    private static readonly Color START_BTN_COLOR = new Color(0.82f, 0.72f, 0.55f, 1f);   // warm brown
    private static readonly Color VS_COLOR = new Color(0.95f, 0.55f, 0.15f, 1f);          // orange
    private static readonly Color BLUE_TEAM_COLOR = new Color(0.3f, 0.6f, 0.95f, 1f);
    private static readonly Color RED_TEAM_COLOR = new Color(0.95f, 0.35f, 0.35f, 1f);

    // Category tab sprite names — 6 tab tương ứng GameRegistry.CATEGORY_COUNT.
    // Nếu không có sprite riêng, builder sẽ dùng màu nền + text label.
    private static readonly string[] TAB_SPRITES = {
        "tab_tinh_toan", "tab_phan_tich", "tab_hinh_anh",
        "tab_tri_nho",   "tab_nhan_biet", "tab_nhan_biet"
    };

    // Game icon sprites [col 0-5, row 0-3] cho 4 rows × 6 cols = 24 slot.
    // Chỉ dùng khi build scene tĩnh; runtime UpdateGrid() load từ Resources/GameIcons/
    private static readonly string[,] GAME_ICON_SPRITES = {
        { "icon_tinh_toan_2", "icon_tinh_toan_1", "icon_unknown", "icon_unknown" },
        { "icon_phan_tich_1", "icon_phan_tich_2", "icon_unknown", "icon_unknown" },
        { "icon_unknown",     "icon_unknown",     "icon_unknown", "icon_unknown" },
        { "icon_unknown",     "icon_unknown",     "icon_unknown", "icon_unknown" },
        { "icon_unknown",     "icon_unknown",     "icon_unknown", "icon_unknown" },
        { "icon_unknown",     "icon_unknown",     "icon_unknown", "icon_unknown" },
    };

    /// <summary>
    /// Tạo scene mới hoàn toàn từ đầu — MỌI chỉnh sửa thủ công trong Inspector sẽ BỊ MẤT.
    /// Chỉ dùng lần đầu hoặc khi muốn reset hoàn toàn.
    /// </summary>
    [MenuItem("Tools/MenuScene/Build Menu Scene (Reset - Xoa chinh sua tay)")]
    public static void BuildMenuScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("MenuSceneBuilder: Cannot build scene while in Play mode.");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Xác nhận Reset Scene",
            "Thao tác này sẽ XOÁ TOÀN BỘ chỉnh sửa thủ công trong MenuScene và build lại từ đầu.\n\nBạn có chắc không?",
            "Xoá và build lại",
            "Huỷ"
        );
        if (!confirm) return;

        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MenuSceneBuilder: finished generating MenuScene.");
    }

    /// <summary>
    /// Chỉ wire lại references (script → UI objects) mà KHÔNG đụng vào vị trí/kích thước.
    /// Dùng sau khi đã chỉnh layout thủ công — giữ nguyên mọi transform đã chỉnh.
    /// </summary>
    [MenuItem("Tools/MenuScene/Rewire References Only (Giu nguyen vi tri)")]
    public static void RewireReferencesOnly()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("MenuSceneBuilder: Cannot rewire while in Play mode.");
            return;
        }

        if (!System.IO.File.Exists(ToAbsolutePath(ScenePath)))
        {
            Debug.LogWarning("MenuSceneBuilder: MenuScene chưa tồn tại. Chạy Build trước.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Tìm root objects
        var root = GameObject.Find("MenuSceneRoot");
        if (root == null) { Debug.LogError("Không tìm thấy MenuSceneRoot trong scene!"); return; }

        var view       = root.GetComponent<MenuSceneView>();
        var controller = root.GetComponent<MenuSceneController>();
        if (view == null || controller == null)
        {
            Debug.LogError("Không tìm thấy MenuSceneView / MenuSceneController!"); return;
        }

        // Thu thập tabs
        var tabButtonList = new System.Collections.Generic.List<Button>();
        var tabImageList  = new System.Collections.Generic.List<Image>();
        for (int i = 0; i < GameRegistry.CATEGORY_COUNT; i++)
        {
            var tabGo = GameObject.Find("CategoryTab_" + i);
            if (tabGo != null)
            {
                tabButtonList.Add(tabGo.GetComponent<Button>());
                tabImageList.Add(tabGo.GetComponent<Image>());
            }
        }

        // Thu thập game slots
        var slotList = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < GameRegistry.MAX_PER_CATEGORY; i++)
        {
            var slotGo = GameObject.Find("GameSlot_" + i);
            if (slotGo != null) slotList.Add(slotGo);
        }

        // Tìm object theo tên — bao gồm cả object đang inactive (SetActive=false)
        var allGOs   = Resources.FindObjectsOfTypeAll<GameObject>();
        System.Func<string, GameObject> findByName = n =>
            System.Array.Find(allGOs, g => g.scene == scene && g.name == n);

        var backBtn      = findByName("BackButton_Hidden");
        var homeBtn      = findByName("HomeButton");
        var settingsBtn  = findByName("SettingsButton_Hidden");
        var blueLabel    = findByName("BlueTeamNameText");
        var redLabel     = findByName("RedTeamNameText");
        var blueAvatar   = findByName("BlueTeamAvatarContainer");
        var redAvatar    = findByName("RedTeamAvatarContainer");
        var slotTemplate = findByName("TeamAvatarSlotTemplate");
        var randomBtn    = findByName("RandomSelectButton");
        var startBtn     = findByName("StartButton");

        if (tabButtonList.Count == 0 || slotList.Count == 0)
        {
            Debug.LogWarning("MenuSceneBuilder: Không tìm thấy đủ tab/slot. " +
                             "Hãy đảm bảo tên object khớp (CategoryTab_0..N, GameSlot_0..M).");
        }

        WireReferences(view, controller,
            backBtn, homeBtn, settingsBtn,
            blueLabel, redLabel, blueAvatar, redAvatar, slotTemplate,
            tabButtonList.ToArray(), tabImageList.ToArray(),
            slotList.ToArray(),
            randomBtn, startBtn);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddSceneToBuildSettings(ScenePath);
        Debug.Log($"MenuSceneBuilder: Rewire xong — {tabButtonList.Count} tab, {slotList.Count} slot. Vị trí giữ nguyên.");
    }

    /// <summary>
    /// Thêm các slot còn thiếu vào GameGridPanel mà KHÔNG đụng vào slot đã có.
    /// Dùng khi đổi số slot (ví dụ 18 → 24) mà không muốn mất layout đã chỉnh.
    /// Sau khi thêm slot xong sẽ tự Rewire để cập nhật gameSlots[].
    /// </summary>
    [MenuItem("Tools/MenuScene/Patch Grid — Add Missing Slots Only")]
    public static void PatchGrid()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("MenuSceneBuilder: Cannot patch while in Play mode.");
            return;
        }

        if (!System.IO.File.Exists(ToAbsolutePath(ScenePath)))
        {
            Debug.LogWarning("MenuSceneBuilder: MenuScene chưa tồn tại. Chạy Build trước.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Tìm GameGridPanel — dùng FindObjectsOfTypeAll để bắt cả inactive
        var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject gridPanel = System.Array.Find(allGOs, g => g.scene == scene && g.name == "GameGridPanel");
        if (gridPanel == null)
        {
            Debug.LogError("MenuSceneBuilder: Không tìm thấy GameGridPanel! Hãy chạy Build trước.");
            return;
        }

        const int SLOT_COUNT = 24; // 6 cols × 4 rows
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Game/Textures/Common/circle_white_256.png");

        int added   = 0;
        int skipped = 0;

        // GridLayoutGroup trên gridPanel tự xếp vị trí — chỉ cần thêm child đúng thứ tự
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            string slotName = "GameSlot_" + i;

            // Bỏ qua nếu slot đã tồn tại trong scene (kể cả inactive)
            bool exists = System.Array.Exists(allGOs,
                g => g.scene == scene && g.name == slotName);
            if (exists) { skipped++; continue; }

            CreateSlot(i, gridPanel.transform, circleSprite);
            added++;
        }

        if (added == 0)
        {
            Debug.Log($"MenuSceneBuilder: PatchGrid — tất cả {SLOT_COUNT} slot đã tồn tại, không cần thêm.");
            EditorUtility.DisplayDialog("Patch Grid", $"Tất cả {SLOT_COUNT} slot đã tồn tại.\nKhông cần thêm gì.", "OK");
            return;
        }

        Debug.Log($"MenuSceneBuilder: PatchGrid — thêm {added} slot mới, bỏ qua {skipped} slot đã có.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Rewire để cập nhật gameSlots[] với toàn bộ 24 slot
        RewireReferencesOnly();

        EditorUtility.DisplayDialog("Patch Grid", $"Đã thêm {added} slot mới.\n{skipped} slot cũ giữ nguyên.\ngameSlots[] đã được Rewire.", "OK");
    }

    [MenuItem("Tools/MenuScene/Add Camera To Menu Scene")]
    public static void AddCameraToMenuScene()
    {
        if (!File.Exists(ToAbsolutePath(ScenePath)))
        {
            Debug.LogWarning("MenuSceneBuilder: MenuScene does not exist yet. Run Build Menu Scene first.");
            return;
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera existingMain = Camera.main;
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (existingMain == null && (allCameras == null || allCameras.Length == 0))
        {
            CreateMainCamera();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MenuSceneBuilder: added Main Camera to MenuScene.");
            return;
        }

        if (existingMain == null && allCameras != null && allCameras.Length > 0)
        {
            allCameras[0].gameObject.tag = "MainCamera";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MenuSceneBuilder: existing camera tagged as MainCamera and scene saved.");
            return;
        }

        Debug.Log("MenuSceneBuilder: MenuScene already has a Main Camera.");
    }

    // ===== Scene Build =====

    private static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMainCamera();

        // Canvas
        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(REF_W, REF_H);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root
        GameObject root = new GameObject("MenuSceneRoot");
        MenuSceneController controller = root.AddComponent<MenuSceneController>();
        MenuSceneView view = root.AddComponent<MenuSceneView>();

        // ===== Background (sprite) =====
        Sprite bgSprite = LoadSprite("bg_menu_scene");
        GameObject bg = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bg.GetComponent<Image>();
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
        }
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;
        SetAnchors(bg, 0, 0, 1, 1);

        // ===== Home Button (top left) =====
        Sprite homeSprite = LoadSprite("btn_home");
        GameObject homeButton = CreateImageButton("HomeButton", canvasGo.transform, homeSprite);
        SetAnchors(homeButton, 0.01f, 0.88f, 0.065f, 0.99f);

        // ===== Team Banner (same sprites as TeamSelectScene) =====
        Sprite teamBlueSprite = LoadSpriteFromFolder(TeamSpriteFolder, "TeamBlueBar");
        Sprite teamRedSprite = LoadSpriteFromFolder(TeamSpriteFolder, "TeamRedBar");
        Sprite vsSprite = LoadSpriteFromFolder(TeamSpriteFolder, "VsButton");

        GameObject teamBar = CreatePanel("TeamInfoBar", canvasGo.transform, new Color(0, 0, 0, 0));
        teamBar.GetComponent<Image>().raycastTarget = false;
        SetAnchors(teamBar, 0.08f, 0.83f, 0.92f, 0.99f);

        // Blue team panel (sprite bar — circle on LEFT, bar extends right)
        GameObject bluePanel = new GameObject("BlueTeamPanel", typeof(RectTransform), typeof(Image));
        bluePanel.transform.SetParent(teamBar.transform, false);
        Image bluePanelImg = bluePanel.GetComponent<Image>();
        if (teamBlueSprite != null)
        {
            bluePanelImg.sprite = teamBlueSprite;
            bluePanelImg.type = Image.Type.Simple;
            bluePanelImg.color = Color.white;
        }
        else
            bluePanelImg.color = new Color(0.7f, 0.85f, 1f, 0.8f);
        bluePanelImg.preserveAspect = true;
        SetAnchors(bluePanel, 0.08f, 0.10f, 0.42f, 0.90f);

        // "To 1" team label
        GameObject blueTeamLabel = CreateText("BlueTeamNameText", bluePanel.transform, "To 1", 16, TextAnchor.MiddleCenter);
        SetAnchors(blueTeamLabel, 0.12f, 0.05f, 0.38f, 0.95f);
        blueTeamLabel.GetComponent<Text>().color = Color.white;
        blueTeamLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Blue avatar container
        GameObject blueAvatarContainer = new GameObject("BlueTeamAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        blueAvatarContainer.transform.SetParent(bluePanel.transform, false);
        SetAnchors(blueAvatarContainer, 0.38f, 0.02f, 0.95f, 0.98f);
        var blueLayout = blueAvatarContainer.GetComponent<GridLayoutGroup>();
        blueLayout.cellSize = new Vector2(30f, 30f);
        blueLayout.spacing = new Vector2(2f, 1f);
        blueLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        blueLayout.constraintCount = 3;
        blueLayout.childAlignment = TextAnchor.MiddleCenter;

        // VS icon (center, overlapping both panels)
        GameObject vsIcon = new GameObject("VsIcon", typeof(RectTransform), typeof(Image));
        vsIcon.transform.SetParent(teamBar.transform, false);
        Image vsImg = vsIcon.GetComponent<Image>();
        if (vsSprite != null)
        {
            vsImg.sprite = vsSprite;
            vsImg.preserveAspect = true;
        }
        vsImg.color = Color.white;
        vsImg.raycastTarget = false;
        SetAnchors(vsIcon, 0.42f, -0.10f, 0.58f, 1.10f);

        // Red team panel (sprite bar — bar extends left, circle on RIGHT)
        GameObject redPanel = new GameObject("RedTeamPanel", typeof(RectTransform), typeof(Image));
        redPanel.transform.SetParent(teamBar.transform, false);
        Image redPanelImg = redPanel.GetComponent<Image>();
        if (teamRedSprite != null)
        {
            redPanelImg.sprite = teamRedSprite;
            redPanelImg.type = Image.Type.Simple;
            redPanelImg.color = Color.white;
        }
        else
            redPanelImg.color = new Color(1f, 0.75f, 0.75f, 0.8f);
        redPanelImg.preserveAspect = true;
        SetAnchors(redPanel, 0.58f, 0.10f, 0.92f, 0.90f);

        // Red avatar container
        GameObject redAvatarContainer = new GameObject("RedTeamAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        redAvatarContainer.transform.SetParent(redPanel.transform, false);
        SetAnchors(redAvatarContainer, 0.05f, 0.02f, 0.62f, 0.98f);
        var redLayout = redAvatarContainer.GetComponent<GridLayoutGroup>();
        redLayout.cellSize = new Vector2(30f, 30f);
        redLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        redLayout.constraintCount = 3;
        redLayout.spacing = new Vector2(2f, 1f);
        redLayout.childAlignment = TextAnchor.MiddleCenter;

        // "To 2" team label
        GameObject redTeamLabel = CreateText("RedTeamNameText", redPanel.transform, "To 2", 16, TextAnchor.MiddleCenter);
        SetAnchors(redTeamLabel, 0.62f, 0.05f, 0.88f, 0.95f);
        redTeamLabel.GetComponent<Text>().color = Color.white;
        redTeamLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Avatar slot template (hidden, instantiated at runtime)
        GameObject teamAvatarSlotTemplate = CreatePanel("TeamAvatarSlotTemplate", canvasGo.transform, new Color(0.7f, 0.85f, 1f, 1f));
        RectTransform slotRt = teamAvatarSlotTemplate.GetComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(36f, 36f);
        GameObject slotText = CreateText("Initial", teamAvatarSlotTemplate.transform, "?", 18, TextAnchor.MiddleCenter);
        SetAnchors(slotText, 0, 0, 1, 1);
        slotText.GetComponent<Text>().color = Color.white;
        slotText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        teamAvatarSlotTemplate.SetActive(false);

        // ===== Category Tabs (GameRegistry.CATEGORY_COUNT tabs) =====
        // Mỗi tab = sprite nếu có + text label tên category ở trên.
        int tabCount = GameRegistry.CATEGORY_COUNT;
        Button[] tabButtons = new Button[tabCount];
        Image[] tabImages   = new Image[tabCount];

        float tabAreaY0 = 0.68f;
        float tabAreaY1 = 0.80f;
        float tabWidth  = 0.14f;
        float tabGap    = 0.008f;
        // Căn giữa toàn bộ dải tab trên canvas
        float totalTabW = tabCount * tabWidth + (tabCount - 1) * tabGap;
        float tabStartX = (1f - totalTabW) * 0.5f;

        for (int i = 0; i < tabCount; i++)
        {
            float x0 = tabStartX + i * (tabWidth + tabGap);
            float x1 = x0 + tabWidth;

            Sprite tabSprite = (i < TAB_SPRITES.Length) ? LoadSprite(TAB_SPRITES[i]) : null;

            GameObject tab = new GameObject("CategoryTab_" + i,
                typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(canvasGo.transform, false);

            Image tabImg = tab.GetComponent<Image>();
            if (tabSprite != null)
            {
                tabImg.sprite      = tabSprite;
                tabImg.type        = Image.Type.Sliced;
                tabImg.preserveAspect = true;
            }
            else
            {
                tabImg.color = new Color(0.55f, 0.75f, 0.95f, 0.9f);
            }

            RectTransform tabRT = tab.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(x0, tabAreaY0);
            tabRT.anchorMax = new Vector2(x1, tabAreaY1);
            tabRT.offsetMin = Vector2.zero;
            tabRT.offsetMax = Vector2.zero;

            // Text label — tên category đọc từ GameRegistry
            string catName = (i < GameRegistry.CategoryNames.Length)
                ? GameRegistry.CategoryNames[i] : ("Cat" + i);
            GameObject tabLabel = CreateText("Label", tab.transform, catName, 11, TextAnchor.MiddleCenter);
            SetAnchors(tabLabel, 0.04f, 0.04f, 0.96f, 0.96f);
            tabLabel.GetComponent<Text>().color      = Color.white;
            tabLabel.GetComponent<Text>().fontStyle  = FontStyle.Bold;
            tabLabel.GetComponent<Text>().raycastTarget = false;

            var btnComp = tab.GetComponent<Button>();
            var nav = btnComp.navigation;
            nav.mode = Navigation.Mode.None;
            btnComp.navigation = nav;

            tabButtons[i] = btnComp;
            tabImages[i]  = tabImg;
        }

        // ===== Game Grid — 3 rows × 6 cols = 18 slots (GameRegistry.MAX_PER_CATEGORY) =====
        // Cấu trúc mỗi slot:
        //   GameSlot_X  (RectTransform, Image trong suốt — container)
        //     ├── Highlight  (Image — vòng glow, tên "Highlight" để InitView.Find() tìm được)
        //     └── Icon       (Image + Button — tên "Icon" để InitView.Find() tìm được)

        GameObject gridPanel = CreatePanel("GameGridPanel", canvasGo.transform, new Color(0, 0, 0, 0));
        SetAnchors(gridPanel, 0.02f, 0.14f, 0.98f, 0.67f);

        // GridLayoutGroup — Unity tự sắp xếp slot, không cần tính anchor thủ công.
        // cellSize tính trên canvas 1024×600:
        //   panel w ≈ 0.96×1024 = 983px → (983 - 2×4 padding - 5×6 spacing) / 6 ≈ 155px/cell
        //   panel h ≈ 0.53×600  = 318px → (318 - 2×4 padding - 3×6 spacing) / 4 ≈  72px/cell
        var glg = gridPanel.AddComponent<GridLayoutGroup>();
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 6;
        glg.cellSize        = new Vector2(155f, 72f);
        glg.spacing         = new Vector2(6f, 6f);
        glg.padding         = new RectOffset(4, 4, 4, 4);
        glg.childAlignment  = TextAnchor.UpperLeft;
        glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis       = GridLayoutGroup.Axis.Horizontal;

        const int SLOT_COUNT = 24; // 6 cols × 4 rows
        GameObject[] gameSlotObjects = new GameObject[SLOT_COUNT];
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Game/Textures/Common/circle_white_256.png");

        for (int i = 0; i < SLOT_COUNT; i++)
            gameSlotObjects[i] = CreateSlot(i, gridPanel.transform, circleSprite);

        // ===== Bottom Bar =====
        GameObject bottomBar = CreatePanel("BottomBar", canvasGo.transform, new Color(0, 0, 0, 0));
        SetAnchors(bottomBar, 0f, 0f, 1f, 0.16f);

        // Random select button (left side)
        Sprite randomSprite = LoadSprite("btn_random_select");
        GameObject randomButton = CreateImageButton("RandomSelectButton", bottomBar.transform, randomSprite);
        SetAnchors(randomButton, 0.03f, 0.10f, 0.30f, 0.90f);

        // Start button (center) — sprite "Bat Dau"
        Sprite startSprite = LoadSprite("btn_start");
        GameObject startButtonGo = CreateImageButton("StartButton", bottomBar.transform, startSprite);
        SetAnchors(startButtonGo, 0.33f, 0.08f, 0.67f, 0.92f);

        // Hidden text for SetStartButtonEnabled reference (not displayed)
        GameObject startText = CreateText("Text", startButtonGo.transform, "", 1, TextAnchor.MiddleCenter);
        SetAnchors(startText, 0, 0, 0, 0);
        startText.GetComponent<Text>().color = Color.clear;

        // ===== Wire References =====
        // Settings button — not visible in design but keep reference for future
        GameObject settingsButton = new GameObject("SettingsButton_Hidden", typeof(RectTransform), typeof(Button));
        settingsButton.transform.SetParent(canvasGo.transform, false);
        settingsButton.SetActive(false);

        // Back button — use homeButton as back too (design only shows home button)
        GameObject backButton = new GameObject("BackButton_Hidden", typeof(RectTransform), typeof(Button));
        backButton.transform.SetParent(canvasGo.transform, false);
        backButton.SetActive(false);

        WireReferences(view, controller,
            backButton, homeButton, settingsButton,
            blueTeamLabel, redTeamLabel, blueAvatarContainer, redAvatarContainer,
            teamAvatarSlotTemplate,
            tabButtons, tabImages,
            gameSlotObjects,
            randomButton, startButtonGo);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Wire References =====

    // Helper: gán 1 component vào SerializedProperty — bỏ qua nếu go hoặc component null
    static void SetProp<T>(SerializedProperty prop, GameObject go) where T : Component
    {
        if (prop == null) return;
        prop.objectReferenceValue = go != null ? go.GetComponent<T>() : null;
    }
    static void SetPropObj(SerializedProperty prop, UnityEngine.Object obj)
    {
        if (prop != null) prop.objectReferenceValue = obj;
    }

    private static void WireReferences(
        MenuSceneView view, MenuSceneController controller,
        GameObject backButton, GameObject homeButton, GameObject settingsButton,
        GameObject blueTeamNameText, GameObject redTeamNameText,
        GameObject blueAvatarContainer, GameObject redAvatarContainer,
        GameObject teamAvatarSlotTemplate,
        Button[] tabButtons, Image[] tabImages,
        GameObject[] gameSlots,
        GameObject randomButton, GameObject startButton)
    {
        SerializedObject viewSo = new SerializedObject(view);

        // Top bar (null-safe — hidden buttons có thể không tìm được qua Find())
        SetProp<Button>(viewSo.FindProperty("backButton"),     backButton);
        SetProp<Button>(viewSo.FindProperty("homeButton"),     homeButton);
        SetProp<Button>(viewSo.FindProperty("settingsButton"), settingsButton);

        // Team banner
        SetProp<Text>     (viewSo.FindProperty("blueTeamNameText"),       blueTeamNameText);
        SetProp<Text>     (viewSo.FindProperty("redTeamNameText"),         redTeamNameText);
        SetPropObj        (viewSo.FindProperty("blueTeamAvatarContainer"), blueAvatarContainer?.transform);
        SetPropObj        (viewSo.FindProperty("redTeamAvatarContainer"),  redAvatarContainer?.transform);
        SetPropObj        (viewSo.FindProperty("teamAvatarSlotTemplate"),  teamAvatarSlotTemplate);

        // Avatar bg + character sprites
        viewSo.FindProperty("avatarBgBlue").objectReferenceValue  = LoadSpriteFromFolder("Assets/Game/Textures/TeamSelectScene", "AvatarBgBlue");
        viewSo.FindProperty("avatarBgRed").objectReferenceValue   = LoadSpriteFromFolder("Assets/Game/Textures/TeamSelectScene", "AvatarBgRed");
        viewSo.FindProperty("charBodySprite").objectReferenceValue = LoadSpriteFromFolder("Assets/Game/Textures/PlayerPanel", "body_male");
        SerializedProperty hairArr = viewSo.FindProperty("charHairSprites");
        hairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            hairArr.GetArrayElementAtIndex(i).objectReferenceValue =
                LoadSpriteFromFolder("Assets/Game/Textures/PlayerPanel", "char_hair_" + (i + 1));

        // Category tabs
        SetArrayProperty(viewSo, "categoryTabButtons", tabButtons);
        SetArrayProperty(viewSo, "categoryTabImages",  tabImages);

        // Game grid slots (runtime InitView() tự tìm Button/Icon/Highlight trong children)
        SetArrayProperty(viewSo, "gameSlots", gameSlots);

        // Bottom bar
        SetProp<Button>(viewSo.FindProperty("randomSelectButton"), randomButton);
        SetProp<Button>(viewSo.FindProperty("startButton"),        startButton);
        SetPropObj(viewSo.FindProperty("startButtonText"),
            startButton != null ? startButton.GetComponentInChildren<Text>() : null);

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // Controller
        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("menuSceneView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArrayProperty<T>(SerializedObject so, string propertyName, T[] items) where T : UnityEngine.Object
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    // ===== Sprite Loading =====

    private static Sprite LoadSprite(string name)
    {
        return LoadSpriteFromFolder(SpriteFolder, name);
    }

    private static Sprite LoadSpriteFromFolder(string folder, string name)
    {
        string path = folder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning("MenuSceneBuilder: Sprite not found at " + path + ". Ensure texture import settings have Texture Type = Sprite.");

            // Try to fix import settings
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return sprite;
    }

    // ===== Helpers =====

    private static GameObject CreateImageButton(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        img.color = Color.white;
        // No color tint transition
        var btn = go.GetComponent<Button>();
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        return go;
    }

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BG_COLOR;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        cameraGo.transform.rotation = Quaternion.identity;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    /// <summary>
    /// Tạo 1 GameSlot với đúng hierarchy: Slot > Highlight + Icon.
    /// Kích thước và vị trí do GridLayoutGroup trên GameGridPanel quản lý.
    /// Dùng chung bởi BuildScene() và PatchGrid().
    /// </summary>
    private static GameObject CreateSlot(int idx, Transform parent, Sprite circleSprite)
    {
        // ── Slot container — GridLayoutGroup tự set kích thước & vị trí ────────
        GameObject slot = new GameObject("GameSlot_" + idx, typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);
        slot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        slot.GetComponent<Image>().raycastTarget = false;

        // ── Highlight (tên chính xác "Highlight" — InitView dùng Transform.Find) ──
        GameObject hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        hlGo.transform.SetParent(slot.transform, false);
        Image hlImg = hlGo.GetComponent<Image>();
        if (circleSprite != null) hlImg.sprite = circleSprite;
        hlImg.preserveAspect = true;
        hlImg.color = new Color(1f, 0.95f, 0.35f, 0f); // alpha=0 → ẩn mặc định
        hlImg.raycastTarget = false;
        SetAnchors(hlGo, 0f, 0f, 1f, 1f); // phủ toàn slot

        // ── Icon (tên chính xác "Icon" — InitView dùng Transform.Find) ──────
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Button));
        iconGo.transform.SetParent(slot.transform, false);
        iconGo.GetComponent<Image>().preserveAspect = true;
        iconGo.GetComponent<Image>().color = Color.white;
        iconGo.GetComponent<Button>().interactable = true;
        SetAnchors(iconGo, 0.06f, 0.06f, 0.94f, 0.94f); // inset nhẹ để có khoảng trống trực quan

        return slot;
    }

    private static GameObject CreateText(string name, Transform parent, string content, int size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.alignment = anchor;
        text.fontSize = size;
        text.color = Color.white;
        text.font = GetBuiltinFont();
        return go;
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void SetAnchors(GameObject go, float minX, float minY, float maxX, float maxY)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Scenes");
        EnsureFolder(SceneFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void AddSceneToBuildSettings(string targetPath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == targetPath) return;
        }
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
        newScenes[scenes.Length] = new EditorBuildSettingsScene(targetPath, true);
        EditorBuildSettings.scenes = newScenes;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        return projectPath + "/" + assetPath;
    }
}
