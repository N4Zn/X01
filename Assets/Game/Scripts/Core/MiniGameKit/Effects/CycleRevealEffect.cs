using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Quay số rồi dừng lại đúng kết quả" — hiệu ứng dùng chung cho mọi khoảnh khắc random-reveal
/// (đổ xúc xắc, quay thưởng, mở khoá...): đổi chữ NGẪU NHIÊN liên tục trong 1 khoảng thời gian,
/// rồi dừng hẳn ở giá trị THẬT (đã biết trước, không random lại) — tạo cảm giác hồi hộp mà không
/// cần animation vật lý phức tạp (quay bánh xe thật, vật lý xúc xắc...).
/// </summary>
public static class CycleRevealEffect
{
    /// <param name="target">Text hiện kết quả — bỏ qua an toàn nếu null.</param>
    /// <param name="randomValue">Sinh 1 giá trị hiển thị ngẫu nhiên mỗi tick (lúc đang "quay").</param>
    /// <param name="finalValue">Giá trị THẬT — hiện ra khi dừng, giữ nguyên sau khi coroutine xong.</param>
    /// <param name="duration">Tổng thời gian quay trước khi dừng (giây).</param>
    /// <param name="interval">Khoảng cách giữa mỗi lần đổi số khi đang quay (giây).</param>
    public static IEnumerator Spin(Text target, Func<string> randomValue, string finalValue,
        float duration = 0.6f, float interval = 0.06f)
    {
        float t = 0f;
        while (t < duration)
        {
            t += interval;
            if (target != null) target.text = randomValue();
            yield return new WaitForSeconds(interval);
        }
        if (target != null) target.text = finalValue;
    }
}
