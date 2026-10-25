using System.Collections.Generic;

public class AnswerValidator
{
    readonly QuestionData _q;
    readonly HashSet<int> _correctSet;

    // Dùng cho MultiSelect và OrderedSequence
    readonly List<int> _selected = new();
    int _sequenceStep = 0;

    public AnswerValidator(QuestionData q)
    {
        _q          = q;
        _correctSet = new HashSet<int>(q.correctAnswers);
    }

    public ClickResult RegisterClick(int index)
    {
        return _q.answerMode switch
        {
            AnswerMode.Single          => ValidateSingle(index),
            AnswerMode.MultiSelect     => ValidateMulti(index),
            AnswerMode.OrderedSequence => ValidateOrdered(index),
            AnswerMode.SumToTarget     => ValidateSum(index),
            _                         => ClickResult.WrongFinal
        };
    }

    // ─── SumToTarget ──────────────────────────────────────────────────────────
    //
    // Luật: mọi đáp án bấm được (không có "đáp án đúng/sai" riêng) — mỗi lần bấm cộng giá trị
    // sumValues[index] (thiếu/≤0 → 1) vào tổng. Tổng == sumTarget → CorrectFinal; tổng < → CorrectPartial
    // (ẩn item, round tiếp tục); tổng > → WrongFinal. Tạo validator MỚI = chọn lại từ đầu
    // (ButtonDisplay.ResetTeamAttempt).

    int _sumTotal;

    ClickResult ValidateSum(int index)
    {
        int v = (_q.sumValues != null && index >= 0 && index < _q.sumValues.Length) ? _q.sumValues[index] : 1;
        _sumTotal += v > 0 ? v : 1;
        _selected.Add(index);
        if (_sumTotal == _q.sumTarget) return ClickResult.CorrectFinal;
        if (_sumTotal > _q.sumTarget) return ClickResult.WrongFinal;
        // sumWrongEnds: còn chọn tiếp cũng không thể đủ (kể cả hết đáp án) → sai luôn, khỏi chờ chạm thanh chọn lại.
        return _q.sumWrongEnds && !CanStillReachSum() ? ClickResult.WrongFinal : ClickResult.CorrectPartial;
    }

    /// <summary>Có tập con đáp án CHƯA chọn nào cộng đúng phần còn thiếu (target − tổng hiện tại) không.</summary>
    bool CanStillReachSum()
    {
        int need = _q.sumTarget - _sumTotal;
        if (need < 0) return false;
        var can = new bool[need + 1];
        can[0] = true;
        int n = _q.sumValues != null ? _q.sumValues.Length : 0;
        for (int i = 0; i < n; i++)
        {
            if (_selected.Contains(i)) continue;
            int v = _q.sumValues[i] > 0 ? _q.sumValues[i] : 1;
            for (int s = need; s >= v; s--) if (can[s - v]) can[s] = true;
        }
        return can[need];
    }

    // ─── Single ───────────────────────────────────────────────────────────────

    ClickResult ValidateSingle(int index)
        => _correctSet.Contains(index) ? ClickResult.CorrectFinal : ClickResult.WrongFinal;

    // ─── MultiSelect ─────────────────────────────────────────────────────────
    //
    // Luật:
    //   - Sai → WrongFinal: kết thúc round ngay.
    //   - Đúng nhưng chưa hết → CorrectPartial: +1 điểm, ẩn item, round tiếp tục.
    //   - Đúng và là cái cuối cùng → CorrectFinal: kết thúc round.
    //   Không có toggle/deselect, không có retry.

    ClickResult ValidateMulti(int index)
    {
        if (!_correctSet.Contains(index))
            return ClickResult.WrongFinal;

        _selected.Add(index);
        return _selected.Count == _correctSet.Count
            ? ClickResult.CorrectFinal
            : ClickResult.CorrectPartial;
    }

    // ─── OrderedSequence ──────────────────────────────────────────────────────

    ClickResult ValidateOrdered(int index)
    {
        int expected = _q.correctAnswers[_sequenceStep];

        if (index != expected)
            return ClickResult.WrongFinal;   // sai thứ tự → fail luôn, không retry

        _selected.Add(index);
        _sequenceStep++;

        return _sequenceStep == _q.correctAnswers.Length
            ? ClickResult.CorrectFinal
            : ClickResult.CorrectPartial;
    }

    // ─── Query helpers ────────────────────────────────────────────────────────

    public bool IsSelected(int index) => _selected.Contains(index);
    public int  SequenceStep          => _sequenceStep;
    public int  ExpectedNext          => _sequenceStep < _q.correctAnswers.Length
                                         ? _q.correctAnswers[_sequenceStep] : -1;
}
