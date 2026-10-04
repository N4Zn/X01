using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ĐẾM QUẢ" — bản ĐƠN GIẢN HƠN của HaiQuaGame (xem HaiQuaController.cs). Mỗi bên chỉ có 1 khung
/// cảnh = 1 LOẠI QUẢ + 1 RỔ (không phải 5 rổ/5 loại như HaiQua). Mỗi round: 1-5 quả (số đếm CHỈ
/// từ 1 đến 5, khác HaiQua 3-6), chạm hết → quả bay vào rổ → PHÁT ÂM THANH GHÉP "số + tên quả"
/// (vd 5 củ cà rốt → phát clip "5" rồi clip "củ cà rốt" nối tiếp nhau, xem AnnounceCount/
/// PlaySequentialAndRelease) → sau đó sang round mới (loại quả khác, số lượng khác).
///
/// 2 bên PHẢI phát audio LẦN LƯỢT, không được chồng tiếng cùng lúc (dù dùng 2 AudioSource riêng)
/// — xem _audioQueue/TryPlayNextAnnouncement: bên nào hoàn thành round SAU phải chờ bên kia phát
/// xong (cả 2 clip) mới tới lượt, dù round của nó đã xong từ trước.
///
/// Không có chỉ tiêu/không có ràng buộc né lặp như HaiQua — đơn giản, chơi liên tục theo thời gian
/// (GameSettings.GameTime), đúng tinh thần "clone đơn giản hơn" user yêu cầu. Rổ CÓ hiện 1 text
/// đếm số quả — NHƯNG đếm LẠI TỪ 0 mỗi round (KHÔNG cộng dồn cả ván, khác suy nghĩ ban đầu) — xem
/// IncrementRoundCounter()/ResetRoundCounter(). Sau khi hái hết quả trong round, Kit tự chờ
/// feedbackDelayCorrect (~1.2-1.8s, xem field kế thừa từ MiniGameControllerBase) trước khi sang
/// round mới — ĐÚNG khoảng thời gian để AnnounceCount() phát xong audio "số + tên quả" (vd "ba quả
/// táo") trước khi round sau xoá số về 0.
///
/// Tái dùng NGUYÊN XI ButtonDisplay + AnswerValidator.MultiSelect (mọi quả đều đúng, không cần
/// thứ tự) — giống HaiQua, không viết IAnswerDisplay mới, không cần CSV.
///
/// Ảnh/âm thanh do user tự chuẩn bị — xem SpriteRoot/NumberAudioRoot/FruitAudioRoot bên dưới cho
/// đúng đường dẫn cần đặt file.
/// </summary>
public class DemQuaController : MiniGameControllerBase
{
    [Header("DemQua — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Image leftBackground;
    [SerializeField] Image rightBackground;
    [SerializeField] RectTransform leftBasketIcon;
    [SerializeField] RectTransform rightBasketIcon;
    [Tooltip("Tổng số quả đã hái CỘNG DỒN cả ván (mọi loại quả gộp chung, không tách theo loại như HaiQua) — hiện NGAY trên thân rổ, tăng dần theo từng cú chạm.")]
    [SerializeField] Text leftBasketCountText;
    [SerializeField] Text rightBasketCountText;
    [Tooltip("5 slot Image quả TRONG giỏ (con của basket icon, xem DemQuaGameSceneBuilder.CreateBasketFruitSlots) — ẩn sẵn, hiện dần đúng thứ tự khi hái được (xem RevealBasketFruitSlot).")]
    [SerializeField] Image[] leftBasketFruitSlots;
    [SerializeField] Image[] rightBasketFruitSlots;

    [Header("DemQua — layout quả trong giỏ (chỉnh trực tiếp ở đây, xem ngay cả lúc KHÔNG Play nhờ OnValidate)")]
    [Tooltip("Kích thước mỗi icon quả — fractional so với rect của basket (0-1), KHÔNG phải pixel. Vd 0.44 = 44% chiều rộng/cao basket.")]
    [SerializeField] Vector2 basketFruitSize = new Vector2(0.44f, 0.56f);
    [Tooltip("Khoảng cách hàng SAU (3 quả) và hàng TRƯỚC (2 quả) theo trục dọc — fractional so với basket. Số nhỏ = 2 hàng gần nhau/chồng lên nhau.")]
    [SerializeField] float basketFruitRowDistance = 0.12f;
    [Tooltip("Khoảng cách giữa 2 quả CÙNG hàng — fractional so với basket (hàng sau dùng khoảng cách này giữa quả giữa và quả 2 bên; hàng trước dùng 1 nửa giá trị này). Số nhỏ = các quả gần/chồng nhau.")]
    [SerializeField] float basketFruitColumnDistance = 0.18f;
    [Tooltip("Dịch TÂM cả cụm 5 quả lên (+)/xuống (-) so với TÂM basket (0.5) — fractional so với basket.")]
    [SerializeField] float basketFruitCenterOffsetY = -0.1f;

    /// <summary>Chạy cả lúc KHÔNG Play (Editor gọi khi đổi giá trị Inspector) lẫn lúc Play (Start())
    /// để layout luôn khớp field hiện tại, không cần Build Scene lại mỗi lần chỉnh số.</summary>
    void OnValidate() => ApplyBasketFruitLayout();

    public void ApplyBasketFruitLayout()
    {
        ApplyBasketFruitLayoutForSide(leftBasketFruitSlots);
        ApplyBasketFruitLayoutForSide(rightBasketFruitSlots);
    }

    /// <summary>5 slot xếp pyramid: 3 quả hàng sau (cao hơn) + 2 quả hàng trước (thấp hơn, so le
    /// giữa 3 quả sau) — toàn bộ tham số (size/khoảng cách/tâm cụm) lấy từ field Inspector phía
    /// trên, KHÔNG hardcode như bản cũ, để chỉnh trực tiếp không cần sửa code/Build Scene lại.</summary>
    void ApplyBasketFruitLayoutForSide(Image[] slots)
    {
        if (slots == null || slots.Length < 5) return;

        Vector2 half = basketFruitSize * 0.5f;
        Vector2 clusterCenter = new Vector2(0.5f, 0.5f + basketFruitCenterOffsetY);
        float backY  = clusterCenter.y + basketFruitRowDistance * 0.5f;
        float frontY = clusterCenter.y - basketFruitRowDistance * 0.5f;

        Vector2[] centers =
        {
            new Vector2(clusterCenter.x - basketFruitColumnDistance,        backY),  // hàng sau trái
            new Vector2(clusterCenter.x,                                    backY),  // hàng sau giữa
            new Vector2(clusterCenter.x + basketFruitColumnDistance,        backY),  // hàng sau phải
            new Vector2(clusterCenter.x - basketFruitColumnDistance * 0.5f, frontY), // hàng trước trái
            new Vector2(clusterCenter.x + basketFruitColumnDistance * 0.5f, frontY), // hàng trước phải
        };

        for (int i = 0; i < slots.Length && i < centers.Length; i++)
        {
            if (slots[i] == null) continue;
            var rt = slots[i].rectTransform;
            rt.anchorMin = centers[i] - half;
            rt.anchorMax = centers[i] + half;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    // Text đếm ngược "Start/Next in Ns" — KHÔNG khai báo field mới ở đây: MiniGameControllerBase
    // đã có SẴN leftCountdownText/rightCountdownText (protected, dùng cho "Next in Ns" mặc định
    // của Combined mode — xem TransitionCountdown/ShowTransitionCountdown) — khai lại field TRÙNG
    // TÊN sẽ bị Unity báo lỗi "same field name is serialized multiple times" (đã gặp thật). Field
    // kế thừa vẫn dùng bình thường qua tên leftCountdownText/rightCountdownText, không cần khai
    // báo gì thêm — SceneBuilder tự FindProperty đúng field của lớp cha qua SerializedObject.

    [Tooltip("CHÍNH XÁC những GameObject đã wire vào ButtonDisplay.leftButtons/rightButtons (kiểu " +
             "ButtonItem, KHÔNG phải RectTransform) — dùng để ĐỔI VỊ TRÍ theo chỉ số mảng " +
             "(RandomizeFruitPositions, ButtonDisplay luôn dồn quả active về đầu mảng nên an toàn " +
             "để suy theo vị trí ở ĐÂY). KHÔNG dùng mảng này để tìm nút vừa CHẠM — việc đó phải qua " +
             "ButtonDisplay.GetActiveButtonByAnswerIndex() (xem OnFruitTapped), vì answerIndex " +
             "KHÔNG phải vị trí mảng.")]
    [SerializeField] ButtonItem[] leftFruitSlots;
    [SerializeField] ButtonItem[] rightFruitSlots;

    [Header("DemQua — âm thanh đếm (ghép \"số\" + \"tên quả\", xem AnnounceCount)")]
    [Tooltip("Mỗi bên 1 AudioSource riêng — 2 bên có thể hoàn thành round cùng lúc, dùng chung 1 " +
             "AudioSource sẽ bị 1 bên cắt ngang âm thanh của bên kia.")]
    [SerializeField] AudioSource leftAudioSource;
    [SerializeField] AudioSource rightAudioSource;
    [Tooltip("Số giây CẮT BỚT ở cuối clip số trước khi phát clip tên quả — clip số thường có 1 đoạn " +
             "lặng ở cuối, chờ đủ hết clip mới phát tiếp nghe bị khoảng dừng dài giữa 'số' và 'tên " +
             "quả'. Tăng giá trị này nếu vẫn còn nghe khoảng lặng, giảm nếu bị cắt mất tiếng số.")]
    [SerializeField] float numberFruitGapTrim = 0.15f;
    [Tooltip("TẠM tắt — chỉ đọc SỐ, bỏ qua clip tên quả (vd cu_ca_rot) theo yêu cầu. Bật lại (true) khi cần ghép đủ 'số + tên quả'.")]
    [SerializeField] bool announceFruitName = false;

    [Header("HaiQua — hiệu ứng bay lên rổ")]
    [SerializeField] float flyDuration = 0.4f;
    [Tooltip("Để trống = dùng ô vuông trắng mặc định (placeholder) thay vì sprite quả thật.")]
    [SerializeField] Sprite flyIconSprite;
    [Tooltip("Kích thước (pixel) của quả lúc đang bay lên rổ — trước hardcode 48x48, quá nhỏ so với quả trên màn chơi (150px) và quả trong rổ (to). Tăng lên cho khớp.")]
    [SerializeField] Vector2 flyIconSize = new Vector2(120f, 120f);

    [Header("DemQua — khu vực rải quả (fractional 0-1 toàn Canvas)")]
    [SerializeField] Vector2 leftGardenMin = new Vector2(0.05f, 0.12f);
    [SerializeField] Vector2 leftGardenMax = new Vector2(0.45f, 0.62f);
    [SerializeField] Vector2 rightGardenMin = new Vector2(0.55f, 0.12f);
    [SerializeField] Vector2 rightGardenMax = new Vector2(0.95f, 0.62f);

    // Lưới ẩn 3x2 = 6 ô (dư 1 so với MaxFruit=5) — đủ chỗ rải quả không chồng nhau, xem
    // HaiQuaController.RandomizeFruitPositions cho logic gốc (copy nguyên).
    static readonly Vector2Int GardenGrid = new Vector2Int(3, 2);

    const int MinFruit = 1;
    const int MaxFruit = 5; // số đếm CHỈ 1-5 theo yêu cầu — khác HaiQua (3-6)

    const string SpriteRoot      = "TestTongHop/images/fruits/";       // dùng CHUNG với HaiQuaGame
    const string NumberAudioRoot = "SoDem/";                           // + "1".."5" → "SoDem/1".."SoDem/5" (Assets/Resources/SoDem/, file thật user cung cấp)
    const string FruitAudioRoot  = "TestTongHop/audio/fruit_names/";   // clip đọc CẢ cụm "đơn vị đếm + tên quả" (quy ước cũ — dùng cho loại quả CHƯA có audio thật riêng)

    // Định nghĩa 1 loại quả — audioName trỏ tới 1 clip đọc SẴN cả cụm "củ cà rốt"/"quả táo"/
    // "chùm nho" (đơn vị đếm khác nhau theo loại, không suy ra tự động được nên ghi sẵn cả cụm
    // trong file audio, không ghép "đơn vị" + "tên" riêng — chỉ ghép "số" + "cụm-này").
    // KHÁC HaiQuaGame: game này KHÔNG loại trừ quả theo chùm (nho/chuối) vì mỗi loại đã có đơn vị
    // đếm riêng trong audio, không cần là 1 thể đơn lẻ.
    [Serializable]
    struct FruitDef
    {
        public string id;
        public string label;      // fallback hiển thị nếu spritePath chưa có ảnh thật
        public string spritePath; // "TestTongHop/images/fruits/<ten>" — đi qua ButtonItem/ItemMediaHelper
                                   // (CSV-style, tự cộng imageRoot) nên BẮT BUỘC nằm trong
                                   // Resources/TestTongHop/images/... — không đổi tự do được.
        public string audioName;  // "TestTongHop/audio/fruit_names/<ten>" — clip đọc "củ cà rốt", "chùm nho", ...
        public Color color;       // màu nền placeholder — dùng khi backgroundPath rỗng (chưa có ảnh nền thật)
        public string backgroundPath; // "Background/<ten>_BG" — load TRỰC TIẾP bằng Resources.Load (KHÔNG qua
                                       // ItemMediaHelper/imageRoot, nên tự do nằm ở Resources/Background/ như
                                       // user đã đặt) — rỗng = chưa có ảnh nền thật, dùng color phẳng thay thế.
    }

    static readonly FruitDef[] Fruits =
    {
        new FruitDef { id = "CaRot",  label = "Cà rốt",  spritePath = SpriteRoot + "carot",  audioName = "Fruit/cu_ca_rot",  color = new Color(1.00f, 0.55f, 0.15f), backgroundPath = "Background/Carrot_BG" },
        new FruitDef { id = "Tao",    label = "Táo",     spritePath = SpriteRoot + "tao",    audioName = FruitAudioRoot + "tao",    color = new Color(0.85f, 0.20f, 0.20f) },
        new FruitDef { id = "Nho",    label = "Nho",     spritePath = SpriteRoot + "nho",    audioName = FruitAudioRoot + "nho",    color = new Color(0.55f, 0.30f, 0.65f) },
        new FruitDef { id = "Cam",    label = "Cam",     spritePath = SpriteRoot + "cam",    audioName = FruitAudioRoot + "cam",    color = new Color(1.00f, 0.60f, 0.05f) },
        new FruitDef { id = "Chuoi",  label = "Chuối",   spritePath = SpriteRoot + "chuoi",  audioName = FruitAudioRoot + "chuoi",  color = new Color(0.95f, 0.85f, 0.25f) },
        new FruitDef { id = "DuaHau", label = "Dưa hấu", spritePath = SpriteRoot + "duahau", audioName = FruitAudioRoot + "duahau", color = new Color(0.25f, 0.65f, 0.30f) },
    };

    // Loại quả + số lượng của round ĐANG chơi từng bên — cần để AnnounceCount() biết phát clip nào
    // khi round đó kết thúc (CorrectFinal xảy ra trong OnFruitTapped, không có sẵn QuestionData).
    FruitDef _leftFruit, _rightFruit;
    int _leftCount, _rightCount;

    // Số quả đã hái TRONG ROUND ĐANG CHƠI — hiện trên thân rổ, đếm LẠI TỪ 0 mỗi round (reset ở
    // SetupIndependentDisplay), KHÔNG cộng dồn qua các round như suy nghĩ ban đầu.
    int _leftRoundCount, _rightRoundCount;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        if (buttonDisplay != null) buttonDisplay.onAnswerTapped = OnFruitTapped;
        ApplyBasketFruitLayout(); // đảm bảo đúng layout lúc chạy thật, không chỉ phụ thuộc OnValidate ở Editor
    }

    /// <summary>Mỗi củ = 1 điểm, cộng vào điểm TỔNG (HUD) khi round kết thúc. IndependentPlayerLoop
    /// (base class) đã tự ScoreManager.AddPoint(team) = +1 CỐ ĐỊNH mỗi round trước khi gọi hook
    /// này (xem MiniGameControllerBase) — cộng thêm ĐÚNG (count-1) điểm còn lại ở đây để tổng cả
    /// round = count (số quả đã hái), không phải luôn +1 bất kể hái được mấy quả.</summary>
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        if (!correct) return; // luôn đúng trong game này (mọi quả đều là đáp án đúng), giữ để phòng hờ
        int count = team == Team.Left ? _leftCount : _rightCount;
        if (count > 1) ScoreManager.AddPoints(team, count - 1);
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    // TẠM THỜI chỉ test 1 loại có ảnh/nền thật (CaRot) — 5 loại còn lại chưa có art, để random đủ
    // hết sẽ ra placeholder xen kẽ gây khó test. Đổi lại thành null (hoặc xoá điều kiện bên dưới
    // trong PullNextQuestion) để random đủ 6 loại như thiết kế gốc khi có đủ ảnh/nền các loại kia.
    const string TestOnlyFruitId = "CaRot";

    /// <summary>Không dùng CSV — mỗi lần gọi sinh 1 loại quả NGẪU NHIÊN với số lượng ngẫu nhiên
    /// (1-5), mọi quả đều là đáp án đúng. Không cần team-aware (khác HaiQua) vì không có trạng
    /// thái tích luỹ/né lặp nào phải theo dõi riêng từng bên.</summary>
    protected override QuestionData PullNextQuestion()
    {
        var fruit = string.IsNullOrEmpty(TestOnlyFruitId)
            ? Fruits[UnityEngine.Random.Range(0, Fruits.Length)]
            : FindFruit(TestOnlyFruitId);
        int count = UnityEngine.Random.Range(MinFruit, MaxFruit + 1);

        var answers = new string[count];
        var correct = new int[count];
        for (int i = 0; i < count; i++)
        {
            answers[i] = fruit.spritePath;
            correct[i] = i; // MỌI quả đều đúng — không có đáp án sai
        }

        return new QuestionData
        {
            id                 = fruit.id,
            questionType       = QuestionType.Choose,
            questionMediaType  = QuestionMediaType.Text,
            questionMediaValue = "",
            answerMediaType    = AnswerMediaType.Image,
            answers            = answers,
            answerMode         = AnswerMode.MultiSelect,
            correctAnswers     = correct,
        };
    }

    protected override void SetupIndependentDisplay(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        var fruit = FindFruit(q.id);
        if (team == Team.Left) { _leftFruit = fruit; _leftCount = q.correctAnswers.Length; }
        else { _rightFruit = fruit; _rightCount = q.correctAnswers.Length; }

        ResetRoundCounter(team);

        var bg = team == Team.Left ? leftBackground : rightBackground;
        ApplyBackground(bg, fruit, flipHorizontal: team == Team.Right);

        // Rổ ẩn sẵn từ lúc dựng scene (tránh hiện placeholder xấu trước khi round 1 sẵn sàng, xem
        // DemQuaGameSceneBuilder) — bật lại đúng lúc bên này có round đầu tiên. Gọi lại mỗi round
        // vô hại (SetActive(true) trên object đã active là no-op) — rổ KHÔNG ẩn lại mỗi round sau
        // (nó hiện tổng cộng dồn cả ván, ẩn/hiện lại mỗi round sẽ giật hình vô nghĩa).
        var basketIcon = team == Team.Left ? leftBasketIcon : rightBasketIcon;
        if (basketIcon != null) basketIcon.gameObject.SetActive(true);

        // "Start/Next in Ns" do MiniGameControllerBase tự lo (UseIndependentRoundCountdown, mặc định
        // bật) — CHẠY TRƯỚC khi SetupIndependentDisplay() này được gọi lại cho round
        // sau, nên tới đây là "go time", gọi thẳng không cần tự đếm/tự trì hoãn gì thêm nữa.
        buttonDisplay.SetupPlayerIndependent(team, q, onDone);
        RandomizeFruitPositions(team, q.correctAnswers.Length);
    }

    static readonly Dictionary<string, Sprite> _backgroundCache = new();

    /// <summary>Ảnh nền thật (backgroundPath) nếu có — load TRỰC TIẾP bằng Resources.Load (KHÔNG
    /// qua ItemMediaHelper/AssetOverrideLoader như spritePath quả, nên KHÔNG bị cộng imageRoot —
    /// backgroundPath tự do nằm ở Resources/Background/ như user đã đặt file thật). Chưa có ảnh
    /// (backgroundPath rỗng) → giữ placeholder màu phẳng như cũ.
    ///
    /// flipHorizontal: bên phải dùng CHUNG 1 ảnh nền với bên trái (đỡ tốn thêm file) nhưng lật
    /// ngang cho đỡ giống hệt 1-1 — lật bằng cách đảo dấu localScale.x của chính Image (cách chuẩn
    /// để lật ảnh trong uGUI, không cần sprite lật sẵn/không cần shader).</summary>
    static void ApplyBackground(Image bg, FruitDef fruit, bool flipHorizontal)
    {
        if (bg == null) return;

        var scale = bg.rectTransform.localScale;
        bg.rectTransform.localScale = new Vector3(flipHorizontal ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x), scale.y, scale.z);

        if (string.IsNullOrEmpty(fruit.backgroundPath))
        {
            bg.sprite = null;
            bg.color = fruit.color;
            return;
        }

        if (!_backgroundCache.TryGetValue(fruit.backgroundPath, out var sprite))
        {
            sprite = Resources.Load<Sprite>(fruit.backgroundPath);
            if (sprite == null)
                Debug.LogWarning($"[DemQuaController] Không tìm thấy background sprite: {fruit.backgroundPath}");
            _backgroundCache[fruit.backgroundPath] = sprite;
        }

        if (sprite != null)
        {
            bg.sprite = sprite;
            bg.color = Color.white; // không tint — hiện đúng màu ảnh gốc
        }
        else
        {
            bg.sprite = null;
            bg.color = fruit.color; // sprite lỗi/thiếu → fallback màu phẳng, không để trắng trơn
        }
    }

    /// <summary>Đếm LẠI TỪ 0 khi round mới bắt đầu — round trước có thể đã hiện "3"/"5" trong lúc
    /// chờ AnnounceCount() phát xong audio, round này xoá về 0 rồi đếm lại từ đầu.</summary>
    void ResetRoundCounter(Team team)
    {
        if (team == Team.Left) _leftRoundCount = 0; else _rightRoundCount = 0;
        UpdateBasketText(team, 0);
        HideAllBasketFruitSlots(team);
    }

    /// <summary>Ẩn cả 5 slot quả trong giỏ — gọi lúc round MỚI bắt đầu để dọn sạch quả của round
    /// trước, chuẩn bị hiện dần lại từ đầu (xem RevealBasketFruitSlot).</summary>
    void HideAllBasketFruitSlots(Team team)
    {
        var slots = team == Team.Left ? leftBasketFruitSlots : rightBasketFruitSlots;
        if (slots == null) return;
        foreach (var s in slots) if (s != null) s.gameObject.SetActive(false);
    }

    /// <summary>Hiện slot quả thứ filledCount (1-based, khớp đúng thứ tự đã hái) trong giỏ, dùng
    /// đúng sprite của loại quả đang chơi round này (_leftFruit/_rightFruit) — gọi mỗi khi 1 quả
    /// bay tới rổ (IncrementRoundCounter), filledCount = tổng số quả đã hái TRONG round.</summary>
    void RevealBasketFruitSlot(Team team, int filledCount)
    {
        var slots = team == Team.Left ? leftBasketFruitSlots : rightBasketFruitSlots;
        if (slots == null || filledCount < 1 || filledCount > slots.Length) return;
        var slot = slots[filledCount - 1];
        if (slot == null) return;

        var fruit = team == Team.Left ? _leftFruit : _rightFruit;
        var sprite = AssetOverrideLoader.GetSprite(fruit.spritePath);
        if (sprite != null) slot.sprite = sprite;
        slot.gameObject.SetActive(true);
    }

    static FruitDef FindFruit(string id)
    {
        foreach (var f in Fruits) if (f.id == id) return f;
        return Fruits[0];
    }

    /// <summary>Xáo vị trí các quả ĐANG active trong lưới ẩn 3x2 của khu vườn — copy nguyên logic
    /// từ HaiQuaController (xem comment gốc ở đó), chỉ khác MaxFruit=5 thay vì 6.</summary>
    void RandomizeFruitPositions(Team team, int activeCount)
    {
        var slots = team == Team.Left ? leftFruitSlots : rightFruitSlots;
        if (slots == null || slots.Length == 0) return;

        int cellCount = GardenGrid.x * GardenGrid.y;
        var cellOrder = new int[cellCount];
        for (int i = 0; i < cellCount; i++) cellOrder[i] = i;
        for (int i = cellCount - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cellOrder[i], cellOrder[j]) = (cellOrder[j], cellOrder[i]);
        }

        Vector2 areaMin = team == Team.Left ? leftGardenMin : rightGardenMin;
        Vector2 areaMax = team == Team.Left ? leftGardenMax : rightGardenMax;
        Vector2 cellSize = new Vector2(
            (areaMax.x - areaMin.x) / GardenGrid.x,
            (areaMax.y - areaMin.y) / GardenGrid.y);

        for (int i = 0; i < activeCount && i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            int cell = cellOrder[i % cellCount];
            int col = cell % GardenGrid.x;
            int row = cell / GardenGrid.x;
            Vector2 cellMin = areaMin + new Vector2(col * cellSize.x, row * cellSize.y);

            Vector2 jitterMin = cellMin + cellSize * 0.15f;
            Vector2 jitterMax = cellMin + cellSize * 0.85f;
            Vector2 center = new Vector2(
                UnityEngine.Random.Range(jitterMin.x, jitterMax.x),
                UnityEngine.Random.Range(jitterMin.y, jitterMax.y));

            var rt = (RectTransform)slots[i].transform;
            Vector2 halfSize = (rt.anchorMax - rt.anchorMin) * 0.5f;
            rt.anchorMin = center - halfSize;
            rt.anchorMax = center + halfSize;
        }
    }

    /// <summary>Bắn cho MỌI lần chạm đúng — chạy TRƯỚC khi ButtonDisplay ẩn quả vừa chạm, nên còn
    /// kịp lấy vị trí quả để bay lên rổ. CorrectFinal (quả cuối của round) mới phát âm thanh đếm
    /// (xem AnnounceCount) — không phát dồn dập giữa chừng.
    ///
    /// QUAN TRỌNG: answerIndex là ButtonItem.AnswerIndex (chỉ số LOGIC trong QuestionData.answers),
    /// KHÔNG phải vị trí trong mảng leftFruitSlots/rightFruitSlots — ButtonDisplay.PickSlots() xáo
    /// việc gán answerIndex cho từng slot hiển thị mỗi round. Dùng API dùng chung
    /// ButtonDisplay.GetActiveButtonByAnswerIndex() (đã lọc activeSelf sẵn) thay vì tự dò lại.</summary>
    void OnFruitTapped(Team team, int answerIndex, ClickResult result)
    {
        if (result != ClickResult.CorrectPartial && result != ClickResult.CorrectFinal) return;

        // SFX "đúng" mỗi lần dẫm 1 quả — dùng chung MusicManager.PlayCorrectSfx() như MỌI game
        // khác trong Kit (xem MiniGameControllerBase.PlayDefaultFeedbackFx), nhưng DemQua ở
        // Independent mode không đi qua đường đó nên phải tự gọi ở đây.
        MusicManager.Instance?.PlayCorrectSfx();

        var tappedButton = buttonDisplay.GetActiveButtonByAnswerIndex(team, answerIndex);
        var basket = team == Team.Left ? leftBasketIcon : rightBasketIcon;
        bool isFinal = result == ClickResult.CorrectFinal;

        if (isFinal && tappedButton != null)
            tappedButton.gameObject.SetActive(false);

        if (tappedButton != null)
            StartCoroutine(FlyThenReveal(tappedButton.transform.position, basket, tappedButton.CurrentSprite, team, isFinal));
        else
        {
            // Không lấy được vị trí quả (hiếm) — không có hiệu ứng bay để chờ, cập nhật ngay như cũ.
            IncrementRoundCounter(team);
            PunchBasket(basket);
            if (isFinal) AnnounceCount(team);
        }
    }

    /// <summary>Chờ HẾT hiệu ứng bay (FlyToBasket) rồi MỚI cập nhật số trên rổ + hiện quả trong giỏ
    /// + nảy rổ + (nếu là quả cuối) phát âm thanh đếm — trước đây các bước này chạy NGAY khi chạm,
    /// tách rời khỏi lúc quả thật sự "tới nơi", nhìn bị lệch (số/hình đổi trước khi quả bay xong).</summary>
    IEnumerator FlyThenReveal(Vector3 fromWorldPos, RectTransform basket, Sprite fruitSprite, Team team, bool isFinal)
    {
        yield return FlyToBasket(fromWorldPos, basket, fruitSprite);

        IncrementRoundCounter(team);
        PunchBasket(basket);

        if (isFinal)
            AnnounceCount(team);
    }

    /// <summary>+1 vào số đếm CỦA ROUND ĐANG CHƠI, hiện NGAY trên thân rổ — reset về 0 khi round
    /// MỚI bắt đầu (xem ResetRoundCounter), không cộng dồn qua các round.</summary>
    void IncrementRoundCounter(Team team)
    {
        int count = team == Team.Left ? ++_leftRoundCount : ++_rightRoundCount;
        UpdateBasketText(team, count);
        RevealBasketFruitSlot(team, count);
    }

    void UpdateBasketText(Team team, int value)
    {
        var text = team == Team.Left ? leftBasketCountText : rightBasketCountText;
        if (text != null) text.text = value.ToString();
    }


    // Cache AudioClip theo path — load bằng Resources.Load TRỰC TIẾP (KHÔNG qua
    // AssetOverrideLoader.GetClip như trước) vì path giờ nằm ở 2 thư mục KHÁC NHAU, không theo
    // quy ước TestTongHop/audio/ nữa (Audio/SoDem/ và Fruit/ do user cung cấp thật) —
    // AssetOverrideLoader sẽ tự cộng nhầm prefix "TestTongHop/audio/" vào path không bắt đầu bằng
    // đúng root đó (xem AssetOverrideLoader.Resolve), y hệt lý do ApplyBackground() cũng load
    // Resources trực tiếp thay vì qua pipeline CSV.
    static readonly Dictionary<string, AudioClip> _audioClipCache = new();

    static AudioClip LoadClip(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_audioClipCache.TryGetValue(path, out var clip))
        {
            clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[DemQuaController] Không tìm thấy audio clip: {path}");
            _audioClipCache[path] = clip;
        }
        return clip;
    }

    // Hàng đợi audio DÙNG CHUNG cho cả 2 bên — dù 2 AudioSource khác nhau (leftAudioSource/
    // rightAudioSource) vẫn phải PHÁT LẦN LƯỢT, không được chồng tiếng (2 giọng đọc cùng lúc nghe
    // không rõ). Bên nào hái xong quả cuối TRƯỚC thì phát trước; bên kia phải chờ phát xong (cả 2
    // clip) rồi mới tới lượt mình, dù round của nó đã xong từ sớm hơn.
    readonly Queue<System.Action> _audioQueue = new();
    bool _audioPlaying;

    /// <summary>Phát 2 clip NỐI TIẾP nhau: "số" (1-5) rồi tới "tên quả" (đã ghi sẵn cả cụm đơn vị
    /// đếm, vd "củ cà rốt"/"chùm nho") — vd round vừa hái 5 củ cà rốt → phát "5" rồi "củ cà rốt".
    /// Xếp hàng qua _audioQueue thay vì phát ngay — xem ghi chú ở _audioQueue.</summary>
    void AnnounceCount(Team team)
    {
        var source = team == Team.Left ? leftAudioSource : rightAudioSource;
        if (source == null) return;

        var fruit = team == Team.Left ? _leftFruit : _rightFruit;
        int count = team == Team.Left ? _leftCount : _rightCount;

        var numberClip = LoadClip(NumberAudioRoot + count);
        var fruitClip  = announceFruitName ? LoadClip(fruit.audioName) : null;

        _audioQueue.Enqueue(() => StartCoroutine(PlaySequentialAndRelease(source, numberClip, fruitClip)));
        TryPlayNextAnnouncement();
    }

    void TryPlayNextAnnouncement()
    {
        if (_audioPlaying || _audioQueue.Count == 0) return;
        _audioPlaying = true;
        _audioQueue.Dequeue().Invoke();
    }

    /// <summary>Phát xong CẢ 2 clip (chờ đủ thời lượng, kể cả clip thứ 2 — khác bản gốc chỉ chờ
    /// clip đầu) rồi mới nhả khoá + tự kích hoạt clip đang xếp hàng của bên kia (nếu có). Clip đầu
    /// bị CẮT BỚT numberFruitGapTrim giây cuối trước khi phát clip 2 — xem ghi chú ở
    /// numberFruitGapTrim (giảm khoảng lặng nối "số" + "tên quả" nghe bị dài).</summary>
    IEnumerator PlaySequentialAndRelease(AudioSource source, AudioClip first, AudioClip second)
    {
        if (first != null)
        {
            source.clip = first;
            source.Play();
            yield return new WaitForSeconds(Mathf.Max(0f, first.length - numberFruitGapTrim));
        }
        if (second != null)
        {
            source.clip = second;
            source.Play();
            yield return new WaitForSeconds(second.length);
        }
        _audioPlaying = false;
        TryPlayNextAnnouncement();
    }

    // ── Hiệu ứng: quả bay (vòng cung) lên rổ rồi biến mất ────────────────────────
    /// <summary>fruitSprite = ảnh THẬT của đúng quả vừa chạm (ButtonItem.CurrentSprite) — ưu tiên
    /// dùng ảnh này thay vì flyIconSprite (field cũ, giờ chỉ còn là fallback khi quả đang hiện
    /// dạng chữ/chưa có ảnh thật) để hiệu ứng bay không còn là 1 ô vuông trắng vô nghĩa.</summary>
    IEnumerator FlyToBasket(Vector3 fromWorldPos, RectTransform basket, Sprite fruitSprite)
    {
        if (basket == null) yield break;

        var go = new GameObject("FlyingFruit", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(basket.root, false);
        var rt = (RectTransform)go.transform;
        rt.position = fromWorldPos;
        rt.sizeDelta = flyIconSize;
        var img = go.GetComponent<Image>();
        img.sprite = fruitSprite != null ? fruitSprite : flyIconSprite; // fallback: ô vuông trắng mặc định
        img.preserveAspect = true;
        img.raycastTarget = false;

        Vector3 start = rt.position;
        Vector3 end = basket.position;
        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / flyDuration);
            Vector3 pos = Vector3.Lerp(start, end, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * 60f;
            rt.position = pos;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, p);
            yield return null;
        }
        Destroy(go);
    }

    // ── Hiệu ứng: rổ nảy nhẹ mỗi lần chạm ────────────────────────────────────────
    void PunchBasket(RectTransform basket)
    {
        if (basket != null) StartCoroutine(PunchScaleRoutine(basket));
    }

    IEnumerator PunchScaleRoutine(RectTransform rect)
    {
        Vector3 original = rect.localScale;
        const float half = 0.1f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            rect.localScale = original * Mathf.Lerp(1f, 1.2f, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            rect.localScale = original * Mathf.Lerp(1.2f, 1f, t / half);
            yield return null;
        }
        rect.localScale = original;
    }
}
