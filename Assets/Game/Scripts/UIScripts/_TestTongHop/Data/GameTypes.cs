using System;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum QuestionType { Choose, Matching }

public enum AnswerMode
{
    Single,           // 1 đáp án đúng
    MultiSelect,      // nhiều đáp án, không thứ tự
    OrderedSequence   // nhiều đáp án, đúng thứ tự
}

public enum QuestionMediaType { Text, Image, Audio, IconCompose }

public enum AnswerMediaType { Text, Image, IconCompose }

public enum ItemState { Normal, Selected, Correct, Wrong, Revealed, Locked }

/// <summary>Override display type per question. Auto = dùng weight trong config.</summary>
public enum ChooseDisplayMode { Auto, Button, Floating, SolarSystem, SolarSystemEn, PlanetOrder }

public enum ClickResult
{
    WrongFinal,        // sai, kết thúc (không cho retry)
    CorrectPartial,    // đúng nhưng chưa xong (multi/ordered)
    CorrectFinal,      // đúng và hoàn thành
    WrongInSequence,   // sai vị trí trong ordered → reset về đầu
    Deselected,        // MultiSelect: click lại ô đã chọn → bỏ chọn
    WrongPartial       // MultiSelect: chọn sai nhưng chưa đủ slot → vẫn cho deselect + retry
}

public enum Team { Left, Right }

public enum GameState { Idle, ShowQuestion, WaitAnswer, Feedback, GameOver }

// ─── Data Models ─────────────────────────────────────────────────────────────

[Serializable]
public class QuestionData
{
    public string id;
    public string topic;
    public int difficulty;           // 1–10 (khớp với cột difficulty trong CSV)
    public QuestionType questionType;

    // Đề bài
    public QuestionMediaType questionMediaType;
    public string questionMediaValue;

    // Đáp án (Choose)
    public AnswerMediaType  answerMediaType;
    public string[]         answers;          // tối đa 7 phần tử (a0–a6), rỗng = không hiện
    public AnswerMode       answerMode;
    public int[]            correctAnswers;
    public ChooseDisplayMode displayMode;     // Auto = theo weight config; Button/Floating = cố định

    // Đáp án (Matching) — left/right mỗi bên 3
    public string[]     leftItems;
    public string[]     rightItems;
    public int[]        correctPairs;    // correctPairs[leftIdx] = rightIdx
    public AnswerMediaType rightMediaType; // CSV col 13 (optional) — override cho right items
                                           // Mặc định = answerMediaType nếu không ghi
}

[Serializable]
public class IconComposeData
{
    public string spriteName;
    public int count;

    // Parse từ string "path/to/sprite:5"
    // Dùng LastIndexOf để path dài có nhiều ký tự không bị split sai.
    public static IconComposeData Parse(string raw)
    {
        int last = raw.LastIndexOf(':');
        if (last < 0)
            return new IconComposeData { spriteName = raw.Trim(), count = 1 };

        return new IconComposeData
        {
            spriteName = raw.Substring(0, last).Trim(),
            count      = int.TryParse(raw.Substring(last + 1).Trim(), out var n) ? n : 1
        };
    }
}

[Serializable]
public class LogEntry
{
    public string questionId;
    public string topic;
    public QuestionType questionType;
    public AnswerMode answerMode;
    public int[] playerAnswer;
    public bool isCorrect;
    public Team team;
    public float responseTimeSeconds;
    public string timestamp;
}
