using System.Collections.Generic;
using UnityEngine;

public class SoDemModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    public string TargetValue; // Shared target
    public string[][] Values;      // [playerIdx][boxIdx]
    public int[] BoxCount;        // [playerIdx]

    public SoDemModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        TargetValue = "";
        Values = new string[2][];
        BoxCount = new int[] { 3, 3 };
    }

    public int GetBoxCountForDifficulty()
    {
        int avgScore = (Player1Score + Player2Score) / 2;
        if (avgScore >= 8) return 5;
        if (avgScore >= 4) return 4;
        return 3;
    }

    public void GenerateRound()
    {
        int count = GetBoxCountForDifficulty();
        BoxCount[0] = count;
        BoxCount[1] = count;

        List<int> pool = new List<int>();
        for (int i = 0; i <= 10; i++) pool.Add(i);

        // Shuffle pool
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        TargetValue = pool[0].ToString();

        // Generate same set of numbers for both players to be fair
        List<string> roundValues = new List<string>();
        for (int i = 0; i < count; i++)
        {
            roundValues.Add(pool[i].ToString());
        }

        // For Player 1
        List<string> p1List = new List<string>(roundValues);
        Shuffle(p1List);
        Values[0] = p1List.ToArray();

        // For Player 2
        List<string> p2List = new List<string>(roundValues);
        Shuffle(p2List);
        Values[1] = p2List.ToArray();
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    public bool CheckTap(int playerIndex, int boxIndex)
    {
        return Values[playerIndex][boxIndex] == TargetValue;
    }

    public void ResetGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        GameTimer = MaxGameTime;
    }
}
