using UnityEngine;

/// <summary>
/// Model for PathFinder game. Uses MazeGenerator to create procedural mazes.
/// Each player gets an independent maze puzzle per round.
/// </summary>
public class PathFinderGameModel
{
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public int CurrentRound { get; set; }
    public float GameTimer { get; set; }
    public int MaxGameTime { get; private set; }

    // Current puzzle for each player
    public MazeGenerator.MazePuzzle Player1Puzzle { get; private set; }
    public MazeGenerator.MazePuzzle Player2Puzzle { get; private set; }

    // Player selections: 0=Left, 1=Straight, 2=Right, -1=no selection
    public int Player1Selection { get; set; }
    public int Player2Selection { get; set; }

    public PathFinderGameModel()
    {
        Player1Score = 0;
        Player2Score = 0;
        CurrentRound = 0;
        Player1Selection = -1;
        Player2Selection = -1;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;
        GameTimer = MaxGameTime;
    }

    /// <summary>
    /// Generate a new maze puzzle for a player.
    /// Difficulty increases as rounds progress.
    /// </summary>
    public MazeGenerator.MazePuzzle GenerateNewPuzzle(int playerIndex)
    {
        int difficulty = GetDifficultyForRound(CurrentRound);
        MazeGenerator.MazePuzzle puzzle = MazeGenerator.Generate(difficulty);

        if (playerIndex == 0)
            Player1Puzzle = puzzle;
        else
            Player2Puzzle = puzzle;

        return puzzle;
    }

    public void SetSelection(int playerIndex, int choice)
    {
        if (playerIndex == 0)
            Player1Selection = choice;
        else
            Player2Selection = choice;
    }

    /// <summary>
    /// Check if the player's choice is correct.
    /// </summary>
    public bool CheckAnswer(int playerIndex)
    {
        MazeGenerator.MazePuzzle puzzle = playerIndex == 0 ? Player1Puzzle : Player2Puzzle;
        int selection = playerIndex == 0 ? Player1Selection : Player2Selection;

        if (puzzle == null || selection < 0) return false;
        return selection == puzzle.CorrectChoice;
    }

    /// <summary>
    /// Get score for correct answer based on difficulty.
    /// </summary>
    public int GetScoreForDifficulty(int difficulty)
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
    /// Difficulty ramps up over rounds.
    /// </summary>
    private int GetDifficultyForRound(int round)
    {
        if (round <= 5) return 1;
        if (round <= 12) return 2;
        return 3;
    }

    public void ResetForNewGame()
    {
        Player1Score = 0;
        Player2Score = 0;
        CurrentRound = 0;
        Player1Selection = -1;
        Player2Selection = -1;
        MaxGameTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;
        GameTimer = MaxGameTime;
    }
}
