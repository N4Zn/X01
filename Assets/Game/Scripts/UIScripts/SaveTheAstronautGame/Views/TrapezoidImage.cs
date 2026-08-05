using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Graphic UI vẽ HÌNH THANG VUÔNG (1 cạnh thẳng đứng, 1 cạnh xiên) thay vì hình chữ nhật — dùng
/// cho cặp nút trái/phải của Save The Astronaut: cạnh thẳng đứng luôn nằm ở mép TRONG (giáp nhau
/// giữa 2 nút, không hở), cạnh xiên ở mép NGOÀI, tạo cảm giác phối cảnh "trôi xa dần" khi 2 nút
/// cùng scale nhỏ lại. Ghép 2 hình thang vuông (1 quay trái, 1 quay phải) sát nhau ra đúng 1 hình
/// thang cân lớn — không cần asset ảnh riêng. topWidthRatio = 1 thì thành hình chữ nhật bình thường.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class TrapezoidImage : MaskableGraphic
{
    [Range(0f, 1f)] public float topWidthRatio = 0.55f;
    [Tooltip("true: cạnh trái thẳng đứng, cạnh phải xiên. false: cạnh phải thẳng đứng, cạnh trái xiên.")]
    public bool verticalEdgeOnLeft = true;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        float left = r.x;
        float right = r.x + r.width;
        float bottom = r.y;
        float top = r.y + r.height;
        float inset = r.width * (1f - topWidthRatio);

        float topLeft = verticalEdgeOnLeft ? left : left + inset;
        float topRight = verticalEdgeOnLeft ? right - inset : right;

        vh.AddVert(new Vector3(left, bottom), color, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(topLeft, top), color, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(topRight, top), color, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(right, bottom), color, new Vector2(1f, 0f));

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
