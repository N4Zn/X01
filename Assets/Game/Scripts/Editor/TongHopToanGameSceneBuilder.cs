using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "Tổng hợp Toán" (xem TongHopToanController.cs). Nền + ô câu hỏi/đáp án lấy theo
/// AddNumberGame: nền trơn bg_gameplay, ô box_green, thẻ vàng + dấu "?" cho ô bị ẩn. Mỗi nửa màn
/// hình có 1 panel (x 0-0.5 / 0.5-1, y 0-0.88 — chừa thanh HUD phía trên) với ô A/B/C ở y 0.55-0.88
/// và hàng 4 đáp án ở y 0.05-0.46, đúng tỉ lệ Player1Panel/Player2Panel của AddNumberGame.unity.
/// </summary>
public static class TongHopToanGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/TongHopToanGame";
    const string ScenePath = SceneFolder + "/TongHopToanGame.unity";
    const string TexDir = "Assets/Game/Textures/AddUpGame/";
    const float PanelTop = 0.88f;       // khớp Player1Panel của AddNumberGame
    const float IconCellPx = 78f;

    // Toạ độ trong panel (fractional) — copy từ AddNumberGame.unity.
    static readonly Vector2[] SlotMin = { new Vector2(0.02f, 0.55f), new Vector2(0.38f, 0.55f), new Vector2(0.74f, 0.55f) };
    static readonly Vector2[] SlotMax = { new Vector2(0.28f, 0.88f), new Vector2(0.64f, 0.88f), new Vector2(0.98f, 0.88f) };
    static readonly Vector2 PlusMin = new Vector2(0.28f, 0.62f), PlusMax = new Vector2(0.38f, 0.82f);
    static readonly Vector2 EqualsMin = new Vector2(0.64f, 0.62f), EqualsMax = new Vector2(0.74f, 0.82f);

    [MenuItem("Tools/TongHopToanGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var boxGreen = Load("box_green.png");
        var cardYellow = Load("card_yellow.png");
        var iconQuestion = Load("icon_question.png");

        var root = new GameObject("TongHopToanRoot");
        var controller = root.AddComponent<TongHopToanController>();

        var audioGo = new GameObject("AudioSource", typeof(AudioSource));
        audioGo.transform.SetParent(root.transform, false);
        var audioSource = audioGo.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Nền trơn của AddNumberGame (full màn hình).
        var bg = MiniGameSceneBuilderHelpers.CreateImage("Background", canvasGo.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bgImg = bg.GetComponent<Image>();
        bgImg.raycastTarget = false;
        var bgSprite = Load("bg_gameplay.png");
        if (bgSprite != null) bgImg.sprite = bgSprite;

        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // Mỗi nửa màn hình: panel câu hỏi + hàng đáp án.
        var leftPanelRt = CreatePanel("LeftPanel", canvasGo.transform, 0f, 0.5f);
        var rightPanelRt = CreatePanel("RightPanel", canvasGo.transform, 0.5f, 1f);
        var leftQ = BuildQuestionPanel(leftPanelRt, boxGreen, cardYellow, iconQuestion);
        var rightQ = BuildQuestionPanel(rightPanelRt, boxGreen, cardYellow, iconQuestion);

        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();
        // Nút đáp án PHẢI là con của ButtonDisplay (Cleanup() tắt cả display để ẩn đáp án trước "Next in Ns").
        // 2 khung con có anchor y hệt panel câu hỏi nên toạ độ fractional dùng chung.
        var leftButtons = CreateAnswerRow("LeftBtn", CreatePanel("LeftAnswers", buttonDisplayGo.transform, 0f, 0.5f), boxGreen);
        var rightButtons = CreateAnswerRow("RightBtn", CreatePanel("RightAnswers", buttonDisplayGo.transform, 0.5f, 1f), boxGreen);

        var bdSo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(bdSo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(bdSo, "rightButtons", rightButtons);
        bdSo.ApplyModifiedPropertiesWithoutUndo();

        // Icon ✔/✖ mỗi bên (giữa nửa màn hình, phía trên hàng đáp án).
        var leftCorrect = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true, new Vector2(0.18f, 0.30f), new Vector2(0.32f, 0.50f));
        var leftWrong = FeedbackIconBuilder.Create("LeftWrongIcon", canvasGo.transform, false, new Vector2(0.18f, 0.30f), new Vector2(0.32f, 0.50f));
        var rightCorrect = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true, new Vector2(0.68f, 0.30f), new Vector2(0.82f, 0.50f));
        var rightWrong = FeedbackIconBuilder.Create("RightWrongIcon", canvasGo.transform, false, new Vector2(0.68f, 0.30f), new Vector2(0.82f, 0.50f));

        var leftCountdown = CreateCountdown("LeftCountdownText", canvasGo.transform, new Vector2(0.02f, 0.35f), new Vector2(0.48f, 0.65f));
        var rightCountdown = CreateCountdown("RightCountdownText", canvasGo.transform, new Vector2(0.52f, 0.35f), new Vector2(0.98f, 0.65f));

        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        backButton.SetActive(false);

        // Không dựng tutorial panel (giữ null → Kit vào thẳng "Start in 3").

        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "TongHopToanGame";
        ctrlSo.FindProperty("playMode").enumValueIndex = (int)MiniGamePlayMode.Combined;
        ctrlSo.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrect;
        ctrlSo.FindProperty("leftWrongIcon").objectReferenceValue = leftWrong;
        ctrlSo.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrect;
        ctrlSo.FindProperty("rightWrongIcon").objectReferenceValue = rightWrong;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdown;
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdown;
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftAnswerButtons", System.Array.ConvertAll(leftButtons, b => (Object)b));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightAnswerButtons", System.Array.ConvertAll(rightButtons, b => (Object)b));
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("audioSource").objectReferenceValue = audioSource;
        AssignPanel(ctrlSo, "leftPanel", leftQ);
        AssignPanel(ctrlSo, "rightPanel", rightQ);
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        Debug.Log("[TongHopToanGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    static Sprite Load(string file)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(TexDir + file);
        if (s == null) Debug.LogWarning($"[TongHopToanGameSceneBuilder] Không thấy {TexDir}{file}");
        return s;
    }

    static RectTransform CreatePanel(string name, Transform parent, float xMin, float xMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        MiniGameSceneBuilderHelpers.SetRect(rt, new Vector2(xMin, 0f), new Vector2(xMax, PanelTop), Vector2.zero, Vector2.zero);
        return rt;
    }

    // ── Panel câu hỏi: 3 ô A/B/C (+ thẻ vàng "?") + nhãn +/- và "=" + lưới icon cho câu đếm ──

    static TongHopToanController.QuestionPanel BuildQuestionPanel(RectTransform panel, Sprite boxGreen, Sprite cardYellow, Sprite iconQuestion)
    {
        var q = new TongHopToanController.QuestionPanel();
        for (int i = 0; i < 3; i++)
        {
            var slot = MiniGameSceneBuilderHelpers.CreateImage("Slot" + "ABC"[i], panel, Color.white);
            MiniGameSceneBuilderHelpers.SetRect(slot.GetComponent<RectTransform>(), SlotMin[i], SlotMax[i], Vector2.zero, Vector2.zero);
            var slotImg = slot.GetComponent<Image>();
            slotImg.raycastTarget = false;
            if (boxGreen != null) slotImg.sprite = boxGreen;

            var text = MiniGameSceneBuilderHelpers.CreateTmpText("Text", slot.transform, "", 80, TextAlignmentOptions.Center);
            MiniGameSceneBuilderHelpers.SetRect(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tmp = text.GetComponent<TextMeshProUGUI>();
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            // Ô bị ẩn: thẻ vàng + dấu "?" phủ lên (giống HiddenOverlay của AddNumberGame).
            var hidden = MiniGameSceneBuilderHelpers.CreateImage("Hidden", slot.transform, Color.white);
            MiniGameSceneBuilderHelpers.SetRect(hidden.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hiddenImg = hidden.GetComponent<Image>();
            hiddenImg.raycastTarget = false;
            if (cardYellow != null) hiddenImg.sprite = cardYellow;
            var mark = MiniGameSceneBuilderHelpers.CreateImage("QuestionMark", hidden.transform, Color.white);
            MiniGameSceneBuilderHelpers.SetRect(mark.GetComponent<RectTransform>(), new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f), Vector2.zero, Vector2.zero);
            var markImg = mark.GetComponent<Image>();
            markImg.raycastTarget = false;
            markImg.preserveAspect = true;
            if (iconQuestion != null) markImg.sprite = iconQuestion;
            hidden.SetActive(false);
            slot.SetActive(false);

            q.slots[i] = slot;
            q.slotTexts[i] = tmp;
            q.hidden[i] = hidden;
        }
        q.opLabel = CreateLabel("OpLabel", panel, PlusMin, PlusMax);
        q.equalsLabel = CreateLabel("EqualsLabel", panel, EqualsMin, EqualsMax);

        var gridGo = new GameObject("IconGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(panel, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)gridGo.transform, new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);
        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(IconCellPx, IconCellPx);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.MiddleCenter;
        gridGo.SetActive(false);
        q.iconGrid = (RectTransform)gridGo.transform;
        return q;
    }

    static TextMeshProUGUI CreateLabel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = MiniGameSceneBuilderHelpers.CreateTmpText(name, parent, "", 80, TextAlignmentOptions.Center);
        MiniGameSceneBuilderHelpers.SetRect(go.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.35f, 0.2f, 0.1f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 30;
        tmp.fontSizeMax = 90;
        tmp.raycastTarget = false;
        go.SetActive(false);
        return tmp;
    }

    // ── Hàng đáp án: 4 ô box_green cao, cách đều (controller căn giữa lại khi có < 4 đáp án) ──

    static ButtonItem[] CreateAnswerRow(string prefix, Transform panel, Sprite boxGreen)
    {
        const float w = 0.23f, gap = 0.02f, yMin = 0.05f, yMax = 0.46f;
        var items = new ButtonItem[4];
        for (int i = 0; i < 4; i++)
        {
            float x = 0.01f + i * (w + gap);
            var go = MiniGameSceneBuilderHelpers.CreateButtonItem($"{prefix}_{i}", panel, new Vector2(x, yMin), new Vector2(x + w, yMax));
            var img = go.GetComponent<Image>();
            if (boxGreen != null) img.sprite = boxGreen;
            img.color = Color.white;

            // Nền ô là ảnh box_green: đừng nhân màu thẻ trắng/xanh/đỏ mặc định của ButtonItem lên sprite.
            // Đúng/sai đã có icon ✔/✖; sai chỉ làm tối ô.
            var so = new SerializedObject(go.GetComponent<ButtonItem>());
            so.FindProperty("colorNormal").colorValue = Color.white;
            so.FindProperty("colorChosen").colorValue = new Color(0.8f, 0.8f, 0.8f);
            so.FindProperty("colorSelected").colorValue = Color.white;
            so.FindProperty("colorCorrect").colorValue = Color.white;
            so.FindProperty("colorWrong").colorValue = new Color(0.55f, 0.55f, 0.55f);
            so.FindProperty("colorLocked").colorValue = new Color(0.7f, 0.7f, 0.7f);
            so.ApplyModifiedPropertiesWithoutUndo();
            items[i] = go.GetComponent<ButtonItem>();
        }
        return items;
    }

    static void AssignPanel(SerializedObject so, string field, TongHopToanController.QuestionPanel p)
    {
        MiniGameSceneBuilderHelpers.AssignObjectArray(so, field + ".slots", System.Array.ConvertAll(p.slots, o => (Object)o));
        MiniGameSceneBuilderHelpers.AssignObjectArray(so, field + ".slotTexts", System.Array.ConvertAll(p.slotTexts, o => (Object)o));
        MiniGameSceneBuilderHelpers.AssignObjectArray(so, field + ".hidden", System.Array.ConvertAll(p.hidden, o => (Object)o));
        so.FindProperty(field + ".opLabel").objectReferenceValue = p.opLabel;
        so.FindProperty(field + ".equalsLabel").objectReferenceValue = p.equalsLabel;
        so.FindProperty(field + ".iconGrid").objectReferenceValue = p.iconGrid;
    }

    static Text CreateCountdown(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = MiniGameSceneBuilderHelpers.CreateText(name, parent, "Start in 3", 56, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(go.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);
        var text = go.GetComponent<Text>();
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        go.transform.SetAsLastSibling();
        return text;
    }
}
