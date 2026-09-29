using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "SENTENCE BUILDER" — sinh từ MiniGameKit, tái dùng ButtonDisplay (AnswerMode.
/// OrderedSequence) của TestTongHopGame. Hiện 1 hình trạng thái (vd "thirsty") + 3 chỗ trống, 4 nút
/// đáp án (3 đúng đúng thứ tự + 1 nhiễu). Tự sinh CSV 8 câu (I am / You are / We are / They are +
/// 8 từ trạng thái, chéo nhiễu lẫn nhau) nếu chưa có, dùng ảnh + audio thật đã có sẵn tại
/// Assets/Resources/Family/. Chạy 1 lần "Tools → SentenceBuilderGame → Build Scene" là chơi thử
/// được ngay.
/// </summary>
public static class SentenceBuilderGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/SentenceBuilderGame";
    const string ScenePath = SceneFolder + "/SentenceBuilderGame.unity";
    const string DataFolder = "Assets/Game/Resources/MiniGameKit/SentenceBuilderGame";
    const string ChooseCsvPath = DataFolder + "/choose.csv";

    // Sprite thật đã xác nhận qua .unity của FamilySpellingGame (dò guid trong scene → .meta):
    // ButtonItem.bgImage dùng đúng file này (kèm Outline nâu), BackButton dùng file kia.
    const string ButtonSpritePath = "Assets/Game/Scenes/FamilySpellingGame/1785484620158_2202675960459059129_3394951359776055861_1aff863233ab7a0fda7023c61c3d0e2c-removebg-preview.png";
    const string BackButtonSpritePath = "Assets/Game/Scenes/FamilySpellingGame/Gemini_Generated_Image_nqeq9cnqeq9cnqeq-removebg-preview.png";
    static readonly Color ButtonOutlineColor = new Color(0.5450981f, 0.2705882f, 0.07450981f, 1f);

    // state (topic=Family, khớp ảnh Assets/Resources/Family/{state}.png + audio {state}.mp3),
    // subject+verb (khớp đúng audio I/You/We/They + am/are đã có sẵn), state đối lập dùng làm nhiễu.
    static readonly (string state, string subject, string verb, string distractor)[] Rows =
    {
        ("thirsty", "I",    "am",  "hungry"),
        ("hungry",  "You",  "are", "thirsty"),
        ("tired",   "We",   "are", "sleepy"),
        ("sleepy",  "They", "are", "tired"),
        ("happy",   "I",    "am",  "sad"),
        ("sad",     "You",  "are", "happy"),
        ("hot",     "We",   "are", "cold"),
        ("cold",    "They", "are", "hot"),
    };

    [MenuItem("Tools/SentenceBuilderGame/Build Scene")]
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

        var root = new GameObject("SentenceBuilderGameRoot");
        var controller = root.AddComponent<SentenceBuilderGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // ── HUD prefab mặc định của Kit — ẩn LeftStarIcon trang trí (đè lên giữa màn hình, xấu
        // với layout game này) — chỉ ẩn cục bộ ở game này, KHÔNG sửa prefab dùng chung ───────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);
        HideDeepChild(hud.transform, "LeftStarIcon");

        // ── Ảnh câu hỏi (dùng chung 2 bên — cùng 1 câu) — to ~1.5x, KHÔNG còn khung nền trắng,
        // parent thẳng vào canvas ──────────────────────────────────────────────────────────────
        var questionImageGo = new GameObject("QuestionImage", typeof(RectTransform), typeof(Image));
        questionImageGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(questionImageGo.GetComponent<RectTransform>(),
            new Vector2(0.30f, 0.46f), new Vector2(0.70f, 0.86f), Vector2.zero, Vector2.zero);
        questionImageGo.GetComponent<Image>().preserveAspect = true;

        // ── Chỗ trống "___ ___ ___" — điền dần theo từng từ đúng, riêng mỗi bên ──
        var leftBlanks = MiniGameSceneBuilderHelpers.CreateText(
            "LeftBlanksText", canvasGo.transform, "", 32, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftBlanks.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.36f), new Vector2(0.48f, 0.45f), Vector2.zero, Vector2.zero);
        var leftBlanksComp = leftBlanks.GetComponent<Text>();
        leftBlanksComp.fontStyle = FontStyle.Bold;
        leftBlanksComp.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rightBlanks = MiniGameSceneBuilderHelpers.CreateText(
            "RightBlanksText", canvasGo.transform, "", 32, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightBlanks.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.36f), new Vector2(0.98f, 0.45f), Vector2.zero, Vector2.zero);
        var rightBlanksComp = rightBlanks.GetComponent<Text>();
        rightBlanksComp.fontStyle = FontStyle.Bold;
        rightBlanksComp.horizontalOverflow = HorizontalWrapMode.Overflow;

        // ── 4 nút đáp án mỗi bên (ButtonDisplay — AnswerMode.OrderedSequence) — to hết cỡ, gần
        // sát mép màn hình, không còn vạch chia giữa (bỏ theo yêu cầu, xấu) ─────────────────────
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        var leftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "LeftBtn", buttonDisplayGo.transform, new Vector2(0.01f, 0.02f), new Vector2(0.49f, 0.35f), 4);
        var rightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "RightBtn", buttonDisplayGo.transform, new Vector2(0.51f, 0.02f), new Vector2(0.99f, 0.35f), 4);

        // Đáp án dùng chung sprite + viền nâu của FamilySpellingGame (xem ApplyFamilySpellButtonSkin)
        // thay vì màu phẳng mặc định của Kit.
        ApplyFamilySpellButtonSkin(leftButtons);
        ApplyFamilySpellButtonSkin(rightButtons);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightButtons);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // ── Đếm ngược "Next in Ns" mặc định của Kit — cùng khung với vùng nút (đã ẩn khi countdown hiện) ──
        var leftCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "LeftCountdownText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftCountdown.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0.02f), new Vector2(0.49f, 0.35f), Vector2.zero, Vector2.zero);
        leftCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftCountdown.SetActive(false);

        var rightCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "RightCountdownText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightCountdown.GetComponent<RectTransform>(),
            new Vector2(0.51f, 0.02f), new Vector2(0.99f, 0.35f), Vector2.zero, Vector2.zero);
        rightCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightCountdown.SetActive(false);

        // ── Feedback mặc định của Kit (âm thanh + icon đúng/sai) — to, giữa vùng nút mỗi bên ────
        var leftIconMin = new Vector2(0.11f, 0.06f);
        var leftIconMax = new Vector2(0.39f, 0.31f);
        var rightIconMin = new Vector2(0.61f, 0.06f);
        var rightIconMax = new Vector2(0.89f, 0.31f);
        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true, leftIconMin, leftIconMax);
        var leftWrongIcon = FeedbackIconBuilder.Create("LeftWrongIcon", canvasGo.transform, false, leftIconMin, leftIconMax);
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true, rightIconMin, rightIconMax);
        var rightWrongIcon = FeedbackIconBuilder.Create("RightWrongIcon", canvasGo.transform, false, rightIconMin, rightIconMax);

        // Back button + tutorial
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        ApplyFamilySpellBackButtonSkin(backButton);
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Sentence Builder");

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[SentenceBuilderGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Sentence Builder";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "SentenceBuilderGame";
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("questionImage").objectReferenceValue = questionImageGo.GetComponent<Image>();
        ctrlSo.FindProperty("leftBlanksText").objectReferenceValue = leftBlanksComp;
        ctrlSo.FindProperty("rightBlanksText").objectReferenceValue = rightBlanksComp;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdown.GetComponent<Text>();
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdown.GetComponent<Text>();
        ctrlSo.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrectIcon;
        ctrlSo.FindProperty("leftWrongIcon").objectReferenceValue = leftWrongIcon;
        ctrlSo.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrectIcon;
        ctrlSo.FindProperty("rightWrongIcon").objectReferenceValue = rightWrongIcon;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log("[SentenceBuilderGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    /// <summary>Áp sprite + viền nâu của FamilySpellingGame lên các nút đáp án (ButtonItem.bgImage
    /// + Outline) — thay cho màu phẳng mặc định của Kit. Bỏ qua an toàn nếu không tìm thấy sprite
    /// (vd file bị di chuyển/xoá) — nút vẫn hiện bằng màu Image mặc định, không lỗi.</summary>
    static void ApplyFamilySpellButtonSkin(ButtonItem[] items)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[SentenceBuilderGameSceneBuilder] Không tìm thấy sprite nút tại {ButtonSpritePath} — dùng màu mặc định của Kit.");
            return;
        }

        foreach (var item in items)
        {
            if (item == null) continue;
            var img = item.GetComponent<Image>();
            if (img != null) img.sprite = sprite;

            var outline = item.GetComponent<Outline>();
            if (outline == null) outline = item.gameObject.AddComponent<Outline>();
            outline.effectColor = ButtonOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
        }
    }

    static void ApplyFamilySpellBackButtonSkin(GameObject backButtonGo)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackButtonSpritePath);
        if (sprite == null) return;
        var img = backButtonGo.GetComponent<Image>();
        if (img != null) img.sprite = sprite;
    }

    /// <summary>Ẩn 1 con (ở bất kỳ độ sâu nào) theo tên trong prefab đã instantiate — dùng để tắt
    /// riêng cho GAME NÀY 1 phần tử trang trí của GameHUD.prefab (vd LeftStarIcon) mà KHÔNG sửa
    /// prefab dùng chung cho các game khác. Bỏ qua an toàn nếu không tìm thấy.</summary>
    static void HideDeepChild(Transform root, string childName)
    {
        var found = FindDeepChild(root, childName);
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

    /// <summary>Sinh choose.csv — chỉ tạo nếu CHƯA có file. correctAnswers luôn "0,2,3" (a0=subject,
    /// a2=verb, a3=state — đúng thứ tự cần bấm; a1=nhiễu). QUAN TRỌNG: chữ trong a0-a3 phải khớp
    /// CHÍNH XÁC (kể cả hoa/thường) tên file audio trong Assets/Resources/Family/ vì controller phát
    /// âm trực tiếp theo giá trị này (Resources.Load&lt;AudioClip&gt;("Family/"+word)).</summary>
    static void EnsureChooseCsv()
    {
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(ChooseCsvPath);
        if (File.Exists(absolutePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0,a1,a2,a3,a4,answerMode,correctAnswers");
        for (int i = 0; i < Rows.Length; i++)
        {
            var r = Rows[i];
            sb.AppendLine($"q{i + 1},Family,1,Image,{r.state},Text,{r.subject},{r.distractor},{r.verb},{r.state},,OrderedSequence,\"0,2,3\"");
        }
        File.WriteAllText(absolutePath, sb.ToString());
    }
}
