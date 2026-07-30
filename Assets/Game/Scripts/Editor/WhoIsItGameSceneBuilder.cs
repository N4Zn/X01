using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "WHO IS IT?" — sinh bởi /newminigame từ MiniGameKit (xem
/// Assets/Game/Scripts/Core/MiniGameKit/README.md). Tự sinh CSV nội dung + placeholder ảnh
/// (vòng tròn màu) nếu chưa có — chạy 1 lần "Tools → WhoIsItGame → Build Scene" là ra scene
/// chơi thử được ngay, không cần chuẩn bị file gì trước. Thay CSV/ảnh thật sau, không cần sửa code.
/// </summary>
public static class WhoIsItGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/WhoIsItGame";
    const string ScenePath = SceneFolder + "/WhoIsItGame.unity";
    const string DataFolder = "Assets/Game/Resources/MiniGameKit/WhoIsItGame";
    const string ImageFolder = DataFolder + "/images";
    const string ChooseCsvPath = DataFolder + "/choose.csv";

    // key, caption noun, color, a0..a3 (a0 luôn là đáp án đúng)
    static readonly (string key, string noun, Color color, string[] answers)[] Words =
    {
        ("mom",     "mother",      new Color(0.95f, 0.45f, 0.60f), new[] { "Mom", "Dad", "Brother", "Baby" }),
        ("dad",     "father",      new Color(0.30f, 0.55f, 0.85f), new[] { "Dad", "Mom", "Sister", "Grandpa" }),
        ("sister",  "sister",      new Color(0.95f, 0.75f, 0.30f), new[] { "Sister", "Brother", "Mom", "Grandma" }),
        ("brother", "brother",     new Color(0.40f, 0.75f, 0.55f), new[] { "Brother", "Sister", "Dad", "Baby" }),
        ("grandma", "grandmother", new Color(0.70f, 0.55f, 0.85f), new[] { "Grandma", "Grandpa", "Mom", "Sister" }),
        ("grandpa", "grandfather", new Color(0.55f, 0.55f, 0.55f), new[] { "Grandpa", "Grandma", "Dad", "Brother" }),
        ("baby",    "baby",        new Color(0.95f, 0.90f, 0.55f), new[] { "Baby", "Mom", "Sister", "Grandma" }),
    };

    [MenuItem("Tools/WhoIsItGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Resources/MiniGameKit");
        MiniGameSceneBuilderHelpers.EnsureFolder(DataFolder);
        MiniGameSceneBuilderHelpers.EnsureFolder(ImageFolder);

        foreach (var w in Words) CreatePlaceholderCircle(w.key, w.color);
        EnsureChooseCsv();
        AssetDatabase.Refresh();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("WhoIsItGameRoot");
        var controller = root.AddComponent<WhoIsItGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // ── Thứ tự tạo QUYẾT ĐỊNH thứ tự vẽ (sibling sau vẽ đè lên sibling trước) — theo đúng
        // yêu cầu: GameHUD → RewardBadge → QuestionFrame → Divider → ButtonDisplay → Feedback.
        // Trước đây QuestionFrame tạo TRƯỚC HUD nên bị HUD (nền to) đè lên, che mất ảnh.

        // ── HUD — dùng prefab có sẵn của bạn (đã có GameHUD + nghệ thuật riêng) ─────────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ── Khung tròn "current reward" — ngay dưới time, phóng to theo điểm câu hỏi hiện tại ──
        CreateRewardCircleSprite();
        var rewardSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ImageFolder}/reward_circle.png");

        var rewardBadgeGo = new GameObject("RewardBadge", typeof(RectTransform), typeof(Image));
        rewardBadgeGo.transform.SetParent(canvasGo.transform, false);
        var rewardBadgeRt = rewardBadgeGo.GetComponent<RectTransform>();
        rewardBadgeRt.anchorMin = rewardBadgeRt.anchorMax = new Vector2(0.5f, 0.77f);
        rewardBadgeRt.pivot = new Vector2(0.5f, 0.5f);
        rewardBadgeRt.sizeDelta = new Vector2(110f, 110f);
        var rewardBadgeImg = rewardBadgeGo.GetComponent<Image>();
        if (rewardSprite != null) rewardBadgeImg.sprite = rewardSprite;
        else rewardBadgeImg.color = new Color(1f, 0.85f, 0.3f, 1f);

        var rewardTextGo = MiniGameSceneBuilderHelpers.CreateText(
            "RewardText", rewardBadgeGo.transform, "5 points!", 16, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rewardTextGo.GetComponent<RectTransform>(),
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        rewardTextGo.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rewardTextGo.GetComponent<Text>().color = Color.white;

        // ── Khung câu hỏi: ảnh chiếm FULL khung (không còn caption — đã có ảnh thật, không cần
        // chú thích chữ nữa). Khung kéo cao tối đa: từ sát mép dưới RewardBadge (~0.66) xuống sát
        // vùng feedback text (0.35) — không còn chừa chỗ cho caption như trước.
        var frame = MiniGameSceneBuilderHelpers.CreateImage("QuestionFrame", canvasGo.transform, new Color(1f, 1f, 1f, 0.9f));
        MiniGameSceneBuilderHelpers.SetRect(frame.GetComponent<RectTransform>(),
            new Vector2(0.30f, 0.35f), new Vector2(0.70f, 0.66f), Vector2.zero, Vector2.zero);

        var questionImageGo = new GameObject("QuestionImage", typeof(RectTransform), typeof(Image));
        questionImageGo.transform.SetParent(frame.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(questionImageGo.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
        questionImageGo.GetComponent<Image>().preserveAspect = true;

        // ── Vạch chia giữa sàn — chỉ trong vùng học sinh đứng (bet/đáp án/hộp quà, y 0.02-0.34),
        // KHÔNG kéo lên chọc vào khung ảnh (khung ảnh bắt đầu từ 0.44).
        var divider = MiniGameSceneBuilderHelpers.CreateImage("Divider", canvasGo.transform, new Color(0.6f, 0.6f, 0.6f, 0.6f));
        MiniGameSceneBuilderHelpers.SetRect(divider.GetComponent<RectTransform>(),
            new Vector2(0.497f, 0.02f), new Vector2(0.503f, 0.35f), Vector2.zero, Vector2.zero);

        // ── 4 đáp án mỗi bên, xếp HÌNH CUNG (fan) — 2 ngoài rìa thấp sát cạnh dưới (nơi học
        // sinh đứng), 2 ở giữa cao hơn, toả lên như tay quạt (theo hình bạn vẽ) ──────────────
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();
        buttonDisplayGo.SetActive(false); // ẩn đến khi WaitAnswer — tránh đè lên bet UI/hộp quà (cùng chỗ ở đáy)

        var arcRadius = new Vector2(0.20f, 0.19f);
        var arcItemSize = new Vector2(0.11f, 0.12f);
        var leftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupArc(
            "LeftBtn", buttonDisplayGo.transform, new Vector2(0.25f, 0.02f), arcRadius, 160f, 20f, arcItemSize, 4);
        var rightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroupArc(
            "RightBtn", buttonDisplayGo.transform, new Vector2(0.75f, 0.02f), arcRadius, 160f, 20f, arcItemSize, 4);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightButtons);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // ── Feedback riêng mỗi bên — "Try again"/"Great job!"/text hộp quà (có rich-text màu),
        // đặt cao hơn (sát khung ảnh) và KHÔNG xuống dòng — chữ hộp quà khá dài (vd "Share! 2
        // points for Player 2"). Font to hơn cho dễ đọc từ xa.
        var leftFeedbackText = MiniGameSceneBuilderHelpers.CreateText(
            "LeftFeedbackText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftFeedbackText.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.35f), new Vector2(0.48f, 0.43f), Vector2.zero, Vector2.zero);
        var leftFeedbackTextComp = leftFeedbackText.GetComponent<Text>();
        leftFeedbackTextComp.fontStyle = FontStyle.Bold;
        leftFeedbackTextComp.supportRichText = true;
        leftFeedbackTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        leftFeedbackTextComp.verticalOverflow = VerticalWrapMode.Overflow;

        var rightFeedbackText = MiniGameSceneBuilderHelpers.CreateText(
            "RightFeedbackText", canvasGo.transform, "", 30, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightFeedbackText.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.35f), new Vector2(0.98f, 0.43f), Vector2.zero, Vector2.zero);
        var rightFeedbackTextComp = rightFeedbackText.GetComponent<Text>();
        rightFeedbackTextComp.fontStyle = FontStyle.Bold;
        rightFeedbackTextComp.supportRichText = true;
        rightFeedbackTextComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        rightFeedbackTextComp.verticalOverflow = VerticalWrapMode.Overflow;

        // Star burst (đơn giản: 1 ảnh sao vàng, scale+fade khi đúng — xem PlayStarBurst())
        var starBurstGo = new GameObject("StarBurst", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        starBurstGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect(starBurstGo.GetComponent<RectTransform>(),
            new Vector2(0.42f, 0.38f), new Vector2(0.58f, 0.58f), Vector2.zero, Vector2.zero);
        starBurstGo.GetComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 1f);
        starBurstGo.SetActive(false);

        // ── Bet phase UI: "Hope Star? Yes/No" — đặt SÁT ĐÁY màn hình (đúng vùng học sinh đứng,
        // trước khi câu hỏi + đáp án hiện ra). Chờ CẢ 2 bên bấm Yes/No mới qua câu hỏi.
        var betRoot = MiniGameSceneBuilderHelpers.CreateImage("BetPromptRoot", canvasGo.transform, new Color(1f, 1f, 0.85f, 0.95f));
        MiniGameSceneBuilderHelpers.SetRect(betRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.34f), Vector2.zero, Vector2.zero);

        // Left half
        var leftBetTitle = MiniGameSceneBuilderHelpers.CreateText(
            "LeftTitle", betRoot.transform, "Hope Star?", 20, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftBetTitle.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.72f), new Vector2(0.48f, 0.95f), Vector2.zero, Vector2.zero);
        leftBetTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var leftStarCount = MiniGameSceneBuilderHelpers.CreateText(
            "LeftStarCount", betRoot.transform, "* 3", 18, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftStarCount.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.50f), new Vector2(0.48f, 0.70f), Vector2.zero, Vector2.zero);

        var leftYesBtn = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "LeftYesButton", betRoot.transform, "YES", new Vector2(0.02f, 0.03f), new Vector2(0.23f, 0.46f));
        leftYesBtn.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.35f, 1f);
        var leftNoBtn = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "LeftNoButton", betRoot.transform, "NO", new Vector2(0.27f, 0.03f), new Vector2(0.48f, 0.46f));
        leftNoBtn.GetComponent<Image>().color = new Color(0.85f, 0.35f, 0.35f, 1f);

        // Right half
        var rightBetTitle = MiniGameSceneBuilderHelpers.CreateText(
            "RightTitle", betRoot.transform, "Hope Star?", 20, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightBetTitle.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.72f), new Vector2(0.98f, 0.95f), Vector2.zero, Vector2.zero);
        rightBetTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var rightStarCount = MiniGameSceneBuilderHelpers.CreateText(
            "RightStarCount", betRoot.transform, "* 3", 18, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightStarCount.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.50f), new Vector2(0.98f, 0.70f), Vector2.zero, Vector2.zero);

        var rightYesBtn = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "RightYesButton", betRoot.transform, "YES", new Vector2(0.52f, 0.03f), new Vector2(0.73f, 0.46f));
        rightYesBtn.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.35f, 1f);
        var rightNoBtn = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "RightNoButton", betRoot.transform, "NO", new Vector2(0.77f, 0.03f), new Vector2(0.98f, 0.46f));
        rightNoBtn.GetComponent<Image>().color = new Color(0.85f, 0.35f, 0.35f, 1f);

        betRoot.SetActive(false);

        // ── Hộp quà bí mật: 3 nút "?" mỗi bên, SÁT ĐÁY màn hình (cùng vùng bet UI/đáp án — không
        // bao giờ hiện cùng lúc nên dùng chung được khoảng không gian). Màu random mỗi lần hiện
        // (xem WhoIsItGameController.RandomizeBoxColors) + hoạ tiết nơ quà cho đỡ trơn.
        var leftBoxRoot = MiniGameSceneBuilderHelpers.CreateImage("LeftMysteryBoxRoot", canvasGo.transform, new Color(0.15f, 0.15f, 0.20f, 0.85f));
        MiniGameSceneBuilderHelpers.SetRect(leftBoxRoot.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.02f), new Vector2(0.48f, 0.34f), Vector2.zero, Vector2.zero);
        leftBoxRoot.GetComponent<Image>().raycastTarget = false;
        var leftBoxes = CreateMysteryBoxTrio(leftBoxRoot.transform);
        leftBoxRoot.SetActive(false);

        var rightBoxRoot = MiniGameSceneBuilderHelpers.CreateImage("RightMysteryBoxRoot", canvasGo.transform, new Color(0.15f, 0.15f, 0.20f, 0.85f));
        MiniGameSceneBuilderHelpers.SetRect(rightBoxRoot.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.02f), new Vector2(0.98f, 0.34f), Vector2.zero, Vector2.zero);
        rightBoxRoot.GetComponent<Image>().raycastTarget = false;
        var rightBoxes = CreateMysteryBoxTrio(rightBoxRoot.transform);
        rightBoxRoot.SetActive(false);

        // Back button
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));

        // Tutorial
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Who Is It?");

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[WhoIsItGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Who Is It?";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "WhoIsItGame";
        ctrlSo.FindProperty("totalRounds").intValue = 0; // 0 = chơi theo thời gian (GameSettings.GameTime), câu hỏi tự shuffle lại trong pool khi hết
        ctrlSo.FindProperty("questionTimeout").floatValue = 0f;
        ctrlSo.FindProperty("feedbackDelayCorrect").floatValue = 3.5f; // đủ thời gian đọc text hộp quà (dài hơn "Great job!" thường)
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("questionFrameRoot").objectReferenceValue = frame;
        ctrlSo.FindProperty("questionImage").objectReferenceValue = questionImageGo.GetComponent<Image>();
        // questionCaption CỐ Ý để trống — đã có ảnh thật, không cần chú thích chữ nữa (field vẫn
        // còn trong controller cho các game demo/placeholder sau này cần caption).
        ctrlSo.FindProperty("rewardBadge").objectReferenceValue = rewardBadgeGo.GetComponent<RectTransform>();
        ctrlSo.FindProperty("rewardText").objectReferenceValue = rewardTextGo.GetComponent<Text>();
        ctrlSo.FindProperty("leftFeedbackText").objectReferenceValue = leftFeedbackText.GetComponent<Text>();
        ctrlSo.FindProperty("rightFeedbackText").objectReferenceValue = rightFeedbackText.GetComponent<Text>();
        ctrlSo.FindProperty("starBurst").objectReferenceValue = starBurstGo.GetComponent<RectTransform>();
        ctrlSo.FindProperty("betPromptRoot").objectReferenceValue = betRoot;
        ctrlSo.FindProperty("leftYesButton").objectReferenceValue = leftYesBtn.GetComponent<Button>();
        ctrlSo.FindProperty("leftNoButton").objectReferenceValue = leftNoBtn.GetComponent<Button>();
        ctrlSo.FindProperty("rightYesButton").objectReferenceValue = rightYesBtn.GetComponent<Button>();
        ctrlSo.FindProperty("rightNoButton").objectReferenceValue = rightNoBtn.GetComponent<Button>();
        ctrlSo.FindProperty("leftStarCountText").objectReferenceValue = leftStarCount.GetComponent<Text>();
        ctrlSo.FindProperty("rightStarCountText").objectReferenceValue = rightStarCount.GetComponent<Text>();
        ctrlSo.FindProperty("leftMysteryBoxRoot").objectReferenceValue = leftBoxRoot.GetComponent<RectTransform>();
        ctrlSo.FindProperty("rightMysteryBoxRoot").objectReferenceValue = rightBoxRoot.GetComponent<RectTransform>();
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftMysteryBoxes", leftBoxes);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightMysteryBoxes", rightBoxes);
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log("[WhoIsItGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    /// <summary>3 hộp quà "?" cách nhau rõ ràng (gap giữa các hộp), có nơ trang trí. Màu random
    /// mỗi lần hiện — xem WhoIsItGameController.RandomizeBoxColors (đảm bảo 3 màu luôn khác nhau).
    /// Toạ độ tính theo % của "parent" (khung chứa nửa màn hình), không phải % toàn màn hình.</summary>
    static Button[] CreateMysteryBoxTrio(Transform parent)
    {
        const float boxWidth = 0.28f;
        const float gap = 0.06f;
        var boxes = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.02f + i * (boxWidth + gap);
            var box = MiniGameSceneBuilderHelpers.CreateSimpleButton(
                $"Box{i}", parent, "?", new Vector2(x0, 0.05f), new Vector2(x0 + boxWidth, 0.95f));
            box.GetComponentInChildren<Text>().fontSize = 40;
            MiniGameSceneBuilderHelpers.AddGiftRibbon(box);
            boxes[i] = box.GetComponent<Button>();
        }
        return boxes;
    }

    /// <summary>Sinh choose.csv từ mảng Words ở trên — chỉ tạo nếu CHƯA có file (giống ảnh
    /// placeholder). Nếu bạn tự sửa tay CSV sau này (thêm câu, đổi nội dung thật), Build Scene
    /// lần sau sẽ KHÔNG ghi đè. Muốn quay lại bản sinh từ code: xoá file rồi Build Scene lại.</summary>
    static void EnsureChooseCsv()
    {
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(ChooseCsvPath);
        if (File.Exists(absolutePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0,a1,a2,a3,a4,answerMode,correctAnswers");
        for (int i = 0; i < Words.Length; i++)
        {
            var w = Words[i];
            sb.AppendLine($"q{i + 1},Family,1,Image,{w.key},Text,{w.answers[0]},{w.answers[1]},{w.answers[2]},{w.answers[3]},,Single,0");
        }
        File.WriteAllText(absolutePath, sb.ToString());
    }

    /// <summary>Sprite tròn dùng cho khung "current reward" (chỉ 1 file, dùng chung, không phải
    /// per-từ-vựng như CreatePlaceholderCircle) — gọi CreatePlaceholderCircle với key/màu riêng.</summary>
    static void CreateRewardCircleSprite() => CreatePlaceholderCircle("reward_circle", new Color(1f, 0.75f, 0.25f));

    static void CreatePlaceholderCircle(string key, Color color)
    {
        string assetPath = $"{ImageFolder}/{key}.png";
        string absolutePath = MiniGameSceneBuilderHelpers.ToAbsolutePath(assetPath);
        if (File.Exists(absolutePath)) return;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        int cx = size / 2, cy = size / 2, r = size / 2 - 4;
        int rSq = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= rSq && x >= 0 && x < size && y >= 0 && y < size)
                    tex.SetPixel(x, y, color);
            }
        }
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
