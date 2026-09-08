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
    float _questionShownTime;

    // ── Independent mode state (playMode == Independent) ─────────────────────
    bool _independentRunning;
    int _leftRoundIndex;
    int _rightRoundIndex;
    Coroutine _leftIndependentLoop;
    Coroutine _rightIndependentLoop;

    /// <summary>Instance minigame đang chạy trong scene hiện tại — dùng cho control panel
    /// (ControlActivity/GameControlBridge) gọi Pause()/Resume() mà không cần tham chiếu
    /// trực tiếp tới subclass cụ thể nào. Chỉ 1 minigame chạy tại 1 thời điểm nên static là
    /// đủ, không cần danh sách.</summary>
    public static MiniGameControllerBase Current { get; private set; }

    /// <summary>True khi đang Pause (từ control panel) — Update() đóng băng timer hoàn
    /// toàn, không tự resume. Không tự chặn input riêng ở đây: khi Pause, control panel
    /// (GameControlBridge) đồng thời tắt LidarTouchBridge nên touch không tới được UI —
    /// đủ cho use-case thật (nguồn touch duy nhất là Lidar).</summary>
    public bool IsPaused { get; private set; }

    protected virtual void Start()
    {
        Current = this;
        InitScoring();
        InitFsm();
    }

    protected virtual void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Debug.Log("[MiniGameKit] Pause");
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Debug.Log("[MiniGameKit] Resume");
    }

    protected virtual void Update()
    {
        if (IsPaused) return;

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

        PushReportIfDue();
    }

    float _lastReportPushTime;

    /// <summary>Đẩy report (thời gian còn lại, tổng điểm) sang ControlActivity mỗi giây —
    /// không đẩy mỗi frame để tránh gọi JNI quá dày. Xem GameControlBridge.PushReport().</summary>
    void PushReportIfDue()
    {
        if (Time.time - _lastReportPushTime < 1f) return;
        _lastReportPushTime = Time.time;
        int secondsLeft = _useTimer ? Mathf.CeilToInt(Mathf.Max(0f, _gameTimer)) : 0;
        string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Trái";
        string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Phải";
        int rightScore = playMode == MiniGamePlayMode.Solo ? 0 : ScoreManager.ScoreRight;
        GameControlBridge.Instance?.PushReport(secondsLeft, leftName, ScoreManager.ScoreLeft, rightName, rightScore);
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

        _questionShownTime = Time.time;
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
        LogRoundResult(team, CurrentQuestion, playerAnswer, correct, Time.time - _questionShownTime);
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

        // Combined/Solo rounds are synchronized (both sides transition together), so recognize
        // both slots at once here — headless (no camera preview/bounding box).
        PlayerRecognitionService.Instance.RecognizeSlot(0, _ => RefreshHudNames());
        PlayerRecognitionService.Instance.RecognizeSlot(1, _ => RefreshHudNames());

        for (int i = seconds; i >= 1; i--)
        {
            ShowTransitionCountdown(i);
            yield return new WaitForSeconds(1f);
        }
        HideTransitionCountdown();
    }

    void ShowTransitionCountdown(int secondsLeft, string label = "Next")
    {
        string msg = $"{label} in {secondsLeft}s";
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

        // Hết giờ tự nhiên (không phải bấm Stop) — báo ControlActivity tự quay Menu chọn
        // game tiếp theo, coi như hết 1 round. Display máy chiếu không bị đụng, vẫn hiện
        // ScoreScene vừa load ở trên như bình thường.
        GameControlBridge.Instance?.PushGameEnded();
    }

    // ── Actions dùng chung — gọi từ UI event (nút Retry/Back) hoặc từ hook ────

    protected void StartGame()
    {
        _roundIndex = 0;
        _leftRoundIndex = 0;
        _rightRoundIndex = 0;
        ScoreManager.Reset();
        MusicManager.Instance?.PlayGameplayMusic();

        // Tên game cụ thể (GameSessionManager.SelectedGameName, vd variant key trong
        // GameRegistry) — KHÔNG phải sceneNameForRegistry (tên scene, nhiều game có thể share
        // chung 1 scene). Cùng bug đã fix ở GameManager.cs (TestTongHop)/TestTongHopController.cs.
        string gameName = GameSessionManager.Instance != null && !string.IsNullOrEmpty(GameSessionManager.Instance.SelectedGameName)
            ? GameSessionManager.Instance.SelectedGameName
            : (!string.IsNullOrEmpty(sceneNameForRegistry) ? sceneNameForRegistry : GetType().Name);
        PlayerRecognitionService.Instance.BeginGameSession(gameName);

        StartCoroutine(InitialStartCountdownThenBegin());
    }

    /// <summary>
    /// "Start in 3,2,1" shown before round 1 (or before Independent mode's per-player loops
    /// begin), with recognition running for both slots throughout — otherwise gameplay would
    /// start under whatever stale name GameSessionManager had from team select, and the first
    /// real recognition attempt wouldn't happen until the first round-transition countdown.
    /// </summary>
    IEnumerator InitialStartCountdownThenBegin()
    {
        PlayerRecognitionService.Instance.RecognizeSlot(0, _ => RefreshHudNames());
        PlayerRecognitionService.Instance.RecognizeSlot(1, _ => RefreshHudNames());

        for (int i = 3; i >= 1; i--)
        {
            ShowTransitionCountdown(i, "Start");
            yield return new WaitForSeconds(1f);
        }
        HideTransitionCountdown();

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

    /// <summary>Refreshes the HUD name labels from GameSessionManager — call after a
    /// PlayerRecognitionService.RecognizeSlot callback so a newly-recognized name shows up.</summary>
    void RefreshHudNames()
    {
        if (hud == null || GameSessionManager.Instance == null) return;
        hud.UpdatePlayerNames(GameSessionManager.Instance.GetDisplayName1(), GameSessionManager.Instance.GetDisplayName2());
    }

    // ── Gamelog — full per-round record (question/answer/correct/time), merged with whoever was
    // last recognized for that slot by PlayerRecognitionService. One shared insertion point here
    // covers every MiniGameKit-derived game without needing per-subclass wiring.

    void LogRoundResult(Team team, QuestionData q, int[] playerAnswer, bool correct, float answerTimeSec)
    {
        int slot = team == Team.Left ? 0 : 1;
        int round = playMode == MiniGamePlayMode.Independent
            ? (team == Team.Left ? _leftRoundIndex : _rightRoundIndex)
            : _roundIndex;
        string questionDesc = DescribeQuestion(q);
        string answerDesc = DescribeAnswer(q, playerAnswer);
        PlayerRecognitionService.Instance.LogRound(slot, round, questionDesc, answerDesc, correct, answerTimeSec);

        // Track E: đồng bộ Google Sheet liên tục — điểm chèn DUY NHẤT này phủ được mọi game
        // dựng trên MiniGameKit (không cần sửa từng subclass). Xem SheetsSyncManager.
        // eventType + playerName: thiếu ở bản trước — mọi log khác (GameLogger, PlayerRecognitionService)
        // đều có 2 field này, thiếu khiến cột eventType trống, không lọc/nhận biết được ai trả lời.
        string playerName = GameSessionManager.Instance != null && GameSessionManager.Instance.Players.Count > slot
            ? GameSessionManager.Instance.Players[slot].PlayerName
            : (team == Team.Left ? "Player 1" : "Player 2");
        SheetsSyncManager.Enqueue(new Dictionary<string, object>
        {
            {"timestamp", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")},
            {"eventType", "round_end"},
            {"gameName", string.IsNullOrEmpty(sceneNameForRegistry) ? GetType().Name : sceneNameForRegistry},
            {"gameVariant", GameSessionManager.Instance != null ? GameSessionManager.Instance.SelectedGameName : ""},
            {"slot", slot},
            {"team", team.ToString()},
            {"playerName", playerName},
            {"round", round},
            {"question", questionDesc},
            {"answer", answerDesc},
            {"correct", correct},
            {"responseTimeSec", System.Math.Round(answerTimeSec, 2)},
        });
    }

    static string DescribeQuestion(QuestionData q)
    {
        if (q == null) return "";
        if (q.questionType == QuestionType.Matching)
            return $"[Matching] {string.Join("|", q.leftItems ?? new string[0])} -> {string.Join("|", q.rightItems ?? new string[0])}";
        return q.questionMediaValue ?? "";
    }

    static string DescribeAnswer(QuestionData q, int[] playerAnswer)
    {
        if (playerAnswer == null || playerAnswer.Length == 0) return "";
        if (q != null && q.questionType == QuestionType.Choose && q.answers != null)
        {
            var parts = new List<string>(playerAnswer.Length);
            foreach (int idx in playerAnswer)
                parts.Add(idx >= 0 && idx < q.answers.Length ? q.answers[idx] : idx.ToString());
            return string.Join(",", parts);
        }
        return string.Join(",", playerAnswer);
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
            float questionStart = Time.time;

            SetupIndependentDisplay(team, q, (correct, t, answer) =>
            {
                done = true;
                correctResult = correct;
                if (correct) ScoreManager.AddPoint(team);
                LogRoundResult(team, q, answer, correct, Time.time - questionStart);
                OnRoundResult(correct, team, answer);
            });

            yield return new WaitUntil(() => done || !_independentRunning);
            if (!_independentRunning) yield break;

            // Independent mode: each side has its own pace, so recognize only THIS team's slot —
            // PlayerRecognitionService's per-slot reference counting keeps this safe even if the
            // other team's own loop is mid-recognition at the same time.
            PlayerRecognitionService.Instance.RecognizeSlot(team == Team.Left ? 0 : 1, _ => RefreshHudNames());

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
