using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Parser CSV thuần (không MonoBehaviour, không phụ thuộc TongHopConfig/GameSessionManager) —
/// lift từ QuestionPool.cs (_TestTongHop) để dùng chung cho mọi mini-game mới.
///
/// Cột layout giống hệt CSV của TestTongHopGame để tái dùng được kinh nghiệm/tooling đã có:
///   Choose:   id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,a0..a4[,a5,a6],answerMode,correctAnswers[,displayMode]
///   Matching: id,topic,difficulty,questionMediaType,questionMediaValue,answerMediaType,left0-2,right0-2,correctPairs[,rightMediaType]
/// </summary>
public static class CsvQuestionLoader
{
    public static List<QuestionData> ParseChoose(string raw)
    {
        var list = new List<QuestionData>();
        var lines = raw.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Length < 13) continue;

            bool extended = cols.Length >= 15; // có cột a5, a6
            int modeCol = extended ? 13 : 11;
            int corrCol = extended ? 14 : 12;
            int dispCol = extended ? 15 : 13; // displayMode (optional)

            var answers = new string[7];
            for (int j = 0; j < 5; j++) answers[j] = cols[6 + j].Trim();
            if (extended)
            {
                answers[5] = cols[11].Trim();
                answers[6] = cols[12].Trim();
            }

            var q = new QuestionData
            {
                id = cols[0].Trim(),
                topic = cols[1].Trim(),
                difficulty = ParseInt(cols[2], 1),
                questionType = QuestionType.Choose,
                questionMediaType = ParseEnum<QuestionMediaType>(cols[3]),
                questionMediaValue = cols[4].Trim(),
                answerMediaType = ParseEnum<AnswerMediaType>(cols[5]),
                answers = answers,
                answerMode = ParseEnum<AnswerMode>(cols[modeCol]),
                correctAnswers = ParseIntArray(cols[corrCol]),
                displayMode = cols.Length > dispCol
                    ? ParseEnum<ChooseDisplayMode>(cols[dispCol])
                    : ChooseDisplayMode.Auto
            };
            AutoResolvePaths(q);
            list.Add(q);
        }
        return list;
    }

    public static List<QuestionData> ParseMatching(string raw)
    {
        var list = new List<QuestionData>();
        var lines = raw.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Length < 13) continue;

            var leftMedia = ParseEnum<AnswerMediaType>(cols[5]);
            var rightMedia = (cols.Length > 13 && !string.IsNullOrEmpty(cols[13].Trim()))
                ? ParseEnum<AnswerMediaType>(cols[13])
                : leftMedia;

            var q = new QuestionData
            {
                id = cols[0].Trim(),
                topic = cols[1].Trim(),
                difficulty = ParseInt(cols[2], 1),
                questionType = QuestionType.Matching,
                questionMediaType = ParseEnum<QuestionMediaType>(cols[3]),
                questionMediaValue = cols[4].Trim(),
                answerMediaType = leftMedia,
                rightMediaType = rightMedia,
                leftItems = new[] { cols[6].Trim(), cols[7].Trim(), cols[8].Trim() },
                rightItems = new[] { cols[9].Trim(), cols[10].Trim(), cols[11].Trim() },
                correctPairs = ParseIntArray(cols[12])
            };
            AutoResolvePaths(q);
            list.Add(q);
        }
        return list;
    }

    // ─── Auto-path resolution (giống QuestionPool) ───────────────────────────

    static void AutoResolvePaths(QuestionData q)
    {
        string topic = q.topic;

        if (q.questionMediaType != QuestionMediaType.Text)
            q.questionMediaValue = AutoPath(q.questionMediaValue, topic,
                q.questionMediaType == QuestionMediaType.IconCompose);

        if (q.answers != null && q.answerMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.answers.Length; i++)
                q.answers[i] = AutoPath(q.answers[i], topic,
                    q.answerMediaType == AnswerMediaType.IconCompose);

        if (q.leftItems != null && q.answerMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.leftItems.Length; i++)
                q.leftItems[i] = AutoPath(q.leftItems[i], topic,
                    q.answerMediaType == AnswerMediaType.IconCompose);

        if (q.rightItems != null && q.rightMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.rightItems.Length; i++)
                q.rightItems[i] = AutoPath(q.rightItems[i], topic,
                    q.rightMediaType == AnswerMediaType.IconCompose);
    }

    static string AutoPath(string value, string topic, bool isIconCompose = false)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(topic)) return value;

        if (!isIconCompose)
            return value.Contains('/') ? value : $"{topic}/{value}";

        var parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            var seg = parts[i].Trim();
            int colon = seg.LastIndexOf(':');
            if (colon > 0)
            {
                string p = seg.Substring(0, colon);
                string cnt = seg.Substring(colon);
                parts[i] = (p.Contains('/') ? p : $"{topic}/{p}") + cnt;
            }
            else
            {
                parts[i] = seg.Contains('/') ? seg : $"{topic}/{seg}";
            }
        }
        return string.Join(",", parts);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQ = false;
        var cur = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { result.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }
        result.Add(cur.ToString());
        return result.ToArray();
    }

    static int ParseInt(string raw, int defaultVal = 1) =>
        int.TryParse(raw.Trim(), out var v) ? v : defaultVal;

    static int[] ParseIntArray(string raw)
    {
        var s = raw.Trim();
        if (string.IsNullOrEmpty(s)) return System.Array.Empty<int>();
        return s.Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => int.TryParse(x.Trim(), out var v) ? v : 0)
                .ToArray();
    }

    static T ParseEnum<T>(string raw) where T : struct =>
        System.Enum.TryParse<T>(raw.Trim(), out var v) ? v : default;
}
