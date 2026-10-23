using System.Collections;
using UnityEngine;

/// <summary>
/// Giọng đọc đề CHUNG cho cả ván khi 2 bên chơi độc lập (Counting...): 1 file, 1 luồng phát — không để mỗi bên/mỗi round tự phát rồi đè nhau.
///
///   • Có ít nhất 1 bên đang hiện câu hỏi → phát (đầu ván / sau khi nghỉ xong).
///   • Phát TRỌN clip, kể cả khi round đổi giữa lúc đang đọc. Đọc xong → nghỉ <see cref="GameSettings.QuestionAudioRepeat"/> giây → đọc lại.
///   • Thời gian nghỉ là TỐI THIỂU: round mới bắt đầu giữa lúc nghỉ thì vẫn nghỉ cho đủ. Hết nghỉ mà CẢ 2 bên đều không còn câu hỏi
///     (đang icon đúng/sai, chờ vùng sạch, đếm "Next in") → chờ tới khi có bên hiện câu mới thì đọc NGAY (round mới).
///   • Một bên trả lời xong nhưng bên kia còn câu → vẫn đọc/lặp bình thường.
/// Timer dùng Time.deltaTime (có scale) nên Pause (timeScale 0) đứng yên theo.
/// </summary>
public class SharedPromptAudio : MonoBehaviour
{
    AudioSource _src;
    AudioClip _clip;
    readonly bool[] _active = new bool[2];
    Coroutine _loop;

    bool AnyActive => _active[0] || _active[1];

    /// <summary>Tạo nguồn phát riêng (không dùng AudioSource của display vì Hide() của display Stop() nó). volume/mixer lấy theo mẫu.</summary>
    public void Init(AudioSource template)
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.spatialBlend = 0f;
        if (template != null) { _src.volume = template.volume; _src.outputAudioMixerGroup = template.outputAudioMixerGroup; }
        _loop = StartCoroutine(Loop());
    }

    /// <summary>Bên <paramref name="side"/> (0 = trái, 1 = phải) vừa hiện câu hỏi; clip null = câu này không có giọng đọc.</summary>
    public void SideActive(int side, AudioClip clip)
    {
        if (side < 0 || side > 1) return;
        if (clip != null) _clip = clip;
        _active[side] = clip != null;
    }

    /// <summary>Bên <paramref name="side"/> hết câu hỏi (đã trả lời/ẩn). Không cắt tiếng đang đọc.</summary>
    public void SideIdle(int side)
    {
        if (side >= 0 && side <= 1) _active[side] = false;
    }

    /// <summary>Hết ván: dừng hẳn, cắt tiếng, trả nhạc nền.</summary>
    public void Shutdown()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        _active[0] = _active[1] = false;
        if (_src != null) _src.Stop();
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
    }

    void OnDestroy() => MusicManager.Instance?.SetMusicVolumeMultiplier(1f);

    IEnumerator Loop()
    {
        while (true)
        {
            while (!AnyActive || _clip == null) yield return null; // chờ có bên hiện câu hỏi (round mới)

            var clip = _clip;
            MusicManager.Instance?.SetMusicVolumeMultiplier(0.2f);   // hạ nhạc nền lúc đọc
            _src.clip = clip;
            _src.Play();
            for (float t = 0f; t < clip.length; t += Time.deltaTime) yield return null; // đọc trọn, không bị round đổi cắt ngang
            MusicManager.Instance?.SetMusicVolumeMultiplier(1f);

            // Nghỉ TỐI THIỂU N giây tính từ lúc đọc xong, bất kể round đổi hay không (round mới bắt đầu sớm hơn thì vẫn phải nghỉ đủ).
            // Hết nghỉ mà cả 2 bên đang chuyển round → vòng ngoài chờ tới khi có bên hiện câu mới rồi đọc ngay.
            float rest = GameSettings.Instance != null ? GameSettings.Instance.QuestionAudioRepeat : 4f;
            for (float t = 0f; t < rest; t += Time.deltaTime) yield return null;
        }
    }
}
