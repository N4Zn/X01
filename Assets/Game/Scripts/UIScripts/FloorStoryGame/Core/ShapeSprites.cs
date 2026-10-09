using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprite hình học sinh LÚC RUNTIME (trắng, tô màu bằng Image.color) — game không cần ảnh để chạy được:
/// tròn, vuông/chữ nhật, tam giác, sao, tim, thoi, trăng khuyết, hoa, thẻ bo góc (9-slice).
/// Cache tĩnh; khử răng cưa bằng lấy mẫu 3x3.
/// </summary>
public static class ShapeSprites
{
    const int N = 128;
    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Circle   => Get("circle",   (x, y) => x * x + y * y <= 0.95f * 0.95f);
    public static Sprite Square   => Get("square",   (x, y) => true);
    public static Sprite Diamond  => Get("diamond",  (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 0.95f);
    public static Sprite Triangle => Get("triangle", (x, y) =>
    {
        if (y < -0.85f || y > 0.9f) return false;
        float t = (0.9f - y) / 1.75f;              // 0 ở đỉnh → 1 ở đáy
        return Mathf.Abs(x) <= t * 0.95f;
    });
    public static Sprite Star => Get("star", (x, y) => InPolygon(StarPoly, x, y));
    public static Sprite Heart => Get("heart", (x, y) =>
    {
        float hx = x * 1.3f, hy = y * 1.3f + 0.1f;
        float a = hx * hx + hy * hy - 1f;
        return a * a * a - hx * hx * hy * hy * hy <= 0f;
    });
    public static Sprite Moon => Get("moon", (x, y) =>
    {
        bool outer = x * x + y * y <= 0.9f * 0.9f;
        float cx = x - 0.42f, cy = y - 0.18f;
        bool cut = cx * cx + cy * cy <= 0.78f * 0.78f;
        return outer && !cut;
    });
    public static Sprite Flower => Get("flower", (x, y) =>
    {
        if (x * x + y * y <= 0.27f * 0.27f) return true;
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 5f;
            float px = Mathf.Cos(a) * 0.58f, py = Mathf.Sin(a) * 0.58f;
            float dx = x - px, dy = y - py;
            if (dx * dx + dy * dy <= 0.36f * 0.36f) return true;
        }
        return false;
    });

    /// <summary>Thẻ bo góc, dùng Image.Type.Sliced (đã có border) — co giãn tự do không méo góc.</summary>
    public static Sprite RoundedRect
    {
        get
        {
            const string key = "rrect";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            const int n = 64;
            var tex = NewTexture(n);
            var px = new Color32[n * n];
            const float r = 22f;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int acc = 0;
                for (int sy = 0; sy < 3; sy++)
                for (int sx = 0; sx < 3; sx++)
                {
                    float fx = i + (sx + 0.5f) / 3f, fy = j + (sy + 0.5f) / 3f;
                    float qx = Mathf.Abs(fx - n * 0.5f) - (n * 0.5f - r);
                    float qy = Mathf.Abs(fy - n * 0.5f) - (n * 0.5f - r);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f);
                    if (d <= r) acc++;
                }
                px[j * n + i] = new Color32(255, 255, 255, (byte)(acc * 255 / 9));
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            Cache[key] = s;
            return s;
        }
    }

    /// <summary>Theo tên: circle/square/triangle/diamond/star/heart/moon/flower.</summary>
    public static Sprite ByName(string name)
    {
        switch (name)
        {
            case "circle":   return Circle;
            case "triangle": return Triangle;
            case "diamond":  return Diamond;
            case "star":     return Star;
            case "heart":    return Heart;
            case "moon":     return Moon;
            case "flower":   return Flower;
            default:         return Square;
        }
    }

    // ── nội bộ ───────────────────────────────────────────────────────────────

    static Vector2[] _starPoly;
    static Vector2[] StarPoly
    {
        get
        {
            if (_starPoly != null) return _starPoly;
            _starPoly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.98f : 0.4f;
                _starPoly[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return _starPoly;
        }
    }

    static bool InPolygon(Vector2[] poly, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > y) != (poly[j].y > y) &&
                x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    static Texture2D NewTexture(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

    static Sprite Get(string key, Func<float, float, bool> inside)
    {
        if (Cache.TryGetValue(key, out var s) && s != null) return s;
        var tex = NewTexture(N);
        var px = new Color32[N * N];
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            int acc = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
            {
                float x = ((i + (sx + 0.5f) / 3f) / N) * 2f - 1f;
                float y = ((j + (sy + 0.5f) / 3f) / N) * 2f - 1f;
                if (inside(x, y)) acc++;
            }
            px[j * N + i] = new Color32(255, 255, 255, (byte)(acc * 255 / 9));
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        Cache[key] = s;
        return s;
    }
}
