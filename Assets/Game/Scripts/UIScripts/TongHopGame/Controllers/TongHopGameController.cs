using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MasterData;

/// <summary>
/// Controller for the TongHop game scene.
/// Split-screen 2-player math game (1024x600).
/// Cloned from AddNumber: equation A + B = C with hidden operand, pick 1 of 4 answers.
/// </summary>
public class TongHopGameController : MonoBehaviour
{
    [SerializeField] private TongHopGameView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;
    [SerializeField] private UnityEngine.Video.VideoClip tutorialClip;
    [SerializeField] private bool forceResolution1024x600 = true;

    private CustomFSMManager _customFSMManager;
    private TongHopGameModel _gameModel;

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
        // [CSV-SKIP] game is fully procedural — AddUpMaster not used by model.
        // To revert: remove the yield return null below and uncomment the block.
        yield return null;
        // bool isLoaded = false;
        // int retryCount = 0;
        // const int maxRetries = 10;
        // while (!isLoaded && retryCount < maxRetries)
        // {
        //     bool checkResult = CheckAndLoadAddUpMaster();
        //     if (checkResult) { isLoaded = true; }
        //     else { Debug.LogWarning($"NDL: AddUpMaster not loaded yet, retrying... ({retryCount + 1}/{maxRetries})"); yield return new WaitForSeconds(0.5f); retryCount++; }
        // }
        // if (!isLoaded) { Debug.LogError("NDL: Failed to load AddUpMaster after retries. Returning to MenuScene."); SceneManager.LoadScene("MenuScene"); yield break; }

        _gameModel = new TongHopGameModel();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;
        if (GameSettings.Instance != null) _feedbackDelay = GameSettings.Instance.RoundEndDelay;

        gameView.InitView();

        if (GameSessionManager.Instance != null)
        {
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);
        }

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
        if (GetCurrentState() == TongHopSceneState.Playing)
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
        _customFSMManager.Initialize(typeof(TongHopSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(TongHopSceneState.Initialize);
    }

    public TongHopSceneState GetCurrentState()
    {
        if (_customFSMManager == null) return TongHopSceneState.Initialize;
        Enum state = _customFSMManager.GetCurrentState();
        if (state == null) return TongHopSceneState.Initialize;
        return (TongHopSceneState)state;
    }

    // ===== State Machine Handlers =====

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_Initialize");
        _customFSMManager.StateMachineChange(TongHopSceneState.Tutorial);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_Tutorial");
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStartGame;
            if (tutorialClip != null)
                tutorialPanel.Show(tutorialClip, "Huong dan: TongHop");
            else
                tutorialPanel.ShowPlaceholder("Huong dan: TongHop");
        }
        else
        {
            StartNewGame();
        }
    }

    protected void StateMachineExit_Tutorial(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_Tutorial");
        if (tutorialPanel != null)
            tutorialPanel.OnStartGame -= OnTutorialStartGame;
    }

    private void OnTutorialStartGame()
    {
        StartNewGame();
    }

    protected void StateMachineEnter_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_Playing");
        MusicManager.Instance?.PlayGameplayMusic();
    }

    protected void StateMachineExit_Playing(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_Playing");
    }

    protected void StateMachineEnter_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_Paused");
        Time.timeScale = 0;
    }

    protected void StateMachineExit_Paused(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_Paused");
        Time.timeScale = 1;
    }

    protected void StateMachineEnter_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_RoundResult");
    }

    protected void StateMachineExit_RoundResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_RoundResult");
    }

    protected void StateMachineEnter_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_GameOver");
        gameView.SetPlayerAnswersInteractable(0, false);
        gameView.SetPlayerAnswersInteractable(1, false);
        MusicManager.Instance?.PlayMainMusic();

        GameSessionManager.Instance.RecordScores(_gameModel.Player1Score, _gameModel.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "TongHopGame";
        SceneManager.LoadScene("ScoreScene");
    }

    protected void StateMachineExit_GameOver(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_GameOver");
    }

    protected void StateMachineEnter_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineEnter_GameResult");
    }

    protected void StateMachineExit_GameResult(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: TongHop - StateMachineExit_GameResult");
    }

    // ===== Game Logic =====

    private void StartNewGame()
    {
        _gameModel.ResetGame();
        gameView.HideGameOverPanel();
        gameView.SetQuestionText("Fill in the missing number!");
        gameView.UpdateScores(0, 0);

        LoadNewRound();
        _customFSMManager.StateMachineChange(TongHopSceneState.Playing);
    }

    private void LoadNewRound()
    {
        _gameModel.CurrentRound++;
        _p1Answered = false;
        _p2Answered = false;

        LoadQuestionForPlayer(0);
        LoadQuestionForPlayer(1);

        gameView.HideFeedbackIcons();
        MusicManager.Instance?.PlayQuestionSfx();
    }

    private void LoadQuestionForPlayer(int playerIndex)
    {
        AddUpMaster.Param q = _gameModel.GetNextQuestion(playerIndex);
        if (q == null) return;

        int a = _gameModel.ValA[playerIndex];
        int b = _gameModel.ValB[playerIndex];
        int c = _gameModel.ValC[playerIndex];
        string hidden = _gameModel.HiddenPos[playerIndex];

        gameView.DisplayEquation(playerIndex, a, b, c, hidden, q.image_type);

        int[] answers = new int[4];
        for (int i = 0; i < 4; i++) answers[i] = _gameModel.GetStoredAnswer(playerIndex, i);

        gameView.DisplayAnswers(playerIndex, answers, q.image_type);

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
        if (GetCurrentState() != TongHopSceneState.Playing) yield break;
        bool answered = playerIndex == 0 ? _p1Answered : _p2Answered;
        if (answered) yield break;

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
        MusicManager.Instance?.PlayWrongSfx();
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
            _customFSMManager.StateMachineChange(TongHopSceneState.GameOver);
        }
    }

    // ===== Event Handlers =====

    private void OnAnswerSelected(int playerIndex, int answerIndex)
    {
        if (GetCurrentState() != TongHopSceneState.Playing) return;

        // Check if this player already answered
        if (playerIndex == 0 && _p1Answered) return;
        if (playerIndex == 1 && _p2Answered) return;

        int result = _gameModel.ProcessAnswer(playerIndex, answerIndex);

        if (result == 3) // Cooldown or already answered
        {
            return;
        }

        AddUpMaster.Param q = playerIndex == 0 ? _gameModel.Player1Question : _gameModel.Player2Question;

        // Update visual selection for multiple choice
        if (_gameModel.IsMultiPick[playerIndex])
        {
            gameView.SetSelectedAnswerBorder(playerIndex, answerIndex, _gameModel.MultiPickSelectedIndices[playerIndex].Contains(answerIndex));
            gameView.UpdateHiddenValues(playerIndex, _gameModel.MultiPickSelectedIndices[playerIndex],
                                      _gameModel.GetStoredAnswers(playerIndex),
                                      _gameModel.HiddenPos[playerIndex],
                                      q.image_type);
        }
        else if (result == 0 || result == 2)
        {
            // For single pick, reveal the choice in the equation box upon selection
            gameView.UpdateHiddenValues(playerIndex, new List<int> { answerIndex },
                                      _gameModel.GetStoredAnswers(playerIndex),
                                      _gameModel.HiddenPos[playerIndex],
                                      q.image_type);
        }

        if (result == 0) // Wrong
        {
            CancelQuestionTimeout(playerIndex);
            if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;

            MusicManager.Instance?.PlayWrongSfx();

            if (_gameModel.IsMultiPick[playerIndex])
            {
                foreach (int idx in _gameModel.MultiPickSelectedIndices[playerIndex])
                    gameView.SetWrongAnswerBorder(playerIndex, idx);
            }
            else
            {
                gameView.SetWrongAnswerBorder(playerIndex, answerIndex);
            }

            gameView.ShowFeedback(playerIndex, false);
            gameView.SetPlayerAnswersInteractable(playerIndex, false);
            ScheduleNextQuestion(playerIndex);
        }
        else if (result == 1) // Partial Correct / Toggle
        {
            MusicManager.Instance?.PlayCorrectSfx(); // Selection sound
        }
        else if (result == 2) // Fully Correct
        {
            CancelQuestionTimeout(playerIndex);
            if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;

            MusicManager.Instance?.PlayCorrectSfx();
            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);
            gameView.UpdateStars(playerIndex, playerIndex == 0 ? _gameModel.Player1Score : _gameModel.Player2Score);

            if (_gameModel.IsMultiPick[playerIndex])
            {
                foreach (int idx in _gameModel.MultiPickSelectedIndices[playerIndex])
                    gameView.SetCorrectAnswerBorder(playerIndex, idx);
            }
            else
            {
                gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            }

            gameView.ShowFeedback(playerIndex, true);

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
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (GetCurrentState() != TongHopSceneState.Playing) yield break;

        gameView.HideFeedbackIcon(playerIndex);
        gameView.HideQuestion(playerIndex);

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (GetCurrentState() != TongHopSceneState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        if (playerIndex == 0) _p1Answered = false;
        else _p2Answered = false;

        gameView.HideFeedbackIcons();
        LoadQuestionForPlayer(playerIndex);
        MusicManager.Instance?.PlayQuestionSfx();
    }

    private void OnRetryClicked()
    {
        gameView.HideGameOverPanel();
        _customFSMManager.StateMachineChange(TongHopSceneState.Initialize);
    }

    private void OnBackClicked()
    {
        MusicManager.Instance?.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnHomeClicked()
    {
        MusicManager.Instance?.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnSettingClicked()
    {
        Debug.Log("NDL: TongHopGame - OnSettingClicked");
    }
}
