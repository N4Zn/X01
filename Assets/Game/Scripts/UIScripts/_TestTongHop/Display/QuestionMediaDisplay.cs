using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hiển thị đề bài cho một người chơi.
///
/// Load order: persistentDataPath override → Resources (xem AssetOverrideLoader).
///   questionMediaType = Text   → value = "Mèo kêu gì?"
///   questionMediaType = Image  → value = "TestTongHop/images/animals/cat"
///   questionMediaType = Audio  → value = "TestTongHop/audio/cat_sound"
///
/// Audio loop: play → chờ hết clip → nghỉ repeatPauseSeconds → play lại (vô hạn cho đến khi Hide()).
/// </summary>
public class QuestionMediaDisplay : MonoBehaviour
{
    [Header("UI slots")]
    [SerializeField] GameObject      textSlot;
    [SerializeField] TextMeshProUGUI textLabel;

    [SerializeField] GameObject      imageSlot;
    [SerializeField] Image           imageHolder;

    [SerializeField] GameObject      iconSlot;
    [SerializeField] Transform       iconContainer;
    [SerializeField] GameObject      iconPrefab;

    [SerializeField] AudioSource     audioSource;

    [Header("Audio loop")]
    [SerializeField] float repeatPauseSeconds = 4f; // thời gian nghỉ giữa 2 lần play

    Coroutine _audioLoopCoroutine;

    public void Show(QuestionData q)
    {
        HideAll();
        switch (q.questionMediaType)
        {
            case QuestionMediaType.Text:
                textSlot.SetActive(true);
                textLabel.text = q.questionMediaValue;
                break;

            case QuestionMediaType.Image:
                var sprite = AssetOverrideLoader.GetSprite(q.questionMediaValue);
                if (sprite == null)
                {
                    Debug.LogWarning($"[QuestionMediaDisplay] Không tìm thấy sprite: {q.questionMediaValue}");
                    textSlot.SetActive(true);
                    textLabel.text = q.questionMediaValue;
                    break;
                }
                imageSlot.SetActive(true);
                imageHolder.sprite = sprite;
                break;

            case QuestionMediaType.Audio:
                var clip = AssetOverrideLoader.GetClip(q.questionMediaValue);
                if (clip == null)
                {
                    Debug.LogWarning($"[QuestionMediaDisplay] Không tìm thấy audio: {q.questionMediaValue}");
                    break;
                }
                textSlot.SetActive(true);
                textLabel.text = "🔊 Nghe và chọn đáp án đúng";
                // Bắt đầu vòng lặp: play → nghỉ repeatPauseSeconds → play lại
                if (_audioLoopCoroutine != null) StopCoroutine(_audioLoopCoroutine);
                _audioLoopCoroutine = StartCoroutine(AudioLoopRoutine(clip));
                break;

            case QuestionMediaType.IconCompose:
                iconSlot.SetActive(true);

                // Xoá icon cũ: unparent trước để GridLayout không tính vào,
                // sau đó Destroy (cuối frame). Tránh icon cũ/mới cùng hiển thị 1 frame.
                for (int ci = iconContainer.childCount - 1; ci >= 0; ci--)
                {
                    var child = iconContainer.GetChild(ci);
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }

                // Layout do GridLayoutGroup (+ ContentSizeFitter) đã được SceneBuilder tạo sẵn.
                int totalIcons = 0;
                foreach (var segment in q.questionMediaValue.Split(','))
                {
                    var data = IconComposeData.Parse(segment);

                    // Nếu spriteName kết thúc bằng "RANDOM" (vd: "RANDOM" hoặc "Counting/RANDOM")
                    // → chọn ngẫu nhiên 1 loài vật từ CountingAnimalPicker.
                    // Mỗi segment RANDOM nhận 1 con vật khác nhau (anti-repeat).
                    Sprite iconSprite = data.spriteName.EndsWith(
                            "RANDOM", System.StringComparison.OrdinalIgnoreCase)
                        ? CountingAnimalPicker.GetRandom()
                        : AssetOverrideLoader.GetSprite(data.spriteName);

                    if (iconSprite == null)
                        Debug.LogWarning($"[QuestionMediaDisplay] Icon sprite không tìm thấy: {data.spriteName}");

                    Debug.Log($"[C5:Display] q={q.id} | sprite={iconSprite?.name ?? "null"} × {data.count}");

                    for (int i = 0; i < data.count; i++)
                    {
                        GameObject go;
                        if (iconPrefab != null)
                        {
                            go = Instantiate(iconPrefab, iconContainer);
                        }
                        else
                        {
                            go = new GameObject("Icon", typeof(RectTransform));
                            go.transform.SetParent(iconContainer, false);
                            go.AddComponent<Image>();
                        }

                        var img = go.GetComponent<Image>();
                        if (img != null)
                        {
                            img.sprite         = iconSprite;
                            img.preserveAspect = true;
                            img.raycastTarget  = false;
                        }
                    }
                    totalIcons += data.count;
                }
                Debug.Log($"[C5:Display] q={q.id} | TỔNG hiển thị: {totalIcons} icons");
                break;
        }
    }

    /// <summary>Ẩn nội dung câu hỏi — gọi từ Controller trước countdown.</summary>
    public void Hide() => HideAll();

    /// <summary>Chỉ phát audio mà không hiện panel — dùng khi panel đã bị ẩn bởi display khác.</summary>
    public void PlayAudioOnly(QuestionData q)
    {
        if (q.questionMediaType != QuestionMediaType.Audio) return;
        var clip = AssetOverrideLoader.GetClip(q.questionMediaValue);
        if (clip == null)
        {
            Debug.LogWarning($"[QuestionMediaDisplay] Không tìm thấy audio: {q.questionMediaValue}");
            return;
        }
        if (_audioLoopCoroutine != null) StopCoroutine(_audioLoopCoroutine);
        _audioLoopCoroutine = StartCoroutine(AudioLoopRoutine(clip));
    }

    /// <summary>Dừng vòng lặp audio ngay lập tức — gọi khi người chơi trả lời đúng.</summary>
    public void StopAudio()
    {
        if (_audioLoopCoroutine != null) { StopCoroutine(_audioLoopCoroutine); _audioLoopCoroutine = null; }
        if (audioSource != null) audioSource.Stop();
    }

    void HideAll()
    {
        StopAudio();
        textSlot.SetActive(false);
        imageSlot.SetActive(false);
        iconSlot.SetActive(false);
    }

    // ── Audio loop ────────────────────────────────────────────────────────────

    /// <summary>
    /// Vòng lặp: play clip → chờ hết clip → nghỉ repeatPauseSeconds → play lại.
    /// Dừng khi Hide() hoặc StopAudio() được gọi.
    /// </summary>
    IEnumerator AudioLoopRoutine(AudioClip clip)
    {
        audioSource.clip = clip;
        while (true)
        {
            MusicManager.Instance?.SetMusicVolumeMultiplier(0.2f);  // duck BGM
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);            // chờ hết clip
            MusicManager.Instance?.SetMusicVolumeMultiplier(1f);    // restore BGM
            yield return new WaitForSeconds(repeatPauseSeconds);    // nghỉ 4s
        }
    }
}
