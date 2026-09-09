using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for PlanetOrder game.
/// Split screen â€” each player has 3 boxes with numbers, tap in ascending order.
/// Both players play simultaneously (like AddUp).
/// Correct round = +1 star + new round. Wrong tap = reset that player's round.
/// Team mode: 3s countdown between rounds.
/// </summary>
public class PlanetOrderController : MonoBehaviour
{
    [SerializeField] private PlanetOrderView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _customFSMManager;
    private PlanetOrderModel _model;
    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _p1TimeoutCoroutine;
    private Coroutine _p2TimeoutCoroutine;
    private float _feedbackDelay = 1.5f;
    private float _questionTimeout = 10f;
    private readonly int[] _roundIndex = new int[2];
    private readonly float[] _questionShownTime = new float[2];

    void Start()
    {
        if (GameSessionManager.Instance == null) new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
        if (GameSettings.Instance == null) new GameObject("GameSettings").AddComponent<GameSettings>();
        if (MusicManager.Instance == null) new GameObject("MusicManager").AddComponent<MusicManager>();

        _model = new PlanetOrderModel();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;
        if (GameSettings.Instance != null) _feedbackDelay = GameSettings.Instance.RoundEndDelay;

        gameView.InitView();

        // NamNN change with Android Studio Agent: Set dynamic max score
        if (GameSessionManager.Instance != null)
        {
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);
        }

        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance?.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance?.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnSettingClicked += () => { Debug.Log("NDL: PlanetOrder - Setting"); };
        gameView.OnRetryClicked += () => { gameView.HideGameOver(); _customFSMManager.StateMachineChange(PlanetOrderState.Initialize); };

        gameView.SetPlayerNames(
            GameSessionManager.Instance.GetDisplayName1() ?? "Player 1",
            GameSessionManager.Instance.GetDisplayName2() ?? "Player 2"
        );

        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = "PlanetOrderFSM";
        _customFSMManager.Initialize(typeof(PlanetOrderState), this.GetType(), false);
        _customFSMManager.StateMachineChange(PlanetOrderState.Initialize);
    }

    void Update()
    {
        if (GetState() == PlanetOrderState.Playing)
        {
            _model.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_model.GameTimer);
            if (_model.GameTimer <= 0)
            {
                _model.GameTimer = 0;
                _customFSMManager.StateMachineChange(PlanetOrderState.GameOver);
            }
        }
    }

    private PlanetOrderState GetState()
    {
        if (_customFSMManager == null) return PlanetOrderState.Initialize;
        Enum s = _customFSMManager.GetCurrentState();
        return s != null ? (PlanetOrderState)s : PlanetOrderState.Initialize;
    }

    // ===== FSM =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        _customFSMManager.StateMachineChange(PlanetOrderState.Tutorial);
    }
    protected void StateMachineExit_Initialize(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStart;
            tutorialPanel.ShowPlaceholder("Huong dan: PlanetOrder");
        }
        else { StartGame(); }
    }
    protected void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= OnTutorialStart;
    }
    private void OnTutorialStart() { StartGame(); }

    protected void StateMachineEnter_Playing(Enum prev, Dictionary<string, object> opt) { MusicManager.Instance?.PlayGameplayMusic(); }
    protected void StateMachineExit_Playing(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineEnter_WaitingSwitch(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_WaitingSwitch(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineEnter_RoundComplete(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_RoundComplete(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        gameView.SetPlayerInteractable(0, false);
        gameView.SetPlayerInteractable(1, false);
        MusicManager.Instance?.PlayMainMusic();
        GameSessionManager.Instance.RecordScores(_model.Player1Score, _model.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "PlanetOrderGame";
        SceneManager.LoadScene("ScoreScene");
    }
    protected void StateMachineExit_GameOver(Enum prev, Dictionary<string, object> opt) { }

    // ===== Game Logic =====

    private void StartGame()
    {
        _model.ResetGame();
        gameView.HideGameOver();
        gameView.SetQuestionText("Nhan theo thu tu tang dan!");
        gameView.UpdateScores(0, 0);
        PlayerRecognitionService.Instance.BeginGameSession(GameSessionManager.ResolveActiveGameName("PlanetOrderGame"));
        StartCoroutine(InitialStartCountdown());
    }

    /// <summary>"Start in 3,2,1" before round 1, recognizing both slots throughout so gameplay
    /// doesn't start under stale/default names.</summary>
    private IEnumerator InitialStartCountdown()
    {
        PlayerRecognitionService.Instance.RecognizeSlot(0, _ => RefreshPlayerNames());
        PlayerRecognitionService.Instance.RecognizeSlot(1, _ => RefreshPlayerNames());

        for (int i = 3; i >= 1; i--)
        {
            gameView.ShowCountdown(0, i);
            gameView.ShowCountdown(1, i);
            yield return new WaitForSeconds(1f);
        }
        gameView.HideCountdown(0);
        gameView.HideCountdown(1);

        LoadNewRound(0);
        LoadNewRound(1);
        _customFSMManager.StateMachineChange(PlanetOrderState.Playing);
    }

    private void RefreshPlayerNames()
    {
        gameView.SetPlayerNames(
            GameSessionManager.Instance.GetDisplayName1(),
            GameSessionManager.Instance.GetDisplayName2());
    }

    private void LoadNewRound(int playerIndex)
    {
        _model.GenerateRound(playerIndex);
        _roundIndex[playerIndex]++;
        _questionShownTime[playerIndex] = Time.time;
        gameView.HideFeedback(playerIndex);
        gameView.ShowBoxes(playerIndex, _model.Values[playerIndex]);
        gameView.SetPlayerInteractable(playerIndex, true);
        MusicManager.Instance?.PlayQuestionSfx();
        StartQuestionTimeout(playerIndex);
    }

    private void StartQuestionTimeout(int playerIndex)
    {
        if (playerIndex == 0)
        { if (_p1TimeoutCoroutine != null) StopCoroutine(_p1TimeoutCoroutine); _p1TimeoutCoroutine = StartCoroutine(QuestionTimeoutCoroutine(playerIndex)); }
        else
        { if (_p2TimeoutCoroutine != null) StopCoroutine(_p2TimeoutCoroutine); _p2TimeoutCoroutine = StartCoroutine(QuestionTimeoutCoroutine(playerIndex)); }
    }

    private void CancelQuestionTimeout(int playerIndex)
    {
        if (playerIndex == 0 && _p1TimeoutCoroutine != null) { StopCoroutine(_p1TimeoutCoroutine); _p1TimeoutCoroutine = null; }
        if (playerIndex == 1 && _p2TimeoutCoroutine != null) { StopCoroutine(_p2TimeoutCoroutine); _p2TimeoutCoroutine = null; }
    }

    private IEnumerator QuestionTimeoutCoroutine(int playerIndex)
    {
        yield return new WaitForSeconds(_questionTimeout);
        if (GetState() != PlanetOrderState.Playing) yield break;

        MusicManager.Instance?.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        gameView.SetPlayerInteractable(playerIndex, false);
        string question = string.Join(",", _model.Values[playerIndex]);
        PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], question, "(timeout)", false, Time.time - _questionShownTime[playerIndex]);
        if (playerIndex == 0)
        { if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine); _p1FeedbackCoroutine = StartCoroutine(LoadNextRoundForPlayer(playerIndex)); }
        else
        { if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine); _p2FeedbackCoroutine = StartCoroutine(LoadNextRoundForPlayer(playerIndex)); }
    }

    private void OnBoxTapped(int playerIndex, int boxIndex)
    {
        if (GetState() != PlanetOrderState.Playing) return;

        CancelQuestionTimeout(playerIndex);

        bool correct = _model.CheckTap(playerIndex, boxIndex);
        string question = string.Join(",", _model.Values[playerIndex]);
        string tapped = _model.Values[playerIndex][boxIndex].ToString();
        PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], question, tapped, correct, Time.time - _questionShownTime[playerIndex]);

        if (correct)
        {
            MusicManager.Instance?.PlayCorrectSfx();
            _model.AdvanceCorrect(playerIndex);
            gameView.SetBoxCorrect(playerIndex, boxIndex);

            if (_model.IsRoundComplete(playerIndex))
            {
                // Round complete! +1 star
                if (playerIndex == 0) _model.Player1Score++;
                else _model.Player2Score++;
                gameView.UpdateScores(_model.Player1Score, _model.Player2Score);
                gameView.ShowFeedback(playerIndex, true);

                // Load next round after delay
                if (playerIndex == 0)
                {
                    if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
                    _p1FeedbackCoroutine = StartCoroutine(LoadNextRoundForPlayer(playerIndex));
                }
                else
                {
                    if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
                    _p2FeedbackCoroutine = StartCoroutine(LoadNextRoundForPlayer(playerIndex));
                }
            }
            else
            {
                // Partial progress â€” restart timeout so player still has a deadline for next tap
                StartQuestionTimeout(playerIndex);
            }
        }
        else
        {
            // Wrong! Show feedback, reset round after delay
            MusicManager.Instance?.PlayWrongSfx();
            gameView.SetBoxWrong(playerIndex, boxIndex);
            gameView.ShowFeedback(playerIndex, false);
            gameView.SetPlayerInteractable(playerIndex, false);

            if (playerIndex == 0)
            {
                if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
                _p1FeedbackCoroutine = StartCoroutine(ResetRoundForPlayer(playerIndex));
            }
            else
            {
                if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
                _p2FeedbackCoroutine = StartCoroutine(ResetRoundForPlayer(playerIndex));
            }
        }
    }

    private IEnumerator LoadNextRoundForPlayer(int playerIndex)
    {
        gameView.SetPlayerInteractable(playerIndex, false);
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (GetState() != PlanetOrderState.Playing) yield break;

        gameView.HideFeedback(playerIndex);
        gameView.HideBoxes(playerIndex);

        PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, _ => RefreshPlayerNames());

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (GetState() != PlanetOrderState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        LoadNewRound(playerIndex);
    }

    private IEnumerator ResetRoundForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (GetState() != PlanetOrderState.Playing) yield break;

        gameView.HideFeedback(playerIndex);
        gameView.HideBoxes(playerIndex);

        PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, _ => RefreshPlayerNames());

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (GetState() != PlanetOrderState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        LoadNewRound(playerIndex);
    }
}
