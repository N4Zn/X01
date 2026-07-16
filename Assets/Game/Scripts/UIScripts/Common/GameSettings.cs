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

    // Values
    public float SfxVolume { get; set; }
    public float MusicVolume { get; set; }
    public int GameTime { get; set; }           // seconds: 60, 90, 120, or custom
    public int QuestionTimeout { get; set; }    // seconds per question: 10, 15, or 20
    public float RoundEndDelay { get; set; }    // seconds delay between questions/rounds: 1-4

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
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetInt(KeyGameTime, GameTime);
        PlayerPrefs.SetInt(KeyQuestionTimeout, QuestionTimeout);
        PlayerPrefs.SetFloat(KeyRoundEndDelay, Mathf.Clamp(RoundEndDelay, 1f, 4f));
        PlayerPrefs.Save();
        Debug.Log("GameSettings: saved");
    }
}
