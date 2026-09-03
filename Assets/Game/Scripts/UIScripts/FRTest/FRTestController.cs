using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// FRTest — mini-game kiểm tra tích hợp nhận diện khuôn mặt.
///
/// Flow mỗi round:
///   1. Question phase (5s): camera feed hiển thị, nhận diện TẮT (tiết kiệm CPU)
///   2. Countdown "Next in 3 2 1": nhận diện BẬT
///   3. Cuối countdown: đọc kết quả nhận diện, cập nhật tên người chơi
///   4. Lặp lại
///
/// Scene setup (tất cả đều wire qua Inspector):
///   - GameHUD prefab
///   - RawImage cho camera feed (full screen hoặc full area)
///   - TextMeshPro "countdownText" ở giữa
///   - Button leftGreenBtn, leftRedBtn (150×150)
///   - Button rightGreenBtn, rightRedBtn (150×150)
/// </summary>
public class FRTestController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("HUD")]
    [SerializeField] GameHUD hud;

    [Header("Camera")]
    [SerializeField] RawImage cameraImage;

    [Header("Countdown")]
    [SerializeField] TextMeshProUGUI countdownText;

    [Header("Buttons — Left side")]
    [SerializeField] Button leftGreenBtn;
    [SerializeField] Button leftRedBtn;

    [Header("Buttons — Right side")]
    [SerializeField] Button rightGreenBtn;
    [SerializeField] Button rightRedBtn;

    [Header("Timing")]
    [Tooltip("Seconds the simulated question lasts")]
    [SerializeField] int questionDurationSec = 5;
    [Tooltip("Countdown seconds before next question")]
    [SerializeField] int countdownSec = 3;

    // ── State ─────────────────────────────────────────────────────────────────

    string _leftName  = "Người chơi 1";
    string _rightName = "Người chơi 2";
    int    _scoreLeft, _scoreRight;
    int    _roundNum;
    bool   _running;
    float  _timeRemaining;
    bool   _logExported;

    FaceRecognitionPlugin _plugin;
    readonly List<FRRoundRecord> _rounds = new();
    float  _roundStartTime;
    string _leftPressedThisRound;
    string _rightPressedThisRound;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        _plugin = FaceRecognitionPlugin.Instance;
        _plugin?.SetBoundingBoxEnabled(true); // this scene shows the camera preview + boxes

        var session = GameSessionManager.Instance;
        if (session != null)
        {
            _leftName  = session.GetDisplayName1();
            _rightName = session.GetDisplayName2();
        }

        _timeRemaining = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;

        hud.Initialize(null, _leftName, _rightName);
        hud.HideScoreBars();

        leftGreenBtn .onClick.AddListener(() => OnButton(Team.Left,  correct: true));
        leftRedBtn   .onClick.AddListener(() => OnButton(Team.Left,  correct: false));
        rightGreenBtn.onClick.AddListener(() => OnButton(Team.Right, correct: true));
        rightRedBtn  .onClick.AddListener(() => OnButton(Team.Right, correct: false));

        SetCountdownVisible(false);
        SetButtonsInteractable(false);

        _running = true;
        StartCoroutine(GameLoop());
    }

    void Update()
    {
        // Camera feed
        if (_plugin != null && _plugin.CameraTexture != null)
        {
            cameraImage.texture = _plugin.CameraTexture;
            cameraImage.color   = Color.white;
        }

        // Total game timer countdown
        if (_running)
        {
            _timeRemaining -= Time.deltaTime;
            hud.UpdateTimer(_timeRemaining);
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _running = false;
                OnGameEnd();
            }
        }

        // Back button → MenuScene
        if (Input.GetKeyDown(KeyCode.Escape))
            SceneManager.LoadScene("MenuScene");
    }

    // ── Main loop ─────────────────────────────────────────────────────────────

    IEnumerator GameLoop()
    {
        // Round 1 previously had no dedicated recognition window — the first attempt only
        // fired near the end of round 1's own countdown, so round 1 mostly played out under
        // stale/default names. Give an explicit "Start in 3,2,1" before round 1 begins, with
        // recognition running throughout, so both players are already identified in time.
        yield return StartCoroutine(InitialStartCountdown());

        while (_running)
        {
            _roundNum++;
            _leftPressedThisRound  = null;
            _rightPressedThisRound = null;
            _roundStartTime        = Time.time;
            int roundNum            = _roundNum;

            // 1. Question phase — buttons active
            SetButtonsInteractable(true);
            SetCountdownVisible(false);

            float elapsed = 0f;
            while (elapsed < questionDurationSec && _running)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!_running) break;

            // 2. Countdown display. Recognition starts on the last second and then runs on
            // its own timeout (independent of the countdown/question-phase cadence) — real
            // recognition latency can exceed the visible countdown.
            SetButtonsInteractable(false);

            for (int i = countdownSec; i >= 1; i--)
            {
                ShowCountdown(i);
                if (i == 1)
                {
                    StartCoroutine(RecognitionRoutine(roundNum, _roundStartTime,
                        _leftPressedThisRound, _rightPressedThisRound));
                }
                yield return new WaitForSeconds(1f);
                if (!_running) yield break;
            }

            SetCountdownVisible(false);
        }
    }

    /// <summary>
    /// One-time "Start in 3,2,1" shown before round 1, with recognition running the whole time
    /// so both players are identified before gameplay begins instead of round 1 running blind.
    /// </summary>
    IEnumerator InitialStartCountdown()
    {
        SetButtonsInteractable(false);
        StartCoroutine(InitialRecognition());

        for (int i = 3; i >= 1; i--)
        {
            ShowCountdown(i, "Start");
            yield return new WaitForSeconds(1f);
            if (!_running) yield break;
        }
        SetCountdownVisible(false);
    }

    /// <summary>
    /// Same polling logic as RecognitionRoutine but for the pre-round-1 window only: no round
    /// record is logged, and on timeout the names already set from GameSessionManager (team/
    /// session names) are kept as-is rather than overwritten with the generic default names.
    /// </summary>
    IEnumerator InitialRecognition()
    {
        // If FRTest is the very first scene to ever touch FaceRecognitionPlugin.Instance this
        // session, Start() (which flips IsInitialized) may not have run yet even though the
        // singleton GameObject already exists — StartRound() would silently no-op and this
        // round's recognition would be lost with no retry. Wait briefly for it first.
        if (_plugin != null && !_plugin.IsInitialized)
        {
            float waitStart = Time.time;
            while (!_plugin.IsInitialized && Time.time - waitStart < 3f)
                yield return null;
        }

        _plugin?.StartRound();

        float timeout  = _plugin != null ? _plugin.GetTimeoutSec() : 4f;
        bool  leftDone  = false;
        bool  rightDone = false;
        float t         = 0f;

        while (t < timeout && !(leftDone && rightDone) && _running)
        {
            var (left, right) = _plugin != null ? _plugin.GetConfirmed() : (null, null);
            if (left != null && !leftDone)
            {
                _leftName = left;
                leftDone  = true;
                hud.UpdatePlayerNames(_leftName, _rightName);
            }
            if (right != null && !rightDone)
            {
                _rightName = right;
                rightDone  = true;
                hud.UpdatePlayerNames(_leftName, _rightName);
            }
            yield return null;
            t += Time.deltaTime;
        }

        _plugin?.StopRound();
    }

    // ── Recognition ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs independently of the round cadence: enables recognition, waits up to the
    /// configured timeout for each slot to be confidently recognized (single-shot — a
    /// slot locks and stops re-checking as soon as it matches), then falls back to the
    /// default name for any slot still unrecognized when time runs out.
    /// </summary>
    IEnumerator RecognitionRoutine(int roundNum, float roundStartTime, string leftPressed, string rightPressed)
    {
        _plugin?.StartRound();

        float timeout       = _plugin != null ? _plugin.GetTimeoutSec() : 4f;
        bool  leftDone      = false;
        bool  rightDone     = false;
        float leftElapsed   = -1f; // -1 = never recognized this round (timed out)
        float rightElapsed  = -1f;
        float startTime     = Time.time;
        float t             = 0f;

        Debug.Log($"[FRTest] Recognition START round={roundNum} timeout={timeout}s");

        while (t < timeout && !(leftDone && rightDone) && _running)
        {
            var (left, right) = _plugin != null ? _plugin.GetConfirmed() : (null, null);
            if (left != null && !leftDone)
            {
                _leftName   = left;
                leftDone    = true;
                leftElapsed = Time.time - startTime;
                hud.UpdatePlayerNames(_leftName, _rightName);
                Debug.Log($"[FRTest] Recognition DONE round={roundNum} slot=left name={left} elapsed={leftElapsed:F2}s");
            }
            if (right != null && !rightDone)
            {
                _rightName   = right;
                rightDone    = true;
                rightElapsed = Time.time - startTime;
                hud.UpdatePlayerNames(_leftName, _rightName);
                Debug.Log($"[FRTest] Recognition DONE round={roundNum} slot=right name={right} elapsed={rightElapsed:F2}s");
            }
            yield return null;
            t += Time.deltaTime;
        }

        if (!leftDone)
        {
            _leftName = _plugin != null ? _plugin.GetDefaultNameLeft() : "Player_1";
            Debug.Log($"[FRTest] Recognition TIMEOUT round={roundNum} slot=left → default {_leftName}");
        }
        if (!rightDone)
        {
            _rightName = _plugin != null ? _plugin.GetDefaultNameRight() : "Player_2";
            Debug.Log($"[FRTest] Recognition TIMEOUT round={roundNum} slot=right → default {_rightName}");
        }
        hud.UpdatePlayerNames(_leftName, _rightName);

        _plugin?.StopRound();

        _rounds.Add(new FRRoundRecord
        {
            round             = roundNum,
            recognizedLeft    = leftDone  ? _leftName  : "",
            recognizedRight   = rightDone ? _rightName : "",
            leftRecognizeSec  = leftDone  ? (float)Math.Round(leftElapsed, 2)  : -1f,
            rightRecognizeSec = rightDone ? (float)Math.Round(rightElapsed, 2) : -1f,
            leftPressed       = leftPressed  ?? "None",
            rightPressed      = rightPressed ?? "None",
            durationSec       = (float)Math.Round(Time.time - roundStartTime, 1),
        });
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    void OnButton(Team team, bool correct)
    {
        if (correct)
        {
            if (team == Team.Left)  _scoreLeft++;
            else                    _scoreRight++;
            hud.SetScore(_scoreLeft, _scoreRight);
        }

        string label = correct ? "Correct" : "Wrong";
        if (team == Team.Left  && _leftPressedThisRound  == null) _leftPressedThisRound  = label;
        if (team == Team.Right && _rightPressedThisRound == null) _rightPressedThisRound = label;
    }

    // ── Log ──────────────────────────────────────────────────────────────────

    void OnGameEnd()
    {
        _plugin?.StopRound();
        ExportLog();

        var session = GameSessionManager.Instance;
        if (session != null)
        {
            session.RecordScores(_scoreLeft, _scoreRight);
            session.LastPlayedGame = "FRTestGame";
        }
        SceneManager.LoadScene("ScoreScene");
    }

    void OnDestroy()
    {
        _running = false;
        _plugin?.StopRound();
        // FaceRecognitionPlugin outlives this scene (DontDestroyOnLoad) — without turning this
        // back off, every other (headless) game visited afterward would keep paying the
        // camera-preview pipeline's cost for a texture nobody displays there.
        _plugin?.SetBoundingBoxEnabled(false);
    }

    void ExportLog()
    {
        if (_logExported || _rounds.Count == 0) return;
        _logExported = true;
        var session = new FRTestSession
        {
            gameName    = "FRTest",
            date        = DateTime.Now.ToString("yyyy-MM-dd"),
            time        = DateTime.Now.ToString("HH:mm:ss"),
            scoreLeft   = _scoreLeft,
            scoreRight  = _scoreRight,
            totalRounds = _rounds.Count,
            rounds      = _rounds,
        };
        string json  = JsonUtility.ToJson(session, prettyPrint: true);
        string fname = $"FRTest_{DateTime.Now:yyyy-MM-dd_HHmmss}.json";
        WriteLog(Path.Combine(Application.persistentDataPath, fname), json);
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var dir      = activity.Call<AndroidJavaObject>("getExternalFilesDir", (AndroidJavaObject)null);
            if (dir != null)
            {
                string ext = Path.Combine(dir.Call<string>("getAbsolutePath"), "GameLogs");
                Directory.CreateDirectory(ext);
                WriteLog(Path.Combine(ext, fname), json);
            }
        }
        catch (Exception e) { Debug.LogWarning($"[FRTest] External log failed: {e.Message}"); }
#endif
    }

    static void WriteLog(string path, string json)
    {
        try   { File.WriteAllText(path, json); Debug.Log($"[FRTest] Log saved → {path}"); }
        catch (Exception e) { Debug.LogWarning($"[FRTest] Write failed: {e.Message}"); }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void ShowCountdown(int sec, string label = "Next")
    {
        if (!countdownText) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = $"{label} in {sec}s";
    }

    void SetCountdownVisible(bool visible)
    {
        if (countdownText) countdownText.gameObject.SetActive(visible);
    }

    void SetButtonsInteractable(bool on)
    {
        leftGreenBtn .interactable = on;
        leftRedBtn   .interactable = on;
        rightGreenBtn.interactable = on;
        rightRedBtn  .interactable = on;
    }

    // ── Serialisable log types ────────────────────────────────────────────────

    [Serializable]
    class FRTestSession
    {
        public string          gameName;
        public string          date;
        public string          time;
        public int             scoreLeft;
        public int             scoreRight;
        public int             totalRounds;
        public List<FRRoundRecord> rounds;
    }

    [Serializable]
    class FRRoundRecord
    {
        public int    round;
        public string recognizedLeft;
        public string recognizedRight;
        public float  leftRecognizeSec;  // time from recognition start to match; -1 = timed out
        public float  rightRecognizeSec; // time from recognition start to match; -1 = timed out
        public string leftPressed;
        public string rightPressed;
        public float  durationSec;
    }
}
