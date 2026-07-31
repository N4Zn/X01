using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Primitives dùng chung để dựng scene mini-game mới bằng Editor script (MenuItem), theo đúng
/// pattern của các *SceneBuilder.cs hiện có (AddUpSceneBuilder, TestTongHopSceneBuilder, ...).
///
/// Khác với các SceneBuilder cũ (hard-code sprite nghệ thuật riêng của từng game), helper ở đây
/// chỉ dựng UI placeholder màu phẳng — đủ để chơi thử ngay, không phụ thuộc asset nghệ thuật.
/// Thay sprite/màu sau khi ý tưởng đã chạy đúng logic.
/// </summary>
public static class MiniGameSceneBuilderHelpers
{
    /// <summary>Prefab HUD mặc định của Kit — đã có sẵn component GameHUD + nghệ thuật riêng.
    /// Mọi game mới dựng từ _Template sẽ dùng prefab này trừ khi chỉ định path khác.</summary>
    public const string DefaultGameHudPrefabPath = "Assets/Game/Prefabs/GameHUD.prefab";

    // ── Folder / build settings ────────────────────────────────────────────────

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static string ToAbsolutePath(string assetPath)
    {
        string projectPath = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        return projectPath + "/" + assetPath;
    }

    public static void AddSceneToBuildSettings(string targetPath)
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == targetPath) return;

        var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
        newScenes[scenes.Length] = new EditorBuildSettingsScene(targetPath, true);
        EditorBuildSettings.scenes = newScenes;
    }

    // ── SerializedObject helpers ────────────────────────────────────────────────

    /// <summary>Gán 1 mảng object reference vào 1 property kiểu array (vd ButtonItem[]).
    /// Nhớ gọi so.ApplyModifiedPropertiesWithoutUndo() sau khi dùng.</summary>
    public static void AssignObjectArray(SerializedObject so, string propName, Object[] values)
    {
        var prop = so.FindProperty(propName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ── Camera / Canvas ────────────────────────────────────────────────────────

    public static void CreateMainCamera(Color? background = null)
    {
        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background ?? new Color(0.93f, 0.93f, 0.95f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
    }

    /// <summary>Canvas 1024x600 (chuẩn projector sàn) + EventSystem. Trả về Canvas GameObject.</summary>
    public static GameObject CreateCanvasWithEventSystem()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 600f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return canvasGo;
    }

    // ── Basic UI ───────────────────────────────────────────────────────────────

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static GameObject CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    public static GameObject CreateText(string name, Transform parent, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = content;
        text.alignment = anchor;
        text.fontSize = size;
        text.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return go;
    }

    public static GameObject CreateTmpText(string name, Transform parent, string content, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        // Không xuống dòng — chữ trong nút (đáp án, tên...) luôn 1 dòng, tự co nhỏ nếu quá dài
        // thay vì wrap, tránh vỡ layout nút nhỏ trên sàn chiếu.
        // Set cả 2 property: bản TMP mới dùng textWrappingMode (enum), enableWordWrapping (bool
        // cũ) ở 1 số bản chỉ là getter/no-op — set cả 2 để chắc chắn ăn theo mọi phiên bản.
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.4f;
        tmp.fontSizeMax = size;
        return go;
    }

    public static GameObject CreateSimpleButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1f);
        SetRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        var textGo = CreateText("Text", go.transform, label, 24, TextAnchor.MiddleCenter);
        SetRect(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go;
    }

    /// <summary>Thêm hoạ tiết "nơ quà" (2 dải trắng chữ thập) phía sau label của 1 button —
    /// dùng cho hộp quà bí mật để trông giống món quà thay vì ô vuông trơn. Đẩy xuống cuối
    /// (sibling index 0) để không đè lên chữ/label đã có.</summary>
    public static void AddGiftRibbon(GameObject box)
    {
        var h = CreateImage("RibbonH", box.transform, new Color(1f, 1f, 1f, 0.85f));
        SetRect(h.GetComponent<RectTransform>(), new Vector2(0f, 0.40f), new Vector2(1f, 0.60f), Vector2.zero, Vector2.zero);
        h.GetComponent<Image>().raycastTarget = false;
        h.transform.SetSiblingIndex(0);

        var v = CreateImage("RibbonV", box.transform, new Color(1f, 1f, 1f, 0.85f));
        SetRect(v.GetComponent<RectTransform>(), new Vector2(0.40f, 0f), new Vector2(0.60f, 1f), Vector2.zero, Vector2.zero);
        v.GetComponent<Image>().raycastTarget = false;
        v.transform.SetSiblingIndex(0);
    }

    // ── ButtonItem (đáp án dạng nút — dùng cho ButtonDisplay) ──────────────────

    /// <summary>
    /// Dựng 1 ButtonItem đầy đủ 3 media slot (Text/Image/IconCompose) — bắt buộc phải có cả 3
    /// GameObject (ItemMediaHelper.ApplyMedia SetActive cả 3 bất kể mediaType nào).
    /// </summary>
    public static GameObject CreateButtonItem(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ButtonItem));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.55f, 0.75f, 0.95f, 1f);

        // Font nhỏ hơn hẳn mặc định — chữ dài kiểu "Grandma"/"Grandpa" vẫn từng bị xuống dòng dù
        // đã tắt wrap + auto-size (nghi TMP không co kịp khi RectTransform nút hẹp). Giảm size cứng
        // luôn cho chắc thay vì trông chờ auto-size.
        var textSlot = CreateTmpText("TextSlot", go.transform, "", 22, TextAlignmentOptions.Center);
        textSlot.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        SetRect(textSlot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var imageSlot = new GameObject("ImageSlot", typeof(RectTransform), typeof(Image));
        imageSlot.transform.SetParent(go.transform, false);
        SetRect(imageSlot.GetComponent<RectTransform>(), new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);
        imageSlot.GetComponent<Image>().preserveAspect = true;
        imageSlot.SetActive(false);

        var iconSlot = new GameObject("IconSlot", typeof(RectTransform), typeof(GridLayoutGroup));
        iconSlot.transform.SetParent(go.transform, false);
        SetRect(iconSlot.GetComponent<RectTransform>(), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);
        var grid = iconSlot.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(24f, 24f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        iconSlot.SetActive(false);

        var so = new SerializedObject(go.GetComponent<ButtonItem>());
        so.FindProperty("bgImage").objectReferenceValue = bg;
        so.FindProperty("textSlot").objectReferenceValue = textSlot;
        so.FindProperty("textLabel").objectReferenceValue = textSlot.GetComponent<TextMeshProUGUI>();
        so.FindProperty("imageSlot").objectReferenceValue = imageSlot;
        so.FindProperty("imageHolder").objectReferenceValue = imageSlot.GetComponent<Image>();
        so.FindProperty("iconSlot").objectReferenceValue = iconSlot;
        so.FindProperty("iconContainer").objectReferenceValue = iconSlot.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    /// <summary>Dựng 1 nhóm ButtonItem theo hàng dọc trong 1 nửa màn hình.</summary>
    public static ButtonItem[] CreateButtonGroup(string prefix, Transform parent, Vector2 areaMin, Vector2 areaMax, int count)
    {
        var items = new ButtonItem[count];
        float slotHeight = (areaMax.y - areaMin.y) / count;
        for (int i = 0; i < count; i++)
        {
            float yMax = areaMax.y - i * slotHeight;
            float yMin = yMax - slotHeight + 0.02f;
            var go = CreateButtonItem($"{prefix}_{i}", parent,
                new Vector2(areaMin.x, yMin), new Vector2(areaMax.x, yMax - 0.02f));
            items[i] = go.GetComponent<ButtonItem>();
        }
        return items;
    }

    /// <summary>Dựng 1 nhóm ButtonItem theo hàng ngang — phù hợp sàn chiếu: người chơi đứng ở
    /// cạnh dưới màn hình, đáp án xếp thành 1 hàng ngang sát cạnh đó để bước/nhảy vào.</summary>
    public static ButtonItem[] CreateButtonGroupHorizontal(string prefix, Transform parent, Vector2 areaMin, Vector2 areaMax, int count)
    {
        var items = new ButtonItem[count];
        float slotWidth = (areaMax.x - areaMin.x) / count;
        for (int i = 0; i < count; i++)
        {
            float xMin = areaMin.x + i * slotWidth;
            float xMax = xMin + slotWidth;
            var go = CreateButtonItem($"{prefix}_{i}", parent,
                new Vector2(xMin + 0.01f, areaMin.y), new Vector2(xMax - 0.01f, areaMax.y));
            items[i] = go.GetComponent<ButtonItem>();
        }
        return items;
    }

    /// <summary>
    /// Dựng 1 nhóm ButtonItem theo hình cung (fan) — 2 item ngoài rìa thấp gần cạnh dưới (nơi
    /// đứng), 2 item giữa cao hơn, toả hình vòng cung như tay quạt. Toạ độ fractional (0-1) so
    /// với parent, giống các helper khác trong file này.
    /// </summary>
    /// <param name="pivot">Tâm cung (fractional) — xấp xỉ điểm học sinh đứng, các item toả lên từ đây.</param>
    /// <param name="radius">Bán kính cung theo trục X/Y (fractional).</param>
    /// <param name="startAngleDeg">Góc (độ, 0=phải, 90=thẳng lên) của item đầu tiên (trái nhất).</param>
    /// <param name="endAngleDeg">Góc của item cuối cùng (phải nhất).</param>
    public static ButtonItem[] CreateButtonGroupArc(string prefix, Transform parent, Vector2 pivot,
        Vector2 radius, float startAngleDeg, float endAngleDeg, Vector2 itemSize, int count)
    {
        var items = new ButtonItem[count];
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angleRad = Mathf.Deg2Rad * Mathf.Lerp(startAngleDeg, endAngleDeg, t);
            Vector2 center = pivot + new Vector2(Mathf.Cos(angleRad) * radius.x, Mathf.Sin(angleRad) * radius.y);
            var go = CreateButtonItem($"{prefix}_{i}", parent,
                center - itemSize * 0.5f, center + itemSize * 0.5f);
            items[i] = go.GetComponent<ButtonItem>();
        }
        return items;
    }

    // ── Board perimeter (bàn cờ vuông — dùng cho game dạng board, vd Monopoly) ─────

    /// <summary>Dựng N ô vuông xếp quanh viền 1 hình chữ nhật (giống bàn cờ tỷ phú) — đi đều quanh
    /// perimeter, bắt đầu từ góc trên-trái, theo chiều kim đồng hồ. Trả về RectTransform của từng
    /// ô theo ĐÚNG thứ tự index 0..N-1 — dùng làm neo vị trí cho quân cờ (game chỉ cần copy
    /// anchorMin/anchorMax của ô tương ứng khi di chuyển). Helper này không biết gì về ý nghĩa ô
    /// (loại ô/luật chơi) — màu + nhãn hoàn toàn do caller quyết định qua colors/labels.</summary>
    /// <param name="outerMin">Góc dưới-trái (fractional 0-1) của khung ngoài cùng chứa board.</param>
    /// <param name="outerMax">Góc trên-phải (fractional 0-1) của khung ngoài cùng chứa board.</param>
    /// <param name="tileSize">Kích thước mỗi ô (fractional).</param>
    /// <param name="colors">Màu nền mỗi ô — độ dài mảng quyết định số ô N.</param>
    /// <param name="labels">Nhãn chữ mỗi ô (cùng độ dài colors) — để trống ("") thì ô không có chữ.</param>
    public static RectTransform[] CreateBoardTilesPerimeter(Transform parent, Vector2 outerMin, Vector2 outerMax,
        Vector2 tileSize, Color[] colors, string[] labels, int labelFontSize = 12)
    {
        int count = colors.Length;
        var result = new RectTransform[count];
        float w = outerMax.x - outerMin.x;
        float h = outerMax.y - outerMin.y;
        float perimeter = 2f * (w + h);

        for (int i = 0; i < count; i++)
        {
            float d = i * perimeter / count;
            Vector2 center = PointOnRectPerimeter(d, outerMin, outerMax, w, h);
            Vector2 tileMin = center - tileSize * 0.5f;
            Vector2 tileMax = center + tileSize * 0.5f;

            var tile = CreateImage($"Tile_{i}", parent, colors[i]);
            SetRect(tile.GetComponent<RectTransform>(), tileMin, tileMax, Vector2.zero, Vector2.zero);

            if (labels != null && i < labels.Length && !string.IsNullOrEmpty(labels[i]))
            {
                var label = CreateText("Label", tile.transform, labels[i], labelFontSize, TextAnchor.MiddleCenter);
                SetRect(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var labelText = label.GetComponent<Text>();
                labelText.color = Color.white;
                labelText.fontStyle = FontStyle.Bold;
                labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            result[i] = tile.GetComponent<RectTransform>();
        }
        return result;
    }

    /// <summary>d = khoảng cách đã đi dọc theo viền (bắt đầu góc trên-trái, chiều kim đồng hồ:
    /// phải theo cạnh trên → xuống cạnh phải → trái theo cạnh dưới → lên cạnh trái).</summary>
    static Vector2 PointOnRectPerimeter(float d, Vector2 min, Vector2 max, float w, float h)
    {
        if (d < w) return new Vector2(min.x + d, max.y);
        d -= w;
        if (d < h) return new Vector2(max.x, max.y - d);
        d -= h;
        if (d < w) return new Vector2(max.x - d, min.y);
        d -= w;
        return new Vector2(min.x, min.y + d);
    }

    // ── Fullscreen overlay (công bố kết quả — đổ xúc xắc, quay số, đếm ngược lớn...) ──

    /// <summary>Dựng 1 overlay phủ TOÀN màn hình (0,0)-(1,1) — nền màu + 1 dòng chữ to giữa màn
    /// hình. Dùng cho khoảnh khắc "công bố kết quả" ngắn cần chiếm trọn màn hình (đổ xúc xắc, quay
    /// thưởng, đếm ngược lớn...) — ẩn mặc định (SetActive(false)), game tự bật/tắt qua field điều
    /// khiển của controller. Gọi hàm này SAU CÙNG trong code dựng scene để overlay có sibling index
    /// cao nhất (vẽ đè lên mọi thứ khác, kể cả HUD).</summary>
    public static (GameObject root, Text bigText) CreateFullscreenOverlay(Transform parent, string name,
        Color backgroundColor, int fontSize = 140, Color? textColor = null)
    {
        var root = CreateImage(name, parent, backgroundColor);
        SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var textGo = CreateText($"{name}Text", root.transform, "", fontSize, TextAnchor.MiddleCenter);
        SetRect(textGo.GetComponent<RectTransform>(), new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);
        var text = textGo.GetComponent<Text>();
        text.color = textColor ?? Color.white;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        root.SetActive(false);
        return (root, text);
    }

    // ── GameHUD (2 đội, điểm + timer) ───────────────────────────────────────────

    /// <summary>Dựng thanh HUD trên cùng: tên + điểm + fill-bar mỗi bên, timer ở giữa.</summary>
    public static GameHUD CreateGameHud(Transform parent)
    {
        var hudGo = new GameObject("GameHUD", typeof(RectTransform));
        hudGo.transform.SetParent(parent, false);
        SetRect(hudGo.GetComponent<RectTransform>(), new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // Left — dịch vào gần giữa, chừa mép trái (0-0.18) cho nút Back/Home không bị đè điểm.
        var leftName = CreateTmpText("LeftName", hudGo.transform, "Left", 20, TextAlignmentOptions.MidlineRight);
        SetRect(leftName.GetComponent<RectTransform>(), new Vector2(0.20f, 0.5f), new Vector2(0.40f, 1f), Vector2.zero, Vector2.zero);

        var leftScore = CreateTmpText("LeftScore", hudGo.transform, "0", 24, TextAlignmentOptions.MidlineRight);
        SetRect(leftScore.GetComponent<RectTransform>(), new Vector2(0.20f, 0f), new Vector2(0.40f, 0.5f), Vector2.zero, Vector2.zero);

        var leftBarTrack = CreateImage("LeftBarTrack", hudGo.transform, new Color(0.85f, 0.85f, 0.85f, 1f));
        SetRect(leftBarTrack.GetComponent<RectTransform>(), new Vector2(0.20f, 0f), new Vector2(0.40f, 0.12f), Vector2.zero, Vector2.zero);
        var leftBar = CreateImage("LeftBarFill", leftBarTrack.transform, new Color(0.3f, 0.55f, 0.9f, 1f));
        var leftBarImg = leftBar.GetComponent<Image>();
        leftBarImg.type = Image.Type.Filled;
        leftBarImg.fillMethod = Image.FillMethod.Horizontal;
        leftBarImg.fillAmount = 0f;
        SetRect(leftBar.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Right — đối xứng, chừa mép phải (0.82-1) cho nút Home/Setting.
        var rightName = CreateTmpText("RightName", hudGo.transform, "Right", 20, TextAlignmentOptions.MidlineLeft);
        SetRect(rightName.GetComponent<RectTransform>(), new Vector2(0.60f, 0.5f), new Vector2(0.80f, 1f), Vector2.zero, Vector2.zero);

        var rightScore = CreateTmpText("RightScore", hudGo.transform, "0", 24, TextAlignmentOptions.MidlineLeft);
        SetRect(rightScore.GetComponent<RectTransform>(), new Vector2(0.60f, 0f), new Vector2(0.80f, 0.5f), Vector2.zero, Vector2.zero);

        var rightBarTrack = CreateImage("RightBarTrack", hudGo.transform, new Color(0.85f, 0.85f, 0.85f, 1f));
        SetRect(rightBarTrack.GetComponent<RectTransform>(), new Vector2(0.60f, 0f), new Vector2(0.80f, 0.12f), Vector2.zero, Vector2.zero);
        var rightBar = CreateImage("RightBarFill", rightBarTrack.transform, new Color(0.9f, 0.35f, 0.35f, 1f));
        var rightBarImg = rightBar.GetComponent<Image>();
        rightBarImg.type = Image.Type.Filled;
        rightBarImg.fillMethod = Image.FillMethod.Horizontal;
        rightBarImg.fillAmount = 0f;
        SetRect(rightBar.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Timer (giữa, giữa 2 khối điểm)
        var timer = CreateTmpText("Timer", hudGo.transform, "0", 28, TextAlignmentOptions.Center);
        SetRect(timer.GetComponent<RectTransform>(), new Vector2(0.42f, 0f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);

        var hud = hudGo.AddComponent<GameHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("leftNameText").objectReferenceValue = leftName.GetComponent<TextMeshProUGUI>();
        so.FindProperty("leftScoreText").objectReferenceValue = leftScore.GetComponent<TextMeshProUGUI>();
        so.FindProperty("leftScoreBar").objectReferenceValue = leftBarImg;
        so.FindProperty("rightNameText").objectReferenceValue = rightName.GetComponent<TextMeshProUGUI>();
        so.FindProperty("rightScoreText").objectReferenceValue = rightScore.GetComponent<TextMeshProUGUI>();
        so.FindProperty("rightScoreBar").objectReferenceValue = rightBarImg;
        so.FindProperty("timerText").objectReferenceValue = timer.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }

    /// <summary>
    /// Dùng prefab HUD có sẵn (đã có component GameHUD + nghệ thuật riêng) thay vì tự sinh
    /// placeholder qua CreateGameHud(). Giữ liên kết prefab (PrefabUtility.InstantiatePrefab,
    /// không phải Object.Instantiate rời) để sau này sửa prefab gốc vẫn áp dụng được.
    ///
    /// KHÔNG đụng vào RectTransform gốc của prefab — nhiều prefab HUD dùng "khung neo cố định"
    /// (anchor 1 điểm + sizeDelta cố định, vd 100x100) làm hệ quy chiếu riêng cho các phần tử con
    /// tính offset tuyệt đối theo pixel. Ép anchor gốc kéo giãn full Canvas sẽ làm phần tử con
    /// tính lại theo % kích thước Canvas thật → phóng to/biến dạng sai hoàn toàn. Cứ giữ nguyên
    /// giá trị đã thiết kế sẵn trong prefab, chỉ parent vào Canvas.
    /// Nếu không tìm thấy prefab hoặc thiếu component GameHUD, fallback về CreateGameHud() mặc định.
    /// </summary>
    public static GameHUD InstantiateGameHudPrefab(Transform parent) =>
        InstantiateGameHudPrefab(parent, DefaultGameHudPrefabPath);

    /// <inheritdoc cref="InstantiateGameHudPrefab(Transform)"/>
    public static GameHUD InstantiateGameHudPrefab(Transform parent, string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[MiniGameSceneBuilderHelpers] Không tìm thấy HUD prefab tại {prefabPath} — dùng CreateGameHud() mặc định.");
            return CreateGameHud(parent);
        }

        var instanceObj = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

        var hud = instanceObj.GetComponent<GameHUD>();
        if (hud == null)
        {
            Debug.LogWarning($"[MiniGameSceneBuilderHelpers] Prefab {prefabPath} không có component GameHUD — dùng CreateGameHud() mặc định.");
            Object.DestroyImmediate(instanceObj);
            return CreateGameHud(parent);
        }
        return hud;
    }

    // ── TutorialPanel (placeholder, không video) ────────────────────────────────

    public static TutorialPanel CreateTutorialPanel(Transform parent, string title)
    {
        var container = new GameObject("TutorialContainer", typeof(RectTransform));
        container.transform.SetParent(parent, false);
        SetRect(container.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var panel = CreateImage("TutorialPanel", container.transform, new Color(0f, 0f, 0f, 0.75f));
        SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var titleGo = CreateText("Title", panel.transform, title, 32, TextAnchor.MiddleCenter);
        SetRect(titleGo.GetComponent<RectTransform>(), new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);
        titleGo.GetComponent<Text>().color = Color.white;

        var startBtn = CreateSimpleButton("StartButton", panel.transform, "Bat dau", new Vector2(0.4f, 0.15f), new Vector2(0.6f, 0.3f));

        var tp = container.AddComponent<TutorialPanel>();
        var so = new SerializedObject(tp);
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        so.FindProperty("titleText").objectReferenceValue = titleGo.GetComponent<Text>();
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        return tp;
    }
}
