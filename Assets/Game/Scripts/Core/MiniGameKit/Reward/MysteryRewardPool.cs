using System.Collections.Generic;
using UnityEngine;

/// <summary>Loại phần thưởng trong hộp quà bí mật.</summary>
public enum RewardKind
{
    BonusFixed,     // Cộng thêm 1 số điểm cố định vào điểm round
    PenaltyFixed,   // Trừ bớt 1 số điểm cố định khỏi điểm round
    ShareFraction,  // Chia sẻ 1/5 hoặc 2/5 (random) điểm round cho đối thủ
}

/// <summary>1 loại quà trong kho — dữ liệu tĩnh, không đổi giữa các round.</summary>
public class RewardOption
{
    public RewardKind kind;
    public int amount;       // dùng cho BonusFixed/PenaltyFixed — bỏ qua ở ShareFraction
    public string message;   // hiện cho người chơi khi mở hộp

    public RewardOption(RewardKind kind, int amount, string message)
    {
        this.kind = kind;
        this.amount = amount;
        this.message = message;
    }
}

/// <summary>
/// Kho phần thưởng cho hộp quà bí mật hiện ra sau khi 1 đội trả lời đúng. 3 hộp = 3 phần tử
/// random (không trùng) rút ra từ kho này mỗi lần — không phải 3 loại cố định.
///
/// Dùng chung được cho mọi mini-game muốn có cơ chế "mở hộp quà" sau khi thắng round.
/// </summary>
public static class MysteryRewardPool
{
    // message ở đây chỉ là nhãn NGẮN (prefix) — số điểm thật + tên đối thủ (cho Share) do
    // caller (WhoIsItGameController) ghép thêm vào lúc hiện, xem OnMysteryBoxClicked.
    public static readonly RewardOption[] All =
    {
        new(RewardKind.BonusFixed,    2, "Nice!"),
        new(RewardKind.BonusFixed,    3, "Lucky!"),
        new(RewardKind.ShareFraction, 0, "Share!"),
        new(RewardKind.PenaltyFixed,  1, "Oops!"),
        new(RewardKind.PenaltyFixed,  2, "Bad luck!"),
    };

    /// <summary>Rút ngẫu nhiên 3 phần thưởng KHÔNG trùng nhau từ kho (dùng cho 3 hộp quà).</summary>
    public static RewardOption[] PickThree()
    {
        var pool = new List<RewardOption>(All);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        int take = Mathf.Min(3, pool.Count);
        return pool.GetRange(0, take).ToArray();
    }

    /// <summary>
    /// Áp dụng 1 phần thưởng lên điểm round hiện tại (roundPoints = giá trị câu hỏi SAU jackpot,
    /// TRƯỚC sao hi vọng). jackpotMultiplier = roundPoints / điểm gốc (luôn nguyên, vd 1/2/4/8...)
    /// — số điểm thưởng/phạt cố định (+2, +3, -1, -2) cũng được nhân theo jackpot này trước khi
    /// cộng/trừ, vd jackpot x2 thì "+2 điểm" thành "+4 điểm".
    ///
    /// Trả về (điểm người thắng nhận — CHƯA nhân sao hi vọng, caller tự x2 nếu có đặt sao,
    /// điểm đối thủ nhận thêm — 0 nếu không chia sẻ, không bị ảnh hưởng bởi sao hi vọng của người thắng).
    /// </summary>
    public static (int winnerPoints, int opponentPoints) Apply(RewardOption reward, int roundPoints, int jackpotMultiplier)
    {
        switch (reward.kind)
        {
            case RewardKind.BonusFixed:
                return (roundPoints + reward.amount * jackpotMultiplier, 0);

            case RewardKind.PenaltyFixed:
                return (Mathf.Max(0, roundPoints - reward.amount * jackpotMultiplier), 0);

            case RewardKind.ShareFraction:
                // Random 1/5 hoặc 2/5 số điểm chia cho đối thủ, phần còn lại người thắng giữ.
                // roundPoints đã bao gồm jackpot sẵn (CurrentValue), không cần nhân thêm ở đây.
                int oppNumerator = Random.value < 0.5f ? 1 : 2;
                int opponentPoints = Mathf.RoundToInt(roundPoints * oppNumerator / 5f);
                return (roundPoints - opponentPoints, opponentPoints);

            default:
                return (roundPoints, 0);
        }
    }
}
