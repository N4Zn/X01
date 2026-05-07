using System.Collections.Generic;
using UnityEngine;

public class ListenSelectModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    public string[] TargetLetters; // [playerIdx]
    public string[][] Values;      // [playerIdx][boxIdx]
    public int[] BoxCount;        // [playerIdx]

    public ListenSelectModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        TargetLetters = new string[2];
        Values = new string[2][];
        BoxCount = new int[] { 3, 3 };
    }

    public int GetBoxCountForDifficulty(int playerIndex)
    {
        int score = (playerIndex == 0) ? Player1Score : Player2Score;
        if (score >= 8) return 5;
        if (score >= 4) return 4;
        return 3;
    }

    public void GenerateRound(int playerIndex)
    {
        int count = GetBoxCountForDifficulty(playerIndex);
        BoxCount[playerIndex] = count;

        List<char> pool = new List<char>();
        for (char c = 'A'; c <= 'Z'; c++) pool.Add(c);

        // Shuffle pool
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            char tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        // Target is the first one
        char target = pool[0];
        TargetLetters[playerIndex] = target.ToString();

        // Values contains target and count-1 distractors
        List<string> roundValues = new List<string>();
        for (int i = 0; i < count; i++) roundValues.Add(pool[i].ToString());

        // Shuffle round values so target isn't always at index 0
        for (int i = roundValues.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = roundValues[i]; roundValues[i] = roundValues[j]; roundValues[j] = tmp;
        }

        Values[playerIndex] = roundValues.ToArray();
    }

    public bool CheckTap(int playerIndex, int boxIndex)
    {
        if (boxIndex < 0 || boxIndex >= Values[playerIndex].Length) return false;
        return Values[playerIndex][boxIndex] == TargetLetters[playerIndex];
    }

    public void ResetGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        GameTimer = MaxGameTime;
    }
}
