using System.Collections;
using UnityEngine;

public enum BakedKind { Deco, Question, Answer, Collect, QuestionArea, AnswerArea, CollectArea }

/// <summary>
/// Marker 1 ô/1 vùng của game.json được "bake" thành GameObject thật trong prefab layout (xem GenericGameZipImporter) để kéo/resize
/// trực quan trong Scene view. Chỉ là DỮ LIỆU VỊ TRÍ: lúc chạy, GenericGameBaked.ApplyTo() đọc lại toạ độ của từng marker rồi ghi đè vào
/// package đã parse (xPct/yPct/wPct/hPct) — toàn bộ logic còn lại của GenericGameController (mirror nửa phải, random, matrix, collect...)
/// chạy y như cũ. Prefab bị ẩn khi chạy.
///
/// Toạ độ lưu bằng ANCHOR (anchorMin/Max = góc ô theo % của container cha, offset = 0) để không phụ thuộc độ phân giải —
/// kéo/resize trong Scene view đổi offset (px) nên Update() ở Editor tự quy đổi ngược về anchor.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class GenericBakedRect : MonoBehaviour
{
    public BakedKind kind;
    public int round = -1;      // Question/Answer/Collect(+Area): chỉ số round
    public int index = -1;      // chỉ số slot trong nhóm (Area: bỏ qua)
    public string decoId;       // chỉ Deco

    RectTransform Rt => (RectTransform)transform;

    /// <summary>Quy offset (px) về 0 bằng cách đẩy vào anchor — giữ nguyên vị trí hiển thị hiện tại, chỉ đổi cách lưu.</summary>
    public void NormalizeToAnchors()
    {
        var rt = Rt;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        var size = parent.rect.size;
        if (size.x < 1f || size.y < 1f) return; // parent chưa có kích thước (canvas chưa layout) — giữ nguyên
        if (rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero) return;
        var min = new Vector2(rt.anchorMin.x * size.x + rt.offsetMin.x, rt.anchorMin.y * size.y + rt.offsetMin.y);
        var max = new Vector2(rt.anchorMax.x * size.x + rt.offsetMax.x, rt.anchorMax.y * size.y + rt.offsetMax.y);
        rt.anchorMin = new Vector2(min.x / size.x, min.y / size.y);
        rt.anchorMax = new Vector2(max.x / size.x, max.y / size.y);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Toạ độ % kiểu RectPct (gốc trên-trái của container cha, y tăng xuống dưới).</summary>
    public RectPct ReadPct()
    {
        NormalizeToAnchors();
        var rt = Rt;
        return new RectPct
        {
            xPct = rt.anchorMin.x * 100f,
            yPct = (1f - rt.anchorMax.y) * 100f,
            wPct = (rt.anchorMax.x - rt.anchorMin.x) * 100f,
            hPct = (rt.anchorMax.y - rt.anchorMin.y) * 100f,
        };
    }

    public void WritePct(RectPct p)
    {
        var rt = Rt;
        rt.anchorMin = new Vector2(p.xPct / 100f, 1f - (p.yPct + p.hPct) / 100f);
        rt.anchorMax = new Vector2((p.xPct + p.wPct) / 100f, 1f - p.yPct / 100f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying) NormalizeToAnchors();
    }
#endif
}
