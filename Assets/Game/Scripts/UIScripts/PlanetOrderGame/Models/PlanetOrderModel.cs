using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model for PlanetOrder game.
/// Each player has 3 boxes with random numbers (0-9).
/// Player taps boxes in ascending order. All correct = +1 star.
/// </summary>
public class PlanetOrderModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    // Per-player round data
    public int[][] Values;          // [playerIdx][0..N] = values in each box
    public int[][] CorrectOrder;    // [playerIdx][0..N] = box indices in ascending order
    public int[] NextExpected;      // [playerIdx] = which position in CorrectOrder to tap next
    public int[] BoxCount;          // [playerIdx] = current box count for this player

    public PlanetOrderModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        Values = new int[2][];
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
    /// Generate new round for a specific player with N unique numbers 0-9.
    /// </summary>
    public void GenerateRound(int playerIndex)
    {
        int count = GetBoxCountForDifficulty(playerIndex);
        BoxCount[playerIndex] = count;

        List<int> pool = new List<int>();
        for (int i = 0; i <= 9; i++) pool.Add(i);

        // Shuffle and pick N
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        Values[playerIndex] = new int[count];
        for (int i = 0; i < count; i++)
            Values[playerIndex][i] = pool[i];

        // Compute correct ascending order
        List<int> indices = new List<int>();
        for (int i = 0; i < count; i++) indices.Add(i);
        int[] vals = Values[playerIndex];
        indices.Sort((a, b) => vals[a].CompareTo(vals[b]));
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
