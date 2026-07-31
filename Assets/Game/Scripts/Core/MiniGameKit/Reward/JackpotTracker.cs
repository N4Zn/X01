/// <summary>
/// Điểm câu hỏi hiện tại, có cơ chế "dồn điểm" (jackpot/rollover): nếu CẢ 2 đội trả lời sai,
/// điểm không mất mà x2 dồn sang câu tiếp theo. Đội nào trả lời đúng câu đang dồn thì ăn trọn
/// CurrentValue tại thời điểm đó, sau đó reset về điểm gốc.
///
/// Dùng chung được cho mọi mini-game muốn có cơ chế dồn điểm khi cả 2 bên cùng sai.
/// </summary>
public class JackpotTracker
{
    readonly int _basePoints;
    public int CurrentValue { get; private set; }

    public JackpotTracker(int basePoints)
    {
        _basePoints = basePoints;
        CurrentValue = basePoints;
    }

    /// <summary>Gọi khi cả 2 đội đều trả lời sai — điểm câu tiếp theo x2.</summary>
    public void RegisterBothWrong() => CurrentValue *= 2;

    /// <summary>Gọi sau khi có đội trả lời đúng (đã lấy CurrentValue để tính điểm xong) — về lại điểm gốc.</summary>
    public void ResetAfterWin() => CurrentValue = _basePoints;
}
