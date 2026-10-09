using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// "Tổng hợp Toán" — 1 scene, tự sinh câu hỏi nối tiếp theo từng đoạn (mỗi đoạn 3 câu), rồi random
/// cộng/trừ/điền dấu cho tới hết giờ (không giới hạn số câu, chỉ giới hạn bởi GameSettings.GameTime).
///
///   1) Nhận biết số   — nghe âm thanh (Audio/SoDem/N) → chọn chữ số   (CÙNG số/âm thanh 2 bên, đáp án nhiễu KHÁC nhau)
///   2) Đếm (dễ)       — đếm 1-5 con vật                                (như Counting5)
///   3) Đếm            — đếm 6-10 con vật (không lặp lại phạm vi 5)     (như Counting)
///   4) Cộng chữ số    — A + B = ?  (chỉ ẩn kết quả)                    (như AddNumberDigit)
///   5) Trừ chữ số     — A - B = ?  (chỉ ẩn kết quả), phạm vi 10        (như SubNumberDigit)
///   6) Điền dấu       — A ? B → chọn &lt; = &gt;                            (như SoSanhSoChuSo)
///   7+) Random cộng / trừ / điền dấu
///
/// Combined mode + WaitForBothTeams: 2 đội cùng nhịp (cả 2 xong mới sang câu), mỗi đội ghi điểm riêng.
/// Từ đoạn 2 trở đi MỖI BÊN CÓ CÂU RIÊNG (đáp án đúng 2 bên luôn khác nhau → không chép được của nhau); chỉ câu
/// "nhận biết số" (cùng audio) hai bên giống nhau nhưng đáp án nhiễu khác. Hiển thị: mỗi nửa 1 QuestionPanel
/// (bố cục ô A/B/C + đáp án lấy theo AddNumberGame); đáp án dùng ButtonDisplay.SetupPerTeam của Kit.
/// </summary>
public class TongHopToanController : MiniGameControllerBase
{
    /// <summary>Phần câu hỏi của MỘT nửa màn hình — SceneBuilder dựng, controller chỉ bật/tắt + đổi chữ.</summary>
    [Serializable]
    public class QuestionPanel
    {
        public GameObject[] slots = new GameObject[3];                 // ô A, B, C (box_green)
        public TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[3];
        public GameObject[] hidden = new GameObject[3];                // thẻ vàng "?" phủ lên ô bị ẩn
        public TextMeshProUGUI opLabel;                                // + / -
        public TextMeshProUGUI equalsLabel;                            // =
        public RectTransform iconGrid;                                 // lưới icon cho câu đếm
    }

    enum Kind { Recognize, CountEasy, Count, Add, Sub, Sign }

    /// <summary>Câu hỏi của MỘT bên (0 = trái, 1 = phải).</summary>
    class TeamQ
    {
        public QuestionData q;
        public int value;                          // Recognize: số cần nghe · Count*: số icon
        public int answerKey;                      // giá trị đáp án đúng — dùng để ép 2 bên khác nhau
        public readonly string[] parts = new string[3];   // chữ trên ô A, B, C (ô bị ẩn để rỗng)
        public int hiddenSlot = -1;
        public string op = "+";
        public Sprite animal;
    }

    // Thứ tự cố định 6 đoạn × 3 câu; sau đó random trong RandomKinds.
    const int QuestionsPerSegment = 3;
    static readonly Kind[] Segments = { Kind.Recognize, Kind.CountEasy, Kind.Count, Kind.Add, Kind.Sub, Kind.Sign };
    static readonly Kind[] RandomKinds = { Kind.Add, Kind.Sub, Kind.Sign };

    const string AudioFolder = "Audio/SoDem/";
    const float AudioRepeatPause = 3f;

    // Hàng đáp án (theo panel AddNumberGame): ô cao, cách đều, căn giữa nửa màn hình.
    const float AnswerW = 0.23f, AnswerGap = 0.02f, AnswerYMin = 0.05f, AnswerYMax = 0.46f;

    [Header("TongHopToan — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] ButtonItem[] leftAnswerButtons;
    [SerializeField] ButtonItem[] rightAnswerButtons;
    [SerializeField] Button backButton;
    [SerializeField] QuestionPanel leftPanel;
    [SerializeField] QuestionPanel rightPanel;
    [SerializeField] AudioSource audioSource;

    int _questionNumber;          // số câu đã phát ra (0-based, dùng chọn đoạn)
    Kind _currentKind;
    readonly TeamQ[] _tq = { new TeamQ(), new TeamQ() };   // [0] trái, [1] phải
    PerTeamDisplay _display;
    int _teamsDone;
    bool _audioLoopOn;
    Coroutine _audioLoop;

    protected override void Start()
    {
        _display = new PerTeamDisplay(buttonDisplay, _tq);   // trước base.Start(): FSM có thể gọi GetDisplayForQuestion
        base.Start();
        if (buttonDisplay != null) buttonDisplay.WaitBothTeams = WaitBothActive;
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        ClearQuestionVisuals();
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => _display;

    // Kết hợp nhưng KHÔNG ai thắng trước: mỗi đội trả lời xong câu của mình, cả 2 xong mới sang câu mới.
    protected override bool WaitForBothTeams => true;

    // Log round đúng câu của từng bên (CurrentQuestion chỉ là câu của bên trái).
    protected override QuestionData QuestionForTeam(Team team, QuestionData shared)
        => team == Team.Right && _tq[1].q != null ? _tq[1].q : shared;

    /// <summary>Cầu nối: base gọi Setup(1 câu chung), ta chuyển thành 2 câu riêng cho ButtonDisplay.</summary>
    sealed class PerTeamDisplay : IAnswerDisplay
    {
        readonly ButtonDisplay _inner;
        readonly TeamQ[] _tq;
        public PerTeamDisplay(ButtonDisplay inner, TeamQ[] tq) { _inner = inner; _tq = tq; }

        public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
            => _inner.SetupPerTeam(_tq[0].q, _tq[1].q, onResult, onPlayerFailed);
        public void HidePlayerAnswers(Team team) => _inner.HidePlayerAnswers(team);
        public void Cleanup() => _inner.Cleanup();
    }

    // ── Sinh câu ─────────────────────────────────────────────────────────────

    protected override QuestionData PullNextQuestion()
    {
        int segment = _questionNumber / QuestionsPerSegment;
        _currentKind = segment < Segments.Length
            ? Segments[segment]
            : RandomKinds[Random.Range(0, RandomKinds.Length)];
        string id = $"THT_{_questionNumber + 1:00}_{_currentKind}";   // pipeline đánh giá parse THT_<số>_<Kind> — giữ nguyên
        _questionNumber++;

        // Nhận biết số: cùng 1 số cho 2 bên (cùng audio), đáp án nhiễu khác nhau.
        int recognizeValue = Random.Range(0, 11);
        _tq[0] = Make(_currentKind, id, null, recognizeValue);
        _tq[1] = Make(_currentKind, id, _tq[0], recognizeValue);
        return _tq[0].q;
    }

    /// <summary>Sinh câu cho 1 bên. `other` = câu bên kia (null nếu đang sinh bên đầu) → ép đáp án đúng khác nhau.</summary>
    TeamQ Make(Kind kind, string id, TeamQ other, int recognizeValue)
    {
        TeamQ t = null;
        for (int tries = 0; tries < 30; tries++)
        {
            t = kind switch
            {
                Kind.Recognize => MakeRecognize(id, recognizeValue, other),
                Kind.CountEasy => MakeCount(id, kind, Random.Range(1, 6)),
                Kind.Count     => MakeCount(id, kind, Random.Range(6, 11)),   // 6-10: không lặp lại phạm vi 5
                Kind.Add       => MakeEquation(id, kind, isAdd: true),
                Kind.Sub       => MakeEquation(id, kind, isAdd: false),
                _              => MakeSign(id, kind),
            };
            if (kind == Kind.Recognize || other == null || t.answerKey != other.answerKey) break;
        }
        return t;
    }

    TeamQ MakeRecognize(string id, int value, TeamQ other)
    {
        var t = new TeamQ { value = value, answerKey = value, hiddenSlot = 1 };
        // Bên phải tránh đáp án nhiễu của bên trái (vd 8: trái 3-8-5, phải 8-2-6).
        HashSet<int> avoid = null;
        if (other != null)
        {
            avoid = new HashSet<int>();
            foreach (var a in other.q.answers) if (int.TryParse(a, out int v) && v != value) avoid.Add(v);
        }
        var opts = Options(value, 3, 0, 10, out int correct, avoid);
        t.q = Build(id, _currentKind, $"Nghe số {value}", opts, correct);
        return t;
    }

    TeamQ MakeCount(string id, Kind kind, int n)
    {
        var t = new TeamQ { value = n, answerKey = n, animal = CountingAnimalPicker.GetRandom() };
        List<string> opts;
        int correct;
        if (kind == Kind.CountEasy) opts = Options(n, 4, 1, 5, out correct);
        else opts = Options(n, 4, Mathf.Max(1, n - 3), Mathf.Min(10, n + 3), out correct);
        t.q = Build(id, kind, $"Đếm {n} con vật", opts, correct);
        return t;
    }

    /// <summary>A ± B = ? — CHỈ ẩn kết quả (ô C), không ẩn số hạng A/B.</summary>
    TeamQ MakeEquation(string id, Kind kind, bool isAdd)
    {
        int a, b, c;
        if (isAdd)
        {
            c = Random.Range(2, 11);
            a = Random.Range(1, c);
            b = c - a;
        }
        else
        {
            a = Random.Range(2, 11);
            b = Random.Range(1, a);
            c = a - b;
        }
        var t = new TeamQ { answerKey = c, hiddenSlot = 2, op = isAdd ? "+" : "-" };
        t.parts[0] = a.ToString();
        t.parts[1] = b.ToString();
        t.parts[2] = "";
        var opts = Options(c, 4, 0, 10, out int correct);
        t.q = Build(id, kind, $"{a} {t.op} {b} = ?", opts, correct);
        return t;
    }

    TeamQ MakeSign(string id, Kind kind)
    {
        int a = Random.Range(1, 11);
        int b = Random.value < 0.3f ? a : Random.Range(1, 11);
        string[] signs = { "<", "=", ">" };
        int correct = a < b ? 0 : a == b ? 1 : 2;
        var t = new TeamQ { answerKey = correct, hiddenSlot = 1 };
        t.parts[0] = a.ToString();
        t.parts[1] = "";
        t.parts[2] = b.ToString();
        t.q = Build(id, kind, $"{a} ? {b}", new List<string>(signs), correct);
        return t;
    }

    /// <summary>`count` đáp án số khác nhau trong [min,max], chắc chắn có `correctValue`, đã xáo.
    /// `avoid` (tuỳ chọn) = các số nhiễu nên tránh (dùng khi bên kia đã dùng) — hết số khác thì mới lấy lại.</summary>
    static List<string> Options(int correctValue, int count, int min, int max, out int correctIndex, HashSet<int> avoid = null)
    {
        var set = new List<int> { correctValue };
        var pool = new List<int>();
        var fallback = new List<int>();
        for (int v = min; v <= max; v++)
        {
            if (v == correctValue) continue;
            if (avoid != null && avoid.Contains(v)) fallback.Add(v); else pool.Add(v);
        }
        Shuffle(pool);
        Shuffle(fallback);
        pool.AddRange(fallback);
        for (int i = 0; i < pool.Count && set.Count < count; i++) set.Add(pool[i]);
        Shuffle(set);
        correctIndex = set.IndexOf(correctValue);
        return set.ConvertAll(v => v.ToString());
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static QuestionData Build(string id, Kind kind, string description, List<string> answers, int correct) => new QuestionData
    {
        id = id,
        topic = "TongHopToan_" + kind,
        difficulty = 1,
        questionType = QuestionType.Choose,
        displayMode = ChooseDisplayMode.Button,
        questionMediaType = QuestionMediaType.Text,
        questionMediaValue = description,      // dùng cho log
        answerMediaType = AnswerMediaType.Text,
        answers = answers.ToArray(),
        answerMode = AnswerMode.Single,
        correctAnswers = new[] { correct },
    };

    // ── Hiển thị câu hỏi (mỗi nửa màn hình 1 câu) ────────────────────────────

    protected override void OnQuestionShown(QuestionData q)
    {
        ClearQuestionVisuals();
        _teamsDone = 0;
        ShowOn(leftPanel, _tq[0]);
        ShowOn(rightPanel, _tq[1]);
        if (_currentKind == Kind.Recognize) StartAudioLoop(_tq[0].value);
        StartCoroutine(RelayoutAnswersNextFrame());
    }

    void ShowOn(QuestionPanel p, TeamQ t)
    {
        if (p == null) return;
        switch (_currentKind)
        {
            case Kind.Recognize:
                SetSlot(p, 1, "", hidden: true);
                break;

            case Kind.CountEasy:
            case Kind.Count:
                ShowIcons(p, t.value, t.animal);
                break;

            case Kind.Add:
            case Kind.Sub:
                for (int i = 0; i < 3; i++) SetSlot(p, i, t.parts[i], hidden: i == t.hiddenSlot);
                SetLabel(p.opLabel, t.op);
                SetLabel(p.equalsLabel, "=");
                break;

            default: // Sign
                SetSlot(p, 0, t.parts[0], hidden: false);
                SetSlot(p, 1, "", hidden: true);
                SetSlot(p, 2, t.parts[2], hidden: false);
                break;
        }
    }

    static void SetSlot(QuestionPanel p, int i, string text, bool hidden)
    {
        if (p.slots[i] != null) p.slots[i].SetActive(true);
        if (p.hidden[i] != null) p.hidden[i].SetActive(hidden);
        var t = p.slotTexts[i];
        if (t == null) return;
        t.gameObject.SetActive(!hidden);
        t.text = text;
        if (!hidden) NumberTextStyle.Refresh(t, text);
    }

    static void SetLabel(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        label.text = text;
        label.gameObject.SetActive(true);
    }

    static void ShowIcons(QuestionPanel p, int count, Sprite sprite)
    {
        if (p.iconGrid == null) return;
        p.iconGrid.gameObject.SetActive(true);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(p.iconGrid, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
    }

    void ClearQuestionVisuals()
    {
        StopAudioLoop();
        ClearPanel(leftPanel);
        ClearPanel(rightPanel);
    }

    static void ClearPanel(QuestionPanel p)
    {
        if (p == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (p.slots[i] != null) p.slots[i].SetActive(false);
            if (p.hidden[i] != null) p.hidden[i].SetActive(false);
        }
        if (p.opLabel != null) p.opLabel.gameObject.SetActive(false);
        if (p.equalsLabel != null) p.equalsLabel.gameObject.SetActive(false);
        if (p.iconGrid != null)
        {
            for (int i = p.iconGrid.childCount - 1; i >= 0; i--)
            {
                var child = p.iconGrid.GetChild(i);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
            p.iconGrid.gameObject.SetActive(false);
        }
    }

    // ── Hàng đáp án: căn giữa số nút đang hiện (3 đáp án thì dồn giữa, không lệch 1 bên) ──

    IEnumerator RelayoutAnswersNextFrame()
    {
        yield return null; // ButtonDisplay.Setup chạy ngay sau OnQuestionShown, đợi nó bật/tắt xong các nút
        RelayoutRow(leftAnswerButtons);
        RelayoutRow(rightAnswerButtons);
    }

    void RelayoutRow(ButtonItem[] row)
    {
        if (row == null) return;
        var active = new List<ButtonItem>();
        foreach (var b in row) if (b != null && b.gameObject.activeSelf) active.Add(b);
        int n = active.Count;
        if (n == 0) return;
        float total = n * AnswerW + (n - 1) * AnswerGap;
        float x = (1f - total) * 0.5f;
        foreach (var b in active)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x, AnswerYMin);
            rt.anchorMax = new Vector2(x + AnswerW, AnswerYMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            x += AnswerW + AnswerGap;
            if (_currentKind == Kind.Sign) { b.SetFontSize(110f); b.SetTextColor(Color.white); }
        }
    }

    // ── Âm thanh (nhận biết số) ──────────────────────────────────────────────

    void StartAudioLoop(int value)
    {
        StopAudioLoop();
        _audioLoopOn = true;
        _audioLoop = StartCoroutine(AudioLoopRoutine(value));
    }

    void StopAudioLoop()
    {
        _audioLoopOn = false;
        if (_audioLoop != null) { StopCoroutine(_audioLoop); _audioLoop = null; }
        if (audioSource != null) audioSource.Stop();
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
    }

    IEnumerator AudioLoopRoutine(int value)
    {
        var clip = Resources.Load<AudioClip>(AudioFolder + value);
        if (clip == null)
        {
            Debug.LogWarning($"[TongHopToan] Không tìm thấy audio {AudioFolder}{value}");
            yield break;
        }
        while (_audioLoopOn)
        {
            MusicManager.Instance?.SetMusicVolumeMultiplier(0.4f);
            yield return new WaitForSeconds(0.4f);
            if (audioSource != null) audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length + 0.3f);
            MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
            yield return new WaitForSeconds(AudioRepeatPause);
        }
    }

    // ── Kết quả / chuyển câu ─────────────────────────────────────────────────

    // Cả 2 đội xong lượt → thôi đọc lại số; câu hỏi giữ nguyên trên màn trong lúc feedback.
    protected override void OnTeamTurnDone(Team team, bool correct)
    {
        if (++_teamsDone >= 2) StopAudioLoop();
    }

    // Chạy sau feedback, ngay trước "Next in Ns": dọn câu cũ để chữ đếm ngược không đè lên.
    protected override void CleanupCurrentDisplay()
    {
        base.CleanupCurrentDisplay();
        ClearQuestionVisuals();
    }

    protected override void OnDestroy()
    {
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
        base.OnDestroy();
    }
}
