using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural maze generator using DFS recursive backtracker.
/// Outputs a render grid (2W+1 x 2H+1) where cells are either wall or path.
/// Finds solution path, picks a junction, and determines correct direction.
/// </summary>
public class MazeGenerator
{
    /// <summary>
    /// Directions: 0=Up, 1=Right, 2=Down, 3=Left
    /// </summary>
    public static readonly int[] DR = { -1, 0, 1, 0 };
    public static readonly int[] DC = { 0, 1, 0, -1 };
    public static readonly string[] DirNames = { "up", "right", "down", "left" };

    /// <summary>
    /// Result of maze generation for one puzzle.
    /// </summary>
    public class MazePuzzle
    {
        public int LogicalWidth;                // e.g., 5
        public int LogicalHeight;               // e.g., 5
        public int RenderWidth;                 // 2*W+1
        public int RenderHeight;                // 2*H+1
        public bool[,] Grid;                    // true=path, false=wall (render grid)
        public Vector2Int Start;                // logical start cell
        public Vector2Int End;                  // logical end cell (destination)
        public Vector2Int RenderStart;          // render grid position of start
        public Vector2Int RenderEnd;            // render grid position of end
        public List<Vector2Int> SolutionPath;   // logical cells from start to end
        public Vector2Int JunctionCell;         // logical cell where player decides
        public Vector2Int RenderJunction;       // render grid position of junction
        public int FacingDirection;             // direction player is facing at junction (0-3)
        public int CorrectDirection;            // correct turn direction (0-3)
        public int CorrectChoice;              // 0=Left, 1=Straight, 2=Right
        public int[] AvailableDirections;       // up to 3 open directions at junction
        public int Difficulty;

        /// <summary>
        /// Convert logical cell to render grid position.
        /// </summary>
        public Vector2Int ToRender(Vector2Int logical)
        {
            return new Vector2Int(logical.x * 2 + 1, logical.y * 2 + 1);
        }
    }

    /// <summary>
    /// Generate a complete maze puzzle.
    /// </summary>
    public static MazePuzzle Generate(int difficulty)
    {
        int size;
        switch (difficulty)
        {
            case 1: size = 5; break;
            case 2: size = 5; break;
            case 3: size = 7; break;
            default: size = 5; break;
        }

        MazePuzzle puzzle = new MazePuzzle();
        puzzle.LogicalWidth = size;
        puzzle.LogicalHeight = size;
        puzzle.RenderWidth = size * 2 + 1;
        puzzle.RenderHeight = size * 2 + 1;
        puzzle.Difficulty = difficulty;

        // Generate maze structure
        bool[,] visited = new bool[size, size];
        bool[,,] walls = new bool[size, size, 4]; // [row, col, dir] true=wall exists
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                for (int d = 0; d < 4; d++)
                    walls[r, c, d] = true;

        // DFS recursive backtracker
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int startCell = new Vector2Int(0, 0);
        visited[startCell.x, startCell.y] = true;
        stack.Push(startCell);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<int> unvisitedNeighbors = new List<int>();

            for (int d = 0; d < 4; d++)
            {
                int nr = current.x + DR[d];
                int nc = current.y + DC[d];
                if (nr >= 0 && nr < size && nc >= 0 && nc < size && !visited[nr, nc])
                {
                    unvisitedNeighbors.Add(d);
                }
            }

            if (unvisitedNeighbors.Count > 0)
            {
                int chosenDir = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                int nr = current.x + DR[chosenDir];
                int nc = current.y + DC[chosenDir];

                // Remove wall between current and neighbor
                walls[current.x, current.y, chosenDir] = false;
                int oppositeDir = (chosenDir + 2) % 4;
                walls[nr, nc, oppositeDir] = false;

                visited[nr, nc] = true;
                stack.Push(new Vector2Int(nr, nc));
            }
            else
            {
                stack.Pop();
            }
        }

        // Build render grid
        puzzle.Grid = new bool[puzzle.RenderHeight, puzzle.RenderWidth];
        // All false (walls) by default

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                // Cell itself is path
                puzzle.Grid[r * 2 + 1, c * 2 + 1] = true;

                // Passages (remove wall between cells)
                if (!walls[r, c, 1] && c + 1 < size) // right
                    puzzle.Grid[r * 2 + 1, c * 2 + 2] = true;
                if (!walls[r, c, 2] && r + 1 < size) // down
                    puzzle.Grid[r * 2 + 2, c * 2 + 1] = true;
            }
        }

        // Choose start and end positions (on opposite sides)
        puzzle.Start = new Vector2Int(size - 1, 0);  // bottom-left
        puzzle.End = new Vector2Int(0, size - 1);     // top-right
        puzzle.RenderStart = puzzle.ToRender(puzzle.Start);
        puzzle.RenderEnd = puzzle.ToRender(puzzle.End);

        // BFS to find solution path
        puzzle.SolutionPath = FindPath(walls, size, puzzle.Start, puzzle.End);

        if (puzzle.SolutionPath == null || puzzle.SolutionPath.Count < 3)
        {
            // Fallback: regenerate
            return Generate(difficulty);
        }

        // Find a junction on the solution path
        if (!FindJunction(puzzle, walls, size))
        {
            // No good junction found, regenerate
            return Generate(difficulty);
        }

        return puzzle;
    }

    /// <summary>
    /// BFS pathfinding on logical maze.
    /// </summary>
    private static List<Vector2Int> FindPath(bool[,,] walls, int size, Vector2Int start, Vector2Int end)
    {
        bool[,] visited = new bool[size, size];
        Vector2Int[,] parent = new Vector2Int[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                parent[r, c] = new Vector2Int(-1, -1);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (current == end)
            {
                // Reconstruct path
                List<Vector2Int> path = new List<Vector2Int>();
                Vector2Int c = end;
                while (c.x >= 0 && c.y >= 0)
                {
                    path.Add(c);
                    c = parent[c.x, c.y];
                }
                path.Reverse();
                return path;
            }

            for (int d = 0; d < 4; d++)
            {
                if (walls[current.x, current.y, d]) continue;
                int nr = current.x + DR[d];
                int nc = current.y + DC[d];
                if (nr >= 0 && nr < size && nc >= 0 && nc < size && !visited[nr, nc])
                {
                    visited[nr, nc] = true;
                    parent[nr, nc] = current;
                    queue.Enqueue(new Vector2Int(nr, nc));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Find a suitable junction on the solution path where the player has 3 choices.
    /// Sets puzzle.JunctionCell, FacingDirection, CorrectDirection, CorrectChoice.
    /// </summary>
    private static bool FindJunction(MazePuzzle puzzle, bool[,,] walls, int size)
    {
        // Walk along solution path, find cells with 3+ open neighbors (excluding where we came from)
        List<int> junctionIndices = new List<int>();

        for (int i = 1; i < puzzle.SolutionPath.Count - 1; i++)
        {
            Vector2Int cell = puzzle.SolutionPath[i];
            int openDirs = 0;
            for (int d = 0; d < 4; d++)
            {
                if (!walls[cell.x, cell.y, d])
                    openDirs++;
            }
            // A junction has 3 or more open directions
            if (openDirs >= 3)
            {
                junctionIndices.Add(i);
            }
        }

        if (junctionIndices.Count == 0)
        {
            // No T-junction found. Try cells with 2+ open dirs (always the case on path)
            // Pick a cell where there's at least one wrong turn possible
            for (int i = 1; i < puzzle.SolutionPath.Count - 1; i++)
            {
                Vector2Int cell = puzzle.SolutionPath[i];
                Vector2Int prev = puzzle.SolutionPath[i - 1];
                Vector2Int next = puzzle.SolutionPath[i + 1];

                int facingDir = GetDirection(prev, cell);
                int correctDir = GetDirection(cell, next);

                // Count open directions excluding where we came from
                int fromDir = (facingDir + 2) % 4; // opposite of facing
                List<int> options = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    if (d == fromDir) continue;
                    if (!walls[cell.x, cell.y, d])
                        options.Add(d);
                }

                if (options.Count >= 2)
                {
                    junctionIndices.Add(i);
                }
            }
        }

        if (junctionIndices.Count == 0)
            return false;

        // Pick a random junction (prefer middle of path for better gameplay)
        int chosenIdx = junctionIndices[Random.Range(0, junctionIndices.Count)];
        Vector2Int junctionCell = puzzle.SolutionPath[chosenIdx];
        Vector2Int prevCell = puzzle.SolutionPath[chosenIdx - 1];
        Vector2Int nextCell = puzzle.SolutionPath[chosenIdx + 1];

        puzzle.JunctionCell = junctionCell;
        puzzle.RenderJunction = puzzle.ToRender(junctionCell);

        int facing = GetDirection(prevCell, junctionCell);
        puzzle.FacingDirection = facing;

        int correct = GetDirection(junctionCell, nextCell);
        puzzle.CorrectDirection = correct;

        // Determine available directions (excluding where we came from)
        int backDir = (facing + 2) % 4;
        List<int> availDirs = new List<int>();
        for (int d = 0; d < 4; d++)
        {
            if (d == backDir) continue;
            if (!walls[junctionCell.x, junctionCell.y, d])
                availDirs.Add(d);
        }
        puzzle.AvailableDirections = availDirs.ToArray();

        // Map correct direction to Left/Straight/Right relative to facing
        puzzle.CorrectChoice = GetRelativeChoice(facing, correct);

        return true;
    }

    /// <summary>
    /// Get direction from cell A to adjacent cell B (0=Up, 1=Right, 2=Down, 3=Left).
    /// </summary>
    public static int GetDirection(Vector2Int from, Vector2Int to)
    {
        int dr = to.x - from.x;
        int dc = to.y - from.y;

        if (dr == -1) return 0; // up
        if (dc == 1) return 1;  // right
        if (dr == 1) return 2;  // down
        if (dc == -1) return 3; // left
        return -1;
    }

    /// <summary>
    /// Convert an absolute direction to a relative choice (Left=0, Straight=1, Right=2)
    /// based on the facing direction.
    /// </summary>
    public static int GetRelativeChoice(int facing, int targetDir)
    {
        // Relative turn: (targetDir - facing + 4) % 4
        // 0 = same direction (straight)
        // 1 = turn right
        // 3 = turn left
        // 2 = turn back (shouldn't happen)
        int relative = (targetDir - facing + 4) % 4;
        switch (relative)
        {
            case 0: return 1; // straight
            case 1: return 2; // right
            case 3: return 0; // left
            default: return 1; // fallback to straight
        }
    }

    /// <summary>
    /// Get absolute direction from facing + relative choice.
    /// </summary>
    public static int GetAbsoluteDirection(int facing, int choice)
    {
        switch (choice)
        {
            case 0: return (facing + 3) % 4; // left
            case 1: return facing;            // straight
            case 2: return (facing + 1) % 4; // right
            default: return facing;
        }
    }
}
