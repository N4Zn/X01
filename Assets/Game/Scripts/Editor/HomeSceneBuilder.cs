using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HomeSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/HomeScene";
    private const string ScenePath = "Assets/Game/Scenes/HomeScene/HomeScene.unity";
    private const string TextureFolder = "Assets/Game/Textures/HomeScene";
    private const string SettingTextureFolder = "Assets/Game/Textures/Setting";

    [MenuItem("Tools/HomeScene/Build Home Scene")]
    public static void BuildHomeScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("HomeSceneBuilder: Cannot build scene while in Play mode.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("HomeSceneBuilder: finished generating HomeScene.");
    }

    [MenuItem("Tools/HomeScene/Add Camera To Home Scene")]
    public static void AddCameraToHomeScene()
    {
        if (!File.Exists(ToAbsolutePath(ScenePath)))
        {
            Debug.LogWarning("HomeSceneBuilder: HomeScene does not exist yet. Run Build Home Scene first.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera existingMain = Camera.main;
        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        if (existingMain == null && (allCameras == null || allCameras.Length == 0))
        {
            CreateMainCamera();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HomeSceneBuilder: added Main Camera to HomeScene.");
            return;
        }

        if (existingMain == null && allCameras != null && allCameras.Length > 0)
        {
            allCameras[0].gameObject.tag = "MainCamera";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HomeSceneBuilder: existing camera tagged as MainCamera and scene saved.");
            return;
        }

        Debug.Log("HomeSceneBuilder: HomeScene already has a Main Camera.");
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
        scaler.referenceResolution = new Vector2(1024f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root object with Controller + View
        GameObject root = new GameObject("HomeSceneRoot");
        HomeSceneController controller = root.AddComponent<HomeSceneController>();
        HomeSceneView view = root.AddComponent<HomeSceneView>();

        // ===== Import sprites as Sprite type =====
        EnsureSpriteImportSettings(TextureFolder + "/HomeBackground.png");
        EnsureSpriteImportSettings(TextureFolder + "/HomeTitle.png");
        EnsureSpriteImportSettings(TextureFolder + "/HomeStartButton.png");
        EnsureSpriteImportSettings(TextureFolder + "/HomeSettingButton.png");

        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/HomeBackground.png");
        Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/HomeTitle.png");
        Sprite startSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/HomeStartButton.png");
        Sprite settingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/HomeSettingButton.png");

        // ===== Background (screen1 artwork) =====
        GameObject bgPanel = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bgPanel.transform.SetParent(canvasGo.transform, false);
        Image bgImage = bgPanel.GetComponent<Image>();
        bgImage.sprite = bgSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.preserveAspect = false;
        bgImage.color = Color.white;
        SetRect(bgPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ===== Main Screen Elements =====

        // Game Title (sprite image instead of text)
        GameObject titleImage = new GameObject("GameTitleImage", typeof(RectTransform), typeof(Image));
        titleImage.transform.SetParent(canvasGo.transform, false);
        Image titleImg = titleImage.GetComponent<Image>();
        titleImg.sprite = titleSprite;
        titleImg.type = Image.Type.Simple;
        titleImg.preserveAspect = true;
        titleImg.color = Color.white;
        titleImg.raycastTarget = false;
        SetRect(titleImage.GetComponent<RectTransform>(), new Vector2(0.2f, 0.55f), new Vector2(0.8f, 0.85f), Vector2.zero, Vector2.zero);

        // Hidden text elements (keep for View compatibility, but invisible)
        GameObject titleText = CreateText("GameTitleText", canvasGo.transform, "Tiny Explorers", 1, TextAnchor.MiddleCenter);
        SetRect(titleText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        titleText.GetComponent<Text>().color = Color.clear;
        titleText.SetActive(false);

        GameObject subtitleText = CreateText("SubtitleText", canvasGo.transform, "Amazing Adventures", 1, TextAnchor.MiddleCenter);
        SetRect(subtitleText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        subtitleText.GetComponent<Text>().color = Color.clear;
        subtitleText.SetActive(false);

        // START Button (sprite image)
        GameObject startButton = CreateSpriteButton("StartButton", canvasGo.transform, startSprite,
            new Vector2(0.3f, 0.15f), new Vector2(0.7f, 0.38f));

        // Settings Button (sprite image, top-right)
        GameObject settingsButton = CreateSpriteButton("SettingsButton", canvasGo.transform, settingSprite,
            new Vector2(0.88f, 0.85f), new Vector2(0.98f, 0.98f));

        // ===== Setting Panel Overlay =====
        GameObject settingPanel = BuildSettingPanel(canvasGo.transform);

        // ===== Wire References =====
        WireReferences(view, controller, canvasGo.transform,
            titleText, subtitleText,
            startButton, settingsButton, settingPanel);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Setting Panel =====

    private static GameObject BuildSettingPanel(Transform canvasTransform)
    {
        // ===== Load Setting sprites =====
        string[] settingAssets = {
            "SettingPanelBgClean", "SettingTitle", "SettingCloseButton",
            "SettingSliderFill", "SettingSliderHandle", "SettingSliderTrack",
            "SettingTimeIcon", "SettingSoundIcon", "SettingMusicIcon", "SettingRingIcon",
            "SettingButtonGreen", "SettingButtonGreenBig",
            "SettingButtonGray", "SettingButtonGrayBig", "SettingButtonGraySmall"
        };
        foreach (string asset in settingAssets)
            EnsureSpriteImportSettings(SettingTextureFolder + "/" + asset + ".png");

        Sprite panelBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingPanelBgClean.png");
        Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingTitle.png");
        Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingCloseButton.png");
        Sprite sliderFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingSliderFill.png");
        Sprite sliderHandleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingSliderHandle.png");
        Sprite sliderTrackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingSliderTrack.png");
        Sprite soundIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingSoundIcon.png");
        Sprite musicIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingMusicIcon.png");
        Sprite timeIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingTimeIcon.png");
        Sprite ringIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingRingIcon.png");
        Sprite greenBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingButtonGreen.png");
        Sprite grayBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingButtonGray.png");
        Sprite graySmallBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SettingTextureFolder + "/SettingButtonGraySmall.png");

        Font quicksandBold = GetQuicksandFont("Bold");
        Color labelColor = new Color(0.12f, 0.14f, 0.22f, 1f);

        // ===== Grid constants — columns relative to innerPanel =====
        // Compact layout to fit within the white paper area
        const float iconL = 0.03f;  const float iconR = 0.07f;   // Icon column (small)
        const float lblL  = 0.09f;  const float lblR  = 0.36f;   // Label column
        const float ctrlL = 0.38f;  const float ctrlR = 0.96f;   // Controls column

        // 5 compact rows with reduced height (~10% each, ~6-7% gap)
        const float row1B = 0.80f; const float row1T = 0.90f;  // Âm Thanh
        const float row2B = 0.64f; const float row2T = 0.74f;  // Nhạc nền
        const float row3B = 0.48f; const float row3T = 0.58f;  // Thời gian chơi
        const float row4B = 0.32f; const float row4T = 0.42f;  // Mức độ phản hồi
        const float row5B = 0.16f; const float row5T = 0.26f;  // Thời gian mỗi câu

        // Root overlay (full screen, semi-transparent bg)
        GameObject panelRoot = new GameObject("SettingPanelRoot", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvasTransform, false);
        panelRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        SetRect(panelRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Background image — notebook with cat mascot (big, centered)
        GameObject bgImage = new GameObject("SettingBgImage", typeof(RectTransform), typeof(Image));
        bgImage.transform.SetParent(panelRoot.transform, false);
        Image bgImgComp = bgImage.GetComponent<Image>();
        bgImgComp.sprite = panelBgSprite;
        bgImgComp.type = Image.Type.Simple;
        bgImgComp.preserveAspect = false;
        bgImgComp.raycastTarget = false;
        bgImgComp.color = Color.white;
        SetRect(bgImage.GetComponent<RectTransform>(), new Vector2(0.01f, -0.02f), new Vector2(0.99f, 0.96f), new Vector2(-10.451f, 11.758f), new Vector2(10.451f, 23.516f));

        // Inner panel — controls container, fits inside the white paper area of the notebook
        // Paper area: skip left binding (~22%), top holes (~18%), bottom margin (~12%)
        GameObject innerPanel = new GameObject("SettingInnerPanel", typeof(RectTransform));
        innerPanel.transform.SetParent(panelRoot.transform, false);
        SetRect(innerPanel.GetComponent<RectTransform>(), new Vector2(0.22f, 0.08f), new Vector2(0.90f, 0.74f), Vector2.zero, Vector2.zero);

        // Title "Cài Đặt" image (centered at top, above inner panel but within notebook)
        GameObject titleImgGo = new GameObject("SettingTitle", typeof(RectTransform), typeof(Image));
        titleImgGo.transform.SetParent(panelRoot.transform, false);
        Image titleImg = titleImgGo.GetComponent<Image>();
        titleImg.sprite = titleSprite;
        titleImg.type = Image.Type.Simple;
        titleImg.preserveAspect = true;
        titleImg.color = Color.white;
        titleImg.raycastTarget = false;
        SetRect(titleImgGo.GetComponent<RectTransform>(), new Vector2(0.40f, 0.76f), new Vector2(0.60f, 0.86f), Vector2.zero, Vector2.zero);

        // Close button (top-right corner of notebook background image) — editor-adjusted offsets
        GameObject closeButton = CreateSpriteButton("SettingCloseButton", bgImage.transform, closeSprite,
            new Vector2(0.88f, 0.82f), new Vector2(0.95f, 0.96f));
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.88f, 0.82f), new Vector2(0.95f, 0.96f), new Vector2(-78f, -56f), new Vector2(-78f, -56f));

        // ===== Row 1: Âm Thanh (Sound Volume) =====
        CreateSettingSpriteIcon(innerPanel.transform, soundIconSprite,
            iconL, iconR, row1B, row1T);

        GameObject sfxLabel = CreateFontText("SfxLabel", innerPanel.transform, "Âm Thanh", 20,
            TextAnchor.MiddleLeft, labelColor, quicksandBold);
        SetRect(sfxLabel.GetComponent<RectTransform>(), new Vector2(lblL, row1B), new Vector2(lblR, row1T), Vector2.zero, Vector2.zero);

        GameObject sfxSlider = CreateSpriteSlider("SfxVolumeSlider", innerPanel.transform, sliderFillSprite, sliderHandleSprite, sliderTrackSprite);
        SetRect(sfxSlider.GetComponent<RectTransform>(), new Vector2(ctrlL, row1B + 0.01f), new Vector2(ctrlR, row1T - 0.01f), Vector2.zero, new Vector2(-66.974f, 0f));

        // Hidden mute controls (kept for View compatibility)
        GameObject sfxMuteButton = CreateStyledButton("SfxMuteButton", innerPanel.transform, "Mute",
            new Vector2(0f, 0f), new Vector2(0f, 0f), Color.clear, Color.clear, 1);
        sfxMuteButton.SetActive(false);
        GameObject sfxMuteLabel = CreateText("SfxMuteLabel", innerPanel.transform, "OFF", 1, TextAnchor.MiddleCenter);
        sfxMuteLabel.SetActive(false);

        // ===== Row 2: Nhạc nền (Music Volume) =====
        CreateSettingSpriteIcon(innerPanel.transform, musicIconSprite,
            iconL, iconR, row2B, row2T);

        GameObject musicLabel = CreateFontText("MusicLabel", innerPanel.transform, "Nhạc nền", 20,
            TextAnchor.MiddleLeft, labelColor, quicksandBold);
        SetRect(musicLabel.GetComponent<RectTransform>(), new Vector2(lblL, row2B), new Vector2(lblR, row2T), Vector2.zero, Vector2.zero);

        GameObject musicSlider = CreateSpriteSlider("MusicVolumeSlider", innerPanel.transform, sliderFillSprite, sliderHandleSprite, sliderTrackSprite);
        SetRect(musicSlider.GetComponent<RectTransform>(), new Vector2(ctrlL, row2B + 0.01f), new Vector2(ctrlR, row2T - 0.01f), Vector2.zero, new Vector2(-66.974f, 0f));

        // Hidden mute controls
        GameObject musicMuteButton = CreateStyledButton("MusicMuteButton", innerPanel.transform, "Mute",
            new Vector2(0f, 0f), new Vector2(0f, 0f), Color.clear, Color.clear, 1);
        musicMuteButton.SetActive(false);
        GameObject musicMuteLabel = CreateText("MusicMuteLabel", innerPanel.transform, "OFF", 1, TextAnchor.MiddleCenter);
        musicMuteLabel.SetActive(false);

        // ===== Row 3: Thời gian chơi (Game Time) =====
        CreateSettingSpriteIcon(innerPanel.transform, timeIconSprite,
            iconL, iconR, row3B, row3T);

        GameObject timeLabel = CreateFontText("TimeLabel", innerPanel.transform, "Thời gian chơi", 18,
            TextAnchor.MiddleLeft, labelColor, quicksandBold);
        SetRect(timeLabel.GetComponent<RectTransform>(), new Vector2(lblL, row3B), new Vector2(lblR, row3T), Vector2.zero, Vector2.zero);

        // Time buttons — compact, evenly spaced
        float tbW = 0.12f; float tbGap = 0.02f; float tbStart = ctrlL;
        GameObject time60Button = CreateSpriteToggleButton("Time60Button", innerPanel.transform, "60s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(tbStart, row3B), new Vector2(tbStart + tbW, row3T));
        tbStart += tbW + tbGap;

        GameObject time90Button = CreateSpriteToggleButton("Time90Button", innerPanel.transform, "90s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(tbStart, row3B), new Vector2(tbStart + tbW, row3T));
        tbStart += tbW + tbGap;

        GameObject time120Button = CreateSpriteToggleButton("Time120Button", innerPanel.transform, "120s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(tbStart, row3B), new Vector2(tbStart + tbW, row3T));
        tbStart += tbW + tbGap;

        // "..." custom time input
        GameObject timeCustomInput = CreateInputField("TimeCustomInput", innerPanel.transform, "...");
        SetRect(timeCustomInput.GetComponent<RectTransform>(), new Vector2(tbStart, row3B), new Vector2(tbStart + 0.08f, row3T), Vector2.zero, Vector2.zero);
        if (graySmallBtnSprite != null)
        {
            timeCustomInput.GetComponent<Image>().sprite = graySmallBtnSprite;
            timeCustomInput.GetComponent<Image>().color = Color.white;
        }

        // ===== Row 4: Khoảng nghỉ giữa câu hỏi (Round Delay) =====
        CreateSettingSpriteIcon(innerPanel.transform, ringIconSprite,
            iconL, iconR, row4B, row4T);

        GameObject feedbackLabel = CreateFontText("FeedbackLabel", innerPanel.transform, "Khoảng nghỉ giữa câu (giây)", 18,
            TextAnchor.MiddleLeft, labelColor, quicksandBold);
        SetRect(feedbackLabel.GetComponent<RectTransform>(), new Vector2(lblL, row4B), new Vector2(lblR, row4T), Vector2.zero, Vector2.zero);

        // Round delay input — 1 ô nhập số giây (1-4s)
        GameObject roundDelayInput = CreateInputField("RoundDelayInput", innerPanel.transform, "1-4");
        SetRect(roundDelayInput.GetComponent<RectTransform>(), new Vector2(ctrlL, row4B), new Vector2(ctrlL + 0.08f, row4T), Vector2.zero, Vector2.zero);
        roundDelayInput.GetComponent<InputField>().contentType = InputField.ContentType.DecimalNumber;
        if (graySmallBtnSprite != null)
        {
            roundDelayInput.GetComponent<Image>().sprite = graySmallBtnSprite;
            roundDelayInput.GetComponent<Image>().color = Color.white;
        }

        // ===== Row 5: Thời gian mỗi câu (Question Timeout) =====
        CreateSettingSpriteIcon(innerPanel.transform, timeIconSprite,
            iconL, iconR, row5B, row5T);

        GameObject timeoutLabel = CreateFontText("TimeoutLabel", innerPanel.transform, "Thời gian mỗi câu", 18,
            TextAnchor.MiddleLeft, labelColor, quicksandBold);
        SetRect(timeoutLabel.GetComponent<RectTransform>(), new Vector2(lblL, row5B), new Vector2(lblR, row5T), Vector2.zero, Vector2.zero);

        float toStart = ctrlL;
        GameObject timeout10Button = CreateSpriteToggleButton("Timeout10Button", innerPanel.transform, "10s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(toStart, row5B), new Vector2(toStart + tbW, row5T));
        toStart += tbW + tbGap;

        GameObject timeout15Button = CreateSpriteToggleButton("Timeout15Button", innerPanel.transform, "15s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(toStart, row5B), new Vector2(toStart + tbW, row5T));
        toStart += tbW + tbGap;

        GameObject timeout20Button = CreateSpriteToggleButton("Timeout20Button", innerPanel.transform, "20s",
            grayBtnSprite, greenBtnSprite,
            new Vector2(toStart, row5B), new Vector2(toStart + tbW, row5T));

        panelRoot.SetActive(false);
        return panelRoot;
    }

    /// <summary>
    /// Creates a toggle button with sprite-based visuals: gray base + green highlight overlay.
    /// Highlight visibility is controlled by View via color (Color.white = show, Color.clear = hide).
    /// </summary>
    private static GameObject CreateSpriteToggleButton(string name, Transform parent, string label,
        Sprite inactiveSprite, Sprite activeSprite, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        Image btnImg = btnGo.GetComponent<Image>();
        btnImg.sprite = inactiveSprite;
        btnImg.type = Image.Type.Simple;
        btnImg.color = Color.white;
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Highlight overlay (green sprite, toggled via color)
        GameObject highlight = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        highlight.transform.SetParent(btnGo.transform, false);
        Image highlightImg = highlight.GetComponent<Image>();
        highlightImg.sprite = activeSprite;
        highlightImg.type = Image.Type.Simple;
        highlightImg.color = Color.clear; // hidden by default
        highlightImg.raycastTarget = false;
        SetRect(highlight.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Label text with Quicksand Bold
        Font btnFont = GetQuicksandFont("Bold");
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(btnGo.transform, false);
        Text txt = textGo.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 16;
        txt.color = Color.white;
        txt.font = btnFont;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.resizeTextMaxSize = 16;
        SetRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return btnGo;
    }

    /// <summary>
    /// Creates a slider with sprite-based track, fill, and handle.
    /// </summary>
    private static GameObject CreateSpriteSlider(string name, Transform parent, Sprite fillSprite, Sprite handleSprite, Sprite trackSprite = null)
    {
        GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Background track
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderGo.transform, false);
        Image bgImg = bg.GetComponent<Image>();
        if (trackSprite != null)
        {
            bgImg.sprite = trackSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.sprite = fillSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = new Color(0.82f, 0.84f, 0.88f, 1f);
        }
        SetRect(bg.GetComponent<RectTransform>(), new Vector2(0f, 0.15f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.15f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.GetComponent<Image>();
        fillImg.sprite = fillSprite;
        fillImg.type = Image.Type.Simple;
        fillImg.color = Color.white;
        SetRect(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Handle area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        SetRect(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImg = handle.GetComponent<Image>();
        handleImg.sprite = handleSprite;
        handleImg.type = Image.Type.Simple;
        handleImg.preserveAspect = true;
        handleImg.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(30f, 30f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;

        return sliderGo;
    }

    private static GameObject CreateSlider(string name, Transform parent)
    {
        GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // Background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sliderGo.transform, false);
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
        SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.75f),
            new Vector2(5f, 0f), new Vector2(-5f, 0f));

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f, 1f);
        SetRect(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Handle area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        SetRect(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(10f, 0f), new Vector2(-10f, 0f));

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);

        // Wire slider references
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();

        return sliderGo;
    }

    private static GameObject CreateInputField(string name, Transform parent, string placeholder)
    {
        GameObject inputGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        inputGo.transform.SetParent(parent, false);
        inputGo.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.28f, 1f);

        // Text child
        GameObject textGo = CreateText("Text", inputGo.transform, "", 18, TextAnchor.MiddleCenter);
        SetRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(5f, 0f), new Vector2(-5f, 0f));

        // Placeholder child
        GameObject placeholderGo = CreateText("Placeholder", inputGo.transform, placeholder, 18, TextAnchor.MiddleCenter);
        SetRect(placeholderGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(5f, 0f), new Vector2(-5f, 0f));
        placeholderGo.GetComponent<Text>().color = new Color(0.5f, 0.5f, 0.6f, 0.6f);
        placeholderGo.GetComponent<Text>().fontStyle = FontStyle.Italic;

        InputField input = inputGo.GetComponent<InputField>();
        input.textComponent = textGo.GetComponent<Text>();
        input.placeholder = placeholderGo.GetComponent<Text>();
        input.contentType = InputField.ContentType.IntegerNumber;

        return inputGo;
    }

    // ===== Wire References =====

    private static void WireReferences(HomeSceneView view, HomeSceneController controller,
        Transform canvas,
        GameObject titleText, GameObject subtitleText,
        GameObject startButton, GameObject settingsButton, GameObject settingPanelRoot)
    {
        SerializedObject viewSo = new SerializedObject(view);

        // Main screen
        viewSo.FindProperty("gameTitleText").objectReferenceValue = titleText.GetComponent<Text>();
        viewSo.FindProperty("subtitleText").objectReferenceValue = subtitleText.GetComponent<Text>();
        viewSo.FindProperty("startButton").objectReferenceValue = startButton.GetComponent<Button>();
        viewSo.FindProperty("settingsButton").objectReferenceValue = settingsButton.GetComponent<Button>();

        // Setting panel root
        viewSo.FindProperty("settingPanelRoot").objectReferenceValue = settingPanelRoot;

        // Find setting panel children via inner panel
        Transform innerPanel = settingPanelRoot.transform.Find("SettingInnerPanel");

        // Sliders
        viewSo.FindProperty("sfxVolumeSlider").objectReferenceValue =
            innerPanel.Find("SfxVolumeSlider").GetComponent<Slider>();
        viewSo.FindProperty("musicVolumeSlider").objectReferenceValue =
            innerPanel.Find("MusicVolumeSlider").GetComponent<Slider>();

        // Mute buttons + labels
        viewSo.FindProperty("sfxMuteButton").objectReferenceValue =
            innerPanel.Find("SfxMuteButton").GetComponent<Button>();
        viewSo.FindProperty("sfxMuteLabel").objectReferenceValue =
            innerPanel.Find("SfxMuteLabel").GetComponent<Text>();
        viewSo.FindProperty("musicMuteButton").objectReferenceValue =
            innerPanel.Find("MusicMuteButton").GetComponent<Button>();
        viewSo.FindProperty("musicMuteLabel").objectReferenceValue =
            innerPanel.Find("MusicMuteLabel").GetComponent<Text>();

        // Time buttons + highlights
        viewSo.FindProperty("time60Button").objectReferenceValue =
            innerPanel.Find("Time60Button").GetComponent<Button>();
        viewSo.FindProperty("time90Button").objectReferenceValue =
            innerPanel.Find("Time90Button").GetComponent<Button>();
        viewSo.FindProperty("time120Button").objectReferenceValue =
            innerPanel.Find("Time120Button").GetComponent<Button>();
        viewSo.FindProperty("timeCustomInput").objectReferenceValue =
            innerPanel.Find("TimeCustomInput").GetComponent<InputField>();

        viewSo.FindProperty("time60Highlight").objectReferenceValue =
            innerPanel.Find("Time60Button/Highlight").GetComponent<Image>();
        viewSo.FindProperty("time90Highlight").objectReferenceValue =
            innerPanel.Find("Time90Button/Highlight").GetComponent<Image>();
        viewSo.FindProperty("time120Highlight").objectReferenceValue =
            innerPanel.Find("Time120Button/Highlight").GetComponent<Image>();

        // Round delay input (seconds between questions, 1-4s)
        viewSo.FindProperty("roundDelayInput").objectReferenceValue =
            innerPanel.Find("RoundDelayInput").GetComponent<InputField>();

        // Question timeout buttons + highlights
        viewSo.FindProperty("timeout10Button").objectReferenceValue =
            innerPanel.Find("Timeout10Button").GetComponent<Button>();
        viewSo.FindProperty("timeout15Button").objectReferenceValue =
            innerPanel.Find("Timeout15Button").GetComponent<Button>();
        viewSo.FindProperty("timeout20Button").objectReferenceValue =
            innerPanel.Find("Timeout20Button").GetComponent<Button>();

        viewSo.FindProperty("timeout10Highlight").objectReferenceValue =
            innerPanel.Find("Timeout10Button/Highlight").GetComponent<Image>();
        viewSo.FindProperty("timeout15Highlight").objectReferenceValue =
            innerPanel.Find("Timeout15Button/Highlight").GetComponent<Image>();
        viewSo.FindProperty("timeout20Highlight").objectReferenceValue =
            innerPanel.Find("Timeout20Button/Highlight").GetComponent<Image>();

        // Close button (now child of SettingBgImage)
        Transform bgImageTr = settingPanelRoot.transform.Find("SettingBgImage");
        viewSo.FindProperty("settingCloseButton").objectReferenceValue =
            bgImageTr.Find("SettingCloseButton").GetComponent<Button>();

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // Controller
        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("homeSceneView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===== Helpers =====

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
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

    private static Font GetQuicksandFont(string weight = "Bold")
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Quicksand/Quicksand-" + weight + ".ttf");
        if (font != null) return font;
        return GetBuiltinFont();
    }

    private static Font GetBuiltinFont()
    {
        // Prefer Quicksand Bold for UI text
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Quicksand/Quicksand-Bold.ttf");
        if (font != null) return font;
        // Fallback to Quicksand Medium
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Quicksand/Quicksand-Medium.ttf");
        if (font != null) return font;
        // Fallback to Roboto
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Roboto/Roboto-Medium.ttf");
        if (font != null) return font;
        // Last resort: builtin
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    /// <summary>
    /// Creates a Text element with a specific font (not the default).
    /// </summary>
    private static GameObject CreateFontText(string name, Transform parent, string content, int size,
        TextAnchor anchor, Color color, Font font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.alignment = anchor;
        text.fontSize = size;
        text.color = color;
        text.font = font != null ? font : GetBuiltinFont();
        return go;
    }

    /// <summary>
    /// Creates a small colored icon using Unicode character text, same size for all rows.
    /// </summary>
    private static void CreateSettingIcon(Transform parent, string unicode, Color color,
        float xMin, float xMax, float yMin, float yMax, Font font)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = unicode;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 32;
        text.color = color;
        text.font = font != null ? font : GetBuiltinFont();
        text.raycastTarget = false;
        SetRect(go.GetComponent<RectTransform>(), new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// Creates a setting row icon using a sprite image instead of unicode text.
    /// </summary>
    private static void CreateSettingSpriteIcon(Transform parent, Sprite iconSprite,
        float xMin, float xMax, float yMin, float yMax)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = iconSprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = Color.white;
        SetRect(go.GetComponent<RectTransform>(), new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);
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

    /// <summary>
    /// Creates a button using a sprite image (no text child needed — image contains the visual).
    /// </summary>
    private static GameObject CreateSpriteButton(string name, Transform parent, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        Image img = btnGo.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        // Configure button transition
        Button btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = colors;

        return btnGo;
    }

    /// <summary>
    /// Ensures a texture asset is imported as Sprite (2D and UI) type.
    /// </summary>
    private static void EnsureSpriteImportSettings(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }
}
