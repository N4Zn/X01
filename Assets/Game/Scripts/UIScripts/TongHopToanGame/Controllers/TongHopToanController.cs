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
///   1) Nhận biết số   — nghe âm thanh (Audio/SoDem/N) → chọn chữ số   (nội dung như game SoDem)
///   2) Đếm (dễ)       — đếm 1-5 con vật                                (như Counting5)
///   3) Đếm            — đếm 1-10 con vật                               (như Counting)
///   4) Cộng chữ số    — A + B = ?                                      (như AddNumberDigit)
///   5) Trừ chữ số     — A - B = ?, trong phạm vi 10                    (như SubNumberDigit)
///   6) Điền dấu       — A ? B → chọn &lt; = &gt;                            (như SoSanhSo2DauChuSo)
///   7+) Random cộng / trừ / điền dấu (cộng/trừ có thể ẩn số hạng đầu hoặc giữa, vd "3 + ? = 7")
///
/// Combined mode + WaitForBothTeams: 2 đội cùng 1 câu, mỗi đội ghi điểm riêng. Câu hỏi hiện ở CẢ 2 nửa màn hình
/// (chờ cả 2 đội xong mới sang câu; mỗi nửa 1 QuestionPanel, bố cục ô A/B/C + đáp án lấy theo AddNumberGame); đáp án dùng ButtonDisplay của Kit.
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
    int _currentValue;            // Recognize: số cần nghe · Count*: số icon
    readonly string[] _parts = new string[3];   // chữ trên ô A, B, C (ô bị ẩn để rỗng)
    int _teamsDone;
    int _hiddenSlot = -1;
    string _op = "+";
    bool _audioLoopOn;
    Coroutine _audioLoop;

    protected override void Start()
    {
        base.Start();
        if (buttonDisplay != null) buttonDisplay.WaitBothTeams = WaitBothActive;
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        ClearQuestionVisuals();
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // Kết hợp nhưng KHÔNG ai thắng trước: mỗi đội trả lời xong câu của mình, cả 2 xong mới sang câu mới.
    protected override bool WaitForBothTeams => true;

    // ── Sinh câu ─────────────────────────────────────────────────────────────

    protected override QuestionData PullNextQuestion()
    {
        int segment = _questionNumber / QuestionsPerSegment;
        _currentKind = segment < Segments.Length
            ? Segments[segment]
            : RandomKinds[Random.Range(0, RandomKinds.Length)];
        bool randomPhase = segment >= Segments.Length;
        string id = $"THT_{_questionNumber + 1:00}_{_currentKind}";
        _questionNumber++;
        _hiddenSlot = -1;

        switch (_currentKind)
        {
            case Kind.Recognize: return MakeRecognize(id);
            case Kind.CountEasy: return MakeCount(id, Random.Range(1, 6));
            case Kind.Count:     return MakeCount(id, Random.Range(1, 11));
            case Kind.Add:       return MakeEquation(id, isAdd: true, hideAnyOperand: randomPhase);
            case Kind.Sub:       return MakeEquation(id, isAdd: false, hideAnyOperand: randomPhase);
            default:             return MakeSign(id);
        }
    }

    QuestionData MakeRecognize(string id)
    {
        _currentValue = Random.Range(0, 11);
        _hiddenSlot = 1;
        var opts = Options(_currentValue, 3, 0, 10, out int correct);
        return Build(id, _currentKind, $"Nghe số {_currentValue}", opts, correct);
    }

    QuestionData MakeCount(string id, int n)
    {
        _currentValue = n;
        int lo = Mathf.Max(1, n - 2);
        var opts = Options(n, 4, lo, lo + 4, out int correct);
        return Build(id, _currentKind, $"Đếm {n} con vật", opts, correct);
    }

    QuestionData MakeEquation(string id, bool isAdd, bool hideAnyOperand)
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
        // Đoạn cố định: luôn ẩn kết quả (A ± B = ?). Đoạn random: ẩn 1 trong 3 vị trí.
        int hidden = hideAnyOperand ? Random.Range(0, 3) : 2;
        int[] vals = { a, b, c };
        _hiddenSlot = hidden;
        _op = isAdd ? "+" : "-";
        for (int i = 0; i < 3; i++) _parts[i] = i == hidden ? "" : vals[i].ToString();
        string desc = $"{(hidden == 0 ? "?" : a.ToString())} {_op} {(hidden == 1 ? "?" : b.ToString())} = {(hidden == 2 ? "?" : c.ToString())}";
        var opts = Options(vals[hidden], 4, 0, 10, out int correct);
        return Build(id, _currentKind, desc, opts, correct);
    }

    QuestionData MakeSign(string id)
    {
        int a = Random.Range(1, 11);
        int b = Random.value < 0.3f ? a : Random.Range(1, 11);
        string[] signs = { "<", "=", ">" };
        int correct = a < b ? 0 : a == b ? 1 : 2;
        _parts[0] = a.ToString();
        _parts[1] = "";
        _parts[2] = b.ToString();
        _hiddenSlot = 1;
        return Build(id, _currentKind, $"{a} ? {b}", new List<string>(signs), correct);
    }

    /// <summary>`count` đáp án số khác nhau trong [min,max], chắc chắn có `correctValue`, đã xáo.</summary>
    static List<string> Options(int correctValue, int count, int min, int max, out int correctIndex)
    {
        var set = new List<int> { correctValue };
        int guard = 0;
        while (set.Count < count && guard++ < 200)
        {
            int v = Random.Range(min, max + 1);
            if (!set.Contains(v)) set.Add(v);
        }
        for (int i = set.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (set[i], set[j]) = (set[j], set[i]);
        }
        correctIndex = set.IndexOf(correctValue);
        return set.ConvertAll(v => v.ToString());
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

    // ── Hiển thị câu hỏi (cả 2 nửa màn hình) ─────────────────────────────────

    protected override void OnQuestionShown(QuestionData q)
    {
        ClearQuestionVisuals();
        _teamsDone = 0;
        Sprite animal = (_currentKind == Kind.CountEasy || _currentKind == Kind.Count)
            ? CountingAnimalPicker.GetRandom() : null;
        ShowOn(leftPanel, animal);
        ShowOn(rightPanel, animal);
        if (_currentKind == Kind.Recognize) StartAudioLoop(_currentValue);
        StartCoroutine(RelayoutAnswersNextFrame());
    }

    void ShowOn(QuestionPanel p, Sprite animal)
    {
        if (p == null) return;
        switch (_currentKind)
        {
            case Kind.Recognize:
                SetSlot(p, 1, "", hidden: true);
                break;

            case Kind.CountEasy:
            case Kind.Count:
                ShowIcons(p, _currentValue, animal);
                break;

            case Kind.Add:
            case Kind.Sub:
                for (int i = 0; i < 3; i++) SetSlot(p, i, _parts[i], hidden: i == _hiddenSlot);
                SetLabel(p.opLabel, _op);
                SetLabel(p.equalsLabel, "=");
                break;

            default: // Sign
                SetSlot(p, 0, _parts[0], hidden: false);
                SetSlot(p, 1, "", hidden: true);
                SetSlot(p, 2, _parts[2], hidden: false);
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
