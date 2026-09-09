using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MasterData;

/// <summary>
/// Controller for NumberAddUp game â€” same logic as AddUp but displays numbers instead of items.
/// Reuses AddUpMaster CSV data.
/// </summary>
public class NumberAddUpGameController : MonoBehaviour
{
    [SerializeField] private NumberAddUpGameView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _customFSMManager;
    private AddUpGameModel _gameModel;

    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _p1TimeoutCoroutine;
    private Coroutine _p2TimeoutCoroutine;
    private bool _p1Answered = false;
    private bool _p2Answered = false;
    private float _feedbackDelay = 1.5f;
    private float _questionTimeout = 10f;
    private readonly float[] _questionShownTime = new float[2];
    private readonly int[] _roundIndex = new int[2];

    void Start()
    {
        // Ensure singletons exist for standalone testing
        if (GameSessionManager.Instance == null) new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
        if (GameSettings.Instance == null) new GameObject("GameSettings").AddComponent<GameSettings>();
        if (MusicManager.Instance == null) new GameObject("MusicManager").AddComponent<MusicManager>();

        if (GameSettings.Instance != null) _questionTimeout = GameSettings.Instance.QuestionTimeout;
        if (GameSettings.Instance != null) _feedbackDelay = GameSettings.Instance.RoundEndDelay;

        // Wire back button immediately so user can always exit
        if (gameView != null)
        {
            gameView.InitView();

            // NamNN change with Android Studio Agent: Set dynamic max score
            if (GameSessionManager.Instance != null)
            {
                gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);
            }

            gameView.OnBackClicked += OnBackClicked;
            gameView.OnHomeClicked += OnHomeClicked;
            gameView.OnSettingClicked += OnSettingClicked;
            gameView.OnRetryClicked += OnRetryClicked;
            gameView.OnAnswerSelected += OnAnswerSelected;
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
        //     try { AddUpMaster master = MasterDataCache.GetCache<AddUpMaster>(); if (master != null && master.param != null && master.param.Count > 0) { isLoaded = true; break; } } catch (Exception) { }
        //     try { if (LoadCsvDataForEditor.Instance == null) new GameObject("LoadCsvDataForEditor").AddComponent<LoadCsvDataForEditor>(); LoadCsvDataForEditor.Instance.LoadMasterData("AddUpMaster"); } catch (Exception ex) { Debug.LogWarning("NDL: NumberAddUp - CSV load attempt " + (retryCount + 1) + ": " + ex.Message); }
        //     yield return new WaitForSeconds(0.5f); retryCount++;
        // }
        // if (!isLoaded) { Debug.LogError("NDL: NumberAddUp - Failed to load data. Going back to MenuScene."); SceneManager.LoadScene("MenuScene"); yield break; }

        _gameModel = new AddUpGameModel();
        // [CSV-SKIP] _gameModel.LoadQuestions(); // data not used — model generates procedurally

        // Set player names from session
        var session = GameSessionManager.Instance;
        if (session != null)
        {
            gameView.SetPlayerNames(
                session.GetDisplayName1() ?? "Player 1",
                session.GetDisplayName2() ?? "Player 2"
            );
        }

        InitFSMManager();
    }

    private void InitFSMManager()
    {
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = "NumberAddUpFSM";
        _customFSMManager.Initialize(typeof(NumberAddUpSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(NumberAddUpSceneState.Initialize);
    }

    void Update()
    {
        if (GetCurrentState() == NumberAddUpSceneState.Playing)
        {
            _gameModel.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_gameModel.GameTimer);
            if (_gameModel.GameTimer <= 0)
            {
                _gameModel.GameTimer = 0;
                _customFSMManager.StateMachineChange(NumberAddUpSceneState.GameOver);
            }
        }
    }

    private NumberAddUpSceneState GetCurrentState()
    {
        if (_customFSMManager == null) return NumberAddUpSceneState.Initialize;
        Enum state = _customFSMManager.GetCurrentState();
        return state != null ? (NumberAddUpSceneState)state : NumberAddUpSceneState.Initialize;
    }

    // ===== FSM States =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        Debug.Log("NDL: NumberAddUp - Initialize");
        _customFSMManager.StateMachineChange(NumberAddUpSceneState.Tutorial);
    }
    protected void StateMachineExit_Initialize(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        Debug.Log("NDL: NumberAddUp - Tutorial");
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += OnTutorialStart;
            tutorialPanel.ShowPlaceholder("Huong dan: NumberAddUp");
        }
        else
        {
            StartNewGame();
        }
    }
    protected void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= OnTutorialStart;
    }
    private void OnTutorialStart() { StartNewGame(); }

    protected void StateMachineEnter_Playing(Enum prev, Dictionary<string, object> opt)
    {
        Debug.Log("NDL: NumberAddUp - Playing");
        MusicManager.Instance?.PlayGameplayMusic();
    }
    protected void StateMachineExit_Playing(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_Paused(Enum prev, Dictionary<string, object> opt) { Time.timeScale = 0; }
    protected void StateMachineExit_Paused(Enum prev, Dictionary<string, object> opt) { Time.timeScale = 1; }
    protected void StateMachineEnter_RoundResult(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_RoundResult(Enum prev, Dictionary<string, object> opt) { }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        Debug.Log("NDL: NumberAddUp - GameOver");
        gameView.SetPlayerAnswersInteractable(0, false);
        gameView.SetPlayerAnswersInteractable(1, false);
        MusicManager.Instance?.PlayMainMusic();
        GameSessionManager.Instance.RecordScores(_gameModel.Player1Score, _gameModel.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "NumberAddUpGame";
        SceneManager.LoadScene("ScoreScene");
    }
    protected void StateMachineExit_GameOver(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineEnter_GameResult(Enum prev, Dictionary<string, object> opt) { }
    protected void StateMachineExit_GameResult(Enum prev, Dictionary<string, object> opt) { }

    // ===== Game Logic =====

    private void StartNewGame()
    {
        if (_gameModel == null) return;

        _gameModel.ResetGame();
        gameView.HideGameOverPanel();
        gameView.SetQuestionText("Dien so con thieu!");
        gameView.UpdateScores(0, 0);
        PlayerRecognitionService.Instance.BeginGameSession(GameSessionManager.ResolveActiveGameName("NumberAddUpGame"));

        StartCoroutine(InitialStartCountdown());
    }

    /// <summary>"Start in 3,2,1" before the first question loads, recognizing both slots
    /// throughout so gameplay doesn't start under stale/default names.</summary>
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

        LoadNewRound();
        _customFSMManager.StateMachineChange(NumberAddUpSceneState.Playing);
    }

    private void RefreshPlayerNames()
    {
        gameView.SetPlayerNames(
            GameSessionManager.Instance.GetDisplayName1(),
            GameSessionManager.Instance.GetDisplayName2());
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

        if (_gameModel.IsLevel4[playerIndex])
        {
            int target = _gameModel.Level4Target[playerIndex];
            gameView.DisplayEquationLevel4(playerIndex, target);
            gameView.DisplayAnswers(playerIndex, q.answer_1, q.answer_2, q.answer_3);
        }
        else
        {
            gameView.DisplayEquation(playerIndex, q.number_a, q.number_b, q.hidden_position);
            gameView.DisplayAnswers(playerIndex, q.answer_1, q.answer_2, q.answer_3);
        }
        gameView.SetPlayerAnswersInteractable(playerIndex, true);
        _questionShownTime[playerIndex] = Time.time;
        _roundIndex[playerIndex]++;
        StartQuestionTimeout(playerIndex);
    }

    string DescribeQuestion(int playerIndex)
    {
        var q = playerIndex == 0 ? _gameModel.Player1Question : _gameModel.Player2Question;
        if (q == null) return "";
        if (_gameModel.IsLevel4[playerIndex])
            return $"? + ? = {_gameModel.Level4Target[playerIndex]}";
        return q.hidden_position == "a"
            ? $"? + {q.number_b} = {_gameModel.GetSum(q)}"
            : $"{q.number_a} + ? = {_gameModel.GetSum(q)}";
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
        if (_gameModel == null || GetCurrentState() != NumberAddUpSceneState.Playing) yield break;
        bool answered = playerIndex == 0 ? _p1Answered : _p2Answered;
        if (answered) yield break;

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
        MusicManager.Instance?.PlayWrongSfx();
        gameView.ShowFeedback(playerIndex, false);
        gameView.SetPlayerAnswersInteractable(playerIndex, false);
        PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], DescribeQuestion(playerIndex), "(timeout)", false, Time.time - _questionShownTime[playerIndex]);
        ScheduleNext(playerIndex);
    }

    // ===== Event Handlers =====

    private void OnAnswerSelected(int playerIndex, int answerIndex)
    {
        if (_gameModel == null) return;
        if (GetCurrentState() != NumberAddUpSceneState.Playing) return;
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

        var answeredQuestion = playerIndex == 0 ? _gameModel.Player1Question : _gameModel.Player2Question;
        PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], DescribeQuestion(playerIndex),
            _gameModel.GetAnswerValue(answeredQuestion, answerIndex).ToString(), isCorrect, Time.time - _questionShownTime[playerIndex]);

        if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;

        if (isCorrect)
        {
            MusicManager.Instance?.PlayCorrectSfx();
            int stars = _gameModel.GetStarsForDifficulty(_gameModel.GetQuestionDifficulty(playerIndex));
            if (playerIndex == 0) _gameModel.Player1Score += stars; else _gameModel.Player2Score += stars;
            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, true);
        }
        else
        {
            MusicManager.Instance?.PlayWrongSfx();
            gameView.SetWrongAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, false);
        }

        gameView.SetPlayerAnswersInteractable(playerIndex, false);
        ScheduleNext(playerIndex);
    }

    private void HandleLevel4Tap(int playerIndex, int answerIndex)
    {
        int result = _gameModel.CheckLevel4Tap(playerIndex, answerIndex);

        if (result == 1)
        {
            // First correct pick â€” disable just this button; others stay clickable for second pick
            MusicManager.Instance?.PlayCorrectSfx();
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.SetAnswerButtonInteractable(playerIndex, answerIndex, false);
        }
        else if (result == 2)
        {
            // Both correct â€” round complete
            MusicManager.Instance?.PlayCorrectSfx();
            gameView.SetCorrectAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, true);

            int stars = _gameModel.GetStarsForDifficulty(4);
            if (playerIndex == 0) _gameModel.Player1Score += stars; else _gameModel.Player2Score += stars;
            gameView.UpdateScores(_gameModel.Player1Score, _gameModel.Player2Score);

            var q2 = playerIndex == 0 ? _gameModel.Player1Question : _gameModel.Player2Question;
            string picks = $"{_gameModel.GetAnswerValue(q2, _gameModel.Level4FirstPick[playerIndex])},{_gameModel.GetAnswerValue(q2, answerIndex)}";
            PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], DescribeQuestion(playerIndex), picks, true, Time.time - _questionShownTime[playerIndex]);

            if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
            gameView.SetPlayerAnswersInteractable(playerIndex, false);
            ScheduleNext(playerIndex);
        }
        else
        {
            // Wrong
            MusicManager.Instance?.PlayWrongSfx();
            gameView.SetWrongAnswerBorder(playerIndex, answerIndex);
            gameView.ShowFeedback(playerIndex, false);

            var qWrong = playerIndex == 0 ? _gameModel.Player1Question : _gameModel.Player2Question;
            PlayerRecognitionService.Instance.LogRound(playerIndex, _roundIndex[playerIndex], DescribeQuestion(playerIndex),
                _gameModel.GetAnswerValue(qWrong, answerIndex).ToString(), false, Time.time - _questionShownTime[playerIndex]);

            if (playerIndex == 0) _p1Answered = true; else _p2Answered = true;
            gameView.SetPlayerAnswersInteractable(playerIndex, false);
            ScheduleNext(playerIndex);
        }
    }

    private void ScheduleNext(int playerIndex)
    {
        if (playerIndex == 0)
        {
            if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
            _p1FeedbackCoroutine = StartCoroutine(LoadNextForPlayer(playerIndex));
        }
        else
        {
            if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
            _p2FeedbackCoroutine = StartCoroutine(LoadNextForPlayer(playerIndex));
        }
    }

    private IEnumerator LoadNextForPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (_gameModel == null || GetCurrentState() != NumberAddUpSceneState.Playing) yield break;

        gameView.HideFeedbackIcon(playerIndex);
        gameView.HideQuestion(playerIndex);

        PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, _ => RefreshPlayerNames());

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(playerIndex, i);
            yield return new WaitForSeconds(1f);
            if (_gameModel == null || GetCurrentState() != NumberAddUpSceneState.Playing) yield break;
        }
        gameView.HideCountdown(playerIndex);

        if (playerIndex == 0) _p1Answered = false; else _p2Answered = false;

        gameView.HideFeedbackIcons();
        LoadQuestionForPlayer(playerIndex);
        MusicManager.Instance?.PlayQuestionSfx();
    }

    private void OnRetryClicked()
    {
        if (_customFSMManager != null)
        {
            gameView.HideGameOverPanel();
            _customFSMManager.StateMachineChange(NumberAddUpSceneState.Initialize);
        }
    }

    private void OnBackClicked()
    {
        Debug.Log("NDL: NumberAddUp - Back to MenuScene");
        MusicManager.Instance?.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnHomeClicked()
    {
        Debug.Log("NDL: NumberAddUp - Home");
        MusicManager.Instance?.PlayMainMusic();
        SceneManager.LoadScene("MenuScene");
    }

    private void OnSettingClicked()
    {
        Debug.Log("NDL: NumberAddUp - Setting");
    }
}
