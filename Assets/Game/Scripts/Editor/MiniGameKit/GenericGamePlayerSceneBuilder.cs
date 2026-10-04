using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dựng 1 LẦN DUY NHẤT scene dùng chung cho MỌI game tạo bằng web tool kéo-thả — xem
/// GenericGameController.cs + SCHEMA.md cùng thư mục Core/MiniGameKit/GenericGame/. Sau khi chạy
/// menu này 1 lần, KHÔNG cần chạy lại khi có game mới (chỉ cần copy game.json + ảnh/âm thanh vào
/// Resources + thêm 1 dòng GameRegistry — xem doc comment đầu GenericGameController.cs).
///
/// Chỉ chạy lại nếu: cần đổi layout khung chung (vd thêm 1 vùng UI mới), hoặc prefab ButtonItem
/// dùng chung bị sửa (menu này tự ghi đè prefab cũ ở bước cuối).
/// </summary>
public static class GenericGamePlayerSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/_GenericGame";
    const string ScenePath = SceneFolder + "/GenericGamePlayer.unity";
    const string ButtonItemPrefabPath = "Assets/Game/Prefabs/GenericGame/GenericButtonItemPrefab.prefab";

    [MenuItem("Tools/GenericGame/Build Player Scene (one-time setup)")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Prefabs/GenericGame");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();
        var canvasTf = canvasGo.transform;

        var root = new GameObject("GenericGameController");
        var controller = root.AddComponent<GenericGameController>();

        // ── HUD / Tutorial / Back (giống hệt mọi game khác trong Kit — tái dùng nguyên khối) ──
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasTf);
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasTf, "Huong dan");
        var backButtonGo = MiniGameSceneBuilderHelpers.CreateSimpleButton("BackButton", canvasTf, "Back",
            new Vector2(0.01f, 0.90f), new Vector2(0.12f, 0.99f));
        backButtonGo.SetActive(false);

        // ── 2 nửa màn hình — chỉ là container trống, nội dung dựng lúc RUNTIME theo game.json ──
        // Thứ tự tạo con QUYẾT ĐỊNH thứ tự hiển thị (sibling sau đè lên sibling trước): Nền dưới
        // cùng → Nhân vật/Item → ButtonsRoot (đáp án) → câu hỏi/countdown ở trên cùng.
        var leftHalf = CreateHalf("LeftHalf", canvasTf, 0f, 0.5f);
        var rightHalf = CreateHalf("RightHalf", canvasTf, 0.5f, 1f);

        var leftBackground = MiniGameSceneBuilderHelpers.CreateImage("Background", leftHalf, Color.white).GetComponent<Image>();
        var rightBackground = MiniGameSceneBuilderHelpers.CreateImage("Background", rightHalf, Color.white).GetComponent<Image>();
        // CreateImage() KHÔNG tự full-stretch rect (vẫn giữ size mặc định của 1 RectTransform mới
        // — nhỏ, không phủ kín nửa màn hình) — phải ép tay, bug thật đã gặp (nền hiện bé tí giữa
        // màn hình thay vì phủ kín).
        StretchFull(leftBackground.rectTransform); StretchFull(rightBackground.rectTransform);
        leftBackground.raycastTarget = false; rightBackground.raycastTarget = false;
        leftBackground.gameObject.SetActive(false); rightBackground.gameObject.SetActive(false); // GenericGameController tự bật nếu game.json có background

        var leftDecoRoot = CreateFullRect("DecoRoot", leftHalf);
        var rightDecoRoot = CreateFullRect("DecoRoot", rightHalf);

        var leftButtonsRoot = CreateFullRect("ButtonsRoot", leftHalf);
        var rightButtonsRoot = CreateFullRect("ButtonsRoot", rightHalf);

        // ── "Spawn liên tục" (layout.spawnFlow) — parent RIÊNG, cùng lớp hiển thị với ButtonsRoot
        // (ngay sau nó trong sibling order) nhưng KHÔNG dùng chung — ButtonsRoot chứa slot cố định
        // tạo 1 lần lúc Start(), còn đây là nơi SpawnFlowDisplay Instantiate item liên tục theo
        // thời gian, 2 kiểu bố trí loại trừ nhau (xem GenericGameController.UsesSpawnFlow).
        var leftSpawnRoot = CreateFullRect("SpawnFlowRoot", leftHalf);
        var rightSpawnRoot = CreateFullRect("SpawnFlowRoot", rightHalf);

        var leftQuestionText = MiniGameSceneBuilderHelpers.CreateText("QuestionText", leftHalf, "", 32, TextAnchor.MiddleCenter)
            .GetComponent<Text>();
        var rightQuestionText = MiniGameSceneBuilderHelpers.CreateText("QuestionText", rightHalf, "", 32, TextAnchor.MiddleCenter)
            .GetComponent<Text>();
        var leftQuestionImage = MiniGameSceneBuilderHelpers.CreateImage("QuestionImage", leftHalf, Color.white)
            .GetComponent<Image>();
        var rightQuestionImage = MiniGameSceneBuilderHelpers.CreateImage("QuestionImage", rightHalf, Color.white)
            .GetComponent<Image>();
        StretchFull(leftQuestionImage.rectTransform); StretchFull(rightQuestionImage.rectTransform); // cùng bug full-stretch như Background ở trên
        leftQuestionImage.preserveAspect = true;
        rightQuestionImage.preserveAspect = true;
        // QUAN TRỌNG: CreateImage() tạo GameObject ở trạng thái ACTIVE mặc định — nếu không tự tắt,
        // ảnh câu hỏi (sprite trắng mặc định) hiện NGAY từ đầu như 1 ô trắng to đè lên màn hình cho
        // tới khi round đầu tiên gọi UpdateQuestionUi() (bug thật đã gặp sau khi full-stretch ở
        // trên làm lộ rõ việc GameObject này chưa bao giờ bị ẩn mặc định).
        leftQuestionImage.gameObject.SetActive(false);
        rightQuestionImage.gameObject.SetActive(false);

        var leftCountdownGo = MiniGameSceneBuilderHelpers.CreateText("CountdownText", leftHalf, "", 48, TextAnchor.MiddleCenter);
        var rightCountdownGo = MiniGameSceneBuilderHelpers.CreateText("CountdownText", rightHalf, "", 48, TextAnchor.MiddleCenter);
        leftCountdownGo.SetActive(false);
        rightCountdownGo.SetActive(false);

        // Mặc định BOLD cho mọi chữ trong game — rõ hơn hẳn chữ thường, theo yêu cầu.
        // Countdown text ("Start in 3,2,1"/"Next in 3,2,1") cũng bị cùng bug full-stretch — box mặc
        // định của CreateText quá hẹp nên chữ bị CẮT MẤT giữa chừng (chỉ còn thấy "Star"/"Nex") —
        // ép full-stretch + overflow:Overflow để không bao giờ bị cắt bất kể box to nhỏ thế nào.
        foreach (var t in new[] { leftQuestionText, rightQuestionText, leftCountdownGo.GetComponent<Text>(), rightCountdownGo.GetComponent<Text>() })
            t.fontStyle = FontStyle.Bold;
        // leftQuestionText/rightQuestionText dính CÙNG bug (box hẹp mặc định) — câu hỏi ngắn như
        // "3+2=?" chưa lộ ra, câu dài hơn sẽ bị cắt y hệt countdown — vá phòng trước luôn.
        foreach (var t in new[] { leftQuestionText, rightQuestionText, leftCountdownGo.GetComponent<Text>(), rightCountdownGo.GetComponent<Text>() })
        {
            StretchFull(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }
        // Countdown text riêng: chuỗi "Start in 3,2,1" ngắn, không cần xuống dòng — cho phép tràn
        // ngang thoải mái thay vì wrap (wrap dễ làm vỡ layout canh giữa với chuỗi ngắn 1 dòng).
        foreach (var go in new[] { leftCountdownGo, rightCountdownGo })
            go.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        // ── ButtonDisplay — leftButtons/rightButtons để TRỐNG, GenericGameController tự
        // ConfigureSlots() lúc runtime theo layout.slots của từng game ──
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasTf, false);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        // ── SpawnFlowDisplay — leftSpawnParent/rightSpawnParent để TRỐNG, GenericGameController tự
        // Configure() lúc runtime theo layout.spawnFlow (chỉ dùng khi spawnFlow.enabled=true, loại
        // trừ với ButtonDisplay — xem GenericGameController.UsesSpawnFlow) ──
        var spawnFlowGo = new GameObject("SpawnFlowDisplay", typeof(RectTransform));
        spawnFlowGo.transform.SetParent(canvasTf, false);
        var spawnFlowDisplay = spawnFlowGo.AddComponent<SpawnFlowDisplay>();

        // ── Icon ✔/✖ fallback mặc định (dùng chung FeedbackIconBuilder như mọi game khác trong Kit)
        // — đặt giữa mỗi nửa màn hình, phía trên vùng đáp án, không đụng HUD (trên cùng) hay
        // decoration/đáp án (thường chiếm nửa dưới màn hình). Ẩn mặc định, GenericGameController tự
        // SetActive khi cần qua ShowFeedbackIcon() (xem MiniGameControllerBase + PlayActionFx).
        var leftIconMin = new Vector2(0.15f, 0.30f);
        var leftIconMax = new Vector2(0.35f, 0.60f);
        var rightIconMin = new Vector2(0.65f, 0.30f);
        var rightIconMax = new Vector2(0.85f, 0.60f);
        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasTf, true, leftIconMin, leftIconMax);
        var leftWrongIcon = FeedbackIconBuilder.Create("LeftWrongIcon", canvasTf, false, leftIconMin, leftIconMax);
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasTf, true, rightIconMin, rightIconMax);
        var rightWrongIcon = FeedbackIconBuilder.Create("RightWrongIcon", canvasTf, false, rightIconMin, rightIconMax);

        var sfxGo = new GameObject("SfxAudioSource", typeof(AudioSource));
        sfxGo.transform.SetParent(root.transform, false);
        var sfxSource = sfxGo.GetComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // ── Prefab ButtonItem dùng chung — build tạm trong scene rồi lưu ra .prefab, xoá instance ──
        var tempParent = new GameObject("__TempPrefabBuild");
        var tempItemGo = MiniGameSceneBuilderHelpers.CreateButtonItem("GenericButtonItem", tempParent.transform,
            Vector2.zero, Vector2.one);
        // CreateButtonItem() đã tự SetActive(false) ở cuối (quy ước Kit-wide) — giữ nguyên khi lưu
        // prefab, ButtonDisplay.SetupGroup() sẽ tự bật/tắt đúng theo từng round như mọi game khác.
        var buttonItemPrefab = PrefabUtility.SaveAsPrefabAsset(tempItemGo, ButtonItemPrefabPath);
        Object.DestroyImmediate(tempParent);

        // ── Wiring riêng SpawnFlowDisplay (tái dùng ĐÚNG prefab ButtonItem vừa lưu ở trên) ──
        var sfSo = new SerializedObject(spawnFlowDisplay);
        sfSo.FindProperty("itemPrefab").objectReferenceValue = buttonItemPrefab.GetComponent<ButtonItem>();
        sfSo.FindProperty("leftSpawnParent").objectReferenceValue = leftSpawnRoot;
        sfSo.FindProperty("rightSpawnParent").objectReferenceValue = rightSpawnRoot;
        sfSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Wiring controller (SerializedObject — field private, không có setter public) ──
        var so = new SerializedObject(controller);
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        so.FindProperty("tutorialText").stringValue = "Huong dan";
        so.FindProperty("leftCountdownText").objectReferenceValue = leftCountdownGo.GetComponent<Text>();
        so.FindProperty("rightCountdownText").objectReferenceValue = rightCountdownGo.GetComponent<Text>();
        so.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        so.FindProperty("leftButtonsRoot").objectReferenceValue = leftButtonsRoot;
        so.FindProperty("rightButtonsRoot").objectReferenceValue = rightButtonsRoot;
        so.FindProperty("spawnFlowDisplay").objectReferenceValue = spawnFlowDisplay;
        so.FindProperty("buttonItemPrefab").objectReferenceValue = buttonItemPrefab.GetComponent<ButtonItem>();
        so.FindProperty("leftQuestionText").objectReferenceValue = leftQuestionText;
        so.FindProperty("rightQuestionText").objectReferenceValue = rightQuestionText;
        so.FindProperty("leftQuestionImage").objectReferenceValue = leftQuestionImage;
        so.FindProperty("rightQuestionImage").objectReferenceValue = rightQuestionImage;
        so.FindProperty("sfxAudioSource").objectReferenceValue = sfxSource;
        so.FindProperty("backButton").objectReferenceValue = backButtonGo.GetComponent<Button>();
        so.FindProperty("leftBackgroundImage").objectReferenceValue = leftBackground;
        so.FindProperty("rightBackgroundImage").objectReferenceValue = rightBackground;
        so.FindProperty("leftDecoRoot").objectReferenceValue = leftDecoRoot;
        so.FindProperty("rightDecoRoot").objectReferenceValue = rightDecoRoot;
        // leftCorrectIcon/leftWrongIcon/rightCorrectIcon/rightWrongIcon — field KẾ THỪA từ
        // MiniGameControllerBase (protected, không phải field riêng của GenericGameController) —
        // SerializedObject.FindProperty vẫn tìm được bình thường vì serialization không phân biệt
        // field khai báo ở class cha hay con, giống cách leftCountdownText/rightCountdownText ở
        // trên cũng là field kế thừa.
        so.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrectIcon;
        so.FindProperty("leftWrongIcon").objectReferenceValue = leftWrongIcon;
        so.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrectIcon;
        so.FindProperty("rightWrongIcon").objectReferenceValue = rightWrongIcon;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(scene, ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log($"[GenericGamePlayerSceneBuilder] Xong — scene: {ScenePath}, prefab: {ButtonItemPrefabPath}");
    }

    static RectTransform CreateHalf(string name, Transform parent, float xMin, float xMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform CreateFullRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
