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
    private const string KeyFeedbackSpeed = "setting_feedback_speed";
    private const string KeyQuestionTimeout = "setting_question_timeout";

    // Values
    public float SfxVolume { get; set; }
    public float MusicVolume { get; set; }
    public int GameTime { get; set; }           // seconds: 60, 90, 120, or custom
    public FeedbackSpeed FeedbackSpeedSetting { get; set; }
    public int QuestionTimeout { get; set; }    // seconds per question: 10, 15, or 20

    protected override void OnCreated()
    {
        Load();
    }

    public void Load()
    {
        SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        GameTime = PlayerPrefs.GetInt(KeyGameTime, 100);
        FeedbackSpeedSetting = (FeedbackSpeed)PlayerPrefs.GetInt(KeyFeedbackSpeed, (int)FeedbackSpeed.Medium);
        QuestionTimeout = PlayerPrefs.GetInt(KeyQuestionTimeout, 15);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetInt(KeyGameTime, GameTime);
        PlayerPrefs.SetInt(KeyFeedbackSpeed, (int)FeedbackSpeedSetting);
        PlayerPrefs.SetInt(KeyQuestionTimeout, QuestionTimeout);
        PlayerPrefs.Save();
        Debug.Log("GameSettings: saved");
    }

    /// <summary>
    /// Get feedback delay in seconds based on speed setting.
    /// </summary>
    public float GetFeedbackDelay()
    {
        switch (FeedbackSpeedSetting)
        {
            case FeedbackSpeed.Fast: return 0.8f;
            case FeedbackSpeed.Medium: return 1.5f;
            case FeedbackSpeed.Slow: return 2.5f;
            default: return 1.5f;
        }
    }
}

public enum FeedbackSpeed
{
    Fast = 0,
    Medium = 1,
    Slow = 2
}
