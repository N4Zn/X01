using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model for PlanetAlphabet game.
/// Each player has 3 boxes with random letters (A-Z).
/// Player taps boxes in alphabetical order. All correct = +1 star.
/// </summary>
public class PlanetAlphabetModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    // Per-player round data
    public string[][] Values;       // [playerIdx][0..N] = letters in each box
    public int[][] CorrectOrder;    // [playerIdx][0..N] = box indices in alphabetical order
    public int[] NextExpected;      // [playerIdx] = which position in CorrectOrder to tap next
    public int[] BoxCount;          // [playerIdx] = current box count for this player

    public PlanetAlphabetModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        Values = new string[2][];
        CorrectOrder = new int[2][];
        NextExpected = new int[2];
        BoxCount = new int[] { 3, 3 };
    }

    /// <summary>
    /// Get box count based on combined score (difficulty ramps 3→5).
    /// </summary>
    public int GetBoxCountForDifficulty(int playerIndex)
    {
        int score = (playerIndex == 0) ? Player1Score : Player2Score;
        if (score >= 6) return 5;
        if (score >= 3) return 4;
        return 3;
    }

    /// <summary>
    /// Generate new round for a specific player with N unique letters A-Z.
    /// </summary>
    public void GenerateRound(int playerIndex)
    {
        int count = GetBoxCountForDifficulty(playerIndex);
        BoxCount[playerIndex] = count;

        List<char> pool = new List<char>();
        for (char c = 'A'; c <= 'Z'; c++) pool.Add(c);

        // Shuffle and pick N
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            char tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        Values[playerIndex] = new string[count];
        for (int i = 0; i < count; i++)
            Values[playerIndex][i] = pool[i].ToString();

        // Compute correct alphabetical order
        List<int> indices = new List<int>();
        for (int i = 0; i < count; i++) indices.Add(i);
        string[] vals = Values[playerIndex];
        indices.Sort((a, b) => string.Compare(vals[a], vals[b]));
        CorrectOrder[playerIndex] = indices.ToArray();

        NextExpected[playerIndex] = 0;
    }

    /// <summary>
    /// Check if tapping box at boxIndex is correct for this player.
    /// </summary>
    public bool CheckTap(int playerIndex, int boxIndex)
    {
        int expected = NextExpected[playerIndex];
        if (expected >= BoxCount[playerIndex]) return false;
        return CorrectOrder[playerIndex][expected] == boxIndex;
    }

    /// <summary>
    /// Advance to next expected box.
    /// </summary>
    public void AdvanceCorrect(int playerIndex)
    {
        NextExpected[playerIndex]++;
    }

    /// <summary>
    /// Is this player's round complete?
    /// </summary>
    public bool IsRoundComplete(int playerIndex)
    {
        return NextExpected[playerIndex] >= BoxCount[playerIndex];
    }

    /// <summary>
    /// Reset round for a player (wrong answer — reshuffle).
    /// </summary>
    public void ResetRound(int playerIndex)
    {
        NextExpected[playerIndex] = 0;
    }

    public void ResetGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        GameTimer = MaxGameTime;
    }
}
