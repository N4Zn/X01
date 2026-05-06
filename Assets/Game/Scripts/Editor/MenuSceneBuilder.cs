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

    // Category tab sprite names (order: Tinh toan, Phan tich, Hinh anh, Tri nho, Nhan biet)
    private static readonly string[] TAB_SPRITES = {
        "tab_tinh_toan", "tab_phan_tich", "tab_hinh_anh", "tab_tri_nho", "tab_nhan_biet"
    };

    // Game icon sprites per category [col, row] — all same size
    private static readonly string[,] GAME_ICON_SPRITES = {
        { "icon_tinh_toan_2", "icon_tinh_toan_1" },
        { "icon_phan_tich_1", "icon_phan_tich_2" },
        { "icon_unknown", "icon_unknown" },
        { "icon_unknown", "icon_unknown" },
        { "icon_unknown", "icon_unknown" }
    };

    [MenuItem("Tools/MenuScene/Build Menu Scene")]
    public static void BuildMenuScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("MenuSceneBuilder: Cannot build scene while in Play mode.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MenuSceneBuilder: finished generating MenuScene.");
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

        // ===== Category Tabs (5 pill buttons with sprite images) — editor-adjusted offsets =====
        Button[] tabButtons = new Button[5];
        Image[] tabImages = new Image[5];

        float tabAreaY0 = 0.68f;
        float tabAreaY1 = 0.80f;
        float tabWidth = 0.155f;
        float tabGap = 0.012f;
        float tabStartX = 0.045f;

        // Per-tab nudge offsets (anchoredPosition) captured from manual editor tweaks
        Vector2[] tabOffsets = new Vector2[]
        {
            new Vector2(21f, -1f),
            new Vector2(31.3f, 0f),
            new Vector2(44.8f, 1f),
            new Vector2(59f, 1f),
            new Vector2(61f, 2f),
        };

        for (int i = 0; i < 5; i++)
        {
            float x0 = tabStartX + i * (tabWidth + tabGap);
            float x1 = x0 + tabWidth;

            Sprite tabSprite = LoadSprite(TAB_SPRITES[i]);

            GameObject tab = new GameObject("CategoryTab_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            tab.transform.SetParent(canvasGo.transform, false);
            Image tabImg = tab.GetComponent<Image>();
            tabImg.sprite = tabSprite;
            tabImg.type = Image.Type.Sliced;
            tabImg.preserveAspect = true;
            RectTransform tabRT = tab.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(x0, tabAreaY0);
            tabRT.anchorMax = new Vector2(x1, tabAreaY1);
            tabRT.offsetMin = tabOffsets[i];
            tabRT.offsetMax = tabOffsets[i];

            tabButtons[i] = tab.GetComponent<Button>();
            tabImages[i] = tabImg;

            // Make button use no color transition (sprite already has visual)
            var btnComp = tab.GetComponent<Button>();
            var nav = btnComp.navigation;
            nav.mode = Navigation.Mode.None;
            btnComp.navigation = nav;
        }

        // ===== Game Grid (2 rows x 5 cols — equal size icons) =====
        GameObject gridPanel = CreatePanel("GameGridPanel", canvasGo.transform, new Color(0, 0, 0, 0));
        SetAnchors(gridPanel, 0.02f, 0.18f, 0.98f, 0.67f);

        Button[] gameButtons = new Button[10];
        Image[] gameIcons = new Image[10];
        Image[] gameHighlights = new Image[10];

        // Circular glow sprite (reused from Common textures)
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/Common/circle_white_256.png");

        float iconSize = 0.145f;   // same width for all icons
        float colWidth = 0.19f;
        float colStart = 0.025f;

        // Row centers (relative to gridPanel) — equal spacing
        float row0CenterY = 0.70f;
        float row1CenterY = 0.28f;
        float halfH = 0.20f;

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                int idx = row * 5 + col;

                float centerY = (row == 0) ? row0CenterY : row1CenterY;
                float colCenterX = colStart + col * colWidth + colWidth * 0.5f;
                float halfW = iconSize * 0.5f;

                float x0 = colCenterX - halfW;
                float x1 = colCenterX + halfW;
                float y0 = centerY - halfH;
                float y1 = centerY + halfH;

                string spriteName = GAME_ICON_SPRITES[col, row];
                Sprite iconSprite = LoadSprite(spriteName);

                bool implemented = MenuSceneView.GameSceneNames[col, row] != null;

                // Circular glow highlight (behind the icon, slightly larger, preserveAspect for circle shape)
                GameObject hlGo = new GameObject("Highlight_" + idx, typeof(RectTransform), typeof(Image));
                hlGo.transform.SetParent(gridPanel.transform, false);
                Image hlImg = hlGo.GetComponent<Image>();
                if (circleSprite != null) hlImg.sprite = circleSprite;
                hlImg.preserveAspect = true;
                hlImg.color = new Color(1f, 0.95f, 0.35f, 0f); // bright yellow, transparent by default
                hlImg.raycastTarget = false;
                SetAnchors(hlGo, x0 - 0.025f, y0 - 0.05f, x1 + 0.025f, y1 + 0.05f);

                // Game icon button (on top of highlight)
                GameObject card = new GameObject("GameIcon_" + idx, typeof(RectTransform), typeof(Image), typeof(Button));
                card.transform.SetParent(gridPanel.transform, false);
                Image cardImg = card.GetComponent<Image>();
                cardImg.sprite = iconSprite;
                cardImg.preserveAspect = true;
                cardImg.color = implemented ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.8f);
                SetAnchors(card, x0, y0, x1, y1);
                card.GetComponent<Button>().interactable = implemented;

                gameButtons[idx] = card.GetComponent<Button>();
                gameIcons[idx] = cardImg;
                gameHighlights[idx] = hlGo.GetComponent<Image>();
            }
        }

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
            gameButtons, gameIcons, gameHighlights,
            randomButton, startButtonGo);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Wire References =====

    private static void WireReferences(
        MenuSceneView view, MenuSceneController controller,
        GameObject backButton, GameObject homeButton, GameObject settingsButton,
        GameObject blueTeamNameText, GameObject redTeamNameText,
        GameObject blueAvatarContainer, GameObject redAvatarContainer,
        GameObject teamAvatarSlotTemplate,
        Button[] tabButtons, Image[] tabImages,
        Button[] gameButtons, Image[] gameIcons, Image[] gameHighlights,
        GameObject randomButton, GameObject startButton)
    {
        SerializedObject viewSo = new SerializedObject(view);

        // Top bar
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        viewSo.FindProperty("homeButton").objectReferenceValue = homeButton.GetComponent<Button>();
        viewSo.FindProperty("settingsButton").objectReferenceValue = settingsButton.GetComponent<Button>();

        // Team banner
        viewSo.FindProperty("blueTeamNameText").objectReferenceValue = blueTeamNameText.GetComponent<Text>();
        viewSo.FindProperty("redTeamNameText").objectReferenceValue = redTeamNameText.GetComponent<Text>();
        viewSo.FindProperty("blueTeamAvatarContainer").objectReferenceValue = blueAvatarContainer.transform;
        viewSo.FindProperty("redTeamAvatarContainer").objectReferenceValue = redAvatarContainer.transform;
        viewSo.FindProperty("teamAvatarSlotTemplate").objectReferenceValue = teamAvatarSlotTemplate;

        // Avatar bg + character sprites
        viewSo.FindProperty("avatarBgBlue").objectReferenceValue = LoadSpriteFromFolder("Assets/Game/Textures/TeamSelectScene", "AvatarBgBlue");
        viewSo.FindProperty("avatarBgRed").objectReferenceValue = LoadSpriteFromFolder("Assets/Game/Textures/TeamSelectScene", "AvatarBgRed");
        viewSo.FindProperty("charBodySprite").objectReferenceValue = LoadSpriteFromFolder("Assets/Game/Textures/PlayerPanel", "body_male");
        SerializedProperty hairArr = viewSo.FindProperty("charHairSprites");
        hairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            hairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadSpriteFromFolder("Assets/Game/Textures/PlayerPanel", "char_hair_" + (i + 1));

        // Category tabs
        SetArrayProperty(viewSo, "categoryTabButtons", tabButtons);
        SetArrayProperty(viewSo, "categoryTabImages", tabImages);

        // Game grid
        SetArrayProperty(viewSo, "gameButtons", gameButtons);
        SetArrayProperty(viewSo, "gameIcons", gameIcons);
        SetArrayProperty(viewSo, "gameHighlights", gameHighlights);

        // Bottom bar
        viewSo.FindProperty("randomSelectButton").objectReferenceValue = randomButton.GetComponent<Button>();
        viewSo.FindProperty("startButton").objectReferenceValue = startButton.GetComponent<Button>();
        viewSo.FindProperty("startButtonText").objectReferenceValue = startButton.GetComponentInChildren<Text>();

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
