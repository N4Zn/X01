using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MasterData;

/// <summary>
/// Data model for AddUp game - manages question data and player state.
/// New gameplay: A + B = C equation with one hidden operand, 4 answer choices.
/// </summary>
public class AddUpGameModel
{
    // Current question data for each player
    public AddUpMaster.Param Player1Question { get; private set; }
    public AddUpMaster.Param Player2Question { get; private set; }

    // Player scores
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }

    // Timer
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    // Current round
    public int CurrentRound { get; set; }
    public int MaxRound { get; set; }

    // Per-player difficulty level (1-4), advances with score
    public int[] PlayerLevel;

    // Level 4 state: player must pick 2 answers that sum to target
    public bool[] IsLevel4;
    public int[] Level4Target;           // the target sum
    public int[][] Level4Choices;        // 3 answer values
    public int[] Level4CorrectA;         // index of first correct choice
    public int[] Level4CorrectB;         // index of second correct choice
    public int[] Level4PickCount;        // how many picks so far
    public int[] Level4FirstPick;        // index of first picked answer

    // All available questions
    private List<AddUpMaster.Param> allQuestions;
    private List<int> usedQuestionIndicesP1;
    private List<int> usedQuestionIndicesP2;

    public AddUpGameModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        CurrentRound = 0;
        MaxRound = 10;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;
        usedQuestionIndicesP1 = new List<int>();
        usedQuestionIndicesP2 = new List<int>();
        PlayerLevel = new int[] { 1, 1 };
        IsLevel4 = new bool[] { false, false };
        Level4Target = new int[2];
        Level4Choices = new int[2][];
        Level4CorrectA = new int[2];
        Level4CorrectB = new int[2];
        Level4PickCount = new int[2];
        Level4FirstPick = new int[] { -1, -1 };
    }

    /// <summary>
    /// Get difficulty level based on player score.
    /// 0-2 = Level 1 (sum < 5), 3-5 = Level 2 (sum < 10), 6-9 = Level 3 (sum < 20), 10+ = Level 4 (pick 2)
    /// </summary>
    public int GetLevelForScore(int score)
    {
        if (score >= 10) return 4;
        if (score >= 6) return 3;
        if (score >= 3) return 2;
        return 1;
    }

    public void LoadQuestions()
    {
        AddUpMaster master = MasterDataCache.GetCache<AddUpMaster>();
        if (master != null)
        {
            allQuestions = new List<AddUpMaster.Param>(master.param);
        }
        else
        {
            allQuestions = new List<AddUpMaster.Param>();
            Debug.LogWarning("AddUpMaster data not loaded!");
        }
    }

    /// <summary>
    /// Generate a question procedurally based on current player level.
    /// </summary>
    public AddUpMaster.Param GetNextQuestion(int playerIndex)
    {
        int score = playerIndex == 0 ? Player1Score : Player2Score;
        int level = GetLevelForScore(score);
        PlayerLevel[playerIndex] = level;

        if (level == 4)
        {
            return GenerateLevel4(playerIndex);
        }

        IsLevel4[playerIndex] = false;
        AddUpMaster.Param q = GenerateProceduralQuestion(level);

        if (playerIndex == 0) Player1Question = q;
        else Player2Question = q;
        return q;
    }

    private AddUpMaster.Param GenerateProceduralQuestion(int level)
    {
        int maxSum;
        switch (level)
        {
            case 1: maxSum = 5; break;
            case 2: maxSum = 10; break;
            default: maxSum = 20; break;
        }

        // Generate A + B where A+B < maxSum
        int sum = Random.Range(2, maxSum);
        int a = Random.Range(1, sum);
        int b = sum - a;

        // Random hidden position
        string hidden = Random.Range(0, 2) == 0 ? "a" : "b";
        int hiddenVal = hidden == "a" ? a : b;

        // Generate 3 answers: 1 correct + 2 wrong
        int correctSlot = Random.Range(0, 3); // 0, 1, or 2
        int[] answers = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (i == correctSlot)
            {
                answers[i] = hiddenVal;
            }
            else
            {
                int wrong;
                int attempts = 0;
                do
                {
                    wrong = Random.Range(Mathf.Max(1, hiddenVal - 3), hiddenVal + 4);
                    attempts++;
                } while ((wrong == hiddenVal || wrong < 0 || System.Array.IndexOf(answers, wrong) >= 0) && attempts < 20);
                if (wrong == hiddenVal) wrong = hiddenVal + (i == 0 ? 1 : -1);
                if (wrong < 0) wrong = hiddenVal + 1;
                answers[i] = wrong;
            }
        }

        AddUpMaster.Param q = new AddUpMaster.Param();
        q.question_id = -1;
        q.number_a = a;
        q.number_b = b;
        q.hidden_position = hidden;
        q.answer_1 = answers[0];
        q.answer_2 = answers[1];
        q.answer_3 = answers[2];
        q.correct_answer = correctSlot + 1; // 1-based
        q.image_type = GetRandomImageType();
        q.difficulty = level;
        return q;
    }

    /// <summary>
    /// Level 4: ? + ? = target. Player picks 2 of 3 numbers that add up to target.
    /// </summary>
    private AddUpMaster.Param GenerateLevel4(int playerIndex)
    {
        IsLevel4[playerIndex] = true;
        Level4PickCount[playerIndex] = 0;
        Level4FirstPick[playerIndex] = -1;

        int target = Random.Range(5, 16); // target sum 5-15
        int a = Random.Range(1, target);
        int b = target - a;

        // 3rd wrong choice
        int c;
        int attempts = 0;
        do
        {
            c = Random.Range(1, target);
            attempts++;
        } while ((c == a || c == b || c + a == target || c + b == target) && attempts < 30);

        // Shuffle into 3 slots
        int[] choices = { a, b, c };
        // Fisher-Yates shuffle
        for (int i = 2; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = choices[i]; choices[i] = choices[j]; choices[j] = tmp;
        }

        Level4Target[playerIndex] = target;
        Level4Choices[playerIndex] = choices;

        // Find correct pair indices
        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
                if (choices[i] + choices[j] == target)
                {
                    Level4CorrectA[playerIndex] = i;
                    Level4CorrectB[playerIndex] = j;
                }

        // Create a Param for display compatibility
        AddUpMaster.Param q = new AddUpMaster.Param();
        q.question_id = -1;
        q.number_a = 0;
        q.number_b = 0;
        q.hidden_position = "a";
        q.answer_1 = choices[0];
        q.answer_2 = choices[1];
        q.answer_3 = choices[2];
        q.correct_answer = -1; // not used for level 4
        q.image_type = GetRandomImageType();
        q.difficulty = 4;

        if (playerIndex == 0) Player1Question = q;
        else Player2Question = q;
        return q;
    }

    /// <summary>
    /// Level 4: check a tap. Returns: 0=wrong pair, 1=first pick ok, 2=complete (both correct)
    /// </summary>
    public int CheckLevel4Tap(int playerIndex, int answerIndex)
    {
        if (Level4PickCount[playerIndex] == 0)
        {
            // First pick — must be one of the correct pair
            if (answerIndex == Level4CorrectA[playerIndex] || answerIndex == Level4CorrectB[playerIndex])
            {
                Level4FirstPick[playerIndex] = answerIndex;
                Level4PickCount[playerIndex] = 1;
                return 1; // first pick accepted
            }
            return 0; // wrong
        }
        else
        {
            // Second pick — must be the OTHER correct index
            int first = Level4FirstPick[playerIndex];
            int expectedSecond = (first == Level4CorrectA[playerIndex])
                ? Level4CorrectB[playerIndex]
                : Level4CorrectA[playerIndex];

            if (answerIndex == expectedSecond)
            {
                Level4PickCount[playerIndex] = 2;
                return 2; // complete
            }
            return 0; // wrong
        }
    }

    private string GetRandomImageType()
    {
        string[] types = { "train", "coin", "apple", "star" };
        return types[Random.Range(0, types.Length)];
    }

    /// <summary>
    /// Get the sum C = A + B for a question
    /// </summary>
    public int GetSum(AddUpMaster.Param question)
    {
        if (question == null) return 0;
        return question.number_a + question.number_b;
    }

    /// <summary>
    /// Get the hidden number value for a question
    /// </summary>
    public int GetHiddenValue(AddUpMaster.Param question)
    {
        if (question == null) return 0;
        return question.hidden_position == "a" ? question.number_a : question.number_b;
    }

    /// <summary>
    /// Get the visible number value for a question
    /// </summary>
    public int GetVisibleValue(AddUpMaster.Param question)
    {
        if (question == null) return 0;
        return question.hidden_position == "a" ? question.number_b : question.number_a;
    }

    /// <summary>
    /// Check if the selected answer (1-3) is correct
    /// </summary>
    public bool CheckAnswer(int playerIndex, int answerIndex)
    {
        AddUpMaster.Param question = playerIndex == 0 ? Player1Question : Player2Question;
        if (question == null) return false;

        // answerIndex is 0-based, correct_answer is 1-based
        return (answerIndex + 1) == question.correct_answer;
    }

    /// <summary>
    /// Get answer value by index (0-2)
    /// </summary>
    public int GetAnswerValue(AddUpMaster.Param question, int answerIndex)
    {
        if (question == null) return 0;

        switch (answerIndex)
        {
            case 0: return question.answer_1;
            case 1: return question.answer_2;
            case 2: return question.answer_3;
            default: return 0;
        }
    }

    /// <summary>
    /// Get star reward for correct answer based on question difficulty.
    /// Difficulty 1 = +1★, Difficulty 2 = +3★, Difficulty 3 = +5★
    /// </summary>
    public int GetStarsForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 1;
            case 2: return 2;
            case 3: return 3;
            case 4: return 5;
            default: return 1;
        }
    }

    /// <summary>
    /// Get difficulty of the current question for a player.
    /// Falls back to 1 if not set.
    /// </summary>
    public int GetQuestionDifficulty(int playerIndex)
    {
        AddUpMaster.Param question = playerIndex == 0 ? Player1Question : Player2Question;
        if (question == null) return 1;
        return question.difficulty > 0 ? question.difficulty : 1;
    }

    /// <summary>
    /// Reset all game data for a new game
    /// </summary>
    public void ResetGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        CurrentRound = 0;
        GameTimer = MaxGameTime;
        usedQuestionIndicesP1.Clear();
        usedQuestionIndicesP2.Clear();
        PlayerLevel = new int[] { 1, 1 };
        IsLevel4 = new bool[] { false, false };
        Level4PickCount = new int[2];
        Level4FirstPick = new int[] { -1, -1 };
    }
}
