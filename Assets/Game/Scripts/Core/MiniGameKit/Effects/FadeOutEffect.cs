using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Mờ dần (alpha 1→0) tại chỗ, không di chuyển — dùng cho đáp án MultiSelect biến mất
/// êm sau khi chọn đúng, thay vì ẩn phựt (SetActive(false) ngay). Dùng CanvasGroup nếu có sẵn
/// trên target, tự thêm nếu chưa có (không phá layout, chỉ thêm component).</summary>
public class FadeOutEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
        float start = cg.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, p.duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, t / dur);
            yield return null;
        }
        if (cg != null) cg.alpha = 0f;
    }
}
