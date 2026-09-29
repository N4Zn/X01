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
            _                         => ClickResult.WrongFinal
        };
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
