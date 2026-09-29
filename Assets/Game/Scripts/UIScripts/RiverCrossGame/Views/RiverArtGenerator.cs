using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sinh toàn bộ visual cho scene River Cross bằng code — không cần ảnh ngoài.
///
/// Gọi Generate() từ RiverCrossController (hoặc Awake nếu attach cùng GameObject).
///
/// Tạo:
///   • Nền trời (gradient xanh nhạt → xanh đậm)
///   • Nước sông (texture ripple, RawImage + WaterBackground để scroll)
///   • Bờ trái / bờ phải (cỏ + đất)
///   • Divider mờ giữa các lane
///   • Texture bè gỗ (áp vào mọi RaftItem trong scene nếu muốn)
///
/// Layout canvas (1024 × 600, center = 0,0):
///   leftBankX  ≈ -460  →  leftEdge  = leftBankX + bankHalfWidth
///   rightBankX ≈  460  →  rightEdge = rightBankX - bankHalfWidth
///   River zone nằm giữa 2 bờ.
/// </summary>
public class RiverArtGenerator : MonoBehaviour
{
    [Header("Target UI elements (gán trên Inspector)")]
    [SerializeField] RawImage skyImage;         // phủ toàn bộ canvas
    [SerializeField] RawImage waterImage;       // chỉ vùng sông (giữa 2 bờ)
    [SerializeField] Image    leftBankImage;    // Image bờ trái
    [SerializeField] Image    rightBankImage;   // Image bờ phải

    [Header("Texture sizes")]
    [SerializeField] int skyTexW   = 256;
    [SerializeField] int skyTexH   = 128;
    [SerializeField] int waterTexW = 256;
    [SerializeField] int waterTexH = 256;
    [SerializeField] int bankTexW  =  64;
    [SerializeField] int bankTexH  = 256;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake() => Generate();

    // ── Public API ────────────────────────────────────────────────────────────

    public void Generate()
    {
        if (skyImage)   ApplyTexture(skyImage,   GenerateSky(skyTexW, skyTexH));
        if (waterImage) ApplyTexture(waterImage, GenerateWater(waterTexW, waterTexH));

        // Chỉ sinh texture bờ nếu chưa có sprite tùy chỉnh được gán trên Inspector.
        // Nếu đã gán ảnh → giữ nguyên, không ghi đè.
        if (leftBankImage  && leftBankImage.sprite  == null)
            leftBankImage.sprite  = ToSprite(GenerateBank(bankTexW, bankTexH, isLeft: true));
        if (rightBankImage && rightBankImage.sprite == null)
            rightBankImage.sprite = ToSprite(GenerateBank(bankTexW, bankTexH, isLeft: false));
    }

    // ── Texture generators ────────────────────────────────────────────────────

    /// <summary>
    /// Nền trời: gradient xanh nhạt (trên) → xanh đậm (dưới).
    /// Thêm vài đám mây nhỏ random để không trơn quá.
    /// </summary>
    static Texture2D GenerateSky(int w, int h)
    {
        var tex = NewTex(w, h);

        // Gradient từ top (nhạt) xuống bottom (đậm)
        Color top    = new Color(0.53f, 0.81f, 0.98f);   // #87CEFB nhạt
        Color bottom = new Color(0.20f, 0.55f, 0.80f);   // #3388CC đậm hơn

        for (int y = 0; y < h; y++)
        {
            float t  = (float)y / (h - 1);
            Color bg = Color.Lerp(bottom, top, t);

            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, bg);
        }

        // Vài đám mây (blob trắng mờ)
        AddCloudBlob(tex, w / 4,     h * 3 / 4, 30, 14, 0.45f);
        AddCloudBlob(tex, w * 3 / 4, h * 7 / 8, 22, 10, 0.35f);
        AddCloudBlob(tex, w / 2,     h * 5 / 8, 18, 8,  0.30f);

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Nước sông: xanh lam trung bình + ripple ngang (sin) + highlight dọc mờ.
    /// Texture này được scroll bởi WaterBackground — cần tileable theo Y.
    /// </summary>
    static Texture2D GenerateWater(int w, int h)
    {
        var tex = NewTex(w, h);

        // Bảng màu nước
        Color deep    = new Color(0.08f, 0.35f, 0.65f);  // xanh đậm
        Color shallow = new Color(0.18f, 0.55f, 0.85f);  // xanh vừa
        Color foam    = new Color(0.55f, 0.80f, 0.95f);  // xanh nhạt (gợn)

        var rng = new System.Random(42);

        for (int y = 0; y < h; y++)
        {
            // Base gradient nhẹ theo Y (tạo cảm giác dòng chảy)
            float baseT = (float)y / (h - 1);
            Color bg    = Color.Lerp(shallow, deep, baseT * 0.4f);

            for (int x = 0; x < w; x++)
            {
                // Ripple ngang: sóng sin nhiều tần số
                float rip  = Mathf.Sin(y * 0.25f + x * 0.05f) * 0.06f
                           + Mathf.Sin(y * 0.6f  - x * 0.02f) * 0.03f;

                // Highlight dọc mờ (vệt sáng theo dòng chảy)
                float hl   = Mathf.Pow(Mathf.Sin(x / (float)w * Mathf.PI * 6f) * 0.5f + 0.5f, 3f) * 0.06f;

                // Noise nhẹ tránh đơn điệu
                float noise = (float)(rng.NextDouble() - 0.5) * 0.015f;

                Color c = bg + new Color(rip + hl + noise,
                                         rip + hl + noise,
                                         rip + hl * 0.5f + noise);

                // Đường gợn sóng nhạt hơn (foam line) tại 1 số Y cố định
                if ((y % 32) < 3)
                    c = Color.Lerp(c, foam, 0.35f * Mathf.Sin(x * 0.15f) * 0.5f + 0.25f);

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;   // để WaterBackground scroll liên tục
        return tex;
    }

    /// <summary>
    /// Bờ sông: gradient đất (nâu → xanh cỏ) + chi tiết cỏ nhỏ.
    /// isLeft: bờ trái có viền đất phải, bờ phải có viền đất trái.
    /// </summary>
    static Texture2D GenerateBank(int w, int h, bool isLeft)
    {
        var tex = NewTex(w, h);

        Color dirt  = new Color(0.45f, 0.30f, 0.15f);  // nâu đất
        Color grass = new Color(0.20f, 0.55f, 0.20f);  // xanh cỏ
        Color edge  = new Color(0.30f, 0.60f, 0.30f);  // xanh đậm rìa nước

        var rng = new System.Random(isLeft ? 1 : 2);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Phía nước (isLeft: x lớn = gần sông; isRight: x nhỏ = gần sông)
                float riverSide = isLeft ? (float)x / w : 1f - (float)x / w;

                Color c;
                if (riverSide > 0.85f)
                    c = Color.Lerp(edge, new Color(0.08f, 0.35f, 0.65f), (riverSide - 0.85f) / 0.15f);
                else if (riverSide > 0.5f)
                    c = Color.Lerp(grass, edge, (riverSide - 0.5f) / 0.35f);
                else
                    c = Color.Lerp(dirt, grass, riverSide / 0.5f);

                // Thêm noise cỏ nhỏ
                float gNoise = (float)(rng.NextDouble() - 0.5) * 0.06f;
                c.r += gNoise * 0.5f;
                c.g += gNoise;
                c.b += gNoise * 0.2f;

                tex.SetPixel(x, y, c);
            }
        }

        // Vài ngọn cỏ cao (đường dọc xanh đậm)
        for (int i = 0; i < 12; i++)
        {
            int gx = rng.Next(isLeft ? 0 : w / 3, isLeft ? w * 2 / 3 : w);
            int gy = rng.Next(0, h);
            int gh = rng.Next(4, 10);
            for (int dy = 0; dy < gh; dy++)
                if (gy + dy < h)
                    tex.SetPixel(gx, gy + dy, new Color(0.10f, 0.45f, 0.10f));
        }

        tex.Apply();
        return tex;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void AddCloudBlob(Texture2D tex, int cx, int cy, int rw, int rh, float alpha)
    {
        int w = tex.width, h = tex.height;
        for (int y = cy - rh; y <= cy + rh; y++)
        {
            for (int x = cx - rw; x <= cx + rw; x++)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                float dx = (float)(x - cx) / rw;
                float dy = (float)(y - cy) / rh;
                float d  = dx * dx + dy * dy;
                if (d > 1f) continue;
                float a = alpha * (1f - d);          // fade ra rìa
                Color existing = tex.GetPixel(x, y);
                tex.SetPixel(x, y, Color.Lerp(existing, Color.white, a));
            }
        }
    }

    static Texture2D NewTex(int w, int h)
        => new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };

    static void ApplyTexture(RawImage img, Texture2D tex)
    {
        img.texture  = tex;
        img.uvRect   = new Rect(0, 0, 1, 1);
    }

    static Sprite ToSprite(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), 100f);
}
