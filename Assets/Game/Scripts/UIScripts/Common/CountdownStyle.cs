using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NGUỒN DUY NHẤT cho kiểu chữ đếm ngược "Start in Ns" / "Next in Ns" của mọi game: trắng, đậm, viền đen
/// (đọc được trên cả nền sáng), cùng cỡ chữ. Scene cũ có màu/cỡ khác nhau (xám đậm 30–56) — gọi
/// <see cref="Apply(Text)"/> / <see cref="Apply(TMP_Text)"/> lúc chạy để đồng bộ mà không phải dựng lại scene.
/// Muốn đổi kiểu countdown toàn app: sửa ĐÚNG file này.
/// </summary>
public static class CountdownStyle
{
    public static readonly Color TextColor = Color.white;
    public static readonly Color OutlineColor = Color.black;
    public const int FontSize = 56;

    public static void Apply(Text t)
    {
        if (t == null) return;
        t.color = TextColor;
        t.fontSize = FontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; // chuỗi ngắn 1 dòng, không để bị cắt chữ
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = t.GetComponent<Outline>();
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = OutlineColor;
        o.effectDistance = new Vector2(2f, -2f);
    }

    public static void Apply(TMP_Text t)
    {
        if (t == null) return;
        t.color = TextColor;
        t.fontSize = FontSize;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.enableWordWrapping = false;
        t.outlineColor = OutlineColor;
        t.outlineWidth = 0.2f;
    }

    /// <summary>Chuỗi countdown chuẩn: "Start in 3s" / "Next in 3s".</summary>
    public static string Format(string label, int secondsLeft) => $"{label} in {secondsLeft}s";
}
