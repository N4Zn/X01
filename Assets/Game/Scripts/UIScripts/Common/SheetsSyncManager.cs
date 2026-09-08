using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Đồng bộ liên tục (mặc định 3s/lần) dữ liệu chi tiết (từng câu, từng slot, thời gian trả
/// lời) lên Google Sheet qua 1 Google Apps Script Web App (doPost) — không cần OAuth/service
/// account phía app, vì Apps Script chạy với quyền của người deploy nó (chủ sheet).
///
/// Nguồn dữ liệu: bất kỳ đâu trong code gọi SheetsSyncManager.Enqueue(dict) — đã nối sẵn ở
/// MiniGameControllerBase.LogRoundResult() (mọi game MiniGameKit) và GameLogger (TestTongHop,
/// per-click). Hàng đợi im lặng gom lại, POST theo batch mỗi chu kỳ — mất mạng không mất dữ
/// liệu, batch gửi lỗi được đưa lại vào hàng đợi để thử lại chu kỳ sau.
///
/// Config KHÔNG cần rebuild — sửa trực tiếp trên máy:
///   Application.persistentDataPath/sheets_sync_config.json
///   { "webAppUrl": "https://script.google.com/macros/s/XXX/exec", "enabled": true, "intervalSeconds": 3 }
/// File tự sinh mặc định (enabled=true, webAppUrl rỗng) nếu chưa có — điền URL thật rồi
/// restart app (hoặc gọi ReloadConfig()).
/// </summary>
public class SheetsSyncManager : Singleton<SheetsSyncManager>
{
    [System.Serializable]
    private class SyncConfig
    {
        public string webAppUrl = "";
        public bool enabled = true;
        public float intervalSeconds = 3f;
    }

    private const string ConfigFileName = "sheets_sync_config.json";
    private SyncConfig _config = new SyncConfig();

    private static readonly List<string> PendingRowsJson = new List<string>();
    private static readonly object Lock = new object();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "SheetsSyncManager";
        LoadConfig();
    }

    void Start()
    {
        StartCoroutine(SyncLoop());
    }

    private string ConfigFilePath => Path.Combine(Application.persistentDataPath, ConfigFileName);

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
            _config = JsonUtility.FromJson<SyncConfig>(File.ReadAllText(path));
            Debug.Log($"[SheetsSyncManager] LoadConfig: enabled={_config.enabled} interval={_config.intervalSeconds}s "
                + $"url={(string.IsNullOrEmpty(_config.webAppUrl) ? "(CHƯA SET)" : "đã set")}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SheetsSyncManager] LoadConfig lỗi ({path}): {e.Message}");
        }
    }

    /// <summary>Đọc lại config từ đĩa không cần restart app — gọi sau khi sửa file JSON
    /// trên máy (vd điền webAppUrl lần đầu).</summary>
    public void ReloadConfig() => LoadConfig();

    /// <summary>Thêm 1 dòng dữ liệu (field → value) vào hàng đợi, gửi lên Sheet ở chu kỳ
    /// đồng bộ tiếp theo. An toàn gọi từ bất kỳ đâu (main thread — Unity API only main
    /// thread-safe, các call site hiện tại đều gọi từ Update()/game logic, không phải
    /// background thread).</summary>
    public static void Enqueue(Dictionary<string, object> row)
    {
        string json = EncodeJsonObject(row);
        lock (Lock) { PendingRowsJson.Add(json); }
    }

    IEnumerator SyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(1f, _config.intervalSeconds));
            if (!_config.enabled || string.IsNullOrEmpty(_config.webAppUrl)) continue;

            List<string> batch;
            lock (Lock)
            {
                if (PendingRowsJson.Count == 0) continue;
                batch = new List<string>(PendingRowsJson);
                PendingRowsJson.Clear();
            }

            string payload = "{\"rows\":[" + string.Join(",", batch) + "]}";
            yield return PostAsync(payload, batch);
        }
    }

    IEnumerator PostAsync(string payload, List<string> batchToRestoreOnFailure)
    {
        using (var req = new UnityWebRequest(_config.webAppUrl, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(payload);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = req.result != UnityWebRequest.Result.Success;
#else
            bool failed = req.isNetworkError || req.isHttpError;
#endif
            if (failed)
            {
                Debug.LogWarning($"[SheetsSyncManager] POST lỗi: {req.error} — đưa lại {batchToRestoreOnFailure.Count} dòng vào hàng đợi, thử lại chu kỳ sau");
                lock (Lock) { PendingRowsJson.InsertRange(0, batchToRestoreOnFailure); }
            }
            else
            {
                // Log luôn response body — HTTP 200 KHÔNG đảm bảo Apps Script thực sự ghi được
                // hàng vào Sheet (vd deploy /exec đang trỏ bản code cũ, hoặc lỗi bị nuốt trong
                // doPost). Body mong đợi: {"ok":true,"count":N} — nếu khác, xem ngay ở log này
                // thay vì phải suy đoán qua việc mở Sheet kiểm tra thủ công.
                string respBody = req.downloadHandler != null ? req.downloadHandler.text : "(no body)";
                Debug.Log($"[SheetsSyncManager] Đã gửi {batchToRestoreOnFailure.Count} dòng lên Sheet — response: {respBody}");
            }
        }
    }

    // JsonUtility không serialize được Dictionary<string,object> trực tiếp (cần kiểu cố định
    // lúc compile) — tự encode JSON tối giản, đủ cho string/number/bool, không cần thư viện
    // ngoài. Không cần parse ngược (chỉ gửi đi), nên không cần decode.
    static string EncodeJsonObject(Dictionary<string, object> row)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        bool first = true;
        foreach (var kv in row)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(EscapeJson(kv.Key)).Append("\":");
            AppendValue(sb, kv.Value);
        }
        sb.Append('}');
        return sb.ToString();
    }

    static void AppendValue(StringBuilder sb, object value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case string s:
                sb.Append('"').Append(EscapeJson(s)).Append('"');
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i);
                break;
            case float f:
                sb.Append(f.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(d.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append('"').Append(EscapeJson(value.ToString())).Append('"');
                break;
        }
    }

    static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
