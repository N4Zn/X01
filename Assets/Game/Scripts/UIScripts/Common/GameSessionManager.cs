using System.Collections.Generic;
using UnityEngine;

public enum GameMode
{
    OneVsOne,
    Team
}

[System.Serializable]
public class PlayerData
{
    public string PlayerName;
    public int TeamIndex; // 0 = Team A, 1 = Team B
    public int CharacterIndex; // index of selected character (0-11)
    public int Score;

    public PlayerData(string name, int teamIndex = 0, int characterIndex = -1)
    {
        PlayerName = name;
        TeamIndex = teamIndex;
        CharacterIndex = characterIndex;
        Score = 0;
    }
}

public static class CharacterDatabase
{
    public static readonly string[] CharacterNames = new string[]
    {
        "Fox", "Rabbit", "Bear", "Cat",
        "Dog", "Owl", "Deer", "Penguin",
        "Lion", "Monkey", "Panda", "Dragon"
    };

    public static readonly Color[] CharacterColors = new Color[]
    {
        new Color(0.9f, 0.5f, 0.2f), new Color(0.8f, 0.7f, 0.6f), new Color(0.6f, 0.4f, 0.2f), new Color(0.7f, 0.6f, 0.8f),
        new Color(0.5f, 0.7f, 0.4f), new Color(0.4f, 0.5f, 0.7f), new Color(0.8f, 0.6f, 0.4f), new Color(0.3f, 0.3f, 0.4f),
        new Color(0.9f, 0.7f, 0.3f), new Color(0.7f, 0.5f, 0.3f), new Color(0.9f, 0.9f, 0.9f), new Color(0.5f, 0.8f, 0.5f)
    };
}

public class GameSessionManager : Singleton<GameSessionManager>
{
    public GameMode CurrentGameMode { get; set; }
    public List<PlayerData> Players { get; private set; }
    public string LastPlayedGame { get; set; }
    public int Player1FinalScore { get; set; }
    public int Player2FinalScore { get; set; }

    /// <summary>
    /// Tên entry game đã chọn từ GameRegistry (vd: "ChuCai", "AddNumber5").
    /// Set bởi MenuSceneController trước khi LoadScene.
    /// TongHop games: QuestionPool dùng để load CSV từ Resources/TongHop/{SelectedGameName}/
    /// AddNumber variants: Controller dùng để xác định maxSum.
    /// </summary>
    public string SelectedGameName { get; set; }

    /// <summary>Tên game để GHI LOG/REPORT (Sheets, PlayerRecognitionService, GameControlBridge)
    /// — ưu tiên SelectedGameName (tên variant cụ thể do ControlActivity chọn, vd "AddNumber5"),
    /// fallback về tên cố định (thường là tên scene) khi chưa có (vd test trực tiếp trong Editor,
    /// không qua ControlActivity). Dùng ở MỌI controller thay vì hardcode literal tên scene —
    /// nhiều game share chung 1 scene/class, hardcode sẽ ghi sai tên khi có > 1 variant.</summary>
    public static string ResolveActiveGameName(string fallback)
    {
        return Instance != null && !string.IsNullOrEmpty(Instance.SelectedGameName)
            ? Instance.SelectedGameName
            : fallback;
    }

    /// <summary>
    /// Full entry từ GameRegistry — chứa bgmTrack, backgroundSprite, pointsPerCorrect.
    /// Set cùng lúc với SelectedGameName bởi MenuSceneController.
    /// </summary>
    public GameRegistry.GameEntry SelectedEntry { get; set; }

    /// <summary>
    /// Calculated target score based on GameSettings (1 point per 5 seconds).
    /// </summary>
    public int TargetScore
    {
        get
        {
            if (GameSettings.Instance != null)
                return GameSettings.Instance.GameTime / 5;
            return 10; // Default fallback
        }
    }

    // New: team data from Scene #2
    public string BlueTeamName { get; set; }
    public string RedTeamName { get; set; }
    public List<PlayerInfo> BlueTeamPlayers { get; private set; }
    public List<PlayerInfo> RedTeamPlayers { get; private set; }

    protected GameSessionManager() { }

    protected override void OnCreated()
    {
        Players = new List<PlayerData>();
        BlueTeamPlayers = new List<PlayerInfo>();
        RedTeamPlayers = new List<PlayerInfo>();
        ResetSession();
    }

    public void SetupOneVsOne(string p1Name, string p2Name)
    {
        CurrentGameMode = GameMode.OneVsOne;
        Players.Clear();
        Players.Add(new PlayerData(p1Name, 0));
        Players.Add(new PlayerData(p2Name, 1));
    }

    public void SetupTeams(string teamAName, string teamBName, string p1Name, string p2Name)
    {
        CurrentGameMode = GameMode.Team;
        Players.Clear();
        Players.Add(new PlayerData(p1Name, 0));
        Players.Add(new PlayerData(p2Name, 1));
    }

    /// <summary>
    /// Setup from TeamSelectScene (Scene #2). Populates both new team data
    /// and legacy Players[0]/[1] for backward compatibility with game scenes.
    /// </summary>
    public void SetupFromTeamSelect(GameMode mode, string blueTeamName, List<PlayerInfo> bluePlayers,
        string redTeamName, List<PlayerInfo> redPlayers)
    {
        CurrentGameMode = mode;
        BlueTeamName = blueTeamName;
        RedTeamName = redTeamName;
        BlueTeamPlayers = new List<PlayerInfo>(bluePlayers);
        RedTeamPlayers = new List<PlayerInfo>(redPlayers);

        // Backward compat: populate Players[0] and Players[1] for existing game scenes
        Players.Clear();
        if (bluePlayers.Count > 0)
            Players.Add(new PlayerData(bluePlayers[0].PlayerName, 0, bluePlayers[0].AvatarIndex));
        if (redPlayers.Count > 0)
            Players.Add(new PlayerData(redPlayers[0].PlayerName, 1, redPlayers[0].AvatarIndex));
    }

    public void RecordScores(int p1Score, int p2Score)
    {
        Player1FinalScore = p1Score;
        Player2FinalScore = p2Score;
        if (Players.Count >= 2)
        {
            Players[0].Score = p1Score;
            Players[1].Score = p2Score;
        }

        // Điểm cuối cho màn Tổng kết trên ControlActivity: game cũ (không qua MiniGameControllerBase/
        // TestTongHop) không đẩy report liên tục nên trước đây Tổng kết luôn 0 – 0. Game mới sẽ đẩy
        // đè lại ngay sau đó bằng số của chính nó (Solo: bên phải = 0).
        var bridge = GameControlBridge.Instance;
        if (bridge != null)
        {
            bridge.PushReport(0, GetDisplayName1(), p1Score, GetDisplayName2(), p2Score);
            bridge.PushPlayerBreakdown(
                PlayerRecognitionService.Instance?.GetPlayerStats(0),
                PlayerRecognitionService.Instance?.GetPlayerStats(1));
        }
    }

    /// <summary>Đặt CurrentGameMode theo ô "Chơi theo đội / 1 vs 1" ở ControlActivity (GameSettings.TeamPlay).
    /// Gọi lúc nạp game và lúc bắt đầu ván; luồng TeamSelect cũ không còn dùng trên K02.</summary>
    public void ApplyPlayModeFromSettings()
    {
        bool team = GameSettings.Instance == null || GameSettings.Instance.TeamPlay;
        CurrentGameMode = team ? GameMode.Team : GameMode.OneVsOne;
    }

    public string GetPlayer1Name()
    {
        return Players.Count > 0 ? Players[0].PlayerName : "Player 1";
    }

    public string GetPlayer2Name()
    {
        return Players.Count > 1 ? Players[1].PlayerName : "Player 2";
    }

    /// <summary>
    /// Returns the player name only (no team prefix) — whoever PlayerRecognitionService last
    /// recognized standing in that slot (updates live as players swap in/out). In Team mode falls
    /// back to the team name while no player name is known. Use this in game HUDs.
    /// </summary>
    public string GetDisplayName1()
    {
        if (CurrentGameMode == GameMode.Team)
        {
            string player = Players.Count > 0 ? Players[0].PlayerName : null;
            return string.IsNullOrEmpty(player) ? GetTeamName1() : PlayerNameDisplay.Format(player);
        }
        return PlayerNameDisplay.Format(GetPlayer1Name());
    }

    public string GetDisplayName2()
    {
        if (CurrentGameMode == GameMode.Team)
        {
            string player = Players.Count > 1 ? Players[1].PlayerName : null;
            return string.IsNullOrEmpty(player) ? GetTeamName2() : PlayerNameDisplay.Format(player);
        }
        return PlayerNameDisplay.Format(GetPlayer2Name());
    }

    public string GetTeamName1() => !string.IsNullOrEmpty(BlueTeamName) ? BlueTeamName : "Blue";

    public string GetTeamName2() => !string.IsNullOrEmpty(RedTeamName) ? RedTeamName : "Red";

    public void ResetSession()
    {
        CurrentGameMode = GameMode.Team;
        if (Players == null) Players = new List<PlayerData>();
        Players.Clear();
        Players.Add(new PlayerData("Player 1", 0));
        Players.Add(new PlayerData("Player 2", 1));
        BlueTeamName = "Blue";
        RedTeamName = "Red";
        if (BlueTeamPlayers == null) BlueTeamPlayers = new List<PlayerInfo>();
        if (RedTeamPlayers == null) RedTeamPlayers = new List<PlayerInfo>();
        BlueTeamPlayers.Clear();
        RedTeamPlayers.Clear();
        LastPlayedGame = "";
        SelectedGameName = "";
        Player1FinalScore = 0;
        Player2FinalScore = 0;
    }
}
