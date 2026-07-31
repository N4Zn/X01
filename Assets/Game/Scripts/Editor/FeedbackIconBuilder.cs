using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared helper to create feedback icons (green circle + ✓, red circle + ✗)
/// for all game SceneBuilders.
/// </summary>
public static class FeedbackIconBuilder
{
    private const string CircleSpritePath = "Assets/Game/Textures/Common/circle_white_256.png";
    private const int CircleSize = 256;

    /// <summary>
    /// Create a circular feedback icon with checkmark or X.
    /// Returns the root GameObject (inactive by default).
    /// </summary>
    public static GameObject Create(string name, Transform parent, bool isCorrect,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        Color bgColor = isCorrect
            ? new Color(0.15f, 0.78f, 0.32f, 1f)  // vivid green
            : new Color(0.88f, 0.18f, 0.18f, 1f);  // vivid red
        string symbol = isCorrect ? "\u2714" : "\u2716"; // ✔ or ✖

        // Ensure high-res circle sprite exists
        Sprite circleSprite = GetOrCreateCircleSprite();

        // Root: circle background
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = root.GetComponent<Image>();
        if (circleSprite != null) bg.sprite = circleSprite;
        bg.color = bgColor;
        bg.preserveAspect = true;
        bg.raycastTarget = false;

        // Symbol text (✓ or ✗)
        GameObject symbolGo = new GameObject("Symbol", typeof(RectTransform), typeof(Text));
        symbolGo.transform.SetParent(root.transform, false);
        RectTransform srt = symbolGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        Text txt = symbolGo.GetComponent<Text>();
        txt.text = symbol;
        txt.fontSize = 48;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.font = font;

        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// Generate a 256x256 white circle PNG sprite if it doesn't exist.
    /// </summary>
    private static Sprite GetOrCreateCircleSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        if (existing != null) return existing;

        // Create folder
        string dir = Path.GetDirectoryName(CircleSpritePath).Replace("\\", "/");
        EnsureFolder(dir);

        // Generate circle texture
        Texture2D tex = new Texture2D(CircleSize, CircleSize, TextureFormat.RGBA32, false);
        float center = CircleSize * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < CircleSize; y++)
        {
            for (int x = 0; x < CircleSize; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (dist <= radius - 1f)
                    tex.SetPixel(x, y, Color.white);
                else if (dist <= radius)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, radius - dist + 1f)); // anti-alias edge
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();

        // Save PNG
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(CircleSpritePath, png);
        AssetDatabase.ImportAsset(CircleSpritePath, ImportAssetOptions.ForceUpdate);

        // Configure as sprite
        TextureImporter imp = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string par = Path.GetDirectoryName(path).Replace("\\", "/");
        string nm = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(par)) EnsureFolder(par);
        AssetDatabase.CreateFolder(par, nm);
    }
}
