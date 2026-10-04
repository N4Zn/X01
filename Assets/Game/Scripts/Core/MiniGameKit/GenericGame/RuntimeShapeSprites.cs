using UnityEngine;

/// <summary>Sinh sprite hình tròn LÚC RUNTIME (không cần import ảnh có sẵn trong project) — dùng
/// cho `ButtonItem.ApplyShape("circle")`. Cache tĩnh — chỉ sinh 1 lần cho cả app, dùng chung mọi
/// GenericGame cần hình tròn.</summary>
public static class RuntimeShapeSprites
{
    const int Size = 128;
    static Sprite _circle;

    public static Sprite GetCircle()
    {
        if (_circle != null) return _circle;

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var center = new Vector2(Size / 2f, Size / 2f);
        float r = Size / 2f - 1f;
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * Size + x] = d <= r ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        _circle = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        return _circle;
    }
}
