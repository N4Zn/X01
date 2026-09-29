using System.Collections.Generic;
using UnityEngine;
using MasterData;

/// <summary>
/// Cell types for the train path grid
/// </summary>
public enum TrainCellType
{
    Straight,
    TurnLeft,
    TurnRight,
    Bush
}

public enum PathExitSide
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>
/// Represents a single 4x4 train path puzzle.
/// One cell is hidden and the player must guess its type.
/// </summary>
public class TrainPathPuzzle
{
    public const int GRID_SIZE = 4;
    public const int CELL_COUNT = GRID_SIZE * GRID_SIZE; // 16

    public TrainCellType[,] Grid;
    public int HiddenRow;
    public int HiddenCol;
    public TrainCellType HiddenAnswer;

    public int StartRow;
    public int StartCol;
    public PathExitSide EntrySide;
    public PathExitSide ExitSide;
    public List<Vector2Int> PathCells;
    public Vector2Int ExitEdgeCell;
    public Vector2Int EntryPoint;
    public Vector2Int ExitPoint;

    // Key: (row*GRID_SIZE+col), Value: (entryDir, exitDir)
    public Dictionary<int, Vector2Int[]> CellDirections;

    public int Difficulty;
    public float InitialRotation;

    public TrainPathPuzzle()
    {
        Grid = new TrainCellType[GRID_SIZE, GRID_SIZE];
        PathCells = new List<Vector2Int>();
        CellDirections = new Dictionary<int, Vector2Int[]>();
    }
}

/// <summary>
/// Generates TrainPath puzzles from CSV-loaded templates
/// </summary>
public static class TrainPathPuzzleGenerator
{
    private struct PathTemplate
    {
        public Vector2Int Entry;
        public Vector2Int[] Path;
        public Vector2Int Exit;
        public int Difficulty;
    }

    private static List<PathTemplate> _templates;
    private static bool _loaded = false;

    public static void LoadTemplates()
    {
        _templates = new List<PathTemplate>();

        TrainPathMaster master = MasterDataCache.GetCache<TrainPathMaster>();
        if (master == null || master.param.Count == 0)
        {
            Debug.LogWarning("TrainPathPuzzleGenerator: No CSV data, using fallback");
            LoadFallbackTemplates();
            _loaded = true;
            return;
        }

        foreach (var p in master.param)
        {
            PathTemplate t = new PathTemplate();
            t.Entry = ParsePoint(p.entry);
            t.Path = ParsePath(p.path);
            t.Exit = ParsePoint(p.exit);
            t.Difficulty = p.difficulty;
            _templates.Add(t);
        }

        _loaded = true;
        Debug.Log($"TrainPathPuzzleGenerator: Loaded {_templates.Count} templates");
    }

    private static void LoadFallbackTemplates()
    {
        // 4x4 fallback templates
        _templates.Add(new PathTemplate {
            Entry = V(-1,1), Path = new[]{ V(0,1), V(1,1), V(2,1), V(3,1) }, Exit = V(4,1), Difficulty = 1
        });
        _templates.Add(new PathTemplate {
            Entry = V(-1,0), Path = new[]{ V(0,0), V(0,1), V(1,1), V(2,1), V(3,1) }, Exit = V(4,1), Difficulty = 1
        });
        _templates.Add(new PathTemplate {
            Entry = V(0,-1), Path = new[]{ V(0,0), V(0,1), V(0,2), V(1,2), V(2,2), V(3,2) }, Exit = V(4,2), Difficulty = 1
        });
        _templates.Add(new PathTemplate {
            Entry = V(-1,2), Path = new[]{ V(0,2), V(1,2), V(1,1), V(2,1), V(2,0), V(3,0) }, Exit = V(4,0), Difficulty = 1
        });
    }

    private static Vector2Int ParsePoint(string s)
    {
        string[] parts = s.Trim().Split(':');
        return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private static Vector2Int[] ParsePath(string s)
    {
        string[] cells = s.Trim().Split('|');
        Vector2Int[] result = new Vector2Int[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            result[i] = ParsePoint(cells[i]);
        return result;
    }

    private static Vector2Int V(int r, int c) => new Vector2Int(r, c);

    public static TrainPathPuzzle Generate()
    {
        if (!_loaded) LoadTemplates();

        TrainPathPuzzle puzzle = new TrainPathPuzzle();
        int gs = TrainPathPuzzle.GRID_SIZE;

        int templateIndex = Random.Range(0, _templates.Count);
        PathTemplate t = _templates[templateIndex];

        // Initialize all cells as Bush
        for (int r = 0; r < gs; r++)
            for (int c = 0; c < gs; c++)
                puzzle.Grid[r, c] = TrainCellType.Bush;

        // Compute cell types from path
        for (int i = 0; i < t.Path.Length; i++)
        {
            Vector2Int prev = i == 0 ? t.Entry : t.Path[i - 1];
            Vector2Int curr = t.Path[i];
            Vector2Int next = i == t.Path.Length - 1 ? t.Exit : t.Path[i + 1];
            puzzle.Grid[curr.x, curr.y] = ComputeCellType(prev, curr, next);
        }

        puzzle.PathCells = new List<Vector2Int>(t.Path);
        puzzle.StartRow = t.Path[0].x;
        puzzle.StartCol = t.Path[0].y;
        puzzle.EntrySide = GetSideFromPoint(t.Entry);
        puzzle.ExitSide = GetSideFromPoint(t.Exit);
        puzzle.ExitEdgeCell = t.Path[t.Path.Length - 1];
        puzzle.EntryPoint = t.Entry;
        puzzle.ExitPoint = t.Exit;
        puzzle.Difficulty = t.Difficulty;

        // Per-cell direction info
        for (int i = 0; i < t.Path.Length; i++)
        {
            Vector2Int prev = i == 0 ? t.Entry : t.Path[i - 1];
            Vector2Int curr = t.Path[i];
            Vector2Int next = i == t.Path.Length - 1 ? t.Exit : t.Path[i + 1];
            int key = curr.x * gs + curr.y;
            puzzle.CellDirections[key] = new Vector2Int[] { curr - prev, next - curr };
        }

        puzzle.InitialRotation = 0f;

        // Pick random track cell to hide (exclude start)
        List<int> hideableIndices = new List<int>();
        for (int i = 1; i < t.Path.Length; i++)
            hideableIndices.Add(i);

        if (hideableIndices.Count > 0)
        {
            int hiddenIdx = hideableIndices[Random.Range(0, hideableIndices.Count)];
            puzzle.HiddenRow = t.Path[hiddenIdx].x;
            puzzle.HiddenCol = t.Path[hiddenIdx].y;
            puzzle.HiddenAnswer = puzzle.Grid[puzzle.HiddenRow, puzzle.HiddenCol];
        }

        return puzzle;
    }

    public static bool IsLoaded() => _loaded;

    private static TrainCellType ComputeCellType(Vector2Int prev, Vector2Int curr, Vector2Int next)
    {
        Vector2Int inDir = curr - prev;
        Vector2Int outDir = next - curr;
        if (inDir == outDir) return TrainCellType.Straight;
        int cross = inDir.x * outDir.y - inDir.y * outDir.x;
        return cross > 0 ? TrainCellType.TurnLeft : TrainCellType.TurnRight;
    }

    private static PathExitSide GetSideFromPoint(Vector2Int point)
    {
        if (point.x < 0) return PathExitSide.Top;
        if (point.x > TrainPathPuzzle.GRID_SIZE - 1) return PathExitSide.Bottom;
        if (point.y < 0) return PathExitSide.Left;
        return PathExitSide.Right;
    }

    public static string GetCellSymbol(TrainCellType ct) { switch(ct) { case TrainCellType.Straight: return "|"; case TrainCellType.TurnLeft: return "<"; case TrainCellType.TurnRight: return ">"; case TrainCellType.Bush: return "#"; default: return "?"; } }
    public static string GetCellName(TrainCellType ct) { switch(ct) { case TrainCellType.Straight: return "Straight"; case TrainCellType.TurnLeft: return "Left"; case TrainCellType.TurnRight: return "Right"; case TrainCellType.Bush: return "Bush"; default: return "?"; } }
    public static Color GetCellColor(TrainCellType ct) { switch(ct) { case TrainCellType.Straight: return new Color(0.6f,0.5f,0.3f); case TrainCellType.TurnLeft: return new Color(0.7f,0.55f,0.2f); case TrainCellType.TurnRight: return new Color(0.7f,0.55f,0.2f); case TrainCellType.Bush: return new Color(0.2f,0.6f,0.2f); default: return Color.gray; } }
}
