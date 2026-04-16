using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor tool to programmatically build the TeamSelect scene (Scene #2).
/// Menu: Tools > TeamSelect > Build Scene
/// </summary>
public static class TeamSelectSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/TeamSelectScene";
    private const string ScenePath = "Assets/Game/Scenes/TeamSelectScene/TeamSelectScene.unity";
    private const string TextureFolder = "Assets/Game/Textures/TeamSelectScene";
    private const string HomeBgPath = "Assets/Game/Textures/HomeScene/HomeBackground.png";
    private const string TeamSelectBgPath = "Assets/Game/Textures/TeamSelectScene/TeamSelectBackground.png";

    [MenuItem("Tools/TeamSelect/Build Scene")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("TeamSelectSceneBuilder: Cannot build scene in Play mode.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TeamSelectSceneBuilder: finished generating TeamSelectScene.");
    }

    // ===== Folder Setup =====

    /// <summary>Find child by name including inactive objects</summary>
    private static GameObject FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;
        }
        return null;
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

    // ===== Scene Build =====

    private static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMainCamera();

        // Canvas (1024x600)
        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root with Controller + View + sub-panel views
        GameObject root = new GameObject("TeamSelectRoot");
        TeamSelectController controller = root.AddComponent<TeamSelectController>();
        TeamSelectView view = root.AddComponent<TeamSelectView>();
        PanelClassView panelClassView = root.AddComponent<PanelClassView>();
        PanelPlayerView panelPlayerView = root.AddComponent<PanelPlayerView>();
        PanelTeamView panelTeamView = root.AddComponent<PanelTeamView>();
        PanelSavedTeamView panelSavedTeamView = root.AddComponent<PanelSavedTeamView>();

        // ===== Load sprites =====
        string[] spriteAssets = {
            "BackButton", "BlueButton", "RedButton", "VsButton",
            "NameWhiteButton", "TeamSavedButton", "PlusButton",
            "PanelClassButton", "OneVsOneButton", "TeamModeButton",
            "TeamBlueBar", "TeamRedBar", "ProgressBarBlue", "ProgressBarGray"
        };
        foreach (string asset in spriteAssets)
            EnsureSpriteImportSettings(TextureFolder + "/" + asset + ".png");
        EnsureSpriteImportSettings(HomeBgPath);
        EnsureSpriteImportSettings(TeamSelectBgPath);

        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TeamSelectBgPath);
        Sprite backBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/BackButton.png");
        Sprite blueBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/BlueButton.png");
        Sprite redBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/RedButton.png");
        Sprite vsSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/VsButton.png");
        Sprite nameWhiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/NameWhiteButton.png");
        Sprite teamSavedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/TeamSavedButton.png");
        Sprite plusSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PlusButton.png");
        Sprite panelClassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PanelClassButton.png");
        Sprite oneVsOneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/OneVsOneButton.png");
        Sprite teamModeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/TeamModeButton.png");
        Sprite teamBlueSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/TeamBlueBar.png");
        Sprite teamRedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/TeamRedBar.png");
        Sprite progressBlueSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/ProgressBarBlue.png");
        Sprite progressGraySprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/ProgressBarGray.png");

        Font quicksandBold = GetBuiltinFont();

        // Background (same white grid as HomeScene)
        GameObject bg = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bg.GetComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type = Image.Type.Simple;
        bgImg.preserveAspect = false;
        bgImg.color = Color.white;
        SetRect(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ===== TOP BAR (y: 0.80-1.0) — two visual tiers =====
        // Tier 1 (top):    [Back]                                              [Settings]
        // Tier 2 (lower):        [Lớp 1A] [Tổ 1 + avatars] [VS] [Tổ 2 + avatars] [1/1] [Đầu đội]
        GameObject topBar = CreateImage("TopBar", canvasGo.transform, new Color(0f, 0f, 0f, 0f));
        SetRect(topBar, new Vector2(0f, 0.80f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // TeamInfoBar is kept as a child of topBar for WireReferences compatibility
        GameObject teamBar = CreateImage("TeamInfoBar", topBar.transform, new Color(0f, 0f, 0f, 0f));
        SetRect(teamBar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // --- Tier 1: Back button (top-left) and Settings (top-right) ---
        GameObject backBtn = CreateSpriteButton("BackButton", topBar.transform, backBtnSprite,
            new Vector2(0.01f, 0.52f), new Vector2(0.065f, 0.95f));

        Sprite settingGearSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/HomeScene/HomeSettingButton.png");
        GameObject settingBtn = CreateSpriteButton("SettingButton", topBar.transform, settingGearSprite,
            new Vector2(0.935f, 0.52f), new Vector2(0.99f, 0.95f));

        // --- Tier 2: Class dropdown + Teams + badges (lower row) ---

        // Class dropdown
        GameObject classDropdownGo = CreateDropdown("ClassDropdown", topBar.transform,
            new Vector2(0.07f, 0.05f), new Vector2(0.19f, 0.52f));
        if (panelClassSprite != null)
        {
            classDropdownGo.GetComponent<Image>().sprite = panelClassSprite;
            classDropdownGo.GetComponent<Image>().type = Image.Type.Sliced;
        }

        // New class button — hidden, keep for compatibility
        GameObject newClassBtn = CreateButton("NewClassButton", topBar.transform, "+",
            new Vector2(0.20f, 0.05f), new Vector2(0.23f, 0.52f), new Color(0.3f, 0.8f, 0.4f, 0f));
        newClassBtn.GetComponentInChildren<Text>().fontSize = 24;
        newClassBtn.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        newClassBtn.SetActive(false);

        // Blue team panel (sprite bar — circle on LEFT, bar extends right)
        GameObject bluePanel = new GameObject("BlueTeamPanel", typeof(RectTransform), typeof(Image));
        bluePanel.transform.SetParent(topBar.transform, false);
        if (teamBlueSprite != null)
        {
            bluePanel.GetComponent<Image>().sprite = teamBlueSprite;
            bluePanel.GetComponent<Image>().type = Image.Type.Simple;
            bluePanel.GetComponent<Image>().color = Color.white;
        }
        else
            bluePanel.GetComponent<Image>().color = new Color(0.7f, 0.85f, 1f, 0.8f);
        SetRect(bluePanel, new Vector2(0.20f, 0.05f), new Vector2(0.44f, 0.55f), Vector2.zero, Vector2.zero);
        GameObject blueBorder = CreateImage("BlueBorder", bluePanel.transform, new Color(0.3f, 0.5f, 1f, 0f));
        SetRect(blueBorder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        // "Tổ 1" — on the bar area after the circle
        GameObject blueLabel = CreateText("BlueLabel", bluePanel.transform, "Tổ 1", 16, TextAnchor.MiddleCenter);
        SetRect(blueLabel, new Vector2(0.12f, 0.05f), new Vector2(0.38f, 0.95f), Vector2.zero, Vector2.zero);
        blueLabel.GetComponent<Text>().color = Color.white;
        blueLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        GameObject blueAvatarContainer = new GameObject("BlueAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        blueAvatarContainer.transform.SetParent(bluePanel.transform, false);
        SetRect(blueAvatarContainer, new Vector2(0.38f, 0.02f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);
        GridLayoutGroup blueGlg = blueAvatarContainer.GetComponent<GridLayoutGroup>();
        blueGlg.cellSize = new Vector2(50f, 50f);
        blueGlg.spacing = new Vector2(2f, 1f);
        blueGlg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        blueGlg.constraintCount = 3;
        blueGlg.childAlignment = TextAnchor.MiddleCenter;

        // VS icon (center, spanning both tiers for visual impact)
        GameObject vsIcon = new GameObject("VsIcon", typeof(RectTransform), typeof(Image));
        vsIcon.transform.SetParent(topBar.transform, false);
        if (vsSprite != null)
        {
            vsIcon.GetComponent<Image>().sprite = vsSprite;
            vsIcon.GetComponent<Image>().preserveAspect = true;
        }
        vsIcon.GetComponent<Image>().color = Color.white;
        vsIcon.GetComponent<Image>().raycastTarget = false;
        SetRect(vsIcon, new Vector2(0.44f, -0.05f), new Vector2(0.56f, 0.80f), Vector2.zero, Vector2.zero);

        // Red team panel (sprite bar — bar extends left, circle on RIGHT)
        GameObject redPanel = new GameObject("RedTeamPanel", typeof(RectTransform), typeof(Image));
        redPanel.transform.SetParent(topBar.transform, false);
        if (teamRedSprite != null)
        {
            redPanel.GetComponent<Image>().sprite = teamRedSprite;
            redPanel.GetComponent<Image>().type = Image.Type.Simple;
            redPanel.GetComponent<Image>().color = Color.white;
        }
        else
            redPanel.GetComponent<Image>().color = new Color(1f, 0.75f, 0.75f, 0.8f);
        SetRect(redPanel, new Vector2(0.56f, 0.05f), new Vector2(0.79f, 0.55f), Vector2.zero, Vector2.zero);
        GameObject redBorder = CreateImage("RedBorder", redPanel.transform, new Color(1f, 0.3f, 0.3f, 0f));
        SetRect(redBorder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        // "Tổ 2" — on the bar area before the circle
        GameObject redLabel = CreateText("RedLabel", redPanel.transform, "Tổ 2", 16, TextAnchor.MiddleCenter);
        SetRect(redLabel, new Vector2(0.62f, 0.05f), new Vector2(0.88f, 0.95f), Vector2.zero, Vector2.zero);
        redLabel.GetComponent<Text>().color = Color.white;
        redLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        GameObject redAvatarContainer = new GameObject("RedAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        redAvatarContainer.transform.SetParent(redPanel.transform, false);
        SetRect(redAvatarContainer, new Vector2(0.05f, 0.02f), new Vector2(0.62f, 0.98f), Vector2.zero, Vector2.zero);
        GridLayoutGroup redGlg = redAvatarContainer.GetComponent<GridLayoutGroup>();
        redGlg.cellSize = new Vector2(50f, 50f);
        redGlg.spacing = new Vector2(2f, 1f);
        redGlg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        redGlg.constraintCount = 3;
        redGlg.childAlignment = TextAnchor.MiddleCenter;

        // Mode toggle — sprite button with text labels, same size as dropdown
        // Uses 1vs1/đấu đội sprites as background, swaps on toggle
        GameObject modePanel = new GameObject("ModePanel", typeof(RectTransform), typeof(Image), typeof(Button));
        modePanel.transform.SetParent(topBar.transform, false);
        if (oneVsOneSprite != null)
        {
            modePanel.GetComponent<Image>().sprite = oneVsOneSprite;
            modePanel.GetComponent<Image>().type = Image.Type.Simple;
        }
        modePanel.GetComponent<Image>().color = Color.white;
        SetRect(modePanel, new Vector2(0.80f, 0.05f), new Vector2(0.93f, 0.52f), Vector2.zero, Vector2.zero);

        // "1/1" text (left half — on dark side when 1vs1 active)
        GameObject modeText1 = CreateText("ModeText1vs1", modePanel.transform, "1/1", 16, TextAnchor.MiddleCenter);
        SetRect(modeText1, new Vector2(0.0f, 0.0f), new Vector2(0.50f, 1.0f), Vector2.zero, Vector2.zero);
        modeText1.GetComponent<Text>().color = Color.white;
        modeText1.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // "Đấu đội" text (right half — on light side when 1vs1 active)
        GameObject modeText2 = CreateText("ModeTextTeam", modePanel.transform, "Đấu\nđội", 11, TextAnchor.MiddleCenter);
        SetRect(modeText2, new Vector2(0.50f, 0.0f), new Vector2(1.0f, 1.0f), Vector2.zero, Vector2.zero);
        modeText2.GetComponent<Text>().color = new Color(0.45f, 0.45f, 0.45f, 1f);
        modeText2.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Use modePanel as the button reference
        GameObject oneVsOneBtn = modePanel;

        // Highlight — dummy image (invisible) for WireReferences compatibility
        GameObject oneVsOneHighlight = CreateImage("Highlight", modePanel.transform, Color.clear);
        SetRect(oneVsOneHighlight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        oneVsOneHighlight.GetComponent<Image>().raycastTarget = false;

        // Wire teamModeButton to the SAME button (single button, two references)
        GameObject teamModeBtn = oneVsOneBtn;
        GameObject teamModeHighlight = oneVsOneHighlight;

        // ===== PLAYER GRID (editor-adjusted) =====
        GameObject playerGridPanel = CreateImage("PlayerGridPanel", canvasGo.transform, new Color(0f, 0f, 0f, 0f));
        playerGridPanel.GetComponent<Image>().raycastTarget = false;
        RectTransform pgRT = playerGridPanel.GetComponent<RectTransform>();
        pgRT.anchorMin = new Vector2(0.03f, 0.10f);
        pgRT.anchorMax = new Vector2(0.97f, 0.55f);
        pgRT.anchoredPosition = new Vector2(0.39f, 111.14f);
        pgRT.sizeDelta = new Vector2(-109.94f, 46.15f);

        // ScrollView
        GameObject scrollView = new GameObject("PlayerScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollView.transform.SetParent(playerGridPanel.transform, false);
        SetRect(scrollView, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scrollView.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scrollView.transform, false);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

        // Content with GridLayout
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(110f, 120f);
        gridLayout.spacing = new Vector2(8f, 8f);
        gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect
        ScrollRect sr = scrollView.GetComponent<ScrollRect>();
        sr.content = contentRect;
        sr.viewport = viewport.GetComponent<RectTransform>();
        sr.horizontal = false;
        sr.vertical = true;

        // Player icon template (hidden, cloned at runtime)
        GameObject playerIconTemplate = CreatePlayerIconTemplate(content.transform);

        // ===== BOTTOM BAR (y: 0.0-0.11) — light background =====
        GameObject bottomBar = CreateImage("BottomBar", canvasGo.transform, new Color(0.92f, 0.95f, 0.88f, 0.9f));
        SetRect(bottomBar, new Vector2(0f, 0f), new Vector2(1f, 0.11f), Vector2.zero, Vector2.zero);

        // Saved teams button (sprite — left side)
        GameObject savedTeamsBtn = CreateSpriteButton("SavedTeamsButton", bottomBar.transform, teamSavedSprite,
            new Vector2(0.02f, 0.08f), new Vector2(0.22f, 0.92f));

        // Start button (center — reuse HomeScene start button sprite)
        Sprite startBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/HomeScene/HomeStartButton.png");
        GameObject startBtn = CreateSpriteButton("StartButton", bottomBar.transform, startBtnSprite,
            new Vector2(0.28f, 0.05f), new Vector2(0.72f, 0.95f));

        // Add player button (sprite — right side, "+" icon)
        GameObject addPlayerBtn = CreateSpriteButton("AddPlayerButton", bottomBar.transform, plusSprite,
            new Vector2(0.78f, 0.08f), new Vector2(0.95f, 0.92f));

        // ===== PANELS (overlays, hidden by default) =====
        GameObject panelClassRoot = BuildPanelClass(canvasGo.transform);
        GameObject panelPlayerRoot = BuildPanelPlayer(canvasGo.transform);
        GameObject panelTeamRoot = BuildPanelTeam(canvasGo.transform);
        GameObject panelSavedTeamRoot = BuildPanelSavedTeam(canvasGo.transform);

        // Team avatar slot template (hidden, used by View to populate team bars)
        GameObject teamAvatarSlotTemplate = CreateImage("TeamAvatarSlotTemplate", canvasGo.transform, new Color(0.7f, 0.85f, 1f, 1f));
        RectTransform slotRt = teamAvatarSlotTemplate.GetComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(56f, 56f);
        GameObject slotText = CreateText("Initial", teamAvatarSlotTemplate.transform, "?", 18, TextAnchor.MiddleCenter);
        SetRect(slotText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        teamAvatarSlotTemplate.SetActive(false);

        // ===== Wire References =====
        WireReferences(view, controller, panelClassView, panelPlayerView, panelTeamView, panelSavedTeamView,
            canvasGo, topBar, teamBar, playerGridPanel, bottomBar,
            backBtn, classDropdownGo, newClassBtn, settingBtn,
            bluePanel, blueBorder, blueAvatarContainer, blueLabel,
            redPanel, redBorder, redAvatarContainer, redLabel,
            oneVsOneBtn, teamModeBtn, oneVsOneHighlight, teamModeHighlight,
            scrollView, content, playerIconTemplate, addPlayerBtn,
            savedTeamsBtn, startBtn,
            panelClassRoot, panelPlayerRoot, panelTeamRoot, panelSavedTeamRoot,
            teamAvatarSlotTemplate);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Panel Builders =====

    private const string ClassPanelSpriteFolder = "Assets/Game/Textures/ClassPanel";

    private static Sprite LoadClassPanelSprite(string name)
    {
        string path = ClassPanelSpriteFolder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
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

    private static GameObject BuildPanelClass(Transform parent)
    {
        GameObject panelRoot = CreateImage("PanelClassRoot", parent, new Color(0f, 0f, 0f, 0.6f));
        SetRect(panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Full-screen BG
        Sprite classBgSprite = LoadClassPanelSprite("panel_class_bg");
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panelRoot.transform, false);
        Image frameImg = frame.GetComponent<Image>();
        if (classBgSprite != null) { frameImg.sprite = classBgSprite; frameImg.color = Color.white; }
        else frameImg.color = new Color(0.4f, 0.8f, 0.75f, 1f);
        SetRect(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Close button (red circle X, top right)
        Sprite closeBtnSprite = LoadClassPanelSprite("btn_close");
        GameObject closeBtn = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtn.transform.SetParent(frame.transform, false);
        Image closeBtnImg = closeBtn.GetComponent<Image>();
        if (closeBtnSprite != null) { closeBtnImg.sprite = closeBtnSprite; closeBtnImg.preserveAspect = true; }
        else closeBtnImg.color = new Color(0.9f, 0.3f, 0.3f, 1f);
        closeBtnImg.color = Color.white;
        SetRect(closeBtn, new Vector2(0.92f, 0.88f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);

        // Class name dropdown-style bar (top center)
        Sprite classDropdownSprite = LoadClassPanelSprite("class_dropdown");
        GameObject classNameBar = new GameObject("ClassNameBar", typeof(RectTransform), typeof(Image));
        classNameBar.transform.SetParent(frame.transform, false);
        Image classBarImg = classNameBar.GetComponent<Image>();
        if (classDropdownSprite != null) { classBarImg.sprite = classDropdownSprite; classBarImg.preserveAspect = true; }
        classBarImg.color = Color.white;
        SetRect(classNameBar, new Vector2(0.25f, 0.87f), new Vector2(0.70f, 0.97f), Vector2.zero, Vector2.zero);

        // Class name input (overlaid on bar)
        GameObject classNameInput = CreateInputField("ClassNameInput", classNameBar.transform, "Ten lop...",
            new Vector2(0.05f, 0.05f), new Vector2(0.75f, 0.95f));

        // Add student button (small + near top)
        Sprite addSmallSprite = LoadClassPanelSprite("btn_add_small");
        GameObject addStudentBtn = new GameObject("AddStudentButton", typeof(RectTransform), typeof(Image), typeof(Button));
        addStudentBtn.transform.SetParent(frame.transform, false);
        Image addBtnImg = addStudentBtn.GetComponent<Image>();
        if (addSmallSprite != null) { addBtnImg.sprite = addSmallSprite; addBtnImg.preserveAspect = true; }
        addBtnImg.color = Color.white;
        SetRect(addStudentBtn, new Vector2(0.72f, 0.88f), new Vector2(0.78f, 0.96f), Vector2.zero, Vector2.zero);

        // Column header: Score label
        GameObject scoreHeader = CreateText("ScoreHeader", frame.transform, "Score", 14, TextAnchor.MiddleCenter);
        SetRect(scoreHeader, new Vector2(0.62f, 0.81f), new Vector2(0.76f, 0.87f), Vector2.zero, Vector2.zero);
        scoreHeader.GetComponent<Text>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
        scoreHeader.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Student list scroll area
        GameObject scrollArea = CreateImage("StudentScrollArea", frame.transform, new Color(0f, 0f, 0f, 0f));
        SetRect(scrollArea, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.81f), Vector2.zero, Vector2.zero);

        // Viewport
        GameObject studentViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        studentViewport.transform.SetParent(scrollArea.transform, false);
        SetRect(studentViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        studentViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        studentViewport.GetComponent<Mask>().showMaskGraphic = false;

        // Student list content
        GameObject studentContent = new GameObject("StudentListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        studentContent.transform.SetParent(studentViewport.transform, false);
        RectTransform studentContentRT = studentContent.GetComponent<RectTransform>();
        studentContentRT.anchorMin = new Vector2(0f, 1f);
        studentContentRT.anchorMax = Vector2.one;
        studentContentRT.pivot = new Vector2(0.5f, 1f);
        studentContentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = studentContent.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 5f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(2, 2, 2, 2);

        ContentSizeFitter studentCsf = studentContent.GetComponent<ContentSizeFitter>();
        studentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        studentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect studentScroll = scrollArea.AddComponent<ScrollRect>();
        studentScroll.content = studentContentRT;
        studentScroll.viewport = studentViewport.GetComponent<RectTransform>();
        studentScroll.horizontal = false;
        studentScroll.vertical = true;
        studentScroll.movementType = ScrollRect.MovementType.Clamped;
        studentScroll.scrollSensitivity = 25f;

        // Student row template
        GameObject studentRowTemplate = CreateClassStudentRowTemplate(studentContent.transform);

        // Bottom buttons with sprites
        Sprite copySprite = LoadClassPanelSprite("btn_copy_class");
        Sprite saveSprite = LoadClassPanelSprite("btn_save_class");
        Sprite deleteSprite = LoadClassPanelSprite("btn_delete_class");

        GameObject copyBtn = new GameObject("CopyButton", typeof(RectTransform), typeof(Image), typeof(Button));
        copyBtn.transform.SetParent(frame.transform, false);
        Image copyImg = copyBtn.GetComponent<Image>();
        if (copySprite != null) { copyImg.sprite = copySprite; copyImg.preserveAspect = true; }
        copyImg.color = Color.white;
        SetRect(copyBtn, new Vector2(0.05f, 0.03f), new Vector2(0.30f, 0.14f), Vector2.zero, Vector2.zero);

        GameObject saveBtn = new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        saveBtn.transform.SetParent(frame.transform, false);
        Image saveImg = saveBtn.GetComponent<Image>();
        if (saveSprite != null) { saveImg.sprite = saveSprite; saveImg.preserveAspect = true; }
        saveImg.color = Color.white;
        SetRect(saveBtn, new Vector2(0.35f, 0.03f), new Vector2(0.60f, 0.14f), Vector2.zero, Vector2.zero);

        GameObject deleteBtn = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        deleteBtn.transform.SetParent(frame.transform, false);
        Image deleteImg = deleteBtn.GetComponent<Image>();
        if (deleteSprite != null) { deleteImg.sprite = deleteSprite; deleteImg.preserveAspect = true; }
        deleteImg.color = Color.white;
        SetRect(deleteBtn, new Vector2(0.65f, 0.03f), new Vector2(0.90f, 0.14f), Vector2.zero, Vector2.zero);

        panelRoot.SetActive(false);
        return panelRoot;
    }

    private const string PlayerPanelSpriteFolder = "Assets/Game/Textures/PlayerPanel";

    private static Sprite LoadPlayerPanelSprite(string name)
    {
        string path = PlayerPanelSpriteFolder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (sprite == null) Debug.LogWarning("TeamSelectSceneBuilder: Sprite not found: " + path);
        }
        return sprite;
    }

    private static GameObject BuildPanelPlayer(Transform parent)
    {
        // Overlay backdrop
        GameObject panelRoot = CreateImage("PanelPlayerRoot", parent, new Color(0f, 0f, 0f, 0.6f));
        SetRect(panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Panel background sprite
        Sprite panelBgSprite = LoadPlayerPanelSprite("panel_bg");
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panelRoot.transform, false);
        Image frameImg = frame.GetComponent<Image>();
        if (panelBgSprite != null) { frameImg.sprite = panelBgSprite; frameImg.color = Color.white; }
        else frameImg.color = new Color(0.4f, 0.8f, 0.75f, 1f);
        SetRect(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Close button (X, top right)
        GameObject closeBtn = CreateButton("CloseButton", frame.transform, "X",
            new Vector2(0.93f, 0.90f), new Vector2(0.99f, 0.98f), new Color(0.9f, 0.3f, 0.3f, 1f));
        closeBtn.GetComponentInChildren<Text>().fontSize = 22;

        // ===== TOP ROW: [NameInput — ClassDropdown — GenderButton] =====

        // Name input with gray bar background
        Sprite nameBarSprite = LoadPlayerPanelSprite("name_bar");
        GameObject nameInput = CreateInputField("NameInput", frame.transform, "Nhap ten...",
            new Vector2(0.04f, 0.82f), new Vector2(0.30f, 0.92f));
        RectTransform niRT = nameInput.GetComponent<RectTransform>();
        niRT.anchoredPosition = new Vector2(52f, -22f);
        if (nameBarSprite != null)
        {
            Image niBg = nameInput.GetComponent<Image>();
            if (niBg != null) { niBg.sprite = nameBarSprite; niBg.color = Color.white; }
        }

        // Class dropdown (styled like outer TeamSelect with PanelClassButton sprite)
        Sprite panelClassBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PanelClassButton.png");
        GameObject classDropdown = CreateDropdown("ClassDropdown", frame.transform,
            new Vector2(0.31f, 0.82f), new Vector2(0.57f, 0.92f));
        RectTransform ddRT = classDropdown.GetComponent<RectTransform>();
        ddRT.anchoredPosition = new Vector2(22.73f, -20f); ddRT.sizeDelta = new Vector2(-84.54f, 0f);
        if (panelClassBtnSprite != null)
        {
            Image ddBg = classDropdown.GetComponent<Image>();
            if (ddBg != null) { ddBg.sprite = panelClassBtnSprite; ddBg.type = Image.Type.Sliced; ddBg.color = Color.white; }
        }

        // Gender toggle button
        Sprite genderIconSprite = LoadPlayerPanelSprite("gender_icon");
        GameObject genderBtn = new GameObject("GenderButton", typeof(RectTransform), typeof(Image), typeof(Button));
        genderBtn.transform.SetParent(frame.transform, false);
        Image genderBtnImg = genderBtn.GetComponent<Image>();
        if (genderIconSprite != null) { genderBtnImg.sprite = genderIconSprite; genderBtnImg.preserveAspect = true; }
        genderBtnImg.color = Color.white;
        RectTransform gbRT = genderBtn.GetComponent<RectTransform>();
        gbRT.anchorMin = new Vector2(0.58f, 0.82f); gbRT.anchorMax = new Vector2(0.72f, 0.92f);
        gbRT.anchoredPosition = new Vector2(139f, -29f); gbRT.sizeDelta = Vector2.zero;

        // Score text (right side, smaller)
        GameObject scoreLabel = CreateText("ScoreLabel", frame.transform, "Score 0", 14, TextAnchor.MiddleRight);
        RectTransform slRT = scoreLabel.GetComponent<RectTransform>();
        slRT.anchorMin = new Vector2(0.73f, 0.82f); slRT.anchorMax = new Vector2(0.96f, 0.92f);
        slRT.anchoredPosition = new Vector2(-66f, -94f); slRT.sizeDelta = Vector2.zero;
        scoreLabel.GetComponent<Text>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // ===== ACCESSORY TABS (hair / glasses) =====
        Sprite hairTabSprite = LoadPlayerPanelSprite("tab_hair");
        Sprite glassesTabSprite = LoadPlayerPanelSprite("tab_glasses");

        // Tab container background (swaps between hair/glasses popup sprite)
        Sprite hairPopupBg = LoadPlayerPanelSprite("hair_popup_bg");
        GameObject tabContainerBg = new GameObject("TabContainerBg", typeof(RectTransform), typeof(Image));
        tabContainerBg.transform.SetParent(frame.transform, false);
        Image tcBgImg = tabContainerBg.GetComponent<Image>();
        if (hairPopupBg != null) { tcBgImg.sprite = hairPopupBg; }
        tcBgImg.color = Color.white;
        tcBgImg.raycastTarget = false;
        RectTransform tcRT = tabContainerBg.GetComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0.04f, 0.13f); tcRT.anchorMax = new Vector2(0.46f, 0.73f);
        tcRT.anchoredPosition = new Vector2(43f, 3f); tcRT.sizeDelta = Vector2.zero;

        // Hair tab button
        GameObject hairTabBtn = new GameObject("HairTabButton", typeof(RectTransform), typeof(Image), typeof(Button));
        hairTabBtn.transform.SetParent(frame.transform, false);
        Image hairTabImg = hairTabBtn.GetComponent<Image>();
        hairTabImg.color = new Color(1f, 1f, 1f, 0.01f);
        RectTransform htRT = hairTabBtn.GetComponent<RectTransform>();
        htRT.anchorMin = new Vector2(0.04f, 0.58f); htRT.anchorMax = new Vector2(0.25f, 0.73f);
        htRT.anchoredPosition = new Vector2(48.12f, 14.96f); htRT.sizeDelta = new Vector2(-72.84f, -29.92f);

        // Glasses tab button
        GameObject glassesTabBtn = new GameObject("GlassesTabButton", typeof(RectTransform), typeof(Image), typeof(Button));
        glassesTabBtn.transform.SetParent(frame.transform, false);
        Image glassesTabImg = glassesTabBtn.GetComponent<Image>();
        glassesTabImg.color = new Color(1f, 1f, 1f, 0.01f);
        RectTransform gtRT = glassesTabBtn.GetComponent<RectTransform>();
        gtRT.anchorMin = new Vector2(0.25f, 0.58f); gtRT.anchorMax = new Vector2(0.46f, 0.73f);
        gtRT.anchoredPosition = new Vector2(-12.36f, 14.96f); gtRT.sizeDelta = new Vector2(-68.94f, -29.92f);

        // ===== HAIR GRID (3x2 = 6 items) =====
        GameObject hairGrid = CreateImage("HairGrid", frame.transform, new Color(0f, 0f, 0f, 0f));
        hairGrid.GetComponent<Image>().raycastTarget = false;
        RectTransform hgRT = hairGrid.GetComponent<RectTransform>();
        hgRT.anchorMin = new Vector2(0.04f, 0.15f); hgRT.anchorMax = new Vector2(0.46f, 0.58f);
        hgRT.anchoredPosition = new Vector2(43f, 4f); hgRT.sizeDelta = Vector2.zero;

        Sprite checkSprite = LoadPlayerPanelSprite("check_icon");
        Button[] hairButtons = new Button[6];
        Image[] hairChecks = new Image[6];
        float[][] hairAnchors = {
            new[]{0.03f, 0.51f, 0.31f, 0.95f}, new[]{0.35f, 0.51f, 0.63f, 0.95f}, new[]{0.67f, 0.51f, 0.95f, 0.95f},
            new[]{0.03f, 0.03f, 0.31f, 0.47f}, new[]{0.35f, 0.03f, 0.63f, 0.47f}, new[]{0.67f, 0.03f, 0.95f, 0.47f}
        };
        for (int i = 0; i < 6; i++)
        {
            float x0 = hairAnchors[i][0]; float y0 = hairAnchors[i][1];
            float x1 = hairAnchors[i][2]; float y1 = hairAnchors[i][3];

            Sprite thumbSprite = LoadPlayerPanelSprite("thumb_hair_" + (i + 1));
            GameObject btn = new GameObject("Hair_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(hairGrid.transform, false);
            Image btnImg = btn.GetComponent<Image>();
            if (thumbSprite != null) { btnImg.sprite = thumbSprite; btnImg.preserveAspect = true; }
            btnImg.color = Color.white;
            SetRect(btn, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);

            // Checkmark overlay
            GameObject chk = new GameObject("Check", typeof(RectTransform), typeof(Image));
            chk.transform.SetParent(btn.transform, false);
            Image chkImg = chk.GetComponent<Image>();
            if (checkSprite != null) { chkImg.sprite = checkSprite; chkImg.preserveAspect = true; }
            chkImg.color = Color.white;
            chkImg.raycastTarget = false;
            SetRect(chk, new Vector2(0.0f, 0.0f), new Vector2(0.35f, 0.35f), Vector2.zero, Vector2.zero);
            chk.SetActive(false);

            hairButtons[i] = btn.GetComponent<Button>();
            hairChecks[i] = chkImg;
        }

        // ===== GLASSES GRID (3x2 = 6 items) =====
        GameObject glassesGrid = CreateImage("GlassesGrid", frame.transform, new Color(0f, 0f, 0f, 0f));
        glassesGrid.GetComponent<Image>().raycastTarget = false;
        RectTransform ggRT = glassesGrid.GetComponent<RectTransform>();
        ggRT.anchorMin = new Vector2(0.04f, 0.15f); ggRT.anchorMax = new Vector2(0.46f, 0.58f);
        ggRT.anchoredPosition = new Vector2(43f, 4f); ggRT.sizeDelta = Vector2.zero;

        Button[] glassesButtons = new Button[6];
        Image[] glassesChecks = new Image[6];
        float[][] glassAnchors = {
            new[]{0.03f, 0.51f, 0.31f, 0.95f}, new[]{0.35f, 0.51f, 0.63f, 0.95f}, new[]{0.67f, 0.51f, 0.95f, 0.95f},
            new[]{0.03f, 0.03f, 0.31f, 0.47f}, new[]{0.35f, 0.03f, 0.63f, 0.47f}, new[]{0.67f, 0.03f, 0.95f, 0.47f}
        };
        for (int i = 0; i < 6; i++)
        {
            float x0 = glassAnchors[i][0]; float y0 = glassAnchors[i][1];
            float x1 = glassAnchors[i][2]; float y1 = glassAnchors[i][3];

            Sprite thumbSprite = LoadPlayerPanelSprite("thumb_glasses_" + (i + 1));
            GameObject btn = new GameObject("Glasses_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(glassesGrid.transform, false);
            Image btnImg = btn.GetComponent<Image>();
            if (thumbSprite != null) { btnImg.sprite = thumbSprite; btnImg.preserveAspect = true; }
            btnImg.color = Color.white;
            SetRect(btn, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);

            GameObject chk = new GameObject("Check", typeof(RectTransform), typeof(Image));
            chk.transform.SetParent(btn.transform, false);
            Image chkImg = chk.GetComponent<Image>();
            if (checkSprite != null) { chkImg.sprite = checkSprite; chkImg.preserveAspect = true; }
            chkImg.color = Color.white;
            chkImg.raycastTarget = false;
            SetRect(chk, new Vector2(0.0f, 0.0f), new Vector2(0.35f, 0.35f), Vector2.zero, Vector2.zero);
            chk.SetActive(false);

            glassesButtons[i] = btn.GetComponent<Button>();
            glassesChecks[i] = chkImg;
        }
        glassesGrid.SetActive(false); // hair tab shown by default

        // ===== RIGHT SIDE: Character Preview =====
        // Body/Hair layer
        Sprite bodySprite = LoadPlayerPanelSprite("char_hair_1");
        GameObject charBody = new GameObject("CharacterBody", typeof(RectTransform), typeof(Image));
        charBody.transform.SetParent(frame.transform, false);
        Image charBodyImg = charBody.GetComponent<Image>();
        if (bodySprite != null) { charBodyImg.sprite = bodySprite; charBodyImg.preserveAspect = true; }
        charBodyImg.color = Color.white;
        RectTransform cbRT = charBody.GetComponent<RectTransform>();
        cbRT.anchorMin = new Vector2(0.52f, 0.12f); cbRT.anchorMax = new Vector2(0.92f, 0.85f);
        cbRT.anchoredPosition = new Vector2(61f, -45f); cbRT.sizeDelta = new Vector2(-179.49f, -152.18f);

        // Glasses overlay layer
        GameObject charGlasses = new GameObject("CharacterGlasses", typeof(RectTransform), typeof(Image));
        charGlasses.transform.SetParent(frame.transform, false);
        Image charGlassesImg = charGlasses.GetComponent<Image>();
        charGlassesImg.preserveAspect = true;
        charGlassesImg.color = Color.white;
        charGlassesImg.raycastTarget = false;
        RectTransform cgRT = charGlasses.GetComponent<RectTransform>();
        cgRT.anchorMin = new Vector2(0.62f, 0.48f); cgRT.anchorMax = new Vector2(0.82f, 0.62f);
        cgRT.anchoredPosition = new Vector2(39.16f, -65.50f); cgRT.sizeDelta = new Vector2(-98.85f, -36.42f);
        charGlasses.SetActive(false);

        // ===== BOTTOM BUTTONS =====
        Sprite saveBtnSprite = LoadPlayerPanelSprite("btn_save");
        Sprite deleteBtnSprite = LoadPlayerPanelSprite("btn_delete");

        GameObject saveBtn = new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        saveBtn.transform.SetParent(frame.transform, false);
        Image saveBtnImg = saveBtn.GetComponent<Image>();
        if (saveBtnSprite != null) { saveBtnImg.sprite = saveBtnSprite; saveBtnImg.preserveAspect = true; }
        saveBtnImg.color = Color.white;
        SetRect(saveBtn, new Vector2(0.10f, 0.02f), new Vector2(0.40f, 0.13f), Vector2.zero, Vector2.zero);

        GameObject deleteBtn = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        deleteBtn.transform.SetParent(frame.transform, false);
        Image deleteBtnImg = deleteBtn.GetComponent<Image>();
        if (deleteBtnSprite != null) { deleteBtnImg.sprite = deleteBtnSprite; deleteBtnImg.preserveAspect = true; }
        deleteBtnImg.color = Color.white;
        SetRect(deleteBtn, new Vector2(0.60f, 0.02f), new Vector2(0.90f, 0.13f), Vector2.zero, Vector2.zero);

        panelRoot.SetActive(false);
        return panelRoot;
    }

    private const string TeamPanelSpriteFolder = "Assets/Game/Textures/TeamPanel";

    private static Sprite LoadTeamPanelSprite(string name)
    {
        string path = TeamPanelSpriteFolder + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
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

    private static GameObject BuildPanelTeam(Transform parent)
    {
        // Overlay
        GameObject panelRoot = CreateImage("PanelTeamRoot", parent, new Color(0f, 0f, 0f, 0.6f));
        SetRect(panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Panel BG (reuse player panel bg)
        Sprite panelBgSprite = LoadPlayerPanelSprite("panel_bg");
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panelRoot.transform, false);
        Image frameImg = frame.GetComponent<Image>();
        if (panelBgSprite != null) { frameImg.sprite = panelBgSprite; frameImg.color = Color.white; }
        else frameImg.color = new Color(0.4f, 0.8f, 0.75f, 1f);
        SetRect(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Close button (X, top right)
        GameObject closeBtn = CreateButton("CloseButton", frame.transform, "X",
            new Vector2(0.93f, 0.90f), new Vector2(0.99f, 0.98f), new Color(0.9f, 0.3f, 0.3f, 1f));
        closeBtn.GetComponentInChildren<Text>().fontSize = 22;

        // Team bar (blue by default, swapped at runtime)
        Sprite teamBarBlue = LoadTeamPanelSprite("team_bar_blue");
        GameObject teamBar = new GameObject("TeamBar", typeof(RectTransform), typeof(Image));
        teamBar.transform.SetParent(frame.transform, false);
        Image teamBarImg = teamBar.GetComponent<Image>();
        if (teamBarBlue != null) { teamBarImg.sprite = teamBarBlue; teamBarImg.preserveAspect = true; }
        teamBarImg.color = Color.white;
        SetRect(teamBar, new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.97f), Vector2.zero, Vector2.zero);

        // Team name on bar
        GameObject teamNameText = CreateText("TeamNameText", teamBar.transform, "To 1", 18, TextAnchor.MiddleCenter);
        SetRect(teamNameText, new Vector2(0.25f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        teamNameText.GetComponent<Text>().color = Color.white;
        teamNameText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Hidden team name input (for editing)
        GameObject teamNameInput = CreateInputField("TeamNameInput", frame.transform, "Ten doi...",
            new Vector2(0.05f, 0.78f), new Vector2(0.55f, 0.86f));

        // Member list scroll area
        GameObject scrollArea = CreateImage("MemberScrollArea", frame.transform, new Color(0f, 0f, 0f, 0f));
        SetRect(scrollArea, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.77f), Vector2.zero, Vector2.zero);

        // Viewport with mask
        GameObject memberViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        memberViewport.transform.SetParent(scrollArea.transform, false);
        SetRect(memberViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        memberViewport.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 0.5f);
        memberViewport.GetComponent<Mask>().showMaskGraphic = true;

        // Content with VerticalLayout (1 member per row, matching design)
        GameObject memberContent = new GameObject("MemberListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        memberContent.transform.SetParent(memberViewport.transform, false);
        RectTransform memberContentRT = memberContent.GetComponent<RectTransform>();
        memberContentRT.anchorMin = new Vector2(0f, 1f);
        memberContentRT.anchorMax = Vector2.one;
        memberContentRT.pivot = new Vector2(0.5f, 1f);
        memberContentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = memberContent.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter memberCsf = memberContent.GetComponent<ContentSizeFitter>();
        memberCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        memberCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ScrollRect
        ScrollRect memberScroll = scrollArea.AddComponent<ScrollRect>();
        memberScroll.content = memberContentRT;
        memberScroll.viewport = memberViewport.GetComponent<RectTransform>();
        memberScroll.horizontal = false;
        memberScroll.vertical = true;
        memberScroll.movementType = ScrollRect.MovementType.Clamped;
        memberScroll.scrollSensitivity = 25f;

        // Member row template: [number] [name bar] [avatars] [X]
        GameObject memberCardTemplate = CreateTeamMemberRowTemplate(memberContent.transform);

        // Bottom buttons with sprites
        Sprite saveBtnSprite = LoadTeamPanelSprite("btn_save_team");
        Sprite deleteBtnSprite = LoadTeamPanelSprite("btn_delete_team");

        GameObject saveBtn = new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        saveBtn.transform.SetParent(frame.transform, false);
        Image saveBtnImg = saveBtn.GetComponent<Image>();
        if (saveBtnSprite != null) { saveBtnImg.sprite = saveBtnSprite; saveBtnImg.preserveAspect = true; }
        saveBtnImg.color = Color.white;
        SetRect(saveBtn, new Vector2(0.10f, 0.03f), new Vector2(0.48f, 0.15f), Vector2.zero, Vector2.zero);

        GameObject deleteBtn = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        deleteBtn.transform.SetParent(frame.transform, false);
        Image deleteBtnImg = deleteBtn.GetComponent<Image>();
        if (deleteBtnSprite != null) { deleteBtnImg.sprite = deleteBtnSprite; deleteBtnImg.preserveAspect = true; }
        deleteBtnImg.color = Color.white;
        SetRect(deleteBtn, new Vector2(0.52f, 0.03f), new Vector2(0.90f, 0.15f), Vector2.zero, Vector2.zero);

        panelRoot.SetActive(false);
        return panelRoot;
    }

    private const string SaveTeamSpriteFolder = "Assets/Game/Textures/SaveTeamPanel";

    private static Sprite LoadSaveTeamSprite(string name)
    {
        string path = SaveTeamSpriteFolder + "/" + name + ".png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null) { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false; imp.SaveAndReimport(); s = AssetDatabase.LoadAssetAtPath<Sprite>(path); }
        }
        return s;
    }

    private static GameObject BuildPanelSavedTeam(Transform parent)
    {
        GameObject panelRoot = CreateImage("PanelSavedTeamRoot", parent, new Color(0f, 0f, 0f, 0.6f));
        SetRect(panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Full-screen BG sprite
        Sprite panelBgSprite = LoadSaveTeamSprite("panel_bg");
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panelRoot.transform, false);
        Image frameImg = frame.GetComponent<Image>();
        if (panelBgSprite != null) { frameImg.sprite = panelBgSprite; frameImg.color = Color.white; }
        else frameImg.color = new Color(0.4f, 0.8f, 0.75f, 1f);
        SetRect(frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Close button (top right, sprite)
        Sprite closeBtnSprite = LoadSaveTeamSprite("btn_close");
        GameObject closeBtn = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtn.transform.SetParent(frame.transform, false);
        Image closeBtnImg = closeBtn.GetComponent<Image>();
        if (closeBtnSprite != null) { closeBtnImg.sprite = closeBtnSprite; closeBtnImg.preserveAspect = true; }
        closeBtnImg.color = Color.white;
        SetRect(closeBtn, new Vector2(0.93f, 0.90f), new Vector2(0.99f, 0.98f), Vector2.zero, Vector2.zero);

        // Team list (grid 3x2 of team bars)
        GameObject listArea = CreateImage("GridArea", frame.transform, new Color(0f, 0f, 0f, 0f));
        listArea.GetComponent<Image>().raycastTarget = false;
        SetRect(listArea, new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.88f), Vector2.zero, Vector2.zero);

        // Viewport with mask
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(listArea.transform, false);
        SetRect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content with GridLayout (3 columns)
        GameObject teamGridContent = new GameObject("TeamGridContent", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        teamGridContent.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = teamGridContent.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup glg = teamGridContent.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(430f, 100f);
        glg.spacing = new Vector2(10f, 14f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.UpperCenter;
        glg.padding = new RectOffset(8, 8, 8, 8);

        ContentSizeFitter csf = teamGridContent.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ScrollRect on listArea
        ScrollRect scrollRect = listArea.AddComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Scrollbar
        GameObject scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbar.transform.SetParent(listArea.transform, false);
        RectTransform scrollbarRT = scrollbar.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(10f, 0f);
        scrollbarRT.offsetMin = new Vector2(-10f, 2f);
        scrollbarRT.offsetMax = new Vector2(0f, -2f);
        scrollbar.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.9f, 1f);

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbar.transform, false);
        SetRect(slidingArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject handle = CreateImage("Handle", slidingArea.transform, new Color(0.5f, 0.55f, 0.65f, 1f));
        SetRect(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Scrollbar scrollbarComp = scrollbar.GetComponent<Scrollbar>();
        scrollbarComp.handleRect = handle.GetComponent<RectTransform>();
        scrollbarComp.direction = Scrollbar.Direction.BottomToTop;
        scrollbarComp.targetGraphic = handle.GetComponent<Image>();

        scrollRect.verticalScrollbar = scrollbarComp;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -2f;

        // Team card template (gray bar sprite)
        Sprite grayBarSprite = LoadSaveTeamSprite("team_bar_gray");
        GameObject teamCardTemplate = new GameObject("TeamCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
        teamCardTemplate.transform.SetParent(teamGridContent.transform, false);
        Image cardImg = teamCardTemplate.GetComponent<Image>();
        if (grayBarSprite != null) { cardImg.sprite = grayBarSprite; cardImg.preserveAspect = true; }
        cardImg.color = Color.white;

        // Team name on the bar (right of mascot icon, offset to clear mascot)
        GameObject cardText = CreateText("TeamName", teamCardTemplate.transform, "Ten doi", 16, TextAnchor.MiddleLeft);
        SetRect(cardText, new Vector2(0.15f, 0.05f), new Vector2(0.60f, 0.95f), new Vector2(74.52f, 0f), Vector2.zero);
        cardText.GetComponent<Text>().color = Color.white;
        cardText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        teamCardTemplate.SetActive(false);

        // Buttons with sprites
        Sprite backBtnSprite = LoadSaveTeamSprite("btn_back");
        Sprite confirmBtnSprite = LoadSaveTeamSprite("btn_confirm");

        GameObject backBtn = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backBtn.transform.SetParent(frame.transform, false);
        Image backImg = backBtn.GetComponent<Image>();
        if (backBtnSprite != null) { backImg.sprite = backBtnSprite; backImg.preserveAspect = true; }
        backImg.color = Color.white;
        SetRect(backBtn, new Vector2(0.10f, 0.02f), new Vector2(0.38f, 0.14f), Vector2.zero, Vector2.zero);

        GameObject confirmBtn = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
        confirmBtn.transform.SetParent(frame.transform, false);
        Image confirmImg = confirmBtn.GetComponent<Image>();
        if (confirmBtnSprite != null) { confirmImg.sprite = confirmBtnSprite; confirmImg.preserveAspect = true; }
        confirmImg.color = Color.white;
        SetRect(confirmBtn, new Vector2(0.62f, 0.02f), new Vector2(0.90f, 0.14f), Vector2.zero, Vector2.zero);

        panelRoot.SetActive(false);
        return panelRoot;
    }

    // ===== Templates =====

    private static GameObject CreatePlayerIconTemplate(Transform parent)
    {
        // Use PlayerCardWhite sprite as default background
        EnsureSpriteImportSettings(TextureFolder + "/PlayerCardWhite.png");
        Sprite cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PlayerCardWhite.png");

        GameObject icon = new GameObject("PlayerIconTemplate", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        Image img = icon.GetComponent<Image>();
        if (cardSprite != null) { img.sprite = cardSprite; }
        img.color = Color.white;
        RectTransform rt = icon.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(110f, 120f);

        // Border — hidden, kept for team color sprite swap at runtime
        GameObject border = CreateImage("Border", icon.transform, new Color(0f, 0f, 0f, 0f));
        SetRect(border, Vector2.zero, Vector2.one, new Vector2(-3f, -3f), new Vector2(3f, 3f));
        border.transform.SetAsFirstSibling();

        // Avatar area (character body+hair sprite)
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(icon.transform, false);
        Image avatarImg = avatar.GetComponent<Image>();
        avatarImg.color = Color.white;
        avatarImg.preserveAspect = true;
        SetRect(avatar, new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.92f), Vector2.zero, Vector2.zero);

        // Glasses overlay (child of Avatar, hidden by default)
        GameObject glasses = new GameObject("Glasses", typeof(RectTransform), typeof(Image));
        glasses.transform.SetParent(avatar.transform, false);
        Image glassesImg = glasses.GetComponent<Image>();
        glassesImg.color = Color.white;
        glassesImg.preserveAspect = true;
        glassesImg.raycastTarget = false;
        // Default glasses position (overridden at runtime for monocle index 2)
        SetRect(glasses, new Vector2(0.15f, 0.40f), new Vector2(0.85f, 0.60f), new Vector2(2f, 3.4f), new Vector2(-15.6f, 3.4f));
        glasses.SetActive(false);

        // Name text
        GameObject nameText = CreateText("NameText", icon.transform, "Name", 14, TextAnchor.MiddleCenter);
        SetRect(nameText, new Vector2(0f, 0f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        icon.SetActive(false);
        return icon;
    }

    private static GameObject CreateStudentRowTemplate(Transform parent)
    {
        // Legacy - kept for compatibility
        return CreateClassStudentRowTemplate(parent);
    }

    /// <summary>
    /// Row: [#] [gray name bar] [avatar] [score] [rank] [X delete] [edit]
    /// </summary>
    private static GameObject CreateClassStudentRowTemplate(Transform parent)
    {
        Sprite rowBarSprite = LoadClassPanelSprite("row_bar");
        Sprite xIconSprite = LoadClassPanelSprite("icon_x");
        Sprite editIconSprite = LoadClassPanelSprite("icon_edit");

        GameObject row = new GameObject("StudentRowTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        row.GetComponent<LayoutElement>().preferredHeight = 44f;

        // Number
        GameObject numLabel = CreateText("NumLabel", row.transform, "1", 20, TextAnchor.MiddleCenter);
        SetRect(numLabel, new Vector2(0f, 0f), new Vector2(0.06f, 1f), Vector2.zero, Vector2.zero);
        numLabel.GetComponent<Text>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
        numLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Name bar (gray rounded rectangle)
        GameObject nameBar = new GameObject("NameBar", typeof(RectTransform), typeof(Image));
        nameBar.transform.SetParent(row.transform, false);
        Image nameBarImg = nameBar.GetComponent<Image>();
        if (rowBarSprite != null) { nameBarImg.sprite = rowBarSprite; nameBarImg.type = Image.Type.Sliced; }
        nameBarImg.color = new Color(0.82f, 0.82f, 0.85f, 1f);
        SetRect(nameBar, new Vector2(0.07f, 0.08f), new Vector2(0.52f, 0.92f), Vector2.zero, Vector2.zero);

        GameObject nameText = CreateText("NameText", nameBar.transform, "Name", 15, TextAnchor.MiddleLeft);
        SetRect(nameText, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Avatar (small character)
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(row.transform, false);
        Image avatarImg = avatar.GetComponent<Image>();
        avatarImg.color = Color.white;
        avatarImg.preserveAspect = true;
        SetRect(avatar, new Vector2(0.53f, 0.02f), new Vector2(0.62f, 0.98f), Vector2.zero, Vector2.zero);

        // Glasses overlay
        GameObject glasses = new GameObject("Glasses", typeof(RectTransform), typeof(Image));
        glasses.transform.SetParent(avatar.transform, false);
        Image glassesImg = glasses.GetComponent<Image>();
        glassesImg.color = Color.white;
        glassesImg.preserveAspect = true;
        glassesImg.raycastTarget = false;
        SetRect(glasses, new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.55f), Vector2.zero, Vector2.zero);
        glasses.SetActive(false);

        // Score
        GameObject scoreText = CreateText("ScoreText", row.transform, "0", 16, TextAnchor.MiddleCenter);
        SetRect(scoreText, new Vector2(0.63f, 0f), new Vector2(0.76f, 1f), Vector2.zero, Vector2.zero);
        scoreText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Rank
        GameObject rankText = CreateText("RankText", row.transform, "0", 16, TextAnchor.MiddleCenter);
        SetRect(rankText, new Vector2(0.77f, 0f), new Vector2(0.84f, 1f), Vector2.zero, Vector2.zero);
        rankText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Delete (X icon)
        GameObject delBtn = new GameObject("DeleteBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        delBtn.transform.SetParent(row.transform, false);
        Image delImg = delBtn.GetComponent<Image>();
        if (xIconSprite != null) { delImg.sprite = xIconSprite; delImg.preserveAspect = true; }
        delImg.color = Color.white;
        SetRect(delBtn, new Vector2(0.85f, 0.12f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

        // Edit (pencil icon)
        GameObject editBtn = new GameObject("EditBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        editBtn.transform.SetParent(row.transform, false);
        Image editImg = editBtn.GetComponent<Image>();
        if (editIconSprite != null) { editImg.sprite = editIconSprite; editImg.preserveAspect = true; }
        editImg.color = Color.white;
        SetRect(editBtn, new Vector2(0.93f, 0.12f), new Vector2(1.00f, 0.88f), Vector2.zero, Vector2.zero);

        row.SetActive(false);
        return row;
    }

    private static GameObject CreateMemberCardTemplate(Transform parent)
    {
        GameObject card = new GameObject("MemberCardTemplate", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);

        // Character avatar area
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(card.transform, false);
        Image avatarImg = avatar.GetComponent<Image>();
        avatarImg.color = Color.white;
        avatarImg.preserveAspect = true;
        SetRect(avatar, new Vector2(0.10f, 0.25f), new Vector2(0.90f, 0.95f), Vector2.zero, Vector2.zero);

        // Glasses overlay (child of avatar)
        GameObject glasses = new GameObject("Glasses", typeof(RectTransform), typeof(Image));
        glasses.transform.SetParent(avatar.transform, false);
        Image glassesImg = glasses.GetComponent<Image>();
        glassesImg.color = Color.white;
        glassesImg.preserveAspect = true;
        glassesImg.raycastTarget = false;
        SetRect(glasses, new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.58f), Vector2.zero, Vector2.zero);
        glasses.SetActive(false);

        // Name text (bottom)
        GameObject nameText = CreateText("NameText", card.transform, "Name", 12, TextAnchor.MiddleCenter);
        SetRect(nameText, new Vector2(0f, 0.02f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Delete button (small X, top right corner)
        GameObject delBtn = CreateButton("DeleteBtn", card.transform, "x",
            new Vector2(0.78f, 0.82f), new Vector2(0.98f, 0.98f), new Color(0.9f, 0.3f, 0.3f, 0.8f));
        delBtn.GetComponentInChildren<Text>().fontSize = 12;

        card.SetActive(false);
        return card;
    }

    /// <summary>
    /// Row template for team panel: [number] [gray name bar] [small avatars] [X delete]
    /// Matches design: horizontal row per member.
    /// </summary>
    private static GameObject CreateTeamMemberRowTemplate(Transform parent)
    {
        Sprite nameBarSprite = LoadPlayerPanelSprite("name_bar");

        GameObject row = new GameObject("MemberCardTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent bg
        row.GetComponent<LayoutElement>().preferredHeight = 44f;

        // Number label (left)
        GameObject numLabel = CreateText("NumLabel", row.transform, "1", 20, TextAnchor.MiddleCenter);
        SetRect(numLabel, new Vector2(0f, 0f), new Vector2(0.07f, 1f), Vector2.zero, Vector2.zero);
        numLabel.GetComponent<Text>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
        numLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Name bar (gray background with name text)
        GameObject nameBar = new GameObject("NameBar", typeof(RectTransform), typeof(Image));
        nameBar.transform.SetParent(row.transform, false);
        Image nameBarImg = nameBar.GetComponent<Image>();
        if (nameBarSprite != null) { nameBarImg.sprite = nameBarSprite; nameBarImg.type = Image.Type.Sliced; }
        nameBarImg.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        SetRect(nameBar, new Vector2(0.08f, 0.08f), new Vector2(0.60f, 0.92f), Vector2.zero, Vector2.zero);

        GameObject nameText = CreateText("NameText", nameBar.transform, "Name", 16, TextAnchor.MiddleLeft);
        SetRect(nameText, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Avatar area (small character preview, child of row)
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(row.transform, false);
        Image avatarImg = avatar.GetComponent<Image>();
        avatarImg.color = Color.white;
        avatarImg.preserveAspect = true;
        SetRect(avatar, new Vector2(0.62f, 0.02f), new Vector2(0.72f, 0.98f), Vector2.zero, Vector2.zero);

        // Glasses overlay
        GameObject glasses = new GameObject("Glasses", typeof(RectTransform), typeof(Image));
        glasses.transform.SetParent(avatar.transform, false);
        Image glassesImg = glasses.GetComponent<Image>();
        glassesImg.color = Color.white;
        glassesImg.preserveAspect = true;
        glassesImg.raycastTarget = false;
        SetRect(glasses, new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.55f), Vector2.zero, Vector2.zero);
        glasses.SetActive(false);

        // Delete button (red X, right side)
        GameObject delBtn = CreateButton("DeleteBtn", row.transform, "X",
            new Vector2(0.88f, 0.08f), new Vector2(0.98f, 0.92f), new Color(0.9f, 0.3f, 0.3f, 1f));
        delBtn.GetComponentInChildren<Text>().fontSize = 18;

        row.SetActive(false);
        return row;
    }

    // ===== Wire References =====

    private static void WireReferences(
        TeamSelectView view, TeamSelectController controller,
        PanelClassView panelClassView, PanelPlayerView panelPlayerView,
        PanelTeamView panelTeamView, PanelSavedTeamView panelSavedTeamView,
        GameObject canvas, GameObject topBar, GameObject teamBar, GameObject playerGrid, GameObject bottomBar,
        GameObject backBtn, GameObject classDropdown, GameObject newClassBtn, GameObject settingBtn,
        GameObject bluePanel, GameObject blueBorder, GameObject blueAvatarContainer, GameObject blueLabel,
        GameObject redPanel, GameObject redBorder, GameObject redAvatarContainer, GameObject redLabel,
        GameObject oneVsOneBtn, GameObject teamModeBtn, GameObject oneVsOneHL, GameObject teamModeHL,
        GameObject scrollView, GameObject content, GameObject playerIconTemplate, GameObject addPlayerBtn,
        GameObject savedTeamsBtn, GameObject startBtn,
        GameObject panelClassRoot, GameObject panelPlayerRoot, GameObject panelTeamRoot, GameObject panelSavedTeamRoot,
        GameObject teamAvatarSlotTemplate)
    {
        // TeamSelectView
        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("backButton").objectReferenceValue = backBtn.GetComponent<Button>();
        viewSo.FindProperty("classDropdown").objectReferenceValue = classDropdown.GetComponent<Dropdown>();
        viewSo.FindProperty("newClassButton").objectReferenceValue = newClassBtn.GetComponent<Button>();
        viewSo.FindProperty("settingButton").objectReferenceValue = settingBtn.GetComponent<Button>();
        viewSo.FindProperty("blueTeamButton").objectReferenceValue = bluePanel.GetComponent<Button>() ?? bluePanel.AddComponent<Button>();
        viewSo.FindProperty("blueTeamBorder").objectReferenceValue = blueBorder.GetComponent<Image>();
        viewSo.FindProperty("blueTeamAvatarContainer").objectReferenceValue = blueAvatarContainer.transform;
        viewSo.FindProperty("blueTeamLabel").objectReferenceValue = blueLabel.GetComponent<Text>();
        viewSo.FindProperty("redTeamButton").objectReferenceValue = redPanel.GetComponent<Button>() ?? redPanel.AddComponent<Button>();
        viewSo.FindProperty("redTeamBorder").objectReferenceValue = redBorder.GetComponent<Image>();
        viewSo.FindProperty("redTeamAvatarContainer").objectReferenceValue = redAvatarContainer.transform;
        viewSo.FindProperty("redTeamLabel").objectReferenceValue = redLabel.GetComponent<Text>();
        viewSo.FindProperty("oneVsOneButton").objectReferenceValue = oneVsOneBtn.GetComponent<Button>();
        viewSo.FindProperty("teamModeButton").objectReferenceValue = teamModeBtn.GetComponent<Button>();
        viewSo.FindProperty("oneVsOneHighlight").objectReferenceValue = oneVsOneHL.GetComponent<Image>();
        viewSo.FindProperty("teamModeHighlight").objectReferenceValue = teamModeHL.GetComponent<Image>();
        viewSo.FindProperty("playerGridContent").objectReferenceValue = content.transform;
        viewSo.FindProperty("playerIconTemplate").objectReferenceValue = playerIconTemplate;
        viewSo.FindProperty("addPlayerButton").objectReferenceValue = addPlayerBtn.GetComponent<Button>();
        viewSo.FindProperty("playerScrollRect").objectReferenceValue = scrollView.GetComponent<ScrollRect>();
        viewSo.FindProperty("savedTeamsButton").objectReferenceValue = savedTeamsBtn.GetComponent<Button>();
        viewSo.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        viewSo.FindProperty("panelClassView").objectReferenceValue = panelClassView;
        viewSo.FindProperty("panelPlayerView").objectReferenceValue = panelPlayerView;
        viewSo.FindProperty("panelTeamView").objectReferenceValue = panelTeamView;
        viewSo.FindProperty("panelSavedTeamView").objectReferenceValue = panelSavedTeamView;
        viewSo.FindProperty("teamAvatarSlotTemplate").objectReferenceValue = teamAvatarSlotTemplate;
        // Mode toggle sprites + text labels
        Sprite s1vs1 = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/OneVsOneButton.png");
        Sprite sTeam = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/TeamModeButton.png");
        viewSo.FindProperty("modeSprite1vs1").objectReferenceValue = s1vs1;
        viewSo.FindProperty("modeSpriteTeam").objectReferenceValue = sTeam;
        GameObject mt1 = oneVsOneBtn.transform.Find("ModeText1vs1")?.gameObject;
        GameObject mt2 = oneVsOneBtn.transform.Find("ModeTextTeam")?.gameObject;
        if (mt1 != null) viewSo.FindProperty("modeText1vs1").objectReferenceValue = mt1.GetComponent<Text>();
        if (mt2 != null) viewSo.FindProperty("modeTextTeam").objectReferenceValue = mt2.GetComponent<Text>();

        // Wire character sprites for player grid and team avatars
        viewSo.FindProperty("charBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_male");
        viewSo.FindProperty("femaleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_female");
        SerializedProperty hairArr = viewSo.FindProperty("charHairSprites");
        hairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            hairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));
        SerializedProperty femHairArr = viewSo.FindProperty("charHairFemaleSprites");
        femHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            femHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_female_" + (i + 1));
        SerializedProperty glassArr = viewSo.FindProperty("bigGlassesSprites");
        glassArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            glassArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("big_glasses_" + (i + 1));

        // Avatar background sprites (blue/red rounded squares)
        EnsureSpriteImportSettings(TextureFolder + "/AvatarBgBlue.png");
        EnsureSpriteImportSettings(TextureFolder + "/AvatarBgRed.png");
        EnsureSpriteImportSettings(TextureFolder + "/PlayerCardWhite.png");
        viewSo.FindProperty("avatarBgBlue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgBlue.png");
        viewSo.FindProperty("avatarBgRed").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgRed.png");
        viewSo.FindProperty("playerCardWhite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PlayerCardWhite.png");
        viewSo.FindProperty("playerCardBlue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgBlue.png");
        viewSo.FindProperty("playerCardRed").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgRed.png");

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // TeamSelectController
        SerializedObject ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("teamSelectView").objectReferenceValue = view;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // PanelClassView
        SerializedObject pcSo = new SerializedObject(panelClassView);
        Transform pcFrame = panelClassRoot.transform.Find("Frame");
        if (pcFrame == null) { Debug.LogWarning("TeamSelectSceneBuilder: PanelClassRoot/Frame not found, skipping panel wiring."); return; }
        pcSo.FindProperty("panelRoot").objectReferenceValue = panelClassRoot;
        // ClassNameInput is inside ClassNameBar
        Transform classBarT = pcFrame.Find("ClassNameBar");
        pcSo.FindProperty("classNameInput").objectReferenceValue = classBarT != null
            ? classBarT.Find("ClassNameInput").GetComponent<InputField>()
            : pcFrame.Find("ClassNameInput").GetComponent<InputField>();
        pcSo.FindProperty("studentListContent").objectReferenceValue = pcFrame.Find("StudentScrollArea/Viewport/StudentListContent");
        pcSo.FindProperty("studentRowTemplate").objectReferenceValue = pcFrame.Find("StudentScrollArea/Viewport/StudentListContent/StudentRowTemplate").gameObject;
        pcSo.FindProperty("addStudentButton").objectReferenceValue = pcFrame.Find("AddStudentButton").GetComponent<Button>();
        pcSo.FindProperty("copyButton").objectReferenceValue = pcFrame.Find("CopyButton").GetComponent<Button>();
        pcSo.FindProperty("saveButton").objectReferenceValue = pcFrame.Find("SaveButton").GetComponent<Button>();
        pcSo.FindProperty("deleteButton").objectReferenceValue = pcFrame.Find("DeleteButton").GetComponent<Button>();
        pcSo.FindProperty("closeButton").objectReferenceValue = pcFrame.Find("CloseButton").GetComponent<Button>();

        // Wire character sprites for class panel
        pcSo.FindProperty("charBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_male");
        pcSo.FindProperty("femaleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_female");
        SerializedProperty pcHairArr = pcSo.FindProperty("charHairSprites");
        pcHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            pcHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));
        SerializedProperty pcFemHairArr = pcSo.FindProperty("charHairFemaleSprites");
        pcFemHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            pcFemHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_female_" + (i + 1));
        SerializedProperty pcGlassArr = pcSo.FindProperty("bigGlassesSprites");
        pcGlassArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            pcGlassArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("big_glasses_" + (i + 1));

        pcSo.ApplyModifiedPropertiesWithoutUndo();

        // PanelPlayerView
        SerializedObject ppSo = new SerializedObject(panelPlayerView);
        Transform ppFrame = panelPlayerRoot.transform.Find("Frame");
        if (ppFrame == null) { Debug.LogWarning("TeamSelectSceneBuilder: PanelPlayerRoot/Frame not found, skipping panel wiring."); return; }
        ppSo.FindProperty("panelRoot").objectReferenceValue = panelPlayerRoot;
        ppSo.FindProperty("nameInput").objectReferenceValue = ppFrame.Find("NameInput").GetComponent<InputField>();
        ppSo.FindProperty("classDropdown").objectReferenceValue = ppFrame.Find("ClassDropdown").GetComponent<Dropdown>();
        ppSo.FindProperty("scoreText").objectReferenceValue = ppFrame.Find("ScoreLabel").GetComponent<Text>();
        ppSo.FindProperty("saveButton").objectReferenceValue = ppFrame.Find("SaveButton").GetComponent<Button>();
        ppSo.FindProperty("deleteButton").objectReferenceValue = ppFrame.Find("DeleteButton").GetComponent<Button>();
        ppSo.FindProperty("closeButton").objectReferenceValue = ppFrame.Find("CloseButton").GetComponent<Button>();

        // Tab buttons and images
        ppSo.FindProperty("hairTabButton").objectReferenceValue = ppFrame.Find("HairTabButton").GetComponent<Button>();
        ppSo.FindProperty("glassesTabButton").objectReferenceValue = ppFrame.Find("GlassesTabButton").GetComponent<Button>();
        ppSo.FindProperty("hairTabImage").objectReferenceValue = ppFrame.Find("HairTabButton").GetComponent<Image>();
        ppSo.FindProperty("glassesTabImage").objectReferenceValue = ppFrame.Find("GlassesTabButton").GetComponent<Image>();
        ppSo.FindProperty("tabContainerBg").objectReferenceValue = ppFrame.Find("TabContainerBg").GetComponent<Image>();
        ppSo.FindProperty("hairPopupSprite").objectReferenceValue = LoadPlayerPanelSprite("hair_popup_bg");
        ppSo.FindProperty("glassesPopupSprite").objectReferenceValue = LoadPlayerPanelSprite("glasses_popup_bg");

        // Grids — Find inactive children manually
        ppSo.FindProperty("hairGrid").objectReferenceValue = FindChildByName(ppFrame, "HairGrid");
        ppSo.FindProperty("glassesGrid").objectReferenceValue = FindChildByName(ppFrame, "GlassesGrid");

        // Hair buttons + checkmarks
        Transform hairGridT = ppFrame.Find("HairGrid");
        SerializedProperty hairBtnsArr = ppSo.FindProperty("hairButtons");
        SerializedProperty hairChkArr = ppSo.FindProperty("hairCheckmarks");
        hairBtnsArr.arraySize = 6;
        hairChkArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            Transform hb = hairGridT.Find("Hair_" + i);
            if (hb != null)
            {
                hairBtnsArr.GetArrayElementAtIndex(i).objectReferenceValue = hb.GetComponent<Button>();
                Transform chk = hb.Find("Check");
                if (chk != null) hairChkArr.GetArrayElementAtIndex(i).objectReferenceValue = chk.GetComponent<Image>();
            }
        }

        // Glasses buttons + checkmarks
        Transform glassesGridT = ppFrame.Find("GlassesGrid");
        SerializedProperty glassBtnsArr = ppSo.FindProperty("glassesButtons");
        SerializedProperty glassChkArr = ppSo.FindProperty("glassesCheckmarks");
        glassBtnsArr.arraySize = 6;
        glassChkArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            Transform gb = glassesGridT.Find("Glasses_" + i);
            if (gb != null)
            {
                glassBtnsArr.GetArrayElementAtIndex(i).objectReferenceValue = gb.GetComponent<Button>();
                Transform chk = gb.Find("Check");
                if (chk != null) glassChkArr.GetArrayElementAtIndex(i).objectReferenceValue = chk.GetComponent<Image>();
            }
        }

        // Character preview
        ppSo.FindProperty("characterBodyImage").objectReferenceValue = ppFrame.Find("CharacterBody").GetComponent<Image>();
        ppSo.FindProperty("characterGlassesImage").objectReferenceValue = ppFrame.Find("CharacterGlasses").GetComponent<Image>();

        // Load sprite arrays for runtime preview
        Sprite bodyBaldSprite = LoadPlayerPanelSprite("body_male");
        ppSo.FindProperty("charBodySprite").objectReferenceValue = bodyBaldSprite;

        SerializedProperty hairSpritesArr = ppSo.FindProperty("charHairSprites");
        hairSpritesArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            hairSpritesArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));

        SerializedProperty bigGlassesArr = ppSo.FindProperty("bigGlassesSprites");
        bigGlassesArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            bigGlassesArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("big_glasses_" + (i + 1));

        // Gender button + body sprites
        Transform genderBtnT = ppFrame.Find("GenderButton");
        if (genderBtnT != null)
        {
            ppSo.FindProperty("genderButton").objectReferenceValue = genderBtnT.GetComponent<Button>();
            ppSo.FindProperty("genderIcon").objectReferenceValue = genderBtnT.GetComponent<Image>();
        }
        ppSo.FindProperty("maleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_male");
        ppSo.FindProperty("femaleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_female");

        // Female hair sprites
        SerializedProperty femaleHairArr = ppSo.FindProperty("charHairFemaleSprites");
        femaleHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            femaleHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_female_" + (i + 1));

        // Male hair sprites (alias of charHairSprites, already wired above)
        SerializedProperty maleHairArr = ppSo.FindProperty("charHairMaleSprites");
        maleHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            maleHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));

        // Hair grid thumbnails — male
        SerializedProperty thumbMaleArr = ppSo.FindProperty("thumbHairMaleSprites");
        thumbMaleArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            thumbMaleArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("thumb_hair_" + (i + 1));

        // Hair grid thumbnails — female
        SerializedProperty thumbFemaleArr = ppSo.FindProperty("thumbHairFemaleSprites");
        thumbFemaleArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            thumbFemaleArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("thumb_hair_female_" + (i + 1));

        ppSo.ApplyModifiedPropertiesWithoutUndo();

        // PanelTeamView
        SerializedObject ptSo = new SerializedObject(panelTeamView);
        Transform ptFrame = panelTeamRoot.transform.Find("Frame");
        if (ptFrame == null) { Debug.LogWarning("TeamSelectSceneBuilder: PanelTeamRoot/Frame not found, skipping panel wiring."); return; }
        ptSo.FindProperty("panelRoot").objectReferenceValue = panelTeamRoot;
        ptSo.FindProperty("teamNameInput").objectReferenceValue = ptFrame.Find("TeamNameInput").GetComponent<InputField>();
        ptSo.FindProperty("memberListContent").objectReferenceValue = ptFrame.Find("MemberScrollArea/Viewport/MemberListContent");
        ptSo.FindProperty("memberCardTemplate").objectReferenceValue = ptFrame.Find("MemberScrollArea/Viewport/MemberListContent/MemberCardTemplate").gameObject;
        ptSo.FindProperty("saveButton").objectReferenceValue = ptFrame.Find("SaveButton").GetComponent<Button>();
        ptSo.FindProperty("deleteButton").objectReferenceValue = ptFrame.Find("DeleteButton").GetComponent<Button>();
        ptSo.FindProperty("closeButton").objectReferenceValue = ptFrame.Find("CloseButton").GetComponent<Button>();

        // Team bar image + sprites
        ptSo.FindProperty("teamBarImage").objectReferenceValue = ptFrame.Find("TeamBar").GetComponent<Image>();
        ptSo.FindProperty("teamNameText").objectReferenceValue = ptFrame.Find("TeamBar/TeamNameText").GetComponent<Text>();
        ptSo.FindProperty("teamBarBlueSprite").objectReferenceValue = LoadTeamPanelSprite("team_bar_blue");
        ptSo.FindProperty("teamBarRedSprite").objectReferenceValue = LoadTeamPanelSprite("team_bar_red");

        // Wire character sprites for team panel
        ptSo.FindProperty("charBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_male");
        ptSo.FindProperty("femaleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_female");
        SerializedProperty ptHairArr = ptSo.FindProperty("charHairSprites");
        ptHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            ptHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));
        SerializedProperty ptFemHairArr = ptSo.FindProperty("charHairFemaleSprites");
        ptFemHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            ptFemHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_female_" + (i + 1));
        SerializedProperty ptGlassArr = ptSo.FindProperty("bigGlassesSprites");
        ptGlassArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            ptGlassArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("big_glasses_" + (i + 1));

        ptSo.ApplyModifiedPropertiesWithoutUndo();

        // PanelSavedTeamView
        SerializedObject pstSo = new SerializedObject(panelSavedTeamView);
        Transform pstFrame = panelSavedTeamRoot.transform.Find("Frame");
        if (pstFrame == null) { Debug.LogWarning("TeamSelectSceneBuilder: PanelSavedTeamRoot/Frame not found, skipping panel wiring."); return; }
        pstSo.FindProperty("panelRoot").objectReferenceValue = panelSavedTeamRoot;
        pstSo.FindProperty("teamGridContent").objectReferenceValue = pstFrame.Find("GridArea/Viewport/TeamGridContent");
        pstSo.FindProperty("teamCardTemplate").objectReferenceValue = pstFrame.Find("GridArea/Viewport/TeamGridContent/TeamCardTemplate").gameObject;
        pstSo.FindProperty("closeButton").objectReferenceValue = pstFrame.Find("CloseButton").GetComponent<Button>();
        pstSo.FindProperty("backButton").objectReferenceValue = pstFrame.Find("BackButton").GetComponent<Button>();
        pstSo.FindProperty("confirmButton").objectReferenceValue = pstFrame.Find("ConfirmButton").GetComponent<Button>();
        // Team bar sprites
        pstSo.FindProperty("teamBarGray").objectReferenceValue = LoadSaveTeamSprite("team_bar_gray");
        pstSo.FindProperty("teamBarBlue").objectReferenceValue = LoadSaveTeamSprite("team_bar_blue");
        pstSo.FindProperty("teamBarRed").objectReferenceValue = LoadSaveTeamSprite("team_bar_red");
        // Avatar bg sprites (white/blue/red)
        EnsureSpriteImportSettings(TextureFolder + "/PlayerCardWhite.png");
        pstSo.FindProperty("avatarBgWhite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/PlayerCardWhite.png");
        pstSo.FindProperty("avatarBgBlue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgBlue.png");
        pstSo.FindProperty("avatarBgRed").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/AvatarBgRed.png");
        pstSo.FindProperty("charBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_male");
        pstSo.FindProperty("femaleBodySprite").objectReferenceValue = LoadPlayerPanelSprite("body_female");
        SerializedProperty stHairArr = pstSo.FindProperty("charHairSprites");
        stHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            stHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_" + (i + 1));
        SerializedProperty stFemHairArr = pstSo.FindProperty("charHairFemaleSprites");
        stFemHairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            stFemHairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadPlayerPanelSprite("char_hair_female_" + (i + 1));
        pstSo.ApplyModifiedPropertiesWithoutUndo();

        // Add Button components to team panels if missing
        if (bluePanel.GetComponent<Button>() == null) bluePanel.AddComponent<Button>();
        if (redPanel.GetComponent<Button>() == null) redPanel.AddComponent<Button>();
    }

    // ===== UI Helpers =====

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
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
        text.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        text.font = GetBuiltinFont();
        return go;
    }

    private static GameObject CreateButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bgColor;
        SetRect(go, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        GameObject textGo = CreateText("Text", go.transform, label, 20, TextAnchor.MiddleCenter);
        SetRect(textGo, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textGo.GetComponent<Text>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        return go;
    }

    private static GameObject CreateInputField(string name, Transform parent, string placeholder,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Color.white;
        SetRect(go, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Text child
        GameObject textGo = CreateText("Text", go.transform, "", 18, TextAnchor.MiddleLeft);
        SetRect(textGo, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);

        // Placeholder child
        GameObject phGo = CreateText("Placeholder", go.transform, placeholder, 18, TextAnchor.MiddleLeft);
        SetRect(phGo, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        phGo.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        phGo.GetComponent<Text>().fontStyle = FontStyle.Italic;

        InputField inputField = go.GetComponent<InputField>();
        inputField.textComponent = textGo.GetComponent<Text>();
        inputField.placeholder = phGo.GetComponent<Text>();

        return go;
    }

    private static GameObject CreateDropdown(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        SetRect(go, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Caption text — larger font, padding, truncate overflow
        GameObject captionText = CreateText("Label", go.transform, "Chọn...", 20, TextAnchor.MiddleLeft);
        SetRect(captionText, new Vector2(0.06f, 0f), new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);
        captionText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
        captionText.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Truncate;
        captionText.GetComponent<Text>().color = new Color(0.15f, 0.15f, 0.2f, 1f);

        // Arrow indicator — ▼ symbol
        GameObject arrow = CreateText("Arrow", go.transform, "\u25bc", 14, TextAnchor.MiddleCenter);
        SetRect(arrow, new Vector2(0.84f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        arrow.GetComponent<Text>().color = new Color(0.4f, 0.4f, 0.5f, 1f);

        // Template (dropdown list) — taller, with border and shadow
        GameObject template = CreateImage("Template", go.transform, new Color(1f, 1f, 1f, 1f));
        RectTransform templateRT = template.GetComponent<RectTransform>();
        // Anchor at bottom of dropdown, expand downward with fixed height
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.sizeDelta = new Vector2(0f, 250f);

        // Add Canvas + GraphicRaycaster so dropdown renders on top of everything
        Canvas templateCanvas = template.AddComponent<Canvas>();
        templateCanvas.overrideSorting = true;
        templateCanvas.sortingOrder = 30000;
        template.AddComponent<GraphicRaycaster>();

        // Border/outline effect via slightly larger background
        GameObject templateBorder = CreateImage("Border", template.transform, new Color(0.6f, 0.6f, 0.7f, 1f));
        SetRect(templateBorder, Vector2.zero, Vector2.one, new Vector2(-2f, -2f), new Vector2(2f, 2f));

        // Viewport with mask + ScrollRect for scrollable list
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(template.transform, false);
        SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = true;

        // Content container with layout for auto-sizing
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        // VerticalLayoutGroup so items stack properly
        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 0f;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        // ContentSizeFitter so content grows with items (enables scrolling)
        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ScrollRect on template for scrolling
        ScrollRect scrollRect = template.AddComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        // Scrollbar
        GameObject scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbar.transform.SetParent(template.transform, false);
        RectTransform scrollbarRT = scrollbar.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(12f, 0f);
        scrollbarRT.offsetMin = new Vector2(-12f, 2f);
        scrollbarRT.offsetMax = new Vector2(0f, -2f);
        scrollbar.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.9f, 1f);

        // Scrollbar handle
        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbar.transform, false);
        SetRect(slidingArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject handle = CreateImage("Handle", slidingArea.transform, new Color(0.5f, 0.55f, 0.65f, 1f));
        SetRect(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Scrollbar scrollbarComp = scrollbar.GetComponent<Scrollbar>();
        scrollbarComp.handleRect = handle.GetComponent<RectTransform>();
        scrollbarComp.direction = Scrollbar.Direction.BottomToTop;
        scrollbarComp.targetGraphic = handle.GetComponent<Image>();

        scrollRect.verticalScrollbar = scrollbarComp;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -2f;

        // Item template — taller for touch, with hover color
        float itemHeight = 44f;
        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRT = item.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f);
        itemRT.anchorMax = new Vector2(1f, 0.5f);
        itemRT.pivot = new Vector2(0.5f, 0.5f);
        itemRT.sizeDelta = new Vector2(0f, itemHeight);

        // LayoutElement so VerticalLayoutGroup knows the item height
        LayoutElement le = item.GetComponent<LayoutElement>();
        le.minHeight = itemHeight;
        le.preferredHeight = itemHeight;

        // Item background with hover highlight
        GameObject itemBg = CreateImage("Item Background", item.transform, new Color(0.96f, 0.96f, 0.98f, 1f));
        SetRect(itemBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Item checkmark — small colored dot
        GameObject checkmark = CreateImage("Item Checkmark", item.transform, new Color(0.2f, 0.6f, 0.9f, 1f));
        SetRect(checkmark, new Vector2(0.02f, 0.25f), new Vector2(0.06f, 0.75f), Vector2.zero, Vector2.zero);

        // Item label — larger readable font
        GameObject itemLabel = CreateText("Item Label", item.transform, "Option", 20, TextAnchor.MiddleLeft);
        SetRect(itemLabel, new Vector2(0.08f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        itemLabel.GetComponent<Text>().color = new Color(0.15f, 0.15f, 0.2f, 1f);
        itemLabel.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;

        // Wire dropdown component
        Dropdown dropdown = go.GetComponent<Dropdown>();
        dropdown.captionText = captionText.GetComponent<Text>();
        dropdown.template = templateRT;
        dropdown.itemText = itemLabel.GetComponent<Text>();

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBg.GetComponent<Image>();
        toggle.graphic = checkmark.GetComponent<Image>();
        toggle.isOn = true;

        // Toggle colors for highlight on hover
        ColorBlock cb = toggle.colors;
        cb.normalColor = new Color(0.96f, 0.96f, 0.98f, 1f);
        cb.highlightedColor = new Color(0.85f, 0.9f, 1f, 1f);
        cb.pressedColor = new Color(0.75f, 0.85f, 0.95f, 1f);
        cb.selectedColor = new Color(0.85f, 0.9f, 1f, 1f);
        toggle.colors = cb;

        template.SetActive(false);

        return go;
    }

    private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static Font GetBuiltinFont()
    {
        // Prefer Quicksand Bold
        Font quicksand = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Quicksand/Quicksand-Bold.ttf");
        if (quicksand != null) return quicksand;
        quicksand = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Quicksand/Quicksand-Medium.ttf");
        if (quicksand != null) return quicksand;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject CreateSpriteButton(string name, Transform parent, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }
        img.color = Color.white;
        SetRect(go, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        return go;
    }

    private static void EnsureSpriteImportSettings(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
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
}
