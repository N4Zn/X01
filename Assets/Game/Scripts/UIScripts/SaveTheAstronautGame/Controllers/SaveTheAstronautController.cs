using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller "Save The Astronaut" — giải cứu phi hành gia qua 12 chặng thiên văn (xem
/// SaveTheAstronautContent), 2 làn độc lập (SaveTheAstronautLane) cùng tốc độ cuộn (dùng chung 1
/// config). Game real-time liên tục, không có câu hỏi/CSV — không kế thừa MiniGameControllerBase
/// (xem MiniGameKit/README.md: game liên tục/phi lượt tự lo ScoreManager+GameHUD trực tiếp, giống
/// LaneDashGame/RiverCrossGame).
///
/// Chấm điểm: mỗi lần 1 đội đi hết 12 chặng (về tới Trái Đất) = +pointsPerLap điểm, KHÔNG giới hạn
/// số lần "chết" trong lúc chơi — chơi hết GameSettings.GameTime, đội nào về đích nhiều lần hơn thắng.
/// </summary>
public class SaveTheAstronautController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] SaveTheAstronautConfigData config = new();

    [Header("Làn (2 đội)")]
    [SerializeField] SaveTheAstronautLane laneLeft;
    [SerializeField] SaveTheAstronautLane laneRight;

    [Header("HUD")]
    [SerializeField] GameHUD gameHud;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI countdownText;
    [SerializeField] GameObject resultPanel;
    [SerializeField] TextMeshProUGUI resultText;

    ScoreManager _scoreManager;
    float _timeRemaining;
    bool _isPlaying;

    void Awake()
    {
        _scoreManager = new ScoreManager();
        laneLeft?.Init(config);
        laneRight?.Init(config);
        if (laneLeft != null) laneLeft.OnLapCompleted += () => HandleLap(Team.Left);
        if (laneRight != null) laneRight.OnLapCompleted += () => HandleLap(Team.Right);
    }

    void Start() => StartCoroutine(RunGame());

    void Update()
    {
        if (!_isPlaying) return;

        _timeRemaining -= Time.deltaTime;
        if (gameHud) gameHud.UpdateTimer(Mathf.Max(_timeRemaining, 0f));

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _isPlaying = false;
            StartCoroutine(TimeUp());
        }
    }

    IEnumerator RunGame()
    {
        if (resultPanel) resultPanel.SetActive(false);

        string leftName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Left";
        string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Right";
        if (gameHud) gameHud.Initialize(_scoreManager, leftName, rightName, config.targetScoreForHud);

        _timeRemaining = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 90f;

        yield return StartCoroutine(Countdown());

        _isPlaying = true;
        laneLeft?.SetRunning(true);
        laneRight?.SetRunning(true);
        MusicManager.Instance?.PlayGameplayMusic();
    }

    IEnumerator Countdown()
    {
        if (countdownText) countdownText.gameObject.SetActive(true);

        for (int i = config.countdownSeconds; i >= 1; i--)
        {
            if (countdownText) countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText)
        {
            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.5f);
            countdownText.gameObject.SetActive(false);
        }
    }

    void HandleLap(Team team) => _scoreManager.AddPoints(team, config.pointsPerLap);

    IEnumerator TimeUp()
    {
        laneLeft?.SetRunning(false);
        laneRight?.SetRunning(false);
        MusicManager.Instance?.StopMusic();

        if (resultPanel) resultPanel.SetActive(true);
        if (resultText)
            resultText.text = $"Hết giờ!\nTrái: {_scoreManager.ScoreLeft}   Phải: {_scoreManager.ScoreRight}";

        GameSessionManager.Instance?.RecordScores(_scoreManager.ScoreLeft, _scoreManager.ScoreRight);

        yield return new WaitForSeconds(config.resultDisplaySeconds);
        SceneManager.LoadScene("ScoreScene");
    }
}
