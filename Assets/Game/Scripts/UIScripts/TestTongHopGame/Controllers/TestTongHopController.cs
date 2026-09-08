using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// FSM Controller cho TestTongHop game.
/// Điều khiển toàn bộ flow: ShowQuestion → WaitAnswer → Feedback → loop / GameOver.
/// GameManager (model) và các Display component chỉ là tầng service — Controller gọi chúng.
/// </summary>
public class TestTongHopController : MonoBehaviour
{
    [Header("Model")]
    [SerializeField] GameManager gameModel;

    [Header("Display")]
    [SerializeField] AnswerDisplayManager   answerDisplayManager;
    [SerializeField] QuestionMediaDisplay[] questionDisplays;  // index 0 = P1, index 1 = P2
    [SerializeField] GameHUD               gameHud;

    [Header("Feedback Icons (same pattern as TongHopGameView)")]
    [SerializeField] GameObject p1CorrectIcon;
    [SerializeField] GameObject p1WrongIcon;
    [SerializeField] GameObject p2CorrectIcon;
    [SerializeField] GameObject p2WrongIcon;

    [Header("Countdown Text (same pattern as TongHopGameView)")]
    [SerializeField] TextMeshProUGUI leftCountdownText;   // nửa trái — "Next in 3s"
    [SerializeField] TextMeshProUGUI rightCountdownText;  // nửa phải

    // feedbackDelayCorrect / feedbackDelayWrong → TongHopConfig.Current (gameconfig.json)

    // ── Internal state ────────────────────────────────────────────────────────

    CustomFSMManager _fsm;
    QuestionData     _currentQuestion;
    float            _questionStartTime;
    bool             _lastCorrect;
    Team             _lastTeam;

    // Adaptive difficulty
    int _correctStreak;
    int _wrongStreak;

    // Track players đã bị fail trong round này (tránh show ✗ 2 lần)
    readonly System.Collections.Generic.HashSet<Team> _failedThisRound = new();

    // Track xem câu hỏi của từng player đã bị ẩn chưa (index 0=Left, 1=Right)
    bool[] _questionHidden = new bool[2];

    // Thời điểm (Time.time) mà FeedbackThenNext được phép ẩn icon và bắt đầu countdown
    // Correct: Time.time + feedbackDelayCorrect (chờ icon B hiện đủ thời gian)
    // Wrong  : Time.time + 0 (icon đã hiện đủ thời gian cùng với delay ẩn câu hỏi)
    float _iconDisplayUntil;

    // ── First-question & recognition state ───────────────────────────────────
    bool   _isFirstQuestion = true;
    string _pendingRecognizedLeft;
    string _pendingRecognizedRight;

    // ── Independent play state ────────────────────────────────────────────────
    bool _isIndependentPlay;
    bool _independentStarted;
    int  _leftRoundsCompleted;
    int  _rightRoundsCompleted;

    // ── Control panel (ControlActivity/GameControlBridge) — Pause/Resume + report ─────────
    // TestTongHopController KHÔNG kế thừa MiniGameControllerBase (FSM/state hoàn toàn riêng)
    // nên phải tự có Current/IsPaused/Pause()/Resume() y hệt pattern bên đó — nếu không,
    // GameControlBridge.OnPauseRequested/PushReport sẽ luôn no-op cho game chính này (đã xác
    // nhận qua test thực tế: bấm Pause không dừng timer, report không cập nhật).
    public static TestTongHopController Current { get; private set; }
    public bool IsPaused { get; private set; }
    float _lastReportPushTime;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Update()
    {
        if (IsPaused) { PushReportIfDue(); return; }

        var state = GetCurrentState();
        if (state != TestTongHopSceneState.WaitAnswer &&
            state != TestTongHopSceneState.Feedback)
        {
            PushReportIfDue();
            return;
        }

        if (gameModel.IsTimeUp) { PushReportIfDue(); return; }

        gameModel.GameTimer -= Time.deltaTime;
        gameHud?.UpdateTimer(gameModel.GameTimer);

        if (gameModel.IsTimeUp)
        {
            gameModel.GameTimer = 0;
            foreach (var display in questionDisplays) display.Hide();
            answerDisplayManager.Cleanup();
            _fsm.StateMachineChange(TestTongHopSceneState.GameOver);
        }

        PushReportIfDue();
    }

    void Start()
    {
        Current       = this;
        _fsm          = gameObject.AddComponent<CustomFSMManager>();
        _fsm.fsmName  = nameof(TestTongHopController) + "FSM";
        _fsm.Initialize(typeof(TestTongHopSceneState), GetType(), false);
        _fsm.StateMachineChange(TestTongHopSceneState.Initialize);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>Gọi từ GameControlBridge.OnPauseRequested — chỉ đóng băng timer (giống
    /// MiniGameControllerBase.Pause()); coroutine feedback/countdown vẫn chạy theo thời gian
    /// thật, touch bị chặn riêng ở LidarTouchBridge nên không ai bấm được trong lúc pause.</summary>
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Debug.Log("[TestTongHopController] Pause");
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Debug.Log("[TestTongHopController] Resume");
    }

    /// <summary>Đẩy report (thời gian còn lại, tổng điểm) sang ControlActivity mỗi giây —
    /// cùng nhịp/API với MiniGameControllerBase.PushReportIfDue().</summary>
    void PushReportIfDue()
    {
        if (Time.time - _lastReportPushTime < 1f) return;
        _lastReportPushTime = Time.time;
        int secondsLeft = Mathf.CeilToInt(Mathf.Max(0f, gameModel.GameTimer));
        var (left, right) = gameModel.GetScore();
        string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Trái";
        string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Phải";
        GameControlBridge.Instance?.PushReport(secondsLeft, leftName, left, rightName, right);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    TestTongHopSceneState GetCurrentState()
    {
        if (_fsm == null) return TestTongHopSceneState.Initialize;
        Enum s = _fsm.GetCurrentState();
        return s == null ? TestTongHopSceneState.Initialize : (TestTongHopSceneState)s;
    }

    // =========================================================================
    // FSM — Initialize
    // =========================================================================

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opts)
    {
        // Đọc visual/audio identity từ GameRegistry qua GameSessionManager
        var entry = GameSessionManager.Instance?.SelectedEntry ?? default;
        MusicManager.Instance?.PlayBgm(entry.bgmTrack);
        gameModel.PointsPerCorrect = entry.pointsPerCorrect > 0 ? entry.pointsPerCorrect : 1;

        // Đổi background nếu game có khai báo backgroundSprite trong GameRegistry.
        // Ưu tiên Texture2D (jpg/png dạng Texture) → fallback Sprite (png dạng Sprite 2D and UI).
        if (!string.IsNullOrEmpty(entry.backgroundSprite))
        {
            var tex = Resources.Load<Texture2D>(entry.backgroundSprite);
            if (tex != null)
                gameHud?.SetBackground(tex);
            else
                gameHud?.SetBackground(Resources.Load<Sprite>(entry.backgroundSprite));
        }

        if (entry.hideScoreBars)
            gameHud?.HideScoreBars();

        // Lấy tên hiển thị từ GameSessionManager — dùng cùng API với ScoreScene
        // để HUD và màn kết quả luôn hiện nhất quán (OneVsOne: tên cá nhân, Team: tên đội).
        string leftName  = "Player 1";
        string rightName = "Player 2";
        var session = GameSessionManager.Instance;
        if (session != null)
        {
            leftName  = session.GetDisplayName1();
            rightName = session.GetDisplayName2();
        }

        // Override timing từ MenuScene settings (nhất quán với tất cả game khác)
        if (GameSettings.Instance != null)
        {
            float roundDelay = GameSettings.Instance.RoundEndDelay;
            TongHopConfig.Current.feedbackDelayCorrect = 1f;
            TongHopConfig.Current.feedbackDelayWrong = 1f;
            int countdownSecs = Mathf.Max(0, Mathf.RoundToInt(roundDelay) - 1);
            TongHopConfig.Current.nextQuestionDelay = countdownSecs;
            TongHopConfig.Current.nextQuestionDelayPerPlayer = countdownSecs;
        }

        _isIndependentPlay = TongHopConfig.Current.independentPlay;

        // Phòng thủ: TongHopConfig.Current là static — difficulty có thể mang giá trị
        // từ lần chơi trước (trong cùng app session). Luôn reset về difficultyMin khi
        // bắt đầu game mới để pool được build đúng ngay từ câu hỏi đầu tiên.
        gameModel.SetDifficulty(TongHopConfig.Current.difficultyMin);

        // Feedback icons phải render trên SolarSystemDisplay (lazy-created → last sibling).
        // Wrapper Canvas riêng tránh conflict CanvasGroup + Canvas trên cùng 1 GO.
        SetupFeedbackIconOverlay();

        gameHud?.Initialize(gameModel.Score, leftName, rightName);
        PlayerRecognitionService.Instance.BeginGameSession("TestTongHopGame");
        _fsm.StateMachineChange(TestTongHopSceneState.ShowQuestion);
    }

    protected void StateMachineExit_Initialize(Enum prev, Dictionary<string, object> opts) { }

    void SetupFeedbackIconOverlay()
    {
        // Tìm root Canvas từ bất kỳ icon nào
        GameObject[] icons = { p1CorrectIcon, p1WrongIcon, p2CorrectIcon, p2WrongIcon };
        Canvas root = null;
        foreach (var icon in icons)
        {
            if (icon == null) continue;
            var c = icon.GetComponentInParent<Canvas>();
            if (c != null) { root = c.rootCanvas; break; }
        }
        if (root == null) return;

        // Tạo wrapper overlay — children kế thừa sortingOrder mà không conflict CanvasGroup
        var overlayGo = new GameObject("FeedbackOverlay");
        var rt = overlayGo.AddComponent<RectTransform>();
        rt.SetParent(root.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var cv = overlayGo.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 15;

        // Reparent icons vào overlay (giữ nguyên vị trí màn hình)
        foreach (var icon in icons)
        {
            if (icon != null)
                icon.transform.SetParent(rt, worldPositionStays: true);
        }
    }

    // =========================================================================
    // FSM — ShowQuestion
    // =========================================================================

    protected void StateMachineEnter_ShowQuestion(Enum prev, Dictionary<string, object> opts)
    {
        // ── Independent play: chỉ khởi động 1 lần, rồi park FSM ở WaitAnswer ──
        if (_isIndependentPlay)
        {
            if (!_independentStarted)
            {
                _independentStarted = true;
                StartIndependentPlay();
                _fsm.StateMachineChange(TestTongHopSceneState.WaitAnswer);
            }
            return;
        }

        // Câu đầu tiên: chạy "Start in 3 2 1" trước khi hiện câu hỏi
        if (_isFirstQuestion)
        {
            _isFirstQuestion = false;
            StartCoroutine(FirstQuestionCountdown());
            return;
        }

        DoShowQuestion();
    }

    protected void StateMachineExit_ShowQuestion(Enum prev, Dictionary<string, object> opts) { }

    /// <summary>Đếm ngược "Start in 3 2 1" trước câu hỏi đầu tiên, kết hợp nhận diện người chơi.</summary>
    IEnumerator FirstQuestionCountdown()
    {
        int countFrom = TongHopConfig.Current.nextQuestionDelay;
        CaptureRecognizedPlayers();
        for (int i = countFrom; i >= 1; i--)
        {
            ShowCountdown(i, isStart: true);
            yield return new WaitForSeconds(1f);
            if (GetCurrentState() != TestTongHopSceneState.ShowQuestion) yield break;
        }
        HideCountdown();
        DoShowQuestion();
    }

    /// <summary>Phần core của ShowQuestion — gọi sau countdown (câu 1) hoặc trực tiếp (câu 2+).</summary>
    void DoShowQuestion()
    {
        gameModel.IncrementRound();
        _failedThisRound.Clear();
        _questionHidden[0] = _questionHidden[1] = false;

        // Score-based difficulty (SoDem-style): tổng điểm 2 player < 4 → min, >= 4 → +1, >= 8 → max
        if (TongHopConfig.Current.roundBasedDifficulty)
        {
            var (left, right) = gameModel.GetScore();
            int total = left + right;
            var cfg   = TongHopConfig.Current;
            int newDiff = total >= 8 ? cfg.difficultyMax
                        : total >= 4 ? cfg.difficultyMin + 1
                        : cfg.difficultyMin;
            newDiff = Mathf.Clamp(newDiff, cfg.difficultyMin, cfg.difficultyMax);
            if (newDiff != cfg.difficulty)
                gameModel.SetDifficulty(newDiff);
        }

        // AnswerDisplayManager chọn loại câu hỏi (dựa trên weight) và lấy từ pool
        answerDisplayManager.Show(OnAnswerResult, OnPlayerFailed, OnPartialCorrect);

        _currentQuestion   = answerDisplayManager.CurrentQuestion;
        _questionStartTime = Time.time;

        if (_currentQuestion == null)
        {
            Debug.LogError("[TestTongHopController] Pool rỗng — không có câu hỏi nào.");
            _fsm.StateMachineChange(TestTongHopSceneState.GameOver);
            return;
        }

        // Báo logger câu hỏi vừa được chọn → bắt đầu ghi click cho round này
        gameModel.LogRoundStart(_currentQuestion);
        // Ghi người chơi nhận diện được trong lần đếm ngược vừa rồi
        gameModel.LogRoundRecognizedPlayers(_pendingRecognizedLeft, _pendingRecognizedRight);
        _pendingRecognizedLeft = _pendingRecognizedRight = null;

        // SolarSystem display tự hiện thị câu hỏi bên trong — ẩn question text panel
        // Audio question: audio phát tự động, panel text không cần thiết
        bool hiddenByDisplay = _currentQuestion.displayMode == ChooseDisplayMode.SolarSystem
                            || _currentQuestion.displayMode == ChooseDisplayMode.SolarSystemEn
                            || _currentQuestion.questionMediaType == QuestionMediaType.Audio;
        if (!hiddenByDisplay)
            foreach (var display in questionDisplays)
                display.Show(_currentQuestion);
        else if (_currentQuestion.questionMediaType == QuestionMediaType.Audio)
            foreach (var display in questionDisplays)
                display.PlayAudioOnly(_currentQuestion);

        MusicManager.Instance?.PlayQuestionSfx();

        _fsm.StateMachineChange(TestTongHopSceneState.WaitAnswer);
    }

    // =========================================================================
    // FSM — WaitAnswer  (passive — chờ callback OnAnswerResult)
    // =========================================================================

    protected void StateMachineEnter_WaitAnswer(Enum prev, Dictionary<string, object> opts) { }
    protected void StateMachineExit_WaitAnswer(Enum prev, Dictionary<string, object> opts) { }

    // MultiSelect: mỗi lần click đúng 1 đáp án → +1 điểm ngay + SFX (không hiện icon)
    void OnPartialCorrect(Team team)
    {
        gameModel.Score.AddPoint(team);
        MusicManager.Instance?.PlayCorrectSfx();
    }

    // Callback: ngay khi 1 player hết lượt và sai — hiện ✗, GIỮ nguyên câu hỏi/đáp án để player kia xem
    void OnPlayerFailed(Team team)
    {
        if (_failedThisRound.Contains(team)) return;
        _failedThisRound.Add(team);
        MusicManager.Instance?.PlayWrongSfx();
        ShowWrongIcon(team);
        // Không ẩn câu hỏi/đáp án — giữ để player kia vẫn thấy và player fail thấy đáp án đúng sau
    }

    // Callback: cả 2 player đã hoàn thành lượt chơi (đúng hoặc cả 2 sai)
    void OnAnswerResult(bool isCorrect, Team team, int[] playerAnswer)
    {
        if (GetCurrentState() != TestTongHopSceneState.WaitAnswer) return;

        float responseTime = Time.time - _questionStartTime;
        // MultiSelect: điểm đã cộng từng phần qua OnPartialCorrect → chỉ log, không cộng lại
        bool addScore = !(isCorrect && _currentQuestion?.answerMode == AnswerMode.MultiSelect);
        gameModel.RecordAnswer(_currentQuestion, isCorrect, team, responseTime, addScore);

        _lastCorrect = isCorrect;
        _lastTeam    = team;

        // Correct: icon B cần hiện đủ feedbackDelayCorrect trước khi ẩn
        // Wrong  : icon ✗ đã đủ thời gian cùng với delay ẩn câu hỏi → không cần thêm
        _iconDisplayUntil = isCorrect
            ? Time.time + TongHopConfig.Current.feedbackDelayCorrect
            : Time.time;

        if (isCorrect)
        {
            MusicManager.Instance?.PlayCorrectSfx();

            // ✓ cho winner — có animation
            ShowCorrectIcon(team);

            // ✗ cho loser (nếu OnPlayerFailed chưa chạy)
            Team loser = team == Team.Left ? Team.Right : Team.Left;
            if (!_failedThisRound.Contains(loser))
            {
                _failedThisRound.Add(loser);
                ShowWrongIcon(loser);
            }
            // Không ẩn câu hỏi/đáp án — chờ zone clear mới ẩn (FeedbackThenNext xử lý)
        }
        else
        {
            // Cả 2 sai — đảm bảo ✗ cho bất kỳ bên chưa được xử lý
            foreach (Team t in new[] { Team.Left, Team.Right })
            {
                if (!_failedThisRound.Contains(t))
                {
                    _failedThisRound.Add(t);
                    ShowWrongIcon(t);
                }
            }
        }

        // Dừng audio câu hỏi ngay khi cả 2 trả lời xong
        foreach (var d in questionDisplays) d?.StopAudio();

        TryAdaptDifficulty(isCorrect);
        _fsm.StateMachineChange(TestTongHopSceneState.Feedback);
    }

    // =========================================================================
    // FSM — Feedback
    // =========================================================================

    protected void StateMachineEnter_Feedback(Enum prev, Dictionary<string, object> opts)
    {
        StartCoroutine(FeedbackThenNext());
    }

    protected void StateMachineExit_Feedback(Enum prev, Dictionary<string, object> opts) { }

    IEnumerator FeedbackThenNext()
    {
        // 1. Hiện feedback icon ~1s; đáp án đúng màu xanh + "Go to START!" hiện song song
        yield return new WaitForSeconds(TongHopConfig.Current.feedbackDelayCorrect);
        if (GetCurrentState() != TestTongHopSceneState.Feedback) yield break;

        // 2. Ẩn icon (câu hỏi/đáp án GIỮ NGUYÊN để player thấy đáp án đúng khi về START)
        HideFeedbackIcons();

        // 3. Chờ mọi người bước ra khỏi vùng chơi (FloorZoneClearer AwaitBothSides)
        yield return new WaitUntil(() =>
            answerDisplayManager.ZoneCleared ||
            GetCurrentState() != TestTongHopSceneState.Feedback);
        if (GetCurrentState() != TestTongHopSceneState.Feedback) yield break;

        // 4. Ẩn câu hỏi + dọn dẹp display sau khi mọi người đã ra vị trí START
        foreach (var d in questionDisplays) d?.Hide();
        answerDisplayManager.Cleanup();

        // 5. Đếm ngược "Next in Xs" + nhận diện người chơi cho câu tiếp theo
        int countFrom = TongHopConfig.Current.nextQuestionDelay;
        CaptureRecognizedPlayers();
        for (int i = countFrom; i >= 1; i--)
        {
            ShowCountdown(i);
            yield return new WaitForSeconds(1f);
            if (GetCurrentState() != TestTongHopSceneState.Feedback) yield break;
        }
        HideCountdown();

        // 6. Chuyển câu tiếp
        _fsm.StateMachineChange(TestTongHopSceneState.ShowQuestion);
    }

    // Coroutine: sau delay giây, ẩn câu hỏi + đáp án của player bị fail — chỉ còn icon ✗
    IEnumerator HideQuestionAfterDelay(int playerIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerIndex < questionDisplays.Length && questionDisplays[playerIndex] != null)
            questionDisplays[playerIndex].Hide();
        Team team = playerIndex == 0 ? Team.Left : Team.Right;
        answerDisplayManager.HidePlayerAnswers(team);
        _questionHidden[playerIndex] = true;
    }

    // =========================================================================
    // FSM — GameOver
    // =========================================================================

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opts)
    {
        gameModel.ExportLog();

        if (GameSessionManager.Instance != null)
        {
            var (left, right) = gameModel.GetScore();
            GameSessionManager.Instance.RecordScores(left, right);
            GameSessionManager.Instance.LastPlayedGame = "TestTongHopGame";
        }

        MusicManager.Instance?.PlayMainMusic();
        SceneManager.LoadScene("ScoreScene");
    }

    protected void StateMachineExit_GameOver(Enum prev, Dictionary<string, object> opts) { }

    // ── Feedback icons ────────────────────────────────────────────────────────

    /// <summary>✓ icon với FeedbackEffect animation (bounce + pulse).</summary>
    void ShowCorrectIcon(Team team)
    {
        int idx            = team == Team.Left ? 0 : 1;
        GameObject correct = idx == 0 ? p1CorrectIcon : p2CorrectIcon;
        GameObject wrong   = idx == 0 ? p1WrongIcon   : p2WrongIcon;
        if (wrong   != null) wrong.SetActive(false);
        if (correct != null)
        {
            correct.SetActive(true);
            FeedbackEffect fx = correct.GetComponent<FeedbackEffect>();
            if (fx == null) fx = correct.AddComponent<FeedbackEffect>();
            fx.Play(isCorrect: true, withFadeOut: false);  // bounce + pulse, rồi giữ đến HideFeedbackIcons
        }
    }

    /// <summary>✗ icon với shake animation, sau đó giữ nguyên (không fade) cho đến HideFeedbackIcons().</summary>
    void ShowWrongIcon(Team team)
    {
        int idx            = team == Team.Left ? 0 : 1;
        GameObject wrong   = idx == 0 ? p1WrongIcon   : p2WrongIcon;
        GameObject correct = idx == 0 ? p1CorrectIcon : p2CorrectIcon;
        if (correct != null) correct.SetActive(false);
        if (wrong   != null)
        {
            wrong.SetActive(true);
            FeedbackEffect fx = wrong.GetComponent<FeedbackEffect>();
            if (fx == null) fx = wrong.AddComponent<FeedbackEffect>();
            fx.Play(isCorrect: false, withFadeOut: false);  // bounce + shake, rồi đứng yên
        }
    }

    void HideFeedbackIcons()
    {
        StopFeedback(p1CorrectIcon);
        StopFeedback(p1WrongIcon);
        StopFeedback(p2CorrectIcon);
        StopFeedback(p2WrongIcon);
    }

    void StopFeedback(GameObject icon)
    {
        if (icon == null) return;
        FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
        if (fx != null) fx.StopEffect();
        icon.SetActive(false);
    }

    // ── Countdown (same pattern as TongHopGameView.ShowCountdown) ─────────────

    void ShowCountdown(int seconds, bool isStart = false)
    {
        string msg = $"{(isStart ? "Start" : "Next")} in {seconds}s";
        if (leftCountdownText)
        {
            leftCountdownText.text = msg;
            leftCountdownText.gameObject.SetActive(true);
        }
        if (rightCountdownText)
        {
            rightCountdownText.text = msg;
            rightCountdownText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Kích hoạt nhận diện cả 2 slot ngay lúc countdown bắt đầu — headless (không preview/bbox).
    /// Kết quả tới không đồng bộ (có thể trễ hơn cả countdown hiển thị) nên ghi vào
    /// _pendingRecognizedLeft/Right cho log round, đồng thời refresh tên hiển thị trên HUD ngay
    /// khi có kết quả thay vì chỉ log âm thầm như PlayerRecognitionHook cũ (chưa từng được gán).
    /// </summary>
    void CaptureRecognizedPlayers()
    {
        PlayerRecognitionService.Instance.RecognizeSlot(0, name =>
        {
            _pendingRecognizedLeft = name;
            RefreshHudNames();
        });
        PlayerRecognitionService.Instance.RecognizeSlot(1, name =>
        {
            _pendingRecognizedRight = name;
            RefreshHudNames();
        });
    }

    void RefreshHudNames()
    {
        if (gameHud == null || GameSessionManager.Instance == null) return;
        gameHud.UpdatePlayerNames(GameSessionManager.Instance.GetDisplayName1(), GameSessionManager.Instance.GetDisplayName2());
    }

    void HideCountdown()
    {
        if (leftCountdownText)  leftCountdownText.gameObject.SetActive(false);
        if (rightCountdownText) rightCountdownText.gameObject.SetActive(false);
    }

    // =========================================================================
    // Independent Play — mỗi player chạy coroutine riêng, không chờ nhau
    // =========================================================================

    void StartIndependentPlay()
    {
        _leftRoundsCompleted  = 0;
        _rightRoundsCompleted = 0;
        StartCoroutine(PlayerLoop(Team.Left));
        StartCoroutine(PlayerLoop(Team.Right));
    }

    IEnumerator PlayerLoop(Team team)
    {
        int  playerIdx   = team == Team.Left ? 0 : 1;
        bool isFirstRound = true;

        // Chạy đến khi timer hết (Update() trigger GameOver)
        while (GetCurrentState() != TestTongHopSceneState.GameOver)
        {
            // Câu đầu tiên: đếm ngược "Start in 3 2 1" trước khi hiện câu hỏi
            if (isFirstRound)
            {
                isFirstRound = false;
                PlayerRecognitionService.Instance.RecognizeSlot(playerIdx, _ => RefreshHudNames());
                int preCount = TongHopConfig.Current.nextQuestionDelayPerPlayer;
                for (int i = preCount; i >= 1; i--)
                {
                    if (GetCurrentState() == TestTongHopSceneState.GameOver) yield break;
                    ShowCountdownForPlayer(team, i, isStart: true);
                    yield return new WaitForSeconds(1f);
                }
                HideCountdownForPlayer(team);
            }

            // 1. Lấy câu hỏi tiếp theo từ pool
            QuestionData q = answerDisplayManager.GetNextQuestion();
            if (q == null) { Debug.LogError("[IndependentPlay] Pool rỗng!"); break; }
            // [DEBUG] trace — xóa khi đã xác nhận
            Debug.Log($"[PlayerLoop] {team} → q={q.id} | correct=[{string.Join(",", q.correctAnswers)}] | answers=[{string.Join(",", q.answers ?? System.Array.Empty<string>())}]");

            // 2. Hiện câu hỏi phía player này
            if (playerIdx < questionDisplays.Length)
                questionDisplays[playerIdx].Show(q);

            // 3. Setup buttons cho player này — callback báo khi player xong
            bool answered  = false;
            bool isCorrect = false;
            answerDisplayManager.SetupPlayerIndependent(team, q, (ok, _, __) =>
            {
                isCorrect = ok;
                answered  = true;
            });

            float startTime = Time.time;

            // 4. Chờ player trả lời hoặc hết giờ
            yield return new WaitUntil(() => answered ||
                GetCurrentState() == TestTongHopSceneState.GameOver);
            if (GetCurrentState() == TestTongHopSceneState.GameOver) yield break;

            // 5. Ghi kết quả
            // [DEBUG] trace — xóa khi đã xác nhận
            Debug.Log($"[PlayerLoop] {team} answered q={q.id} → isCorrect={isCorrect}");
            gameModel.RecordAnswer(q, isCorrect, team, Time.time - startTime);
            if (team == Team.Left) _leftRoundsCompleted++; else _rightRoundsCompleted++;

            // 6. Feedback icon
            if (isCorrect) { MusicManager.Instance?.PlayCorrectSfx(); ShowCorrectIcon(team); }
            else           { MusicManager.Instance?.PlayWrongSfx();   ShowWrongIcon(team);   }

            // 7. Ẩn câu hỏi + đáp án của player này ngay
            if (playerIdx < questionDisplays.Length) questionDisplays[playerIdx].Hide();
            answerDisplayManager.HidePlayerAnswers(team);

            // 8. Hiện feedback icon ~1.5s
            yield return new WaitForSeconds(TongHopConfig.Current.feedbackDelayCorrect);
            if (GetCurrentState() == TestTongHopSceneState.GameOver) yield break;

            // 9. Ẩn icon
            HidePlayerFeedbackIcon(team);

            // 10. Đếm ngược + nhận diện lại (người chơi có thể đã đổi)
            PlayerRecognitionService.Instance.RecognizeSlot(playerIdx, _ => RefreshHudNames());
            int delay = TongHopConfig.Current.nextQuestionDelayPerPlayer;
            for (int i = delay; i >= 1; i--)
            {
                if (GetCurrentState() == TestTongHopSceneState.GameOver) yield break;
                ShowCountdownForPlayer(team, i);
                yield return new WaitForSeconds(1f);
            }
            HideCountdownForPlayer(team);
        }

        // Hết giờ hoặc pool rỗng — ẩn giao diện bên này
        int idx = team == Team.Left ? 0 : 1;
        if (idx < questionDisplays.Length) questionDisplays[idx].Hide();
        answerDisplayManager.HidePlayerAnswers(team);
    }

    void ShowCountdownForPlayer(Team team, int seconds, bool isStart = false)
    {
        string msg = $"{(isStart ? "Start" : "Next")} in {seconds}s";
        var txt    = team == Team.Left ? leftCountdownText : rightCountdownText;
        if (txt) { txt.text = msg; txt.gameObject.SetActive(true); }
    }

    void HideCountdownForPlayer(Team team)
    {
        var txt = team == Team.Left ? leftCountdownText : rightCountdownText;
        if (txt) txt.gameObject.SetActive(false);
    }

    void HidePlayerFeedbackIcon(Team team)
    {
        if (team == Team.Left) { StopFeedback(p1CorrectIcon); StopFeedback(p1WrongIcon); }
        else                   { StopFeedback(p2CorrectIcon); StopFeedback(p2WrongIcon); }
    }

    // ── Adaptive difficulty ────────────────────────────────────────────────────

    void TryAdaptDifficulty(bool isCorrect)
    {
        var cfg = TongHopConfig.Current;
        if (!cfg.adaptiveDifficulty) return;

        if (isCorrect)
        {
            _correctStreak++;
            _wrongStreak = 0;
            if (_correctStreak >= cfg.correctStreakToLevelUp)
            {
                _correctStreak = 0;
                ShiftDifficulty(+1);
            }
        }
        else
        {
            _wrongStreak++;
            _correctStreak = 0;
            if (_wrongStreak >= cfg.wrongStreakToLevelDown)
            {
                _wrongStreak = 0;
                ShiftDifficulty(-1);
            }
        }
    }

    void ShiftDifficulty(int delta)
    {
        var cfg     = TongHopConfig.Current;
        int current = cfg.difficulty;
        int next    = Mathf.Clamp(current + delta, cfg.difficultyMin, cfg.difficultyMax);
        if (next == current) return;

        gameModel.SetDifficulty(next);
        Debug.Log($"[Adaptive] Difficulty: {current} → {next}  " +
                  $"(streak correct={_correctStreak} wrong={_wrongStreak})");
    }
}
