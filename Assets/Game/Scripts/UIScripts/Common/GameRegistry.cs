/// <summary>
/// GameRegistry — nguồn sự thật duy nhất cho toàn bộ danh sách game.
///
/// Mỗi game được khai báo 1 lần bằng GameEntry (name + displayName + sceneName + engine).
/// Không có mảng song song, không có nguy cơ lệch index.
///
/// `name` là ĐỊNH DANH NỘI BỘ ổn định (khoá tra icon + thư mục CSV variant) — KHÔNG đổi giá trị
/// này khi chỉnh câu chữ hiển thị, sẽ làm mất icon/CSV. `displayName` mới là tên tiếng Việt hiện
/// lên UI (control panel, danh sách chọn game...) — sửa thoải mái, không ảnh hưởng gì khác.
///
/// Cách thêm game mới:
///   Set(category, index, "TenGame", "Tên hiển thị", "SceneName", Engine.XxxGame);
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
        public string   name;              // Định danh nội bộ: dùng cho icon + CSV variant key
        public string   displayName;       // Tên tiếng Việt hiển thị lên UI (control panel, ...)
        public string   group;             // Tiêu đề nhóm chủ đề trong danh sách chọn game, vd
                                            // "Đếm", "Cộng" (control panel gom theo group này,
                                            // hiện dạng tiêu đề collapse/expand được) — "" = game
                                            // đứng riêng, không thuộc nhóm nào (hiện phẳng như cũ)
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
        // group gom game theo chủ đề — control panel hiện dạng tiêu đề collapse/expand được
        // (xem ControlActivity.java). "Hình học" chưa có game nào, sẽ thêm sau (chỉ cần Set()
        // game mới với group:"Hình học", tự động xuất hiện thành 1 mục mới, không cần sửa UI).
        Set(0, 0, "Counting5",  "Đếm số (dễ)",         "TestTongHopGame", Engine.TongHopGame, group: "Đếm");
        Set(0, 1, "Counting",   "Đếm số",               "TestTongHopGame", Engine.TongHopGame, group: "Đếm");
        Set(0, 2, "AddNumber5", "Cộng số (dễ)",         "AddNumberGame",   Engine.MathGame,    group: "Cộng");
        Set(0, 3, "AddNumber",  "Cộng số",              "AddNumberGame",   Engine.MathGame,    group: "Cộng");
        Set(0, 17, "SubNumber5", "Trừ trong phạm vi 5", "SubNumberGame",   Engine.MathGame,    group: "Trừ");
        Set(0, 18, "SubNumber",  "Trừ trong phạm vi 10", "SubNumberGame",  Engine.MathGame,    group: "Trừ");
        // Biến thể hiện CHỮ SỐ thay cho hình (controller đọc hậu tố "Digit" của tên game) — cùng scene với bản hình.
        Set(0, 19, "AddNumber5Digit", "Cộng trong phạm vi 5 (chữ số)",  "AddNumberGame", Engine.MathGame, group: "Cộng");
        Set(0, 20, "AddNumberDigit",  "Cộng trong phạm vi 10 (chữ số)", "AddNumberGame", Engine.MathGame, group: "Cộng");
        Set(0, 21, "SubNumber5Digit", "Trừ trong phạm vi 5 (chữ số)",   "SubNumberGame", Engine.MathGame, group: "Trừ");
        Set(0, 22, "SubNumberDigit",  "Trừ trong phạm vi 10 (chữ số)",  "SubNumberGame", Engine.MathGame, group: "Trừ");
        Set(0, 4, "SoDem",      "Số đếm",               "SoDemGame",       Engine.ListenGame,  group: "Đếm");
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(0, 5, "SoDem2",     "Số đếm - Bóng bay",    "BalloonGame",     Engine.BalloonGame, group: "Đếm");
        Set(0, 6, "Numbers",    "Nhận biết số",         "NumbersGame",     Engine.ListenGame);

        // DISABLED 2026-10-05 (chưa mở lại, đừng đụng): Set(0, 7, "HaiQua",         "Hái quả",                    "HaiQuaGame", Engine.MiniGameKit, group: "Đếm");
        Set(0, 8, "DemQua",        "Đếm quả",                  "DemQuaGame", Engine.MiniGameKit, group: "Đếm");

        // ── Category 1: Tiếng Việt ───────────────────────────────────────────
        // 2026-09-18: soi lại asset/audio thật thì ListenSelect/FamilySpellingJump/WordHuntMaze/
        // SentenceBuilder đều là nội dung TIẾNG ANH (audio "Audio/Letters/A-Z.mp3" kiểu tên chữ
        // cái Anh, CSV "mom/dad/sister", hardcode "MOM/HAPPY/SAD", câu "He is thirsty"...) —
        // trước đó bị gán nhầm category 1, đã chuyển đúng sang category 2 bên dưới. Chỉ ChuCai/
        // ChuCai2 dùng kho âm chữ cái tiếng Việt thật (Dờ/Sờ/Đờ/Ư...) nên mới thực sự ở đây.
        Set(1, 0, "ChuCai",  "Chữ cái",            "ChuCaiGame",  Engine.ListenGame);
        Set(1, 2, "ChuCaiThuong", "Chữ cái (thường)", "ChuCaiGame", Engine.ListenGame); // cùng scene/controller, hiện chữ viết thường
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(1, 1, "ChuCai2", "Chữ cái - Bóng bay", "BalloonGame", Engine.BalloonGame);

        // ── Category 2: Tiếng Anh ────────────────────────────────────────────
        // 4 game chuyển từ category 1 sang đây (xem ghi chú ở trên) — SolarQuizEn (category 3)
        // vẫn KHÔNG tính vào đây, chỉ là bản dịch câu hỏi khoa học, không phải nội dung dạy tiếng Anh.
        Set(2, 0, "ListenSelect",       "Nghe và chọn",          "ListenGame",          Engine.ListenGame);
        Set(2, 1, "FamilySpellingJump", "Ghép vần - Nhảy ô",     "FamilySpellingGame",  Engine.MiniGameKit);
        Set(2, 2, "WordHuntMaze",       "Tìm từ - Mê cung",      "WordHuntMazeGame",    Engine.MiniGameKit);
        Set(2, 3, "SentenceBuilder",    "Ghép câu",              "SentenceBuilderGame", Engine.MiniGameKit);

        // ── Category 3: Khoa học (khám phá tự nhiên - xã hội) ────────────────
        Set(3, 0,  "SaveEnvironment",  "Bảo vệ môi trường",         "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 1,  "Fruit",            "Hoa quả",                   "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 2,  "Animal",           "Động vật",                  "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 3,  "WaterAnimal",      "Động vật dưới nước",        "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 4,  "Things",           "Đồ vật",                    "TestTongHopGame",      Engine.TongHopGame);
        Set(3, 5,  "SolarSystem",      "Hệ mặt trời",               "SolarSystemScene",     Engine.ExploreGame, BgmTrack.SolarSystem);
        Set(3, 6,  "SolarSystemVi",    "Hệ mặt trời (Tiếng Việt)",  "SolarSystemVi",        Engine.ExploreGame, BgmTrack.SolarSystem);
        Set(3, 7,  "SolarQuizEn",      "Đố vui vũ trụ (English)",   "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10, hideScoreBars: true);
        Set(3, 8,  "SolarQuizVi",      "Đố vui vũ trụ",             "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10, hideScoreBars: true);
        Set(3, 9,  "SolarOrder",       "Sắp xếp hành tinh",         "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", pts: 10);
        Set(3, 10, "SolarOrder2",      "Sắp xếp hành tinh (2)",     "TestTongHopGame",      Engine.TongHopGame, BgmTrack.SolarSystem, "SolarSystem/Textures/2k_stars", hideScoreBars: true);
        Set(3, 11, "SaveTheAstronaut", "Giải cứu phi hành gia",     "SaveTheAstronautGame", Engine.MiniGameKit);

        // ── Category 4: Kỹ năng sống ──────────────────────────────────────────
        Set(4, 0, "WhoIsIt",      "Đây là ai?",           "WhoIsItGame",      Engine.MiniGameKit);
        Set(4, 1, "FamilyMember", "Thành viên gia đình",  "FamilyMemberGame", Engine.MiniGameKit);

        // ── Category 5: Tư duy - Logic ────────────────────────────────────────
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(5, 0, "PathFinder", "Tìm đường",    "PathFinderGame", Engine.PathGame);
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(5, 1, "Monopoly",   "Cờ tỷ phú",     "MonopolyGame",   Engine.MiniGameKit);

        // ── Category 6: Trí nhớ ───────────────────────────────────────────────
        Set(6, 0, "PlanetOrder",    "Thứ tự hành tinh",       "PlanetOrderGame",    Engine.PlanetGame);
        Set(6, 1, "PlanetAlphabet", "Bảng chữ cái hành tinh", "PlanetAlphabetGame", Engine.PlanetGame);
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(6, 2, "TrainPath",      "Đường tàu",              "TrainPathGame",      Engine.TrainGame);

        // ── Category 7: Vận động ──────────────────────────────────────────────
        // DISABLED 2026-10-04 (chưa mở lại, đừng đụng): Set(7, 0, "RiverCross", "Vượt sông",       "RiverCrossGame", Engine.ArcadeGame);
        Set(7, 1, "LaneDash",   "Chạy làn đường",  "LaneDashGame",   Engine.ArcadeGame);

        // ── Category 8: Khác (giải trí / chưa rõ mục tiêu giáo dục / công cụ test) ──
        // TongHop, TestTongHop: bộ câu hỏi CSV "tổng hợp" nhiều chủ đề — chưa thuộc hẳn 1 môn.
        // FRTest: màn test nhận diện khuôn mặt (công cụ debug), không phải nội dung học.
        Set(8, 0, "TongHop",     "Tổng hợp",              "TestTongHopGame", Engine.TongHopGame);
        Set(8, 1, "TestTongHop", "Tổng hợp (thử nghiệm)", "TestTongHopGame", Engine.TongHopGame);
        Set(8, 2, "FRTest",      "Test nhận diện mặt",    "FRTestGame",      Engine.MiniGameKit);

        // ── Game import từ .zip (Tools/GenericGame/Import Zip) — importer tự chèn Set(...) vào ngay phía trên dòng marker, KHÔNG xoá marker ──
        Set(0, 9, "CuaHangKemTruocSau", "cửa hàng kem ( trước , sau)", "CuaHangKemTruocSau", Engine.MiniGameKit, group: "Vị trí, định hướng");
        Set(0, 10, "DongHo", "Đồng hồ", "DongHo", Engine.MiniGameKit, group: "Thời gian");
        Set(0, 11, "SangTruaChieuToi", "sáng trưa chiều tối", "SangTruaChieuToi", Engine.MiniGameKit, group: "Thời gian");
        Set(0, 12, "ThuNghiem2", "Tách số", "ThuNghiem2", Engine.MiniGameKit, group: "Đếm");
        Set(0, 13, "SoSanhSo", "So sánh số", "SoSanhSo", Engine.MiniGameKit, group: "So sánh");
        Set(0, 14, "GameSangChuaChieuToiCuaBe", "Các buổi trong ngày", "GameSangChuaChieuToiCuaBe", Engine.MiniGameKit, group: "Thời gian");
        Set(0, 15, "TachSo2", "Tách số (2)", "TachSo2", Engine.MiniGameKit, group: "Cộng");
        Set(0, 16, "SoSanhSo2Dau", "So sánh số (>, <, =)", "SoSanhSo2Dau", Engine.MiniGameKit, group: "So sánh");
        Set(0, 23, "TongHopToan", "Tổng hợp Toán", "TongHopToanGame", Engine.MiniGameKit, group: "Ôn tập chung");
        // <GENERIC-GAMES>
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Đăng ký game với đầy đủ visual/audio identity.</summary>
    static void Set(int cat, int idx, string name, string displayName, string scene, Engine engine,
                    BgmTrack bgm = BgmTrack.Gameplay, string bg = "", int pts = 0, bool hideScoreBars = false,
                    string group = "")
    {
        Games[cat, idx] = new GameEntry
        {
            name             = name,
            displayName      = string.IsNullOrEmpty(displayName) ? name : displayName,
            group            = group,
            sceneName        = scene,
            engine           = engine,
            bgmTrack         = bgm,
            backgroundSprite = bg,
            pointsPerCorrect = pts,
            hideScoreBars    = hideScoreBars,
        };
    }
}
