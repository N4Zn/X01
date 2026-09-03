using UnityEngine;

/// <summary>
/// Singleton music/SFX manager. Persists across scenes (DontDestroyOnLoad).
/// BGM clips loaded from Resources/Audio/Music/, SFX from Resources/Audio/SFX/.
/// Respects GameSettings.Instance volume settings.
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    // Cached clips
    private AudioClip _mainMusic;
    private AudioClip _gameplayMusic;
    private AudioClip _solarSystemMusic;
    private AudioClip _questionSfx;
    private AudioClip _correctSfx;
    private AudioClip _wrongSfx;
    private AudioClip _explosionSfx;
    private AudioClip _meteorFallSfx;

    // Track which BGM is playing to avoid restart on same clip
    private string _currentBgmName;

    protected MusicManager() { }

    protected override void OnCreated()
    {
        // Create AudioSources
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;

        // Load clips from Resources
        _mainMusic = Resources.Load<AudioClip>("audio/Music/main");
        _gameplayMusic = Resources.Load<AudioClip>("audio/Music/gameplay");
        _solarSystemMusic = Resources.Load<AudioClip>("audio/Music/solar_system");
        _questionSfx  = Resources.Load<AudioClip>("Audio/SFX/question");
        _correctSfx   = Resources.Load<AudioClip>("Audio/SFX/correct");
        _wrongSfx     = Resources.Load<AudioClip>("Audio/SFX/wrong");
        _explosionSfx  = Resources.Load<AudioClip>("Audio/SFX/explosion");
        _meteorFallSfx = Resources.Load<AudioClip>("Audio/SFX/meteor_fall");

        ApplyVolumes();
    }

    /// <summary>
    /// Sync volume from GameSettings. Call after settings change.
    /// </summary>
    public void ApplyVolumes()
    {
        var gs = GameSettings.Instance;
        if (gs == null) return;

        _bgmSource.volume = gs.MusicVolume;
        _sfxSource.volume = gs.SfxVolume;
    }

    /// <summary>
    /// Temporarily multiply BGM volume (e.g. 0.5f for ducking)
    /// </summary>
    public void SetMusicVolumeMultiplier(float multiplier)
    {
        if (_bgmSource == null) return;
        float baseVol = GameSettings.Instance != null ? GameSettings.Instance.MusicVolume : 1f;
        _bgmSource.volume = baseVol * multiplier;
    }

    /// <summary>
    /// Set BGM playback pitch (e.g. LaneDashGame speeding up music as difficulty ramps up).
    /// Reset to 1 automatically next time a new BGM starts via PlayBgm, so it doesn't leak into
    /// whatever scene/game plays music next.
    /// </summary>
    public void SetMusicPitch(float pitch)
    {
        if (_bgmSource == null) return;
        _bgmSource.pitch = pitch;
    }

    // ===== BGM =====

    /// <summary>
    /// Phát nhạc nền theo BgmTrack khai báo trong GameRegistry.GameEntry.
    /// </summary>
    public void PlayBgm(GameRegistry.BgmTrack track)
    {
        switch (track)
        {
            case GameRegistry.BgmTrack.SolarSystem: PlaySolarSystemMusic(); break;
            case GameRegistry.BgmTrack.None:        StopMusic();            break;
            default:                                PlayGameplayMusic();    break;
        }
    }

    /// <summary>
    /// Play main/menu background music (HomeScene, TeamSelect, MenuScene).
    /// </summary>
    public void PlayMainMusic()
    {
        PlayBgm(_mainMusic, "main");
    }

    /// <summary>
    /// Play gameplay background music (AddUp, TrainPath, PathFinder).
    /// </summary>
    public void PlayGameplayMusic()
    {
        PlayBgm(_gameplayMusic, "gameplay");
    }

    /// <summary>
    /// Play Solar System scene background music.
    /// Clip: Assets/Resources/Audio/Music/solar_system.mp3 (or .ogg / .wav)
    /// </summary>
    public void PlaySolarSystemMusic()
    {
        PlayBgm(_solarSystemMusic, "solar_system");
    }

    /// <summary>
    /// Stop background music.
    /// </summary>
    public void StopMusic()
    {
        _bgmSource.Stop();
        _currentBgmName = null;
    }

    private void PlayBgm(AudioClip clip, string clipName)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[MusicManager] BGM clip '{clipName}' not found in Resources.");
            return;
        }

        // Don't restart if same clip is already playing
        if (_currentBgmName == clipName && _bgmSource.isPlaying) return;

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.volume = GameSettings.Instance != null ? GameSettings.Instance.MusicVolume : 1f;
        _bgmSource.pitch = 1f; // reset — tránh pitch tăng dần của LaneDashGame lọt sang scene sau
        _bgmSource.Play();
        _currentBgmName = clipName;
    }

    // ===== SFX =====

    /// <summary>
    /// Play question appear sound effect.
    /// </summary>
    public void PlayQuestionSfx()
    {
        PlaySfx(_questionSfx);
    }

    /// <summary>
    /// Play correct answer sound effect.
    /// </summary>
    public void PlayCorrectSfx()
    {
        PlaySfx(_correctSfx);
    }

    /// <summary>
    /// Play wrong answer sound effect.
    /// </summary>
    public void PlayWrongSfx()
    {
        PlaySfx(_wrongSfx);
    }

    /// <summary>
    /// Play explosion sound effect (meteor impact).
    /// </summary>
    public void PlayExplosionSfx()
    {
        PlaySfx(_explosionSfx);
    }

    /// <summary>
    /// Play meteor falling sound effect.
    /// </summary>
    public void PlayMeteorFallSfx()
    {
        PlaySfx(_meteorFallSfx);
    }

    public float MeteorFallClipLength => _meteorFallSfx != null ? _meteorFallSfx.length : 0f;

    /// <summary>Phát 1 AudioClip TÙY Ý (không phải SFX cố định đã cache sẵn) — dùng khi clip được
    /// load động lúc runtime (vd phát âm từng từ vựng theo QuestionData). Vẫn tôn trọng SfxVolume.</summary>
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        float vol = GameSettings.Instance != null ? GameSettings.Instance.SfxVolume : 1f;
        _sfxSource.PlayOneShot(clip, vol);
    }
}
