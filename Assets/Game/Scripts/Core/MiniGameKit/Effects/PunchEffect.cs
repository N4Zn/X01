using System.Collections;
using UnityEngine;

/// <summary>Phóng to rồi co lại về đúng scale ban đầu — dùng khi chạm đúng (xem capabilities.json).</summary>
public class PunchEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        Vector3 original = target.localScale;
        Vector3 peak = original * Mathf.Max(1f, p.scale);
        float half = Mathf.Max(0.01f, p.duration) * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(original, peak, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(peak, original, t / half);
            yield return null;
        }
        if (target != null) target.localScale = original;
    }
}
