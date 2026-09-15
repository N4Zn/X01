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
    // ── BgmTrack: nhạc nền của mỗi game ─────────────────────────────────────
    public enum BgmTrack
    {
        Gameplay    = 0,  // mặc định — MusicManager.PlayGameplayMusic()
        SolarSystem = 1,  // MusicManager.PlaySolarSystemMusic()
        None        = 2,  // không phát nhạc
    }

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
        public string   name;              // Tên game: dùng cho icon + CSV variant key
        public string   sceneName;         // Unity scene cần load (null = chưa implement)
        public Engine   engine;            // Nhóm cơ chế

        // Visual / audio identity — khai báo 1 lần ở đây, không cần per-variant config.json
        public BgmTrack bgmTrack;          // Nhạc nền (default = Gameplay)
        public string   backgroundSprite;  // Resources path đến texture nền ("" = dùng default của prefab)
        public int      pointsPerCorrect;  // 0 = dùng mặc định (1 điểm/câu đúng)
        public bool     hideScoreBars;     // true → ẩn fill bar + star icon (vd: SolarQuiz)

        /// <summary>Game có scene đã làm xong → có thể bấm chơi.</summary>
        public bool IsImplemented => !string.IsNullOrEmpty(sceneName);

        /// <summary>Slot trống — không hiển thị trong grid.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(name);
    }

    // ── Kích thước lưới ──────────────────────────────────────────────────────
    // CATEGORY_COUNT giờ là trục MÔN HỌC (trước đây là cấp lớp Mầm Non/Lớp 1-5 — trục cấp
    // lớp bị bỏ, "chia sau" theo quyết định của user, xem CLAUDE.md Track A). Có thể tăng thêm
    // nữa sau này — chỉ cần tăng số này + thêm dòng CategoryNames + Set() tương ứng, không cần
    // sửa gì khác.
    public const int CATEGORY_COUNT   = 9;
    public const int MAX_PER_CATEGORY = 24;

    // ── Tên hiển thị cho từng tab category (= môn học) ────────────────────────
    public static readonly string[] CategoryNames =
    {
        "Toán",           // 0
        "Tiếng Việt",     // 1
        "Tiếng Anh",      // 2 — chưa có game nào, để sẵn chỗ
        "Khoa học",       // 3
        "Kỹ năng sống",   // 4
        "Tư duy - Logic", // 5
        "Trí nhớ",        // 6
        "Vận động",       // 7
        "Khác",           // 8
    };

    // ── Bảng game ────────────────────────────────────────────────────────────
    public static readonly GameEntry[,] Games;

    static GameRegistry()
    {
        Games = new GameEntry[CATEGORY_COUNT, MAX_PER_CATEGORY];

        // ── Category 0: Toán ─────────────────────────────────────────────────
        Set(0, 0, "Counting5",  "TestTongHopGame", Engine.TongHopGame);
        Set(0, 1, "Counting",   "TestTongHopGame", Engine.TongHopGame);
        Set(0, 2, "AddNumber5", "AddNumberGame",   Engine.MathGame);
        Set(0, 3, "AddNumber",  "AddNumberGame",   Engine.MathGame);
        Set(0, 4, "SoDem",      "SoDemGame",       Engine.ListenGame);
        Set(0, 5, "SoDem2",     "BalloonGame",     Engine.BalloonGame);
        Set(0, 6, "Numbers",    "NumbersGame",     Engine.ListenGame);

        // ── Category 1: Tiếng Việt ───────────────────────────────────────────
        Set(1, 0, "ListenSelect",       "ListenGame",          Engine.ListenGame);
        Set(1, 1, "ChuCai",             "ChuCaiGame",          Engine.ListenGame);
        Set(1, 2, "ChuCai2",            "BalloonGame",         Engine.BalloonGame);
        Set(1, 3, "FamilySpellingJump", "FamilySpellingGame",  Engine.MiniGameKit);
        Set(1, 4, "WordHuntMaze",       "WordHuntMazeGame",    Engine.MiniGameKit);
        Set(1, 5, "SentenceBuilder",    "SentenceBuilderGame", Engine.MiniGameKit);

        // ── Category 2: Tiếng Anh ────────────────────────────────────────────
        // Chưa có game riêng cho tiếng Anh (SolarQuizEn chỉ là bản dịch câu hỏi khoa học,
        // không phải nội dung dạy tiếng Anh) — để trống, thêm game vào đây khi có.

        // ── Category 3: Khoa học (khám phá tự nhiên - xã hội) ────────────────
        Set(3, 0,  "SaveEnvironment",  "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 1,  "Fruit",            "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 2,  "Animal",           "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 3,  "WaterAnimal",      "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 4,  "Things",           "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 5,  "SolarSystem",      "SolarSystemScene",     Engine.ExploreGame, BgmTrack.SolarSystem);
        Set(3, 6,  "SolarSystemVi",    "SolarSystemVi",        Engine.ExploreGame, BgmTrack.SolarSystem);
        Set(3, 7,  "SolarQuizEn",      "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10, hideScoreBars: true);
        Set(3, 8,  "SolarQuizVi",      "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10, hideScoreBars: true);
        Set(3, 9,  "SolarOrder",       "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10);
        Set(3, 10, "SolarOrder2",      "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", hideScoreBars: true);
        Set(3, 11, "SaveTheAstronaut", "SaveTheAstronautGame", Engine.MiniGameKit);

        // ── Category 4: Kỹ năng sống ──────────────────────────────────────────
        Set(4, 0, "WhoIsIt",      "WhoIsItGame",      Engine.MiniGameKit);
        Set(4, 1, "FamilyMember", "FamilyMemberGame", Engine.MiniGameKit);

        // ── Category 5: Tư duy - Logic ────────────────────────────────────────
        Set(5, 0, "PathFinder", "PathFinderGame", Engine.PathGame);
        Set(5, 1, "Monopoly",   "MonopolyGame",   Engine.MiniGameKit);

        // ── Category 6: Trí nhớ ───────────────────────────────────────────────
        Set(6, 0, "PlanetOrder",    "PlanetOrderGame",    Engine.PlanetGame);
        Set(6, 1, "PlanetAlphabet", "PlanetAlphabetGame", Engine.PlanetGame);
        Set(6, 2, "TrainPath",      "TrainPathGame",      Engine.TrainGame);

        // ── Category 7: Vận động ──────────────────────────────────────────────
        Set(7, 0, "RiverCross", "RiverCrossGame", Engine.ArcadeGame);
        Set(7, 1, "LaneDash",   "LaneDashGame",   Engine.ArcadeGame);

        // ── Category 8: Khác (giải trí / chưa rõ mục tiêu giáo dục / công cụ test) ──
        // TongHop, TestTongHop: bộ câu hỏi CSV "tổng hợp" nhiều chủ đề — chưa thuộc hẳn 1 môn.
        // FRTest: màn test nhận diện khuôn mặt (công cụ debug), không phải nội dung học.
        Set(8, 0, "TongHop",     "TestTongHopGame", Engine.TongHopGame);
        Set(8, 1, "TestTongHop", "TestTongHopGame", Engine.TongHopGame);
        Set(8, 2, "FRTest",      "FRTestGame",      Engine.MiniGameKit);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Đăng ký game với đầy đủ visual/audio identity.</summary>
    static void Set(int cat, int idx, string name, string scene, Engine engine,
                    BgmTrack bgm = BgmTrack.Gameplay, string bg = "", int pts = 0, bool hideScoreBars = false)
    {
        Games[cat, idx] = new GameEntry
        {
            name             = name,
            sceneName        = scene,
            engine           = engine,
            bgmTrack         = bgm,
            backgroundSprite = bg,
            pointsPerCorrect = pts,
            hideScoreBars    = hideScoreBars,
        };
    }
}
