using System.Collections.Generic;
using System.Linq;

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
    // Luật mới:
    //   - Chọn đúng / sai đều được, miễn là số đã chọn < số đáp án đúng.
    //   - Sai → WrongPartial: hiện màu đỏ nhưng vẫn cho deselect và thử lại.
    //   - Khi đã chọn đủ slot (= correctCount) → submit:
    //       tất cả đúng → CorrectFinal
    //       có sai       → WrongFinal (khoá, không retry nữa)

    ClickResult ValidateMulti(int index)
    {
        // Toggle off — cho phép bỏ chọn bất cứ lúc nào trước khi submit
        if (_selected.Contains(index))
        {
            _selected.Remove(index);
            return ClickResult.Deselected;
        }

        _selected.Add(index);
        bool thisCorrect = _correctSet.Contains(index);

        // Chưa đủ slot → chưa submit, cho phép thay đổi
        if (_selected.Count < _correctSet.Count)
            return thisCorrect ? ClickResult.CorrectPartial : ClickResult.WrongPartial;

        // Đủ slot → submit ngay
        bool allCorrect = _selected.All(i => _correctSet.Contains(i));
        return allCorrect ? ClickResult.CorrectFinal : ClickResult.WrongFinal;
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
