using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tạo toàn bộ scene + prefab cho game River Cross.
/// Menu: Tools > RiverCross > Build Scene
/// </summary>
public static class RiverCrossSceneBuilder
{
    const string SceneFolder  = "Assets/Game/Scenes/RiverCrossGame";
    const string ScenePath    = "Assets/Game/Scenes/RiverCrossGame/RiverCrossGame.unity";
    const string PrefabFolder = "Assets/Game/Prefabs/RiverCrossGame";

    const string RaftItemPrefabPath = "Assets/Game/Prefabs/RiverCrossGame/RaftItem.prefab";
    const string RaftLanePrefabPath = "Assets/Game/Prefabs/RiverCrossGame/RaftLane.prefab";

    // Canvas design resolution (same as rest of project)
    const float CW = 1024f;
    const float CH = 600f;

    // Layout (canvas-space, center = 0,0)
    const float LEFT_BANK_X  = -460f;
    const float RIGHT_BANK_X =  460f;
    const float BANK_W       =  104f;   // chiều rộng mỗi bờ

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/RiverCross/Build Scene")]
    public static void BuildAll()
    {
        EnsureFolders();

        // 1. Tạo prefabs trước (scene cần reference đến chúng)
        GameObject raftItemPrefab = CreateRaftItemPrefab();
        GameObject raftLanePrefab = CreateRaftLanePrefab(raftItemPrefab);

        // 2. Build scene
        BuildScene(raftLanePrefab);

        // 3. Thêm vào Build Settings
        AddSceneToBuildSettings(ScenePath);

        // 4. Tạo icon nếu chưa có
        CreateGameIconIfMissing();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RiverCrossSceneBuilder] Done — scene, prefabs, icon created.");
    }

    // ── Game icon ─────────────────────────────────────────────────────────────

    const string IconPath = "Assets/Resources/GameIcons/RiverCross.png";

    /// <summary>
    /// Tạo icon 128×128 cho game RiverCross nếu chưa tồn tại.
    /// Vẽ: nền trời xanh nhạt | sông xanh dương | 2 bờ cỏ xanh | 1 chiếc bè gỗ.
    /// </summary>
    static void CreateGameIconIfMissing()
    {
        if (File.Exists(ToAbs(IconPath))) return;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);

        // Màu nền
        Color sky   = new Color(0.53f, 0.81f, 0.98f);
        Color water = new Color(0.08f, 0.38f, 0.70f);
        Color grass = new Color(0.20f, 0.60f, 0.20f);
        Color raft  = new Color(0.62f, 0.38f, 0.15f);
        Color plank = new Color(0.50f, 0.28f, 0.08f);

        // Phân vùng dọc (Y từ dưới lên):
        //   0-15  : trời (phần dưới icon nhìn từ trên)
        //   16-111: sông
        //   112-127: trời (phần trên)
        // Phân vùng ngang:
        //   0-19 : bờ trái (cỏ)
        //   20-107: sông
        //   108-127: bờ phải (cỏ)

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                Color c;
                bool isBank = x < 20 || x >= 108;
                bool isSky  = y >= 112 || y < 16;

                if (isSky)
                    c = sky;
                else if (isBank)
                    c = grass;
                else
                    c = Color.Lerp(water, new Color(0.18f, 0.55f, 0.85f),
                                   Mathf.Sin(y * 0.3f) * 0.2f + 0.2f);

                tex.SetPixel(x, y, c);
            }
        }

        // Bè gỗ: dải ngang ở giữa sông (y=58-72, x=22-106)
        for (int y = 58; y <= 72; y++)
        {
            for (int x = 22; x <= 106; x++)
            {
                // Đường vân gỗ mỗi ~14px
                Color c = ((y - 58) % 5 == 0) ? plank : raft;
                tex.SetPixel(x, y, c);
            }
        }

        // Viền tối cho bè
        for (int x = 22; x <= 106; x++)
        {
            tex.SetPixel(x, 58, plank);
            tex.SetPixel(x, 72, plank);
        }
        for (int y = 58; y <= 72; y++)
        {
            tex.SetPixel(22, y, plank);
            tex.SetPixel(106, y, plank);
        }

        // Vài gợn sóng đơn giản
        for (int i = 0; i < 3; i++)
        {
            int wy = 30 + i * 20;
            for (int x = 25; x < 100; x += 4)
            {
                tex.SetPixel(x,     wy, new Color(0.55f, 0.80f, 0.95f));
                tex.SetPixel(x + 1, wy, new Color(0.55f, 0.80f, 0.95f));
            }
        }

        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string absPath = ToAbs(IconPath);
        string dir = Path.GetDirectoryName(absPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(absPath, png);

        AssetDatabase.ImportAsset(IconPath);

        // Đặt import settings: Sprite (UI)
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType       = TextureImporterType.Sprite;
            importer.spriteImportMode  = SpriteImportMode.Single;
            importer.mipmapEnabled     = false;
            importer.filterMode        = FilterMode.Bilinear;
            importer.maxTextureSize    = 128;
            importer.SaveAndReimport();
        }

        Debug.Log($"[RiverCrossSceneBuilder] Icon created: {IconPath}");
    }

    // ── Prefab creation ───────────────────────────────────────────────────────

    /// <summary>RaftItem prefab: RectTransform + Image + RaftItem.cs</summary>
    static GameObject CreateRaftItemPrefab()
    {
        if (File.Exists(ToAbs(RaftItemPrefabPath)))
            return AssetDatabase.LoadAssetAtPath<GameObject>(RaftItemPrefabPath);

        var go = new GameObject("RaftItem", typeof(RectTransform), typeof(Image), typeof(RaftItem));
        var img = go.GetComponent<Image>();
        img.color = Color.white;       // white = sprite hiện màu gốc, không bị tint
        img.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 55f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, RaftItemPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>RaftLane prefab: RectTransform + RaftLane.cs (gán raftPrefab)</summary>
    static GameObject CreateRaftLanePrefab(GameObject raftItemPrefab)
    {
        if (File.Exists(ToAbs(RaftLanePrefabPath)))
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RaftLanePrefabPath);
            // Đảm bảo raftPrefab được gán
            var so = new SerializedObject(existing.GetComponent<RaftLane>());
            so.FindProperty("raftPrefab").objectReferenceValue =
                raftItemPrefab.GetComponent<RaftItem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(existing);
            return existing;
        }

        var go = new GameObject("RaftLane", typeof(RectTransform), typeof(RaftLane));
        var rt = go.GetComponent<RectTransform>();
        // Lane container phủ toàn canvas
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, RaftLanePrefabPath);
        Object.DestroyImmediate(go);

        // Gán raftPrefab sau khi save (prefab phải tồn tại trước)
        var laneSo = new SerializedObject(prefab.GetComponent<RaftLane>());
        laneSo.FindProperty("raftPrefab").objectReferenceValue =
            raftItemPrefab.GetComponent<RaftItem>();
        laneSo.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);

        return prefab;
    }

    // ── Scene build ───────────────────────────────────────────────────────────

    static void BuildScene(GameObject raftLanePrefab)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ────────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.20f, 0.55f, 0.80f);
        cam.orthographic     = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane    = 0.3f;
        cam.farClipPlane     = 1000f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────────────
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas   = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CW, CH);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ── Background layers (thứ tự: sky → water → banks) ──────────────────

        // Sky (phủ toàn canvas, dưới cùng)
        var skyGo  = MakeRawImage("BG_Sky", canvasGo.transform, Color.white);
        StretchFull(skyGo.GetComponent<RectTransform>());

        // Water (vùng sông giữa 2 bờ)
        float riverLeft  = LEFT_BANK_X  + BANK_W / 2f;
        float riverRight = RIGHT_BANK_X - BANK_W / 2f;
        var waterGo = MakeRawImage("BG_Water", canvasGo.transform, Color.white);
        var waterRT = waterGo.GetComponent<RectTransform>();
        waterRT.anchorMin = new Vector2(0.5f, 0f);
        waterRT.anchorMax = new Vector2(0.5f, 1f);
        waterRT.sizeDelta = new Vector2(riverRight - riverLeft, 0f);
        waterRT.anchoredPosition = Vector2.zero;
        waterGo.AddComponent<WaterBackground>();   // scroll tự động

        // Left bank
        var leftBankGo = MakeImage("BG_LeftBank", canvasGo.transform, new Color(0.20f, 0.55f, 0.20f));
        var leftBankRT = leftBankGo.GetComponent<RectTransform>();
        leftBankRT.anchorMin       = new Vector2(0f, 0f);
        leftBankRT.anchorMax       = new Vector2(0f, 1f);
        leftBankRT.pivot           = new Vector2(0f, 0.5f);
        leftBankRT.anchoredPosition = new Vector2(-CW / 2f, 0f);
        leftBankRT.sizeDelta       = new Vector2(BANK_W + (CW / 2f + LEFT_BANK_X + BANK_W / 2f), 0f);

        // Right bank
        var rightBankGo = MakeImage("BG_RightBank", canvasGo.transform, new Color(0.20f, 0.55f, 0.20f));
        var rightBankRT = rightBankGo.GetComponent<RectTransform>();
        rightBankRT.anchorMin       = new Vector2(1f, 0f);
        rightBankRT.anchorMax       = new Vector2(1f, 1f);
        rightBankRT.pivot           = new Vector2(1f, 0.5f);
        rightBankRT.anchoredPosition = new Vector2(CW / 2f, 0f);
        rightBankRT.sizeDelta       = new Vector2(CW / 2f - RIGHT_BANK_X + BANK_W / 2f, 0f);

        // ── Art generator (gán sau khi có refs) ──────────────────────────────
        // (attach lên canvasGo, refs gán qua SerializedObject bên dưới)

        // ── Lane container ────────────────────────────────────────────────────
        var laneContainerGo = new GameObject("LaneContainer", typeof(RectTransform));
        laneContainerGo.transform.SetParent(canvasGo.transform, false);
        StretchFull(laneContainerGo.GetComponent<RectTransform>());

        // ── Player ────────────────────────────────────────────────────────────
        var playerGo  = new GameObject("Player", typeof(RectTransform), typeof(PlayerAvatar));
        playerGo.transform.SetParent(canvasGo.transform, false);
        playerGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 80f);

        // Character image child
        var charImgGo = MakeImage("CharacterImage", playerGo.transform, Color.white);
        charImgGo.GetComponent<Image>().color = new Color(0.98f, 0.82f, 0.55f); // placeholder skin
        StretchFull(charImgGo.GetComponent<RectTransform>());

        // ── UI overlay ────────────────────────────────────────────────────────
        var uiRoot = new GameObject("UI", typeof(RectTransform));
        uiRoot.transform.SetParent(canvasGo.transform, false);
        StretchFull(uiRoot.GetComponent<RectTransform>());

        // Timer text (top-left) — MM:SS đếm ngược
        var timerGo = MakeTMP("TimerText", uiRoot.transform, "02:00", 32);
        var timerRT = timerGo.GetComponent<RectTransform>();
        timerRT.anchorMin       = new Vector2(0f, 1f);
        timerRT.anchorMax       = new Vector2(0f, 1f);
        timerRT.pivot           = new Vector2(0f, 1f);
        timerRT.anchoredPosition = new Vector2(16f, -10f);
        timerRT.sizeDelta       = new Vector2(160f, 50f);
        var timerTMP = timerGo.GetComponent<TextMeshProUGUI>();
        timerTMP.alignment = TextAlignmentOptions.Left;
        timerTMP.color     = Color.white;

        // Cross count text (top-right) — "Đã qua: N lần"
        var crossGo = MakeTMP("CrossCountText", uiRoot.transform, "Đã qua: 0 lần", 28);
        var crossRT = crossGo.GetComponent<RectTransform>();
        crossRT.anchorMin       = new Vector2(1f, 1f);
        crossRT.anchorMax       = new Vector2(1f, 1f);
        crossRT.pivot           = new Vector2(1f, 1f);
        crossRT.anchoredPosition = new Vector2(-16f, -10f);
        crossRT.sizeDelta       = new Vector2(240f, 50f);
        var crossTMP = crossGo.GetComponent<TextMeshProUGUI>();
        crossTMP.alignment = TextAlignmentOptions.Right;
        crossTMP.color     = Color.white;

        // Celebration text (center, hidden) — "🎉 Qua sông! (N lần)" flash
        var celebrationGo = MakeTMP("CelebrationText", uiRoot.transform, "🎉", 60);
        var celebRT = celebrationGo.GetComponent<RectTransform>();
        celebRT.anchorMin       = new Vector2(0.5f, 0.5f);
        celebRT.anchorMax       = new Vector2(0.5f, 0.5f);
        celebRT.pivot           = new Vector2(0.5f, 0.5f);
        celebRT.sizeDelta       = new Vector2(600f, 100f);
        celebRT.anchoredPosition = new Vector2(0f, 80f);
        var celebTMP = celebrationGo.GetComponent<TextMeshProUGUI>();
        celebTMP.alignment = TextAlignmentOptions.Center;
        celebTMP.color     = Color.yellow;
        celebrationGo.SetActive(false);

        // Countdown text (center screen, hidden initially)
        var countdownGo = MakeTMP("CountdownText", uiRoot.transform, "3", 120);
        var cdRT = countdownGo.GetComponent<RectTransform>();
        cdRT.anchorMin       = new Vector2(0.5f, 0.5f);
        cdRT.anchorMax       = new Vector2(0.5f, 0.5f);
        cdRT.sizeDelta       = new Vector2(300f, 160f);
        cdRT.anchoredPosition = Vector2.zero;
        var cdTMP = countdownGo.GetComponent<TextMeshProUGUI>();
        cdTMP.alignment = TextAlignmentOptions.Center;
        cdTMP.color     = Color.white;
        countdownGo.SetActive(false);

        // Result panel (hidden) — hiện khi hết giờ
        var resultPanelGo = MakeImage("ResultPanel", uiRoot.transform, new Color(0f, 0f, 0f, 0.6f));
        StretchFull(resultPanelGo.GetComponent<RectTransform>());
        resultPanelGo.SetActive(false);

        var resultTextGo = MakeTMP("ResultText", resultPanelGo.transform, "", 48);
        var resultRT = resultTextGo.GetComponent<RectTransform>();
        resultRT.anchorMin       = new Vector2(0.5f, 0.5f);
        resultRT.anchorMax       = new Vector2(0.5f, 0.5f);
        resultRT.sizeDelta       = new Vector2(700f, 160f);
        resultRT.anchoredPosition = Vector2.zero;
        var resultTMP = resultTextGo.GetComponent<TextMeshProUGUI>();
        resultTMP.alignment = TextAlignmentOptions.Center;
        resultTMP.color     = Color.white;

        // ── Touch visualizer (độc lập, tự tạo overlay canvas) ───────────────
        var touchVizGo = new GameObject("TouchVisualizer");
        touchVizGo.AddComponent<TouchVisualizer>();

        // ── Controller + ArtGenerator (GameObject riêng) ─────────────────────
        var controllerGo = new GameObject("RiverCrossController");
        var controller   = controllerGo.AddComponent<RiverCrossController>();
        var artGen       = controllerGo.AddComponent<RiverArtGenerator>();

        // ── Wire references — Controller ──────────────────────────────────────
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("mainCanvas")      .objectReferenceValue = canvas;
        ctrlSo.FindProperty("laneContainer")   .objectReferenceValue = laneContainerGo.GetComponent<RectTransform>();
        ctrlSo.FindProperty("player")          .objectReferenceValue = playerGo.GetComponent<PlayerAvatar>();
        ctrlSo.FindProperty("raftLanePrefab")  .objectReferenceValue = raftLanePrefab.GetComponent<RaftLane>();
        ctrlSo.FindProperty("countdownText")   .objectReferenceValue = countdownGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("timerText")       .objectReferenceValue = timerGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("crossCountText")  .objectReferenceValue = crossGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("celebrationText") .objectReferenceValue = celebrationGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("resultPanel")     .objectReferenceValue = resultPanelGo;
        ctrlSo.FindProperty("resultText")      .objectReferenceValue = resultTextGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire references — PlayerAvatar ────────────────────────────────────
        var avatarSo = new SerializedObject(playerGo.GetComponent<PlayerAvatar>());
        avatarSo.FindProperty("characterImage").objectReferenceValue = charImgGo.GetComponent<Image>();
        avatarSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire references — ArtGenerator ───────────────────────────────────
        var artSo = new SerializedObject(artGen);
        artSo.FindProperty("skyImage")        .objectReferenceValue = skyGo.GetComponent<RawImage>();
        artSo.FindProperty("waterImage")      .objectReferenceValue = waterGo.GetComponent<RawImage>();
        artSo.FindProperty("leftBankImage")   .objectReferenceValue = leftBankGo.GetComponent<Image>();
        artSo.FindProperty("rightBankImage")  .objectReferenceValue = rightBankGo.GetComponent<Image>();
        artSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Mark dirty & save ─────────────────────────────────────────────────
        EditorUtility.SetDirty(controllerGo);
        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color         = color;
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    static GameObject MakeRawImage(string name, Transform parent, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        go.GetComponent<RawImage>().color         = color;
        go.GetComponent<RawImage>().raycastTarget = false;
        return go;
    }

    static GameObject MakeTMP(string name, Transform parent, string text, int fontSize)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text              = text;
        tmp.fontSize          = fontSize;
        tmp.color             = Color.white;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.raycastTarget     = false;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin       = Vector2.zero;
        rt.anchorMax       = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta       = Vector2.zero;
        rt.offsetMin       = Vector2.zero;
        rt.offsetMax       = Vector2.zero;
    }

    // ── Folders & Build Settings ──────────────────────────────────────────────

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Scenes");
        EnsureFolder(SceneFolder);
        EnsureFolder("Assets/Game/Prefabs");
        EnsureFolder(PrefabFolder);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
        string child  = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == scenePath) return;

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[RiverCrossSceneBuilder] Added to Build Settings: {scenePath}");
    }

    static string ToAbs(string assetPath)
        => Path.Combine(Application.dataPath, "..", assetPath).Replace("\\", "/");
}
