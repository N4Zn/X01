using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NumbersState { Initialize, Tutorial, Playing, GameOver }

public class NumbersController : MonoBehaviour
{
    [SerializeField] private NumbersView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _fsm;
    private NumbersModel _model;
    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _audioReminderCoroutine;

    private float _feedbackDelay = 1.0f;
    private float _audioInterval = 4f;

    void Start()
    {
        _model = new NumbersModel();
        gameView.InitView();

        if (GameSessionManager.Instance != null)
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);

        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("HomeScene"); };
        gameView.OnRetryClicked += () => { _fsm.StateMachineChange(NumbersState.Initialize); };

        gameView.SetPlayerNames(
            GameSessionManager.Instance?.GetDisplayName1() ?? "Player 1",
            GameSessionManager.Instance?.GetDisplayName2() ?? "Player 2"
        );

        _fsm = gameObject.AddComponent<CustomFSMManager>();
        _fsm.Initialize(typeof(NumbersState), this.GetType(), false);
        _fsm.StateMachineChange(NumbersState.Initialize);
    }

    void Update()
    {
        if (GetState() == NumbersState.Playing)
        {
            _model.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_model.GameTimer);
            if (_model.GameTimer <= 0)
            {
                _model.GameTimer = 0;
                _fsm.StateMachineChange(NumbersState.GameOver);
            }
        }
    }

    private NumbersState GetState() => (NumbersState)_fsm.GetCurrentState();

    // ===== FSM =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        _model.ResetGame();
        gameView.HideGameOver();
        _fsm.StateMachineChange(NumbersState.Tutorial);
    }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += StartGame;
            tutorialPanel.ShowPlaceholder("Hướng dẫn: Numbers\nListen to the number and choose the correct planet!");
        }
        else StartGame();
    }

    protected void StateMachineExit_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null) tutorialPanel.OnStartGame -= StartGame;
    }

    private void StartGame()
    {
        gameView.UpdateScores(0, 0);
        _fsm.StateMachineChange(NumbersState.Playing);
        LoadNewRound();
        MusicManager.Instance.PlayGameplayMusic();
    }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        gameView.SetPlayerInteractable(0, false);
        gameView.SetPlayerInteractable(1, false);
        GameSessionManager.Instance?.RecordScores(_model.Player1Score, _model.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "NumbersGame";
        SceneManager.LoadScene("ScoreScene");
    }

    // ===== Logic =====

    private void LoadNewRound()
    {
        _model.GenerateRound();
        gameView.HideFeedback();

        gameView.ShowBoxes(0, _model.Values[0]);
        gameView.ShowBoxes(1, _model.Values[1]);

        gameView.SetPlayerInteractable(0, true);
        gameView.SetPlayerInteractable(1, true);

        // Start repeating audio reminder
        if (_audioReminderCoroutine != null) StopCoroutine(_audioReminderCoroutine);
        _audioReminderCoroutine = StartCoroutine(AudioReminderRoutine(_model.TargetValue));
    }

    private IEnumerator AudioReminderRoutine(string value)
    {
        while (GetState() == NumbersState.Playing)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Numbers/" + value);
            float clipLen = (clip != null) ? clip.length : 0f;

            if (clip != null)
            {
                MusicManager.Instance.SetMusicVolumeMultiplier(0.5f);
                yield return new WaitForSeconds(0.5f);
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
                yield return new WaitForSeconds(clipLen);
                yield return new WaitForSeconds(0.5f);
                MusicManager.Instance.SetMusicVolumeMultiplier(1f);
            }

            float waitTime = Mathf.Max(0.1f, _audioInterval - clipLen - 1.0f);
            yield return new WaitForSeconds(waitTime);
        }
        MusicManager.Instance.SetMusicVolumeMultiplier(1f);
    }

    private void OnBoxTapped(int playerIndex, int boxIndex)
    {
        if (GetState() != NumbersState.Playing) return;

        bool correct = _model.CheckTap(playerIndex, boxIndex);
        if (correct)
        {
            // Stop reminder as soon as someone wins
            if (_audioReminderCoroutine != null) { StopCoroutine(_audioReminderCoroutine); _audioReminderCoroutine = null; }

            MusicManager.Instance.PlayCorrectSfx();
            gameView.SetBoxCorrect(playerIndex, boxIndex);

            if (playerIndex == 0) _model.Player1Score++; else _model.Player2Score++;
            gameView.UpdateScores(_model.Player1Score, _model.Player2Score);
            gameView.ShowFeedback(playerIndex, true);

            // First player correct ends the round for both
            gameView.SetPlayerInteractable(0, false);
            gameView.SetPlayerInteractable(1, false);

            if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
            _p1FeedbackCoroutine = StartCoroutine(NextRoundRoutine());
        }
        else
        {
            MusicManager.Instance.PlayWrongSfx();
            gameView.SetBoxWrong(playerIndex, boxIndex);
            gameView.ShowFeedback(playerIndex, false);

            // Hiện wrong icon trong 1s rồi tắt
            StartCoroutine(HideWrongFeedbackRoutine(playerIndex));
        }
    }

    private IEnumerator HideWrongFeedbackRoutine(int playerIndex)
    {
        yield return new WaitForSeconds(1f);
        gameView.HideFeedback(playerIndex);
    }

    private IEnumerator NextRoundRoutine()
    {
        yield return new WaitForSeconds(_feedbackDelay);
        if (GetState() != NumbersState.Playing) yield break;

        if (GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            gameView.HideFeedback();
            gameView.HideAllBoxes();
            for (int i = 2; i >= 1; i--)
            {
                gameView.ShowCountdown(0, i);
                gameView.ShowCountdown(1, i);
                yield return new WaitForSeconds(1f);
            }
            gameView.HideCountdown(0);
            gameView.HideCountdown(1);
        }

        LoadNewRound();
    }
}
