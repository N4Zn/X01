using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dựng scene "WORD HUNT MAZE" — sinh từ MiniGameKit. Bảng chữ cái 7x7 mirror 2 bên trái/phải
/// (tránh 2 đội giẫm chân lên nhau khi cùng tìm 1 bảng thật), mỗi bảng giấu 3 từ Family (1 ngang, 1
/// dọc, 1 chéo) KHÔNG có gợi ý — học sinh tự nhận diện, dẫm xuôi hoặc ngược đều được. Không cần
/// CSV — danh sách từ cố định trong WordHuntMazeGameController. Chạy 1 lần
/// "Tools → WordHuntMazeGame → Build Scene" là chơi thử được ngay.
/// </summary>
public static class WordHuntMazeGameSceneBuilder
{
    const string SceneFolder = "Assets/Game/Scenes/WordHuntMazeGame";
    const string ScenePath = SceneFolder + "/WordHuntMazeGame.unity";
    const int GridSize = 7;

    [MenuItem("Tools/WordHuntMazeGame/Build Scene")]
    public static void BuildScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(SceneFolder);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniGameSceneBuilderHelpers.CreateMainCamera();
        var canvasGo = MiniGameSceneBuilderHelpers.CreateCanvasWithEventSystem();

        var root = new GameObject("WordHuntMazeGameRoot");
        var controller = root.AddComponent<WordHuntMazeGameController>();

        // ── HUD prefab mặc định của Kit ──────────────────────────────────────
        var hud = MiniGameSceneBuilderHelpers.InstantiateGameHudPrefab(canvasGo.transform);

        // ── Vạch chia giữa sàn — 2 vùng bảng chữ tách biệt, TO HẾT CỠ (không còn banner đề bài
        // chiếm chỗ phía trên nữa — không hiện gợi ý, dồn hết không gian cho bảng chữ) ─────────
        var leftZoneMin = new Vector2(0.015f, 0.02f);
        var leftZoneMax = new Vector2(0.485f, 0.83f);
        var rightZoneMin = new Vector2(0.515f, 0.02f);
        var rightZoneMax = new Vector2(0.985f, 0.83f);

        var divider = MiniGameSceneBuilderHelpers.CreateImage("Divider", canvasGo.transform, new Color(0.6f, 0.6f, 0.6f, 0.6f));
        MiniGameSceneBuilderHelpers.SetRect(divider.GetComponent<RectTransform>(),
            new Vector2(0.497f, 0.02f), new Vector2(0.503f, 0.83f), Vector2.zero, Vector2.zero);

        // ── 2 bảng chữ 7x7 (mirror nội dung, wiring click xảy ra ở runtime trong WordGridDisplay) ──
        var gridDisplayGo = new GameObject("WordGridDisplay", typeof(RectTransform));
        gridDisplayGo.transform.SetParent(canvasGo.transform, false);
        MiniGameSceneBuilderHelpers.SetRect((RectTransform)gridDisplayGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var gridDisplay = gridDisplayGo.AddComponent<WordGridDisplay>();

        var leftCells = new Button[GridSize * GridSize];
        var leftTexts = new Text[GridSize * GridSize];
        BuildGrid("LeftCell", gridDisplayGo.transform, leftZoneMin, leftZoneMax, leftCells, leftTexts);

        var rightCells = new Button[GridSize * GridSize];
        var rightTexts = new Text[GridSize * GridSize];
        BuildGrid("RightCell", gridDisplayGo.transform, rightZoneMin, rightZoneMax, rightCells, rightTexts);

        var gridSo = new SerializedObject(gridDisplay);
        gridSo.FindProperty("gridSize").intValue = GridSize;
        MiniGameSceneBuilderHelpers.AssignObjectArray(gridSo, "leftCells", leftCells);
        MiniGameSceneBuilderHelpers.AssignObjectArray(gridSo, "leftCellTexts", leftTexts);
        MiniGameSceneBuilderHelpers.AssignObjectArray(gridSo, "rightCells", rightCells);
        MiniGameSceneBuilderHelpers.AssignObjectArray(gridSo, "rightCellTexts", rightTexts);
        gridSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Đếm ngược "Next in Ns" mặc định của Kit — bảng chữ đã ẩn hẳn lúc này
        // (WordGridDisplay.Cleanup SetActive(false) cả GameObject) nên dùng chung đúng khung vùng chơi ──
        var leftCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "LeftCountdownText", canvasGo.transform, "", 34, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(leftCountdown.GetComponent<RectTransform>(),
            leftZoneMin, leftZoneMax, Vector2.zero, Vector2.zero);
        leftCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        leftCountdown.SetActive(false);

        var rightCountdown = MiniGameSceneBuilderHelpers.CreateText(
            "RightCountdownText", canvasGo.transform, "", 34, TextAnchor.MiddleCenter);
        MiniGameSceneBuilderHelpers.SetRect(rightCountdown.GetComponent<RectTransform>(),
            rightZoneMin, rightZoneMax, Vector2.zero, Vector2.zero);
        rightCountdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
        rightCountdown.SetActive(false);

        // ── Feedback mặc định của Kit — chỉ icon ĐÚNG (round xong cả 3 từ), NHỎ, góc trên mỗi vùng.
        // Game này không có tín hiệu SAI (dẫm trật không phải lỗi — không gợi ý, dò thử là bình
        // thường, xem WordGridDisplay) nên không dựng leftWrongIcon/rightWrongIcon — để trống,
        // Kit tự bỏ qua an toàn (null-check) nếu HandlePlayerFailed không bao giờ được gọi.
        var leftCorrectIcon = FeedbackIconBuilder.Create("LeftCorrectIcon", canvasGo.transform, true,
            new Vector2(0.40f, 0.75f), new Vector2(0.485f, 0.83f));
        var rightCorrectIcon = FeedbackIconBuilder.Create("RightCorrectIcon", canvasGo.transform, true,
            new Vector2(0.515f, 0.75f), new Vector2(0.60f, 0.83f));

        // Back button + tutorial
        var backButton = MiniGameSceneBuilderHelpers.CreateSimpleButton(
            "BackButton", canvasGo.transform, "Back", new Vector2(0.01f, 0.9f), new Vector2(0.1f, 0.99f));
        var tutorialPanel = MiniGameSceneBuilderHelpers.CreateTutorialPanel(canvasGo.transform, "Word Hunt Maze");

        // Wire controller
        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("hud").objectReferenceValue = hud;
        ctrlSo.FindProperty("tutorialPanel").objectReferenceValue = tutorialPanel;
        ctrlSo.FindProperty("tutorialText").stringValue = "Word Hunt Maze";
        ctrlSo.FindProperty("sceneNameForRegistry").stringValue = "WordHuntMazeGame";
        ctrlSo.FindProperty("backButton").objectReferenceValue = backButton.GetComponent<Button>();
        ctrlSo.FindProperty("gridDisplay").objectReferenceValue = gridDisplay;
        ctrlSo.FindProperty("leftCountdownText").objectReferenceValue = leftCountdown.GetComponent<Text>();
        ctrlSo.FindProperty("rightCountdownText").objectReferenceValue = rightCountdown.GetComponent<Text>();
        ctrlSo.FindProperty("leftCorrectIcon").objectReferenceValue = leftCorrectIcon;
        ctrlSo.FindProperty("rightCorrectIcon").objectReferenceValue = rightCorrectIcon;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ScenePath);

        Debug.Log("[WordHuntMazeGameSceneBuilder] Xong. Mở scene " + ScenePath + " và bấm Play để chơi thử.");
    }

    /// <summary>Dựng 1 lưới GridSize x GridSize ô vuông trong vùng zoneMin-zoneMax (fractional), điền
    /// vào 2 mảng song song cells/texts theo index = row*GridSize+col (row 0 = TRÊN CÙNG, khớp thứ
    /// tự flatten của WordGridGenerator). Có đệm nhỏ giữa các ô để nhìn rõ viền lưới.</summary>
    static void BuildGrid(string prefix, Transform parent, Vector2 zoneMin, Vector2 zoneMax, Button[] cells, Text[] texts)
    {
        const float pad = 0.003f;
        float cellW = (zoneMax.x - zoneMin.x) / GridSize;
        float cellH = (zoneMax.y - zoneMin.y) / GridSize;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int idx = row * GridSize + col;
                float x0 = zoneMin.x + col * cellW;
                float x1 = x0 + cellW;
                float y1 = zoneMax.y - row * cellH;
                float y0 = y1 - cellH;

                var cellGo = MiniGameSceneBuilderHelpers.CreateSimpleButton(
                    $"{prefix}{idx}", parent, "", new Vector2(x0 + pad, y0 + pad), new Vector2(x1 - pad, y1 - pad));
                cells[idx] = cellGo.GetComponent<Button>();
                texts[idx] = cellGo.GetComponentInChildren<Text>();
            }
        }
    }
}
