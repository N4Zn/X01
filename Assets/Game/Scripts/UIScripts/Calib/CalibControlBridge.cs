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

    /// <summary>Chế độ tuần tự (1 trụ) — bắt đầu phiên đo từng điểm 1, TH giáo viên chỉ có 1 trụ.</summary>
    public void OnStartSequentialRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnStartSequentialRequested");
        CalibSceneController.Current?.BeginSequential();
    }

    public void OnCaptureStepRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnCaptureStepRequested");
        CalibSceneController.Current?.CaptureSequentialStep();
    }

    public void OnStepBackRequested(string unused)
    {
        Debug.Log("[CalibControlBridge] OnStepBackRequested");
        CalibSceneController.Current?.StepBackSequential();
    }

    /// <summary>Giáo viên vừa chọn đúng số ứng viên nào (0-based) sau khi thấy nhiều vật cùng
    /// lúc trong vùng quét (vd tường + trụ) — xem PushCandidates/DrawCandidateMarkers.</summary>
    public void OnCandidateChosen(string indexStr)
    {
        Debug.Log($"[CalibControlBridge] OnCandidateChosen: {indexStr}");
        if (int.TryParse(indexStr, out int index)) CalibSceneController.Current?.ChooseCandidate(index);
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

    /// <summary>Báo đã chuyển sang điểm thứ mấy trong phiên tuần tự (1 trụ) — tablet hiện
    /// "Điểm X/5: Góc ..." + đổi tên nút hành động chính.</summary>
    public void PushSequentialStep(int index, int total, string role, string roleLabel)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnSequentialStep", index, total, role, roleLabel);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushSequentialStep lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Báo kết quả đo 1 điểm ĐƠN trong phiên tuần tự (khác PushResult — đó là kết quả
    /// CUỐI của cả phiên sau khi đã giải affine xong).</summary>
    public void PushSequentialStepResult(bool success, string failReason)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnSequentialStepResult", success, failReason ?? "");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushSequentialStepResult lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Phát hiện NHIỀU vật cùng lúc trong vùng quét (vd tường + trụ) — không có cách
    /// tự động phân biệt đáng tin, chuyển cho giáo viên tự chọn. encodedCounts: "n1;n2;n3;..."
    /// (số điểm/cụm của từng ứng viên, thứ tự khớp đúng với số đã đánh trên máy chiếu —
    /// DrawCandidateMarkers). Tablet dựng nút "Số i (ni điểm)" cho từng ứng viên.</summary>
    public void PushCandidates(string encodedCounts)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass(CalibActivityClass))
                cls.CallStatic("OnCandidatesFound", encodedCounts);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CalibControlBridge] PushCandidates lỗi (CalibActivity có thể chưa chạy/khác display): {e.Message}");
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
