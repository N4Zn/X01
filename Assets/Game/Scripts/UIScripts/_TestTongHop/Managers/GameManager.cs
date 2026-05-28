using UnityEngine;

/// <summary>
/// Pure model/service layer for TestTongHop.
/// Không tự chạy — mọi flow do TestTongHopController điều khiển qua FSM.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] QuestionPool questionPool;

    [Header("Settings")]
    [SerializeField] string gameName = "TestTongHop";
    // totalRounds → TongHopConfig.Current.totalRounds (gameconfig.json)

    ScoreManager _score;
    GameLogger   _logger;
    int          _roundsPlayed;

    // ── Public read-only state ────────────────────────────────────────────────

    public ScoreManager Score        => _score;
    public int          RoundsPlayed => _roundsPlayed;
    public int          TotalRounds  => TongHopConfig.Current.totalRounds;
    public bool         IsGameOver   => _roundsPlayed >= TongHopConfig.Current.totalRounds;

    public float GameTimer   { get; set; }
    public float MaxGameTime { get; private set; }
    public bool  IsTimeUp    => GameTimer <= 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Lấy tên hiển thị từ GameSessionManager (Team: tên đội, OneVsOne: tên cá nhân)
        string leftName  = "Player 1";
        string rightName = "Player 2";
        var session = GameSessionManager.Instance;
        if (session != null)
        {
            leftName  = session.GetDisplayName1();
            rightName = session.GetDisplayName2();
        }

        _score      = new ScoreManager();
        _logger     = new GameLogger(gameName, leftName, rightName);
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer   = MaxGameTime;
    }

    // ── Public API (called by TestTongHopController) ──────────────────────────

    /// <summary>Tăng round counter — AnswerDisplayManager tự lấy câu hỏi từ pool.</summary>
    public void IncrementRound() => _roundsPlayed++;

    /// <summary>
    /// Báo logger bắt đầu round mới với câu hỏi đã chọn.
    /// Gọi từ Controller sau khi AnswerDisplayManager.Show() đã lấy được câu hỏi.
    /// </summary>
    public void LogRoundStart(QuestionData q) => _logger.BeginRound(_roundsPlayed, q);

    /// <summary>Ghi nhận kết quả round và cập nhật điểm.</summary>
    public void RecordAnswer(QuestionData q, bool isCorrect, Team team, float responseTime)
    {
        _logger.EndRound(isCorrect, team, responseTime, q);
        if (isCorrect) _score.AddPoint(team);
    }

    /// <summary>Xuất log session ra file JSON.</summary>
    public void ExportLog() => _logger.Export(_score);

    /// <summary>Reset toàn bộ về trạng thái ban đầu (để Replay).</summary>
    public void ResetGame()
    {
        _roundsPlayed = 0;
        _score.Reset();
    }

    // ── Config helpers ────────────────────────────────────────────────────────

    public (int left, int right) GetScore() => (_score.ScoreLeft, _score.ScoreRight);
    public void SetDifficulty(int d)         => questionPool.SetDifficulty(d);
}
