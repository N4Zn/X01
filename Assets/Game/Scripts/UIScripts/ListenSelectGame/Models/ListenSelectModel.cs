using System.Collections.Generic;
using UnityEngine;

public class ListenSelectModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    public string TargetLetter; // Shared target
    public string[][] Values;      // [playerIdx][boxIdx]
    public int[] BoxCount;        // [playerIdx]

    public ListenSelectModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        TargetLetter = "";
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

        List<char> pool = new List<char>();
        for (char c = 'A'; c <= 'Z'; c++) pool.Add(c);

        // Shuffle pool
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            char tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        char target = pool[0];
        TargetLetter = target.ToString();

        // Generate same set of letters for both players to be fair
        List<string> roundValues = new List<string>();
        roundValues.Add(TargetLetter);

        int addedCount = 1;
        int poolIdx = 1;
        while (addedCount < count && poolIdx < pool.Count)
        {
            char candidate = pool[poolIdx];
            bool skip = false;

            // Check if adding this candidate violates I/Y rule
            if (candidate == 'I' || candidate == 'Y')
            {
                foreach (string val in roundValues)
                {
                    if ((candidate == 'I' && val == "Y") || (candidate == 'Y' && val == "I"))
                    {
                        skip = true;
                        break;
                    }
                }
            }

            if (!skip)
            {
                roundValues.Add(candidate.ToString());
                addedCount++;
            }
            poolIdx++;
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
        return Values[playerIndex][boxIndex] == TargetLetter;
    }

    public void ResetGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        GameTimer = MaxGameTime;
    }
}
