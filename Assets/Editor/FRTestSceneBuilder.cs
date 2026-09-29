// Assets/Editor/FRTestSceneBuilder.cs
// Run from Unity menu: Tools → FRTest → Build Scene
// Creates Assets/Game/Scenes/FRTest/FRTestGame.unity with all components wired.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class FRTestSceneBuilder
{
    const string ScenePath = "Assets/Game/Scenes/FRTest/FRTestGame.unity";
    const string HudPrefabPath = "Assets/Game/Prefabs/GameHUD.prefab";

    [MenuItem("Tools/FRTest/Build Scene")]
    static void BuildScene()
    {
        // 1. Create empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.depth = -1;

        // 3. EventSystem
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // 4. Root Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 600);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 5. GameHUD prefab instance (child of canvas)
        var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        GameHUD hud = null;
        if (hudPrefab != null)
        {
            var hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, canvasGo.transform);
            hudGo.name = "GameHUD";
            // DO NOT override the GameHUD root RectTransform.
            // GameHUD uses anchor=(0.5,0.5) size=100×100 at canvas centre — its children
            // use large anchoredPosition offsets to reach canvas edges.
            // Overriding to full-canvas stretch multiplies those offsets by the canvas
            // size and pushes every badge/timer outside the screen.
            hud = hudGo.GetComponent<GameHUD>();

            // Background child has SizeDelta (924,522) — when Awake() reparents it to
            // the canvas root the sizeDelta overflows the canvas. Disable it so the
            // camera-feed RawImage is used as background instead.
            var bgChild = hudGo.transform.Find("Background");
            if (bgChild != null) bgChild.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError($"[FRTestSceneBuilder] GameHUD prefab not found at {HudPrefabPath}");
        }

        // 6. Camera feed RawImage (full canvas, behind HUD)
        var feedGo = new GameObject("CameraFeed");
        feedGo.transform.SetParent(canvasGo.transform, false);
        var feedRt = feedGo.AddComponent<RectTransform>();
        feedRt.anchorMin = Vector2.zero;
        feedRt.anchorMax = Vector2.one;
        feedRt.offsetMin = Vector2.zero;
        feedRt.offsetMax = Vector2.zero;
        feedRt.SetAsFirstSibling();
        var rawImg = feedGo.AddComponent<RawImage>();
        rawImg.color = Color.white;

        // 7. Countdown text (center, large, initially hidden)
        var cdGo = new GameObject("CountdownText");
        cdGo.transform.SetParent(canvasGo.transform, false);
        var cdRt = cdGo.AddComponent<RectTransform>();
        cdRt.anchorMin = new Vector2(0.25f, 0.3f);
        cdRt.anchorMax = new Vector2(0.75f, 0.7f);
        cdRt.offsetMin = Vector2.zero;
        cdRt.offsetMax = Vector2.zero;
        var cdText = cdGo.AddComponent<TextMeshProUGUI>();
        cdText.text = "Next in 3s";
        cdText.fontSize = 80;
        cdText.alignment = TextAlignmentOptions.Center;
        cdText.color = Color.white;
        cdGo.SetActive(false);

        // 8–9. Buttons — 4 buttons in a horizontal row at the BOTTOM.
        // Left player zone (left half): green | red   — side by side.
        // Right player zone (right half): green | red — side by side.
        // y: 0 → 0.38 (bottom ~228px of 600px canvas).
        var red = new Color(0.88f, 0.1f, 0.1f);
        Button leftGreenBtn  = MakeButton(canvasGo.transform, "LeftGreenBtn",
            new Vector2(0.02f, 0.04f), new Vector2(0.24f, 0.38f), Color.green, "✓");
        Button leftRedBtn    = MakeButton(canvasGo.transform, "LeftRedBtn",
            new Vector2(0.26f, 0.04f), new Vector2(0.48f, 0.38f), red, "✗");

        Button rightGreenBtn = MakeButton(canvasGo.transform, "RightGreenBtn",
            new Vector2(0.52f, 0.04f), new Vector2(0.74f, 0.38f), Color.green, "✓");
        Button rightRedBtn   = MakeButton(canvasGo.transform, "RightRedBtn",
            new Vector2(0.76f, 0.04f), new Vector2(0.98f, 0.38f), red, "✗");

        // 10. Controller GameObject. FaceRecognitionPlugin is NOT placed here — it's a
        // DontDestroyOnLoad singleton (Singleton<T>) shared across every scene, auto-created on
        // first FaceRecognitionPlugin.Instance access. Placing a second one here would spawn a
        // duplicate on every re-visit of this scene, each re-running UnityFaceBridge.initialize().
        var ctrlGo = new GameObject("FRTestController");
        ctrlGo.transform.SetParent(canvasGo.transform.parent, false);
        var ctrl = ctrlGo.AddComponent<FRTestController>();

        // 11. Wire Inspector references via SerializedObject
        var so = new SerializedObject(ctrl);
        so.FindProperty("hud")          .objectReferenceValue = hud;
        so.FindProperty("cameraImage")  .objectReferenceValue = rawImg;
        so.FindProperty("countdownText").objectReferenceValue = cdText;
        so.FindProperty("leftGreenBtn") .objectReferenceValue = leftGreenBtn;
        so.FindProperty("leftRedBtn")   .objectReferenceValue = leftRedBtn;
        so.FindProperty("rightGreenBtn").objectReferenceValue = rightGreenBtn;
        so.FindProperty("rightRedBtn")  .objectReferenceValue = rightRedBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 12. Save scene
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // 13. Add to Build Settings
        AddSceneToBuildSettings(ScenePath);

        AssetDatabase.Refresh();
        Debug.Log($"[FRTestSceneBuilder] Scene built → {ScenePath}");
        EditorUtility.DisplayDialog("FRTest Scene Built",
            $"Scene saved to:\n{ScenePath}\n\nAdded to Build Settings.", "OK");
    }

    static Button MakeButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(10, 10);
        rt.offsetMax = new Vector2(-10, -10);

        var img = go.AddComponent<Image>();
        img.color = color;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
        btn.colors = colors;

        // Label text
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 60;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == scenePath) return; // already in list

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
