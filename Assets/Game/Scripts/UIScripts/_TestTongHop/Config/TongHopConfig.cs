using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// ─── Data ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Tất cả tham số tuning cho TestTongHop.
/// Default ở đây là giá trị fallback khi không tìm thấy file config.
/// </summary>
[Serializable]
public class TongHopConfigData
{
    // ── Matching ──────────────────────────────────────────────────────────────
    /// <summary>Giây phải chờ trước khi có thể bỏ chọn ô (click lại ô đó).</summary>
    public float matchingDeselectDelay = 2f;

    // ── Display weights ───────────────────────────────────────────────────────
    /// <summary>Tỷ lệ xuất hiện câu Choose dạng Floating (planet bay). Tổng 3 weight = 100.</summary>
    public int weightFloating = 40;
    /// <summary>Tỷ lệ xuất hiện câu Choose dạng Button (nút bấm tĩnh).</summary>
    public int weightButton   = 35;
    /// <summary>Tỷ lệ xuất hiện câu Matching (nối cặp).</summary>
    public int weightMatching = 25;

    // ── Floating display ──────────────────────────────────────────────────────
    /// <summary>Khoảng cách từ tâm màn hình đến tâm quỹ đạo mỗi bên (pixel).</summary>
    public float orbitCenterX  = 265f;
    /// <summary>Bán kính quỹ đạo (pixel).</summary>
    public float orbitRadius   = 150f;
    /// <summary>Tốc độ xoay quỹ đạo (độ/giây).</summary>
    public float orbitSpeed    = 2f;
    /// <summary>Kích thước (width = height) của mỗi floating item (pixel).</summary>
    public float floatingSize  = 125f;

    // ── Choose display ────────────────────────────────────────────────────────
    /// <summary>
    /// Thời gian tối thiểu (giây) phải chờ sau khi chọn một đáp án trước khi
    /// có thể bỏ chọn nó (MultiSelect toggle-off) hoặc click lại nó
    /// (OrderedSequence tránh double-tap → WrongInSequence).
    /// Single không bị ảnh hưởng.
    /// </summary>
    public float chooseDeselectDelay = 0.5f;

    // ── Feedback timing ───────────────────────────────────────────────────────
    /// <summary>Thời gian hiển thị feedback khi trả lời đúng (giây).</summary>
    public float feedbackDelayCorrect = 1.2f;
    /// <summary>Thời gian hiển thị feedback khi trả lời sai (giây).</summary>
    public float feedbackDelayWrong   = 2.0f;
    /// <summary>Đếm ngược "Next in Xs" trước khi chuyển câu tiếp theo (giây nguyên).</summary>
    public int   nextQuestionDelay    = 4;

    // ── Adaptive difficulty ───────────────────────────────────────────────────
    /// <summary>Bật/tắt adaptive difficulty. false = cố định theo difficulty bên dưới.</summary>
    public bool adaptiveDifficulty    = false;
    /// <summary>Số câu đúng liên tiếp để tăng lên mức khó hơn.</summary>
    public int  correctStreakToLevelUp = 3;
    /// <summary>Số câu sai liên tiếp để giảm xuống mức dễ hơn.</summary>
    public int  wrongStreakToLevelDown = 2;

    // ── Asset roots (rút ngắn đường dẫn CSV) ─────────────────────────────────
    /// <summary>
    /// Prefix tự động gắn vào đường dẫn ảnh ngắn trong CSV.
    /// Ví dụ: imageRoot = "TestTongHop/images"
    ///   CSV viết "animals/cat"   → load "TestTongHop/images/animals/cat"
    ///   CSV viết đường dẫn đầy đủ vẫn hoạt động bình thường (backward-compat).
    /// </summary>
    public string imageRoot = "TestTongHop/images";
    /// <summary>
    /// Prefix tự động gắn vào đường dẫn audio ngắn trong CSV.
    /// Ví dụ: audioRoot = "TestTongHop/audio"
    ///   CSV viết "cat_sound"     → load "TestTongHop/audio/cat_sound"
    /// </summary>
    public string audioRoot = "TestTongHop/audio";

    // ── Game mechanics ────────────────────────────────────────────────────────
    /// <summary>
    /// true = chế độ độc lập: mỗi player tự trả lời câu hỏi riêng, không chờ người kia.
    /// Bên nào xong trước thì đếm ngược nextQuestionDelayPerPlayer rồi nhận câu tiếp theo.
    /// Không giới hạn thời gian.
    /// false (mặc định) = chia sẻ hiện tại (ai đúng trước thắng vòng đó).
    /// </summary>
    public bool independentPlay = false;
    /// <summary>Giây chờ của từng player trước khi nhận câu tiếp (chỉ dùng khi independentPlay=true).</summary>
    public int nextQuestionDelayPerPlayer = 4;

    // ── Game ──────────────────────────────────────────────────────────────────
    /// <summary>Độ khó hiện tại (1–10). Adaptive sẽ điều chỉnh trong phạm vi [difficultyMin, difficultyMax].</summary>
    public int difficulty    = 1;
    /// <summary>Giới hạn dưới của độ khó (1–10). difficultyMin == difficultyMax → cố định.</summary>
    public int difficultyMin = 1;
    /// <summary>Giới hạn trên của độ khó (1–10).</summary>
    public int difficultyMax = 10;
}

// ─── Loader ───────────────────────────────────────────────────────────────────

/// <summary>
/// Load config tuning từ file JSON — có thể sửa trực tiếp trên thiết bị mà không rebuild APK.
///
/// ──────────────────────────────────────────────────────────────────────────────
/// Load priority:
///   1. persistentDataPath/TestTongHop/gameconfig.json   ← user tự sửa được
///   2. StreamingAssets/TestTongHop/gameconfig.json       ← default trong APK
///      (lần đầu chạy sẽ copy sang persistentDataPath tự động)
///
/// Trên Android — sửa config không cần rebuild:
///   adb push gameconfig.json \
///     "/sdcard/Android/data/<package>/files/TestTongHop/gameconfig.json"
///   (hoặc dùng file manager nếu máy không cần root)
///
/// Reset về default:
///   adb shell rm \
///     "/sdcard/Android/data/<package>/files/TestTongHop/gameconfig.json"
///   → lần chạy tiếp theo sẽ copy lại từ APK
/// ──────────────────────────────────────────────────────────────────────────────
///
/// Gọi:  yield return StartCoroutine(TongHopConfig.Load());
///       (trong StartMainController, trước khi chuyển scene)
/// </summary>
public static class TongHopConfig
{
    const string SubPath = "TestTongHop/gameconfig.json";

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Dữ liệu config hiện tại — mọi component đọc từ đây.</summary>
    public static TongHopConfigData Current { get; private set; } = new TongHopConfigData();

    /// <summary>True sau khi Load() hoàn thành.</summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>Đường dẫn file config phía người dùng (có thể sửa trên thiết bị).</summary>
    public static string UserConfigPath
        => Path.Combine(Application.persistentDataPath, SubPath);

    // ── Auto-load (Editor + mọi scene, không cần StartScene chạy trước) ─────────

    /// <summary>
    /// Tự động load config trước khi bất kỳ scene nào start.
    /// Cho phép test Play trực tiếp từ game scene mà không cần đi qua StartScene.
    /// Trên Android lần đầu (chưa copy file), StartMainController.Load() vẫn
    /// xử lý copy từ StreamingAssets; AutoLoad chỉ skip nếu IsLoaded == true.
    /// </summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad()
    {
        if (IsLoaded) return;   // StartScene đã load rồi → skip

        // persistentDataPath có file (thiết bị đã chạy qua StartScene ít nhất 1 lần)
        if (File.Exists(UserConfigPath))
        {
            ApplyJson(File.ReadAllText(UserConfigPath));
            IsLoaded = true;
            UnityEngine.Debug.Log($"[TongHopConfig] AutoLoad from: {UserConfigPath}");
            return;
        }

        // Fallback: StreamingAssets (Editor hoặc standalone — file access trực tiếp)
        string src = Path.Combine(Application.streamingAssetsPath, SubPath);
        if (File.Exists(src))
        {
            ApplyJson(File.ReadAllText(src));
            UnityEngine.Debug.Log($"[TongHopConfig] AutoLoad from StreamingAssets: {src}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[TongHopConfig] AutoLoad: no config file found — using hardcoded defaults.");
        }

        IsLoaded = true;
        // Ghi chú: Android lần đầu chạy thẳng vào game scene (không qua StartScene)
        // sẽ dùng defaults vì StreamingAssets trên Android cần UnityWebRequest async.
        // Flow chuẩn (StartScene → HomeScene → Game) vẫn copy + load đầy đủ.
    }

    // ── Load (coroutine, dùng trong StartMainController) ─────────────────────

    /// <summary>
    /// Coroutine — gọi từ StartMainController trước khi chuyển sang MenuScene.
    /// Luôn đọc trực tiếp từ StreamingAssets (nguồn gốc trong APK) để tránh
    /// file cache cũ ở persistentDataPath từ bản install trước override sai giá trị.
    /// </summary>
    public static IEnumerator Load()
    {
        IsLoaded = false;

        string dir = Path.GetDirectoryName(UserConfigPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string json = null;
        string src  = Path.Combine(Application.streamingAssetsPath, SubPath);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: StreamingAssets nằm trong APK — phải dùng UnityWebRequest
        using (var req = UnityWebRequest.Get(src))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                json = req.downloadHandler.text;
                // Cache sang persistentDataPath để AutoLoad() dùng được ở lần chạy sau
                try { File.WriteAllText(UserConfigPath, json); }
                catch (Exception ex) { Debug.LogWarning($"[TongHopConfig] Cache write failed: {ex.Message}"); }
            }
            else
            {
                Debug.LogWarning($"[TongHopConfig] Cannot read StreamingAssets: {req.error}");
                // Chỉ fall back sang cache nếu UnityWebRequest thực sự lỗi
                if (File.Exists(UserConfigPath))
                    json = File.ReadAllText(UserConfigPath);
            }
        }
#else
        // Editor / Standalone: đọc trực tiếp từ file
        if (File.Exists(src)) json = File.ReadAllText(src);
        yield return null;
#endif

        if (json != null)
        {
            ApplyJson(json);
        }
        else
        {
            Debug.LogWarning("[TongHopConfig] No config found — using hardcoded defaults.");
        }

        IsLoaded = true;
        Debug.Log($"[TongHopConfig] Ready — difficulty={Current.difficulty}, " +
                  $"weights={Current.weightFloating}/{Current.weightButton}/{Current.weightMatching}");
    }

    /// <summary>
    /// Lưu Current config ra persistentDataPath.
    /// Trên Android (release APK) không write được vào StreamingAssets,
    /// nhưng persistentDataPath thì OK.
    /// </summary>
    public static void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(UserConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(UserConfigPath, JsonUtility.ToJson(Current, prettyPrint: true));
            Debug.Log($"[TongHopConfig] Saved to: {UserConfigPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TongHopConfig] Save failed: {e.Message}");
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// Copy file default từ StreamingAssets → persistentDataPath.
    /// Trên Android cần UnityWebRequest vì StreamingAssets nằm trong APK.
    static IEnumerator CopyDefaultToUserPath()
    {
        string src = Path.Combine(Application.streamingAssetsPath, SubPath);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var req = UnityWebRequest.Get(src))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllText(UserConfigPath, req.downloadHandler.text);
                Debug.Log($"[TongHopConfig] Default config copied to {UserConfigPath}");
            }
            else
            {
                Debug.LogWarning($"[TongHopConfig] Cannot read StreamingAssets default: {req.error}");
            }
        }
#else
        if (File.Exists(src))
        {
            File.Copy(src, UserConfigPath, overwrite: true);
            Debug.Log($"[TongHopConfig] Default config copied to {UserConfigPath}");
        }
        else
        {
            Debug.LogWarning($"[TongHopConfig] StreamingAssets default not found: {src}");
        }
        yield return null;
#endif
    }

    static void ApplyJson(string json)
    {
        try
        {
            JsonUtility.FromJsonOverwrite(json, Current);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TongHopConfig] JSON parse error: {e.Message} — using defaults.");
        }
    }
}
