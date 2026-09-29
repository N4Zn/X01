using UnityEngine;

/// <summary>
/// Quy ước màu + rich-text dùng chung cho text feedback (đúng/sai/được điểm/mất điểm) — để mọi
/// mini-game hiển thị nhất quán thay vì mỗi game tự chọn mã màu riêng. Dùng được với cả
/// UnityEngine.UI.Text (supportRichText=true) và TextMeshProUGUI (rich text mặc định bật).
///
/// Quy ước: XANH = được điểm/thắng, ĐỎ = mất điểm/thua/chia sẻ đi.
/// </summary>
public static class FeedbackTextStyle
{
    public static readonly Color GainColor = new(0.18f, 0.62f, 0.31f, 1f);
    public static readonly Color LossColor = new(0.80f, 0.20f, 0.20f, 1f);

    static readonly string GainHex = ColorUtility.ToHtmlStringRGB(GainColor);
    static readonly string LossHex = ColorUtility.ToHtmlStringRGB(LossColor);

    public static string Bold(string text) => $"<b>{text}</b>";
    public static string Colored(string text, string hex) => $"<color=#{hex}>{text}</color>";

    /// <summary>"+N points" tô xanh đậm — dùng khi 1 bên ĐƯỢC điểm.</summary>
    public static string Gain(int amount) => Colored(Bold($"+{amount} points"), GainHex);

    /// <summary>"-N points" tô đỏ đậm — dùng khi 1 bên MẤT/bị trừ điểm.</summary>
    public static string Loss(int amount) => Colored(Bold($"-{amount} points"), LossHex);

    /// <summary>"N points" tô đỏ đậm (không dấu +/-) — dùng khi hiện góc nhìn người NHƯỜNG điểm
    /// (vd "Share! 2 points for Player 2" — với người thắng, đây là phần họ cho đi).</summary>
    public static string Shared(int amount) => Colored(Bold($"{amount} points"), LossHex);
}
