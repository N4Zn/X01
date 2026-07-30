using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ví dụ tối giản dùng MiniGameControllerBase — chứng minh Kit chạy end-to-end.
/// Đây là điểm bắt đầu khi Claude (/newminigame) scaffold 1 game mới: copy file này,
/// đổi tên class, và code phần "ý tưởng mới" ở các hook // TODO(idea) bên dưới.
///
/// Bản thân Template chỉ tái dùng ButtonDisplay có sẵn (Single/MultiSelect/OrderedSequence
/// đều chạy qua đây) — không có gì "mới" ở đây, phần mới nằm ở game thực tế được sinh ra.
/// </summary>
public class TemplateMiniGameController : MiniGameControllerBase
{
    [Header("Template — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Text questionText;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
    }

    // TODO(idea): nếu ý tưởng mới cần 1 IAnswerDisplay khác (không phải chọn nút/thả),
    // thay field buttonDisplay bằng display tự viết (implements IAnswerDisplay) và trả về ở đây.
    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // TODO(idea): thêm hiệu ứng/âm thanh riêng khi 1 câu hỏi mới được hiện ra.
    protected override void OnQuestionShown(QuestionData q)
    {
        if (questionText != null && q.questionMediaType == QuestionMediaType.Text)
            questionText.text = q.questionMediaValue;
    }

    // TODO(idea): thêm logic riêng khi có kết quả 1 vòng (vd combo, hiệu ứng đặc biệt).
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer) { }
}
