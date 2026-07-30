using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "FAMILY SPELLING JUMP" — luyện spelling: hiện hình + từ bị thiếu 1 chữ cái, học sinh nhảy
/// vào chữ cái đúng trong 4 lựa chọn.
///
/// Dựng từ MiniGameControllerBase, chế độ Combined (mặc định), Single-select, không reward
/// system riêng — dùng nguyên cơ chế quiz gốc của Kit (ButtonDisplay tự tô xanh/đỏ đúng/sai).
///
/// Từ mẫu với vị trí chữ cái trống (vd "M_M", "MO_", "_AD") được lưu thẳng trong cột `id` của
/// CSV — mỗi câu 1 template riêng biệt nên id vẫn giữ đúng vai trò định danh, không cần thêm cột
/// mới hay đụng vào CsvQuestionLoader/QuestionData của Kit.
/// </summary>
public class FamilySpellingGameController : MiniGameControllerBase
{
    [Header("FamilySpelling — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Image questionImage;
    [SerializeField] Text wordTemplateText;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
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

        if (wordTemplateText != null)
            wordTemplateText.text = FormatTemplate(q.id);
    }

    /// <summary>Trả lời đúng → điền chữ cái đúng vào chỗ trống, học sinh thấy từ hoàn chỉnh ngay
    /// (xem FillBlankEffect — cơ chế dùng chung, không riêng game này).</summary>
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        if (!correct || wordTemplateText == null || CurrentQuestion?.answers == null) return;
        if (playerAnswer == null || playerAnswer.Length == 0) return;

        int idx = playerAnswer[0];
        if (idx < 0 || idx >= CurrentQuestion.answers.Length) return;

        string filledWord = FillBlankEffect.Fill(CurrentQuestion.id, CurrentQuestion.answers[idx]);
        wordTemplateText.text = FormatTemplate(filledWord);
    }

    /// <summary>"M_M" → "M _ M" — cách chữ ra cho dễ đọc từ xa, giữ nguyên "_" làm chỗ trống.</summary>
    static string FormatTemplate(string template) =>
        string.IsNullOrEmpty(template) ? template : string.Join(" ", template.ToCharArray());
}
