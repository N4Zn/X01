using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Spawn liên tục" — đáp án xuất hiện định kỳ, trôi theo 1 HƯỚNG CỐ ĐỊNH (lên/xuống/trái/phải)
/// qua hết vùng chơi rồi tự biến mất nếu không chạm trúng (KHÔNG phạt điểm, chỉ mất lượt xem đáp
/// án đó — xem GenericGame/SCHEMA.md). Dùng cho GenericGame (`layout.spawnFlow`), implement
/// IAnswerDisplay đúng khuôn `FloatingDisplay.cs` (orbit) — khác biệt chính là cách item di chuyển
/// (đường thẳng, có spawn/despawn liên tục) thay vì quỹ đạo tròn tạo 1 lần cho cả round.
///
/// Item bị CHẠM TRÚNG (đúng hoặc sai) KHÔNG bị huỷ ngay — chỉ ĐÓNG BĂNG tại chỗ (dừng coroutine di
/// chuyển, vẫn hiển thị) để GenericGameController kịp chạy hiệu ứng onCorrectTap/onWrongTap (Punch/
/// Shake/...) trên đúng RectTransform đó, giống hệt ButtonDisplay (tap xong vẫn hiện, tô màu, chờ
/// Cleanup() mới mất). `HidePlayerAnswers(team)` (GenericGameController gọi khi ẩn câu hỏi/đáp án
/// lúc "Next in Ns") mới thật sự dọn sạch, bao gồm cả item đã đóng băng.
///
/// KHÔNG tự biết gì về answerCaptions/shape/transparency/onIdle — GenericGameController cấu hình
/// qua `OnItemSpawned` callback (xem GenericGameController.BuildRuntimeUi) để tái dùng đúng logic
/// đã có, không viết lại ở đây. onIdle/caption-đè-ảnh CHƯA hỗ trợ ở spawn flow (giới hạn v1, xem
/// SCHEMA.md) vì item xuất hiện rải rác theo thời gian, không có "lúc round bắt đầu" rõ ràng để áp
/// 1 lần như ButtonDisplay.
/// </summary>
public class SpawnFlowDisplay : MonoBehaviour, IAnswerDisplay
{
    [SerializeField] ButtonItem itemPrefab;
    [SerializeField] RectTransform leftSpawnParent;
    [SerializeField] RectTransform rightSpawnParent;

    /// <summary>Fires on EVERY tap, NGAY SAU khi validator tính xong — khớp API
    /// ButtonDisplay.onAnswerTapped để GenericGameController dùng CHUNG 1 HandleAnswerTapped bất kể
    /// display nào đang active.</summary>
    public Action<Team, int, ClickResult> onAnswerTapped;

    /// <summary>true = Combined "chờ cả 2 đội" (xem ButtonDisplay.WaitBothTeams): đội đúng KHÔNG dọn bên kia, cả 2 sai không tự báo kết thúc.</summary>
    public bool WaitBothTeams { get; set; }
    /// <summary>Gọi mỗi khi 1 item mới được tạo (trước khi bắt đầu di chuyển) — GenericGameController
    /// dùng để áp shape/transparency giống hệt CreateSlot() của ButtonDisplay.</summary>
    public Action<Team, ButtonItem, int> OnItemSpawned;

    // ── Cấu hình (GenericGameController gán 1 lần qua Configure()) ─────────────
    string _direction = "BottomToTop"; // "BottomToTop" | "TopToBottom" | "LeftToRight" | "RightToLeft"
    float _itemWPct = 15f, _itemHPct = 15f;
    float _speedPct = 25f;           // % chiều di chuyển / giây
    float _spawnIntervalSec = 1f;
    int _maxConcurrent = 4;
    RectPct _areaLeft, _areaRight;

    public void Configure(string direction, float itemWPct, float itemHPct, float speedPct,
        float spawnIntervalSec, int maxConcurrent, RectPct areaLeft, RectPct areaRight)
    {
        _direction = direction;
        _itemWPct = Mathf.Max(2f, itemWPct);
        _itemHPct = Mathf.Max(2f, itemHPct);
        _speedPct = Mathf.Max(1f, speedPct);
        _spawnIntervalSec = Mathf.Max(0.1f, spawnIntervalSec);
        _maxConcurrent = Mathf.Max(1, maxConcurrent);
        _areaLeft = areaLeft;
        _areaRight = areaRight;
        ClipToHalf(leftSpawnParent);
        ClipToHalf(rightSpawnParent);
    }

    /// <summary>Item xuất hiện/biến mất NGOÀI mép (startX=-itemW / endX=100) — phải bị CẮT đúng ở rìa nửa
    /// màn hình của bên đó (khớp web: `.pt-half{overflow:hidden}`), không trôi sang nửa kia. RectMask2D gắn
    /// vào SpawnFlowRoot (con của LeftHalf/RightHalf) nên áp được cho cả scene đã import từ trước.</summary>
    public RectTransform SpawnParent(Team team) => team == Team.Left ? leftSpawnParent : rightSpawnParent;

    static void ClipToHalf(RectTransform parent)
    {
        if (parent != null && parent.GetComponent<UnityEngine.UI.RectMask2D>() == null)
            parent.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
    }

    /// <summary>Đổi vùng trôi của RIÊNG 1 bên (GenericGame v2: vùng đáp án khác nhau theo round, mỗi bên có thể đang ở round khác
    /// khi chơi độc lập) — `area` đã mirror sẵn cho bên phải. Có hiệu lực từ lần spawn kế tiếp.</summary>
    public void SetArea(Team team, RectPct area)
    {
        if (team == Team.Left) _areaLeft = area; else _areaRight = area;
    }

    Action<bool, Team, int[]> _onResult;
    Action<Team> _onPlayerFailed;
    Action<Team> _onPartialCorrect;
    QuestionData _current;

    AnswerValidator _leftValidator, _rightValidator;
    bool _leftAnsweredWrong, _rightAnsweredWrong;
    bool _leftFinalised, _rightFinalised;
    bool _leftRunning, _rightRunning;
    Coroutine _leftSpawnLoop, _rightSpawnLoop;

    class LiveItem
    {
        public ButtonItem Item;
        public Coroutine Move;
        public bool Frozen; // đã chạm trúng, dừng di chuyển, chờ HidePlayerAnswers() dọn
    }
    // answerIndex -> item đang sống (đang bay HOẶC đã đóng băng do vừa chạm) — tối đa 1 bản/answerIndex
    // tại 1 thời điểm, khớp bất biến của ButtonDisplay (GetActiveButtonByAnswerIndex không mơ hồ).
    readonly Dictionary<int, LiveItem> _leftActive = new Dictionary<int, LiveItem>();
    readonly Dictionary<int, LiveItem> _rightActive = new Dictionary<int, LiveItem>();
    // answerIndex đã "xong" (đúng đã gom ở MultiSelect/OrderedSequence) — loại khỏi random spawn tiếp theo.
    readonly HashSet<int> _leftRetired = new HashSet<int>();
    readonly HashSet<int> _rightRetired = new HashSet<int>();

    // ── Independent mode ───────────────────────────────────────────────────────
    bool _isIndependent;
    QuestionData _leftQuestionInd, _rightQuestionInd;
    Action<bool, Team, int[]> _leftOnDoneInd, _rightOnDoneInd;

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void SetPartialCorrectCallback(Action<Team> cb) => _onPartialCorrect = cb;

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current = q;
        _onResult = onResult;
        _onPlayerFailed = onPlayerFailed;
        _onPartialCorrect = null;
        _isIndependent = false;
        ResetTeamState(Team.Left, q);
        ResetTeamState(Team.Right, q);
        gameObject.SetActive(true);
        StartSide(Team.Left);
        StartSide(Team.Right);
    }

    public void SetupPlayerIndependent(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        _isIndependent = true;
        gameObject.SetActive(true);
        bool isLeft = team == Team.Left;
        if (isLeft) { _leftQuestionInd = q; _leftOnDoneInd = onDone; } else { _rightQuestionInd = q; _rightOnDoneInd = onDone; }
        ResetTeamState(team, q);
        StartSide(team);
    }

    /// <summary>Dọn SẠCH mọi item của 1 bên (đang bay LẪN đã đóng băng do vừa chạm) — gọi khi ẩn
    /// câu hỏi/đáp án lúc "Next in Ns" (GenericGameController.HideQuestionAndAnswers) hoặc khi 1
    /// bên fail sớm. Đây là nơi DUY NHẤT item đã đóng băng thật sự bị huỷ.</summary>
    public void HidePlayerAnswers(Team team)
    {
        StopSide(team);
        ClearActive(team);
    }

    public void Cleanup()
    {
        StopSide(Team.Left);
        StopSide(Team.Right);
        ClearActive(Team.Left);
        ClearActive(Team.Right);
        _isIndependent = false;
        _leftQuestionInd = _rightQuestionInd = null;
        _leftOnDoneInd = _rightOnDoneInd = null;
        _onPartialCorrect = null;
        gameObject.SetActive(false);
    }

    void ResetTeamState(Team team, QuestionData q)
    {
        bool isLeft = team == Team.Left;
        if (isLeft) { _leftValidator = new AnswerValidator(q); _leftAnsweredWrong = false; _leftFinalised = false; _leftRetired.Clear(); }
        else { _rightValidator = new AnswerValidator(q); _rightAnsweredWrong = false; _rightFinalised = false; _rightRetired.Clear(); }
        ClearActive(team); // dọn nốt item đóng băng còn sót từ round trước, nếu HidePlayerAnswers chưa kịp gọi
    }

    void ClearActive(Team team)
    {
        var dict = team == Team.Left ? _leftActive : _rightActive;
        foreach (var kv in dict)
        {
            if (kv.Value.Move != null) StopCoroutine(kv.Value.Move);
            if (kv.Value.Item != null) Destroy(kv.Value.Item.gameObject);
        }
        dict.Clear();
    }

    void StartSide(Team team)
    {
        StopSide(team);
        if (team == Team.Left) { _leftRunning = true; _leftSpawnLoop = StartCoroutine(SpawnLoop(Team.Left)); }
        else { _rightRunning = true; _rightSpawnLoop = StartCoroutine(SpawnLoop(Team.Right)); }
    }

    void StopSide(Team team)
    {
        if (team == Team.Left) { _leftRunning = false; if (_leftSpawnLoop != null) StopCoroutine(_leftSpawnLoop); _leftSpawnLoop = null; }
        else { _rightRunning = false; if (_rightSpawnLoop != null) StopCoroutine(_rightSpawnLoop); _rightSpawnLoop = null; }
    }

    // ─── Spawn loop ───────────────────────────────────────────────────────────

    IEnumerator SpawnLoop(Team team)
    {
        // Bắn 1 item ngay lúc bắt đầu (không chờ hết interval đầu tiên) — round mới mà phải đợi cả
        // giây mới thấy gì sẽ có cảm giác "đơ".
        SpawnOne(team);
        var wait = new WaitForSeconds(_spawnIntervalSec);
        while (IsRunning(team))
        {
            yield return wait;
            if (!IsRunning(team)) yield break;
            if (ActiveCount(team) < _maxConcurrent) SpawnOne(team);
        }
    }

    bool IsRunning(Team team) => team == Team.Left ? _leftRunning : _rightRunning;
    int ActiveCount(Team team) => (team == Team.Left ? _leftActive : _rightActive).Count;

    void SpawnOne(Team team)
    {
        var q = QuestionFor(team);
        if (q?.answers == null || q.answers.Length == 0) return;
        int idx = PickSpawnIndex(team, q);
        if (idx < 0) return;

        var parent = team == Team.Left ? leftSpawnParent : rightSpawnParent;
        if (parent == null || itemPrefab == null) return;

        var area = team == Team.Left ? _areaLeft : _areaRight;
        // Không còn làn nào trống (mọi vị trí đều đè/sát item đang trôi) → bỏ lượt spawn này, lượt sau thử lại.
        if (!TryGetPath(team, area, out float startX, out float startY, out float endX, out float endY, out float travelPct)) return;

        var go = Instantiate(itemPrefab.gameObject, parent);
        var item = go.GetComponent<ButtonItem>();
        item.Setup(q.answers[idx], q.answerMediaType, idx, OnItemClicked, team);
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        // Đặt vị trí + KÍCH THƯỚC BAN ĐẦU ngay lập tức (đồng bộ) — tránh 1 frame hiện sai chỗ trước khi
        // coroutine di chuyển kịp chạy frame đầu tiên. PHẢI làm TRƯỚC OnItemSpawned: callback đó (GenericGame)
        // dựng lưới icon theo rect của item (ButtonItem.ArrangeIconGrid) — gọi khi item còn cỡ prefab thì
        // cellSize tính sai → icon/đáp án bị kéo dãn.
        ApplyRectPct(rt, startX, startY, _itemWPct, _itemHPct);

        OnItemSpawned?.Invoke(team, item, idx);

        var live = new LiveItem { Item = item };
        live.Move = StartCoroutine(MoveAndMaybeDespawn(team, live, rt, idx, startX, startY, endX, endY, travelPct));
        var active = team == Team.Left ? _leftActive : _rightActive;
        active[idx] = live;
    }

    /// <summary>Random 1 answerIndex hợp lệ (không rỗng, chưa "retired", KHÔNG đang có bản sao sống
    /// — đảm bảo tối đa 1 bản/answerIndex tại 1 thời điểm). Trả -1 nếu không còn ứng viên nào (hết
    /// tạm thời, chờ item đang bay despawn/retired thì lại có chỗ).</summary>
    int PickSpawnIndex(Team team, QuestionData q)
    {
        var retired = team == Team.Left ? _leftRetired : _rightRetired;
        var active = team == Team.Left ? _leftActive : _rightActive;
        var candidates = new List<int>();
        for (int i = 0; i < q.answers.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(q.answers[i])) continue;
            if (retired.Contains(i) || active.ContainsKey(i)) continue;
            candidates.Add(i);
        }
        if (candidates.Count == 0) return -1;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    // ─── Di chuyển + despawn ───────────────────────────────────────────────────

    IEnumerator MoveAndMaybeDespawn(Team team, LiveItem live, RectTransform rt, int answerIndex,
        float startX, float startY, float endX, float endY, float travelPct)
    {
        // Hệ số tốc độ tổng từ Control panel (0.1–5x) nhân vào tốc độ gốc của game.
        float flow = GameSettings.Instance != null ? GameSettings.Instance.FlowSpeed : 1f;
        float duration = Mathf.Max(0.1f, travelPct / (_speedPct * flow));
        float t = 0f;
        while (t < 1f)
        {
            if (live.Item == null) yield break; // đã bị dọn (ClearActive) trong lúc đang bay
            if (live.Frozen) yield break;        // vừa bị chạm trúng — đứng yên tại chỗ, không tự huỷ ở đây
            t += Time.deltaTime / duration;
            float x = Mathf.Lerp(startX, endX, t);
            float y = Mathf.Lerp(startY, endY, t);
            ApplyRectPct(rt, x, y, _itemWPct, _itemHPct);
            yield return null;
        }
        if (live.Item == null || live.Frozen) yield break;
        // Trôi hết màn hình mà KHÔNG bị chạm — bỏ qua, không phạt điểm, chỉ dọn item. Phải gỡ khỏi
        // active để answerIndex này có thể được random spawn lại ở lượt sau.
        var active = team == Team.Left ? _leftActive : _rightActive;
        active.Remove(answerIndex);
        Destroy(live.Item.gameObject);
    }

    /// <summary>Khoảng cách TỐI THIỂU (% cạnh nửa màn hình) giữa 2 đáp án flow — không bao giờ đè/chạm nhau.
    /// Khớp SPAWN_GAP_PCT của web (game_builder_v2.html).</summary>
    const float MinGapPct = 2f;

    /// <summary>Tính điểm đầu/cuối (%) theo hướng cấu hình + chọn vị trí trên trục VUÔNG GÓC trong `area`
    /// (answerArea, đã mirror sẵn cho bên phải) — item xuất phát/kết thúc NGOÀI vùng hiển thị (offset = đúng
    /// 1 lần kích thước item) để "xuất hiện/biến mất" tự nhiên ở rìa màn hình thay vì bật ra giữa khung hình.
    /// Vị trí vuông góc chỉ chọn trong các làn KHÔNG đè (và cách ≥ MinGapPct) mọi item đang sống của `team`
    /// (kể cả item đóng băng) — hết làn trống thì trả false (không spawn).</summary>
    bool TryGetPath(Team team, RectPct area, out float startX, out float startY, out float endX, out float endY, out float travelPct)
    {
        bool horizontal = _direction == "LeftToRight" || _direction == "RightToLeft";
        startX = startY = endX = endY = travelPct = 0f;

        // Điểm xuất phát theo trục chuyển động.
        float alongStart, alongEnd, alongSize = horizontal ? _itemWPct : _itemHPct;
        switch (_direction)
        {
            case "LeftToRight": alongStart = -_itemWPct; alongEnd = 100f; break;
            case "RightToLeft": alongStart = 100f; alongEnd = -_itemWPct; break;
            case "TopToBottom": alongStart = -_itemHPct; alongEnd = 100f; break;
            default: alongStart = 100f; alongEnd = -_itemHPct; break; // BottomToTop
        }
        float perpSize = horizontal ? _itemHPct : _itemWPct;
        float perpLo = horizontal ? area.yPct : area.xPct;
        float perpHi = Mathf.Max(perpLo, perpLo + (horizontal ? area.hPct : area.wPct) - perpSize);

        // Làn bị chặn bởi item đang sống: chỉ item còn ở gần điểm xuất phát (theo trục chuyển động) mới chặn.
        var blocked = new List<Vector2>();
        var active = team == Team.Left ? _leftActive : _rightActive;
        foreach (var live in active.Values)
        {
            if (live?.Item == null) continue;
            var rt = (RectTransform)live.Item.transform;
            float ox = rt.anchorMin.x * 100f, oyTop = (1f - rt.anchorMax.y) * 100f;
            float oAlong = horizontal ? ox : oyTop, oPerp = horizontal ? oyTop : ox;
            if (oAlong < alongStart + alongSize + MinGapPct && alongStart < oAlong + alongSize + MinGapPct)
                blocked.Add(new Vector2(oPerp - perpSize - MinGapPct, oPerp + perpSize + MinGapPct));
        }

        if (!PickFreePosition(perpLo, perpHi, blocked, out float perp)) return false;

        if (horizontal) { startX = alongStart; endX = alongEnd; startY = endY = perp; }
        else { startY = alongStart; endY = alongEnd; startX = endX = perp; }
        travelPct = Mathf.Abs(alongEnd - alongStart);
        return true;
    }

    /// <summary>Chọn ngẫu nhiên 1 giá trị trong [lo,hi] không rơi vào khoảng MỞ nào của `blocked` (ngẫu nhiên đều theo độ dài
    /// các đoạn trống). Không còn đoạn trống → false.</summary>
    static bool PickFreePosition(float lo, float hi, List<Vector2> blocked, out float value)
    {
        blocked.Sort((a, b) => a.x.CompareTo(b.x));
        var segs = new List<Vector2>();
        float cur = lo;
        foreach (var b in blocked)
        {
            if (cur > hi) break;
            if (b.x >= cur) segs.Add(new Vector2(cur, Mathf.Min(b.x, hi)));
            cur = Mathf.Max(cur, b.y);
        }
        if (cur <= hi) segs.Add(new Vector2(cur, hi));

        value = 0f;
        if (segs.Count == 0) return false;
        float total = 0f;
        foreach (var sg in segs) total += sg.y - sg.x;
        if (total < 0.001f) { value = segs[UnityEngine.Random.Range(0, segs.Count)].x; return true; }
        float r = UnityEngine.Random.value * total;
        foreach (var sg in segs)
        {
            float len = sg.y - sg.x;
            if (r <= len) { value = sg.x + r; return true; }
            r -= len;
        }
        value = segs[segs.Count - 1].y;
        return true;
    }

    static void ApplyRectPct(RectTransform rt, float xPct, float yPct, float wPct, float hPct)
    {
        rt.anchorMin = new Vector2(xPct / 100f, 1f - (yPct + hPct) / 100f);
        rt.anchorMax = new Vector2((xPct + wPct) / 100f, 1f - yPct / 100f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    QuestionData QuestionFor(Team team) => _isIndependent
        ? (team == Team.Left ? _leftQuestionInd : _rightQuestionInd)
        : _current;

    // ─── Click handling — mirror ButtonDisplay.OnItemClicked (cùng ClickResult, cùng luật) ──────

    void OnItemClicked(int answerIndex, Team team)
    {
        bool isLeft = team == Team.Left;
        if (isLeft ? _leftFinalised : _rightFinalised) return;

        var q = QuestionFor(team);
        if (q == null) return;

        var validator = isLeft ? _leftValidator : _rightValidator;
        var active = isLeft ? _leftActive : _rightActive;
        var retired = isLeft ? _leftRetired : _rightRetired;

        var result = validator.RegisterClick(answerIndex);
        GameLogger.Current?.LogClick(team, answerIndex, result);
        onAnswerTapped?.Invoke(team, answerIndex, result);

        active.TryGetValue(answerIndex, out var live);

        switch (result)
        {
            case ClickResult.WrongFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                StopSide(team);
                FreezeTapped(live);
                // Dọn SẠCH mọi item KHÁC còn bay của bên này (không phải cái vừa chạm) — lượt của
                // bên này đã kết thúc, item vừa chạm (đóng băng) được giữ lại để hiện hiệu ứng sai,
                // chỉ thật sự mất khi HidePlayerAnswers() được gọi (lúc "Next in Ns").
                ClearOthersExcept(team, answerIndex);
                if (_isIndependent)
                {
                    (isLeft ? _leftOnDoneInd : _rightOnDoneInd)?.Invoke(false, team, new[] { answerIndex }); // đáp án ĐÃ CHỌN
                }
                else
                {
                    if (isLeft) _leftAnsweredWrong = true; else _rightAnsweredWrong = true;
                    _onPlayerFailed?.Invoke(team);
                    CheckBothWrong();
                }
                break;

            case ClickResult.CorrectPartial:
                // Round CHƯA xong (MultiSelect/OrderedSequence giữa chừng) — item này coi như "đã
                // xong việc", KHÔNG tính vào active nữa (để spawn lại các answerIndex khác), nhưng
                // vẫn đóng băng hiện tại chỗ cho hiệu ứng onCorrectRemove (PlayCorrectRemoveClone)
                // chạy — GenericGameController tự clone+ẩn bản gốc, không cần tự huỷ ở đây.
                retired.Add(answerIndex);
                if (live != null) active.Remove(answerIndex);
                FreezeTapped(live);
                _onPartialCorrect?.Invoke(team);
                break;

            case ClickResult.CorrectFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                StopSide(team);
                FreezeTapped(live);
                ClearOthersExcept(team, answerIndex);
                if (!_isIndependent && !WaitBothTeams)
                {
                    // Combined: dọn sạch LUÔN bên kia (round kết thúc, không để lộ đáp án đang trôi
                    // dở của bên vừa thua) — khớp tinh thần ButtonDisplay.LockGroup().
                    var other = team == Team.Left ? Team.Right : Team.Left;
                    StopSide(other);
                    ClearActive(other);
                }
                if (_isIndependent)
                    (isLeft ? _leftOnDoneInd : _rightOnDoneInd)?.Invoke(true, team, q.correctAnswers);
                else
                    _onResult?.Invoke(true, team, q.correctAnswers);
                break;
        }
    }

    static void FreezeTapped(LiveItem live)
    {
        if (live != null) live.Frozen = true;
    }

    /// <summary>Huỷ mọi item ĐANG BAY của 1 bên TRỪ answerIndex vừa chạm (đã đóng băng, giữ lại cho
    /// hiệu ứng) — dùng khi round/lượt của bên đó kết thúc.</summary>
    void ClearOthersExcept(Team team, int exceptIndex)
    {
        var dict = team == Team.Left ? _leftActive : _rightActive;
        var toRemove = new List<int>();
        foreach (var kv in dict)
        {
            if (kv.Key == exceptIndex) continue;
            if (kv.Value.Move != null) StopCoroutine(kv.Value.Move);
            if (kv.Value.Item != null) Destroy(kv.Value.Item.gameObject);
            toRemove.Add(kv.Key);
        }
        foreach (var k in toRemove) dict.Remove(k);
    }

    void CheckBothWrong()
    {
        if (!WaitBothTeams && _leftAnsweredWrong && _rightAnsweredWrong)
            _onResult?.Invoke(false, Team.Left, Array.Empty<int>());
    }

    // ─── API song song với ButtonDisplay — để GenericGameController dùng CHUNG 1 đường gọi bất kể
    // display nào đang active ───────────────────────────────────────────────────────────────────

    /// <summary>Tương đương ButtonDisplay.GetActiveButtonByAnswerIndex — item ứng với answerIndex
    /// này (đang bay HOẶC đã đóng băng do vừa chạm), null nếu chưa spawn/đã bị dọn.</summary>
    public ButtonItem GetActiveButtonByAnswerIndex(Team team, int answerIndex)
    {
        var active = team == Team.Left ? _leftActive : _rightActive;
        return active.TryGetValue(answerIndex, out var live) ? live.Item : null;
    }

    /// <summary>Tương đương ButtonDisplay.ResetTeamAttempt — mở khoá lại 1 bên sau WrongFinal
    /// (wrongEndsRound=false): dọn sạch item đóng băng gây sai, validator mới tinh, RESUME spawn.</summary>
    public void ResetTeamAttempt(Team team)
    {
        var q = QuestionFor(team);
        if (q == null) return;
        bool isLeft = team == Team.Left;
        ClearActive(team);
        if (isLeft) { _leftFinalised = false; _leftAnsweredWrong = false; _leftValidator = new AnswerValidator(q); }
        else { _rightFinalised = false; _rightAnsweredWrong = false; _rightValidator = new AnswerValidator(q); }
        StartSide(team);
    }
}
