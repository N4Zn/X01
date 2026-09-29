using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-player split: P1 có board riêng (p1Left/p1Right), P2 có board riêng (p2Left/p2Right).
/// Ai nối đủ 3 cặp đúng trước → win. Bên kia bị lock ngay.
///
/// Luật chọn:
///   - Có thể click bên trái hoặc bên phải trước đều được
///   - Sau khi chọn một ô (highlight vàng), phải chờ deselectDelay giây
///     mới có thể bỏ chọn hoặc đổi sang ô khác cùng phía
///   - Bấm ô đối diện bất kỳ lúc nào để hoàn thành cặp (không bị timer giới hạn)
/// </summary>
public class MatchingDisplay : MonoBehaviour, IAnswerDisplay
{
    [Header("Player 1 board (left half)")]
    [SerializeField] MatchingItem[] p1LeftItems;
    [SerializeField] MatchingItem[] p1RightItems;

    [Header("Player 2 board (right half)")]
    [SerializeField] MatchingItem[] p2LeftItems;
    [SerializeField] MatchingItem[] p2RightItems;

    [SerializeField] RectTransform linePrefab;

    // deselectDelay → TongHopConfig.Current.matchingDeselectDelay (gameconfig.json)

    Action<bool, Team, int[]> _onResult;
    Action<Team>              _onPlayerFailed;
    QuestionData _current;

    // ── Per-player state ──────────────────────────────────────────────────────
    readonly Dictionary<int, int> _p1Pairs = new();
    readonly Dictionary<int, int> _p2Pairs = new();
    readonly List<RectTransform>  _p1Lines  = new();
    readonly List<RectTransform>  _p2Lines  = new();

    MatchingItem _p1Selected;
    MatchingItem _p2Selected;
    float        _p1SelectTime;
    float        _p2SelectTime;
    bool         _p1Locked;
    bool         _p2Locked;
    bool         _p1AnsweredWrong;
    bool         _p2AnsweredWrong;

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current        = q;
        _onResult       = onResult;
        _onPlayerFailed = onPlayerFailed;

        _p1Pairs.Clear(); _p2Pairs.Clear();
        _p1Lines.Clear(); _p2Lines.Clear();
        _p1Selected      = _p2Selected      = null;
        _p1SelectTime    = _p2SelectTime    = 0f;
        _p1Locked        = _p2Locked        = false;
        _p1AnsweredWrong = _p2AnsweredWrong = false;

        // Re-enable tất cả items (có thể bị SetActive(false) từ HidePlayerAnswers round trước)
        foreach (var item in p1LeftItems)  item.gameObject.SetActive(true);
        foreach (var item in p1RightItems) item.gameObject.SetActive(true);
        foreach (var item in p2LeftItems)  item.gameObject.SetActive(true);
        foreach (var item in p2RightItems) item.gameObject.SetActive(true);

        // Mỗi bên shuffle cột phải riêng → 2 board không giống nhau
        int[] p1RightOrder = ShuffledIndices(3);
        int[] p2RightOrder = ShuffledIndices(3);

        for (int i = 0; i < 3; i++)
        {
            // Left items: answerMediaType  |  Right items: rightMediaType (có thể khác)
            p1LeftItems[i].Setup(q.leftItems[i], q.answerMediaType, i,
                OnP1LeftClicked, isLeft: true);
            p1RightItems[i].Setup(q.rightItems[p1RightOrder[i]], q.rightMediaType,
                p1RightOrder[i], OnP1RightClicked, isLeft: false);

            p2LeftItems[i].Setup(q.leftItems[i], q.answerMediaType, i,
                OnP2LeftClicked, isLeft: true);
            p2RightItems[i].Setup(q.rightItems[p2RightOrder[i]], q.rightMediaType,
                p2RightOrder[i], OnP2RightClicked, isLeft: false);
        }

        gameObject.SetActive(true);
    }

    public void HidePlayerAnswers(Team team)
    {
        var leftGroup  = team == Team.Left ? p1LeftItems  : p2LeftItems;
        var rightGroup = team == Team.Left ? p1RightItems : p2RightItems;
        foreach (var item in leftGroup)  item.gameObject.SetActive(false);
        foreach (var item in rightGroup) item.gameObject.SetActive(false);
        ClearLines(team == Team.Left ? _p1Lines : _p2Lines);
    }

    public void Cleanup()
    {
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        ClearLines(_p1Lines);
        ClearLines(_p2Lines);
        _p1Pairs.Clear(); _p2Pairs.Clear();
        foreach (var item in p1LeftItems)  item.SetState(ItemState.Normal);
        foreach (var item in p1RightItems) item.SetState(ItemState.Normal);
        foreach (var item in p2LeftItems)  item.SetState(ItemState.Normal);
        foreach (var item in p2RightItems) item.SetState(ItemState.Normal);
        gameObject.SetActive(false);
    }

    // ─── Click dispatchers ────────────────────────────────────────────────────
    // Tìm MatchingItem theo AnswerIndex, rồi chuyển vào HandleClick thống nhất.

    void OnP1LeftClicked(int index, Team _)
    {
        if (_p1Locked) return;
        var item = FindItem(p1LeftItems, index);
        if (item == null) return;
        HandleClick(item, ref _p1Selected, ref _p1SelectTime,
                    p1LeftItems, p1RightItems, _p1Pairs, _p1Lines, ref _p1Locked, Team.Left);
    }

    void OnP1RightClicked(int index, Team _)
    {
        if (_p1Locked) return;
        var item = FindItem(p1RightItems, index);
        if (item == null) return;
        HandleClick(item, ref _p1Selected, ref _p1SelectTime,
                    p1LeftItems, p1RightItems, _p1Pairs, _p1Lines, ref _p1Locked, Team.Left);
    }

    void OnP2LeftClicked(int index, Team _)
    {
        if (_p2Locked) return;
        var item = FindItem(p2LeftItems, index);
        if (item == null) return;
        HandleClick(item, ref _p2Selected, ref _p2SelectTime,
                    p2LeftItems, p2RightItems, _p2Pairs, _p2Lines, ref _p2Locked, Team.Right);
    }

    void OnP2RightClicked(int index, Team _)
    {
        if (_p2Locked) return;
        var item = FindItem(p2RightItems, index);
        if (item == null) return;
        HandleClick(item, ref _p2Selected, ref _p2SelectTime,
                    p2LeftItems, p2RightItems, _p2Pairs, _p2Lines, ref _p2Locked, Team.Right);
    }

    // ─── Unified click logic ──────────────────────────────────────────────────

    void HandleClick(MatchingItem        clicked,
                     ref MatchingItem     selected,
                     ref float            selectTime,
                     MatchingItem[]       leftGroup,
                     MatchingItem[]       rightGroup,
                     Dictionary<int, int> pairs,
                     List<RectTransform>  lines,
                     ref bool             locked,
                     Team                 team)
    {
        // ── 1. Bấm lại ô đang chọn → bỏ chọn (chỉ sau khi hết timer) ─────────
        if (selected == clicked)
        {
            if (Time.time - selectTime < TongHopConfig.Current.matchingDeselectDelay) return;
            selected.SetState(ItemState.Normal);
            selected = null;
            return;
        }

        // ── 2. Bấm ô cùng phía → đổi selection ngay (không bị timer) ──────────
        // Timer chỉ chặn việc bỏ chọn hoàn toàn (click lại chính ô đó ở case 1),
        // không chặn việc đổi sang ô khác cùng phía.
        if (selected != null && selected.IsLeft == clicked.IsLeft)
        {
            selected.SetState(ItemState.Normal);
            BeginSelect(clicked, ref selected, ref selectTime, pairs, lines);
            return;
        }

        // ── 3. Chưa có ô nào được chọn → chọn ô này ─────────────────────────
        if (selected == null)
        {
            BeginSelect(clicked, ref selected, ref selectTime, pairs, lines);
            return;
        }

        // ── 4. Có ô đang chọn và bấm ô đối diện → ghép cặp ──────────────────
        MatchingItem leftItem  = selected.IsLeft ? selected : clicked;
        MatchingItem rightItem = selected.IsLeft ? clicked  : selected;
        int leftIndex  = leftItem.AnswerIndex;
        int rightIndex = rightItem.AnswerIndex;

        // Xoá pair cũ của left item (nếu có)
        if (pairs.ContainsKey(leftIndex))
            RemoveLine(leftIndex, pairs, lines);

        // Xoá pair cũ của right item (nếu đã nối với left item khác)
        int staleLeft = FindRightOwner(pairs, rightIndex);
        if (staleLeft >= 0)
            RemoveLine(staleLeft, pairs, lines);

        // Tạo pair mới
        pairs[leftIndex] = rightIndex;
        DrawLine(leftItem, rightItem, lines);

        // Block re-click ngay sau khi ghép — cả 2 bên chờ matchingDeselectDelay
        leftItem.SetPaired();
        rightItem.SetPaired();
        selected = null;

        // Validate khi đủ 3 cặp
        if (pairs.Count == 3)
        {
            // Log từng cặp trước khi validate (chưa biết đúng/sai)
            foreach (var kvp in pairs)
            {
                bool pairCorrect = _current.correctPairs != null
                                   && kvp.Key < _current.correctPairs.Length
                                   && _current.correctPairs[kvp.Key] == kvp.Value;
                GameLogger.Current?.LogPair(team, kvp.Key, kvp.Value, pairCorrect);
            }
            ValidateBoard(pairs, leftGroup, rightGroup, lines, ref locked, team);
        }
    }

    /// <summary>
    /// Bắt đầu chọn item: xoá pair cũ liên quan (nếu có), rồi highlight.
    /// </summary>
    void BeginSelect(MatchingItem item, ref MatchingItem selected, ref float selectTime,
                     Dictionary<int, int> pairs, List<RectTransform> lines)
    {
        // Xoá pair cũ liên quan đến item này để "giải phóng" nó trước khi re-select
        if (item.IsLeft)
        {
            if (pairs.ContainsKey(item.AnswerIndex))
                RemoveLine(item.AnswerIndex, pairs, lines);
        }
        else
        {
            int owner = FindRightOwner(pairs, item.AnswerIndex);
            if (owner >= 0) RemoveLine(owner, pairs, lines);
        }

        selected   = item;
        selectTime = Time.time;
        item.SetState(ItemState.Selected);
    }

    // ─── Validate ─────────────────────────────────────────────────────────────

    void ValidateBoard(Dictionary<int, int> pairs,
                       MatchingItem[]       leftGroup,
                       MatchingItem[]       rightGroup,
                       List<RectTransform>  lines,
                       ref bool             locked,
                       Team                 team)
    {
        locked = true;
        bool allCorrect   = true;
        var  playerAnswer = new int[3];

        for (int i = 0; i < 3; i++)
        {
            playerAnswer[i] = pairs.ContainsKey(i) ? pairs[i] : -1;
            bool correct = pairs.ContainsKey(i) && pairs[i] == _current.correctPairs[i];

            FindItem(leftGroup,  i)               ?.SetState(correct ? ItemState.Correct : ItemState.Wrong);
            FindItem(rightGroup, playerAnswer[i])  ?.SetState(correct ? ItemState.Correct : ItemState.Wrong);

            if (!correct) allCorrect = false;
        }

        if (allCorrect)
        {
            LockOtherBoard(team);
            _onResult?.Invoke(true, team, playerAnswer);
        }
        else
        {
            if (team == Team.Left) _p1AnsweredWrong = true;
            else                   _p2AnsweredWrong = true;
            _onPlayerFailed?.Invoke(team);   // hiện ✗ ngay, trước khi chờ player kia
            CheckBothWrong();
        }
    }

    // ─── Both-wrong ───────────────────────────────────────────────────────────

    void CheckBothWrong()
    {
        if (_p1AnsweredWrong && _p2AnsweredWrong)
            _onResult?.Invoke(false, Team.Left, System.Array.Empty<int>());
    }

    void LockOtherBoard(Team winner)
    {
        var theirLeft  = winner == Team.Left ? p2LeftItems  : p1LeftItems;
        var theirRight = winner == Team.Left ? p2RightItems : p1RightItems;
        foreach (var item in theirLeft)  item.SetState(ItemState.Locked);
        foreach (var item in theirRight) item.SetState(ItemState.Locked);
        if (winner == Team.Left) _p2Locked = true;
        else                     _p1Locked = true;
    }

    // ─── Line drawing ─────────────────────────────────────────────────────────

    void DrawLine(MatchingItem left, MatchingItem right, List<RectTransform> lines)
    {
        if (linePrefab == null || left == null || right == null) return;

        var   line      = Instantiate(linePrefab, transform);
        var   leftRT    = left.GetComponent<RectTransform>();
        var   rightRT   = right.GetComponent<RectTransform>();
        float leftHalf  = leftRT.rect.width  * 0.5f * left.transform.lossyScale.x;
        float rightHalf = rightRT.rect.width * 0.5f * right.transform.lossyScale.x;

        Vector3 start = left.transform.position  + new Vector3( leftHalf,  0, 0);
        Vector3 end   = right.transform.position - new Vector3(rightHalf, 0, 0);
        Vector3 dir   = end - start;

        line.position  = (start + end) * 0.5f;
        line.rotation  = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        line.sizeDelta = new Vector2(dir.magnitude, line.sizeDelta.y);
        line.name      = $"line_{left.AnswerIndex}";
        lines.Add(line);
    }

    void RemoveLine(int leftIndex, Dictionary<int, int> pairs, List<RectTransform> lines)
    {
        var line = lines.Find(l => l != null && l.name == $"line_{leftIndex}");
        if (line != null) { lines.Remove(line); Destroy(line.gameObject); }
        pairs.Remove(leftIndex);
    }

    void ClearLines(List<RectTransform> lines)
    {
        foreach (var l in lines) if (l) Destroy(l.gameObject);
        lines.Clear();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    static MatchingItem FindItem(MatchingItem[] group, int index)
        => System.Array.Find(group, x => x.AnswerIndex == index);

    /// <summary>Tìm left index đang sở hữu rightIndex trong pairs. -1 nếu không có.</summary>
    static int FindRightOwner(Dictionary<int, int> pairs, int rightIndex)
    {
        foreach (var kvp in pairs)
            if (kvp.Value == rightIndex) return kvp.Key;
        return -1;
    }

    int[] ShuffledIndices(int n)
    {
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        return idx;
    }
}
