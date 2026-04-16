using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TrainPathSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/TrainPathGame";
    private const string ScenePath = "Assets/Game/Scenes/TrainPathGame/TrainPathGame.unity";
    private const string ArtFolder = "Assets/Game/Textures/TrainPathGame";

    private static Sprite LoadArtSprite(string name)
    {
        string path = ArtFolder + "/" + name + ".png";
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

    [MenuItem("Tools/TrainPath/Build Scene")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("TrainPathSceneBuilder: Cannot build while in Play mode.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TrainPathSceneBuilder: finished generating scene.");
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

    private static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMainCamera();

        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject root = new GameObject("TrainPathRoot");
        TrainPathGameController controller = root.AddComponent<TrainPathGameController>();
        TrainPathGameView view = root.AddComponent<TrainPathGameView>();

        // Background (sprite)
        Sprite bgSprite = LoadArtSprite("bg_gameplay");
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bg.GetComponent<Image>();
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.96f, 0.87f, 0.60f, 1f);
        bgImg.raycastTarget = false;
        SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Team bars (top)
        Sprite teamBlueSprite = LoadArtSprite("team_bar_blue");
        Sprite teamRedSprite = LoadArtSprite("team_bar_red");

        GameObject teamBlueBar = new GameObject("TeamBlueBar", typeof(RectTransform), typeof(Image));
        teamBlueBar.transform.SetParent(canvasGo.transform, false);
        Image tbImg = teamBlueBar.GetComponent<Image>();
        if (teamBlueSprite != null) { tbImg.sprite = teamBlueSprite; tbImg.preserveAspect = true; }
        tbImg.color = Color.white; tbImg.raycastTarget = false;
        RectTransform tbRT = teamBlueBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0.01f, 0.88f); tbRT.anchorMax = new Vector2(0.35f, 0.99f);
        tbRT.anchoredPosition = new Vector2(125f, 2f); tbRT.sizeDelta = Vector2.zero;

        GameObject teamRedBar = new GameObject("TeamRedBar", typeof(RectTransform), typeof(Image));
        teamRedBar.transform.SetParent(canvasGo.transform, false);
        Image trImg = teamRedBar.GetComponent<Image>();
        if (teamRedSprite != null) { trImg.sprite = teamRedSprite; trImg.preserveAspect = true; }
        trImg.color = Color.white; trImg.raycastTarget = false;
        RectTransform trRT = teamRedBar.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0.65f, 0.88f); trRT.anchorMax = new Vector2(0.99f, 0.99f);
        trRT.anchoredPosition = new Vector2(-124f, 1f); trRT.sizeDelta = Vector2.zero;

        // Timer (circle, top center)
        Sprite timerCircleSprite = LoadArtSprite("timer_circle");
        GameObject timerBg = new GameObject("TimerBg", typeof(RectTransform), typeof(Image));
        timerBg.transform.SetParent(canvasGo.transform, false);
        Image tcImg = timerBg.GetComponent<Image>();
        if (timerCircleSprite != null) { tcImg.sprite = timerCircleSprite; tcImg.preserveAspect = true; }
        tcImg.color = Color.white; tcImg.raycastTarget = false;
        RectTransform timerBgRT = timerBg.GetComponent<RectTransform>();
        timerBgRT.anchorMin = new Vector2(0.5f, 1f); timerBgRT.anchorMax = new Vector2(0.5f, 1f);
        timerBgRT.pivot = new Vector2(0.5f, 0.5f);
        timerBgRT.anchoredPosition = new Vector2(0f, -35f); timerBgRT.sizeDelta = new Vector2(60f, 60f);

        GameObject timerText = CreateText("TimerText", timerBg.transform, "120", 22, TextAnchor.MiddleCenter);
        timerText.GetComponent<Text>().color = Color.white;
        timerText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        SetRect(timerText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Navigation buttons (sprites)
        Sprite backSprite = LoadArtSprite("btn_back");
        Sprite homeSprite = LoadArtSprite("btn_home");
        Sprite settingSprite = LoadArtSprite("btn_setting");

        GameObject backButton = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backButton.transform.SetParent(canvasGo.transform, false);
        Image backImg = backButton.GetComponent<Image>();
        if (backSprite != null) { backImg.sprite = backSprite; backImg.preserveAspect = true; }
        backImg.color = Color.white;
        RectTransform backRT = backButton.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0f, 1f); backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(8f, -5f); backRT.sizeDelta = new Vector2(40f, 40f);

        GameObject homeButton = new GameObject("HomeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        homeButton.transform.SetParent(canvasGo.transform, false);
        Image homeImg = homeButton.GetComponent<Image>();
        if (homeSprite != null) { homeImg.sprite = homeSprite; homeImg.preserveAspect = true; }
        homeImg.color = Color.white;
        RectTransform homeRT = homeButton.GetComponent<RectTransform>();
        homeRT.anchorMin = new Vector2(1f, 1f); homeRT.anchorMax = new Vector2(1f, 1f);
        homeRT.pivot = new Vector2(1f, 1f);
        homeRT.anchoredPosition = new Vector2(-8f, -5f); homeRT.sizeDelta = new Vector2(40f, 40f);

        GameObject settingButton = new GameObject("SettingButton", typeof(RectTransform), typeof(Image), typeof(Button));
        settingButton.transform.SetParent(canvasGo.transform, false);
        Image settingImg = settingButton.GetComponent<Image>();
        if (settingSprite != null) { settingImg.sprite = settingSprite; settingImg.preserveAspect = true; }
        settingImg.color = Color.white;
        RectTransform settingRT = settingButton.GetComponent<RectTransform>();
        settingRT.anchorMin = new Vector2(1f, 1f); settingRT.anchorMax = new Vector2(1f, 1f);
        settingRT.pivot = new Vector2(1f, 1f);
        settingRT.anchoredPosition = new Vector2(-52f, -5f); settingRT.sizeDelta = new Vector2(40f, 40f);

        // Score bars (vertical)
        Sprite barBlueSprite = LoadArtSprite("bar_blue");
        Sprite barRedSprite = LoadArtSprite("bar_red");
        Sprite barWhiteSprite = LoadArtSprite("bar_white");

        GameObject leftBarTrack = new GameObject("LeftScoreBar", typeof(RectTransform), typeof(Image));
        leftBarTrack.transform.SetParent(canvasGo.transform, false);
        Image lbTrackImg = leftBarTrack.GetComponent<Image>();
        if (barWhiteSprite != null) { lbTrackImg.sprite = barWhiteSprite; }
        lbTrackImg.color = new Color(0.9f, 0.9f, 0.9f, 0.8f); lbTrackImg.raycastTarget = false;
        RectTransform lbRT = leftBarTrack.GetComponent<RectTransform>();
        lbRT.anchorMin = new Vector2(0.005f, 0.05f); lbRT.anchorMax = new Vector2(0.03f, 0.86f);
        lbRT.anchoredPosition = new Vector2(486.5f, -26.8f); lbRT.sizeDelta = new Vector2(-8f, -76.1f);

        GameObject leftBarFill = new GameObject("LeftScoreBarFill", typeof(RectTransform), typeof(Image));
        leftBarFill.transform.SetParent(leftBarTrack.transform, false);
        Image lbFillImg = leftBarFill.GetComponent<Image>();
        if (barBlueSprite != null) { lbFillImg.sprite = barBlueSprite; }
        lbFillImg.color = Color.white; lbFillImg.type = Image.Type.Filled;
        lbFillImg.fillMethod = Image.FillMethod.Vertical; lbFillImg.fillOrigin = 0;
        lbFillImg.fillAmount = 0f; lbFillImg.raycastTarget = false;
        SetRect(leftBarFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject rightBarTrack = new GameObject("RightScoreBar", typeof(RectTransform), typeof(Image));
        rightBarTrack.transform.SetParent(canvasGo.transform, false);
        Image rbTrackImg = rightBarTrack.GetComponent<Image>();
        if (barWhiteSprite != null) { rbTrackImg.sprite = barWhiteSprite; }
        rbTrackImg.color = new Color(0.9f, 0.9f, 0.9f, 0.8f); rbTrackImg.raycastTarget = false;
        RectTransform rbRT = rightBarTrack.GetComponent<RectTransform>();
        rbRT.anchorMin = new Vector2(0.97f, 0.05f); rbRT.anchorMax = new Vector2(0.995f, 0.86f);
        rbRT.anchoredPosition = new Vector2(-477.3f, -26.8f); rbRT.sizeDelta = new Vector2(-8.7f, -76.1f);

        GameObject rightBarFill = new GameObject("RightScoreBarFill", typeof(RectTransform), typeof(Image));
        rightBarFill.transform.SetParent(rightBarTrack.transform, false);
        Image rbFillImg = rightBarFill.GetComponent<Image>();
        if (barRedSprite != null) { rbFillImg.sprite = barRedSprite; }
        rbFillImg.color = Color.white; rbFillImg.type = Image.Type.Filled;
        rbFillImg.fillMethod = Image.FillMethod.Vertical; rbFillImg.fillOrigin = 0;
        rbFillImg.fillAmount = 0f; rbFillImg.raycastTarget = false;
        SetRect(rightBarFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Divider (center white bars)
        GameObject divider = CreateImage("Divider", canvasGo.transform, new Color(1f, 1f, 1f, 0.8f));
        RectTransform divRT = divider.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.497f, 0.02f); divRT.anchorMax = new Vector2(0.503f, 0.89f);
        divRT.anchoredPosition = new Vector2(2f, 0f); divRT.sizeDelta = new Vector2(4f, 0f);

        // Player panels
        var p1Refs = CreatePlayerPanel("P1", canvasGo.transform, true);
        var p2Refs = CreatePlayerPanel("P2", canvasGo.transform, false);

        AssignViewReferences(view, controller, timerText, backButton, p1Refs, p2Refs);

        // Tutorial panel
        GameObject tutPanel = CreateTutorialPanel(canvasGo.transform);

        // Wire tutorial to controller
        SerializedObject ctrlSo2 = new SerializedObject(controller);
        ctrlSo2.FindProperty("tutorialPanel").objectReferenceValue = tutPanel.GetComponent<TutorialPanel>();
        ctrlSo2.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    private static GameObject CreateTutorialPanel(Transform parent)
    {
        // Container (always active)
        GameObject container = new GameObject("TutorialContainer", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        SetRect(container.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Panel root (toggled)
        GameObject panel = CreateImage("TutorialPanel", container.transform, new Color(0f, 0f, 0f, 0.7f));
        SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Sprite startBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/MenuScene/btn_start.png");
        GameObject startBtn = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        startBtn.transform.SetParent(panel.transform, false);
        Image sbImg = startBtn.GetComponent<Image>();
        if (startBtnSprite != null) { sbImg.sprite = startBtnSprite; sbImg.preserveAspect = true; }
        sbImg.color = Color.white;
        SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.14f), Vector2.zero, Vector2.zero);

        GameObject titleText = CreateText("TitleText", panel.transform, "Huong dan: TrainPath", 28, TextAnchor.MiddleCenter);
        SetRect(titleText.GetComponent<RectTransform>(), new Vector2(0.15f, 0.80f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        titleText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        TutorialPanel tp = container.AddComponent<TutorialPanel>();
        SerializedObject tpSo = new SerializedObject(tp);
        tpSo.FindProperty("panelRoot").objectReferenceValue = panel;
        tpSo.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        tpSo.FindProperty("titleText").objectReferenceValue = titleText.GetComponent<Text>();
        tpSo.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        return container;
    }

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.96f, 0.91f, 0.82f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static PlayerPanelRefs CreatePlayerPanel(string prefix, Transform parent, bool isLeft)
    {
        PlayerPanelRefs refs = new PlayerPanelRefs();

        float xMin = isLeft ? 0.01f : 0.51f;
        float xMax = isLeft ? 0.49f : 0.99f;

        // Panel background
        GameObject panel = CreateImage(prefix + "Panel", parent, new Color(0f, 0f, 0f, 0f));
        panel.GetComponent<Image>().raycastTarget = false;
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(xMin, 0.02f), new Vector2(xMax, 0.89f), Vector2.zero, Vector2.zero);

        // Player name
        refs.nameText = CreateText(prefix + "Name", panel.transform, isLeft ? "Player 1" : "Player 2", 20, TextAnchor.MiddleCenter);
        SetRect(refs.nameText.GetComponent<RectTransform>(), new Vector2(0.15f, 0.93f), new Vector2(0.75f, 0.99f), Vector2.zero, Vector2.zero);
        refs.nameText.GetComponent<Text>().color = new Color(0.15f, 0.15f, 0.4f, 1f);

        // Score (star display)
        refs.scoreText = CreateText(prefix + "Score", panel.transform, "\u2605 0", 60, TextAnchor.MiddleRight);
        refs.scoreText.GetComponent<Text>().color = new Color(0.9f, 0.6f, 0.1f, 1f);
        refs.scoreText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        refs.scoreText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        refs.scoreText.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(refs.scoreText.GetComponent<RectTransform>(), new Vector2(0.60f, 0.88f), new Vector2(0.98f, 1.00f), Vector2.zero, Vector2.zero);

        // === Grid area with end markers (made square for rotation) ===
        // Panel is ~491x522px. Grid area height = 0.62*522 ≈ 324px.
        // Make width match: 324/491 ≈ 0.66, centered: (0.5-0.33)=0.17 to (0.5+0.33)=0.83
        float markerThickness = 0.09f;
        float gridInnerMin = markerThickness;
        float gridInnerMax = 1f - markerThickness;

        GameObject gridArea = new GameObject(prefix + "GridArea", typeof(RectTransform));
        gridArea.transform.SetParent(panel.transform, false);
        SetRect(gridArea.GetComponent<RectTransform>(), new Vector2(0.17f, 0.30f), new Vector2(0.83f, 0.92f), Vector2.zero, Vector2.zero);
        // Set pivot to center for proper rotation
        gridArea.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        refs.gridArea = gridArea;

        // Grid container
        GameObject gridContainer = new GameObject(prefix + "Grid", typeof(RectTransform));
        gridContainer.transform.SetParent(gridArea.transform, false);
        SetRect(gridContainer.GetComponent<RectTransform>(),
            new Vector2(gridInnerMin, gridInnerMin),
            new Vector2(gridInnerMax, gridInnerMax),
            Vector2.zero, Vector2.zero);

        // Grid background (darker area behind cells)
        GameObject gridBg = CreateImage(prefix + "GridBg", gridContainer.transform, new Color(0.35f, 0.55f, 0.3f, 1f));
        SetRect(gridBg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        int GS = 4; // grid size
        int CC = GS * GS; // cell count
        refs.gridCells = new GameObject[CC];
        refs.gridTexts = new GameObject[CC];
        refs.trackStripsA = new GameObject[CC];
        refs.trackStripsB = new GameObject[CC];

        float cellPad = 0f;
        float cellSize = 1f / GS;

        for (int row = 0; row < GS; row++)
        {
            for (int col = 0; col < GS; col++)
            {
                int idx = row * GS + col;
                float cx0 = cellPad + col * (cellSize + cellPad);
                float cy0 = 1f - cellPad - (row + 1) * (cellSize + cellPad) + cellPad;
                float cx1 = cx0 + cellSize;
                float cy1 = cy0 + cellSize;

                // Cell background
                GameObject cell = new GameObject(prefix + "Cell_" + idx, typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(gridContainer.transform, false);
                cell.GetComponent<Image>().color = new Color(0.25f, 0.6f, 0.25f, 1f); // default green (bush)
                SetRect(cell.GetComponent<RectTransform>(), new Vector2(cx0, cy0), new Vector2(cx1, cy1), Vector2.zero, Vector2.zero);
                refs.gridCells[idx] = cell;

                // Track strip A (entry direction) - child of cell
                GameObject stripA = new GameObject(prefix + "StripA_" + idx, typeof(RectTransform), typeof(Image));
                stripA.transform.SetParent(cell.transform, false);
                stripA.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.22f, 1f);
                SetRect(stripA.GetComponent<RectTransform>(), new Vector2(0.35f, 0f), new Vector2(0.65f, 0.5f), Vector2.zero, Vector2.zero);
                stripA.SetActive(false);
                refs.trackStripsA[idx] = stripA;

                // Track strip B (exit direction) - child of cell
                GameObject stripB = new GameObject(prefix + "StripB_" + idx, typeof(RectTransform), typeof(Image));
                stripB.transform.SetParent(cell.transform, false);
                stripB.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.22f, 1f);
                SetRect(stripB.GetComponent<RectTransform>(), new Vector2(0.35f, 0.5f), new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);
                stripB.SetActive(false);
                refs.trackStripsB[idx] = stripB;

                // Cell text (direction arrow or symbol)
                GameObject cellText = CreateText(prefix + "CellText_" + idx, cell.transform, "", 20, TextAnchor.MiddleCenter);
                SetRect(cellText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                cellText.GetComponent<Text>().color = Color.white;
                refs.gridTexts[idx] = cellText;
            }
        }

        // === End Markers (4 sides around grid) ===
        Color endMarkerCol = new Color(0.9f, 0.2f, 0.15f, 1f);

        refs.endMarkers = new GameObject[4];
        refs.endMarkerTexts = new GameObject[4];

        // Top
        refs.endMarkers[0] = CreateEndMarkerCell(prefix + "EndTop", gridArea.transform, endMarkerCol,
            new Vector2(0.38f, gridInnerMax + 0.005f), new Vector2(0.62f, 1f));
        refs.endMarkerTexts[0] = refs.endMarkers[0].transform.GetChild(0).gameObject;

        // Bottom
        refs.endMarkers[1] = CreateEndMarkerCell(prefix + "EndBottom", gridArea.transform, endMarkerCol,
            new Vector2(0.38f, 0f), new Vector2(0.62f, gridInnerMin - 0.005f));
        refs.endMarkerTexts[1] = refs.endMarkers[1].transform.GetChild(0).gameObject;

        // Left
        refs.endMarkers[2] = CreateEndMarkerCell(prefix + "EndLeft", gridArea.transform, endMarkerCol,
            new Vector2(0f, 0.38f), new Vector2(gridInnerMin - 0.005f, 0.62f));
        refs.endMarkerTexts[2] = refs.endMarkers[2].transform.GetChild(0).gameObject;

        // Right
        refs.endMarkers[3] = CreateEndMarkerCell(prefix + "EndRight", gridArea.transform, endMarkerCol,
            new Vector2(gridInnerMax + 0.005f, 0.38f), new Vector2(1f, 0.62f));
        refs.endMarkerTexts[3] = refs.endMarkers[3].transform.GetChild(0).gameObject;

        for (int i = 0; i < 4; i++)
            refs.endMarkers[i].SetActive(false);

        // === Train Icon (child of gridContainer so it rotates with the grid) ===
        GameObject trainIcon = new GameObject(prefix + "TrainIcon", typeof(RectTransform), typeof(Image));
        trainIcon.transform.SetParent(gridContainer.transform, false);
        trainIcon.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.85f, 0.95f);
        RectTransform trainRT = trainIcon.GetComponent<RectTransform>();
        trainRT.anchorMin = new Vector2(0.5f, 0.5f);
        trainRT.anchorMax = new Vector2(0.5f, 0.5f);
        trainRT.sizeDelta = new Vector2(48f, 48f);

        GameObject trainText = CreateText(prefix + "TrainText", trainIcon.transform, "T", 16, TextAnchor.MiddleCenter);
        SetRect(trainText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        trainText.GetComponent<Text>().color = Color.white;
        trainText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        trainIcon.SetActive(false);
        refs.trainIcon = trainIcon;

        // === Answer Options (visual tile style) ===
        GameObject prompt = CreateText(prefix + "Prompt", panel.transform, "What track is hidden?", 14, TextAnchor.MiddleCenter);
        SetRect(prompt.GetComponent<RectTransform>(), new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.29f), Vector2.zero, Vector2.zero);
        prompt.GetComponent<Text>().color = new Color(0.25f, 0.25f, 0.4f, 1f);

        // Answer buttons: simple sprite-only squares
        Sprite ansLeftSprite = LoadArtSprite("answer_left");
        Sprite ansStraightSprite = LoadArtSprite("answer_straight");
        Sprite ansRightSprite = LoadArtSprite("answer_right");

        refs.leftBtn = CreateSimpleOptionButton(prefix + "OptLeft", panel.transform,
            new Vector2(0.03f, 0.02f), new Vector2(0.33f, 0.21f), ansLeftSprite, out refs.leftTile);

        refs.straightBtn = CreateSimpleOptionButton(prefix + "OptStraight", panel.transform,
            new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.21f), ansStraightSprite, out refs.straightTile);

        refs.rightBtn = CreateSimpleOptionButton(prefix + "OptRight", panel.transform,
            new Vector2(0.67f, 0.02f), new Vector2(0.97f, 0.21f), ansRightSprite, out refs.rightTile);

        // Feedback icons (circle + ✓/✗)
        refs.correctIcon = FeedbackIconBuilder.Create(prefix + "CorrectIcon", panel.transform, true, new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.70f));
        refs.wrongIcon = FeedbackIconBuilder.Create(prefix + "WrongIcon", panel.transform, false, new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.70f));

        // Countdown text (Team mode)
        refs.countdownText = CreateText(prefix + "CountdownText", panel.transform, "3", 72, TextAnchor.MiddleCenter);
        Text cdText = refs.countdownText.GetComponent<Text>();
        cdText.color = new Color(1f, 1f, 1f, 0.9f);
        cdText.fontStyle = FontStyle.Bold;
        Outline cdOutline = refs.countdownText.AddComponent<Outline>();
        cdOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        cdOutline.effectDistance = new Vector2(2f, -2f);
        SetRect(refs.countdownText.GetComponent<RectTransform>(), new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero);
        refs.countdownText.SetActive(false);

        return refs;
    }

    /// <summary>
    /// Create a visual tile-style option button matching the reference game style
    /// Layout: [Border/Button] > [TilePreview (colored square)] + [Label text]
    /// </summary>
    private static GameObject CreateTileOptionButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Color tileColor, string symbol, string label,
        out GameObject tileObject)
    {
        // Outer button (acts as border for selection feedback)
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.8f, 1f); // neutral border
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Tile preview (colored square showing track type)
        GameObject tile = CreateImage(name + "Tile", btnGo.transform, tileColor);
        SetRect(tile.GetComponent<RectTransform>(), new Vector2(0.15f, 0.30f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        tileObject = tile;

        // Track strip preview inside tile (vertical brown strip to show track)
        GameObject tileStrip = CreateImage(name + "Strip", tile.transform, new Color(0.55f, 0.42f, 0.22f, 1f));
        // Position strip based on track type
        if (symbol == "|")
        {
            // Straight: vertical strip through center
            SetRect(tileStrip.GetComponent<RectTransform>(), new Vector2(0.35f, 0f), new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);
        }
        else if (symbol == "<")
        {
            // Turn left: vertical strip top + horizontal strip left
            SetRect(tileStrip.GetComponent<RectTransform>(), new Vector2(0.35f, 0.5f), new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);
            GameObject hStrip = CreateImage(name + "HStrip", tile.transform, new Color(0.55f, 0.42f, 0.22f, 1f));
            SetRect(hStrip.GetComponent<RectTransform>(), new Vector2(0f, 0.35f), new Vector2(0.5f, 0.65f), Vector2.zero, Vector2.zero);
        }
        else if (symbol == ">")
        {
            // Turn right: vertical strip top + horizontal strip right
            SetRect(tileStrip.GetComponent<RectTransform>(), new Vector2(0.35f, 0.5f), new Vector2(0.65f, 1f), Vector2.zero, Vector2.zero);
            GameObject hStrip = CreateImage(name + "HStrip", tile.transform, new Color(0.55f, 0.42f, 0.22f, 1f));
            SetRect(hStrip.GetComponent<RectTransform>(), new Vector2(0.5f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);
        }

        // Symbol overlay on tile
        GameObject symbolText = CreateText(name + "Symbol", tile.transform, symbol, 28, TextAnchor.MiddleCenter);
        SetRect(symbolText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        symbolText.GetComponent<Text>().color = new Color(1f, 1f, 1f, 0.6f);

        // Label below tile
        GameObject labelText = CreateText(name + "Label", btnGo.transform, label, 14, TextAnchor.MiddleCenter);
        SetRect(labelText.GetComponent<RectTransform>(), new Vector2(0f, 0.02f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);
        labelText.GetComponent<Text>().color = new Color(0.2f, 0.2f, 0.35f, 1f);
        labelText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        return btnGo;
    }

    /// <summary>
    /// Simple option button: just a square with sprite image, no text/strips/labels.
    /// </summary>
    private static GameObject CreateSimpleOptionButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, out GameObject tileObject)
    {
        // Button = sprite image itself (no visible border)
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        Image btnImg = btnGo.GetComponent<Image>();
        if (sprite != null) { btnImg.sprite = sprite; btnImg.preserveAspect = true; }
        btnImg.color = Color.white;
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Tile reference = same as button (for border color feedback)
        tileObject = btnGo;
        return btnGo;
    }

    private static GameObject CreateEndMarkerCell(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject marker = new GameObject(name, typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(parent, false);
        marker.GetComponent<Image>().color = color;
        SetRect(marker.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        GameObject markerText = CreateText(name + "Text", marker.transform, "END", 12, TextAnchor.MiddleCenter);
        SetRect(markerText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        markerText.GetComponent<Text>().color = Color.white;
        markerText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        return marker;
    }

    private struct PlayerPanelRefs
    {
        public GameObject nameText;
        public GameObject scoreText;
        public GameObject[] gridCells;
        public GameObject[] gridTexts;
        public GameObject[] trackStripsA; // 9 entry direction strips
        public GameObject[] trackStripsB; // 9 exit direction strips
        public GameObject leftBtn;
        public GameObject straightBtn;
        public GameObject rightBtn;
        public GameObject leftTile;      // tile preview inside option
        public GameObject straightTile;
        public GameObject rightTile;
        public GameObject correctIcon;
        public GameObject wrongIcon;
        public GameObject trainIcon;
        public GameObject gridArea;      // rotates during gameplay
        public GameObject[] endMarkers;
        public GameObject[] endMarkerTexts;
        public GameObject countdownText;
    }

    private static void AssignViewReferences(
        TrainPathGameView view,
        TrainPathGameController controller,
        GameObject timerText,
        GameObject backButton,
        PlayerPanelRefs p1,
        PlayerPanelRefs p2)
    {
        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("timerText").objectReferenceValue = timerText.GetComponent<Text>();
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();

        AssignPlayerRefs(viewSo, "p1", p1);
        AssignPlayerRefs(viewSo, "p2", p2);

        // Wire cell sprites
        viewSo.FindProperty("bushSprite").objectReferenceValue = LoadArtSprite("icon_tree");
        viewSo.FindProperty("hiddenSprite").objectReferenceValue = LoadArtSprite("bubble_question");
        viewSo.FindProperty("trainSprite").objectReferenceValue = LoadArtSprite("icon_bus");
        viewSo.FindProperty("endpointSprite").objectReferenceValue = LoadArtSprite("icon_school");
        viewSo.FindProperty("arrowSprite").objectReferenceValue = LoadArtSprite("icon_arrow");
        // Tile sprites
        viewSo.FindProperty("tileStraightH").objectReferenceValue = LoadArtSprite("tile_straight_h");
        viewSo.FindProperty("tileStraightV").objectReferenceValue = LoadArtSprite("tile_straight_v");
        viewSo.FindProperty("tileCornerTopLeft").objectReferenceValue = LoadArtSprite("tile_corner_top_left");
        viewSo.FindProperty("tileCornerTopRight").objectReferenceValue = LoadArtSprite("tile_corner_top_right");
        viewSo.FindProperty("tileCornerBottomRight").objectReferenceValue = LoadArtSprite("tile_corner_bottom_right");
        viewSo.FindProperty("tileCornerBottomLeft").objectReferenceValue = LoadArtSprite("tile_corner_bottom_left");
        viewSo.FindProperty("tileBush").objectReferenceValue = LoadArtSprite("tile_bush");

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("gameView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPlayerRefs(SerializedObject viewSo, string prefix, PlayerPanelRefs refs)
    {
        viewSo.FindProperty(prefix + "NameText").objectReferenceValue = refs.nameText.GetComponent<Text>();
        viewSo.FindProperty(prefix + "ScoreText").objectReferenceValue = refs.scoreText.GetComponent<Text>();

        // Grid cells (4x4 = 16)
        int cc = 16;
        SerializedProperty cells = viewSo.FindProperty(prefix + "GridCells");
        cells.arraySize = cc;
        for (int i = 0; i < cc; i++)
            cells.GetArrayElementAtIndex(i).objectReferenceValue = refs.gridCells[i].GetComponent<Image>();

        SerializedProperty texts = viewSo.FindProperty(prefix + "GridTexts");
        texts.arraySize = cc;
        for (int i = 0; i < cc; i++)
            texts.GetArrayElementAtIndex(i).objectReferenceValue = refs.gridTexts[i].GetComponent<Text>();

        // Track strips
        SerializedProperty stripsA = viewSo.FindProperty(prefix + "TrackStripsA");
        stripsA.arraySize = cc;
        for (int i = 0; i < cc; i++)
            stripsA.GetArrayElementAtIndex(i).objectReferenceValue = refs.trackStripsA[i].GetComponent<Image>();

        SerializedProperty stripsB = viewSo.FindProperty(prefix + "TrackStripsB");
        stripsB.arraySize = cc;
        for (int i = 0; i < cc; i++)
            stripsB.GetArrayElementAtIndex(i).objectReferenceValue = refs.trackStripsB[i].GetComponent<Image>();

        // Answer option buttons and borders
        viewSo.FindProperty(prefix + "OptionLeftBtn").objectReferenceValue = refs.leftBtn.GetComponent<Button>();
        viewSo.FindProperty(prefix + "OptionStraightBtn").objectReferenceValue = refs.straightBtn.GetComponent<Button>();
        viewSo.FindProperty(prefix + "OptionRightBtn").objectReferenceValue = refs.rightBtn.GetComponent<Button>();
        viewSo.FindProperty(prefix + "OptionLeftBorder").objectReferenceValue = refs.leftBtn.GetComponent<Image>();
        viewSo.FindProperty(prefix + "OptionStraightBorder").objectReferenceValue = refs.straightBtn.GetComponent<Image>();
        viewSo.FindProperty(prefix + "OptionRightBorder").objectReferenceValue = refs.rightBtn.GetComponent<Image>();

        // Option tile previews
        viewSo.FindProperty(prefix + "OptionLeftTile").objectReferenceValue = refs.leftTile.GetComponent<Image>();
        viewSo.FindProperty(prefix + "OptionStraightTile").objectReferenceValue = refs.straightTile.GetComponent<Image>();
        viewSo.FindProperty(prefix + "OptionRightTile").objectReferenceValue = refs.rightTile.GetComponent<Image>();

        // Feedback
        viewSo.FindProperty(prefix + "CorrectIcon").objectReferenceValue = refs.correctIcon;
        viewSo.FindProperty(prefix + "WrongIcon").objectReferenceValue = refs.wrongIcon;
        if (refs.countdownText != null)
            viewSo.FindProperty(prefix + "CountdownText").objectReferenceValue = refs.countdownText.GetComponent<Text>();

        // Train icon
        viewSo.FindProperty(prefix + "TrainIcon").objectReferenceValue = refs.trainIcon.GetComponent<Image>();

        // Grid area (for rotation)
        viewSo.FindProperty(prefix + "GridArea").objectReferenceValue = refs.gridArea.GetComponent<RectTransform>();

        // End markers
        SerializedProperty endMarkers = viewSo.FindProperty(prefix + "EndMarkers");
        endMarkers.arraySize = 4;
        for (int i = 0; i < 4; i++)
            endMarkers.GetArrayElementAtIndex(i).objectReferenceValue = refs.endMarkers[i].GetComponent<Image>();

        SerializedProperty endMarkerTexts = viewSo.FindProperty(prefix + "EndMarkerTexts");
        endMarkerTexts.arraySize = 4;
        for (int i = 0; i < 4; i++)
            endMarkerTexts.GetArrayElementAtIndex(i).objectReferenceValue = refs.endMarkerTexts[i].GetComponent<Text>();
    }

    // ===== Utility =====

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

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static GameObject CreateStyledButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor, int fontSize)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = bgColor;
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        GameObject textGo = CreateText("Text", btnGo.transform, label, fontSize, TextAnchor.MiddleCenter);
        SetRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textGo.GetComponent<Text>().color = textColor;
        return btnGo;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
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
