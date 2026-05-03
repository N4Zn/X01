using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for PathFinder game — procedural maze with junction decisions.
/// No CSV data needed — mazes are generated at runtime.
/// </summary>
public class PathFinderGameController : MonoBehaviour
{
    [SerializeField] private PathFinderGameView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;
    [SerializeField] private UnityEngine.Video.VideoClip tutorialClip;
    [SerializeField] private bool forceResolution1024x600 = true;

    private CustomFSMManager _customFSMManager;
    private PathFinderGameModel _gameModel;

    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _p1TimeoutCoroutine;
    private Coroutine _p2TimeoutCoroutine;

    private bool _p1Answered = false;
    private bool _p2Answered = false;

    private float _feedbackDelay = 1.5f;
    private float _questionTimeout = 10f;

    void Start()
    {
        if (forceResolution1024x600)
        {
            Screen.SetResolution(1024, 600, false);
        }

        _gameModel = new PathFinderGameModel();

        if (GameSettings.Instance != null)
        {
            _feedbackDelay = GameSettings.Instance.GetFeedbackDelay();
            _questionTimeout = GameSettings.Instance.QuestionTimeout;
        }

        gameView.InitView();
        gameView.OnAnswerSelected += OnAnswerSelected;
        gameView.OnBackClicked += OnBackClicked;

        InitFSMManager();
    }

    private void InitFSMManager()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(PathFinderSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(PathFinderSceneState.Initialize);
    }

    // ===== FSM States =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: PathFinder - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(PathFinderSceneState.Tutorial);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
    }

    protected void StateMachineEnter_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: PathFinder - StateMachineEnter_Tutorial");
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStartGame;
            if (tutorialClip != null)
                tutorialPanel.Show(tutorialClip, "Hướng dẫn: PathFinder");
            else
                tutorialPanel.ShowPlaceholder("Hướng dẫn: PathFinder");
        }
        else
        {
            StartNewGame();
        }
    }

    protected void StateMachineExit_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        if (tutorialPanel != null)
            tutorialPanel.OnStartGame -= OnTutorialStartGame;
    }

    private void OnTutorialStartGame()
    {
        StartNewGame();
    }

    protected void StateMachineEnter_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: PathFinder - StateMachineEnter_Playing");
        MusicManager.Instance.PlayGameplayMusic();
    }

    protected void StateMachineExit_Playing(Enum previousState, Dictionary<string, object> options)
    {
    }

    protected void StateMachineEnter_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: PathFinder - StateMachineEnter_GameOver");
        OnGameOver();
    }

    protected void StateMachineExit_GameOver(Enum previousState, Dictionary<string, object> options)
    {
    }

    // ===== Game Logic =====

    private void StartNewGame()
    {
        _gameModel.ResetForNewGame();
        gameView.UpdateScore(0, 0);
        gameView.UpdateScore(1, 0);
        gameView.UpdateStars(0, 0);
        gameView.UpdateStars(1, 0);
        gameView.UpdateTimer(_gameModel.GameTimer);

        LoadNewRound();

        _customFSMManager.StateMachineChange(PathFinderSceneState.Playing);
    }

    private void LoadNewRound()
    {
        _gameModel.CurrentRound++;
        _p1Answered = false;
        _p2Answered = false;
        _gameModel.Player1Selection = -1;
        _gameModel.Player2Selection = -1;

        // Generate maze for each player
        MazeGenerator.MazePuzzle p1 = _gameModel.GenerateNewPuzzle(0);
        MazeGenerator.MazePuzzle p2 = _gameModel.GenerateNewPuzzle(1);

        // Display after a short delay to let layout settle
        StartCoroutine(DisplayMazesDelayed(p1, p2));
        MusicManager.Instance.PlayQuestionSfx();
        StartQuestionTimeout(0);
        StartQuestionTimeout(1);

        Debug.Log($"NDL: PathFinder Round {_gameModel.CurrentRound} - P1 correct={p1.CorrectChoice} P2 correct={p2.CorrectChoice}");
    }

    private IEnumerator DisplayMazesDelayed(MazeGenerator.MazePuzzle p1, MazeGenerator.MazePuzzle p2)
    {
        yield return null; // wait 1 frame for layout
        gameView.DisplayMaze(0, p1);
        gameView.DisplayMaze(1, p2);
    }

    void Update()
    {
        if (_customFSMManager == null) return;

        Enum state = _customFSMManager.GetCurrentState();
        if (state != null && state.ToString() == PathFinderSceneState.Playing.ToString())
        {
            UpdateTimer();
        }
    }

    private void UpdateTimer()
    {
        _gameModel.GameTimer -= Time.deltaTime;
        if (_gameModel.GameTimer <= 0f)
        {
            _gameModel.GameTimer = 0f;
            gameView.UpdateTimer(0f);
            _customFSMManager.StateMachineChange(PathFinderSceneState.GameOver);
            return;
        }
        gameView.UpdateTimer(_gameModel.GameTimer);
    }

    // ===== Answer Handling =====

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
        Enum state = _customFSMManager != null ? _customFSMManager.GetCurrentState() : null;
        if (state == null || state.ToString() != PathFinderSceneState.Playing.ToString()) yield break;
        bool answered = playerIndex == 0 ? _p1Answered : _p2Answered;
        if (answered) yield break;

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
        MusicManager.Instance.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        if (playerIndex == 0)
        { if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine); _p1FeedbackCoroutine = StartCoroutine(NextRoundAfterDelay(playerIndex)); }
        else
        { if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine); _p2FeedbackCoroutine = StartCoroutine(NextRoundAfterDelay(playerIndex)); }
    }

    private void OnAnswerSelected(int playerIndex, int choice)
    {
        if (playerIndex == 0 && _p1Answered) return;
        if (playerIndex == 1 && _p2Answered) return;

        CancelQuestionTimeout(playerIndex);

        Debug.Log($"NDL: PathFinder - Player {playerIndex + 1} chose {choice} (0=Left,1=Straight,2=Right)");

        _gameModel.SetSelection(playerIndex, choice);

        if (playerIndex == 0) _p1Answered = true;
        else _p2Answered = true;

        gameView.HighlightSelectedOption(playerIndex, choice);

        bool isCorrect = _gameModel.CheckAnswer(playerIndex);
        MazeGenerator.MazePuzzle puzzle = playerIndex == 0 ? _gameModel.Player1Puzzle : _gameModel.Player2Puzzle;

        if (isCorrect)
        {
            MusicManager.Instance.PlayCorrectSfx();
            int points = _gameModel.GetScoreForDifficulty(puzzle.Difficulty);
            if (playerIndex == 0)
                _gameModel.Player1Score += points;
            else
                _gameModel.Player2Score += points;

            gameView.UpdateScore(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);
            gameView.UpdateStars(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);
        }
        else
        {
            MusicManager.Instance.PlayWrongSfx();
        }

        gameView.ShowAnswerResult(playerIndex, choice, isCorrect);

        // Schedule next round
        if (playerIndex == 0)
        {
            if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
            _p1FeedbackCoroutine = StartCoroutine(NextRoundAfterDelay(playerIndex));
        }
        else
        {
            if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
            _p2FeedbackCoroutine = StartCoroutine(NextRoundAfterDelay(playerIndex));
        }
    }

    private IEnumerator NextRoundAfterDelay(int playerIndex)
    {
        yield return new WaitForSeconds(_feedbackDelay);

        Enum state = _customFSMManager.GetCurrentState();
        if (state == null || state.ToString() != PathFinderSceneState.Playing.ToString())
            yield break;

        // Team mode: 3-second countdown before next round
        if (GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            //NamNN change with Android Studio Agent
            // Hide this player's feedback icon before showing countdown
            gameView.HideFeedback(playerIndex);
            gameView.HideQuestion(playerIndex);

            for (int i = 3; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
                state = _customFSMManager.GetCurrentState();
                if (state == null || state.ToString() != PathFinderSceneState.Playing.ToString())
                    yield break;
            }
            gameView.HideCountdown(playerIndex);
        }

        if (playerIndex == 0)
        {
            _p1Answered = false;
            _gameModel.Player1Selection = -1;
        }
        else
        {
            _p2Answered = false;
            _gameModel.Player2Selection = -1;
        }

        _gameModel.CurrentRound++;
        MazeGenerator.MazePuzzle puzzle = _gameModel.GenerateNewPuzzle(playerIndex);
        gameView.DisplayMaze(playerIndex, puzzle);
        MusicManager.Instance.PlayQuestionSfx();
        StartQuestionTimeout(playerIndex);
    }

    // ===== Game Over =====

    private void OnGameOver()
    {
        Debug.Log($"NDL: PathFinder GameOver - P1:{_gameModel.Player1Score} P2:{_gameModel.Player2Score}");
        MusicManager.Instance.PlayMainMusic();

        GameSessionManager.Instance.RecordScores(_gameModel.Player1Score, _gameModel.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "PathFinderGame";

        StartCoroutine(TransitionToScoreScene());
    }

    private IEnumerator TransitionToScoreScene()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene("ScoreScene");
    }

    private void OnBackClicked()
    {
        MusicManager.Instance.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }
}
