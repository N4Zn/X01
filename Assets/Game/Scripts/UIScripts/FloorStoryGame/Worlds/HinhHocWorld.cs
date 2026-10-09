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
        public string[] requires;  // phải xây xong các phần này trước (tường → mái → ống khói ...)
        public Image hl, fill;     // hl: nền sáng nhấp nháy khi là phần cần xây
        public RectTransform dashRoot;
        public CanvasGroup dashGroup;
        public readonly List<Image> dashes = new List<Image>();
        public bool filled;
        public Color color;
    }

    static readonly Color DashIdle = new Color(0.18f, 0.23f, 0.35f, 0.9f);
    static readonly Color DashLocked = new Color(0.18f, 0.23f, 0.35f, 0.35f);
    static readonly Color DashTarget = new Color(1f, 0.45f, 0f, 1f);

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

        // Thứ tự dựng = thứ tự vẽ (sau đè lên trước). Ống khói vẽ trước để nằm sau mái.
        // requires: tường trước → mái / cửa / cửa sổ → ống khói, cửa sổ tròn trên mái cần có mái.
        Add("ống khói",   Kind.ChuNhat, 0.66f, 0.76f, 0.07f, 0.13f, "#8D7B6A", "mái nhà");
        Add("thân nhà",   Kind.ChuNhat, 0.50f, 0.50f, 0.52f, 0.28f, "#F2C57C");
        Add("mái nhà",    Kind.TamGiac, 0.50f, 0.73f, 0.62f, 0.22f, "#E0453F", "thân nhà");
        Add("cửa sổ tròn",Kind.Tron,    0.50f, 0.72f, 0.10f, 0.10f, "#BDE6FF", "mái nhà");
        Add("cửa ra vào", Kind.ChuNhat, 0.50f, 0.425f,0.11f, 0.19f, "#7A4A2A", "thân nhà");
        Add("cửa sổ trái",Kind.Vuong,   0.33f, 0.52f, 0.11f, 0.11f, "#8ED0FF", "thân nhà", lights: true);
        Add("cửa sổ phải",Kind.Vuong,   0.67f, 0.52f, 0.11f, 0.11f, "#8ED0FF", "thân nhà", lights: true);
        Add("mặt trời",   Kind.Tron,    0.14f, 0.84f, 0.14f, 0.14f, "#FFD93D", "thân nhà");
        Add("quả bóng",   Kind.Tron,    0.18f, 0.33f, 0.09f, 0.09f, "#FF6B6B", "thân nhà");

        // Mỗi phần: nền sáng (chỉ hiện khi nhấp nháy) + khung nét đứt + hình tô màu (ẩn) đè lên đúng chỗ.
        foreach (var p in _parts)
        {
            var spr = ShapeSprites.ByName(SpriteKey(p.kind));
            var size = new Vector2(p.sizeU.x * U, p.sizeU.y * U);
            var pos = P(p.center.x, p.center.y);
            p.hl = StoryUI.Pic(root, "Hl_" + p.name, spr, new Color(1f, 1f, 1f, 0f), pos, size);
            p.hl.preserveAspect = false;
            p.dashRoot = StoryUI.Rect(root, "Dash_" + p.name, pos, size);
            p.dashGroup = p.dashRoot.gameObject.AddComponent<CanvasGroup>();
            BuildDashes(p, size);
            p.fill = StoryUI.Pic(root, "Fill_" + p.name, spr, p.color, pos, size);
            p.fill.preserveAspect = false;
            p.fill.gameObject.SetActive(false);
        }
        RefreshDashes(null);

        _prompt = StoryUI.Label(root, "", 38, Color.white, P(0.5f, 0.93f), new Vector2(W * 0.95f, H * 0.11f));
    }

    void Add(string name, Kind kind, float fx, float fy, float wU, float hU, string hex, string requires = null, bool lights = false)
    {
        _parts.Add(new Part
        {
            name = name, kind = kind, center = new Vector2(fx, fy), sizeU = new Vector2(wU, hU),
            colorHex = hex, color = StoryUI.Hex(hex), lightsOn = lights,
            requires = requires == null ? new string[0] : new[] { requires }
        });
    }

    bool Available(Part p)
    {
        foreach (var r in p.requires)
        {
            var need = _parts.Find(x => x.name == r);
            if (need != null && !need.filled) return false;
        }
        return true;
    }

    // ── Khung nét đứt ─────────────────────────────────────────────────────────

    void BuildDashes(Part p, Vector2 size)
    {
        float thick = Mathf.Max(4f, U * 0.011f);
        float period = U * 0.04f;
        float hw = size.x * 0.5f, hh = size.y * 0.5f;
        if (p.kind == Kind.Tron)
        {
            float rx = hw * 0.95f, ry = hh * 0.95f;
            float perim = Mathf.PI * (3f * (rx + ry) - Mathf.Sqrt((3f * rx + ry) * (rx + 3f * ry)));
            int n = Mathf.Max(8, Mathf.RoundToInt(perim / period));
            float len = perim / n * 0.62f;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                var tan = new Vector2(-rx * Mathf.Sin(a), ry * Mathf.Cos(a));
                AddDash(p, new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a)), Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg, len, thick);
            }
        }
        else if (p.kind == Kind.TamGiac)
        {
            // Khớp ShapeSprites.Triangle: đỉnh y=+0.9, đáy y=-0.85, nửa đáy 0.95.
            var top = new Vector2(0f, hh * 0.9f);
            var bl = new Vector2(-hw * 0.95f, -hh * 0.85f);
            var br = new Vector2(hw * 0.95f, -hh * 0.85f);
            DashSegment(p, bl, br, period, thick);
            DashSegment(p, br, top, period, thick);
            DashSegment(p, top, bl, period, thick);
        }
        else
        {
            var a = new Vector2(-hw, -hh); var b = new Vector2(hw, -hh);
            var c = new Vector2(hw, hh);   var d = new Vector2(-hw, hh);
            DashSegment(p, a, b, period, thick); DashSegment(p, b, c, period, thick);
            DashSegment(p, c, d, period, thick); DashSegment(p, d, a, period, thick);
        }
    }

    void DashSegment(Part p, Vector2 a, Vector2 b, float period, float thick)
    {
        float L = Vector2.Distance(a, b);
        int n = Mathf.Max(2, Mathf.RoundToInt(L / period));
        float len = L / n * 0.62f;
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        for (int i = 0; i < n; i++) AddDash(p, Vector2.Lerp(a, b, (i + 0.5f) / n), ang, len, thick);
    }

    void AddDash(Part p, Vector2 pos, float angle, float len, float thick)
    {
        var img = StoryUI.Pic(p.dashRoot, "d", ShapeSprites.Square, DashIdle, pos, new Vector2(len, thick));
        img.preserveAspect = false;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        p.dashes.Add(img);
    }

    /// <summary>Tô lại khung: phần cần xây = cam; phần chưa tới lượt (thiếu nền) = mờ; còn lại = xám đậm.</summary>
    void RefreshDashes(Part target)
    {
        foreach (var p in _parts)
        {
            p.dashRoot.gameObject.SetActive(!p.filled);
            if (p.filled) continue;
            p.dashGroup.alpha = 1f;
            var c = p == target ? DashTarget : Available(p) ? DashIdle : DashLocked;
            foreach (var d in p.dashes) d.color = c;
        }
    }

    IEnumerator Blink(Part p, Func<bool> alive)
    {
        float t = 0f;
        while (alive())
        {
            t += Time.deltaTime;
            float s = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            p.dashGroup.alpha = Mathf.Lerp(0.2f, 1f, s);
            p.hl.color = new Color(1f, 0.95f, 0.55f, s * 0.55f);
            yield return null;
        }
        p.dashGroup.alpha = 1f;
        p.hl.color = new Color(1f, 1f, 1f, 0f);
    }

    static string SpriteKey(Kind k) => k == Kind.Tron ? "circle" : k == Kind.TamGiac ? "triangle" : "square";

    // ── Lượt chơi ─────────────────────────────────────────────────────────────

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearCards();
        _locked = false;
        if (_parts.TrueForAll(p => p.filled)) NewHouse();

        // Chỉ chọn phần đã có nền để xây (tường đầu tiên → mái/cửa → ống khói ...).
        var open = _parts.FindAll(p => !p.filled && Available(p));
        _target = open[Rand(0, open.Count)];
        RefreshDashes(_target);
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

        _prompt.text = mode == 0 ? $"Xây {_target.name}: hình nào vừa khít?" : "Tìm " + kindName.ToLower();
        ctx.Voice(mode == 0 ? "hinh_vuakhit" : KindVoice[(int)_target.kind]);
        var target = _target;
        Run(Blink(target, () => !_locked && _target == target));

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
        target.dashRoot.gameObject.SetActive(false);
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
            p.hl.color = new Color(1f, 1f, 1f, 0f);
        }
        RefreshDashes(null);
    }

    void ClearCards()
    {
        foreach (var c in _cards) if (c != null && c.rt != null) UnityEngine.Object.Destroy(c.rt.gameObject);
        _cards.Clear();
    }
}
