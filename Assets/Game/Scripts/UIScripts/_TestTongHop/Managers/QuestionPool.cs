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

    void Awake()
    {
        // ── Dynamic loading theo SelectedGameName ─────────────────────────────
        // Nếu MenuScene đã set GameSessionManager.SelectedGameName (vd: "ChuCai"),
        // load CSV từ Resources/TongHop/{variant}/choose  và  .../matching
        // Nếu không có file trong Resources → giữ nguyên SerializeField đã gán trong Inspector
        string variant = GameSessionManager.Instance != null
            ? GameSessionManager.Instance.SelectedGameName : "";

        if (!string.IsNullOrEmpty(variant))
        {
            var dynChoose   = Resources.Load<TextAsset>($"TongHop/{variant}/choose");
            var dynMatching = Resources.Load<TextAsset>($"TongHop/{variant}/matching");

            if (dynChoose == null && dynMatching == null)
            {
                Debug.LogWarning($"[QuestionPool] Không tìm thấy CSV cho variant '{variant}' " +
                                 $"tại Resources/TongHop/{variant}/choose(.txt/.csv). " +
                                 "Dùng TextAsset đã gán trong Inspector (nếu có).");
                // KHÔNG override imageRoot — giữ nguyên config gốc ("TestTongHop/images")
                // để game gốc (TestTongHop) vẫn load sprite đúng đường dẫn.
            }
            else
            {
                // Variant có ít nhất 1 file CSV → complete override:
                // CSV không có trong variant folder thì clear về null
                // (tránh Inspector-assigned CSV của scene mặc định bị giữ lại).
                chooseCSV   = dynChoose;    // null nếu variant không có choose
                matchingCSV = dynMatching;  // null nếu variant không có matching

                Debug.Log($"[QuestionPool] Loaded variant '{variant}'" +
                          $" | choose={dynChoose != null} | matching={dynMatching != null}");

                // Override imageRoot / audioRoot CHỈ KHI có file CSV trong Resources.
                //
                // Ví dụ variant = "Counting":
                //   imageRoot = "Counting"
                //   CSV "T-Rex" → AutoResolvePaths → "Counting/T-Rex"
                //   Resolve("Counting/T-Rex", "Counting") → đã có prefix → giữ nguyên
                //   Resources.Load("Counting/T-Rex") → Assets/Resources/Counting/T-Rex.png ✓
                TongHopConfig.Current.imageRoot = variant;
                TongHopConfig.Current.audioRoot = variant;
            }

            // ── Variant-specific config override (luôn chạy nếu variant != "") ────
            // Load Resources/TongHop/{variant}/config.json nếu có,
            // merge vào TongHopConfig.Current (JsonUtility.FromJsonOverwrite chỉ ghi đè
            // các field có trong JSON, giữ nguyên phần còn lại của global config).
            // Ví dụ: Resources/TongHop/Counting/config.json có "independentPlay": true
            //         → chỉ Counting dùng independent play, các variant khác không bị ảnh hưởng.
            var variantCfg = Resources.Load<TextAsset>($"TongHop/{variant}/config");
            if (variantCfg != null)
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(variantCfg.text, TongHopConfig.Current);
                    Debug.Log($"[QuestionPool] Applied variant config for '{variant}'");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[QuestionPool] Variant config parse error: {e.Message}");
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────

        BuildPool();
    }

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

        List<QuestionData> allChoose   = chooseCSV   != null ? ParseChooseCSV(chooseCSV.text)     : new List<QuestionData>();
        List<QuestionData> allMatching = matchingCSV != null ? ParseMatchingCSV(matchingCSV.text) : new List<QuestionData>();

        _choosePool.AddRange(allChoose.Where(q => q.difficulty == diff));
        _matchingPool.AddRange(allMatching.Where(q => q.difficulty == diff));

        // Fallback: nếu không có câu nào khớp difficulty → dùng toàn bộ CSV (bất kể difficulty)
        // Tránh pool rỗng khi giá trị difficulty trong config không khớp với CSV.
        if (_choosePool.Count == 0 && allChoose.Count > 0)
        {
            Debug.LogWarning($"[QuestionPool] Không có câu Choose nào ở difficulty={diff} — dùng toàn bộ {allChoose.Count} câu.");
            _choosePool.AddRange(allChoose);
        }
        if (_matchingPool.Count == 0 && allMatching.Count > 0)
        {
            Debug.LogWarning($"[QuestionPool] Không có câu Matching nào ở difficulty={diff} — dùng toàn bộ {allMatching.Count} câu.");
            _matchingPool.AddRange(allMatching);
        }

        Shuffle(_choosePool);
        Shuffle(_matchingPool);

        Debug.Log($"[QuestionPool] BuildPool — diff={diff} | choose={_choosePool.Count} | matching={_matchingPool.Count}");
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
            AutoFillIconCompose(qChoose);
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

    int[] ParseIntArray(string raw)
    {
        var s = raw.Trim();
        if (string.IsNullOrEmpty(s)) return System.Array.Empty<int>();
        return s.Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => int.TryParse(x.Trim(), out var v) ? v : 0)
                .ToArray();
    }

    T ParseEnum<T>(string raw) where T : struct =>
        System.Enum.TryParse<T>(raw.Trim(), out var v) ? v : default;

    // ─── Auto-fill IconCompose answers ────────────────────────────────────────

    /// <summary>
    /// Nếu câu hỏi là IconCompose và answers/correctAnswers bị bỏ trống trong CSV,
    /// tự tính:
    ///   1. Tổng số icon (total) từ "sprite:N" trong questionMediaValue.
    ///   2. 4 đáp án xoay quanh total (ví dụ total=3 → 1,2,3,4).
    ///   3. correctAnswers = index của total trong mảng answers.
    ///
    /// Cho phép CSV chỉ cần viết:
    ///   RANDOM:3 ,,,,  Single,, Button
    /// mà không cần điền đáp án hay chỉ số đáp án đúng.
    /// </summary>
    static void AutoFillIconCompose(QuestionData q)
    {
        if (q.questionMediaType != QuestionMediaType.IconCompose) return;

        // ── Auto-fill TẠM THỜI BỊ COMMENT — correctAnswers lấy thẳng từ CSV ──
        // Khi muốn bật lại: bỏ comment toàn bộ khối bên dưới.
        // Hiển thị ảnh random con vật (QuestionMediaDisplay) không bị ảnh hưởng.

        /*
        // ── 1. Tính tổng icon từ questionMediaValue ──────────────────────────
        //    Gọi sau AutoResolvePaths nên value đã có prefix, vd "Counting/RANDOM:3"
        int total = 0;
        foreach (var seg in q.questionMediaValue.Split(','))
        {
            var t = seg.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            int colon = t.LastIndexOf(':');
            if (colon > 0 && int.TryParse(t.Substring(colon + 1), out int n))
                total += n;
        }
        if (total <= 0) return;

        // ── 2. Nếu answers rỗng → sinh 4 lựa chọn quanh total ───────────────
        bool answersEmpty = q.answers == null ||
                            q.answers.All(string.IsNullOrEmpty);
        if (answersEmpty)
        {
            int[] choices = BuildDistractors(total, 4);
            q.answers = new string[7];
            for (int i = 0; i < choices.Length; i++)
                q.answers[i] = choices[i].ToString();
        }

        // ── 3. LUÔN tính lại correctAnswers từ total ─────────────────────────
        //    Bỏ qua giá trị CSV cũ — đảm bảo đáp án khớp số icon thực tế.
        //    Tìm index của total trong answers hiện tại.
        for (int i = 0; i < q.answers.Length; i++)
        {
            if (int.TryParse(q.answers[i], out int v) && v == total)
            {
                q.correctAnswers = new[] { i };
                return;
            }
        }

        // ── 4. total không có trong answers (vd: CSV cũ có [1,2,3,4] nhưng total=10)
        //    → sinh lại answers mới bao gồm total, rồi tính correctAnswers.
        {
            int[] choices = BuildDistractors(total, 4);
            q.answers = new string[7];
            for (int i = 0; i < choices.Length; i++)
                q.answers[i] = choices[i].ToString();

            for (int i = 0; i < q.answers.Length; i++)
            {
                if (int.TryParse(q.answers[i], out int v) && v == total)
                {
                    q.correctAnswers = new[] { i };
                    return;
                }
            }
        }
        */
    }

    /// <summary>
    /// Tạo mảng <paramref name="count"/> số nguyên dương, luôn chứa
    /// <paramref name="correct"/>, các số còn lại là lân cận (≥ 1),
    /// được sắp xếp tăng dần.
    /// Ví dụ: correct=3, count=4 → [1, 2, 3, 4]
    ///         correct=1, count=4 → [1, 2, 3, 4]
    ///         correct=12,count=4 → [10,11,12,13]
    /// </summary>
    static int[] BuildDistractors(int correct, int count)
    {
        var set = new System.Collections.Generic.HashSet<int> { correct };
        int offset = 1;
        while (set.Count < count)
        {
            if (correct - offset >= 1) set.Add(correct - offset);
            if (set.Count < count)     set.Add(correct + offset);
            offset++;
        }
        var arr = set.ToArray();
        System.Array.Sort(arr);
        return arr;
    }
}
