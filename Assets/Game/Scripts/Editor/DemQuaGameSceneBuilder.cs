using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "Đếm Quả" — bản đơn giản hơn HaiQuaGame (xem DemQuaController.cs). Placeholder
/// 100% màu phẳng (chưa có sprite quả/nền/âm thanh thật) — thay bằng nghệ thuật/âm thanh thật sau
/// khi cơ chế đã chạy đúng.
/// </summary>
public static class DemQuaGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/DemQuaGame";
    const string ScenePath = SceneFolder + "/DemQuaGame.unity";
    const int FruitSlotsPerSide = 5; // khớp DemQuaController.MaxFruit (1-5, không phải 3-6 như HaiQua)
    const string BasketSpritePath = "Assets/Resources/Item/Bracket.png"; // ảnh giỏ thật (tên file "Bracket" nhưng nội dung là cái giỏ)
    const string BasketFrontSpritePath = "Assets/Resources/Item/Bracket_front.png"; // vành/thành trước của giỏ — vẽ ĐÈ LÊN quả để tạo cảm giác quả nằm TRONG giỏ

    [MenuItem("Tools/DemQuaGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("DemQuaRoot");
        var controller = root.AddComponent<DemQuaController>();

        // AudioSource riêng mỗi bên (2 bên có thể hoàn thành round cùng lúc — xem
        // DemQuaController.AnnounceCount). playOnAwake=false, controller tự gọi Play().
        var leftAudioGo = new GameObject("LeftAudioSource", typeof(AudioSource));
        leftAudioGo.transform.SetParent(root.transform, false);
        var leftAudioSource = leftAudioGo.GetComponent<AudioSource>();
        leftAudioSource.playOnAwake = false;

        var rightAudioGo = new GameObject("RightAudioSource", typeof(AudioSource));
        rightAudioGo.transform.SetParent(root.transform, false);
        var rightAudioSource = rightAudioGo.GetComponent<AudioSource>();
        rightAudioSource.playOnAwake = false;

        // Nền — DemQuaController.ApplyBackground() sẽ tự set đúng ảnh/màu theo loại quả mỗi round,
        // NHƯNG chỉ chạy khi round 1 thật sự bắt đầu — có thể trễ vài khung hình sau khi scene load
        // (PlayerRecognitionService, FSM...), khiến người chơi thấy 1 khoảng TRẮNG TRƠN xấu trước
        // đó (đã xảy ra thật). Set SẴN ảnh nền carrot (loại quả duy nhất đang test — xem
        // DemQuaController.TestOnlyFruitId) NGAY lúc dựng scene để không bao giờ trắng trơn, dù
        // ApplyBackground() vẫn ghi đè lại y hệt giá trị này ngay khi round 1 chạy (không xung đột).
        Sprite carrotBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Background/Carrot_BG.png");
        var leftBg = MiniGameSceneBuilderHelpers.CreateImage("LeftBackground", canvasGo.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(leftBg.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var leftBgImg = leftBg.GetComponent<Image>();
        leftBgImg.raycastTarget = false;
        if (carrotBgSprite != null) { leftBgImg.sprite = carrotBgSprite; leftBgImg.color = Color.white; }

        var rightBg = MiniGameSceneBuilderHelpers.CreateImage("RightBackground", canvasGo.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(rightBg.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var rightBgImg = rightBg.GetComponent<Image>();
        rightBgImg.raycastTarget = false;
        if (carrotBgSprite != null)
        {
            rightBgImg.sprite = carrotBgSprite;
            rightBgImg.color = Color.white;
            rightBg.GetComponent<RectTransform>().localScale = new Vector3(-1f, 1f, 1f); // lật ngang — khớp flipHorizontal của ApplyBackground()
        }

        // HUD (điểm + timer) — dùng prefab thật của bạn nếu có.
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ButtonDisplay — quả rải ngẫu nhiên trong vùng vườn mỗi bên (RandomizeFruitPositions tự
        // ghi đè vị trí), FruitSlotsPerSide=5 chỗ mỗi bên (số quả thật mỗi round 1-5).
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        // Button 100x100px (dễ bấm hơn placeholder cũ ~90px), xếp 2 hàng (3 trên/2 dưới) lúc dựng
        // scene — chỉ có ý nghĩa lúc xem trong Editor, vào Play là RandomizeFruitPositions random
        // lại vị trí trong lưới vườn ngay (xem DemQuaController), area cao hơn bản cũ (0.08-0.42
        // thay vì 0.08-0.30) để 2 hàng không dính nhau.
        const float FruitButtonPx = 150f; // x1.5 bản cũ (100px)
        var leftFruits = MiniGameSceneBuilderHelpers.CreateButtonGridTwoRows(
            "LeftFruit", buttonDisplayGo.transform, new Vector2(0.03f, 0.08f), new Vector2(0.47f, 0.42f), FruitSlotsPerSide, FruitButtonPx);
        var rightFruits = MiniGameSceneBuilderHelpers.CreateButtonGridTwoRows(
            "RightFruit", buttonDisplayGo.transform, new Vector2(0.53f, 0.08f), new Vector2(0.97f, 0.42f), FruitSlotsPerSide, FruitButtonPx);
        // Ảnh quả PNG nền trong suốt thật — xoá màu nền "thẻ bài" trắng mặc định của ButtonItem để
        // không lộ khung trắng phía sau phần trong suốt (xem MakeButtonGroupBackgroundTransparent).
        MiniGameSceneBuilderHelpers.MakeButtonGroupBackgroundTransparent(leftFruits);
        MiniGameSceneBuilderHelpers.MakeButtonGroupBackgroundTransparent(rightFruits);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftFruits);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightFruits);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // Ẩn NGAY từ đầu — trước khi round 1 chạy, các quả này vẫn còn màu placeholder xanh mặc
        // định của CreateButtonItem (colorNormal chỉ áp dụng khi Setup() chạy lần đầu), hiện ra sẽ
        // rất xấu. buttonDisplay.SetupPlayerIndependent() đã tự gọi gameObject.SetActive(true) ở
        // cuối, nên KHÔNG cần thêm code gì ở controller cho việc BẬT LẠI container này.
        buttonDisplayGo.SetActive(false);
        // NHƯNG container là DÙNG CHUNG cho cả 2 bên — giờ mỗi bên tự đếm ngược "Start/Next in Ns"
        // RIÊNG (xem DemQuaController.CountdownThenRevealFruits), nên bên nào đếm xong TRƯỚC sẽ tự
        // bật container này lên, LỘ LUÔN quả của bên KIA nếu bên kia chưa từng qua SetupGroup lần
        // nào (vẫn đang mặc định active + màu xanh placeholder). Ẩn THÊM từng quả riêng lẻ ở đây —
        // ButtonDisplay.SetupGroup() sẽ tự bật lại ĐÚNG quả cần thiết của TỪNG bên khi bên đó thật
        // sự tới lượt, không phụ thuộc trạng thái ẩn ban đầu này.
        foreach (var b in leftFruits) b.gameObject.SetActive(false);
        foreach (var b in rightFruits) b.gameObject.SetActive(false);

        // 1 rổ mỗi bên — hiện text tổng số quả đã hái (cộng dồn cả ván, xem
        // DemQuaController.AddToTotal). Kích thước x1.5 bản cũ (cũ: 0.13x0.17 → mới: 0.195x0.255)
        // và dịch sát mép ngoài màn hình (trái sát mép trái, phải sát mép phải) theo yêu cầu. Vẫn
        // giữ y=[0.62,0.875] — dưới hẳn thanh tên người chơi của GameHUD.prefab (P1/P2 box neo
        // y=[0.88,0.99]) và sát trên vùng vườn (leftGardenMax.y=0.62), chỉ vừa đủ khít với chiều
        // cao mới (0.255) trong đúng khoảng trống đó. SetAsLastSibling() để luôn nổi trên HUD.
        // Text tổng số hạ xuống 1/3 dưới của rổ (trước đó full rổ) — anchorMax.y=0.333 thay vì 1.
        Sprite basketSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasketSpritePath);
        Sprite basketFrontSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasketFrontSpritePath);
        // Top offset +75 (Inspector "Top" hiển thị -75, xem RectTransform.offsetMax.y = -Top) —
        // theo tinh chỉnh thật của user, kéo mép trên rổ lên cao hơn 75px so với anchor gốc.
        var leftBasket = MiniGameSceneBuilderHelpers.CreateImage("LeftBasketIcon", canvasGo.transform, new Color(0.55f, 0.35f, 0.15f));
        MiniGameSceneBuilderHelpers.SetRect(leftBasket.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0.62f), new Vector2(0.205f, 0.875f), Vector2.zero, new Vector2(0f, 75f));
        var leftBasketImg = leftBasket.GetComponent<Image>();
        leftBasketImg.raycastTarget = false;
        if (basketSprite != null) { leftBasketImg.sprite = basketSprite; leftBasketImg.color = Color.white; leftBasketImg.preserveAspect = true; }
        // Quả trong giỏ — 5 slot Image ẩn sẵn (con của leftBasket), DemQuaController tự hiện dần
        // theo thứ tự khi hái được (xem RevealBasketFruitSlot). Tạo TRƯỚC overlay vành giỏ để vành
        // vẽ ĐÈ LÊN quả (đúng thứ tự lớp: nền giỏ → quả → vành trước → số đếm).
        var leftBasketFruitSlots = CreateBasketFruitSlots(leftBasket.transform, "LeftBasketFruit");
        AddBasketFrontOverlay(leftBasket, basketFrontSprite);
        var leftBasketCount = MiniGameSceneBuilderHelpers.CreateText("LeftBasketCount", leftBasket.transform, "0", 48, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftBasketCount.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.333f), Vector2.zero, Vector2.zero);
        var leftBasketCountText = leftBasketCount.GetComponent<Text>();
        leftBasketCountText.color = Color.white;
        leftBasketCountText.fontStyle = FontStyle.Bold;
        leftBasketCountText.resizeTextForBestFit = true; // Best Fit — tự co chữ vừa khung 1/3 dưới của rổ, tránh tràn/vỡ chữ
        leftBasketCountText.resizeTextMinSize = 10;
        leftBasketCountText.resizeTextMaxSize = 48;
        leftBasket.transform.SetAsLastSibling();
        leftBasket.SetActive(false); // ẩn tới khi round 1 của bên Trái sẵn sàng — xem DemQuaController.SetupIndependentDisplay

        var rightBasket = MiniGameSceneBuilderHelpers.CreateImage("RightBasketIcon", canvasGo.transform, new Color(0.55f, 0.35f, 0.15f));
        MiniGameSceneBuilderHelpers.SetRect(rightBasket.GetComponent<RectTransform>(),
            new Vector2(0.795f, 0.62f), new Vector2(0.99f, 0.875f), Vector2.zero, new Vector2(0f, 75f));
        var rightBasketImg = rightBasket.GetComponent<Image>();
        rightBasketImg.raycastTarget = false;
        if (basketSprite != null) { rightBasketImg.sprite = basketSprite; rightBasketImg.color = Color.white; rightBasketImg.preserveAspect = true; }
        var rightBasketFruitSlots = CreateBasketFruitSlots(rightBasket.transform, "RightBasketFruit");
        AddBasketFrontOverlay(rightBasket, basketFrontSprite);
        var rightBasketCount = MiniGameSceneBuilderHelpers.CreateText("RightBasketCount", rightBasket.transform, "0", 48, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightBasketCount.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.333f), Vector2.zero, Vector2.zero);
        var rightBasketCountText = rightBasketCount.GetComponent<Text>();
        rightBasketCountText.color = Color.white;
        rightBasketCountText.fontStyle = FontStyle.Bold;
        rightBasketCountText.resizeTextForBestFit = true; // Best Fit — tự co chữ vừa khung 1/3 dưới của rổ, tránh tràn/vỡ chữ
        rightBasketCountText.resizeTextMinSize = 10;
        rightBasketCountText.resizeTextMaxSize = 48;
        rightBasket.transform.SetAsLastSibling();
        rightBasket.SetActive(false); // ẩn tới khi round 1 của bên Phải sẵn sàng — xem DemQuaController.SetupIndependentDisplay

        // "Start in 3/2/1" — che tạm khoảng chờ đầu game (round 1 setup + độ trễ nếu có) bằng 1
        // đếm ngược có chủ đích thay vì để lộ trạng thái placeholder/trắng trơn. Nổi trên MỌI thứ
        // khác (SetAsLastSibling) — DemQuaController tự đếm 3→2→1 rồi ẩn đi.
        var leftCountdown = MiniGameSceneBuilderHelpers.CreateText("LeftCountdownText", canvasGo.transform, "Start in 3", 56, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftCountdown.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.35f), new Vector2(0.48f, 0.65f), Vector2.zero, Vector2.zero);
        var leftCountdownText = leftCountdown.GetComponent<Text>();
        leftCountdownText.color = Color.white;
        leftCountdownText.fontStyle = FontStyle.Bold;
        var leftCountdownOutline = leftCountdown.AddComponent<Outline>(); // viền đen — chữ trắng vẫn đọc được trên nền ảnh sáng màu
        leftCountdownOutline.effectColor = Color.black;
        leftCountdownOutline.effectDistance = new Vector2(2f, -2f);
        leftCountdown.transform.SetAsLastSibling();

        var rightCountdown = MiniGameSceneBuilderHelpers.CreateText("RightCountdownText", canvasGo.transform, "Start in 3", 56, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightCountdown.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.35f), new Vector2(0.98f, 0.65f), Vector2.zero, Vector2.zero);
        var rightCountdownText = rightCountdown.GetComponent<Text>();
        rightCountdownText.color = Color.white;
        rightCountdownText.fontStyle = FontStyle.Bold;
        var rightCountdownOutline = rightCountdown.AddComponent<Outline>();
        rightCountdownOutline.effectColor = Color.black;
        rightCountdownOutline.effectDistance = new Vector2(2f, -2f);
        rightCountdown.transform.SetAsLastSibling();

        // Back button
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        backButton.SetActive(false); // ẩn nút back — vẫn wire bình thường, chỉ không hiện/không bấm được

        // KHÔNG dựng tutorial panel — tutorialPanel giữ null, MiniGameControllerBase tự bỏ qua
        // bước Tutorial và vào thẳng StartGame() (xem StateMachineEnter_Tutorial).

        // Wire controller — playMode PHẢI là Independent (2 bên tự nhịp riêng).
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "DemQuaGame";
        ctrlSo.FindProperty("playMode").enumValueIndex = (int)MiniGamePlayMode.Independent;
        // Khoảng chờ trước round mới (kế thừa từ MiniGameControllerBase, mặc định 1.2s) — nới lên
        // 1.8s để đủ chỗ cho AnnounceCount() phát xong audio "số + tên quả" (vd "ba quả táo")
        // trước khi round sau xoá số trên rổ về 0. Vẫn nằm trong khoảng "~1-2s" user yêu cầu.
        ctrlSo.FindProperty("feedbackDelayCorrect").floatValue = 1.8f;
        // "Next in 3,2,1" riêng từng bên từ round 2 trở đi — tính năng dùng chung mới thêm vào
        // MiniGameControllerBase (xem IndependentRoundCountdown), thay cho coroutine tự viết cũ.
        ctrlSo.FindProperty("useIndependentRoundCountdown").boolValue = true;
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("leftBackground").objectReferenceValue = leftBg.GetComponent<Image>();
        ctrlSo.FindProperty("rightBackground").objectReferenceValue = rightBg.GetComponent<Image>();
        ctrlSo.FindProperty("leftBasketIcon").objectReferenceValue = leftBasket.GetComponent<RectTransform>();
        ctrlSo.FindProperty("rightBasketIcon").objectReferenceValue = rightBasket.GetComponent<RectTransform>();
        ctrlSo.FindProperty("leftBasketCountText").objectReferenceValue = leftBasketCount.GetComponent<Text>();
        ctrlSo.FindProperty("rightBasketCountText").objectReferenceValue = rightBasketCount.GetComponent<Text>();
        ctrlSo.FindProperty("leftAudioSource").objectReferenceValue = leftAudioSource;
        ctrlSo.FindProperty("rightAudioSource").objectReferenceValue = rightAudioSource;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdownText;
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdownText;
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftFruitSlots",
            System.Array.ConvertAll(leftFruits, b => (Object)b));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightFruitSlots",
            System.Array.ConvertAll(rightFruits, b => (Object)b));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftBasketFruitSlots",
            System.Array.ConvertAll(leftBasketFruitSlots, i => (Object)i));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightBasketFruitSlots",
            System.Array.ConvertAll(rightBasketFruitSlots, i => (Object)i));
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // Áp layout 5 quả trong giỏ NGAY (không chờ OnValidate tự chạy) — gọi TRỰC TIẾP (method đã
        // đổi thành public), KHÔNG dùng SendMessage: SendMessage kiểm tra ShouldRunBehaviour() nội
        // bộ, luôn false ngoài Play mode → assert lỗi thật (Editor script chạy ở Edit mode).
        controller.ApplyBasketFruitLayout();

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        Debug.Log("[DemQuaGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    /// <summary>5 slot Image RỖNG (chưa có sprite), ẨN SẴN — con của basketTransform (giỏ). Vị trí
    /// KHÔNG tính ở đây nữa — DemQuaController.ApplyBasketFruitLayout() tự xếp pyramid (3 hàng
    /// sau + 2 hàng trước) NGAY khi component load (OnValidate + Start), dùng field Inspector
    /// (basketFruitSize/RowDistance/ColumnDistance/CenterOffsetY) — chỉnh trực tiếp bên đó, không
    /// cần Build Scene lại. Rect khởi tạo ở đây chỉ là placeholder tạm, bị ghi đè ngay lập tức.</summary>
    static Image[] CreateBasketFruitSlots(Transform basketTransform, string prefix)
    {
        const int count = 5;
        var slots = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = MiniGameSceneBuilderHelpers.CreateImage($"{prefix}{i}", basketTransform, Color.white);
            MiniGameSceneBuilderHelpers.SetRect(go.GetComponent<RectTransform>(),
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Vector2.zero, Vector2.zero); // placeholder — DemQuaController ghi đè ngay
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false); // DemQuaController bật dần theo số quả đã hái — xem RevealBasketFruitSlot
            slots[i] = img;
        }
        return slots;
    }

    /// <summary>Vành/thành trước của giỏ — vẽ ĐÈ LÊN quả (tạo sau trong sibling order), CĂN ĐÁY
    /// (align bottom) với giỏ thay vì kéo giãn phủ kín — giữ đúng tỷ lệ ảnh gốc theo chiều rộng
    /// giỏ rồi neo xuống đáy, phần dư phía trên để trống (trong suốt) cho củ quả hàng sau ló ra tự
    /// nhiên phía trên vành.</summary>
    static void AddBasketFrontOverlay(GameObject basket, Sprite frontSprite)
    {
        if (frontSprite == null) return;

        var basketRect = basket.GetComponent<RectTransform>();
        float basketWidthFrac  = basketRect.anchorMax.x - basketRect.anchorMin.x;
        float basketHeightFrac = basketRect.anchorMax.y - basketRect.anchorMin.y;
        float basketPxW = basketWidthFrac  * 1024f; // reference resolution — xem CreateCanvasWithEventSystem
        float basketPxH = basketHeightFrac * 600f;

        float spriteAspect = frontSprite.rect.width / frontSprite.rect.height;
        float frontPxHAtBasketWidth = basketPxW / spriteAspect;
        float heightFraction = Mathf.Clamp01(frontPxHAtBasketWidth / basketPxH);

        // Top offset -20 (Inspector "Top" hiển thị 20, xem RectTransform.offsetMax.y = -Top) — theo
        // tinh chỉnh thật của user, kéo mép trên vành xuống thấp hơn 20px so với anchor gốc.
        var go = MiniGameSceneBuilderHelpers.CreateImage("BasketFront", basket.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(go.GetComponent<RectTransform>(),
            Vector2.zero, new Vector2(1f, heightFraction), Vector2.zero, new Vector2(0f, -20f)); // căn đáy — anchorMin.y=0
        var img = go.GetComponent<Image>();
        img.sprite = frontSprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
    }
}
