using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoDemState { Initialize, Tutorial, Playing, GameOver }

public class SoDemController : MonoBehaviour
{
    [SerializeField] private SoDemView gameView;
    [SerializeField] private TutorialPanel tutorialPanel;

    private CustomFSMManager _fsm;
    private SoDemModel _model;
    private Coroutine _p1FeedbackCoroutine;
    private Coroutine _p2FeedbackCoroutine;
    private Coroutine _audioReminderCoroutine;
    private Coroutine _p1WrongIconCoroutine;
    private Coroutine _p2WrongIconCoroutine;
    private bool      _p1WrongSfxLocked;
    private bool      _p2WrongSfxLocked;

    private float _feedbackDelay = 1.0f;
    private float _audioInterval = 4f;

    void Start()
    {
        _model = new SoDemModel();
        gameView.InitView();

        if (GameSettings.Instance != null)
            _feedbackDelay = GameSettings.Instance.RoundEndDelay;

        if (GameSessionManager.Instance != null)
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);

        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance?.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance?.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnRetryClicked += () => { _fsm.StateMachineChange(SoDemState.Initialize); };

        gameView.SetPlayerNames(
            GameSessionManager.Instance?.GetDisplayName1() ?? "Player 1",
            GameSessionManager.Instance?.GetDisplayName2() ?? "Player 2"
        );

        _fsm = gameObject.AddComponent<CustomFSMManager>();
        _fsm.Initialize(typeof(SoDemState), this.GetType(), false);
        _fsm.StateMachineChange(SoDemState.Initialize);
    }

    void Update()
    {
        if (GetState() == SoDemState.Playing)
        {
            _model.GameTimer -= Time.deltaTime;
            gameView.UpdateTimer(_model.GameTimer);
            if (_model.GameTimer <= 0)
            {
                _model.GameTimer = 0;
                _fsm.StateMachineChange(SoDemState.GameOver);
            }
        }
    }

    private SoDemState GetState() => (SoDemState)_fsm.GetCurrentState();

    // ===== FSM =====

    protected void StateMachineEnter_Initialize(Enum prev, Dictionary<string, object> opt)
    {
        _model.ResetGame();
        gameView.HideGameOver();
        _fsm.StateMachineChange(SoDemState.Tutorial);
    }

    protected void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opt)
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.OnStartGame += StartGame;
            tutorialPanel.ShowPlaceholder("HÆ°á»›ng dáº«n: Sá»‘ Äáº¿m\nNghe Ã¢m thanh phÃ¡t ra vÃ  chá»n Ä‘Ãºng hÃ nh tinh mang sá»‘ Ä‘Ã³!");
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
        _fsm.StateMachineChange(SoDemState.Playing);
        LoadNewRound();
        MusicManager.Instance?.PlayGameplayMusic();
    }

    protected void StateMachineEnter_GameOver(Enum prev, Dictionary<string, object> opt)
    {
        gameView.SetPlayerInteractable(0, false);
        gameView.SetPlayerInteractable(1, false);
        GameSessionManager.Instance?.RecordScores(_model.Player1Score, _model.Player2Score);
        GameSessionManager.Instance.LastPlayedGame = "SoDemGame";
        SceneManager.LoadScene("ScoreScene");
    }

    // ===== Logic =====

    private void LoadNewRound()
    {
        _p1WrongSfxLocked = false;
        _p2WrongSfxLocked = false;
        if (_p1WrongIconCoroutine != null) { StopCoroutine(_p1WrongIconCoroutine); _p1WrongIconCoroutine = null; }
        if (_p2WrongIconCoroutine != null) { StopCoroutine(_p2WrongIconCoroutine); _p2WrongIconCoroutine = null; }

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
        while (GetState() == SoDemState.Playing)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/SoDem/" + value);
            float clipLen = (clip != null) ? clip.length : 0f;

            if (clip != null)
            {
                MusicManager.Instance?.SetMusicVolumeMultiplier(0.5f);
                yield return new WaitForSeconds(0.5f);
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
                yield return new WaitForSeconds(clipLen);
                yield return new WaitForSeconds(0.5f);
                MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
            }

            float waitTime = Mathf.Max(0.1f, _audioInterval - clipLen - 1.0f); // Subtract the 1s padding (0.5 + 0.5)
            yield return new WaitForSeconds(waitTime);
        }
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
    }

    private void OnBoxTapped(int playerIndex, int boxIndex)
    {
        if (GetState() != SoDemState.Playing) return;

        bool correct = _model.CheckTap(playerIndex, boxIndex);
        if (correct)
        {
            // Stop reminder as soon as someone wins
            if (_audioReminderCoroutine != null) { StopCoroutine(_audioReminderCoroutine); _audioReminderCoroutine = null; }

            MusicManager.Instance?.PlayCorrectSfx();
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
            bool sfxLocked = playerIndex == 0 ? _p1WrongSfxLocked : _p2WrongSfxLocked;
            if (!sfxLocked)
            {
                MusicManager.Instance?.PlayWrongSfx();
                if (playerIndex == 0) _p1WrongSfxLocked = true;
                else                  _p2WrongSfxLocked = true;
            }
            gameView.SetBoxWrong(playerIndex, boxIndex);
            gameView.SetBoxInteractable(playerIndex, boxIndex, false);
            gameView.ShowFeedback(playerIndex, false);

            if (playerIndex == 0)
            {
                if (_p1WrongIconCoroutine != null) StopCoroutine(_p1WrongIconCoroutine);
                _p1WrongIconCoroutine = StartCoroutine(HideWrongFeedbackRoutine(playerIndex));
            }
            else
            {
                if (_p2WrongIconCoroutine != null) StopCoroutine(_p2WrongIconCoroutine);
                _p2WrongIconCoroutine = StartCoroutine(HideWrongFeedbackRoutine(playerIndex));
            }
        }
    }

    private IEnumerator HideWrongFeedbackRoutine(int playerIndex)
    {
        yield return new WaitForSeconds(1f);
        gameView.HideFeedback(playerIndex);
        if (playerIndex == 0) _p1WrongIconCoroutine = null;
        else                  _p2WrongIconCoroutine = null;
    }

    private IEnumerator NextRoundRoutine()
    {
        yield return new WaitForSeconds(1f); // feedback icon visible
        if (GetState() != SoDemState.Playing) yield break;

        gameView.HideFeedback();
        gameView.HideAllBoxes();

        int countSeconds = Mathf.Max(0, Mathf.RoundToInt(_feedbackDelay) - 1);
        for (int i = countSeconds; i >= 1; i--)
        {
            gameView.ShowCountdown(0, i);
            gameView.ShowCountdown(1, i);
            yield return new WaitForSeconds(1f);
            if (GetState() != SoDemState.Playing) yield break;
        }
        gameView.HideCountdown(0);
        gameView.HideCountdown(1);

        LoadNewRound();
    }
}
