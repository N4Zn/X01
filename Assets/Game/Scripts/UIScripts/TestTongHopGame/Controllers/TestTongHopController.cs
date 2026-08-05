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

    // ── Independent play state ────────────────────────────────────────────────
    bool _isIndependentPlay;
    bool _independentStarted;
    int  _leftRoundsCompleted;
    int  _rightRoundsCompleted;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Update()
    {
        var state = GetCurrentState();
        if (state != TestTongHopSceneState.WaitAnswer &&
            state != TestTongHopSceneState.Feedback) return;

        if (gameModel.IsTimeUp) return;

        gameModel.GameTimer -= Time.deltaTime;
        gameHud?.UpdateTimer(gameModel.GameTimer);

        if (gameModel.IsTimeUp)
        {
            gameModel.GameTimer = 0;
            foreach (var display in questionDisplays) display.Hide();
            answerDisplayManager.Cleanup();
            _fsm.StateMachineChange(TestTongHopSceneState.GameOver);
        }
    }

    void Start()
    {
        _fsm          = gameObject.AddComponent<CustomFSMManager>();
        _fsm.fsmName  = nameof(TestTongHopController) + "FSM";
        _fsm.Initialize(typeof(TestTongHopSceneState), GetType(), false);
        _fsm.StateMachineChange(TestTongHopSceneState.Initialize);
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
        // SolarQuiz variants use SolarSystem ambient music; all other games use gameplay music.
        string gameName = GameSessionManager.Instance?.SelectedGameName ?? "";
        bool isSolarQuiz = gameName == "SolarQuiz" || gameName == "SolarQuizEn";
        if (isSolarQuiz)
            MusicManager.Instance?.PlaySolarSystemMusic();
        else
            MusicManager.Instance?.PlayGameplayMusic();

        gameModel.PointsPerCorrect = isSolarQuiz ? 10 : 1;

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

        gameHud?.Initialize(gameModel.Score, leftName, rightName);
        _fsm.StateMachineChange(TestTongHopSceneState.ShowQuestion);
    }

    protected void StateMachineExit_Initialize(Enum prev, Dictionary<string, object> opts) { }

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

        gameModel.IncrementRound();
        _failedThisRound.Clear();
        _questionHidden[0] = _questionHidden[1] = false;

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

        foreach (var display in questionDisplays)
            display.Show(_currentQuestion);

        MusicManager.Instance?.PlayQuestionSfx();

        _fsm.StateMachineChange(TestTongHopSceneState.WaitAnswer);
    }

    protected void StateMachineExit_ShowQuestion(Enum prev, Dictionary<string, object> opts) { }

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

    // Callback: ngay khi 1 player hết lượt và sai — hiện ✗ static, schedule ẩn câu hỏi sau delay
    void OnPlayerFailed(Team team)
    {
        if (_failedThisRound.Contains(team)) return;
        _failedThisRound.Add(team);
        MusicManager.Instance?.PlayWrongSfx();
        ShowWrongIcon(team);

        int idx = team == Team.Left ? 0 : 1;
        StartCoroutine(HideQuestionAfterDelay(idx, TongHopConfig.Current.feedbackDelayWrong));
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

            // ✗ cho loser — static + schedule ẩn câu hỏi (nếu OnPlayerFailed chưa chạy)
            Team loser    = team == Team.Left ? Team.Right : Team.Left;
            int loserIdx  = loser == Team.Left ? 0 : 1;
            if (!_failedThisRound.Contains(loser))
            {
                _failedThisRound.Add(loser);
                ShowWrongIcon(loser);
                StartCoroutine(HideQuestionAfterDelay(loserIdx, TongHopConfig.Current.feedbackDelayWrong));
            }

            // Ẩn câu hỏi của winner ngay (loser ẩn sau delay bởi HideQuestionAfterDelay)
            int winnerIdx = team == Team.Left ? 0 : 1;
            if (winnerIdx < questionDisplays.Length) questionDisplays[winnerIdx]?.Hide();
            _questionHidden[winnerIdx] = true;
        }
        else
        {
            // Cả 2 sai — đảm bảo ✗ + schedule ẩn cho bất kỳ bên nào chưa được xử lý
            // (SFX đã được phát trong OnPlayerFailed khi từng player fail)
            foreach (Team t in new[] { Team.Left, Team.Right })
            {
                if (!_failedThisRound.Contains(t))
                {
                    _failedThisRound.Add(t);
                    ShowWrongIcon(t);
                    int i = t == Team.Left ? 0 : 1;
                    StartCoroutine(HideQuestionAfterDelay(i, TongHopConfig.Current.feedbackDelayWrong));
                }
            }
        }

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
        // 1. Hiện feedback icon ~1.5s (feedbackDelayCorrect), câu hỏi tự ẩn song song
        yield return new WaitForSeconds(TongHopConfig.Current.feedbackDelayCorrect);
        if (GetCurrentState() != TestTongHopSceneState.Feedback) yield break;

        // 2. Ẩn icon + force-hide câu hỏi/đáp án (nếu HideQuestionAfterDelay chưa xong)
        HideFeedbackIcons();
        foreach (var d in questionDisplays) d?.Hide();
        answerDisplayManager.Cleanup();

        // 3. Đếm ngược "Next in Xs" trên nền trống
        int countFrom = TongHopConfig.Current.nextQuestionDelay;
        for (int i = countFrom; i >= 1; i--)
        {
            ShowCountdown(i);
            yield return new WaitForSeconds(1f);
            if (GetCurrentState() != TestTongHopSceneState.Feedback) yield break;
        }
        HideCountdown();

        // 4. Chuyển câu tiếp
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
            FeedbackEffect fx = correct.GetComponent<FeedbackEffect>() ?? correct.AddComponent<FeedbackEffect>();
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
            FeedbackEffect fx = wrong.GetComponent<FeedbackEffect>() ?? wrong.AddComponent<FeedbackEffect>();
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

    void ShowCountdown(int seconds)
    {
        string msg = $"Next in {seconds}s";
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
        int playerIdx = team == Team.Left ? 0 : 1;

        // Chạy đến khi timer hết (Update() trigger GameOver)
        while (GetCurrentState() != TestTongHopSceneState.GameOver)
        {
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

            // 10. Đếm ngược
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

    void ShowCountdownForPlayer(Team team, int seconds)
    {
        string msg = $"Next in {seconds}s";
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
