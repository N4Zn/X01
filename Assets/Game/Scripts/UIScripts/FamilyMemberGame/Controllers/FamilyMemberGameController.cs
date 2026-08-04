using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "FAMILY MEMBER" — nhận biết từ vựng Family qua hình ảnh: hiện 1 ảnh (vd mẹ), 4 đáp án chữ (1
/// đúng + 3 nhiễu, đều là tên thành viên gia đình khác — vd ảnh mom → đáp án Baby/Mom/Dad/Brother).
///
/// Về cơ bản giống FamilySpellingGame (cùng Combined mode, cùng art skin, cùng Kit defaults: icon
/// to giữa vùng chơi, đếm ngược chuyển câu, chơi theo thời gian) — chỉ khác cơ chế câu hỏi: không
/// có "từ thiếu chữ cái" để điền, chỉ đơn giản AnswerMode.Single chọn đúng 1 trong 4 đáp án chữ.
/// Dùng lại toàn bộ ảnh Family đã có (Assets/Resources/Family/*.png) — không cần override gì thêm
/// ngoài hiện ảnh câu hỏi, mọi thứ khác (ghi điểm, feedback, next round) là mặc định của Kit.
/// </summary>
public class FamilyMemberGameController : MiniGameControllerBase
{
    [Header("FamilyMember — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Image questionImage;

    [Header("FamilyMember — âm thanh từ đúng")]
    [Tooltip("Giây chờ sau âm thanh correct rồi mới phát âm từ.")]
    [SerializeField] float wordAudioDelay = 0.8f;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    protected override void OnQuestionShown(QuestionData q)
    {
        if (questionImage == null || q.questionMediaType != QuestionMediaType.Image) return;
        var sprite = AssetOverrideLoader.GetSprite(q.questionMediaValue);
        questionImage.sprite = sprite;
        questionImage.gameObject.SetActive(sprite != null);
    }

    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        base.OnRoundResult(correct, team, playerAnswer);
        if (!correct || string.IsNullOrEmpty(CurrentQuestion?.questionMediaValue)) return;
        StartCoroutine(PlayWordAudio(CurrentQuestion.questionMediaValue, wordAudioDelay));
    }

    IEnumerator PlayWordAudio(string resourcePath, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var clip = AssetOverrideLoader.GetClip(resourcePath);
        MusicManager.Instance?.PlaySfx(clip);
    }
}
