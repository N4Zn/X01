using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton that manages JSON persistence for classes, players, and teams.
/// Save location: Application.persistentDataPath + "/game_data.json"
/// </summary>
public class DataManager : Singleton<DataManager>
{
    private const string FileName = "game_data.json";
    public PersistentData Data { get; private set; }

    protected override void OnCreated()
    {
        Load();
    }

    private string GetFilePath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }

    // ===== Save / Load =====

    public void Save()
    {
        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(GetFilePath(), json);
        Debug.Log("DataManager: saved to " + GetFilePath());
    }

    public void Load()
    {
        string path = GetFilePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Data = JsonUtility.FromJson<PersistentData>(json);
            if (Data == null) Data = new PersistentData();
            Debug.Log("DataManager: loaded from " + path);
        }
        else
        {
            Data = new PersistentData();
            Debug.Log("DataManager: no save file, starting fresh");
        }
    }

    // ===== Class Operations =====

    public List<string> GetAllClassNames()
    {
        List<string> names = new List<string>();
        foreach (ClassData c in Data.Classes)
        {
            names.Add(c.ClassName);
        }
        return names;
    }

    public ClassData GetClass(string className)
    {
        foreach (ClassData c in Data.Classes)
        {
            if (c.ClassName == className) return c;
        }
        return null;
    }

    public ClassData CreateClass(string className)
    {
        ClassData existing = GetClass(className);
        if (existing != null) return existing;

        ClassData newClass = new ClassData(className);
        Data.Classes.Add(newClass);
        Save();
        return newClass;
    }

    public void RenameClass(string oldName, string newName)
    {
        ClassData c = GetClass(oldName);
        if (c == null) return;

        c.ClassName = newName;
        foreach (PlayerInfo p in c.Students)
        {
            p.ClassName = newName;
        }
        Save();
    }

    public void DeleteClass(string className)
    {
        ClassData c = GetClass(className);
        if (c == null) return;
        Data.Classes.Remove(c);
        Save();
    }

    public ClassData CopyClass(string sourceName, string newName)
    {
        ClassData source = GetClass(sourceName);
        if (source == null) return null;

        ClassData copy = new ClassData(newName);
        foreach (PlayerInfo p in source.Students)
        {
            PlayerInfo pCopy = new PlayerInfo(p.PlayerName, newName, p.AvatarIndex);
            pCopy.BackgroundColorIndex = p.BackgroundColorIndex;
            pCopy.HatIndex = p.HatIndex;
            pCopy.GlassesIndex = p.GlassesIndex;
            pCopy.ShirtIndex = p.ShirtIndex;
            copy.Students.Add(pCopy);
        }
        Data.Classes.Add(copy);
        Save();
        return copy;
    }

    // ===== Player Operations =====

    public PlayerInfo AddPlayer(string className, string playerName)
    {
        ClassData c = GetClass(className);
        if (c == null)
        {
            c = CreateClass(className);
        }

        PlayerInfo player = new PlayerInfo(playerName, className);
        c.Students.Add(player);
        Save();
        return player;
    }

    public void DeletePlayer(string className, string playerId)
    {
        ClassData c = GetClass(className);
        if (c == null) return;

        c.Students.RemoveAll(p => p.PlayerId == playerId);
        Save();
    }

    public void UpdatePlayer(PlayerInfo updated)
    {
        foreach (ClassData c in Data.Classes)
        {
            for (int i = 0; i < c.Students.Count; i++)
            {
                if (c.Students[i].PlayerId == updated.PlayerId)
                {
                    // Handle class change
                    if (c.ClassName != updated.ClassName)
                    {
                        c.Students.RemoveAt(i);
                        ClassData newClass = GetClass(updated.ClassName);
                        if (newClass == null) newClass = CreateClass(updated.ClassName);
                        newClass.Students.Add(updated);
                    }
                    else
                    {
                        c.Students[i] = updated;
                    }
                    Save();
                    return;
                }
            }
        }
    }

    public PlayerInfo GetPlayer(string playerId)
    {
        foreach (ClassData c in Data.Classes)
        {
            foreach (PlayerInfo p in c.Students)
            {
                if (p.PlayerId == playerId) return p;
            }
        }
        return null;
    }

    // ===== Team Operations =====

    public List<TeamData> GetAllTeams()
    {
        return Data.Teams;
    }

    public TeamData GetTeam(string teamId)
    {
        foreach (TeamData t in Data.Teams)
        {
            if (t.TeamId == teamId) return t;
        }
        return null;
    }

    public TeamData CreateTeam(string teamName)
    {
        TeamData team = new TeamData(teamName);
        Data.Teams.Add(team);
        Save();
        return team;
    }

    public void SaveTeam(TeamData team)
    {
        for (int i = 0; i < Data.Teams.Count; i++)
        {
            if (Data.Teams[i].TeamId == team.TeamId)
            {
                Data.Teams[i] = team;
                Save();
                return;
            }
        }
        Data.Teams.Add(team);
        Save();
    }

    public void DeleteTeam(string teamId)
    {
        Data.Teams.RemoveAll(t => t.TeamId == teamId);
        Save();
    }

    public TeamData CopyTeam(string sourceId, string newName)
    {
        TeamData source = GetTeam(sourceId);
        if (source == null) return null;

        TeamData copy = new TeamData(newName);
        copy.TeamIconIndex = source.TeamIconIndex;
        copy.MemberPlayerIds = new List<string>(source.MemberPlayerIds);
        Data.Teams.Add(copy);
        Save();
        return copy;
    }
}
