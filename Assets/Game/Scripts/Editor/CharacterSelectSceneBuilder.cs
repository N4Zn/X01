using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterSelectSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/CharacterSelectScene";
    private const string ScenePath = "Assets/Game/Scenes/CharacterSelectScene/CharacterSelectScene.unity";

    [MenuItem("Tools/CharacterSelectScene/Build Character Select Scene")]
    public static void BuildCharacterSelectScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("CharacterSelectSceneBuilder: Cannot build scene while in Play mode. Please exit Play mode first.");
            return;
        }
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CharacterSelectSceneBuilder: finished generating CharacterSelectScene.");
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
        GameObject root = new GameObject("CharacterSelectRoot");
        CharacterSelectController controller = root.AddComponent<CharacterSelectController>();
        CharacterSelectView view = root.AddComponent<CharacterSelectView>();

        // Background
        GameObject bgPanel = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bgPanel.transform.SetParent(canvasGo.transform, false);
        bgPanel.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.35f, 1f);
        SetRect(bgPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Title
        GameObject titleText = CreateText("TitleText", canvasGo.transform, "Choose Mode", 40, TextAnchor.MiddleCenter);
        SetRect(titleText.GetComponent<RectTransform>(), new Vector2(0.15f, 0.88f), new Vector2(0.85f, 0.98f), Vector2.zero, Vector2.zero);
        titleText.GetComponent<Text>().color = Color.white;

        // Back button (top-left)
        GameObject backButton = CreateStyledButton("BackButton", canvasGo.transform, "< Back",
            new Vector2(0.02f, 0.88f), new Vector2(0.13f, 0.98f),
            new Color(0.4f, 0.4f, 0.5f, 1f), Color.white, 18);

        // === Mode Select Panel ===
        GameObject modeSelectPanel = new GameObject("ModeSelectPanel", typeof(RectTransform));
        modeSelectPanel.transform.SetParent(canvasGo.transform, false);
        SetRect(modeSelectPanel.GetComponent<RectTransform>(), new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);

        GameObject oneVsOneButton = CreateStyledButton("OneVsOneButton", modeSelectPanel.transform, "1 vs 1",
            new Vector2(0.05f, 0.25f), new Vector2(0.45f, 0.75f),
            new Color(0.2f, 0.5f, 0.8f, 1f), Color.white, 40);

        GameObject teamModeButton = CreateStyledButton("TeamModeButton", modeSelectPanel.transform, "Team Mode",
            new Vector2(0.55f, 0.25f), new Vector2(0.95f, 0.75f),
            new Color(0.8f, 0.4f, 0.2f, 1f), Color.white, 36);

        // === Character Select Panel (hidden initially) ===
        GameObject characterSelectPanel = new GameObject("CharacterSelectPanel", typeof(RectTransform));
        characterSelectPanel.transform.SetParent(canvasGo.transform, false);
        SetRect(characterSelectPanel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.86f), Vector2.zero, Vector2.zero);

        // "Player X - Choose your character!" text
        GameObject selectingPlayerText = CreateText("SelectingPlayerText", characterSelectPanel.transform,
            "Player 1 - Choose your character!", 24, TextAnchor.MiddleCenter);
        SetRect(selectingPlayerText.GetComponent<RectTransform>(), new Vector2(0.15f, 0.88f), new Vector2(0.85f, 0.99f), Vector2.zero, Vector2.zero);
        selectingPlayerText.GetComponent<Text>().color = new Color(0.3f, 0.7f, 1f, 1f);

        // --- Player indicators (left and right of grid) ---
        // Player 1 indicator (left)
        GameObject p1Panel = new GameObject("P1Panel", typeof(RectTransform), typeof(Image));
        p1Panel.transform.SetParent(characterSelectPanel.transform, false);
        p1Panel.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.45f, 1f);
        SetRect(p1Panel.GetComponent<RectTransform>(), new Vector2(0.0f, 0.15f), new Vector2(0.13f, 0.87f), Vector2.zero, Vector2.zero);

        GameObject p1CharIcon = new GameObject("P1CharIcon", typeof(RectTransform), typeof(Image));
        p1CharIcon.transform.SetParent(p1Panel.transform, false);
        p1CharIcon.GetComponent<Image>().color = new Color(0.3f, 0.35f, 0.5f, 1f);
        SetRect(p1CharIcon.GetComponent<RectTransform>(), new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);

        GameObject p1CharLabel = CreateText("P1CharLabel", p1Panel.transform, "P1: ?", 16, TextAnchor.MiddleCenter);
        SetRect(p1CharLabel.GetComponent<RectTransform>(), new Vector2(0.0f, 0.05f), new Vector2(1.0f, 0.4f), Vector2.zero, Vector2.zero);
        p1CharLabel.GetComponent<Text>().color = new Color(0.3f, 0.7f, 1f, 1f);

        // Player 2 indicator (right)
        GameObject p2Panel = new GameObject("P2Panel", typeof(RectTransform), typeof(Image));
        p2Panel.transform.SetParent(characterSelectPanel.transform, false);
        p2Panel.GetComponent<Image>().color = new Color(0.45f, 0.2f, 0.15f, 1f);
        SetRect(p2Panel.GetComponent<RectTransform>(), new Vector2(0.87f, 0.15f), new Vector2(1.0f, 0.87f), Vector2.zero, Vector2.zero);

        GameObject p2CharIcon = new GameObject("P2CharIcon", typeof(RectTransform), typeof(Image));
        p2CharIcon.transform.SetParent(p2Panel.transform, false);
        p2CharIcon.GetComponent<Image>().color = new Color(0.3f, 0.35f, 0.5f, 1f);
        SetRect(p2CharIcon.GetComponent<RectTransform>(), new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);

        GameObject p2CharLabel = CreateText("P2CharLabel", p2Panel.transform, "P2: ?", 16, TextAnchor.MiddleCenter);
        SetRect(p2CharLabel.GetComponent<RectTransform>(), new Vector2(0.0f, 0.05f), new Vector2(1.0f, 0.4f), Vector2.zero, Vector2.zero);
        p2CharLabel.GetComponent<Text>().color = new Color(1f, 0.5f, 0.3f, 1f);

        // --- Character Grid (4 columns x 3 rows) in center ---
        GameObject gridPanel = new GameObject("GridPanel", typeof(RectTransform));
        gridPanel.transform.SetParent(characterSelectPanel.transform, false);
        SetRect(gridPanel.GetComponent<RectTransform>(), new Vector2(0.14f, 0.02f), new Vector2(0.86f, 0.87f), Vector2.zero, Vector2.zero);

        // Character names and colors
        string[] charNames = { "Fox", "Rabbit", "Bear", "Cat", "Dog", "Owl", "Deer", "Penguin", "Lion", "Monkey", "Panda", "Dragon" };
        Color[] charColors = {
            new Color(0.9f, 0.5f, 0.2f), new Color(0.8f, 0.7f, 0.6f), new Color(0.6f, 0.4f, 0.2f), new Color(0.7f, 0.6f, 0.8f),
            new Color(0.5f, 0.7f, 0.4f), new Color(0.4f, 0.5f, 0.7f), new Color(0.8f, 0.6f, 0.4f), new Color(0.3f, 0.3f, 0.4f),
            new Color(0.9f, 0.7f, 0.3f), new Color(0.7f, 0.5f, 0.3f), new Color(0.9f, 0.9f, 0.9f), new Color(0.5f, 0.8f, 0.5f)
        };

        GameObject[] charButtons = new GameObject[12];
        GameObject[] charImages = new GameObject[12];
        GameObject[] charNameTexts = new GameObject[12];

        int cols = 4;
        int rows = 3;
        float padding = 0.02f;
        float cellW = (1f - padding * (cols + 1)) / cols;
        float cellH = (1f - padding * (rows + 1)) / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int index = row * cols + col;
                float x0 = padding + col * (cellW + padding);
                float y0 = 1f - padding - (row + 1) * (cellH + padding) + padding;
                float x1 = x0 + cellW;
                float y1 = y0 + cellH;

                // Button with background
                GameObject btn = new GameObject("CharBtn_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
                btn.transform.SetParent(gridPanel.transform, false);
                btn.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.4f, 1f);
                SetRect(btn.GetComponent<RectTransform>(), new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
                charButtons[index] = btn;

                // Character icon (colored square placeholder)
                GameObject icon = new GameObject("CharIcon_" + index, typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(btn.transform, false);
                icon.GetComponent<Image>().color = charColors[index];
                SetRect(icon.GetComponent<RectTransform>(), new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero);
                charImages[index] = icon;

                // Character name
                GameObject nameText = CreateText("CharName_" + index, btn.transform, charNames[index], 14, TextAnchor.MiddleCenter);
                SetRect(nameText.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);
                nameText.GetComponent<Text>().color = Color.white;
                charNameTexts[index] = nameText;
            }
        }

        characterSelectPanel.SetActive(false);

        // NEXT button (bottom center, on canvas level)
        GameObject nextButton = CreateStyledButton("NextButton", canvasGo.transform, "NEXT",
            new Vector2(0.35f, 0.01f), new Vector2(0.65f, 0.10f),
            new Color(0.2f, 0.7f, 0.3f, 1f), Color.white, 28);
        nextButton.SetActive(false);

        // Wire references
        AssignViewReferences(view, controller, titleText, oneVsOneButton, teamModeButton,
            backButton, nextButton, modeSelectPanel, characterSelectPanel,
            selectingPlayerText, p1CharLabel, p2CharLabel, p1CharIcon, p2CharIcon,
            charButtons, charImages, charNameTexts);

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
        camera.backgroundColor = new Color(0.1f, 0.15f, 0.3f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        cameraGo.transform.rotation = Quaternion.identity;
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

    private static void AssignViewReferences(CharacterSelectView view, CharacterSelectController controller,
        GameObject titleText, GameObject oneVsOneButton, GameObject teamModeButton,
        GameObject backButton, GameObject nextButton,
        GameObject modeSelectPanel, GameObject characterSelectPanel,
        GameObject selectingPlayerText, GameObject p1CharLabel, GameObject p2CharLabel,
        GameObject p1CharIcon, GameObject p2CharIcon,
        GameObject[] charButtons, GameObject[] charImages, GameObject[] charNameTexts)
    {
        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("titleText").objectReferenceValue = titleText.GetComponent<Text>();
        viewSo.FindProperty("oneVsOneButton").objectReferenceValue = oneVsOneButton.GetComponent<Button>();
        viewSo.FindProperty("teamModeButton").objectReferenceValue = teamModeButton.GetComponent<Button>();
        viewSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        viewSo.FindProperty("nextButton").objectReferenceValue = nextButton.GetComponent<Button>();
        viewSo.FindProperty("modeSelectPanel").objectReferenceValue = modeSelectPanel;
        viewSo.FindProperty("characterSelectPanel").objectReferenceValue = characterSelectPanel;
        viewSo.FindProperty("selectingPlayerText").objectReferenceValue = selectingPlayerText.GetComponent<Text>();
        viewSo.FindProperty("player1CharLabel").objectReferenceValue = p1CharLabel.GetComponent<Text>();
        viewSo.FindProperty("player2CharLabel").objectReferenceValue = p2CharLabel.GetComponent<Text>();
        viewSo.FindProperty("player1CharIcon").objectReferenceValue = p1CharIcon.GetComponent<Image>();
        viewSo.FindProperty("player2CharIcon").objectReferenceValue = p2CharIcon.GetComponent<Image>();

        // Wire character button arrays
        SerializedProperty btnArrayProp = viewSo.FindProperty("characterButtons");
        btnArrayProp.arraySize = 12;
        for (int i = 0; i < 12; i++)
            btnArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = charButtons[i].GetComponent<Button>();

        SerializedProperty imgArrayProp = viewSo.FindProperty("characterImages");
        imgArrayProp.arraySize = 12;
        for (int i = 0; i < 12; i++)
            imgArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = charImages[i].GetComponent<Image>();

        SerializedProperty nameArrayProp = viewSo.FindProperty("characterNameTexts");
        nameArrayProp.arraySize = 12;
        for (int i = 0; i < 12; i++)
            nameArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = charNameTexts[i].GetComponent<Text>();

        viewSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("characterSelectView").objectReferenceValue = view;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();
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
