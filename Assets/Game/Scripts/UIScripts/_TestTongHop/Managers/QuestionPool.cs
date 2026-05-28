using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Giữ 2 pool riêng biệt: Choose và Matching.
/// AnswerDisplayManager gọi GetNextChoose() hoặc GetNextMatching() tuỳ weight đã chọn.
/// </summary>
public class QuestionPool : MonoBehaviour
{
    [Header("CSV Files")]
    [SerializeField] TextAsset chooseCSV;
    [SerializeField] TextAsset matchingCSV;

    // difficulty → TongHopConfig.Current.difficulty (gameconfig.json)

    // Hai pool riêng — shuffle độc lập
    readonly List<QuestionData> _choosePool   = new();
    readonly List<QuestionData> _matchingPool = new();
    int _chooseIdx;
    int _matchingIdx;

    void Awake() => BuildPool();

    public void SetDifficulty(int d)
    {
        TongHopConfig.Current.difficulty = d;
        BuildPool();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public bool HasChoose   => _choosePool.Count   > 0;
    public bool HasMatching => _matchingPool.Count > 0;

    public QuestionData GetNextChoose()
    {
        if (_choosePool.Count == 0) return null;
        if (_chooseIdx >= _choosePool.Count) { Shuffle(_choosePool); _chooseIdx = 0; }
        return _choosePool[_chooseIdx++];
    }

    public QuestionData GetNextMatching()
    {
        if (_matchingPool.Count == 0) return null;
        if (_matchingIdx >= _matchingPool.Count) { Shuffle(_matchingPool); _matchingIdx = 0; }
        return _matchingPool[_matchingIdx++];
    }

    public bool IsEmpty() => !HasChoose && !HasMatching;

    // ─── Build ────────────────────────────────────────────────────────────────

    void BuildPool()
    {
        _choosePool.Clear();
        _matchingPool.Clear();
        _chooseIdx = _matchingIdx = 0;

        // Difficulty từ config file (1–10) — có thể thay đổi mà không cần rebuild
        int diff = TongHopConfig.Current.difficulty;

        if (chooseCSV != null)
            _choosePool.AddRange(
                ParseChooseCSV(chooseCSV.text)
                    .Where(q => q.difficulty == diff));

        if (matchingCSV != null)
            _matchingPool.AddRange(
                ParseMatchingCSV(matchingCSV.text)
                    .Where(q => q.difficulty == diff));

        Shuffle(_choosePool);
        Shuffle(_matchingPool);
    }

    // ─── Parse Choose CSV ─────────────────────────────────────────────────────

    List<QuestionData> ParseChooseCSV(string raw)
    {
        var list  = new List<QuestionData>();
        var lines = raw.Split('\n');

        // Cột layout (tất cả backward-compatible):
        //   13 col (cũ):      …a0-a4, answerMode, correctAnswers
        //   14 col (cũ+disp): …a0-a4, answerMode, correctAnswers, displayMode
        //   15 col (mới):     …a0-a6, answerMode, correctAnswers
        //   16 col (mới+disp):…a0-a6, answerMode, correctAnswers, displayMode
        //
        // displayMode: Auto (rỗng) | Button | Floating
        // a5/a6 để trống → đáp án không hiện; a6 nếu có → hiện cố định tại tâm orbit

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = SplitCSVLine(line);
            if (cols.Length < 13) continue;

            bool extended = cols.Length >= 15;  // có cột a5, a6
            int  modeCol  = extended ? 13 : 11;
            int  corrCol  = extended ? 14 : 12;
            int  dispCol  = extended ? 15 : 13; // displayMode (optional)

            var answers = new string[7];
            for (int j = 0; j < 5; j++) answers[j] = cols[6 + j].Trim();
            if (extended)
            {
                answers[5] = cols[11].Trim();
                answers[6] = cols[12].Trim();
            }

            var qChoose = new QuestionData
            {
                id                 = cols[0].Trim(),
                topic              = cols[1].Trim(),
                difficulty         = ParseInt(cols[2], 1),
                questionType       = QuestionType.Choose,
                questionMediaType  = ParseEnum<QuestionMediaType>(cols[3]),
                questionMediaValue = cols[4].Trim(),
                answerMediaType    = ParseEnum<AnswerMediaType>(cols[5]),
                answers            = answers,
                answerMode         = ParseEnum<AnswerMode>(cols[modeCol]),
                correctAnswers     = ParseIntArray(cols[corrCol]),
                displayMode        = cols.Length > dispCol
                                     ? ParseEnum<ChooseDisplayMode>(cols[dispCol])
                                     : ChooseDisplayMode.Auto
            };
            AutoResolvePaths(qChoose);
            list.Add(qChoose);
        }
        return list;
    }

    // ─── Parse Matching CSV ───────────────────────────────────────────────────

    List<QuestionData> ParseMatchingCSV(string raw)
    {
        var list  = new List<QuestionData>();
        var lines = raw.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var cols = SplitCSVLine(line);
            if (cols.Length < 13) continue;

            var leftMedia  = ParseEnum<AnswerMediaType>(cols[5]);
            // Col 13 (optional): rightMediaType — khác left nếu 2 cột left/right khác type
            var rightMedia = (cols.Length > 13 && !string.IsNullOrEmpty(cols[13].Trim()))
                             ? ParseEnum<AnswerMediaType>(cols[13])
                             : leftMedia;

            var qMatch = new QuestionData
            {
                id                 = cols[0].Trim(),
                topic              = cols[1].Trim(),
                difficulty         = ParseInt(cols[2], 1),
                questionType       = QuestionType.Matching,
                questionMediaType  = ParseEnum<QuestionMediaType>(cols[3]),
                questionMediaValue = cols[4].Trim(),
                answerMediaType    = leftMedia,
                rightMediaType     = rightMedia,
                leftItems          = new[] { cols[6].Trim(), cols[7].Trim(), cols[8].Trim() },
                rightItems         = new[] { cols[9].Trim(), cols[10].Trim(), cols[11].Trim() },
                correctPairs       = ParseIntArray(cols[12])
            };
            AutoResolvePaths(qMatch);
            list.Add(qMatch);
        }
        return list;
    }

    // ─── Auto-path resolution ────────────────────────────────────────────────

    /// <summary>
    /// Sau khi parse xong một QuestionData, tự ghép "topic/path" cho mọi path
    /// ngắn (không chứa '/') trong question và answer.
    ///
    /// Quy tắc:
    ///   path không có '/' → "topic/path"
    ///   path đã có '/'    → giữ nguyên (backward-compat / đường dẫn tuyệt đối)
    ///
    /// IconCompose ("sprite:count,sprite:count,...") → xử lý từng segment.
    /// </summary>
    static void AutoResolvePaths(QuestionData q)
    {
        string topic = q.topic;

        // Question media (Image / Audio / IconCompose)
        if (q.questionMediaType != QuestionMediaType.Text)
            q.questionMediaValue = AutoPath(q.questionMediaValue, topic,
                q.questionMediaType == QuestionMediaType.IconCompose);

        // Answer media — Choose (Image / IconCompose)
        if (q.answers != null && q.answerMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.answers.Length; i++)
                q.answers[i] = AutoPath(q.answers[i], topic,
                    q.answerMediaType == AnswerMediaType.IconCompose);

        // Answer media — Matching left items
        if (q.leftItems != null && q.answerMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.leftItems.Length; i++)
                q.leftItems[i] = AutoPath(q.leftItems[i], topic,
                    q.answerMediaType == AnswerMediaType.IconCompose);

        // Answer media — Matching right items
        if (q.rightItems != null && q.rightMediaType != AnswerMediaType.Text)
            for (int i = 0; i < q.rightItems.Length; i++)
                q.rightItems[i] = AutoPath(q.rightItems[i], topic,
                    q.rightMediaType == AnswerMediaType.IconCompose);
    }

    /// <summary>
    /// Ghép "topic/value" nếu value chưa có '/'.
    /// Với IconCompose: từng segment "path:count" được xử lý riêng.
    /// </summary>
    static string AutoPath(string value, string topic, bool isIconCompose = false)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(topic)) return value;

        if (!isIconCompose)
            return value.Contains('/') ? value : $"{topic}/{value}";

        // IconCompose: "sprite:count,sprite:count,..."
        var parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            var seg   = parts[i].Trim();
            int colon = seg.LastIndexOf(':');
            if (colon > 0)
            {
                string p   = seg.Substring(0, colon);
                string cnt = seg.Substring(colon);            // ":N"
                parts[i]   = (p.Contains('/') ? p : $"{topic}/{p}") + cnt;
            }
            else
            {
                parts[i] = seg.Contains('/') ? seg : $"{topic}/{seg}";
            }
        }
        return string.Join(",", parts);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    void Shuffle(List<QuestionData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    string[] SplitCSVLine(string line)
    {
        var result  = new List<string>();
        bool inQ    = false;
        var cur     = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { result.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }
        result.Add(cur.ToString());
        return result.ToArray();
    }

    int ParseInt(string raw, int defaultVal = 1) =>
        int.TryParse(raw.Trim(), out var v) ? v : defaultVal;

    int[] ParseIntArray(string raw) =>
        raw.Trim().Split(',').Select(s => int.Parse(s.Trim())).ToArray();

    T ParseEnum<T>(string raw) where T : struct =>
        System.Enum.TryParse<T>(raw.Trim(), out var v) ? v : default;
}
