using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sinh 1 bảng chữ cái NxN giấu ĐÚNG 3 từ — 1 hàng ngang, 1 hàng dọc, 1 đường chéo. Chiều VIẾT của
/// từ trong bảng LUÔN là chiều đọc tự nhiên: ngang = trái→phải, dọc = trên→dưới, chéo = trên-trái→
/// dưới-phải — không bao giờ đảo ngược (học sinh vẫn được DẪM xuôi hoặc ngược khi tìm, xem
/// WordGridDisplay, nhưng CHỮ hiện trên bảng luôn đúng chiều đọc).
///
/// Cho phép 2 từ GIAO NHAU tại 1 ô nếu trùng đúng chữ cái tại đó (kiểu ô chữ/crossword) — cấm hoàn
/// toàn chồng lấn sẽ gần như bất khả thi với từ dài 7 chữ trên board 7x7 (1 từ ngang dài 7 chiếm
/// TRỌN 1 hàng, nên bất kỳ từ dọc dài 7 nào cũng BẮT BUỘC đi qua hàng đó ở đâu đó).
/// </summary>
public static class WordGridGenerator
{
    static readonly (int dr, int dc) HorizontalDir = (0, 1);
    static readonly (int dr, int dc) VerticalDir = (1, 0);
    static readonly (int dr, int dc) DiagonalDir = (1, 1);

    public struct Grid
    {
        public int size;
        public char[] letters; // flat, index = row*size+col
        public string[] words; // [0]=ngang, [1]=dọc, [2]=chéo
        public int[][] paths;  // path[i] = thứ tự ô (row*size+col) của words[i], LUÔN đúng chiều đọc
    }

    public static Grid Generate(string[] words, int size)
    {
        // Retry nếu ForcePlace ghi đè letter của từ đã đặt trước (vd BROTHER bị SISTER overwrite).
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var grid = GenerateOnce(words, size);
            if (IsValid(grid, words)) return grid;
        }
        return GenerateOnce(words, size);
    }

    static Grid GenerateOnce(string[] words, int size)
    {
        var letters = new char[size * size];
        for (int i = 0; i < letters.Length; i++) letters[i] = (char)('A' + Random.Range(0, 26));

        var dirs = new[] { HorizontalDir, VerticalDir, DiagonalDir };
        var paths = new int[3][];
        var fixedLetters = new Dictionary<int, char>(); // ô nào đã có chữ CỐ ĐỊNH (thuộc 1 từ trước đó)

        // Đặt theo thứ tự RÀNG BUỘC CHẶT NHẤT trước: 1 từ dài đúng bằng size chỉ có ĐÚNG 1 vị trí
        // khả dĩ theo hướng chéo (góc trên-trái→dưới-phải), ít vị trí hơn hẳn hướng dọc/ngang — đặt
        // nó LÚC BẢNG CÒN TRỐNG (chéo→dọc→ngang) để tránh xung đột tốt hơn nhiều so với đặt sau cùng.
        int[] placeOrder = { 2, 1, 0 };
        foreach (int w in placeOrder)
        {
            if (w >= words.Length) continue;
            string word = (words[w] ?? "").ToUpperInvariant();
            var (dr, dc) = dirs[w];
            int[] path = TryPlace(word, size, dr, dc, fixedLetters, 300) ?? ForcePlace(word, size, dr, dc);
            paths[w] = path;
            for (int i = 0; i < word.Length; i++) fixedLetters[path[i]] = word[i];
        }

        foreach (var kv in fixedLetters) letters[kv.Key] = kv.Value;

        return new Grid { size = size, letters = letters, words = words, paths = paths };
    }

    // Kiểm tra mọi chữ trong path khớp với grid.letters — ForcePlace có thể ghi đè letters của từ trước.
    static bool IsValid(Grid grid, string[] words)
    {
        for (int w = 0; w < 3 && w < words.Length; w++)
        {
            if (string.IsNullOrEmpty(words[w]) || grid.paths[w] == null) continue;
            string word = words[w].ToUpperInvariant();
            for (int i = 0; i < word.Length; i++)
                if (grid.letters[grid.paths[w][i]] != word[i]) return false;
        }
        return true;
    }

    /// <summary>Thử random vị trí bắt đầu, LUÔN đi theo (dr,dc) cố định (đúng chiều đọc). Chấp nhận
    /// giao với từ đã đặt trước NẾU trùng chữ cái tại ô giao — chỉ từ chối khi chữ cái xung đột.</summary>
    static int[] TryPlace(string word, int size, int dr, int dc, Dictionary<int, char> fixedLetters, int attempts)
    {
        if (word.Length == 0) return new int[0];

        for (int a = 0; a < attempts; a++)
        {
            int row = Random.Range(0, size);
            int col = Random.Range(0, size);
            int endRow = row + dr * (word.Length - 1);
            int endCol = col + dc * (word.Length - 1);
            if (endRow < 0 || endRow >= size || endCol < 0 || endCol >= size) continue;

            var candidate = new int[word.Length];
            bool ok = true;
            for (int i = 0; i < word.Length; i++)
            {
                int idx = (row + dr * i) * size + (col + dc * i);
                if (fixedLetters.TryGetValue(idx, out char existing) && existing != word[i]) { ok = false; break; }
                candidate[i] = idx;
            }
            if (ok) return candidate;
        }
        return null;
    }

    /// <summary>Cực hiếm khi tới đây (cho phép giao chữ đã gần như luôn tìm được chỗ) — đặt ở góc
    /// trên-trái (0,0), vẫn ĐÚNG CHIỀU ĐỌC, chấp nhận ghi đè nếu buộc phải vậy thay vì đảo hướng.
    /// An toàn vì word.Length &lt;= size luôn đúng cho danh sách từ Family dùng trong game này.</summary>
    static int[] ForcePlace(string word, int size, int dr, int dc)
    {
        var path = new int[word.Length];
        for (int i = 0; i < word.Length; i++) path[i] = (dr * i) * size + (dc * i);
        return path;
    }
}
