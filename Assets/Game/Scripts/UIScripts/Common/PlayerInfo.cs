using System;

/// <summary>
/// Persistent player data — saved to JSON via DataManager.
/// </summary>
[System.Serializable]
public class PlayerInfo
{
    public string PlayerId;
    public string PlayerName;
    public string ClassName;
    public int AvatarIndex;           // 0-11 (CharacterDatabase)
    public int BackgroundColorIndex;  // 0=red, 1=orange, 2=yellow, 3=green, 4=purple, 5=black
    public int HairIndex;             // 0-5 (hair style), -1 = none (bald)
    public int HatIndex;              // -1 = none
    public int GlassesIndex;          // -1 = none (0-5 for glasses styles)
    public int ShirtIndex;            // -1 = none
    public int Gender;                // 0 = male, 1 = female
    public int AccumulatedScore;
    public int Cups;

    public PlayerInfo()
    {
        PlayerId = Guid.NewGuid().ToString();
        PlayerName = "";
        ClassName = "";
        AvatarIndex = 0;
        BackgroundColorIndex = -1;
        HairIndex = 0;
        HatIndex = -1;
        GlassesIndex = -1;
        ShirtIndex = -1;
        Gender = 0;
        AccumulatedScore = 0;
        Cups = 0;
    }

    public PlayerInfo(string name, string className, int avatarIndex = 0)
    {
        PlayerId = Guid.NewGuid().ToString();
        PlayerName = name;
        ClassName = className;
        AvatarIndex = avatarIndex;
        BackgroundColorIndex = -1;
        HairIndex = 0;
        HatIndex = -1;
        GlassesIndex = -1;
        ShirtIndex = -1;
        Gender = 0;
        AccumulatedScore = 0;
        Cups = 0;
    }
}
