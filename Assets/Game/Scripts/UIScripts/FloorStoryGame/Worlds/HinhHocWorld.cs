using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Xây nhà hình học" — nhận biết hình tròn / vuông / tam giác / chữ nhật. Ngôi nhà hiện dưới dạng các
/// BÓNG ĐEN; mỗi lượt bé chọn hình đúng → hình bay vào lấp đầy bóng, nhà được xây dần. Xong nhà thì đèn
/// cửa sổ sáng + pháo giấy rồi xây nhà mới (đổi màu).
/// Lượt kiểu 0 "ghép bóng": bóng cần lấp nhấp nháy, bé chọn hình vừa khít. Lượt kiểu 1 "gọi tên": chỉ nghe/đọc
/// tên hình ("Tìm hình tam giác"), hình đúng tự bay vào chỗ còn trống của loại đó.
/// Không cần ảnh — hình vẽ bằng code (ShapeSprites).
/// </summary>
public sealed class HinhHocWorld : StoryWorld
{
    enum Kind { Tron, Vuong, TamGiac, ChuNhat }
    static readonly string[] KindName = { "Hình tròn", "Hình vuông", "Hình tam giác", "Hình chữ nhật" };
    static readonly string[] KindVoice = { "hinh_tron", "hinh_vuong", "hinh_tamgiac", "hinh_chunhat" };

    sealed class Part
    {
        public string name;
        public Kind kind;
        public Vector2 center;     // phân số của world
        public Vector2 sizeU;      // (rộng, cao) theo đơn vị U
        public string colorHex;
        public bool lightsOn;      // cửa sổ: đổi màu khi nhà xong
        public Image sil, fill;
        public bool filled;
        public Color color;
    }

    readonly List<Part> _parts = new List<Part>();
    readonly List<StoryCard> _cards = new List<StoryCard>();
    Text _prompt;
    bool _locked;
    int _taskCount;
    Part _target;

    static readonly string[] RoofColors = { "#E0453F", "#8E5BD8", "#2F9E8F", "#E8872E" };
    static readonly string[] WallColors = { "#F2C57C", "#F4A6C0", "#9AD5A0", "#9FC9F2" };

    protected override void Build()
    {
        StoryUI.Fill(root, "Sky", StoryUI.Hex("#CFEFFF"));
        StoryUI.Pic(root, "Ground", ShapeSprites.Square, StoryUI.Hex("#7ACB5B"), P(0.5f, 0.12f), new Vector2(W, H * 0.27f));
        var tree = StoryUI.Load("NatureKit/Isometric/tree_oak_NE");
        if (tree != null) StoryUI.Pic(root, "Tree", tree, Color.white, P(0.90f, 0.43f), Vector2.one * U * 0.32f);

        Add("ống khói",   Kind.ChuNhat, 0.66f, 0.76f, 0.07f, 0.13f, "#8D7B6A");
        Add("thân nhà",   Kind.ChuNhat, 0.50f, 0.50f, 0.52f, 0.28f, "#F2C57C");
        Add("mái nhà",    Kind.TamGiac, 0.50f, 0.73f, 0.62f, 0.22f, "#E0453F");
        Add("cửa sổ tròn",Kind.Tron,    0.50f, 0.72f, 0.10f, 0.10f, "#BDE6FF");
        Add("cửa ra vào", Kind.ChuNhat, 0.50f, 0.425f,0.11f, 0.19f, "#7A4A2A");
        Add("cửa sổ trái",Kind.Vuong,   0.33f, 0.52f, 0.11f, 0.11f, "#8ED0FF", lights: true);
        Add("cửa sổ phải",Kind.Vuong,   0.67f, 0.52f, 0.11f, 0.11f, "#8ED0FF", lights: true);
        Add("mặt trời",   Kind.Tron,    0.14f, 0.84f, 0.14f, 0.14f, "#FFD93D");
        Add("quả bóng",   Kind.Tron,    0.18f, 0.33f, 0.09f, 0.09f, "#FF6B6B");

        // Bóng đen trước, hình tô màu (ẩn) đè lên đúng chỗ.
        foreach (var p in _parts)
        {
            var spr = ShapeSprites.ByName(SpriteKey(p.kind));
            var size = new Vector2(p.sizeU.x * U, p.sizeU.y * U);
            p.sil = StoryUI.Pic(root, "Sil_" + p.name, spr, new Color(0.12f, 0.15f, 0.25f, 0.42f), P(p.center.x, p.center.y), size);
            p.sil.preserveAspect = false;
            p.fill = StoryUI.Pic(root, "Fill_" + p.name, spr, p.color, P(p.center.x, p.center.y), size);
            p.fill.preserveAspect = false;
            p.fill.gameObject.SetActive(false);
        }

        _prompt = StoryUI.Label(root, "", 38, Color.white, P(0.5f, 0.93f), new Vector2(W * 0.95f, H * 0.11f));
    }

    void Add(string name, Kind kind, float fx, float fy, float wU, float hU, string hex, bool lights = false)
    {
        _parts.Add(new Part { name = name, kind = kind, center = new Vector2(fx, fy), sizeU = new Vector2(wU, hU), colorHex = hex, color = StoryUI.Hex(hex), lightsOn = lights });
    }

    static string SpriteKey(Kind k) => k == Kind.Tron ? "circle" : k == Kind.TamGiac ? "triangle" : "square";

    // ── Lượt chơi ─────────────────────────────────────────────────────────────

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearCards();
        _locked = false;
        if (_parts.TrueForAll(p => p.filled)) NewHouse();

        var open = _parts.FindAll(p => !p.filled);
        _target = open[Rand(0, open.Count)];
        int mode = _taskCount < 3 ? 0 : Rand(0, 2);
        _taskCount++;

        // 3 loại hình: đúng + 2 loại khác.
        var kinds = new List<Kind> { _target.kind };
        var others = new List<Kind>();
        foreach (Kind k in Enum.GetValues(typeof(Kind))) if (k != _target.kind) others.Add(k);
        var oa = others.ToArray(); Shuffle(oa);
        kinds.Add(oa[0]); kinds.Add(oa[1]);
        var order = kinds.ToArray(); Shuffle(order);

        var labels = new string[order.Length];
        int correctPos = 0;
        for (int i = 0; i < order.Length; i++) { labels[i] = KindName[(int)order[i]]; if (order[i] == _target.kind) correctPos = i; }
        string kindName = KindName[(int)_target.kind];
        Describe(info, $"HINH_{_target.kind}_{mode}", "HinhHoc_NhanBietHinh",
                 mode == 0 ? $"Ghép hình vào {_target.name} ({kindName.ToLower()})" : $"Tìm {kindName.ToLower()}", labels, correctPos);

        _prompt.text = mode == 0 ? "Hình nào vừa khít?" : "Tìm " + kindName.ToLower();
        ctx.Voice(mode == 0 ? "hinh_vuakhit" : KindVoice[(int)_target.kind]);
        var target = _target;
        if (mode == 0) Run(StoryUI.Pulse(target.sil.rectTransform, () => !_locked && _target == target, 0.10f, 5f));

        float cardW = Mathf.Min(W * 0.28f, H * 0.28f);
        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        for (int i = 0; i < order.Length; i++)
        {
            int idx = i;
            var kind = order[i];
            float x = (i - (order.Length - 1) * 0.5f) * (cardW + W * 0.03f);
            var pos = new Vector2(x, P(0.5f, 0.15f).y);
            StoryCard card = StoryUI.Card(root, "Shape" + i, ShapeSprites.ByName(SpriteKey(kind)), KindName[(int)kind],
                                          StoryUI.Hex("#FFF4D6"), pos, new Vector2(cardW, cardW * 1.05f), null);
            StyleShapeIcon(card, kind, cardW);
            card.label.color = StoryUI.Hex("#3A2A12");
            var o = card.label.GetComponent<Outline>(); if (o != null) o.enabled = false;
            StoryUI.OnTap(card.bg.gameObject, () =>
            {
                if (_locked) return;
                if (idx == correctPos)
                {
                    _locked = true; hint = false;
                    ctx.SfxRight();
                    Run(Place(card, target, mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(StoryUI.Shake(card.rt));
                    Say("Chưa vừa! Thử hình khác nhé", P(0.5f, 0.84f), StoryUI.Hex("#FFE27A"), 1.4f);
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_cards[correctPos].rt, () => hint && !_locked)); }
                }
            });
            _cards.Add(card);
        }
    }

    void StyleShapeIcon(StoryCard card, Kind kind, float cardW)
    {
        card.icon.color = ColorOfKind(kind);
        card.icon.preserveAspect = false;
        float s = cardW * 0.58f;
        var size = kind == Kind.ChuNhat ? new Vector2(s * 1.25f, s * 0.75f)
                 : kind == Kind.TamGiac ? new Vector2(s * 1.1f, s * 0.95f)
                 : new Vector2(s, s);
        card.icon.rectTransform.sizeDelta = size;
    }

    static Color ColorOfKind(Kind k)
    {
        switch (k)
        {
            case Kind.Tron:    return StoryUI.Hex("#FF8A3D");
            case Kind.Vuong:   return StoryUI.Hex("#4D96FF");
            case Kind.TamGiac: return StoryUI.Hex("#E0453F");
            default:           return StoryUI.Hex("#2FB26B");
        }
    }

    IEnumerator Place(StoryCard card, Part target, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        // Hình bay từ thẻ vào bóng đen.
        var fly = StoryUI.Pic(root, "Fly", card.icon.sprite, card.icon.color, card.basePos, card.icon.rectTransform.sizeDelta);
        fly.preserveAspect = false;
        fly.transform.SetAsLastSibling();
        card.icon.enabled = false;
        var toSize = new Vector2(target.sizeU.x * U, target.sizeU.y * U);
        yield return StoryUI.MoveResize(fly.rectTransform, P(target.center.x, target.center.y), toSize, 0.5f);
        target.filled = true;
        target.fill.gameObject.SetActive(true);
        target.fill.color = target.color;
        Run(StoryUI.PopIn(target.fill.rectTransform, 0.3f));
        UnityEngine.Object.Destroy(fly.gameObject);
        ctx.Sfx("wood");
        Say($"Đây là {KindName[(int)target.kind].ToLower()}!", P(0.5f, 0.84f), Color.white, 1.3f);
        foreach (var c in _cards) if (c != card) Run(StoryUI.Fade(c.bg, 0f, 0.3f));
        Run(StoryUI.Fade(card.bg, 0f, 0.3f));

        if (_parts.TrueForAll(p => p.filled))
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var p in _parts) if (p.lightsOn) Run(StoryUI.ColorTo(p.fill, StoryUI.Hex("#FFF06B"), 0.5f));
            Confetti(P(0.5f, 0.55f), 20);
            Say("Nhà xong rồi! Giỏi quá!", P(0.5f, 0.84f), StoryUI.Hex("#FFE27A"), 2f);
            yield return new WaitForSeconds(1.8f);
        }
        else yield return new WaitForSeconds(0.3f);
        done(firstTry, answer);
    }

    void NewHouse()
    {
        string roof = RoofColors[Rand(0, RoofColors.Length)];
        string wall = WallColors[Rand(0, WallColors.Length)];
        foreach (var p in _parts)
        {
            p.filled = false;
            p.fill.gameObject.SetActive(false);
            if (p.name == "mái nhà") p.color = StoryUI.Hex(roof);
            else if (p.name == "thân nhà") p.color = StoryUI.Hex(wall);
            else p.color = StoryUI.Hex(p.colorHex);
            p.fill.color = p.color;
            p.sil.rectTransform.localScale = Vector3.one;
        }
    }

    void ClearCards()
    {
        foreach (var c in _cards) if (c != null && c.rt != null) UnityEngine.Object.Destroy(c.rt.gameObject);
        _cards.Clear();
    }
}
