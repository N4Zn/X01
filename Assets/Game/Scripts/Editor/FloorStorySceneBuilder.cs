using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene cho các game FloorStory — MỖI GAME 1 SCENE RIÊNG (tên scene = tên game, khớp GameRegistry.sceneName).
/// Cả 9 scene dùng chung khung: camera, canvas, nền, HUD, 2 vùng thế giới (nửa trái/phải), chữ đếm ngược, AudioSource
/// giọng đọc; nội dung từng game do <see cref="StoryWorld"/> tương ứng dựng bằng code lúc chạy
/// (field <c>gameName</c> trên FloorStoryController chọn world). Chạy lại builder sẽ GHI ĐÈ scene cùng tên.
/// </summary>
public static class FloorStorySceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/FloorStory";
    const float WorldTop = 0.88f;   // chừa thanh HUD phía trên (như TongHopToan)

    public static readonly string[] Games =
    {
        "NgayCuaBe", "HinhHoc", "CongHinh", "BeKhoeManh", "VoBongBay", "SanKyDieu", "CauBacQua", "DatDoVaoCho", "DongHoKhongLo",
        "TrungMauSac", "HinhDonGian", "NhoChuoiHinh", "LatTheNhoGiong", "NangNhe", "DemKhoiHop",
    };

    [MenuItem("Tools/FloorStoryGame/Build ALL Scenes")]
    public static void BuildAll()
    {
        foreach (var g in Games) BuildScene(g);
        Debug.Log($"[FloorStorySceneBuilder] Đã dựng {Games.Length} scene trong {SceneFolder}. Mở từng scene và bấm Play để thử.");
    }

    [MenuItem("Tools/FloorStoryGame/Build One/NgayCuaBe")]      static void B0() => BuildScene("NgayCuaBe");
    [MenuItem("Tools/FloorStoryGame/Build One/HinhHoc")]        static void B1() => BuildScene("HinhHoc");
    [MenuItem("Tools/FloorStoryGame/Build One/CongHinh")]       static void B2() => BuildScene("CongHinh");
    [MenuItem("Tools/FloorStoryGame/Build One/BeKhoeManh")]     static void B3() => BuildScene("BeKhoeManh");
    [MenuItem("Tools/FloorStoryGame/Build One/VoBongBay")]      static void B4() => BuildScene("VoBongBay");
    [MenuItem("Tools/FloorStoryGame/Build One/SanKyDieu")]      static void B5() => BuildScene("SanKyDieu");
    [MenuItem("Tools/FloorStoryGame/Build One/CauBacQua")]      static void B6() => BuildScene("CauBacQua");
    [MenuItem("Tools/FloorStoryGame/Build One/DatDoVaoCho")]    static void B7() => BuildScene("DatDoVaoCho");
    [MenuItem("Tools/FloorStoryGame/Build One/DongHoKhongLo")]  static void B8() => BuildScene("DongHoKhongLo");
    [MenuItem("Tools/FloorStoryGame/Build One/TrungMauSac")]    static void B9() => BuildScene("TrungMauSac");
    [MenuItem("Tools/FloorStoryGame/Build One/HinhDonGian")]    static void B10() => BuildScene("HinhDonGian");
    [MenuItem("Tools/FloorStoryGame/Build One/NhoChuoiHinh")]   static void B11() => BuildScene("NhoChuoiHinh");
    [MenuItem("Tools/FloorStoryGame/Build One/LatTheNhoGiong")] static void B12() => BuildScene("LatTheNhoGiong");
    [MenuItem("Tools/FloorStoryGame/Build One/NangNhe")]        static void B13() => BuildScene("NangNhe");
    [MenuItem("Tools/FloorStoryGame/Build One/DemKhoiHop")]     static void B14() => BuildScene("DemKhoiHop");

    public static void BuildScene(string game)
    {
        string scenePath = SceneFolder + "/" + game + ".unity";
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject(game + "Root");
        var controller = root.AddComponent<FloorStoryController>();

        var audioGo = new GameObject("VoiceSource", typeof(AudioSource));
        audioGo.transform.SetParent(root.transform, false);
        var audioSource = audioGo.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Nền nhạt (HUD.ApplyGameBackground có thể thay nếu có Resources/Background/<tên game>).
        var bg = MiniGameSceneBuilderHelpers.CreateImage("Background", canvasGo.transform, new Color(0.92f, 0.96f, 1f, 1f));
        MiniGameSceneBuilderHelpers.SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.GetComponent<Image>().raycastTarget = false;

        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        var leftWorld = CreateWorldRoot("LeftWorld", canvasGo.transform, 0.005f, 0.495f);
        var rightWorld = CreateWorldRoot("RightWorld", canvasGo.transform, 0.505f, 0.995f);

        var leftCountdown = CreateCountdown("LeftCountdownText", canvasGo.transform, new Vector2(0.02f, 0.35f), new Vector2(0.48f, 0.65f));
        var rightCountdown = CreateCountdown("RightCountdownText", canvasGo.transform, new Vector2(0.52f, 0.35f), new Vector2(0.98f, 0.65f));

        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        backButton.SetActive(false);

        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = game;
        ctrlSo.FindProperty("playMode").enumValueIndex = (int)MiniGamePlayMode.Independent;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdown;
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdown;
        ctrlSo.FindProperty("leftWorldRoot").objectReferenceValue = leftWorld;
        ctrlSo.FindProperty("rightWorldRoot").objectReferenceValue = rightWorld;
        ctrlSo.FindProperty("voiceSource").objectReferenceValue = audioSource;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("gameName").stringValue = game;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(scenePath);

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
        Debug.Log("[FloorStorySceneBuilder] Xong " + scenePath);
    }

    static RectTransform CreateWorldRoot(string name, Transform parent, float xMin, float xMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        MiniGameSceneBuilderHelpers.SetRect(rt, new Vector2(xMin, 0f), new Vector2(xMax, WorldTop), Vector2.zero, Vector2.zero);
        return rt;
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
