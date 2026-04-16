using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AddUpSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/AddUpGame";
    private const string ScenePath = "Assets/Game/Scenes/AddUpGame/AddUpGame.unity";
    private const string PrefabFolder = "Assets/Game/Prefabs/AddUpGame";
    private const string ResourceFolder = "Assets/Game/Resources/ui/addup";
    private const string ItemPrefabPath = "Assets/Game/Prefabs/AddUpGame/AddUpItemImage.prefab";
    private const string TrainPath = "Assets/Game/Resources/ui/addup/train.png";
    private const string CoinPath = "Assets/Game/Resources/ui/addup/coin.png";
    private const string ArtFolder = "Assets/Game/Textures/AddUpGame";

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

    [MenuItem("Tools/AddUp/Build Scene + Prefab + Placeholders")]
    public static void BuildAll()
    {
        EnsureFolders();
        CreatePlaceholderSprite(TrainPath, new Color(0.95f, 0.35f, 0.35f, 1f), true);
        CreatePlaceholderSprite(CoinPath, new Color(0.98f, 0.75f, 0.15f, 1f), false);
        GameObject itemPrefab = CreateItemPrefab();
        BuildScene(itemPrefab);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("AddUpSceneBuilder: finished generating scene, prefab, placeholder sprites.");
    }

    [MenuItem("Tools/AddUp/Add Camera To AddUp Scene")]
    public static void AddCameraToAddUpScene()
    {
        if (!File.Exists(ToAbsolutePath(ScenePath)))
        {
            Debug.LogWarning("AddUpSceneBuilder: AddUp scene does not exist yet. Run Build Scene first.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera existingMain = Camera.main;
        Camera[] allCameras = Object.FindObjectsOfType<Camera>();
        if (existingMain == null && (allCameras == null || allCameras.Length == 0))
        {
            CreateMainCamera();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AddUpSceneBuilder: added Main Camera to AddUp scene.");
            return;
        }

        if (existingMain == null && allCameras != null && allCameras.Length > 0)
        {
            allCameras[0].gameObject.tag = "MainCamera";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AddUpSceneBuilder: existing camera tagged as MainCamera and scene saved.");
            return;
        }

        Debug.Log("AddUpSceneBuilder: AddUp scene already has a Main Camera.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Scenes");
        EnsureFolder(SceneFolder);
        EnsureFolder("Assets/Game/Prefabs");
        EnsureFolder(PrefabFolder);
        EnsureFolder("Assets/Game/Resources");
        EnsureFolder("Assets/Game/Resources/ui");
        EnsureFolder(ResourceFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void CreatePlaceholderSprite(string assetPath, Color color, bool trainStyle)
    {
        if (File.Exists(ToAbsolutePath(assetPath)))
        {
            return;
        }

        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        if (trainStyle)
        {
            DrawRect(tex, 10, 18, 44, 26, color);
            DrawRect(tex, 18, 36, 16, 10, color * 0.8f);
            DrawCircle(tex, 20, 14, 7, Color.black);
            DrawCircle(tex, 44, 14, 7, Color.black);
        }
        else
        {
            DrawCircle(tex, 32, 32, 23, color);
            DrawCircle(tex, 32, 32, 10, color * 0.8f);
        }

        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(ToAbsolutePath(assetPath), png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void DrawRect(Texture2D tex, int x, int y, int width, int height, Color color)
    {
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                if (i >= 0 && i < tex.width && j >= 0 && j < tex.height)
                {
                    tex.SetPixel(i, j, color);
                }
            }
        }
    }

    private static void DrawCircle(Texture2D tex, int centerX, int centerY, int radius, Color color)
    {
        int rSquared = radius * radius;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy <= rSquared)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }
        }
    }

    private static GameObject CreateItemPrefab()
    {
        // Circle background with Mask — icon rendered as child
        GameObject prefabObj = new GameObject("AddUpItemImage", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform rt = prefabObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(32f, 32f);
        Image bgImage = prefabObj.GetComponent<Image>();
        bgImage.sprite = LoadArtSprite("dot_blue");
        bgImage.color = new Color(1f, 1f, 1f, 0.25f); // subtle circle bg
        bgImage.preserveAspect = true;
        Mask mask = prefabObj.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        // Child icon image (actual item sprite)
        GameObject iconObj = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(prefabObj.transform, false);
        RectTransform iconRT = iconObj.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(2f, 2f);
        iconRT.offsetMax = new Vector2(-2f, -2f);
        Image iconImage = iconObj.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TrainPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabObj, ItemPrefabPath);
        Object.DestroyImmediate(prefabObj);
        return prefab;
    }

    private static void BuildScene(GameObject itemPrefab)
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

        GameObject root = new GameObject("AddUpRoot");
        AddUpGameController controller = root.AddComponent<AddUpGameController>();
        AddUpGameView view = root.AddComponent<AddUpGameView>();

        // ===== Background sprite =====
        Sprite bgSprite = LoadArtSprite("bg_gameplay");
        GameObject bgPanel = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgPanel.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bgPanel.GetComponent<Image>();
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.96f, 0.87f, 0.60f, 1f);
        bgImg.raycastTarget = false;
        SetRect(bgPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ===== Team bars (top) =====
        Sprite teamBlueSprite = LoadArtSprite("team_bar_blue");
        Sprite teamRedSprite = LoadArtSprite("team_bar_red");

        GameObject teamBlueBar = new GameObject("TeamBlueBar", typeof(RectTransform), typeof(Image));
        teamBlueBar.transform.SetParent(canvasGo.transform, false);
        Image tbImg = teamBlueBar.GetComponent<Image>();
        if (teamBlueSprite != null) { tbImg.sprite = teamBlueSprite; tbImg.preserveAspect = true; }
        tbImg.color = Color.white;
        tbImg.raycastTarget = false;
        RectTransform tbRT = teamBlueBar.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0.01f, 0.88f);
        tbRT.anchorMax = new Vector2(0.35f, 0.99f);
        tbRT.anchoredPosition = new Vector2(125f, 2f);
        tbRT.sizeDelta = Vector2.zero;

        // Player 1 name on blue bar (editor-adjusted)
        GameObject p1BarName = CreateText("P1BarName", teamBlueBar.transform, "Player 1", 16, TextAnchor.MiddleCenter);
        SetRect(p1BarName.GetComponent<RectTransform>(), new Vector2(0.15f, 0.05f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);
        p1BarName.GetComponent<Text>().color = Color.white;
        p1BarName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        GameObject teamRedBar = new GameObject("TeamRedBar", typeof(RectTransform), typeof(Image));
        teamRedBar.transform.SetParent(canvasGo.transform, false);
        Image trImg = teamRedBar.GetComponent<Image>();
        if (teamRedSprite != null) { trImg.sprite = teamRedSprite; trImg.preserveAspect = true; }
        trImg.color = Color.white;
        trImg.raycastTarget = false;
        RectTransform trRT = teamRedBar.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0.65f, 0.88f);
        trRT.anchorMax = new Vector2(0.99f, 0.99f);
        trRT.anchoredPosition = new Vector2(-124f, 1f);
        trRT.sizeDelta = Vector2.zero;

        // Player 2 name on red bar (editor-adjusted)
        GameObject p2BarName = CreateText("P2BarName", teamRedBar.transform, "Player 2", 16, TextAnchor.MiddleCenter);
        SetRect(p2BarName.GetComponent<RectTransform>(), new Vector2(0.10f, 0.05f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        p2BarName.GetComponent<Text>().color = Color.white;
        p2BarName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // ===== Timer with circle bg =====
        Sprite timerCircleSprite = LoadArtSprite("timer_circle");
        GameObject timerBg = new GameObject("TimerBg", typeof(RectTransform), typeof(Image));
        timerBg.transform.SetParent(canvasGo.transform, false);
        Image tcImg = timerBg.GetComponent<Image>();
        if (timerCircleSprite != null) { tcImg.sprite = timerCircleSprite; tcImg.preserveAspect = true; }
        tcImg.color = Color.white;
        tcImg.raycastTarget = false;
        RectTransform timerBgRT = timerBg.GetComponent<RectTransform>();
        timerBgRT.anchorMin = new Vector2(0.5f, 1f);
        timerBgRT.anchorMax = new Vector2(0.5f, 1f);
        timerBgRT.pivot = new Vector2(0.5f, 0.5f);
        timerBgRT.anchoredPosition = new Vector2(0f, -35f);
        timerBgRT.sizeDelta = new Vector2(60f, 60f);

        GameObject timerTextGo = CreateText("TimerText", timerBg.transform, "100", 22, TextAnchor.MiddleCenter);
        SetRect(timerTextGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        timerTextGo.GetComponent<Text>().color = Color.white;
        timerTextGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Question text removed per design request
        GameObject questionTextGo = null;

        // Score bars (vertical, left and right edges)
        Sprite barBlueSprite = LoadArtSprite("bar_blue");
        Sprite barRedSprite = LoadArtSprite("bar_red");
        Sprite barWhiteSprite = LoadArtSprite("bar_white");

        // Left score bar (blue fill over white track)
        GameObject leftBarTrack = new GameObject("LeftScoreBar", typeof(RectTransform), typeof(Image));
        leftBarTrack.transform.SetParent(canvasGo.transform, false);
        Image lbTrackImg = leftBarTrack.GetComponent<Image>();
        if (barWhiteSprite != null) { lbTrackImg.sprite = barWhiteSprite; }
        lbTrackImg.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
        lbTrackImg.raycastTarget = false;
        RectTransform lbRT = leftBarTrack.GetComponent<RectTransform>();
        lbRT.anchorMin = new Vector2(0.005f, 0.05f);
        lbRT.anchorMax = new Vector2(0.03f, 0.86f);
        lbRT.anchoredPosition = new Vector2(486.5f, -26.8f);
        lbRT.sizeDelta = new Vector2(-8f, -76.1f);

        GameObject leftBarFill = new GameObject("LeftScoreBarFill", typeof(RectTransform), typeof(Image));
        leftBarFill.transform.SetParent(leftBarTrack.transform, false);
        Image lbFillImg = leftBarFill.GetComponent<Image>();
        if (barBlueSprite != null) { lbFillImg.sprite = barBlueSprite; }
        lbFillImg.color = Color.white;
        lbFillImg.type = Image.Type.Filled;
        lbFillImg.fillMethod = Image.FillMethod.Vertical;
        lbFillImg.fillOrigin = 0; // bottom
        lbFillImg.fillAmount = 0f;
        lbFillImg.raycastTarget = false;
        SetRect(leftBarFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Right score bar (red fill over white track)
        GameObject rightBarTrack = new GameObject("RightScoreBar", typeof(RectTransform), typeof(Image));
        rightBarTrack.transform.SetParent(canvasGo.transform, false);
        Image rbTrackImg = rightBarTrack.GetComponent<Image>();
        if (barWhiteSprite != null) { rbTrackImg.sprite = barWhiteSprite; }
        rbTrackImg.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
        rbTrackImg.raycastTarget = false;
        RectTransform rbRT = rightBarTrack.GetComponent<RectTransform>();
        rbRT.anchorMin = new Vector2(0.97f, 0.05f);
        rbRT.anchorMax = new Vector2(0.995f, 0.86f);
        rbRT.anchoredPosition = new Vector2(-477.3f, -26.8f);
        rbRT.sizeDelta = new Vector2(-8.7f, -76.1f);

        GameObject rightBarFill = new GameObject("RightScoreBarFill", typeof(RectTransform), typeof(Image));
        rightBarFill.transform.SetParent(rightBarTrack.transform, false);
        Image rbFillImg = rightBarFill.GetComponent<Image>();
        if (barRedSprite != null) { rbFillImg.sprite = barRedSprite; }
        rbFillImg.color = Color.white;
        rbFillImg.type = Image.Type.Filled;
        rbFillImg.fillMethod = Image.FillMethod.Vertical;
        rbFillImg.fillOrigin = 0; // bottom
        rbFillImg.fillAmount = 0f;
        rbFillImg.raycastTarget = false;
        SetRect(rightBarFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Star icon + score label (editor-adjusted)
        Sprite starSprite = LoadArtSprite("icon_star");
        GameObject leftStarIcon = new GameObject("LeftStarIcon", typeof(RectTransform), typeof(Image));
        leftStarIcon.transform.SetParent(canvasGo.transform, false);
        Image lsImg = leftStarIcon.GetComponent<Image>();
        if (starSprite != null) { lsImg.sprite = starSprite; lsImg.preserveAspect = true; }
        lsImg.color = Color.white; lsImg.raycastTarget = false;
        RectTransform lsRT = leftStarIcon.GetComponent<RectTransform>();
        lsRT.anchorMin = new Vector2(0f, 0.86f); lsRT.anchorMax = new Vector2(0f, 0.86f);
        lsRT.pivot = new Vector2(0.5f, 0f);
        lsRT.anchoredPosition = new Vector2(185f, -65f); lsRT.sizeDelta = new Vector2(72f, 72f);

        GameObject leftScoreLabel = CreateText("LeftScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter);
        RectTransform lslRT = leftScoreLabel.GetComponent<RectTransform>();
        lslRT.anchorMin = new Vector2(0f, 0.86f); lslRT.anchorMax = new Vector2(0f, 0.86f);
        lslRT.pivot = new Vector2(0.5f, 0f);
        lslRT.anchoredPosition = new Vector2(291f, -65f); lslRT.sizeDelta = new Vector2(120f, 60f);
        leftScoreLabel.GetComponent<Text>().color = new Color(0.3f, 0.5f, 0.9f, 1f);
        leftScoreLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftScoreLabel.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        GameObject rightStarIcon = new GameObject("RightStarIcon", typeof(RectTransform), typeof(Image));
        rightStarIcon.transform.SetParent(canvasGo.transform, false);
        Image rsImg = rightStarIcon.GetComponent<Image>();
        if (starSprite != null) { rsImg.sprite = starSprite; rsImg.preserveAspect = true; }
        rsImg.color = Color.white; rsImg.raycastTarget = false;
        RectTransform rsRT = rightStarIcon.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(1f, 0.86f); rsRT.anchorMax = new Vector2(1f, 0.86f);
        rsRT.pivot = new Vector2(0.5f, 0f);
        rsRT.anchoredPosition = new Vector2(-185f, -65f); rsRT.sizeDelta = new Vector2(72f, 72f);

        GameObject rightScoreLabel = CreateText("RightScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter);
        RectTransform rslRT = rightScoreLabel.GetComponent<RectTransform>();
        rslRT.anchorMin = new Vector2(1f, 0.86f); rslRT.anchorMax = new Vector2(1f, 0.86f);
        rslRT.pivot = new Vector2(0.5f, 0f);
        rslRT.anchoredPosition = new Vector2(-291f, -65f); rslRT.sizeDelta = new Vector2(120f, 60f);
        rightScoreLabel.GetComponent<Text>().color = new Color(0.9f, 0.3f, 0.3f, 1f);
        rightScoreLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightScoreLabel.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        // Divider (center, editor-adjusted)
        GameObject divider = CreateImage("Divider", canvasGo.transform, new Color(0.35f, 0.6f, 0.9f, 0.6f));
        RectTransform divRT = divider.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.5f, 0.05f);
        divRT.anchorMax = new Vector2(0.5f, 0.82f);
        divRT.anchoredPosition = new Vector2(2f, 0f);
        divRT.sizeDelta = new Vector2(4f, 0f);

        // Navigation buttons (top right: home, setting)
        Sprite backSprite = LoadArtSprite("btn_back");
        Sprite homeSprite = LoadArtSprite("btn_home");
        Sprite settingSprite = LoadArtSprite("btn_setting");

        // Back button (top left, beside blue team bar)
        GameObject backButton = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backButton.transform.SetParent(canvasGo.transform, false);
        Image backImg = backButton.GetComponent<Image>();
        if (backSprite != null) { backImg.sprite = backSprite; backImg.preserveAspect = true; }
        backImg.color = Color.white;
        RectTransform backRT = backButton.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0f, 1f);
        backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(8f, -5f);
        backRT.sizeDelta = new Vector2(40f, 40f);

        // Home button (top right)
        GameObject homeButton = new GameObject("HomeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        homeButton.transform.SetParent(canvasGo.transform, false);
        Image homeImg = homeButton.GetComponent<Image>();
        if (homeSprite != null) { homeImg.sprite = homeSprite; homeImg.preserveAspect = true; }
        homeImg.color = Color.white;
        RectTransform homeRT = homeButton.GetComponent<RectTransform>();
        homeRT.anchorMin = new Vector2(1f, 1f);
        homeRT.anchorMax = new Vector2(1f, 1f);
        homeRT.pivot = new Vector2(1f, 1f);
        homeRT.anchoredPosition = new Vector2(-8f, -5f);
        homeRT.sizeDelta = new Vector2(40f, 40f);

        // Setting button (top right, left of home)
        GameObject settingButton = new GameObject("SettingButton", typeof(RectTransform), typeof(Image), typeof(Button));
        settingButton.transform.SetParent(canvasGo.transform, false);
        Image settingImg = settingButton.GetComponent<Image>();
        if (settingSprite != null) { settingImg.sprite = settingSprite; settingImg.preserveAspect = true; }
        settingImg.color = Color.white;
        RectTransform settingRT = settingButton.GetComponent<RectTransform>();
        settingRT.anchorMin = new Vector2(1f, 1f);
        settingRT.anchorMax = new Vector2(1f, 1f);
        settingRT.pivot = new Vector2(1f, 1f);
        settingRT.anchoredPosition = new Vector2(-52f, -5f);
        settingRT.sizeDelta = new Vector2(40f, 40f);

        // Player panels
        GameObject p1Panel = CreatePlayerPanel("Player1Panel", canvasGo.transform, true);
        GameObject p2Panel = CreatePlayerPanel("Player2Panel", canvasGo.transform, false);

        // Game over panel
        GameObject gameOverPanel = CreateGameOverPanel(canvasGo.transform);
        gameOverPanel.SetActive(false);

        // Tutorial panel (static image)
        Sprite tutorialSprite = LoadArtSprite("tutorial_bg");
        GameObject tutorialPanel = CreateTutorialPanel(canvasGo.transform, tutorialSprite);

        AssignViewReferences(view, controller, itemPrefab, questionTextGo, timerTextGo, gameOverPanel, p1Panel, p2Panel, backButton, homeButton, settingButton, leftBarFill, rightBarFill);

        // Wire bar name texts + score labels
        SerializedObject viewSo2 = new SerializedObject(view);
        viewSo2.FindProperty("p1BarNameText").objectReferenceValue = p1BarName.GetComponent<Text>();
        viewSo2.FindProperty("p2BarNameText").objectReferenceValue = p2BarName.GetComponent<Text>();
        // Override score texts to point to bar labels
        viewSo2.FindProperty("p1ScoreText").objectReferenceValue = leftScoreLabel.GetComponent<Text>();
        viewSo2.FindProperty("p2ScoreText").objectReferenceValue = rightScoreLabel.GetComponent<Text>();
        viewSo2.ApplyModifiedPropertiesWithoutUndo();

        // Wire tutorial panel to controller
        SerializedObject ctrlSo2 = new SerializedObject(controller);
        ctrlSo2.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel.GetComponent<TutorialPanel>();
        ctrlSo2.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
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
        cameraGo.transform.rotation = Quaternion.identity;
    }

    private static GameObject CreatePlayerPanel(string name, Transform parent, bool isLeft)
    {
        GameObject panel = CreateImage(name, parent, new Color(0f, 0f, 0f, 0f));
        panel.GetComponent<Image>().raycastTarget = false;
        RectTransform prt = panel.GetComponent<RectTransform>();
        if (isLeft)
        {
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0.88f);
            prt.anchoredPosition = new Vector2(-13.85f, 4.70f);
            prt.sizeDelta = new Vector2(-59.70f, -49.39f);
        }
        else
        {
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(1f, 0.88f);
            prt.anchoredPosition = new Vector2(16.12f, 4.70f);
            prt.sizeDelta = new Vector2(-64.23f, -49.39f);
        }

        // === Equation Row: [BoxA] + [BoxB] = [BoxC] ===
        // Box A (left operand)
        GameObject boxA = CreateEquationBox("BoxA", panel.transform, new Vector2(0.02f, 0.55f), new Vector2(0.28f, 0.88f));
        // Plus label
        GameObject plusLabel = CreateText("PlusLabel", panel.transform, "+", 36, TextAnchor.MiddleCenter);
        SetRect(plusLabel.GetComponent<RectTransform>(), new Vector2(0.28f, 0.62f), new Vector2(0.38f, 0.82f), Vector2.zero, Vector2.zero);
        plusLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        // Box B (right operand)
        GameObject boxB = CreateEquationBox("BoxB", panel.transform, new Vector2(0.38f, 0.55f), new Vector2(0.64f, 0.88f));
        // Equals label
        GameObject equalsLabel = CreateText("EqualsLabel", panel.transform, "=", 36, TextAnchor.MiddleCenter);
        SetRect(equalsLabel.GetComponent<RectTransform>(), new Vector2(0.64f, 0.62f), new Vector2(0.74f, 0.82f), Vector2.zero, Vector2.zero);
        equalsLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        // Box C (sum)
        GameObject boxC = CreateEquationBox("BoxC", panel.transform, new Vector2(0.74f, 0.55f), new Vector2(0.98f, 0.88f));

        // Hidden overlay (yellow rectangle with question mark sprite)
        Sprite yellowSprite = LoadArtSprite("card_yellow");
        Sprite questionSprite = LoadArtSprite("icon_question");

        GameObject hiddenOverlay = new GameObject("HiddenOverlay", typeof(RectTransform), typeof(Image));
        hiddenOverlay.transform.SetParent(panel.transform, false);
        Image hoImg = hiddenOverlay.GetComponent<Image>();
        if (yellowSprite != null) { hoImg.sprite = yellowSprite; hoImg.preserveAspect = false; }
        else hoImg.color = new Color(0.95f, 0.9f, 0.4f, 0.9f);
        hoImg.color = Color.white;
        SetRect(hiddenOverlay.GetComponent<RectTransform>(), new Vector2(0.02f, 0.55f), new Vector2(0.28f, 0.88f), Vector2.zero, Vector2.zero);

        // Question mark icon (centered in overlay)
        GameObject qmIcon = new GameObject("QuestionMark", typeof(RectTransform), typeof(Image));
        qmIcon.transform.SetParent(hiddenOverlay.transform, false);
        Image qmImg = qmIcon.GetComponent<Image>();
        if (questionSprite != null) { qmImg.sprite = questionSprite; qmImg.preserveAspect = true; }
        qmImg.color = Color.white;
        qmImg.raycastTarget = false;
        SetRect(qmIcon.GetComponent<RectTransform>(), new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero);

        // Second hidden overlay for Level 4 (? + ? = target)
        GameObject hiddenOverlayB = new GameObject("HiddenOverlayB", typeof(RectTransform), typeof(Image));
        hiddenOverlayB.transform.SetParent(panel.transform, false);
        Image hoBImg = hiddenOverlayB.GetComponent<Image>();
        if (yellowSprite != null) { hoBImg.sprite = yellowSprite; hoBImg.preserveAspect = false; }
        else hoBImg.color = new Color(0.95f, 0.9f, 0.4f, 0.9f);
        hoBImg.color = Color.white;
        SetRect(hiddenOverlayB.GetComponent<RectTransform>(), new Vector2(0.37f, 0.55f), new Vector2(0.63f, 0.88f), Vector2.zero, Vector2.zero);

        GameObject qmIconB = new GameObject("QuestionMark", typeof(RectTransform), typeof(Image));
        qmIconB.transform.SetParent(hiddenOverlayB.transform, false);
        Image qmBImg = qmIconB.GetComponent<Image>();
        if (questionSprite != null) { qmBImg.sprite = questionSprite; qmBImg.preserveAspect = true; }
        qmBImg.color = Color.white;
        qmBImg.raycastTarget = false;
        SetRect(qmIconB.GetComponent<RectTransform>(), new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero);
        hiddenOverlayB.SetActive(false);

        // === 3 Answer buttons in a row (editor-adjusted) ===
        GameObject ans1 = CreateAnswerButton("Answer1Button", panel.transform, new Vector2(0.02f, 0.28f), new Vector2(0.32f, 0.50f));
        RectTransform a1rt = ans1.GetComponent<RectTransform>();
        a1rt.anchoredPosition = new Vector2(isLeft ? 0f : -0.18f, isLeft ? -67.82f : -70.43f);
        a1rt.sizeDelta = new Vector2(isLeft ? 0f : -0.35f, isLeft ? 103.55f : 99.34f);

        GameObject ans2 = CreateAnswerButton("Answer2Button", panel.transform, new Vector2(0.35f, 0.28f), new Vector2(0.65f, 0.50f));
        RectTransform a2rt = ans2.GetComponent<RectTransform>();
        a2rt.anchoredPosition = new Vector2(isLeft ? 0f : -0.57f, isLeft ? -67.82f : -70.43f);
        a2rt.sizeDelta = new Vector2(isLeft ? 0f : -0.35f, isLeft ? 103.55f : 99.34f);

        GameObject ans3 = CreateAnswerButton("Answer3Button", panel.transform, new Vector2(0.68f, 0.28f), new Vector2(0.98f, 0.50f));
        RectTransform a3rt = ans3.GetComponent<RectTransform>();
        a3rt.anchoredPosition = new Vector2(isLeft ? 2f : 0f, isLeft ? -68.17f : -71f);
        a3rt.sizeDelta = new Vector2(isLeft ? -0.35f : 0f, isLeft ? 98.15f : 104.77f);

        // NameText placeholder (hidden, actual name shown on team bar)
        GameObject nameText = CreateText("NameText", panel.transform, "", 1, TextAnchor.MiddleCenter);
        SetRect(nameText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = Color.clear;

        // ScoreText placeholder (actual display is on bar labels above process bars)
        GameObject scoreText = CreateText("ScoreText", panel.transform, "", 1, TextAnchor.MiddleCenter);
        SetRect(scoreText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        scoreText.GetComponent<Text>().color = Color.clear;

        // Feedback icons (circle + ✓/✗)
        GameObject okIcon = FeedbackIconBuilder.Create("CorrectIcon", panel.transform, true, new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f));
        GameObject wrongIcon = FeedbackIconBuilder.Create("WrongIcon", panel.transform, false, new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f));

        // Countdown text (Team mode — large number centered on player panel)
        GameObject countdownText = CreateText("CountdownText", panel.transform, "3", 72, TextAnchor.MiddleCenter);
        Text cdText = countdownText.GetComponent<Text>();
        cdText.color = new Color(1f, 1f, 1f, 0.9f);
        cdText.fontStyle = FontStyle.Bold;
        Outline cdOutline = countdownText.AddComponent<Outline>();
        cdOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        cdOutline.effectDistance = new Vector2(2f, -2f);
        SetRect(countdownText.GetComponent<RectTransform>(), new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero);
        countdownText.SetActive(false);

        return panel;
    }

    /// <summary>
    /// Create an equation box (container with border and grid layout for item images)
    /// </summary>
    private static GameObject CreateEquationBox(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        Sprite greenSprite = LoadArtSprite("box_green");
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        Image boxImg = box.GetComponent<Image>();
        if (greenSprite != null) { boxImg.sprite = greenSprite; boxImg.preserveAspect = false; }
        boxImg.color = Color.white;
        SetRect(box.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Image container with grid layout inside the box
        GameObject container = new GameObject("ImageContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        container.transform.SetParent(box.transform, false);
        SetRect(container.GetComponent<RectTransform>(), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(28f, 28f);
        grid.spacing = new Vector2(2f, 2f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        return box;
    }

    /// <summary>
    /// Create an answer button with green rectangle background
    /// </summary>
    private static GameObject CreateAnswerButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        Sprite greenSprite = LoadArtSprite("box_green");
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        if (greenSprite != null) { bg.sprite = greenSprite; bg.preserveAspect = false; }
        bg.color = Color.white;
        SetRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Image container with grid layout inside the button
        GameObject container = new GameObject("ImageContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        container.transform.SetParent(go.transform, false);
        SetRect(container.GetComponent<RectTransform>(), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(26f, 26f);
        grid.spacing = new Vector2(2f, 2f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        return go;
    }

    private static GameObject CreateTutorialPanel(Transform parent, Sprite tutorialSprite)
    {
        // Container (always active, holds TutorialPanel component)
        GameObject container = new GameObject("TutorialContainer", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        SetRect(container.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Panel root (overlay, toggled on/off)
        GameObject panel = CreateImage("TutorialPanel", container.transform, new Color(0f, 0f, 0f, 0.7f));
        SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Tutorial image
        if (tutorialSprite != null)
        {
            GameObject tutImg = new GameObject("TutorialImage", typeof(RectTransform), typeof(Image));
            tutImg.transform.SetParent(panel.transform, false);
            Image img = tutImg.GetComponent<Image>();
            img.sprite = tutorialSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            SetRect(tutImg.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), Vector2.zero, Vector2.zero);
        }

        // Start button (positioned over the tutorial image's button area)
        Sprite startBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/MenuScene/btn_start.png");
        GameObject startBtn = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        startBtn.transform.SetParent(panel.transform, false);
        Image sbImg = startBtn.GetComponent<Image>();
        if (startBtnSprite != null) { sbImg.sprite = startBtnSprite; sbImg.preserveAspect = true; }
        sbImg.color = Color.white;
        SetRect(startBtn.GetComponent<RectTransform>(), new Vector2(0.35f, 0.14f), new Vector2(0.65f, 0.26f), Vector2.zero, Vector2.zero);

        // TutorialPanel component on container (always active)
        TutorialPanel tp = container.AddComponent<TutorialPanel>();
        SerializedObject tpSo = new SerializedObject(tp);
        tpSo.FindProperty("panelRoot").objectReferenceValue = panel; // panel root = child overlay
        tpSo.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        tpSo.ApplyModifiedPropertiesWithoutUndo();

        // Panel starts hidden (Awake will also hide it)
        panel.SetActive(false);
        return container;
    }

    private static GameObject CreateGameOverPanel(Transform parent)
    {
        GameObject panel = CreateImage("GameOverPanel", parent, new Color(0f, 0f, 0f, 0.75f));
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero);

        GameObject result = CreateText("ResultText", panel.transform, "Player 1 Wins!\n0 - 0", 40, TextAnchor.MiddleCenter);
        SetRect(result.GetComponent<RectTransform>(), new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
        result.GetComponent<Text>().color = Color.white;

        CreateSimpleButton("RetryButton", panel.transform, "Retry", new Vector2(0.16f, 0.12f), new Vector2(0.44f, 0.3f));
        CreateSimpleButton("BackButton", panel.transform, "Back", new Vector2(0.56f, 0.12f), new Vector2(0.84f, 0.3f));
        return panel;
    }

    private static GameObject CreateSimpleButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        GameObject textGo = CreateText("Text", btnGo.transform, label, 28, TextAnchor.MiddleCenter);
        SetRect(textGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        return btnGo;
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

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void AssignViewReferences(
        AddUpGameView view,
        AddUpGameController controller,
        GameObject itemPrefab,
        GameObject questionText,
        GameObject timerText,
        GameObject gameOverPanel,
        GameObject p1Panel,
        GameObject p2Panel,
        GameObject backButton,
        GameObject homeButton,
        GameObject settingButton,
        GameObject p1ScoreBarFill,
        GameObject p2ScoreBarFill)
    {
        SerializedObject viewSo = new SerializedObject(view);
        // questionText removed — leave unassigned (View.SetQuestionText is a no-op when null)
        viewSo.FindProperty("timerText").objectReferenceValue = timerText.GetComponent<Text>();
        viewSo.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        viewSo.FindProperty("gameOverResultText").objectReferenceValue = gameOverPanel.transform.Find("ResultText").GetComponent<Text>();
        viewSo.FindProperty("retryButton").objectReferenceValue = gameOverPanel.transform.Find("RetryButton").GetComponent<Button>();
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        viewSo.FindProperty("homeButton").objectReferenceValue = homeButton.GetComponent<Button>();
        viewSo.FindProperty("settingButton").objectReferenceValue = settingButton.GetComponent<Button>();

        // Score bar fills
        viewSo.FindProperty("p1ScoreBarFill").objectReferenceValue = p1ScoreBarFill.GetComponent<Image>();
        viewSo.FindProperty("p2ScoreBarFill").objectReferenceValue = p2ScoreBarFill.GetComponent<Image>();

        // Player 1 references
        AssignPlayerReferences(viewSo, "p1", p1Panel);
        // Player 2 references
        AssignPlayerReferences(viewSo, "p2", p2Panel);

        viewSo.FindProperty("imagePrefab").objectReferenceValue = itemPrefab;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("gameView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPlayerReferences(SerializedObject viewSo, string prefix, GameObject panel)
    {
        viewSo.FindProperty(prefix + "NameText").objectReferenceValue = panel.transform.Find("NameText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "ScoreText").objectReferenceValue = panel.transform.Find("ScoreText").GetComponent<Text>();

        // Equation boxes
        viewSo.FindProperty(prefix + "BoxAImageContainer").objectReferenceValue = panel.transform.Find("BoxA/ImageContainer");
        viewSo.FindProperty(prefix + "BoxBImageContainer").objectReferenceValue = panel.transform.Find("BoxB/ImageContainer");
        viewSo.FindProperty(prefix + "BoxCImageContainer").objectReferenceValue = panel.transform.Find("BoxC/ImageContainer");
        viewSo.FindProperty(prefix + "BoxABorder").objectReferenceValue = panel.transform.Find("BoxA").GetComponent<Image>();
        viewSo.FindProperty(prefix + "BoxBBorder").objectReferenceValue = panel.transform.Find("BoxB").GetComponent<Image>();
        viewSo.FindProperty(prefix + "BoxCBorder").objectReferenceValue = panel.transform.Find("BoxC").GetComponent<Image>();

        // Labels
        viewSo.FindProperty(prefix + "PlusLabel").objectReferenceValue = panel.transform.Find("PlusLabel").GetComponent<Text>();
        viewSo.FindProperty(prefix + "EqualsLabel").objectReferenceValue = panel.transform.Find("EqualsLabel").GetComponent<Text>();

        // Hidden overlays
        viewSo.FindProperty(prefix + "HiddenOverlay").objectReferenceValue = panel.transform.Find("HiddenOverlay").gameObject;
        viewSo.FindProperty(prefix + "HiddenOverlayB").objectReferenceValue = panel.transform.Find("HiddenOverlayB").gameObject;

        // Answer buttons
        viewSo.FindProperty(prefix + "Answer1Button").objectReferenceValue = panel.transform.Find("Answer1Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer2Button").objectReferenceValue = panel.transform.Find("Answer2Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer3Button").objectReferenceValue = panel.transform.Find("Answer3Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer1ImageContainer").objectReferenceValue = panel.transform.Find("Answer1Button/ImageContainer");
        viewSo.FindProperty(prefix + "Answer2ImageContainer").objectReferenceValue = panel.transform.Find("Answer2Button/ImageContainer");
        viewSo.FindProperty(prefix + "Answer3ImageContainer").objectReferenceValue = panel.transform.Find("Answer3Button/ImageContainer");
        viewSo.FindProperty(prefix + "Answer1Border").objectReferenceValue = panel.transform.Find("Answer1Button").GetComponent<Image>();
        viewSo.FindProperty(prefix + "Answer2Border").objectReferenceValue = panel.transform.Find("Answer2Button").GetComponent<Image>();
        viewSo.FindProperty(prefix + "Answer3Border").objectReferenceValue = panel.transform.Find("Answer3Button").GetComponent<Image>();

        // Feedback icons
        viewSo.FindProperty(prefix + "CorrectIcon").objectReferenceValue = panel.transform.Find("CorrectIcon").gameObject;
        viewSo.FindProperty(prefix + "WrongIcon").objectReferenceValue = panel.transform.Find("WrongIcon").gameObject;

        // Countdown text
        viewSo.FindProperty(prefix + "CountdownText").objectReferenceValue = panel.transform.Find("CountdownText").GetComponent<Text>();
    }

    private static void AddSceneToBuildSettings(string targetPath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == targetPath)
            {
                return;
            }
        }

        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++)
        {
            newScenes[i] = scenes[i];
        }
        newScenes[scenes.Length] = new EditorBuildSettingsScene(targetPath, true);
        EditorBuildSettings.scenes = newScenes;
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        return projectPath + "/" + assetPath;
    }
}
