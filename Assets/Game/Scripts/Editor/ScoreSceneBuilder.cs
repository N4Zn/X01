using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ScoreSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/ScoreScene";
    private const string ScenePath = "Assets/Game/Scenes/ScoreScene/ScoreScene.unity";
    private const string SpriteFolder = "Assets/Game/Textures/ScoreScene";
    private const string TeamSpriteFolder = "Assets/Game/Textures/TeamSelectScene";

    [MenuItem("Tools/ScoreScene/Build Score Scene")]
    public static void BuildScoreScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("ScoreSceneBuilder: Cannot build scene while in Play mode.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ScoreSceneBuilder: finished generating ScoreScene.");
    }

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

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Root
        GameObject root = new GameObject("ScoreSceneRoot");
        ScoreSceneController controller = root.AddComponent<ScoreSceneController>();
        ScoreSceneView view = root.AddComponent<ScoreSceneView>();

        // ============================================================
        // 1V1 MODE PANEL
        // ============================================================
        GameObject ovoPanel = new GameObject("OneVsOnePanel", typeof(RectTransform));
        ovoPanel.transform.SetParent(canvasGo.transform, false);
        SetAnchors(ovoPanel, 0, 0, 1, 1);

        // 1v1 Background (same as team mode bg)
        Sprite bg1v1Sprite = LoadSprite("bg_team_mode");
        GameObject ovoBg = CreateSpriteImage("Background", ovoPanel.transform, bg1v1Sprite);
        SetAnchors(ovoBg, 0, 0, 1, 1);
        ovoBg.GetComponent<Image>().raycastTarget = false;

        // Confetti (1v1) — shown on win
        Sprite ovoConfettiSprite = LoadSprite("confetti");
        GameObject ovoConfetti = CreateSpriteImage("Confetti", ovoPanel.transform, ovoConfettiSprite);
        RectTransform ovoCfRT = ovoConfetti.GetComponent<RectTransform>();
        ovoCfRT.anchorMin = new Vector2(0f, 0.15f); ovoCfRT.anchorMax = new Vector2(1f, 0.95f);
        ovoCfRT.offsetMin = Vector2.zero; ovoCfRT.offsetMax = Vector2.zero;
        ovoConfetti.GetComponent<Image>().raycastTarget = false;

        // Team banner for 1v1 (reuse TeamSelect sprites for name panels)
        Sprite ovoBlueBarSprite = LoadSpriteFrom(TeamSpriteFolder, "TeamBlueBar");
        Sprite ovoRedBarSprite = LoadSpriteFrom(TeamSpriteFolder, "TeamRedBar");
        Sprite ovoVsSprite = LoadSpriteFrom(TeamSpriteFolder, "VsButton");

        // Player 1 name bar (top left)
        GameObject ovoP1Bar = CreateSpriteImage("P1Bar", ovoPanel.transform, ovoBlueBarSprite);
        SetAnchors(ovoP1Bar, 0.08f, 0.87f, 0.40f, 0.97f);

        GameObject ovoP1Name = CreateText("P1Name", ovoP1Bar.transform, "Player 1", 16, TextAnchor.MiddleCenter);
        SetAnchors(ovoP1Name, 0.12f, 0.05f, 0.90f, 0.95f);
        ovoP1Name.GetComponent<Text>().color = Color.white;
        ovoP1Name.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // VS icon (top center)
        GameObject ovoVsIcon = CreateSpriteImage("VsIcon", ovoPanel.transform, ovoVsSprite);
        SetAnchors(ovoVsIcon, 0.40f, 0.84f, 0.60f, 1.03f);
        ovoVsIcon.GetComponent<Image>().raycastTarget = false;

        // Player 2 name bar (top right)
        GameObject ovoP2Bar = CreateSpriteImage("P2Bar", ovoPanel.transform, ovoRedBarSprite);
        SetAnchors(ovoP2Bar, 0.60f, 0.87f, 0.92f, 0.97f);

        GameObject ovoP2Name = CreateText("P2Name", ovoP2Bar.transform, "Player 2", 16, TextAnchor.MiddleCenter);
        SetAnchors(ovoP2Name, 0.10f, 0.05f, 0.88f, 0.95f);
        ovoP2Name.GetComponent<Text>().color = Color.white;
        ovoP2Name.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Blue polygon (left) — shifted down to leave room for brain icon above
        Sprite polyBlueSprite = LoadSprite("polygon_blue");
        GameObject polyBlue = CreateSpriteImage("PolygonBlue", ovoPanel.transform, polyBlueSprite);
        SetAnchors(polyBlue, 0.16f, 0.24f, 0.37f, 0.56f);

        // Player 1 score brain icon — placed above the blue polygon, no overlap
        Sprite brainSprite = LoadSprite("brain_icon");
        GameObject p1BrainIcon = CreateSpriteImage("P1BrainIcon", ovoPanel.transform, brainSprite);
        SetAnchors(p1BrainIcon, 0.21f, 0.58f, 0.32f, 0.78f);

        GameObject ovoP1Score = CreateText("P1Score", p1BrainIcon.transform, "0", 28, TextAnchor.MiddleCenter);
        SetAnchors(ovoP1Score, 0.10f, 0.10f, 0.90f, 0.80f);
        ovoP1Score.GetComponent<Text>().color = Color.white;
        ovoP1Score.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Pink polygon (right) — shifted down
        Sprite polyPinkSprite = LoadSprite("polygon_pink");
        GameObject polyPink = CreateSpriteImage("PolygonPink", ovoPanel.transform, polyPinkSprite);
        SetAnchors(polyPink, 0.63f, 0.24f, 0.84f, 0.56f);

        // Player 2 score brain icon — placed above the pink polygon
        GameObject p2BrainIcon = CreateSpriteImage("P2BrainIcon", ovoPanel.transform, brainSprite);
        SetAnchors(p2BrainIcon, 0.68f, 0.58f, 0.79f, 0.78f);

        GameObject ovoP2Score = CreateText("P2Score", p2BrainIcon.transform, "0", 28, TextAnchor.MiddleCenter);
        SetAnchors(ovoP2Score, 0.10f, 0.10f, 0.90f, 0.80f);
        ovoP2Score.GetComponent<Text>().color = Color.white;
        ovoP2Score.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Winner text (1v1) — only visible on tie
        GameObject ovoWinner = CreateText("WinnerText", ovoPanel.transform, "", 32, TextAnchor.MiddleCenter);
        SetAnchors(ovoWinner, 0.25f, 0.12f, 0.75f, 0.22f);
        ovoWinner.GetComponent<Text>().color = new Color(1f, 0.85f, 0.2f, 1f);
        ovoWinner.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Cup icon (1v1) — 50% smaller, moves to winner side (outer corner)
        Sprite ovoCupSprite = LoadSprite("cup_icon");
        GameObject ovoCup = CreateSpriteImage("OvoCupIcon", ovoPanel.transform, ovoCupSprite);
        RectTransform ovoCupRT = ovoCup.GetComponent<RectTransform>();
        ovoCupRT.anchorMin = new Vector2(0.02f, 0.66f);
        ovoCupRT.anchorMax = new Vector2(0.12f, 0.79f);
        ovoCupRT.offsetMin = Vector2.zero; ovoCupRT.offsetMax = Vector2.zero;
        ovoCup.GetComponent<Image>().raycastTarget = false;

        // Winner badge (1v1) — text with bounce near winner polygon
        GameObject ovoBadge = new GameObject("WinnerBadge", typeof(RectTransform), typeof(Image));
        ovoBadge.transform.SetParent(ovoPanel.transform, false);
        Image ovoBadgeBg = ovoBadge.GetComponent<Image>();
        ovoBadgeBg.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        ovoBadgeBg.raycastTarget = false;
        RectTransform ovoBadgeRT = ovoBadge.GetComponent<RectTransform>();
        ovoBadgeRT.anchorMin = new Vector2(0.08f, 0.78f);
        ovoBadgeRT.anchorMax = new Vector2(0.42f, 0.86f);
        ovoBadgeRT.offsetMin = Vector2.zero; ovoBadgeRT.offsetMax = Vector2.zero;

        GameObject ovoBadgeText = CreateText("BadgeText", ovoBadge.transform, "CHIẾN THẮNG!", 22, TextAnchor.MiddleCenter);
        SetAnchors(ovoBadgeText, 0f, 0f, 1f, 1f);
        ovoBadgeText.GetComponent<Text>().color = new Color(0.35f, 0.18f, 0.05f, 1f);
        ovoBadgeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        ovoBadgeText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        ovoBadgeText.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;

        // ============================================================
        // TEAM MODE PANEL
        // ============================================================
        GameObject teamPanel = new GameObject("TeamPanel", typeof(RectTransform));
        teamPanel.transform.SetParent(canvasGo.transform, false);
        SetAnchors(teamPanel, 0, 0, 1, 1);

        // Team mode background (editor-adjusted)
        Sprite bgTeamSprite = LoadSprite("bg_team_mode");
        GameObject teamBg = CreateSpriteImage("Background", teamPanel.transform, bgTeamSprite);
        RectTransform tbgRT = teamBg.GetComponent<RectTransform>();
        tbgRT.anchorMin = Vector2.zero; tbgRT.anchorMax = Vector2.one;
        tbgRT.anchoredPosition = new Vector2(-0.0002f, -5.66f); tbgRT.sizeDelta = new Vector2(65.05f, 22.63f);
        teamBg.GetComponent<Image>().raycastTarget = false;

        // Confetti (editor-adjusted)
        Sprite confettiSprite = LoadSprite("confetti");
        GameObject confetti = CreateSpriteImage("Confetti", teamPanel.transform, confettiSprite);
        RectTransform cfRT = confetti.GetComponent<RectTransform>();
        cfRT.anchorMin = new Vector2(0f, 0.15f); cfRT.anchorMax = new Vector2(1f, 0.95f);
        cfRT.anchoredPosition = new Vector2(-14.85f, 0f); cfRT.sizeDelta = new Vector2(-29.70f, 0f);
        confetti.GetComponent<Image>().raycastTarget = false;

        // Team banner (reuse TeamSelect sprites)
        Sprite blueBarSprite = LoadSpriteFrom(TeamSpriteFolder, "TeamBlueBar");
        Sprite redBarSprite = LoadSpriteFrom(TeamSpriteFolder, "TeamRedBar");
        Sprite vsSprite = LoadSpriteFrom(TeamSpriteFolder, "VsButton");

        // Blue team bar (top left)
        GameObject blueBar = CreateSpriteImage("BlueTeamBar", teamPanel.transform, blueBarSprite);
        SetAnchors(blueBar, 0.08f, 0.87f, 0.40f, 0.97f);

        GameObject teamBlueName = CreateText("BlueTeamName", blueBar.transform, "To 1", 16, TextAnchor.MiddleCenter);
        SetAnchors(teamBlueName, 0.12f, 0.05f, 0.40f, 0.95f);
        teamBlueName.GetComponent<Text>().color = Color.white;
        teamBlueName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        GameObject blueAvatarContainer = new GameObject("BlueAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        blueAvatarContainer.transform.SetParent(blueBar.transform, false);
        SetAnchors(blueAvatarContainer, 0.38f, 0.02f, 0.95f, 0.98f);
        var blueLayout = blueAvatarContainer.GetComponent<GridLayoutGroup>();
        blueLayout.cellSize = new Vector2(30f, 30f);
        blueLayout.spacing = new Vector2(2f, 1f);
        blueLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        blueLayout.constraintCount = 3;
        blueLayout.childAlignment = TextAnchor.MiddleCenter;

        // VS icon (center top)
        GameObject vsIcon = CreateSpriteImage("VsIcon", teamPanel.transform, vsSprite);
        SetAnchors(vsIcon, 0.40f, 0.84f, 0.60f, 1.03f);
        vsIcon.GetComponent<Image>().raycastTarget = false;

        // Red team bar (top right)
        GameObject redBar = CreateSpriteImage("RedTeamBar", teamPanel.transform, redBarSprite);
        SetAnchors(redBar, 0.60f, 0.87f, 0.92f, 0.97f);

        GameObject redAvatarContainer = new GameObject("RedAvatarContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        redAvatarContainer.transform.SetParent(redBar.transform, false);
        SetAnchors(redAvatarContainer, 0.05f, 0.02f, 0.62f, 0.98f);
        var redLayout = redAvatarContainer.GetComponent<GridLayoutGroup>();
        redLayout.cellSize = new Vector2(30f, 30f);
        redLayout.spacing = new Vector2(2f, 1f);
        redLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        redLayout.constraintCount = 3;
        redLayout.childAlignment = TextAnchor.MiddleCenter;

        GameObject teamRedName = CreateText("RedTeamName", redBar.transform, "To 2", 16, TextAnchor.MiddleCenter);
        SetAnchors(teamRedName, 0.60f, 0.05f, 0.88f, 0.95f);
        teamRedName.GetComponent<Text>().color = Color.white;
        teamRedName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Cup icon (editor-adjusted)
        Sprite cupSprite = LoadSprite("cup_icon");
        GameObject cupIcon = CreateSpriteImage("CupIcon", teamPanel.transform, cupSprite);
        RectTransform cupRT = cupIcon.GetComponent<RectTransform>();
        cupRT.anchorMin = new Vector2(0.38f, 0.62f); cupRT.anchorMax = new Vector2(0.62f, 0.88f);
        cupRT.anchoredPosition = new Vector2(-101f, -49f); cupRT.sizeDelta = new Vector2(-159.80f, -67.88f);

        // Blue score star (editor-adjusted)
        Sprite starSprite = LoadSprite("big_star");
        GameObject blueStar = CreateSpriteImage("BlueStar", teamPanel.transform, starSprite);
        RectTransform bsRT = blueStar.GetComponent<RectTransform>();
        bsRT.anchorMin = new Vector2(0.10f, 0.15f); bsRT.anchorMax = new Vector2(0.50f, 0.78f);
        bsRT.anchoredPosition = new Vector2(9f, 35f); bsRT.sizeDelta = new Vector2(-211.43f, -192.46f);

        GameObject blueScoreText = CreateText("BlueScore", blueStar.transform, "0", 56, TextAnchor.MiddleCenter);
        SetAnchors(blueScoreText, 0.15f, 0.18f, 0.85f, 0.72f);
        blueScoreText.GetComponent<Text>().color = new Color(0.5f, 0.3f, 0.1f, 1f);
        blueScoreText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Red score star (editor-adjusted)
        GameObject redStar = CreateSpriteImage("RedStar", teamPanel.transform, starSprite);
        RectTransform rsRT = redStar.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(0.50f, 0.15f); rsRT.anchorMax = new Vector2(0.90f, 0.78f);
        rsRT.anchoredPosition = new Vector2(-48.41f, 43.18f); rsRT.sizeDelta = new Vector2(-227.81f, -176.11f);

        GameObject redScoreText = CreateText("RedScore", redStar.transform, "0", 56, TextAnchor.MiddleCenter);
        SetAnchors(redScoreText, 0.15f, 0.18f, 0.85f, 0.72f);
        redScoreText.GetComponent<Text>().color = new Color(0.5f, 0.3f, 0.1f, 1f);
        redScoreText.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Vertical left/right bars removed per design request

        // Avatar slot template (hidden)
        GameObject avatarTemplate = new GameObject("AvatarSlotTemplate", typeof(RectTransform), typeof(Image));
        avatarTemplate.transform.SetParent(canvasGo.transform, false);
        avatarTemplate.GetComponent<Image>().color = new Color(0.7f, 0.85f, 1f, 1f);
        avatarTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 36f);
        GameObject slotText = CreateText("Initial", avatarTemplate.transform, "?", 18, TextAnchor.MiddleCenter);
        SetAnchors(slotText, 0, 0, 1, 1);
        slotText.GetComponent<Text>().color = Color.white;
        slotText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        avatarTemplate.SetActive(false);

        // ============================================================
        // SHARED BUTTONS (on top of both panels)
        // ============================================================
        Sprite replaySprite = LoadSprite("btn_replay");

        // Replay button (left) — "Choi lai"
        GameObject replayButton = CreateSpriteButton("ReplayButton", canvasGo.transform, replaySprite);
        SetAnchors(replayButton, 0.12f, 0.02f, 0.38f, 0.13f);

        // Change team button (right) — "Doi doi"
        Sprite changeTeamSprite = LoadSpriteFrom("Assets/Game/Textures/MenuScene", "btn_start");
        GameObject changeTeamButton = CreateSpriteButton("ChangeTeamButton", canvasGo.transform, changeTeamSprite);
        SetAnchors(changeTeamButton, 0.62f, 0.02f, 0.88f, 0.13f);

        // ============================================================
        // WIRE REFERENCES
        // ============================================================
        SerializedObject viewSo = new SerializedObject(view);

        // Mode panels
        viewSo.FindProperty("oneVsOnePanel").objectReferenceValue = ovoPanel;
        viewSo.FindProperty("teamPanel").objectReferenceValue = teamPanel;

        // 1v1 fields
        viewSo.FindProperty("ovoPlayer1NameText").objectReferenceValue = ovoP1Name.GetComponent<Text>();
        viewSo.FindProperty("ovoPlayer1ScoreText").objectReferenceValue = ovoP1Score.GetComponent<Text>();
        viewSo.FindProperty("ovoPlayer2NameText").objectReferenceValue = ovoP2Name.GetComponent<Text>();
        viewSo.FindProperty("ovoPlayer2ScoreText").objectReferenceValue = ovoP2Score.GetComponent<Text>();
        viewSo.FindProperty("ovoWinnerText").objectReferenceValue = ovoWinner.GetComponent<Text>();
        viewSo.FindProperty("ovoConfetti").objectReferenceValue = ovoConfetti;
        viewSo.FindProperty("ovoCupIcon").objectReferenceValue = ovoCup.GetComponent<Image>();
        viewSo.FindProperty("ovoP1Polygon").objectReferenceValue = polyBlue.GetComponent<RectTransform>();
        viewSo.FindProperty("ovoP2Polygon").objectReferenceValue = polyPink.GetComponent<RectTransform>();
        viewSo.FindProperty("ovoWinnerBadge").objectReferenceValue = ovoBadge;
        viewSo.FindProperty("ovoWinnerBadgeText").objectReferenceValue = ovoBadgeText.GetComponent<Text>();

        // Team fields
        viewSo.FindProperty("teamBlueNameText").objectReferenceValue = teamBlueName.GetComponent<Text>();
        viewSo.FindProperty("teamRedNameText").objectReferenceValue = teamRedName.GetComponent<Text>();
        viewSo.FindProperty("teamBlueScoreText").objectReferenceValue = blueScoreText.GetComponent<Text>();
        viewSo.FindProperty("teamRedScoreText").objectReferenceValue = redScoreText.GetComponent<Text>();
        viewSo.FindProperty("teamBlueAvatarContainer").objectReferenceValue = blueAvatarContainer.transform;
        viewSo.FindProperty("teamRedAvatarContainer").objectReferenceValue = redAvatarContainer.transform;
        viewSo.FindProperty("teamAvatarSlotTemplate").objectReferenceValue = avatarTemplate;
        viewSo.FindProperty("teamCupIcon").objectReferenceValue = cupIcon.GetComponent<Image>();
        viewSo.FindProperty("teamConfetti").objectReferenceValue = confetti;
        viewSo.FindProperty("teamBlueStar").objectReferenceValue = blueStar.GetComponent<RectTransform>();
        viewSo.FindProperty("teamRedStar").objectReferenceValue = redStar.GetComponent<RectTransform>();

        // Avatar bg + character sprites
        viewSo.FindProperty("avatarBgBlue").objectReferenceValue = LoadSpriteFrom("Assets/Game/Textures/TeamSelectScene", "AvatarBgBlue");
        viewSo.FindProperty("avatarBgRed").objectReferenceValue = LoadSpriteFrom("Assets/Game/Textures/TeamSelectScene", "AvatarBgRed");
        viewSo.FindProperty("charBodySprite").objectReferenceValue = LoadSpriteFrom("Assets/Game/Textures/PlayerPanel", "body_male");
        SerializedProperty hairArr = viewSo.FindProperty("charHairSprites");
        hairArr.arraySize = 6;
        for (int i = 0; i < 6; i++)
            hairArr.GetArrayElementAtIndex(i).objectReferenceValue = LoadSpriteFrom("Assets/Game/Textures/PlayerPanel", "char_hair_" + (i + 1));

        // Shared buttons
        viewSo.FindProperty("replayButton").objectReferenceValue = replayButton.GetComponent<Button>();
        viewSo.FindProperty("changeTeamButton").objectReferenceValue = changeTeamButton.GetComponent<Button>();

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // Controller
        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("scoreSceneView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Helpers =====

    private static Sprite LoadSprite(string name)
    {
        return LoadSpriteFrom(SpriteFolder, name);
    }

    private static Sprite LoadSpriteFrom(string folder, string name)
    {
        string path = folder + "/" + name + ".png";
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
            if (sprite == null)
                Debug.LogWarning("ScoreSceneBuilder: Sprite not found: " + path);
        }
        return sprite;
    }

    private static GameObject CreateSpriteImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
        img.color = Color.white;
        return go;
    }

    private static GameObject CreateSpriteButton(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
        img.color = Color.white;
        return go;
    }

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera cam = cameraGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.96f, 0.91f, 0.82f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
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
            if (scenes[i].path == targetPath) return;
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
        newScenes[scenes.Length] = new EditorBuildSettingsScene(targetPath, true);
        EditorBuildSettings.scenes = newScenes;
    }
}
