using System;

public class ScoreManager
{
    public int ScoreLeft  { get; private set; }
    public int ScoreRight { get; private set; }

    public event Action<Team, int, int> OnScoreChanged;  // team, scoreLeft, scoreRight

    public void AddPoint(Team team)
    {
        if (team == Team.Left)  ScoreLeft++;
        else                    ScoreRight++;

        OnScoreChanged?.Invoke(team, ScoreLeft, ScoreRight);
    }

    /// <summary>Cộng 1 số điểm bất kỳ (có thể 0) cho 1 đội — dùng cho reward system (MiniGameKit)
    /// nơi giá trị round không cố định +1 (jackpot, hope star x2, mystery box...).</summary>
    public void AddPoints(Team team, int points)
    {
        if (points == 0) return;
        if (team == Team.Left)  ScoreLeft  += points;
        else                    ScoreRight += points;

        OnScoreChanged?.Invoke(team, ScoreLeft, ScoreRight);
    }

    public Team? GetLeader()
    {
        if (ScoreLeft  > ScoreRight) return Team.Left;
        if (ScoreRight > ScoreLeft)  return Team.Right;
        return null;   // hòa
    }

    public void Reset()
    {
        ScoreLeft  = 0;
        ScoreRight = 0;
    }
}
