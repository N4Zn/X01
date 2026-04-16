using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MasterData;

/// <summary>
/// Controller for the AddUp game scene.
/// Split-screen 2-player math game (1024x600).
/// New gameplay: equation A + B = C with hidden operand, pick 1 of 4 answers.
/// </summary>
public class AddUpGameController : MonoBehaviour
{
    [SerializeField] private AddUpGameView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;
    [SerializeField] private UnityEngine.Video.VideoClip tutorialClip;
    [SerializeField] private bool forceResolution1024x600 = true;

    private CustomFSMManager _customFSMManager;
    private AddUpGameModel _gameModel;

    // Coroutine reference for feedback delay
    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _p1TimeoutCoroutine;
    private Coroutine _p2TimeoutCoroutine;

    // Whether each player has answered current round
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

        StartCoroutine(InitializeGameWithMasterData());
    }

    private IEnumerator InitializeGameWithMasterData()
    {
        bool isLoaded = false;
        int retryCount = 0;
        const int maxRetries = 10;

        while (!isLoaded && retryCount < maxRetries)
        {
            bool checkResult = CheckAndLoadAddUpMaster();

            if (checkResult)
            {
                isLoaded = true;
            }
            else
            {
                Debug.LogWarning($"NDL: AddUpMaster not loaded yet, retrying... ({retryCount + 1}/{maxRetries})");
                yield return new WaitForSeconds(0.5f);
                retryCount++;
            }
        }

        if (!isLoaded)
        {
            Debug.LogError("NDL: Failed to load AddUpMaster after retries. Returning to MenuScene.");
            SceneManager.LoadScene("MenuScene");
            yield break;
        }

        _gameModel = new AddUpGameModel();
        _gameModel.LoadQuestions();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;

        gameView.InitView();
        gameView.OnAnswerSelected += OnAnswerSelected;
        gameView.OnRetryClicked += OnRetryClicked;
        gameView.OnBackClicked += OnBackClicked;
        gameView.OnHomeClicked += OnHomeClicked;
        gameView.OnSettingClicked += OnSettingClicked;

        InitFSMManager();
    }

    private bool CheckAndLoadAddUpMaster()
    {
        try
        {
            AddUpMaster master = MasterDataCache.GetCache<AddUpMaster>();
            if (master != null && master.param != null && master.param.Count > 0)
            {
                Debug.Log($"NDL: AddUpMaster loaded successfully with {master.param.Count} questions");
                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NDL: AddUpMaster not in cache: {ex.Message}");
        }

        try
        {
            if (LoadCsvDataForEditor.Instance == null)
            {
                GameObject loaderObj = new GameObject("LoadCsvDataForEditor");
                loaderObj.AddComponent<LoadCsvDataForEditor>();
            }
            LoadCsvDataForEditor.Instance.LoadMasterData("AddUpMaster");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NDL: Failed to load AddUpMaster: {ex.Message}");
        }

        return false;
    }

    void Update()
    {
        if (GetCurrentState() == AddUpSceneState.Playing)
        {
            UpdateTimer();
        }
    }

    void OnDestroy()
    {
        if (gameView != null)
        {
            gameView.OnAnswerSelected -= OnAnswerSelected;
            gameView.OnRetryClicked -= OnRetryClicked;
            gameView.OnBackClicked -= OnBackClicked;
        }
    }

    // ===== FSM Management =====

    private void InitFSMManager()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(AddUpSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(AddUpSceneState.Initialize);
    }

    public AddUpSceneState GetCurrentState()
    {
        if (_customFSMManager == null) return AddUpSceneState.Initialize;
        Enum state = _customFSMManager.GetCurrentState();
        if (state == null) return AddUpSceneState.Initialize;
        return (AddUpSceneState)state;
    }

    // ===== State Machine Handlers =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(AddUpSceneState.Tutorial);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_Tutorial");
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStartGame;
            if (tutorialClip != null)
                tutorialPanel.Show(tutorialClip, "Huong dan: AddUp");
            else
                tutorialPanel.ShowPlaceholder("Huong dan: AddUp");
        }
        else
        {
            StartNewGame();
        }
    }

    protected void StateMachineExit_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_Tutorial");
        if (tutorialPanel != null)
            tutorialPanel.OnStartGame -= OnTutorialStartGame;
    }

    private void OnTutorialStartGame()
    {
        StartNewGame();
    }

    protected void StateMachineEnter_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_Playing");
        MusicManager.Instance.PlayGameplayMusic();
    }

    protected void StateMachineExit_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_Playing");
    }

    protected void StateMachineEnter_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_Paused");
        Time.timeScale = 0;
    }

    protected void StateMachineExit_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_Paused");
        Time.timeScale = 1;
    }

    protected void StateMachineEnter_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_RoundResult");
    }

    protected void StateMachineExit_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_RoundResult");
    }

    protected void StateMachineEnter_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_GameOver");
        gameView.SetPlayerAnswersInteractable(0, false);
        gameView.SetPlayerAnswersInteractable(1, false);
        MusicManager.Instance.PlayMainMusic();

        GameSessionManager.Instance.RecordScores(_gameModel.Player1Score, _gameModel.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "AddUpGame";
        SceneManager.LoadScene("ScoreScene");
    }

    protected void StateMachineExit_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_GameOver");
    }

    protected void StateMachineEnter_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineEnter_GameResult");
    }

    protected void StateMachineExit_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: AddUp - StateMachineExit_GameResult");
    }

    // ===== Game Logic =====

    private void StartNewGame()
    {
        _gameModel.ResetGame();
        gameView.HideGameOverPanel();
        gameView.SetQuestionText("Fill in the missing number!");
        gameView.UpdateScores(0, 0);

        LoadNewRound();
        _customFSMManager.StateMachineChange(AddUpSceneState.Playing);
    }

    private void LoadNewRound()
    {
        _gameModel.CurrentRound++;
        _p1Answered = false;
        _p2Answered = false;

        LoadQuestionForPlayer(0);
        LoadQuestionForPlayer(1);

        gameView.HideFeedbackIcons();
        MusicManager.Instance.PlayQuestionSfx();
    }

    private void LoadQuestionForPlayer(int playerIndex)
    {
        AddUpMaster.Param q = _gameModel.GetNextQuestion(playerIndex);
        if (q == null) return;

        int level = _gameModel.PlayerLevel[playerIndex];

        if (_gameModel.IsLevel4[playerIndex])
        {
            // Level 4: show "? + ? = target"
            int target = _gameModel.Level4Target[playerIndex];
            gameView.DisplayEquationLevel4(playerIndex, target, q.image_type);
            gameView.DisplayAnswers(playerIndex, q.answer_1, q.answer_2, q.answer_3, q.image_type);
        }
        else
        {
            gameView.DisplayEquation(playerIndex, q.number_a, q.number_b, q.hidden_position, q.image_type);
            gameView.DisplayAnswers(playerIndex, q.answer_1, q.answer_2, q.answer_3, q.image_type);
        }
        gameView.SetPlayerAnswersInteractable(playerIndex, true);
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
        if (GetCurrentState() != AddUpSceneState.Playing) yield break;
        bool answered = playerIndex == 0 ? _p1Answered : _p2Answered;
        if (answered) yield break;

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
        MusicManager.Instance.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        gameView.SetPlayerAnswersInteractable(playerIndex, false);
        ScheduleNextQuestion(playerIndex);
    }

    private void UpdateTimer()
    {
        _gameModel.GameTimer -= Time.deltaTime;
        gameView.UpdateTimer(_gameModel.GameTimer);

        if (_gameModel.GameTimer <= 0)
        {
            _gameModel.GameTimer = 0;
            _customFSMManager.StateMachineChange(AddUpSceneState.GameOver);
        }
    }

    // ===== Event Handlers =====

    private void OnAnswerSelected(int playerIndex, int answerIndex)
    {
        if (GetCurrentState() != AddUpSceneState.Playing) return;

        // Check if this player already answered
        if (playerIndex == 0 && _p1Answered) return;
        if (playerIndex == 1 && _p2Answered) return;

        CancelQuestionTimeout(playerIndex);

        // Level 4: pick-2 mechanic
        if (_gameModel.IsLevel4[playerIndex])
        {
            HandleLevel4Tap(playerIndex, answerIndex);
            return;
        }

        bool isCorrect = _gameModel.CheckAnswer(playerIndex, answerIndex);

        // Mark as answered
        if (playerIndex == 0) _p1Answered = true;
        else _p2Answered = true;

        if (isCorrect)
        {
            MusicManager.Instance.PlayCorrectSfx();
            int difficulty = _gameModel.GetQuestionDifficulty(playerIndex);
            int stars = _gameModel.GetStarsForDifficulty(difficulty);

            if (playerIndex == 0)
                _gameModel.Player1Score += stars;
            else
                _gameModel.Player2Score += stars;

            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);
            gameView.UpdateStars(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, true);
        }
        else
        {
            MusicManager.Instance.PlayWrongSfx();
            gameView.SetWrongAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, false);
        }

        gameView.SetPlayerAnswersInteractable(playerIndex, false);
        ScheduleNextQuestion(playerIndex);
    }

    private void HandleLevel4Tap(int playerIndex, int answerIndex)
    {
        int result = _gameModel.CheckLevel4Tap(playerIndex, answerIndex);

        if (result == 1)
        {
            // First correct pick — highlight it, disable just this button (others stay clickable for second pick)
            MusicManager.Instance.PlayCorrectSfx();
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.SetAnswerButtonInteractable(playerIndex, answerIndex, false);
        }
        else if (result == 2)
        {
            // Second correct pick — round complete!
            MusicManager.Instance.PlayCorrectSfx();
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, true);

            int stars = _gameModel.GetStarsForDifficulty(4);
            if (playerIndex == 0) _gameModel.Player1Score += stars;
            else _gameModel.Player2Score += stars;
            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);
            gameView.UpdateStars(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);

            if (playerIndex == 0) _p1Answered = true;
            else _p2Answered = true;
            gameView.SetPlayerAnswersInteractable(playerIndex, false);
            ScheduleNextQuestion(playerIndex);
        }
        else
        {
            // Wrong pick
            MusicManager.Instance.PlayWrongSfx();
            gameView.SetWrongAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, false);

            if (playerIndex == 0) _p1Answered = true;
            else _p2Answered = true;
            gameView.SetPlayerAnswersInteractable(playerIndex, false);
            ScheduleNextQuestion(playerIndex);
        }
    }

    private void ScheduleNextQuestion(int playerIndex)
    {
        if (playerIndex == 0)
        {
            if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
            _p1FeedbackCoroutine = StartCoroutine(LoadNextQuestionForPlayer(playerIndex));
        }
        else
        {
            if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
            _p2FeedbackCoroutine = StartCoroutine(LoadNextQuestionForPlayer(playerIndex));
        }
    }

    private IEnumerator LoadNextQuestionForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(_feedbackDelay);

        if (GetCurrentState() != AddUpSceneState.Playing) yield break;

        // Team mode: 3-second countdown before next question
        if (GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            // Hide this player's feedback icon before showing countdown
            gameView.HideFeedbackIcon(playerIndex);

            for (int i = 3; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
                if (GetCurrentState() != AddUpSceneState.Playing) yield break;
            }
            gameView.HideCountdown(playerIndex);
        }

        if (playerIndex == 0) _p1Answered = false;
        else _p2Answered = false;

        gameView.HideFeedbackIcons();
        LoadQuestionForPlayer(playerIndex);
        MusicManager.Instance.PlayQuestionSfx();
    }

    private void OnRetryClicked()
    {
        gameView.HideGameOverPanel();
        _customFSMManager.StateMachineChange(AddUpSceneState.Initialize);
    }

    private void OnBackClicked()
    {
        MusicManager.Instance.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnHomeClicked()
    {
        MusicManager.Instance.PlayMainMusic();
        SceneManager.LoadScene("HomeScene");
    }

    private void OnSettingClicked()
    {
        Debug.Log("NDL: AddUpGame - OnSettingClicked");
    }
}
