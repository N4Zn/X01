using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "FAMILY MEMBER" — sinh từ MiniGameKit. Về cơ bản giống FamilySpellingGameSceneBuilder
/// (cùng art skin nút/back button, cùng cách ẩn LeftStarIcon trang trí, ảnh câu hỏi to không khung
/// nền trắng) nhưng đơn giản hơn: không có "chỗ trống điền chữ" — chỉ hiện ảnh + 4 đáp án chữ
/// (AnswerMode.Single). Tự sinh CSV 7 câu (đúng bộ ảnh + đáp án nhiễu đã dùng ở WhoIsItGame) nếu
/// chưa có, dùng lại ảnh Family thật đã có sẵn. Chạy 1 lần "Tools → FamilyMemberGame → Build Scene"
/// là chơi thử được ngay.
/// </summary>
public static class FamilyMemberGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/FamilyMemberGame";
    const string ScenePath = SceneFolder + "/FamilyMemberGame.unity";
    const string DataFolder = "Assets/Game/Resources/MiniGameKit/FamilyMemberGame";
    const string ChooseCsvPath = DataFolder + "/choose.csv";

    // Sprite thật đã dùng cho SentenceBuilderGame (dò từ FamilySpellingGame.unity) — dùng lại cho
    // đồng bộ giao diện giữa các game trong Kit.
    const string ButtonSpritePath = "Assets/Game/Scenes/FamilySpellingGame/1785484620158_2202675960459059129_3394951359776055861_1aff863233ab7a0fda7023c61c3d0e2c-removebg-preview.png";
    const string BackButtonSpritePath = "Assets/Game/Scenes/FamilySpellingGame/Gemini_Generated_Image_nqeq9cnqeq9cnqeq-removebg-preview.png";
    static readonly Color ButtonOutlineColor = new Color(0.5450981f, 0.2705882f, 0.07450981f, 1f);

    // key ảnh (topic=Family, khớp Assets/Resources/Family/{key}.png), a0..a3 (a0 luôn đúng) — đúng
    // bộ đáp án đã dùng ở WhoIsItGame (đã chứng minh hợp lý, dùng lại nguyên).
    static readonly (string key, string[] answers)[] Rows =
    {
        ("mom",     new[] { "Mom", "Dad", "Brother", "Baby" }),
        ("dad",     new[] { "Dad", "Mom", "Sister", "Grandpa" }),
        ("sister",  new[] { "Sister", "Brother", "Mom", "Grandma" }),
        ("brother", new[] { "Brother", "Sister", "Dad", "Baby" }),
        ("grandma", new[] { "Grandma", "Grandpa", "Mom", "Sister" }),
        ("grandpa", new[] { "Grandpa", "Grandma", "Dad", "Brother" }),
        ("baby",    new[] { "Baby", "Mom", "Sister", "Grandma" }),
    };

    [MenuItem("Tools/FamilyMemberGame/Build Scene")]
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

        var root = new GameObject("FamilyMemberGameRoot");
        var controller = root.AddComponent<FamilyMemberGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // ── HUD prefab mặc định của Kit — ẩn LeftStarIcon trang trí (đè giữa màn hình) ─────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);
        HideDeepChild(hud.transform, "LeftStarIcon");

        // ── Ảnh câu hỏi — hiện riêng CẢ 2 BÊN (không phải 1 ảnh dùng chung ở giữa), giống hệt
        // nhau, không khung nền trắng, parent thẳng vào canvas ──────────────────────────────────
        var leftQuestionImageGo = new GameObject("LeftQuestionImage", typeof(RectTransform), typeof(Image));
        leftQuestionImageGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(leftQuestionImageGo.GetComponent<RectTransform>(),
            new Vector2(0.06f, 0.42f), new Vector2(0.44f, 0.84f), Vector2.zero, Vector2.zero);
        leftQuestionImageGo.GetComponent<Image>().preserveAspect = true;

        var rightQuestionImageGo = new GameObject("RightQuestionImage", typeof(RectTransform), typeof(Image));
        rightQuestionImageGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(rightQuestionImageGo.GetComponent<RectTransform>(),
            new Vector2(0.56f, 0.42f), new Vector2(0.94f, 0.84f), Vector2.zero, Vector2.zero);
        rightQuestionImageGo.GetComponent<Image>().preserveAspect = true;

        // ── 4 đáp án chữ mỗi bên (ButtonDisplay — AnswerMode.Single) — to hết cỡ, không vạch chia ──
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        var leftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "LeftBtn", buttonDisplayGo.transform, new Vector2(0.01f, 0.02f), new Vector2(0.49f, 0.38f), 4);
        var rightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupHorizontal(
            "RightBtn", buttonDisplayGo.transform, new Vector2(0.51f, 0.02f), new Vector2(0.99f, 0.38f), 4);

        // Đáp án dùng chung sprite + viền nâu của FamilySpellingGame — đồng bộ giao diện trong Kit.
        ApplyFamilySpellButtonSkin(leftButtons);
        ApplyFamilySpellButtonSkin(rightButtons);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightButtons);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // ── Đếm ngược "Next in Ns" mặc định của Kit — cùng khung với vùng nút ───────────────────
        var leftCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "LeftCountdownText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftCountdown.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0.02f), new Vector2(0.49f, 0.38f), Vector2.zero, Vector2.zero);
        leftCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftCountdown.SetActive(false);

        var rightCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "RightCountdownText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightCountdown.GetComponent<RectTransform>(),
            new Vector2(0.51f, 0.02f), new Vector2(0.99f, 0.38f), Vector2.zero, Vector2.zero);
        rightCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightCountdown.SetActive(false);

        // ── Feedback mặc định của Kit (âm thanh + icon đúng/sai) — to, giữa vùng nút mỗi bên ────
        var leftIconMin = new Vector2(0.11f, 0.08f);
        var leftIconMax = new Vector2(0.39f, 0.32f);
        var rightIconMin = new Vector2(0.61f, 0.08f);
        var rightIconMax = new Vector2(0.89f, 0.32f);
        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true, leftIconMin, leftIconMax);
        var leftWrongIcon = FeedbackIconBuilder.Create("LeftWrongIcon", canvasGo.transform, false, leftIconMin, leftIconMax);
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true, rightIconMin, rightIconMax);
        var rightWrongIcon = FeedbackIconBuilder.Create("RightWrongIcon", canvasGo.transform, false, rightIconMin, rightIconMax);

        // Back button + tutorial
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        ApplyFamilySpellBackButtonSkin(backButton);
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Family Member");

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[FamilyMemberGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Family Member";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "FamilyMemberGame";
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("leftQuestionImage").objectReferenceValue = leftQuestionImageGo.GetComponent<Image>();
        ctrlSo.FindProperty("rightQuestionImage").objectReferenceValue = rightQuestionImageGo.GetComponent<Image>();
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

        Debug.Log("[FamilyMemberGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    static void ApplyFamilySpellButtonSkin(ButtonItem[] items)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[FamilyMemberGameSceneBuilder] Không tìm thấy sprite nút tại {ButtonSpritePath} — dùng màu mặc định của Kit.");
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

            // Chữ trắng, bold — nền gỗ/nâu tối nên chữ tối mặc định (0.2,0.2,0.2) khó đọc.
            var label = item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = Color.white;
                label.fontStyle = FontStyles.Bold;
            }
        }
    }

    static void ApplyFamilySpellBackButtonSkin(GameObject backButtonGo)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackButtonSpritePath);
        if (sprite == null) return;
        var img = backButtonGo.GetComponent<Image>();
        if (img != null) img.sprite = sprite;
    }

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

    /// <summary>Sinh choose.csv — chỉ tạo nếu CHƯA có file. topic=Family để AutoPath tự thêm
    /// "Family/" vào key ảnh — dùng lại đúng ảnh thật đã có (Assets/Resources/Family/*.png).</summary>
    static void EnsureChooseCsv()
    {
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(ChooseCsvPath);
        if (File.Exists(absolutePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0,a1,a2,a3,a4,answerMode,correctAnswers");
        for (int i = 0; i < Rows.Length; i++)
        {
            var r = Rows[i];
            sb.AppendLine($"q{i + 1},Family,1,Image,{r.key},Text,{r.answers[0]},{r.answers[1]},{r.answers[2]},{r.answers[3]},,Single,0");
        }
        File.WriteAllText(absolutePath, sb.ToString());
    }
}
