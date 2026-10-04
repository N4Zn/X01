using System.Collections;
using UnityEngine;

/// <summary>Rung lắc tại chỗ theo trục X — dùng khi chạm sai (xem capabilities.json).
/// amplitudePct tính theo % chiều rộng nửa màn hình (áp vào RectTransform.rect.width của chính
/// target để không cần biết referenceWidth tổng).</summary>
public class ShakeEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        Vector2 original = target.anchoredPosition;
        float amp = target.rect.width * Mathf.Max(0f, p.amplitudePct) / 100f;
        float speed = Mathf.Max(1f, p.speed);
        float t = 0f;
        while (t < p.duration)
        {
            t += Time.deltaTime;
            float decay = 1f - (t / Mathf.Max(0.01f, p.duration));
            float x = Mathf.Sin(t * speed) * amp * decay;
            target.anchoredPosition = original + new Vector2(x, 0f);
            yield return null;
        }
        if (target != null) target.anchoredPosition = original;
    }
}
