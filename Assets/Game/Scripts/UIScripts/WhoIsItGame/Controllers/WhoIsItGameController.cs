using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
///
/// Điều kiện thắng: thay vì chờ hết giờ (mặc định của Kit khi totalRounds &lt;= 0), game này kết
/// thúc NGAY khi 1 trong 2 đội đầy thanh điểm HUD (chạm HudMaxScoreFallback) — xem
/// CheckForFillBarWin() gọi cuối OnMysteryBoxClicked, nơi duy nhất thật sự cộng điểm.
/// </summary>
public class WhoIsItGameController : MiniGameControllerBase
{
    [Header("WhoIsIt — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] GameObject questionFrameRoot; // toàn bộ khung ảnh — ẩn đi khi hộp quà hiện
    [SerializeField] Image leftQuestionImage;  // ảnh nhân vật bên trái — giống hệt bên phải
    [SerializeField] Image rightQuestionImage; // ảnh nhân vật bên phải — giống hệt bên trái
    [SerializeField] Text questionCaption;
    [SerializeField] RectTransform rewardBadge; // khung tròn "current reward" — to nhỏ theo điểm
    [SerializeField] Text rewardText;           // "N points!" bên trong khung tròn
    [SerializeField] Text leftFeedbackText;  // "Try again"/"Great job!"/reward message riêng cho Left
    [SerializeField] Text rightFeedbackText; // "Try again"/"Great job!"/reward message riêng cho Right
    [SerializeField] RectTransform starBurst;
    [SerializeField] ParticleSystem correctAnswerVFX;
    [SerializeField] ParticleSystem fireworksVFX; // pháo hoa khi mở được hộp quà thưởng (bonus/share)

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

    [Header("WhoIsIt — bet phase visuals")]
    [SerializeField] RectTransform leftHopeStarIcon;
    [SerializeField] RectTransform rightHopeStarIcon;
    [SerializeField] float starIdlePulseDuration = 1f;

    [Header("WhoIsIt — answer button idle sway")]
    [SerializeField] float swayAngle = 4f;
    [SerializeField] float swaySpeed = 1.3f;

    [Header("WhoIsIt — win condition")]
    [Tooltip("Thời gian chờ (giây) để xem hiệu ứng ăn mừng trước khi chuyển sang màn hình kết thúc, khi 1 đội vừa đầy thanh điểm.")]
    [SerializeField] float winDelaySeconds = 1.5f;

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
    Coroutine _leftStarPulseCo;
    Coroutine _rightStarPulseCo;

    // Trạng thái round đang chờ chọn hộp quà (chỉ có ý nghĩa khi round vừa đúng)
    bool _awaitingBoxChoice;
    Team _pendingWinner;
    int _pendingRoundPoints;
    RewardOption[] _currentBoxes;
    Enum _pendingPrevState;
    Dictionary<string, object> _pendingOpts;

    // Toàn bộ RectTransform của các nút đáp án (LeftBtn_0..3, RightBtn_0..3) — tự thu thập 1 lần
    // lúc Start, dựa theo ButtonItem (script thật sự gắn trên mỗi nút — không dùng Button chuẩn
    // của Unity UI, xem CollectAnswerButtons()).
    RectTransform[] _answerButtonRects;
    Coroutine[] _answerSwayCoroutines;

    protected override void Start()
    {
        // Khởi tạo TRƯỚC base.Start(): base.Start() → InitFsm() gọi StateMachineChange(Initialize)
        // đồng bộ ngay lập tức, có thể cascade thẳng tới StateMachineEnter_ShowQuestion (khi không
        // có tutorialPanel) trong CÙNG lệnh gọi — _stars/_jackpot phải sẵn sàng trước lúc đó.
        _stars = new HopeStarTracker(maxHopeStars);
        _jackpot = new JackpotTracker(basePoints);

        base.Start();

        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);

        if (leftYesButton != null) leftYesButton.onClick.AddListener(() => {
            PunchButton(leftYesButton.GetComponent<RectTransform>());
            DecideBet(Team.Left, true);
        });
        if (leftNoButton != null) leftNoButton.onClick.AddListener(() => {
            PunchButton(leftNoButton.GetComponent<RectTransform>());
            DecideBet(Team.Left, false);
        });
        if (rightYesButton != null) rightYesButton.onClick.AddListener(() => {
            PunchButton(rightYesButton.GetComponent<RectTransform>());
            DecideBet(Team.Right, true);
        });
        if (rightNoButton != null) rightNoButton.onClick.AddListener(() => {
            PunchButton(rightNoButton.GetComponent<RectTransform>());
            DecideBet(Team.Right, false);
        });

        WireMysteryBoxes(leftMysteryBoxes, Team.Left);
        WireMysteryBoxes(rightMysteryBoxes, Team.Right);
        CollectAnswerButtons();

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

    // Thu thập toàn bộ ButtonItem con bên trong buttonDisplay (LeftBtn_0..3, RightBtn_0..3) — chạy
    // 1 lần lúc Start. Dùng ButtonItem thay vì Button chuẩn của Unity UI vì mỗi nút đáp án trong
    // project này KHÔNG có component Button — chỉ có Image + ButtonItem (Script) tự xử lý click
    // riêng. Đồng thời gắn EventTrigger để bấm có phản hồi punch-scale, hoạt động song song với
    // logic chọn đáp án đã có sẵn trong ButtonItem (không thay thế logic đó).
    void CollectAnswerButtons()
    {
        if (buttonDisplay == null) return;

        var items = buttonDisplay.GetComponentsInChildren<ButtonItem>(true);
        _answerButtonRects = new RectTransform[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            _answerButtonRects[i] = items[i].GetComponent<RectTransform>();

            var trigger = items[i].gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = items[i].gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            var rect = _answerButtonRects[i];
            entry.callback.AddListener(_ => PunchButton(rect, 1.12f, 0.08f));
            trigger.triggers.Add(entry);
        }
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // Điểm mỗi câu có thể lên vài chục (jackpot/hope star/thưởng), không phải +1 như mặc định Kit
    // — nâng mốc fill-bar của HUD lên cho hợp lý hơn khi chơi theo thời gian. Đồng thời đây cũng
    // chính là MỐC THẮNG: đội nào đầy thanh điểm (chạm mốc này) trước sẽ thắng ngay lập tức, xem
    // CheckForFillBarWin().
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
        StopAllAnswerSways();
    }

    void ShowBetPrompt()
    {
        if (betPromptRoot != null) betPromptRoot.SetActive(true);
        UpdateBetUi();

        if (leftHopeStarIcon != null)
            _leftStarPulseCo = StartCoroutine(LoopIdleStarPulse(leftHopeStarIcon));
        if (rightHopeStarIcon != null)
            _rightStarPulseCo = StartCoroutine(LoopIdleStarPulse(rightHopeStarIcon));
    }

    void HideBetPrompt()
    {
        if (betPromptRoot != null) betPromptRoot.SetActive(false);

        if (_leftStarPulseCo != null) { StopCoroutine(_leftStarPulseCo); _leftStarPulseCo = null; }
        if (_rightStarPulseCo != null) { StopCoroutine(_rightStarPulseCo); _rightStarPulseCo = null; }
        if (leftHopeStarIcon != null) leftHopeStarIcon.localScale = Vector3.one;
        if (rightHopeStarIcon != null) rightHopeStarIcon.localScale = Vector3.one;
    }

    // Nhấp nháy nhẹ liên tục trong lúc chờ chọn — tái dùng PulseEffect đã có sẵn trong project
    // (dùng cho starBurst khi trả lời đúng), lặp vô hạn cho tới khi HideBetPrompt dừng coroutine.
    IEnumerator LoopIdleStarPulse(RectTransform icon)
    {
        var cg = icon.GetComponent<CanvasGroup>();
        while (true)
            yield return PulseEffect.ScaleFadePulse(icon, cg, 0.9f, 1.15f, starIdlePulseDuration);
    }

    void UpdateBetUi()
    {
        if (leftStarCountText != null) leftStarCountText.text = $"{_stars.Remaining(Team.Left)}";
        if (rightStarCountText != null) rightStarCountText.text = $"{_stars.Remaining(Team.Right)}";

        if (leftYesButton != null) leftYesButton.interactable = !_leftDecided && _stars.Remaining(Team.Left) > 0;
        if (leftNoButton != null) leftNoButton.interactable = !_leftDecided;
        if (rightYesButton != null) rightYesButton.interactable = !_rightDecided && _stars.Remaining(Team.Right) > 0;
        if (rightNoButton != null) rightNoButton.interactable = !_rightDecided;
    }

    // ── Hiệu ứng dùng chung: punch-scale khi bấm ────────────────────────────────

    void PunchButton(RectTransform rect, float scaleUp = 1.15f, float halfDuration = 0.12f)
    {
        if (rect != null) StartCoroutine(PunchScaleRoutine(rect, scaleUp, halfDuration));
    }

    IEnumerator PunchScaleRoutine(RectTransform rect, float scaleUp, float halfDuration)
    {
        Vector3 original = rect.localScale;
        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            rect.localScale = original * Mathf.Lerp(1f, scaleUp, t / halfDuration);
            yield return null;
        }
        t = 0f;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            rect.localScale = original * Mathf.Lerp(scaleUp, 1f, t / halfDuration);
            yield return null;
        }
        rect.localScale = original;
    }

    // ── Hiệu ứng cảm xúc: ăn mừng (thắng điểm) / buồn (bị phạt) ─────────────────

    // Ăn mừng khi thắng điểm (đúng câu / mở hộp quà bonus) — nảy lên 2 nhịp liên tiếp kèm xoay
    // lắc nhẹ 2 chiều, tạo cảm giác hào hứng. Dùng cho cả text feedback lẫn khung ảnh câu hỏi.
    IEnumerator CelebrateRoutine(RectTransform rect)
    {
        if (rect == null) yield break;

        Vector3 original = rect.localScale;
        Quaternion originalRot = rect.localRotation;

        for (int bounce = 0; bounce < 2; bounce++)
        {
            float t = 0f;
            const float duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                float scale = Mathf.Sin(p * Mathf.PI) * 0.25f + 1f; // nảy lên rồi về 1
                rect.localScale = original * scale;
                rect.localRotation = originalRot * Quaternion.Euler(0f, 0f, Mathf.Sin(p * Mathf.PI * 2f) * 10f);
                yield return null;
            }
        }
        rect.localScale = original;
        rect.localRotation = originalRot;
    }

    // Hiệu ứng buồn khi bị trừ điểm (mở hộp quà penalty) — rũ xuống nhẹ (co lại + lệch xuống) rồi
    // trở lại, kiểu "xìu" chứ không giật mạnh như celebrate.
    IEnumerator SadRoutine(RectTransform rect)
    {
        if (rect == null) yield break;

        Vector3 original = rect.localScale;
        Vector2 originalPos = rect.anchoredPosition;

        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float droop = Mathf.Sin(p * Mathf.PI) * 12f; // lún xuống rồi trở lại
            rect.anchoredPosition = originalPos + Vector2.down * droop;
            rect.localScale = original * Mathf.Lerp(1f, 0.9f, Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        rect.anchoredPosition = originalPos;
        rect.localScale = original;
    }

    // Lóe màu ngắn rồi trả về màu gốc — dùng để nhấn mạnh cảm xúc (vàng/xanh cho vui, xám cho
    // buồn) mà không đổi màu vĩnh viễn của text.
    IEnumerator FlashColor(Text text, Color flashColor, float duration = 0.4f)
    {
        if (text == null) yield break;
        Color original = text.color;
        text.color = flashColor;
        yield return new WaitForSeconds(duration);
        text.color = original;
    }

    // Bắn pháo hoa ngay tại vị trí đội thắng vừa mở hộp quà (bonus/share) — dùng chung 1 particle
    // system, tự di chuyển tới đúng vị trí trước khi Play() mỗi lần, không cần Instantiate nhiều bản.
    void PlayFireworksAt(RectTransform anchor)
    {
        if (fireworksVFX == null) return;
        if (anchor != null)
            fireworksVFX.transform.position = anchor.position;
        fireworksVFX.Play();
    }

    // ── Animation pop-in + idle sway cho nút đáp án ─────────────────────────────

    // Nút đáp án "pop in" khi câu hỏi vừa hiện — phồng to kèm xoay nhẹ (lắc qua lại rồi đứng yên),
    // so le từng nút (stagger). Sau khi pop-in xong, mỗi nút tự chuyển sang lắc nhẹ liên tục
    // (idle sway) cho tới khi bị dừng (round mới, hoặc lúc mở hộp quà).
    void AnimateAnswerButtonsPopIn()
    {
        if (_answerButtonRects == null) return;

        StopAllAnswerSways();
        _answerSwayCoroutines = new Coroutine[_answerButtonRects.Length];

        const float stagger = 0.06f;
        for (int i = 0; i < _answerButtonRects.Length; i++)
        {
            if (_answerButtonRects[i] == null) continue;
            int index = i;
            StartCoroutine(PopInThenSway(_answerButtonRects[i], index, index * stagger));
        }
    }

    IEnumerator PopInThenSway(RectTransform rect, int index, float delay)
    {
        yield return AnswerPopInRoutine(rect, delay);
        _answerSwayCoroutines[index] = StartCoroutine(LoopIdleSway(rect, index));
    }

    IEnumerator AnswerPopInRoutine(RectTransform rect, float delay)
    {
        rect.localScale = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        const float duration = 0.3f;
        const float overshoot = 1.12f;
        const float wiggleAngle = 8f; // độ nghiêng lắc nhẹ lúc phồng lên
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;

            float scale = p < 0.6f
                ? Mathf.Lerp(0f, overshoot, p / 0.6f)
                : Mathf.Lerp(overshoot, 1f, (p - 0.6f) / 0.4f);
            rect.localScale = Vector3.one * scale;

            float wiggle = p < 0.6f
                ? Mathf.Sin(p * Mathf.PI * 3f) * wiggleAngle * (1f - p / 0.6f)
                : 0f;
            rect.localRotation = Quaternion.Euler(0f, 0f, wiggle);

            yield return null;
        }
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    // Lắc nhẹ qua lại kiểu con lắc, lệch pha theo index để 8 nút không đung đưa cùng nhịp — trông
    // sống động tự nhiên hơn là đồng bộ cứng nhắc. Chạy vô hạn cho tới khi StopAllAnswerSways gọi.
    IEnumerator LoopIdleSway(RectTransform rect, int index)
    {
        float phaseOffset = index * 0.7f;
        float t = 0f;

        while (true)
        {
            t += Time.deltaTime;
            float angle = Mathf.Sin((t + phaseOffset) * swaySpeed) * swayAngle;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
    }

    void StopAllAnswerSways()
    {
        if (_answerSwayCoroutines == null) return;
        foreach (var co in _answerSwayCoroutines)
            if (co != null) StopCoroutine(co);
        _answerSwayCoroutines = null;

        if (_answerButtonRects != null)
            foreach (var rect in _answerButtonRects)
                if (rect != null) rect.localRotation = Quaternion.identity;
    }

    // ── Question shown ─────────────────────────────────────────────────────────

    protected override void OnQuestionShown(QuestionData q)
    {
        HideAllFeedback();
        if (questionFrameRoot != null) questionFrameRoot.SetActive(true);

        // Bật lại buttonDisplay — ShowMysteryBoxes() có thể đã tắt nó ở round trước (đội thắng
        // chọn hộp quà), câu hỏi mới luôn cần nó hiện ra.
        if (buttonDisplay != null) buttonDisplay.gameObject.SetActive(true);

        string captionNoun = null;
        if (q.questionMediaType == QuestionMediaType.Image)
        {
            var sprite = AssetOverrideLoader.GetSprite(q.questionMediaValue);
            SetQuestionImage(leftQuestionImage, sprite, out captionNoun, q.questionMediaValue);
            SetQuestionImage(rightQuestionImage, sprite, out _, q.questionMediaValue);
        }

        if (questionCaption != null)
        {
            string key = captionNoun ?? LastPathSegment(q.questionMediaValue);
            string noun = CaptionNouns.TryGetValue(key, out var n) ? n : key;
            questionCaption.text = $"Picture of {noun}";
        }

        UpdateRewardBadge();
        AnimateAnswerButtonsPopIn();
    }

    // Cùng 1 sprite gán cho cả 2 ảnh trái/phải. captionKey trả về tên file để hiện caption fallback.
    static void SetQuestionImage(Image img, Sprite sprite, out string captionKey, string path)
    {
        captionKey = LastPathSegment(path);
        if (img == null) return;
        img.sprite = sprite;
        img.gameObject.SetActive(sprite != null);
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
        if (correctAnswerVFX != null)
            correctAnswerVFX.Play();

        // Ăn mừng ngay trên khung ảnh câu hỏi phía đội thắng.
        var winningImage = team == Team.Left ? leftQuestionImage : rightQuestionImage;
        if (winningImage != null)
            StartCoroutine(CelebrateRoutine(winningImage.GetComponent<RectTransform>()));
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

    void ShowMysteryBoxes(Team winner)
    {
        _currentBoxes = MysteryRewardPool.PickThree();
        _awaitingBoxChoice = true;

        StopAllAnswerSways();

        // Ẩn câu hỏi + đáp án phía sau trong lúc chọn hộp quà — tránh rối mắt/lẫn với nền.
        if (questionFrameRoot != null) questionFrameRoot.SetActive(false);
        if (buttonDisplay != null) buttonDisplay.gameObject.SetActive(false);

        var boxes = winner == Team.Left ? leftMysteryBoxes : rightMysteryBoxes;
        RandomizeBoxColors(boxes);
        var root = winner == Team.Left ? leftMysteryBoxRoot : rightMysteryBoxRoot;
        if (root != null) root.gameObject.SetActive(true);
    }

    /// <summary>Trước đây tô màu ngẫu nhiên (multiply-tint) lên mỗi hộp quà cho vui mắt, nhưng
    /// sprite hộp quà đã có màu sắc/chi tiết đẹp sẵn — multiply-tint của Unity UI Image làm ảnh
    /// bị tối/xỉn màu so với thiết kế gốc. Giữ nguyên Color.white (không tint gì cả) để sprite
    /// hiện đúng như thiết kế, không còn random màu nữa.</summary>
    static void RandomizeBoxColors(Button[] boxes)
    {
        if (boxes == null) return;

        foreach (var box in boxes)
        {
            if (box == null) continue;
            var img = box.GetComponent<Image>();
            if (img != null) img.color = Color.white;
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

            // Ăn mừng cho bonus/share (được cộng điểm, kèm pháo hoa), buồn cho penalty (bị trừ điểm).
            var winnerRect = winnerText.GetComponent<RectTransform>();
            if (reward.kind == RewardKind.PenaltyFixed)
            {
                StartCoroutine(SadRoutine(winnerRect));
                StartCoroutine(FlashColor(winnerText, new Color(0.5f, 0.5f, 0.5f))); // xám buồn
            }
            else
            {
                StartCoroutine(CelebrateRoutine(winnerRect));
                StartCoroutine(FlashColor(winnerText, new Color(1f, 0.75f, 0.1f))); // vàng ăn mừng
                PlayFireworksAt(team == Team.Left ? leftMysteryBoxRoot : rightMysteryBoxRoot);
            }
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

                var opponentRect = opponentText.GetComponent<RectTransform>();
                StartCoroutine(CelebrateRoutine(opponentRect));
                StartCoroutine(FlashColor(opponentText, new Color(1f, 0.75f, 0.1f)));
            }
        }

        // Kiểm tra điều kiện thắng ngay: nếu 1 trong 2 đội vừa đầy thanh điểm (chạm
        // HudMaxScoreFallback), kết thúc game NGAY thay vì chờ hết giờ — cho xem hiệu ứng ăn mừng
        // vừa chạy ở trên (winDelaySeconds) rồi mới chuyển sang màn hình kết thúc.
        if (CheckForFillBarWin())
            StartCoroutine(GameOverAfterDelay(winDelaySeconds));
        else
            base.StateMachineEnter_Feedback(_pendingPrevState, _pendingOpts);
    }

    // Trả về true nếu 1 trong 2 đội đã đạt/vượt mốc HudMaxScoreFallback (thanh điểm đầy).
    bool CheckForFillBarWin()
    {
        return ScoreManager.ScoreLeft >= HudMaxScoreFallback || ScoreManager.ScoreRight >= HudMaxScoreFallback;
    }

    IEnumerator GameOverAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Fsm.StateMachineChange(MiniGameState.GameOver);
    }
}