using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Vỡ bóng bay" (bé nhỏ 3-4 tuổi) — nhận biết MÀU SẮC + tên CON VẬT. Mỗi lượt 3 bóng bay màu khác nhau bay lên,
/// bé dậm vào bóng đúng màu được gọi → bóng vỡ, con vật chui ra kèm tên. Bóng sai chỉ rung nhẹ (không phạt),
/// sau 2 lần sai thì bóng đúng nhấp nháy. Sau khi vỡ bóng đúng, các bóng còn lại vẫn vỡ được để xem con vật.
/// Ảnh: Resources/Balloon/balloon_&lt;màu&gt;.png, Resources/GameImages/Animal/&lt;tên&gt;.png (sẵn có).
/// </summary>
public sealed class VoBongBayWorld : StoryWorld
{
    sealed class ColorDef { public string key, label, hex; public ColorDef(string k, string l, string h) { key = k; label = l; hex = h; } }

    static readonly ColorDef[] Colors =
    {
        new ColorDef("red", "Đỏ", "#E53935"), new ColorDef("blue", "Xanh dương", "#1E88E5"), new ColorDef("green", "Xanh lá", "#43A047"),
        new ColorDef("yellow", "Vàng", "#FDD835"), new ColorDef("orange", "Cam", "#FB8C00"), new ColorDef("pink", "Hồng", "#EC6FA7"),
        new ColorDef("purple", "Tím", "#8E24AA"),
    };

    static readonly (string file, string vn)[] Animals =
    {
        ("bear", "Gấu"), ("buffalo", "Trâu"), ("chick", "Gà con"), ("chicken", "Gà"), ("cow", "Bò"), ("crocodile", "Cá sấu"),
        ("dog", "Chó"), ("duck", "Vịt"), ("elephant", "Voi"), ("frog", "Ếch"), ("giraffe", "Hươu cao cổ"), ("goat", "Dê"),
        ("gorilla", "Khỉ đột"), ("hippo", "Hà mã"), ("horse", "Ngựa"), ("monkey", "Khỉ"), ("owl", "Cú mèo"), ("panda", "Gấu trúc"),
        ("parrot", "Vẹt"), ("penguin", "Chim cánh cụt"), ("pig", "Lợn"), ("rabbit", "Thỏ"), ("rhino", "Tê giác"),
        ("sloth", "Con lười"), ("snake", "Rắn"), ("zebra", "Ngựa vằn"), ("whale", "Cá voi"), ("moose", "Nai sừng tấm"),
    };

    sealed class Balloon
    {
        public RectTransform rt;
        public Image img;
        public ColorDef color;
        public bool popped;
        public Vector2 basePos;
    }

    readonly List<Balloon> _balloons = new List<Balloon>();
    readonly List<GameObject> _extras = new List<GameObject>();
    Text _prompt;
    Image _chip;
    int _round;
    bool _active;
    int _lastAnimal = -1;

    protected override void Build()
    {
        StoryUI.Fill(root, "Sky", StoryUI.Hex("#BFE9FF"));
        foreach (var c in new[] { new Vector2(0.18f, 0.80f), new Vector2(0.62f, 0.70f), new Vector2(0.86f, 0.84f) })
            StoryUI.Pic(root, "Cloud", ShapeSprites.Circle, new Color(1f, 1f, 1f, 0.75f), P(c.x, c.y), new Vector2(U * 0.26f, U * 0.13f)).preserveAspect = false;
        StoryUI.Pic(root, "Ground", ShapeSprites.Square, StoryUI.Hex("#7ACB5B"), P(0.5f, 0.06f), new Vector2(W, H * 0.14f));

        _chip = StoryUI.Pic(root, "Chip", ShapeSprites.Circle, Color.white, P(0.18f, 0.915f), Vector2.one * U * 0.12f);
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.58f, 0.915f), new Vector2(W * 0.72f, H * 0.12f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;
        _round++;

        // 3 màu khác nhau; màu được gọi = 1 trong 3.
        var pool = (ColorDef[])Colors.Clone(); Shuffle(pool);
        var picks = new[] { pool[0], pool[1], pool[2] };
        int correctPos = Rand(0, 3);
        var target = picks[correctPos];
        var labels = new[] { picks[0].label, picks[1].label, picks[2].label };
        Describe(info, $"BONG_{target.key}", "VoBongBay_MauSac", $"Vỡ bóng màu {target.label.ToLower()}", labels, correctPos);

        _chip.color = StoryUI.Hex(target.hex);
        _prompt.text = $"Vỡ bóng màu {target.label.ToUpper()}!";
        ctx.Voice("mau_" + target.key);

        float[] xs = { 0.20f, 0.50f, 0.80f };
        int mistakes = 0, firstWrong = -1;
        bool hint = false, answered = false;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var def = picks[i];
            var spr = StoryUI.Load("Balloon/balloon_" + def.key);
            float size = U * 0.30f;
            var start = P(xs[i], -0.15f);
            var img = StoryUI.Pic(root, "Balloon_" + def.key, spr ?? ShapeSprites.Circle, spr != null ? Color.white : StoryUI.Hex(def.hex),
                                  start, new Vector2(size * 0.75f, size), raycast: true);
            if (spr == null) img.preserveAspect = false;
            var b = new Balloon { rt = img.rectTransform, img = img, color = def, basePos = P(xs[i], 0.50f + (i == 1 ? 0.06f : 0f)) };
            _balloons.Add(b);
            Run(Rise(b, 0.2f * i));
            StoryUI.OnTap(img.gameObject, () =>
            {
                if (!_active || b.popped) return;
                if (idx == correctPos && !answered)
                {
                    answered = true; hint = false;
                    ctx.SfxRight();
                    Run(Pop(b));
                    Run(FinishAfterReward(mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else if (answered)
                {
                    Run(Pop(b));   // thưởng thêm: vỡ nốt bóng còn lại để xem con vật
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.Sfx("tap");
                    Run(StoryUI.Shake(b.rt, 0.35f, 10f));
                    Say($"Bóng này màu {def.label.ToLower()} nè!", P(0.5f, 0.30f), StoryUI.Hex("#FFE27A"), 1.3f);
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_balloons[correctPos].rt, () => hint && !answered, 0.18f, 7f)); }
                }
            });
        }
    }

    IEnumerator Rise(Balloon b, float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return StoryUI.MoveTo(b.rt, b.basePos, 0.9f);
        if (b.rt != null && !b.popped) Run(StoryUI.Bob(b.rt, U * 0.02f, 2.2f, UnityEngine.Random.value * 6f, () => _active && !b.popped));
    }

    IEnumerator Pop(Balloon b)
    {
        b.popped = true;
        ctx.Sfx("pop");
        var center = b.rt.anchoredPosition;
        Confetti(center, 8, sound: false);
        Run(StoryUI.ScaleTo(b.rt, 1.35f, 0.12f));
        yield return StoryUI.Fade(b.img, 0f, 0.12f);
        if (b.img != null) b.img.gameObject.SetActive(false);

        // Con vật chui ra + tên.
        int ai;
        do { ai = Rand(0, Animals.Length); } while (ai == _lastAnimal);
        _lastAnimal = ai;
        var (file, vn) = Animals[ai];
        var spr = StoryUI.Load("GameImages/Animal/" + file);
        var animal = StoryUI.Pic(root, "Animal", spr ?? ShapeSprites.Circle, spr != null ? Color.white : StoryUI.Hex("#F6B26B"),
                                 center, Vector2.one * U * 0.30f);
        var name = StoryUI.Label(root, vn, 34, Color.white, center + new Vector2(0f, -U * 0.19f), new Vector2(U * 0.5f, U * 0.1f));
        _extras.Add(animal.gameObject);
        _extras.Add(name.gameObject);
        ctx.Voice("con_" + file);
        yield return StoryUI.PopIn(animal.rectTransform, 0.35f);
        Run(StoryUI.Bounce(animal.rectTransform, 0.12f, 0.6f));
    }

    IEnumerator FinishAfterReward(bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(2.4f);
        done(firstTry, answer);
    }

    void Clear()
    {
        _active = false;
        foreach (var b in _balloons) if (b != null && b.rt != null) UnityEngine.Object.Destroy(b.rt.gameObject);
        _balloons.Clear();
        foreach (var e in _extras) if (e != null) UnityEngine.Object.Destroy(e);
        _extras.Clear();
    }
}
