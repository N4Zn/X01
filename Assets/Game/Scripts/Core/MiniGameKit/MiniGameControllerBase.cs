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
    // Combined: bên đã được log round ở câu này (bên SAI cũng phải có round, nhưng không log đôi ở HandleResult).
    readonly HashSet<Team> _loggedThisRound = new HashSet<Team>();

    // ── Independent mode state (playMode == Independent) ─────────────────────
    bool _independentRunning;
    /// <summary>true khi vòng chơi Independent đang chạy (false sau GameOver/dừng) — subclass dùng để thoát coroutine chờ.</summary>
    protected bool IndependentRunning => _independentRunning;
    int _leftRoundIndex;
    int _rightRoundIndex;
    // Độ tiến của 2 đội theo "lượt" (1-based) — dùng cho điểm hẹn đồng bộ (SyncIndependentRounds) và vòng lặp âm câu hỏi.
    int[] _indArrived = new int[2], _indDone = new int[2];
    bool[] _indEnded = new bool[2];
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
        Debug.Log($"[MiniGameControllerBase][DEBUG] Start() — type={GetType().Name} instance={GetInstanceID()} scene={gameObject.scene.name} t={Time.unscaledTime:F1}");

        // Auto bật touch ngay khi game bắt đầu (mọi đường vào scene đều chạy qua đây — cold
        // boot/OnLoadGameRequested/nút "Chơi lại" tự SceneManager.LoadScene() bỏ qua
        // GameControlBridge — nên đây là chỗ DUY NHẤT chắc chắn chạy đúng 1 lần/sớm nhất cho
        // mọi trường hợp). Set thẳng true, không cần if — đã true thì gọi lại vẫn true, không
        // ảnh hưởng gì nếu giáo viên vừa bật tay bằng nút cứng trước đó.
        LidarTouchBridge.Instance?.SetTouchEnabled(true);

        // Mọi countdown Kit (Start/Next, Combined lẫn Independent) dùng chung 1 kiểu chữ — xem CountdownStyle.
        CountdownStyle.Apply(leftCountdownText);
        CountdownStyle.Apply(rightCountdownText);

        Current = this;
        InitScoring();
        InitFsm();
        Debug.Log($"[MiniGameControllerBase][DEBUG] Start() xong — type={GetType().Name}");
    }

    protected virtual void OnDestroy()
    {
        StopQuestionAudio();
        if (Current == this) Current = null;
    }

    // ═══ CƠ CHẾ CHUNG của mọi minigame (viết MỘT lần ở đây — game mới KHÔNG tự viết lại; xem Core/MiniGameKit/README.md) ═══
    // • Âm câu hỏi: phát lúc câu hiện → phát xong NGHỈ 4s → lặp lại, tới khi lượt kết thúc (Combined; hoặc Independent có SyncIndependentRounds).
    // • Thứ tự cuối lượt (Combined VÀ Independent giống nhau): kết quả → icon đúng/sai → nghỉ feedbackDelay → DỌN câu hỏi/đáp án + ẩn icon
    //   → chờ vùng sạch → đếm "Next in Ns" → (Independent đồng bộ: chờ đội kia tới) → câu mới.
    // • Đồng bộ 2 đội ở Independent (SyncIndependentRounds): điểm hẹn — cả 2 đội cùng tới mới hiện câu mới.

    /// <summary>Thời gian NGHỈ giữa 2 lần phát âm câu hỏi (giây), tính từ lúc phát xong.</summary>
    protected const float QuestionAudioRepeatPause = 4f;

    /// <summary>Phát âm đọc câu hỏi 1 LẦN, trả về tổng độ dài (giây); 0 = câu không có âm → không lặp. Mặc định không có âm.
    /// Base lo việc lặp + dừng; game chỉ cần phát và báo độ dài.</summary>
    protected virtual float PlayQuestionAudioOnce(QuestionData q) => 0f;

    /// <summary>Base vừa dừng vòng lặp âm câu hỏi giữa chừng (sang bước kết quả/thoát) — game cắt nốt đoạn âm đang phát nếu muốn.</summary>
    protected virtual void OnQuestionAudioStopped() { }

    /// <summary>true = đang có tiếng khác chen vào (vd tiếng vật lúc chạm) → vòng lặp âm câu hỏi CHƯA đếm 4s nghỉ / chưa phát lại; hết true mới đếm lại 4s.</summary>
    protected virtual bool QuestionAudioHold => false;

    Coroutine _qAudioLoop;
    bool _qAudioActive;

    void StartQuestionAudio(QuestionData q, Func<bool> stillOpen)
    {
        StopQuestionAudio();
        if (q == null) return;
        _qAudioActive = true;
        _qAudioLoop = StartCoroutine(QuestionAudioLoop(q, stillOpen));
    }

    protected void StopQuestionAudio()
    {
        _qAudioActive = false;
        if (_qAudioLoop == null) return;   // không có vòng lặp đang chạy → không đụng tới sfx khác (đúng/sai...)
        StopCoroutine(_qAudioLoop);
        _qAudioLoop = null;
        OnQuestionAudioStopped();
    }

    /// <summary>Vòng lặp âm câu hỏi tự kết thúc vì lượt đã xong (`stillOpen` hết đúng — vd cả 2 đội đều đã trả lời, đã hiện đúng/sai) sau khi đã phát ít nhất 1 lần
    /// → game CẮT NGAY âm câu hỏi/âm vật còn đang phát dở của lượt đó. Khác <see cref="OnQuestionAudioStopped"/> (Base chủ động dừng): hook này KHÔNG được cắt tiếng đúng/sai
    /// vừa phát, nên mặc định không làm gì.</summary>
    protected virtual void OnQuestionAudioClosed() { }

    IEnumerator QuestionAudioLoop(QuestionData q, Func<bool> stillOpen)
    {
        bool played = false;
        while (_qAudioActive && (stillOpen == null || stillOpen()))
        {
            float len = PlayQuestionAudioOnce(q);
            if (len <= 0f) break;   // câu không có âm
            played = true;
            // Chờ phát xong nhưng thoát ngay nếu lượt đã xong (không đợi hết đoạn âm dài mới biết).
            for (float t = 0f; t < len && _qAudioActive && (stillOpen == null || stillOpen()); t += Time.deltaTime) yield return null;
            for (float t = 0f; t < QuestionAudioRepeatPause && _qAudioActive && (stillOpen == null || stillOpen()); t += Time.deltaTime) { if (QuestionAudioHold) t = 0f; yield return null; }
        }
        bool closed = played && _qAudioActive && stillOpen != null && !stillOpen();
        _qAudioLoop = null;
        if (closed) OnQuestionAudioClosed();
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

    /// <summary>Đẩy report NGAY (bỏ throttle 1s) — Stop tay gọi trước khi Pause để màn Tổng kết có số cuối chính xác.</summary>
    public void ForcePushReport()
    {
        _lastReportPushTime = -999f;
        PushReportIfDue();
    }

    /// <summary>Đẩy report (thời gian còn lại, tổng điểm) sang ControlActivity mỗi giây —
    /// không đẩy mỗi frame để tránh gọi JNI quá dày. Xem GameControlBridge.PushReport().</summary>
    void PushReportIfDue()
    {
        if (ScoreManager == null) return; // chưa InitScoring / đang huỷ scene (thoát Play) — tránh NRE
        if (Time.time - _lastReportPushTime < 1f) return;
        _lastReportPushTime = Time.time;
        int secondsLeft = _useTimer ? Mathf.CeilToInt(Mathf.Max(0f, _gameTimer)) : 0;
        string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Trái";
        string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Phải";
        int rightScore = playMode == MiniGamePlayMode.Solo ? 0 : ScoreManager.ScoreRight;
        GameControlBridge.Instance?.PushReport(secondsLeft, leftName, ScoreManager.ScoreLeft, rightName, rightScore);

        // Breakdown từng người chơi thật (khác tổng điểm ở trên) — cùng nhịp throttle 1s, để
        // panel_live trên ControlActivity hết roster mock. Xem GameControlBridge.PushPlayerBreakdown().
        GameControlBridge.Instance?.PushPlayerBreakdown(
            PlayerRecognitionService.Instance?.GetPlayerStats(0),
            PlayerRecognitionService.Instance?.GetPlayerStats(1));
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
            // Có Resources/Background/<tên game> thì dùng làm nền; không có thì giữ nền của scene.
            var bgEntry = GameSessionManager.Instance != null ? GameSessionManager.Instance.SelectedEntry : default;
            hud.ApplyGameBackground(bgEntry.name, bgEntry.backgroundSprite);
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
        if (tutorialPanel != null && TutorialPanel.Enabled)
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
        _loggedThisRound.Clear();
        _teamsDone.Clear();
        _anyTeamCorrect = false;
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
        StartQuestionAudio(CurrentQuestion, null);   // Combined/Solo: lặp tới khi sang Feedback (wait-both: tới khi CẢ 2 đội xong)

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

    // ── Combined "chờ cả 2 đội" (WaitForBothTeams) ───────────────────────────────
    // Mỗi đội chơi hết câu của mình; round chỉ kết thúc khi CẢ 2 đội đã xong (đúng, hoặc sai mà không được thử lại).
    readonly HashSet<Team> _teamsDone = new HashSet<Team>();
    bool _anyTeamCorrect;

    /// <summary>true (chỉ có nghĩa ở Combined) = 2 đội cùng 1 câu hỏi nhưng KHÔNG ai "thắng trước": đội xong sớm
    /// chờ đội kia, cả 2 xong mới sang câu mới. Display tương ứng phải bật WaitBothTeams (không khoá bên kia).</summary>
    protected virtual bool WaitForBothTeams => false;

    /// <summary>Wait-both: chạm SAI có kết thúc lượt của đội đó không (false = đội được thử lại, chưa tính xong).</summary>
    protected virtual bool WrongEndsTeamTurn => true;

    protected bool WaitBothActive => WaitForBothTeams && playMode == MiniGamePlayMode.Combined;

    /// <summary>Wait-both: `team` vừa XONG lượt (đúng hết, hoặc sai không được thử lại) — đội kia có thể còn đang chơi. Icon ✔/✖ của
    /// đội này được GIỮ cho tới khi cả 2 đội xong (NextRoundAfterDelay mới ẩn); subclass dùng hook này để ẩn đáp án của đội đã xong.</summary>
    protected virtual void OnTeamTurnDone(Team team, bool correct) { }

    void MarkTeamDone(Team team, bool correct)
    {
        if (GetCurrentState() != MiniGameState.WaitAnswer) return;
        if (!_teamsDone.Add(team)) return;
        if (correct) _anyTeamCorrect = true;
        OnTeamTurnDone(team, correct);
        if (_teamsDone.Count < 2) return;
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        Fsm.StateMachineChange(MiniGameState.Feedback,
            new Dictionary<string, object> { { "correct", _anyTeamCorrect }, { "team", team } });
    }

    void HandleResult(bool correct, Team team, int[] playerAnswer)
    {
        if (WaitBothActive && correct)
        {
            AwardDefaultPoint(team);
            if (UseDefaultFeedbackFx) PlayDefaultFeedbackFx(team, true);
            if (_loggedThisRound.Add(team))
                LogRoundResult(team, CurrentQuestion, playerAnswer, true, Time.time - _questionShownTime);
            OnRoundResult(true, team, playerAnswer);
            MarkTeamDone(team, true);
            return;
        }
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        if (correct) AwardDefaultPoint(team);
        if (correct && UseDefaultFeedbackFx) PlayDefaultFeedbackFx(team, true);
        if (playMode == MiniGamePlayMode.Independent || _loggedThisRound.Add(team))
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
        // Bên này vừa trả lời SAI: ghi round ngay để có tên/số câu trong panel live + lịch sử (trước đây chỉ bên chốt kết quả được ghi).
        if (playMode != MiniGamePlayMode.Independent && CurrentQuestion != null && _loggedThisRound.Add(team))
            LogRoundResult(team, CurrentQuestion, null, false, Time.time - _questionShownTime);
        if (UseDefaultFeedbackFx) PlayDefaultFeedbackFx(team, false);
        OnPlayerFailed(team);
        if (WaitBothActive && WrongEndsTeamTurn) MarkTeamDone(team, false);
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
        ShowFeedbackIcon(team, correct);
    }

    /// <summary>Hiện icon ✔/✖ bounce (FeedbackEffect) cho 1 bên — tách riêng từ PlayDefaultFeedbackFx
    /// để subclass tự gọi được khi KHÔNG dùng UseDefaultFeedbackFx mặc định (vd GenericGameController
    /// tự quyết định có phát âm thanh/icon hay không theo cấu hình riêng từng game — xem
    /// GenericGameController.PlayActionFx) nhưng vẫn muốn tái dùng ĐÚNG hiệu ứng icon đã dùng chung
    /// cho mọi game khác, không viết lại.</summary>
    protected void ShowFeedbackIcon(Team team, bool correct)
    {
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

    /// <summary>Ẩn icon ✔/✖ của 1 bên — tách riêng từ HideDefaultFeedbackIcons (ẩn CẢ 2 bên) để
    /// subclass Independent-mode tự ẩn ĐÚNG 1 bên lúc round MỚI của bên đó bắt đầu, không đụng icon
    /// bên KIA có thể đang hiện dở (xem GenericGameController.SetupIndependentDisplay).</summary>
    protected void HideFeedbackIcon(Team team)
    {
        if (team == Team.Left)
        {
            if (leftCorrectIcon != null) leftCorrectIcon.SetActive(false);
            if (leftWrongIcon != null) leftWrongIcon.SetActive(false);
        }
        else
        {
            if (rightCorrectIcon != null) rightCorrectIcon.SetActive(false);
            if (rightWrongIcon != null) rightWrongIcon.SetActive(false);
        }
    }

    void HideDefaultFeedbackIcons()
    {
        HideFeedbackIcon(Team.Left);
        HideFeedbackIcon(Team.Right);
    }

    protected virtual void StateMachineEnter_Feedback(Enum prev, Dictionary<string, object> opts)
    {
        StopQuestionAudio();
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
        // Game tắt countdown (vd GenericGame countdownMode=none) VẪN phải chờ clear nếu setting bật
        if (UseDefaultTransitionCountdown || WaitForClearEnabled) yield return TransitionCountdown();
        Fsm.StateMachineChange(MiniGameState.ShowQuestion);
    }

    /// <summary>Bật/tắt khoảng chờ "Next in Ns" mặc định của Kit giữa các câu — game chiếu sàn,
    /// học sinh vừa trả lời xong thường còn đứng nguyên tại ô vừa chọn, chưa kịp di chuyển. Thời
    /// lượng lấy từ setting tổng GameSettings.RoundEndDelay (menu Cài đặt chung, 1-4s), KHÔNG
    /// hard-code riêng từng game. Override thành false nếu game tự có nhịp chuyển câu riêng.</summary>
    protected virtual bool UseDefaultTransitionCountdown => true;

    [Tooltip("Bật thì đếm ngược 'Next in Ns' CHỜ vùng trả lời sạch (không còn ai đứng/chạm) trước " +
             "khi bắt đầu đếm — tái dùng FloorZoneClearer đã chạy thật ở SolarQuizVi (engine CSV " +
             "cũ), giờ dùng chung được cho Kit mới. Mặc định TẮT — không đổi hành vi của game nào " +
             "trừ khi tự bật. Chỉ áp dụng cho Combined mode (TransitionCountdown), KHÔNG áp dụng cho " +
             "Independent mode (xem UseIndependentRoundCountdown/IndependentRoundCountdown riêng).")]
    [SerializeField] protected bool waitForZoneClearBeforeCountdown = false;

    bool WaitForClearEnabled => waitForZoneClearBeforeCountdown ||
                                (GameSettings.Instance != null && GameSettings.Instance.WaitForClear);

    IEnumerator TransitionCountdown()
    {
        // Game tắt countdown: chỉ chờ clear, không đếm "Next in Ns"
        int seconds = !UseDefaultTransitionCountdown ? 0 : Mathf.Max(0, Mathf.RoundToInt(
            GameSettings.Instance != null ? GameSettings.Instance.RoundEndDelay : 2f));
        // Cờ riêng từng scene HOẶC setting tổng "chờ clear" (Control panel, mặc định BẬT).
        // Chờ clear áp dụng KỂ CẢ khi RoundEndDelay = 0 (0 chỉ bỏ phần đếm "Next in Ns").
        bool waitClear = waitForZoneClearBeforeCountdown ||
                         (GameSettings.Instance != null && GameSettings.Instance.WaitForClear);
        bool canWait = waitClear && hud != null;
        if (seconds <= 0 && !canWait) yield break;

        // Combined/Solo rounds are synchronized (both sides transition together), so recognize
        // both slots at once here — headless (no camera preview/bounding box).
        PlayerRecognitionService.Instance.RecognizeSlot(0, _ => RefreshHudNames());
        PlayerRecognitionService.Instance.RecognizeSlot(1, _ => RefreshHudNames());

        if (canWait)
        {
            bool cleared = false;
            FloorZoneClearer.AwaitBothSides(hud.transform.root as RectTransform, () => cleared = true);
            yield return new WaitUntil(() => cleared);
        }

        for (int i = seconds; i >= 1; i--)
        {
            ShowTransitionCountdown(i);
            yield return new WaitForSeconds(1f);
        }
        HideTransitionCountdown();
    }

    /// <summary>Hiện chữ countdown cho 1 bên — ĐIỂM DUY NHẤT ghi chữ "Start/Next in Ns" (mọi đường đếm ngược gọi vào đây).</summary>
    void ShowCountdownText(Text text, string label, int secondsLeft)
    {
        if (text == null) return;
        text.text = CountdownStyle.Format(label, secondsLeft);
        text.gameObject.SetActive(true);
    }

    void ShowTransitionCountdown(int secondsLeft, string label = "Next")
    {
        ShowCountdownText(leftCountdownText, label, secondsLeft);
        ShowCountdownText(rightCountdownText, label, secondsLeft);
    }

    void HideTransitionCountdown()
    {
        if (leftCountdownText != null) leftCountdownText.gameObject.SetActive(false);
        if (rightCountdownText != null) rightCountdownText.gameObject.SetActive(false);
    }

    /// <summary>Gọi 1 lần ngay trước "Start in 3,2,1": lúc này chỉ được hiện nền — subclass ẩn câu hỏi/đáp án mặc định của scene.</summary>
    protected virtual void OnBeforeStartCountdown() { }

    protected virtual void CleanupCurrentDisplay()
    {
        GetDisplayForQuestion(CurrentQuestion)?.Cleanup();
    }

    protected virtual void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opts)
    {
        Debug.Log($"[MiniGameControllerBase][DEBUG] StateMachineEnter_GameOver — type={GetType().Name} instance={GetInstanceID()} t={Time.unscaledTime:F1}");
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        if (_nextRoundCoroutine != null) { StopCoroutine(_nextRoundCoroutine); _nextRoundCoroutine = null; }
        StopQuestionAudio();

        if (_independentRunning)
        {
            _independentRunning = false;
            if (_leftIndependentLoop != null) { StopCoroutine(_leftIndependentLoop); _leftIndependentLoop = null; }
            if (_rightIndependentLoop != null) { StopCoroutine(_rightIndependentLoop); _rightIndependentLoop = null; }
        }

        CleanupCurrentDisplay();

        // KHÔNG tắt touch ở đây — đã thử tắt lúc GameOver (yêu cầu ban đầu) nhưng gây lỗi thật:
        // nút "Chơi lại"/"Đổi đội" trên chính ScoreScene (máy chiếu) cũng được bấm qua LiDAR
        // floor-touch giống lúc chơi, tắt touch trước khi ScoreScene load = 2 nút đó liệt luôn,
        // dậm chân tại chỗ không phản hồi gì (đã xác nhận qua báo cáo thực tế trên K02). Touch
        // vẫn để nguyên xuyên suốt Game → ScoreScene → game tiếp theo; chỉ tắt lúc Pause/Stop
        // (hành động thủ công của giáo viên, không liên quan gì tới GameOver tự nhiên).

        if (GameSessionManager.Instance != null)
        {
            int leftScore = ScoreManager.ScoreLeft;
            int rightScore = playMode == MiniGamePlayMode.Solo ? ScoreManager.ScoreLeft : ScoreManager.ScoreRight;
            GameSessionManager.Instance.RecordScores(leftScore, rightScore);
            if (!string.IsNullOrEmpty(sceneNameForRegistry))
                GameSessionManager.Instance.LastPlayedGame = sceneNameForRegistry;
        }
        MusicManager.Instance?.PlayMainMusic();
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"[MiniGameControllerBase][DEBUG] sắp SceneManager.LoadScene('{nextSceneName}')...");
            SceneManager.LoadScene(nextSceneName);
            Debug.Log($"[MiniGameControllerBase][DEBUG] LoadScene('{nextSceneName}') ĐÃ RETURN.");
        }

        // PushReportIfDue() throttle 1 lần/giây — điểm cuối cùng (đủ thắng round cuối) có thể
        // đổi trong khoảng <1s trước GameOver và chưa kịp đẩy, khiến report ControlActivity/màn
        // chiếu bị lệch/cũ so với điểm thật (đã xác nhận qua báo cáo thực tế: trong game/tổng kết
        // đúng 3-2 nhưng report dưới màn chiếu vẫn còn 2-1 của round trước). Ép đẩy ngay 1 lần
        // không qua throttle bằng điểm vừa chốt trước khi báo game kết thúc.
        _lastReportPushTime = -999f;
        PushReportIfDue();

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
        // chung 1 scene). Cùng bug đã fix ở GameManager.cs (TestTongHop)/TestTongHopController.cs
        // và 13 controller khác không đi qua base class này (xem GameSessionManager.ResolveActiveGameName).
        string gameName = GameSessionManager.ResolveActiveGameName(
            !string.IsNullOrEmpty(sceneNameForRegistry) ? sceneNameForRegistry : GetType().Name);
        PlayerRecognitionService.Instance.BeginGameSession(gameName);
        // BeginGameSession vừa xoá tên ván trước → vẽ lại HUD ngay, không để tên cũ nằm trên màn tới khi nhận diện xong.
        RefreshHudNames();

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

        OnBeforeStartCountdown();
        for (int i = 3; i >= 1; i--)
        {
            ShowTransitionCountdown(i, "Start");
            yield return new WaitForSeconds(1f);
        }
        HideTransitionCountdown();

        if (playMode == MiniGamePlayMode.Independent)
        {
            _independentRunning = true;
            _indArrived = new int[2]; _indDone = new int[2]; _indEnded = new bool[2];
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

    /// <summary>Combined mà mỗi bên có câu hỏi RIÊNG (vd TongHopToan): trả câu của `team` để log đúng nội dung/đáp án
    /// (mặc định: câu chung CurrentQuestion).</summary>
    protected virtual QuestionData QuestionForTeam(Team team, QuestionData shared) => shared;

    void LogRoundResult(Team team, QuestionData q, int[] playerAnswer, bool correct, float answerTimeSec)
    {
        q = QuestionForTeam(team, q);
        int slot = team == Team.Left ? 0 : 1;
        int round = playMode == MiniGamePlayMode.Independent
            ? (team == Team.Left ? _leftRoundIndex : _rightRoundIndex)
            : _roundIndex;
        string questionDesc = DescribeQuestionForLog(q);
        string answerDesc = DescribeAnswerForLog(q, playerAnswer);
        string correctDesc = DescribeCorrectForLog(q);
        // Ghi vào file log phiên (GameLogs); SheetsSyncManager tự gửi nguyên file lên Drive — mọi game MiniGameKit
        // đi qua điểm này nên không cần đẩy từng dòng.
        PlayerRecognitionService.Instance.LogRound(slot, round, questionDesc, answerDesc, correct, answerTimeSec, correctDesc, q != null ? q.id : "");
    }

    // Mô tả để ghi log + hiện ở Lịch sử trên ControlActivity. Game có nội dung câu hỏi riêng (vd GenericGame:
    // câu hỏi là các slot, QuestionData.questionMediaValue rỗng) override 3 hàm này.
    protected virtual string DescribeQuestionForLog(QuestionData q)
        => q != null && q.questionType == QuestionType.Choose && q.questionMediaType != QuestionMediaType.Text
            ? "Câu hỏi media" : DescribeQuestion(q);
    protected virtual string DescribeAnswerForLog(QuestionData q, int[] playerAnswer) => DescribeAnswer(q, playerAnswer);
    /// <summary>Đáp án đúng, dạng chữ. Rỗng = không biết/không áp dụng.</summary>
    protected virtual string DescribeCorrectForLog(QuestionData q) => DescribeAnswer(q, q != null ? q.correctAnswers : null);

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
        int roundCount = 0; // KHÔNG dùng _leftRoundIndex/_rightRoundIndex — 2 field đó chỉ tăng khi
                             // totalRounds>0, còn round đếm ngược "Next in Ns" cần đếm cả khi chơi
                             // vô hạn (totalRounds<=0, trường hợp HaiQua/DemQua).
        while (_independentRunning)
        {
            // Luôn đếm round từng bên (kể cả totalRounds<=0) — LogRoundResult đọc 2 field này,
            // trước đây chỉ tăng khi totalRounds>0 nên chơi vô hạn bị ghi round=0.
            int idx = team == Team.Left ? ++_leftRoundIndex : ++_rightRoundIndex;
            if (totalRounds > 0 && idx > totalRounds) break;

            roundCount++;
            if (roundCount > 1)
            {
                bool waitClear = waitForZoneClearBeforeCountdown || (GameSettings.Instance != null && GameSettings.Instance.WaitForClear);
                if (waitClear && hud != null)
                {
                    bool cleared = false;
                    FloorZoneClearer.AwaitSide(team, hud.transform.root as RectTransform, () => cleared = true);
                    yield return new WaitUntil(() => cleared || !_independentRunning);
                    if (!_independentRunning) yield break;
                }

                if (UseIndependentRoundCountdown)
                {
                    yield return IndependentRoundCountdown(team);
                    if (!_independentRunning) yield break;
                }
            }

            if (SyncIndependentRounds)
            {
                yield return IndependentBarrier(team, roundCount);
                if (!_independentRunning) yield break;
            }

            QuestionData q = PullNextQuestion();
            if (q == null) break;

            bool done = false;
            bool correctResult = false;
            float questionStart = Time.time;
            int rc = roundCount, me = team == Team.Left ? 0 : 1;

            SetupIndependentDisplay(team, q, (correct, t, answer) =>
            {
                done = true;
                _indDone[me] = rc;
                correctResult = correct;
                if (correct && AwardTaskPoint(team)) ScoreManager.AddPoint(team);
                LogRoundResult(team, q, answer, correct, Time.time - questionStart);
                OnRoundResult(correct, team, answer);
            });

            // Đồng bộ: âm câu hỏi do ĐỘI TRÁI phát 1 lần cho cả 2 (2 đội cùng câu), lặp tới khi cả 2 xong lượt này.
            if (SyncIndependentRounds && team == Team.Left) StartQuestionAudio(q, () => !(_indDone[0] >= rc && _indDone[1] >= rc));

            yield return new WaitUntil(() => done || !_independentRunning);
            if (!_independentRunning) yield break;

            // Independent mode: each side has its own pace, so recognize only THIS team's slot —
            // PlayerRecognitionService's per-slot reference counting keeps this safe even if the
            // other team's own loop is mid-recognition at the same time.
            PlayerRecognitionService.Instance.RecognizeSlot(team == Team.Left ? 0 : 1, _ => RefreshHudNames());

            yield return new WaitForSeconds(correctResult ? feedbackDelayCorrect : feedbackDelayWrong);

            // Giống Combined (NextRoundAfterDelay): hết feedback → DỌN câu hỏi/đáp án của đội này + ẩn icon, rồi mới chờ vùng sạch / đếm "Next".
            CleanupIndependentDisplay(team);
            HideFeedbackIcon(team);
        }

        _indEnded[team == Team.Left ? 0 : 1] = true;
        CheckIndependentBothDone();
    }

    /// <summary>true = 2 đội ở Independent vẫn CÙNG nhịp: lượt k chỉ hiện câu khi CẢ 2 đội đã xong lượt k-1 VÀ qua dọn câu / chờ vùng sạch /
    /// đếm "Next" của mình (điểm hẹn). Game cho 2 đội cùng 1 câu (cùng seed) bật cờ này. Mặc định false (mỗi đội nhịp riêng).</summary>
    protected virtual bool SyncIndependentRounds => false;

    /// <summary>Independent: lượt của `team` vừa xong ĐÚNG → có cộng +1 điểm lượt không. Game tự cộng điểm từng bước (vd mỗi ô đúng) override trả false để khỏi cộng đôi. Mặc định true.</summary>
    protected virtual bool AwardTaskPoint(Team team) => true;

    /// <summary>Đội `team` tới điểm hẹn trước và đang chờ đội kia (on=true), hoặc hết chờ (on=false) — game hiện/ẩn chữ "Chờ bạn nhé...".</summary>
    protected virtual void OnWaitingForPartner(Team team, bool on) { }

    /// <summary>Independent: hết feedback của đội `team` → dọn câu hỏi + đáp án của đội đó khỏi màn hình (bản Independent của
    /// CleanupCurrentDisplay). Mặc định không làm gì — game nào hiện câu hỏi riêng từng đội thì override.</summary>
    protected virtual void CleanupIndependentDisplay(Team team) { }

    IEnumerator IndependentBarrier(Team team, int round)
    {
        int me = team == Team.Left ? 0 : 1, other = 1 - me;
        _indArrived[me] = round;
        if (_indArrived[other] >= round || _indEnded[other]) yield break;
        OnWaitingForPartner(team, true);
        while (_independentRunning && _indArrived[other] < round && !_indEnded[other]) yield return null;
        OnWaitingForPartner(team, false);
    }

    /// <summary>"Next in 3,2,1" riêng cho TỪNG BÊN — xem UseIndependentRoundCountdown. Dùng lại
    /// đúng field leftCountdownText/rightCountdownText của bên đó, KHÔNG đụng bên còn lại (2 bên
    /// Independent mode không đồng bộ round nên không thể dùng 1 coroutine chung như
    /// TransitionCountdown() của Combined mode).</summary>
    IEnumerator IndependentRoundCountdown(Team team)
    {
        var text = team == Team.Left ? leftCountdownText : rightCountdownText;
        if (text == null) yield break; // không có chỗ hiện chữ → KHÔNG tạo khoảng dừng vô hình
        int seconds = Mathf.Max(0, Mathf.RoundToInt(
            GameSettings.Instance != null ? GameSettings.Instance.RoundEndDelay : 2f)); // cùng setting với Combined
        for (int i = seconds; i >= 1; i--)
        {
            ShowCountdownText(text, "Next", i);
            yield return new WaitForSeconds(1f);
            if (!_independentRunning) break;
        }
        text.gameObject.SetActive(false);
    }

    /// <summary>Có đếm "Next in Ns" riêng từng bên giữa các round ở Independent mode không (từ round 2).
    /// Mặc định BẬT cho mọi game Kit — không còn phụ thuộc cờ serialize trong scene (scene dựng từ
    /// trước bị thiếu cờ → mất countdown, như DemQua). Game muốn tắt thì override (vd GenericGame theo
    /// settings.countdownMode). Không có Text gán thì tự bỏ qua, không tạo khoảng chờ vô hình.</summary>
    protected virtual bool UseIndependentRoundCountdown => true;

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
