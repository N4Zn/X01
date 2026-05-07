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

    private float _feedbackDelay = 1.2f;

    void Start()
    {
        _model = new ListenSelectModel();
        gameView.InitView();

        if (GameSessionManager.Instance != null)
            gameView.SetMaxScore(GameSessionManager.Instance.TargetScore);

        gameView.OnBoxTapped += OnBoxTapped;
        gameView.OnBackClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("MenuScene"); };
        gameView.OnHomeClicked += () => { MusicManager.Instance.PlayMainMusic(); SceneManager.LoadScene("HomeScene"); };
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
            tutorialPanel.ShowPlaceholder("Hướng dẫn: Listen & Select\nNghe hoặc nhìn chữ cái yêu cầu và chọn đúng hành tinh!");
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
        LoadNewRound(0);
        LoadNewRound(1);
        _fsm.StateMachineChange(ListenSelectState.Playing);
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

    private void LoadNewRound(int playerIndex)
    {
        _model.GenerateRound(playerIndex);
        gameView.HideFeedback(playerIndex);
        gameView.SetTargetText(playerIndex, _model.TargetLetters[playerIndex]);
        gameView.ShowBoxes(playerIndex, _model.Values[playerIndex]);
        gameView.SetPlayerInteractable(playerIndex, true);

        // Play Audio for the letter
        PlayLetterAudio(_model.TargetLetters[playerIndex]);
    }

    private void PlayLetterAudio(string letter)
    {
        // Path: Assets/Game/Resources/Audio/Letters/A.mp3
        AudioClip clip = Resources.Load<AudioClip>("Audio/Letters/" + letter);
        if (clip != null)
        {
            // Assuming MusicManager has a generic PlaySfx(AudioClip) or we use a temporary source
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
        else
        {
            Debug.LogWarning("Missing audio for letter: " + letter);
        }
    }

    private void OnBoxTapped(int playerIndex, int boxIndex)
    {
        if (GetState() != ListenSelectState.Playing) return;

        bool correct = _model.CheckTap(playerIndex, boxIndex);
        if (correct)
        {
            MusicManager.Instance.PlayCorrectSfx();
            gameView.SetBoxCorrect(playerIndex, boxIndex);
            if (playerIndex == 0) _model.Player1Score++; else _model.Player2Score++;
            gameView.UpdateScores(_model.Player1Score, _model.Player2Score);
            gameView.ShowFeedback(playerIndex, true);

            if (playerIndex == 0)
            {
                if (_p1FeedbackCoroutine != null) StopCoroutine(_p1FeedbackCoroutine);
                _p1FeedbackCoroutine = StartCoroutine(NextRoundRoutine(playerIndex));
            }
            else
            {
                if (_p2FeedbackCoroutine != null) StopCoroutine(_p2FeedbackCoroutine);
                _p2FeedbackCoroutine = StartCoroutine(NextRoundRoutine(playerIndex));
            }
        }
        else
        {
            MusicManager.Instance.PlayWrongSfx();
            gameView.SetBoxWrong(playerIndex, boxIndex);
            gameView.ShowFeedback(playerIndex, false);
            // Optionally reset round or just wait
        }
    }

    private IEnumerator NextRoundRoutine(int playerIndex)
    {
        gameView.SetPlayerInteractable(playerIndex, false);
        yield return new WaitForSeconds(_feedbackDelay);
        if (GetState() != ListenSelectState.Playing) yield break;

        // Team mode countdown logic if needed
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
        {
            gameView.HideFeedback(playerIndex);
            gameView.HideAllBoxes(); // Simplified for now
            for (int i = 2; i >= 1; i--)
            {
                gameView.ShowCountdown(playerIndex, i);
                yield return new WaitForSeconds(1f);
            }
            gameView.HideCountdown(playerIndex);
        }

        LoadNewRound(playerIndex);
    }
}
