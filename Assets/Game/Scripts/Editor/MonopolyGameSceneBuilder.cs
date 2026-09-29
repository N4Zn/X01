using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "MONOPOLY" — sinh từ MiniGameKit. Board vuông 40 ô quanh viền (CreateBoardTilesPerimeter,
/// helper dùng chung của Kit), mỗi đội có xúc xắc riêng (2 viên) ở bên mình, ô Question trả lời
/// đúng thì CHIẾM ô (đổi màu theo đội) — bước vào ô đối phương đã chiếm bị trừ điểm rồi có cơ hội
/// giành lại, vòng quay may mắn. Tự sinh CSV câu hỏi Family (trộn ảnh-hỏi/chữ-đáp + chữ-hỏi/ảnh-đáp)
/// nếu chưa có, dùng lại ảnh Family thật đã có từ WhoIsItGame — chạy 1 lần
/// "Tools → MonopolyGame → Build Scene" là chơi thử được ngay.
/// </summary>
public static class MonopolyGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/MonopolyGame";
    const string ScenePath = SceneFolder + "/MonopolyGame.unity";
    const string DataFolder = "Assets/Game/Resources/MiniGameKit/MonopolyGame";
    const string ChooseCsvPath = DataFolder + "/choose.csv";

    // ── Nội dung câu hỏi Family — trộn 2 kiểu theo đúng đề bài ─────────────────
    // Kiểu A: hiện ảnh, hỏi "This is...", đáp án chữ (giống WhoIsItGame).
    static readonly (string key, string[] answers)[] ImageQuestionRows =
    {
        ("mom",     new[] { "Mom", "Dad", "Sister", "Baby" }),
        ("dad",     new[] { "Dad", "Mom", "Grandpa", "Brother" }),
        ("sister",  new[] { "Sister", "Brother", "Mom", "Grandma" }),
        ("brother", new[] { "Brother", "Sister", "Dad", "Baby" }),
        ("grandma", new[] { "Grandma", "Grandpa", "Mom", "Sister" }),
        ("grandpa", new[] { "Grandpa", "Grandma", "Dad", "Brother" }),
        ("baby",    new[] { "Baby", "Mom", "Sister", "Grandma" }),
    };

    // Kiểu B: hiện chữ hỏi "Where is X?", đáp án là ẢNH (answerMediaType=Image) — học sinh chọn
    // đúng hình. a0 luôn là đáp án đúng (đúng key hỏi).
    static readonly (string question, string[] answerKeys)[] TextQuestionRows =
    {
        ("Where is mom?",     new[] { "mom", "dad", "sister", "brother" }),
        ("Where is dad?",     new[] { "dad", "mom", "grandpa", "baby" }),
        ("Where is sister?",  new[] { "sister", "brother", "grandma", "mom" }),
        ("Where is brother?", new[] { "brother", "sister", "baby", "dad" }),
        ("Where is grandma?", new[] { "grandma", "grandpa", "mom", "sister" }),
        ("Where is grandpa?", new[] { "grandpa", "grandma", "dad", "brother" }),
        ("Where is baby?",    new[] { "baby", "mom", "sister", "grandma" }),
    };

    [MenuItem("Tools/MonopolyGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Resources/MiniGameKit");
        MiniGameSceneBuilderHelpers.EnsureFolder(DataFolder);

        EnsureChooseCsv();
        AssetDatabase.Refresh();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("MonopolyGameRoot");
        var controller = root.AddComponent<MonopolyGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // ── HUD prefab mặc định của Kit ──────────────────────────────────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ── Board vuông quanh viền — nền + N ô ───────────────────────────────
        var boardMin = new Vector2(0.14f, 0.05f);
        var boardMax = new Vector2(0.86f, 0.82f);

        var boardBg = MiniGameSceneBuilderHelpers.CreateImage("BoardBackground", canvasGo.transform, new Color(0.92f, 0.90f, 0.82f, 1f));
        MiniGameSceneBuilderHelpers.SetRect(boardBg.GetComponent<RectTransform>(), boardMin, boardMax, Vector2.zero, Vector2.zero);

        var layout = MonopolyBoard.GenerateLayout(MonopolyBoard.DefaultTileCount);
        var values = MonopolyBoard.GenerateValues(layout);
        var colors = new Color[layout.Length];
        var labels = new string[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            var (color, label) = TileVisual(layout[i], values[i]);
            colors[i] = color;
            labels[i] = label;
        }
        // 40 ô (x2 bản gốc) quanh cùng 1 viền → ô nhỏ hơn để không chồng nhau (spacing perimeter/40).
        var tileAnchors = MiniGameSceneBuilderHelpers.CreateBoardTilesPerimeter(
            canvasGo.transform, boardMin, boardMax, new Vector2(0.05f, 0.058f), colors, labels, labelFontSize: 13);

        // ── Quân cờ 2 đội — nhỏ hơn ô ────────────────────────────────────────
        var leftToken = CreateToken("LeftToken", canvasGo.transform, new Color(0.25f, 0.55f, 0.95f), new Vector2(2, 2), new Vector2(-16, -11));
        var rightToken = CreateToken("RightToken", canvasGo.transform, new Color(0.90f, 0.30f, 0.30f), new Vector2(16, 11), new Vector2(-2, -2));

        // ── Trung tâm board: banner lượt + thông báo ô ──────────────────────────
        var turnBanner = MiniGameSceneBuilderHelpers.CreateText(
            "TurnBannerText", canvasGo.transform, "", 34, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(turnBanner.GetComponent<RectTransform>(),
            new Vector2(0.30f, 0.66f), new Vector2(0.70f, 0.75f), Vector2.zero, Vector2.zero);
        turnBanner.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // ── Xúc xắc TÁCH RIÊNG mỗi đội — nằm ở bên đội đó (lề trái/phải, ngoài board),
        // chỉ bên đang có lượt mới bấm được (điều khiển trong controller) ──────────────
        var leftDiceButtonGo = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "LeftDiceButton", canvasGo.transform, "🎲🎲", new Vector2(0.01f, 0.04f), new Vector2(0.12f, 0.18f));
        leftDiceButtonGo.GetComponent<Image>().color = new Color(0.75f, 0.85f, 0.98f, 1f);
        var leftDiceValueGo = MiniGameSceneBuilderHelpers.CreateText(
            "LeftDiceValueText", canvasGo.transform, "", 22, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftDiceValueGo.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0.34f), new Vector2(0.12f, 0.42f), Vector2.zero, Vector2.zero);
        leftDiceValueGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var rightDiceButtonGo = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "RightDiceButton", canvasGo.transform, "🎲🎲", new Vector2(0.88f, 0.04f), new Vector2(0.99f, 0.18f));
        rightDiceButtonGo.GetComponent<Image>().color = new Color(0.98f, 0.80f, 0.78f, 1f);
        var rightDiceValueGo = MiniGameSceneBuilderHelpers.CreateText(
            "RightDiceValueText", canvasGo.transform, "", 22, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightDiceValueGo.GetComponent<RectTransform>(),
            new Vector2(0.88f, 0.34f), new Vector2(0.99f, 0.42f), Vector2.zero, Vector2.zero);
        rightDiceValueGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var tileMessage = MiniGameSceneBuilderHelpers.CreateText(
            "TileMessageText", canvasGo.transform, "", 32, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(tileMessage.GetComponent<RectTransform>(),
            new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.42f), Vector2.zero, Vector2.zero);
        var tileMessageComp = tileMessage.GetComponent<Text>();
        tileMessageComp.fontStyle = FontStyle.Bold;
        tileMessageComp.supportRichText = true;
        tileMessageComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        tileMessage.SetActive(false);

        // ── Overlay câu hỏi tại ô — vùng bao trùm board, dùng chung cho CẢ chiếm ô mới
        // lẫn giành lại ô đối phương (không còn đấu tay đôi 2 đội cùng lúc) ──────────
        var overlayMin = new Vector2(0.12f, 0.04f);
        var overlayMax = new Vector2(0.88f, 0.82f);

        var soloRoot = MiniGameSceneBuilderHelpers.CreateImage("SoloQuestionRoot", canvasGo.transform, new Color(1f, 1f, 1f, 0.95f));
        MiniGameSceneBuilderHelpers.SetRect(soloRoot.GetComponent<RectTransform>(), overlayMin, overlayMax, Vector2.zero, Vector2.zero);
        var soloButtonDisplayGo = new GameObject("SoloButtonDisplay", typeof(RectTransform));
        soloButtonDisplayGo.transform.SetParent(soloRoot.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)soloButtonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var soloButtonDisplay = soloButtonDisplayGo.AddComponent<ButtonDisplay>();

        var soloLeftContainer = new GameObject("SoloLeftContainer", typeof(RectTransform));
        soloLeftContainer.transform.SetParent(soloButtonDisplayGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)soloLeftContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var soloRightContainer = new GameObject("SoloRightContainer", typeof(RectTransform));
        soloRightContainer.transform.SetParent(soloButtonDisplayGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)soloRightContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var soloLeftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "SoloLeftBtn", soloLeftContainer.transform, new Vector2(0.06f, 0.06f), new Vector2(0.47f, 0.30f), 4);
        var soloRightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "SoloRightBtn", soloRightContainer.transform, new Vector2(0.53f, 0.06f), new Vector2(0.94f, 0.30f), 4);

        var soloSo = new SerializedObject(soloButtonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(soloSo, "leftButtons", soloLeftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(soloSo, "rightButtons", soloRightButtons);
        soloSo.ApplyModifiedPropertiesWithoutUndo();
        soloRoot.SetActive(false);

        // ── Vòng quay may mắn — overlay nhỏ hơn, giữa màn hình ──────────────────
        var wheelRoot = MiniGameSceneBuilderHelpers.CreateImage("LuckyWheelRoot", canvasGo.transform, new Color(0.95f, 0.85f, 0.55f, 0.97f));
        MiniGameSceneBuilderHelpers.SetRect(wheelRoot.GetComponent<RectTransform>(),
            new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.55f), Vector2.zero, Vector2.zero);
        var wheelResultText = MiniGameSceneBuilderHelpers.CreateText(
            "LuckyWheelResultText", wheelRoot.transform, "", 36, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(wheelResultText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        wheelResultText.GetComponent<Text>().fontStyle = FontStyle.Bold;
        wheelRoot.SetActive(false);

        // ── Đề bài dùng chung (ảnh hoặc chữ) — TRÊN CÙNG, render đè lên overlay câu hỏi ──
        var promptImageGo = new GameObject("QuestionPromptImage", typeof(RectTransform), typeof(Image));
        promptImageGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(promptImageGo.GetComponent<RectTransform>(),
            new Vector2(0.36f, 0.55f), new Vector2(0.64f, 0.80f), Vector2.zero, Vector2.zero);
        promptImageGo.GetComponent<Image>().preserveAspect = true;
        promptImageGo.SetActive(false);

        var promptTextGo = MiniGameSceneBuilderHelpers.CreateText(
            "QuestionPromptText", canvasGo.transform, "", 36, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(promptTextGo.GetComponent<RectTransform>(),
            new Vector2(0.20f, 0.55f), new Vector2(0.80f, 0.80f), Vector2.zero, Vector2.zero);
        var promptTextComp = promptTextGo.GetComponent<Text>();
        promptTextComp.fontStyle = FontStyle.Bold;
        promptTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptTextGo.SetActive(false);

        // ── Feedback mặc định (icon ✔/✖ + âm thanh — âm thanh tự chạy trong controller) ──
        var iconMin = new Vector2(0.40f, 0.60f);
        var iconMax = new Vector2(0.60f, 0.78f);
        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true, iconMin, iconMax);
        var leftWrongIcon = FeedbackIconBuilder.Create("LeftWrongIcon", canvasGo.transform, false, iconMin, iconMax);
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true, iconMin, iconMax);
        var rightWrongIcon = FeedbackIconBuilder.Create("RightWrongIcon", canvasGo.transform, false, iconMin, iconMax);

        // Back button + tutorial
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Monopoly");

        // ── Đổ xúc xắc — overlay phủ TOÀN màn hình (helper dùng chung của Kit) — tạo SAU CÙNG để
        // sibling cao nhất, vẽ đè lên mọi thứ (kể cả HUD) trong lúc đang đổ xúc xắc ─────────────
        var (diceRollRoot, diceRollFullscreenText) = MiniGameSceneBuilderHelpers.CreateFullscreenOverlay(
            canvasGo.transform, "DiceRollOverlay", new Color(0.15f, 0.35f, 0.65f, 0.96f), 160);

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[MonopolyGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Monopoly";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "MonopolyGame";
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();

        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "tileAnchors", tileAnchors);
        var tileTypesProp = ctrlSo.FindProperty("tileTypes");
        tileTypesProp.arraySize = layout.Length;
        for (int i = 0; i < layout.Length; i++) tileTypesProp.GetArrayElementAtIndex(i).enumValueIndex = (int)layout[i];
        var tileValuesProp = ctrlSo.FindProperty("tileValues");
        tileValuesProp.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) tileValuesProp.GetArrayElementAtIndex(i).intValue = values[i];

        ctrlSo.FindProperty("leftToken").objectReferenceValue = leftToken;
        ctrlSo.FindProperty("rightToken").objectReferenceValue = rightToken;
        ctrlSo.FindProperty("turnBannerText").objectReferenceValue = turnBanner.GetComponent<Text>();
        ctrlSo.FindProperty("leftDiceButton").objectReferenceValue = leftDiceButtonGo.GetComponent<Button>();
        ctrlSo.FindProperty("leftDiceValueText").objectReferenceValue = leftDiceValueGo.GetComponent<Text>();
        ctrlSo.FindProperty("rightDiceButton").objectReferenceValue = rightDiceButtonGo.GetComponent<Button>();
        ctrlSo.FindProperty("rightDiceValueText").objectReferenceValue = rightDiceValueGo.GetComponent<Text>();
        ctrlSo.FindProperty("diceRollRoot").objectReferenceValue = diceRollRoot;
        ctrlSo.FindProperty("diceRollFullscreenText").objectReferenceValue = diceRollFullscreenText;

        ctrlSo.FindProperty("questionPromptImage").objectReferenceValue = promptImageGo.GetComponent<Image>();
        ctrlSo.FindProperty("questionPromptText").objectReferenceValue = promptTextComp;

        ctrlSo.FindProperty("soloQuestionRoot").objectReferenceValue = soloRoot;
        ctrlSo.FindProperty("soloButtonDisplay").objectReferenceValue = soloButtonDisplay;
        ctrlSo.FindProperty("soloLeftContainer").objectReferenceValue = soloLeftContainer;
        ctrlSo.FindProperty("soloRightContainer").objectReferenceValue = soloRightContainer;

        ctrlSo.FindProperty("luckyWheelRoot").objectReferenceValue = wheelRoot;
        ctrlSo.FindProperty("luckyWheelResultText").objectReferenceValue = wheelResultText.GetComponent<Text>();

        ctrlSo.FindProperty("tileMessageText").objectReferenceValue = tileMessageComp;

        ctrlSo.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrectIcon;
        ctrlSo.FindProperty("leftWrongIcon").objectReferenceValue = leftWrongIcon;
        ctrlSo.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrectIcon;
        ctrlSo.FindProperty("rightWrongIcon").objectReferenceValue = rightWrongIcon;

        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log("[MonopolyGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    static GameObject CreateToken(string name, Transform parent, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    /// <summary>Màu + nhãn mỗi ô — ô Question hiện luôn GIÁ TRỊ điểm (vd "3") thay vì "?" chung
    /// chung, để học sinh thấy rõ "tuỳ theo giá trị của ô" trước khi bước vào.</summary>
    static (Color color, string label) TileVisual(MonopolyTileType type, int value) => type switch
    {
        MonopolyTileType.Question => (new Color(0.55f, 0.75f, 0.95f), value.ToString()),
        MonopolyTileType.LoseTurn => (new Color(0.60f, 0.60f, 0.60f), "SKIP"),
        MonopolyTileType.Reward => (new Color(0.95f, 0.80f, 0.35f), "+"),
        MonopolyTileType.DoublePoints => (new Color(0.65f, 0.55f, 0.90f), "x2"),
        MonopolyTileType.MinusPoints => (new Color(0.90f, 0.45f, 0.45f), "-"),
        MonopolyTileType.LuckyWheel => (new Color(0.45f, 0.85f, 0.65f), "?!"),
        _ => (Color.white, ""),
    };

    /// <summary>Sinh choose.csv (Family, trộn Type A: ảnh-hỏi/chữ-đáp + Type B: chữ-hỏi/ảnh-đáp) —
    /// chỉ tạo nếu CHƯA có file. Dùng lại ảnh Family thật đã có sẵn (Assets/Resources/Family/*.png,
    /// đã import từ WhoIsItGame) — topic=Family để AutoPath tự thêm "Family/" vào key ảnh.</summary>
    static void EnsureChooseCsv()
    {
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(ChooseCsvPath);
        if (File.Exists(absolutePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0,a1,a2,a3,a4,answerMode,correctAnswers");

        for (int i = 0; i < ImageQuestionRows.Length; i++)
        {
            var r = ImageQuestionRows[i];
            sb.AppendLine($"qa{i + 1},Family,1,Image,{r.key},Text,{r.answers[0]},{r.answers[1]},{r.answers[2]},{r.answers[3]},,Single,0");
        }
        for (int i = 0; i < TextQuestionRows.Length; i++)
        {
            var r = TextQuestionRows[i];
            sb.AppendLine($"qb{i + 1},Family,1,Text,{r.question},Image,{r.answerKeys[0]},{r.answerKeys[1]},{r.answerKeys[2]},{r.answerKeys[3]},,Single,0");
        }
        File.WriteAllText(absolutePath, sb.ToString());
    }
}
