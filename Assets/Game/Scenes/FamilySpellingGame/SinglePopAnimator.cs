using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn script này vào object "QuestionImage" (tấm hình minh họa trên đầu).
/// Cho object đó nảy vào (pop-in) mỗi khi có câu hỏi mới, giống hiệu ứng của các ô đáp án.
///
/// Cách dùng:
/// - Trong FamilySpellingGameController, thêm biến tham chiếu tới script này,
///   rồi gọi PlayAppear() ngay sau khi set sprite mới cho questionImage.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SinglePopAnimator : MonoBehaviour
{
    [Header("Thông số hiệu ứng")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private float overshoot = 1.12f; // 1 = không phóng quá đà, 1.12 = phóng thêm 12%

    private RectTransform rt;
    private Vector3 originalScale;
    private Coroutine running;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        originalScale = rt.localScale;
    }

    /// <summary>Gọi hàm này mỗi khi ảnh vừa được đổi sang câu hỏi mới.</summary>
    public void PlayAppear()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        rt.localScale = Vector3.zero;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popDuration);
            float e = EaseOutBack(p);
            rt.localScale = originalScale * Mathf.LerpUnclamped(0f, overshoot, e);
            yield return null;
        }

        rt.localScale = originalScale;
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
