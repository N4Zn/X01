using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Đồng bộ log chơi lên Google Drive, MỖI PHIÊN CHƠI MỘT FILE (thay cơ chế cũ "mỗi round một dòng Sheet").
///
/// Nguồn duy nhất = các file log local /sdcard/EduXplore/GameLogs/yyyy-MM-dd_HHmmss_Game.json
/// (PlayerRecognitionService ghi lại TOÀN BỘ file sau mỗi sự kiện; GameLogger.Export ghi file chi tiết click).
/// Định kỳ (intervalSeconds, tối thiểu 15s) quét thư mục, file nào mới/đã đổi so với lần gửi thành công trước thì
/// POST nguyên nội dung lên Apps Script (docs/apps-script/DriveUpload.gs) → lưu Drive
/// EduXplore_Logs/&lt;deviceId&gt;/&lt;ngày&gt;/&lt;tên file&gt;. Cùng tên = ghi đè, nên gửi lại bao nhiêu lần cũng không trùng.
///
/// Độ bền:
///  - Chỉ coi là xong khi script trả {"ok":true,"bytes":N} và N đúng bằng số byte đã gửi (HTTP 200 một mình không đủ).
///  - Trạng thái đã gửi lưu ở sheets_sync_state.tsv; mất file này chỉ khiến gửi lại (an toàn).
///  - Mỗi file lỗi tự lùi nhịp (30s, 60s, … tối đa 15 phút); lỗi liên tiếp quá MaxFails lần thì bỏ qua cho tới khi file đổi/khởi động lại,
///    không chặn các file khác.
///  - Dùng thời gian thực (không bị Time.timeScale=0 khi Stop/Pause đóng băng).
///  - Lần chạy đầu sau khi nâng cấp tự gửi bù toàn bộ GameLogs chưa gửi.
///
/// Config KHÔNG cần rebuild: /sdcard/EduXplore/sheets_sync_config.json
///   { "webAppUrl": "https://script.google.com/macros/s/XXX/exec", "enabled": true, "intervalSeconds": 30,
///     "deviceId": "K02-01", "secret": "" }
/// (tên class/file giữ nguyên "SheetsSyncManager" để khỏi đổi tham chiếu; đích giờ là Drive.)
/// </summary>
public class SheetsSyncManager : Singleton<SheetsSyncManager>
{
    [Serializable]
    private class SyncConfig
    {
        public string webAppUrl = "";
        public bool enabled = true;
        public float intervalSeconds = 30f;
        // Tên máy (vd "K02-01") = tên thư mục trên Drive. Rỗng = 8 ký tự mã thiết bị Android.
        public string deviceId = "";
        // Khớp Script property SECRET của Apps Script. Rỗng = script không đòi.
        public string secret = "";
    }

    [Serializable]
    private class Ack
    {
        public bool ok;
        public string error;
        public string path;
        public int bytes;
    }

    private struct Retry
    {
        public int fails;
        public float nextTry;   // Time.realtimeSinceStartup
        public string sig;
    }

    private const string ConfigFileName = "sheets_sync_config.json";
    private const string StateFileName = "sheets_sync_state.tsv";
    private const string LegacyPendingFileName = "sheets_pending.jsonl";

    private const float MinIntervalSec = 15f;
    private const float StartDelaySec = 10f;
    private const int PostTimeoutSec = 60;
    private const int MaxFilesPerCycle = 25;
    private const int MaxFails = 6;
    private const long MaxFileBytes = 10 * 1024 * 1024;
    // File vừa ghi trong N giây chưa gửi (tránh đọc giữa lúc đang ghi); lần quét sau gửi.
    private const double QuietSec = 3.0;

    // yyyy-MM-dd_HHmmss_<Game>.json — chỉ file phiên chơi, bỏ qua file lạ trong thư mục.
    private static readonly Regex SessionFileRx = new Regex(@"^\d{4}-\d{2}-\d{2}_\d{6}_.+\.json$", RegexOptions.Compiled);

    private SyncConfig _config = new SyncConfig();
    // tên file → chữ ký "độ dài|mtimeTicks" của bản đã gửi thành công.
    private readonly Dictionary<string, string> _synced = new Dictionary<string, string>();
    private readonly Dictionary<string, Retry> _retry = new Dictionary<string, Retry>();
    private bool _stateDirty;

    static string _deviceId;
    static string DeviceId
    {
        get
        {
            if (!string.IsNullOrEmpty(_deviceId)) return _deviceId;
            string id = SystemInfo.deviceUniqueIdentifier ?? "";
            _deviceId = id.Length > 8 ? id.Substring(0, 8) : (id.Length > 0 ? id : "unknown");
            return _deviceId;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "SheetsSyncManager";
        LoadConfig();
        LoadState();
        RetireLegacyQueue();
    }

    void Start()
    {
        StartCoroutine(SyncLoop());
    }

    private string ConfigFilePath => SharedStorage.PathFor(ConfigFileName);
    private static string StateFilePath => SharedStorage.PathFor(StateFileName);

    void LoadConfig()
    {
        string path = ConfigFilePath;
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonUtility.ToJson(_config, true));
                Debug.Log($"[SheetsSyncManager] Chưa có config, tạo mặc định tại {path} — điền webAppUrl rồi restart app.");
                return;
            }
            var cfg = JsonUtility.FromJson<SyncConfig>(File.ReadAllText(path));
            if (cfg != null) _config = cfg;
            if (!string.IsNullOrEmpty(_config.deviceId)) _deviceId = _config.deviceId;
            Debug.Log($"[SheetsSyncManager] LoadConfig: enabled={_config.enabled} interval={_config.intervalSeconds}s "
                + $"url={(string.IsNullOrEmpty(_config.webAppUrl) ? "(CHƯA SET)" : "đã set")} device={DeviceId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SheetsSyncManager] LoadConfig lỗi ({path}): {e.Message}");
        }
    }

    /// <summary>Đọc lại config từ đĩa không cần restart app — gọi sau khi sửa file JSON trên máy.</summary>
    public void ReloadConfig() => LoadConfig();

    // Hàng đợi từng dòng của cơ chế cũ: mọi dòng trong đó đều suy ra được từ GameLogs nên bỏ, không nạp lại.
    // Đổi tên (không xoá) để còn đối chiếu nếu cần.
    void RetireLegacyQueue()
    {
        try
        {
            string path = SharedStorage.PathFor(LegacyPendingFileName);
            if (!File.Exists(path)) return;
            string dst = path + ".legacy";
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(path, dst);
            Debug.Log($"[SheetsSyncManager] Bỏ hàng đợi dòng cũ → {dst}; dữ liệu được gửi lại theo từng file GameLogs.");
        }
        catch (Exception e) { Debug.LogWarning($"[SheetsSyncManager] Không đổi tên hàng đợi cũ: {e.Message}"); }
    }

    void LoadState()
    {
        try
        {
            string path = StateFilePath;
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab >= line.Length - 1) continue;
                _synced[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
        }
        catch (Exception e) { Debug.LogWarning($"[SheetsSyncManager] LoadState lỗi: {e.Message}"); }
    }

    void SaveState()
    {
        if (!_stateDirty) return;
        try
        {
            var sb = new StringBuilder();
            foreach (var kv in _synced) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            string path = StateFilePath;
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            _stateDirty = false;
        }
        catch (Exception e) { Debug.LogWarning($"[SheetsSyncManager] SaveState lỗi: {e.Message}"); }
    }

    IEnumerator SyncLoop()
    {
        yield return new WaitForSecondsRealtime(StartDelaySec);
        while (true)
        {
            if (_config.enabled && !string.IsNullOrEmpty(_config.webAppUrl))
                yield return SyncOnce();
            yield return new WaitForSecondsRealtime(Mathf.Max(MinIntervalSec, _config.intervalSeconds));
        }
    }

    IEnumerator SyncOnce()
    {
        List<FileInfo> todo = FindChanged();
        if (todo == null || todo.Count == 0) yield break;

        int sent = 0, ok = 0;
        foreach (var fi in todo)
        {
            if (sent >= MaxFilesPerCycle || !_config.enabled) break;
            sent++;
            bool success = false;
            yield return Upload(fi, r => success = r);
            if (success) ok++;
            yield return null;
        }
        SaveState();
        Debug.Log($"[SheetsSyncManager] Chu kỳ: gửi {ok}/{sent} file (còn {Mathf.Max(0, todo.Count - sent)} file chờ chu kỳ sau).");
    }

    // File phiên chơi mới hoặc đã đổi so với lần gửi thành công, cũ nhất trước (phiên đang chơi — mới nhất — gửi sau cùng).
    List<FileInfo> FindChanged()
    {
        var list = new List<FileInfo>();
        try
        {
            DateTime now = DateTime.UtcNow;
            float rt = Time.realtimeSinceStartup;
            foreach (string path in Directory.GetFiles(SharedStorage.LogsDir, "*.json"))
            {
                string name = Path.GetFileName(path);
                if (!SessionFileRx.IsMatch(name)) continue;
                var fi = new FileInfo(path);
                if (fi.Length == 0) continue;
                double age = (now - fi.LastWriteTimeUtc).TotalSeconds;
                if (age >= 0 && age < QuietSec) continue;
                string sig = Signature(fi);
                if (_synced.TryGetValue(name, out var done) && done == sig) continue;
                if (_retry.TryGetValue(name, out var r) && r.sig == sig && (r.fails >= MaxFails || rt < r.nextTry)) continue;
                list.Add(fi);
            }
            list.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SheetsSyncManager] Quét GameLogs lỗi: {e.Message}");
            return null;
        }
        return list;
    }

    static string Signature(FileInfo fi) => fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;

    string BuildUrl(string name)
    {
        var sb = new StringBuilder(_config.webAppUrl);
        sb.Append(_config.webAppUrl.Contains("?") ? '&' : '?');
        sb.Append("device=").Append(UnityWebRequest.EscapeURL(DeviceId));
        sb.Append("&name=").Append(UnityWebRequest.EscapeURL(name));
        if (!string.IsNullOrEmpty(_config.secret)) sb.Append("&key=").Append(UnityWebRequest.EscapeURL(_config.secret));
        return sb.ToString();
    }

    IEnumerator Upload(FileInfo fi, Action<bool> done)
    {
        string name = fi.Name;
        string sig = Signature(fi);   // chữ ký chụp TRƯỚC khi đọc: file đổi giữa chừng thì chu kỳ sau gửi lại

        byte[] body;
        try
        {
            if (fi.Length > MaxFileBytes) throw new IOException($"file quá lớn ({fi.Length} byte)");
            body = File.ReadAllBytes(fi.FullName);
            // Bỏ BOM nếu có: Apps Script không đưa BOM vào body → số byte ack sẽ lệch.
            if (body.Length >= 3 && body[0] == 0xEF && body[1] == 0xBB && body[2] == 0xBF)
            {
                var trimmed = new byte[body.Length - 3];
                Buffer.BlockCopy(body, 3, trimmed, 0, trimmed.Length);
                body = trimmed;
            }
            if (body.Length == 0) throw new IOException("file rỗng");
        }
        catch (Exception e)
        {
            body = null;
            Fail(name, sig, "đọc file: " + e.Message);
        }
        if (body == null) { done(false); yield break; }

        using (var req = new UnityWebRequest(BuildUrl(name), "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "text/plain; charset=utf-8");
            req.timeout = PostTimeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Fail(name, sig, req.error);
                done(false);
                yield break;
            }

            string txt = req.downloadHandler != null ? req.downloadHandler.text : "";
            Ack ack = null;
            try { ack = JsonUtility.FromJson<Ack>(txt); } catch { }

            if (ack == null || !ack.ok)
            {
                Fail(name, sig, "script không xác nhận: " + Trunc(ack != null && !string.IsNullOrEmpty(ack.error) ? ack.error : txt, 160));
                done(false);
                yield break;
            }
            if (ack.bytes != body.Length)
            {
                Fail(name, sig, $"lệch byte (gửi {body.Length}, script nhận {ack.bytes})");
                done(false);
                yield break;
            }

            _synced[name] = sig;
            _stateDirty = true;
            _retry.Remove(name);
            done(true);
        }
    }

    void Fail(string name, string sig, string why)
    {
        _retry.TryGetValue(name, out var r);
        r.fails = r.sig == sig ? r.fails + 1 : 1;   // file đã đổi → đếm lại từ đầu
        r.sig = sig;
        r.nextTry = Time.realtimeSinceStartup + Mathf.Min(30f * (1 << Mathf.Min(r.fails - 1, 5)), 900f);
        _retry[name] = r;
        Debug.LogWarning($"[SheetsSyncManager] Gửi {name} lỗi (lần {r.fails}/{MaxFails}): {why}");
    }

    static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));
}
