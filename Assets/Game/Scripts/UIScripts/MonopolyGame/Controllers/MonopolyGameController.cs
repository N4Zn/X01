using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "MONOPOLY" — lấy ý tưởng từ cờ tỷ phú: mỗi đội có xúc xắc RIÊNG (2 viên) ở bên mình, đổ xong
/// di chuyển quân cờ trên board. Ô Question chưa ai chiếm → trả lời đúng để CHIẾM Ô (được điểm =
/// giá trị ô, ô đổi màu theo đội). Bước vào ô Question đối phương đã chiếm → bị trừ điểm ngay, rồi
/// có 1 câu hỏi để giành lại — đúng thì ô đổi chủ. Trả lời sai bất kỳ đâu đều -1 điểm. Đổ đôi (2
/// xúc xắc giống mặt) + trả lời đúng → được đổ thêm lần nữa; đổ đôi 3 lần liền → mất lượt kế tiếp.
/// Còn 5 loại ô sự kiện tức thời (mất lượt/thưởng/x2 điểm/trừ điểm/vòng quay may mắn). Thắng =
/// điểm cao hơn khi hết giờ (GameSettings.GameTime).
///
/// KHÔNG kế thừa MiniGameControllerBase — nhịp lượt-chơi luân phiên (roll→move→resolve→đổi lượt,
/// có thể lặp lại nếu đổ đôi) khác hẳn nhịp ShowQuestion/WaitAnswer/Feedback đối xứng 2 đội mà base
/// đó giả định. Dùng thẳng CustomFSMManager (cùng engine reflection Kit đang dùng) với 1 enum
/// trạng thái riêng (MonopolyState, xem MonopolyBoard.cs). Các layer thấp hơn của Kit vẫn tái dùng
/// nguyên: CSV question loading, ButtonDisplay (SetupPlayerIndependent — chỉ 1 đội trả lời mỗi
/// lần, không còn đấu tay đôi 2 đội cùng lúc), ScoreManager, GameHUD, FeedbackEffect/MusicManager
/// sfx, LuckyWheelPool, CycleRevealEffect, CreateFullscreenOverlay.
/// </summary>
public class MonopolyGameController : MonoBehaviour
{
    [Header("Monopoly — refs chung")]
    [SerializeField] GameHUD hud;
    [SerializeField] MiniGameQuestionSource questionSource;
    [SerializeField] TutorialPanel tutorialPanel;
    [SerializeField] string tutorialText = "Monopoly";
    [SerializeField] string sceneNameForRegistry = "MonopolyGame";
    [SerializeField] string nextSceneName = "ScoreScene";
    [SerializeField] string backSceneName = "MenuScene";
    [SerializeField] Button backButton;

    [Header("Monopoly — board")]
    [SerializeField] MonopolyTileType[] tileTypes;   // set bởi SceneBuilder (MonopolyBoard.GenerateLayout)
    [SerializeField] int[] tileValues;               // set bởi SceneBuilder (MonopolyBoard.GenerateValues) — điểm mỗi ô Question
    [SerializeField] RectTransform[] tileAnchors;    // vị trí từng ô trên màn hình, khớp index với tileTypes
    [SerializeField] RectTransform leftToken;
    [SerializeField] RectTransform rightToken;
    [SerializeField] Text turnBannerText;

    [Header("Monopoly — xúc xắc (tách riêng mỗi đội — chỉ bên đang có lượt bấm được)")]
    [SerializeField] Button leftDiceButton;
    [SerializeField] Text leftDiceValueText;
    [SerializeField] Button rightDiceButton;
    [SerializeField] Text rightDiceValueText;
    [Tooltip("Overlay phủ toàn màn hình lúc đang đổ xúc xắc (CreateFullscreenOverlay của Kit) — to, dễ thấy từ xa.")]
    [SerializeField] GameObject diceRollRoot;
    [SerializeField] Text diceRollFullscreenText;

    [Header("Monopoly — đề bài (dùng chung cho mọi câu hỏi tại ô)")]
    [Tooltip("Câu hỏi dạng ảnh (vd hiện ảnh mẹ, hỏi 'This is...') — questionMediaType=Image trong CSV.")]
    [SerializeField] Image questionPromptImage;
    [Tooltip("Câu hỏi dạng chữ (vd 'Where is mom?') — questionMediaType=Text trong CSV. Đáp án khi đó thường là ảnh (answerMediaType=Image) để học sinh chọn đúng hình.")]
    [SerializeField] Text questionPromptText;

    [Header("Monopoly — câu hỏi tại ô (chiếm ô mới HOẶC giành lại ô đối phương)")]
    [SerializeField] GameObject soloQuestionRoot;
    [SerializeField] ButtonDisplay soloButtonDisplay;
    [Tooltip("Container riêng của MỖI bên trong soloButtonDisplay — bật đúng bên đang có lượt, ẩn bên còn lại để không hiện nút trống/vô nghĩa.")]
    [SerializeField] GameObject soloLeftContainer;
    [SerializeField] GameObject soloRightContainer;
    [SerializeField] float soloQuestionTimeout = 15f;

    [Header("Monopoly — vòng quay may mắn")]
    [SerializeField] GameObject luckyWheelRoot;
    [SerializeField] Text luckyWheelResultText;

    [Header("Monopoly — thông báo ô (mất lượt/thưởng/x2/trừ điểm/chiếm ô/đổ đôi)")]
    [SerializeField] Text tileMessageText;

    [Header("Monopoly — feedback mặc định (âm thanh + icon đúng/sai)")]
    [SerializeField] GameObject leftCorrectIcon;
    [SerializeField] GameObject leftWrongIcon;
    [SerializeField] GameObject rightCorrectIcon;
    [SerializeField] GameObject rightWrongIcon;

    [Header("Monopoly — điểm số")]
    [Tooltip("Trả lời sai (chiếm ô mới HOẶC giành lại ô đối phương) — áp dụng chung mọi câu hỏi tại ô.")]
    [SerializeField] int wrongAnswerPenalty = 1;
    [SerializeField] int rewardPoints = 3;
    [SerializeField] int minusPoints = 2;

    ScoreManager _scoreManager;
    CustomFSMManager _fsm;
    Team _currentTeam = Team.Left;
    readonly int[] _position = new int[2];
    readonly bool[] _skipNextTurn = new bool[2];
    readonly int[] _pendingMultiplier = { 1, 1 };
    readonly int[] _doublesStreak = new int[2];
    Team?[] _tileOwner;
    Color[] _originalTileColors;
    bool _lastRollWasDouble;
    bool _lastAnswerCorrect;
    float _gameTimer;
    bool _gameStarted;
    bool _endingGame;

    void Start()
    {
        _scoreManager = new ScoreManager();
        if (hud != null)
        {
            string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Left";
            string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Right";
            hud.Initialize(_scoreManager, leftName, rightName, 10);
        }
        _gameTimer = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;

        _tileOwner = new Team?[tileTypes != null ? tileTypes.Length : 0];
        _originalTileColors = new Color[tileAnchors != null ? tileAnchors.Length : 0];
        if (tileAnchors != null)
        {
            for (int i = 0; i < tileAnchors.Length; i++)
            {
                var img = tileAnchors[i] != null ? tileAnchors[i].GetComponent<Image>() : null;
                _originalTileColors[i] = img != null ? img.color : Color.white;
            }
        }

        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        if (leftDiceButton != null) leftDiceButton.onClick.AddListener(OnDiceClicked);
        if (rightDiceButton != null) rightDiceButton.onClick.AddListener(OnDiceClicked);

        _fsm = gameObject.AddComponent<CustomFSMManager>();
        _fsm.fsmName = "MonopolyFSM";
        _fsm.Initialize(typeof(MonopolyState), GetType(), false);
        _fsm.StateMachineChange(MonopolyState.Initialize);
    }

    void Update()
    {
        if (!_gameStarted || _endingGame) return;
        _gameTimer -= Time.deltaTime;
        hud?.UpdateTimer(Mathf.Max(0f, _gameTimer));
    }

    // ── FSM: khởi động ──────────────────────────────────────────────────────

    void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opts)
        => _fsm.StateMachineChange(MonopolyState.Tutorial);

    void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opts)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialDone;
            tutorialPanel.ShowPlaceholder(tutorialText);
        }
        else StartGame();
    }

    void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opts)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= OnTutorialDone;
    }

    void OnTutorialDone() => StartGame();

    void StartGame()
    {
        _scoreManager.Reset();
        MusicManager.Instance?.PlayGameplayMusic();
        _gameStarted = true;
        PlaceTokenAt(Team.Left, 0);
        PlaceTokenAt(Team.Right, 0);
        _currentTeam = Team.Left;
        _fsm.StateMachineChange(MonopolyState.TurnStart);
    }

    // ── FSM: 1 lượt chơi ─────────────────────────────────────────────────────

    void StateMachineEnter_TurnStart(Enum prev, Dictionary<string, object> opts)
    {
        int idx = (int)_currentTeam;
        if (_skipNextTurn[idx])
        {
            _skipNextTurn[idx] = false;
            ShowTileMessage($"{TeamName(_currentTeam)} mất lượt!");
            StartCoroutine(SkipTurnThenSwap());
            return;
        }

        if (turnBannerText != null) turnBannerText.text = $"Lượt của {TeamName(_currentTeam)}";
        if (leftDiceButton != null) leftDiceButton.interactable = _currentTeam == Team.Left;
        if (rightDiceButton != null) rightDiceButton.interactable = _currentTeam == Team.Right;
    }

    IEnumerator SkipTurnThenSwap()
    {
        yield return new WaitForSeconds(1.5f);
        HideTileMessage();
        SwapTeamAndContinue();
    }

    void SwapTeamAndContinue()
    {
        _currentTeam = _currentTeam == Team.Left ? Team.Right : Team.Left;
        _doublesStreak[(int)_currentTeam] = 0;
        _fsm.StateMachineChange(MonopolyState.TurnStart);
    }

    void OnDiceClicked()
    {
        if (leftDiceButton != null) leftDiceButton.interactable = false;
        if (rightDiceButton != null) rightDiceButton.interactable = false;
        _fsm.StateMachineChange(MonopolyState.Rolling);
    }

    void StateMachineEnter_Rolling(Enum prev, Dictionary<string, object> opts)
        => StartCoroutine(RollDiceCoroutine());

    IEnumerator RollDiceCoroutine()
    {
        int d1 = UnityEngine.Random.Range(1, 7);
        int d2 = UnityEngine.Random.Range(1, 7);
        _lastRollWasDouble = d1 == d2;
        int sum = d1 + d2;

        if (diceRollRoot != null) diceRollRoot.SetActive(true);
        yield return CycleRevealEffect.Spin(diceRollFullscreenText,
            () => $"{UnityEngine.Random.Range(1, 7)} + {UnityEngine.Random.Range(1, 7)}",
            _lastRollWasDouble ? $"{d1} + {d2}  (ĐÔI!)" : $"{d1} + {d2}");

        var diceText = _currentTeam == Team.Left ? leftDiceValueText : rightDiceValueText;
        if (diceText != null) diceText.text = $"{d1} + {d2} = {sum}";

        yield return new WaitForSeconds(0.6f);
        if (diceRollRoot != null) diceRollRoot.SetActive(false);
        _fsm.StateMachineChange(MonopolyState.Moving, new Dictionary<string, object> { { "steps", sum } });
    }

    void StateMachineEnter_Moving(Enum prev, Dictionary<string, object> opts)
    {
        int steps = opts != null && opts.TryGetValue("steps", out var s) ? (int)s : 1;
        StartCoroutine(MoveTokenCoroutine(steps));
    }

    IEnumerator MoveTokenCoroutine(int steps)
    {
        int idx = (int)_currentTeam;
        for (int i = 0; i < steps; i++)
        {
            _position[idx] = (_position[idx] + 1) % tileTypes.Length;
            PlaceTokenAt(_currentTeam, _position[idx]);
            yield return new WaitForSeconds(0.15f);
        }
        _fsm.StateMachineChange(MonopolyState.ResolveTile);
    }

    void PlaceTokenAt(Team team, int tileIndex)
    {
        if (tileAnchors == null || tileIndex < 0 || tileIndex >= tileAnchors.Length) return;
        var token = team == Team.Left ? leftToken : rightToken;
        if (token == null) return;
        token.anchorMin = tileAnchors[tileIndex].anchorMin;
        token.anchorMax = tileAnchors[tileIndex].anchorMax;
    }

    // ── FSM: xử lý ô đích ────────────────────────────────────────────────────

    void StateMachineEnter_ResolveTile(Enum prev, Dictionary<string, object> opts)
        => StartCoroutine(ResolveTileCoroutine());

    IEnumerator ResolveTileCoroutine()
    {
        int idx = (int)_currentTeam;
        int pos = _position[idx];
        bool answeredCorrectly = false;

        if (tileTypes[pos] == MonopolyTileType.Question)
        {
            Team? owner = _tileOwner[pos];
            if (owner == null)
            {
                yield return SoloQuestionCoroutine(pos);
                answeredCorrectly = _lastAnswerCorrect;
                if (answeredCorrectly)
                {
                    _tileOwner[pos] = _currentTeam;
                    MarkTileOwner(pos, _currentTeam);
                }
            }
            else if (owner.Value == _currentTeam)
            {
                ShowTileMessage("Ô của bạn rồi!");
                yield return new WaitForSeconds(1.0f);
                HideTileMessage();
            }
            else
            {
                // Xâm phạm ô đối phương đang chiếm — trừ điểm ngay, rồi cho cơ hội trả lời để giành lại.
                _scoreManager.AddPoints(_currentTeam, -wrongAnswerPenalty);
                ShowTileMessage($"Ô của {TeamName(owner.Value)}! {FeedbackTextStyle.Loss(wrongAnswerPenalty)}");
                yield return new WaitForSeconds(1.2f);
                HideTileMessage();

                yield return SoloQuestionCoroutine(pos);
                answeredCorrectly = _lastAnswerCorrect;
                if (answeredCorrectly)
                {
                    _tileOwner[pos] = _currentTeam;
                    MarkTileOwner(pos, _currentTeam);
                    ShowTileMessage($"{TeamName(_currentTeam)} đã giành được ô!");
                    yield return new WaitForSeconds(1.2f);
                    HideTileMessage();
                }
            }
        }
        else
        {
            switch (tileTypes[pos])
            {
                case MonopolyTileType.LoseTurn:
                    _skipNextTurn[idx] = true;
                    ShowTileMessage($"{TeamName(_currentTeam)} sẽ mất lượt kế tiếp!");
                    yield return new WaitForSeconds(1.5f);
                    HideTileMessage();
                    break;

                case MonopolyTileType.Reward:
                    AwardPoints(_currentTeam, rewardPoints);
                    ShowTileMessage($"Thưởng! {FeedbackTextStyle.Gain(rewardPoints)}");
                    yield return new WaitForSeconds(1.5f);
                    HideTileMessage();
                    break;

                case MonopolyTileType.DoublePoints:
                    _pendingMultiplier[idx] = 2;
                    ShowTileMessage($"{TeamName(_currentTeam)}: x2 điểm cho lần ghi điểm tiếp theo!");
                    yield return new WaitForSeconds(1.5f);
                    HideTileMessage();
                    break;

                case MonopolyTileType.MinusPoints:
                    _scoreManager.AddPoints(_currentTeam, -minusPoints);
                    ShowTileMessage($"Mất điểm! {FeedbackTextStyle.Loss(minusPoints)}");
                    yield return new WaitForSeconds(1.5f);
                    HideTileMessage();
                    break;

                case MonopolyTileType.LuckyWheel:
                    yield return LuckyWheelCoroutine();
                    break;
            }
        }

        // Đổ đôi (2 xúc xắc giống mặt): luôn tính vào chuỗi liên tiếp, bất kể ô gì. 3 lần liền →
        // mất lượt kế tiếp (không được đổ thêm nữa dù câu vừa rồi đúng). Chưa tới 3 lần + vừa trả
        // lời ĐÚNG (chỉ có ở ô Question) → được đổ xúc xắc thêm 1 lần, vẫn là lượt của đội này.
        if (_lastRollWasDouble)
        {
            _doublesStreak[idx]++;
            if (_doublesStreak[idx] >= 3)
            {
                _skipNextTurn[idx] = true;
                ShowTileMessage($"{TeamName(_currentTeam)} đổ đôi 3 lần liền — mất lượt kế tiếp!");
                yield return new WaitForSeconds(1.6f);
                HideTileMessage();
                _fsm.StateMachineChange(MonopolyState.EndTurn);
                yield break;
            }
            if (answeredCorrectly)
            {
                ShowTileMessage("Đổ đôi + trả lời đúng — đổ xúc xắc thêm 1 lần!");
                yield return new WaitForSeconds(1.4f);
                HideTileMessage();
                _fsm.StateMachineChange(MonopolyState.TurnStart);
                yield break;
            }
        }

        _fsm.StateMachineChange(MonopolyState.EndTurn);
    }

    /// <summary>Câu hỏi tại 1 ô cụ thể — dùng chung cho CẢ chiếm ô mới lẫn giành lại ô đối phương.
    /// Đúng → +giá trị ô (tileValues[tileIndex], có nhân theo _pendingMultiplier). Sai → -wrongAnswerPenalty
    /// (áp dụng chung mọi câu hỏi tại ô, không nhân). Kết quả trả về qua _lastAnswerCorrect (coroutine
    /// không return được giá trị — caller đọc field này ngay sau khi yield return xong).</summary>
    IEnumerator SoloQuestionCoroutine(int tileIndex)
    {
        _lastAnswerCorrect = false;
        var q = PullQuestion();
        if (q == null || soloButtonDisplay == null) yield break;

        if (soloQuestionRoot != null) soloQuestionRoot.SetActive(true);
        soloButtonDisplay.gameObject.SetActive(true);
        if (soloLeftContainer != null) soloLeftContainer.SetActive(_currentTeam == Team.Left);
        if (soloRightContainer != null) soloRightContainer.SetActive(_currentTeam == Team.Right);
        ShowQuestionPrompt(q);

        bool done = false;
        bool correct = false;

        soloButtonDisplay.SetupPlayerIndependent(_currentTeam, q, (isCorrect, team, answer) =>
        {
            done = true;
            correct = isCorrect;
        });

        float elapsed = 0f;
        while (!done && elapsed < soloQuestionTimeout) { elapsed += Time.deltaTime; yield return null; }

        if (correct) AwardPoints(_currentTeam, tileValues[tileIndex]);
        else _scoreManager.AddPoints(_currentTeam, -wrongAnswerPenalty);
        PlayFeedback(_currentTeam, correct);
        _lastAnswerCorrect = correct;

        yield return new WaitForSeconds(correct ? 1.2f : 1.5f);
        HideFeedbackIcons();
        HideQuestionPrompt();
        soloButtonDisplay.Cleanup();
        if (soloQuestionRoot != null) soloQuestionRoot.SetActive(false);
    }

    /// <summary>Đề bài dùng chung cho mọi câu hỏi tại ô — CSV trộn 2 kiểu tự do (đúng yêu cầu đề
    /// bài): questionMediaType=Image (hiện ảnh, đáp án chữ) HOẶC =Text (hiện chữ hỏi, đáp án
    /// thường là ảnh — answerMediaType=Image, ButtonItem tự vẽ ảnh cho từng đáp án).</summary>
    void ShowQuestionPrompt(QuestionData q)
    {
        bool isImage = q.questionMediaType == QuestionMediaType.Image;
        if (questionPromptImage != null)
        {
            var sprite = isImage ? Resources.Load<Sprite>(q.questionMediaValue) : null;
            questionPromptImage.sprite = sprite;
            questionPromptImage.gameObject.SetActive(isImage && sprite != null);
        }
        if (questionPromptText != null)
        {
            questionPromptText.text = isImage ? "" : q.questionMediaValue;
            questionPromptText.gameObject.SetActive(!isImage);
        }
    }

    void HideQuestionPrompt()
    {
        if (questionPromptImage != null) questionPromptImage.gameObject.SetActive(false);
        if (questionPromptText != null) questionPromptText.gameObject.SetActive(false);
    }

    IEnumerator LuckyWheelCoroutine()
    {
        if (luckyWheelRoot != null) luckyWheelRoot.SetActive(true);

        var outcome = LuckyWheelPool.PickOne();
        yield return CycleRevealEffect.Spin(luckyWheelResultText,
            () => LuckyWheelPool.All[UnityEngine.Random.Range(0, LuckyWheelPool.All.Length)].message,
            outcome.message, 1.2f, 0.08f);

        if (outcome.amount > 0) AwardPoints(_currentTeam, outcome.amount);
        else if (outcome.amount < 0) _scoreManager.AddPoints(_currentTeam, outcome.amount);

        yield return new WaitForSeconds(1.5f);
        if (luckyWheelRoot != null) luckyWheelRoot.SetActive(false);
    }

    /// <summary>Tô màu ô theo đội đang chiếm (blend với màu gốc của loại ô) — cho học sinh thấy rõ
    /// ô nào đã bị chiếm và bởi đội nào, giống ô "sở hữu" trong cờ tỷ phú thật.</summary>
    void MarkTileOwner(int tileIndex, Team team)
    {
        if (tileAnchors == null || tileIndex < 0 || tileIndex >= tileAnchors.Length) return;
        var img = tileAnchors[tileIndex].GetComponent<Image>();
        if (img == null) return;
        Color baseColor = _originalTileColors != null && tileIndex < _originalTileColors.Length ? _originalTileColors[tileIndex] : Color.white;
        img.color = Color.Lerp(baseColor, TeamColor(team), 0.65f);
    }

    static Color TeamColor(Team team) => team == Team.Left ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.90f, 0.30f, 0.30f);

    // ── FSM: kết thúc lượt / ván ─────────────────────────────────────────────

    void StateMachineEnter_EndTurn(Enum prev, Dictionary<string, object> opts)
    {
        if (_gameTimer <= 0f)
        {
            _fsm.StateMachineChange(MonopolyState.GameOver);
            return;
        }
        SwapTeamAndContinue();
    }

    void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opts)
    {
        _endingGame = true;
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.RecordScores(_scoreManager.ScoreLeft, _scoreManager.ScoreRight);
            if (!string.IsNullOrEmpty(sceneNameForRegistry))
                GameSessionManager.Instance.LastPlayedGame = sceneNameForRegistry;
        }
        MusicManager.Instance?.PlayMainMusic();
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    void GoBackToMenu()
    {
        MusicManager.Instance?.PlayMainMusic();
        if (!string.IsNullOrEmpty(backSceneName)) SceneManager.LoadScene(backSceneName);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    QuestionData PullQuestion() => questionSource != null && questionSource.HasChoose ? questionSource.GetNextChoose() : null;

    void AwardPoints(Team team, int basePoints)
    {
        int idx = (int)team;
        int multiplier = _pendingMultiplier[idx];
        _pendingMultiplier[idx] = 1;
        _scoreManager.AddPoints(team, basePoints * multiplier);
    }

    void ShowTileMessage(string msg)
    {
        if (tileMessageText == null) return;
        tileMessageText.text = msg;
        tileMessageText.gameObject.SetActive(true);
    }

    void HideTileMessage()
    {
        if (tileMessageText != null) tileMessageText.gameObject.SetActive(false);
    }

    static string TeamName(Team team)
    {
        if (GameSessionManager.Instance == null) return team == Team.Left ? "Player 1" : "Player 2";
        return team == Team.Left ? GameSessionManager.Instance.GetDisplayName1() : GameSessionManager.Instance.GetDisplayName2();
    }

    // Âm thanh + icon đúng/sai — cùng cơ chế mặc định của MiniGameControllerBase (FeedbackEffect +
    // MusicManager), viết lại thủ công ở đây vì controller này không kế thừa base đó.
    void PlayFeedback(Team team, bool correct)
    {
        if (correct) MusicManager.Instance?.PlayCorrectSfx();
        else MusicManager.Instance?.PlayWrongSfx();

        var show = team == Team.Left ? (correct ? leftCorrectIcon : leftWrongIcon) : (correct ? rightCorrectIcon : rightWrongIcon);
        var hide = team == Team.Left ? (correct ? leftWrongIcon : leftCorrectIcon) : (correct ? rightWrongIcon : rightCorrectIcon);
        if (hide != null) hide.SetActive(false);
        if (show == null) return;

        show.SetActive(true);
        var fx = show.GetComponent<FeedbackEffect>();
        if (fx == null) fx = show.AddComponent<FeedbackEffect>();
        fx.Play(correct, 1f, false);
    }

    void HideFeedbackIcons()
    {
        if (leftCorrectIcon != null) leftCorrectIcon.SetActive(false);
        if (leftWrongIcon != null) leftWrongIcon.SetActive(false);
        if (rightCorrectIcon != null) rightCorrectIcon.SetActive(false);
        if (rightWrongIcon != null) rightWrongIcon.SetActive(false);
    }
}
