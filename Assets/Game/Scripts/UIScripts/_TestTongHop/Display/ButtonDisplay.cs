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

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current             = q;
        _onResult            = onResult;
        _onPlayerFailed      = onPlayerFailed;
        _leftValidator       = new AnswerValidator(q);
        _rightValidator      = new AnswerValidator(q);
        _leftAnsweredWrong   = false;
        _rightAnsweredWrong  = false;
        _leftFinalised       = false;
        _rightFinalised      = false;
        _leftSelectTimes.Clear();
        _rightSelectTimes.Clear();

        // Lọc đáp án không rỗng, shuffle toàn bộ trước rồi lấy min(valid, buttons)
        // → đảm bảo mọi đáp án đều có cơ hội xuất hiện (không bị bỏ cố định)
        var valid = new System.Collections.Generic.List<int>();
        for (int i = 0; i < q.answers.Length; i++)
            if (!string.IsNullOrWhiteSpace(q.answers[i])) valid.Add(i);

        int[] leftOrder  = TakeShuffled(valid, leftButtons.Length);
        int[] rightOrder = TakeShuffled(valid, rightButtons.Length);

        SetupGroup(leftButtons,  leftOrder,  valid, q, Team.Left);
        SetupGroup(rightButtons, rightOrder, valid, q, Team.Right);

        gameObject.SetActive(true);
    }

    public void HidePlayerAnswers(Team team)
    {
        var group = team == Team.Left ? leftButtons : rightButtons;
        foreach (var b in group) b.gameObject.SetActive(false);
    }

    public void Cleanup()
    {
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
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
                group[i].gameObject.SetActive(true);
                group[i].Setup(q.answers[answerIdx], q.answerMediaType, answerIdx, OnItemClicked, team);
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
        bool        isLeft      = team == Team.Left;
        // Khoá hoàn toàn sau khi đã submit (WrongFinal / CorrectFinal)
        if (isLeft ? _leftFinalised : _rightFinalised) return;

        var         validator   = isLeft ? _leftValidator   : _rightValidator;
        var         myGroup     = isLeft ? leftButtons       : rightButtons;
        var         theirGroup  = isLeft ? rightButtons      : leftButtons;
        var         selectTimes = isLeft ? _leftSelectTimes  : _rightSelectTimes;

        // ── Interact guard (chống bấm nhầm nhanh) — chỉ áp dụng cho MultiSelect ──
        if (_current.answerMode == AnswerMode.MultiSelect)
        {
            float t = selectTimes.TryGetValue(answerIndex, out var st) ? st : 0f;
            if (Time.time - t < TongHopConfig.Current.chooseDeselectDelay) return;
        }

        var result = validator.RegisterClick(answerIndex);
        GameLogger.Current?.LogClick(team, answerIndex, result);

        switch (result)
        {
            case ClickResult.WrongPartial:
                // Xám — không hé lộ sai, vẫn deselect được sau delay
                GetButton(myGroup, answerIndex)?.SetChosen();
                selectTimes[answerIndex] = Time.time;
                break;

            case ClickResult.WrongFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                if (_current.answerMode == AnswerMode.OrderedSequence)
                    ApplyOrderedFinalState(myGroup, validator, answerIndex);
                else
                    ApplyMultiFinalState(myGroup, validator, _current.correctAnswers);
                if (isLeft) _leftAnsweredWrong  = true;
                else        _rightAnsweredWrong = true;
                _onPlayerFailed?.Invoke(team);
                CheckBothWrong();
                break;

            case ClickResult.CorrectPartial:
            {
                var picked = GetButton(myGroup, answerIndex);
                if (_current.answerMode == AnswerMode.OrderedSequence)
                {
                    picked?.SetState(ItemState.Correct);  // xanh ngay
                    picked?.Lock();                        // khoá — không click lại
                }
                else
                {
                    picked?.SetChosen();                   // xám — chưa hé lộ đúng/sai
                }
                selectTimes[answerIndex] = Time.time;
                break;
            }

            case ClickResult.Deselected:
                GetButton(myGroup, answerIndex)?.SetState(ItemState.Normal);
                selectTimes[answerIndex] = Time.time;   // chặn re-select quá nhanh
                break;

            case ClickResult.CorrectFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                ApplyFinalState(myGroup, _current.correctAnswers);
                LockGroup(theirGroup);
                _onResult?.Invoke(true, team, _current.correctAnswers);
                break;

            // WrongInSequence không còn dùng — ValidateOrdered trả WrongFinal trực tiếp
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

    /// <summary>
    /// Shuffle toàn bộ valid indices rồi lấy min(valid.Count, take) phần tử đầu.
    /// Đảm bảo mọi đáp án đều có xác suất xuất hiện như nhau khi count > take.
    /// </summary>
    int[] TakeShuffled(System.Collections.Generic.List<int> valid, int take)
    {
        int n   = valid.Count;
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        // Lấy đúng min(n, take) phần tử đầu sau khi shuffle
        int count = Mathf.Min(n, take);
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = idx[i];
        return result;
    }
}
