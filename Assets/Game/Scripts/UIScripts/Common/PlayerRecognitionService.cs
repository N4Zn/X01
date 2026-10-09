using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Headless (no camera preview, no bounding-box UI) face-recognition hook shared across all
/// mini-games. Each slot (0=left, 1=right) is enabled/cleared independently via
/// FaceRecognitionPlugin's per-slot API, so independent per-player timers (e.g. a game with
/// separate left/right countdowns) can each request recognition without disturbing the other
/// slot's state or paying its align+embed+match cost.
///
/// Usage per game:
///   PlayerRecognitionService.Instance.BeginGameSession("AddNumberGame");  // once, at game start
///   PlayerRecognitionService.Instance.RecognizeSlot(playerIndex, name => { ... });  // at countdown
/// </summary>
public class PlayerRecognitionService : Singleton<PlayerRecognitionService>
{
    int _activeRequests;
    string _currentGameName;
    string _sessionFileName;
    readonly List<LogEntry> _logEntries = new();

    // Last recognition result per slot (0=left, 1=right) this session — LogRound() stamps each
    // round entry with whoever was most recently recognized for that slot, so a per-round result
    // never has to re-run recognition itself to know who answered.
    readonly string[] _lastName = new string[2];
    readonly bool[] _lastRecognized = new bool[2];

    // Mỗi RecognizeSlot() của 1 slot tăng _gen[slot]; request cũ thấy _gen đổi thì tự thoát (không ghi đè kết quả của
    // request mới). _slotActive đếm request còn chạy trên slot đó — chỉ tắt nhận diện native khi về 0, nếu không request
    // cũ kết thúc sẽ tắt nhầm slot mà request mới đang chờ. _requested: slot đã từng được yêu cầu nhận diện trong ván này.
    readonly int[] _gen = new int[2];
    readonly int[] _slotActive = new int[2];
    readonly bool[] _requested = new bool[2];

    // ── Chế độ 1 vs 1 (cá nhân): mỗi bên chỉ có 1 người chơi cả ván ─────────────────────────────
    // Mỗi lần nhận ra 1 người (1 request = 1 phiếu) thì cộng phiếu cho người đó; người đang hiển thị/ghi log (_owner) chỉ
    // đổi khi có người KHÁC có số phiếu NHIỀU HƠN hẳn. Không nhận ra ai → không cộng phiếu, giữ nguyên _owner.
    // Người nhiễu thoáng qua vì vậy không làm đổi tên. Chế độ đội không dùng 2 mảng này.
    readonly Dictionary<string, int>[] _votes = { new Dictionary<string, int>(), new Dictionary<string, int>() };
    readonly string[] _owner = new string[2];

    static bool Individual => GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentGameMode != GameMode.Team;

    /// <summary>Cộng phiếu (nếu nhận ra) rồi trả tên dùng cho slot: người đang giữ chỗ, hoặc <paramref name="fallback"/> khi chưa có ai.</summary>
    string ResolveOwner(int slot, string name, bool recognized, string fallback)
    {
        if (recognized && !string.IsNullOrEmpty(name))
        {
            var votes = _votes[slot];
            votes.TryGetValue(name, out int n);
            votes[name] = ++n;
            string owner = _owner[slot];
            if (owner == null || (name != owner && n > votes[owner])) _owner[slot] = name;
        }
        return _owner[slot] ?? fallback;
    }

    /// <summary>Sau khi hết timeout mà chưa nhận ra ai, vẫn tiếp tục dò thêm ngần này giây (người chơi bước vào muộn).</summary>
    const float LateWatchSec = 30f;

    /// <summary>Tên mặc định khi không nhận ra ai ở slot (cùng quy ước với nhánh timeout).</summary>
    string DefaultName(int slot)
    {
        var plugin = FaceRecognitionPlugin.Instance;
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentGameMode == GameMode.Team)
            return slot == 0 ? "Blue_1" : "Red_1";
        return slot == 0
            ? (plugin != null ? plugin.GetDefaultNameLeft()  : "Player_1")
            : (plugin != null ? plugin.GetDefaultNameRight() : "Player_2");
    }

    void SetSessionName(int slot, string name)
    {
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.Players.Count > slot)
            GameSessionManager.Instance.Players[slot].PlayerName = name;
    }

    // Scene đang chạy lúc BeginGameSession — GameControlBridge chỉ đẩy realtime dự phòng khi còn ở đúng scene này
    // (sang scene khác = ván cũ đã hết, không đẩy số liệu cũ đè lên ván mới).
    int _sessionSceneHandle = -1;

    /// <summary>true = đang trong ván (scene hiện tại chính là scene đã gọi BeginGameSession).</summary>
    public bool IsSessionSceneActive =>
        _sessionFileName != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle == _sessionSceneHandle;

    /// <summary>Call once when a mini-game session starts, before any RecognizeSlot calls.</summary>
    public void BeginGameSession(string gameName)
    {
        _sessionSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        _currentGameName = gameName;
        _logEntries.Clear();
        _lastName[0] = _lastName[1] = null;
        _lastRecognized[0] = _lastRecognized[1] = false;
        _requested[0] = _requested[1] = false;
        GameSessionManager.Instance?.ApplyPlayModeFromSettings();
        for (int s = 0; s < 2; s++) { _votes[s].Clear(); _owner[s] = null; }
        // Huỷ mọi request/dò muộn còn sót từ ván trước, và bỏ tên người chơi ván trước còn nằm trong
        // GameSessionManager (singleton sống qua các scene) — nếu không HUD/ControlActivity hiện tên cũ
        // cho tới khi nhận diện xong.
        _gen[0]++; _gen[1]++;
        for (int s = 0; s < 2; s++) SetSessionName(s, DefaultName(s));
        _sessionFileName = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{gameName}.json";
        // Mọi dòng Sheet sau đây mang sessionId = tên file log local → ghép 1-1 với file trên máy.
        SheetsSyncManager.CurrentSessionId = Path.GetFileNameWithoutExtension(_sessionFileName);
    }

    /// <summary>
    /// Recognizes one slot (0 = left, 1 = right). Updates GameSessionManager.Players[slot].PlayerName,
    /// appends a realtime log entry, then invokes onDone with the resolved name (never null — falls
    /// back to the configured default name on timeout). Safe to call for both slots on independent
    /// timers — recognition only actually stops once every in-flight request has finished.
    /// </summary>
    public void RecognizeSlot(int slot, Action<string> onDone = null)
    {
        StartCoroutine(RecognizeSlotRoutine(slot, onDone));
    }

    IEnumerator RecognizeSlotRoutine(int slot, Action<string> onDone)
    {
        var plugin = FaceRecognitionPlugin.Instance;

        // Singleton<T> can create the GameObject this frame, but Start() (which actually runs
        // UnityFaceBridge.initialize() and flips IsInitialized) isn't guaranteed to have run yet
        // — happens the first time ANY game touches .Instance before FRTest ever has. Calling
        // ClearRoundVotes()/SetSlotRecognitionEnabled() while still uninitialized silently
        // no-ops with no retry, permanently losing that round's recognition. Wait briefly for it
        // instead of assuming it's already ready.
        if (plugin != null && !plugin.IsInitialized)
        {
            float waitStart = Time.time;
            while (!plugin.IsInitialized && Time.time - waitStart < 3f)
                yield return null;
        }

        // First request in this window: a full clear (both slots) is safe since nobody else is
        // active — guarantees no stale lock from an earlier cycle can leak into either slot
        // later. A request joining an already-active window only clears its OWN slot, leaving
        // the other slot's in-flight/locked state untouched. Either way, only THIS slot gets
        // enabled — the other slot's align+embed+match cost is never paid unless something is
        // actually waiting on it too.
        if (_activeRequests == 0) plugin?.ClearRoundVotes();
        else                      plugin?.ClearSlotVote(slot);
        plugin?.SetSlotRecognitionEnabled(slot, true);
        _activeRequests++;
        _slotActive[slot]++;
        int myGen = ++_gen[slot];
        // Từ lúc này tới khi có kết quả, "ai đang đứng ở slot" là CHƯA BIẾT: không để LogRound() gán câu trả lời
        // cho người của round trước (trước đây câu trả lời đến trước kết quả nhận diện bị ghi nhầm sang bạn cũ).
        _requested[slot] = true;
        _lastName[slot] = null;
        _lastRecognized[slot] = false;

        float timeout   = plugin != null ? plugin.GetTimeoutSec() : 4f;
        float startTime  = Time.time;
        float t          = 0f;
        string result    = null;
        float confidence = -1f;

        while (t < timeout && _gen[slot] == myGen)
        {
            var (left, right, leftSim, rightSim) = plugin != null ? plugin.GetConfirmed() : (null, null, -1f, -1f);
            string candidate = slot == 0 ? left : right;
            if (candidate != null) { result = candidate; confidence = slot == 0 ? leftSim : rightSim; break; }
            yield return null;
            t += Time.deltaTime;
        }

        // _gen đổi = có RecognizeSlot() mới cho cùng slot (hoặc ván mới) thay thế request này → không ghi đè kết quả của nó.
        if (_gen[slot] == myGen)
        {
            bool recognized = result != null;
            if (!recognized) result = DefaultName(slot);
            ApplyRecognition(slot, result, recognized, Time.time - startTime, confidence, onDone);

            // Hết timeout mà chưa ai được nhận ra → KHÔNG tắt slot ngay: người chơi bước vào muộn vẫn được nhận,
            // tên cập nhật lại (HUD + ControlActivity + log) thay vì giữ tên mặc định cả round.
            if (!recognized)
            {
                float watched = 0f;
                while (watched < LateWatchSec && _gen[slot] == myGen)
                {
                    var (left, right, leftSim, rightSim) = plugin != null ? plugin.GetConfirmed() : (null, null, -1f, -1f);
                    string candidate = slot == 0 ? left : right;
                    if (candidate != null)
                    {
                        ApplyRecognition(slot, candidate, true, Time.time - startTime, slot == 0 ? leftSim : rightSim, onDone);
                        break;
                    }
                    yield return null;
                    watched += Time.deltaTime;
                }
            }
        }

        // Chỉ tắt slot khi không còn request nào khác đang chờ nó (request mới cùng slot có thể đã thay thế request này);
        // slot còn lại có request riêng vẫn chạy bình thường.
        _slotActive[slot]--;
        if (_slotActive[slot] <= 0) { _slotActive[slot] = 0; plugin?.SetSlotRecognitionEnabled(slot, false); }
        _activeRequests--;
        if (_activeRequests < 0) _activeRequests = 0;
    }

    void ApplyRecognition(int slot, string name, bool recognized, float elapsed, float confidence, Action<string> onDone)
    {
        // 1 vs 1: log "recognition" giữ tên nhận được thô; tên dùng cho HUD/ControlActivity/log round là người đang giữ chỗ.
        string detected = name;
        if (Individual) name = ResolveOwner(slot, name, recognized, name);
        SetSessionName(slot, name);
        _lastName[slot] = name;
        _lastRecognized[slot] = recognized;
        AppendRecognitionLog(slot, detected, recognized, elapsed, confidence, Individual ? name : "");
        onDone?.Invoke(name);
    }

    // ── Realtime log — rewritten to disk after every single event, not batched ─────────────────
    // Two event kinds share one file/list so each game ends up with ONE full log: "recognition"
    // entries (who was detected, how long it took) and "round" entries (question/answer/correct,
    // stamped with whoever was most recently recognized for that slot at LogRound() time).

    void AppendRecognitionLog(int slot, string name, bool recognized, float elapsedSec, float confidence, string owner = "")
    {
        float roundedElapsed = (float)Math.Round(elapsedSec, 2);
        // Làm tròn 3 chữ số (không phải 2 như thời gian) — cosine sim đủ nhạy để cần độ chính
        // xác cao hơn khi so sánh các lần nhận diện gần ngưỡng match.
        float roundedConfidence = confidence >= 0f ? (float)Math.Round(confidence, 3) : -1f;
        _logEntries.Add(new LogEntry
        {
            time                = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            game                = _currentGameName ?? "Unknown",
            slot                = slot == 0 ? "left" : "right",
            eventType           = "recognition",
            name                = name,
            owner               = owner,
            recognized          = recognized,
            recognizeElapsedSec = roundedElapsed,
            recognizeConfidence = roundedConfidence, // -1 = không nhận diện được (dùng tên mặc định)
            round               = -1,
            question            = "",
            answer              = "",
            correct             = false,
            answerTimeSec       = -1f,
        });
        WriteLogNow();

        // Track E: đồng bộ Google Sheet liên tục — log nhận diện khuôn mặt (ai, mất bao lâu),
        // cùng format với log local ở trên (round=-1 cố định để phân biệt với dòng "round").
        SheetsSyncManager.Enqueue(new Dictionary<string, object>
        {
            {"timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")},
            {"eventType", "recognition"},
            {"gameName", _currentGameName ?? "Unknown"},
            {"slot", slot == 0 ? "left" : "right"},
            {"round", -1},
            {"playerName", name},
            {"recognized", recognized},
            {"recognizeElapsedSec", roundedElapsed},
            {"recognizeConfidence", roundedConfidence},
        });
    }

    /// <summary>
    /// Call after a round/question is answered (correct or not) to log the full gameplay result —
    /// question text, given answer, correct/wrong, and time taken — merged with whoever was last
    /// recognized for that slot. Safe to call even if RecognizeSlot was never called for this slot
    /// (falls back to the current GameSessionManager display name).
    /// Trả về tên đã gán cho round (cùng tên ghi vào log local). pushSheets=false khi nơi gọi tự đẩy dòng
    /// round_end lên Sheet (MiniGameControllerBase, GameLogger) — mặc định true để game cũ chỉ gọi LogRound
    /// cũng có log online khớp log local.
    /// </summary>
    public string LogRound(int slot, int round, string question, string answer, bool correct, float answerTimeSec, string correctAnswer = "", string questionId = "", bool pushSheets = true)
    {
        string name = (slot == 0 || slot == 1) ? _lastName[slot] : null;
        // 1 vs 1: cả ván chỉ có 1 người mỗi bên → mọi round ghi tên người đang giữ chỗ, kể cả round không nhận ra ai.
        if (Individual && (slot == 0 || slot == 1) && _owner[slot] != null) name = _owner[slot];
        bool recognized = (slot == 0 || slot == 1) && _lastRecognized[slot];
        if (string.IsNullOrEmpty(name))
        {
            var session = GameSessionManager.Instance;
            // Slot đã yêu cầu nhận diện mà chưa có kết quả: tên trong session là của người round trước → dùng tên mặc định.
            bool pending = (slot == 0 || slot == 1) && _requested[slot];
            if (!pending && session != null && session.Players.Count > slot && !string.IsNullOrEmpty(session.Players[slot].PlayerName))
                name = session.Players[slot].PlayerName;
            else if (session != null && session.CurrentGameMode == GameMode.Team)
                name = slot == 0 ? "Blue_1" : "Red_1";
            else
                name = slot == 0 ? "Player_1" : "Player_2";
        }

        _logEntries.Add(new LogEntry
        {
            time                = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            game                = _currentGameName ?? "Unknown",
            slot                = slot == 0 ? "left" : "right",
            eventType           = "round",
            name                = name,
            recognized          = recognized,
            recognizeElapsedSec = -1f,
            recognizeConfidence = -1f,
            round               = round,
            questionId          = questionId ?? "",
            question            = question ?? "",
            answer              = answer ?? "",
            correctAnswer       = correctAnswer ?? "",
            correct             = correct,
            answerTimeSec       = (float)Math.Round(answerTimeSec, 2),
        });
        WriteLogNow();
        // Đẩy từng round sang ControlActivity (tính điểm 50 round gần nhất + lịch sử chi tiết từng câu).
        GameControlBridge.Instance?.PushRound(name, recognized, _currentGameName ?? "Unknown", slot == 0 ? "left" : "right",
            round, questionId ?? "", question ?? "", answer ?? "", correctAnswer ?? "", correct, (float)Math.Round(answerTimeSec, 2));

        // Log online khớp log local: dòng round_end đẩy từ ĐÚNG dữ liệu vừa ghi vào file (cùng tên, slot, round).
        if (pushSheets)
        {
            SheetsSyncManager.Enqueue(new Dictionary<string, object>
            {
                {"timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")},
                {"eventType", "round_end"},
                {"gameName", _currentGameName ?? "Unknown"},
                {"gameVariant", GameSessionManager.Instance != null ? GameSessionManager.Instance.SelectedGameName : ""},
                {"slot", slot},
                {"team", slot == 0 ? "Left" : "Right"},
                {"playerName", name},
                {"recognized", recognized},
                {"round", round},
                {"questionId", questionId ?? ""},
                {"question", question ?? ""},
                {"answer", answer ?? ""},
                {"correct", correct},
                {"responseTimeSec", Math.Round(answerTimeSec, 2)},
            });
        }
        return name;
    }

    /// <summary>Điểm/số liệu gộp theo TỪNG người chơi thật (tên nhận diện được) trong 1 bên
    /// (slot) — dùng cho bảng "điểm cá nhân trong đội" ở ScoreScene, khác với
    /// ScoreManager.ScoreLeft/Right vốn chỉ gộp theo BÊN (có thể nhiều bạn thay phiên nhau chơi
    /// cùng 1 bên qua các round khác nhau).</summary>
    [Serializable]
    public struct PlayerRoundStat
    {
        public string name;
        public int    answered;
        public int    correct;
        public float  totalAnswerTimeSec;
        public float  totalCorrectAnswerTimeSec;

        public float AvgAnswerTimeSec => answered > 0 ? totalAnswerTimeSec / answered : 0f;
        public float AvgCorrectAnswerTimeSec => correct > 0 ? totalCorrectAnswerTimeSec / correct : 0f;
    }

    /// <summary>Gộp mọi entry "round" của 1 slot (0=trái, 1=phải) theo tên người chơi thật, sort
    /// điểm (số câu đúng) giảm dần — khớp đúng cách ScoreManager.AddPoint() cộng điểm (1 điểm/
    /// câu đúng), nên correct count của từng người CỘNG LẠI đúng bằng tổng điểm của cả bên.</summary>
    public List<PlayerRoundStat> GetPlayerStats(int slot)
    {
        string slotTag = slot == 0 ? "left" : "right";
        var map = new Dictionary<string, PlayerRoundStat>();
        foreach (var e in _logEntries)
        {
            if (e.eventType != "round" || e.slot != slotTag) continue;
            string name = string.IsNullOrEmpty(e.name) ? "?" : e.name;
            if (!map.TryGetValue(name, out var stat)) stat = new PlayerRoundStat { name = name };
            float t = Mathf.Max(0f, e.answerTimeSec);
            stat.answered++;
            stat.totalAnswerTimeSec += t;
            if (e.correct) { stat.correct++; stat.totalCorrectAnswerTimeSec += t; }
            map[name] = stat;
        }
        // Người đã được NHẬN DIỆN ở bên này nhưng chưa có round nào (vừa vào chơi, hoặc chưa trả lời kịp/chưa đúng câu nào)
        // vẫn phải hiện tên (0/0) — trước đây chỉ liệt kê người có round nên panel live báo "chưa nhận diện được ai".
        foreach (var e in _logEntries)
        {
            if (e.eventType != "recognition" || e.slot != slotTag || !e.recognized || string.IsNullOrEmpty(e.name)) continue;
            if (Individual && e.name != _owner[slot]) continue; // 1 vs 1: người nhiễu không phải người chơi
            if (!map.ContainsKey(e.name)) map[e.name] = new PlayerRoundStat { name = e.name };
        }
        var list = new List<PlayerRoundStat>(map.Values);
        list.Sort((a, b) => b.correct != a.correct ? b.correct.CompareTo(a.correct) : b.answered.CompareTo(a.answered));
        return list;
    }

    void WriteLogNow()
    {
        if (string.IsNullOrEmpty(_sessionFileName)) return;
        string json = JsonUtility.ToJson(new LogWrapper { entries = _logEntries }, prettyPrint: true);

        // Lịch sử chơi lưu ở /sdcard/EduXplore/GameLogs (sống qua lần cài lại app).
        WriteFile(Path.Combine(SharedStorage.LogsDir, _sessionFileName), json);
    }

    static void WriteFile(string path, string json)
    {
        try   { File.WriteAllText(path, json); }
        catch (Exception e) { Debug.LogWarning($"[PlayerRecognitionService] Write failed ({path}): {e.Message}"); }
    }

    [Serializable] class LogWrapper { public List<LogEntry> entries; }

    [Serializable]
    class LogEntry
    {
        public string time;
        public string game;
        public string slot;                // "left" / "right"
        public string eventType;           // "recognition" or "round"
        public string name;
        public string owner;               // chỉ 1 vs 1: người đang giữ chỗ sau lần nhận diện này (rỗng ở chế độ đội)
        public bool   recognized;
        public float  recognizeElapsedSec; // -1 for "round" entries
        public float  recognizeConfidence; // cosine sim 0..1; -1 for "round" entries or no match
        public int    round;               // -1 for "recognition" entries
        public string questionId;          // id câu hỏi (rỗng nếu game không có QuestionData)
        public string question;
        public string answer;
        public string correctAnswer;       // đáp án đúng (rỗng nếu game chưa cung cấp)
        public bool   correct;
        public float  answerTimeSec;       // -1 for "recognition" entries
    }
}
