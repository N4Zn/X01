#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editor tool: Tools > TestTongHop > Build Scene
/// Tạo scene TestTongHopGame với toàn bộ GameObject cần thiết.
/// Sau khi build, gán các SerializeField còn thiếu trong Inspector:
///   - QuestionPool → chooseCSV, matchingCSV (TextAsset)
///   - FloatingDisplay → itemPrefab (FloatingItem prefab)
///   - ButtonDisplay → buttons[] (ButtonItem prefab × 5)
///   - MatchingDisplay → leftItems[], rightItems[], linePrefab
///   - QuestionMediaDisplay → iconPrefab
/// </summary>
public static class TestTongHopSceneBuilder
{
    const string ScenePath  = "Assets/Game/Scenes/TestTongHopGame/TestTongHopGame.unity";
    const string SceneName  = "TestTongHopGame";

    [MenuItem("Tools/TestTongHop/Build Scene")]
    static void BuildScene()
    {
        // ── 1. Tạo scene mới ─────────────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 2. EventSystem ───────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // ── 3. Camera ────────────────────────────────────────────────────────
        // FloatingDisplay dùng Camera.main để spawn world-space items
        var camGo     = new GameObject("Main Camera");
        var cam       = camGo.AddComponent<Camera>();
        cam.clearFlags     = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        cam.orthographic   = true;
        cam.orthographicSize = 5f;
        cam.tag            = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // ── 4. Canvas (1024×600, ScreenSpaceOverlay) ─────────────────────────
        var canvasGo      = new GameObject("Canvas");
        var canvas        = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler        = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1024, 600);
        scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight   = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── 5. QuestionMediaDisplay ───────────────────────────────────────────
        var mediaGo    = CreateChild(canvasGo, "QuestionMediaDisplay");
        StretchFull(mediaGo);
        var qmd        = mediaGo.AddComponent<QuestionMediaDisplay>();

        // Text slot
        var textSlot   = CreateChild(mediaGo, "TextSlot");
        StretchFull(textSlot);
        var textLabel  = textSlot.AddComponent<TextMeshProUGUI>();
        textLabel.fontSize  = 48;
        textLabel.alignment = TextAlignmentOptions.Center;
        textLabel.color     = Color.white;

        // Image slot
        var imageSlot  = CreateChild(mediaGo, "ImageSlot");
        StretchFull(imageSlot);
        var imageHolder = imageSlot.AddComponent<Image>();
        imageHolder.preserveAspect = true;

        // Icon slot + container
        // iconSlot: full area (same as other slots)
        // iconContainer: center-anchored, auto-expands để chứa icons
        var iconSlot      = CreateChild(mediaGo, "IconSlot");
        StretchFull(iconSlot);
        var iconContainer = CreateChild(iconSlot, "IconContainer");
        CenterAnchor(iconContainer);

        // GridLayoutGroup: 2 columns, cell 80×80, spacing 10
        // ContentSizeFitter: container tự co giãn theo số icon thực tế
        var icGrid = iconContainer.AddComponent<GridLayoutGroup>();
        icGrid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        icGrid.constraintCount = 2;
        icGrid.childAlignment  = TextAnchor.MiddleCenter;
        icGrid.cellSize        = new Vector2(80, 80);
        icGrid.spacing         = new Vector2(10, 10);

        var icFitter = iconContainer.AddComponent<ContentSizeFitter>();
        icFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        icFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // AudioSource
        var audioSource = mediaGo.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Gán SerializeField qua SerializedObject
        var qmdSO   = new SerializedObject(qmd);
        qmdSO.FindProperty("textSlot").objectReferenceValue      = textSlot;
        qmdSO.FindProperty("textLabel").objectReferenceValue     = textLabel;
        qmdSO.FindProperty("imageSlot").objectReferenceValue     = imageSlot;
        qmdSO.FindProperty("imageHolder").objectReferenceValue   = imageHolder;
        qmdSO.FindProperty("iconSlot").objectReferenceValue      = iconSlot;
        qmdSO.FindProperty("iconContainer").objectReferenceValue = iconContainer.transform;
        qmdSO.FindProperty("audioSource").objectReferenceValue   = audioSource;
        qmdSO.ApplyModifiedProperties();

        textSlot.SetActive(false);
        imageSlot.SetActive(false);
        iconSlot.SetActive(false);

        // ── 6. AnswerDisplayManager ───────────────────────────────────────────
        var admGo  = CreateChild(canvasGo, "AnswerDisplayManager");
        var adm    = admGo.AddComponent<AnswerDisplayManager>();

        // FloatingDisplay — spawn UI items vào Canvas (Screen Space)
        var floatingGo   = CreateChild(admGo, "FloatingDisplay");
        var floatingDisp = floatingGo.AddComponent<FloatingDisplay>();
        // Gán spawnParent = Canvas (items sẽ là con của Canvas)
        var floatingSO = new SerializedObject(floatingDisp);
        floatingSO.FindProperty("spawnParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        floatingSO.ApplyModifiedProperties();
        floatingGo.SetActive(false);

        // ButtonDisplay (4 fixed buttons inside Canvas)
        var buttonGo   = CreateChild(admGo, "ButtonDisplay");
        StretchFull(buttonGo);
        var buttonDisp = buttonGo.AddComponent<ButtonDisplay>();
        buttonGo.SetActive(false);

        // MatchingDisplay (3+3 items + LineRenderer)
        var matchingGo   = CreateChild(admGo, "MatchingDisplay");
        StretchFull(matchingGo);
        var matchingDisp = matchingGo.AddComponent<MatchingDisplay>();
        matchingGo.SetActive(false);

        // Gán AnswerDisplayManager refs
        var admSO = new SerializedObject(adm);
        admSO.FindProperty("floatingDisplay").objectReferenceValue = floatingDisp;
        admSO.FindProperty("buttonDisplay").objectReferenceValue   = buttonDisp;
        admSO.FindProperty("matchingDisplay").objectReferenceValue = matchingDisp;
        admSO.ApplyModifiedProperties();

        // ── 7. GameModel (GameManager + QuestionPool) ─────────────────────────
        var modelGo   = new GameObject("[GameModel]");
        var gm        = modelGo.AddComponent<GameManager>();
        var qp        = modelGo.AddComponent<QuestionPool>();

        var gmSO = new SerializedObject(gm);
        gmSO.FindProperty("questionPool").objectReferenceValue = qp;
        gmSO.ApplyModifiedProperties();

        // ── 8. GameController (TestTongHopController) ─────────────────────────
        var ctrlGo = new GameObject("[GameController]");
        var ctrl   = ctrlGo.AddComponent<TestTongHopController>();

        var ctrlSO = new SerializedObject(ctrl);
        ctrlSO.FindProperty("gameModel").objectReferenceValue            = gm;
        ctrlSO.FindProperty("answerDisplayManager").objectReferenceValue = adm;
        ctrlSO.FindProperty("questionMediaDisplay").objectReferenceValue = qmd;
        ctrlSO.ApplyModifiedProperties();

        // ── 9. Lưu scene ─────────────────────────────────────────────────────
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        // ── 10. Thêm vào Build Settings ───────────────────────────────────────
        AddToBuildSettings(ScenePath);

        Debug.Log($"[TestTongHopSceneBuilder] Scene created at: {ScenePath}");
        Debug.Log("[TestTongHopSceneBuilder] TODO: Gán Inspector refs còn thiếu:");
        Debug.Log("  - QuestionPool → chooseCSV, matchingCSV");
        Debug.Log("  - FloatingDisplay → itemPrefab (FloatingItem prefab)");
        Debug.Log("  - ButtonDisplay → buttons[] (ButtonItem prefabs ×4)");
        Debug.Log("  - MatchingDisplay → leftItems[], rightItems[], linePrefab");
        Debug.Log("  - QuestionMediaDisplay → iconPrefab");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void StretchFull(GameObject go)
    {
        var rt         = go.GetComponent<RectTransform>();
        rt.anchorMin   = Vector2.zero;
        rt.anchorMax   = Vector2.one;
        rt.offsetMin   = Vector2.zero;
        rt.offsetMax   = Vector2.zero;
    }

    // Anchor giữa, kích thước do ContentSizeFitter quyết định
    static void CenterAnchor(GameObject go)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
    }

    static void AddToBuildSettings(string scenePath)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        foreach (var s in scenes)
            if (s.path == scenePath) return;   // already added

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[TestTongHopSceneBuilder] Added '{SceneName}' to Build Settings.");
    }
}
#endif
