using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper dùng chung cho FloatingItem, ButtonItem, MatchingItem.
///
/// CSV value là đường dẫn relative (không extension):
///   "TestTongHop/images/animals/cat"
///   "TestTongHop/audio/meow"
///
/// Load order: persistentDataPath override → Resources (xem AssetOverrideLoader).
///
/// IconCompose value: "path/to/sprite:count" — nhiều nhóm cách nhau bằng ','
///   "TestTongHop/images/animals/cat:5"
///   "TestTongHop/images/animals/cat:3,TestTongHop/images/animals/dog:2"
/// </summary>
public static class ItemMediaHelper
{
    public static void ApplyMedia(
        string          value,
        AnswerMediaType mediaType,
        GameObject      textSlot,
        TextMeshProUGUI textLabel,
        GameObject      imageSlot,
        Image           imageHolder,
        GameObject      iconSlot,
        Transform       iconContainer,
        GameObject      iconPrefab)   // có thể null — sẽ tự tạo GO nếu null
    {
        textSlot.SetActive(false);
        imageSlot.SetActive(false);
        iconSlot.SetActive(false);

        switch (mediaType)
        {
            case AnswerMediaType.Text:
                textSlot.SetActive(true);
                textLabel.text = value;
                break;

            case AnswerMediaType.Image:
                var sprite = AssetOverrideLoader.GetSprite(value);
                if (sprite == null)
                {
                    Debug.LogWarning($"[ItemMediaHelper] Không tìm thấy sprite: {value}");
                    textSlot.SetActive(true);
                    textLabel.text = value;
                    return;
                }
                imageSlot.SetActive(true);
                imageHolder.sprite = sprite;
                break;

            case AnswerMediaType.IconCompose:
                iconSlot.SetActive(true);

                // Xoá icon cũ
                foreach (Transform child in iconContainer)
                    Object.Destroy(child.gameObject);

                // Layout do GridLayoutGroup đã được cấu hình sẵn trong prefab/scene.
                // Mỗi segment: "path/to/sprite:count"
                foreach (var segment in value.Split(','))
                {
                    var data       = IconComposeData.Parse(segment);
                    var iconSprite = AssetOverrideLoader.GetSprite(data.spriteName);
                    if (iconSprite == null)
                        Debug.LogWarning($"[ItemMediaHelper] Icon sprite không tìm thấy: {data.spriteName}");

                    for (int i = 0; i < data.count; i++)
                    {
                        var go  = CreateIconGO(iconContainer, iconPrefab);
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

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo icon GameObject: dùng prefab nếu có, ngược lại tạo Image đơn giản bằng code.
    /// </summary>
    static GameObject CreateIconGO(Transform parent, GameObject prefab)
    {
        if (prefab != null)
            return Object.Instantiate(prefab, parent);

        var go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>();
        return go;
    }
}
