/// <summary>
/// "Điền chữ vào chỗ trống" — dùng cho câu hỏi dạng "từ có 1 chỗ bị thiếu" (spelling, điền từ...).
/// Khi trả lời đúng, thay "_" trong template bằng đáp án thật để học sinh thấy từ hoàn chỉnh ngay
/// (phần thưởng thị giác tức thời) thay vì chỉ tô xanh nút bấm. Thuần C#/static, không phụ thuộc
/// FamilySpellingGame — dùng lại được cho bất kỳ game "điền khuyết" nào khác sau này.
/// </summary>
public static class FillBlankEffect
{
    /// <summary>Thay CHỖ TRỐNG ĐẦU TIÊN ("_") trong template bằng answer. Không có "_" thì trả về
    /// nguyên template. Vd Fill("M_M", "O") → "MOM".</summary>
    public static string Fill(string template, string answer)
    {
        if (string.IsNullOrEmpty(template) || string.IsNullOrEmpty(answer)) return template;
        int idx = template.IndexOf('_');
        return idx < 0 ? template : template.Substring(0, idx) + answer + template.Substring(idx + 1);
    }
}
