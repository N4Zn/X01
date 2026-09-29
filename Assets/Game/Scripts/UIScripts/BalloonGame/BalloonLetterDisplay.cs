using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hiển thị trung tâm: chữ cái hiện tại + fill bar + audio loop.
/// popsRequired đọc từ BalloonGameConfig.
/// </summary>
public class BalloonLetterDisplay : MonoBehaviour
{
    [Header("Letter + fill")]
    [SerializeField] Image           letterMask;
    [SerializeField] Image           fillImage;
    [SerializeField] TextMeshProUGUI letterLabel;

    [Header("Audio")]
    [SerializeField] AudioSource audioSrc;

    [SerializeField] BalloonGameConfig config;

    int _popsLeft;
    int _popsRight;
    Coroutine _audioRoutine;

    public int  TotalPops  => _popsLeft + _popsRight;
    public bool IsComplete => TotalPops >= config.popsRequired;

    static readonly Color TrackBg   = new Color(0.08f, 0.10f, 0.20f, 0.92f);
    static readonly Color FillTrack = new Color(0.25f, 0.80f, 1.00f, 1.00f);

    // ── Public API ─────────────────────────────────────────────────────────────

    void ResolveConfig()
    {
        if (config == null)
            config = UnityEngine.Resources.Load<BalloonGameConfig>("GameConfig/BalloonGameConfig");
    }

    public void ShowRound(string letter, Sprite letterSprite, AudioClip audio)
    {
        ResolveConfig();
        _popsLeft  = 0;
        _popsRight = 0;

        if (letterMask != null)
        {
            letterMask.sprite  = letterSprite;
            letterMask.enabled = true;
            letterMask.color   = letterSprite != null ? Color.white : TrackBg;
        }
        if (fillImage != null)
        {
            fillImage.color      = FillTrack;
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(true);
        }
        if (letterLabel != null)
        {
            letterLabel.text    = letter;
            letterLabel.enabled = true;
        }

        gameObject.SetActive(true);
        PlayAudio(audio);
    }

    public void RegisterPop(Team team)
    {
        if (team == Team.Left) _popsLeft++;
        else                   _popsRight++;

        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01((float)TotalPops / config.popsRequired);
    }

    public (int left, int right) GetPops() => (_popsLeft, _popsRight);

    public void StopAudio()
    {
        if (_audioRoutine != null) { StopCoroutine(_audioRoutine); _audioRoutine = null; }
        if (audioSrc != null) audioSrc.Stop();
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
    }

    public void Hide()
    {
        StopAudio();
        gameObject.SetActive(false);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    void PlayAudio(AudioClip clip)
    {
        if (audioSrc == null || clip == null) return;
        if (_audioRoutine != null) StopCoroutine(_audioRoutine);
        _audioRoutine = StartCoroutine(AudioRepeatRoutine(clip));
    }

    IEnumerator AudioRepeatRoutine(AudioClip clip)
    {
        ResolveConfig();
        float pauseBetween = config != null ? config.audioInterval : 4f;

        while (true)
        {
            MusicManager.Instance?.SetMusicVolumeMultiplier(0.5f);
            audioSrc.loop = false;
            audioSrc.clip = clip;
            audioSrc.Play();
            yield return new WaitForSeconds(clip.length);
            MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
            yield return new WaitForSeconds(pauseBetween);
        }
    }
}
