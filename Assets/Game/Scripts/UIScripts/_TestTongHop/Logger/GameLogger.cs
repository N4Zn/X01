using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Logger toàn diện cho TestTongHop.
///
/// Ba tầng dữ liệu:
///   Click  → mỗi lần player bấm (team, answerIndex, kết quả, timestamp ms)
///   Round  → kết quả từng vòng (ai thắng, đáp án đúng, thời gian, danh sách click)
///   Session→ tổng kết (tên, điểm, thời lượng, toàn bộ rounds)
///
/// Sử dụng từ display:
///   GameLogger.Current?.LogClick(team, answerIndex, result);
/// </summary>
public class GameLogger
{
    // ── Static current session ────────────────────────────────────────────────

    /// <summary>Logger đang active — null khi ngoài game.</summary>
    public static GameLogger Current { get; private set; }

    // ── Session fields ────────────────────────────────────────────────────────

    readonly string          _gameName;
    readonly string          _playerLeft;
    readonly string          _playerRight;
    readonly DateTime        _sessionStart;
    readonly List<RoundRecord> _rounds = new();
    RoundRecord _currentRound;

    // ── Constructor ───────────────────────────────────────────────────────────

    public GameLogger(string gameName,
                      string playerLeft  = "Player 1",
                      string playerRight = "Player 2")
    {
        _gameName     = gameName;
        _playerLeft   = playerLeft;
        _playerRight  = playerRight;
        _sessionStart = DateTime.Now;
        Current       = this;
    }

    // ── Round lifecycle ───────────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu round mới — gọi từ GameManager sau khi câu hỏi được chọn.
    /// </summary>
    public void BeginRound(int roundNumber, QuestionData q)
    {
        _currentRound = new RoundRecord
        {
            round                 = roundNumber,
            questionId            = q.id,
            topic                 = q.topic,
            questionType          = q.questionType.ToString(),
            answerMode            = q.answerMode.ToString(),
            playerLeftRecognized  = "",
            playerRightRecognized = "",
            clicks                = new List<ClickRecord>()
        };
    }

    /// <summary>
    /// Cập nhật người chơi được nhận diện qua camera cho round đang chạy.
    /// Gọi từ Controller sau BeginRound(), trong khoảng đếm ngược trước câu hỏi.
    /// null = không nhận diện được bên đó (giữ nguyên "").
    /// </summary>
    public void UpdateRoundPlayers(string left, string right)
    {
        if (_currentRound == null) return;
        if (left  != null) _currentRound.playerLeftRecognized  = left;
        if (right != null) _currentRound.playerRightRecognized = right;
    }

    /// <summary>
    /// Ghi nhận một click (Choose mode).
    /// Gọi từ FloatingDisplay / ButtonDisplay ngay sau RegisterClick().
    /// </summary>
    public void LogClick(Team team, int answerIndex, ClickResult result)
    {
        if (_currentRound == null) return;
        _currentRound.clicks.Add(new ClickRecord
        {
            time        = DateTime.Now.ToString("HH:mm:ss.fff"),
            playerName  = team == Team.Left ? _playerLeft : _playerRight,
            side        = team.ToString(),
            answerIndex = answerIndex,
            result      = result.ToString()
        });
    }

    /// <summary>
    /// Ghi nhận một lần ghép cặp (Matching mode).
    /// Gọi từ MatchingDisplay khi player hoàn thành một cặp.
    /// </summary>
    public void LogPair(Team team, int leftIndex, int rightIndex, bool correct)
    {
        if (_currentRound == null) return;
        _currentRound.clicks.Add(new ClickRecord
        {
            time        = DateTime.Now.ToString("HH:mm:ss.fff"),
            playerName  = team == Team.Left ? _playerLeft : _playerRight,
            side        = team.ToString(),
            answerIndex = leftIndex,          // left item
            result      = correct ? $"PairCorrect→{rightIndex}" : $"PairWrong→{rightIndex}"
        });
    }

    /// <summary>
    /// Kết thúc round — gọi từ GameManager.RecordAnswer().
    /// </summary>
    public void EndRound(bool isCorrect, Team winner, float responseTime, QuestionData q)
    {
        if (_currentRound == null) return;
        _currentRound.isCorrect           = isCorrect;
        _currentRound.winnerName          = isCorrect
            ? (winner == Team.Left ? _playerLeft : _playerRight)
            : "";
        _currentRound.responseTimeSeconds = (float)Math.Round(responseTime, 2);
        _currentRound.correctAnswers      = q.correctAnswers;
        _rounds.Add(_currentRound);
        _currentRound = null;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public void Export(ScoreManager score)
    {
        float duration = (float)Math.Round((DateTime.Now - _sessionStart).TotalSeconds, 1);

        var log = new SessionLog
        {
            gameName        = _gameName,
            playerLeft      = _playerLeft,
            playerRight     = _playerRight,
            sessionDate     = _sessionStart.ToString("yyyy-MM-dd"),
            sessionTime     = _sessionStart.ToString("HH:mm:ss"),
            durationSeconds = duration,
            scoreLeft       = score.ScoreLeft,
            scoreRight      = score.ScoreRight,
            totalRounds     = _rounds.Count,
            rounds          = _rounds
        };

        string json  = JsonUtility.ToJson(log, prettyPrint: true);
        string fname = $"{_gameName}_{_sessionStart:yyyy-MM-dd_HHmmss}.json";

        // 1. Internal storage — luôn ghi được
        WriteFile(Path.Combine(Application.persistentDataPath, fname), json);

        // 2. External storage — dễ truy cập qua USB / file manager
        string extDir = ExternalLogDir();
        if (extDir != null)
            WriteFile(Path.Combine(extDir, fname), json);

        if (Current == this) Current = null;
    }

    // ── Storage helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Thư mục external tương ứng với từng platform:
    ///   Android  → /sdcard/Android/data/&lt;pkg&gt;/files/GameLogs/
    ///              (getExternalFilesDir — không cần quyền WRITE_EXTERNAL_STORAGE trên API ≥ 19)
    ///   Editor   → &lt;project root&gt;/GameLogs/  (dễ mở trong Explorer)
    ///   Khác     → null (bỏ qua)
    /// </summary>
    static string ExternalLogDir()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            // getExternalFilesDir(null) → /sdcard/Android/data/<pkg>/files/
            using var dir      = activity.Call<AndroidJavaObject>("getExternalFilesDir",
                                                                   (AndroidJavaObject)null);
            if (dir == null) return null;
            string path = dir.Call<string>("getAbsolutePath");
            return EnsureDir(Path.Combine(path, "GameLogs"));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] External path unavailable: {e.Message}");
            return null;
        }
#elif UNITY_EDITOR
        // Project root (thư mục chứa Assets/) → dễ tìm trong Explorer
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return EnsureDir(Path.Combine(root, "GameLogs"));
#else
        return null;
#endif
    }

    static string EnsureDir(string dir)
    {
        try   { Directory.CreateDirectory(dir); return dir; }
        catch { return null; }
    }

    static void WriteFile(string path, string json)
    {
        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"[GameLogger] Saved → {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] Write failed ({path}): {e.Message}");
        }
    }

    // ── Private serialisable models ───────────────────────────────────────────

    [Serializable]
    class SessionLog
    {
        public string gameName;
        public string playerLeft;
        public string playerRight;
        public string sessionDate;
        public string sessionTime;
        public float  durationSeconds;
        public int    scoreLeft;
        public int    scoreRight;
        public int    totalRounds;
        public List<RoundRecord> rounds;
    }

    [Serializable]
    class RoundRecord
    {
        public int    round;
        public string questionId;
        public string topic;
        public string questionType;          // "Choose" / "Matching"
        public string answerMode;            // "Single" / "MultiSelect" / "OrderedSequence"
        public string playerLeftRecognized;  // tên nhận diện qua camera — "" nếu chưa nhận diện
        public string playerRightRecognized;
        public bool   isCorrect;
        public string winnerName;            // "" = cả 2 sai
        public float  responseTimeSeconds;
        public int[]  correctAnswers;
        public List<ClickRecord> clicks;
    }

    [Serializable]
    class ClickRecord
    {
        public string time;         // "HH:mm:ss.fff"
        public string playerName;   // tên thực: "Đội Xanh", "Alice", ...
        public string side;         // "Left" / "Right"
        public int    answerIndex;
        public string result;       // "CorrectPartial", "WrongFinal", "PairCorrect→2", ...
    }
}
