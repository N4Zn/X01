using UnityEngine;

/// <summary>1 kết quả trên vòng quay may mắn — dữ liệu tĩnh. amount dương = thưởng, âm = phạt,
/// 0 = hụt (chỉ để vui, không đổi điểm).</summary>
public class WheelOutcome
{
    public int amount;
    public string message;

    public WheelOutcome(int amount, string message)
    {
        this.amount = amount;
        this.message = message;
    }
}

/// <summary>
/// Kho kết quả cho ô "vòng quay may mắn" — quay ra ĐÚNG 1 kết quả ngẫu nhiên (khác
/// MysteryRewardPool: không có bước "chọn 1 trong 3", chỉ "quay và nhận"). Thuần C#/static, dùng
/// chung được cho bất kỳ mini-game nào cần 1 bảng thưởng/phạt ngẫu nhiên đơn giản kiểu vòng quay.
/// </summary>
public static class LuckyWheelPool
{
    public static readonly WheelOutcome[] All =
    {
        new(3, "Jackpot!"),
        new(2, "Nice!"),
        new(1, "Lucky!"),
        new(0, "So close!"),
        new(-1, "Oops!"),
        new(-2, "Bad luck!"),
    };

    public static WheelOutcome PickOne() => All[Random.Range(0, All.Length)];
}
