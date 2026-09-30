using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "Hái Quả" — xem HaiQuaController.cs cho cơ chế đầy đủ. Placeholder 100% màu phẳng
/// (chưa có sprite quả/nền thật) — thay bằng nghệ thuật thật sau khi cơ chế đã chạy đúng.
/// Mỗi bên có 5 rổ (khớp FruitCount = 5 loại quả cố định trong HaiQuaController.Fruits[]) — đổi
/// số loại quả bên đó thì PHẢI đổi cả FruitCount ở đây, không tự đồng bộ.
/// </summary>
public static class HaiQuaGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/HaiQuaGame";
    const string ScenePath = SceneFolder + "/HaiQuaGame.unity";
    const int FruitSlotsPerSide = 6; // khớp FruitDef.maxFruit trong HaiQuaController
    const int FruitCount = 5;        // khớp HaiQuaController.Fruits.Length
    const string BasketSpritePath = "Assets/Resources/Item/Bracket.png"; // ảnh giỏ thật (tên file "Bracket" nhưng nội dung là cái giỏ)

    [MenuItem("Tools/HaiQuaGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("HaiQuaRoot");
        var controller = root.AddComponent<HaiQuaController>();

        // Nền placeholder — nửa trái/phải, đổi màu theo loại quả của round hiện tại (xem
        // HaiQuaController.ApplySceneBackground). Đặt sibling index thấp nhất (vẽ dưới cùng).
        var leftBg = MiniGameSceneBuilderHelpers.CreateImage("LeftBackground", canvasGo.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(leftBg.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        leftBg.GetComponent<Image>().raycastTarget = false;

        var rightBg = MiniGameSceneBuilderHelpers.CreateImage("RightBackground", canvasGo.transform, Color.white);
        MiniGameSceneBuilderHelpers.SetRect(rightBg.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rightBg.GetComponent<Image>().raycastTarget = false;

        // HUD (điểm hoàn thành round + timer) — dùng prefab thật của bạn nếu có.
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ButtonDisplay — quả rải ngẫu nhiên trong vùng vườn mỗi bên (RandomizeFruitPositions tự
        // ghi đè vị trí, layout ngang ở đây chỉ quyết định KÍCH THƯỚC quả), FruitSlotsPerSide=6
        // chỗ mỗi bên (số quả thật mỗi round 3-6, dư thì tự ẩn — xem ButtonDisplay.SetupGroup).
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        // Button 100x100px (dễ bấm hơn placeholder cũ ~75px với 6 quả/hàng), xếp 2 hàng (3 trên/3
        // dưới) lúc dựng scene — chỉ có ý nghĩa lúc xem trong Editor, vào Play là
        // RandomizeFruitPositions random lại vị trí trong lưới vườn ngay (xem HaiQuaController),
        // area cao hơn bản cũ (0.08-0.42 thay vì 0.08-0.30) để 2 hàng không dính nhau.
        const float FruitButtonPx = 100f;
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
        // cuối, nên KHÔNG cần thêm code gì ở controller — bật lại đúng lúc round 1 sẵn sàng.
        buttonDisplayGo.SetActive(false);

        // 5 rổ mỗi bên — mỗi rổ có: text "+N" phía TRÊN (đếm dở trong round, rỗng lúc đầu), icon rổ
        // màu theo đúng loại quả (placeholder), text "0/20" trên THÂN rổ. Đặt y=[0.63,0.87] — dưới
        // hẳn thanh tên người chơi của GameHUD.prefab (P1/P2 box neo y=[0.88,0.99], xem
        // GameHUD.prefab) và trên vùng vườn (leftGardenMax.y=0.62) để không chồng lên cả 2. Basket/
        // round-text tự SetAsLastSibling() trong BuildBasketRow để LUÔN nổi trên HUD (kể cả nếu
        // HUD dùng Canvas/sibling order riêng), không bị che.
        Sprite basketSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasketSpritePath);
        var leftBaskets     = BuildBasketRow("Left",  canvasGo.transform, new Vector2(0.02f, 0.63f), new Vector2(0.48f, 0.87f), basketSprite);
        var rightBaskets    = BuildBasketRow("Right", canvasGo.transform, new Vector2(0.52f, 0.63f), new Vector2(0.98f, 0.87f), basketSprite);
        // Ẩn tới khi round 1 của mỗi bên sẵn sàng — xem HaiQuaController.SetupIndependentDisplay
        // (tránh hiện "0/20" trên nền chưa đổi màu/chưa đúng loại quả trước khi round 1 chạy).
        SetBasketRowActive(leftBaskets, false);
        SetBasketRowActive(rightBaskets, false);

        // "Hoàn thành!" — chữ to giữa mỗi nửa màn hình, ẩn mặc định, HaiQuaController tự bật khi
        // bên đó hái đủ chỉ tiêu cả 5 loại quả.
        var leftComplete = MiniGameSceneBuilderHelpers.CreateText("LeftCompleteText", canvasGo.transform, "Hoàn thành!", 48, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftComplete.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.35f), new Vector2(0.48f, 0.65f), Vector2.zero, Vector2.zero);
        leftComplete.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftComplete.GetComponent<Text>().color = new Color(0.15f, 0.55f, 0.20f);
        leftComplete.SetActive(false);

        var rightComplete = MiniGameSceneBuilderHelpers.CreateText("RightCompleteText", canvasGo.transform, "Hoàn thành!", 48, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightComplete.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.35f), new Vector2(0.98f, 0.65f), Vector2.zero, Vector2.zero);
        rightComplete.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightComplete.GetComponent<Text>().color = new Color(0.15f, 0.55f, 0.20f);
        rightComplete.SetActive(false);

        // Back button
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        backButton.SetActive(false); // ẩn nút back — vẫn wire bình thường, chỉ không hiện/không bấm được

        // KHÔNG dựng tutorial panel — tutorialPanel giữ null, MiniGameControllerBase tự bỏ qua
        // bước Tutorial và vào thẳng StartGame() (xem StateMachineEnter_Tutorial).

        // Wire controller — playMode PHẢI là Independent (2 bên tự nhịp riêng, xem yêu cầu gốc).
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "HaiQuaGame";
        ctrlSo.FindProperty("playMode").enumValueIndex = (int)MiniGamePlayMode.Independent;
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("leftBackground").objectReferenceValue = leftBg.GetComponent<Image>();
        ctrlSo.FindProperty("rightBackground").objectReferenceValue = rightBg.GetComponent<Image>();
        ctrlSo.FindProperty("leftCompleteText").objectReferenceValue = leftComplete.GetComponent<Text>();
        ctrlSo.FindProperty("rightCompleteText").objectReferenceValue = rightComplete.GetComponent<Text>();
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftFruitSlots",
            System.Array.ConvertAll(leftFruits, b => (Object)b));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightFruitSlots",
            System.Array.ConvertAll(rightFruits, b => (Object)b));
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftBasketIcons", leftBaskets.icons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightBasketIcons", rightBaskets.icons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftBasketTotalTexts", leftBaskets.totalTexts);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightBasketTotalTexts", rightBaskets.totalTexts);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "leftBasketRoundTexts", leftBaskets.roundTexts);
        MiniGameSceneBuilderHelpers.AssignObjectArray(ctrlSo, "rightBasketRoundTexts", rightBaskets.roundTexts);
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        Debug.Log("[HaiQuaGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    struct BasketRow
    {
        public Object[] icons;
        public Object[] totalTexts;
        public Object[] roundTexts;
    }

    /// <summary>Dựng FruitCount rổ nằm ngang trong vùng [areaMin, areaMax] (fractional 0-1 toàn
    /// Canvas) — mỗi rổ: ảnh giỏ thật (basketSprite, null thì fallback màu nâu placeholder —
    /// HaiQuaController tự đổi màu theo loại quả lúc runtime KHÔNG áp dụng cho rổ, chỉ áp dụng cho
    /// nền — rổ giữ NGUYÊN 1 ảnh cho MỌI loại để không cần biết trước thứ tự Fruits[] lúc dựng
    /// scene), text tổng "0/20" trên thân rổ, text "+N" rỗng phía trên rổ.</summary>
    static BasketRow BuildBasketRow(string sidePrefix, Transform canvasParent, Vector2 areaMin, Vector2 areaMax, Sprite basketSprite)
    {
        var icons = new Object[FruitCount];
        var totalTexts = new Object[FruitCount];
        var roundTexts = new Object[FruitCount];

        float cellW = (areaMax.x - areaMin.x) / FruitCount;
        float gap = cellW * 0.08f;

        for (int i = 0; i < FruitCount; i++)
        {
            float cellMinX = areaMin.x + i * cellW + gap * 0.5f;
            float cellMaxX = areaMin.x + (i + 1) * cellW - gap * 0.5f;

            // Rổ — 2/3 dưới của ô
            float basketMinY = areaMin.y;
            float basketMaxY = areaMin.y + (areaMax.y - areaMin.y) * 0.62f;
            var basket = MiniGameSceneBuilderHelpers.CreateImage($"{sidePrefix}Basket{i}", canvasParent, new Color(0.55f, 0.35f, 0.15f));
            MiniGameSceneBuilderHelpers.SetRect(basket.GetComponent<RectTransform>(),
                new Vector2(cellMinX, basketMinY), new Vector2(cellMaxX, basketMaxY), Vector2.zero, Vector2.zero);
            var basketImg = basket.GetComponent<Image>();
            basketImg.raycastTarget = false;
            if (basketSprite != null) { basketImg.sprite = basketSprite; basketImg.color = Color.white; basketImg.preserveAspect = true; }

            var totalText = MiniGameSceneBuilderHelpers.CreateText($"{sidePrefix}BasketTotal{i}", basket.transform, $"0/20", 16, TextAnchor.MiddleCenter);
            MiniGameSceneBuilderHelpers.SetRect(totalText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            totalText.GetComponent<Text>().color = Color.white;
            totalText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // "+N" — 1/3 trên của ô, phía TRÊN rổ, rỗng lúc đầu
            float roundMinY = basketMaxY;
            float roundMaxY = areaMax.y;
            var roundText = MiniGameSceneBuilderHelpers.CreateText($"{sidePrefix}BasketRound{i}", canvasParent, "", 18, TextAnchor.MiddleCenter);
            MiniGameSceneBuilderHelpers.SetRect(roundText.GetComponent<RectTransform>(),
                new Vector2(cellMinX, roundMinY), new Vector2(cellMaxX, roundMaxY), Vector2.zero, Vector2.zero);
            roundText.GetComponent<Text>().color = new Color(1f, 0.85f, 0.2f);
            roundText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Đẩy lên cuối danh sách con của Canvas — LUÔN vẽ đè lên HUD/background dù chúng được
            // dựng trước hay sau (tránh bị thanh tên người chơi của HUD che mất, đã xảy ra thật).
            basket.transform.SetAsLastSibling();
            roundText.transform.SetAsLastSibling();

            icons[i] = basket.GetComponent<Image>();
            totalTexts[i] = totalText.GetComponent<Text>();
            roundTexts[i] = roundText.GetComponent<Text>();
        }

        return new BasketRow { icons = icons, totalTexts = totalTexts, roundTexts = roundTexts };
    }

    /// <summary>Bật/tắt cả 5 icon rổ (kéo theo totalText — con của icon) + 5 roundText (sibling
    /// riêng) cùng lúc. active=false lúc dựng scene để tránh hiện "0/20" trên rổ/nền còn placeholder
    /// trước khi round 1 chạy xong (xem HaiQuaController.SetupIndependentDisplay).</summary>
    static void SetBasketRowActive(BasketRow row, bool active)
    {
        foreach (var icon in row.icons) ((Image)icon).gameObject.SetActive(active);
        foreach (var roundText in row.roundTexts) ((Text)roundText).gameObject.SetActive(active);
    }
}
