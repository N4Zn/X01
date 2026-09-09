using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for PlanetAlphabet game.
/// Split screen â€” each player has boxes with letters, tap in alphabetical order.
/// </summary>
public class PlanetAlphabetController : MonoBehaviour
{
    [SerializeField] private PlanetAlphabetView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _customFSMManager;
    private PlanetAlphabetModel _model;
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

        _model = new PlanetAlphabetModel();

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
        gameView.OnSettingClicked += () => { Debug.Log("PlanetAlphabet - Setting"); };
        gameView.OnRetryClicked += () => { gameView.HideGameOver(); _customFSMManager.StateMachineChange(PlanetAlphabetState.Initialize); };

        gameView.SetPlayerNames(
            GameSessionManager.Instance.GetDisplayName1() ?? "Player 1",
            GameSessionManager.Instance.GetDisplayName2() ?? "Player 2"
        );

        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = "PlanetAlphabetFSM";
        _customFSMManager.Initialize(typeof(PlanetAlphabetState), this.GetType(), false);
        _customFSMManager.StateMachineChange(PlanetAlphabetState.Initialize);
    }

    void Update()
    {
        if (GetState() == PlanetAlphabetState.Playing)
        {
            _model.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_model.GameTimer);
            if (_model.GameTimer <= 0)
            {
                _model.GameTimer = 0;
                _customFSMManager.StateMachineChange(PlanetAlphabetState.GameOver);
            }
        }
    }

    private PlanetAlphabetState GetState()
    {
        if (_customFSMManager == null) return PlanetAlphabetState.Initialize;
        Enum s = _customFSMManager.GetCurrentState();
        return s != null ? (PlanetAlphabetState)s : PlanetAlphabetState.Initialize;
    }

    // ===== FSM =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        _customFSMManager.StateMachineChange(PlanetAlphabetState.Tutorial);
    }
    protected void StateMachineExit_Initialize(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStart;
            tutorialPanel.ShowPlaceholder("HÆ°á»›ng dáº«n: Planet Alphabet\nNháº¥n cÃ¡c hÃ nh tinh theo thá»© tá»± báº£ng chá»¯ cÃ¡i!");
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
        GameSessionManager.Instance.LastPlayedGame = "PlanetAlphabetGame";
        SceneManager.LoadScene("ScoreScene");
    }
    protected void StateMachineExit_GameOver(Enum prev, Dictionary<string, object> opt) { }

    // ===== Game Logic =====

    private void StartGame()
    {
        _model.ResetGame();
        gameView.HideGameOver();
        gameView.SetQuestionText("Nháº¥n theo thá»© tá»± báº£ng chá»¯ cÃ¡i!");
        gameView.UpdateScores(0, 0);
        PlayerRecognitionService.Instance.BeginGameSession(GameSessionManager.ResolveActiveGameName("PlanetAlphabetGame"));
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
        _customFSMManager.StateMachineChange(PlanetAlphabetState.Playing);
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
        if (GetState() != PlanetAlphabetState.Playing) yield break;

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
        if (GetState() != PlanetAlphabetState.Playing) return;

        CancelQuestionTimeout(playerIndex);

        bool correct = _model.CheckTap(playerIndex, boxIndex);
        string question = string.Join(",", _model.Values[playerIndex]);
        string tapped = _model.Values[playerIndex][boxIndex];
        PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], question, tapped, correct, Time.time - _questionShownTime[playerIndex]);

        if (correct)
        {
            MusicManager.Instance?.PlayCorrectSfx();
            _model.AdvanceCorrect(playerIndex);
            gameView.SetBoxCorrect(playerIndex, boxIndex);

            if (_model.IsRoundComplete(playerIndex))
            {
                if (playerIndex == 0) _model.Player1Score++;
                else _model.Player2Score++;
                gameView.UpdateScores(_model.Player1Score, _model.Player2Score);
                gameView.ShowFeedback(playerIndex, true);

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
                StartQuestionTimeout(playerIndex);
            }
        }
        else
        {
            MusicManager.Instance?.PlayWrongSfx();
            gameView.SetBoxWrong(playerIndex, boxIndex);
            gameView.SetPlayerInteractable(playerIndex, false);
            gameView.ShowFeedback(playerIndex, false);
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
        if (GetState() != PlanetAlphabetState.Playing) yield break;

        gameView.HideFeedback(playerIndex);
        gameView.HideBoxes(playerIndex);

        PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, _ => RefreshPlayerNames());

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (GetState() != PlanetAlphabetState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        LoadNewRound(playerIndex);
    }

    private IEnumerator ResetRoundForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (GetState() != PlanetAlphabetState.Playing) yield break;

        gameView.HideFeedback(playerIndex);
        gameView.HideBoxes(playerIndex);

        PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, _ => RefreshPlayerNames());

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (GetState() != PlanetAlphabetState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        LoadNewRound(playerIndex);
    }
}
