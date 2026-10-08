using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NGUỒN DUY NHẤT cho kiểu chữ SỐ hiển thị ở câu hỏi/đáp án của mọi game: trắng, đậm, viền đen, cỡ to
/// (tự co giãn 36–100pt trong khung — đúng cỡ đáp án của "Đếm đến 5"). Chữ trắng + viền đen đọc được cả trên
/// nền sáng lẫn tối. Muốn đổi kiểu số toàn app: sửa ĐÚNG file này (giống <see cref="CountdownStyle"/>).
/// Chỉ áp cho nội dung là SỐ (xem <see cref="IsNumber"/>) — chữ/từ giữ nguyên kiểu riêng của từng game.
/// </summary>
public static class NumberTextStyle
{
    public static readonly Color TextColor = Color.white;
    public static readonly Color OutlineColor = Color.black;
    public const int MinSize = 36;
    public const int MaxSize = 100;

    /// <summary>true nếu chuỗi chỉ gồm chữ số (vd "7", "10"), bỏ khoảng trắng hai đầu.</summary>
    public static bool IsNumber(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        s = s.Trim();
        if (s.Length == 0) return false;
        for (int i = 0; i < s.Length; i++)
            if (s[i] < '0' || s[i] > '9') return false;
        return true;
    }

    /// <summary>Áp kiểu số cho Text (UGUI cũ): tự co giãn vừa khung, trắng, đậm, viền đen.</summary>
    public static void Apply(Text t)
    {
        if (t == null) return;
        t.color = TextColor;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = MinSize;
        t.resizeTextMaxSize = MaxSize;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        var o = t.GetComponent<Outline>();
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = OutlineColor;
        o.effectDistance = new Vector2(3f, -3f);
    }

    public const int OperatorSize = 60;
    const string MinusSign = "−"; // dấu trừ chuẩn (dài hơn gạch nối '-' ~1.5–2 lần), KHÔNG kéo giãn thêm

    /// <summary>Kiểu cho dấu phép tính (+ − =) ở câu hỏi game cộng/trừ: cỡ 60, đậm, chữ ĐEN viền TRẮNG (ngược với số).
    /// Dấu '-' đổi thành dấu trừ chuẩn (U+2212).</summary>
    public static void ApplyOperator(Text t)
    {
        if (t == null) return;
        string s = t.text == null ? "" : t.text.Trim();
        bool minus = s == "-" || s == MinusSign;
        if (minus) t.text = MinusSign;
        t.color = OutlineColor;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.resizeTextForBestFit = false;
        t.fontSize = OperatorSize;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = t.GetComponent<Outline>();
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = TextColor;
        o.effectDistance = new Vector2(2f, -2f);
        o.enabled = true;
    }

    /// <summary>Áp kiểu số cho TextMeshPro: tự co giãn 36–100, trắng, đậm, viền đen.</summary>
    public static void Apply(TMP_Text t)
    {
        if (t == null) return;
        t.color = TextColor;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMin = MinSize;
        t.fontSizeMax = MaxSize;
        t.enableWordWrapping = false;
        // Viền chỉ là trang trí: outlineWidth/outlineColor tạo material instance, NÉM ArgumentNullException nếu text
        // thiếu material (xem ghi chú ở CountdownStyle.Apply(TMP_Text)) → tự vá từ font, thiếu nữa thì bỏ qua viền.
        if (t.fontSharedMaterial == null && t.font != null) t.fontSharedMaterial = t.font.material;
        if (t.fontSharedMaterial == null) return;
        try
        {
            t.outlineColor = OutlineColor;
            t.outlineWidth = 0.2f;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[NumberTextStyle] không đặt được viền chữ: " + e.Message);
        }
    }

    /// <summary>Kiểu gốc của 1 label TMP trước khi bị áp kiểu số — để trả lại khi label được dùng lại cho CHỮ ở câu sau
    /// (nút đáp án được tái dùng giữa các round: số → từ không được giữ chữ trắng viền đen).</summary>
    sealed class Baseline : MonoBehaviour
    {
        public Color color; public FontStyles style; public bool auto; public float min, max, size; public bool wrap;
        public TextAlignmentOptions align; public float outlineWidth; public Color outlineColor; public bool numberApplied;
    }

    /// <summary>Gán nội dung rồi gọi hàm này cho label TMP dùng chung nhiều round: SỐ → áp kiểu số; không phải số → trả về kiểu gốc.</summary>
    public static void Refresh(TMP_Text label, string text)
    {
        if (label == null) return;
        var b = label.GetComponent<Baseline>();
        if (IsNumber(text))
        {
            if (b == null)
            {
                b = label.gameObject.AddComponent<Baseline>();
                b.color = label.color; b.style = label.fontStyle; b.auto = label.enableAutoSizing;
                b.min = label.fontSizeMin; b.max = label.fontSizeMax; b.size = label.fontSize;
                b.wrap = label.enableWordWrapping; b.align = label.alignment;
                if (label.fontSharedMaterial != null) { try { b.outlineWidth = label.outlineWidth; b.outlineColor = label.outlineColor; } catch { } }
            }
            b.numberApplied = true;
            Apply(label);
        }
        else if (b != null && b.numberApplied)
        {
            b.numberApplied = false;
            label.color = b.color; label.fontStyle = b.style; label.enableAutoSizing = b.auto;
            label.fontSizeMin = b.min; label.fontSizeMax = b.max; label.fontSize = b.size;
            label.enableWordWrapping = b.wrap; label.alignment = b.align;
            if (label.fontSharedMaterial != null) { try { label.outlineWidth = b.outlineWidth; label.outlineColor = b.outlineColor; } catch { } }
        }
    }

    /// <summary>Chỉ thêm VIỀN ĐEN cho Text (UGUI) đang hiện số — giữ nguyên màu/cỡ chữ đã đặt (dùng cho GenericGame, nơi
    /// màu/cỡ chữ do người soạn game chọn trong builder).</summary>
    public static void OutlineIfNumber(Text t, string text)
    {
        if (t == null) return;
        var o = t.GetComponent<Outline>();
        if (!IsNumber(text)) { if (o != null) o.enabled = false; return; } // Text dùng lại cho chữ ở câu sau: tắt viền
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = OutlineColor;
        o.effectDistance = new Vector2(3f, -3f);
        o.enabled = true;
    }

    /// <summary>Gọi sau khi gán nội dung: nếu <paramref name="text"/> là SỐ thì áp kiểu số cho label.</summary>
    public static void ApplyIfNumber(TMP_Text label, string text)
    {
        if (label != null && IsNumber(text)) Apply(label);
    }

    public static void ApplyIfNumber(Text label, string text)
    {
        if (label != null && IsNumber(text)) Apply(label);
    }
}
