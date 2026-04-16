using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Editor tool to add a TutorialPanel to any game scene.
/// Creates the full UI hierarchy and wires it to the scene's controller.
/// </summary>
public static class TutorialPanelBuilder
{
    [MenuItem("Tools/Tutorial/Add TutorialPanel to AddUp Scene")]
    public static void AddToAddUpScene()
    {
        AddToScene(
            "Assets/Game/Scenes/AddUpGame/AddUpGame.unity",
            typeof(AddUpGameController),
            "Hướng dẫn: AddUp"
        );
    }

    [MenuItem("Tools/Tutorial/Add TutorialPanel to TrainPath Scene")]
    public static void AddToTrainPathScene()
    {
        AddToScene(
            "Assets/Game/Scenes/TrainPathGame/TrainPathGame.unity",
            typeof(TrainPathGameController),
            "Hướng dẫn: TrainPath"
        );
    }

    [MenuItem("Tools/Tutorial/Add TutorialPanel to PathFinder Scene")]
    public static void AddToPathFinderScene()
    {
        AddToScene(
            "Assets/Game/Scenes/PathFinderGame/PathFinderGame.unity",
            typeof(PathFinderGameController),
            "Hướng dẫn: PathFinder"
        );
    }

    [MenuItem("Tools/Tutorial/Add TutorialPanel to ALL Game Scenes")]
    public static void AddToAllScenes()
    {
        AddToAddUpScene();
        AddToTrainPathScene();
        AddToPathFinderScene();
        Debug.Log("TutorialPanelBuilder: Added TutorialPanel to all game scenes.");
    }

    [MenuItem("Tools/Tutorial/Add TutorialPanel to Current Scene")]
    public static void AddToCurrentScene()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("TutorialPanelBuilder: No Canvas found in current scene.");
            return;
        }

        // Try to find any game controller
        MonoBehaviour controller = Object.FindObjectOfType<AddUpGameController>() as MonoBehaviour;
        string controllerProp = "tutorialPanel";
        System.Type controllerType = null;

        if (controller == null) controller = Object.FindObjectOfType<TrainPathGameController>();
        if (controller == null) controller = Object.FindObjectOfType<PathFinderGameController>();

        if (controller != null) controllerType = controller.GetType();

        GameObject panelGo = BuildTutorialPanelUI(canvas.transform, "Hướng dẫn");
        TutorialPanel panel = panelGo.GetComponent<TutorialPanel>();

        if (controller != null)
        {
            WireTutorialPanel(controller, panel);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("TutorialPanelBuilder: Added TutorialPanel to current scene.");
    }

    private static void AddToScene(string scenePath, System.Type controllerType, string title)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Check if TutorialPanel already exists
        TutorialPanel existing = Object.FindObjectOfType<TutorialPanel>();
        if (existing != null)
        {
            Debug.Log($"TutorialPanelBuilder: TutorialPanel already exists in {scenePath}. Skipping.");
            return;
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"TutorialPanelBuilder: No Canvas found in {scenePath}.");
            return;
        }

        GameObject panelGo = BuildTutorialPanelUI(canvas.transform, title);
        TutorialPanel panel = panelGo.GetComponent<TutorialPanel>();

        // Find controller and wire up
        MonoBehaviour controller = Object.FindObjectOfType(controllerType) as MonoBehaviour;
        if (controller != null)
        {
            WireTutorialPanel(controller, panel);
        }
        else
        {
            Debug.LogWarning($"TutorialPanelBuilder: Controller {controllerType.Name} not found in {scenePath}.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"TutorialPanelBuilder: Added TutorialPanel to {scenePath}.");
    }

    /// <summary>
    /// Build the TutorialPanel UI hierarchy under the given parent (Canvas).
    /// Layout: Full-screen overlay with dark background, video area, title, and Start button.
    /// </summary>
    public static GameObject BuildTutorialPanelUI(Transform canvasParent, string title)
    {
        // Root object with RectTransform + TutorialPanel component
        // Must be last sibling to render on top of game UI
        GameObject root = new GameObject("TutorialPanelRoot", typeof(RectTransform));
        root.transform.SetParent(canvasParent, false);
        root.transform.SetAsLastSibling();
        SetRectFull(root.GetComponent<RectTransform>());
        TutorialPanel tutorialPanel = root.AddComponent<TutorialPanel>();

        // PanelRoot — full screen dark overlay
        GameObject panelRoot = CreateImage("PanelRoot", root.transform, new Color(0f, 0f, 0f, 0.85f));
        SetRectFull(panelRoot.GetComponent<RectTransform>());

        // Content container — centered
        GameObject content = CreateImage("Content", panelRoot.transform, new Color(0.15f, 0.2f, 0.3f, 0.95f));
        SetRect(content.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f),
            Vector2.zero, Vector2.zero);

        // Title text
        GameObject titleGo = CreateText("TitleText", content.transform, title, 32, TextAnchor.MiddleCenter);
        SetRect(titleGo.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f),
            Vector2.zero, Vector2.zero);
        titleGo.GetComponent<Text>().color = Color.white;
        titleGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Video display area (RawImage)
        GameObject videoArea = new GameObject("VideoDisplay", typeof(RectTransform), typeof(RawImage));
        videoArea.transform.SetParent(content.transform, false);
        SetRect(videoArea.GetComponent<RectTransform>(),
            new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.82f),
            Vector2.zero, Vector2.zero);
        RawImage rawImage = videoArea.GetComponent<RawImage>();
        rawImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // placeholder dark

        // VideoPlayer (on same object as RawImage)
        VideoPlayer videoPlayer = videoArea.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        // AudioSource for video sound
        AudioSource audioSource = videoArea.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Start Game button
        GameObject startBtn = CreateButton("StartButton", content.transform, "BẮT ĐẦU",
            new Vector2(0.3f, 0.03f), new Vector2(0.7f, 0.16f),
            new Color(0.2f, 0.75f, 0.3f, 1f));
        startBtn.transform.Find("Text").GetComponent<Text>().color = Color.white;
        startBtn.transform.Find("Text").GetComponent<Text>().fontStyle = FontStyle.Bold;
        startBtn.transform.Find("Text").GetComponent<Text>().fontSize = 30;

        // Wire TutorialPanel serialized fields
        SerializedObject so = new SerializedObject(tutorialPanel);
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
        so.FindProperty("videoDisplay").objectReferenceValue = rawImage;
        so.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
        so.FindProperty("audioSource").objectReferenceValue = audioSource;
        so.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        so.FindProperty("titleText").objectReferenceValue = titleGo.GetComponent<Text>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden
        panelRoot.SetActive(false);

        return root;
    }

    private static void WireTutorialPanel(MonoBehaviour controller, TutorialPanel panel)
    {
        SerializedObject controllerSo = new SerializedObject(controller);
        SerializedProperty prop = controllerSo.FindProperty("tutorialPanel");
        if (prop != null)
        {
            prop.objectReferenceValue = panel;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning($"TutorialPanelBuilder: 'tutorialPanel' field not found on {controller.GetType().Name}.");
        }
    }

    // ===== UI Helpers =====

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
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        btnGo.GetComponent<Image>().color = bgColor;
        SetRect(btnGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        GameObject textGo = CreateText("Text", btnGo.transform, label, 28, TextAnchor.MiddleCenter);
        SetRectFull(textGo.GetComponent<RectTransform>());

        return btnGo;
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetRectFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
