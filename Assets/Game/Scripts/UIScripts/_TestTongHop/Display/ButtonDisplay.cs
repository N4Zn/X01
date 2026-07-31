using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 2-player split: leftButtons (nửa trái) và rightButtons (nửa phải).
/// Mỗi bên có validator độc lập. Ai đúng trước → win round.
/// </summary>
public class ButtonDisplay : MonoBehaviour, IAnswerDisplay
{
    [SerializeField] ButtonItem[] leftButtons;   // 4-5 buttons phía trái màn hình
    [SerializeField] ButtonItem[] rightButtons;  // 4-5 buttons phía phải màn hình

    Action<bool, Team, int[]> _onResult;
    Action<Team>              _onPlayerFailed;
    Action<Team>              _onPartialCorrect;
    AnswerValidator           _leftValidator;
    AnswerValidator           _rightValidator;
    QuestionData              _current;
    bool                      _leftAnsweredWrong;
    bool                      _rightAnsweredWrong;
    // Khoá sau WrongFinal / CorrectFinal — tránh click tiếp sau khi đã submit
    bool                      _leftFinalised;
    bool                      _rightFinalised;

    // selectTime[answerIndex] = Time.time khi item được chọn (CorrectPartial)
    // Dùng để chặn deselect/re-click quá nhanh (chooseDeselectDelay).
    readonly System.Collections.Generic.Dictionary<int, float> _leftSelectTimes  = new();
    readonly System.Collections.Generic.Dictionary<int, float> _rightSelectTimes = new();

    // ── Independent play mode (từng player câu hỏi riêng) ───────────────────
    bool                      _isIndependent;
    QuestionData              _leftQuestionInd;
    QuestionData              _rightQuestionInd;
    Action<bool, Team, int[]> _leftOnDone;
    Action<bool, Team, int[]> _rightOnDone;

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void SetPartialCorrectCallback(Action<Team> cb) => _onPartialCorrect = cb;

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current             = q;
        _onResult            = onResult;
        _onPlayerFailed      = onPlayerFailed;
        _onPartialCorrect    = null;
        _leftValidator       = new AnswerValidator(q);
        _rightValidator      = new AnswerValidator(q);
        _leftAnsweredWrong   = false;
        _rightAnsweredWrong  = false;
        _leftFinalised       = false;
        _rightFinalised      = false;
        _leftSelectTimes.Clear();
        _rightSelectTimes.Clear();

        // Lọc đáp án không rỗng.
        // PickSlots đảm bảo đáp án đúng LUÔN có mặt trong danh sách nút bấm.
        var valid = BuildValidList(q);

        int[] leftOrder  = PickSlots(valid, leftButtons.Length,  q.correctAnswers);
        int[] rightOrder = PickSlots(valid, rightButtons.Length, q.correctAnswers);

        SetupGroup(leftButtons,  leftOrder,  valid, q, Team.Left);
        SetupGroup(rightButtons, rightOrder, valid, q, Team.Right);

        gameObject.SetActive(true);
    }

    public void HidePlayerAnswers(Team team)
    {
        var group = team == Team.Left ? leftButtons : rightButtons;
        foreach (var b in group) b.gameObject.SetActive(false);
    }

    /// <summary>
    /// Independent mode: setup button group của MỘT player, không ảnh hưởng player kia.
    /// Đảm bảo đáp án đúng luôn xuất hiện trong danh sách nút bấm.
    /// </summary>
    public void SetupPlayerIndependent(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        _isIndependent = true;
        bool isLeft    = team == Team.Left;

        if (isLeft)
        {
            _leftQuestionInd = q;
            _leftValidator   = new AnswerValidator(q);
            _leftFinalised   = false;
            _leftOnDone      = onDone;
            _leftSelectTimes.Clear();

            var valid     = BuildValidList(q);
            int[] order   = PickSlots(valid, leftButtons.Length, q.correctAnswers);
            SetupGroup(leftButtons, order, valid, q, Team.Left);
            LogButtonSetup(Team.Left, q, leftButtons);
        }
        else
        {
            _rightQuestionInd = q;
            _rightValidator   = new AnswerValidator(q);
            _rightFinalised   = false;
            _rightOnDone      = onDone;
            _rightSelectTimes.Clear();

            var valid     = BuildValidList(q);
            int[] order   = PickSlots(valid, rightButtons.Length, q.correctAnswers);
            SetupGroup(rightButtons, order, valid, q, Team.Right);
            LogButtonSetup(Team.Right, q, rightButtons);
        }

        gameObject.SetActive(true);
    }

    public void Cleanup()
    {
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        _isIndependent    = false;
        _leftQuestionInd  = null;
        _rightQuestionInd = null;
        _leftOnDone       = null;
        _rightOnDone      = null;
        foreach (var b in leftButtons)  b.SetState(ItemState.Normal);
        foreach (var b in rightButtons) b.SetState(ItemState.Normal);
        gameObject.SetActive(false);
    }

    // ─── Setup group ─────────────────────────────────────────────────────────

    // order[i] là index trong valid → valid[order[i]] là AnswerIndex thực tế
    void SetupGroup(ButtonItem[] group, int[] order,
                    System.Collections.Generic.List<int> valid, QuestionData q, Team team)
    {
        for (int i = 0; i < group.Length; i++)
        {
            if (i < order.Length)
            {
                int answerIdx = valid[order[i]];
                group[i].Setup(q.answers[answerIdx], q.answerMediaType, answerIdx, OnItemClicked, team);
                group[i].gameObject.SetActive(true);
            }
            else
            {
                group[i].gameObject.SetActive(false);
            }
        }
    }

    // ─── Click ────────────────────────────────────────────────────────────────

    void OnItemClicked(int answerIndex, Team team)
    {
        bool isLeft = team == Team.Left;
        if (isLeft ? _leftFinalised : _rightFinalised) return;

        // Resolve câu hỏi của player này (shared hoặc independent)
        var myQuestion = _isIndependent
            ? (isLeft ? _leftQuestionInd : _rightQuestionInd)
            : _current;
        if (myQuestion == null) return;

        var validator  = isLeft ? _leftValidator : _rightValidator;
        var myGroup    = isLeft ? leftButtons    : rightButtons;
        var theirGroup = isLeft ? rightButtons   : leftButtons;

        var result = validator.RegisterClick(answerIndex);
        GameLogger.Current?.LogClick(team, answerIndex, result);

        string clickedValue = (myQuestion?.answers != null && answerIndex >= 0 && answerIndex < myQuestion.answers.Length)
            ? myQuestion.answers[answerIndex] : "?";
        string correctDesc = myQuestion?.correctAnswers != null && myQuestion.answers != null
            ? string.Join(", ", System.Array.ConvertAll(myQuestion.correctAnswers,
                ca => $"idx{ca}=\"{(ca < myQuestion.answers.Length ? myQuestion.answers[ca] : "?")}\""))
            : "?";
        var clickedBtn = GetButton(isLeft ? leftButtons : rightButtons, answerIndex);
        int clickedSib = clickedBtn != null ? clickedBtn.transform.GetSiblingIndex() : -1;
        Debug.Log($"[C5:Click] {team} | q={myQuestion?.id} | clicked idx={answerIndex} value=\"{clickedValue}\" visual[{clickedSib}] | correct=[{correctDesc}] | result={result}");

        switch (result)
        {
            case ClickResult.WrongFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                if (myQuestion.answerMode == AnswerMode.OrderedSequence)
                    ApplyOrderedFinalState(myGroup, validator, answerIndex);
                else
                    ApplyMultiFinalState(myGroup, validator, myQuestion.correctAnswers);

                if (_isIndependent)
                {
                    // Independent: thông báo riêng cho player này, không ảnh hưởng bên kia
                    (isLeft ? _leftOnDone : _rightOnDone)?.Invoke(false, team, myQuestion.correctAnswers);
                }
                else
                {
                    if (isLeft) _leftAnsweredWrong = true; else _rightAnsweredWrong = true;
                    _onPlayerFailed?.Invoke(team);
                    CheckBothWrong();
                }
                break;

            case ClickResult.CorrectPartial:
            {
                var picked = GetButton(myGroup, answerIndex);
                if (myQuestion.answerMode == AnswerMode.OrderedSequence)
                {
                    picked?.SetState(ItemState.Correct);
                    picked?.Lock();
                }
                else // MultiSelect: ẩn button + +1 điểm + SFX, round tiếp tục
                {
                    if (picked != null) picked.gameObject.SetActive(false);
                    _onPartialCorrect?.Invoke(team);
                }
                break;
            }

            case ClickResult.CorrectFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                ApplyFinalState(myGroup, myQuestion.correctAnswers);

                if (_isIndependent)
                {
                    // Independent: KHÔNG khoá player kia, chỉ thông báo cho player này
                    (isLeft ? _leftOnDone : _rightOnDone)?.Invoke(true, team, myQuestion.correctAnswers);
                }
                else
                {
                    LockGroup(theirGroup);
                    _onResult?.Invoke(true, team, myQuestion.correctAnswers);
                }
                break;
        }
    }

    // OrderedSequence WrongFinal: đã chọn đúng → Correct, item gây fail → Wrong, còn lại → Locked
    void ApplyOrderedFinalState(ButtonItem[] group, AnswerValidator validator, int wrongIndex)
    {
        foreach (var b in group)
        {
            if (validator.IsSelected(b.AnswerIndex))
                b.SetState(ItemState.Correct);
            else if (b.AnswerIndex == wrongIndex)
                b.SetState(ItemState.Wrong);
            else
                b.SetState(ItemState.Locked);
        }
    }

    /// <summary>
    /// MultiSelect WrongFinal: hiện đúng/sai từng ô đã chọn, lock những ô chưa chọn.
    /// </summary>
    void ApplyMultiFinalState(ButtonItem[] group, AnswerValidator validator, int[] correctAnswers)
    {
        foreach (var b in group)
        {
            if (validator.IsSelected(b.AnswerIndex))
            {
                bool ok = System.Array.IndexOf(correctAnswers, b.AnswerIndex) >= 0;
                b.SetState(ok ? ItemState.Correct : ItemState.Wrong);
            }
            else
            {
                b.SetState(ItemState.Locked);
            }
        }
    }

    // ─── Both-wrong check ────────────────────────────────────────────────────

    void CheckBothWrong()
    {
        if (_leftAnsweredWrong && _rightAnsweredWrong)
            _onResult?.Invoke(false, Team.Left, System.Array.Empty<int>());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    void ApplyFinalState(ButtonItem[] group, int[] correctAnswers)
    {
        foreach (var b in group)
        {
            bool ok = System.Array.IndexOf(correctAnswers, b.AnswerIndex) >= 0;
            b.SetState(ok ? ItemState.Correct : ItemState.Locked);
        }
    }

    void LockGroup(ButtonItem[] group)
    {
        foreach (var b in group) b.SetState(ItemState.Locked);
    }

    ButtonItem GetButton(ButtonItem[] group, int answerIndex)
        => System.Array.Find(group, b => b.AnswerIndex == answerIndex);

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Tạo danh sách các answerIndex hợp lệ (không rỗng) từ QuestionData.</summary>
    static System.Collections.Generic.List<int> BuildValidList(QuestionData q)
    {
        var valid = new System.Collections.Generic.List<int>();
        if (q.answers == null) return valid;
        for (int i = 0; i < q.answers.Length; i++)
            if (!string.IsNullOrWhiteSpace(q.answers[i])) valid.Add(i);
        return valid;
    }

    /// <summary>
    /// Chọn min(valid.Count, take) indices từ valid, đảm bảo mọi đáp án đúng
    /// (correctAnswers) LUÔN có mặt trong kết quả. Các slot còn lại lấy ngẫu nhiên.
    /// Trả về mảng đã shuffle (vị trí của đáp án đúng ngẫu nhiên, không cố định).
    /// </summary>
    void LogButtonSetup(Team team, QuestionData q, ButtonItem[] group)
    {
        if (q?.answers == null || q.correctAnswers == null) return;
        string correctDesc = string.Join(", ", System.Array.ConvertAll(q.correctAnswers,
            ca => $"idx{ca}=\"{(ca < q.answers.Length ? q.answers[ca] : "?")}\""));
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < group.Length; i++)
        {
            var b = group[i];
            if (!b.gameObject.activeSelf) continue;
            int sib = b.transform.GetSiblingIndex();
            string val = b.AnswerIndex < q.answers.Length ? q.answers[b.AnswerIndex] : "?";
            var rt = b.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners); // [0]=BL [1]=TL [2]=TR [3]=BR  (ScreenOverlay = screen px)
            parts.Add($"arr[{i}]/sib[{sib}]=idx{b.AnswerIndex}\"{val}\"@x[{corners[0].x:F0}~{corners[2].x:F0}]y[{corners[0].y:F0}~{corners[1].y:F0}]");
        }
        Debug.Log($"[C5:Setup] {team} | q={q.id} | correct=[{correctDesc}] | buttons=[{string.Join(", ", parts)}]");
    }

    int[] PickSlots(System.Collections.Generic.List<int> valid, int take, int[] correctAnswers)
    {
        int n = valid.Count;
        take  = Mathf.Min(n, take);

        var included = new System.Collections.Generic.HashSet<int>(); // indices vào valid
        var result   = new System.Collections.Generic.List<int>(take);

        // 1. Ưu tiên đưa đáp án đúng vào trước
        if (correctAnswers != null)
        {
            foreach (int ca in correctAnswers)
            {
                int pos = valid.IndexOf(ca);
                if (pos >= 0 && included.Add(pos))
                {
                    result.Add(pos);
                    if (result.Count >= take) break;
                }
            }
        }

        // 2. Shuffle toàn bộ valid indices rồi lấy các phần tử chưa được chọn
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        foreach (int i in idx)
        {
            if (included.Contains(i)) continue;
            result.Add(i);
            if (result.Count >= take) break;
        }

        // 3. Shuffle lại result để vị trí đáp án đúng không cố định ở đầu
        var arr = result.ToArray();
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
