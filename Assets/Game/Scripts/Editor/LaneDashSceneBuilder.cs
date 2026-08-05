using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tạo toàn bộ scene 3D cho game LaneDash (né vật cản trên cánh đồng, 2 bên, 3 làn/bên, camera
/// phối cảnh split-screen). Menu: Tools > LaneDash > Build Scene
///
/// Kiến trúc: 2 Camera perspective, mỗi Camera chiếm nửa màn hình (viewport rect) và chỉ render
/// Unity Layer riêng của bên đó (cullingMask) — 2 bên dùng CHUNG toạ độ world X cho làn (tách biệt
/// hoàn toàn nhờ layer, không cần offset toạ độ). HUD/countdown/kết quả vẫn là 1 Canvas
/// ScreenSpaceOverlay phủ lên trên (theo đúng cách SolarSystemSceneBuilder tách UI khỏi world 3D).
///
/// Vật cản/thưởng/vật phẩm KHÔNG dùng prefab lưu sẵn — LaneDashItemVisuals.CreateVisual dựng
/// runtime (mesh Kenney NatureKit cho Bush/Rock, primitive màu cho các biến thể còn lại).
/// </summary>
public static class LaneDashSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/LaneDashGame";
    const string ScenePath   = "Assets/Game/Scenes/LaneDashGame/LaneDashGame.unity";

    const int LANE_COUNT        = 3;
    const float LANE_WIDTH      = 2f;
    const float SPAWN_DISTANCE_Z = 40f;
    const float PLAYER_Z        = 0f;
    const float CAM_HEIGHT      = 3f;
    const float CAM_BACK_OFFSET = 5f;

    // Trang trí mặt đường (tile đường đất, bụi cỏ) trải dọc track trong khoảng [GROUND_START_Z,
    // GROUND_END_Z] — LaneGroundScroller dùng GROUND_TRACK_LENGTH để "quấn vòng" khi cuộn.
    const float GROUND_START_Z     = PLAYER_Z - 5f;
    const float GROUND_END_Z       = PLAYER_Z + SPAWN_DISTANCE_Z + 5f;
    const float GROUND_TRACK_LENGTH = GROUND_END_Z - GROUND_START_Z;

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/LaneDash/Build Scene")]
    public static void BuildAll()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game");
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        int layerLeft  = EnsureLayer("LaneLeft");
        int layerRight = EnsureLayer("LaneRight");

        BuildScene(layerLeft, layerRight);

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LaneDashSceneBuilder] Done — scene 3D created: " + ScenePath);
    }

    // ── Scene build ───────────────────────────────────────────────────────────

    static void BuildScene(int layerLeft, int layerRight)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupLighting();

        CreateSideCamera("CameraLeft",  new Rect(0f, 0f, 0.5f, 1f), layerLeft,  isMain: true);
        CreateSideCamera("CameraRight", new Rect(0.5f, 0f, 0.5f, 1f), layerRight, isMain: false);

        var groundScrollerLeft  = CreateGroundScroller("GroundScrollerLeft");
        var groundScrollerRight = CreateGroundScroller("GroundScrollerRight");

        CreateDirtPath("RoadLeft",  layerLeft,  groundScrollerLeft);
        CreateDirtPath("RoadRight", layerRight, groundScrollerRight);
        CreateGrassDividers("RoadLeft",  layerLeft,  groundScrollerLeft);
        CreateGrassDividers("RoadRight", layerRight, groundScrollerRight);

        var trackLeftGo  = new GameObject("LaneTrackLeft", typeof(LaneTrack));
        var trackRightGo = new GameObject("LaneTrackRight", typeof(LaneTrack));

        // Dựng AnimatorController + Avatar 1 LẦN DUY NHẤT, dùng chung cho cả 2 nhân vật — tránh
        // lặp lại bug cũ (nhân vật 2 xoá mất asset nhân vật 1 đang tham chiếu giữa chừng).
        var (runController, runAvatar) = BuildPlayerRunSetup();

        var playerLeftGo  = CreatePlayerAvatar("PlayerLeft",  new Color(0.30f, 0.55f, 0.95f), layerLeft,  runController, runAvatar);
        var playerRightGo = CreatePlayerAvatar("PlayerRight", new Color(0.95f, 0.35f, 0.35f), layerRight, runController, runAvatar);

        // ── UI overlay (Canvas ScreenSpaceOverlay — KHÔNG đổi so với bản 2D cũ) ──
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);
        HideHudExtras(hud);

        var uiRoot = new GameObject("UI", typeof(RectTransform));
        uiRoot.transform.SetParent(canvasGo.transform, false);
        StretchFull(uiRoot.GetComponent<RectTransform>());

        var countdownGo = MiniGameSceneBuilderHelpers.CreateTmpText("CountdownText", uiRoot.transform, "3", 120, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(countdownGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -80f), new Vector2(150f, 80f));
        countdownGo.GetComponent<TextMeshProUGUI>().color = Color.white;
        countdownGo.SetActive(false);

        var resultPanelGo = MiniGameSceneBuilderHelpers.CreateImage("ResultPanel", uiRoot.transform, new Color(0f, 0f, 0f, 0.6f));
        StretchFull(resultPanelGo.GetComponent<RectTransform>());
        resultPanelGo.SetActive(false);

        var resultTextGo = MiniGameSceneBuilderHelpers.CreateTmpText("ResultText", resultPanelGo.transform, "", 44, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(resultTextGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-350f, -80f), new Vector2(350f, 80f));
        resultTextGo.GetComponent<TextMeshProUGUI>().color = Color.white;

        // Thông số mỗi bên (🎁 phần thưởng / tốc độ / quãng đường) — 3 dòng xếp dọc ngay dưới
        // thanh HUD, khớp cột điểm Left/Right của CreateGameHud (x 0.20-0.40 / 0.60-0.80).
        var rewardLeftGo   = CreateStatText("RewardTextLeft",   uiRoot.transform, "🎁 0",      0.20f, 0.40f, 0.80f, 0.88f);
        var speedLeftGo    = CreateStatText("SpeedTextLeft",    uiRoot.transform, "0.0 km/h",  0.20f, 0.40f, 0.72f, 0.80f);
        var distanceLeftGo = CreateStatText("DistanceTextLeft", uiRoot.transform, "0 m",       0.20f, 0.40f, 0.64f, 0.72f);

        var rewardRightGo   = CreateStatText("RewardTextRight",   uiRoot.transform, "🎁 0",      0.60f, 0.80f, 0.80f, 0.88f);
        var speedRightGo    = CreateStatText("SpeedTextRight",    uiRoot.transform, "0.0 km/h", 0.60f, 0.80f, 0.72f, 0.80f);
        var distanceRightGo = CreateStatText("DistanceTextRight", uiRoot.transform, "0 m",      0.60f, 0.80f, 0.64f, 0.72f);

        // ── Controller ────────────────────────────────────────────────────────
        var controllerGo = new GameObject("LaneDashController");
        var controller = controllerGo.AddComponent<LaneDashController>();

        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("trackLeft")      .objectReferenceValue = trackLeftGo.GetComponent<LaneTrack>();
        ctrlSo.FindProperty("trackRight")     .objectReferenceValue = trackRightGo.GetComponent<LaneTrack>();
        ctrlSo.FindProperty("avatarLeft")     .objectReferenceValue = playerLeftGo.GetComponent<LaneRunnerAvatar>();
        ctrlSo.FindProperty("avatarRight")    .objectReferenceValue = playerRightGo.GetComponent<LaneRunnerAvatar>();
        ctrlSo.FindProperty("groundScrollLeft") .objectReferenceValue = groundScrollerLeft;
        ctrlSo.FindProperty("groundScrollRight").objectReferenceValue = groundScrollerRight;
        ctrlSo.FindProperty("gameHud")        .objectReferenceValue = hud;
        ctrlSo.FindProperty("countdownText")  .objectReferenceValue = countdownGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("resultPanel")    .objectReferenceValue = resultPanelGo;
        ctrlSo.FindProperty("resultText")     .objectReferenceValue = resultTextGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("rewardTextLeft")   .objectReferenceValue = rewardLeftGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("speedTextLeft")    .objectReferenceValue = speedLeftGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("distanceTextLeft") .objectReferenceValue = distanceLeftGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("rewardTextRight")  .objectReferenceValue = rewardRightGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("speedTextRight")   .objectReferenceValue = speedRightGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.FindProperty("distanceTextRight").objectReferenceValue = distanceRightGo.GetComponent<TextMeshProUGUI>();
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire references — LaneTrack (layer riêng mỗi bên) ─────────────────
        var trackLeftSo = new SerializedObject(trackLeftGo.GetComponent<LaneTrack>());
        trackLeftSo.FindProperty("itemLayer").intValue = layerLeft;
        trackLeftSo.ApplyModifiedPropertiesWithoutUndo();

        var trackRightSo = new SerializedObject(trackRightGo.GetComponent<LaneTrack>());
        trackRightSo.FindProperty("itemLayer").intValue = layerRight;
        trackRightSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Mark dirty & save ─────────────────────────────────────────────────
        EditorUtility.SetDirty(controllerGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    // ── Lighting ──────────────────────────────────────────────────────────────

    static void SetupLighting()
    {
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.40f);

        var dirLightGo = new GameObject("Directional Light");
        var dirLight   = dirLightGo.AddComponent<Light>();
        dirLight.type      = LightType.Directional;
        dirLight.intensity = 1.1f;
        dirLight.color     = new Color(1f, 0.98f, 0.90f);
        dirLightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    static Camera CreateSideCamera(string name, Rect viewportRect, int cullLayer, bool isMain)
    {
        var go  = new GameObject(name);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f); // trời xanh nhạt
        cam.fieldOfView     = 60f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 100f;
        cam.rect            = viewportRect;
        cam.cullingMask     = (1 << cullLayer) | (1 << 0); // layer riêng bên này + Default

        if (isMain)
        {
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>(); // chỉ 1 AudioListener toàn scene
        }

        go.transform.position = new Vector3(0f, CAM_HEIGHT, PLAYER_Z - CAM_BACK_OFFSET);
        go.transform.LookAt(new Vector3(0f, 0.5f, PLAYER_Z + 15f));

        return cam;
    }

    // ── World objects ─────────────────────────────────────────────────────────

    const string DirtPathTileResourcePath = "kenney_nature-kit/Models/FBX format/ground_pathStraight";
    const string GrassTuftResourcePath    = "kenney_nature-kit/Models/FBX format/grass";

    /// <summary>Tạo 1 GameObject LaneGroundScroller dùng chung cho mọi tile đường đất + bụi cỏ của
    /// 1 bên — đăng ký ("Register") từng object vào đây để chúng cùng cuộn theo tốc độ thật.</summary>
    static LaneGroundScroller CreateGroundScroller(string name)
    {
        var go = new GameObject(name);
        var scroller = go.AddComponent<LaneGroundScroller>();
        scroller.Init(GROUND_START_Z, GROUND_TRACK_LENGTH);
        return scroller;
    }

    /// <summary>Dựng đường đất bằng cách lặp lại tile ground_pathStraight (Kenney Nature Kit) dọc
    /// track thay vì 1 Plane phẳng tô màu — đo kích thước tile thật bằng Renderer.bounds (không
    /// biết trước trục nào là "chiều dài" trong file FBX gốc) để ghép khít, không hở/chồng. Mỗi
    /// tile được đăng ký vào scroller để cuộn theo tốc độ ("mặt đường chạy theo").</summary>
    static void CreateDirtPath(string name, int layer, LaneGroundScroller scroller)
    {
        var tilePrefab = Resources.Load<GameObject>(DirtPathTileResourcePath);
        if (tilePrefab == null)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy '{DirtPathTileResourcePath}' — fallback về Plane phẳng màu đất (không cuộn).");
            CreateFallbackGroundPlane(name, layer);
            return;
        }

        // Đo 1 tile mẫu để biết kích thước thật (world-space, rotation identity) trước khi ghép hàng loạt.
        var probe = Object.Instantiate(tilePrefab);
        var probeRenderer = probe.GetComponentInChildren<Renderer>();
        Vector3 size = probeRenderer != null ? probeRenderer.bounds.size : new Vector3(2f, 0.2f, 2f);
        Object.DestroyImmediate(probe);

        // Tile có thể dài theo X hoặc Z tuỳ cách model gốc dựng — chọn trục dài hơn làm "chiều dài",
        // xoay 90° nếu cần để chiều dài đó nằm dọc track (trục Z).
        bool rotate90 = size.x > size.z;
        float tileLength = rotate90 ? size.x : size.z;
        float tileWidth  = rotate90 ? size.z : size.x;
        if (tileLength < 0.1f) tileLength = 2f;
        if (tileWidth  < 0.1f) tileWidth  = 2f;

        float pathWidth = LANE_COUNT * LANE_WIDTH + 2f;
        float widthScale = pathWidth / tileWidth;
        int tileCount = Mathf.CeilToInt(GROUND_TRACK_LENGTH / tileLength);

        var container = new GameObject(name);
        container.layer = layer;

        for (int i = 0; i < tileCount; i++)
        {
            var tile = Object.Instantiate(tilePrefab, container.transform);
            tile.name = $"{name}_Tile{i}";
            float z = GROUND_START_Z + tileLength * (i + 0.5f);
            tile.transform.position      = new Vector3(0f, 0f, z);
            tile.transform.localRotation = rotate90 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            tile.transform.localScale    = rotate90 ? new Vector3(1f, 1f, widthScale) : new Vector3(widthScale, 1f, 1f);

            foreach (var col in tile.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);
            foreach (var t in tile.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            scroller.Register(tile.transform);
        }
    }

    /// <summary>Dự phòng nếu không tìm thấy mesh đường đất — Plane phẳng màu nâu đất, không tile.</summary>
    static void CreateFallbackGroundPlane(string name, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.layer = layer;

        float width  = LANE_COUNT * LANE_WIDTH + 2f;
        float length = SPAWN_DISTANCE_Z + 10f;
        go.transform.position   = new Vector3(0f, -0.02f, PLAYER_Z + (SPAWN_DISTANCE_Z - 5f) / 2f);
        go.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);

        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.45f, 0.33f, 0.20f); // nâu đất
        renderer.material = mat;
    }

    /// <summary>Hàng cỏ (grass.fbx) dọc theo 2 đường phân làn (giữa làn 0-1 và 1-2) — mô phỏng
    /// "giữa các lane là hàng cỏ". Mỗi bụi được đăng ký vào scroller để cuộn cùng đường đất.</summary>
    static void CreateGrassDividers(string namePrefix, int layer, LaneGroundScroller scroller)
    {
        var grassPrefab = Resources.Load<GameObject>(GrassTuftResourcePath);
        if (grassPrefab == null)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy '{GrassTuftResourcePath}' — bỏ qua hàng cỏ phân làn.");
            return;
        }

        var container = new GameObject($"{namePrefix}_GrassDividers");
        container.layer = layer;

        float center = (LANE_COUNT - 1) / 2f;
        const float spacing = 3f;

        for (int i = 0; i < LANE_COUNT - 1; i++)
        {
            float dividerX = (i + 0.5f - center) * LANE_WIDTH; // giữa làn i và làn i+1

            for (float z = GROUND_START_Z; z < GROUND_END_Z; z += spacing)
            {
                var tuft = Object.Instantiate(grassPrefab, container.transform);
                tuft.name = $"{namePrefix}_Grass_{i}";
                tuft.transform.position      = new Vector3(dividerX, 0f, z);
                tuft.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                foreach (var col in tuft.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(col);
                foreach (var t in tuft.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;

                scroller.Register(tuft.transform);
            }
        }
    }

    // Model nhân vật lấy từ "Hyper Casual Characters" (đã có sẵn texture/material riêng — không
    // cần tô màu phẳng như pack Kevin Iglesias trước đây). Animation Run vẫn TÁI DÙNG clip cũ của
    // "Kevin Iglesias Human Animations" — pack Hyper Casual không có clip chạy, chỉ có
    // walk/idle/sit/drip. Hoạt động được là nhờ Humanoid retargeting: 1 clip Humanoid có thể phát
    // trên BẤT KỲ Avatar Humanoid hợp lệ nào khác lúc runtime (animator.avatar), không phụ thuộc
    // avatar nào được dùng để decode clip lúc import.
    const string PlayerModelFolder            = "Hyper Casual Characters/Base Mesh";
    const string PlayerCharacterResourcePath  = PlayerModelFolder + "/stickman_1 1";
    const string PlayerModelAssetPath         = "Assets/Resources/" + PlayerCharacterResourcePath + ".fbx";
    const string PlayerRunAnimationAssetPath  = "Assets/Resources/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx";
    const string PlayerRunControllerAssetPath = "Assets/Game/Scenes/LaneDashGame/PlayerRunController.controller";

    // Avatar dùng để GIẢI MÃ clip Run gốc lúc import — PHẢI là avatar sinh từ đúng model đã dùng để
    // dựng ra file animation này (HumanM_Model, cùng gói Kevin Iglesias với chính clip), vì "Copy
    // From Other Avatar" chỉ copy bảng ánh xạ HumanBone→tên bone — nếu trỏ avatar của model KHÁC
    // (vd stickman, tên bone hoàn toàn khác: "root.x" thay vì tên bone gốc của Kevin Iglesias) thì
    // Unity không tìm thấy bone tương ứng trong chính file này, decode thất bại, ra Clip Count = 0.
    // Avatar THẬT chạy trong game (stickman, xem PlayerModelAssetPath) tách riêng, gán trực tiếp
    // vào Animator lúc chơi — Humanoid retargeting giữa 2 avatar khác nhau chỉ áp dụng ở bước NÀY
    // (theo muscle-space đã giải mã xong), không áp dụng ngược lại lúc import.
    const string ReferenceHumanoidModelAssetPath = "Assets/Resources/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";

    static GameObject CreatePlayerAvatar(string name, Color color, int layer, AnimatorController runController, Avatar runAvatar)
    {
        var prefab = Resources.Load<GameObject>(PlayerCharacterResourcePath);
        var go = prefab != null ? Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;

        // Mesh nhân vật (SkinnedMeshRenderer) + collider thường nằm ở object CON, không phải gốc —
        // GetComponent (không phải InChildren) sẽ trả null và crash dòng gán material bên dưới.
        foreach (var col in go.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(col);

        // Gán layer cho CẢ CÂY CON — Camera lọc theo layer của từng Renderer, không kế thừa từ cha.
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        var renderer = go.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            if (renderer is SkinnedMeshRenderer smr)
                smr.updateWhenOffscreen = true; // tránh Unity cull nhầm khi animation di chuyển xương ra ngoài bounds tĩnh ban đầu

            // Model đã có texture/material riêng (khác pack Kevin Iglesias trước đây) — giữ nguyên,
            // không tạo material mới đè lên. `renderer.material` (không phải sharedMaterial) tự
            // clone ra bản instance riêng, không sửa vào asset gốc trong Project.
            if (color != Color.white)
                renderer.material.color = color;
        }
        else
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] '{name}': không tìm thấy Renderer nào trong '{PlayerCharacterResourcePath}' — nhân vật sẽ không có màu/không hiện hình.");
        }

        var avatarComp = go.AddComponent<LaneRunnerAvatar>();
        var so = new SerializedObject(avatarComp);
        so.FindProperty("characterRenderer").objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (runController != null && runAvatar != null)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.avatar = runAvatar;
            animator.runtimeAnimatorController = runController;
            animator.applyRootMotion = false; // nhân vật đứng yên tại chỗ, không trôi theo animation "chạy tới trước"
        }

        return go;
    }

    /// <summary>
    /// Dựng AnimatorController (chạy clip Run) + Avatar THẬT (stickman) dùng chung cho cả 2 nhân
    /// vật — gọi 1 LẦN DUY NHẤT trong BuildScene trước khi tạo nhân vật nào, tránh lặp lại bug cũ
    /// (nhân vật 2 xoá mất asset nhân vật 1 đang tham chiếu).
    ///
    /// Quy trình Humanoid ĐÚNG (đã xác nhận thủ công trong Editor, xem thêm chú thích tại
    /// ReferenceHumanoidModelAssetPath):
    ///   1. Model gốc của clip (HumanM_Model.fbx) → Create From This Model → avatar "giải mã".
    ///   2. File animation (HumanM@Run01_Forward.fbx) → Copy From Other Avatar, Source = avatar (1)
    ///      → Unity giải mã đúng bone của chính nó, ra AnimationClip hợp lệ (muscle-space).
    ///   3. Model THẬT đang dùng trong game (stickman) → Create From This Model → avatar "chạy".
    ///   4. Animator trên nhân vật trong scene: Avatar = avatar (3), Controller chứa clip (2),
    ///      Apply Root Motion = OFF — Unity tự retarget muscle-space sang đúng khung xương (3).
    /// </summary>
    static (AnimatorController controller, Avatar avatar) BuildPlayerRunSetup()
    {
        var decodeAvatar = EnsureHumanoidModelAvatar(ReferenceHumanoidModelAssetPath);
        if (decodeAvatar == null)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không sinh được Avatar giải mã từ '{ReferenceHumanoidModelAssetPath}' — nhân vật sẽ đứng yên (T-pose).");
            return (null, null);
        }

        EnsureHumanoidAnimationRig(PlayerRunAnimationAssetPath, decodeAvatar);

        // Lấy TẤT CẢ clip trong file (có thể có sub-asset tham chiếu/T-pose tĩnh đi kèm, thứ tự đọc
        // không đảm bảo) — ưu tiên clip có tên chứa "run", log toàn bộ tên tìm được để kiểm chứng.
        AnimationClip clip = null;
        var clipNames = new System.Collections.Generic.List<string>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PlayerRunAnimationAssetPath))
        {
            if (asset is not AnimationClip c) continue;
            clipNames.Add(c.name);
            if (clip == null || c.name.ToLowerInvariant().Contains("run"))
                clip = c;
        }
        Debug.Log($"[LaneDashSceneBuilder] AnimationClip tìm thấy trong '{PlayerRunAnimationAssetPath}': [{string.Join(", ", clipNames)}] — chọn dùng '{clip?.name}'.");

        // Avatar THẬT chạy trong game (stickman) — tách riêng khỏi avatar giải mã ở trên, gán vào
        // Animator lúc chơi (xem CreatePlayerAvatar).
        var runtimeAvatar = EnsureHumanoidModelAvatar(PlayerModelAssetPath);
        if (runtimeAvatar == null)
            Debug.LogWarning($"[LaneDashSceneBuilder] Không sinh được Avatar chạy từ '{PlayerModelAssetPath}' — nhân vật sẽ đứng yên (T-pose).");

        if (clip == null)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy AnimationClip trong '{PlayerRunAnimationAssetPath}' — nhân vật sẽ đứng yên (T-pose).");
            return (null, runtimeAvatar);
        }
        clip.wrapMode = WrapMode.Loop;

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerRunControllerAssetPath) != null)
            AssetDatabase.DeleteAsset(PlayerRunControllerAssetPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerRunControllerAssetPath);
        var stateMachine = controller.layers[0].stateMachine;
        var state = stateMachine.AddState("Run");
        state.motion = clip;
        state.speed = 1f;
        stateMachine.defaultState = state;

        return (controller, runtimeAvatar);
    }

    /// <summary>Ép model về Humanoid + tự sinh Avatar (Create From This Model), trả về Avatar vừa sinh.</summary>
    static Avatar EnsureHumanoidModelAvatar(string modelAssetPath)
    {
        if (AssetImporter.GetAtPath(modelAssetPath) is not ModelImporter importer)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy ModelImporter cho '{modelAssetPath}'.");
            return null;
        }

        if (importer.animationType != ModelImporterAnimationType.Human
            || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelAssetPath))
            if (asset is Avatar avatar)
                return avatar;

        return null;
    }

    /// <summary>Ép file animation về Humanoid + Copy From Other Avatar, trỏ vào avatar của model gốc.</summary>
    static void EnsureHumanoidAnimationRig(string animAssetPath, Avatar sourceAvatar)
    {
        if (AssetImporter.GetAtPath(animAssetPath) is not ModelImporter importer)
        {
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy ModelImporter cho '{animAssetPath}'.");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = sourceAvatar;
        EnsureLoopingClip(importer, "run");
        importer.SaveAndReimport();
    }

    /// <summary>Bật Loop Time + Loop Pose cho clip có tên chứa <paramref name="clipNameContains"/> —
    /// Loop Pose yêu cầu Unity tự điều chỉnh root motion để khớp tư thế đầu/cuối, giảm hiện tượng
    /// "giật" khi clip lặp lại. Không sửa được nếu bản thân clip gốc không được quay/dựng thành 1
    /// chu kỳ khớp nhau — khi đó vẫn cần chỉnh tay Start/End frame trong Animation tab.</summary>
    static void EnsureLoopingClip(ModelImporter importer, string clipNameContains)
    {
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
        {
            if (!clips[i].name.ToLowerInvariant().Contains(clipNameContains)) continue;
            clips[i].loopTime = true;
            clips[i].loopPose = true;
        }

        importer.clipAnimations = clips;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    /// <summary>Ẩn bớt vài object con của GameHUD prefab dùng chung (Assets/Game/Prefabs/GameHUD.prefab)
    /// không phù hợp với LaneDash: Background (game đã có nền 3D riêng, khỏi cần nền 2D của HUD
    /// đè lên), LeftStarIcon (không dùng hope-star ở game này), Left/RightScoreBar (thanh fill
    /// điểm không cần thiết, đã có số điểm + 3 dòng thống kê riêng). Ẩn thay vì xoá hẳn để không
    /// đụng vào prefab dùng chung — game khác vẫn thấy đủ các phần này bình thường.</summary>
    static void HideHudExtras(GameHUD hud)
    {
        if (hud == null) return;

        HideChild(hud.transform, "Background");
        HideChild(hud.transform, "LeftStarIcon");
        HideChild(hud.transform, "P1/LeftScoreBar");
        HideChild(hud.transform, "P2/RightScoreBar");
    }

    static void HideChild(Transform root, string path)
    {
        var t = root.Find(path);
        if (t != null)
            t.gameObject.SetActive(false);
        else
            Debug.LogWarning($"[LaneDashSceneBuilder] Không tìm thấy '{path}' trong GameHUD prefab để ẩn — prefab có thể đã đổi cấu trúc, kiểm tra lại tên/đường dẫn.");
    }

    static GameObject CreateStatText(string name, Transform parent, string content, float xMin, float xMax, float yMin, float yMax)
    {
        var go = MiniGameSceneBuilderHelpers.CreateTmpText(name, parent, content, 20, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(go.GetComponent<RectTransform>(), new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);
        go.GetComponent<TextMeshProUGUI>().color = Color.white;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
    }

    // ── Layer setup (tạo Unity Layer mới qua TagManager.asset nếu chưa có) ─────

    static int EnsureLayer(string layerName)
    {
        var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets.Length == 0)
        {
            Debug.LogWarning("[LaneDashSceneBuilder] Không đọc được TagManager.asset — dùng layer Default (0), 2 bên có thể lẫn hình.");
            return 0;
        }

        var tagManager = new SerializedObject(tagManagerAssets[0]);
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < layersProp.arraySize; i++)
            if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                return i;

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }

        Debug.LogWarning($"[LaneDashSceneBuilder] Hết layer trống cho '{layerName}' — dùng layer Default (0), 2 bên có thể lẫn hình.");
        return 0;
    }
}
