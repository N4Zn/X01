using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene ví dụ cho MiniGameKit — bấm chạy để xác nhận Kit hoạt động end-to-end
/// (Questions CSV → ButtonDisplay → AnswerValidator → ScoreManager/GameHUD → GameOver → ScoreScene).
///
/// Đây cũng là mẫu tham khảo khi Claude (/newminigame) sinh SceneBuilder cho 1 game thật:
/// copy file này, đổi tên, đổi phần hiện ý tưởng mới (display/handler riêng).
/// </summary>
public static class TemplateMiniGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/_MiniGameKitTemplate";
    const string ScenePath = SceneFolder + "/MiniGameKitTemplate.unity";
    const string ChooseCsvPath = "Assets/Game/Resources/MiniGameKit/_Template/choose.csv";

    [MenuItem("Tools/MiniGameKit/Build Template Scene")]
    public static void BuildTemplateScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("TemplateMiniGameRoot");
        var controller = root.AddComponent<TemplateMiniGameController>();
        var questionSource = root.AddComponent<MiniGameQuestionSource>();

        // Question text (giữa màn hình, phía trên)
        var questionText = MiniGameSceneBuilderHelpers.CreateText(
            "QuestionText", canvasGo.transform, "", 32, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(questionText.GetComponent<RectTransform>(),
            new Vector2(0.15f, 0.7f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);

        // HUD (điểm 2 đội + timer)
        // Mặc định dùng prefab HUD thật của bạn (MiniGameSceneBuilderHelpers.DefaultGameHudPrefabPath)
        // — tự fallback về placeholder (CreateGameHud) nếu không tìm thấy prefab.
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // Answer buttons — 4 nút mỗi bên (trái/phải, tối đa 5 câu trong CSV mẫu chỉ dùng 4 đáp án)
        var buttonDisplayGo = new GameObject("ButtonDisplay", typeof(RectTransform));
        buttonDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)buttonDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var buttonDisplay = buttonDisplayGo.AddComponent<ButtonDisplay>();

        var leftButtons = MiniGameSceneBuilderHelpers.CreateButtonGroup(
            "LeftBtn", buttonDisplayGo.transform, new Vector2(0.05f, 0.15f), new Vector2(0.45f, 0.65f), 4);
        var rightButtons = MiniGameSceneBuilderHelpers.CreateButtonGroup(
            "RightBtn", buttonDisplayGo.transform, new Vector2(0.55f, 0.15f), new Vector2(0.95f, 0.65f), 4);

        var buttonDisplaySo = new SerializedObject(buttonDisplay);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "leftButtons", leftButtons);
        MiniGameSceneBuilderHelpers.AssignObjectArray(buttonDisplaySo, "rightButtons", rightButtons);
        buttonDisplaySo.ApplyModifiedPropertiesWithoutUndo();

        // Back button
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));

        // Tutorial panel
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "MiniGameKit Template");

        // CSV
        var chooseCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(ChooseCsvPath);
        if (chooseCsv == null)
            Debug.LogWarning($"[TemplateMiniGameSceneBuilder] Không tìm thấy CSV tại {ChooseCsvPath}");

        var qsSo = new SerializedObject(questionSource);
        qsSo.FindProperty("chooseCsv").objectReferenceValue = chooseCsv;
        qsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire controller (các field protected [SerializeField] khai báo trong MiniGameControllerBase)
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("questionSource").objectReferenceValue = questionSource;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "MiniGameKit Template";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "MiniGameKitTemplate";
        ctrlSo.FindProperty("buttonDisplay").objectReferenceValue = buttonDisplay;
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("questionText").objectReferenceValue = questionText.GetComponent<Text>();
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);

        Debug.Log("[TemplateMiniGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }
}
