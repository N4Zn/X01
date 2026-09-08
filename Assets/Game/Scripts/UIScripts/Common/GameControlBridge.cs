using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cầu nối 2 chiều giữa ControlActivity (Java, display 0 — nút Pause/Stop) và game đang
/// chạy trong Unity (display máy chiếu) — CÙNG process, khác Activity/task/display.
///
/// Chiều vào (Java → C#): ControlActivity gọi UnityPlayer.UnitySendMessage("GameControlBridge",
/// "OnPauseRequested"/"OnResumeRequested", "") qua reflection (tránh phụ thuộc biên dịch,
/// giống LidarUsbBridge.java) — GameObject PHẢI tên đúng "GameControlBridge".
///
/// Chiều ra (C# → Java): PushReport() gọi AndroidJavaClass("com.eduxplore.control.ControlActivity")
/// .CallStatic("UpdateReport", ...) — AndroidJavaClass luôn resolve qua reflection lúc chạy,
/// không có vấn đề phụ thuộc biên dịch chiều nào (khác UnitySendMessage ở trên, đây là C#
/// gọi Java, không phải Java gọi C#).
///
/// Singleton&lt;T&gt; (cùng pattern LidarTouchBridge) — RuntimeInitializeOnLoadMethod warm-up
/// để đảm bảo GameObject tồn tại TRƯỚC khi ControlActivity có thể gửi lệnh (nút Pause chỉ
/// bấm được sau khi UnityPlayerActivity đã khởi động, nhưng cứ warm-up sớm cho chắc, khớp
/// pattern đã dùng cho LidarTouchBridge).
/// </summary>
public class GameControlBridge : Singleton<GameControlBridge>
{
    private const string ControlActivityClass = "com.eduxplore.control.ControlActivity";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "GameControlBridge";
    }

    /// <summary>Gọi từ ControlActivity (UnitySendMessage) khi bấm Pause. Dừng timer/logic
    /// của minigame đang chạy (nếu có) VÀ tắt Lidar touch cùng lúc — 2 việc luôn đi đôi vì
    /// nguồn touch duy nhất trên thiết bị là Lidar (xem LidarTouchBridge).</summary>
    public void OnPauseRequested(string unused)
    {
        MiniGameControllerBase.Current?.Pause();
        // TestTongHopController (game chính, engine TongHopGame) có FSM/state hoàn toàn
        // riêng, KHÔNG kế thừa MiniGameControllerBase — phải gọi thêm nhánh này, nếu không
        // Pause/report luôn no-op với game chính (đã xác nhận qua test thực tế).
        TestTongHopController.Current?.Pause();
        LidarTouchBridge.Instance.SetTouchEnabled(false);
        MusicManager.Instance?.PauseBgm();
        Debug.Log("[GameControlBridge] OnPauseRequested");
    }

    public void OnResumeRequested(string unused)
    {
        MiniGameControllerBase.Current?.Resume();
        TestTongHopController.Current?.Resume();
        LidarTouchBridge.Instance.SetTouchEnabled(true);
        MusicManager.Instance?.ResumeBgm();
        Debug.Log("[GameControlBridge] OnResumeRequested");
    }

    /// <summary>Gọi từ ControlActivity khi bấm Stop — KHÔNG destroy/finish UnityPlayerActivity
    /// (khác finishUnityTask() cũ). Unity tự kill() cả process (dùng chung với ControlActivity)
    /// khi Activity của nó bị destroy — hành vi engine, không sửa được từ code app (xem lịch sử
    /// bug "Stop thoát cả app"). Né hẳn vấn đề bằng cách giữ UnityPlayerActivity SỐNG NGUYÊN,
    /// chỉ dừng game + che đen display — giống hệt việc bấm Home để app chạy nền, chỉ khác là
    /// phải tự làm thủ công vì 2 display riêng biệt không có 1 cử chỉ Home áp dụng đúng cho
    /// riêng display máy chiếu.</summary>
    public void OnStopRequested(string unused)
    {
        MiniGameControllerBase.Current?.Pause();
        TestTongHopController.Current?.Pause();
        LidarTouchBridge.Instance.SetTouchEnabled(false);
        MusicManager.Instance?.PauseBgm();
        ShowBlankOverlay(true);
        Debug.Log("[GameControlBridge] OnStopRequested");
    }

    /// <summary>Gọi từ ControlActivity khi bấm Start LẦN 2 TRỞ ĐI — UnityPlayerActivity đã
    /// sống sẵn từ lần chơi trước (Stop không còn destroy nó nữa), nên chỉ cần lệnh nạp game
    /// mới qua message thay vì khởi động lại Activity (Intent extra chỉ đọc được 1 lần lúc
    /// cold-boot — xem ControlBridge.Init()). payload: "sceneName|gameName".</summary>
    public void OnLoadGameRequested(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;
        int sep = payload.IndexOf('|');
        string sceneName = sep >= 0 ? payload.Substring(0, sep) : payload;
        string gameName  = sep >= 0 ? payload.Substring(sep + 1) : null;

        ShowBlankOverlay(false);
        LidarTouchBridge.Instance.SetTouchEnabled(true);
        ControlBridge.LoadGame(sceneName, gameName);
    }

    // ── Blank overlay — che display máy chiếu lúc Stop, thay cho việc destroy Activity ──────

    Canvas _blankOverlay;

    void ShowBlankOverlay(bool visible)
    {
        if (_blankOverlay == null && visible) _blankOverlay = CreateBlankOverlay();
        if (_blankOverlay != null) _blankOverlay.gameObject.SetActive(visible);
    }

    static Canvas CreateBlankOverlay()
    {
        var go = new GameObject("[GameControlBridge_BlankOverlay]");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // đè lên mọi UI khác, kể cả debug touch indicator

        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(go.transform, false);
        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false; // không cần chặn touch riêng — LidarTouchBridge đã tắt touch

        return canvas;
    }

    /// <summary>Báo ControlActivity game vừa TỰ kết thúc (hết giờ/hết vòng — GameOver tự
    /// nhiên, KHÔNG phải do bấm Stop) — gọi từ StateMachineEnter_GameOver của
    /// TestTongHopController/MiniGameControllerBase. ControlActivity tự quay Menu để chọn game
    /// tiếp theo, coi như hết 1 round, không cần user bấm Stop thủ công. Display máy chiếu
    /// KHÔNG bị đụng tới — Unity tự chuyển ScoreScene như bình thường, vẫn hiện kết quả.</summary>
    public void PushGameEnded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("OnGameEnded");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushGameEnded lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Đẩy report (giây còn lại, tên + điểm TỪNG BÊN trái/phải riêng) sang
    /// ControlActivity — gọi từ MiniGameControllerBase.Update()/TestTongHopController.Update(),
    /// throttle 1 lần/giây ở phía gọi. Trước đây chỉ gộp 1 tổng điểm, không có tên — sửa lại
    /// tách riêng + kèm tên (GameSessionManager.GetDisplayName1/2, cập nhật liên tục từ
    /// PlayerRecognitionService) vì control panel cần biết "ai" đang chơi, không chỉ điểm số.</summary>
    public void PushReport(int secondsLeft, string leftName, int leftScore, string rightName, int rightScore)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("UpdateReport", secondsLeft + "s",
                    leftName, leftScore.ToString(), rightName, rightScore.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushReport lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }
}
