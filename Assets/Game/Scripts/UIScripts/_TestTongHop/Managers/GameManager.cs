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
    [Tooltip("Fallback khi chưa chọn game qua ControlActivity (vd test trực tiếp trong Editor). Lúc chạy thật luôn ưu tiên GameSessionManager.SelectedGameName (tên cụ thể của minigame, vd Counting5/AddNumber5) — KHÔNG dùng tên scene chung, vì nhiều game share 1 scene TestTongHopGame.")]
    [SerializeField] string gameName = "TestTongHop";
    ScoreManager _score;
    GameLogger   _logger;
    int          _roundsPlayed;

    // ── Public read-only state ────────────────────────────────────────────────

    public ScoreManager Score        => _score;
    public int          RoundsPlayed => _roundsPlayed;

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

        // Tên game ghi vào log/Sheet PHẢI là tên cụ thể minigame (vd Counting5, AddNumber5) —
        // KHÔNG phải tên field cố định ở trên, vì nhiều game share chung scene TestTongHopGame,
        // chỉ khác CSV theo SelectedGameName (xem ControlBridge.cs). Field gameName giữ lại làm
        // fallback khi test trực tiếp trong Editor (không qua ControlActivity nên chưa có
        // SelectedGameName).
        string resolvedGameName = session != null && !string.IsNullOrEmpty(session.SelectedGameName)
            ? session.SelectedGameName
            : gameName;

        _score      = new ScoreManager();
        _logger     = new GameLogger(resolvedGameName, leftName, rightName);
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

    /// <summary>Số điểm cộng cho mỗi câu trả lời đúng. Mặc định 1; SolarQuiz set 10.</summary>
    public int PointsPerCorrect { get; set; } = 1;

    /// <summary>Ghi nhận kết quả round và cập nhật điểm.
    /// addScore=false khi điểm đã được cộng từng phần (MultiSelect PartialCorrect).</summary>
    public void RecordAnswer(QuestionData q, bool isCorrect, Team team, float responseTime, bool addScore = true)
    {
        _logger.EndRound(isCorrect, team, responseTime, q);
        if (isCorrect && addScore) _score.AddPoints(team, PointsPerCorrect);
    }

    /// <summary>
    /// Ghi người chơi được nhận diện qua camera cho round hiện tại.
    /// Gọi sau LogRoundStart(). null = không nhận diện được bên đó.
    /// </summary>
    public void LogRoundRecognizedPlayers(string left, string right)
        => _logger.UpdateRoundPlayers(left, right);

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
