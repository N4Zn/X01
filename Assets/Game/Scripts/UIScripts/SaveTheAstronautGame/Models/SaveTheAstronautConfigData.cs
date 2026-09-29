using System;

/// <summary>Thông số chỉnh nhịp độ/điểm cho "Save The Astronaut" — dùng CHUNG cho cả 2 làn (đảm
/// bảo 2 đội luôn cùng tốc độ cuộn, đúng yêu cầu "world trôi đồng bộ 2 bên").</summary>
[Serializable]
public class SaveTheAstronautConfigData
{
    [UnityEngine.Header("Chặng")]
    public int stepCount = 8;
    public int pointsPerLap = 10;
    public int targetScoreForHud = 100;

    [UnityEngine.Header("Tốc độ cuộn")]
    [UnityEngine.Tooltip("Giây để 1 cặp nút trôi từ xa (nhỏ) tới sát người chơi (to) — không bấm kịp trong khoảng này thì tự tính sai (buộc phải chọn).")]
    public float travelDuration = 2.0f;

    [UnityEngine.Header("Kích thước nút hình thang lúc trôi")]
    [UnityEngine.Tooltip("Nhỏ hơn = hàng xa trông nhỏ/mỏng hơn hẳn, đỡ chồng lấn khi nhiều hàng cùng hiện (xem DecoyCount trong SceneBuilder).")]
    public float spawnScale = 0.15f;
    public float captureScale = 1f;
    [UnityEngine.Tooltip("Vừa là độ xiên hình thang MỖI hàng, vừa là tỉ lệ co kích thước GIỮA 2 hàng liền kề (bắt buộc dùng CHUNG 1 giá trị — đây là điều kiện để mép các hàng khớp liền mạch, không hở/không đè). Nhỏ hơn = xiên hơn NHƯNG cũng co nhanh hơn (ít hàng còn thấy rõ trước khi quá nhỏ).")]
    [UnityEngine.Range(0.3f, 0.85f)] public float trapezoidTopWidthRatio = 0.6f;

    [UnityEngine.Header("Thời gian giữ hiệu ứng reveal")]
    public float correctRevealSeconds = 1.1f;
    public float wrongRevealSeconds = 1.3f;
    public float retryDelaySeconds = 2f;
    public float earthRevealSeconds = 2.2f;

    [UnityEngine.Header("Pattern")]
    [UnityEngine.Tooltip("Số lần đi hết vòng trước khi re-randomize thứ tự đúng/sai.")]
    public int lapsPerPattern = 5;

    [UnityEngine.Header("Countdown / kết thúc")]
    public int countdownSeconds = 3;
    public float resultDisplaySeconds = 3f;
}
