using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tạo toàn bộ scene BalloonGame.
/// Menu: Tools > BalloonGame > Build Scene
///
/// Scene hierarchy (sau khi build):
///   Canvas
///   ├── BG
///   ├── Divider
///   ├── GameHUD           ← top strip 60px
///   ├── LeftField         ← BalloonField, 12 BalloonItem con
///   ├── CenterDisplay     ← BalloonLetterDisplay, letter + fill bar + audio
///   ├── RightField        ← BalloonField, 12 BalloonItem con
///   ├── RoundClearPanel   ← overlay khi round xong
///   └── BalloonGameController
/// </summary>
public static class BalloonGameSceneBuilder
{
    const string SceneFolder  = "Assets/Game/Scenes/BalloonGame";
    const string ScenePath    = "Assets/Game/Scenes/BalloonGame/BalloonGame.unity";
    // Resources/GameConfig/ → Resources.Load<BalloonGameConfig>("GameConfig/BalloonGameConfig")
    const string ConfigPath   = "Assets/Game/Resources/GameConfig/BalloonGameConfig.asset";

    const float CW       = 1024f;
    const float CH       = 600f;
    const float HUD_H    = 60f;
    const float CENTER_W = 100f;
    const float PLAY_H   = CH - HUD_H;                // 540
    const float FIELD_W  = (CW - CENTER_W) / 2f;     // 462

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/BalloonGame/Build Scene")]
    public static void BuildAll()
    {
        EnsureFolders();
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BalloonGameSceneBuilder] Done — scene built and added to Build Settings.");
    }

    // ── Config asset ──────────────────────────────────────────────────────────

    // Tái dùng config đã có (giữ giá trị người dùng chỉnh), tạo mới nếu chưa có.
    static BalloonGameConfig GetOrCreateConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<BalloonGameConfig>(ConfigPath);
        if (existing != null) return existing;

        var cfg = ScriptableObject.CreateInstance<BalloonGameConfig>();
        AssetDatabase.CreateAsset(cfg, ConfigPath);
        return cfg;
    }

    // ── Scene ─────────────────────────────────────────────────────────────────

    static void BuildScene()
    {
        var config = GetOrCreateConfig();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.15f, 0.25f);
        cam.orthographic    = true;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CW, CH);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Background
        var bgGo = Img("BG", canvasGo.transform, new Color(0.10f, 0.15f, 0.25f));
        StretchFull(RT(bgGo));

        // Center divider strip (subtle line between two player zones)
        var divGo = Img("Divider", canvasGo.transform, new Color(1f, 1f, 1f, 0.10f));
        var divRT = RT(divGo);
        divRT.anchorMin = new Vector2(0.5f, 0f); divRT.anchorMax = new Vector2(0.5f, 1f);
        divRT.pivot = new Vector2(0.5f, 0.5f);
        divRT.anchoredPosition = Vector2.zero; divRT.sizeDelta = new Vector2(CENTER_W, 0f);

        // HUD
        var (hudGo, hudComp) = BuildHUD(canvasGo.transform);

        // Fields
        var (_, leftFieldComp, leftItems)  = BuildField("LeftField",  canvasGo.transform, isLeft: true);
        var (_, rightFieldComp, rightItems) = BuildField("RightField", canvasGo.transform, isLeft: false);

        // Center display
        var (centerGo, centerComp) = BuildCenterDisplay(canvasGo.transform);

        // Round clear overlay
        var (roundPanelGo, roundLabelTMP) = BuildRoundClearPanel(canvasGo.transform);

        // Controller
        var ctrlGo   = new GameObject("BalloonGameController");
        var ctrlComp = ctrlGo.AddComponent<BalloonGameController>();

        var ctrlSo = new SerializedObject(ctrlComp);
        ctrlSo.FindProperty("leftField")      .objectReferenceValue = leftFieldComp;
        ctrlSo.FindProperty("rightField")     .objectReferenceValue = rightFieldComp;
        ctrlSo.FindProperty("centerDisplay")  .objectReferenceValue = centerComp;
        ctrlSo.FindProperty("gameHud")        .objectReferenceValue = hudComp;
        ctrlSo.FindProperty("roundClearPanel").objectReferenceValue = roundPanelGo;
        ctrlSo.FindProperty("roundClearLabel").objectReferenceValue = roundLabelTMP;
        ctrlSo.FindProperty("config")         .objectReferenceValue = config;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire config to fields and center display
        WireFieldItems(leftFieldComp,  leftItems,  config);
        WireFieldItems(rightFieldComp, rightItems, config);

        var centerSo = new SerializedObject(centerComp);
        centerSo.FindProperty("config").objectReferenceValue = config;
        centerSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(centerComp);

        EditorUtility.SetDirty(ctrlGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    static (GameObject, GameHUD) BuildHUD(Transform parent)
    {
        var go = Img("GameHUD", parent, new Color(0f, 0f, 0f, 0.55f));
        var rt = RT(go);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0f, HUD_H);
        go.GetComponent<Image>().raycastTarget = false;

        var hudComp = go.AddComponent<GameHUD>();

        // Timer (center)
        var timerGo = TMP("TimerText", go.transform, "180", 36);
        PlaceRT(RT(timerGo), 0.5f, 0.5f, 0f, 0f, 120f, HUD_H);
        timerGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // Left: name above, score below, bar to the right of name
        var leftNameGo  = TMP("LeftNameText",  go.transform, "Player 1", 20);
        var leftScoreGo = TMP("LeftScoreText", go.transform, "0",         32);
        PlaceRT(RT(leftNameGo),  0f, 0.5f,  10f,  12f, 180f, 28f);
        PlaceRT(RT(leftScoreGo), 0f, 0.5f,  10f, -14f, 100f, 36f);
        leftNameGo.GetComponent<TextMeshProUGUI>().alignment  = TextAlignmentOptions.Left;
        leftScoreGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var leftBarGo  = Img("LeftScoreBar", go.transform, new Color(0.30f, 0.82f, 0.45f));
        PlaceRT(RT(leftBarGo), 0f, 0.5f, 200f, 0f, 200f, 14f);
        SetFilledH(leftBarGo.GetComponent<Image>(), 0); // fill from Left

        // Right: mirrored
        var rightNameGo  = TMP("RightNameText",  go.transform, "Player 2", 20);
        var rightScoreGo = TMP("RightScoreText", go.transform, "0",         32);
        PlaceRT(RT(rightNameGo),  1f, 0.5f, -10f,  12f, 180f, 28f, pivotRight: true);
        PlaceRT(RT(rightScoreGo), 1f, 0.5f, -10f, -14f, 100f, 36f, pivotRight: true);
        rightNameGo.GetComponent<TextMeshProUGUI>().alignment  = TextAlignmentOptions.Right;
        rightScoreGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        var rightBarGo = Img("RightScoreBar", go.transform, new Color(1.00f, 0.38f, 0.38f));
        PlaceRT(RT(rightBarGo), 1f, 0.5f, -200f, 0f, 200f, 14f, pivotRight: true);
        SetFilledH(rightBarGo.GetComponent<Image>(), 1); // fill from Right

        // Wire GameHUD
        var hudSo = new SerializedObject(hudComp);
        hudSo.FindProperty("leftNameText")  .objectReferenceValue = leftNameGo.GetComponent<TextMeshProUGUI>();
        hudSo.FindProperty("leftScoreText") .objectReferenceValue = leftScoreGo.GetComponent<TextMeshProUGUI>();
        hudSo.FindProperty("leftScoreBar")  .objectReferenceValue = leftBarGo.GetComponent<Image>();
        hudSo.FindProperty("rightNameText") .objectReferenceValue = rightNameGo.GetComponent<TextMeshProUGUI>();
        hudSo.FindProperty("rightScoreText").objectReferenceValue = rightScoreGo.GetComponent<TextMeshProUGUI>();
        hudSo.FindProperty("rightScoreBar") .objectReferenceValue = rightBarGo.GetComponent<Image>();
        hudSo.FindProperty("timerText")     .objectReferenceValue = timerGo.GetComponent<TextMeshProUGUI>();
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        return (go, hudComp);
    }

    // ── BalloonField ──────────────────────────────────────────────────────────

    static (GameObject, BalloonField, BalloonItem[]) BuildField(
        string fieldName, Transform parent, bool isLeft)
    {
        float xPos = isLeft
            ? -(CENTER_W / 2f + FIELD_W / 2f)   // left center
            :  (CENTER_W / 2f + FIELD_W / 2f);  // right center

        var go = new GameObject(fieldName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = RT(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(FIELD_W, PLAY_H);
        rt.anchoredPosition = new Vector2(xPos, -HUD_H / 2f);

        var comp = go.AddComponent<BalloonField>();

        var items = new BalloonItem[12];
        for (int i = 0; i < 12; i++)
            items[i] = MakeBalloonItem($"Balloon_{i:00}", go.transform, isLeft);

        return (go, comp, items);
    }

    static BalloonItem MakeBalloonItem(string itemName, Transform parent, bool isLeft)
    {
        var go = new GameObject(itemName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.SetActive(false); // BalloonField.Setup() activates

        RT(go).sizeDelta = new Vector2(100f, 100f);

        // Balloon body image (raycastTarget=TRUE → IPointerClickHandler fires)
        var bgImg = go.GetComponent<Image>();
        bgImg.color         = isLeft
            ? new Color(0.30f, 0.60f, 1.00f)   // blue  (left)
            : new Color(1.00f, 0.42f, 0.30f);  // coral (right)
        bgImg.raycastTarget = true;

        // Letter label (child)
        var labelGo = TMP("Label", go.transform, "?", 44);
        StretchFull(RT(labelGo));
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.color         = Color.white;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        // Attach BalloonItem and wire serialized fields
        var item   = go.AddComponent<BalloonItem>();
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("balloonBg").objectReferenceValue = bgImg;
        itemSo.FindProperty("label")    .objectReferenceValue = tmp;
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    static void WireFieldItems(BalloonField field, BalloonItem[] items, BalloonGameConfig config)
    {
        // Wire items + config into BalloonField
        var fieldSo   = new SerializedObject(field);
        var itemsProp = fieldSo.FindProperty("items");
        itemsProp.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];

        var fieldConfigProp = fieldSo.FindProperty("config");
        if (fieldConfigProp == null)
            Debug.LogError($"[BalloonGameSceneBuilder] FindProperty('config') null trên {field.name}!");
        else
            fieldConfigProp.objectReferenceValue = config;

        fieldSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(field);

        // Wire config into each BalloonItem
        foreach (var item in items)
        {
            var so = new SerializedObject(item);
            var itemConfigProp = so.FindProperty("config");
            if (itemConfigProp != null)
                itemConfigProp.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }
    }

    // ── Center display ────────────────────────────────────────────────────────

    static (GameObject, BalloonLetterDisplay) BuildCenterDisplay(Transform parent)
    {
        var go = new GameObject("CenterDisplay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = RT(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(CENTER_W, PLAY_H);
        rt.anchoredPosition = new Vector2(0f, -HUD_H / 2f);

        var comp    = go.AddComponent<BalloonLetterDisplay>();
        var audio   = go.AddComponent<AudioSource>();
        audio.playOnAwake  = false;
        audio.loop         = true;
        audio.spatialBlend = 0f;

        // ── Letter + mask fill ────────────────────────────────────────────────
        // LetterMask: Image with Mask component — sprite set at runtime = letter shape
        var maskGo  = Img("LetterMask", go.transform, Color.white);
        var maskImg = maskGo.GetComponent<Image>();
        maskImg.raycastTarget = false;
        maskImg.preserveAspect = false;
        maskGo.AddComponent<Mask>().showMaskGraphic = true;
        var maskRT = RT(maskGo);
        maskRT.anchorMin = maskRT.anchorMax = new Vector2(0.5f, 0.5f);
        maskRT.sizeDelta        = new Vector2(88f, 130f);
        maskRT.anchoredPosition = new Vector2(0f, 55f);

        // FillImage (child of LetterMask — clipped to letter shape when sprite is set)
        var fillGo  = Img("FillImage", maskGo.transform, new Color(0.25f, 0.80f, 1.00f, 0.85f));
        StretchFull(RT(fillGo));
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.type         = Image.Type.Filled;
        fillImg.fillMethod   = Image.FillMethod.Vertical;
        fillImg.fillOrigin   = 0; // Bottom
        fillImg.fillAmount   = 0f;
        fillImg.raycastTarget = false;

        // LetterLabel (TMP, on top — shown when no sprite is assigned)
        var lblGo = TMP("LetterLabel", go.transform, "A", 80);
        RT(lblGo).anchorMin = RT(lblGo).anchorMax = new Vector2(0.5f, 0.5f);
        RT(lblGo).sizeDelta = new Vector2(88f, 130f);
        RT(lblGo).anchoredPosition = new Vector2(0f, 55f);
        var lbl = lblGo.GetComponent<TextMeshProUGUI>();
        lbl.fontStyle     = FontStyles.Bold;
        lbl.color         = Color.white;
        lbl.alignment     = TextAlignmentOptions.Center;
        lbl.raycastTarget = false;

        // ── Simple progress bar (always visible) ──────────────────────────────
        // Background bar
        var barBgGo = Img("ProgressBg", go.transform, new Color(0f, 0f, 0f, 0.35f));
        var barBgRT = RT(barBgGo);
        barBgRT.anchorMin = barBgRT.anchorMax = new Vector2(0.5f, 0f);
        barBgRT.sizeDelta = new Vector2(16f, PLAY_H - 20f);
        barBgRT.anchoredPosition = new Vector2(0f, (PLAY_H - 20f) / 2f + 10f - PLAY_H / 2f);
        barBgGo.GetComponent<Image>().raycastTarget = false;

        // Wire BalloonLetterDisplay
        var dispSo = new SerializedObject(comp);
        dispSo.FindProperty("letterMask")  .objectReferenceValue = maskImg;
        dispSo.FindProperty("fillImage")   .objectReferenceValue = fillImg;
        dispSo.FindProperty("letterLabel") .objectReferenceValue = lbl;
        dispSo.FindProperty("audioSrc")    .objectReferenceValue = audio;
        dispSo.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(false); // hidden; controller shows it at round start

        return (go, comp);
    }

    // ── Round clear panel ─────────────────────────────────────────────────────

    static (GameObject, TextMeshProUGUI) BuildRoundClearPanel(Transform parent)
    {
        var panelGo = Img("RoundClearPanel", parent, new Color(0f, 0f, 0f, 0.70f));
        StretchFull(RT(panelGo));
        panelGo.SetActive(false);

        // Big letter (just completed)
        var bigLetterGo = TMP("CompletedLetter", panelGo.transform, "A", 130);
        RT(bigLetterGo).anchorMin = RT(bigLetterGo).anchorMax = new Vector2(0.5f, 0.5f);
        RT(bigLetterGo).sizeDelta = new Vector2(400f, 200f);
        RT(bigLetterGo).anchoredPosition = new Vector2(0f, 20f);
        var bigLbl = bigLetterGo.GetComponent<TextMeshProUGUI>();
        bigLbl.fontStyle = FontStyles.Bold;
        bigLbl.color     = Color.yellow;
        bigLbl.alignment = TextAlignmentOptions.Center;

        // Sub-label (optional decoration)
        var subGo = TMP("SubLabel", panelGo.transform, "Round Clear!", 36);
        RT(subGo).anchorMin = RT(subGo).anchorMax = new Vector2(0.5f, 0.5f);
        RT(subGo).sizeDelta = new Vector2(400f, 60f);
        RT(subGo).anchoredPosition = new Vector2(0f, -80f);
        subGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        return (panelGo, bigLbl);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

    static GameObject Img(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return go;
    }

    static GameObject TMP(string name, Transform parent, string text, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
    }

    /// <param name="anchorX">0=left edge, 0.5=center, 1=right edge of parent</param>
    /// <param name="pivotRight">true → pivot at (1,0.5) so anchoredPos is from right</param>
    static void PlaceRT(RectTransform rt, float anchorX, float anchorY,
                        float x, float y, float w, float h, bool pivotRight = false)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX, anchorY);
        rt.pivot     = new Vector2(pivotRight ? 1f : 0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    static void SetFilledH(Image img, int origin)
    {
        img.type        = Image.Type.Filled;
        img.fillMethod  = Image.FillMethod.Horizontal;
        img.fillOrigin  = origin;
        img.fillAmount  = 0f;
    }

    // ── Folders & Build Settings ──────────────────────────────────────────────

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Game");
        EnsureFolder("Assets/Game/Scenes");
        EnsureFolder(SceneFolder);
        EnsureFolder("Assets/Game/Resources");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
        string child  = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
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
    }
}
