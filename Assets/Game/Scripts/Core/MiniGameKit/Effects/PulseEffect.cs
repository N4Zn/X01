using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Hiệu ứng "to dần rồi mờ đi" (scale + fade) dùng chung cho mọi mini-game — star burst, coin
/// pop, badge nhấn mạnh... Trước đây mỗi game tự viết 1 coroutine riêng (vd
/// WhoIsItGameController.PlayStarBurst) — giờ chỉ cần gọi StartCoroutine(PulseEffect.ScaleFadePulse(...)).
///
/// Yêu cầu <paramref name="target"/> đã có GameObject; nếu muốn fade thật (không chỉ scale) thì
/// truyền thêm 1 CanvasGroup trên cùng GameObject (hoặc null nếu chỉ cần scale, không fade).
/// </summary>
public static class PulseEffect
{
    public static IEnumerator ScaleFadePulse(RectTransform target, CanvasGroup group,
        float fromScale, float toScale, float duration, Action onComplete = null)
    {
        if (target == null) yield break;

        target.gameObject.SetActive(true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            target.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, p);
            if (group != null) group.alpha = Mathf.Lerp(1f, 0f, p);
            yield return null;
        }
        target.gameObject.SetActive(false);
        onComplete?.Invoke();
    }
}
