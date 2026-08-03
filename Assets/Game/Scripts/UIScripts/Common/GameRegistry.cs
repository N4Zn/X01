/// <summary>
/// GameRegistry — nguồn sự thật duy nhất cho toàn bộ danh sách game.
///
/// Mỗi game được khai báo 1 lần bằng GameEntry (name + sceneName + engine).
/// Không có mảng song song, không có nguy cơ lệch index.
///
/// Cách thêm game mới:
///   Set(category, index, "TenGame", "SceneName", Engine.XxxGame);
///   → Thêm icon:  Assets/Resources/GameIcons/TenGame.png
///   → Thêm CSV:   Assets/Resources/TongHop/TenGame/  (chỉ với Engine.TongHopGame)
/// </summary>
public static class GameRegistry
{
    // ── Engine enum: nhóm game theo cơ chế ──────────────────────────────────
    public enum Engine
    {
        MathGame,       // Phép tính — AddUp, NumberAddUp, AddNumber
        TrainGame,      // Lưới xoay — TrainPath
        PathGame,       // Chọn đường — PathFinder
        TongHopGame,    // Choose + Matching từ CSV — ChuCai, SoDem, Numbers, TongHop...
        PlanetGame,     // Planet order/alphabet
        ListenGame,     // Audio-based
        ArcadeGame,     // Vận động / chiếu sàn — RiverCross, ...
        BalloonGame,    // Cooperative balloon-pop — BalloonAlpha, BalloonNumber
        ExploreGame,    // Khám phá / mô phỏng — SolarSystem, ...
        MiniGameKit,    // Game dựng từ Assets/Game/Scripts/Core/MiniGameKit — mỗi game 1 scene riêng
    }

    // ── GameEntry: 1 struct thay cho 2 mảng song song ────────────────────────
    public struct GameEntry
    {
        public string name;       // Tên game: dùng cho icon + CSV variant key
        public string sceneName;  // Unity scene cần load (null = chưa implement)
        public Engine engine;     // Nhóm cơ chế

        /// <summary>Game có scene đã làm xong → có thể bấm chơi.</summary>
        public bool IsImplemented => !string.IsNullOrEmpty(sceneName);

        /// <summary>Slot trống — không hiển thị trong grid.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(name);
    }

    // ── Kích thước lưới ──────────────────────────────────────────────────────
    public const int CATEGORY_COUNT   = 6;
    public const int MAX_PER_CATEGORY = 24;

    // ── Tên hiển thị cho từng tab category ───────────────────────────────────
    public static readonly string[] CategoryNames =
    {
        "Mam Non",  // 0
        "Lop 1",    // 1
        "Lop 2",    // 2
        "Lop 3",    // 3
        "Lop 4",    // 4
        "Lop 5",    // 5
    };

    // ── Bảng game ────────────────────────────────────────────────────────────
    public static readonly GameEntry[,] Games;

    static GameRegistry()
    {
        Games = new GameEntry[CATEGORY_COUNT, MAX_PER_CATEGORY];

        // ── Category 0: Mầm Non ──────────────────────────────────────────────
        Set(0, 0, "Counting5",         "TestTongHopGame", Engine.TongHopGame);
        Set(0, 1, "Counting",         "TestTongHopGame", Engine.TongHopGame);
        Set(0, 2, "AddNumber5",        "AddNumberGame",   Engine.MathGame);
        Set(0, 3, "AddNumber",        "AddNumberGame",   Engine.MathGame);
        Set(0, 4, "RiverCross",       "RiverCrossGame",  Engine.ArcadeGame);
        Set(0, 5, "SaveEnvironment",  "TestTongHopGame", Engine.TongHopGame);
		Set(0, 6, "ListenSelect", "ListenGame",      Engine.ListenGame);
        Set(0, 7, "ChuCai",       "ChuCaiGame",      Engine.ListenGame);
        Set(0, 8, "SoDem",        "SoDemGame",        Engine.ListenGame);
        Set(0, 9, "Numbers",      "NumbersGame",      Engine.ListenGame);
        Set(0, 10, "ChuCai2",  "BalloonGame", Engine.BalloonGame);
        Set(0, 11, "SoDem2", "BalloonGame", Engine.BalloonGame);
        Set(0, 12, "PlanetOrder",    "PlanetOrderGame",    Engine.PlanetGame);
        Set(0, 13, "PlanetAlphabet", "PlanetAlphabetGame", Engine.PlanetGame);
        Set(0, 14, "Fruit", "TestTongHopGame", Engine.TongHopGame);
        Set(0, 15, "Animal", "TestTongHopGame", Engine.TongHopGame);
        Set(0, 16, "Things",       "TestTongHopGame",  Engine.TongHopGame);
        Set(0, 17, "SolarSystem", "SolarSystemScene", Engine.ExploreGame);
        Set(0, 18, "WaterAnimal", "TestTongHopGame", Engine.TongHopGame);
        Set(0, 19, "SolarQuizVi",  "TestTongHopGame", Engine.TongHopGame);
        Set(0, 20, "SolarQuizEn", "TestTongHopGame", Engine.TongHopGame);
        Set(0, 21, "SolarOrder",  "TestTongHopGame", Engine.TongHopGame);

		
		
		
        Set(0, 22, "AddUp",       "AddUpGame",         Engine.MathGame);
        Set(0, 23, "NumberAddUp",      "NumberAddUpGame", Engine.MathGame);
        // ── Category 1: Phân tích ─────────────────────────────────────────────
        Set(1, 0, "TrainPath",  "TrainPathGame",  Engine.TrainGame);
        Set(1, 1, "PathFinder", "PathFinderGame", Engine.PathGame);
        Set(1, 2, "LaneDash",   "LaneDashGame",   Engine.ArcadeGame);

        // ── Category 2: Hình ảnh ── engine: PlanetGame ───────────────────────
        Set(2, 0, "PlanetOrder",    "PlanetOrderGame",    Engine.PlanetGame);
        Set(2, 1, "PlanetAlphabet", "PlanetAlphabetGame", Engine.PlanetGame);

        // ── Category 3: Trí nhớ ───────────────────────────────────────────────
        Set(3, 0, "PlanetAlphabet", "PlanetAlphabetGame", Engine.PlanetGame);

        // ── Category 4: Nhận biết ─────────────────────────────────────────────
        Set(4, 0, "WhoIsIt", "WhoIsItGame", Engine.MiniGameKit);
        Set(4, 1, "FamilySpellingJump", "FamilySpellingGame", Engine.MiniGameKit);
        Set(4, 2, "Monopoly", "MonopolyGame", Engine.MiniGameKit);
        Set(4, 3, "WordHuntMaze", "WordHuntMazeGame", Engine.MiniGameKit);

        // ── Category 5: Âm thanh ─────────────────────────────────────────────
        // ListenSelect / ChuCai / SoDem / Numbers: scene riêng, sinh nội dung procedurally.
        // TongHop / TestTongHop: dùng chung TestTongHopGame + CSV từ Resources/TongHop/{name}/
        Set(5, 0, "ListenSelect", "ListenGame",      Engine.ListenGame);
        Set(5, 1, "ChuCai",       "ChuCaiGame",      Engine.ListenGame);
        Set(5, 2, "SoDem",        "SoDemGame",        Engine.ListenGame);
        Set(5, 3, "Numbers",      "NumbersGame",      Engine.ListenGame);
        Set(5, 4, "TongHop",      "TestTongHopGame", Engine.TongHopGame);
        Set(5, 5, "TestTongHop",  "TestTongHopGame", Engine.TongHopGame);
    }

    // ── Helper ───────────────────────────────────────────────────────────────
    static void Set(int cat, int idx, string name, string scene, Engine engine)
    {
        Games[cat, idx] = new GameEntry { name = name, sceneName = scene, engine = engine };
    }
}
