using System.Collections.Generic;

/// <summary>
/// Logic thuần để chấm 1 board matching (không UI, không state chọn/deselect —
/// phần đó vẫn nằm trong MatchingDisplay.cs vì gắn chặt với MatchingItem/DrawLine).
/// Dùng khi 1 IAnswerDisplay mới cũng cần kiểu "nối cặp" nhưng UI hoàn toàn khác MatchingDisplay.
/// </summary>
public static class MatchingValidator
{
    /// <summary>True nếu cặp (leftIndex → rightIndex) khớp với correctPairs[leftIndex].</summary>
    public static bool IsPairCorrect(int leftIndex, int rightIndex, int[] correctPairs)
        => correctPairs != null
           && leftIndex >= 0 && leftIndex < correctPairs.Length
           && correctPairs[leftIndex] == rightIndex;

    /// <summary>True nếu đã nối đủ và tất cả các cặp trong <paramref name="pairs"/> đều đúng.</summary>
    public static bool IsAllCorrect(IDictionary<int, int> pairs, int[] correctPairs)
    {
        if (correctPairs == null || pairs.Count < correctPairs.Length) return false;
        for (int i = 0; i < correctPairs.Length; i++)
        {
            if (!pairs.TryGetValue(i, out int right) || right != correctPairs[i])
                return false;
        }
        return true;
    }

    /// <summary>Trả về playerAnswer[i] = pairs[i] nếu có, ngược lại -1 — dùng cho callback IAnswerDisplay.</summary>
    public static int[] ToPlayerAnswer(IDictionary<int, int> pairs, int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = pairs.TryGetValue(i, out int v) ? v : -1;
        return result;
    }
}
