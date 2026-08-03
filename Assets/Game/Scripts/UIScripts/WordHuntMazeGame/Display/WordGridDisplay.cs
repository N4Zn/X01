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

    // Nền
    static readonly Color NormalBg = Color.white;
    static readonly Color TappedBg = Color.black;
    static readonly Color FoundBg  = new Color(0.18f, 0.92f, 0.38f, 1f); // xanh lá rất tươi
    // Chữ
    static readonly Color NormalText = Color.black;
    static readonly Color TappedText = Color.white;
    static readonly Color FoundText  = Color.black;
    // Rounded-rect highlight border quanh từ đúng
    static readonly Color HighlightBorderColor = new Color(0.05f, 0.70f, 0.20f, 1f); // xanh đậm hơn FoundBg để tạo border
    const float HighlightPadding = 5f;

    // Sprite 9-slice bo góc, sinh 1 lần, dùng chung cho mọi highlight
    static Sprite _roundedSprite;

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
            if (cells[i] != null) cells[i].onClick.AddListener(() => OnCellClicked(team, idx));
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
        ClearHighlights();
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
                texts[i].color = NormalText;
            }
            var img = cells[i] != null ? cells[i].GetComponent<Image>() : null;
            if (img != null) img.color = NormalBg;
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
        SetCellStyle(cells, texts, cellIndex, TappedBg, TappedText);

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
            foreach (var idx in path) SetCellStyle(cells, texts, idx, FoundBg, FoundText);
            DrawWordHighlight(cells, path);
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

    // Vẽ rounded-rect border bao quanh tất cả ô của từ vừa tìm được.
    void DrawWordHighlight(Button[] cells, int[] path)
    {
        if (path == null || path.Length == 0 || cells == null) return;

        // Tính bounding box trong local space của parent chứa các cell.
        var parentRt = cells[path[0]]?.GetComponent<RectTransform>()?.parent as RectTransform;
        if (parentRt == null) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        var corners = new Vector3[4];

        foreach (var idx in path)
        {
            if (idx < 0 || idx >= cells.Length || cells[idx] == null) continue;
            cells[idx].GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach (var c in corners)
            {
                var local = parentRt.InverseTransformPoint(c);
                if (local.x < minX) minX = local.x;
                if (local.y < minY) minY = local.y;
                if (local.x > maxX) maxX = local.x;
                if (local.y > maxY) maxY = local.y;
            }
        }

        var go = new GameObject("_wordHighlight");
        go.transform.SetParent(parentRt, false);

        var img = go.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = HighlightBorderColor;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(minX - HighlightPadding, minY - HighlightPadding);
        rt.sizeDelta = new Vector2(maxX - minX + HighlightPadding * 2f, maxY - minY + HighlightPadding * 2f);

        // Render sau các cell để không che chữ, nhưng trước background grid.
        go.transform.SetAsFirstSibling();
    }

    // Sinh sprite bo góc 64x64, 9-slice — chỉ tạo 1 lần rồi cache.
    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;

        const int size = 64, r = 16, border = 5;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool outer = InRoundedRect(x, y, size, size, r);
            bool inner = InRoundedRect(x, y, size, size, r - border, border, border, size - border - 1, size - border - 1);
            pixels[y * size + x] = (outer && !inner)
                ? new Color32(255, 255, 255, 255)
                : new Color32(0, 0, 0, 0);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect,
            new Vector4(r, r, r, r));
        return _roundedSprite;
    }

    // true nếu (px,py) nằm trong rounded-rect với corner radius r, trong vùng [x0,y0]-[x1,y1].
    static bool InRoundedRect(int px, int py, int w, int h, int r,
        int x0 = 0, int y0 = 0, int x1 = -1, int y1 = -1)
    {
        if (x1 < 0) x1 = w - 1;
        if (y1 < 0) y1 = h - 1;
        if (px < x0 || px > x1 || py < y0 || py > y1) return false;

        // 4 góc: kiểm tra circle
        bool inCornerX = px < x0 + r || px > x1 - r;
        bool inCornerY = py < y0 + r || py > y1 - r;
        if (!inCornerX || !inCornerY) return true;

        int cx = px < x0 + r ? x0 + r : x1 - r;
        int cy = py < y0 + r ? y0 + r : y1 - r;
        float dx = px - cx, dy = py - cy;
        return dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
    }

    void ClearHighlights()
    {
        // Xóa tất cả _wordHighlight GameObjects từ round trước.
        foreach (Transform child in transform)
            if (child.name == "_wordHighlight") Destroy(child.gameObject);

        // Xóa cả trong parent của left/right cells
        void ClearFrom(Button[] cells)
        {
            if (cells == null || cells.Length == 0 || cells[0] == null) return;
            var p = cells[0].transform.parent;
            if (p == null) return;
            foreach (Transform child in p)
                if (child.name == "_wordHighlight") Destroy(child.gameObject);
        }
        ClearFrom(leftCells);
        ClearFrom(rightCells);
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
