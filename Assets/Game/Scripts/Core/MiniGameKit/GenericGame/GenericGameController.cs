using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller DUY NHẤT cho MỌI game tạo bằng web tool kéo-thả (xem SCHEMA.md cùng thư mục) — khác
/// hẳn các game khác trong Kit (1 game = 1 scene riêng), ở đây 1 SCENE DUY NHẤT
/// (Assets/Game/Scenes/GenericGamePlayer.unity, dựng 1 LẦN qua Tools/GenericGame/Build Player
/// Scene) phục vụ MỌI game — nội dung/layout/hiệu ứng đọc từ file
/// Resources/GenericGames/&lt;gameId&gt;/game.json lúc runtime, KHÔNG build lại scene mỗi khi có game
/// mới. gameId lấy từ GameSessionManager.SelectedGameName (đúng quy ước game nhiều variant chung 1
/// scene đã có sẵn trong Kit — xem GameSessionManager.ResolveActiveGameName).
///
/// MÔ HÌNH v2 (schemaVersion 2): mỗi ROUND có 2 nhóm slot — câu hỏi và đáp án — mỗi nhóm có bố cục riêng (manual/matrix/random),
/// mỗi slot = vị trí + nội dung (ảnh nền / icon / chữ / âm thanh / hiệu ứng ghi đè riêng). Item (nhân vật trang trí) là của CẢ GAME.
/// Game v1 cũ được chuyển sang v2 ngay lúc nạp (GenericGameMigration). Pool ButtonItem đáp án dựng 1 lần theo số slot đáp án
/// LỚN NHẤT trong mọi round; mỗi round đặt lại vị trí/hình dạng/nội dung cho các ô đang dùng (ApplyRoundLayout/ApplyRoundContent).
///
/// Thêm 1 game mới (sau khi web tool xuất xong gói game.json + ảnh/âm thanh):
///   1. Copy game.json → Assets/Game/Resources/GenericGames/&lt;gameId&gt;/game.json
///   2. Copy ảnh      → Assets/Game/Resources/TestTongHop/images/GenericGames/&lt;gameId&gt;/*.png
///   3. Copy âm thanh → Assets/Game/Resources/TestTongHop/audio/GenericGames/&lt;gameId&gt;/*.mp3
///   4. Thêm 1 dòng GameRegistry.Set(cat, idx, gameId, displayName, "GenericGamePlayer", Engine.MiniGameKit, ...)
///   KHÔNG cần mở Unity Editor dựng scene — chỉ cần Editor import lại ảnh (tự động) + rebuild APK.
/// </summary>
public class GenericGameController : MiniGameControllerBase
{
    [Header("GenericGame — refs dựng SẴN trong scene dùng chung (xem GenericGamePlayerSceneBuilder)")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] RectTransform leftButtonsRoot;
    [SerializeField] RectTransform rightButtonsRoot;
    [SerializeField] ButtonItem buttonItemPrefab;
    [SerializeField] SpawnFlowDisplay spawnFlowDisplay;
    // 4 ref câu hỏi dưới đây CHỈ còn dùng làm "mốc" (cha + thứ tự vẽ) để chèn các slot câu hỏi dựng lúc runtime — bản thân
    // Text/Image cũ bị ẩn vĩnh viễn (câu hỏi v2 = nhiều slot, xem QuestionSlotUi).
    [SerializeField] Text leftQuestionText;
    [SerializeField] Text rightQuestionText;
    [SerializeField] Image leftQuestionImage;
    [SerializeField] Image rightQuestionImage;
    [SerializeField] AudioSource sfxAudioSource;
    [SerializeField] Button backButton;
    [SerializeField] Image leftBackgroundImage;
    [SerializeField] Image rightBackgroundImage;
    [SerializeField] RectTransform leftDecoRoot;
    [SerializeField] RectTransform rightDecoRoot;

    [Header("GenericGame — scene riêng (tuỳ chọn; bỏ trống = dùng scene chung GenericGamePlayer đọc Resources)")]
    [SerializeField] GenericGameBaked baked;

    [Header("GenericGame — debug")]
    [Tooltip("Dùng khi bấm Play thẳng trong Editor (không qua MenuScene) để test 1 gameId cụ thể — " +
             "bỏ trống khi build thật, lúc đó luôn ưu tiên GameSessionManager.SelectedGameName.")]
    [SerializeField] string debugGameId;

    const string SceneNameForRegistry = "GenericGamePlayer";
    /// <summary>Giá trị "giữ chỗ" của QuestionData.answers[i] khi slot đáp án không có chữ (chỉ ảnh/icon) — ButtonDisplay bỏ qua đáp án
    /// rỗng/toàn khoảng trắng nên phải có ký tự thật; chữ hiển thị thật do ApplyAnswerContent đặt lại ngay sau Setup().</summary>
    const string PlaceholderAnswer = "·";
    /// <summary>Âm thanh câu hỏi: phát hết (lần lượt các slot câu hỏi có âm thanh) rồi NGHỈ ngần này giây mới lặp lại.</summary>
    const float QuestionAudioRepeatPause = 4f;

    GenericGamePackageV2 _package;
    string _gameId;
    readonly List<QuestionData> _pool = new List<QuestionData>();
    int _poolCursor;
    /// <summary>Thông tin runtime của 1 round (tra theo QuestionData.id): spec gốc + file ảnh nền của TỪNG slot đáp án đã chốt 1 lần
    /// lúc dựng pool (ảnh riêng / ảnh random từ ImagePool / ảnh nền chung của nhóm).</summary>
    sealed class RoundRuntime
    {
        public RoundSpecV2 spec;
        public string[] answerImages;
    }
    readonly Dictionary<string, RoundRuntime> _rounds = new Dictionary<string, RoundRuntime>();
    readonly Dictionary<Team, List<Coroutine>> _idleCoroutines = new Dictionary<Team, List<Coroutine>>();
    ButtonItem[] _leftSlotItems;
    ButtonItem[] _rightSlotItems;
    readonly Dictionary<Team, int> _lastRoundPoints = new Dictionary<Team, int>();
    readonly List<(Team team, Text textEl, string bind)> _decoScoreTexts = new List<(Team, Text, string)>();
    readonly List<GameObject> _decorationRoots = new List<GameObject>();
    bool _decorationsRevealed;
    // FlyToStay ("Bay tới & Ở lại") — xem GenericGameLayout.collectSlots + ApplyCollectSlot/ResetCollectSlots.
    readonly Dictionary<Team, int> _collectCounters = new Dictionary<Team, int> { { Team.Left, 0 }, { Team.Right, 0 } };
    readonly Dictionary<Team, List<GameObject>> _collectClones = new Dictionary<Team, List<GameObject>>
        { { Team.Left, new List<GameObject>() }, { Team.Right, new List<GameObject>() } };
    /// <summary>Mảng ô "Vùng thu thập" ĐANG ÁP DỤNG cho round hiện tại của mỗi bên — tính lại ở đầu
    /// mỗi round (xem ResetCollectSlots/BuildEffectiveCollectSlots), KHÔNG đọc thẳng
    /// `_package.layout.collectSlots` nữa vì `collectAutoStretch=true` cần 1 mảng KHÁC NHAU mỗi
    /// round (theo đúng số đáp án đúng của round đó).</summary>
    readonly Dictionary<Team, RectPct[]> _effectiveCollectSlots = new Dictionary<Team, RectPct[]>();

    // SumToTarget ("Cộng dồn tới mục tiêu") + "tên lửa" — xem vùng "SumToTarget" ở cuối file.
    readonly Dictionary<Team, QuestionData> _teamQuestion = new Dictionary<Team, QuestionData>();
    readonly Dictionary<Team, int> _sumPicked = new Dictionary<Team, int> { { Team.Left, 0 }, { Team.Right, 0 } };
    readonly Dictionary<Team, bool> _sumWon = new Dictionary<Team, bool> { { Team.Left, false }, { Team.Right, false } };
    readonly Dictionary<Team, List<ButtonItem>> _sumHidden = new Dictionary<Team, List<ButtonItem>>
        { { Team.Left, new List<ButtonItem>() }, { Team.Right, new List<ButtonItem>() } };
    readonly Dictionary<Team, List<GameObject>> _sumLitCells = new Dictionary<Team, List<GameObject>>
        { { Team.Left, new List<GameObject>() }, { Team.Right, new List<GameObject>() } };
    readonly Dictionary<Team, List<GameObject>> _sumStatic = new Dictionary<Team, List<GameObject>>
        { { Team.Left, new List<GameObject>() }, { Team.Right, new List<GameObject>() } };
    readonly Dictionary<Team, GameObject> _sumHint = new Dictionary<Team, GameObject>();
    sealed class LaunchDeco
    {
        public Team team;
        public RectTransform rt, parent;
        public Vector2 origMin, origMax;
        public float bottomPct;
        public Coroutine flight;
        public EffectType type;
        public EffectParams effectParams;
        public Coroutine idle;
    }
    readonly List<LaunchDeco> _launchDecos = new List<LaunchDeco>();

    /// <summary>1 item (nhân vật trang trí) ở 1 bên — để chạy phản hồi theo sự kiện (đúng/đúng 1 phần/sai/bị chạm) rồi chạy lại hiệu ứng chờ.</summary>
    sealed class ItemRuntime
    {
        public Team team;
        public RectTransform rt;
        public DecorationSpec spec;
        public EffectType idleType;
        public EffectParams idleParams;
        public Coroutine idle, reacting;
    }
    readonly List<ItemRuntime> _items = new List<ItemRuntime>();
    enum ItemTrigger { Correct, Partial, Wrong }

    protected override void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);

        LoadPackage();
        if (_package == null)
        {
            Debug.LogError("[GenericGameKit] Dừng lại — không có game.json hợp lệ để chạy.");
            return;
        }

        playMode = _package.settings.playMode == "Independent" ? MiniGamePlayMode.Independent : MiniGamePlayMode.Combined;
        sceneNameForRegistry = baked != null && baked.gameJson != null
            ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            : SceneNameForRegistry;

        BuildQuestionPool();
        BuildRuntimeUi();
        BuildBackground();
        BuildDecorations();
        ApplyFeedbackLayers();
        ApplyHud();
        // Có "tên lửa" (launchOnComplete) thì chờ lâu hơn sau khi THẮNG round cho kịp bay hết màn hình
        // (LaunchDecos ~1.5s) rồi mới sang round mới — khớp web (endRound(1800)).
        if (_launchDecos.Count > 0) feedbackDelayCorrect = Mathf.Max(feedbackDelayCorrect, 1.8f);

        base.Start();

        // Phải gọi SAU base.Start() — StartGame() (bên trong base.Start(), qua Tutorial->StartGame
        // override ở dưới) đã tự gọi MusicManager.PlayGameplayMusic(); gọi sau để THAY THẾ nhạc nền
        // mặc định đó bằng nhạc riêng của game này nếu có cấu hình.
        BuildBackgroundAudio();

        // ScoreManager chỉ được tạo BÊN TRONG base.Start() (InitScoring()) — phải wiring binding
        // điểm số cho decoration SAU khi gọi base.Start(), không thể làm trong BuildDecorations().
        WireScoreBinding();
    }

    protected override void OnDestroy()
    {
        StopQuestionAudio();
        base.OnDestroy();
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    void LoadPackage()
    {
        // Scene RIÊNG của 1 game (import từ zip qua Tools/GenericGame/Import Zip): game.json + gameId nằm sẵn trong
        // prefab layout (GenericGameBaked), toạ độ chỉnh trực quan trong scene được ghi đè lên package sau khi parse.
        if (baked == null) baked = FindFirstObjectByType<GenericGameBaked>(FindObjectsInactive.Include);
        if (baked != null && baked.gameJson != null)
        {
            _gameId = baked.gameId;
            _package = GenericGameMigration.Parse(baked.gameJson.text);
            baked.ApplyTo(_package);
            baked.HideAtRuntime();
            return;
        }

        _gameId = GameSessionManager.Instance != null && !string.IsNullOrEmpty(GameSessionManager.Instance.SelectedGameName)
            ? GameSessionManager.Instance.SelectedGameName
            : debugGameId;

        if (string.IsNullOrEmpty(_gameId))
        {
            Debug.LogError("[GenericGameKit] Không xác định được gameId (SelectedGameName rỗng, debugGameId cũng rỗng).");
            return;
        }

        var jsonAsset = Resources.Load<TextAsset>($"GenericGames/{_gameId}/game");
        if (jsonAsset == null)
        {
            Debug.LogError($"[GenericGameKit] Không tìm thấy Resources/GenericGames/{_gameId}/game.json");
            return;
        }

        // v1 hay v2 đều ra GenericGamePackageV2 đã Sanitize đầy đủ (xem GenericGameMigration.Parse).
        _package = GenericGameMigration.Parse(jsonAsset.text);
    }

    // ── Question pool (thay cho CSV/MiniGameQuestionSource) ──────────────────

    void BuildQuestionPool()
    {
        _pool.Clear();
        _rounds.Clear();
        var rounds = _package.rounds;
        for (int i = 0; i < rounds.Length; i++)
            _pool.Add(BuildQuestion(rounds[i], i));
        Shuffle(_pool);
    }

    QuestionData BuildQuestion(RoundSpecV2 r, int idx)
    {
        var q = new QuestionData
        {
            id = _package.meta.gameId + "_" + idx,
            topic = _package.meta.gameId,
            difficulty = 1,
            questionType = QuestionType.Choose,
            displayMode = ChooseDisplayMode.Button,
            // Câu hỏi v2 là các slot (QuestionSlotUi) — QuestionData không mang nội dung câu hỏi nữa.
            questionMediaType = QuestionMediaType.Text,
            questionMediaValue = "",
            // Nội dung đáp án hiển thị thật do ApplyAnswerContent đặt SAU Setup() — ở đây chỉ cần chuỗi không rỗng + Text.
            answerMediaType = AnswerMediaType.Text,
        };

        var slots = r.answers.slots;
        var rr = new RoundRuntime { spec = r, answerImages = new string[slots.Length] };
        q.answers = new string[slots.Length];
        var correct = new List<int>();
        for (int i = 0; i < slots.Length; i++)
        {
            rr.answerImages[i] = ResolveSlotImageFile(slots[i], r.answers);
            q.answers[i] = string.IsNullOrWhiteSpace(slots[i].text) ? PlaceholderAnswer : slots[i].text;
            if (slots[i].correct) correct.Add(i);
        }
        // Giá trị SỐ của từng đáp án (đọc từ `text`, không phải số/≤0 → 1) — dùng cho SumToTarget (cộng
        // dồn) lẫn collectFillByValue (đáp án "N" chiếm N ô) ở bất kỳ answerMode nào, xem FillSpanOf().
        q.sumValues = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            q.sumValues[i] = int.TryParse((slots[i].text ?? "").Trim(), out var sv) && sv > 0 ? sv : 1;
        // SumToTarget: KHÔNG có khái niệm đáp án đúng/sai riêng (cờ `correct` bị bỏ qua, giống web) →
        // correctAnswers rỗng; mục tiêu = round.target, mặc định = số ô của thanh (hoặc 10 nếu chưa có).
        bool sumMode = _package.settings.answerMode == "SumToTarget";
        if (sumMode) correct.Clear();
        q.sumTarget = r.target > 0 ? r.target : DefaultSumTarget(r);
        q.correctAnswers = correct.ToArray();
        q.answerMode = _package.settings.answerMode switch
        {
            "SumToTarget" => AnswerMode.SumToTarget,
            "MultiSelect" => AnswerMode.MultiSelect,
            // OrderedSequence: thứ tự chạm CHÍNH LÀ thứ tự các slot correct:true trong mảng slots[] của nhóm đáp án.
            "OrderedSequence" => AnswerMode.OrderedSequence,
            _ => AnswerMode.Single,
        };
        _rounds[q.id] = rr;
        return q;
    }

    // ── Mô tả câu hỏi/đáp án để ghi log + xem ở Lịch sử trên ControlActivity ─────────────────
    // QuestionData của GenericGame không mang nội dung câu hỏi (câu hỏi là các slot của round) nên phải tự mô tả.

    /// <summary>Slot → chữ đọc được: có icon → "tênIcon ×số lượng"; có chữ → chữ; chỉ có ảnh → "[ảnh] tên file".</summary>
    static string DescribeSlot(SlotSpec s, string resolvedImage)
    {
        if (s == null) return "";
        string text = (s.text ?? "").Trim();
        if (!string.IsNullOrEmpty(s.icon))
            return $"{Path.GetFileNameWithoutExtension(s.icon)} ×{(text.Length > 0 ? text : "1")}";
        if (text.Length > 0) return text;
        string img = !string.IsNullOrEmpty(s.image) ? s.image : resolvedImage;
        return string.IsNullOrEmpty(img) ? "·" : "[ảnh] " + Path.GetFileNameWithoutExtension(img);
    }

    string DescribeAnswerSlot(RoundRuntime rr, int i)
    {
        var slots = rr.spec.answers != null ? rr.spec.answers.slots : null;
        if (slots == null || i < 0 || i >= slots.Length) return i.ToString();
        return DescribeSlot(slots[i], rr.answerImages != null && i < rr.answerImages.Length ? rr.answerImages[i] : null);
    }

    protected override string DescribeQuestionForLog(QuestionData q)
    {
        if (q == null || !_rounds.TryGetValue(q.id, out var rr)) return base.DescribeQuestionForLog(q);
        // Chỉ ghi phần ĐỌC ĐƯỢC (chữ, "tênIcon ×số lượng"); câu hỏi chỉ có ảnh/âm thanh → "Câu hỏi media".
        var parts = new List<string>();
        var qs = rr.spec.question != null ? rr.spec.question.slots : null;
        if (qs != null)
            foreach (var sl in qs)
            {
                if (sl == null) continue;
                string text = (sl.text ?? "").Trim();
                if (!string.IsNullOrEmpty(sl.icon)) parts.Add($"{Path.GetFileNameWithoutExtension(sl.icon)} ×{(text.Length > 0 ? text : "1")}");
                else if (text.Length > 0) parts.Add(text);
            }
        if (_package.settings.answerMode == "SumToTarget") parts.Add($"mục tiêu tổng {q.sumTarget}");
        return parts.Count == 0 ? "Câu hỏi media" : string.Join(" | ", parts);
    }

    protected override string DescribeAnswerForLog(QuestionData q, int[] playerAnswer)
    {
        if (q == null || playerAnswer == null || playerAnswer.Length == 0 || !_rounds.TryGetValue(q.id, out var rr))
            return base.DescribeAnswerForLog(q, playerAnswer);
        var parts = new List<string>();
        foreach (int i in playerAnswer) parts.Add(DescribeAnswerSlot(rr, i));
        return string.Join(_package.settings.answerMode == "OrderedSequence" ? " → " : ", ", parts);
    }

    protected override string DescribeCorrectForLog(QuestionData q)
    {
        if (q == null || !_rounds.TryGetValue(q.id, out var rr)) return base.DescribeCorrectForLog(q);
        if (_package.settings.answerMode == "SumToTarget") return $"tổng = {q.sumTarget}";
        if (q.correctAnswers == null || q.correctAnswers.Length == 0) return "";
        var parts = new List<string>();
        foreach (int i in q.correctAnswers) parts.Add(DescribeAnswerSlot(rr, i));
        return string.Join(_package.settings.answerMode == "OrderedSequence" ? " → " : ", ", parts);
    }

    /// <summary>File ảnh nền của 1 slot đáp án: imagePool (random 1 ảnh trong tập) → ảnh riêng của slot → ảnh nền chung của nhóm → null.
    /// Chốt 1 LẦN lúc dựng pool (cùng round trong cùng phiên chơi luôn thấy đúng ảnh đó), khớp web (random 1 lần/round khi mở Chơi thử).</summary>
    string ResolveSlotImageFile(SlotSpec s, GroupSpec g)
    {
        if (!string.IsNullOrEmpty(s.imagePool))
        {
            var pool = Array.Find(_package.imagePools, p => p.id == s.imagePool);
            if (pool != null && pool.images != null && pool.images.Length > 0)
                return pool.images[UnityEngine.Random.Range(0, pool.images.Length)];
        }
        if (!string.IsNullOrEmpty(s.image)) return s.image;
        return string.IsNullOrEmpty(g.bgImage) ? null : g.bgImage;
    }

    string ResolveImagePath(string fileName) => string.IsNullOrEmpty(fileName) ? null
        : $"TestTongHop/images/GenericGames/{_gameId}/{Path.GetFileNameWithoutExtension(fileName)}";

    string ResolveAudioPath(string fileName) => string.IsNullOrEmpty(fileName) ? null
        : $"TestTongHop/audio/GenericGames/{_gameId}/{Path.GetFileNameWithoutExtension(fileName)}";

    static void Shuffle(List<QuestionData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    protected override QuestionData PullNextQuestion()
    {
        if (_pool.Count == 0) return null;
        if (_poolCursor >= _pool.Count) { Shuffle(_pool); _poolCursor = 0; }
        return _pool[_poolCursor++];
    }

    // ── Runtime UI (thay cho Editor SceneBuilder cố định) ─────────────────────

    /// <summary>Đáp án dạng spawn liên tục — cờ CHUNG cả game, ghi đè cách sắp xếp (manual/matrix/random) của nhóm đáp án.</summary>
    bool UsesSpawnFlow => _package.layout.spawnFlow != null && _package.layout.spawnFlow.enabled
        && spawnFlowDisplay != null;

    void BuildRuntimeUi()
    {
        LoadSlotFrameSprites();
        BuildQuestionFrameSprite();
        if (UsesSpawnFlow)
        {
            BuildSpawnFlowUi();
        }
        else
        {
            // Pool ButtonItem dựng 1 LẦN theo số slot đáp án LỚN NHẤT trong mọi round; vị trí/hình dạng/nội dung đặt lại
            // mỗi round (ApplyRoundLayout/ApplyRoundContent) — các ô chưa dùng ở round hiện tại do ButtonDisplay tự ẩn.
            int max = 1;
            foreach (var r in _package.rounds) max = Mathf.Max(max, r.answers.slots.Length);
            var left = new ButtonItem[max];
            var right = new ButtonItem[max];
            var none = new RectPct { xPct = 0f, yPct = 0f, wPct = 20f, hPct = 20f };
            for (int i = 0; i < max; i++)
            {
                left[i] = CreateSlot(leftButtonsRoot, none, i);
                right[i] = CreateSlot(rightButtonsRoot, none, i);
            }
            _leftSlotItems = left;
            _rightSlotItems = right;
            buttonDisplay.ConfigureSlots(left, right);
            buttonDisplay.onAnswerTapped += HandleAnswerTapped;
        }
        BuildQuestionSlotUis();
    }

    /// <summary>Cấu hình SpawnFlowDisplay theo layout.spawnFlow — KHÔNG dựng _leftSlotItems/
    /// _rightSlotItems (giữ null, mọi chỗ khác đọc 2 field này đều tự bỏ qua an toàn khi null).
    /// Vùng trôi (area) khác nhau theo round nên đặt mỗi round qua SetArea (ApplyRoundLayout); nội dung/hình dạng
    /// của từng item áp qua OnItemSpawned (item tạo liên tục theo thời gian, không phải 1 lần lúc Start()).</summary>
    void BuildSpawnFlowUi()
    {
        var sf = _package.layout.spawnFlow;
        var area = new RectPct { xPct = 5f, yPct = 50f, wPct = 90f, hPct = 45f };
        spawnFlowDisplay.Configure(sf.direction, sf.itemWPct, sf.itemHPct, sf.speedPct,
            sf.spawnIntervalSec, sf.maxConcurrent, area, MirrorRect(area));
        // Layer "answers" gắn 1 LẦN lên SpawnFlowRoot (có RectMask2D cắt ở rìa nửa màn hình — xem SpawnFlowDisplay.ClipToHalf),
        // KHÔNG gắn Canvas overrideSorting lên từng item: Canvas lồng của item sẽ cắt đứt RectMask2D của cha → item không bị cắt.
        ApplyLayer(spawnFlowDisplay.SpawnParent(Team.Left)?.gameObject, "answers");
        ApplyLayer(spawnFlowDisplay.SpawnParent(Team.Right)?.gameObject, "answers");
        spawnFlowDisplay.OnItemSpawned = (team, item, answerIndex) =>
        {
            ApplyAnswerContent(team, item, answerIndex, -1);
        };
        spawnFlowDisplay.onAnswerTapped += HandleAnswerTapped;
    }

    // ── Bố cục + nội dung ĐÁP ÁN theo round ─────────────────────────────────────

    /// <summary>Vị trí các slot của 1 nhóm cho 1 bên: manual/matrix = toạ độ đã soạn (bên phải mirror), random = xáo MỚI trong `area`
    /// mỗi lần gọi (mỗi bên random ĐỘC LẬP, không mirror cho nhau) với cỡ ô random.itemWPct×itemHPct, tránh đè nhau.</summary>
    RectPct[] ComputePositions(GroupSpec g, Team team)
    {
        int n = g.slots.Length;
        if (g.arrangement == "random")
        {
            var area = (team == Team.Right && g.mirror) ? MirrorRect(g.area) : g.area;
            return GenericGameMigration.PickNonOverlapping(area, n, g.random.itemWPct, g.random.itemHPct, g.random.gapPct);
        }
        var res = new RectPct[n];
        for (int i = 0; i < n; i++)
        {
            var r = new RectPct { xPct = g.slots[i].xPct, yPct = g.slots[i].yPct, wPct = g.slots[i].wPct, hPct = g.slots[i].hPct };
            res[i] = (team == Team.Right && g.mirror) ? MirrorRect(r) : r;
        }
        return res;
    }

    /// <summary>Đầu mỗi round của `team`: đặt VỊ TRÍ + HÌNH DẠNG các ô đáp án theo nhóm đáp án của round đó (gọi TRƯỚC khi display Setup).
    /// Spawn flow: chỉ đổi vùng trôi của bên này.</summary>
    void ApplyRoundLayout(Team team, QuestionData q)
    {
        if (q == null || !_rounds.TryGetValue(q.id, out var rr)) return;
        var g = rr.spec.answers;
        if (UsesSpawnFlow)
        {
            spawnFlowDisplay.SetArea(team, team == Team.Left ? g.area : MirrorRect(g.area));
            return;
        }
        var items = team == Team.Left ? _leftSlotItems : _rightSlotItems;
        if (items == null) return;
        var pos = ComputePositions(g, team);
        for (int i = 0; i < items.Length && i < pos.Length; i++)
        {
            if (items[i] == null) continue;
            ApplyRectPct((RectTransform)items[i].transform, pos[i]);
            items[i].ApplyShape(g.shape);
        }
    }

    /// <summary>Sau khi ButtonDisplay đã Setup() xong cho round này (đã biết đáp án nào nằm ở ô nào — PickSlots xáo mỗi round): đặt NỘI DUNG
    /// thật (ảnh nền, icon×N hoặc chữ) lên từng ô đang hiện. Không áp ở spawn flow — item spawn tự áp qua OnItemSpawned.</summary>
    void ApplyRoundContent(Team team, QuestionData q)
    {
        if (UsesSpawnFlow || q == null) return;
        var items = team == Team.Left ? _leftSlotItems : _rightSlotItems;
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var btn = items[i];
            if (btn == null || !btn.gameObject.activeSelf) continue;
            ApplyAnswerContent(team, btn, btn.AnswerIndex, i);
        }
    }

    /// <summary>Nền: ảnh của slot (riêng/chung nhóm/pool) giữ tỉ lệ → không có thì xoay vòng ảnh ô đáp án chung của game (kéo kín) → không có thì thẻ
    /// trắng. Nội dung trên nền: slot/nhóm có ICON thì hiện N icon (N = số trong `text`) và KHÔNG hiện chữ, không thì hiện `text`.</summary>
    void ApplyAnswerContent(Team team, ButtonItem item, int answerIndex, int slotIndex)
    {
        if (item == null || !_teamQuestion.TryGetValue(team, out var q) || q == null || !_rounds.TryGetValue(q.id, out var rr)) return;
        var g = rr.spec.answers;
        if (answerIndex < 0 || answerIndex >= g.slots.Length) return;
        var s = g.slots[answerIndex];

        item.ApplyShape(g.shape);
        string imgFile = rr.answerImages[answerIndex];
        Sprite bg = string.IsNullOrEmpty(imgFile) ? null : AssetOverrideLoader.GetSprite(ResolveImagePath(imgFile));
        bool framed = false;
        if (bg == null && _slotFrameSprites.Length > 0)
        {
            bg = _slotFrameSprites[Mathf.Abs(slotIndex >= 0 ? slotIndex : answerIndex) % _slotFrameSprites.Length];
            framed = true;
        }
        item.SetBackgroundSprite(bg, false); // mặc định STRETCH (kéo kín ô), kể cả ảnh riêng

        string iconFile = !string.IsNullOrEmpty(s.icon) ? s.icon : g.bgIcon;
        string iconValue = string.IsNullOrEmpty(iconFile) ? null : ResolveImagePath(iconFile) + ":" + IconCountOf(s.text);
        string text = s.text;
        if (string.IsNullOrEmpty(text) && bg == null) text = "?";
        item.SetContent(text, iconValue, _package.settings.iconColumns, IconCountOf(s.text));
        ApplyAnswerTextStyle(item);
    }

    /// <summary>Hex "#RRGGBB"/"#RRGGBBAA" → Color; rỗng/sai định dạng → fallback.</summary>
    static Color ParseTextColor(string hex, Color fallback)
        => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

    /// <summary>Cỡ + màu chữ đáp án theo settings (0/rỗng = giữ mặc định prefab).</summary>
    void ApplyAnswerTextStyle(ButtonItem item)
    {
        item.SetFontSize(_package.settings.answerFontSize);
        if (!string.IsNullOrEmpty(_package.settings.answerTextColor) &&
            ColorUtility.TryParseHtmlString(_package.settings.answerTextColor, out var c))
            item.SetTextColor(c);
    }

    /// <summary>Số icon từ chữ của slot: để trống/không phải số = 1, tối đa 20 (khớp web iconCountOf).</summary>
    static int IconCountOf(string text)
    {
        string t = (text ?? "").Trim();
        if (t.Length == 0) return 1;
        return int.TryParse(t, out var n) ? Mathf.Clamp(n, 0, 20) : 1;
    }

    // ── Ảnh khung (layout.slotFrames / layout.qFrame) ─────────────────────────

    Sprite[] _slotFrameSprites = Array.Empty<Sprite>();
    Sprite _qFrameSliced;

    void LoadSlotFrameSprites()
    {
        var names = _package.layout.slotFrames;
        var list = new List<Sprite>();
        if (names != null)
            foreach (var n in names)
            {
                var s = AssetOverrideLoader.GetSprite(ResolveImagePath(n));
                if (s != null) list.Add(s);
            }
        _slotFrameSprites = list.ToArray();
    }

    /// <summary>Khung 9-slice đặt DƯỚI chữ của slot câu hỏi (web: CSS border-image slice 90 fill). Sprite gốc của AssetOverrideLoader không có
    /// border → tạo lại Sprite từ cùng texture với border 90px (tối đa nửa cạnh ngắn) để Image.Type.Sliced hoạt động. Tạo 1 lần dùng chung.</summary>
    void BuildQuestionFrameSprite()
    {
        _qFrameSliced = null;
        if (string.IsNullOrEmpty(_package.layout.qFrame)) return;
        var src = AssetOverrideLoader.GetSprite(ResolveImagePath(_package.layout.qFrame));
        if (src == null) return;
        var tex = src.texture;
        var rect = src.textureRect;
        float b = Mathf.Min(90f, Mathf.Floor(Mathf.Min(rect.width, rect.height) / 2f) - 1f);
        if (b < 1f) return;
        _qFrameSliced = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    // ── Slot CÂU HỎI (nhiều slot/round, bố cục riêng từng round) ────────────────

    /// <summary>1 slot câu hỏi dựng lúc runtime: Frame (9-slice, chỉ khi chữ trần) / Bg (ảnh nền) / Icons (hàng icon) / Text. Pool dùng lại mỗi round.</summary>
    sealed class QuestionSlotUi
    {
        public RectTransform root, icons;
        public GridLayoutGroup grid;
        public Image frame, bg;
        public Text text;
        public readonly List<Coroutine> idle = new List<Coroutine>();
    }
    readonly Dictionary<Team, List<QuestionSlotUi>> _qUis = new Dictionary<Team, List<QuestionSlotUi>>
        { { Team.Left, new List<QuestionSlotUi>() }, { Team.Right, new List<QuestionSlotUi>() } };

    static Image MakeFullImage(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Combined: câu hỏi là 1 vùng CHUNG của cả màn hình — chỉ dựng 1 bộ slot (gắn với Team.Left) trên lớp phủ toàn canvas, không mirror.
    /// Independent: mỗi bên 1 bộ slot riêng (bên phải mirror nếu group.mirror).</summary>
    bool SharedQuestion => playMode == MiniGamePlayMode.Combined;

    void BuildQuestionSlotUis()
    {
        // Khối Text/Image câu hỏi cũ của scene không còn dùng — ẩn vĩnh viễn (chỉ giữ làm mốc cha/thứ tự vẽ).
        leftQuestionText?.gameObject.SetActive(false);
        rightQuestionText?.gameObject.SetActive(false);
        leftQuestionImage?.gameObject.SetActive(false);
        rightQuestionImage?.gameObject.SetActive(false);

        int max = 0;
        foreach (var r in _package.rounds) max = Mathf.Max(max, r.question.slots.Length);
        if (SharedQuestion)
        {
            Transform root = leftQuestionText != null ? leftQuestionText.canvas.rootCanvas.transform : (leftButtonsRoot != null ? leftButtonsRoot.root : null);
            if (root == null) return;
            var layer = new GameObject("SharedQuestionLayer", typeof(RectTransform));
            var lrt = (RectTransform)layer.transform;
            lrt.SetParent(root, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lrt.SetAsLastSibling();
            for (int i = 0; i < max; i++)
            {
                var ui = MakeQuestionSlotUi(Team.Left, lrt, -1, i);
                if (ui != null) _qUis[Team.Left].Add(ui);
            }
            return;
        }
        for (int i = 0; i < max; i++)
        {
            var l = MakeQuestionSlotUi(Team.Left, leftQuestionText != null ? leftQuestionText.transform.parent : leftButtonsRoot, leftQuestionText != null ? leftQuestionText.transform.GetSiblingIndex() : -1, i);
            var rt = MakeQuestionSlotUi(Team.Right, rightQuestionText != null ? rightQuestionText.transform.parent : rightButtonsRoot, rightQuestionText != null ? rightQuestionText.transform.GetSiblingIndex() : -1, i);
            if (l != null) _qUis[Team.Left].Add(l);
            if (rt != null) _qUis[Team.Right].Add(rt);
        }
    }

    QuestionSlotUi MakeQuestionSlotUi(Team team, Transform parent, int siblingIndex, int index)
    {
        if (parent == null) return null;
        var go = new GameObject("QSlot_" + team + "_" + index, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        ApplyLayer(go, "question");
        if (siblingIndex >= 0) rt.SetSiblingIndex(siblingIndex); // chèn NGAY TRƯỚC mốc → vẽ dưới đáp án/nhân vật như câu hỏi cũ
        var ui = new QuestionSlotUi { root = rt };

        ui.frame = MakeFullImage(rt, "Frame");
        ui.frame.type = Image.Type.Sliced;
        ui.frame.sprite = _qFrameSliced;
        ui.frame.gameObject.SetActive(false);

        ui.bg = MakeFullImage(rt, "Bg");
        ui.bg.preserveAspect = false; // stretch kín ô
        ui.bg.gameObject.SetActive(false);

        var icGo = new GameObject("Icons", typeof(RectTransform));
        ui.icons = (RectTransform)icGo.transform;
        ui.icons.SetParent(rt, false);
        ui.icons.anchorMin = Vector2.zero; ui.icons.anchorMax = Vector2.one; ui.icons.offsetMin = Vector2.zero; ui.icons.offsetMax = Vector2.zero;
        ui.grid = icGo.AddComponent<GridLayoutGroup>();
        ui.grid.childAlignment = TextAnchor.MiddleCenter;
        ui.grid.spacing = new Vector2(4f, 4f);
        ui.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.SetParent(rt, false);
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        ui.text = txtGo.AddComponent<Text>();
        ui.text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ui.text.alignment = TextAnchor.MiddleCenter;
        ui.text.color = ParseTextColor(_package.settings.questionTextColor, Color.white);
        ui.text.fontStyle = FontStyle.Bold;
        ui.text.raycastTarget = false;
        ui.text.horizontalOverflow = HorizontalWrapMode.Wrap;
        ui.text.verticalOverflow = VerticalWrapMode.Truncate;

        go.SetActive(false);
        return ui;
    }

    void StopQuestionSlotIdle(QuestionSlotUi ui)
    {
        foreach (var c in ui.idle) if (c != null) StopCoroutine(c);
        ui.idle.Clear();
        if (ui.root != null) ui.root.localScale = Vector3.one;
    }

    void HideQuestionSlots(Team team)
    {
        foreach (var ui in _qUis[team])
        {
            StopQuestionSlotIdle(ui);
            if (ui.root != null) ui.root.gameObject.SetActive(false);
        }
    }

    /// <summary>Hiện các slot câu hỏi của round `q` cho `team`: vị trí theo bố cục nhóm câu hỏi, mỗi slot = ảnh nền (riêng/chung nhóm) + hàng icon
    /// (N = số trong text) HOẶC chữ (icon và chữ không bao giờ cùng lúc) + hiệu ứng chờ riêng (slot.fx.onIdle). questionAudioOnly = ẩn hết.</summary>
    void ShowQuestionSlots(Team team, QuestionData q)
    {
        HideQuestionSlots(team);
        if (q == null || _package.settings.questionAudioOnly || !_rounds.TryGetValue(q.id, out var rr)) return;
        var g = rr.spec.question;
        var list = _qUis[team];
        var pos = ComputePositions(g, team);
        for (int i = 0; i < list.Count && i < g.slots.Length; i++)
        {
            var ui = list[i];
            var s = g.slots[i];
            string imgFile = !string.IsNullOrEmpty(s.image) ? s.image : g.bgImage;
            string iconFile = !string.IsNullOrEmpty(s.icon) ? s.icon : g.bgIcon;
            bool hasBg = !string.IsNullOrEmpty(imgFile);
            bool hasIcon = !string.IsNullOrEmpty(iconFile);
            bool hasText = !string.IsNullOrEmpty(s.text);
            if (!hasBg && !hasIcon && !hasText) continue;

            ApplyRectPct(ui.root, pos[i]);
            ui.root.gameObject.SetActive(true);
            var rect = ui.root.rect;

            var bgSprite = hasBg ? AssetOverrideLoader.GetSprite(ResolveImagePath(imgFile)) : null;
            ui.bg.sprite = bgSprite;
            ui.bg.gameObject.SetActive(bgSprite != null);

            for (int c = ui.icons.childCount - 1; c >= 0; c--)
            {
                var child = ui.icons.GetChild(c).gameObject;
                child.SetActive(false); // loại khỏi layout NGAY (Destroy chỉ xoá cuối frame)
                Destroy(child);
            }
            if (hasIcon)
            {
                var iconSprite = AssetOverrideLoader.GetSprite(ResolveImagePath(iconFile));
                int n = IconCountOf(s.text);
                int cols = Mathf.Max(1, Mathf.Min(Mathf.Max(1, n), _package.settings.iconColumns));
                int rows = Mathf.CeilToInt(Mathf.Max(1, n) / (float)cols);
                ui.grid.constraintCount = cols;
                ui.grid.cellSize = new Vector2(Mathf.Max(8f, (rect.width - ui.grid.spacing.x * (cols - 1)) / cols), Mathf.Max(8f, (rect.height - ui.grid.spacing.y * (rows - 1)) / rows));
                for (int k = 0; k < n; k++)
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(ui.icons, false);
                    var im = go.GetComponent<Image>();
                    im.sprite = iconSprite; im.preserveAspect = true; im.raycastTarget = false;
                }
            }

            bool showText = !hasIcon && hasText;
            ui.text.gameObject.SetActive(showText);
            bool useFrame = _qFrameSliced != null && showText && !hasBg;
            ui.frame.gameObject.SetActive(useFrame);
            if (showText)
            {
                ui.text.text = s.text;
                if (_package.settings.questionFontSize > 0f)
                {
                    ui.text.resizeTextForBestFit = false;
                    ui.text.fontSize = Mathf.RoundToInt(_package.settings.questionFontSize);
                }
                else if (useFrame && rect.height > 1f)
                {
                    ui.text.resizeTextForBestFit = false;
                    ui.text.fontSize = Mathf.Max(16, Mathf.RoundToInt(rect.height * 0.26f));
                }
                else
                {
                    ui.text.resizeTextForBestFit = true;
                    ui.text.resizeTextMinSize = 10;
                    ui.text.resizeTextMaxSize = Mathf.Max(14, Mathf.RoundToInt(rect.height * 0.6f));
                }
                if (useFrame && rect.width > 1f && rect.height > 1f)
                {
                    float bw = Mathf.Max(6f, Mathf.Min(rect.height * 0.32f, rect.width * 0.12f));
                    float border = ui.frame.sprite != null ? ui.frame.sprite.border.x : 90f;
                    ui.frame.pixelsPerUnitMultiplier = Mathf.Max(0.01f, border / bw);
                }
            }

            foreach (var spec in s.fx.onIdle.effects)
            {
                var type = ParseType(spec.type);
                if (type == EffectType.None) continue;
                var cor = EffectLibrary.Play(this, ui.root, type, MirrorForTeam(spec.effectParams, team, type));
                if (cor != null) ui.idle.Add(cor);
            }
        }
    }

    // ── Âm thanh câu hỏi: vào round phát, nghỉ 4s rồi lặp lại — CHỈ ở chế độ Combined ──────────

    Coroutine _qAudioLoop;

    void StartQuestionAudio(QuestionData q)
    {
        StopQuestionAudio();
        // Independent: 2 bên mỗi bên 1 nhịp câu hỏi riêng → âm thanh chồng nhau gây nhiễu, nên KHÔNG phát.
        if (playMode == MiniGamePlayMode.Independent || sfxAudioSource == null || q == null) return;
        if (!_rounds.TryGetValue(q.id, out var rr)) return;
        var clips = new List<AudioClip>();
        foreach (var s in rr.spec.question.slots)
        {
            if (string.IsNullOrEmpty(s.sound)) continue;
            var clip = AssetOverrideLoader.GetClip(ResolveAudioPath(s.sound));
            if (clip != null) clips.Add(clip);
        }
        if (clips.Count == 0) return;
        _qAudioLoop = StartCoroutine(QuestionAudioLoop(clips));
    }

    void StopQuestionAudio()
    {
        if (_qAudioLoop == null) return; // không có vòng lặp đang chạy → không đụng tới sfx khác (đúng/sai...)
        StopCoroutine(_qAudioLoop);
        _qAudioLoop = null;
        if (sfxAudioSource != null) sfxAudioSource.Stop(); // cắt nốt đoạn âm thanh câu hỏi đang phát dở
    }

    IEnumerator QuestionAudioLoop(List<AudioClip> clips)
    {
        while (true)
        {
            foreach (var clip in clips)
            {
                sfxAudioSource.PlayOneShot(clip);
                yield return new WaitForSeconds(clip.length);
            }
            yield return new WaitForSeconds(QuestionAudioRepeatPause);
        }
    }

    // ── Bối cảnh (nền) + Nhân vật/Item ─────────────────────────────────────────

    void BuildBackground()
    {
        if (leftBackgroundImage == null || rightBackgroundImage == null) return;
        if (string.IsNullOrEmpty(_package.layout.background))
        {
            leftBackgroundImage.gameObject.SetActive(false);
            rightBackgroundImage.gameObject.SetActive(false);
            return;
        }
        var sprite = AssetOverrideLoader.GetSprite(ResolveImagePath(_package.layout.background));
        leftBackgroundImage.sprite = sprite;
        rightBackgroundImage.sprite = sprite;
        leftBackgroundImage.gameObject.SetActive(sprite != null);
        rightBackgroundImage.gameObject.SetActive(sprite != null);
        // Lật ngang nền ở nửa phải — khớp hành vi web tool (bg.style.transform='scaleX(-1)').
        var s = rightBackgroundImage.rectTransform.localScale;
        rightBackgroundImage.rectTransform.localScale = new Vector3(-Mathf.Abs(s.x), s.y, s.z);
    }

    /// <summary>GameHUD (P1/P2/Timer) vẽ trên mọi layer của game (nền, câu hỏi, item, đáp án) nhưng DƯỚI icon ✔/✖ (luôn trên cùng) — GameHUD tự đặt
    /// sortingOrder = 10 trong Awake nên phải ghi đè lại theo thứ tự layer của game. Có ảnh nền thì đổi luôn nền của HUD (GameHUD.SetBackground).</summary>
    void ApplyHud()
    {
        if (hud == null) return;
        var cv = hud.GetComponent<Canvas>();
        if (cv != null)
        {
            // Thứ tự layer lấy NGUYÊN từ game.json (layerOrder; thiếu thì mặc định như web) — HUD không có trong json nên chỉ đặt
            // trên câu hỏi + nền, và không bao giờ đè lên icon ✔/✖.
            int fb = LayerOrderOf("feedback"), q = LayerOrderOf("question");
            cv.overrideSorting = true;
            cv.sortingOrder = fb > q ? fb - 5 : fb - 1;
            if (fb <= q)
                Debug.LogWarning("[GenericGameKit] XUNG ĐỘT: layerOrder trong game.json đặt icon ✔/✖ DƯỚI câu hỏi — giữ đúng json cho icon, nhưng HUD (muốn nằm trên câu hỏi) cũng bị kẹt dưới câu hỏi. Cần thống nhất.");
        }
        if (!string.IsNullOrEmpty(_package.layout.background))
            hud.SetBackground(AssetOverrideLoader.GetSprite(ResolveImagePath(_package.layout.background)));
    }

    /// <summary>Nhạc nền RIÊNG của game này (nếu có cấu hình) — thay thế nhạc nền mặc định Kit vừa
    /// tự bật lúc StartGame() (xem lời gọi ở Start()). Không có cấu hình thì bỏ qua, giữ nguyên
    /// nhạc nền mặc định.</summary>
    void BuildBackgroundAudio()
    {
        if (string.IsNullOrEmpty(_package.layout.backgroundAudio)) return;
        var clip = AssetOverrideLoader.GetClip(ResolveAudioPath(_package.layout.backgroundAudio));
        if (clip != null) MusicManager.Instance?.PlayCustomBgm(clip, "generic_" + _gameId);
    }

    /// <summary>Item (nhân vật/đồ vật trang trí cho cả game) — hiệu ứng chờ chạy ngay (Breathing mặc định), phản hồi theo sự kiện qua FireItems,
    /// bắt chạm CHỈ khi có fx.onClick (raycastTarget). FlyOff/FlyTo/FlyToStay bị bỏ qua có chủ đích ở item (bay đi mất thì hết còn gì để trang trí).</summary>
    void BuildDecorations()
    {
        _decoScoreTexts.Clear();
        _items.Clear();
        foreach (var d in _package.layout.decorations)
        {
            BuildOneDecoration(d, Team.Left, leftDecoRoot, false);
            BuildOneDecoration(d, Team.Right, rightDecoRoot, true);
        }
    }

    static bool HasAnyFx(ActionFx fx) => fx != null && ((fx.effects != null && Array.Exists(fx.effects, s => s != null && ParseType(s.type) != EffectType.None)) || !string.IsNullOrEmpty(fx.sound));

    void BuildOneDecoration(DecorationSpec d, Team team, RectTransform parent, bool mirror)
    {
        if (parent == null) return;
        var go = new GameObject("Deco_" + (string.IsNullOrEmpty(d.id) ? "item" : d.id), typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        ApplyLayer(go, "deco:" + d.id);
        var rect = new RectPct { xPct = d.xPct, yPct = d.yPct, wPct = d.wPct, hPct = d.hPct };
        ApplyRectPct(rt, mirror ? MirrorRect(rect) : rect);
        // Ẩn ngay từ đầu — chỉ hiện nền trong lúc chờ (tutorial/đếm ngược Start), nhân vật/item chỉ
        // hiện khi vào gameplay thật (xem RevealDecorations(), gọi lần đầu round thật sự bắt đầu).
        go.SetActive(false);
        _decorationRoots.Add(go);

        var it = new ItemRuntime { team = team, rt = rt, spec = d, idleType = EffectType.None };
        _items.Add(it);

        if (!string.IsNullOrEmpty(d.image))
        {
            var imgGo = new GameObject("Image", typeof(RectTransform));
            var imgRt = (RectTransform)imgGo.transform;
            imgRt.SetParent(rt, false);
            imgRt.anchorMin = Vector2.zero; imgRt.anchorMax = Vector2.one; imgRt.offsetMin = Vector2.zero; imgRt.offsetMax = Vector2.zero;
            var img = imgGo.AddComponent<Image>();
            img.sprite = AssetOverrideLoader.GetSprite(ResolveImagePath(d.image));
            img.preserveAspect = true;
            img.raycastTarget = false;
            // Lật NGANG ảnh ở nửa phải (nhân vật quay mặt vào giữa màn hình) — chỉ ảnh, KHÔNG đụng
            // text sibling (text lật ngang sẽ không đọc được).
            if (mirror && d.mirrorImage) imgRt.localScale = new Vector3(-1f, 1f, 1f);

            // Bắt chạm CHỈ khi item có phản hồi "khi bị chạm" — mặc định item thuần hiển thị, không chặn chạm của đáp án.
            if (HasAnyFx(d.fx.onClick))
            {
                img.raycastTarget = true;
                var btn = imgGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => RunItemFx(it, d.fx.onClick));
            }
        }

        if (d.text != null && !string.IsNullOrEmpty(d.text.bind))
        {
            var txtGo = new GameObject("Text", typeof(RectTransform));
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.SetParent(rt, false);
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.fontStyle = FontStyle.Bold;
            txt.fontSize = Mathf.Max(8, Mathf.RoundToInt(d.text.fontSizePct / 100f * parent.rect.height));
            _decoScoreTexts.Add((team, txt, d.text.bind));
        }

        var fx = EffectLibrary.Create(ParseType(d.effect?.type));
        Coroutine idle = null;
        EffectParams effectP = default;
        var effectType = EffectType.None;
        if (fx != null)
        {
            effectP = MirrorForTeam(d.effect.effectParams, team, ParseType(d.effect.type));
            effectType = ParseType(d.effect.type);
            if (effectType != EffectType.FlyOff && effectType != EffectType.FlyTo)
                idle = StartCoroutine(fx.Play(rt, effectP, this));
        }
        it.idleType = effectType; it.idleParams = effectP; it.idle = idle;

        if (d.launchOnComplete)
        {
            // Tên lửa: ghi nhớ vị trí gốc + hiệu ứng liên tục để LaunchDecos() bay lên rồi
            // ResetDecoLaunch() trả về + chạy lại hiệu ứng ở round mới.
            // bottomPct = khoảng cách từ ĐÁY deco tới mép TRÊN nửa màn hình (tính theo % chiều cao) —
            // quãng bay thật (px) tính lúc bay trong LaunchDecos vì parent.rect.height chưa chắc đã ổn
            // định ngay lúc Start().
            _launchDecos.Add(new LaunchDeco
            {
                team = team, rt = rt, parent = parent,
                origMin = rt.offsetMin, origMax = rt.offsetMax,
                bottomPct = rect.yPct + rect.hPct,
                type = effectType, effectParams = effectP, idle = idle,
            });
        }
    }

    /// <summary>Chạy phản hồi của các item của `team` theo sự kiện: đúng (hết round) / đúng 1 phần / sai. Item nào không cấu hình trigger đó thì bỏ qua.</summary>
    void FireItems(Team team, ItemTrigger trigger)
    {
        foreach (var it in _items)
        {
            if (it.team != team || it.rt == null) continue;
            var fx = trigger == ItemTrigger.Correct ? it.spec.fx.onCorrect : trigger == ItemTrigger.Partial ? it.spec.fx.onPartial : it.spec.fx.onWrong;
            if (HasAnyFx(fx)) RunItemFx(it, fx);
        }
    }

    /// <summary>Phát âm thanh + chạy các effect (Punch/Shake/FadeOut/Breathing — Fly* bị bỏ qua) lên item, tạm dừng hiệu ứng chờ trong lúc phản hồi rồi chạy lại.
    /// Bỏ qua item "tên lửa" (launchOnComplete) — nó có luồng bay riêng, chạy thêm effect lên sẽ đánh nhau.</summary>
    void RunItemFx(ItemRuntime it, ActionFx fx)
    {
        if (it == null || it.rt == null || fx == null || it.spec.launchOnComplete) return;
        if (fx.playSound && !string.IsNullOrEmpty(fx.sound)) PlaySound(fx.sound);
        var specs = new List<(EffectType type, EffectParams p)>();
        float maxDur = 0f;
        foreach (var spec in fx.effects)
        {
            var type = ParseType(spec.type);
            if (type == EffectType.None || type == EffectType.FlyOff || type == EffectType.FlyTo || type == EffectType.FlyToStay) continue;
            var p = MirrorForTeam(spec.effectParams, it.team, type);
            specs.Add((type, p));
            maxDur = Mathf.Max(maxDur, p.duration > 0f ? p.duration : 0.45f);
        }
        if (specs.Count == 0) return;
        if (it.reacting != null) StopCoroutine(it.reacting);
        it.reacting = StartCoroutine(ItemReactRoutine(it, specs, maxDur));
    }

    IEnumerator ItemReactRoutine(ItemRuntime it, List<(EffectType type, EffectParams p)> specs, float maxDur)
    {
        if (it.idle != null) { StopCoroutine(it.idle); it.idle = null; }
        it.rt.localScale = Vector3.one;
        foreach (var (type, p) in specs) EffectLibrary.Play(this, it.rt, type, p);
        yield return new WaitForSeconds(maxDur + 0.06f);
        if (it.rt == null) yield break;
        it.rt.localScale = Vector3.one;
        var idleFx = EffectLibrary.Create(it.idleType);
        if (idleFx != null && it.idleType != EffectType.FlyOff && it.idleType != EffectType.FlyTo)
            it.idle = StartCoroutine(idleFx.Play(it.rt, it.idleParams, this));
        it.reacting = null;
    }

    void WireScoreBinding()
    {
        if (ScoreManager == null || _decoScoreTexts.Count == 0) return;
        ScoreManager.OnScoreChanged += (team, left, right) => RefreshDecoScoreTexts();
        RefreshDecoScoreTexts();
    }

    void RefreshDecoScoreTexts()
    {
        foreach (var (team, textEl, bind) in _decoScoreTexts)
        {
            if (textEl == null) continue;
            int total = team == Team.Left ? ScoreManager.ScoreLeft : ScoreManager.ScoreRight;
            switch (bind)
            {
                case "totalScore": textEl.text = total.ToString(); break;
                case "roundScore":
                    _lastRoundPoints.TryGetValue(team, out var last);
                    textEl.text = "+" + last;
                    break;
                case "targetScore":
                    textEl.text = _package.settings.hasTargetScore ? _package.settings.targetScore.ToString("0") : "-";
                    break;
            }
        }
    }

    // ── LAYERS ── thứ tự vẽ theo layout.layerOrder (khớp panel Layers của web). Mỗi đối tượng gắn 1 Canvas
    // overrideSorting với sortingOrder = (vị trí+1)*10 → không phụ thuộc cây hierarchy (Combined/Independent khác parent).
    Dictionary<string, int> _layerIndex;

    /// <summary>Chuẩn hoá layerOrder — PHẢI giống layerKeys() trong web: khoá hệ thống thiếu chèn theo thứ tự mặc định, item thiếu chèn ngay dưới "answers".</summary>
    static List<string> NormalizeLayerKeys(GenericGameLayout layout)
    {
        var sys = new[] { "question", "answers", "collect", "feedback" };
        var decoKeys = new List<string>();
        if (layout.decorations != null) foreach (var d in layout.decorations) decoKeys.Add("deco:" + d.id);
        var valid = new HashSet<string>(sys);
        foreach (var k in decoKeys) valid.Add(k);
        var outList = new List<string>();
        if (layout.layerOrder != null)
            foreach (var k in layout.layerOrder) if (k != null && valid.Contains(k) && !outList.Contains(k)) outList.Add(k);
        for (int i = 0; i < sys.Length; i++)
        {
            if (outList.Contains(sys[i])) continue;
            int pos = 0;
            for (int j = i - 1; j >= 0; j--) { int q = outList.IndexOf(sys[j]); if (q >= 0) { pos = q + 1; break; } }
            outList.Insert(pos, sys[i]);
        }
        foreach (var k in decoKeys) if (!outList.Contains(k)) outList.Insert(outList.IndexOf("answers"), k);
        return outList;
    }

    int LayerOrderOf(string key)
    {
        if (_layerIndex == null)
        {
            _layerIndex = new Dictionary<string, int>();
            var keys = NormalizeLayerKeys(_package.layout);
            for (int i = 0; i < keys.Count; i++) _layerIndex[keys[i]] = (i + 1) * 10;
        }
        return _layerIndex.TryGetValue(key, out var v) ? v : 0;
    }

    /// <summary>Gắn đối tượng vào layer `key` ("question"/"answers"/"collect"/"feedback"/"deco:id"). `offset` &gt; 0 để đặt trên đối tượng cùng layer.</summary>
    void ApplyLayer(GameObject go, string key, int offset = 0)
    {
        if (go == null || _package == null) return;
        // Canvas thêm vào object ĐANG TẮT (vd icon ✔/✖) bị mất overrideSorting/sortingOrder khi object bật lần đầu
        // → bật tạm để Canvas khởi tạo xong rồi mới gán, sau đó trả lại trạng thái cũ.
        bool wasActive = go.activeSelf;
        if (!wasActive) go.SetActive(true);
        var c = go.GetComponent<Canvas>();
        if (c == null) c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = LayerOrderOf(key) + offset;
        // Nested canvas cần GraphicRaycaster để Button/chạm bên trong vẫn hoạt động.
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        if (!wasActive) go.SetActive(false);
    }

    void ApplyFeedbackLayers()
    {
        ApplyLayer(leftCorrectIcon, "feedback"); ApplyLayer(leftWrongIcon, "feedback");
        ApplyLayer(rightCorrectIcon, "feedback"); ApplyLayer(rightWrongIcon, "feedback");
    }

    ButtonItem CreateSlot(RectTransform parent, RectPct pct, int index)
    {
        var go = Instantiate(buttonItemPrefab.gameObject, parent);
        go.name = "Slot" + index;
        ApplyLayer(go, "answers");
        ApplyRectPct((RectTransform)go.transform, pct);
        var item = go.GetComponent<ButtonItem>();
        ApplyAnswerTextStyle(item);
        return item;
    }

    static void ApplyRectPct(RectTransform rt, RectPct p)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(p.xPct / 100f, 1f - (p.yPct + p.hPct) / 100f);
        rt.anchorMax = new Vector2((p.xPct + p.wPct) / 100f, 1f - p.yPct / 100f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Lật ngang quanh tâm NỬA màn hình (0-100% là trong 1 nửa, không phải toàn màn hình)
    /// — bên phải luôn là ảnh gương của bên trái, đúng quy ước split-screen đã dùng xuyên suốt Kit.</summary>
    static RectPct MirrorRect(RectPct p) => new RectPct { xPct = 100f - p.xPct - p.wPct, yPct = p.yPct, wPct = p.wPct, hPct = p.hPct };

    // ── Question UI + audio ───────────────────────────────────────────────────

    /// <summary>Hiện TẤT CẢ nhân vật/item cùng lúc — gọi đúng 1 LẦN khi round thật sự đầu tiên bắt
    /// đầu (sau tutorial/đếm ngược Start, lúc đó chỉ có nền hiện sẵn). Idempotent — gọi lại không
    /// sao, chỉ làm gì đó ở lần gọi đầu tiên.</summary>
    void RevealDecorations()
    {
        if (_decorationsRevealed) return;
        _decorationsRevealed = true;
        foreach (var go in _decorationRoots) if (go != null) go.SetActive(true);
    }

    protected override void OnQuestionShown(QuestionData q)
    {
        RevealDecorations();
        SetDecorationsActive(Team.Left, true);
        SetDecorationsActive(Team.Right, true);
        // Round MỚI → ẩn icon ✔/✖ fallback của round TRƯỚC (base class tự làm việc này khi
        // UseDefaultFeedbackFx=true, nhưng GenericGame tắt cờ đó để tự quyết định sound/icon qua
        // PlayActionFx — phải tự gọi lại ở đây để không bị "đứng hình" icon cũ sang round mới).
        HideFeedbackIcon(Team.Left);
        HideFeedbackIcon(Team.Right);
        // Round MỚI → dọn sạch "giỏ" FlyToStay của round TRƯỚC, đếm lại từ ô đầu — mặc định RESET
        // mỗi round (không tích luỹ xuyên suốt game), theo đúng yêu cầu. correctAnswers.Length
        // truyền vào để BuildEffectiveCollectSlots tính lại lưới đúng số ô khi collectAutoStretch=true.
        int correctCount = q.correctAnswers?.Length ?? 1;
        ResetCollectSlots(Team.Left, correctCount, q);
        ResetCollectSlots(Team.Right, correctCount, q);
        ResetRoundSumAndLaunch(Team.Left, q);
        ResetRoundSumAndLaunch(Team.Right, q);
        // Round MỚI → điểm round hiển thị (decoration bind="roundScore") phải về 0 ngay, không để
        // lộ số của round TRƯỚC cho tới khi round này có pick đúng đầu tiên.
        _lastRoundPoints[Team.Left] = 0;
        _lastRoundPoints[Team.Right] = 0;
        RefreshDecoScoreTexts();
        ApplyRoundLayout(Team.Left, q);
        ApplyRoundLayout(Team.Right, q);
        ShowQuestionSlots(Team.Left, q);
        ShowQuestionSlots(Team.Right, q);
        StartQuestionAudio(q); // chỉ Combined
    }

    // ── Independent mode ───────────────────────────────────────────────────────

    protected override void SetupIndependentDisplay(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        RevealDecorations();
        SetDecorationsActive(team, true);
        StopIdleEffects(team);
        // Round MỚI của riêng bên NÀY → ẩn icon ✔/✖ fallback round trước CỦA BÊN NÀY (xem
        // OnQuestionShown — bản Combined ẩn cả 2 bên cùng lúc, Independent chỉ ẩn đúng 1 bên để
        // không đụng icon bên KIA đang hiện dở, 2 bên không đồng bộ round).
        HideFeedbackIcon(team);
        // Round MỚI của riêng bên này → dọn sạch "giỏ" FlyToStay của round trước CỦA BÊN NÀY.
        ResetCollectSlots(team, q.correctAnswers?.Length ?? 1, q);
        ResetRoundSumAndLaunch(team, q);
        // Round MỚI của riêng bên này → điểm round hiển thị về 0 ngay (xem OnQuestionShown — bản
        // Combined dùng chỗ đó, Independent mỗi bên tự nhịp riêng nên reset ở đây).
        _lastRoundPoints[team] = 0;
        RefreshDecoScoreTexts();
        ApplyRoundLayout(team, q);
        ShowQuestionSlots(team, q);
        // Không phát âm thanh câu hỏi ở Independent (2 bên tranh nhau) — xem StartQuestionAudio.
        Action<bool, Team, int[]> wrappedOnDone = (correct, t, answer) =>
        {
            // SumToTarget: sai (vượt mục tiêu) KHÔNG BAO GIỜ kết thúc round — chờ người chơi chạm thanh
            // để chọn lại (xem SumReset), không tự mở khoá theo timer như wrongEndsRound=false.
            if (!correct && SumMode) return;
            if (!correct && !_package.settings.wrongEndsRound)
            {
                // "Sai không kết thúc round": KHÔNG gọi onDone thật — IndependentPlayerLoop (base
                // class) vẫn đang await onDone nên coi như round CHƯA xong, tiếp tục chờ ở round
                // này. Sau khoảng feedbackDelayWrong (đã đủ thời gian cho hiệu ứng/tô đỏ sai hiện
                // ra), mở khoá lại ĐÚNG bên này để thử lại CÙNG câu hỏi.
                StartCoroutine(RetryWrongAfterDelay(team));
                return;
            }
            StopIdleEffects(team);
            // Round của bên này THẬT SỰ kết thúc — sau khoảng feedback (đủ thời gian xem hiệu ứng
            // đúng/sai) thì ẩn hẳn câu hỏi + đáp án của bên này, chỉ còn nền + "Next in Ns" (nếu
            // có) cho tới khi round mới thật sự bắt đầu. Dùng CHÍNH feedbackDelayCorrect/Wrong của
            // base class để khớp đúng thời điểm base class tự hiện "Next in Ns" riêng cho bên này
            // (IndependentRoundCountdown) — không có hook nào khác chạy đúng lúc đó để ẩn hộ.
            StartCoroutine(HideQuestionAndAnswersAfterDelay(team, correct ? feedbackDelayCorrect : feedbackDelayWrong));
            onDone?.Invoke(correct, t, answer);
        };
        if (UsesSpawnFlow)
        {
            spawnFlowDisplay.SetupPlayerIndependent(team, q, wrappedOnDone);
        }
        else
        {
            buttonDisplay.SetupPlayerIndependent(team, q, wrappedOnDone);
            ApplyRoundContent(team, q);
            StartIdleEffects(team, q);
        }
    }

    /// <summary>Trong khoảng "Next in Ns" chỉ còn nền + chữ đếm ngược: dọn đáp án đã bay vào vùng "Đã chọn", ô sáng/vạch mục tiêu của thanh cộng dồn,
    /// icon ✔/✖ và ẨN item của bên này. Round mới tự bật lại item (SetDecorationsActive(team, true) trong OnQuestionShown/SetupIndependentDisplay).</summary>
    void HideRoundEffects(Team team)
    {
        HideFeedbackIcon(team);
        DestroyAll(_collectClones[team]);
        DestroyAll(_sumLitCells[team]);
        DestroyAll(_sumStatic[team]);
        if (_sumHint.TryGetValue(team, out var h) && h != null) { Destroy(h); _sumHint[team] = null; }
        SetDecorationsActive(team, false);
    }

    void SetDecorationsActive(Team team, bool active)
    {
        if (!_decorationsRevealed) return; // chưa vào round thật đầu tiên: RevealDecorations lo
        foreach (var it in _items)
            if (it.team == team && it.rt != null) it.rt.gameObject.SetActive(active);
    }

    IEnumerator HideQuestionAndAnswersAfterDelay(Team team, float delay)
    {
        yield return new WaitForSeconds(delay);
        HideQuestionAndAnswers(team);
    }

    /// <summary>Ẩn hẳn câu hỏi + TOÀN BỘ ô đáp án của 1 bên — dùng trong khoảng chờ chuyển round
    /// ("Next in Ns") để chỉ còn nền + nhân vật/item + dòng đếm ngược, không để lộ câu hỏi/đáp án
    /// round VỪA XONG (đã tô xanh/đỏ/khoá) trong lúc đếm ngược. Round mới tự kích hoạt lại qua
    /// ShowQuestionSlots()/ButtonDisplay.SetupGroup() như bình thường, không cần tự bật lại ở đây.</summary>
    void HideQuestionAndAnswers(Team team)
    {
        HideQuestionSlots(team);
        HideRoundEffects(team);
        if (UsesSpawnFlow)
        {
            // Dọn SẠCH item spawn flow (đang bay LẪN đã đóng băng do vừa chạm) — đây là nơi DUY
            // NHẤT item đóng băng sau khi chạm thật sự bị huỷ, xem SpawnFlowDisplay.HidePlayerAnswers.
            spawnFlowDisplay.HidePlayerAnswers(team);
            return;
        }
        var items = team == Team.Left ? _leftSlotItems : _rightSlotItems;
        if (items == null) return;
        foreach (var item in items)
            if (item != null) item.gameObject.SetActive(false);
    }

    /// <summary>Combined/Solo mode: base class gọi hàm này NGAY TRƯỚC khi hiện "Next in Ns"
    /// (NextRoundAfterDelay) — ẩn luôn câu hỏi + đáp án CẢ 2 BÊN ở đây, đúng yêu cầu "hết hiệu ứng
    /// thì tắt hết câu hỏi/đáp án, chỉ còn nền + Next in Ns".</summary>
    protected override void CleanupCurrentDisplay()
    {
        base.CleanupCurrentDisplay();
        StopIdleEffects(Team.Left);
        StopIdleEffects(Team.Right);
        StopQuestionAudio();
        HideQuestionAndAnswers(Team.Left);
        HideQuestionAndAnswers(Team.Right);
    }

    IEnumerator RetryWrongAfterDelay(Team team)
    {
        yield return new WaitForSeconds(feedbackDelayWrong);
        if (UsesSpawnFlow) spawnFlowDisplay.ResetTeamAttempt(team);
        else buttonDisplay.ResetTeamAttempt(team);
    }

    // ── Hiệu ứng theo slot (ghi đè hiệu ứng chung) ─────────────────────────────

    /// <summary>SlotFx của đáp án `answerIndex` ở round hiện tại của `team` (null nếu không tra được).</summary>
    SlotFx SlotFxOf(Team team, int answerIndex)
    {
        if (!_teamQuestion.TryGetValue(team, out var q) || q == null || !_rounds.TryGetValue(q.id, out var rr)) return null;
        var slots = rr.spec.answers.slots;
        return answerIndex >= 0 && answerIndex < slots.Length ? slots[answerIndex].fx : null;
    }

    /// <summary>Slot có hiệu ứng/âm thanh riêng ở trigger này thì THAY HẲN hiệu ứng chung, không thì dùng cái chung. ("None" tính là có hiệu ứng → cố ý tắt.)</summary>
    static ActionFx Pick(ActionFx slotOverride, ActionFx global)
        => slotOverride != null && ((slotOverride.effects != null && slotOverride.effects.Length > 0) || !string.IsNullOrEmpty(slotOverride.sound)) ? slotOverride : global;

    void StartIdleEffects(Team team, QuestionData q)
    {
        PlaySound(_package.effects.onIdle.sound);
        var list = new List<Coroutine>();
        for (int i = 0; i < q.answers.Length; i++)
        {
            var btn = buttonDisplay.GetActiveButtonByAnswerIndex(team, i);
            if (btn == null) continue;
            ((RectTransform)btn.transform).localScale = Vector3.one; // idle round trước bị dừng giữa nhịp có thể để lại scale lệch
            var fx = Pick(SlotFxOf(team, i)?.onIdle, _package.effects.onIdle);
            foreach (var spec in fx.effects)
            {
                var type = ParseType(spec.type);
                if (type == EffectType.None) continue;
                var c = EffectLibrary.Play(this, (RectTransform)btn.transform, type, MirrorForTeam(spec.effectParams, team, type));
                if (c != null) list.Add(c);
            }
        }
        _idleCoroutines[team] = list;
    }

    void StopIdleEffects(Team team)
    {
        if (!_idleCoroutines.TryGetValue(team, out var list)) return;
        foreach (var c in list) if (c != null) StopCoroutine(c);
        list.Clear();
    }

    // ── Tap effects ────────────────────────────────────────────────────────────

    /// <summary>Lấy ĐÚNG ButtonItem đang hiện answerIndex này bất kể display nào đang active — thay
    /// thế mọi chỗ trước đây gọi thẳng buttonDisplay.GetActiveButtonByAnswerIndex (sai khi game
    /// dùng spawn flow).</summary>
    ButtonItem GetActiveAnswerButton(Team team, int answerIndex) => UsesSpawnFlow
        ? spawnFlowDisplay.GetActiveButtonByAnswerIndex(team, answerIndex)
        : buttonDisplay.GetActiveButtonByAnswerIndex(team, answerIndex);

    void HandleAnswerTapped(Team team, int answerIndex, ClickResult result)
    {
        var btn = GetActiveAnswerButton(team, answerIndex);
        if (btn == null) return;
        var rt = (RectTransform)btn.transform;
        var slotFx = SlotFxOf(team, answerIndex);
        PlayAnswerSlotSound(team, answerIndex); // chạm vào slot = phát âm thanh riêng của slot (nếu có)

        if (SumMode)
        {
            HandleSumTapped(team, btn, rt, result);
            if (!_package.settings.showCorrectWrongTint) StartCoroutine(SuppressTintNextFrame(team));
            return;
        }

        switch (result)
        {
            case ClickResult.CorrectFinal:
                LaunchDecos(team);
                StopQuestionAudio();
                // Kit mặc định GIỮ NGUYÊN hiển thị đáp án cuối (tô xanh ApplyFinalState), KHÔNG tự
                // ẩn như các đáp án đúng trước đó (CorrectPartial). Nếu game đã gắn hiệu ứng
                // "biến mất" (onCorrectRemove, vd FlyTo) thì đáp án CUỐI cũng phải bay giống hệt
                // các đáp án trước — tự ẩn bản gốc rồi chạy clone, không chờ Kit làm hộ.
                var remove = Pick(slotFx?.onCorrectRemove, _package.effects.onCorrectRemove);
                if (HasRealEffects(remove))
                {
                    PlayCorrectRemoveClone(team, btn, rt, remove);
                    btn.gameObject.SetActive(false);
                }
                PlayActionFx(team, rt, Pick(slotFx?.onCorrectTap, _package.effects.onCorrectTap), true);
                FireItems(team, ItemTrigger.Correct);
                break;
            case ClickResult.WrongFinal:
                PlayActionFx(team, rt, Pick(slotFx?.onWrongTap, _package.effects.onWrongTap), false);
                FireItems(team, ItemTrigger.Wrong);
                // "Sai không kết thúc round" ở Combined mode: Kit vẫn tự khoá side này
                // (ApplyMultiFinalState) + kiểm tra CheckBothWrong ngay sau khi hàm này return —
                // nếu bên KIA chưa sai thì round chưa thật sự kết thúc ở tầng FSM, mở khoá lại
                // được bình thường. Nếu CẢ 2 bên cùng sai thì Kit vẫn tự kết thúc round (giới hạn
                // đã biết, xem SCHEMA.md — không can thiệp được từ đây).
                if (!_package.settings.wrongEndsRound && playMode != MiniGamePlayMode.Independent)
                    StartCoroutine(RetryWrongAfterDelay(team));
                break;
            case ClickResult.CorrectPartial:
                // MultiSelect: Kit sẽ SetActive(false) bản gốc NGAY sau khi callback này return —
                // nhân bản 1 ảnh y hệt TRƯỚC khi điều đó xảy ra rồi chạy hiệu ứng trên bản sao, bản
                // gốc ẩn phựt theo đúng hành vi Kit, không ảnh hưởng gì tới nhau.
                PlayCorrectRemoveClone(team, btn, rt, Pick(slotFx?.onCorrectRemove, _package.effects.onCorrectRemove));
                FireItems(team, ItemTrigger.Partial);
                // "perCorrectCount": mỗi đáp án đúng CHƯA hết round cũng phải +1 vào điểm round
                // hiển thị NGAY (không đợi tới lúc round kết thúc mới cộng dồn 1 lần) — đáp án
                // CUỐI (CorrectFinal) đã được AwardDefaultPoint/OnRoundResult set đúng tổng cuối
                // cùng rồi, không cần cộng thêm ở đây.
                if (_package.settings.scoringMode == "perCorrectCount")
                {
                    _lastRoundPoints.TryGetValue(team, out var cur);
                    _lastRoundPoints[team] = cur + 1;
                    RefreshDecoScoreTexts();
                }
                break;
        }

        if (!_package.settings.showCorrectWrongTint)
            StartCoroutine(SuppressTintNextFrame(team));
    }

    void PlayAnswerSlotSound(Team team, int answerIndex)
    {
        if (!_teamQuestion.TryGetValue(team, out var q) || q == null || !_rounds.TryGetValue(q.id, out var rr)) return;
        var slots = rr.spec.answers.slots;
        if (answerIndex < 0 || answerIndex >= slots.Length) return;
        PlaySound(slots[answerIndex].sound);
    }

    /// <summary>Ghi đè lại màu nền của TẤT CẢ ô đang active về bình thường NGAY SAU khi
    /// Kit tự tô xanh/đỏ/xám (ApplyFinalState/ApplyMultiFinalState chạy ngay sau khi hàm này được
    /// gọi, trong CÙNG frame — phải đợi 1 frame rồi mới ghi đè, gọi ngay lập tức sẽ bị Kit tô đè
    /// lại lần nữa). Chỉ đổi màu, KHÔNG đụng trạng thái khoá (_locked) — ô vẫn không bấm lại được
    /// như thiết kế gốc, chỉ là không hiện màu báo hiệu.</summary>
    IEnumerator SuppressTintNextFrame(Team team)
    {
        yield return null;
        var items = team == Team.Left ? _leftSlotItems : _rightSlotItems;
        if (items == null) yield break;
        foreach (var item in items)
            if (item != null && item.gameObject.activeSelf) item.SetBackgroundTransparent(false);
    }

    /// <summary>CHỈ dùng cho onCorrectTap/onWrongTap (2 chỗ cắm duy nhất gọi hàm này — xem
    /// HandleAnswerTapped) — chạy TẤT CẢ effect khai báo CÙNG LÚC trên target, cộng thêm âm
    /// thanh + icon ✔/✖ fallback mặc định của Kit nếu game không tự định nghĩa riêng. Vì
    /// GenericGameController tự quyết định sound/icon ở đây (UseDefaultFeedbackFx=false), base
    /// class KHÔNG còn tự phát âm thanh/hiện icon mặc định song song nữa — tránh phát 2 lần.</summary>
    void PlayActionFx(Team team, RectTransform rt, ActionFx fx, bool correct)
    {
        if (fx.playSound)
        {
            if (!string.IsNullOrEmpty(fx.sound)) PlaySound(fx.sound);
            else if (correct) MusicManager.Instance?.PlayCorrectSfx();
            else MusicManager.Instance?.PlayWrongSfx();
        }
        if (fx.showIcon) ShowFeedbackIcon(team, correct);
        foreach (var spec in fx.effects)
        {
            var type = ParseType(spec.type);
            if (type == EffectType.None) continue;
            EffectLibrary.Play(this, rt, type, MirrorForTeam(spec.effectParams, team, type));
        }
    }

    void PlaySound(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || sfxAudioSource == null) return;
        var clip = AssetOverrideLoader.GetClip(ResolveAudioPath(fileName));
        if (clip != null) sfxAudioSource.PlayOneShot(clip);
    }

    /// <summary>Dọn sạch mọi item đã "Bay tới & Ở lại" (FlyToStay) của 1 bên + đếm lại từ ô đầu —
    /// gọi ở ĐẦU mỗi round mới (OnQuestionShown/SetupIndependentDisplay) vì mặc định KHÔNG tích luỹ
    /// xuyên suốt game, chỉ trong phạm vi 1 round. `correctCount` = số đáp án ĐÚNG của round SẮP
    /// hiện (QuestionData.correctAnswers.Length) — chỉ thật sự dùng khi `collectAutoStretch=true`,
    /// để tính lại lưới MỚI vừa khít đúng số đó (xem BuildEffectiveCollectSlots).</summary>
    void ResetCollectSlots(Team team, int correctCount, QuestionData q)
    {
        _collectCounters[team] = 0;
        var list = _collectClones[team];
        foreach (var go in list) if (go != null) Destroy(go);
        list.Clear();
        _effectiveCollectSlots[team] = BuildEffectiveCollectSlots(Mathf.Max(1, correctCount), q);
    }

    /// <summary>`collectAutoStretch=false` (mặc định) → dùng thẳng `layout.collectSlots` đã soạn
    /// sẵn (Lưới tĩnh hay Tự do đều ra cùng 1 mảng, không khác gì hành vi gốc). `true` → BỎ QUA
    /// mảng đó, tự chia `collectArea` thành đúng `correctCount` ô theo `collectCols` cột (lấp đầy,
    /// không gap) + áp `collectFillReverse` (Z→A = đảo thứ tự mảng kết quả).</summary>
    RectPct[] BuildEffectiveCollectSlots(int correctCount, QuestionData q)
    {
        var g = CollectOf(q);
        if (g == null) return Array.Empty<RectPct>();
        // "Lấp theo giá trị"/SumToTarget BỎ QUA auto-stretch — thanh có đúng số ô đã soạn.
        if (FillByValueFor(q) || !g.autoStretch)
        {
            var fixedSlots = new RectPct[g.slots.Length];
            for (int i = 0; i < fixedSlots.Length; i++)
                fixedSlots[i] = new RectPct { xPct = g.slots[i].xPct, yPct = g.slots[i].yPct, wPct = g.slots[i].wPct, hPct = g.slots[i].hPct };
            return fixedSlots;
        }
        int cols = Mathf.Max(1, g.matrix.cols);
        int rows = Mathf.CeilToInt(correctCount / (float)cols);
        var ca = g.area;
        float itemW = ca.wPct / cols, itemH = ca.hPct / rows;
        var slots = new RectPct[correctCount];
        for (int i = 0; i < correctCount; i++)
        {
            int r = i / cols, c = i % cols;
            slots[i] = new RectPct { xPct = ca.xPct + c * itemW, yPct = ca.yPct + r * itemH, wPct = itemW, hPct = itemH };
        }
        if (g.fillReverse) Array.Reverse(slots);
        return slots;
    }

    /// <summary>Vùng thu thập của round q (nhóm collect trong RoundSpecV2) — null nếu không tra được.</summary>
    GroupSpec CollectOf(QuestionData q) => q != null && _rounds.TryGetValue(q.id, out var rr) ? rr.spec.collect : null;

    /// <summary>Nửa phải có lật ngang vùng "Đã chọn" không: Right && collect.mirror (mặc định true).</summary>
    bool CollectFlipsFor(Team team)
    {
        if (team != Team.Right) return false;
        var g = CollectOf(_teamQuestion.TryGetValue(team, out var q) ? q : null);
        return g == null || g.mirror;
    }

    bool FillByValueFor(QuestionData q) => SumMode || (CollectOf(q)?.fillByValue ?? false);

    /// <summary>Tính targetXPct/targetYPct/targetWPct/targetHPct cho FlyToStay từ Ô TIẾP THEO trong
    /// mảng ô ĐÃ TÍNH SẴN cho round hiện tại (`_effectiveCollectSlots`, xem ResetCollectSlots —
    /// tĩnh từ `layout.collectSlots` hoặc tính lại mỗi round nếu `collectAutoStretch=true`), tăng
    /// dần theo _collectCounters, clamp ở ô cuối nếu vượt quá (người soạn tự đảm bảo không xảy ra
    /// ở chế độ tĩnh; chế độ autoStretch luôn khớp đúng số lượng nên không bao giờ clamp). Mảng rỗng
    /// (chưa cấu hình) → trả nguyên `p` (fallback dùng targetXPct/Y tĩnh đã mirror sẵn, giống FlyTo).</summary>
    EffectParams ApplyCollectSlot(EffectParams p, Team team, int span, out int first, out int last)
    {
        first = last = -1;
        var slots = _effectiveCollectSlots.TryGetValue(team, out var s) ? s : null;
        if (slots == null || slots.Length == 0) return p;
        // span > 1 (chế độ "Lấp theo giá trị"): đáp án phủ `span` ô LIỀN NHAU kể từ ô đang đếm tới, bay
        // tới đúng KHUNG BAO của cả nhóm ô đó; hết ô thì dừng ở ô cuối (không báo lỗi).
        span = Mathf.Max(1, span);
        first = Mathf.Min(_collectCounters[team], slots.Length - 1);
        last = Mathf.Min(first + span - 1, slots.Length - 1);
        _collectCounters[team] += span;
        var slot = UnionSlots(team, slots, first, last);
        p.targetXPct = slot.xPct + slot.wPct / 2f;
        p.targetYPct = slot.yPct + slot.hPct / 2f;
        p.targetWPct = slot.wPct;
        p.targetHPct = slot.hPct;
        return p;
    }

    /// <summary>Khung bao (đã mirror cho bên phải) của các ô slots[first..last].</summary>
    RectPct UnionSlots(Team team, RectPct[] slots, int first, int last)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int k = first; k <= last; k++)
        {
            var r = CollectFlipsFor(team) ? MirrorRect(slots[k]) : slots[k];
            minX = Mathf.Min(minX, r.xPct); minY = Mathf.Min(minY, r.yPct);
            maxX = Mathf.Max(maxX, r.xPct + r.wPct); maxY = Mathf.Max(maxY, r.yPct + r.hPct);
        }
        return new RectPct { xPct = minX, yPct = minY, wPct = maxX - minX, hPct = maxY - minY };
    }

    void PlayCorrectRemoveClone(Team team, ButtonItem btn, RectTransform rt, ActionFx fx)
    {
        // playSound/showIcon áp dụng GIỐNG HỆT onCorrectTap/onWrongTap (xem PlayActionFx) — đáp án
        // đúng-chưa-hết-round vẫn là "đúng", fallback dùng đúng âm thanh correct mặc định của Kit
        // nếu game không tự định nghĩa sound riêng. showIcon mặc định FALSE ở đây (khác
        // onCorrectTap) vì onCorrectRemove bắn liên tục nhiều lần/round (mỗi lần gom 1 đáp án) —
        // hiện icon to mỗi lần sẽ gây rối mắt, chỉ bật khi game tự chọn.
        if (fx.playSound)
        {
            if (!string.IsNullOrEmpty(fx.sound)) PlaySound(fx.sound);
            else MusicManager.Instance?.PlayCorrectSfx();
        }
        // Icon ✔ phải chờ hiệu ứng (vd FlyToStay bay vào giỏ) chạy XONG mới hiện — hiện ngay lập tức
        // trước đây che mất hiệu ứng bay, trông sai thứ tự "báo đúng" so với "vừa bay xong". Không
        // có effect nào thì hiện ngay (giữ nguyên hành vi cũ).
        if (fx.effects.Length == 0)
        {
            if (fx.showIcon) ShowFeedbackIcon(team, true);
            return;
        }

        var clone = Instantiate(btn.gameObject, rt.parent);
        clone.name = btn.name + "_FxClone";
        ApplyLayer(clone, "collect");
        var cloneRt = (RectTransform)clone.transform;
        cloneRt.anchorMin = rt.anchorMin;
        cloneRt.anchorMax = rt.anchorMax;
        cloneRt.offsetMin = rt.offsetMin;
        cloneRt.offsetMax = rt.offsetMax;
        cloneRt.localScale = rt.localScale;

        var cloneItem = clone.GetComponent<ButtonItem>();
        if (cloneItem != null) Destroy(cloneItem);
        var cg = clone.GetComponent<CanvasGroup>();
        if (cg == null) cg = clone.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var realTypes = new List<EffectType>();
        float maxDur = 0f;
        foreach (var spec in fx.effects)
        {
            var t = ParseType(spec.type);
            if (t == EffectType.None) continue;
            realTypes.Add(t);
            maxDur = Mathf.Max(maxDur, spec.effectParams.duration > 0 ? spec.effectParams.duration : 0.45f);
        }
        if (realTypes.Count == 0)
        {
            if (fx.showIcon) ShowFeedbackIcon(team, true);
            Destroy(clone);
            return;
        }
        if (fx.showIcon) StartCoroutine(ShowFeedbackIconAfterDelay(team, maxDur));

        // FlyToStay = item Ở LẠI VĨNH VIỄN (tới khi round mới reset — xem ResetCollectSlots) — KHÔNG
        // tự Destroy khi effect xong, kể cả khi đi kèm effect khác trong CÙNG chỗ cắm.
        // "Lấp theo giá trị" (SumToTarget/collectFillByValue): thẻ đáp án bay tới rồi BIẾN MẤT (không
        // đọng lại), thay bằng N ô SÁNG LÊN — nên không giữ clone.
        bool fillByValue = FillByValueFor(_teamQuestion.TryGetValue(team, out var fq) ? fq : null);
        bool keepsClone = realTypes.Contains(EffectType.FlyToStay) && !fillByValue;
        if (keepsClone) _collectClones[team].Add(clone);

        int span = fillByValue ? FillSpanOf(team, btn) : 1;
        int litFirst = -1, litLast = -1;
        int remaining = realTypes.Count;
        foreach (var spec in fx.effects)
        {
            var type = ParseType(spec.type);
            if (type == EffectType.None) continue;
            var p = MirrorForTeam(spec.effectParams, team, type);
            if (type == EffectType.FlyToStay)
            {
                p = ApplyCollectSlot(p, team, span, out int f, out int l);
                if (litFirst < 0) { litFirst = f; litLast = l; }
            }
            EffectLibrary.Play(this, cloneRt, type, p, () => { if (!keepsClone && --remaining <= 0) Destroy(clone); });
        }
        if (fillByValue && litFirst >= 0) SpawnLitCells(team, (RectTransform)rt.parent, litFirst, litLast, maxDur * 0.65f);
    }

    IEnumerator ShowFeedbackIconAfterDelay(Team team, float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowFeedbackIcon(team, true);
    }

    static EffectType ParseType(string s) => Enum.TryParse<EffectType>(s, out var t) ? t : EffectType.None;

    /// <summary>true nếu ActionFx có ít nhất 1 effect khác "None" — dùng để quyết định có cần
    /// nhân bản/ẩn đáp án cuối hay không (xem HandleAnswerTapped's CorrectFinal case).</summary>
    static bool HasRealEffects(ActionFx fx) => fx != null && fx.effects != null && Array.Exists(fx.effects, s => ParseType(s.type) != EffectType.None);

    /// <summary>Mirror targetXPct (FlyTo) / angleDeg (FlyOff) cho bên PHẢI — web tool chỉ cho đặt
    /// 1 lần (xem như đang đặt ở nửa trái), bên phải tự lấy đối xứng ngang, đúng quy ước layout
    /// slots/questionArea đã dùng xuyên suốt.</summary>
    static EffectParams MirrorForTeam(EffectParams p, Team team, EffectType type)
    {
        if (team != Team.Right) return p;
        // FlyToStay: chỉ mirror targetXPct ở đây khi CHƯA cấu hình collectSlots (fallback 1 điểm
        // tĩnh, giống FlyTo) — có collectSlots thì ApplyCollectSlot() tự mirror NGUYÊN Ô qua
        // MirrorRect() rồi, gọi mirror thêm ở đây sẽ bị lệch (gọi SAU, không ảnh hưởng kết quả
        // cuối vì ApplyCollectSlot ghi đè lại targetXPct hoàn toàn — an toàn giữ chung 1 nhánh).
        if (type == EffectType.FlyTo || type == EffectType.FlyToStay) p.targetXPct = 100f - p.targetXPct;
        if (type == EffectType.FlyOff) p.angleDeg = NormalizeAngle(180f - p.angleDeg);
        return p;
    }
    static float NormalizeAngle(float a) { a %= 360f; if (a < 0f) a += 360f; return a; }

    // ── Scoring ────────────────────────────────────────────────────────────────

    protected override void AwardDefaultPoint(Team team)
    {
        int pts = _package.settings.scoringMode == "perCorrectCount"
            ? Mathf.Max(1, SumMode ? _sumPicked[team] : (CurrentQuestion?.correctAnswers?.Length ?? 1))
            : Mathf.Max(1, _package.settings.flatPoints);
        _lastRoundPoints[team] = pts; // dùng cho decoration bind="roundScore"
        ScoreManager.AddPoints(team, pts);
    }

    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        // Independent mode: IndependentPlayerLoop (base class) đã tự +1 điểm cố định rồi — chỉ
        // cộng THÊM phần chênh lệch ở đây. Combined/Solo mode đã xử lý đủ qua AwardDefaultPoint ở
        // trên (HandleResult gọi AwardDefaultPoint TRƯỚC khi gọi OnRoundResult).
        if (!correct || playMode != MiniGamePlayMode.Independent) return;
        int pts = _package.settings.scoringMode == "perCorrectCount"
            ? Mathf.Max(1, SumMode ? _sumPicked[team] : (playerAnswer?.Length ?? 1))
            : Mathf.Max(1, _package.settings.flatPoints);
        _lastRoundPoints[team] = pts; // dùng cho decoration bind="roundScore"
        if (pts > 1) ScoreManager.AddPoints(team, pts - 1);
        RefreshDecoScoreTexts(); // Independent mode không qua OnScoreChanged nếu pts==1 (AddPoints không gọi) — ép refresh để roundScore vẫn cập nhật
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => UsesSpawnFlow ? (IAnswerDisplay)spawnFlowDisplay : buttonDisplay;

    /// <summary>GenericGame không có nội dung tutorial riêng cho từng game (web tool chưa hỗ trợ
    /// soạn video/text hướng dẫn) — bỏ qua hẳn màn "Bat dau"/tutorialPanel mặc định của Kit, vào
    /// thẳng "Start in 3,2,1" cho gọn, đúng yêu cầu "mặc định bỏ tutorial".</summary>
    protected override void StateMachineEnter_Tutorial(Enum prev, Dictionary<string, object> opts)
    {
        StartGame();
    }

    /// <summary>Combined/Solo mode: base class tự gọi display.Setup() ở đây (KHÔNG có hook riêng
    /// nào khác chạy SAU khi Setup xong) — chèn ApplyRoundContent() ngay sau base để đặt ảnh nền/icon/chữ thật lên
    /// các ô đúng lúc PickSlots đã xáo xong vị trí. Independent mode tự áp dụng riêng trong SetupIndependentDisplay
    /// (không đi qua state này).</summary>
    protected override void StateMachineEnter_WaitAnswer(Enum prev, Dictionary<string, object> opts)
    {
        base.StateMachineEnter_WaitAnswer(prev, opts);
        if (playMode == MiniGamePlayMode.Independent) return;
        ApplyRoundContent(Team.Left, CurrentQuestion);
        ApplyRoundContent(Team.Right, CurrentQuestion);
        if (!UsesSpawnFlow)
        {
            StartIdleEffects(Team.Left, CurrentQuestion);
            StartIdleEffects(Team.Right, CurrentQuestion);
        }
    }

    protected override bool UseIndependentRoundCountdown => _package != null && _package.settings.countdownMode == "nextInN";
    protected override bool UseDefaultTransitionCountdown => _package == null || _package.settings.countdownMode != "none";

    /// <summary>Tắt hẳn cơ chế feedback mặc định của base class (tự phát PlayCorrectSfx/PlayWrongSfx
    /// + hiện icon ✔/✖ NGAY KHI round kết thúc, không biết gì về cấu hình riêng từng game) —
    /// GenericGameController tự quyết định phát âm thanh gì (custom hay fallback mặc định) và có
    /// hiện icon hay không theo ActionFx.playSound/showIcon của TỪNG game, qua PlayActionFx(). Nếu
    /// để base class tự chạy song song thì âm thanh mặc định sẽ phát ĐÈ LÊN âm thanh custom của
    /// game mỗi lần đúng/sai — bug thật nếu không tắt cờ này.</summary>
    protected override bool UseDefaultFeedbackFx => false;

    // ══════════════════════════════════════════════════════════════════════════
    // SumToTarget ("Cộng dồn tới mục tiêu") + lấp ô theo GIÁ TRỊ + "tên lửa" (launchOnComplete)
    // — port từ game_builder.html (sumToTargetPick/sumReset/fillByValueOn/launchDecos). Luật:
    //   • Mọi đáp án bấm được; mỗi lần bấm cộng giá trị SỐ của đáp án vào "thanh nhiên liệu"
    //     (layout.collectSlots) — đáp án "3" phủ 3 ô, ô SÁNG LÊN theo màu gradient xanh→đỏ.
    //   • Tổng == mục tiêu (round.target, mặc định = số ô của thanh) → THẮNG (CorrectFinal) + tên lửa bay.
    //   • Tổng VƯỢT mục tiêu → hiệu ứng SAI + khoá + gợi ý; hết đáp án mà chưa đủ → cũng gợi ý. KHÔNG
    //     bao giờ kết thúc round vì sai — CHẠM VÀO THANH = chọn lại từ đầu (không trừ điểm).
    // Giới hạn đã biết: chưa hỗ trợ ở spawn flow; Combined mode nếu CẢ 2 bên cùng vượt thì Kit vẫn tự
    // kết thúc round (giống wrongEndsRound=false, xem SCHEMA.md).
    // ══════════════════════════════════════════════════════════════════════════

    bool SumMode => _package != null && _package.settings.answerMode == "SumToTarget";

    int DefaultSumTarget(RoundSpecV2 r)
    {
        int n = r.collect != null && r.collect.slots != null ? r.collect.slots.Length : 0;
        return Mathf.Max(1, n > 0 ? n : 10);
    }

    /// <summary>Số ô mà đáp án này chiếm khi "Lấp theo giá trị" = giá trị SỐ của đáp án (QuestionData.
    /// sumValues, đã parse từ `text` lúc BuildQuestion — không phải số/≤0 → 1).</summary>
    int FillSpanOf(Team team, ButtonItem btn)
    {
        if (btn == null || !_teamQuestion.TryGetValue(team, out var q) || q == null || q.sumValues == null) return 1;
        int idx = btn.AnswerIndex;
        return idx >= 0 && idx < q.sumValues.Length ? Mathf.Max(1, q.sumValues[idx]) : 1;
    }

    static void DestroyAll(List<GameObject> list)
    {
        foreach (var go in list) if (go != null) Destroy(go);
        list.Clear();
    }

    /// <summary>Gọi ở ĐẦU mỗi round của `team` (sau ResetCollectSlots) — nhớ câu hỏi hiện tại của bên
    /// này, dọn state cộng dồn round trước, dựng lại vạch MỤC TIÊU + vùng chạm-để-chọn-lại, trả tên
    /// lửa về chỗ cũ.</summary>
    void ResetRoundSumAndLaunch(Team team, QuestionData q)
    {
        _teamQuestion[team] = q;
        _sumPicked[team] = 0;
        _sumWon[team] = false;
        DestroyAll(_sumLitCells[team]);
        DestroyAll(_sumStatic[team]);
        _sumHidden[team].Clear();
        if (_sumHint.TryGetValue(team, out var h) && h != null) Destroy(h);
        _sumHint[team] = null;
        ResetDecoLaunch(team);
        if (SumMode && !UsesSpawnFlow && q != null) BuildSumBarOverlays(team, q);
    }

    static RectPct InsetSlot(RectPct r) => new RectPct
    {
        xPct = r.xPct + r.wPct * 0.02f, yPct = r.yPct + r.hPct * 0.043f,
        wPct = r.wPct * 0.96f, hPct = r.hPct * 0.914f,
    };

    void BuildSumBarOverlays(Team team, QuestionData q)
    {
        var parent = team == Team.Left ? leftButtonsRoot : rightButtonsRoot;
        if (parent == null || !_effectiveCollectSlots.TryGetValue(team, out var slots) || slots == null || slots.Length == 0) return;

        // Viền VÀNG quanh ô MỤC TIÊU của round này (round khác mục tiêu khác → vạch khác chỗ).
        int k = Mathf.Min(q.sumTarget, slots.Length) - 1;
        if (k >= 0)
        {
            var marker = new GameObject("SumTargetMarker", typeof(RectTransform));
            marker.transform.SetParent(parent, false);
            ApplyLayer(marker, "collect", 1);
            ApplyRectPct((RectTransform)marker.transform, InsetSlot(UnionSlots(team, slots, k, k)));
            var gold = new Color(1f, 0.835f, 0.29f, 1f);
            AddBorderBars(marker.transform, gold, 3f);
            _sumStatic[team].Add(marker);
        }

        // Vùng chạm phủ TOÀN BỘ thanh → chạm để chọn lại. Đặt làm anh em ĐẦU TIÊN (vẽ/raycast dưới
        // cùng) để nếu thanh có chồng lên ô đáp án thì ô đáp án vẫn nhận chạm trước.
        var tap = new GameObject("SumBarTap", typeof(RectTransform), typeof(Image), typeof(Button));
        tap.transform.SetParent(parent, false);
        tap.transform.SetAsFirstSibling();
        ApplyLayer(tap, "answers", -1); // ngay DƯỚI đáp án để đáp án vẫn nhận chạm trước
        ApplyRectPct((RectTransform)tap.transform, UnionSlots(team, slots, 0, slots.Length - 1));
        var img = tap.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var btn = tap.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        var t = team;
        btn.onClick.AddListener(() => SumReset(t));
        _sumStatic[team].Add(tap);
    }

    static void AddBorderBars(Transform parent, Color c, float px)
    {
        MakeBar(parent, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, px), c);
        MakeBar(parent, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, px), c);
        MakeBar(parent, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(px, 0), c);
        MakeBar(parent, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(px, 0), c);
    }

    static void MakeBar(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    /// <summary>Thay thế HandleAnswerTapped's switch khi SumMode — xem khối comment đầu vùng này.</summary>
    void HandleSumTapped(Team team, ButtonItem btn, RectTransform rt, ClickResult result)
    {
        _sumPicked[team]++;
        var slotFx = SlotFxOf(team, btn.AnswerIndex);
        switch (result)
        {
            case ClickResult.CorrectFinal:
                _sumWon[team] = true;
                StopQuestionAudio();
                // Giống đáp án cuối ở chế độ thường: có hiệu ứng "biến mất" thì đáp án CUỐI cũng bay vào
                // thanh (lấp nốt các ô còn lại) rồi mới ẩn bản gốc.
                var remove = Pick(slotFx?.onCorrectRemove, _package.effects.onCorrectRemove);
                if (HasRealEffects(remove))
                {
                    PlayCorrectRemoveClone(team, btn, rt, remove);
                    btn.gameObject.SetActive(false);
                }
                LaunchDecos(team);
                PlayActionFx(team, rt, Pick(slotFx?.onCorrectTap, _package.effects.onCorrectTap), true);
                FireItems(team, ItemTrigger.Correct);
                break;
            case ClickResult.WrongFinal:
                // Vượt mục tiêu → hiệu ứng SAI tại chỗ (không bay), Kit đã tự khoá bên này; chờ chạm thanh.
                PlayActionFx(team, rt, Pick(slotFx?.onWrongTap, _package.effects.onWrongTap), false);
                FireItems(team, ItemTrigger.Wrong);
                ShowSumRetryHint(team);
                break;
            case ClickResult.CorrectPartial:
                PlayCorrectRemoveClone(team, btn, rt, Pick(slotFx?.onCorrectRemove, _package.effects.onCorrectRemove));
                FireItems(team, ItemTrigger.Partial);
                _sumHidden[team].Add(btn); // Kit ẩn bản gốc ngay sau callback — nhớ để trả về khi chọn lại
                if (CountActiveOtherButtons(team, btn) == 0) ShowSumRetryHint(team); // hết đáp án mà chưa đủ
                break;
        }
    }

    int CountActiveOtherButtons(Team team, ButtonItem except)
    {
        var items = team == Team.Left ? _leftSlotItems : _rightSlotItems;
        if (items == null) return 1; // không biết (vd spawn flow) → coi như còn, không hiện gợi ý
        int n = 0;
        foreach (var it in items) if (it != null && it != except && it.gameObject.activeSelf) n++;
        return n;
    }

    void ShowSumRetryHint(Team team)
    {
        if (_sumWon[team]) return;
        var parent = team == Team.Left ? leftButtonsRoot : rightButtonsRoot;
        if (parent == null) return;
        if (_sumHint.TryGetValue(team, out var old) && old != null) Destroy(old);

        var go = new GameObject("SumRetryHint", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.12f, 0.02f); rt.anchorMax = new Vector2(0.88f, 0.09f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("Text", typeof(RectTransform));
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.SetParent(rt, false);
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = "Chạm thanh nhiên liệu để chọn lại";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 0.835f, 0.29f, 1f);
        txt.fontStyle = FontStyle.Bold;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.resizeTextMaxSize = Mathf.Max(14, Mathf.RoundToInt(parent.rect.height * 0.045f));
        txt.raycastTarget = false;
        _sumHint[team] = go;
    }

    /// <summary>Chạm vào thanh nhiên liệu: bỏ hết lựa chọn của round này, trả đáp án về chỗ cũ, tắt các
    /// ô đã sáng, chọn lại từ đầu — KHÔNG trừ điểm. Bỏ qua nếu đã thắng hoặc chưa chọn gì.</summary>
    void SumReset(Team team)
    {
        if (!SumMode || _sumWon[team] || _sumPicked[team] == 0) return;
        _sumPicked[team] = 0;
        _collectCounters[team] = 0;
        DestroyAll(_sumLitCells[team]);
        if (_sumHint.TryGetValue(team, out var h) && h != null) Destroy(h);
        _sumHint[team] = null;
        foreach (var it in _sumHidden[team]) if (it != null) it.gameObject.SetActive(true);
        _sumHidden[team].Clear();
        HideFeedbackIcon(team);
        // Validator mới tinh (tổng về 0) + màu về Normal cho các ô đang hiện (ĐÃ bật lại ở trên).
        if (UsesSpawnFlow) spawnFlowDisplay.ResetTeamAttempt(team);
        else buttonDisplay.ResetTeamAttempt(team);
        StopIdleEffects(team);
        if (!UsesSpawnFlow && _teamQuestion.TryGetValue(team, out var q) && q != null) StartIdleEffects(team, q);
    }

    // Màu thanh nhiên liệu theo vị trí ô: xanh lá → vàng (45%) → cam (75%) → đỏ (100%).
    static readonly (float t, Color c)[] FuelStops =
    {
        (0f,    new Color32(46, 204, 113, 255)),
        (0.45f, new Color32(241, 225, 60, 255)),
        (0.75f, new Color32(255, 160, 40, 255)),
        (1f,    new Color32(255, 82, 70, 255)),
    };

    static Color FuelColorAt(float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 1; i < FuelStops.Length; i++)
            if (t <= FuelStops[i].t)
                return Color.Lerp(FuelStops[i - 1].c, FuelStops[i].c, (t - FuelStops[i - 1].t) / (FuelStops[i].t - FuelStops[i - 1].t));
        return FuelStops[FuelStops.Length - 1].c;
    }

    /// <summary>Dựng các ô SÁNG LÊN (số thứ tự ô + màu gradient) cho slots[first..last] — hiện dần (fade)
    /// sau `fadeDelay` giây (khi thẻ đáp án bay gần tới nơi). Giữ tới hết round (ResetCollectSlots) hoặc
    /// tới khi chạm thanh chọn lại (SumReset).</summary>
    void SpawnLitCells(Team team, RectTransform parent, int first, int last, float fadeDelay)
    {
        if (!_effectiveCollectSlots.TryGetValue(team, out var slots) || slots == null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int k = first; k <= last; k++)
        {
            var sl = CollectFlipsFor(team) ? MirrorRect(slots[k]) : slots[k];
            var go = new GameObject("FuelCell" + (k + 1), typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            ApplyLayer(go, "collect", 1);
            ApplyRectPct(rt, InsetSlot(sl));
            var img = go.GetComponent<Image>();
            img.color = FuelColorAt(slots.Length > 1 ? k / (float)(slots.Length - 1) : 0f);
            img.raycastTarget = false;
            var edge = go.AddComponent<UnityEngine.UI.Outline>();
            edge.effectColor = new Color(1f, 1f, 1f, 0.85f);
            edge.effectDistance = new Vector2(2f, -2f);
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

            var txtGo = new GameObject("Num", typeof(RectTransform));
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.SetParent(rt, false);
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = font;
            txt.text = (k + 1).ToString();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 8;
            txt.resizeTextMaxSize = 200;
            var shadow = txtGo.AddComponent<UnityEngine.UI.Outline>();
            shadow.effectColor = new Color(0.08f, 0.16f, 0.27f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            _sumLitCells[team].Add(go);
            _collectClones[team].Add(go); // dọn cùng "giỏ" FlyToStay khi round mới (ResetCollectSlots)
            StartCoroutine(FadeInAfter(cg, fadeDelay, 0.26f));
        }
    }

    static IEnumerator FadeInAfter(CanvasGroup cg, float delay, float duration)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float t = 0f;
        while (cg != null && t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    // ── "Tên lửa" (DecorationSpec.launchOnComplete) ───────────────────────────

    /// <summary>Deco đánh dấu launchOnComplete của `team` bay thẳng LÊN khỏi màn hình (~1.5s, tăng tốc)
    /// khi bên đó THẮNG round. Dừng hiệu ứng liên tục (Breathing...) của deco đó trước khi bay.</summary>
    void LaunchDecos(Team team)
    {
        foreach (var d in _launchDecos)
        {
            if (d.team != team || d.rt == null) continue;
            if (d.idle != null) { StopCoroutine(d.idle); d.idle = null; }
            if (d.flight != null) StopCoroutine(d.flight);
            d.rt.localScale = Vector3.one;
            float dist = d.bottomPct / 100f * d.parent.rect.height + 40f; // từ đáy deco tới mép trên + 40px
            d.flight = StartCoroutine(LaunchRoutine(d, dist, 1.5f));
        }
    }

    IEnumerator LaunchRoutine(LaunchDeco d, float dist, float duration)
    {
        float t = 0f;
        while (d.rt != null && t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            var off = new Vector2(0f, dist * k * k); // ease-in: càng bay càng nhanh (như web cubic-bezier .45,.05,.9,.5)
            d.rt.offsetMin = d.origMin + off;
            d.rt.offsetMax = d.origMax + off;
            yield return null;
        }
    }

    /// <summary>Round MỚI: trả tên lửa (CHỈ những deco đã bay) về chỗ cũ + chạy lại hiệu ứng liên tục.</summary>
    void ResetDecoLaunch(Team team)
    {
        foreach (var d in _launchDecos)
        {
            if (d.team != team || d.rt == null || d.flight == null) continue;
            StopCoroutine(d.flight);
            d.flight = null;
            d.rt.offsetMin = d.origMin;
            d.rt.offsetMax = d.origMax;
            var fx = EffectLibrary.Create(d.type);
            if (fx != null && d.type != EffectType.FlyOff && d.type != EffectType.FlyTo)
                d.idle = StartCoroutine(fx.Play(d.rt, d.effectParams, this));
        }
    }
}
