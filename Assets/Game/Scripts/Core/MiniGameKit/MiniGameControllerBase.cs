using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Base lifecycle cho mini-game dạng "quiz-shaped": hiện câu hỏi → chờ trả lời → feedback → lặp → tổng kết.
///
/// Đóng gói đúng phần bị copy-paste giữa AddUpGameController/TrainPathGameController hiện có:
/// khởi tạo CustomFSMManager, wiring ScoreManager+GameHUD, lấy câu hỏi từ MiniGameQuestionSource,
/// coroutine feedback-delay/timeout, ghi điểm GameSessionManager rồi load ScoreScene.
///
/// Game mới KHÔNG cần viết lại phần trên — chỉ override các hook ở cuối file
/// (GetDisplayForQuestion bắt buộc; OnQuestionShown/OnRoundResult/OnPlayerFailed tuỳ chọn),
/// hoặc override thẳng StateMachineEnter_X/StateMachineExit_X (protected virtual) nếu cần
/// thay đổi hẳn 1 state.
///
/// KHÔNG bắt buộc dùng: game có cơ chế liên tục / phi lượt (vd kéo-thả liên tục kiểu RiverCrossGame)
/// có thể bỏ qua class này, chỉ dùng ScoreManager + GameHUD trực tiếp.
///
/// 3 chế độ chơi qua field playMode (xem MiniGamePlayMode.cs):
///   Combined    (mặc định) — 2 bên cùng 1 câu qua IAnswerDisplay.Setup, ai đúng trước ghi điểm.
///   Solo        — 1 luồng chơi, kỹ thuật giống Combined (xem MiniGamePlayMode.cs).
///   Independent — 2 bên tự nhịp câu hỏi riêng; subclass bắt buộc override SetupIndependentDisplay().
/// </summary>
public abstract class MiniGameControllerBase : MonoBehaviour
{
    [Header("MiniGame Kit — refs")]
    [SerializeField] protected GameHUD hud;
    [SerializeField] protected MiniGameQuestionSource questionSource;
    [SerializeField] protected TutorialPanel tutorialPanel;
    [SerializeField] protected UnityEngine.Video.VideoClip tutorialClip;
    [SerializeField] protected string tutorialText = "Huong dan";

    [Header("MiniGame Kit — timing")]
    [Tooltip("Mặc định 0 = không giới hạn thời gian mỗi câu (chờ vô hạn, học sinh cần bao lâu cũng được).")]
    [SerializeField] protected float questionTimeout = 0f;
    [SerializeField] protected float feedbackDelayCorrect = 1.2f;
    [SerializeField] protected float feedbackDelayWrong = 2f;
    [Tooltip("Mặc định 0 = chơi theo thời gian (GameSettings.GameTime), câu hỏi tự shuffle lại trong pool khi hết — KHÔNG giới hạn theo số câu. Đặt > 0 nếu game cần đúng N câu/lượt (vd bài kiểm tra cố định).")]
    [SerializeField] protected int totalRounds = 0;

    [Header("MiniGame Kit — scene")]
    [Tooltip("Tên game để ghi vào GameSessionManager.LastPlayedGame (khớp GameRegistry).")]
    [SerializeField] protected string sceneNameForRegistry = "";
    [SerializeField] protected string nextSceneName = "ScoreScene";
    [SerializeField] protected string backSceneName = "MenuScene";

    [Header("MiniGame Kit — play mode")]
    [SerializeField] protected MiniGamePlayMode playMode = MiniGamePlayMode.Combined;

    [Header("MiniGame Kit — feedback mặc định (âm thanh + icon đúng/sai)")]
    [Tooltip("Để trống nếu game không dựng icon riêng — hiệu ứng vẫn chạy phần âm thanh, chỉ bỏ qua icon.")]
    [SerializeField] protected GameObject leftCorrectIcon;
    [SerializeField] protected GameObject leftWrongIcon;
    [SerializeField] protected GameObject rightCorrectIcon;
    [SerializeField] protected GameObject rightWrongIcon;

    [Header("MiniGame Kit — khoảng chờ chuyển câu (đứng lại di chuyển)")]
    [Tooltip("Text 'Next in Ns' mỗi bên trong lúc chờ chuyển câu — để trống nếu không cần hiện chữ (khoảng chờ vẫn chạy, chỉ không có số đếm ngược hiện ra).")]
    [SerializeField] protected Text leftCountdownText;
    [SerializeField] protected Text rightCountdownText;

    public ScoreManager ScoreManager { get; private set; }
    protected CustomFSMManager Fsm { get; private set; }
    protected QuestionData CurrentQuestion { get; private set; }

    int _roundIndex;
    float _gameTimer;
    bool _useTimer;
    Coroutine _timeoutCoroutine;
    Coroutine _nextRoundCoroutine;

    // ── Independent mode state (playMode == Independent) ─────────────────────
    bool _independentRunning;
    int _leftRoundIndex;
    int _rightRoundIndex;
    Coroutine _leftIndependentLoop;
    Coroutine _rightIndependentLoop;

    protected virtual void Start()
    {
        InitScoring();
        InitFsm();
    }

protected virtual void Update()
{
    // Đếm ngược liên tục trong suốt quá trình chơi (WaitAnswer, ShowQuestion, Feedback)
    var currentState = GetCurrentState();
    bool isGamePlaying = currentState == MiniGameState.WaitAnswer 
                      || currentState == MiniGameState.ShowQuestion 
                      || currentState == MiniGameState.Feedback;

    if (_useTimer && isGamePlaying)
    {
        _gameTimer -= Time.deltaTime;
        hud?.UpdateTimer(_gameTimer);
        if (_gameTimer <= 0f)
        {
            _gameTimer = 0f;
            Fsm.StateMachineChange(MiniGameState.GameOver);
        }
    }
}
    protected virtual void Update()
    {
        // Timer chạy liên tục trong suốt ván (WaitAnswer + ShowQuestion + Feedback)
        // để tránh timer bị dừng trong khoảng chờ feedback/chuyển câu.
        var currentState = GetCurrentState();
        bool isGamePlaying = currentState == MiniGameState.WaitAnswer
                          || currentState == MiniGameState.ShowQuestion
                          || currentState == MiniGameState.Feedback;

        if (_useTimer && isGamePlaying)
        {
            _gameTimer -= Time.deltaTime;
            hud?.UpdateTimer(_gameTimer);
            if (_gameTimer <= 0f)
            {
                _gameTimer = 0f;
                Fsm.StateMachineChange(MiniGameState.GameOver);
            }
        }
    }

    // ── Setup ────────────────────────────────────────────────────────────────

    void InitScoring()
    {
        ScoreManager = new ScoreManager();
        _useTimer = totalRounds <= 0;

        if (hud != null)
        {
            string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Left";
            string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Right";
            hud.Initialize(ScoreManager, leftName, rightName, totalRounds > 0 ? totalRounds : HudMaxScoreFallback);
            if (_useTimer) { /* timer text driven by Update() below */ }
            else hud.HideTimer();
        }

        if (_useTimer)
            _gameTimer = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;
    }

    void InitFsm()
    {
        Fsm = gameObject.AddComponent<CustomFSMManager>();
        Fsm.fsmName = GetType().Name + "FSM";
        Fsm.Initialize(typeof(MiniGameState), GetType(), false);
        Fsm.StateMachineChange(MiniGameState.Initialize);
    }

    public MiniGameState GetCurrentState()
    {
        if (Fsm == null) return MiniGameState.Initialize;
        var s = Fsm.GetCurrentState();
        return s == null ? MiniGameState.Initialize : (MiniGameState)s;
    }

    // ── Canonical state handlers — protected virtual, override khi cần ────────

    protected virtual void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opts)
    {
        Fsm.StateMachineChange(MiniGameState.Tutorial);
    }

    protected virtual void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opts)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialDone;
            if (tutorialClip != null) tutorialPanel.Show(tutorialClip, tutorialText);
            else tutorialPanel.ShowPlaceholder(tutorialText);
        }
        else
        {
            StartGame();
        }
    }

    protected virtual void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opts)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= OnTutorialDone;
    }

    void OnTutorialDone() => StartGame();

    protected virtual void StateMachineEnter_ShowQuestion(Enum prev, Dictionary<string, object> opts)
    {
        if (UseDefaultFeedbackFx) HideDefaultFeedbackIcons();

        _roundIndex++;
        CurrentQuestion = (totalRounds <= 0 || _roundIndex <= totalRounds) ? PullNextQuestion() : null;

        if (CurrentQuestion == null)
        {
            Fsm.StateMachineChange(MiniGameState.GameOver);
            return;
        }

        OnQuestionShown(CurrentQuestion);
        Fsm.StateMachineChange(MiniGameState.WaitAnswer);
    }

    /// <summary>Mặc định: Choose trước, fallback Matching. Override nếu cần logic chọn câu khác.</summary>
    protected virtual QuestionData PullNextQuestion()
    {
        if (questionSource == null) return null;
        if (questionSource.HasChoose) return questionSource.GetNextChoose();
        if (questionSource.HasMatching) return questionSource.GetNextMatching();
        return null;
    }

    protected virtual void StateMachineEnter_WaitAnswer(Enum prev, Dictionary<string, object> opts)
    {
        // Independent mode: FSM chỉ "đỗ" ở WaitAnswer suốt ván — 2 IndependentPlayerLoop tự lo
        // hiện câu hỏi/chấm điểm riêng, không đi qua đường ShowQuestion/WaitAnswer/Feedback chung.
        if (playMode == MiniGamePlayMode.Independent) return;

        var display = GetDisplayForQuestion(CurrentQuestion);
        if (display == null)
        {
            Debug.LogError("[MiniGameKit] GetDisplayForQuestion() trả về null — không thể hiện câu hỏi.");
            return;
        }
        (display as MonoBehaviour)?.gameObject.SetActive(true);
        display.Setup(CurrentQuestion, HandleResult, HandlePlayerFailed);

        if (questionTimeout > 0)
        {
            if (_timeoutCoroutine != null) StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = StartCoroutine(TimeoutCoroutine());
        }
    }

    IEnumerator TimeoutCoroutine()
    {
        yield return new WaitForSeconds(questionTimeout);
        if (GetCurrentState() != MiniGameState.WaitAnswer) yield break;

        OnQuestionTimeout();
        Fsm.StateMachineChange(MiniGameState.Feedback, new Dictionary<string, object> { { "correct", false } });
    }

    void HandleResult(bool correct, Team team, int[] playerAnswer)
    {
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        if (correct) AwardDefaultPoint(team);
        if (correct && UseDefaultFeedbackFx) PlayDefaultFeedbackFx(team, true);
        OnRoundResult(correct, team, playerAnswer);
        Fsm.StateMachineChange(MiniGameState.Feedback,
            new Dictionary<string, object> { { "correct", correct }, { "team", team } });
    }

    /// <summary>Mặc định +1 điểm khi trả lời đúng (Combined mode chuẩn). Override thành no-op
    /// nếu game tự quản lý điểm theo cách khác (vd reward system tính điểm ở nơi khác — xem
    /// WhoIsItGameController) để tránh cộng điểm 2 lần.</summary>
    protected virtual void AwardDefaultPoint(Team team) => ScoreManager.AddPoint(team);

    void HandlePlayerFailed(Team team)
    {
        if (UseDefaultFeedbackFx) PlayDefaultFeedbackFx(team, false);
        OnPlayerFailed(team);
    }

    /// <summary>Bật/tắt hiệu ứng mặc định của Kit khi trả lời đúng/sai — âm thanh
    /// (MusicManager.PlayCorrectSfx/PlayWrongSfx) + icon ✔/✖ bounce (FeedbackEffect, tái dùng
    /// nguyên khối từ TestTongHopGame/AddUpGame — xem ShowCorrectIcon/ShowWrongIcon ở đó).
    /// Override thành false nếu game tự làm feedback riêng (vd WhoIsItGame đã có text/sfx/mystery
    /// box riêng) để tránh phát âm thanh 2 lần.</summary>
    protected virtual bool UseDefaultFeedbackFx => true;

    void PlayDefaultFeedbackFx(Team team, bool correct)
    {
        if (correct) MusicManager.Instance?.PlayCorrectSfx();
        else MusicManager.Instance?.PlayWrongSfx();

        var showIcon = team == Team.Left
            ? (correct ? leftCorrectIcon : leftWrongIcon)
            : (correct ? rightCorrectIcon : rightWrongIcon);
        var hideIcon = team == Team.Left
            ? (correct ? leftWrongIcon : leftCorrectIcon)
            : (correct ? rightWrongIcon : rightCorrectIcon);

        if (hideIcon != null) hideIcon.SetActive(false);
        if (showIcon == null) return;

        showIcon.SetActive(true);
        var fx = showIcon.GetComponent<FeedbackEffect>();
        if (fx == null) fx = showIcon.AddComponent<FeedbackEffect>();
        // ~1s bounce + pulse/shake, KHÔNG tự fade — ẩn hẳn (SetActive(false)) ở NextRoundAfterDelay
        // ngay trước khi vào countdown, để không có khoảng chồng hình icon ↔ "Next in Ns".
        fx.Play(correct, 1f, false);
    }

    void HideDefaultFeedbackIcons()
    {
        if (leftCorrectIcon != null) leftCorrectIcon.SetActive(false);
        if (leftWrongIcon != null) leftWrongIcon.SetActive(false);
        if (rightCorrectIcon != null) rightCorrectIcon.SetActive(false);
        if (rightWrongIcon != null) rightWrongIcon.SetActive(false);
    }

    protected virtual void StateMachineEnter_Feedback(Enum prev, Dictionary<string, object> opts)
    {
        bool correct = opts != null && opts.TryGetValue("correct", out var c) && (bool)c;
        float delay = correct ? feedbackDelayCorrect : feedbackDelayWrong;
        if (_nextRoundCoroutine != null) StopCoroutine(_nextRoundCoroutine);
        _nextRoundCoroutine = StartCoroutine(NextRoundAfterDelay(delay));
    }

    IEnumerator NextRoundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CleanupCurrentDisplay();
        // Ẩn HẲN icon đúng/sai (không chờ tự fade) trước khi vào countdown — tránh chồng hình
        // icon ↔ "Next in Ns", đúng thứ tự: hiện icon ~1s → ẩn hết → mới hiện countdown.
        if (UseDefaultFeedbackFx) HideDefaultFeedbackIcons();
        if (UseDefaultTransitionCountdown) yield return TransitionCountdown();
        Fsm.StateMachineChange(MiniGameState.ShowQuestion);
    }

    /// <summary>Bật/tắt khoảng chờ "Next in Ns" mặc định của Kit giữa các câu — game chiếu sàn,
    /// học sinh vừa trả lời xong thường còn đứng nguyên tại ô vừa chọn, chưa kịp di chuyển. Thời
    /// lượng lấy từ setting tổng GameSettings.RoundEndDelay (menu Cài đặt chung, 1-4s), KHÔNG
    /// hard-code riêng từng game. Override thành false nếu game tự có nhịp chuyển câu riêng.</summary>
    protected virtual bool UseDefaultTransitionCountdown => true;

    IEnumerator TransitionCountdown()
    {
        int seconds = Mathf.Max(0, Mathf.RoundToInt(
            GameSettings.Instance != null ? GameSettings.Instance.RoundEndDelay : 2f));
        if (seconds <= 0) yield break;

        for (int i = seconds; i >= 1; i--)
        {
            ShowTransitionCountdown(i);
            yield return new WaitForSeconds(1f);
        }
        HideTransitionCountdown();
    }

    void ShowTransitionCountdown(int secondsLeft)
    {
        string msg = $"Next in {secondsLeft}s";
        if (leftCountdownText != null) { leftCountdownText.text = msg; leftCountdownText.gameObject.SetActive(true); }
        if (rightCountdownText != null) { rightCountdownText.text = msg; rightCountdownText.gameObject.SetActive(true); }
    }

    void HideTransitionCountdown()
    {
        if (leftCountdownText != null) leftCountdownText.gameObject.SetActive(false);
        if (rightCountdownText != null) rightCountdownText.gameObject.SetActive(false);
    }

    protected virtual void CleanupCurrentDisplay()
    {
        GetDisplayForQuestion(CurrentQuestion)?.Cleanup();
    }

    protected virtual void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opts)
    {
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        if (_nextRoundCoroutine != null) { StopCoroutine(_nextRoundCoroutine); _nextRoundCoroutine = null; }

        if (_independentRunning)
        {
            _independentRunning = false;
            if (_leftIndependentLoop != null) { StopCoroutine(_leftIndependentLoop); _leftIndependentLoop = null; }
            if (_rightIndependentLoop != null) { StopCoroutine(_rightIndependentLoop); _rightIndependentLoop = null; }
        }

        CleanupCurrentDisplay();

        if (GameSessionManager.Instance != null)
        {
            int leftScore = ScoreManager.ScoreLeft;
            int rightScore = playMode == MiniGamePlayMode.Solo ? ScoreManager.ScoreLeft : ScoreManager.ScoreRight;
            GameSessionManager.Instance.RecordScores(leftScore, rightScore);
            if (!string.IsNullOrEmpty(sceneNameForRegistry))
                GameSessionManager.Instance.LastPlayedGame = sceneNameForRegistry;
        }
        MusicManager.Instance?.PlayMainMusic();
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    // ── Actions dùng chung — gọi từ UI event (nút Retry/Back) hoặc từ hook ────

    protected void StartGame()
    {
        _roundIndex = 0;
        _leftRoundIndex = 0;
        _rightRoundIndex = 0;
        ScoreManager.Reset();
        MusicManager.Instance?.PlayGameplayMusic();

        if (playMode == MiniGamePlayMode.Independent)
        {
            _independentRunning = true;
            Fsm.StateMachineChange(MiniGameState.WaitAnswer);
            _leftIndependentLoop = StartCoroutine(IndependentPlayerLoop(Team.Left));
            _rightIndependentLoop = StartCoroutine(IndependentPlayerLoop(Team.Right));
        }
        else
        {
            Fsm.StateMachineChange(MiniGameState.ShowQuestion);
        }
    }

    // ── Independent mode ───────────────────────────────────────────────────────

    IEnumerator IndependentPlayerLoop(Team team)
    {
        while (_independentRunning)
        {
            if (totalRounds > 0)
            {
                int idx = team == Team.Left ? ++_leftRoundIndex : ++_rightRoundIndex;
                if (idx > totalRounds) break;
            }

            QuestionData q = PullNextQuestion();
            if (q == null) break;

            bool done = false;
            bool correctResult = false;

            SetupIndependentDisplay(team, q, (correct, t, answer) =>
            {
                done = true;
                correctResult = correct;
                if (correct) ScoreManager.AddPoint(team);
                OnRoundResult(correct, team, answer);
            });

            yield return new WaitUntil(() => done || !_independentRunning);
            if (!_independentRunning) yield break;

            yield return new WaitForSeconds(correctResult ? feedbackDelayCorrect : feedbackDelayWrong);
        }

        CheckIndependentBothDone();
    }

    void CheckIndependentBothDone()
    {
        if (!_independentRunning || totalRounds <= 0) return;
        if (_leftRoundIndex > totalRounds && _rightRoundIndex > totalRounds)
            Fsm.StateMachineChange(MiniGameState.GameOver);
    }

    /// <summary>
    /// Bắt buộc override khi playMode = Independent: hiện câu hỏi riêng cho 1 team, gọi
    /// onDone(correct, team, answer) khi team đó xong — dùng thẳng
    /// display.SetupPlayerIndependent(team, q, onDone) của ButtonDisplay/FloatingDisplay (đã có sẵn).
    /// </summary>
    protected virtual void SetupIndependentDisplay(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        Debug.LogError("[MiniGameKit] playMode=Independent yêu cầu override SetupIndependentDisplay() ở subclass.");
    }

    protected void GoBackToMenu()
    {
        MusicManager.Instance?.PlayMainMusic();
        if (!string.IsNullOrEmpty(backSceneName)) SceneManager.LoadScene(backSceneName);
    }

    // ── Hooks — override ở subclass cho phần "ý tưởng mới" ─────────────────────

    /// <summary>Bắt buộc: trả về IAnswerDisplay sẽ hiện câu hỏi này (ButtonDisplay/FloatingDisplay/
    /// MatchingDisplay có sẵn, hoặc 1 IAnswerDisplay tự viết cho cơ chế hoàn toàn mới).</summary>
    protected abstract IAnswerDisplay GetDisplayForQuestion(QuestionData q);

    protected virtual void OnQuestionShown(QuestionData q) { }
    protected virtual void OnRoundResult(bool correct, Team team, int[] playerAnswer) { }
    protected virtual void OnPlayerFailed(Team team) { }
    protected virtual void OnQuestionTimeout() { }

    /// <summary>Mốc điểm tối đa dùng để normalize thanh fill của GameHUD khi chơi theo thời gian
    /// (totalRounds &lt;= 0, không có tổng số câu cố định để dùng làm mốc). Override nếu 1 điểm
    /// đúng của game bạn không phải +1 (vd hệ thống reward điểm thay đổi theo câu).</summary>
    protected virtual int HudMaxScoreFallback => 10;
}
