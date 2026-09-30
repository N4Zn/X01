using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "HÁI QUẢ" — 2 bên tự nhịp riêng (Independent). Đúng 5 LOẠI QUẢ CỐ ĐỊNH (xem Fruits[]), mỗi
/// loại có chỉ tiêu <see cref="targetPerFruit"/> (mặc định 20) — hái đủ CẢ 5 loại thì bên đó
/// "xong game" (bên kia không bị ảnh hưởng, vẫn chơi độc lập tới khi cũng xong hoặc hết giờ).
///
/// Mỗi round chỉ hiện 1 loại quả (3-6 quả/round, tự giới hạn không vượt quá phần còn thiếu của
/// loại đó) — chạm quả nào cũng đúng (tái dùng NGUYÊN XI ButtonDisplay + AnswerValidator.MultiSelect
/// với correctAnswers = toàn bộ index, xem PullNextQuestion cũ). Loại quả của round SAU không
/// được trùng round TRƯỚC (trừ khi chỉ còn đúng 1 loại chưa đủ chỉ tiêu) — xem PickFruitIndex().
///
/// UI: mỗi bên có 5 rổ (1/loại quả). Thân rổ hiện tổng đã hái "X/20". Phía TRÊN mỗi rổ có 1 ô
/// text riêng hiện "+N" — đếm số quả đã chạm TRONG round đang chơi của đúng loại đó, tăng dần
/// theo từng cú chạm; khi hái HẾT quả trong round (CorrectFinal), chờ 0.5s (cho trẻ kịp nhìn số
/// "+N" cuối) rồi mới cộng dồn vào tổng ở thân rổ và xoá "+N" về rỗng — xem
/// IncrementRoundCounter()/CommitRoundToBasketDelayed(). Quả cuối cùng của round KHÔNG bị tô màu
/// xanh "đúng" kiểu quiz thường thấy (đã tự ẩn ngay trong OnFruitTapped) — không cần feedback đó
/// cho cơ chế "hái quả bay đi".
/// </summary>
public class HaiQuaController : MiniGameControllerBase
{
    [Header("HaiQua — refs")]
    [SerializeField] ButtonDisplay buttonDisplay;
    [SerializeField] Button backButton;
    [SerializeField] Image leftBackground;
    [SerializeField] Image rightBackground;

    [Tooltip("CHÍNH XÁC những GameObject đã wire vào ButtonDisplay.leftButtons/rightButtons (kiểu " +
             "ButtonItem, KHÔNG phải RectTransform — cần đọc .AnswerIndex để tìm đúng nút vừa bấm, " +
             "xem ghi chú ở FindTappedButton). KHÔNG tự điều khiển hiện/ẩn (ButtonDisplay đã tự lo), " +
             "chỉ đọc để lấy vị trí bay + tự ẩn quả cuối round (xem OnFruitTapped).")]
    [SerializeField] ButtonItem[] leftFruitSlots;
    [SerializeField] ButtonItem[] rightFruitSlots;

    [Header("HaiQua — mục tiêu")]
    [Tooltip("Số quả cần hái cho MỖI loại (trong 5 loại ở Fruits[]) để tính là 'xong' — hái đủ cả 5 loại thì bên đó xong game.")]
    [SerializeField] int targetPerFruit = 20;

    [Header("HaiQua — 5 rổ mỗi bên (khớp thứ tự Fruits[])")]
    [SerializeField] Image[] leftBasketIcons;         // size 5
    [SerializeField] Image[] rightBasketIcons;        // size 5
    [Tooltip("Text tổng đã hái, hiện trên THÂN rổ — vd '5/20'.")]
    [SerializeField] Text[] leftBasketTotalTexts;     // size 5
    [SerializeField] Text[] rightBasketTotalTexts;    // size 5
    [Tooltip("Text đếm dở TRONG round hiện tại, hiện Ở TRÊN rổ — vd '+3'. Rỗng khi round chưa bắt đầu/đã cộng dồn xong.")]
    [SerializeField] Text[] leftBasketRoundTexts;     // size 5
    [SerializeField] Text[] rightBasketRoundTexts;    // size 5

    [Tooltip("Hiện khi bên đó hái đủ chỉ tiêu cả 5 loại — để trống nếu chưa cần UI này (không lỗi, chỉ là bên đó im lặng dừng nhận round mới).")]
    [SerializeField] Text leftCompleteText;
    [SerializeField] Text rightCompleteText;

    [Header("HaiQua — hiệu ứng bay lên rổ")]
    [SerializeField] float flyDuration = 0.4f;
    [Tooltip("Để trống = dùng ô vuông trắng mặc định (placeholder) thay vì sprite quả thật.")]
    [SerializeField] Sprite flyIconSprite;

    [Header("HaiQua — khu vực rải quả (fractional 0-1 toàn Canvas, khớp vùng đã cấp cho " +
             "leftFruitSlots/rightFruitSlots trong SceneBuilder — kích thước quả giữ nguyên, " +
             "chỉ đổi vị trí trong khu vực này)")]
    [SerializeField] Vector2 leftGardenMin = new Vector2(0.05f, 0.12f);
    [SerializeField] Vector2 leftGardenMax = new Vector2(0.45f, 0.62f);
    [SerializeField] Vector2 rightGardenMin = new Vector2(0.55f, 0.12f);
    [SerializeField] Vector2 rightGardenMax = new Vector2(0.95f, 0.62f);

    // Lưới ẩn 3x2 = 6 ô, khớp đúng FruitSlotsPerSide (SceneBuilder) — mỗi round xáo thứ tự ô rồi
    // gán lần lượt cho các quả ĐANG active, quả jitter ngẫu nhiên trong ô của mình.
    static readonly Vector2Int GardenGrid = new Vector2Int(3, 2);

    // Định nghĩa 1 loại quả — placeholder dùng màu nền phẳng cho NỀN (xem ApplySceneBackground),
    // riêng ẢNH QUẢ đã trỏ thẳng vào file thật tại spritePath — chưa có file thì ItemMediaHelper
    // tự fallback hiện chữ (label) thay vì lỗi, nên build/test được ngay cả khi chưa có ảnh.
    // Mọi quả đều là 1 THỂ ĐƠN LẺ (không theo chùm như nho/chuối).
    [Serializable]
    struct FruitDef
    {
        public string id;
        public string label;      // nhãn chữ — dùng làm fallback nếu spritePath chưa có ảnh thật
        public string spritePath; // "TestTongHop/images/fruits/<ten>" — đặt file tại
                                   // Assets/Game/Resources/TestTongHop/images/fruits/<ten>.png
                                   // (thư mục CHUNG theo loại nội dung, không riêng HaiQua — game
                                   // khác dùng quả/rau củ tương tự có thể tái dùng cùng thư mục này)
        public Color color;       // màu nền placeholder của khung cảnh / màu rổ placeholder
        public int minFruit;
        public int maxFruit;
    }

    const string SpriteRoot = "TestTongHop/images/fruits/";

    // ĐÚNG 5 loại — khớp số rổ hiển thị (leftBasketIcons/... size 5). Đổi số lượng ở đây thì PHẢI
    // đổi cả size mảng basket UI trong SceneBuilder, không tự đồng bộ.
    static readonly FruitDef[] Fruits =
    {
        new FruitDef { id = "CaRot",  label = "Cà rốt",  spritePath = SpriteRoot + "carot",  color = new Color(1.00f, 0.55f, 0.15f), minFruit = 3, maxFruit = 6 },
        new FruitDef { id = "CaChua", label = "Cà chua", spritePath = SpriteRoot + "cachua", color = new Color(0.90f, 0.25f, 0.15f), minFruit = 3, maxFruit = 6 },
        new FruitDef { id = "Tao",    label = "Táo",     spritePath = SpriteRoot + "tao",    color = new Color(0.85f, 0.20f, 0.20f), minFruit = 3, maxFruit = 6 },
        new FruitDef { id = "DuaHau", label = "Dưa hấu", spritePath = SpriteRoot + "duahau", color = new Color(0.25f, 0.65f, 0.30f), minFruit = 3, maxFruit = 6 },
        new FruitDef { id = "DauTay", label = "Dâu tây", spritePath = SpriteRoot + "dautay", color = new Color(0.95f, 0.30f, 0.45f), minFruit = 3, maxFruit = 6 },
    };

    // ── State theo từng bên (KHÔNG dùng chung field — 2 IndependentPlayerLoop chạy song song) ──
    readonly int[] _leftTotals  = new int[Fruits.Length];
    readonly int[] _rightTotals = new int[Fruits.Length];
    int _leftRoundCount, _rightRoundCount;             // đếm dở "+N" của round đang chơi
    int _leftRoundFruitIndex = -1, _rightRoundFruitIndex = -1; // loại quả round ĐANG chơi
    int _leftLastFruitIndex = -1, _rightLastFruitIndex = -1;   // loại quả round TRƯỚC (né lặp)

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
        if (buttonDisplay != null) buttonDisplay.onAnswerTapped = OnFruitTapped;
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => buttonDisplay;

    /// <summary>KHÔNG dùng team-aware được (base class không truyền team vào đây) — trả về
    /// placeholder khác null để IndependentPlayerLoop không break. Việc CHỌN loại quả/số lượng
    /// thật sự nằm ở SetupIndependentDisplay() (nhận được team), xem ở đó.</summary>
    protected override QuestionData PullNextQuestion()
    {
        return new QuestionData { answers = Array.Empty<string>(), correctAnswers = Array.Empty<int>() };
    }

    /// <summary>Bỏ qua QuestionData rỗng từ PullNextQuestion() — tự chọn loại quả THEO ĐÚNG BÊN
    /// (né lặp round trước, bỏ qua loại đã đủ chỉ tiêu), tự dựng QuestionData thật rồi mới giao
    /// cho ButtonDisplay như mọi game Independent khác.</summary>
    protected override void SetupIndependentDisplay(Team team, QuestionData unused, Action<bool, Team, int[]> onDone)
    {
        if (IsSideComplete(team))
        {
            ShowCompleteState(team);
            return; // KHÔNG gọi onDone — loop của IndependentPlayerLoop parks ở WaitUntil, an toàn
                     // (chỉ thoát thật khi _independentRunning=false, xem MiniGameControllerBase).
        }

        int fruitIndex = PickFruitIndex(team);
        SetRoundFruitIndex(team, fruitIndex);
        ResetRoundCounter(team);

        var fruit = Fruits[fruitIndex];
        int remaining = targetPerFruit - GetTotal(team, fruitIndex);
        int fruitCount = Mathf.Clamp(UnityEngine.Random.Range(fruit.minFruit, fruit.maxFruit + 1), 1, remaining);

        var answers = new string[fruitCount];
        var correct = new int[fruitCount];
        for (int i = 0; i < fruitCount; i++)
        {
            answers[i] = fruit.spritePath;
            correct[i] = i; // MỌI quả đều đúng — không có đáp án sai
        }

        var q = new QuestionData
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

        ApplySceneBackground(team, fruit);
        RevealBasketRow(team);
        buttonDisplay.SetupPlayerIndependent(team, q, onDone);
        RandomizeFruitPositions(team, fruitCount);
    }

    /// <summary>5 rổ + 5 text "+N" của bên này ẩn sẵn từ lúc dựng scene (tránh hiện "0/20" trên
    /// nền/rổ chưa đúng loại quả trước khi round 1 chạy xong, xem HaiQuaGameSceneBuilder) — bật lại
    /// đúng lúc bên này có round đầu tiên. Gọi lại mỗi round vô hại (SetActive(true) trên object đã
    /// active là no-op).</summary>
    void RevealBasketRow(Team team)
    {
        var icons = team == Team.Left ? leftBasketIcons : rightBasketIcons;
        var roundTexts = team == Team.Left ? leftBasketRoundTexts : rightBasketRoundTexts;
        if (icons != null) foreach (var icon in icons) if (icon != null) icon.gameObject.SetActive(true);
        if (roundTexts != null) foreach (var t in roundTexts) if (t != null) t.gameObject.SetActive(true);
    }

    /// <summary>Danh sách loại quả CHƯA đủ chỉ tiêu, loại bỏ loại của round TRƯỚC (né lặp liên
    /// tiếp) TRỪ KHI đó là lựa chọn duy nhất còn lại (vd chỉ còn Dâu tây chưa đủ 20).</summary>
    int PickFruitIndex(Team team)
    {
        var totals = team == Team.Left ? _leftTotals : _rightTotals;
        int lastIndex = team == Team.Left ? _leftLastFruitIndex : _rightLastFruitIndex;

        var candidates = new List<int>();
        for (int i = 0; i < Fruits.Length; i++)
            if (totals[i] < targetPerFruit) candidates.Add(i);

        if (candidates.Count > 1) candidates.Remove(lastIndex);

        int picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (team == Team.Left) _leftLastFruitIndex = picked; else _rightLastFruitIndex = picked;
        return picked;
    }

    bool IsSideComplete(Team team)
    {
        var totals = team == Team.Left ? _leftTotals : _rightTotals;
        foreach (int t in totals) if (t < targetPerFruit) return false;
        return true;
    }

    void ShowCompleteState(Team team)
    {
        var text = team == Team.Left ? leftCompleteText : rightCompleteText;
        if (text == null) return;
        text.gameObject.SetActive(true);
        text.text = "Hoàn thành!";
    }

    void SetRoundFruitIndex(Team team, int idx)
    {
        if (team == Team.Left) _leftRoundFruitIndex = idx; else _rightRoundFruitIndex = idx;
    }

    void ResetRoundCounter(Team team)
    {
        if (team == Team.Left) _leftRoundCount = 0; else _rightRoundCount = 0;
    }

    int GetTotal(Team team, int fruitIndex) => (team == Team.Left ? _leftTotals : _rightTotals)[fruitIndex];

    /// <summary>Xáo vị trí các quả ĐANG active — ButtonDisplay luôn dồn quả active về đầu mảng
    /// slot (0..activeCount-1, xem ButtonDisplay.SetupGroup), nên chỉ cần random đúng activeCount
    /// slot đầu. Chia khu vườn của bên đó thành lưới ẩn 3x2 (GardenGrid), xáo thứ tự ô rồi gán
    /// lần lượt cho từng quả, mỗi quả jitter ngẫu nhiên trong ô của mình (tránh chồng lên nhau).
    /// Giữ nguyên kích thước quả đã đặt lúc dựng scene, chỉ đổi tâm vị trí.</summary>
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

            // Jitter trong 70% giữa ô (chừa lề 15% mỗi cạnh) để quả không dính mép ô kề.
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

    void ApplySceneBackground(Team team, FruitDef fruit)
    {
        var bg = team == Team.Left ? leftBackground : rightBackground;
        if (bg != null) bg.color = fruit.color;
    }

    /// <summary>Bắn cho MỌI lần chạm đúng — chạy TRƯỚC khi ButtonDisplay ẩn quả vừa chạm, nên còn
    /// kịp lấy vị trí quả để bay lên rổ. CorrectFinal (quả cuối của round): (1) tự ẩn quả vừa chạm
    /// NGAY — ButtonDisplay chỉ tự ẩn quả ở CorrectPartial, còn CorrectFinal thì gọi ApplyFinalState()
    /// tô màu ItemState.Correct (xanh lá) lên quả đó rồi mới ẩn; ẩn trước ở đây để né hẳn màu xanh
    /// đó, không cần cho game "hái quả" (quả bay đi luôn, không cần feedback đúng/sai kiểu quiz);
    /// (2) cộng dồn "+N" vào tổng ở thân rổ SAU 0.5s (không cộng ngay) — xem
    /// CommitRoundToBasketDelayed(). WrongFinal không xảy ra trong thực tế vì mọi quả đều là đáp
    /// án đúng, giữ nhánh return để phòng hờ.
    ///
    /// QUAN TRỌNG: answerIndex là ButtonItem.AnswerIndex (chỉ số LOGIC trong QuestionData.answers),
    /// KHÔNG phải vị trí trong mảng leftFruitSlots/rightFruitSlots — ButtonDisplay.PickSlots() xáo
    /// (shuffle) việc gán answerIndex cho từng slot hiển thị (xem PickSlots bước 3), nên
    /// slots[answerIndex] có thể trỏ NHẦM sang 1 quả khác quả vừa bấm thật. Phải tìm đúng bằng
    /// FindTappedButton() (so khớp .AnswerIndex), không được suy từ vị trí mảng.</summary>
    void OnFruitTapped(Team team, int answerIndex, ClickResult result)
    {
        if (result != ClickResult.CorrectPartial && result != ClickResult.CorrectFinal) return;

        int fruitIndex = team == Team.Left ? _leftRoundFruitIndex : _rightRoundFruitIndex;
        var group = team == Team.Left ? leftFruitSlots : rightFruitSlots;
        var tappedButton = FindTappedButton(group, answerIndex);
        var basketRect = GetBasketRect(team, fruitIndex);

        if (tappedButton != null && basketRect != null)
            StartCoroutine(FlyToBasket(tappedButton.transform.position, basketRect, tappedButton.CurrentSprite));

        if (result == ClickResult.CorrectFinal && tappedButton != null)
            tappedButton.gameObject.SetActive(false);

        IncrementRoundCounter(team, fruitIndex);
        PunchBasket(basketRect);

        if (result == ClickResult.CorrectFinal)
            StartCoroutine(CommitRoundToBasketDelayed(team, fruitIndex));
    }

    /// <summary>Chỉ so khớp button ĐANG active — ButtonDisplay.SetupGroup() chỉ gọi .Setup() (set
    /// .AnswerIndex) cho slot active round NÀY, slot inactive còn lại giữ NGUYÊN .AnswerIndex CŨ
    /// từ round trước. Không lọc activeSelf thì có thể khớp NHẦM 1 slot ẩn còn sót giá trị trùng
    /// answerIndex, khiến hiệu ứng bay lấy vị trí SAI (đứng yên từ round trước) — đây chính là
    /// nguyên nhân hiệu ứng bay "thỉnh thoảng lệch vị trí".</summary>
    static ButtonItem FindTappedButton(ButtonItem[] group, int answerIndex)
    {
        if (group == null) return null;
        foreach (var b in group)
            if (b != null && b.gameObject.activeSelf && b.AnswerIndex == answerIndex) return b;
        return null;
    }

    /// <summary>Chờ 0.5s sau khi hái hết quả trong round rồi mới cộng "+N" vào tổng ở thân rổ —
    /// cho trẻ kịp nhìn số "+N" cuối cùng trước khi nó biến mất/gộp vào tổng. Bắt team/fruitIndex
    /// làm tham số coroutine (KHÔNG đọc lại field lúc coroutine chạy xong) để round MỚI (nếu lỡ
    /// bắt đầu trước 0.5s trôi qua) không làm sai loại quả đang chờ cộng dồn.</summary>
    IEnumerator CommitRoundToBasketDelayed(Team team, int fruitIndex)
    {
        yield return new WaitForSeconds(0.5f);
        CommitRoundToBasket(team, fruitIndex);
    }

    /// <summary>Tăng "+N" hiện Ở TRÊN rổ (ô text riêng, KHÁC tổng ở thân rổ) — mỗi cú chạm trong
    /// round +1, chưa cộng vào tổng cho tới khi round kết thúc (xem CommitRoundToBasket).</summary>
    void IncrementRoundCounter(Team team, int fruitIndex)
    {
        if (team == Team.Left) _leftRoundCount++; else _rightRoundCount++;

        var texts = team == Team.Left ? leftBasketRoundTexts : rightBasketRoundTexts;
        int count = team == Team.Left ? _leftRoundCount : _rightRoundCount;
        if (texts != null && fruitIndex >= 0 && fruitIndex < texts.Length && texts[fruitIndex] != null)
            texts[fruitIndex].text = "+" + count;
    }

    /// <summary>Hái HẾT quả trong round (CorrectFinal) — cộng dồn "+N" đang đếm dở vào tổng ở
    /// thân rổ (vd "5/20" → "8/20"), rồi xoá "+N" về rỗng cho tới round sau của ĐÚNG loại này.</summary>
    void CommitRoundToBasket(Team team, int fruitIndex)
    {
        int roundCount = team == Team.Left ? _leftRoundCount : _rightRoundCount;
        var totals = team == Team.Left ? _leftTotals : _rightTotals;
        totals[fruitIndex] += roundCount;
        if (team == Team.Left) _leftRoundCount = 0; else _rightRoundCount = 0;

        var totalTexts = team == Team.Left ? leftBasketTotalTexts : rightBasketTotalTexts;
        if (totalTexts != null && fruitIndex >= 0 && fruitIndex < totalTexts.Length && totalTexts[fruitIndex] != null)
            totalTexts[fruitIndex].text = totals[fruitIndex] + "/" + targetPerFruit;

        var roundTexts = team == Team.Left ? leftBasketRoundTexts : rightBasketRoundTexts;
        if (roundTexts != null && fruitIndex >= 0 && fruitIndex < roundTexts.Length && roundTexts[fruitIndex] != null)
            roundTexts[fruitIndex].text = "";
    }

    RectTransform GetBasketRect(Team team, int fruitIndex)
    {
        var icons = team == Team.Left ? leftBasketIcons : rightBasketIcons;
        return (icons != null && fruitIndex >= 0 && fruitIndex < icons.Length && icons[fruitIndex] != null)
            ? icons[fruitIndex].rectTransform : null;
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
        rt.sizeDelta = new Vector2(48f, 48f);
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
            pos.y += Mathf.Sin(p * Mathf.PI) * 60f; // vòng cung nhẹ, không bay thẳng đường
            rt.position = pos;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, p);
            yield return null;
        }
        Destroy(go);
    }

    // ── Hiệu ứng: rổ nảy nhẹ mỗi lần đếm tăng ───────────────────────────────────
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
