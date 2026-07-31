using System.Collections.Generic;
using UnityEngine;

/// <summary>Loại ô trên board Monopoly.</summary>
public enum MonopolyTileType
{
    Question,
    LoseTurn,
    Reward,
    DoublePoints,
    MinusPoints,
    LuckyWheel,
}

/// <summary>FSM state cho MonopolyGameController — lượt-chơi (turn-based), khác hẳn nhịp
/// ShowQuestion/WaitAnswer/Feedback đối xứng 2 đội của MiniGameControllerBase nên không kế thừa
/// base đó, dùng thẳng CustomFSMManager (cùng engine reflection Kit đang dùng).</summary>
public enum MonopolyState
{
    Initialize,
    Tutorial,
    TurnStart,
    Rolling,
    Moving,
    ResolveTile,
    EndTurn,
    GameOver,
}

/// <summary>Sinh danh sách loại ô cho board — mặc định 40 ô (x2 bản gốc, cho khoảng cách giữa các
/// ô sát hơn khi xếp quanh cùng 1 viền), 60% Question + 40% chia đều 5 loại ô đặc biệt còn lại. Ô
/// đầu tiên (điểm xuất phát) luôn ép về Question để lượt đầu không rơi ngay vào ô đặc biệt trước
/// khi người chơi kịp hiểu luật.</summary>
public static class MonopolyBoard
{
    public const int DefaultTileCount = 40;

    public static MonopolyTileType[] GenerateLayout(int tileCount = DefaultTileCount)
    {
        var special = new List<MonopolyTileType>
        {
            MonopolyTileType.LoseTurn, MonopolyTileType.Reward, MonopolyTileType.DoublePoints,
            MonopolyTileType.MinusPoints, MonopolyTileType.LuckyWheel,
        };

        int specialCount = Mathf.RoundToInt(tileCount * 0.4f);
        var tiles = new List<MonopolyTileType>();
        for (int i = 0; i < specialCount; i++)
            tiles.Add(special[i % special.Count]);
        while (tiles.Count < tileCount)
            tiles.Add(MonopolyTileType.Question);

        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }

        int questionIdx = tiles.IndexOf(MonopolyTileType.Question);
        (tiles[0], tiles[questionIdx]) = (tiles[questionIdx], tiles[0]);

        return tiles.ToArray();
    }

    /// <summary>Giá trị điểm mỗi ô Question (random minValue-maxValue) — trả lời đúng tại ô đó được
    /// đúng số điểm này (xem MonopolyGameController — "vào ô, trả lời đúng thì được điểm tuỳ giá
    /// trị ô"). Ô không phải Question → giá trị 0 (không dùng tới, các ô đó có luật điểm riêng).</summary>
    public static int[] GenerateValues(MonopolyTileType[] layout, int minValue = 1, int maxValue = 5)
    {
        var values = new int[layout.Length];
        for (int i = 0; i < layout.Length; i++)
            values[i] = layout[i] == MonopolyTileType.Question ? Random.Range(minValue, maxValue + 1) : 0;
        return values;
    }
}
