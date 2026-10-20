using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Màn HƯỚNG DẪN mô phỏng cho game GenericGame chế độ SumToTarget ("tên lửa" — vd Tách số / TachSo2), chạy
/// TRƯỚC "Start in 3,2,1". Không cần video: dựng lại 1 round thật bằng CHÍNH tài nguyên của game (nền, thanh
/// nhiên liệu, tên lửa, câu hỏi, thẻ số — dữ liệu do GenericGameController.BuildSumTutorialData chuẩn bị) rồi
/// cho 1 BÀN CHÂN (Resources/ui/common_ui/icon_foot; thiếu ảnh thì dùng bàn tay icon_hand) làm mẫu — trò chơi chiếu sàn nên
/// bé chạm bằng chân. Bàn chân ĐỨNG Ở MÉP DƯỚI (chỗ bé đứng), mỗi lần chọn thì NHẢY lên ô rồi nhảy về:
///   1. "Tên lửa cần N ô nhiên liệu" (ô mục tiêu nhấp nháy)  →  2. nhảy lên thẻ số thứ nhất, thẻ bay vào thanh, ô sáng lên
///   →  (lượt cuối) nhảy NHẦM lên thẻ làm tổng vượt N: thẻ rung đỏ → nhảy lên thanh nhiên liệu → thanh trống, thẻ hiện lại, chọn lại
///   →  3. nhảy đủ các thẻ cho tổng = N  →  4. tên lửa bay lên.
/// Mỗi bước có LỜI ĐỌC (bé lớp 1 chưa đọc kịp chữ): ghép các clip ở Resources/Audio/TutorialTenLua/ (xem VoiceScript) với
/// số so_{n} cùng thư mục (thiếu thì dùng Resources/Audio/Numbers/{n}). Thiếu clip nào thì chỉ hiện chữ và chờ 1 lúc thay cho clip đó.
/// Chạy cùng lúc ở 2 nửa màn hình (nửa phải là ảnh gương, đúng quy ước MirrorRect của Kit). `loops` <= 0: LẶP MÃI
/// tới khi cô bấm "VÀO CHƠI" trên máy điều khiển (ControlActivity → GameControlBridge.OnStartPlayRequested → RequestStart;
/// trong Editor: Space/Enter); `loops` > 0: lặp đủ số lần rồi tự vào game. Tự huỷ toàn bộ UI khi xong.
/// </summary>
public class GenericSumTutorial : MonoBehaviour
{
    // ── Dữ liệu đầu vào (đã resolve sprite/toạ độ sẵn — tutorial KHÔNG tự đọc game.json) ──────────────

    public sealed class Card
    {
        public Sprite sprite;
        public string text;
        public int value;
        public RectPct rect;
    }

    public sealed class Deco
    {
        public Sprite sprite;
        public RectPct rect;
        public bool mirrorImage;
        public bool isRocket;
    }

    public sealed class Data
    {
        public Sprite background;
        public Sprite questionFrame;       // khung 9-slice của câu hỏi (layout.qFrame), null = nền tối mặc định
        public string questionText;
        public RectPct questionRect;
        public bool mirrorQuestion = true;
        public Color questionColor = Color.white;
        public int questionFontSize;       // 0 = tự co vừa ô
        public readonly List<Deco> decos = new List<Deco>();
        public readonly List<Card> cards = new List<Card>();
        public bool mirrorAnswers = true;
        public RectPct[] fuelSlots;        // slots[0] = ô "1" (ô được lấp đầu tiên)
        public bool mirrorFuel = true;
        public int target;
        public int[] taps;                 // chỉ số thẻ (trong cards) bàn tay sẽ chạm, theo thứ tự
        public Sprite pointer;             // bàn chân (hoặc bàn tay nếu thiếu ảnh bàn chân)
        public bool pointerIsFoot = true;
    }

    // ── Dựng + chạy ──────────────────────────────────────────────────────────

    const int SortingOrder = 500; // trên mọi layer của game (layerOrder ×10) và HUD

    /// <summary>Dựng overlay phủ 2 nửa màn hình (cùng anchor với leftHalf/rightHalf), chạy mô phỏng, gọi `onDone`
    /// đúng 1 lần khi xong (hết `loops` lượt).</summary>
    public static GenericSumTutorial Play(RectTransform leftHalf, RectTransform rightHalf, Data data, int loops, Action onDone)
    {
        var parent = leftHalf.parent;
        var go = new GameObject("GenericSumTutorial", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        go.AddComponent<GraphicRaycaster>();

        var t = go.AddComponent<GenericSumTutorial>();
        t._data = data;
        t._loops = loops; // <= 0: lặp mãi tới khi cô bấm "VÀO CHƠI" trên máy điều khiển (RequestStart)
        Current = t;
        GameControlBridge.PushTutorialRunning(true);
        t._onDone = onDone;
        // Màn chơi thật phía dưới: phủ tối (chặn chạm) + bàn chân gợi ý chỗ dẫm trên thẻ thật.
        var dim = MakeImage(rt, "Dim", null, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // KHUNG hướng dẫn ở giữa trên: bản thu nhỏ CẢ màn hình (2 nửa) — cùng tỉ lệ màn hình (78% × 78%).
        var border = MakeImage(rt, "FrameBorder", null, new Color(1f, 0.78f, 0.24f, 1f));
        SetAnchors(border.rectTransform, FrameMin - new Vector2(0.008f, 0.014f), FrameMax + new Vector2(0.008f, 0.014f));
        var shadow = border.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(6f, -8f);
        var miniGo = new GameObject("MiniScreen", typeof(RectTransform), typeof(RectMask2D));
        var mini = (RectTransform)miniGo.transform;
        mini.SetParent(rt, false);
        SetAnchors(mini, FrameMin, FrameMax);

        t._halves.Add(t.BuildHalf(mini, leftHalf, false));
        t._halves.Add(t.BuildHalf(mini, rightHalf, true));
        // Lớp bàn chân: trùng khung nhưng KHÔNG cắt (mask) → bàn chân đứng ngoài khung (dưới đáy) rồi nhảy vào đáp án trong khung.
        var hintLayer = (RectTransform)new GameObject("HintLayer", typeof(RectTransform)).transform;
        hintLayer.SetParent(rt, false);
        SetAnchors(hintLayer, FrameMin, FrameMax);
        t._halves[0].hint = t.BuildHint(hintLayer, t._halves[0].root, false);
        t._halves[1].hint = t.BuildHint(hintLayer, t._halves[1].root, true);
        t.BuildStartButton(rt);
        t._voice = go.AddComponent<AudioSource>();
        t._voice.playOnAwake = false;
        t._voice.volume = GameSettings.Instance != null ? GameSettings.Instance.SfxVolume : 1f;
        MusicManager.Instance?.SetMusicVolumeMultiplier(0.35f); // nhạc nền nhỏ lại cho nghe rõ lời
        t._routine = t.StartCoroutine(t.Run());
        return t;
    }

    sealed class Half
    {
        public RectTransform root;
        public bool mirror;
        public RectTransform[] cards;
        public CanvasGroup[] cardGroups;
        public RectTransform cells;
        public RectTransform rocket;
        public RectPct rocketRect;
        public RectTransform targetMarker;
        public RectTransform hand;
        public RectTransform hint;   // bàn chân gợi ý trên MÀN CHƠI THẬT (theo ô bàn chân mẫu đang dẫm)
        public Coroutine hintMove;
        public Text caption;
        public GameObject captionBox;
        public readonly Dictionary<Image, Color> tints = new Dictionary<Image, Color>(); // thẻ đang tô đỏ → màu gốc
    }

    /// <summary>Màn hướng dẫn đang chạy (null = không có).</summary>
    public static GenericSumTutorial Current { get; private set; }

    /// <summary>Cô bấm "VÀO CHƠI" (GameControlBridge.OnStartPlayRequested): dừng hướng dẫn, vào game.</summary>
    public static void RequestStart()
    {
        if (Current != null) Current.Finish();
    }

    // Khung hướng dẫn (anchor theo cả màn hình) — rộng = cao = 78% để giữ đúng tỉ lệ màn hình; chừa dải dưới cho bàn chân.
    static readonly Vector2 FrameMin = new Vector2(0.11f, 0.17f), FrameMax = new Vector2(0.89f, 0.95f);
    // Nút BẮT ĐẦU nhỏ ở góc dưới bên phải (trên sàn — cô/bé dẫm vào).
    static readonly Vector2 StartMin = new Vector2(0.895f, 0.03f), StartMax = new Vector2(0.985f, 0.14f);
    // Chỗ bàn chân ĐỨNG chờ: ngoài khung, sát mép dưới màn hình (toạ độ % nửa trái CỦA KHUNG, >100 = dưới khung; nửa phải tự lật).
    static readonly Vector2 HintHome = new Vector2(50f, 111f);

    RectTransform _startBtn;

    Data _data;
    int _loops;
    Action _onDone;
    Coroutine _routine;
    bool _finished;
    Font _font;
    AudioSource _voice;
    bool _voicePaused;
    readonly List<Half> _halves = new List<Half>();

    Font UiFont => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    Half BuildHalf(RectTransform overlay, RectTransform source, bool mirror)
    {
        var h = new Half { mirror = mirror };
        var go = new GameObject(mirror ? "RightHalf" : "LeftHalf", typeof(RectTransform), typeof(RectMask2D));
        h.root = (RectTransform)go.transform;
        h.root.SetParent(overlay, false);
        h.root.anchorMin = source.anchorMin; h.root.anchorMax = source.anchorMax;
        float scale = FrameMax.x - FrameMin.x; // khung thu nhỏ → offset (pixel) thu nhỏ theo
        h.root.offsetMin = source.offsetMin * scale; h.root.offsetMax = source.offsetMax * scale;

        // Nền (chặn chạm xuống game bên dưới). Nửa phải lật ngang giống GenericGameController.BuildBackground.
        var bg = MakeImage(h.root, "Background", _data.background, new Color(0.05f, 0.08f, 0.18f, 1f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;
        bg.preserveAspect = false;
        if (mirror && _data.background != null) bg.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        // Câu hỏi "Cần N".
        var q = new GameObject("Question", typeof(RectTransform));
        var qrt = (RectTransform)q.transform;
        qrt.SetParent(h.root, false);
        Place(qrt, _data.questionRect, mirror && _data.mirrorQuestion);
        var frame = MakeImage(qrt, "Frame", _data.questionFrame, new Color(0.09f, 0.16f, 0.32f, 0.85f));
        Stretch(frame.rectTransform);
        if (_data.questionFrame != null && _data.questionFrame.border != Vector4.zero) frame.type = Image.Type.Sliced;
        var qText = MakeText(qrt, "Text", _data.questionText, _data.questionColor);
        if (_data.questionFontSize > 0) { qText.resizeTextForBestFit = false; qText.fontSize = _data.questionFontSize; }

        // Item trang trí (thanh nhiên liệu, tên lửa...). Ảnh lật ngang ở nửa phải nếu mirrorImage.
        foreach (var d in _data.decos)
        {
            var dgo = new GameObject(d.isRocket ? "Rocket" : "Deco", typeof(RectTransform));
            var drt = (RectTransform)dgo.transform;
            drt.SetParent(h.root, false);
            Place(drt, d.rect, mirror);
            var img = MakeImage(drt, "Image", d.sprite, Color.white);
            Stretch(img.rectTransform);
            img.preserveAspect = true;
            if (mirror && d.mirrorImage) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            if (d.isRocket && h.rocket == null) { h.rocket = drt; h.rocketRect = d.rect; }
        }

        // Viền vàng quanh ô MỤC TIÊU (giống SumTargetMarker trong game).
        int k = Mathf.Clamp(_data.target, 1, _data.fuelSlots.Length) - 1;
        var marker = new GameObject("TargetMarker", typeof(RectTransform));
        h.targetMarker = (RectTransform)marker.transform;
        h.targetMarker.SetParent(h.root, false);
        Place(h.targetMarker, GenericGameController.InsetSlot(_data.fuelSlots[k]), mirror && _data.mirrorFuel);
        AddBorder(h.targetMarker, new Color(1f, 0.835f, 0.29f, 1f), 3f);

        // Thẻ số.
        h.cards = new RectTransform[_data.cards.Count];
        h.cardGroups = new CanvasGroup[_data.cards.Count];
        for (int i = 0; i < _data.cards.Count; i++)
        {
            var c = _data.cards[i];
            var crt = MakeCard(h.root, c, "Card" + i);
            Place(crt, c.rect, mirror && _data.mirrorAnswers);
            h.cards[i] = crt;
            h.cardGroups[i] = crt.gameObject.AddComponent<CanvasGroup>();
        }

        // Lớp chứa các ô nhiên liệu sáng lên.
        var cells = new GameObject("FuelCells", typeof(RectTransform));
        h.cells = (RectTransform)cells.transform;
        h.cells.SetParent(h.root, false);
        Stretch(h.cells);

        // Dòng chữ hướng dẫn: ngay trên hàng thẻ số, tránh thanh nhiên liệu.
        h.captionBox = new GameObject("Caption", typeof(RectTransform), typeof(Image));
        var crt2 = (RectTransform)h.captionBox.transform;
        crt2.SetParent(h.root, false);
        Place(crt2, CaptionRect(), mirror && _data.mirrorFuel); // CaptionRect né thanh ở hệ nửa trái → lật theo thanh (chữ không bị lật)
        var box = h.captionBox.GetComponent<Image>();
        box.color = new Color(0f, 0f, 0f, 0.7f);
        box.raycastTarget = false;
        h.caption = MakeText(crt2, "Text", "", new Color(1f, 0.835f, 0.29f, 1f));
        h.captionBox.SetActive(false); // không hiện chữ (bé lớp 1 chưa đọc kịp) — chỉ có lời đọc

        // Bàn chân/bàn tay (vẽ trên cùng) — điểm "chạm" xem PointerRectAt. Nửa phải lật ngang → thành chân kia.
        var handImg = MakeImage(h.root, _data.pointerIsFoot ? "Foot" : "Hand", _data.pointer, new Color(1f, 0.85f, 0.2f, 1f));
        handImg.preserveAspect = true;
        var shadow = handImg.gameObject.AddComponent<Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        h.hand = handImg.rectTransform;
        if (mirror) h.hand.localScale = new Vector3(-1f, 1f, 1f);
        h.hand.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        return h;
    }

    /// <summary>Bàn chân (duy nhất) của hướng dẫn: lớp cùng anchor với nửa khung `source` nhưng không bị cắt, toạ độ % như nửa khung.</summary>
    RectTransform BuildHint(RectTransform overlay, RectTransform source, bool mirror)
    {
        var go = new GameObject(mirror ? "HintRight" : "HintLeft", typeof(RectTransform));
        var area = (RectTransform)go.transform;
        area.SetParent(overlay, false);
        area.anchorMin = source.anchorMin; area.anchorMax = source.anchorMax;
        area.offsetMin = source.offsetMin; area.offsetMax = source.offsetMax;

        var glow = MakeImage(area, "Glow", null, new Color(1f, 1f, 1f, 0f)); // chỉ là khung chứa (trong suốt) để Pulse/ẩn hiện
        var foot = MakeImage(glow.rectTransform, "Foot", _data.pointer, new Color(1f, 0.85f, 0.2f, 1f));
        foot.preserveAspect = true;
        Stretch(foot.rectTransform);
        if (mirror) foot.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        glow.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        return glow.rectTransform;
    }

    void BuildStartButton(RectTransform overlay)
    {
        var go = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        _startBtn = (RectTransform)go.transform;
        _startBtn.SetParent(overlay, false);
        SetAnchors(_startBtn, StartMin, StartMax);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.72f, 0.33f, 1f);
        var o = go.AddComponent<Outline>();
        o.effectColor = Color.white;
        o.effectDistance = new Vector2(4f, -4f);
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(4f, -8f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Finish);
        var txt = MakeText(_startBtn, "Text", "BẮT ĐẦU", Color.white);
        var to = txt.gameObject.AddComponent<Outline>();
        to.effectColor = new Color(0.08f, 0.43f, 0.2f, 1f);
        to.effectDistance = new Vector2(2f, -2f);
    }

    static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    RectTransform MakeCard(RectTransform parent, Card c, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = MakeImage(rt, "Bg", c.sprite, new Color(0.93f, 0.36f, 0.5f, 1f));
        Stretch(img.rectTransform);
        img.preserveAspect = false;
        var txt = MakeText(rt, "Num", c.text, Color.white);
        var o = txt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.6f);
        o.effectDistance = new Vector2(2f, -2f);
        return rt;
    }

    /// <summary>Vùng dòng chữ: dải ngang ngay trên hàng thẻ, bên KIA thanh nhiên liệu (không che ô sáng lên).</summary>
    RectPct CaptionRect()
    {
        float cardsTop = 100f, barLeft = 100f, barRight = 0f;
        foreach (var c in _data.cards) cardsTop = Mathf.Min(cardsTop, c.rect.yPct);
        foreach (var s in _data.fuelSlots) { barLeft = Mathf.Min(barLeft, s.xPct); barRight = Mathf.Max(barRight, s.xPct + s.wPct); }
        float y = Mathf.Clamp(cardsTop - 12f, 2f, 86f);
        bool barOnLeft = barLeft + barRight < 100f;
        float x0 = barOnLeft ? barRight + 2f : 3f;
        float x1 = barOnLeft ? 97f : barLeft - 2f;
        if (x1 - x0 < 30f) { x0 = 3f; x1 = 97f; }
        return new RectPct { xPct = x0, yPct = y, wPct = x1 - x0, hPct = 10f };
    }

    // ── Kịch bản ─────────────────────────────────────────────────────────────

    /// <summary>Kịch bản NGẮN, lặp trong KHUNG ở giữa; bàn chân gợi ý trên màn chơi thật đi theo ô bàn chân mẫu dẫm:
    ///   1) "Tên lửa cần [n] ô nhiên liệu để bay, chọn đáp án có tổng bằng [n]." → dẫm đúng → tên lửa bay.
    ///   2) (không đọc lại đề) dẫm thẻ đầu + thẻ nhầm (rung đỏ): "Sai rồi, bước lên thanh nhiên liệu chọn lại nhé." → dẫm thanh.
    ///   → quay lại 1).</summary>
    IEnumerator Run()
    {
        yield return null; // chờ layout ổn định 1 frame
        int wrong = PickWrongCard();
        int n = _data.target;
        for (int loop = 0; _loops <= 0 || loop < _loops; loop++)
        {
            // ── 1) Đọc đề → chọn đúng → tên lửa bay.
            ResetScene();
            yield return Each(ShowFootAtHome);
            StartCoroutine(Each(h => Pulse(h.targetMarker, 1.15f, 0.5f)));
            yield return Say($"Tên lửa cần {n} ô nhiên liệu để bay, chọn đáp án có tổng bằng {n}.",
                "tl2_can|tl_ten_lua_can", Num(n), "tl2_o_chon|tl_o_nhien_lieu+tl_o_chon", Num(n));

            int sum = 0;
            for (int i = 0; i < _data.taps.Length; i++)
            {
                int idx = _data.taps[i];
                int from = sum;
                sum += _data.cards[idx].value;
                yield return TapCard(idx, from, sum - 1, null);
            }
            yield return Wait(0.45f); // chờ thẻ cuối bay vào thanh
            MusicManager.Instance?.PlayCorrectSfx();
            yield return Each(ShowHandOff);
            foreach (var h in _halves) MoveHint(h, HomeTip); // bàn chân gợi ý về chỗ đứng dưới khung
            if (_halves[0].rocket != null) yield return Each(Launch);
            yield return Wait(0.6f);

            // ── 2) Làm mẫu chọn SAI (không đọc lại đề): dẫm thẻ đầu, dẫm thẻ nhầm → "Sai rồi…" → dẫm thanh nhiên liệu.
            if (wrong < 0) continue;
            ResetScene();
            yield return Each(ShowFootAtHome);
            int a = _data.taps[0];
            yield return TapCard(a, 0, _data.cards[a].value - 1, null);
            var w = _data.cards[wrong];
            yield return Each(h => JumpTo(h, Center(w.rect), h.mirror && _data.mirrorAnswers));
            var say = StartCoroutine(Say("Sai rồi, bước lên thanh nhiên liệu chọn lại nhé.", "tl2_sai_roi|tl_chon_lai"));
            MusicManager.Instance?.PlayWrongSfx();
            yield return Each(h => Press(h, wrong));
            yield return Each(h => WrongShake(h, wrong));
            var bar = Center(Union(0, _data.fuelSlots.Length - 1));
            yield return Each(h => JumpTo(h, bar, h.mirror && _data.mirrorFuel));
            yield return Each(h => Press(h, -1));
            yield return Each(ClearBar);
            yield return say;
            yield return Wait(0.5f);
        }
        Finish();
    }

    string SumText()
    {
        var parts = new string[_data.taps.Length];
        for (int i = 0; i < parts.Length; i++) parts[i] = _data.cards[_data.taps[i]].value.ToString();
        return string.Join(" + ", parts);
    }

    IEnumerator ShowHandOff(Half h)
    {
        var cg = h.hand.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 0.2f; t += Time.deltaTime) { cg.alpha = 1f - t / 0.2f; yield return null; }
        cg.alpha = 0f;
    }

    /// <summary>Bàn chân nhảy lên thẻ `idx` (từ chỗ đang đứng) → dẫm → thẻ bay vào thanh, sáng các ô [first..last].
    /// `speech` (nếu có) đọc song song, chờ đọc xong mới sang bước sau.</summary>
    IEnumerator TapCard(int idx, int first, int last, IEnumerator speech)
    {
        var card = _data.cards[idx];
        var say = speech != null ? StartCoroutine(speech) : null;
        yield return Each(h => JumpTo(h, Center(card.rect), h.mirror && _data.mirrorAnswers));
        yield return Each(h => Press(h, idx));
        StartCoroutine(Each(h => FlyCardToBar(h, idx, first, last))); // thẻ bay, chân nhảy tiếp luôn
        if (say != null) yield return say;
    }

    // ── Lời đọc ───────────────────────────────────────────────────────────────

    const string VoicePath = "Audio/TutorialTenLua/";

    /// <summary>Danh sách clip lời đọc (tên file → câu). Số n: "so_n" trong cùng thư mục (cùng giọng), thiếu thì dùng
    /// Resources/Audio/Numbers/{n}.</summary>
    public static readonly string[,] VoiceScript =
    {
        { "tl2_can",      "Tên lửa cần" },
        { "tl2_o_chon",   "ô nhiên liệu để bay, chọn đáp án có tổng bằng" },
        { "tl2_sai_roi",  "Sai rồi, bước lên thanh nhiên liệu chọn lại nhé." },
    };

    static string Num(int n) => "#" + n;

    /// <summary>Hiện `caption` và đọc lần lượt các clip ("#n" = số n). Clip thiếu → chờ thay (số 0.6s, câu 1.4s).</summary>
    IEnumerator Say(string caption, params string[] parts)
    {
        Caption(caption);
        foreach (var p in parts)
        {
            bool isNum = p.StartsWith("#");
            var clips = new List<AudioClip>();
            if (isNum)
            {
                var c = Resources.Load<AudioClip>(VoicePath + "so_" + p.Substring(1));
                if (c == null) c = Resources.Load<AudioClip>("Audio/Numbers/" + p.Substring(1));
                if (c != null) clips.Add(c);
            }
            else foreach (var alt in p.Split('|')) // "mới|cũ": dùng clip mới, chưa có thì dùng clip cũ; "a+b" = ghép nhiều clip
            {
                clips.Clear();
                foreach (var name in alt.Split('+'))
                {
                    var c = Resources.Load<AudioClip>(VoicePath + name);
                    if (c == null) { clips.Clear(); break; }
                    clips.Add(c);
                }
                if (clips.Count > 0) break;
            }
            if (clips.Count == 0) { yield return Wait(isNum ? 0.6f : 1.4f); continue; }
            foreach (var clip in clips)
            {
                _voice.Stop();
                _voice.clip = clip;
                _voice.Play();
                yield return Wait(clip.length + 0.03f);
            }
        }
    }

    // Cô bấm Pause trên máy điều khiển → timeScale = 0: dừng luôn lời đọc (AudioSource không theo timeScale).
    void Update()
    {
#if UNITY_EDITOR
        // Trong Editor không có máy điều khiển → Space/Enter thay cho nút "VÀO CHƠI".
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) { Finish(); return; }
#endif
        if (_startBtn != null) _startBtn.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f));
        bool paused = Time.timeScale == 0f;
        if (paused == _voicePaused || _voice == null) return;
        _voicePaused = paused;
        if (paused) _voice.Pause(); else _voice.UnPause();
    }

    /// <summary>Thẻ để làm mẫu chọn SAI: chưa dùng trong `taps`, cộng với thẻ đầu tiên thì VƯỢT mục tiêu (vượt ít nhất
    /// được ưu tiên — dễ hiểu nhất). -1 = không có (bỏ qua bước chọn sai).</summary>
    int PickWrongCard()
    {
        if (_data.taps == null || _data.taps.Length < 2) return -1;
        int va = _data.cards[_data.taps[0]].value, best = -1, bestOver = int.MaxValue;
        for (int j = 0; j < _data.cards.Count; j++)
        {
            if (Array.IndexOf(_data.taps, j) >= 0) continue;
            int over = va + _data.cards[j].value - _data.target;
            if (over > 0 && over < bestOver) { bestOver = over; best = j; }
        }
        return best;
    }

    void ResetScene()
    {
        foreach (var h in _halves)
        {
            for (int i = 0; i < h.cards.Length; i++)
            {
                h.cards[i].gameObject.SetActive(true);
                h.cards[i].localScale = Vector3.one;
                h.cardGroups[i].alpha = 1f;
            }
            foreach (Transform c in h.cells) Destroy(c.gameObject);
            RestoreTints(h);
            if (h.rocket != null) Place(h.rocket, h.rocketRect, h.mirror);
            h.hand.GetComponent<CanvasGroup>().alpha = 0f;
        }
    }

    void Caption(string s)
    {
        foreach (var h in _halves) h.caption.text = s;
    }

    /// <summary>Chạy cùng 1 coroutine cho cả 2 nửa, chờ tất cả xong.</summary>
    IEnumerator Each(Func<Half, IEnumerator> f)
    {
        var running = new List<Coroutine>();
        foreach (var h in _halves) running.Add(StartCoroutine(f(h)));
        foreach (var c in running) yield return c;
    }

    static IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

    /// <summary>Chỗ bé ĐỨNG chờ: giữa mép dưới nửa màn hình (bàn chân thò 1 phần ra ngoài mép).</summary>
    static readonly Vector2 HomeTip = new Vector2(50f, 97f);

    /// <summary>Đặt bàn chân ở chỗ đứng rồi hiện dần.</summary>
    IEnumerator ShowFootAtHome(Half h)
    {
        Place(h.hand, HandRectAt(HomeTip), h.mirror);
        h.hand.localScale = new Vector3(h.mirror ? -1f : 1f, 1f, 1f);
        if (h.hint != null)
        {
            if (h.hintMove != null) StopCoroutine(h.hintMove);
            h.hintMove = null;
            Place(h.hint, HandRectAt(HintHome), h.mirror);
            h.hint.localScale = Vector3.one;
            h.hint.GetComponent<CanvasGroup>().alpha = 1f;
        }
        h.hand.GetComponent<CanvasGroup>().alpha = 0f; // bàn chân trong khung chỉ để tính đường đi, không hiện
        yield return null;
    }

    IEnumerator JumpHome(Half h) => JumpTo(h, HomeTip, h.mirror);

    /// <summary>NHẢY tới điểm `tip`: đi theo đường vòng cung lên trên, to ra giữa chừng (đang ở trên không) rồi nhỏ lại khi đáp.</summary>
    IEnumerator JumpTo(Half h, Vector2 tip, bool mirrorTarget, float dur = 0.45f)
    {
        // Điểm đích tính theo hệ toạ độ của nửa trái; nếu ĐÍCH ở nửa phải không lật (mirrorTarget=false) mà
        // bàn chân lật (h.mirror) thì phải lật ngược điểm đích trước để sau khi Place() lật lại vẫn trúng.
        if (h.mirror && !mirrorTarget) tip.x = 100f - tip.x;
        var from = CurrentRect(h.hand, h.mirror);
        var to = HandRectAt(tip);
        float sx = h.mirror ? -1f : 1f;
        MoveHint(h, tip, dur); // bàn chân gợi ý trên màn thật nhảy cùng lúc
        float arc = Mathf.Clamp(Mathf.Abs(to.yPct - from.yPct) * 0.25f + 4f, 4f, 14f);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = t / dur;
            var r = Lerp(from, to, Mathf.SmoothStep(0f, 1f, u));
            float k = Mathf.Sin(u * Mathf.PI); // 0 → 1 → 0
            r.yPct -= arc * k;
            Place(h.hand, r, h.mirror);
            float s = 1f + (_data.pointerIsFoot ? 0.3f : 0.1f) * k;
            h.hand.localScale = new Vector3(sx * s, s, 1f);
            yield return null;
        }
        Place(h.hand, to, h.mirror);
        h.hand.localScale = new Vector3(sx, 1f, 1f);
    }

    /// <summary>Bàn chân gợi ý trên màn chơi thật: đứng dưới khung (HintHome); khi chân mẫu dẫm thì NHẢY lên đúng ô đó
    /// trên màn thật; chân mẫu về chỗ đứng thì nó cũng nhảy về dưới khung. `tip` đã lật sẵn theo JumpTo.</summary>
    void MoveHint(Half h, Vector2 tip, float dur = 0.45f)
    {
        if (h.hint == null) return;
        bool home = Mathf.Abs(tip.y - HomeTip.y) < 0.01f;
        var to = HandRectAt(home ? HintHome : tip);
        if (h.hintMove != null) StopCoroutine(h.hintMove);
        h.hintMove = StartCoroutine(HintJump(h, to, dur, !home));
    }

    IEnumerator HintJump(Half h, RectPct to, float dur, bool pulse)
    {
        h.hint.GetComponent<CanvasGroup>().alpha = 1f;
        var from = CurrentRect(h.hint, h.mirror);
        float arc = Mathf.Clamp(Mathf.Abs(to.yPct - from.yPct) * 0.25f + 4f, 4f, 14f);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = t / dur;
            var r = Lerp(from, to, Mathf.SmoothStep(0f, 1f, u));
            float k = Mathf.Sin(u * Mathf.PI);
            r.yPct -= arc * k;
            Place(h.hint, r, h.mirror);
            h.hint.localScale = Vector3.one * (1f + 0.3f * k);
            yield return null;
        }
        Place(h.hint, to, h.mirror);
        h.hint.localScale = Vector3.one;
        h.hintMove = null;
        if (pulse) yield return Pulse(h.hint, 1.25f, 0.3f);
    }

    /// <summary>Hộp của bàn chân/bàn tay sao cho "điểm chạm" trùng (x,y) % của nửa trái (tự lật cho nửa phải). Hộp cùng tỉ lệ ảnh
    /// trên nửa màn hình 512×600 để ảnh phủ kín hộp. Bàn chân (icon_foot 241×382): điểm chạm = giữa lòng bàn chân phía trước
    /// (~40% từ trên). Bàn tay (icon_hand 106×118): điểm chạm = đầu ngón tay (35%, mép trên).</summary>
    RectPct HandRectAt(Vector2 tip)
    {
        if (_data.pointerIsFoot)
        {
            const float fw = 10f, fh = 13.5f;
            return new RectPct { xPct = tip.x - fw / 2f, yPct = tip.y - fh * 0.4f, wPct = fw, hPct = fh };
        }
        const float w = 11f, h = 10.5f, tipX = 0.35f;
        return new RectPct { xPct = tip.x - w * tipX, yPct = tip.y - 0.5f, wPct = w, hPct = h };
    }

    /// <summary>Dẫm/chạm (sau khi đã nhảy tới nơi): bàn chân ép xuống, thẻ lún xuống cùng lúc.
    /// `idx` = -1: không có thẻ (vd dẫm vào thanh nhiên liệu).</summary>
    IEnumerator Press(Half h, int idx)
    {
        var card = idx >= 0 ? h.cards[idx] : null;
        var hs = h.hand.localScale;
        const float dur = 0.3f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI); // 0 → 1 → 0
            float s = 1f - (_data.pointerIsFoot ? 0.08f : 0.15f) * k;
            h.hand.localScale = new Vector3(hs.x * s, hs.y * s, 1f);
            if (card != null) card.localScale = Vector3.one * (1f - 0.12f * k);
            yield return null;
        }
        if (card != null) card.localScale = Vector3.one;
        h.hand.localScale = hs;
    }

    /// <summary>Chọn SAI (tổng vượt mục tiêu): thẻ rung ngang + tô đỏ, KHÔNG bay vào thanh (giống game: WrongFinal).
    /// Màu đỏ giữ tới khi chạm thanh chọn lại (ClearBar) hoặc lượt mới.</summary>
    IEnumerator WrongShake(Half h, int idx)
    {
        var card = h.cards[idx];
        var bg = card.Find("Bg")?.GetComponent<Image>();
        if (bg != null)
        {
            if (!h.tints.ContainsKey(bg)) h.tints[bg] = bg.color;
            bg.color = new Color(1f, 0.45f, 0.45f, 1f);
        }
        const float dur = 0.45f;
        float amp = Mathf.Max(4f, card.rect.width * 0.06f);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float dx = Mathf.Sin(t * 45f) * amp * (1f - t / dur);
            card.offsetMin = new Vector2(dx, 0f);
            card.offsetMax = new Vector2(dx, 0f);
            yield return null;
        }
        card.offsetMin = Vector2.zero;
        card.offsetMax = Vector2.zero;
    }

    /// <summary>Dẫm vào thanh nhiên liệu = chọn lại (giống SumReset của game): các ô đã sáng tắt đi, thẻ đã dùng hiện lại,
    /// thẻ đỏ về màu cũ.</summary>
    IEnumerator ClearBar(Half h)
    {
        StartCoroutine(Pulse(h.cells, 1.06f, 0.25f));
        yield return Wait(0.25f);
        foreach (Transform c in h.cells) Destroy(c.gameObject);
        RestoreTints(h);
        for (int i = 0; i < h.cards.Length; i++)
        {
            if (h.cards[i].gameObject.activeSelf) continue;
            h.cards[i].gameObject.SetActive(true);
            StartCoroutine(Pulse(h.cards[i], 1.15f, 0.3f));
        }
    }

    static void RestoreTints(Half h)
    {
        foreach (var kv in h.tints) if (kv.Key != null) kv.Key.color = kv.Value;
        h.tints.Clear();
    }

    /// <summary>Thẻ (bản sao) bay tới khung bao các ô [first..last] của thanh, nhỏ dần + mờ đi; thẻ gốc ẩn; các ô sáng lên.</summary>
    IEnumerator FlyCardToBar(Half h, int idx, int first, int last)
    {
        int n = _data.fuelSlots.Length;
        first = Mathf.Clamp(first, 0, n - 1);
        last = Mathf.Clamp(last, first, n - 1);
        bool mirrorFuel = h.mirror && _data.mirrorFuel;
        bool mirrorCard = h.mirror && _data.mirrorAnswers;

        var clone = MakeCard(h.root, _data.cards[idx], "FlyingCard");
        clone.SetSiblingIndex(h.cells.GetSiblingIndex() + 1);
        var cg = clone.gameObject.AddComponent<CanvasGroup>();
        h.cards[idx].gameObject.SetActive(false);

        // Toạ độ đích tính trong hệ CỦA THẺ (lật hay không theo mirrorCard) để Lerp cùng 1 hệ.
        var from = _data.cards[idx].rect;
        var dst = Union(first, last);
        if (mirrorFuel != mirrorCard) dst = MirrorRect(dst);
        const float dur = 0.45f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            Place(clone, Lerp(from, dst, k), mirrorCard);
            cg.alpha = 1f - 0.7f * k;
            yield return null;
        }
        Destroy(clone.gameObject);
        for (int i = first; i <= last; i++) SpawnCell(h, i, mirrorFuel);
    }

    void SpawnCell(Half h, int i, bool mirror)
    {
        int n = _data.fuelSlots.Length;
        var go = new GameObject("FuelCell" + (i + 1), typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(h.cells, false);
        Place(rt, GenericGameController.InsetSlot(_data.fuelSlots[i]), mirror);
        var img = go.GetComponent<Image>();
        img.color = GenericGameController.FuelColorAt(n > 1 ? i / (float)(n - 1) : 0f);
        img.raycastTarget = false;
        var edge = go.AddComponent<Outline>();
        edge.effectColor = new Color(1f, 1f, 1f, 0.85f);
        edge.effectDistance = new Vector2(2f, -2f);
        MakeText(rt, "Num", (i + 1).ToString(), Color.white);
        StartCoroutine(Pulse(rt, 1.2f, 0.3f));
    }

    IEnumerator Launch(Half h)
    {
        if (h.rocket == null) yield break;
        var from = h.rocketRect;
        var to = from;
        to.yPct = -(from.hPct + 10f); // bay hết lên khỏi mép trên
        const float dur = 1.1f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            Place(h.rocket, Lerp(from, to, k * k), h.mirror); // ease-in giống LaunchRoutine của game
            yield return null;
        }
        Place(h.rocket, to, h.mirror);
    }

    static IEnumerator Pulse(RectTransform rt, float scale, float dur)
    {
        for (float t = 0f; rt != null && t < dur; t += Time.deltaTime)
        {
            rt.localScale = Vector3.one * Mathf.Lerp(1f, scale, Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        if (Current == this) Current = null;
        GameControlBridge.PushTutorialRunning(false);
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
        if (_routine != null) StopCoroutine(_routine);
        StopAllCoroutines();
        Destroy(gameObject);
        _onDone?.Invoke();
    }

    // ── Toạ độ % (giống GenericGameController: 0,0 = góc trên-trái của NỬA màn hình) ─────────────────

    static RectPct MirrorRect(RectPct p) => new RectPct { xPct = 100f - p.xPct - p.wPct, yPct = p.yPct, wPct = p.wPct, hPct = p.hPct };

    static void Place(RectTransform rt, RectPct p, bool mirror)
    {
        if (mirror) p = MirrorRect(p);
        rt.anchorMin = new Vector2(p.xPct / 100f, 1f - (p.yPct + p.hPct) / 100f);
        rt.anchorMax = new Vector2((p.xPct + p.wPct) / 100f, 1f - p.yPct / 100f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Đọc ngược anchor hiện tại về RectPct (hệ nửa trái nếu `mirror`).</summary>
    static RectPct CurrentRect(RectTransform rt, bool mirror)
    {
        var p = new RectPct
        {
            xPct = rt.anchorMin.x * 100f,
            yPct = (1f - rt.anchorMax.y) * 100f,
            wPct = (rt.anchorMax.x - rt.anchorMin.x) * 100f,
            hPct = (rt.anchorMax.y - rt.anchorMin.y) * 100f,
        };
        return mirror ? MirrorRect(p) : p;
    }

    static RectPct Lerp(RectPct a, RectPct b, float k) => new RectPct
    {
        xPct = Mathf.LerpUnclamped(a.xPct, b.xPct, k), yPct = Mathf.LerpUnclamped(a.yPct, b.yPct, k),
        wPct = Mathf.LerpUnclamped(a.wPct, b.wPct, k), hPct = Mathf.LerpUnclamped(a.hPct, b.hPct, k),
    };

    static Vector2 Center(RectPct p) => new Vector2(p.xPct + p.wPct / 2f, p.yPct + p.hPct / 2f);

    RectPct Union(int first, int last)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int k = first; k <= last; k++)
        {
            var r = _data.fuelSlots[k];
            minX = Mathf.Min(minX, r.xPct); minY = Mathf.Min(minY, r.yPct);
            maxX = Mathf.Max(maxX, r.xPct + r.wPct); maxY = Mathf.Max(maxY, r.yPct + r.hPct);
        }
        return new RectPct { xPct = minX, yPct = minY, wPct = maxX - minX, hPct = maxY - minY };
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color fallback)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : fallback;
        img.raycastTarget = false;
        return img;
    }

    Text MakeText(RectTransform parent, string name, string text, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.04f, 0.06f); rt.anchorMax = new Vector2(0.96f, 0.94f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.font = UiFont;
        txt.text = text;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        txt.raycastTarget = false;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 8;
        txt.resizeTextMaxSize = 200;
        return txt;
    }

    static void AddBorder(RectTransform parent, Color c, float px)
    {
        void Bar(string n, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
        }
        Bar("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, px));
        Bar("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, px));
        Bar("Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(px, 0));
        Bar("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(px, 0));
    }
}
