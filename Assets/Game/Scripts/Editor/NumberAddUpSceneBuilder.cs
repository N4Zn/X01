using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene builder for NumberAddUp — identical layout to AddUp but displays numbers as Text.
/// </summary>
public static class NumberAddUpSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/NumberAddUpGame";
    private const string ScenePath = "Assets/Game/Scenes/NumberAddUpGame/NumberAddUpGame.unity";
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

    [MenuItem("Tools/NumberAddUp/Build Scene")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("NumberAddUpSceneBuilder: Cannot build in Play mode."); return; }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("NumberAddUpSceneBuilder: finished.");
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

        GameObject root = new GameObject("NumberAddUpRoot");
        NumberAddUpGameController controller = root.AddComponent<NumberAddUpGameController>();
        NumberAddUpGameView view = root.AddComponent<NumberAddUpGameView>();

        // ===== Background =====
        Sprite bgSprite = LoadArtSprite("bg_gameplay");
        GameObject bgPanel = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgPanel.transform.SetParent(canvasGo.transform, false);
        Image bgImg = bgPanel.GetComponent<Image>();
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.96f, 0.87f, 0.60f, 1f);
        bgImg.raycastTarget = false;
        SetRect(bgPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ===== Team bars =====
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

        GameObject p1BarName = CreateText("P1BarName", teamBlueBar.transform, "Player 1", 16, TextAnchor.MiddleCenter);
        SetRect(p1BarName.GetComponent<RectTransform>(), new Vector2(0.15f, 0.05f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);
        p1BarName.GetComponent<Text>().color = Color.white; p1BarName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        GameObject teamRedBar = new GameObject("TeamRedBar", typeof(RectTransform), typeof(Image));
        teamRedBar.transform.SetParent(canvasGo.transform, false);
        Image trImg = teamRedBar.GetComponent<Image>();
        if (teamRedSprite != null) { trImg.sprite = teamRedSprite; trImg.preserveAspect = true; }
        trImg.color = Color.white; trImg.raycastTarget = false;
        RectTransform trRT = teamRedBar.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0.65f, 0.88f); trRT.anchorMax = new Vector2(0.99f, 0.99f);
        trRT.anchoredPosition = new Vector2(-124f, 1f); trRT.sizeDelta = Vector2.zero;

        GameObject p2BarName = CreateText("P2BarName", teamRedBar.transform, "Player 2", 16, TextAnchor.MiddleCenter);
        SetRect(p2BarName.GetComponent<RectTransform>(), new Vector2(0.10f, 0.05f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        p2BarName.GetComponent<Text>().color = Color.white; p2BarName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // ===== Timer =====
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

        GameObject timerTextGo = CreateText("TimerText", timerBg.transform, "100", 22, TextAnchor.MiddleCenter);
        SetRect(timerTextGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        timerTextGo.GetComponent<Text>().color = Color.white;
        timerTextGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Question text removed per design request

        // ===== Score bars =====
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
        lbFillImg.fillMethod = Image.FillMethod.Vertical; lbFillImg.fillOrigin = 0; lbFillImg.fillAmount = 0f; lbFillImg.raycastTarget = false;
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
        rbFillImg.fillMethod = Image.FillMethod.Vertical; rbFillImg.fillOrigin = 0; rbFillImg.fillAmount = 0f; rbFillImg.raycastTarget = false;
        SetRect(rightBarFill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Star icons + score labels above bars
        Sprite starSprite = LoadArtSprite("icon_star");
        GameObject leftStarIcon = new GameObject("LeftStarIcon", typeof(RectTransform), typeof(Image));
        leftStarIcon.transform.SetParent(canvasGo.transform, false);
        if (starSprite != null) { leftStarIcon.GetComponent<Image>().sprite = starSprite; leftStarIcon.GetComponent<Image>().preserveAspect = true; }
        leftStarIcon.GetComponent<Image>().color = Color.white; leftStarIcon.GetComponent<Image>().raycastTarget = false;
        RectTransform lsRT = leftStarIcon.GetComponent<RectTransform>();
        lsRT.anchorMin = new Vector2(0f, 0.86f); lsRT.anchorMax = new Vector2(0f, 0.86f); lsRT.pivot = new Vector2(0.5f, 0f);
        lsRT.anchoredPosition = new Vector2(185f, -65f); lsRT.sizeDelta = new Vector2(72f, 72f);

        GameObject leftScoreLabel = CreateText("LeftScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter);
        RectTransform lslRT = leftScoreLabel.GetComponent<RectTransform>();
        lslRT.anchorMin = new Vector2(0f, 0.86f); lslRT.anchorMax = new Vector2(0f, 0.86f); lslRT.pivot = new Vector2(0.5f, 0f);
        lslRT.anchoredPosition = new Vector2(291f, -65f); lslRT.sizeDelta = new Vector2(120f, 60f);
        leftScoreLabel.GetComponent<Text>().color = new Color(0.3f, 0.5f, 0.9f, 1f); leftScoreLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftScoreLabel.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        GameObject rightStarIcon = new GameObject("RightStarIcon", typeof(RectTransform), typeof(Image));
        rightStarIcon.transform.SetParent(canvasGo.transform, false);
        if (starSprite != null) { rightStarIcon.GetComponent<Image>().sprite = starSprite; rightStarIcon.GetComponent<Image>().preserveAspect = true; }
        rightStarIcon.GetComponent<Image>().color = Color.white; rightStarIcon.GetComponent<Image>().raycastTarget = false;
        RectTransform rsRT = rightStarIcon.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(1f, 0.86f); rsRT.anchorMax = new Vector2(1f, 0.86f); rsRT.pivot = new Vector2(0.5f, 0f);
        rsRT.anchoredPosition = new Vector2(-185f, -65f); rsRT.sizeDelta = new Vector2(72f, 72f);

        GameObject rightScoreLabel = CreateText("RightScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter);
        RectTransform rslRT = rightScoreLabel.GetComponent<RectTransform>();
        rslRT.anchorMin = new Vector2(1f, 0.86f); rslRT.anchorMax = new Vector2(1f, 0.86f); rslRT.pivot = new Vector2(0.5f, 0f);
        rslRT.anchoredPosition = new Vector2(-291f, -65f); rslRT.sizeDelta = new Vector2(120f, 60f);
        rslRT.GetComponent<Text>().color = new Color(0.9f, 0.3f, 0.3f, 1f); rslRT.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rslRT.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        // Divider
        GameObject divider = CreateImage("Divider", canvasGo.transform, new Color(0.35f, 0.6f, 0.9f, 0.6f));
        RectTransform divRT = divider.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.5f, 0.05f); divRT.anchorMax = new Vector2(0.5f, 0.82f);
        divRT.anchoredPosition = new Vector2(2f, 0f); divRT.sizeDelta = new Vector2(4f, 0f);

        // ===== Navigation buttons =====
        Sprite backSprite = LoadArtSprite("btn_back");
        Sprite homeSprite = LoadArtSprite("btn_home");
        Sprite settingSprite = LoadArtSprite("btn_setting");

        GameObject backButton = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backButton.transform.SetParent(canvasGo.transform, false);
        if (backSprite != null) { backButton.GetComponent<Image>().sprite = backSprite; backButton.GetComponent<Image>().preserveAspect = true; }
        backButton.GetComponent<Image>().color = Color.white;
        RectTransform backRT = backButton.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0f, 1f); backRT.anchorMax = new Vector2(0f, 1f); backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(8f, -5f); backRT.sizeDelta = new Vector2(40f, 40f);

        GameObject homeButton = new GameObject("HomeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        homeButton.transform.SetParent(canvasGo.transform, false);
        if (homeSprite != null) { homeButton.GetComponent<Image>().sprite = homeSprite; homeButton.GetComponent<Image>().preserveAspect = true; }
        homeButton.GetComponent<Image>().color = Color.white;
        RectTransform homeRT = homeButton.GetComponent<RectTransform>();
        homeRT.anchorMin = new Vector2(1f, 1f); homeRT.anchorMax = new Vector2(1f, 1f); homeRT.pivot = new Vector2(1f, 1f);
        homeRT.anchoredPosition = new Vector2(-8f, -5f); homeRT.sizeDelta = new Vector2(40f, 40f);

        GameObject settingButton = new GameObject("SettingButton", typeof(RectTransform), typeof(Image), typeof(Button));
        settingButton.transform.SetParent(canvasGo.transform, false);
        if (settingSprite != null) { settingButton.GetComponent<Image>().sprite = settingSprite; settingButton.GetComponent<Image>().preserveAspect = true; }
        settingButton.GetComponent<Image>().color = Color.white;
        RectTransform settingRT = settingButton.GetComponent<RectTransform>();
        settingRT.anchorMin = new Vector2(1f, 1f); settingRT.anchorMax = new Vector2(1f, 1f); settingRT.pivot = new Vector2(1f, 1f);
        settingRT.anchoredPosition = new Vector2(-52f, -5f); settingRT.sizeDelta = new Vector2(40f, 40f);

        // ===== Player panels =====
        GameObject p1Panel = CreatePlayerPanel("Player1Panel", canvasGo.transform, true);
        GameObject p2Panel = CreatePlayerPanel("Player2Panel", canvasGo.transform, false);

        // Game over panel
        GameObject gameOverPanel = CreateImage("GameOverPanel", canvasGo.transform, new Color(0f, 0f, 0f, 0.75f));
        SetRect(gameOverPanel.GetComponent<RectTransform>(), new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero);
        GameObject goResultText = CreateText("ResultText", gameOverPanel.transform, "", 28, TextAnchor.MiddleCenter);
        SetRect(goResultText.GetComponent<RectTransform>(), new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
        goResultText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        GameObject goRetryBtn = CreateStyledButton("RetryButton", gameOverPanel.transform, "Choi lai",
            new Vector2(0.16f, 0.12f), new Vector2(0.44f, 0.3f), new Color(0.2f, 0.7f, 0.3f, 1f), Color.white, 22);
        gameOverPanel.SetActive(false);

        // ===== Wire References =====
        SerializedObject viewSo = new SerializedObject(view);
        // questionText removed — leave unassigned (View.SetQuestionText is a no-op when null)
        viewSo.FindProperty("timerText").objectReferenceValue = timerTextGo.GetComponent<Text>();
        viewSo.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        viewSo.FindProperty("gameOverResultText").objectReferenceValue = goResultText.GetComponent<Text>();
        viewSo.FindProperty("retryButton").objectReferenceValue = goRetryBtn.GetComponent<Button>();
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        viewSo.FindProperty("homeButton").objectReferenceValue = homeButton.GetComponent<Button>();
        viewSo.FindProperty("settingButton").objectReferenceValue = settingButton.GetComponent<Button>();
        viewSo.FindProperty("p1ScoreBarFill").objectReferenceValue = leftBarFill.GetComponent<Image>();
        viewSo.FindProperty("p2ScoreBarFill").objectReferenceValue = rightBarFill.GetComponent<Image>();

        WirePlayerRefs(viewSo, "p1", p1Panel);
        WirePlayerRefs(viewSo, "p2", p2Panel);
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // Tutorial panel (container always active, panel root toggled)
        GameObject tutContainer = new GameObject("TutorialContainer", typeof(RectTransform));
        tutContainer.transform.SetParent(canvasGo.transform, false);
        SetRect(tutContainer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject tutPanel = CreateImage("TutorialPanel", tutContainer.transform, new Color(0f, 0f, 0f, 0.7f));
        SetRect(tutPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Tutorial image (reuse AddUp tutorial)
        Sprite tutorialSprite = LoadArtSprite("tutorial_bg");
        if (tutorialSprite != null)
        {
            GameObject tutImg = new GameObject("TutorialImage", typeof(RectTransform), typeof(Image));
            tutImg.transform.SetParent(tutPanel.transform, false);
            Image tImg = tutImg.GetComponent<Image>();
            tImg.sprite = tutorialSprite; tImg.preserveAspect = true; tImg.raycastTarget = false;
            SetRect(tutImg.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), Vector2.zero, Vector2.zero);
        }

        Sprite startBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/MenuScene/btn_start.png");
        GameObject tutStartBtn = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        tutStartBtn.transform.SetParent(tutPanel.transform, false);
        if (startBtnSpr != null) { tutStartBtn.GetComponent<Image>().sprite = startBtnSpr; tutStartBtn.GetComponent<Image>().preserveAspect = true; }
        tutStartBtn.GetComponent<Image>().color = Color.white;
        SetRect(tutStartBtn.GetComponent<RectTransform>(), new Vector2(0.35f, 0.14f), new Vector2(0.65f, 0.26f), Vector2.zero, Vector2.zero);
        GameObject tutTitle = CreateText("TitleText", tutPanel.transform, "Huong dan: NumberAddUp", 28, TextAnchor.MiddleCenter);
        SetRect(tutTitle.GetComponent<RectTransform>(), new Vector2(0.15f, 0.80f), new Vector2(0.85f, 0.95f), Vector2.zero, Vector2.zero);
        tutTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
        TutorialPanel tutComp = tutContainer.AddComponent<TutorialPanel>();
        SerializedObject tpSo = new SerializedObject(tutComp);
        tpSo.FindProperty("panelRoot").objectReferenceValue = tutPanel;
        tpSo.FindProperty("startButton").objectReferenceValue = tutStartBtn.GetComponent<Button>();
        tpSo.FindProperty("titleText").objectReferenceValue = tutTitle.GetComponent<Text>();
        tpSo.ApplyModifiedPropertiesWithoutUndo();
        tutPanel.SetActive(false);

        // Wire bar names + score labels
        SerializedObject viewSo2 = new SerializedObject(view);
        viewSo2.FindProperty("p1BarNameText").objectReferenceValue = p1BarName.GetComponent<Text>();
        viewSo2.FindProperty("p2BarNameText").objectReferenceValue = p2BarName.GetComponent<Text>();
        viewSo2.FindProperty("p1ScoreText").objectReferenceValue = leftScoreLabel.GetComponent<Text>();
        viewSo2.FindProperty("p2ScoreText").objectReferenceValue = rightScoreLabel.GetComponent<Text>();
        viewSo2.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("gameView").objectReferenceValue = view;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutContainer.GetComponent<TutorialPanel>();
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Player Panel (identical layout to AddUp, with Text instead of ImageContainer) =====

    private static GameObject CreatePlayerPanel(string name, Transform parent, bool isLeft)
    {
        Sprite greenSprite = LoadArtSprite("box_green");
        Sprite yellowSprite = LoadArtSprite("card_yellow");
        Sprite questionSprite = LoadArtSprite("icon_question");

        GameObject panel = CreateImage(name, parent, new Color(0f, 0f, 0f, 0f));
        panel.GetComponent<Image>().raycastTarget = false;
        RectTransform prt = panel.GetComponent<RectTransform>();
        if (isLeft)
        {
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0.5f, 0.88f);
            prt.anchoredPosition = new Vector2(-13.85f, 4.70f); prt.sizeDelta = new Vector2(-59.70f, -49.39f);
        }
        else
        {
            prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(1f, 0.88f);
            prt.anchoredPosition = new Vector2(16.12f, 4.70f); prt.sizeDelta = new Vector2(-64.23f, -49.39f);
        }

        // Equation boxes with NUMBER TEXT inside (not image grids)
        GameObject boxA = CreateNumberBox("BoxA", panel.transform, greenSprite, new Vector2(0.02f, 0.55f), new Vector2(0.28f, 0.88f));
        GameObject plusLabel = CreateText("PlusLabel", panel.transform, "+", 48, TextAnchor.MiddleCenter);
        SetRect(plusLabel.GetComponent<RectTransform>(), new Vector2(0.28f, 0.58f), new Vector2(0.38f, 0.85f), Vector2.zero, Vector2.zero);
        plusLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        plusLabel.GetComponent<Text>().color = new Color(0.3f, 0.15f, 0.05f, 1f); // dark brown
        GameObject boxB = CreateNumberBox("BoxB", panel.transform, greenSprite, new Vector2(0.38f, 0.55f), new Vector2(0.64f, 0.88f));
        GameObject equalsLabel = CreateText("EqualsLabel", panel.transform, "=", 48, TextAnchor.MiddleCenter);
        SetRect(equalsLabel.GetComponent<RectTransform>(), new Vector2(0.64f, 0.58f), new Vector2(0.74f, 0.85f), Vector2.zero, Vector2.zero);
        equalsLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
        equalsLabel.GetComponent<Text>().color = new Color(0.3f, 0.15f, 0.05f, 1f); // dark brown
        GameObject boxC = CreateNumberBox("BoxC", panel.transform, greenSprite, new Vector2(0.74f, 0.55f), new Vector2(0.98f, 0.88f));

        // Hidden overlay
        GameObject hiddenOverlay = new GameObject("HiddenOverlay", typeof(RectTransform), typeof(Image));
        hiddenOverlay.transform.SetParent(panel.transform, false);
        Image hoImg = hiddenOverlay.GetComponent<Image>();
        if (yellowSprite != null) { hoImg.sprite = yellowSprite; hoImg.preserveAspect = false; }
        hoImg.color = Color.white;
        SetRect(hiddenOverlay.GetComponent<RectTransform>(), new Vector2(0.02f, 0.55f), new Vector2(0.28f, 0.88f), Vector2.zero, Vector2.zero);
        GameObject qmIcon = new GameObject("QuestionMark", typeof(RectTransform), typeof(Image));
        qmIcon.transform.SetParent(hiddenOverlay.transform, false);
        if (questionSprite != null) { qmIcon.GetComponent<Image>().sprite = questionSprite; qmIcon.GetComponent<Image>().preserveAspect = true; }
        qmIcon.GetComponent<Image>().color = Color.white; qmIcon.GetComponent<Image>().raycastTarget = false;
        SetRect(qmIcon.GetComponent<RectTransform>(), new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero);

        // Second hidden overlay for Level 4 (? + ? = target)
        GameObject hiddenOverlayB = new GameObject("HiddenOverlayB", typeof(RectTransform), typeof(Image));
        hiddenOverlayB.transform.SetParent(panel.transform, false);
        Image hoBImg = hiddenOverlayB.GetComponent<Image>();
        if (yellowSprite != null) { hoBImg.sprite = yellowSprite; hoBImg.preserveAspect = false; }
        hoBImg.color = Color.white;
        SetRect(hiddenOverlayB.GetComponent<RectTransform>(), new Vector2(0.37f, 0.55f), new Vector2(0.63f, 0.88f), Vector2.zero, Vector2.zero);
        GameObject qmIconB = new GameObject("QuestionMark", typeof(RectTransform), typeof(Image));
        qmIconB.transform.SetParent(hiddenOverlayB.transform, false);
        if (questionSprite != null) { qmIconB.GetComponent<Image>().sprite = questionSprite; qmIconB.GetComponent<Image>().preserveAspect = true; }
        qmIconB.GetComponent<Image>().color = Color.white; qmIconB.GetComponent<Image>().raycastTarget = false;
        SetRect(qmIconB.GetComponent<RectTransform>(), new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero);
        hiddenOverlayB.SetActive(false);

        // Answer buttons with NUMBER TEXT
        CreateNumberAnswerButton("Answer1Button", panel.transform, greenSprite, new Vector2(0.02f, 0.28f), new Vector2(0.32f, 0.50f),
            new Vector2(isLeft ? 0f : -0.18f, isLeft ? -67.82f : -70.43f), new Vector2(isLeft ? 0f : -0.35f, isLeft ? 103.55f : 99.34f));
        CreateNumberAnswerButton("Answer2Button", panel.transform, greenSprite, new Vector2(0.35f, 0.28f), new Vector2(0.65f, 0.50f),
            new Vector2(isLeft ? 0f : -0.57f, isLeft ? -67.82f : -70.43f), new Vector2(isLeft ? 0f : -0.35f, isLeft ? 103.55f : 99.34f));
        CreateNumberAnswerButton("Answer3Button", panel.transform, greenSprite, new Vector2(0.68f, 0.28f), new Vector2(0.98f, 0.50f),
            new Vector2(isLeft ? 2f : 0f, isLeft ? -68.17f : -71f), new Vector2(isLeft ? -0.35f : 0f, isLeft ? 98.15f : 104.77f));

        // NameText placeholder (actual name on team bar)
        GameObject nameText = CreateText("NameText", panel.transform, "", 1, TextAnchor.MiddleCenter);
        SetRect(nameText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = Color.clear;

        // ScoreText placeholder (actual score on bar labels)
        GameObject scoreText = CreateText("ScoreText", panel.transform, "", 1, TextAnchor.MiddleCenter);
        SetRect(scoreText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        scoreText.GetComponent<Text>().color = Color.clear;

        // Feedback icons (circle + ✓/✗)
        GameObject okIcon = FeedbackIconBuilder.Create("CorrectIcon", panel.transform, true, new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f));
        GameObject badIcon = FeedbackIconBuilder.Create("WrongIcon", panel.transform, false, new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f));

        // Countdown text (Team mode)
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

    /// <summary>Equation box with green card sprite + large centered number</summary>
    private static GameObject CreateNumberBox(string name, Transform parent, Sprite greenSprite, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject box = new GameObject(name, typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        Image boxImg = box.GetComponent<Image>();
        if (greenSprite != null) { boxImg.sprite = greenSprite; boxImg.preserveAspect = false; }
        boxImg.color = Color.white;
        SetRect(box.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Shadow/outline layer (dark offset text behind)
        GameObject shadow = CreateText("Shadow", box.transform, "", 64, TextAnchor.MiddleCenter);
        SetRect(shadow.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, -2f), new Vector2(2f, -2f));
        shadow.GetComponent<Text>().color = new Color(0.1f, 0.3f, 0.1f, 0.6f);
        shadow.GetComponent<Text>().fontStyle = FontStyle.Bold;
        shadow.GetComponent<Text>().raycastTarget = false;

        // Main number text
        GameObject numText = CreateText("NumberText", box.transform, "", 64, TextAnchor.MiddleCenter);
        SetRect(numText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        numText.GetComponent<Text>().color = new Color(1f, 0.95f, 0.3f, 1f); // bright yellow
        numText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        return box;
    }

    /// <summary>Answer button with green card + large number + editor-adjusted offsets</summary>
    private static void CreateNumberAnswerButton(string name, Transform parent, Sprite greenSprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 posOffset, Vector2 sizeOffset)
    {
        GameObject btn = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(parent, false);
        Image img = btn.GetComponent<Image>();
        if (greenSprite != null) { img.sprite = greenSprite; img.preserveAspect = false; }
        img.color = Color.white;
        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.anchoredPosition = posOffset; rt.sizeDelta = sizeOffset;

        // Shadow
        GameObject shadow = CreateText("Shadow", btn.transform, "0", 80, TextAnchor.MiddleCenter);
        SetRect(shadow.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2f, -2f), new Vector2(2f, -2f));
        shadow.GetComponent<Text>().color = new Color(0.1f, 0.3f, 0.1f, 0.6f);
        shadow.GetComponent<Text>().fontStyle = FontStyle.Bold;
        shadow.GetComponent<Text>().raycastTarget = false;

        GameObject txt = CreateText("AnswerText", btn.transform, "0", 80, TextAnchor.MiddleCenter);
        SetRect(txt.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        txt.GetComponent<Text>().color = new Color(1f, 0.95f, 0.3f, 1f); // bright yellow
        txt.GetComponent<Text>().fontStyle = FontStyle.Bold;
    }

    // ===== Wire Player References =====

    private static void WirePlayerRefs(SerializedObject viewSo, string prefix, GameObject panel)
    {
        viewSo.FindProperty(prefix + "NameText").objectReferenceValue = panel.transform.Find("NameText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "ScoreText").objectReferenceValue = panel.transform.Find("ScoreText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "BoxAText").objectReferenceValue = panel.transform.Find("BoxA/NumberText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "BoxBText").objectReferenceValue = panel.transform.Find("BoxB/NumberText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "BoxCText").objectReferenceValue = panel.transform.Find("BoxC/NumberText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "BoxABorder").objectReferenceValue = panel.transform.Find("BoxA").GetComponent<Image>();
        viewSo.FindProperty(prefix + "BoxBBorder").objectReferenceValue = panel.transform.Find("BoxB").GetComponent<Image>();
        viewSo.FindProperty(prefix + "BoxCBorder").objectReferenceValue = panel.transform.Find("BoxC").GetComponent<Image>();
        viewSo.FindProperty(prefix + "PlusLabel").objectReferenceValue = panel.transform.Find("PlusLabel").GetComponent<Text>();
        viewSo.FindProperty(prefix + "EqualsLabel").objectReferenceValue = panel.transform.Find("EqualsLabel").GetComponent<Text>();
        viewSo.FindProperty(prefix + "HiddenOverlay").objectReferenceValue = panel.transform.Find("HiddenOverlay").gameObject;
        viewSo.FindProperty(prefix + "HiddenOverlayB").objectReferenceValue = panel.transform.Find("HiddenOverlayB").gameObject;
        viewSo.FindProperty(prefix + "Answer1Button").objectReferenceValue = panel.transform.Find("Answer1Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer2Button").objectReferenceValue = panel.transform.Find("Answer2Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer3Button").objectReferenceValue = panel.transform.Find("Answer3Button").GetComponent<Button>();
        viewSo.FindProperty(prefix + "Answer1Text").objectReferenceValue = panel.transform.Find("Answer1Button/AnswerText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "Answer2Text").objectReferenceValue = panel.transform.Find("Answer2Button/AnswerText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "Answer3Text").objectReferenceValue = panel.transform.Find("Answer3Button/AnswerText").GetComponent<Text>();
        viewSo.FindProperty(prefix + "Answer1Border").objectReferenceValue = panel.transform.Find("Answer1Button").GetComponent<Image>();
        viewSo.FindProperty(prefix + "Answer2Border").objectReferenceValue = panel.transform.Find("Answer2Button").GetComponent<Image>();
        viewSo.FindProperty(prefix + "Answer3Border").objectReferenceValue = panel.transform.Find("Answer3Button").GetComponent<Image>();
        viewSo.FindProperty(prefix + "CorrectIcon").objectReferenceValue = panel.transform.Find("CorrectIcon").gameObject;
        viewSo.FindProperty(prefix + "WrongIcon").objectReferenceValue = panel.transform.Find("WrongIcon").gameObject;
        viewSo.FindProperty(prefix + "CountdownText").objectReferenceValue = panel.transform.Find("CountdownText").GetComponent<Text>();
    }

    // ===== Standard Helpers =====

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.96f, 0.91f, 0.82f, 1f);
        camera.orthographic = true; camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f; camera.farClipPlane = 1000f;
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
        text.text = content; text.alignment = anchor; text.fontSize = size; text.color = Color.white;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
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
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game"); EnsureFolder("Assets/Game/Scenes"); EnsureFolder(SceneFolder);
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
        for (int i = 0; i < scenes.Length; i++) if (scenes[i].path == targetPath) return;
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
        newScenes[scenes.Length] = new EditorBuildSettingsScene(targetPath, true);
        EditorBuildSettings.scenes = newScenes;
    }
}
