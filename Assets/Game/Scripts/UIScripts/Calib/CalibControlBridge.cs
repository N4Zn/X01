using UnityEngine;

/// <summary>
/// Cầu nối 2 chiều giữa CalibActivity (Java, display 0 — nút Bắt đầu/Lưu/Làm lại/Thoát) và
/// CalibSceneController đang chạy trong Unity (display máy chiếu) — CÙNG kiến trúc với
/// GameControlBridge/ControlActivity (xem file đó), NHƯNG tách bridge/Activity RIÊNG thay vì
/// tổng quát hoá GameControlBridge để dùng chung: GameControlBridge.PushReport/PushGameEnded
/// đang hardcode gọi thẳng lớp ControlActivity, đụng vào đó để "generalize" sẽ chạm luồng
/// Start/Pause/Stop đã test kỹ trên máy thật — rủi ro không đáng cho 1 tính năng phụ. Calib có
/// bridge/Activity độc lập, không đụng gì tới GameControlBridge/ControlActivity.
///
/// Chiều vào (Java → C#): CalibActivity gọi UnityPlayer.UnitySendMessage("CalibControlBridge",
/// "OnStartCalibRequested"/"OnSaveRequested"/"OnExitRequested", "") — GameObject PHẢI tên đúng
/// "CalibControlBridge".
///
/// Chiều ra (C# → Java): PushXxx() gọi AndroidJavaClass("com.eduxplore.control.CalibActivity")
/// .CallStatic(...) — resolve qua reflection lúc chạy, không phụ thuộc biên dịch.
/// </summary>
public class CalibControlBridge : Singleton<CalibControlBridge>
{
    private const string CalibActivityClass = "com.eduxplore.control.CalibActivity";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "CalibControlBridge";
    }

    // ── Chiều vào (Java → C#) — forward cho CalibSceneController.Current, no-op nếu scene chưa
    // sẵn sàng (vd message tới trước khi CalibScene load xong) ─────────────────────────────────

    public void OnStartCalibRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnStartCalibRequested");
        CalibSceneController.Current?.BeginCapture();
    }

    public void OnSaveRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnSaveRequested");
        CalibSceneController.Current?.SavePending();
    }

    public void OnExitRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnExitRequested");
        CalibSceneController.Current?.CancelAndReset();
    }

    // ── Chiều ra (C# → Java) ─────────────────────────────────────────────────────────────────

    /// <summary>Báo tablet đang trong cửa sổ lắng nghe — hiện đếm ngược durationSeconds.</summary>
    public void PushListening(float durationSeconds)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnCalibListening", Mathf.RoundToInt(durationSeconds * 1000));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushListening lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Báo kết quả 1 lần capture — tablet dùng để hiện Xanh/Vàng/Đỏ + bật nút Lưu.</summary>
    public void PushResult(bool success, int found, int expected, float maxResidualPx, string failReason)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnCalibResult", success, found, expected, maxResidualPx, failReason ?? "");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushResult lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Báo đã lưu thành công (sau khi bấm Lưu) — tablet hiện "Đã lưu calib".</summary>
    public void PushSaved()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnCalibSaved");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushSaved lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }
}
