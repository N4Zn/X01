using System.Collections;
using UnityEngine;

/// <summary>Phóng to-nhỏ lặp lại liên tục (loops=-1 mặc định) — hiệu ứng "mời gọi" chạy TRƯỚC khi
/// học sinh chạm (vd round vừa bắt đầu). KHÁC các effect khác: chạy vô thời hạn, caller tự
/// StopCoroutine khi cần dừng (vd học sinh vừa chạm vào) — không tự biết khi nào nên dừng.</summary>
public class BreathingEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        Vector3 original = target.localScale;
        Vector3 peak = original * Mathf.Max(1f, p.scale);
        float half = Mathf.Max(0.05f, p.duration) * 0.5f;
        int remaining = p.loops == 0 ? -1 : p.loops; // JSON từ web không ghi loops → struct = 0 → coi là lặp vô hạn

        while (remaining < 0 || remaining-- > 0)
        {
            float t = 0f;
            while (t < half) { t += Time.deltaTime; target.localScale = Vector3.Lerp(original, peak, t / half); yield return null; }
            t = 0f;
            while (t < half) { t += Time.deltaTime; target.localScale = Vector3.Lerp(peak, original, t / half); yield return null; }
            if (target == null) yield break;
        }
        if (target != null) target.localScale = original;
    }
}
