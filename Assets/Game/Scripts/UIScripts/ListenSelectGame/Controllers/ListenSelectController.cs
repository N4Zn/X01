using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ListenSelectState { Initialize, Tutorial, Playing, GameOver }

public class ListenSelectController : MonoBehaviour
{
    [SerializeField] private ListenSelectView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _fsm;
    private ListenSelectModel _model;
    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _audioReminderCoroutine;

    private float _feedbackDelay = 1.0f;
    private float _audioInterval = 4f;

    void Start()
    {
        _model = new ListenSelectModel();
        gameView.InitView();

        if (GameSessionManager.Instance != null)
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);

        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnRetryClicked += () => { _fsm.StateMachineChange(ListenSelectState.Initialize); };

        gameView.SetPlayerNames(
            GameSessionManager.Instance?.GetDisplayName1() ?? "Player 1",
            GameSessionManager.Instance?.GetDisplayName2() ?? "Player 2"
        );

        _fsm = gameObject.AddComponent<CustomFSMManager>();
        _fsm.Initialize(typeof(ListenSelectState), this.GetType(), false);
        _fsm.StateMachineChange(ListenSelectState.Initialize);
    }

    void Update()
    {
        if (GetState() == ListenSelectState.Playing)
        {
            _model.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_model.GameTimer);
            if (_model.GameTimer <= 0)
            {
                _model.GameTimer = 0;
                _fsm.StateMachineChange(ListenSelectState.GameOver);
            }
        }
    }

    private ListenSelectState GetState() => (ListenSelectState)_fsm.GetCurrentState();

    // ===== FSM =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        _model.ResetGame();
        gameView.HideGameOver();
        _fsm.StateMachineChange(ListenSelectState.Tutorial);
    }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += StartGame;
            tutorialPanel.ShowPlaceholder("HÆ°á»›ng dáº«n: Listen & Select\nNghe Ã¢m thanh phÃ¡t ra vÃ  chá»n Ä‘Ãºng hÃ nh tinh!");
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
        _fsm.StateMachineChange(ListenSelectState.Playing); // Chuyá»ƒn sang Playing trÆ°á»›c
        LoadNewRound();
        MusicManager.Instance.PlayGameplayMusic();
    }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        gameView.SetPlayerInteractable(0, false);
        gameView.SetPlayerInteractable(1, false);
        GameSessionManager.Instance?.RecordScores(_model.Player1Score, _model.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "ListenSelectGame";
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
        _audioReminderCoroutine = StartCoroutine(AudioReminderRoutine(_model.TargetLetter));
    }

    private IEnumerator AudioReminderRoutine(string letter)
    {
        while (GetState() == ListenSelectState.Playing)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Letters/" + letter);
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
        if (GetState() != ListenSelectState.Playing) return;

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

            // Hiá»‡n wrong icon trong 0.2s rá»“i táº¯t
            StartCoroutine(HideWrongFeedbackRoutine(playerIndex));
        }
    }

    private IEnumerator HideWrongFeedbackRoutine(int playerIndex)
    {
        yield return new WaitForSeconds(0.25f);
        gameView.HideFeedback(playerIndex);
    }

    private IEnumerator NextRoundRoutine()
    {
        yield return new WaitForSeconds(_feedbackDelay);
        if (GetState() != ListenSelectState.Playing) yield break;

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
