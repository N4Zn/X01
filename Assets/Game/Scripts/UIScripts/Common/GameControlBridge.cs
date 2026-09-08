using UnityEngine;

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
