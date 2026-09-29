using System.Collections.Generic;

/// <summary>
/// Runtime model for TeamSelect scene — manages team composition state.
/// Not persisted; DataManager handles persistence.
/// </summary>
public class TeamSelectModel
{
    public GameMode SelectedMode { get; set; }
    public string SelectedClassName { get; set; }
    public List<PlayerInfo> CurrentClassStudents { get; set; }
    public List<PlayerInfo> BlueTeamMembers { get; private set; }
    public List<PlayerInfo> RedTeamMembers { get; private set; }
    public string BlueTeamName { get; set; }
    public string RedTeamName { get; set; }

    public const int MaxTeamSizeTeam = 5;
    public const int MaxTeamSizeOneVsOne = 1;

    public int CurrentMaxTeamSize
    {
        get { return SelectedMode == GameMode.OneVsOne ? MaxTeamSizeOneVsOne : MaxTeamSizeTeam; }
    }

    public bool CanAddToBlue
    {
        get { return BlueTeamMembers.Count < CurrentMaxTeamSize; }
    }

    public bool CanAddToRed
    {
        get { return RedTeamMembers.Count < CurrentMaxTeamSize; }
    }

    public bool BothTeamsReady
    {
        get { return BlueTeamMembers.Count > 0 && RedTeamMembers.Count > 0; }
    }

    public TeamSelectModel()
    {
        SelectedMode = GameMode.OneVsOne;
        SelectedClassName = "";
        CurrentClassStudents = new List<PlayerInfo>();
        BlueTeamMembers = new List<PlayerInfo>();
        RedTeamMembers = new List<PlayerInfo>();
        BlueTeamName = "Tổ 1";
        RedTeamName = "Tổ 2";
    }

    public bool AddToBlue(PlayerInfo player)
    {
        if (!CanAddToBlue) return false;
        if (IsInBlue(player.PlayerId) || IsInRed(player.PlayerId)) return false;
        BlueTeamMembers.Add(player);
        return true;
    }

    public bool AddToRed(PlayerInfo player)
    {
        if (!CanAddToRed) return false;
        if (IsInBlue(player.PlayerId) || IsInRed(player.PlayerId)) return false;
        RedTeamMembers.Add(player);
        return true;
    }

    public void ForceAddToBlue(PlayerInfo player)
    {
        if (!IsInBlue(player.PlayerId))
            BlueTeamMembers.Add(player);
    }

    public void ForceAddToRed(PlayerInfo player)
    {
        if (!IsInRed(player.PlayerId))
            RedTeamMembers.Add(player);
    }

    public bool RemoveFromBlue(string playerId)
    {
        return BlueTeamMembers.RemoveAll(p => p.PlayerId == playerId) > 0;
    }

    public bool RemoveFromRed(string playerId)
    {
        return RedTeamMembers.RemoveAll(p => p.PlayerId == playerId) > 0;
    }

    public bool IsInBlue(string playerId)
    {
        foreach (PlayerInfo p in BlueTeamMembers)
        {
            if (p.PlayerId == playerId) return true;
        }
        return false;
    }

    public bool IsInRed(string playerId)
    {
        foreach (PlayerInfo p in RedTeamMembers)
        {
            if (p.PlayerId == playerId) return true;
        }
        return false;
    }

    public void ClearTeams()
    {
        BlueTeamMembers.Clear();
        RedTeamMembers.Clear();
    }

    /// <summary>
    /// When switching mode, enforce team size limits.
    /// </summary>
    public void EnforceTeamSizeLimits()
    {
        while (BlueTeamMembers.Count > CurrentMaxTeamSize)
        {
            BlueTeamMembers.RemoveAt(BlueTeamMembers.Count - 1);
        }
        while (RedTeamMembers.Count > CurrentMaxTeamSize)
        {
            RedTeamMembers.RemoveAt(RedTeamMembers.Count - 1);
        }
    }
}
