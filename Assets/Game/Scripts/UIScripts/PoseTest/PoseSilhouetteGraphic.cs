using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hình mẫu tư thế (người que) — vẽ lại từ các SVG "silhouette" của bản HTML (viewBox 100x160).
/// Đặt vào 1 RectTransform tỉ lệ ~100:160; <see cref="SetPose"/> đổi hình theo id tư thế (<see cref="PoseChecks.Def.id"/>).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PoseSilhouetteGraphic : MaskableGraphic
{
    struct Fig { public float cx, cy, r; public float[] lines; } // lines: x1,y1,x2,y2 ...

    static readonly Dictionary<string, Fig> Figs = new()
    {
        ["hands_raised"] = new Fig { cx = 50, cy = 20, r = 12, lines = new float[] {
            50,32,50,92,  50,42,20,10,  50,42,80,10,  50,92,34,150,  50,92,66,150 } },
        ["squat"] = new Fig { cx = 50, cy = 58, r = 12, lines = new float[] {
            50,70,50,102,  50,76,26,92,  50,76,74,92,  50,102,32,122,  32,122,38,152,  50,102,68,122,  68,122,62,152 } },
        ["arms_cross"] = new Fig { cx = 50, cy = 20, r = 12, lines = new float[] {
            50,32,50,92,  28,44,68,62,  72,44,32,62,  50,92,34,150,  50,92,66,150 } },
        ["hands_on_hips"] = new Fig { cx = 50, cy = 20, r = 12, lines = new float[] {
            50,32,50,92,  50,40,24,52,  24,52,38,78,  50,40,76,52,  76,52,62,78,  50,92,34,150,  50,92,66,150 } },
        ["one_hand_raised"] = new Fig { cx = 50, cy = 20, r = 12, lines = new float[] {
            50,32,50,92,  50,42,80,10,  50,42,30,70,  50,92,34,150,  50,92,66,150 } },
        ["one_leg_stand"] = new Fig { cx = 50, cy = 20, r = 12, lines = new float[] {
            50,32,50,92,  50,42,22,20,  50,42,78,20,  50,92,40,150,  50,92,70,118,  70,118,58,104 } },
    };

    public float lineWidth = 5f;

    string _id;
    public void SetPose(string id) { _id = id; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_id == null || !Figs.TryGetValue(_id, out var f)) return;
        var r = GetPixelAdjustedRect();
        float k = Mathf.Min(r.width / 100f, r.height / 160f);
        var o = new Vector2(r.center.x - 50f * k, r.center.y + 80f * k); // gốc SVG (0,0) = trên-trái
        Vector2 P(float x, float y) => new(o.x + x * k, o.y - y * k);

        for (int i = 0; i + 3 < f.lines.Length; i += 4)
            Line(vh, P(f.lines[i], f.lines[i + 1]), P(f.lines[i + 2], f.lines[i + 3]));

        const int seg = 24;
        Vector2 prev = P(f.cx + f.r, f.cy);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var cur = P(f.cx + Mathf.Cos(a) * f.r, f.cy + Mathf.Sin(a) * f.r);
            Line(vh, prev, cur);
            prev = cur;
        }
    }

    void Line(VertexHelper vh, Vector2 a, Vector2 b)
    {
        var d = (b - a).normalized;
        var n = new Vector2(-d.y, d.x) * (lineWidth * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
}
