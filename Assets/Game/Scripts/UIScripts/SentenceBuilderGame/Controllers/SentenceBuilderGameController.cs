using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "SENTENCE BUILDER" — hiện 1 hình (vd "thirsty") + 3 chỗ trống "___ ___ ___", 4 nút đáp án (3 từ
/// đúng theo ĐÚNG THỨ TỰ ghép thành câu + 1 từ nhiễu). Dựng từ MiniGameControllerBase, tái dùng
/// NGUYÊN `ButtonDisplay` của TestTongHopGame ở chế độ `AnswerMode.OrderedSequence` (đã có sẵn —
/// xem `AnswerValidator.ValidateOrdered`: đúng thứ tự thì tiếp tục, sai thứ tự/nhiễu thì fail
/// ngay, không cho thử lại) thay vì viết lại validator.
///
/// Phần MỚI thật sự (TestTongHopGame/ButtonDisplay chưa có sẵn):
///   - Hiện chỗ trống dần theo từng từ đúng đã chọn (leftBlanksText/rightBlanksText) — dùng hook
///     `ButtonDisplay.onAnswerTapped` (team, answerIndex, ClickResult) mới thêm, bắn ra sau MỖI lần
///     bấm bất kể đúng/sai, không đổi hành vi các game khác đang dùng ButtonDisplay.
///   - KHÔNG phát âm từng từ lúc bấm — dồn lại: khi đúng hết cả câu, phát LẦN LƯỢT từng từ đúng
///     theo đúng thứ tự (đọc liền thành cả câu), rồi mới phát ngẫu nhiên 1 câu khen
///     (well_done/Great_job/...). File audio lấy theo ĐÚNG TÊN chữ trong CSV tại
///     Assets/Resources/Family/{word}.mp3 (phân biệt hoa/thường để khớp Android — xem CSV).
/// </summary>
public class SentenceBuilderGameController : MiniGameControllerBase
{
    [Header("SentenceBuilder — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Image questionImage;
    [SerializeField] Text leftBlanksText;
    [SerializeField] Text rightBlanksText;

    [Tooltip("Khoảng nghỉ (giây) giữa các từ khi đọc liền thành cả câu — để ÂM cho các từ chồng nhẹ lên nhau (cắt bớt khoảng lặng cuối mỗi clip), nghe liền hơn giống câu thật thay vì tách rời từng từ.")]
    [SerializeField] float wordGapSeconds = -0.08f;

    // Đúng tên file trong Assets/Resources/Family/ (phân biệt hoa/thường — "Great_job.mp3" viết hoa G).
    static readonly string[] PraiseClips = { "well_done", "Great_job", "congratulation", "awesome", "nice" };

    string[] _leftWords;
    string[] _rightWords;
    int _leftFilled;
    int _rightFilled;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        if (buttonDisplay != null) buttonDisplay.onAnswerTapped = OnAnswerTapped;
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    protected override void OnQuestionShown(QuestionData q)
    {
        if (questionImage != null && q.questionMediaType == QuestionMediaType.Image)
        {
            var sprite = Resources.Load<Sprite>(q.questionMediaValue);
            questionImage.sprite = sprite;
            questionImage.gameObject.SetActive(sprite != null);
        }

        ResetBlanks(q.correctAnswers != null ? q.correctAnswers.Length : 0);
    }

    // Bắn cho MỌI lần bấm (đúng hay sai) — CHỈ điền vào chỗ trống nếu đó là bước đúng trong chuỗi
    // (CorrectPartial/Final). Không phát âm ở đây nữa — âm thanh dồn lại phát 1 lần khi đúng cả
    // câu (xem OnRoundResult/PlaySentenceThenPraise).
    void OnAnswerTapped(Team team, int answerIndex, ClickResult result)
    {
        if (result == ClickResult.CorrectPartial || result == ClickResult.CorrectFinal)
            FillNextBlank(team, AnswerTextAt(answerIndex));
    }

    string AnswerTextAt(int answerIndex)
    {
        var answers = CurrentQuestion?.answers;
        return answers != null && answerIndex >= 0 && answerIndex < answers.Length ? answers[answerIndex] : null;
    }

    // Đúng hết cả câu → phát LẦN LƯỢT từng từ đúng theo đúng thứ tự (đọc liền thành cả câu), rồi
    // mới phát câu khen — không phát chồng lên nhau.
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        if (!correct) return;
        StartCoroutine(PlaySentenceThenPraise());
    }

    IEnumerator PlaySentenceThenPraise()
    {
        var q = CurrentQuestion;
        if (q?.correctAnswers != null && q.answers != null)
        {
            foreach (int idx in q.correctAnswers)
            {
                if (idx < 0 || idx >= q.answers.Length) continue;
                var clip = Resources.Load<AudioClip>("Family/" + q.answers[idx]);
                if (clip == null) continue;
                MusicManager.Instance?.PlaySfx(clip);
                // wordGapSeconds âm → bắt đầu từ tiếp theo TRƯỚC khi từ này phát xong hẳn (chồng
                // nhẹ lên nhau, PlayOneShot cho phép lớp chồng mà không cắt clip đang phát).
                yield return new WaitForSeconds(Mathf.Max(0f, clip.length + wordGapSeconds));
            }
        }

        string praise = PraiseClips[Random.Range(0, PraiseClips.Length)];
        var praiseClip = Resources.Load<AudioClip>("Family/" + praise);
        if (praiseClip != null) MusicManager.Instance?.PlaySfx(praiseClip);
    }

    void ResetBlanks(int wordCount)
    {
        _leftWords = new string[wordCount];
        _rightWords = new string[wordCount];
        _leftFilled = 0;
        _rightFilled = 0;
        RefreshBlanksText(Team.Left);
        RefreshBlanksText(Team.Right);
    }

    void FillNextBlank(Team team, string word)
    {
        bool isLeft = team == Team.Left;
        var words = isLeft ? _leftWords : _rightWords;
        if (words == null) return;
        int filled = isLeft ? _leftFilled : _rightFilled;
        if (filled >= words.Length) return;

        words[filled] = word;
        filled++;
        if (isLeft) _leftFilled = filled; else _rightFilled = filled;
        RefreshBlanksText(team);
    }

    void RefreshBlanksText(Team team)
    {
        var words = team == Team.Left ? _leftWords : _rightWords;
        var text = team == Team.Left ? leftBlanksText : rightBlanksText;
        if (text == null || words == null) return;

        var parts = new string[words.Length];
        for (int i = 0; i < words.Length; i++) parts[i] = string.IsNullOrEmpty(words[i]) ? "___" : words[i];
        text.text = string.Join("   ", parts);
    }
}
