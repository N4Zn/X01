using System.Collections;
using UnityEngine;

/// <summary>Bay tới 1 ĐIỂM CỐ ĐỊNH (targetXPct/targetYPct, % trong nửa màn hình của chính item,
/// top-left origin — giống quy ước RectPct) rồi co nhỏ+mờ dần — khác FlyOffEffect (bay theo
/// hướng+khoảng cách, không cần biết điểm đến). Dùng khi cần set đúng vị trí "rổ"/ô đếm cụ thể.
/// Thao tác qua anchorMin/anchorMax (không phải anchoredPosition) vì slot do GenericGameController
/// tạo dùng anchor dạng "stretch" theo %, giữ nguyên kích thước trong lúc bay rồi mới co nhỏ qua
/// localScale ở cuối.</summary>
public class FlyToEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

        Vector2 startMin = target.anchorMin, startMax = target.anchorMax;
        float w = startMax.x - startMin.x, h = startMax.y - startMin.y;
        float targetX = p.targetXPct / 100f;
        float targetYTop = 1f - p.targetYPct / 100f; // targetYPct tính từ TRÊN xuống, anchor Unity tính từ DƯỚI lên
        Vector2 endMin = new Vector2(targetX - w / 2f, targetYTop - h / 2f);
        Vector2 endMax = new Vector2(targetX + w / 2f, targetYTop + h / 2f);

        Vector3 startScale = target.localScale;
        Vector3 endScale = startScale * Mathf.Clamp(p.scale <= 0f ? 0.3f : p.scale, 0.05f, 1f);
        float startAlpha = cg.alpha;

        float t = 0f;
        float dur = Mathf.Max(0.01f, p.duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            target.anchorMin = Vector2.Lerp(startMin, endMin, k);
            target.anchorMax = Vector2.Lerp(startMax, endMax, k);
            target.localScale = Vector3.Lerp(startScale, endScale, k);
            cg.alpha = Mathf.Lerp(startAlpha, 0f, k);
            yield return null;
        }
        if (target == null) yield break;
        target.anchorMin = endMin;
        target.anchorMax = endMax;
        target.localScale = endScale;
        cg.alpha = 0f;
    }
}
