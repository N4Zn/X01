using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Programmatic scene builder for PathFinder game (maze-based).
/// Split-screen 2-player layout (1024x600).
/// Each player has: maze container (runtime cells), prompt text, 3 direction buttons, feedback.
/// </summary>
public static class PathFinderSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/PathFinderGame";
    private const string ScenePath = "Assets/Game/Scenes/PathFinderGame/PathFinderGame.unity";

    [MenuItem("Tools/PathFinder/Build Scene")]
    public static void BuildScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("PathFinderSceneBuilder: Cannot build scene while in Play mode.");
            return;
        }
        EnsureFolders();
        CreateScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PathFinderSceneBuilder: finished generating PathFinderGame scene.");
    }

    private static void CreateScene()
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

        // Root
        GameObject root = new GameObject("PathFinderRoot");
        PathFinderGameController controller = root.AddComponent<PathFinderGameController>();
        PathFinderGameView view = root.AddComponent<PathFinderGameView>();

        // Background
        GameObject bg = CreatePanel("Background", canvasGo.transform, new Color(0.18f, 0.30f, 0.20f, 1f));
        SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Timer (top center)
        GameObject timerBg = CreatePanel("TimerBg", canvasGo.transform, new Color(0.15f, 0.15f, 0.25f, 0.9f));
        SetRect(timerBg.GetComponent<RectTransform>(), new Vector2(0.44f, 0.90f), new Vector2(0.56f, 0.98f), Vector2.zero, Vector2.zero);
        GameObject timerText = CreateText("TimerText", timerBg.transform, "100", 28, TextAnchor.MiddleCenter);
        SetRect(timerText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        timerText.GetComponent<Text>().color = Color.white;

        // Back button (top-left)
        GameObject backButton = CreateStyledButton("BackButton", canvasGo.transform, "\u2190",
            new Vector2(0.01f, 0.91f), new Vector2(0.06f, 0.98f),
            new Color(0.4f, 0.4f, 0.5f, 0.8f), Color.white, 22);

        // Center divider
        GameObject divider = CreatePanel("Divider", canvasGo.transform, new Color(0.3f, 0.3f, 0.4f, 0.8f));
        SetRect(divider.GetComponent<RectTransform>(), new Vector2(0.495f, 0f), new Vector2(0.505f, 0.90f), Vector2.zero, Vector2.zero);

        // Star column (center)
        GameObject starColumn = CreatePanel("StarColumn", canvasGo.transform, new Color(0.2f, 0.2f, 0.3f, 0.6f));
        SetRect(starColumn.GetComponent<RectTransform>(), new Vector2(0.46f, 0.30f), new Vector2(0.54f, 0.88f), Vector2.zero, Vector2.zero);

        GameObject p1StarText = CreateText("P1StarText", starColumn.transform, "\u2605 0", 14, TextAnchor.MiddleCenter);
        SetRect(p1StarText.GetComponent<RectTransform>(), new Vector2(0f, 0.55f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
        p1StarText.GetComponent<Text>().color = new Color(0.4f, 0.7f, 1f, 1f);
        p1StarText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        GameObject p2StarText = CreateText("P2StarText", starColumn.transform, "\u2605 0", 14, TextAnchor.MiddleCenter);
        SetRect(p2StarText.GetComponent<RectTransform>(), new Vector2(0f, 0.05f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);
        p2StarText.GetComponent<Text>().color = new Color(1f, 0.5f, 0.5f, 1f);
        p2StarText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        // ===== Player Panels =====
        GameObject p1Name, p1Score, p1MazeContainer, p1Prompt;
        GameObject p1OptL, p1OptS, p1OptR;
        GameObject p1BorderL, p1BorderS, p1BorderR;
        GameObject p1FeedbackOk, p1FeedbackBad, p1Countdown;
        BuildPlayerPanel(canvasGo.transform, 0, new Vector2(0f, 0f), new Vector2(0.49f, 0.90f),
            out p1Name, out p1Score, out p1MazeContainer, out p1Prompt,
            out p1OptL, out p1OptS, out p1OptR,
            out p1BorderL, out p1BorderS, out p1BorderR,
            out p1FeedbackOk, out p1FeedbackBad, out p1Countdown);

        GameObject p2Name, p2Score, p2MazeContainer, p2Prompt;
        GameObject p2OptL, p2OptS, p2OptR;
        GameObject p2BorderL, p2BorderS, p2BorderR;
        GameObject p2FeedbackOk, p2FeedbackBad, p2Countdown;
        BuildPlayerPanel(canvasGo.transform, 1, new Vector2(0.51f, 0f), new Vector2(1f, 0.90f),
            out p2Name, out p2Score, out p2MazeContainer, out p2Prompt,
            out p2OptL, out p2OptS, out p2OptR,
            out p2BorderL, out p2BorderS, out p2BorderR,
            out p2FeedbackOk, out p2FeedbackBad, out p2Countdown);

        // ===== Wire References =====
        WireReferences(view, controller,
            timerText, backButton,
            p1Name, p1Score, p1MazeContainer, p1Prompt,
            p1OptL, p1OptS, p1OptR,
            p1BorderL, p1BorderS, p1BorderR,
            p1FeedbackOk, p1FeedbackBad, p1Countdown,
            p2Name, p2Score, p2MazeContainer, p2Prompt,
            p2OptL, p2OptS, p2OptR,
            p2BorderL, p2BorderS, p2BorderR,
            p2FeedbackOk, p2FeedbackBad, p2Countdown,
            p1StarText, p2StarText);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
    }

    // ===== Build Player Panel =====

    private static void BuildPlayerPanel(Transform parent, int playerIndex,
        Vector2 panelMin, Vector2 panelMax,
        out GameObject nameText, out GameObject scoreText,
        out GameObject mazeContainer, out GameObject promptText,
        out GameObject optL, out GameObject optS, out GameObject optR,
        out GameObject borderL, out GameObject borderS, out GameObject borderR,
        out GameObject feedbackOk, out GameObject feedbackBad, out GameObject countdown)
    {
        string prefix = "P" + (playerIndex + 1);
        Color panelColor = playerIndex == 0
            ? new Color(0.12f, 0.18f, 0.30f, 0.6f)
            : new Color(0.30f, 0.12f, 0.15f, 0.6f);

        GameObject panel = CreatePanel(prefix + "Panel", parent, panelColor);
        SetRect(panel.GetComponent<RectTransform>(), panelMin, panelMax, Vector2.zero, Vector2.zero);

        // Name (top-left of panel)
        nameText = CreateText(prefix + "Name", panel.transform, "Player " + (playerIndex + 1), 18, TextAnchor.MiddleLeft);
        SetRect(nameText.GetComponent<RectTransform>(), new Vector2(0.03f, 0.90f), new Vector2(0.60f, 0.99f), Vector2.zero, Vector2.zero);
        nameText.GetComponent<Text>().color = Color.white;

        // Score (top-right of panel, star display)
        scoreText = CreateText(prefix + "Score", panel.transform, "\u2605 0", 60, TextAnchor.MiddleRight);
        SetRect(scoreText.GetComponent<RectTransform>(), new Vector2(0.45f, 0.84f), new Vector2(0.97f, 1.00f), Vector2.zero, Vector2.zero);
        scoreText.GetComponent<Text>().color = new Color(1f, 0.85f, 0.3f, 1f);
        scoreText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        scoreText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        scoreText.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;

        // Maze container (main visual area — cells created at runtime)
        mazeContainer = new GameObject(prefix + "MazeContainer", typeof(RectTransform));
        mazeContainer.transform.SetParent(panel.transform, false);
        SetRect(mazeContainer.GetComponent<RectTransform>(), new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);

        // Optional background for maze area
        GameObject mazeBg = CreatePanel(prefix + "MazeBg", mazeContainer.transform, new Color(0.15f, 0.20f, 0.30f, 0.8f));
        SetRect(mazeBg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Prompt text (above options)
        promptText = CreateText(prefix + "Prompt", panel.transform, "Ch\u1ecdn h\u01b0\u1edbng \u0111i \u0111\u1ec3 \u0111\u1ebfn \u2605", 14, TextAnchor.MiddleCenter);
        SetRect(promptText.GetComponent<RectTransform>(), new Vector2(0.05f, 0.23f), new Vector2(0.95f, 0.30f), Vector2.zero, Vector2.zero);
        promptText.GetComponent<Text>().color = new Color(0.9f, 0.9f, 0.7f, 1f);

        // Option buttons (bottom of panel): Left / Straight / Right
        float optY0 = 0.03f, optY1 = 0.22f;

        borderL = CreatePanel(prefix + "BorderLeft", panel.transform, new Color(0.5f, 0.5f, 0.55f, 1f));
        SetRect(borderL.GetComponent<RectTransform>(), new Vector2(0.03f, optY0), new Vector2(0.32f, optY1), Vector2.zero, Vector2.zero);
        optL = CreateStyledButton(prefix + "OptLeft", borderL.transform, "\u2190 Tr\u00e1i",
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f),
            new Color(0.25f, 0.35f, 0.50f, 1f), Color.white, 16);

        borderS = CreatePanel(prefix + "BorderStraight", panel.transform, new Color(0.5f, 0.5f, 0.55f, 1f));
        SetRect(borderS.GetComponent<RectTransform>(), new Vector2(0.34f, optY0), new Vector2(0.66f, optY1), Vector2.zero, Vector2.zero);
        optS = CreateStyledButton(prefix + "OptStraight", borderS.transform, "\u2191 Th\u1eb3ng",
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f),
            new Color(0.25f, 0.35f, 0.50f, 1f), Color.white, 16);

        borderR = CreatePanel(prefix + "BorderRight", panel.transform, new Color(0.5f, 0.5f, 0.55f, 1f));
        SetRect(borderR.GetComponent<RectTransform>(), new Vector2(0.68f, optY0), new Vector2(0.97f, optY1), Vector2.zero, Vector2.zero);
        optR = CreateStyledButton(prefix + "OptRight", borderR.transform, "\u2192 Ph\u1ea3i",
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f),
            new Color(0.25f, 0.35f, 0.50f, 1f), Color.white, 16);

        // Feedback icons (circle + ✓/✗, overlaid on maze area)
        feedbackOk = FeedbackIconBuilder.Create(prefix + "CorrectIcon", panel.transform, true, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f));
        feedbackBad = FeedbackIconBuilder.Create(prefix + "WrongIcon", panel.transform, false, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f));

        // Countdown text (Team mode)
        countdown = CreateText(prefix + "Countdown", panel.transform, "3", 72, TextAnchor.MiddleCenter);
        Text cdText = countdown.GetComponent<Text>();
        cdText.color = new Color(1f, 1f, 1f, 0.9f);
        cdText.fontStyle = FontStyle.Bold;
        Outline cdOutline = countdown.AddComponent<Outline>();
        cdOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        cdOutline.effectDistance = new Vector2(2f, -2f);
        SetRect(countdown.GetComponent<RectTransform>(), new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero);
        countdown.SetActive(false);
    }

    // ===== Wire References =====

    private static void WireReferences(PathFinderGameView view, PathFinderGameController controller,
        GameObject timerText, GameObject backButton,
        GameObject p1Name, GameObject p1Score, GameObject p1MazeContainer, GameObject p1Prompt,
        GameObject p1OptL, GameObject p1OptS, GameObject p1OptR,
        GameObject p1BorderL, GameObject p1BorderS, GameObject p1BorderR,
        GameObject p1FeedbackOk, GameObject p1FeedbackBad, GameObject p1Countdown,
        GameObject p2Name, GameObject p2Score, GameObject p2MazeContainer, GameObject p2Prompt,
        GameObject p2OptL, GameObject p2OptS, GameObject p2OptR,
        GameObject p2BorderL, GameObject p2BorderS, GameObject p2BorderR,
        GameObject p2FeedbackOk, GameObject p2FeedbackBad, GameObject p2Countdown,
        GameObject p1StarText, GameObject p2StarText)
    {
        SerializedObject viewSo = new SerializedObject(view);

        viewSo.FindProperty("timerText").objectReferenceValue = timerText.GetComponent<Text>();
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();

        // P1
        viewSo.FindProperty("p1NameText").objectReferenceValue = p1Name.GetComponent<Text>();
        viewSo.FindProperty("p1ScoreText").objectReferenceValue = p1Score.GetComponent<Text>();
        viewSo.FindProperty("p1MazeContainer").objectReferenceValue = p1MazeContainer.GetComponent<RectTransform>();
        viewSo.FindProperty("p1PromptText").objectReferenceValue = p1Prompt.GetComponent<Text>();
        viewSo.FindProperty("p1OptionLeft").objectReferenceValue = p1OptL.GetComponent<Button>();
        viewSo.FindProperty("p1OptionStraight").objectReferenceValue = p1OptS.GetComponent<Button>();
        viewSo.FindProperty("p1OptionRight").objectReferenceValue = p1OptR.GetComponent<Button>();
        viewSo.FindProperty("p1OptionLeftBorder").objectReferenceValue = p1BorderL.GetComponent<Image>();
        viewSo.FindProperty("p1OptionStraightBorder").objectReferenceValue = p1BorderS.GetComponent<Image>();
        viewSo.FindProperty("p1OptionRightBorder").objectReferenceValue = p1BorderR.GetComponent<Image>();
        viewSo.FindProperty("p1CorrectIcon").objectReferenceValue = p1FeedbackOk;
        viewSo.FindProperty("p1WrongIcon").objectReferenceValue = p1FeedbackBad;
        viewSo.FindProperty("p1CountdownText").objectReferenceValue = p1Countdown.GetComponent<Text>();

        // P2
        viewSo.FindProperty("p2NameText").objectReferenceValue = p2Name.GetComponent<Text>();
        viewSo.FindProperty("p2ScoreText").objectReferenceValue = p2Score.GetComponent<Text>();
        viewSo.FindProperty("p2MazeContainer").objectReferenceValue = p2MazeContainer.GetComponent<RectTransform>();
        viewSo.FindProperty("p2PromptText").objectReferenceValue = p2Prompt.GetComponent<Text>();
        viewSo.FindProperty("p2OptionLeft").objectReferenceValue = p2OptL.GetComponent<Button>();
        viewSo.FindProperty("p2OptionStraight").objectReferenceValue = p2OptS.GetComponent<Button>();
        viewSo.FindProperty("p2OptionRight").objectReferenceValue = p2OptR.GetComponent<Button>();
        viewSo.FindProperty("p2OptionLeftBorder").objectReferenceValue = p2BorderL.GetComponent<Image>();
        viewSo.FindProperty("p2OptionStraightBorder").objectReferenceValue = p2BorderS.GetComponent<Image>();
        viewSo.FindProperty("p2OptionRightBorder").objectReferenceValue = p2BorderR.GetComponent<Image>();
        viewSo.FindProperty("p2CorrectIcon").objectReferenceValue = p2FeedbackOk;
        viewSo.FindProperty("p2WrongIcon").objectReferenceValue = p2FeedbackBad;
        viewSo.FindProperty("p2CountdownText").objectReferenceValue = p2Countdown.GetComponent<Text>();

        // Stars
        viewSo.FindProperty("p1StarText").objectReferenceValue = p1StarText.GetComponent<Text>();
        viewSo.FindProperty("p2StarText").objectReferenceValue = p2StarText.GetComponent<Text>();

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // Controller
        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("gameView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===== Helpers =====

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.18f, 0.25f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
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

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
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
}
