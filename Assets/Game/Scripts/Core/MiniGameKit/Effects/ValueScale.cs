using UnityEngine;

/// <summary>
/// Tính hệ số phóng to cho UI cần "to dần theo giá trị" (điểm thưởng, combo, jackpot...) mà
/// không vỡ layout dù giá trị tăng nhiều lần liên tiếp — dùng log2 của bội số so với giá trị gốc
/// nên tăng chậm dần, có trần trên (max). Trước đây tính tay trong
/// WhoIsItGameController.UpdateRewardBadge, giờ tách ra dùng lại được cho UI khác.
/// </summary>
public static class ValueScale
{
    /// <param name="value">Giá trị hiện tại (vd điểm câu hỏi sau jackpot).</param>
    /// <param name="baseValue">Giá trị gốc (khi chưa nhân dồn gì cả).</param>
    /// <param name="stepPerDouble">Scale tăng thêm bao nhiêu mỗi lần value gấp đôi so với gốc.</param>
    /// <param name="min">Scale tối thiểu (khi value == baseValue).</param>
    /// <param name="max">Scale tối đa (trần trên, tránh phóng to vô hạn).</param>
    public static float LogScale(int value, int baseValue, float stepPerDouble = 0.25f, float min = 1f, float max = 2.2f)
    {
        int multiplier = Mathf.Max(1, value / Mathf.Max(1, baseValue));
        return Mathf.Clamp(min + stepPerDouble * Mathf.Log(multiplier, 2), min, max);
    }
}
