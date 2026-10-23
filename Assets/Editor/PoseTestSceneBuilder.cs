// Assets/Editor/PoseTestSceneBuilder.cs
// Unity menu: Tools → PoseTest → Build Scene  (hoặc batchmode: -executeMethod PoseTestSceneBuilder.BuildBatch)
// Tạo Assets/Game/Scenes/PoseTest/PoseTestGame.unity, nối sẵn mọi tham chiếu, thêm vào Build Settings.
#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PoseTestSceneBuilder
{
    const string ScenePath = "Assets/Game/Scenes/PoseTest/PoseTestGame.unity";
    const string HudPrefabPath = "Assets/Game/Prefabs/GameHUD.prefab";

    [MenuItem("Tools/PoseTest/Build Scene")]
    static void BuildMenu()
    {
        Build();
        EditorUtility.DisplayDialog("PoseTest Scene Built", $"Scene saved to:\n{ScenePath}\n\nAdded to Build Settings.", "OK");
    }

    public static void BuildBatch() => Build();

    static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.depth = -1;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 600);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Ảnh camera (đáy) + khung xương trùng rect
        var feedGo = Stretch(new GameObject("CameraFeed"), canvasGo.transform);
        var rawImg = feedGo.AddComponent<RawImage>();
        rawImg.color = Color.black;
        var skelGo = Stretch(new GameObject("Skeleton"), canvasGo.transform);
        var skel = skelGo.AddComponent<PoseSkeletonGraphic>();
        skel.raycastTarget = false;

        // Vạch chia 2 vùng chơi
        var div = new GameObject("ZoneDivider");
        div.transform.SetParent(canvasGo.transform, false);
        var drt = div.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.5f, 0f); drt.anchorMax = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(3f, 0f);
        var dimg = div.AddComponent<Image>();
        dimg.color = new Color(1f, 1f, 1f, 0.45f); dimg.raycastTarget = false;

        // HUD (tên, điểm, giờ) — như FRTest
        GameHUD hud = null;
        var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (hudPrefab != null)
        {
            var hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, canvasGo.transform);
            hudGo.name = "GameHUD";
            hud = hudGo.GetComponent<GameHUD>();
            var bg = hudGo.transform.Find("Background");
            if (bg != null) bg.gameObject.SetActive(false); // camera là nền
        }
        else Debug.LogError($"[PoseTestSceneBuilder] GameHUD prefab not found at {HudPrefabPath}");

        var poseText = MakeText(canvasGo.transform, "PoseText", new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.99f), 50, TextAlignmentOptions.Center, true);

        // Hình mẫu tư thế: nhỏ, giữa màn hình, trong 40% phía trên (y 0.62–0.87)
        var panel = new GameObject("PoseSample");
        panel.transform.SetParent(canvasGo.transform, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.62f); prt.anchorMax = new Vector2(0.5f, 0.87f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(120f, 0f);
        var pimg = panel.AddComponent<Image>();
        pimg.color = new Color(0.07f, 0.09f, 0.12f, 0.8f); pimg.raycastTarget = false;
        var sil = new GameObject("Silhouette");
        sil.transform.SetParent(panel.transform, false);
        var srt = sil.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(8f, 8f); srt.offsetMax = new Vector2(-8f, -8f);
        var silhouette = sil.AddComponent<PoseSilhouetteGraphic>();
        silhouette.color = new Color(1f, 0.85f, 0.3f); silhouette.raycastTarget = false;
        panel.SetActive(false);
        var cdText   = MakeText(canvasGo.transform, "CountdownText", new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.65f), 70, TextAlignmentOptions.Center, true);
        cdText.gameObject.SetActive(false);
        var leftSt   = MakeText(canvasGo.transform, "LeftStatus", new Vector2(0.03f, 0.45f), new Vector2(0.47f, 0.6f), 60, TextAlignmentOptions.Center, true);
        var rightSt  = MakeText(canvasGo.transform, "RightStatus", new Vector2(0.53f, 0.45f), new Vector2(0.97f, 0.6f), 60, TextAlignmentOptions.Center, true);
        var info     = MakeText(canvasGo.transform, "InfoText", new Vector2(0.01f, 0f), new Vector2(0.99f, 0.06f), 20, TextAlignmentOptions.Left, false);

        var clientGo = new GameObject("PoseBridgeClient");
        var client = clientGo.AddComponent<PoseBridgeClient>();

        var ctrlGo = new GameObject("PoseTestController");
        var ctrl = ctrlGo.AddComponent<PoseTestController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("client").objectReferenceValue = client;
        so.FindProperty("cameraImage").objectReferenceValue = rawImg;
        so.FindProperty("skeleton").objectReferenceValue = skel;
        so.FindProperty("poseText").objectReferenceValue = poseText;
        so.FindProperty("sampleRoot").objectReferenceValue = panel;
        so.FindProperty("sampleFigure").objectReferenceValue = silhouette;
        so.FindProperty("countdownText").objectReferenceValue = cdText;
        so.FindProperty("leftStatusText").objectReferenceValue = leftSt;
        so.FindProperty("rightStatusText").objectReferenceValue = rightSt;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[PoseTestSceneBuilder] Scene built → {ScenePath}");
    }

    static GameObject Stretch(GameObject go, Transform parent)
    {
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 amin, Vector2 amax, float size, TextAlignmentOptions align, bool outline)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (outline) { t.outlineWidth = 0.25f; t.outlineColor = Color.black; }
        return t;
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes) if (s.path == scenePath) return;
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(scenePath, true) };
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
