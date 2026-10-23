using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vẽ khung xương (đường + chấm) lên UI. Toạ độ vào là chuẩn hoá 0..1 (gốc trên-trái) trong khung RectTransform này;
/// đặt Graphic cùng rect với ảnh camera thì khớp từng điểm.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PoseSkeletonGraphic : MaskableGraphic
{
    static readonly int[,] Bones =
    {
        { PoseBridgeClient.LShoulder, PoseBridgeClient.RShoulder },
        { PoseBridgeClient.LShoulder, PoseBridgeClient.LElbow }, { PoseBridgeClient.LElbow, PoseBridgeClient.LWrist },
        { PoseBridgeClient.RShoulder, PoseBridgeClient.RElbow }, { PoseBridgeClient.RElbow, PoseBridgeClient.RWrist },
        { PoseBridgeClient.LShoulder, PoseBridgeClient.LHip }, { PoseBridgeClient.RShoulder, PoseBridgeClient.RHip },
        { PoseBridgeClient.LHip, PoseBridgeClient.RHip },
        { PoseBridgeClient.LHip, PoseBridgeClient.LKnee }, { PoseBridgeClient.LKnee, PoseBridgeClient.LAnkle },
        { PoseBridgeClient.RHip, PoseBridgeClient.RKnee }, { PoseBridgeClient.RKnee, PoseBridgeClient.RAnkle },
    };

    public float lineWidth = 6f;
    public float dotSize = 14f;
    const float MinVis = 0.3f;

    struct Skel { public PoseBridgeClient.Person p; public Color c; }
    readonly List<Skel> _skels = new();

    public void Clear() => _skels.Clear();
    public void Add(PoseBridgeClient.Person p, Color c) => _skels.Add(new Skel { p = p, c = c });
    public void Commit() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        foreach (var s in _skels)
        {
            for (int b = 0; b < Bones.GetLength(0); b++)
            {
                int a = Bones[b, 0], c = Bones[b, 1];
                if (s.p.vis[a] < MinVis || s.p.vis[c] < MinVis) continue;
                Line(vh, ToRect(r, s.p.pt[a]), ToRect(r, s.p.pt[c]), s.c);
            }
            for (int i = 0; i < PoseBridgeClient.PointCount; i++)
            {
                if (s.p.vis[i] < MinVis) continue;
                Quad(vh, ToRect(r, s.p.pt[i]), dotSize, Color.white);
            }
        }
    }

    static Vector2 ToRect(Rect r, Vector2 n) => new(r.xMin + n.x * r.width, r.yMax - n.y * r.height);

    void Line(VertexHelper vh, Vector2 a, Vector2 b, Color col)
    {
        var d = (b - a).normalized;
        var n = new Vector2(-d.y, d.x) * (lineWidth * 0.5f);
        int i = vh.currentVertCount;
        vh.AddVert(a - n, col, Vector2.zero); vh.AddVert(a + n, col, Vector2.zero);
        vh.AddVert(b + n, col, Vector2.zero); vh.AddVert(b - n, col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }

    void Quad(VertexHelper vh, Vector2 c, float size, Color col)
    {
        float h = size * 0.5f;
        int i = vh.currentVertCount;
        vh.AddVert(c + new Vector2(-h, -h), col, Vector2.zero); vh.AddVert(c + new Vector2(-h, h), col, Vector2.zero);
        vh.AddVert(c + new Vector2(h, h), col, Vector2.zero); vh.AddVert(c + new Vector2(h, -h), col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
}
