using UnityEngine;

/// <summary>
/// Persistent game settings. Nguồn thật giờ là màn "Cài đặt" (icon ⚙) trên ControlActivity
/// (display 0, native Android) — teacher chỉnh ở đó, KHÔNG có UI nào trong Unity để sửa nữa
/// (MenuScene cũ đã bị Track A thay bằng ControlActivity). Đọc từ ĐÚNG SharedPreferences file
/// "game_settings" mà ControlActivity.showSettingsDialog() ghi vào, qua AndroidJavaObject —
/// 2 module khác process/ngôn ngữ, không đọc chung PlayerPrefs của Unity được, nên thống nhất
/// qua 1 file SharedPreferences chung thay vì 2 nguồn lệch nhau.
/// PlayerPrefs vẫn giữ làm fallback cho Editor/non-Android (không có ControlActivity để đọc).
/// Accessible globally via GameSettings.Instance.
/// Settings: SFX volume, Music volume, Game time, Feedback speed.
/// </summary>
public class GameSettings : Singleton<GameSettings>
{
    private const string PrefsFileName = "game_settings";

    // Keys — PHẢI khớp nguyên văn với ControlActivity.showSettingsDialog() (Java) phía ghi.
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
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var prefs = activity.Call<AndroidJavaObject>("getSharedPreferences", PrefsFileName, 0 /*MODE_PRIVATE*/);

            SfxVolume = prefs.Call<float>("getFloat", KeySfxVolume, 1f);
            MusicVolume = prefs.Call<float>("getFloat", KeyMusicVolume, 1f);
            GameTime = prefs.Call<int>("getInt", KeyGameTime, 100);
            QuestionTimeout = prefs.Call<int>("getInt", KeyQuestionTimeout, 15);
            RoundEndDelay = Mathf.Clamp(prefs.Call<float>("getFloat", KeyRoundEndDelay, 2f), 1f, 4f);
            Debug.Log($"[GameSettings] Đọc từ SharedPreferences '{PrefsFileName}' — GameTime={GameTime} QuestionTimeout={QuestionTimeout} RoundEndDelay={RoundEndDelay}");
            return;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameSettings] Đọc SharedPreferences lỗi (ControlActivity có thể chưa chạy) — fallback PlayerPrefs: {e.Message}");
        }
#endif
        SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        GameTime = PlayerPrefs.GetInt(KeyGameTime, 100);
        QuestionTimeout = PlayerPrefs.GetInt(KeyQuestionTimeout, 15);
        RoundEndDelay = Mathf.Clamp(PlayerPrefs.GetFloat(KeyRoundEndDelay, 2f), 1f, 4f);
    }

    /// <summary>Unity KHÔNG còn ghi settings nữa (chỉ đọc) — giữ Save() cho tương thích ngược
    /// (Editor/non-Android), nhưng đường ghi thật duy nhất giờ là ControlActivity.showSettingsDialog().</summary>
    public void Save()
    {
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetInt(KeyGameTime, GameTime);
        PlayerPrefs.SetInt(KeyQuestionTimeout, QuestionTimeout);
        PlayerPrefs.SetFloat(KeyRoundEndDelay, Mathf.Clamp(RoundEndDelay, 1f, 4f));
        PlayerPrefs.Save();
        Debug.Log("GameSettings: saved (PlayerPrefs, Editor/non-Android fallback only)");
    }
}
