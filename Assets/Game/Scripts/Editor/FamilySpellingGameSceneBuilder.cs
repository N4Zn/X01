using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "FAMILY SPELLING JUMP" — sinh từ MiniGameKit. Tự sinh CSV 20 câu (đúng nội dung
/// đề bài gốc) nếu chưa có — chạy 1 lần "Tools → FamilySpellingGame → Build Scene" là chơi thử
/// được ngay (dùng lại ảnh Family thật đã có sẵn từ WhoIsItGame, không cần asset riêng).
/// </summary>
public static class FamilySpellingGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/FamilySpellingGame";
    const string ScenePath = SceneFolder + "/FamilySpellingGame.unity";
    const string DataFolder = "Assets/Game/Resources/MiniGameKit/FamilySpellingGame";
    const string ChooseCsvPath = DataFolder + "/choose.csv";

    // template (chữ trống đánh dấu "_"), family key (dùng ảnh có sẵn topic=Family), 4 lựa chọn, index đúng
    static readonly (string template, string key, string[] options, int correctIndex)[] Words =
    {
        ("M_M",     "mom",     new[] { "A", "O", "E", "I" }, 1),
        ("D_D",     "dad",     new[] { "A", "E", "I", "O" }, 0),
        ("S_STER",  "sister",  new[] { "I", "E", "A", "O" }, 0),
        ("BR_THER", "brother", new[] { "O", "A", "I", "U" }, 0),
        ("GR_NDMA", "grandma", new[] { "A", "O", "E", "I" }, 0),
        ("GR_NDPA", "grandpa", new[] { "A", "O", "E", "I" }, 0),
        ("B_BY",    "baby",    new[] { "A", "E", "I", "O" }, 0),
        ("MO_",     "mom",     new[] { "M", "N", "T", "P" }, 0),
        ("_AD",     "dad",     new[] { "D", "B", "P", "T" }, 0),
        ("SIS_ER",  "sister",  new[] { "T", "P", "D", "M" }, 0),
        ("BROTH_R", "brother", new[] { "E", "A", "I", "O" }, 0),
        ("GRANDM_", "grandma", new[] { "A", "O", "E", "I" }, 0),
        ("GRANDP_", "grandpa", new[] { "A", "E", "O", "I" }, 0),
        ("BA_Y",    "baby",    new[] { "B", "D", "P", "T" }, 0),
        ("_OM",     "mom",     new[] { "M", "N", "P", "T" }, 0),
        ("DA_",     "dad",     new[] { "D", "B", "P", "T" }, 0),
        ("_ISTER",  "sister",  new[] { "S", "T", "P", "D" }, 0),
        ("B_OTHER", "brother", new[] { "R", "L", "M", "N" }, 0),
        ("GRA_DMA", "grandma", new[] { "N", "M", "R", "T" }, 0),
        ("GRA_DPA", "grandpa", new[] { "N", "R", "M", "T" }, 0),
    };

    [MenuItem("Tools/FamilySpellingGame/Build Scene")]
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

        var root = new GameObject("FamilySpellingGameRoot");
        var controller = root.AddComponent<FamilySpellingGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // ── HUD — prefab thật mặc định của Kit ──────────────────────────────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ── Khung ảnh câu hỏi (hình gia đình) — giữa màn hình, phía trên ────────
        var frame = MiniGameSceneBuilderHelpers.CreateImage("QuestionFrame", canvasGo.transform, new Color(1f, 1f, 1f, 0.9f));
        MiniGameSceneBuilderHelpers.SetRect(frame.GetComponent<RectTransform>(),
            new Vector2(0.38f, 0.55f), new Vector2(0.62f, 0.86f), Vector2.zero, Vector2.zero);

        var questionImageGo = new GameObject("QuestionImage", typeof(RectTransform), typeof(Image));
        questionImageGo.transform.SetParent(frame.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(questionImageGo.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
        questionImageGo.GetComponent<Image>().preserveAspect = true;

        // ── Từ bị thiếu 1 chữ cái — ngay dưới ảnh, to rõ để đọc từ xa ───────────
        var wordText = MiniGameSceneBuilderHelpers.CreateText(
            "WordTemplateText", canvasGo.transform, "", 44, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(wordText.GetComponent<RectTransform>(),
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero);
        var wordTextComp = wordText.GetComponent<Text>();
        wordTextComp.fontStyle = FontStyle.Bold;
        wordTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        wordTextComp.verticalOverflow = VerticalWrapMode.Overflow;

        // ── Vạch chia giữa sàn — vùng học sinh đứng (đáp án), không chọc vào ảnh/từ ─
        var divider = MiniGameSceneBuilderHelpers.CreateImage("Divider", canvasGo.transform, new Color(0.6f, 0.6f, 0.6f, 0.6f));
        MiniGameSceneBuilderHelpers.SetRect(divider.GetComponent<RectTransform>(),
            new Vector2(0.497f, 0.02f), new Vector2(0.503f, 0.35f), Vector2.zero, Vector2.zero);

        // ── 4 chữ cái mỗi bên, xếp hàng ngang sát cạnh dưới (nơi học sinh đứng) ──
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        var leftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "LeftBtn", buttonDisplayGo.transform, new Vector2(0.03f, 0.05f), new Vector2(0.47f, 0.32f), 4);
        var rightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "RightBtn", buttonDisplayGo.transform, new Vector2(0.53f, 0.05f), new Vector2(0.97f, 0.32f), 4);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightButtons);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // ── Feedback mặc định của Kit (âm thanh đã tự chạy qua MiniGameControllerBase — đây chỉ
        // cần dựng icon ✔/✖ mỗi bên, tái dùng FeedbackIconBuilder giống AddUpGame/TestTongHopGame).
        // To, giữa vùng chơi mỗi bên (không phải áp sát 1 nút đáp án cụ thể) — cùng 1 khung với
        // countdown text bên dưới để 2 hiệu ứng luôn đúng vị trí "giữa sân" của người chơi đó.
        var leftZoneMin = new Vector2(0.09f, 0.05f);
        var leftZoneMax = new Vector2(0.41f, 0.32f);
        var rightZoneMin = new Vector2(0.59f, 0.05f);
        var rightZoneMax = new Vector2(0.91f, 0.32f);

        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true, leftZoneMin, leftZoneMax);
        var leftWrongIcon = FeedbackIconBuilder.Create("LeftWrongIcon", canvasGo.transform, false, leftZoneMin, leftZoneMax);
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true, rightZoneMin, rightZoneMax);
        var rightWrongIcon = FeedbackIconBuilder.Create("RightWrongIcon", canvasGo.transform, false, rightZoneMin, rightZoneMax);

        // ── Đếm ngược "Next in Ns" mặc định của Kit — hiện trong lúc chờ chuyển câu (khoảng chờ
        // lấy từ GameSettings.RoundEndDelay), cho học sinh kịp di chuyển khỏi ô vừa trả lời. Icon
        // đã ẩn hẳn trước khi countdown này hiện (xem MiniGameControllerBase.NextRoundAfterDelay)
        // nên dùng chung được đúng khung "giữa sân" ở trên, không bao giờ chồng hình.
        var leftCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "LeftCountdownText", canvasGo.transform, "", 42, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftCountdown.GetComponent<RectTransform>(), leftZoneMin, leftZoneMax, Vector2.zero, Vector2.zero);
        leftCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftCountdown.SetActive(false);

        var rightCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "RightCountdownText", canvasGo.transform, "", 42, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightCountdown.GetComponent<RectTransform>(), rightZoneMin, rightZoneMax, Vector2.zero, Vector2.zero);
        rightCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightCountdown.SetActive(false);

        // Back button
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));

        // Tutorial
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Family Spelling Jump");

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[FamilySpellingGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Family Spelling Jump";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "FamilySpellingGame";
        // totalRounds/questionTimeout để mặc định của Kit (0/0) — chơi theo thời gian
        // (GameSettings.GameTime), 20 câu trong CSV tự shuffle lặp lại khi hết, không giới hạn
        // giây/câu (học sinh cần bao lâu cũng được).
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("questionImage").objectReferenceValue = questionImageGo.GetComponent<Image>();
        ctrlSo.FindProperty("wordTemplateText").objectReferenceValue = wordTextComp;
        ctrlSo.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrectIcon;
        ctrlSo.FindProperty("leftWrongIcon").objectReferenceValue = leftWrongIcon;
        ctrlSo.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrectIcon;
        ctrlSo.FindProperty("rightWrongIcon").objectReferenceValue = rightWrongIcon;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdown.GetComponent<Text>();
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdown.GetComponent<Text>();
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log("[FamilySpellingGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    /// <summary>Sinh choose.csv từ mảng Words ở trên — chỉ tạo nếu CHƯA có file (không ghi đè
    /// nếu bạn tự sửa tay CSV sau này). Muốn quay lại bản gốc: xoá file rồi Build Scene lại.
    /// `id` = chính template chữ trống (vd "M_M") — mỗi câu duy nhất, controller đọc lại từ đó
    /// để hiện từ đúng vị trí trống, không cần thêm cột CSV mới.</summary>
    static void EnsureChooseCsv()
    {
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(ChooseCsvPath);
        if (File.Exists(absolutePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0,a1,a2,a3,a4,answerMode,correctAnswers");
        foreach (var w in Words)
            sb.AppendLine($"{w.template},Family,1,Image,{w.key},Text,{w.options[0]},{w.options[1]},{w.options[2]},{w.options[3]},,Single,{w.correctIndex}");
        File.WriteAllText(absolutePath, sb.ToString());
    }
}
