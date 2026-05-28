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
                audioSource.clip = clip;
                audioSource.Play();
                textSlot.SetActive(true);
                textLabel.text = "🔊 Nghe và chọn đáp án đúng";
                break;

            case QuestionMediaType.IconCompose:
                iconSlot.SetActive(true);

                // Xoá icon cũ
                foreach (Transform child in iconContainer) Destroy(child.gameObject);

                // Layout do GridLayoutGroup (+ ContentSizeFitter) đã được SceneBuilder tạo sẵn.
                foreach (var segment in q.questionMediaValue.Split(','))
                {
                    var data       = IconComposeData.Parse(segment);
                    var iconSprite = AssetOverrideLoader.GetSprite(data.spriteName);
                    if (iconSprite == null)
                        Debug.LogWarning($"[QuestionMediaDisplay] Icon sprite không tìm thấy: {data.spriteName}");

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
                }
                break;
        }
    }

    /// <summary>Ẩn nội dung câu hỏi — gọi từ Controller trước countdown.</summary>
    public void Hide() => HideAll();

    void HideAll()
    {
        textSlot.SetActive(false);
        imageSlot.SetActive(false);
        iconSlot.SetActive(false);
        if (audioSource != null) audioSource.Stop();
    }
}
