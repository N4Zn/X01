using System;
using UnityEngine;

/// <summary>
/// Chọn display phù hợp dựa trên 3 weight, tự lấy câu hỏi từ QuestionPool.
///
/// Weight ý nghĩa:
///   weightFloating  → % câu hỏi Choose hiện bằng FloatingDisplay
///   weightButton    → % câu hỏi Choose hiện bằng ButtonDisplay
///   weightMatching  → % câu hỏi Matching hiện bằng MatchingDisplay
///
/// Ví dụ: 0 / 0 / 100  → chỉ Matching
///        40 / 60 / 0  → chỉ Choose (40% Floating, 60% Button)
///        34 / 33 / 33 → hỗn hợp
///
/// Nếu pool không có câu hỏi loại được chọn, tự fallback sang loại còn lại.
/// </summary>
public class AnswerDisplayManager : MonoBehaviour
{
    [Header("Display references")]
    [SerializeField] FloatingDisplay  floatingDisplay;
    [SerializeField] ButtonDisplay    buttonDisplay;
    [SerializeField] MatchingDisplay  matchingDisplay;

    [Header("Question pool")]
    [SerializeField] QuestionPool questionPool;

    // Weights đọc từ TongHopConfig.Current (gameconfig.json)
    // weightFloating / weightButton / weightMatching

    IAnswerDisplay _active;

    /// <summary>Câu hỏi đang được hiển thị — Controller đọc để log.</summary>
    public QuestionData CurrentQuestion { get; private set; }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Chọn display + câu hỏi dựa trên weight, bắt đầu hiển thị.
    /// onPlayerFailed: callback ngay khi 1 player hết lượt (sai) — trước khi cả 2 xong.
    /// </summary>
    public void Show(Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed = null)
    {
        HideAll();

        // 1. Chọn slot display dựa trên weight
        int slot = PickSlot();           // 0 = Floating, 1 = Button, 2 = Matching

        // 2. Lấy câu hỏi đúng loại; nếu pool trống thì fallback
        QuestionData q = GetQuestionForSlot(slot);
        if (q == null)
        {
            Debug.LogWarning("[AnswerDisplayManager] Pool rỗng hoàn toàn!");
            return;
        }

        // 3. Chọn display:
        //    - Matching → luôn dùng MatchingDisplay
        //    - Choose   → ưu tiên displayMode ghi trong câu hỏi (Button/Floating)
        //                 nếu Auto → dùng slot weight đã chọn (0=Floating, 1=Button)
        if (q.questionType == QuestionType.Matching)
        {
            _active = matchingDisplay;
        }
        else
        {
            _active = q.displayMode switch
            {
                ChooseDisplayMode.Button   => buttonDisplay,
                ChooseDisplayMode.Floating => floatingDisplay,
                _                          => slot == 0 ? (IAnswerDisplay)floatingDisplay : buttonDisplay
            };
        }

        CurrentQuestion = q;

        (_active as MonoBehaviour)?.gameObject.SetActive(true);
        _active.Setup(q, onResult, onPlayerFailed);
    }

    public void Cleanup()
    {
        _active?.Cleanup();
        HideAll();
        _active        = null;
        CurrentQuestion = null;
    }

    /// <summary>Ẩn phần đáp án của 1 player (sau khi họ fail) — giữ nguyên bên player kia.</summary>
    public void HidePlayerAnswers(Team team) => _active?.HidePlayerAnswers(team);

    // ─── Slot selection ───────────────────────────────────────────────────────

    /// <summary>
    /// Weighted random: trả về 0 (Floating), 1 (Button), hoặc 2 (Matching).
    /// </summary>
    int PickSlot()
    {
        var cfg   = TongHopConfig.Current;
        int total = cfg.weightFloating + cfg.weightButton + cfg.weightMatching;
        if (total <= 0) return 1; // fallback = Button

        int roll = UnityEngine.Random.Range(0, total);
        if (roll < cfg.weightFloating)                          return 0;
        if (roll < cfg.weightFloating + cfg.weightButton)       return 1;
        return 2;
    }

    /// <summary>
    /// Lấy câu hỏi phù hợp với slot.
    /// Fallback: nếu pool loại cần không có thì thử loại kia.
    /// </summary>
    QuestionData GetQuestionForSlot(int slot)
    {
        bool wantMatching = (slot == 2);

        if (wantMatching)
        {
            if (questionPool.HasMatching) return questionPool.GetNextMatching();
            if (questionPool.HasChoose)   return questionPool.GetNextChoose();   // fallback
        }
        else
        {
            if (questionPool.HasChoose)   return questionPool.GetNextChoose();
            if (questionPool.HasMatching) return questionPool.GetNextMatching(); // fallback
        }
        return null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    void HideAll()
    {
        floatingDisplay.gameObject.SetActive(false);
        buttonDisplay.gameObject.SetActive(false);
        matchingDisplay.gameObject.SetActive(false);
    }
}

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IAnswerDisplay
{
    /// <summary>
    /// onResult: khi cả 2 player hoàn thành (đúng hoặc cả 2 sai).
    /// onPlayerFailed: ngay khi 1 player hết lượt + sai, trước khi player kia xong.
    /// </summary>
    void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed);
    /// <summary>Ẩn đáp án của 1 bên player sau khi họ fail — không ảnh hưởng bên kia.</summary>
    void HidePlayerAnswers(Team team);
    void Cleanup();
}
