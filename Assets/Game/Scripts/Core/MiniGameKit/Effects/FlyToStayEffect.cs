using System.Collections;
using UnityEngine;

/// <summary>Bay tới 1 Ô ĐÍCH rồi Ở LẠI VĨNH VIỄN (không mờ dần, không bị huỷ khi effect kết thúc)
/// — khác hẳn FlyToEffect (bay tới rồi biến mất). Dùng khi đáp án đúng cần "đọng lại" thành 1 khu
/// vực mới trên màn hình (vd giỏ chứa chất đầy dần trong round — xem GenericGameLayout.collectSlots
/// + GenericGameController.ApplyCollectSlot, mỗi lần dùng hiệu ứng này sẽ bay vào Ô TIẾP THEO trong
/// mảng collectSlots, không phải 1 điểm cố định duy nhất).
///
/// targetWPct/targetHPct (nếu >0, GenericGameController set từ đúng kích thước ô trong collectSlots)
/// quyết định kích thước Ô ĐÍCH trực tiếp — khác FlyToEffect (luôn giữ nguyên kích thước item hiện
/// tại rồi nhân `scale`). targetWPct/targetHPct =0 (chưa cấu hình collectSlots) thì fallback đúng
/// kiểu FlyToEffect: giữ kích thước hiện tại, nhân `scale`.</summary>
public class FlyToStayEffect : IVisualEffect
{
    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;

        Vector2 startMin = target.anchorMin, startMax = target.anchorMax;
        float curW = startMax.x - startMin.x, curH = startMax.y - startMin.y;
        float w = p.targetWPct > 0f ? p.targetWPct / 100f : curW;
        float h = p.targetHPct > 0f ? p.targetHPct / 100f : curH;
        float targetX = p.targetXPct / 100f;
        float targetYTop = 1f - p.targetYPct / 100f; // targetYPct tính từ TRÊN xuống, anchor Unity tính từ DƯỚI lên
        Vector2 endMin = new Vector2(targetX - w / 2f, targetYTop - h / 2f);
        Vector2 endMax = new Vector2(targetX + w / 2f, targetYTop + h / 2f);

        Vector3 startScale = target.localScale;
        // targetWPct/targetHPct đã quyết định kích thước Ô ĐÍCH trực tiếp qua anchor — scale ở đây
        // chỉ còn ý nghĩa khi KHÔNG cấu hình collectSlots (p.targetWPct<=0, fallback kiểu FlyTo cũ).
        Vector3 endScale = p.targetWPct > 0f ? startScale : startScale * Mathf.Clamp(p.scale <= 0f ? 1f : p.scale, 0.05f, 2f);

        float t = 0f;
        float dur = Mathf.Max(0.01f, p.duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            target.anchorMin = Vector2.Lerp(startMin, endMin, k);
            target.anchorMax = Vector2.Lerp(startMax, endMax, k);
            target.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }
        if (target == null) yield break;
        target.anchorMin = endMin;
        target.anchorMax = endMax;
        target.localScale = endScale;
        // KHÔNG đụng CanvasGroup/alpha, KHÔNG huỷ — item ở lại NGUYÊN VẸN tại vị trí mới này cho
        // tới khi GenericGameController dọn ở đầu round mới (xem ResetCollectSlots).
    }
}
