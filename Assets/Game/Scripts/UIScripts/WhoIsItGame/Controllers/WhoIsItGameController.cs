using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "WHO IS IT?" — nhận biết từ vựng Family qua hình ảnh. 1 hình + 4 đáp án (1 đúng + 3 nhiễu),
/// học sinh nhảy vào từ đúng.
///
/// Dựng từ MiniGameControllerBase, chế độ Combined (mặc định), Single-select (1 lần bấm là
/// xong), không giới hạn thời gian mỗi câu, 20 câu/lượt.
///
/// Reward system (dùng khối dùng chung trong Assets/Game/Scripts/Core/MiniGameKit/Reward/):
///   - HopeStarTracker: mỗi đội có `maxHopeStars` lượt đặt sao hi vọng/lượt chơi. Đặt trước khi
///     câu hỏi hiện ra (bet phase, override StateMachineEnter_ShowQuestion) — đúng câu đó thì x2 điểm.
///   - JackpotTracker: cả 2 đội sai → điểm câu tiếp theo x2 (dồn), ai đúng ăn trọn.
///   - MysteryRewardPool: đội thắng chọn 1 trong 3 hộp quà ngẫu nhiên (bonus/penalty/share) —
///     override StateMachineEnter_Feedback để chèn bước chọn hộp trước khi qua câu tiếp theo.
/// </summary>
public class WhoIsItGameController : MiniGameControllerBase
{
    [Header("WhoIsIt — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] GameObject questionFrameRoot; // toàn bộ khung ảnh — ẩn đi khi hộp quà hiện
    [SerializeField] Image questionImage;
    [SerializeField] Text questionCaption;
    [SerializeField] RectTransform rewardBadge; // khung tròn "current reward" — to nhỏ theo điểm
    [SerializeField] Text rewardText;           // "N points!" bên trong khung tròn
    [SerializeField] Text leftFeedbackText;  // "Try again"/"Great job!"/reward message riêng cho Left
    [SerializeField] Text rightFeedbackText; // "Try again"/"Great job!"/reward message riêng cho Right
    [SerializeField] RectTransform starBurst;

    [Header("WhoIsIt — reward system")]
    [SerializeField] int basePoints = 5;
    [SerializeField] int maxHopeStars = 3;
    [Tooltip("Chờ tối đa bao lâu nếu 1 trong 2 bên không bấm Yes/No — an toàn tránh treo game.")]
    [SerializeField] float betMaxWaitSeconds = 15f;
    [SerializeField] GameObject betPromptRoot;
    [SerializeField] Button leftYesButton;
    [SerializeField] Button leftNoButton;
    [SerializeField] Button rightYesButton;
    [SerializeField] Button rightNoButton;
    [SerializeField] Text leftStarCountText;
    [SerializeField] Text rightStarCountText;
    [SerializeField] RectTransform leftMysteryBoxRoot;
    [SerializeField] RectTransform rightMysteryBoxRoot;
    [SerializeField] Button[] leftMysteryBoxes;  // 3 nút "?"
    [SerializeField] Button[] rightMysteryBoxes; // 3 nút "?"

    // Placeholder chưa có ảnh thật — hiện caption "Picture of X" trong khung ảnh để rõ ý câu hỏi.
    static readonly Dictionary<string, string> CaptionNouns = new()
    {
        { "mom", "mother" }, { "dad", "father" }, { "sister", "sister" },
        { "brother", "brother" }, { "grandma", "grandmother" }, { "grandpa", "grandfather" },
        { "baby", "baby" },
    };

    HopeStarTracker _stars;
    JackpotTracker _jackpot;

    // Trạng thái bet phase — chờ cả 2 bên bấm Yes/No mới hiện câu hỏi
    bool _leftDecided;
    bool _rightDecided;
    bool _betResolved;

    // Trạng thái round đang chờ chọn hộp quà (chỉ có ý nghĩa khi round vừa đúng)
    bool _awaitingBoxChoice;
    Team _pendingWinner;
    int _pendingRoundPoints;
    RewardOption[] _currentBoxes;
    Enum _pendingPrevState;
    Dictionary<string, object> _pendingOpts;

    protected override void Start()
    {
        // Khởi tạo TRƯỚC base.Start(): base.Start() → InitFsm() gọi StateMachineChange(Initialize)
        // đồng bộ ngay lập tức, có thể cascade thẳng tới StateMachineEnter_ShowQuestion (khi không
        // có tutorialPanel) trong CÙNG lệnh gọi — _stars/_jackpot phải sẵn sàng trước lúc đó.
        _stars = new HopeStarTracker(maxHopeStars);
        _jackpot = new JackpotTracker(basePoints);

        base.Start();

        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        if (leftYesButton != null) leftYesButton.onClick.AddListener(() => DecideBet(Team.Left, true));
        if (leftNoButton != null) leftNoButton.onClick.AddListener(() => DecideBet(Team.Left, false));
        if (rightYesButton != null) rightYesButton.onClick.AddListener(() => DecideBet(Team.Right, true));
        if (rightNoButton != null) rightNoButton.onClick.AddListener(() => DecideBet(Team.Right, false));
        WireMysteryBoxes(leftMysteryBoxes, Team.Left);
        WireMysteryBoxes(rightMysteryBoxes, Team.Right);

        HideAllFeedback();
        HideBetPrompt();
        HideMysteryBoxes();
    }

    void WireMysteryBoxes(Button[] boxes, Team team)
    {
        if (boxes == null) return;
        for (int i = 0; i < boxes.Length; i++)
        {
            int boxIndex = i;
            if (boxes[i] != null) boxes[i].onClick.AddListener(() => OnMysteryBoxClicked(team, boxIndex));
        }
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // Điểm mỗi câu có thể lên vài chục (jackpot/hope star/thưởng), không phải +1 như mặc định Kit
    // — nâng mốc fill-bar của HUD lên cho hợp lý hơn khi chơi theo thời gian.
    protected override int HudMaxScoreFallback => 10 * basePoints;

    // Tắt +1 điểm mặc định của Kit — WhoIsItGame tự chấm điểm ở OnMysteryBoxClicked (jackpot +
    // hộp quà + sao hi vọng), không phải +1 cố định. Không override sẽ bị cộng điểm 2 lần.
    protected override void AwardDefaultPoint(Team team) { }

    // Tắt hiệu ứng feedback mặc định của Kit (âm thanh + icon) — WhoIsItGame đã tự làm phần này
    // riêng (leftFeedbackText/rightFeedbackText + PlayCorrectSfx/PlayWrongSfx trong OnRoundResult/
    // OnPlayerFailed/OnMysteryBoxClicked). Không override sẽ bị phát âm thanh đúng/sai 2 lần.
    protected override bool UseDefaultFeedbackFx => false;

    // ── Bet phase — chèn trước ShowQuestion gốc ────────────────────────────────

    protected override void StateMachineEnter_ShowQuestion(Enum prev, Dictionary<string, object> opts)
    {
        StartCoroutine(BetPhaseThenShowQuestion(prev, opts));
    }

    /// <summary>Chờ CẢ 2 bên bấm Yes/No (không đếm giờ cố định) — có mốc chờ tối đa
    /// betMaxWaitSeconds chỉ để tránh treo game nếu 1 bên không tương tác. Nếu CẢ 2 đội đều đã
    /// hết sao hi vọng thì bỏ qua hẳn bảng hỏi (không còn gì để đặt) — vào câu hỏi luôn.</summary>
    IEnumerator BetPhaseThenShowQuestion(Enum prev, Dictionary<string, object> opts)
    {
        _stars.ResetRoundBet();
        _leftDecided = false;
        _rightDecided = false;
        _betResolved = false;

        // Luôn dọn dẹp round vừa xong, kể cả khi bỏ qua bảng hỏi.
        ClearOldRoundVisuals();

        bool anyStarsLeft = _stars.Remaining(Team.Left) > 0 || _stars.Remaining(Team.Right) > 0;
        if (anyStarsLeft)
        {
            ShowBetPrompt();

            float elapsed = 0f;
            while (!_betResolved && elapsed < betMaxWaitSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            HideBetPrompt();
        }

        base.StateMachineEnter_ShowQuestion(prev, opts);
    }

    void DecideBet(Team team, bool wantsStar)
    {
        bool alreadyDecided = team == Team.Left ? _leftDecided : _rightDecided;
        if (alreadyDecided) return;

        if (wantsStar) _stars.TryPlaceBet(team);
        if (team == Team.Left) _leftDecided = true; else _rightDecided = true;

        UpdateBetUi();
        if (_leftDecided && _rightDecided) _betResolved = true;
    }

    /// <summary>Dọn câu hỏi/feedback của round vừa xong + cập nhật khung "current reward" theo
    /// đúng giá trị hiện tại (đã tính jackpot nếu vừa dồn) — chạy ngay khi round mới bắt đầu,
    /// bất kể có hiện bảng hỏi hay không.</summary>
    void ClearOldRoundVisuals()
    {
        if (questionFrameRoot != null) questionFrameRoot.SetActive(false);
        HideAllFeedback();
        UpdateRewardBadge();
    }

    void ShowBetPrompt()
    {
        if (betPromptRoot != null) betPromptRoot.SetActive(true);
        UpdateBetUi();
    }

    void HideBetPrompt()
    {
        if (betPromptRoot != null) betPromptRoot.SetActive(false);
    }

    void UpdateBetUi()
    {
        if (leftStarCountText != null) leftStarCountText.text = $"* {_stars.Remaining(Team.Left)}";
        if (rightStarCountText != null) rightStarCountText.text = $"* {_stars.Remaining(Team.Right)}";

        if (leftYesButton != null) leftYesButton.interactable = !_leftDecided && _stars.Remaining(Team.Left) > 0;
        if (leftNoButton != null) leftNoButton.interactable = !_leftDecided;
        if (rightYesButton != null) rightYesButton.interactable = !_rightDecided && _stars.Remaining(Team.Right) > 0;
        if (rightNoButton != null) rightNoButton.interactable = !_rightDecided;
    }

    // ── Question shown ─────────────────────────────────────────────────────────

    protected override void OnQuestionShown(QuestionData q)
    {
        HideAllFeedback();
        if (questionFrameRoot != null) questionFrameRoot.SetActive(true);

        if (questionImage != null && q.questionMediaType == QuestionMediaType.Image)
        {
            var sprite = Resources.Load<Sprite>(q.questionMediaValue);
            questionImage.sprite = sprite;
            questionImage.gameObject.SetActive(sprite != null);
        }

        if (questionCaption != null)
        {
            string key = LastPathSegment(q.questionMediaValue);
            string noun = CaptionNouns.TryGetValue(key, out var n) ? n : key;
            questionCaption.text = $"Picture of {noun}";
        }

        UpdateRewardBadge();
    }

    /// <summary>Cập nhật khung tròn "current reward" — điểm câu hỏi hiện tại (đã tính jackpot),
    /// kích thước phóng to dần theo số lần jackpot đã nhân đôi (log2) để không vỡ layout dù
    /// dồn nhiều lần liên tiếp.</summary>
    void UpdateRewardBadge()
    {
        int value = _jackpot.CurrentValue;
        if (rewardText != null) rewardText.text = $"{value} points!";
        if (rewardBadge != null)
            rewardBadge.localScale = Vector3.one * ValueScale.LogScale(value, basePoints);
    }

    void HideAllFeedback()
    {
        if (leftFeedbackText != null) leftFeedbackText.gameObject.SetActive(false);
        if (rightFeedbackText != null) rightFeedbackText.gameObject.SetActive(false);
        if (starBurst != null) starBurst.gameObject.SetActive(false);
    }

    static string LastPathSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    static string GetDisplayName(Team team)
    {
        if (GameSessionManager.Instance == null) return team == Team.Left ? "Player 1" : "Player 2";
        return team == Team.Left ? GameSessionManager.Instance.GetDisplayName1() : GameSessionManager.Instance.GetDisplayName2();
    }

    // ── Kết quả round ───────────────────────────────────────────────────────────

    /// <summary>Gọi NGAY khi 1 bên bấm sai (không đợi bên kia) — âm thanh + "Try again" ngay bên đó.</summary>
    protected override void OnPlayerFailed(Team team)
    {
        MusicManager.Instance?.PlayWrongSfx();

        var text = team == Team.Left ? leftFeedbackText : rightFeedbackText;
        if (text == null) return;
        text.text = "Try again";
        text.color = FeedbackTextStyle.LossColor;
        text.gameObject.SetActive(true);
    }

    /// <summary>Cả 2 sai → dồn điểm cho câu sau (jackpot). Có bên đúng → tính điểm (jackpot +
    /// hope star x2), lưu lại chờ StateMachineEnter_Feedback mở hộp quà.</summary>
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        if (!correct)
        {
            _jackpot.RegisterBothWrong();
            return; // OnPlayerFailed đã lo xong phần hiện text/âm thanh từng bên
        }

        // Lưu điểm câu hỏi TRƯỚC khi reset jackpot — đây là điểm SAU jackpot, TRƯỚC sao hi vọng
        // (sao hi vọng nhân lên tổng cuối cùng ở OnMysteryBoxClicked, sau khi đã cộng/trừ hộp quà).
        _pendingRoundPoints = _jackpot.CurrentValue;
        _pendingWinner = team;
        _jackpot.ResetAfterWin();

        bool doubled = _stars.HasBet(team);
        var text = team == Team.Left ? leftFeedbackText : rightFeedbackText;
        if (text != null)
        {
            text.text = doubled ? "Great job! (Hope Star x2!)" : "Great job!";
            text.color = FeedbackTextStyle.GainColor;
            text.gameObject.SetActive(true);
        }

        MusicManager.Instance?.PlayCorrectSfx();
        MusicManager.Instance?.PlayExplosionSfx();
        if (starBurst != null)
            StartCoroutine(PulseEffect.ScaleFadePulse(starBurst, starBurst.GetComponent<CanvasGroup>(), 0.3f, 1.3f, 0.6f));
    }

    /// <summary>Round đúng → chèn bước "chọn hộp quà" trước khi thật sự chuyển câu tiếp theo.
    /// Round sai (cả 2) → không có hộp quà, chạy luôn logic Feedback gốc (đợi rồi qua câu mới).</summary>
    protected override void StateMachineEnter_Feedback(Enum prev, Dictionary<string, object> opts)
    {
        bool correct = opts != null && opts.TryGetValue("correct", out var c) && (bool)c;
        if (correct)
        {
            _pendingPrevState = prev;
            _pendingOpts = opts;
            ShowMysteryBoxes(_pendingWinner);
            return;
        }
        base.StateMachineEnter_Feedback(prev, opts);
    }

    // ── Hộp quà bí mật ───────────────────────────────────────────────────────────

    static readonly Color[] BoxPalette =
    {
        new(0.95f, 0.55f, 0.35f), new(0.45f, 0.75f, 0.95f), new(0.95f, 0.80f, 0.35f),
        new(0.65f, 0.55f, 0.90f), new(0.55f, 0.85f, 0.55f), new(0.95f, 0.50f, 0.65f),
    };

    void ShowMysteryBoxes(Team winner)
    {
        _currentBoxes = MysteryRewardPool.PickThree();
        _awaitingBoxChoice = true;

        // Ẩn câu hỏi + đáp án phía sau trong lúc chọn hộp quà — tránh rối mắt/lẫn với nền.
        if (questionFrameRoot != null) questionFrameRoot.SetActive(false);
        if (buttonDisplay != null) buttonDisplay.gameObject.SetActive(false);

        var boxes = winner == Team.Left ? leftMysteryBoxes : rightMysteryBoxes;
        RandomizeBoxColors(boxes);
        var root = winner == Team.Left ? leftMysteryBoxRoot : rightMysteryBoxRoot;
        if (root != null) root.gameObject.SetActive(true);
    }

    /// <summary>Random màu nền mỗi hộp quà mỗi lần hiện — chỉ để đẹp mắt, không ảnh hưởng phần
    /// thưởng bên trong (vẫn hoàn toàn ngẫu nhiên/độc lập, xem MysteryRewardPool). Rút KHÔNG lặp
    /// từ palette nên các hộp luôn khác màu nhau (không random độc lập từng hộp).</summary>
    static void RandomizeBoxColors(Button[] boxes)
    {
        if (boxes == null) return;

        var pool = new List<Color>(BoxPalette);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] == null) continue;
            var img = boxes[i].GetComponent<Image>();
            if (img != null) img.color = pool[i % pool.Count];
        }
    }

    void HideMysteryBoxes()
    {
        if (leftMysteryBoxRoot != null) leftMysteryBoxRoot.gameObject.SetActive(false);
        if (rightMysteryBoxRoot != null) rightMysteryBoxRoot.gameObject.SetActive(false);
    }

    void OnMysteryBoxClicked(Team team, int boxIndex)
    {
        if (!_awaitingBoxChoice || team != _pendingWinner) return;
        if (_currentBoxes == null || boxIndex >= _currentBoxes.Length) return;

        _awaitingBoxChoice = false;
        HideMysteryBoxes();

        var reward = _currentBoxes[boxIndex];

        // jackpotMultiplier luôn nguyên (1/2/4/8...) vì JackpotTracker chỉ nhân đôi mỗi lần cả 2 sai.
        int jackpotMultiplier = _pendingRoundPoints / basePoints;
        var (winnerBase, opponentPoints) = MysteryRewardPool.Apply(reward, _pendingRoundPoints, jackpotMultiplier);

        // Sao hi vọng nhân TỔNG CUỐI CÙNG (sau khi đã cộng/trừ phần thưởng theo jackpot) — không
        // ảnh hưởng phần chia cho đối thủ (đối thủ không đặt sao, không được hưởng nhân đôi của mình).
        bool doubled = _stars.HasBet(team);
        int winnerPoints = doubled ? winnerBase * 2 : winnerBase;

        ScoreManager.AddPoints(team, winnerPoints);
        Team opponent = team == Team.Left ? Team.Right : Team.Left;
        string opponentLabel = GetDisplayName(opponent);

        int scaledAmount = reward.amount * jackpotMultiplier;
        string starNote = doubled ? " <b>x2</b>" : "";

        var winnerText = team == Team.Left ? leftFeedbackText : rightFeedbackText;
        if (winnerText != null)
        {
            winnerText.text = reward.kind switch
            {
                RewardKind.BonusFixed => $"{reward.message} {FeedbackTextStyle.Gain(scaledAmount)}{starNote}",
                RewardKind.PenaltyFixed => $"{reward.message} {FeedbackTextStyle.Loss(scaledAmount)}{starNote}",
                RewardKind.ShareFraction => $"Share! {FeedbackTextStyle.Shared(opponentPoints)} for {opponentLabel}{starNote}",
                _ => reward.message,
            };
            winnerText.color = Color.black;
            winnerText.gameObject.SetActive(true);
        }

        if (opponentPoints > 0)
        {
            ScoreManager.AddPoints(opponent, opponentPoints);
            var opponentText = opponent == Team.Left ? leftFeedbackText : rightFeedbackText;
            if (opponentText != null)
            {
                opponentText.text = FeedbackTextStyle.Gain(opponentPoints) + "!";
                opponentText.color = Color.black;
                opponentText.gameObject.SetActive(true);
            }
        }

        base.StateMachineEnter_Feedback(_pendingPrevState, _pendingOpts);
    }
}
