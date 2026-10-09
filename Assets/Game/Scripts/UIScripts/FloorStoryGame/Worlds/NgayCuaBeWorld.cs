using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Một ngày của Bé Na" — các buổi trong ngày. Bầu trời/mặt trời/mặt trăng đổi theo buổi; mỗi lượt bé chọn việc
/// hợp với buổi hiện tại. Làm ĐÚNG thì Na làm việc đó và trời chuyển sang buổi kế (thế giới thay đổi);
/// làm SAI thì Na ngạc nhiên ("Chưa phải lúc ... đâu!") và được thử lại (gợi ý nhấp nháy sau 2 lần sai).
/// Ảnh hoạt động: Resources/Story/Day/&lt;key&gt;.png (chưa có thì dùng ảnh cảm xúc Family/* hoặc thẻ chữ).
/// </summary>
public sealed class NgayCuaBeWorld : StoryWorld
{
    sealed class Activity
    {
        public string key, label, fallbackImage;
        public int period;
        public Activity(string key, string label, int period, string fallbackImage = null)
        { this.key = key; this.label = label; this.period = period; this.fallbackImage = fallbackImage; }
    }

    static readonly string[] PeriodNames = { "SÁNG", "TRƯA", "CHIỀU", "TỐI" };
    static readonly string[] PeriodTopic = { "Sáng", "Trưa", "Chiều", "Tối" };

    static readonly Activity[] Activities =
    {
        new Activity("thuc_day",     "Thức dậy",         0, "Family/happy"),
        new Activity("danh_rang",    "Đánh răng",        0),
        new Activity("an_sang",      "Ăn sáng",          0, "Family/hungry"),
        new Activity("di_hoc",       "Đi học",           0),
        new Activity("rua_tay",      "Rửa tay",          1),
        new Activity("an_trua",      "Ăn cơm trưa",      1, "Family/hungry"),
        new Activity("ngu_trua",     "Ngủ trưa",         1, "Family/sleepy"),
        new Activity("ve_nha",       "Đi học về",        2),
        new Activity("choi_san",     "Chơi ngoài sân",   2, "Family/happy"),
        new Activity("tam",          "Tắm",              2),
        new Activity("an_toi",       "Ăn tối",           3, "Family/hungry"),
        new Activity("doc_truyen",   "Nghe kể chuyện",   3),
        new Activity("di_ngu",       "Đi ngủ",           3, "Family/sleepy"),
    };

    // Trạng thái bầu trời theo buổi: màu trời, vị trí+màu mặt trời, hiện mặt trăng/sao.
    static readonly string[] SkyHex = { "#8FD3FF", "#4FB0FF", "#FFB070", "#1C2B63" };
    static readonly Vector2[] SunPos = { new Vector2(0.20f, 0.64f), new Vector2(0.50f, 0.85f), new Vector2(0.82f, 0.60f), new Vector2(0.92f, -0.10f) };
    static readonly string[] SunHex = { "#FFE27A", "#FFF04D", "#FF7A3D", "#FF7A3D" };
    static readonly Vector2 MoonHidden = new Vector2(0.08f, -0.10f), MoonShown = new Vector2(0.78f, 0.80f);

    int _period;                 // buổi hiện tại của thế giới (tăng dần mỗi lượt đúng)
    int _lastActivity = -1;
    Image _sky, _sun, _moon;
    readonly List<Image> _stars = new List<Image>();
    Text _periodLabel, _promptLabel;
    RectTransform _na;
    readonly List<StoryCard> _cards = new List<StoryCard>();
    bool _locked;

    protected override void Build()
    {
        _sky = StoryUI.Fill(root, "Sky", StoryUI.Hex(SkyHex[0]));

        var starPos = new[] { new Vector2(0.15f, 0.88f), new Vector2(0.32f, 0.78f), new Vector2(0.55f, 0.90f), new Vector2(0.68f, 0.70f), new Vector2(0.42f, 0.66f), new Vector2(0.9f, 0.9f) };
        foreach (var sp in starPos)
        {
            var s = StoryUI.Pic(root, "Star", ShapeSprites.Star, new Color(1f, 1f, 0.8f, 0f), P(sp.x, sp.y), Vector2.one * U * 0.05f);
            _stars.Add(s);
        }

        _sun = StoryUI.Pic(root, "Sun", ShapeSprites.Circle, StoryUI.Hex(SunHex[0]), P(SunPos[0].x, SunPos[0].y), Vector2.one * U * 0.17f);
        _moon = StoryUI.Pic(root, "Moon", ShapeSprites.Moon, new Color(1f, 1f, 0.85f, 0f), P(MoonHidden.x, MoonHidden.y), Vector2.one * U * 0.15f);

        // Mặt đất + cây/hoa trang trí (Kenney NatureKit nếu có).
        StoryUI.Pic(root, "Ground", ShapeSprites.Square, StoryUI.Hex("#6CC24A"), P(0.5f, 0.07f), new Vector2(W, H * 0.22f));
        Decor("NatureKit/Isometric/tree_oak_NE", 0.08f, 0.30f, 0.30f);
        Decor("NatureKit/Isometric/tree_default_NE", 0.93f, 0.30f, 0.26f);
        Decor("NatureKit/Isometric/flower_redA_NE", 0.30f, 0.20f, 0.10f);
        Decor("NatureKit/Isometric/flower_yellowB_NE", 0.72f, 0.21f, 0.10f);

        _na = BuildNa();

        _periodLabel = StoryUI.Label(root, "", 54, Color.white, P(0.5f, 0.93f), new Vector2(W * 0.9f, H * 0.12f));
        _promptLabel = StoryUI.Label(root, "Na làm gì nào?", 34, Color.white, P(0.5f, 0.84f), new Vector2(W * 0.9f, H * 0.09f));
        ApplySky(_period, 0f);
    }

    void Decor(string path, float fx, float fy, float size)
    {
        var s = StoryUI.Load(path);
        if (s != null) StoryUI.Pic(root, "Decor", s, Color.white, P(fx, fy), Vector2.one * U * size);
    }

    RectTransform BuildNa()
    {
        var spr = StoryUI.Load("Story/Day/na");
        if (spr != null)
            return StoryUI.Pic(root, "Na", spr, Color.white, P(0.5f, 0.50f), Vector2.one * U * 0.36f).rectTransform;

        // Hình thay thế: bé gái đơn giản vẽ bằng hình khối.
        var g = StoryUI.Rect(root, "Na", P(0.5f, 0.50f), Vector2.one * U * 0.36f);
        float u = U;
        StoryUI.Pic(g, "Body", ShapeSprites.RoundedRect, StoryUI.Hex("#FF7FA8"), new Vector2(0f, -u * 0.09f), new Vector2(u * 0.20f, u * 0.17f));
        StoryUI.Pic(g, "Hair", ShapeSprites.Circle, StoryUI.Hex("#6B3F1E"), new Vector2(0f, u * 0.075f), Vector2.one * u * 0.235f);
        StoryUI.Pic(g, "Head", ShapeSprites.Circle, StoryUI.Hex("#FFD6AA"), new Vector2(0f, u * 0.05f), Vector2.one * u * 0.20f);
        StoryUI.Pic(g, "Fringe", ShapeSprites.RoundedRect, StoryUI.Hex("#6B3F1E"), new Vector2(0f, u * 0.125f), new Vector2(u * 0.19f, u * 0.05f));
        StoryUI.Pic(g, "EyeL", ShapeSprites.Circle, Color.black, new Vector2(-u * 0.04f, u * 0.055f), Vector2.one * u * 0.026f);
        StoryUI.Pic(g, "EyeR", ShapeSprites.Circle, Color.black, new Vector2(u * 0.04f, u * 0.055f), Vector2.one * u * 0.026f);
        StoryUI.Pic(g, "Mouth", ShapeSprites.RoundedRect, StoryUI.Hex("#E0454F"), new Vector2(0f, u * 0.010f), new Vector2(u * 0.05f, u * 0.016f));
        return g;
    }

    // ── Lượt chơi ─────────────────────────────────────────────────────────────

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearCards();
        _locked = false;
        int p = _period;
        _periodLabel.text = "BUỔI " + PeriodNames[p];
        _promptLabel.text = "Na làm gì nào?";

        // Việc đúng: ngẫu nhiên trong buổi p (tránh lặp lại việc vừa rồi).
        var good = new List<int>();
        for (int i = 0; i < Activities.Length; i++) if (Activities[i].period == p) good.Add(i);
        int correct = good[Rand(0, good.Count)];
        if (good.Count > 1 && correct == _lastActivity) correct = good[(good.IndexOf(correct) + 1) % good.Count];
        _lastActivity = correct;

        // 2 việc sai từ 2 buổi khác nhau (nếu được).
        var wrongPool = new List<int>();
        for (int i = 0; i < Activities.Length; i++) if (Activities[i].period != p) wrongPool.Add(i);
        var picks = new List<int> { correct };
        var usedPeriods = new HashSet<int> { p };
        var wp = wrongPool.ToArray(); Shuffle(wp);
        foreach (int w in wp) { if (picks.Count >= 3) break; if (usedPeriods.Add(Activities[w].period)) picks.Add(w); }
        foreach (int w in wp) { if (picks.Count >= 3) break; if (!picks.Contains(w)) picks.Add(w); }
        var order = picks.ToArray(); Shuffle(order);

        var labels = new string[order.Length];
        int correctPos = 0;
        for (int i = 0; i < order.Length; i++) { labels[i] = Activities[order[i]].label; if (order[i] == correct) correctPos = i; }
        Describe(info, $"NGAY_{p}_{Activities[correct].key}", "NgayCuaBe_BuoiTrongNgay",
                 $"Buổi {PeriodTopic[p].ToLower()}: bé làm gì?", labels, correctPos);

        ctx.Voice("buoi_" + p);
        float cardW = Mathf.Min(W * 0.30f, H * 0.30f);
        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        for (int i = 0; i < order.Length; i++)
        {
            int idx = i;
            var act = Activities[order[i]];
            var icon = StoryUI.Load("Story/Day/" + act.key) ?? (act.fallbackImage != null ? StoryUI.Load(act.fallbackImage) : null);
            float x = (i - (order.Length - 1) * 0.5f) * (cardW + W * 0.025f);
            var pos = new Vector2(x, P(0.5f, 0.17f).y);
            StoryCard card = null;
            card = StoryUI.Card(root, "Act" + i, icon, act.label, CardColor(order[i]), pos, new Vector2(cardW, cardW * 1.05f), () =>
            {
                if (_locked) return;
                if (idx == correctPos)
                {
                    _locked = true;
                    hint = false;
                    ctx.SfxRight();
                    ctx.Sfx("star");
                    Run(Success(card, act, p, mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(StoryUI.Shake(card.rt));
                    Run(StoryUI.Shake(_na, 0.4f, 10f));
                    Say($"Chưa phải lúc {act.label.ToLower()} đâu!", P(0.5f, 0.66f), StoryUI.Hex("#FFE27A"));
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_cards[correctPos].rt, () => hint && !_locked)); }
                }
            });
            _cards.Add(card);
        }
    }

    IEnumerator Success(StoryCard card, Activity act, int period, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        // Thẻ bay tới Na, Na nhún nhảy, rồi trời chuyển sang buổi kế.
        Run(StoryUI.MoveTo(card.rt, _na.anchoredPosition + new Vector2(0f, U * 0.02f), 0.45f));
        Run(StoryUI.ScaleTo(card.rt, 0.55f, 0.45f));
        foreach (var c in _cards) if (c != card) Run(StoryUI.Fade(c.bg, 0f, 0.3f));
        yield return new WaitForSeconds(0.45f);
        Run(StoryUI.Bounce(_na, 0.18f, 0.5f));
        Say("Giỏi quá! Na " + act.label.ToLower(), P(0.5f, 0.66f), Color.white, 1.4f);
        if (card.bg != null) Run(StoryUI.Fade(card.bg, 0f, 0.4f));
        if (card.icon != null) Run(StoryUI.Fade(card.icon, 0f, 0.4f));
        if (card.label != null) Run(StoryUI.Fade(card.label, 0f, 0.4f));

        _period = (period + 1) % 4;
        yield return ApplySkyAnimated(_period, 1.6f);
        done(firstTry, answer);
    }

    static Color CardColor(int activityIndex)
    {
        string[] hex = { "#F29F3D", "#4FA3E0", "#5BBF7A", "#E0627F", "#9B7AD8", "#E8B33A", "#3FB8AF" };
        return StoryUI.Hex(hex[activityIndex % hex.Length]);
    }

    void ClearCards()
    {
        foreach (var c in _cards) if (c != null && c.rt != null) UnityEngine.Object.Destroy(c.rt.gameObject);
        _cards.Clear();
    }

    // ── Bầu trời ──────────────────────────────────────────────────────────────

    void ApplySky(int p, float t)
    {
        _sky.color = StoryUI.Hex(SkyHex[p]);
        bool night = p == 3;
        _sun.color = StoryUI.Hex(SunHex[p]);
        _sun.rectTransform.anchoredPosition = P(SunPos[p].x, SunPos[p].y);
        _moon.rectTransform.anchoredPosition = P((night ? MoonShown : MoonHidden).x, (night ? MoonShown : MoonHidden).y);
        _moon.color = new Color(1f, 1f, 0.85f, night ? 1f : 0f);
        foreach (var s in _stars) s.color = new Color(1f, 1f, 0.8f, night ? 1f : 0f);
    }

    IEnumerator ApplySkyAnimated(int p, float dur)
    {
        bool night = p == 3;
        var sunTo = P(SunPos[p].x, SunPos[p].y);
        var moonTo = P((night ? MoonShown : MoonHidden).x, (night ? MoonShown : MoonHidden).y);
        Run(StoryUI.ColorTo(_sky, StoryUI.Hex(SkyHex[p]), dur));
        Run(StoryUI.ColorTo(_sun, StoryUI.Hex(SunHex[p]), dur));
        Run(StoryUI.MoveTo(_sun.rectTransform, sunTo, dur));
        Run(StoryUI.MoveTo(_moon.rectTransform, moonTo, dur));
        Run(StoryUI.ColorTo(_moon, new Color(1f, 1f, 0.85f, night ? 1f : 0f), dur));
        foreach (var s in _stars) Run(StoryUI.ColorTo(s, new Color(1f, 1f, 0.8f, night ? 1f : 0f), dur));
        _periodLabel.text = "BUỔI " + PeriodNames[p];
        yield return new WaitForSeconds(dur);
    }
}
