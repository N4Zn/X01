using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "FAMILY SPELLING JUMP" — luyện spelling: hiện hình + từ bị thiếu 1 chữ cái, học sinh nhảy
/// vào chữ cái đúng trong 4 lựa chọn.
///
/// Dựng từ MiniGameControllerBase, chế độ Combined (mặc định), Single-select, không reward
/// system riêng — dùng nguyên cơ chế quiz gốc của Kit (ButtonDisplay tự tô xanh/đỏ đúng/sai).
/// </summary>
public class FamilySpellingGameController : MiniGameControllerBase
{
    [Header("FamilySpelling — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;

    [Header("FamilySpelling — hình ảnh & từ mẫu (2 bên)")]
    [SerializeField] Image questionImageLeft;
    [SerializeField] Image questionImageRight;
    [SerializeField] Text wordTemplateTextLeft;
    [SerializeField] Text wordTemplateTextRight;

    [Header("FamilySpelling — hiệu ứng")]
    [SerializeField] TileAppearAnimator tileAppearAnimator;
    [SerializeField] SinglePopAnimator questionImagePopLeft;
    [SerializeField] SinglePopAnimator questionImagePopRight;
    [SerializeField] SinglePopAnimator wordTemplatePopLeft;
    [SerializeField] SinglePopAnimator wordTemplatePopRight;

    [Header("FamilySpelling — màu chữ cái vừa điền đúng")]
    [SerializeField] string filledLetterColorHex = "#FF8A00";

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    protected override void OnQuestionShown(QuestionData q)
    {
        bool hasImage = q.questionMediaType == QuestionMediaType.Image;
        Sprite sprite = hasImage ? Resources.Load<Sprite>(q.questionMediaValue) : null;

        SetQuestionImage(questionImageLeft, questionImagePopLeft, sprite);
        SetQuestionImage(questionImageRight, questionImagePopRight, sprite);

        string formatted = FormatTemplate(q.id);
        SetWordTemplate(wordTemplateTextLeft, wordTemplatePopLeft, formatted);
        SetWordTemplate(wordTemplateTextRight, wordTemplatePopRight, formatted);

        // Kích hoạt ButtonDisplay nếu chưa active để tránh lỗi Coroutine
        if (buttonDisplay != null && !buttonDisplay.gameObject.activeSelf)
        {
            buttonDisplay.gameObject.SetActive(true);
        }

        // Chỉ chạy animation khi Animator và GameObject đang active
        if (tileAppearAnimator != null && tileAppearAnimator.gameObject.activeInHierarchy)
        {
            tileAppearAnimator.PlayAppearAnimation();
        }
    }

    static void SetQuestionImage(Image target, SinglePopAnimator pop, Sprite sprite)
    {
        if (target == null) return;

        target.sprite = sprite;
        target.gameObject.SetActive(sprite != null);

        if (sprite != null && pop != null && pop.gameObject.activeInHierarchy)
            pop.PlayAppear();
    }

    static void SetWordTemplate(Text target, SinglePopAnimator pop, string text)
    {
        if (target == null) return;

        target.text = text;

        if (pop != null && pop.gameObject.activeInHierarchy)
            pop.PlayAppear();
    }

    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        base.OnRoundResult(correct, team, playerAnswer);

        // Kích hoạt lại Timer ngay lập tức nếu Kit lỡ pause
        ForceResumeTimer();

        if (!correct || CurrentQuestion?.answers == null) return;
        if (playerAnswer == null || playerAnswer.Length == 0) return;

        int idx = playerAnswer[0];
        if (idx < 0 || idx >= CurrentQuestion.answers.Length) return;

        string filledWord = FillBlankEffect.Fill(CurrentQuestion.id, CurrentQuestion.answers[idx]);
        string highlighted = FormatTemplateHighlightFilled(CurrentQuestion.id, filledWord);

        SetWordTemplate(wordTemplateTextLeft, wordTemplatePopLeft, highlighted);
        SetWordTemplate(wordTemplateTextRight, wordTemplatePopRight, highlighted);
    }

    /// <summary>
    /// Ép đếm tiếp đồng hồ ngay lập tức ngay cả trong thời gian chờ delay của Kit
    /// </summary>
    private void ForceResumeTimer()
    {
        // 1. Đảm bảo Time.timeScale không bị tạm dừng
        if (Time.timeScale == 0) Time.timeScale = 1f;

        // 2. Mở file MiniGameControllerBase.cs xem hàm kích hoạt lại timer tên là gì
        // (ví dụ: ResumeTimer(), UnpauseTimer(), StartTimer(), isTimerPaused = false)
        // và bỏ comment dòng tương ứng bên dưới:
        
        // ResumeTimer();
        // isTimerPaused = false;
    }

    static string FormatTemplate(string template) =>
        string.IsNullOrEmpty(template) ? template : string.Join(" ", template.ToCharArray());

    string FormatTemplateHighlightFilled(string originalTemplateWithBlank, string filledWord)
    {
        if (string.IsNullOrEmpty(originalTemplateWithBlank) || string.IsNullOrEmpty(filledWord))
            return filledWord;

        int blankIndex = originalTemplateWithBlank.IndexOf('_');
        var sb = new StringBuilder();

        for (int i = 0; i < filledWord.Length; i++)
        {
            if (i > 0) sb.Append(' ');

            if (i == blankIndex)
                sb.Append($"<color={filledLetterColorHex}><b>{filledWord[i]}</b></color>");
            else
                sb.Append(filledWord[i]);
        }

        return sb.ToString();
    }
}