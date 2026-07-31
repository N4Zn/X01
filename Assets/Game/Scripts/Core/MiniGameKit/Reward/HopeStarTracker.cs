/// <summary>
/// "Ngôi sao hi vọng" — mỗi đội có 1 số lượng giới hạn quyền đặt cược vào câu hỏi SẮP hiện ra
/// (đặt trước khi biết nội dung câu hỏi). Đặt sao + trả lời đúng câu đó → điểm x2 (áp dụng bởi
/// caller, class này chỉ quản lý số lượng còn lại + trạng thái đã đặt hay chưa của round hiện tại).
///
/// Dùng chung được cho mọi mini-game muốn có cơ chế "đặt cược trước câu hỏi" — không phụ thuộc
/// WhoIsItGame.
/// </summary>
public class HopeStarTracker
{
    readonly int _maxStars;
    int _leftRemaining;
    int _rightRemaining;
    bool _leftBet;
    bool _rightBet;

    public HopeStarTracker(int maxStars)
    {
        _maxStars = maxStars;
        _leftRemaining = maxStars;
        _rightRemaining = maxStars;
    }

    public int MaxStars => _maxStars;
    public int Remaining(Team team) => team == Team.Left ? _leftRemaining : _rightRemaining;
    public bool HasBet(Team team) => team == Team.Left ? _leftBet : _rightBet;

    /// <summary>Đặt sao cho round hiện tại. False nếu hết sao hoặc đã đặt rồi (chỉ đặt 1 lần/round).</summary>
    public bool TryPlaceBet(Team team)
    {
        if (HasBet(team) || Remaining(team) <= 0) return false;

        if (team == Team.Left) { _leftRemaining--; _leftBet = true; }
        else                    { _rightRemaining--; _rightBet = true; }
        return true;
    }

    /// <summary>Gọi khi bắt đầu 1 round mới (trước bet phase) — reset trạng thái đặt cược, KHÔNG hoàn sao.</summary>
    public void ResetRoundBet()
    {
        _leftBet = false;
        _rightBet = false;
    }
}
