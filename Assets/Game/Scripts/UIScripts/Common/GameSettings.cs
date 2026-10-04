using UnityEngine;

/// <summary>
/// Persistent game settings — saved via PlayerPrefs.
/// Accessible globally via GameSettings.Instance.
/// Settings: SFX volume, Music volume, Game time, Feedback speed.
/// </summary>
public class GameSettings : Singleton<GameSettings>
{
    // Keys
    private const string KeySfxVolume = "setting_sfx_volume";
    private const string KeyMusicVolume = "setting_music_volume";
    private const string KeyGameTime = "setting_game_time";
    private const string KeyQuestionTimeout = "setting_question_timeout";
    private const string KeyRoundEndDelay = "setting_round_end_delay";
    private const string KeyFlowSpeed = "setting_flow_speed";
    private const string KeyWaitForClear = "setting_wait_for_clear";

    // Values
    public float SfxVolume { get; set; }
    public float MusicVolume { get; set; }
    public int GameTime { get; set; }           // seconds: 60, 90, 120, or custom
    /// <summary>DEPRECATED — thời gian mỗi câu giờ KHÔNG giới hạn (yêu cầu 2026-10-03, Control panel
    /// không còn tham số này). Field giữ lại chỉ để code cũ còn compile; xem
    /// handoff/2026-10-04-game-settings.md việc B7 (gỡ dần ở từng controller).</summary>
    public int QuestionTimeout { get; set; }
    public float RoundEndDelay { get; set; }    // seconds delay between questions/rounds: 1-4

    public const float FlowSpeedMin = 0.1f, FlowSpeedMax = 5f;

    /// <summary>Hệ số tốc độ cho game có spawn liên tục (0.1–5x, 1 = nguyên bản). Nhân vào speed
    /// gốc; xem SpawnFlowDisplay.</summary>
    public float FlowSpeed { get; set; } = 1f;

    /// <summary>Bật = chờ vùng chơi sạch (không còn ai đứng/chạm) mới chuyển round. Mặc định BẬT.
    /// OR với cờ riêng từng scene (MiniGameControllerBase.waitForZoneClearBeforeCountdown).</summary>
    public bool WaitForClear { get; set; } = true;

    protected override void OnCreated()
    {
        Load();
    }

    public void Load()
    {
        SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        GameTime = PlayerPrefs.GetInt(KeyGameTime, 100);
        QuestionTimeout = PlayerPrefs.GetInt(KeyQuestionTimeout, 15);
        RoundEndDelay = Mathf.Clamp(PlayerPrefs.GetFloat(KeyRoundEndDelay, 2f), 1f, 4f);
        FlowSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(KeyFlowSpeed, 1f), FlowSpeedMin, FlowSpeedMax);
        WaitForClear = PlayerPrefs.GetInt(KeyWaitForClear, 1) != 0;
    }

    // JSON do ControlActivity (Java) gửi sang — tên field PHẢI khớp SettingsStore.java.
    [System.Serializable]
    class Dto
    {
        public float musicVolume = -1f, sfxVolume = -1f, roundEndDelay = -1f, flowSpeed = -1f;
        public int gameTime = -1;
        public int waitForClear = -1; // -1 = không đổi, 0/1 = tắt/bật
    }

    /// <summary>Áp JSON từ ControlActivity: field vắng/âm = giữ nguyên. Lưu PlayerPrefs + áp âm lượng
    /// ngay (đang chơi vẫn nghe thay đổi). Game đang chạy đọc lại GameTime/RoundEndDelay ở lần dùng kế.</summary>
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        Dto d;
        try { d = JsonUtility.FromJson<Dto>(json); }
        catch (System.Exception e) { Debug.LogWarning("GameSettings.ApplyJson lỗi: " + e.Message); return; }
        if (d == null) return;
        if (d.musicVolume >= 0f) MusicVolume = Mathf.Clamp01(d.musicVolume);
        if (d.sfxVolume >= 0f) SfxVolume = Mathf.Clamp01(d.sfxVolume);
        if (d.gameTime > 0) GameTime = d.gameTime;
        if (d.roundEndDelay >= 0f) RoundEndDelay = Mathf.Clamp(d.roundEndDelay, 1f, 4f);
        if (d.flowSpeed > 0f) FlowSpeed = Mathf.Clamp(d.flowSpeed, FlowSpeedMin, FlowSpeedMax);
        if (d.waitForClear >= 0) WaitForClear = d.waitForClear != 0;
        Save();
        if (MusicManager.Instance != null) MusicManager.Instance.ApplyVolumes();
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetInt(KeyGameTime, GameTime);
        PlayerPrefs.SetInt(KeyQuestionTimeout, QuestionTimeout);
        PlayerPrefs.SetFloat(KeyRoundEndDelay, Mathf.Clamp(RoundEndDelay, 1f, 4f));
        PlayerPrefs.SetFloat(KeyFlowSpeed, FlowSpeed);
        PlayerPrefs.SetInt(KeyWaitForClear, WaitForClear ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("GameSettings: saved");
    }
}
