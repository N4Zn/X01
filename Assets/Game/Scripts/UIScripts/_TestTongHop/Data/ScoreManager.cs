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
