using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sinh 1 bảng chữ cái NxN giấu ĐÚNG 3 từ — 1 hàng ngang, 1 hàng dọc, 1 đường chéo. Chiều VIẾT của
/// từ trong bảng LUÔN là chiều đọc tự nhiên: ngang = trái→phải, dọc = trên→dưới, chéo = trên-trái→
/// dưới-phải — không bao giờ đảo ngược (học sinh vẫn được DẪM xuôi hoặc ngược khi tìm, xem
/// WordGridDisplay, nhưng CHỮ hiện trên bảng luôn đúng chiều đọc).
///
/// Cho phép 2 từ GIAO NHAU tại 1 ô nếu trùng đúng chữ cái tại đó (kiểu ô chữ/crossword).
/// </summary>
public static class WordGridGenerator
{
    static readonly (int dr, int dc) HorizontalDir = (0, 1);
    static readonly (int dr, int dc) VerticalDir    = (1, 0);
    static readonly (int dr, int dc) DiagonalDir    = (1, 1);

    public struct Grid
    {
        public int size;
        public char[] letters; // flat, index = row*size+col
        public string[] words;
        public int[][] paths;  // path[w] = danh sách chỉ số ô của words[w]
    }

    public static Grid Generate(string[] words, int size)
    {
        // Thử tối đa 30 lần với chiều đặt từ ngẫu nhiên mỗi lần.
        // Một số bộ từ (vd SISTER+BROTHER+GRANDMA) có xung đột bất khả thi khi chiều cố định —
        // xáo ngẫu nhiên chiều đặt từ mỗi retry giải quyết điều này.
        for (int attempt = 0; attempt < 30; attempt++)
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

        var allDirs = new (int dr, int dc)[] { HorizontalDir, VerticalDir, DiagonalDir };
        var paths = new int[3][];
        var fixedLetters = new Dictionary<int, char>();

        // Gán ngẫu nhiên chiều (H/V/D) cho mỗi từ — xáo trộn Fisher-Yates.
        // Chiều cố định (words[0]=H, [1]=V, [2]=D) gây deadlock với bộ từ 7-chữ (vd SISTER+BROTHER+GRANDMA).
        var wordDirIdx = new int[] { 0, 1, 2 };
        for (int i = 2; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (wordDirIdx[i], wordDirIdx[j]) = (wordDirIdx[j], wordDirIdx[i]);
        }

        // Đặt theo mức ràng buộc: diagonal (2) → vertical (1) → horizontal (0).
        for (int dirPriority = 2; dirPriority >= 0; dirPriority--)
        {
            int w = -1;
            for (int i = 0; i < 3 && i < words.Length; i++)
                if (wordDirIdx[i] == dirPriority) { w = i; break; }
            if (w < 0) continue;

            string word = (words[w] ?? "").ToUpperInvariant();
            var dir = allDirs[wordDirIdx[w]];
            int[] path = TryPlace(word, size, dir.dr, dir.dc, fixedLetters, 300)
                      ?? ForcePlace(word, size, dir.dr, dir.dc);
            paths[w] = path;
            for (int i = 0; i < word.Length; i++) fixedLetters[path[i]] = word[i];
        }

        foreach (var kv in fixedLetters) letters[kv.Key] = kv.Value;
        return new Grid { size = size, letters = letters, words = words, paths = paths };
    }

    // Kiểm tra mọi chữ trong path khớp với grid.letters.
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

    /// <summary>Thử random vị trí bắt đầu theo (dr,dc). Chấp nhận giao ô nếu trùng chữ cái.</summary>
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
                if (fixedLetters.TryGetValue(idx, out char existing) && existing != word[i])
                { ok = false; break; }
                candidate[i] = idx;
            }
            if (ok) return candidate;
        }
        return null;
    }

    /// <summary>Fallback: đặt tại (0,0) theo chiều (dr,dc). Chỉ dùng khi TryPlace thất bại.</summary>
    static int[] ForcePlace(string word, int size, int dr, int dc)
    {
        var path = new int[word.Length];
        for (int i = 0; i < word.Length; i++) path[i] = (dr * i) * size + (dc * i);
        return path;
    }
}
