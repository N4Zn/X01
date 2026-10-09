using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Chăm bạn Bi" — bé cần gì để lớn lên và khỏe mạnh: ăn uống lành mạnh, uống nước, ngủ, vận động, vệ sinh.
/// Bi (ảnh cảm xúc Family/*) nói ra điều mình CẦN; bé chọn thứ giúp Bi khỏe. Đúng → món bay vào Bi, Bi vui lên,
/// thêm 1 ngôi sao; đủ 5 sao → Bi LỚN LÊN (to hơn) + pháo giấy. Sai → Bi lắc đầu kèm lời giải thích ngắn
/// ("Ăn nhiều kẹo sẽ sâu răng!") và được thử lại.
/// Ảnh: Story/Food/* (trái cây), SaveEnvironment/* (nước, kẹo, nước ngọt), NatureKit (giường), Story/Health/* (nếu có).
/// </summary>
public sealed class BeKhoeManhWorld : StoryWorld
{
    sealed class Item
    {
        public string label, path, why; public bool good;
        public Item(string label, string path, bool good, string why = null) { this.label = label; this.path = path; this.good = good; this.why = why; }
    }

    sealed class Need
    {
        public string key, image, prompt;
        public Item[] good, bad;
    }

    static readonly (string file, string vn)[] Fruits =
    {
        ("apple", "Táo"), ("banana", "Chuối"), ("orange", "Cam"), ("strawberry", "Dâu tây"), ("watermelon", "Dưa hấu"),
        ("grape", "Nho"), ("mango", "Xoài"), ("pineapple", "Dứa"), ("pear", "Lê"), ("peach", "Đào"),
        ("kiwi", "Kiwi"), ("papaya", "Đu đủ"), ("dragon_fruit", "Thanh long"), ("avocado", "Bơ"),
    };

    static readonly Item Candy = new Item("Kẹo", "SaveEnvironment/vo_keo", false, "Ăn nhiều kẹo sẽ sâu răng!");
    static readonly Item Soda  = new Item("Nước ngọt", "SaveEnvironment/vo_lon_coca", false, "Nước ngọt không tốt cho Bi!");
    static readonly Item Pizza = new Item("Bánh ngọt", "SaveEnvironment/mieng_pizza", false, "Ăn vặt nhiều thì hết đói bữa chính!");

    readonly Need[] _needs;
    readonly List<int> _bag = new List<int>();
    readonly List<StoryCard> _cards = new List<StoryCard>();
    readonly List<Image> _stars = new List<Image>();
    Image _bi;
    RectTransform _biRt;
    Text _prompt;
    bool _locked;
    int _starCount, _stage;

    public BeKhoeManhWorld()
    {
        var fruitItems = new List<Item>();
        foreach (var f in Fruits) fruitItems.Add(new Item(f.vn, "Story/Food/" + f.file, true));
        fruitItems.Add(new Item("Cà rốt", "Fruit/carrot", true));
        fruitItems.Add(new Item("Bữa cơm", "SaveEnvironment/thuc_an", true));

        _needs = new[]
        {
            new Need { key = "an",   image = "Family/hungry",  prompt = "Bi đói bụng! Cho Bi ăn gì?",
                       good = fruitItems.ToArray(), bad = new[] { Candy, Soda } },
            new Need { key = "uong", image = "Family/thirsty", prompt = "Bi khát nước! Bi cần uống gì?",
                       good = new[] { new Item("Nước lọc", "SaveEnvironment/giot_nuoc", true) }, bad = new[] { Soda, Candy } },
            new Need { key = "ngu",  image = "Family/sleepy",  prompt = "Bi buồn ngủ! Bi cần gì?",
                       good = new[] { new Item("Đi ngủ", "NatureKit/Isometric/bed_NE", true) },
                       bad = new[] { new Item("Xem tivi", "Story/Health/xem_tivi", false, "Xem tivi khuya hại mắt!"),
                                     new Item("Chơi điện thoại", "Story/Health/dien_thoai", false, "Chơi điện thoại nhiều hại mắt!") } },
            new Need { key = "van_dong", image = "Family/sad", prompt = "Bi chán quá! Bi cần làm gì?",
                       good = new[] { new Item("Chạy nhảy", "Story/Health/chay_nhay", true) },
                       bad = new[] { new Item("Nằm xem tivi", "Story/Health/xem_tivi", false, "Bi cần vận động cho khỏe!"), Pizza } },
            new Need { key = "ve_sinh", image = "Family/hot", prompt = "Tay Bi bẩn rồi! Bi cần gì?",
                       good = new[] { new Item("Rửa tay", "Story/Health/rua_tay", true) },
                       bad = new[] { new Item("Lau vào áo", "Story/Health/lau_ao", false, "Tay vẫn còn vi khuẩn đấy!"),
                                     new Item("Ăn luôn", "Story/Health/an_luon", false, "Tay bẩn có vi khuẩn, phải rửa trước!") } },
        };
    }

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#FFF1D6"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#F3D9A4"), P(0.5f, 0.10f), new Vector2(W, H * 0.26f));
        var flowers = StoryUI.Load("NatureKit/Isometric/flower_redA_NE");
        if (flowers != null)
        {
            StoryUI.Pic(root, "FlowerL", flowers, Color.white, P(0.09f, 0.50f), Vector2.one * U * 0.14f);
            StoryUI.Pic(root, "FlowerR", StoryUI.Load("NatureKit/Isometric/flower_yellowA_NE") ?? flowers, Color.white, P(0.91f, 0.50f), Vector2.one * U * 0.14f);
        }

        for (int i = 0; i < 5; i++)
            _stars.Add(StoryUI.Pic(root, "Star" + i, ShapeSprites.Star, StoryUI.Hex("#CFC6B2"), P(0.5f + (i - 2) * 0.10f, 0.945f), Vector2.one * U * 0.075f));

        _prompt = StoryUI.Label(root, "", 32, Color.white, P(0.5f, 0.875f), new Vector2(W * 0.97f, H * 0.09f));
        _biRt = StoryUI.Rect(root, "BiRoot", P(0.5f, 0.585f), Vector2.zero);
        _bi = StoryUI.Pic(_biRt, "Bi", null, Color.white, Vector2.zero, new Vector2(U * 0.36f, U * 0.47f));
        _bi.rectTransform.sizeDelta = new Vector2(U * 0.36f, U * 0.47f);
    }

    float StageScale => 0.82f + 0.12f * Mathf.Min(_stage, 3);

    void ShowBi(string imagePath)
    {
        var s = StoryUI.Load(imagePath);
        _bi.sprite = s;
        _bi.color = s != null ? Color.white : StoryUI.Hex("#F6B26B");
    }

    // ── Lượt chơi ─────────────────────────────────────────────────────────────

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearCards();
        _locked = false;
        if (_bag.Count == 0) { var a = new[] { 0, 1, 2, 3, 4 }; Shuffle(a); _bag.AddRange(a); }
        var need = _needs[_bag[0]]; _bag.RemoveAt(0);

        ShowBi(need.image);
        _biRt.localScale = Vector3.one * StageScale;
        _prompt.text = need.prompt;
        ctx.Voice("bi_" + need.key);

        var good = need.good[Rand(0, need.good.Length)];
        var bads = (Item[])need.bad.Clone(); Shuffle(bads);
        var picks = new List<Item> { good, bads[0] };
        if (bads.Length > 1) picks.Add(bads[1]);
        var order = picks.ToArray(); Shuffle(order);

        var labels = new string[order.Length];
        int correctPos = 0;
        for (int i = 0; i < order.Length; i++) { labels[i] = order[i].label; if (order[i] == good) correctPos = i; }
        Describe(info, $"BI_{need.key}_{good.label}", "BeKhoeManh_" + need.key, $"{need.prompt}", labels, correctPos);

        float cardW = Mathf.Min(W * 0.28f, H * 0.28f);
        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        for (int i = 0; i < order.Length; i++)
        {
            int idx = i;
            var item = order[i];
            float x = (i - (order.Length - 1) * 0.5f) * (cardW + W * 0.03f);
            var pos = new Vector2(x, P(0.5f, 0.145f).y);
            var icon = StoryUI.Load(item.path);
            StoryCard card = StoryUI.Card(root, "Item" + i, icon, item.label, StoryUI.Hex(item.good ? "#5BBF7A" : "#4FA3E0"), pos,
                                          new Vector2(cardW, cardW * 1.05f), null);
            card.bg.color = StoryUI.Hex("#FFFFFF");
            if (card.label != null) { card.label.color = StoryUI.Hex("#3A2A12"); var o = card.label.GetComponent<Outline>(); if (o != null) o.enabled = false; }
            StoryUI.OnTap(card.bg.gameObject, () =>
            {
                if (_locked) return;
                if (idx == correctPos)
                {
                    _locked = true; hint = false;
                    ctx.SfxRight();
                    Run(Feed(card, mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(StoryUI.Shake(card.rt));
                    Run(StoryUI.Shake(_biRt, 0.45f, 16f));
                    Say(item.why ?? "Bi không cần cái này!", P(0.5f, 0.40f), StoryUI.Hex("#FFE27A"), 1.8f);
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_cards[correctPos].rt, () => hint && !_locked)); }
                }
            });
            _cards.Add(card);
        }
    }

    IEnumerator Feed(StoryCard card, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        // Món bay vào Bi → Bi vui lên.
        Run(StoryUI.MoveTo(card.rt, _biRt.anchoredPosition + new Vector2(0f, -U * 0.05f), 0.45f));
        Run(StoryUI.ScaleTo(card.rt, 0.4f, 0.45f));
        foreach (var c in _cards) if (c != card) Run(StoryUI.Fade(c.bg, 0f, 0.3f));
        yield return new WaitForSeconds(0.45f);
        Run(StoryUI.Fade(card.bg, 0f, 0.2f));
        if (card.icon != null) Run(StoryUI.Fade(card.icon, 0f, 0.2f));
        if (card.label != null) Run(StoryUI.Fade(card.label, 0f, 0.2f));
        ShowBi("Family/happy");
        Run(StoryUI.Bounce(_biRt, 0.15f, 0.5f));
        ctx.Sfx("plop");

        _starCount++;
        for (int i = 0; i < _stars.Count; i++)
            _stars[i].color = StoryUI.Hex(i < _starCount ? "#FFC83D" : "#CFC6B2");
        if (_starCount >= _stars.Count)
        {
            _starCount = 0; _stage++;
            Say("Bi lớn lên rồi! Khỏe quá!", P(0.5f, 0.40f), StoryUI.Hex("#FFE27A"), 2f);
            Confetti(_biRt.anchoredPosition, 22);
            Run(StoryUI.ScaleTo(_biRt, StageScale, 0.6f, overshoot: true));
            yield return new WaitForSeconds(1.6f);
            foreach (var s in _stars) s.color = StoryUI.Hex("#CFC6B2");
        }
        else
        {
            Say("Bi khỏe hơn rồi!", P(0.5f, 0.40f), Color.white, 1.2f);
            yield return new WaitForSeconds(0.8f);
        }
        done(firstTry, answer);
    }

    void ClearCards()
    {
        foreach (var c in _cards) if (c != null && c.rt != null) UnityEngine.Object.Destroy(c.rt.gameObject);
        _cards.Clear();
    }
}
