using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Word Hunt Maze" — bảng chữ cái NxN cho MỖI bên (trái/phải) — CÙNG 3 từ mục tiêu nhưng sinh
/// ĐỘC LẬP (chữ điền ngẫu nhiên và vị trí đặt từ khác nhau giữa 2 bên, không phải bản mirror y hệt
/// nhau) — tránh 2 đội giẫm chân lên nhau khi cùng tìm 1 bảng thật, đồng thời không bên nào có lợi
/// thế thấy trước layout của bên kia. KHÔNG có gợi ý cho biết từ gì — học sinh tự nhận diện mặt chữ.
///
/// Hoàn thành 1 từ = đã dẫm qua HẾT các ô thuộc từ đó — theo BẤT KỲ THỨ TỰ nào (không bắt buộc
/// liên tục/đúng trình tự xuôi-ngược). Học sinh dò tìm tự do trên cả bảng, không có gợi ý nên việc
/// dẫm không theo trình tự là bình thường — kiểm tra theo THỨ TỰ sẽ dễ bị "kẹt" (đã dẫm đủ hết chữ
/// của từ nhưng không theo đúng chuỗi liên tục thì không bao giờ được công nhận).
///
/// MỌI lần dẫm đều đổi màu ô đó (màu trung tính) — KHÔNG có tín hiệu đúng/sai ngay lúc dẫm. Chỉ khi
/// 1 từ có ĐỦ TẤT CẢ ô đã được dẫm mới tô cả từ đó thành XANH và phát âm thanh xác nhận.
///
/// Bên nào hoàn thành CẢ 3 từ trước → thắng round, khoá NGAY cả 2 bên. onResult trả về
/// (true, winner, [loserWordCount]) — loserWordCount = số từ bên thua đã hoàn thành tại thời điểm
/// đó, để WordHuntMazeGameController tự tính điểm (thắng 3+2=5, thua = loserWordCount).
/// </summary>
public class WordGridDisplay : MonoBehaviour, IAnswerDisplay
{
    const int WordCount = 3;

    [SerializeField] int gridSize = 7;
    [Tooltip("Size*size phần tử, index = row*size+col. Text tương ứng lấy qua GetComponentInChildren khi wiring.")]
    [SerializeField] Button[] leftCells;
    [SerializeField] Text[] leftCellTexts;
    [SerializeField] Button[] rightCells;
    [SerializeField] Text[] rightCellTexts;

    [Header("Màu ô")]
    [SerializeField] Color normalBg        = Color.white;
    [SerializeField] Color tappedBg        = Color.black;
    [SerializeField] Color foundBg         = new Color(0.18f, 0.92f, 0.38f, 1f);
    [SerializeField] Color normalText      = Color.black;
    [SerializeField] Color tappedText      = Color.white;
    [SerializeField] Color foundText       = Color.black;
    Action<bool, Team, int[]> _onResult;
    WordGridGenerator.Grid _leftGrid;
    WordGridGenerator.Grid _rightGrid;
    readonly HashSet<int> _leftVisited = new();
    readonly HashSet<int> _rightVisited = new();
    readonly bool[] _leftWordDone = new bool[WordCount];
    readonly bool[] _rightWordDone = new bool[WordCount];
    bool _roundOver;

    void Awake()
    {
        WireCells(leftCells, Team.Left);
        WireCells(rightCells, Team.Right);
    }

    // Wire lúc runtime (Awake), KHÔNG lúc dựng scene — Button.onClick.AddListener() thêm bằng code
    // là listener non-persistent, không được lưu vào file scene nếu gắn ở Editor script.
    void WireCells(Button[] cells, Team team)
    {
        if (cells == null) return;
        for (int i = 0; i < cells.Length; i++)
        {
            int idx = i;
            if (cells[i] != null)
            {
                cells[i].transition = Selectable.Transition.None;
                cells[i].onClick.AddListener(() => OnCellClicked(team, idx));
            }
        }
    }

    /// <summary>q.id = 3 từ nối bằng dấu phẩy, vd "MOM,SISTER,GRANDPA" — xem
    /// WordHuntMazeGameController.PullNextQuestion. onPlayerFailed không dùng ở game này — dẫm
    /// "trật" ô không được coi là lỗi (không có gợi ý, dò thử là bình thường).</summary>
    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _onResult = onResult;
        _roundOver = false;
        _leftVisited.Clear();
        _rightVisited.Clear();
        Array.Clear(_leftWordDone, 0, WordCount);
        Array.Clear(_rightWordDone, 0, WordCount);

        var words = (q.id ?? "").Split(',');
        // 2 bảng sinh ĐỘC LẬP (cùng từ, khác layout/chữ điền) — không phải cùng 1 Grid dùng chung.
        _leftGrid = WordGridGenerator.Generate(words, gridSize);
        _rightGrid = WordGridGenerator.Generate(words, gridSize);
        ApplyGrid(leftCells, leftCellTexts, _leftGrid);
        ApplyGrid(rightCells, rightCellTexts, _rightGrid);
    }

    void ApplyGrid(Button[] cells, Text[] texts, WordGridGenerator.Grid grid)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].text = grid.letters[i].ToString();
                texts[i].fontStyle = FontStyle.Bold;
                texts[i].color = normalText;
            }
            var img = cells[i] != null ? cells[i].GetComponent<Image>() : null;
            if (img != null) img.color = normalBg;
            if (cells[i] != null) cells[i].interactable = true;
        }
    }

    void OnCellClicked(Team team, int cellIndex)
    {
        if (_roundOver) return;

        bool isLeft = team == Team.Left;
        var cells = isLeft ? leftCells : rightCells;
        var texts = isLeft ? leftCellTexts : rightCellTexts;
        var grid = isLeft ? _leftGrid : _rightGrid;
        var visited = isLeft ? _leftVisited : _rightVisited;
        var done = isLeft ? _leftWordDone : _rightWordDone;

        // Ô đã chọn: không cho chọn lại, màu không đổi nữa.
        if (visited.Contains(cellIndex)) return;

        visited.Add(cellIndex);
        if (cells[cellIndex] != null) cells[cellIndex].interactable = false;
        SetCellStyle(cells, texts, cellIndex, tappedBg, tappedText);

        for (int w = 0; w < WordCount; w++)
        {
            if (done[w]) continue;
            var path = grid.paths[w];
            if (path == null || path.Length == 0) continue;

            bool allVisited = true;
            foreach (var idx in path)
            {
                if (!visited.Contains(idx)) { allVisited = false; break; }
            }
            if (!allVisited) continue;

            done[w] = true;
            foreach (var idx in path) SetCellStyle(cells, texts, idx, foundBg, foundText);
            MusicManager.Instance?.PlayCorrectSfx();
        }

        bool allDone = true;
        for (int w = 0; w < WordCount; w++)
        {
            var path = grid.paths[w];
            if (path != null && path.Length > 0 && !done[w]) { allDone = false; break; }
        }

        if (allDone)
        {
            _roundOver = true;
            LockAll();
            var loserDone = isLeft ? _rightWordDone : _leftWordDone;
            int loserWordCount = 0;
            foreach (var d in loserDone) if (d) loserWordCount++;
            _onResult?.Invoke(true, team, new[] { loserWordCount });
        }
    }

    void SetCellStyle(Button[] cells, Text[] texts, int index, Color bg, Color textColor)
    {
        if (cells == null || index < 0 || index >= cells.Length || cells[index] == null) return;
        var img = cells[index].GetComponent<Image>();
        if (img != null) img.color = bg;
        if (texts != null && index < texts.Length && texts[index] != null)
            texts[index].color = textColor;
    }

    void LockAll()
    {
        SetInteractable(leftCells, false);
        SetInteractable(rightCells, false);
    }

    static void SetInteractable(Button[] cells, bool value)
    {
        if (cells == null) return;
        foreach (var b in cells) if (b != null) b.interactable = value;
    }

    public void HidePlayerAnswers(Team team) => SetInteractable(team == Team.Left ? leftCells : rightCells, false);

    public void Cleanup()
    {
        _onResult = null;
        gameObject.SetActive(false);
    }
}
