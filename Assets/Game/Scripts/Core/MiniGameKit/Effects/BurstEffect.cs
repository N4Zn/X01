using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Nổ tung (web v4.19): ô phồng nhẹ rồi biến mất ngay, chớp sáng tròn + các mảnh cùng màu ảnh của ô văng ra, xoay, rơi nhẹ rồi mờ.
/// Mảnh nằm ở lớp riêng (anh em của target, cùng cha) nên vẫn chạy tiếp khi bản sao (clone) của ô bị huỷ giữa chừng.
/// Màu chủ đạo lấy từ sprite của ô (bỏ pixel xám/trắng); không đọc được → vàng.</summary>
public class BurstEffect : IVisualEffect
{
    static readonly Dictionary<Sprite, Color> ColorCache = new();
    static readonly Color Fallback = new Color(1f, 0.8f, 0.2f, 1f);

    public IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner)
    {
        if (target == null) yield break;
        var host = target.parent as RectTransform;
        if (host == null) yield break;

        float dur = p.duration > 0f ? Mathf.Max(0.2f, p.duration) : 0.55f;
        int n = p.pieces > 0f ? Mathf.Max(4, Mathf.RoundToInt(p.pieces)) : 14;
        float spreadK = p.spread > 0f ? p.spread : 0.9f;

        // Tâm + cỡ của ô trong toạ độ của host (tính trước: target có thể bị huỷ ngay sau khi hiệu ứng bắt đầu).
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 lo = host.InverseTransformPoint(corners[0]), hi = host.InverseTransformPoint(corners[2]);
        Vector2 center = (lo + hi) * 0.5f - host.rect.center;
        float size = Mathf.Min(Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y));
        if (size < 1f) yield break;
        Color col = ColorOf(target);

        var layer = new GameObject("BurstLayer", typeof(RectTransform));
        var lrt = (RectTransform)layer.transform;
        lrt.SetParent(host, false);
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = Vector2.zero;
        lrt.anchoredPosition = Vector2.zero;
        var parentCanvas = target.GetComponentInParent<Canvas>();
        var cv = layer.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 1;

        var circle = RuntimeShapeSprites.GetCircle();
        var flash = NewPiece(lrt, circle, center, new Vector2(size, size), Color.white);
        var pieces = new List<(RectTransform rt, Image img, Vector2 dir, float rot, float life, bool light)>();
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f + (Random.value - 0.5f) * 0.5f;
            float dist = spreadK * size * (0.55f + Random.value * 0.6f);
            float w = size * (0.11f + Random.value * 0.1f), h = w * (0.6f + Random.value * 0.9f);
            bool light = Random.value < 0.25f;
            var img = NewPiece(lrt, circle, center, new Vector2(w, h), light ? Color.white : col);
            pieces.Add((img.rectTransform, img, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist, (Random.value - 0.5f) * 720f, dur * (0.8f + Random.value * 0.35f), light));
        }

        // Ô: phồng 1 chút rồi tắt (alpha 0, không SetActive — caller tự quản ẩn/huỷ), trả scale để vòng sau dùng lại được.
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
        Vector3 original = target.localScale;
        target.localScale = original * 1.18f;

        float t = 0f;
        float maxLife = dur;
        foreach (var pc in pieces) maxLife = Mathf.Max(maxLife, pc.life);
        bool hidden = false;
        while (t < maxLife)
        {
            t += Time.deltaTime;
            if (!hidden && t >= 0.06f)
            {
                hidden = true;
                if (target != null) { cg.alpha = 0f; target.localScale = original; }
            }
            // Chớp sáng: scale .35→1.45, mờ dần trong nửa đầu.
            float ft = Mathf.Clamp01(t / (dur * 0.5f));
            if (flash != null)
            {
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, ft);
                flash.color = new Color(1f, 1f, 1f, 0.95f * (1f - ft));
            }
            foreach (var pc in pieces)
            {
                if (pc.rt == null) continue;
                float u = Mathf.Clamp01(t / pc.life);
                float move = 1f - Mathf.Pow(1f - Mathf.Min(1f, u / 0.55f), 3f);   // văng nhanh rồi chậm
                Vector2 off = pc.dir * (u < 0.55f ? 0.8f * move : Mathf.Lerp(0.8f, 1f, (u - 0.55f) / 0.45f));
                off.y -= size * 0.35f * Mathf.Clamp01((u - 0.55f) / 0.45f);        // rơi nhẹ ở cuối
                pc.rt.anchoredPosition = center + off;
                pc.rt.localRotation = Quaternion.Euler(0f, 0f, pc.rot * u);
                pc.rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((u - 0.55f) / 0.45f));
                var c = pc.light ? Color.white : col;
                c.a = u < 0.55f ? 1f : 1f - (u - 0.55f) / 0.45f;
                pc.img.color = c;
            }
            yield return null;
        }
        if (target != null) { cg.alpha = 0f; target.localScale = original; }
        if (layer != null) Object.Destroy(layer);
    }

    static Image NewPiece(RectTransform parent, Sprite sprite, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject("p", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Màu trung bình các pixel "có màu" (không trong suốt, không xám/trắng) của sprite đầu tiên tìm thấy dưới target.</summary>
    static Color ColorOf(RectTransform target)
    {
        var img = target.GetComponentInChildren<Image>(true);
        var sp = img != null ? img.sprite : null;
        if (sp == null || sp.texture == null) return Fallback;
        if (ColorCache.TryGetValue(sp, out var cached)) return cached;
        Color result = Fallback;
        RenderTexture rt = null;
        var prev = RenderTexture.active;
        try
        {
            // Blit qua RenderTexture: đọc được cả texture không bật Read/Write; chỉ lấy đúng vùng sprite trong atlas.
            const int S = 24;
            rt = RenderTexture.GetTemporary(S, S, 0, RenderTextureFormat.ARGB32);
            var tex = sp.texture;
            var r = sp.textureRect;
            Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
            RenderTexture.active = rt;
            var read = new Texture2D(S, S, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, S, S), 0, 0);
            var px = read.GetPixels32();
            Object.Destroy(read);
            float rs = 0, gs = 0, bs = 0; int cnt = 0;
            foreach (var q in px)
            {
                if (q.a < 160) continue;
                int mx = Mathf.Max(q.r, q.g, q.b), mn = Mathf.Min(q.r, q.g, q.b);
                if (mx - mn < 40 || (mx > 245 && mn > 200)) continue;   // bỏ viền/chữ trắng/xám
                rs += q.r; gs += q.g; bs += q.b; cnt++;
            }
            if (cnt > 0) result = new Color(rs / cnt / 255f, gs / cnt / 255f, bs / cnt / 255f, 1f);
        }
        catch (System.Exception) { /* giữ Fallback */ }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
        ColorCache[sp] = result;
        return result;
    }
}
