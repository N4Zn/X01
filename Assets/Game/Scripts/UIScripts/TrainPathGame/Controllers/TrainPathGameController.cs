using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MasterData;

/// <summary>
/// Controller for the TrainPath game scene
/// Split-screen 2-player game (1024x600)
/// Players see a 3x3 grid with one hidden cell and guess its type
/// Train animates along the path after answer
/// </summary>
public class TrainPathGameController : MonoBehaviour
{
    [SerializeField] private TrainPathGameView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;
    [SerializeField] private UnityEngine.Video.VideoClip tutorialClip;
    [SerializeField] private bool forceResolution1024x600 = true;

    private CustomFSMManager _customFSMManager;
    private TrainPathGameModel _gameModel;

    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _p1TimeoutCoroutine;
    private Coroutine _p2TimeoutCoroutine;

    private bool _p1Answered = false;
    private bool _p2Answered = false;

    private float _feedbackDelay = 2.5f; // Increased for train animation
    private float _questionTimeout = 10f;

    void Start()
    {
        if (forceResolution1024x600)
        {
            Screen.SetResolution(1024, 600, false);
        }

        // Ensure TrainPathMaster is loaded before initializing game
        StartCoroutine(InitializeGameWithMasterData());
    }

    private IEnumerator InitializeGameWithMasterData()
    {
        bool isLoaded = false;
        int retryCount = 0;
        const int maxRetries = 10;

        while (!isLoaded && retryCount < maxRetries)
        {
            bool checkResult = CheckAndLoadTrainPathMaster();

            if (checkResult)
            {
                isLoaded = true;
            }
            else
            {
                Debug.LogWarning($"NDL: TrainPathMaster not loaded yet, retrying... ({retryCount + 1}/{maxRetries})");
                yield return new WaitForSeconds(0.5f);
                retryCount++;
            }
        }

        if (!isLoaded)
        {
            Debug.LogError("NDL: Failed to load TrainPathMaster after retries. Returning to MenuScene.");
            SceneManager.LoadScene("MenuScene");
            yield break;
        }

        _gameModel = new TrainPathGameModel();
        _gameModel.LoadQuestions();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;

        gameView.InitView();
        gameView.OnOptionSelected += OnOptionSelected;
        gameView.OnBackClicked += OnBackClicked;

        InitFSMManager();
    }

    private bool CheckAndLoadTrainPathMaster()
    {
        try
        {
            TrainPathMaster master = MasterDataCache.GetCache<TrainPathMaster>();
            if (master != null && master.param != null && master.param.Count > 0)
            {
                Debug.Log($"NDL: TrainPathMaster loaded successfully with {master.param.Count} templates");
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NDL: TrainPathMaster not in cache: {ex.Message}");
        }

        // Try to load it manually
        try
        {
            if (LoadCsvDataForEditor.Instance == null)
            {
                GameObject loaderObj = new GameObject("LoadCsvDataForEditor");
                loaderObj.AddComponent<LoadCsvDataForEditor>();
            }
            LoadCsvDataForEditor.Instance.LoadMasterData("TrainPathMaster");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NDL: Failed to load TrainPathMaster: {ex.Message}");
        }

        return false;
    }

    void Update()
    {
        if (GetCurrentState() == TrainPathSceneState.Playing)
        {
            UpdateTimer();

            // Continuously rotate both grids
            gameView.UpdateGridRotation(0, Time.deltaTime);
            gameView.UpdateGridRotation(1, Time.deltaTime);
        }
    }

    void OnDestroy()
    {
        if (gameView != null)
        {
            gameView.OnOptionSelected -= OnOptionSelected;
            gameView.OnBackClicked -= OnBackClicked;
        }
    }

    // ===== FSM Management =====

    private void InitFSMManager()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(TrainPathSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(TrainPathSceneState.Initialize);
    }

    public TrainPathSceneState GetCurrentState()
    {
        if (_customFSMManager == null) return TrainPathSceneState.Initialize;
        return (TrainPathSceneState)_customFSMManager.GetCurrentState();
    }

    // ===== State Machine Handlers =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(TrainPathSceneState.Tutorial);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_Tutorial");
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStartGame;
            if (tutorialClip != null)
                tutorialPanel.Show(tutorialClip, "Hướng dẫn: TrainPath");
            else
                tutorialPanel.ShowPlaceholder("Hướng dẫn: TrainPath");
        }
        else
        {
            StartNewGame();
        }
    }

    protected void StateMachineExit_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_Tutorial");
        if (tutorialPanel != null)
            tutorialPanel.OnStartGame -= OnTutorialStartGame;
    }

    private void OnTutorialStartGame()
    {
        StartNewGame();
    }

    protected void StateMachineEnter_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_Playing");
        MusicManager.Instance.PlayGameplayMusic();
    }

    protected void StateMachineExit_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_Playing");
    }

    protected void StateMachineEnter_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_Paused");
        Time.timeScale = 0;
    }

    protected void StateMachineExit_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_Paused");
        Time.timeScale = 1;
    }

    protected void StateMachineEnter_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_RoundResult");
    }

    protected void StateMachineExit_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_RoundResult");
    }

    protected void StateMachineEnter_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_GameOver");
        gameView.SetPlayerOptionsInteractable(0, false);
        gameView.SetPlayerOptionsInteractable(1, false);
        MusicManager.Instance.PlayMainMusic();

        GameSessionManager.Instance.RecordScores(_gameModel.Player1Score, _gameModel.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "TrainPathGame";
        SceneManager.LoadScene("ScoreScene");
    }

    protected void StateMachineExit_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_GameOver");
    }

    protected void StateMachineEnter_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineEnter_GameResult");
    }

    protected void StateMachineExit_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TrainPath - StateMachineExit_GameResult");
    }

    // ===== Game Logic =====

    private void StartNewGame()
    {
        _gameModel.ResetGame();
        gameView.UpdateScores(0, 0);

        LoadNewRound();
        _customFSMManager.StateMachineChange(TrainPathSceneState.Playing);
    }

    private void LoadNewRound()
    {
        _gameModel.CurrentRound++;
        _p1Answered = false;
        _p2Answered = false;

        TrainPathPuzzle p1Puzzle = _gameModel.GenerateNewPuzzle(0);
        TrainPathPuzzle p2Puzzle = _gameModel.GenerateNewPuzzle(1);

        _gameModel.ResetSelection(0);
        _gameModel.ResetSelection(1);

        gameView.DisplayPuzzle(0, p1Puzzle);
        gameView.DisplayPuzzle(1, p2Puzzle);
        gameView.SetPlayerOptionsInteractable(0, true);
        gameView.SetPlayerOptionsInteractable(1, true);
        gameView.HideFeedbackIcons();
        MusicManager.Instance.PlayQuestionSfx();
        StartQuestionTimeout(0);
        StartQuestionTimeout(1);
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
        if (GetCurrentState() != TrainPathSceneState.Playing) yield break;
        bool answered = playerIndex == 0 ? _p1Answered : _p2Answered;
        if (answered) yield break;

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
        MusicManager.Instance.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        gameView.SetPlayerOptionsInteractable(playerIndex, false);
        if (playerIndex == 0)
        { if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine); _p1FeedbackCoroutine = StartCoroutine(LoadNextPuzzleForPlayer(playerIndex)); }
        else
        { if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine); _p2FeedbackCoroutine = StartCoroutine(LoadNextPuzzleForPlayer(playerIndex)); }
    }

    private void UpdateTimer()
    {
        _gameModel.GameTimer -= Time.deltaTime;
        gameView.UpdateTimer(_gameModel.GameTimer);

        if (_gameModel.GameTimer <= 0)
        {
            _gameModel.GameTimer = 0;
            _customFSMManager.StateMachineChange(TrainPathSceneState.GameOver);
        }
    }

    // ===== Event Handlers =====

    private void OnOptionSelected(int playerIndex, int optionIndex)
    {
        if (GetCurrentState() != TrainPathSceneState.Playing) return;

        if (playerIndex == 0 && _p1Answered) return;
        if (playerIndex == 1 && _p2Answered) return;

        CancelQuestionTimeout(playerIndex);

        _gameModel.SetSelection(playerIndex, optionIndex);
        gameView.UpdateOptionSelection(playerIndex, optionIndex);

        bool isCorrect = _gameModel.CheckAnswer(playerIndex);
        TrainPathPuzzle puzzle = playerIndex == 0 ? _gameModel.Player1Puzzle : _gameModel.Player2Puzzle;

        if (isCorrect)
        {
            MusicManager.Instance.PlayCorrectSfx();
            int difficulty = _gameModel.GetPuzzleDifficulty(playerIndex);
            int stars = _gameModel.GetStarsForDifficulty(difficulty);

            if (playerIndex == 0)
            {
                _gameModel.Player1Score += stars;
                _p1Answered = true;
            }
            else
            {
                _gameModel.Player2Score += stars;
                _p2Answered = true;
            }

            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);
            gameView.UpdateStars(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);
            gameView.SetCorrectAnswerBorderColor(playerIndex, optionIndex);
            gameView.RevealHiddenCell(playerIndex, puzzle, true);
            gameView.ShowFeedback(playerIndex, true);
            gameView.SetPlayerOptionsInteractable(playerIndex, false);
        }
        else
        {
            MusicManager.Instance.PlayWrongSfx();
            if (playerIndex == 0) _p1Answered = true;
            else _p2Answered = true;

            int correctIdx = _gameModel.GetCorrectSelectionIndex(playerIndex);
            gameView.SetWrongAnswerBorderColor(playerIndex, optionIndex, correctIdx);
            gameView.RevealHiddenCell(playerIndex, puzzle, false);
            gameView.ShowFeedback(playerIndex, false);
            gameView.SetPlayerOptionsInteractable(playerIndex, false);
        }

        // Animate train along the path after revealing answer
        gameView.AnimateTrain(playerIndex, puzzle);

        // Load next puzzle after delay (gives time for animation)
        if (playerIndex == 0)
        {
            if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
            _p1FeedbackCoroutine = StartCoroutine(LoadNextPuzzleForPlayer(playerIndex));
        }
        else
        {
            if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
            _p2FeedbackCoroutine = StartCoroutine(LoadNextPuzzleForPlayer(playerIndex));
        }
    }

    private IEnumerator LoadNextPuzzleForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(_feedbackDelay);

        if (GetCurrentState() != TrainPathSceneState.Playing) yield break;

        // Team mode: 3-second countdown before next puzzle
        if (GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            //NamNN change with Android Studio Agent
            // Hide this player's feedback icon before showing countdown
            gameView.HideFeedbackIcon(playerIndex);
            gameView.HideQuestion(playerIndex);

            for (int i = 3; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
                if (GetCurrentState() != TrainPathSceneState.Playing) yield break;
            }
            gameView.HideCountdown(playerIndex);
        }

        // Stop any ongoing train animation
        gameView.StopTrainAnimation(playerIndex);

        if (playerIndex == 0) _p1Answered = false;
        else _p2Answered = false;

        _gameModel.ResetSelection(playerIndex);
        TrainPathPuzzle puzzle = _gameModel.GenerateNewPuzzle(playerIndex);

        gameView.DisplayPuzzle(playerIndex, puzzle);
        gameView.SetPlayerOptionsInteractable(playerIndex, true);
        gameView.HideFeedbackIcons();
        MusicManager.Instance.PlayQuestionSfx();
        StartQuestionTimeout(playerIndex);
    }

    private void OnBackClicked()
    {
        MusicManager.Instance.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }
}
