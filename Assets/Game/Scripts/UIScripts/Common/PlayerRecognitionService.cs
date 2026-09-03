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

    /// <summary>Call once when a mini-game session starts, before any RecognizeSlot calls.</summary>
    public void BeginGameSession(string gameName)
    {
        _currentGameName = gameName;
        _logEntries.Clear();
        _lastName[0] = _lastName[1] = null;
        _lastRecognized[0] = _lastRecognized[1] = false;
        _sessionFileName = $"GameLog_{gameName}_{DateTime.Now:yyyy-MM-dd_HHmmss}.json";
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

        float timeout   = plugin != null ? plugin.GetTimeoutSec() : 4f;
        float startTime  = Time.time;
        float t          = 0f;
        string result    = null;

        while (t < timeout)
        {
            var (left, right) = plugin != null ? plugin.GetConfirmed() : (null, null);
            string candidate = slot == 0 ? left : right;
            if (candidate != null) { result = candidate; break; }
            yield return null;
            t += Time.deltaTime;
        }

        bool recognized = result != null;
        if (!recognized)
        {
            result = slot == 0
                ? (plugin != null ? plugin.GetDefaultNameLeft()  : "Player_1")
                : (plugin != null ? plugin.GetDefaultNameRight() : "Player_2");
        }
        float elapsed = Time.time - startTime;

        if (GameSessionManager.Instance != null && GameSessionManager.Instance.Players.Count > slot)
            GameSessionManager.Instance.Players[slot].PlayerName = result;

        _lastName[slot] = result;
        _lastRecognized[slot] = recognized;

        AppendRecognitionLog(slot, result, recognized, elapsed);
        onDone?.Invoke(result);

        // Disable only this slot — the other slot (if it has its own request still running)
        // keeps working uninterrupted, and never had its cost paid by this one to begin with.
        plugin?.SetSlotRecognitionEnabled(slot, false);
        _activeRequests--;
        if (_activeRequests < 0) _activeRequests = 0;
    }

    // ── Realtime log — rewritten to disk after every single event, not batched ─────────────────
    // Two event kinds share one file/list so each game ends up with ONE full log: "recognition"
    // entries (who was detected, how long it took) and "round" entries (question/answer/correct,
    // stamped with whoever was most recently recognized for that slot at LogRound() time).

    void AppendRecognitionLog(int slot, string name, bool recognized, float elapsedSec)
    {
        _logEntries.Add(new LogEntry
        {
            time                = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            game                = _currentGameName ?? "Unknown",
            slot                = slot == 0 ? "left" : "right",
            eventType           = "recognition",
            name                = name,
            recognized          = recognized,
            recognizeElapsedSec = (float)Math.Round(elapsedSec, 2),
            round               = -1,
            question            = "",
            answer              = "",
            correct             = false,
            answerTimeSec       = -1f,
        });
        WriteLogNow();
    }

    /// <summary>
    /// Call after a round/question is answered (correct or not) to log the full gameplay result —
    /// question text, given answer, correct/wrong, and time taken — merged with whoever was last
    /// recognized for that slot. Safe to call even if RecognizeSlot was never called for this slot
    /// (falls back to the current GameSessionManager display name).
    /// </summary>
    public void LogRound(int slot, int round, string question, string answer, bool correct, float answerTimeSec)
    {
        string name = (slot == 0 || slot == 1) ? _lastName[slot] : null;
        bool recognized = (slot == 0 || slot == 1) && _lastRecognized[slot];
        if (string.IsNullOrEmpty(name))
        {
            name = GameSessionManager.Instance != null && GameSessionManager.Instance.Players.Count > slot
                ? GameSessionManager.Instance.Players[slot].PlayerName
                : (slot == 0 ? "Player_1" : "Player_2");
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
            round               = round,
            question            = question ?? "",
            answer              = answer ?? "",
            correct             = correct,
            answerTimeSec       = (float)Math.Round(answerTimeSec, 2),
        });
        WriteLogNow();
    }

    void WriteLogNow()
    {
        if (string.IsNullOrEmpty(_sessionFileName)) return;
        string json = JsonUtility.ToJson(new LogWrapper { entries = _logEntries }, prettyPrint: true);

        WriteFile(Path.Combine(Application.persistentDataPath, _sessionFileName), json);
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
                WriteFile(Path.Combine(ext, _sessionFileName), json);
            }
        }
        catch (Exception e) { Debug.LogWarning($"[PlayerRecognitionService] External log failed: {e.Message}"); }
#endif
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
        public bool   recognized;
        public float  recognizeElapsedSec; // -1 for "round" entries
        public int    round;               // -1 for "recognition" entries
        public string question;
        public string answer;
        public bool   correct;
        public float  answerTimeSec;       // -1 for "recognition" entries
    }
}
