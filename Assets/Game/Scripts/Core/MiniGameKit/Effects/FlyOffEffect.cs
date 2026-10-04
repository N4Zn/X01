using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bay theo 1 hướng+khoảng cách cố định rồi thu nhỏ+mờ dần — mô phỏng "bay vào giỏ"
/// (xem FlyToBasket cũ của DemQuaController) nhưng TỔNG QUÁT: không cần biết toạ độ 1 "rổ" cụ
/// thể nào, chỉ cần hướng (angleDeg, 90 = thẳng lên trên) + khoảng cách (distancePct, % chiều
/// rộng nửa màn hình của chính target) — đủ dùng cho "quả bay lên trên", "thẻ bay sang trái", ...
/// Game nào cần bay ĐẾN 1 điểm cụ thể (vd rổ cố định không nằm theo hướng/khoảng cách suy ra
/// được) vẫn nên tự viết hiệu ứng riêng như cũ, không ép vào effect tổng quát này.</summary>
public class FlyOffEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

        Vector2 startPos = target.anchoredPosition;
        Vector3 startScale = target.localScale;
        float rad = p.angleDeg * Mathf.Deg2Rad;
        float dist = target.rect.width * Mathf.Max(0f, p.distancePct) / 100f;
        Vector2 endPos = startPos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dist;
        Vector3 endScale = startScale * Mathf.Clamp(p.scale <= 0f ? 0.4f : p.scale, 0.05f, 1f);
        float startAlpha = cg.alpha;

        float t = 0f;
        float dur = Mathf.Max(0.01f, p.duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            target.anchoredPosition = Vector2.Lerp(startPos, endPos, k);
            target.localScale = Vector3.Lerp(startScale, endScale, k);
            cg.alpha = Mathf.Lerp(startAlpha, 0f, k);
            yield return null;
        }
        if (target == null) yield break;
        target.anchoredPosition = endPos;
        target.localScale = endScale;
        cg.alpha = 0f;
    }
}
