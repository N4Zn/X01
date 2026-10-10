using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Nhận biết hình" (bé 3 tuổi) — chọn đúng HÌNH được gọi (tròn, vuông, tam giác; sau đó chữ nhật, sao, tim, thoi).
/// CHẾ ĐỘ CHUNG (Synchronized): 2 đội cùng 1 câu, đội xong trước chờ đội kia xong mới sang câu tiếp.
/// Mỗi hình có MÀU CỐ ĐỊNH đi kèm (tim đỏ, tam giác vàng, vuông xanh lá, tròn xanh dương, chữ nhật cam, sao tím, thoi hồng).
/// Giọng đọc: StoryVoice/hinh_&lt;tron|vuong|tamgiac|chunhat|sao|tim|thoi&gt;. Không cần ảnh.
/// </summary>
public sealed class HinhDonGianWorld : StoryWorld
{
    public override bool Synchronized => true;

    sealed class Shp
    {
        public string key, label, hex; public string sprite; public Vector2 aspect;
        public Shp(string k, string l, string s, string h, float ax = 1f, float ay = 1f) { key = k; label = l; sprite = s; hex = h; aspect = new Vector2(ax, ay); }
    }

    // Thứ tự = thứ tự mở khoá độ khó.
    static readonly Shp[] Shapes =
    {
        new Shp("tron", "Hình tròn", "circle", "#1E88E5"), new Shp("vuong", "Hình vuông", "square", "#43A047"), new Shp("tamgiac", "Hình tam giác", "triangle", "#FDD835"),
        new Shp("chunhat", "Hình chữ nhật", "square", "#FB8C00", 1.5f, 0.95f), new Shp("sao", "Ngôi sao", "star", "#8E24AA"),
        new Shp("tim", "Hình trái tim", "heart", "#E53935"), new Shp("thoi", "Hình thoi", "diamond", "#EC6FA7"),
    };

    readonly List<GameObject> _items = new List<GameObject>();
    Text _prompt;
    bool _active;
    int _n;

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#E3F2FD"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#BBDEFB"), P(0.5f, 0.06f), new Vector2(W, H * 0.14f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.5f, 0.91f), new Vector2(W * 0.92f, H * 0.14f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;
        var r = NextTaskRng();
        int n = _n++;

        // 3 hình đầu mở sẵn; sau 4 lượt thêm chữ nhật + sao; sau 8 lượt thêm tim + thoi.
        int allowed = n < 4 ? 3 : n < 8 ? 5 : 7;
        var idxs = new List<int>();
        for (int i = 0; i < allowed; i++) idxs.Add(i);
        for (int i = idxs.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (idxs[i], idxs[j]) = (idxs[j], idxs[i]); }
        var picks = new[] { Shapes[idxs[0]], Shapes[idxs[1]], Shapes[idxs[2]] };
        int correctPos = r.Next(3);
        var target = picks[correctPos];

        Describe(info, "HINH_" + target.key, "HinhDonGian_NhanBietHinh", $"Chọn {target.label.ToLower()}",
                 new[] { picks[0].label, picks[1].label, picks[2].label }, correctPos);

        _prompt.text = $"Chọn {target.label.ToUpper()}!";
        ctx.Voice("hinh_" + target.key);

        float[] xs = { 0.19f, 0.50f, 0.81f };
        int mistakes = 0, firstWrong = -1;
        bool hint = false, answered = false;
        var cards = new StoryCard[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var shp = picks[i];
            float cs = Mathf.Min(U * 0.32f, W * 0.29f);
            var card = StoryUI.Card(root, "Card_" + shp.key, null, "", new Color(1f, 1f, 1f, 0.96f), P(xs[i], 0.46f), new Vector2(cs, cs * 1.1f), null);
            cards[i] = card;
            _items.Add(card.rt.gameObject);
            var custom = StoryUI.Load("Story/Shapes/" + shp.key);   // ảnh thay thế từ gói zip (tuỳ chọn)
            var fig = custom != null
                ? StoryUI.Pic(card.rt, "Shape", custom, Color.white, Vector2.zero, Vector2.one * cs * 0.72f)
                : StoryUI.Pic(card.rt, "Shape", ShapeSprites.ByName(shp.sprite), StoryUI.Hex(shp.hex), Vector2.zero,
                              new Vector2(cs * 0.66f * shp.aspect.x, cs * 0.66f * shp.aspect.y));
            fig.preserveAspect = custom != null;
            Run(StoryUI.PopIn(card.rt, 0.3f));
            StoryUI.OnTap(card.rt.gameObject, () =>
            {
                if (!_active || answered) return;
                if (idx == correctPos)
                {
                    answered = true; hint = false;
                    ctx.SfxRight();
                    card.bg.color = StoryUI.Hex("#C8F7C5");
                    Run(StoryUI.Bounce(fig.rectTransform, 0.3f, 0.6f));
                    Confetti(card.rt.anchoredPosition, 10);
                    Say(shp.label + "!", P(0.5f, 0.15f), StoryUI.Hex("#1B5E20"), 1.6f);
                    Run(FinishAfterReward(mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.Sfx("tap");
                    Run(StoryUI.Shake(card.rt, 0.35f, 10f));
                    Say($"Đây là {shp.label.ToLower()} nè!", P(0.5f, 0.15f), StoryUI.Hex("#BF360C"), 1.3f);
                    if (mistakes >= 2 && !hint)
                    {
                        hint = true;
                        Run(StoryUI.Pulse(cards[correctPos].rt, () => hint && !answered, 0.12f, 7f));
                    }
                }
            });
        }
    }

    IEnumerator FinishAfterReward(bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(1.8f);
        done(firstTry, answer);
    }

    void Clear()
    {
        _active = false;
        foreach (var e in _items) if (e != null) UnityEngine.Object.Destroy(e);
        _items.Clear();
    }
}
