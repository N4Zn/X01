using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MasterData;

/// <summary>
/// Data model for AddNumber game - manages question data and player state.
/// New gameplay: A + B = C equation with one hidden operand, 4 answer choices.
/// </summary>
public class AddNumberGameModel
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

    // Per-player difficulty level (1-6)
    public int[] PlayerLevel;
    public int[] MaxLevelReached;
    public int[] ConsecutiveCorrect;
    public int[] ConsecutiveWrong;
    public int[] TotalCorrectAtLevel1;
    public bool[] IsRecovering;
    public int[] PlayerRound;

    // Level state for multi-pick levels (4, 5, 6)
    public bool[] IsMultiPick;
    public int[] MultiPickTargetCount;
    public int[] MultiPickCurrentCount;
    public List<int>[] MultiPickSelectedIndices;
    public float[][] LastSelectionTime = new float[][] { new float[4], new float[4] };
    private const float ToggleCooldown = 2.0f;
    public List<int> MultiPickCorrectIndicesP1 = new List<int>();
    public List<int> MultiPickCorrectIndicesP2 = new List<int>();

    // Current question values
    public int[] ValA = new int[2];
    public int[] ValB = new int[2];
    public int[] ValC = new int[2];
    public string[] HiddenPos = new string[2]; // "a", "b", "c", "ab", "ac", "bc", "abc"

    // All available questions
    private List<AddUpMaster.Param> allQuestions;

    public AddNumberGameModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        CurrentRound = 0;
        MaxRound = 10;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;
        GameTimer = MaxGameTime;

        PlayerLevel = new int[] { 1, 1 };
        MaxLevelReached = new int[] { 1, 1 };
        ConsecutiveCorrect = new int[] { 0, 0 };
        ConsecutiveWrong = new int[] { 0, 0 };
        TotalCorrectAtLevel1 = new int[] { 0, 0 };
        IsRecovering = new bool[] { false, false };
        PlayerRound = new int[] { 0, 0 };

        IsMultiPick = new bool[] { false, false };
        MultiPickTargetCount = new int[] { 0, 0 };
        MultiPickCurrentCount = new int[] { 0, 0 };
        MultiPickSelectedIndices = new List<int>[] { new List<int>(), new List<int>() };
    }

    /// <summary>
    /// Generate a question procedurally based on current player level.
    /// </summary>
    public AddUpMaster.Param GetNextQuestion(int playerIndex)
    {
        PlayerRound[playerIndex]++;
        int level = PlayerLevel[playerIndex];
        IsMultiPick[playerIndex] = (level >= 4);
        MultiPickCurrentCount[playerIndex] = 0;
        MultiPickSelectedIndices[playerIndex].Clear();

        // Reset selection cooldowns for the new question
        for (int i = 0; i < 4; i++) LastSelectionTime[playerIndex][i] = 0f;

        AddUpMaster.Param q = GenerateQuestionByLevel(playerIndex, level);
        q.image_type = GetRandomImageType();

        if (playerIndex == 0) Player1Question = q;
        else Player2Question = q;
        return q;
    }

    private AddUpMaster.Param GenerateQuestionByLevel(int playerIndex, int level)
    {
        int maxSum = PlayerRound[playerIndex] <= 5 ? 5 : 10;
        int sum = Random.Range(2, maxSum + 1); // Start from 2 (1+1)
        int a = Random.Range(1, sum);
        int b = sum - a;

        ValA[playerIndex] = a;
        ValB[playerIndex] = b;
        ValC[playerIndex] = sum;

        string hidden = "c";
        int targetCount = 1;
        List<int> correctIndices = (playerIndex == 0) ? MultiPickCorrectIndicesP1 : MultiPickCorrectIndicesP2;
        correctIndices.Clear();

        switch (level)
        {
            case 1: hidden = "c"; break;
            case 2: hidden = "b"; break;
            case 3: hidden = "a"; break;
            case 4: hidden = "ab"; targetCount = 2; break;
            case 5: hidden = Random.Range(0, 2) == 0 ? "ac" : "bc"; targetCount = 2; break;
            case 6: hidden = "abc"; targetCount = 3; break;
        }

        HiddenPos[playerIndex] = hidden;
        MultiPickTargetCount[playerIndex] = targetCount;

        // Collect required values based on hidden position
        List<int> requiredValues = new List<int>();
        if (hidden.Contains("a")) requiredValues.Add(a);
        if (hidden.Contains("b")) requiredValues.Add(b);
        if (hidden.Contains("c")) requiredValues.Add(sum);

        // Generate 4 answers
        int[] answers = new int[4];
        List<int> availableSlots = new List<int> { 0, 1, 2, 3 };

        // Place correct values
        foreach (int val in requiredValues)
        {
            int slotIndex = Random.Range(0, availableSlots.Count);
            int slot = availableSlots[slotIndex];
            answers[slot] = val;
            correctIndices.Add(slot);
            availableSlots.RemoveAt(slotIndex);
        }

        // Fill remaining slots with wrong answers
        foreach (int slot in availableSlots)
        {
            int wrong;
            int attempts = 0;
            // Select a random correct value to base the distractor on (result +- 3)
            int baseVal = requiredValues[Random.Range(0, requiredValues.Count)];

            do
            {
                // Wrong answers should be within correct value +- 3
                wrong = baseVal + Random.Range(-3, 4);
                attempts++;
            } while ((wrong < 1 || wrong == a || wrong == b || wrong == sum || System.Array.IndexOf(answers, wrong) >= 0) && attempts < 25);

            // If we couldn't find a unique wrong answer within narrow range, fallback to general range
            if (attempts >= 25)
            {
                do {
                    wrong = Random.Range(1, maxSum + 5);
                    attempts++;
                } while ((wrong == a || wrong == b || wrong == sum || System.Array.IndexOf(answers, wrong) >= 0) && attempts < 50);
            }
            answers[slot] = wrong;
        }

        AddUpMaster.Param q = new AddUpMaster.Param();
        q.number_a = a;
        q.number_b = b;
        q.hidden_position = hidden;
        q.answer_1 = answers[0];
        q.answer_2 = answers[1];
        q.answer_3 = answers[2];
        // Note: AddUpMaster.Param only has answer_1, 2, 3 in its original definition usually.
        // We'll use a hack or assume we might need a custom storage if we can't modify the Param class.
        // For now, let's assume we can add answer_4 or we store it locally.
        q.difficulty = level;

        // Store answer_4 in a way that doesn't break if Param doesn't have it.
        // Since I can't see AddUpMaster definition, I'll store the answers in the model for safe access.
        SetStoredAnswers(playerIndex, answers);

        return q;
    }

    private int[][] _storedAnswers = new int[2][];
    private void SetStoredAnswers(int playerIndex, int[] answers)
    {
        _storedAnswers[playerIndex] = answers;
    }

    public int GetStoredAnswer(int playerIndex, int answerIndex)
    {
        return _storedAnswers[playerIndex][answerIndex];
    }

    public int[] GetStoredAnswers(int playerIndex)
    {
        return _storedAnswers[playerIndex];
    }

    /// <summary>
    /// Check answer and update difficulty logic.
    /// Returns: 0=wrong, 1=partial/toggle, 2=correct, 3=already answered/cooldown
    /// </summary>
    public int ProcessAnswer(int playerIndex, int answerIndex)
    {
        float currentTime = Time.time;
        int targetCount = MultiPickTargetCount[playerIndex];

        // Toggle logic for multi-pick
        if (IsMultiPick[playerIndex])
        {
            // Cooldown check for this specific answer
            if (currentTime - LastSelectionTime[playerIndex][answerIndex] < ToggleCooldown) return 3;

            if (MultiPickSelectedIndices[playerIndex].Contains(answerIndex))
            {
                MultiPickSelectedIndices[playerIndex].Remove(answerIndex);
                MultiPickCurrentCount[playerIndex]--;
                LastSelectionTime[playerIndex][answerIndex] = currentTime;
                return 1; // Unselected
            }
            else
            {
                // Last choice ends the round, no toggle allowed for the final one
                if (MultiPickCurrentCount[playerIndex] >= targetCount - 1)
                {
                    MultiPickSelectedIndices[playerIndex].Add(answerIndex);
                    MultiPickCurrentCount[playerIndex]++;
                    LastSelectionTime[playerIndex][answerIndex] = currentTime;

                    // VALIDATION phase: check if all selected indices are correct
                    List<int> correctIndices = (playerIndex == 0) ? MultiPickCorrectIndicesP1 : MultiPickCorrectIndicesP2;
                    bool allCorrect = true;
                    if (MultiPickSelectedIndices[playerIndex].Count != correctIndices.Count) allCorrect = false;
                    else
                    {
                        foreach (int idx in MultiPickSelectedIndices[playerIndex])
                        {
                            if (!correctIndices.Contains(idx)) { allCorrect = false; break; }
                        }
                    }

                    if (allCorrect)
                    {
                        OnAnswerCorrect(playerIndex);
                        return 2;
                    }
                    else
                    {
                        OnAnswerWrong(playerIndex);
                        return 0;
                    }
                }
                else
                {
                    // Regular selection (partial)
                    MultiPickSelectedIndices[playerIndex].Add(answerIndex);
                    MultiPickCurrentCount[playerIndex]++;
                    LastSelectionTime[playerIndex][answerIndex] = currentTime;
                    return 1;
                }
            }
        }
        else
        {
            // Single pick logic
            List<int> correctIndices = (playerIndex == 0) ? MultiPickCorrectIndicesP1 : MultiPickCorrectIndicesP2;
            if (correctIndices.Contains(answerIndex))
            {
                OnAnswerCorrect(playerIndex);
                return 2;
            }
            else
            {
                OnAnswerWrong(playerIndex);
                return 0;
            }
        }
    }

    private void OnAnswerCorrect(int playerIndex)
    {
        int level = PlayerLevel[playerIndex];

        // Scoring
        int points = level;
        if (playerIndex == 0) Player1Score += points;
        else Player2Score += points;

        ConsecutiveCorrect[playerIndex]++;
        ConsecutiveWrong[playerIndex] = 0;

        // Level up logic
        if (level == 1)
        {
            TotalCorrectAtLevel1[playerIndex]++;
            if (TotalCorrectAtLevel1[playerIndex] >= 10)
            {
                AdvanceLevel(playerIndex);
            }
        }
        else
        {
            if (IsRecovering[playerIndex])
            {
                if (ConsecutiveCorrect[playerIndex] >= 2)
                {
                    PlayerLevel[playerIndex] = MaxLevelReached[playerIndex];
                    IsRecovering[playerIndex] = false;
                    ConsecutiveCorrect[playerIndex] = 0;
                }
            }
            else if (ConsecutiveCorrect[playerIndex] >= 5)
            {
                AdvanceLevel(playerIndex);
            }
        }
    }

    private void AdvanceLevel(int playerIndex)
    {
        if (PlayerLevel[playerIndex] < 6)
        {
            PlayerLevel[playerIndex]++;
            MaxLevelReached[playerIndex] = Mathf.Max(MaxLevelReached[playerIndex], PlayerLevel[playerIndex]);
            ConsecutiveCorrect[playerIndex] = 0;
        }
    }

    private void OnAnswerWrong(int playerIndex)
    {
        ConsecutiveWrong[playerIndex]++;
        ConsecutiveCorrect[playerIndex] = 0;

        if (ConsecutiveWrong[playerIndex] >= 2)
        {
            if (PlayerLevel[playerIndex] > 1)
            {
                PlayerLevel[playerIndex]--;
                IsRecovering[playerIndex] = true;
                ConsecutiveWrong[playerIndex] = 0;
            }
        }
    }

    public string GetRandomImageType()
    {
        string[] animals = {
            "cow", "dog", "owl", "pig", "bear", "duck", "frog", "goat", "chick", "hippo",
            "horse", "moose", "panda", "rhino", "sloth", "snake", "whale", "zebra", "monkey",
            "parrot", "rabbit", "walrus", "buffalo", "chicken", "giraffe", "gorilla", "narwhal",
            "penguin", "elephant", "crocodile"
        };
        return animals[Random.Range(0, animals.Length)];
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

        PlayerLevel = new int[] { 1, 1 };
        MaxLevelReached = new int[] { 1, 1 };
        ConsecutiveCorrect = new int[] { 0, 0 };
        ConsecutiveWrong = new int[] { 0, 0 };
        TotalCorrectAtLevel1 = new int[] { 0, 0 };
        IsRecovering = new bool[] { false, false };
        PlayerRound = new int[] { 0, 0 };

        IsMultiPick = new bool[] { false, false };
        MultiPickTargetCount = new int[] { 0, 0 };
        MultiPickCurrentCount = new int[] { 0, 0 };
        MultiPickSelectedIndices = new List<int>[] { new List<int>(), new List<int>() };
        LastSelectionTime = new float[][] { new float[4], new float[4] };
    }
}
