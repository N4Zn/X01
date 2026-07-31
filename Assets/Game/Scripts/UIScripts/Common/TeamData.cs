using System;
using System.Collections.Generic;

/// <summary>
/// A saved team with member references.
/// Persisted via DataManager.
/// </summary>
[System.Serializable]
public class TeamData
{
    public string TeamId;
    public string TeamName;
    public string ClassName;
    public int TeamIconIndex;
    public List<string> MemberPlayerIds;

    public TeamData()
    {
        TeamId = Guid.NewGuid().ToString();
        TeamName = "";
        ClassName = "";
        TeamIconIndex = 0;
        MemberPlayerIds = new List<string>();
    }

    public TeamData(string teamName)
    {
        TeamId = Guid.NewGuid().ToString();
        TeamName = teamName;
        ClassName = "";
        TeamIconIndex = 0;
        MemberPlayerIds = new List<string>();
    }
}

/// <summary>
/// Wrapper for all persistent data — serialized to a single JSON file.
/// </summary>
[System.Serializable]
public class PersistentData
{
    public List<ClassData> Classes;
    public List<TeamData> Teams;

    public PersistentData()
    {
        Classes = new List<ClassData>();
        Teams = new List<TeamData>();
    }
}
