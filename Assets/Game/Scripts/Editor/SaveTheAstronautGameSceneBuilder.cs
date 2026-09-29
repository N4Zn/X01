using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "Save The Astronaut" — 2 làn UI thuần 2D (không camera 3D), mỗi làn 12 chặng, mỗi
/// chặng 1 cặp nút hình thang (TrapezoidImage) trôi từ xa tới gần. Menu: Tools > SaveTheAstronaut >
/// Build Scene.
///
/// Không dùng art asset thật — placeholder màu phẳng (đúng tinh thần MiniGameSceneBuilderHelpers),
/// ảnh hành tinh/hố đen/Trái Đất/audio thuyết minh nạp qua Resources/SaveTheAstronaut/ lúc runtime,
/// game tự bỏ qua nếu chưa có (xem SaveTheAstronautLane).
/// </summary>
public static class SaveTheAstronautGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/SaveTheAstronautGame";
    const string ScenePath = "Assets/Game/Scenes/SaveTheAstronautGame/SaveTheAstronautGame.unity";

    static readonly Color LaneBg = new(0.05f, 0.05f, 0.14f, 1f);
    static readonly Color TileBg = new(0.08f, 0.08f, 0.20f, 1f);

    [MenuItem("Tools/SaveTheAstronaut/Build Scene")]
    public static void BuildAll()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game");
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        BuildScene();

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SaveTheAstronautGameSceneBuilder] Done — scene created: " + ScenePath);
    }

    static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MiniGameSceneBuilderHelpers.CreateMainCamera(new Color(0.02f, 0.02f, 0.06f));
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);
        HideDeepChild(hud.transform, "LeftStarIcon");

        var laneLeftGo = BuildLane("LaneLeft", canvasGo.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.88f), isLeftLane: true);
        var laneRightGo = BuildLane("LaneRight", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0.88f), isLeftLane: false);

        var countdownGo = MiniGameSceneBuilderHelpers.CreateTmpText("CountdownText", canvasGo.transform, "3", 120, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(countdownGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -80f), new Vector2(150f, 80f));
        countdownGo.GetComponent<TextMeshProUGUI>().color = Color.white;
        countdownGo.SetActive(false);

        var resultPanelGo = MiniGameSceneBuilderHelpers.CreateImage("ResultPanel", canvasGo.transform, new Color(0f, 0f, 0f, 0.6f));
        MiniGameSceneBuilderHelpers.SetRect(resultPanelGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        resultPanelGo.SetActive(false);

        var resultTextGo = MiniGameSceneBuilderHelpers.CreateTmpText("ResultText", resultPanelGo.transform, "", 44, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(resultTextGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-350f, -80f), new Vector2(350f, 80f));
        resultTextGo.GetComponent<TextMeshProUGUI>().color = Color.white;

        var controllerGo = new GameObject("SaveTheAstronautController");
        var controller = controllerGo.AddComponent<SaveTheAstronautController>();

        var so = new SerializedObject(controller);
        so.FindProperty("laneLeft").objectReferenceValue = laneLeftGo.GetComponent<SaveTheAstronautLane>();
        so.FindProperty("laneRight").objectReferenceValue = laneRightGo.GetComponent<SaveTheAstronautLane>();
        so.FindProperty("gameHud").objectReferenceValue = hud;
        so.FindProperty("countdownText").objectReferenceValue = countdownGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("resultPanel").objectReferenceValue = resultPanelGo;
        so.FindProperty("resultText").objectReferenceValue = resultTextGo.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controllerGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    // ── 1 làn (1 đội) ─────────────────────────────────────────────────────────

    const int StepCount = 12; // khớp SaveTheAstronautConfigData.stepCount mặc định
    const float VisibleDepthSlots = 8f; // khớp SaveTheAstronautLane.visibleDepthSlots mặc định — chỉ dùng để đặt vị trí ban đầu hợp lý trước khi Play

    static GameObject BuildLane(string name, Transform canvasParent, Vector2 anchorMin, Vector2 anchorMax, bool isLeftLane)
    {
        var laneGo = new GameObject(name, typeof(RectTransform));
        laneGo.transform.SetParent(canvasParent, false);
        MiniGameSceneBuilderHelpers.SetRect(laneGo.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        var bg = MiniGameSceneBuilderHelpers.CreateImage("Background", laneGo.transform, LaneBg);
        MiniGameSceneBuilderHelpers.SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var stepNameGo = MiniGameSceneBuilderHelpers.CreateTmpText("StepNameText", laneGo.transform, "", 26, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(stepNameGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.90f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        stepNameGo.GetComponent<TextMeshProUGUI>().color = Color.white;

        // 12 hàng THẬT — dựng theo thứ tự XA TỚI GẦN (k=11 trước, k=0 sau cùng) để hàng gần (k nhỏ)
        // có sibling index cao hơn, vẽ đè lên hàng xa — khớp đúng thứ tự phối cảnh, tránh hàng gần bị
        // hàng xa che (dù về mặt kích thước hàng gần luôn to hơn nên hiếm khi thật sự bị che, vẫn nên
        // đúng thứ tự cho chắc).
        var leftRects = new Object[StepCount];
        var rightRects = new Object[StepCount];
        var leftButtons = new Object[StepCount];
        var rightButtons = new Object[StepCount];
        var leftTileImages = new Object[StepCount];
        var rightTileImages = new Object[StepCount];
        var leftReveals = new Object[StepCount];
        var rightReveals = new Object[StepCount];
        var leftOutlines = new Object[StepCount];
        var rightOutlines = new Object[StepCount];

        for (int k = StepCount - 1; k >= 0; k--)
        {
            var (lGo, lReveal, lOutline) = CreateTrapezoidTile($"TileLeft_{k}", laneGo.transform, isLeftOfPair: true, stepNumber: k + 1);
            var (rGo, rReveal, rOutline) = CreateTrapezoidTile($"TileRight_{k}", laneGo.transform, isLeftOfPair: false, stepNumber: k + 1);

            // Vị trí ban đầu hợp lý trước khi Play chạy Update() lần đầu — hàng 0 ở ngay mặt trước
            // (to/gần), các hàng sau xa dần theo đúng công thức depth mà SaveTheAstronautLane dùng.
            float depth = k + 1;
            SetInitialTransform(lGo.GetComponent<RectTransform>(), depth);
            SetInitialTransform(rGo.GetComponent<RectTransform>(), depth);
            bool visible = depth <= VisibleDepthSlots + 1f;
            lGo.SetActive(visible);
            rGo.SetActive(visible);

            leftRects[k] = lGo.GetComponent<RectTransform>();
            rightRects[k] = rGo.GetComponent<RectTransform>();
            leftButtons[k] = lGo.GetComponent<Button>();
            rightButtons[k] = rGo.GetComponent<Button>();
            leftTileImages[k] = lGo.GetComponent<TrapezoidImage>();
            rightTileImages[k] = rGo.GetComponent<TrapezoidImage>();
            leftReveals[k] = lReveal;
            rightReveals[k] = rReveal;
            leftOutlines[k] = lOutline;
            rightOutlines[k] = rOutline;
        }

        var astronautRect = CreateAstronautIcon(laneGo.transform);

        // GameObject TRƠN (không CreateImage — Image + RawImage không được gắn chung 1 GameObject,
        // AddComponent<RawImage> sẽ trả về null nếu GameObject đã có Graphic khác, gây
        // NullReferenceException ở dòng gán màu). Trái Đất = "hàng ảo thứ 13" — cùng công thức
        // depth/scale như 12 hàng trên (xem SaveTheAstronautLane.RefreshBoard).
        var earthGo = new GameObject("EarthPlaceholder", typeof(RectTransform));
        earthGo.transform.SetParent(laneGo.transform, false);
        var earthRect = earthGo.GetComponent<RectTransform>();
        earthRect.anchorMin = earthRect.anchorMax = new Vector2(0.5f, 0.5f);
        earthRect.pivot = new Vector2(0.5f, 0.5f);
        earthRect.sizeDelta = new Vector2(220f, 220f);
        SetInitialTransform(earthRect, StepCount + 1); // xa nhất trong tất cả — chỉ hiện khi đi hết 12 chặng
        var earthRaw = earthGo.AddComponent<RawImage>();
        earthRaw.color = new Color(0.2f, 0.45f, 0.85f, 1f); // placeholder xanh — thay texture Trái Đất thật ở Resources/SaveTheAstronaut/earth_map.png
        var earthSpin = earthGo.AddComponent<EarthGlobeSpin>();
        var earthSpinSo = new SerializedObject(earthSpin);
        earthSpinSo.FindProperty("targetImage").objectReferenceValue = earthRaw;
        earthSpinSo.ApplyModifiedPropertiesWithoutUndo();
        earthGo.SetActive(false);

        var laneComp = laneGo.AddComponent<SaveTheAstronautLane>();
        var laneSo = new SerializedObject(laneComp);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "rightButtons", rightButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "leftRects", leftRects);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "rightRects", rightRects);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "leftTileImages", leftTileImages);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "rightTileImages", rightTileImages);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "leftRevealImages", leftReveals);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "rightRevealImages", rightReveals);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "leftOutlines", leftOutlines);
        MiniGameSceneBuilderHelpers.AssignObjectArray(laneSo, "rightOutlines", rightOutlines);
        laneSo.FindProperty("earthRect").objectReferenceValue = earthRect;
        laneSo.FindProperty("stepNameText").objectReferenceValue = stepNameGo.GetComponent<TextMeshProUGUI>();
        laneSo.FindProperty("astronautRect").objectReferenceValue = astronautRect;
        laneSo.ApplyModifiedPropertiesWithoutUndo();

        return laneGo;
    }

    const float TileHalfWidth = 240f; // 2 nút cộng lại gần hết chiều rộng làn (~512px)

    // TileHeight tính ĐÚNG theo công thức để các hàng ghép liền mạch (không hở/không đè) với vị trí/
    // kích thước phối cảnh 1 điểm tụ (xem SaveTheAstronautLane.ApplyTravel):
    //   TileHeight = 2·|captureY−spawnY|·(1−ratio)/(1+ratio)
    // Đổi spawnY/captureY/ratio (trapezoidTopWidthRatio) thì PHẢI tính lại hằng số này cho khớp.
    const float TileHeight = 190f;

    static void SetInitialTransform(RectTransform rect, float depth)
    {
        const float spawnY = 240f, captureY = -140f, ratio = 0.6f; // PHẢI khớp SaveTheAstronautLane (spawnY/captureY/trapezoidTopWidthRatio)
        float scale = Mathf.Pow(ratio, depth);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, spawnY + (captureY - spawnY) * scale);
        rect.localScale = Vector3.one * scale;
    }

    /// <summary>1 nút HÌNH THANG VUÔNG: TrapezoidImage (nền) + Button + Outline (viền đỏ khi hiện
    /// nguy hiểm) + 1 Image con (ảnh reveal: hành tinh/hố đen) + số thứ tự chặng CỐ ĐỊNH (đặt ở cạnh
    /// NGOÀI, set 1 lần lúc dựng scene — không đổi lúc runtime vì mỗi tile giờ gắn cố định với đúng
    /// 1 chặng suốt game). Anchor là 1 ĐIỂM cố định ở TÂM làn (0.5, 0.5) cho CẢ 2 nút — khác nhau ở
    /// pivot: nút trái pivot=(1, 0.5) (mép PHẢI cố định đúng tâm làn), nút phải pivot=(0, 0.5) (mép
    /// TRÁI cố định đúng tâm làn). Nhờ vậy khi SaveTheAstronautLane tween localScale (xa→gần), mép
    /// GIÁP NHAU ở giữa 2 nút LUÔN đứng yên — 2 nút luôn sát nhau không hở, cạnh thẳng đứng của hình
    /// thang trùng đúng mép giáp đó, cạnh xiên nằm ở mép ngoài — ghép lại đúng 1 hình thang cân lớn
    /// như ảnh minh hoạ.</summary>
    static (GameObject go, Image revealImage, Outline outline) CreateTrapezoidTile(
        string name, Transform parent, bool isLeftOfPair, int stepNumber)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TrapezoidImage), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = isLeftOfPair ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(TileHalfWidth, TileHeight);
        rect.anchoredPosition = Vector2.zero;

        var tileImage = go.GetComponent<TrapezoidImage>();
        tileImage.color = TileBg;
        // Cạnh thẳng đứng luôn ở mép GIÁP NHAU (trong) — trái: mép phải; phải: mép trái.
        tileImage.verticalEdgeOnLeft = !isLeftOfPair;

        var button = go.GetComponent<Button>();
        button.targetGraphic = tileImage;

        // Bo viền bật sẵn (màu/độ dày thật do SaveTheAstronautLane tự set lúc runtime, đây chỉ là
        // giá trị hiển thị trong lúc đếm ngược trước khi Update() chạy lần đầu).
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = true;

        var revealGo = new GameObject("Reveal", typeof(RectTransform), typeof(Image));
        revealGo.transform.SetParent(go.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(revealGo.GetComponent<RectTransform>(), new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
        var revealImage = revealGo.GetComponent<Image>();
        revealImage.preserveAspect = true;
        revealImage.raycastTarget = false;
        revealImage.enabled = false;

        CreateNumberLabel(go.transform, outerOnLocalLeft: isLeftOfPair, stepNumber);

        return (go, revealImage, outline);
    }

    /// <summary>Nhãn số thứ tự chặng — đặt gần cạnh NGOÀI (cạnh xiên) của tile, phía trên, CỐ ĐỊNH
    /// (không đổi lúc runtime). Tile trái (isLeftOfPair=true) có cạnh xiên ở local x≈0 (bên trong
    /// sizeDelta của CHÍNH NÓ, không phải canvas) → outerOnLocalLeft=true; tile phải thì cạnh xiên ở
    /// local x≈1 → false.</summary>
    static void CreateNumberLabel(Transform tileParent, bool outerOnLocalLeft, int stepNumber)
    {
        var go = MiniGameSceneBuilderHelpers.CreateTmpText("StepNumber", tileParent, stepNumber.ToString(), 30, TextAlignmentOptions.Center);
        var rect = go.GetComponent<RectTransform>();
        Vector2 anchor = outerOnLocalLeft ? new Vector2(0.16f, 0.82f) : new Vector2(0.84f, 0.82f);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(60f, 40f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    // ── Phi hành gia / tên lửa (placeholder — chưa có art thật) ────────────────

    static Sprite _circleSpr;

    /// <summary>Icon phi hành gia/tên lửa GHÉP TỪ HÌNH TRÒN thủ công (thân dài dạng viên nang +
    /// mũ + kính) — chưa có art thật, dựng tạm bằng hình khối để LUÔN có hình thay vì để trống,
    /// đặt cố định gần đáy làn (vị trí "người chơi đứng"/tên lửa). Thay bằng sprite thật sau tại
    /// Resources/SaveTheAstronaut/ — chỉ cần đổi Image.sprite trên object "AstronautIcon".</summary>
    static RectTransform CreateAstronautIcon(Transform laneParent)
    {
        _circleSpr ??= MakeCircleSprite(128);

        var root = new GameObject("AstronautIcon", typeof(RectTransform));
        root.transform.SetParent(laneParent, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(90f, 110f);
        rootRect.anchoredPosition = new Vector2(0f, 16f);

        var body = CreateCircleImage("RocketBody", root.transform, new Color(0.92f, 0.92f, 0.95f, 1f));
        MiniGameSceneBuilderHelpers.SetRect(body.GetComponent<RectTransform>(), new Vector2(0.20f, 0f), new Vector2(0.80f, 0.62f), Vector2.zero, Vector2.zero);

        var helmet = CreateCircleImage("Helmet", root.transform, Color.white);
        var helmetRect = helmet.GetComponent<RectTransform>();
        helmetRect.anchorMin = helmetRect.anchorMax = new Vector2(0.5f, 0.78f);
        helmetRect.pivot = new Vector2(0.5f, 0.5f);
        helmetRect.sizeDelta = new Vector2(58f, 58f);

        var visor = CreateCircleImage("Visor", helmet.transform, new Color(0.35f, 0.65f, 0.95f, 1f));
        var visorRect = visor.GetComponent<RectTransform>();
        visorRect.anchorMin = new Vector2(0.18f, 0.18f);
        visorRect.anchorMax = new Vector2(0.82f, 0.82f);
        visorRect.offsetMin = visorRect.offsetMax = Vector2.zero;

        return rootRect;
    }

    static GameObject CreateCircleImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = _circleSpr;
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            byte a = (byte)(Mathf.Clamp01((r - d) * 0.5f) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    static void HideDeepChild(Transform parent, string childName)
    {
        var found = FindDeepChild(parent, childName);
        if (found != null) found.gameObject.SetActive(false);
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
