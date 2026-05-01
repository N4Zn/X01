using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for PlanetAlphabet game.
/// Split screen — each player has boxes with letters, tap in alphabetical order.
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

    void Start()
    {
        if (GameSessionManager.Instance == null) new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
        if (GameSettings.Instance == null) new GameObject("GameSettings").AddComponent<GameSettings>();
        if (MusicManager.Instance == null) new GameObject("MusicManager").AddComponent<MusicManager>();

        _model = new PlanetAlphabetModel();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;

        gameView.InitView();
        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("HomeScene"); };
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
            tutorialPanel.ShowPlaceholder("Hướng dẫn: Planet Alphabet\nNhấn các hành tinh theo thứ tự bảng chữ cái!");
        }
        else { StartGame(); }
    }
    protected void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= OnTutorialStart;
    }
    private void OnTutorialStart() { StartGame(); }

    protected void StateMachineEnter_Playing(Enum prev, Dictionary<string, object> opt) { MusicManager.Instance.PlayGameplayMusic(); }
    protected void StateMachineExit_Playing(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineEnter_WaitingSwitch(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_WaitingSwitch(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineEnter_RoundComplete(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_RoundComplete(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        gameView.SetPlayerInteractable(0, false);
        gameView.SetPlayerInteractable(1, false);
        MusicManager.Instance.PlayMainMusic();
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
        gameView.SetQuestionText("Nhấn theo thứ tự bảng chữ cái!");
        gameView.UpdateScores(0, 0);
        LoadNewRound(0);
        LoadNewRound(1);
        _customFSMManager.StateMachineChange(PlanetAlphabetState.Playing);
    }

    private void LoadNewRound(int playerIndex)
    {
        _model.GenerateRound(playerIndex);
        gameView.HideFeedback(playerIndex);
        gameView.ShowBoxes(playerIndex, _model.Values[playerIndex]);
        gameView.SetPlayerInteractable(playerIndex, true);
        MusicManager.Instance.PlayQuestionSfx();
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

        MusicManager.Instance.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        gameView.SetPlayerInteractable(playerIndex, false);
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

        if (correct)
        {
            MusicManager.Instance.PlayCorrectSfx();
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
            MusicManager.Instance.PlayWrongSfx();
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
        yield return new WaitForSeconds(_feedbackDelay);
        if (GetState() != PlanetAlphabetState.Playing) yield break;

        if (GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            gameView.HideFeedback(playerIndex);
            for (int i = 3; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
                if (GetState() != PlanetAlphabetState.Playing) yield break;
            }
            gameView.HideCountdown(playerIndex);
        }

        LoadNewRound(playerIndex);
    }

    private IEnumerator ResetRoundForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(_feedbackDelay);
        if (GetState() != PlanetAlphabetState.Playing) yield break;

        if (GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            gameView.HideFeedback(playerIndex);
            for (int i = 3; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
                if (GetState() != PlanetAlphabetState.Playing) yield break;
            }
            gameView.HideCountdown(playerIndex);
        }

        LoadNewRound(playerIndex);
    }
}
