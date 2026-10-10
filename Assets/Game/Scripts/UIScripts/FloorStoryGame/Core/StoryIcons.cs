using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Biểu tượng đơn giản ghép từ hình sinh runtime (không cần ảnh): mặt trời, mặt trăng, hoa, sao, tim, bánh, kem, cốc sữa.
/// Có màu đặc trưng để bé 3 tuổi nhận ra. Dùng cho game trí nhớ (chuỗi hình, lật thẻ).
/// </summary>
public static class StoryIcons
{
    public static readonly string[] Keys = { "sun", "moon", "flower", "star", "heart", "cake", "icecream", "cup" };

    public static string VietnameseName(string key)
    {
        switch (key)
        {
            case "sun": return "Mặt trời";
            case "moon": return "Mặt trăng";
            case "flower": return "Bông hoa";
            case "star": return "Ngôi sao";
            case "heart": return "Trái tim";
            case "cake": return "Cái bánh";
            case "icecream": return "Cây kem";
            case "cup": return "Cốc sữa";
            default: return key;
        }
    }

    /// <summary>Dựng icon vào `parent`, hộp vuông cạnh `size`, tâm tại `pos`. Trả về RectTransform gốc (không bắt chạm).</summary>
    public static RectTransform Build(Transform parent, string key, Vector2 pos, float size)
    {
        var rt = StoryUI.Rect(parent, "Icon_" + key, pos, Vector2.one * size);
        float s = size;
        var custom = StoryUI.Load("Story/Icons/" + key);   // ảnh thay thế từ gói zip (tuỳ chọn)
        if (custom != null)
        {
            StoryUI.Pic(rt, "p", custom, Color.white, Vector2.zero, Vector2.one * s);
            return rt;
        }
        switch (key)
        {
            case "sun":
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    var ray = Part(rt, ShapeSprites.RoundedRect, "#FFB300", new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.41f * s, new Vector2(0.11f * s, 0.24f * s));
                    ray.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
                }
                Part(rt, ShapeSprites.Circle, "#FFD23F", Vector2.zero, Vector2.one * 0.58f * s);
                Part(rt, ShapeSprites.Circle, "#7A4B00", new Vector2(-0.08f * s, 0.04f * s), Vector2.one * 0.05f * s);
                Part(rt, ShapeSprites.Circle, "#7A4B00", new Vector2(0.08f * s, 0.04f * s), Vector2.one * 0.05f * s);
                break;
            case "moon":
                Part(rt, ShapeSprites.Moon, "#FFE066", Vector2.zero, Vector2.one * 0.92f * s);
                Part(rt, ShapeSprites.Star, "#FFF3B0", new Vector2(0.28f * s, 0.26f * s), Vector2.one * 0.16f * s);
                break;
            case "flower":
                Part(rt, ShapeSprites.Flower, "#FF6FB5", Vector2.zero, Vector2.one * 0.92f * s);
                Part(rt, ShapeSprites.Circle, "#FFD23F", Vector2.zero, Vector2.one * 0.26f * s);
                break;
            case "star":
                Part(rt, ShapeSprites.Star, "#FFC107", Vector2.zero, Vector2.one * 0.92f * s);
                break;
            case "heart":
                Part(rt, ShapeSprites.Heart, "#F44336", Vector2.zero, Vector2.one * 0.88f * s);
                break;
            case "cake":
                Part(rt, ShapeSprites.RoundedRect, "#E0E0E0", new Vector2(0, -0.38f * s), new Vector2(0.98f * s, 0.1f * s));
                Part(rt, ShapeSprites.RoundedRect, "#F7A1C4", new Vector2(0, -0.17f * s), new Vector2(0.84f * s, 0.36f * s));
                Part(rt, ShapeSprites.RoundedRect, "#FFF3E0", new Vector2(0, 0.10f * s), new Vector2(0.64f * s, 0.28f * s));
                Part(rt, ShapeSprites.Square, "#4DA6FF", new Vector2(0, 0.31f * s), new Vector2(0.07f * s, 0.18f * s));
                Part(rt, ShapeSprites.Circle, "#FF8F00", new Vector2(0, 0.44f * s), new Vector2(0.1f * s, 0.14f * s));
                break;
            case "icecream":
                var cone = Part(rt, ShapeSprites.Triangle, "#E0A458", new Vector2(0, -0.22f * s), new Vector2(0.46f * s, 0.6f * s));
                cone.localRotation = Quaternion.Euler(0, 0, 180f);
                Part(rt, ShapeSprites.Circle, "#FF8FB3", new Vector2(0, 0.08f * s), Vector2.one * 0.54f * s);
                Part(rt, ShapeSprites.Circle, "#8D5A3B", new Vector2(0, 0.30f * s), Vector2.one * 0.38f * s);
                Part(rt, ShapeSprites.Circle, "#E53935", new Vector2(0.02f * s, 0.47f * s), Vector2.one * 0.1f * s);
                break;
            case "cup":
                Part(rt, ShapeSprites.RoundedRect, "#5DADE2", new Vector2(-0.04f * s, -0.04f * s), new Vector2(0.56f * s, 0.64f * s));
                Part(rt, ShapeSprites.Square, "#FFFFFF", new Vector2(-0.04f * s, 0.0f), new Vector2(0.56f * s, 0.1f * s));
                Part(rt, ShapeSprites.Square, "#5DADE2", new Vector2(0.34f * s, 0.0f), new Vector2(0.1f * s, 0.34f * s));
                Part(rt, ShapeSprites.Square, "#5DADE2", new Vector2(0.28f * s, 0.15f * s), new Vector2(0.16f * s, 0.09f * s));
                Part(rt, ShapeSprites.Square, "#5DADE2", new Vector2(0.28f * s, -0.15f * s), new Vector2(0.16f * s, 0.09f * s));
                Part(rt, ShapeSprites.Circle, "#FFFFFF", new Vector2(-0.04f * s, 0.30f * s), new Vector2(0.48f * s, 0.1f * s)).GetComponent<Image>().preserveAspect = false;
                break;
            default:
                Part(rt, ShapeSprites.Circle, "#CCCCCC", Vector2.zero, Vector2.one * 0.8f * s);
                break;
        }
        return rt;
    }

    static RectTransform Part(Transform parent, Sprite sprite, string hex, Vector2 pos, Vector2 size)
    {
        var img = StoryUI.Pic(parent, "p", sprite, StoryUI.Hex(hex), pos, size);
        if (sprite == ShapeSprites.RoundedRect) { img.type = Image.Type.Sliced; img.preserveAspect = false; }
        else if (sprite == ShapeSprites.Square) img.preserveAspect = false;
        return img.rectTransform;
    }
}

/// <summary>Khối hộp đẳng cự (isometric) vẽ bằng mesh: mặt trên sáng, mặt trái vừa, mặt phải tối. Hộp vuông W×W.</summary>
public sealed class StoryCube : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float w = r.width, h = r.height;
        Vector2 c = r.center;
        Vector2 T = c + new Vector2(0, h * 0.5f), R = c + new Vector2(w * 0.5f, h * 0.25f), L = c + new Vector2(-w * 0.5f, h * 0.25f);
        Vector2 RB = c + new Vector2(w * 0.5f, -h * 0.25f), LB = c + new Vector2(-w * 0.5f, -h * 0.25f), B = c + new Vector2(0, -h * 0.5f);
        Quad(vh, T, R, c, L, Shade(1.12f));
        Quad(vh, L, c, B, LB, Shade(0.86f));
        Quad(vh, c, R, RB, B, Shade(0.68f));
    }

    Color Shade(float k)
    {
        var col = color;
        return new Color(Mathf.Clamp01(col.r * k), Mathf.Clamp01(col.g * k), Mathf.Clamp01(col.b * k), col.a);
    }

    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, col, Vector2.zero);
        vh.AddVert(b, col, Vector2.zero);
        vh.AddVert(c, col, Vector2.zero);
        vh.AddVert(d, col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
