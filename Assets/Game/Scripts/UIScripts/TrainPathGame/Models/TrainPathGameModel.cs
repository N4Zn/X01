using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data model for TrainPath game - 3x3 grid puzzle
/// Each round generates a map with one hidden cell, player guesses its type
/// </summary>
public class TrainPathGameModel
{
    // Current puzzle for each player
    public TrainPathPuzzle Player1Puzzle { get; private set; }
    public TrainPathPuzzle Player2Puzzle { get; private set; }

    // Player scores
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }

    // Player selections (0=Left, 1=Straight, 2=Right)
    public int Player1Selection { get; set; }
    public int Player2Selection { get; set; }

    // Timer
    public float GameTimer { get; set; }
    public float MaxGameTime { get; set; }

    // Current round
    public int CurrentRound { get; set; }
    public int MaxRound { get; set; }

    // Maps selection index to cell type
    private static readonly TrainCellType[] SelectionToCellType = new TrainCellType[]
    {
        TrainCellType.TurnLeft,   // 0 = Left
        TrainCellType.Straight,   // 1 = Straight
        TrainCellType.TurnRight   // 2 = Right
    };

    public TrainPathGameModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        Player1Selection = -1;
        Player2Selection = -1;
        CurrentRound = 0;
        MaxRound = 20;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 120f;
        GameTimer = MaxGameTime;
    }

    /// <summary>
    /// Load path templates from MasterDataCache (CSV data)
    /// Must be called after TrainPathMaster is cached
    /// </summary>
    public void LoadQuestions()
    {
        TrainPathPuzzleGenerator.LoadTemplates();
        Debug.Log($"NDL: TrainPathGameModel - Templates loaded: {TrainPathPuzzleGenerator.IsLoaded()}");
    }

    /// <summary>
    /// Generate a new puzzle for a player
    /// </summary>
    public TrainPathPuzzle GenerateNewPuzzle(int playerIndex)
    {
        TrainPathPuzzle puzzle = TrainPathPuzzleGenerator.Generate();

        if (playerIndex == 0)
            Player1Puzzle = puzzle;
        else
            Player2Puzzle = puzzle;

        return puzzle;
    }

    /// <summary>
    /// Set player selection
    /// </summary>
    public void SetSelection(int playerIndex, int optionIndex)
    {
        if (playerIndex == 0)
            Player1Selection = optionIndex;
        else
            Player2Selection = optionIndex;
    }

    /// <summary>
    /// Reset player selection
    /// </summary>
    public void ResetSelection(int playerIndex)
    {
        if (playerIndex == 0)
            Player1Selection = -1;
        else
            Player2Selection = -1;
    }

    /// <summary>
    /// Check if the player's selected answer is correct
    /// Selection: 0=Left, 1=Straight, 2=Right
    /// </summary>
    public bool CheckAnswer(int playerIndex)
    {
        TrainPathPuzzle puzzle = playerIndex == 0 ? Player1Puzzle : Player2Puzzle;
        int selection = playerIndex == 0 ? Player1Selection : Player2Selection;

        if (puzzle == null || selection < 0 || selection > 2) return false;

        TrainCellType selectedType = SelectionToCellType[selection];
        return selectedType == puzzle.HiddenAnswer;
    }

    /// <summary>
    /// Get the correct answer as selection index (0=Left, 1=Straight, 2=Right)
    /// </summary>
    public int GetCorrectSelectionIndex(int playerIndex)
    {
        TrainPathPuzzle puzzle = playerIndex == 0 ? Player1Puzzle : Player2Puzzle;
        if (puzzle == null) return -1;

        for (int i = 0; i < SelectionToCellType.Length; i++)
        {
            if (SelectionToCellType[i] == puzzle.HiddenAnswer)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Get star reward for correct answer based on puzzle difficulty.
    /// Difficulty 1 = +1★, Difficulty 2 = +3★, Difficulty 3 = +5★
    /// </summary>
    public int GetStarsForDifficulty(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return 1;
            case 2: return 3;
            case 3: return 5;
            default: return 1;
        }
    }

    /// <summary>
    /// Get difficulty of the current puzzle for a player.
    /// Falls back to 1 if not set.
    /// </summary>
    public int GetPuzzleDifficulty(int playerIndex)
    {
        TrainPathPuzzle puzzle = playerIndex == 0 ? Player1Puzzle : Player2Puzzle;
        if (puzzle == null) return 1;
        return puzzle.Difficulty > 0 ? puzzle.Difficulty : 1;
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
        ResetSelection(0);
        ResetSelection(1);
        Player1Puzzle = null;
        Player2Puzzle = null;
    }
}
