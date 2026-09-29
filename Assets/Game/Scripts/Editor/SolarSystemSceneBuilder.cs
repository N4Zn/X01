#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > SolarSystem > Build Scene
/// Tạo scene 3D hệ mặt trời: Sun + camera orbit + spawner.
/// Sau khi build:
///   1. Gán PlanetData[] vào SolarSystemSpawner → planets
///   2. Download textures vào Assets/Resources/SolarSystem/Textures/
///   3. Play để xem hành tinh quay
/// </summary>
public static class SolarSystemSceneBuilder
{
    const string ScenePath = "Assets/Game/Scenes/SolarSystemScene/SolarSystemScene.unity";

    [MenuItem("Tools/SolarSystem/Build Scene")]
    static void BuildScene()
    {
        // ── 1. Scene mới (3D empty) ──────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 2. Lighting: tối để space feel ──────────────────────────────────
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.08f);
        RenderSettings.skybox       = null; // sẽ gán skybox material sau

        // ── 3. Directional Light (mờ — ánh sáng chính từ Sun Point Light) ──
        var dirLightGo = new GameObject("Directional Light");
        var dirLight   = dirLightGo.AddComponent<Light>();
        dirLight.type      = LightType.Directional;
        dirLight.intensity = 0.1f;
        dirLight.color     = new Color(0.8f, 0.85f, 1f);
        dirLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 4. EventSystem ───────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();

        // ── 5. Main Camera (perspective 3D) ──────────────────────────────────
        var camGo = new GameObject("Main Camera");
        var cam   = camGo.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.01f, 0.01f, 0.04f); // gần đen — space
        cam.fieldOfView      = 60f;
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 500f;
        cam.tag              = "MainCamera";
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 25f, -45f);
        camGo.transform.LookAt(Vector3.zero);

        // SolarCameraController — orbit + zoom
        var camCtrl = camGo.AddComponent<SolarCameraController>();

        // ── 6. Sun ───────────────────────────────────────────────────────────
        var sunGo  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sunGo.name = "Sun";
        Object.DestroyImmediate(sunGo.GetComponent<SphereCollider>());
        sunGo.transform.position   = Vector3.zero;
        sunGo.transform.localScale = Vector3.one * 3f; // sẽ override bởi PlanetData

        // Point light bên trong Sun — chiếu sáng hành tinh
        var sunLightGo = new GameObject("SunLight");
        sunLightGo.transform.SetParent(sunGo.transform, false);
        var sunLight   = sunLightGo.AddComponent<Light>();
        sunLight.type      = LightType.Point;
        sunLight.range     = 200f;
        sunLight.intensity = 2.5f;
        sunLight.color     = new Color(1f, 0.95f, 0.8f);

        // ── 7. SolarSystemSpawner ────────────────────────────────────────────
        var sysGo      = new GameObject("[SolarSystem]");
        var spawner    = sysGo.AddComponent<SolarSystemSpawner>();

        // Gán sunTransform qua SerializedObject
        var spawnerSO = new SerializedObject(spawner);
        spawnerSO.FindProperty("sunTransform").objectReferenceValue = sunGo.transform;
        spawnerSO.ApplyModifiedProperties();

        // ── 8. Canvas overlay (HUD tối giản: nút Back, speed label) ─────────
        var canvasGo    = new GameObject("Canvas");
        var canvas      = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler      = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 600);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Nút Back (góc trên trái)
        var btnBackGo  = CreateUIButton(canvasGo, "BtnBack", "← Quay lại");
        var btnBackRt  = btnBackGo.GetComponent<RectTransform>();
        btnBackRt.anchorMin   = new Vector2(0f, 1f);
        btnBackRt.anchorMax   = new Vector2(0f, 1f);
        btnBackRt.pivot       = new Vector2(0f, 1f);
        btnBackRt.anchoredPosition = new Vector2(16f, -16f);
        btnBackRt.sizeDelta   = new Vector2(140f, 44f);

        // Label tốc độ (góc dưới phải — placeholder)
        var speedLabelGo = new GameObject("SpeedLabel");
        speedLabelGo.transform.SetParent(canvasGo.transform, false);
        speedLabelGo.AddComponent<RectTransform>();
        var speedTmp     = speedLabelGo.AddComponent<TextMeshProUGUI>();
        speedTmp.text      = "1.0x";
        speedTmp.fontSize  = 20;
        speedTmp.alignment = TextAlignmentOptions.Right;
        speedTmp.color     = new Color(1f, 1f, 1f, 0.7f);
        var speedRt        = speedLabelGo.GetComponent<RectTransform>();
        speedRt.anchorMin  = new Vector2(1f, 0f);
        speedRt.anchorMax  = new Vector2(1f, 0f);
        speedRt.pivot      = new Vector2(1f, 0f);
        speedRt.anchoredPosition = new Vector2(-16f, 16f);
        speedRt.sizeDelta  = new Vector2(100f, 30f);

        // ── 9. Lưu scene ─────────────────────────────────────────────────────
        System.IO.Directory.CreateDirectory(
            System.IO.Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        // ── 10. Thêm vào Build Settings ──────────────────────────────────────
        AddToBuildSettings(ScenePath);

        Debug.Log("[SolarSystemSceneBuilder] ✓ Scene created: " + ScenePath);
        Debug.Log("[SolarSystemSceneBuilder] Việc cần làm tiếp theo:");
        Debug.Log("  1. Chạy Tools > SolarSystem > Create Planet Data Assets");
        Debug.Log("  2. Gán PlanetData[] vào [SolarSystem] → SolarSystemSpawner → planets");
        Debug.Log("  3. Download textures → Assets/Resources/SolarSystem/Textures/");
        Debug.Log("  4. Bấm Play để xem kết quả");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject CreateUIButton(GameObject parent, string name, string label)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        go.AddComponent<Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return go;
    }

    static void AddToBuildSettings(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        foreach (var s in list)
            if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
