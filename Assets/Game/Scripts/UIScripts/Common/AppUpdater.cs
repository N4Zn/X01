using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class AppVersionInfo
{
    public int    versionCode;
    public string versionName;
    public string apkUrl;
    public string apkUrlFallback;
    public string notes;
}

/// <summary>
/// Gắn vào GameObject trong StartScene.
/// Tự động check version từ GitHub khi app khởi động.
/// Hiện dialog nếu có bản mới — user chọn "Cập nhật" hoặc "Bỏ qua".
/// </summary>
public class AppUpdater : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("URL raw của version.json trên GitHub")]
    [SerializeField] string versionJsonUrl = "https://raw.githubusercontent.com/YOUR_USER/edugame-releases/main/version.json";

    [Tooltip("versionCode của build hiện tại — tăng lên 1 mỗi khi release")]
    [SerializeField] int currentVersionCode = 1;

    [Tooltip("Timeout (giây) cho request check version")]
    [SerializeField] int checkTimeoutSeconds = 8;

    // ── Runtime ───────────────────────────────────────────────────────────────
    Canvas              _canvas;
    GameObject          _panel;
    TextMeshProUGUI     _titleTxt;
    TextMeshProUGUI     _bodyTxt;
    Slider              _progressBar;
    Button              _okBtn;
    Button              _skipBtn;
    AppVersionInfo      _remote;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start() => StartCoroutine(CheckForUpdate());

    // ── Check version ─────────────────────────────────────────────────────────

    IEnumerator CheckForUpdate()
    {
        using var req = UnityWebRequest.Get(versionJsonUrl);
        req.timeout = checkTimeoutSeconds;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.Log($"[AppUpdater] Không kết nối được server: {req.error}");
            yield break;
        }

        try   { _remote = JsonUtility.FromJson<AppVersionInfo>(req.downloadHandler.text); }
        catch { Debug.LogWarning("[AppUpdater] Lỗi parse version.json"); yield break; }

        if (_remote == null || _remote.versionCode <= currentVersionCode) yield break;

        BuildUI();
        ShowUpdateDialog();
    }

    // ── UI: dialog cập nhật ───────────────────────────────────────────────────

    void ShowUpdateDialog()
    {
        _titleTxt.text = $"Có bản cập nhật mới!  v{_remote.versionName}";
        _bodyTxt.text  = string.IsNullOrEmpty(_remote.notes) ? "" : _remote.notes;
        _progressBar.gameObject.SetActive(false);
        _okBtn.gameObject.SetActive(true);
        _skipBtn.gameObject.SetActive(true);
        _panel.SetActive(true);
    }

    void OnClickUpdate()
    {
        _okBtn.interactable   = false;
        _skipBtn.interactable = false;
        StartCoroutine(DownloadAndInstall());
    }

    void OnClickSkip() => _panel.SetActive(false);

    // ── Download + Install ────────────────────────────────────────────────────

    IEnumerator DownloadAndInstall()
    {
        string savePath = Path.Combine(Application.persistentDataPath, "update.apk");

        _bodyTxt.text = "Đang tải...";
        _progressBar.gameObject.SetActive(true);
        _progressBar.value = 0f;

        bool success = false;

        // Thử primary URL
        if (!string.IsNullOrEmpty(_remote.apkUrl))
            yield return StartCoroutine(TryDownload(_remote.apkUrl, savePath, v => { _progressBar.value = v; }, ok => success = ok));

        // Fallback
        if (!success && !string.IsNullOrEmpty(_remote.apkUrlFallback))
        {
            _bodyTxt.text      = "Thử nguồn dự phòng...";
            _progressBar.value = 0f;
            yield return StartCoroutine(TryDownload(_remote.apkUrlFallback, savePath, v => { _progressBar.value = v; }, ok => success = ok));
        }

        if (!success)
        {
            _bodyTxt.text         = "Tải thất bại. Vui lòng thử lại sau.";
            _skipBtn.interactable = true;
            yield break;
        }

        _bodyTxt.text      = "Hoàn tất! Đang mở trình cài đặt...";
        _progressBar.value = 1f;
        yield return new WaitForSeconds(0.5f);

        InstallAPK(savePath);
    }

    IEnumerator TryDownload(string url, string savePath, Action<float> onProgress, Action<bool> onDone)
    {
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerFile(savePath);
        var op = req.SendWebRequest();

        while (!op.isDone)
        {
            onProgress(req.downloadProgress);
            yield return null;
        }

        onDone(req.result == UnityWebRequest.Result.Success);
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[AppUpdater] Download lỗi ({url}): {req.error}");
    }

    // ── Android install ───────────────────────────────────────────────────────

    void InstallAPK(string apkPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var context  = activity.Call<AndroidJavaObject>("getApplicationContext");

            string authority = Application.identifier + ".fileprovider";
            using var file         = new AndroidJavaObject("java.io.File", apkPath);
            using var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider");
            using var uri          = fileProvider.CallStatic<AndroidJavaObject>("getUriForFile", context, authority, file);

            using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");
            intent.Call<AndroidJavaObject>("setDataAndType", uri, "application/vnd.android.package-archive");
            intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
            intent.Call<AndroidJavaObject>("addFlags", 0x00000001); // FLAG_GRANT_READ_URI_PERMISSION
            activity.Call("startActivity", intent);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppUpdater] Không thể mở installer: {e.Message}");
            _bodyTxt.text = "Không mở được installer. Kiểm tra quyền REQUEST_INSTALL_PACKAGES.";
        }
#else
        Debug.Log($"[AppUpdater] (Editor) Sẽ install APK tại: {apkPath}");
#endif
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas overlay
        var go = new GameObject("AppUpdaterCanvas");
        DontDestroyOnLoad(go);
        _canvas                  = go.AddComponent<Canvas>();
        _canvas.renderMode        = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder      = 9999;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();

        // Dim background
        _panel = MakeImage(go.transform, "Panel", new Color(0, 0, 0, 0.85f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _panel.AddComponent<Button>(); // block clicks through
        _panel.SetActive(false);

        // White card
        var card = MakeImage(_panel.transform, "Card", new Color(0.13f, 0.15f, 0.22f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-60f, -80f), new Vector2(60f, 80f));
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(520, 280);

        // Title
        _titleTxt = MakeText(card.transform, "Title", "",
            new Vector2(0, 80), new Vector2(480, 50), 22, FontStyles.Bold, Color.white);

        // Body
        _bodyTxt = MakeText(card.transform, "Body", "",
            new Vector2(0, 20), new Vector2(480, 80), 17, FontStyles.Normal,
            new Color(0.8f, 0.8f, 0.8f));

        // Progress bar
        var sliderGo = new GameObject("Progress");
        sliderGo.transform.SetParent(card.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(0, -40);
        sliderRect.sizeDelta        = new Vector2(460, 18);
        _progressBar = sliderGo.AddComponent<Slider>();
        _progressBar.minValue = 0f;
        _progressBar.maxValue = 1f;
        var bg   = MakeImage(sliderGo.transform, "Bg",   new Color(0.3f,0.3f,0.3f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fill = MakeImage(sliderGo.transform, "Fill", new Color(0.2f,0.7f,1f),   new Vector2(0,0), new Vector2(0,1), Vector2.zero, Vector2.zero);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMax = new Vector2(0, 1);
        _progressBar.fillRect = fillRect;
        _progressBar.targetGraphic = bg.GetComponent<Image>();

        // Buttons
        _okBtn   = MakeButton(card.transform, "BtnOK",   "Cập nhật", new Vector2(-80, -100), new Color(0.15f,0.6f,0.95f), OnClickUpdate);
        _skipBtn = MakeButton(card.transform, "BtnSkip", "Bỏ qua",   new Vector2( 80, -100), new Color(0.35f,0.35f,0.40f), OnClickSkip);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    GameObject MakeImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
        var img  = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    TextMeshProUGUI MakeText(Transform parent, string name, string text,
        Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, FontStyles style, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text           = text;
        tmp.fontSize       = fontSize;
        tmp.fontStyle      = style;
        tmp.color          = color;
        tmp.alignment      = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    Button MakeButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(200, 52);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 18;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return btn;
    }
}
