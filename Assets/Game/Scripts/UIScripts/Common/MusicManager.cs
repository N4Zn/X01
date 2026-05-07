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
    private AudioClip _questionSfx;
    private AudioClip _correctSfx;
    private AudioClip _wrongSfx;

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
        _mainMusic = Resources.Load<AudioClip>("Audio/Music/main");
        _gameplayMusic = Resources.Load<AudioClip>("Audio/Music/gameplay");
        _questionSfx = Resources.Load<AudioClip>("Audio/SFX/question");
        _correctSfx = Resources.Load<AudioClip>("Audio/SFX/correct");
        _wrongSfx = Resources.Load<AudioClip>("Audio/SFX/wrong");

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

    // ===== BGM =====

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

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        float vol = GameSettings.Instance != null ? GameSettings.Instance.SfxVolume : 1f;
        _sfxSource.PlayOneShot(clip, vol);
    }
}
