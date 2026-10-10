using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Trứng màu sắc" (bé 3 tuổi) — nhận biết MÀU. Mỗi lượt 3 quả trứng màu khác nhau trong tổ; giọng đọc / chữ gọi 1 màu,
/// bé dậm vào trứng đúng màu → trứng nở ra bông hoa cùng màu (kèm trái cây màu đó). Trứng sai chỉ rung nhẹ (không phạt),
/// sau 2 lần sai thì trứng đúng nhấp nháy. Sau khi nở trứng đúng, bé dậm nốt các trứng còn lại để xem hoa nở.
/// Ảnh: Story/Food/* (trái cây, có sẵn); thiếu ảnh vẫn chơi được (hoa vẽ bằng code).
/// </summary>
public sealed class TrungMauWorld : StoryWorld
{
    sealed class ColorDef
    {
        public string key, label, hex, fruit;
        public ColorDef(string k, string l, string h, string f) { key = k; label = l; hex = h; fruit = f; }
    }

    static readonly ColorDef[] Colors =
    {
        new ColorDef("red", "Đỏ", "#E53935", "apple"), new ColorDef("blue", "Xanh dương", "#1E88E5", "blueberry"),
        new ColorDef("green", "Xanh lá", "#43A047", "pear"), new ColorDef("yellow", "Vàng", "#FDD835", "banana"),
        new ColorDef("orange", "Cam", "#FB8C00", "orange"), new ColorDef("pink", "Hồng", "#EC6FA7", "peach"),
        new ColorDef("purple", "Tím", "#8E24AA", "grape"),
    };

    sealed class Egg
    {
        public RectTransform rt;
        public Image body;
        public ColorDef color;
        public bool hatched;
        public Vector2 basePos;
    }

    readonly List<Egg> _eggs = new List<Egg>();
    readonly List<GameObject> _extras = new List<GameObject>();
    Text _prompt;
    Image _chip;
    bool _active;

    protected override void Build()
    {
        StoryUI.Fill(root, "Sky", StoryUI.Hex("#FFF1CF"));
        StoryUI.Pic(root, "Hill", ShapeSprites.Circle, StoryUI.Hex("#9ED67A"), P(0.5f, -0.18f), new Vector2(W * 1.5f, H * 0.7f)).preserveAspect = false;
        StoryUI.Pic(root, "Sun", ShapeSprites.Circle, StoryUI.Hex("#FFD23F"), P(0.88f, 0.80f), Vector2.one * U * 0.12f);

        _chip = StoryUI.Pic(root, "Chip", ShapeSprites.Circle, Color.white, P(0.14f, 0.91f), Vector2.one * U * 0.13f);
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.56f, 0.91f), new Vector2(W * 0.74f, H * 0.13f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;

        var pool = (ColorDef[])Colors.Clone(); Shuffle(pool);
        var picks = new[] { pool[0], pool[1], pool[2] };
        int correctPos = Rand(0, 3);
        var target = picks[correctPos];
        Describe(info, $"TRUNG_{target.key}", "TrungMauSac_MauSac", $"Chọn trứng màu {target.label.ToLower()}",
                 new[] { picks[0].label, picks[1].label, picks[2].label }, correctPos);

        _chip.color = StoryUI.Hex(target.hex);
        _prompt.text = $"Trứng màu {target.label.ToUpper()} đâu?";
        ctx.Voice("mau_" + target.key);

        float[] xs = { 0.20f, 0.50f, 0.80f };
        int mistakes = 0, firstWrong = -1;
        bool hint = false, answered = false;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var def = picks[i];
            float eggW = U * 0.25f, eggH = U * 0.33f;
            var pos = P(xs[i], 0.40f);
            // Tổ
            var nest = StoryUI.Pic(root, "Nest", ShapeSprites.Circle, StoryUI.Hex("#B98A56"), pos + new Vector2(0, -eggH * 0.42f), new Vector2(eggW * 1.35f, eggH * 0.34f));
            nest.preserveAspect = false;
            _extras.Add(nest.gameObject);

            var body = StoryUI.Pic(root, "Egg_" + def.key, ShapeSprites.Circle, StoryUI.Hex(def.hex), pos, new Vector2(eggW, eggH), raycast: true);
            body.preserveAspect = false;
            var eggImg = StoryUI.Load("Story/Egg/" + def.key);   // ảnh thay thế từ gói zip (tuỳ chọn)
            if (eggImg != null)
            {
                body.sprite = eggImg; body.color = Color.white; body.preserveAspect = true;
            }
            else
            {
                // hoa văn: dải trắng + chấm
                StoryUI.Pic(body.transform, "Band", ShapeSprites.Square, new Color(1f, 1f, 1f, 0.55f), new Vector2(0, -eggH * 0.05f), new Vector2(eggW * 0.8f, eggH * 0.08f)).preserveAspect = false;
                StoryUI.Pic(body.transform, "Dot1", ShapeSprites.Circle, new Color(1f, 1f, 1f, 0.55f), new Vector2(-eggW * 0.18f, eggH * 0.2f), Vector2.one * eggW * 0.13f);
                StoryUI.Pic(body.transform, "Dot2", ShapeSprites.Circle, new Color(1f, 1f, 1f, 0.55f), new Vector2(eggW * 0.16f, -eggH * 0.22f), Vector2.one * eggW * 0.1f);
            }

            var egg = new Egg { rt = body.rectTransform, body = body, color = def, basePos = pos };
            _eggs.Add(egg);
            Run(StoryUI.PopIn(egg.rt, 0.35f));
            StoryUI.OnTap(body.gameObject, () =>
            {
                if (!_active || egg.hatched) return;
                if (idx == correctPos && !answered)
                {
                    answered = true; hint = false;
                    ctx.SfxRight();
                    Run(Hatch(egg));
                    Run(FinishAfterReward(mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else if (answered)
                {
                    Run(Hatch(egg));   // thưởng thêm: nở nốt trứng còn lại
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.Sfx("tap");
                    Run(StoryUI.Shake(egg.rt, 0.35f, 10f));
                    Say($"Trứng này màu {def.label.ToLower()} nè!", P(0.5f, 0.70f), StoryUI.Hex("#7A4B00"), 1.3f);
                    if (mistakes >= 2 && !hint)
                    {
                        hint = true;
                        Run(StoryUI.Pulse(_eggs[correctPos].rt, () => hint && !answered, 0.16f, 7f));
                    }
                }
            });
        }
    }

    IEnumerator Hatch(Egg egg)
    {
        egg.hatched = true;
        ctx.Sfx("plop");
        var center = egg.basePos;
        // rung lắc rồi vỡ
        for (float t = 0f; t < 0.4f && egg.rt != null; t += Time.deltaTime)
        {
            egg.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 40f) * 12f);
            yield return null;
        }
        if (egg.rt == null) yield break;
        Confetti(center, 8, sound: false);
        Run(StoryUI.ScaleTo(egg.rt, 1.3f, 0.12f));
        yield return StoryUI.Fade(egg.body, 0f, 0.12f);
        if (egg.body != null) egg.body.gameObject.SetActive(false);

        // Hoa nở + trái cây cùng màu
        var flower = StoryUI.Pic(root, "Flower", ShapeSprites.Flower, StoryUI.Hex(egg.color.hex), center + new Vector2(0, U * 0.02f), Vector2.one * U * 0.30f);
        var core = StoryUI.Pic(flower.transform, "Core", ShapeSprites.Circle, StoryUI.Hex("#FFD23F"), Vector2.zero, Vector2.one * U * 0.08f);
        var stem = StoryUI.Pic(root, "Stem", ShapeSprites.Square, StoryUI.Hex("#4CAF50"), center + new Vector2(0, -U * 0.19f), new Vector2(U * 0.025f, U * 0.16f));
        stem.preserveAspect = false;
        stem.transform.SetSiblingIndex(flower.transform.GetSiblingIndex());
        _extras.Add(flower.gameObject); _extras.Add(stem.gameObject);
        var label = StoryUI.Label(root, egg.color.label, 34, Color.white, center + new Vector2(0, -U * 0.30f), new Vector2(U * 0.5f, U * 0.1f));
        _extras.Add(label.gameObject);
        var fruit = StoryUI.Load("Story/Food/" + egg.color.fruit);
        if (fruit != null)
        {
            var f = StoryUI.Pic(root, "Fruit", fruit, Color.white, center + new Vector2(U * 0.11f, U * 0.17f), Vector2.one * U * 0.17f);
            _extras.Add(f.gameObject);
            Run(StoryUI.PopIn(f.rectTransform, 0.35f));
        }
        yield return StoryUI.PopIn(flower.rectTransform, 0.4f);
        Run(StoryUI.Bounce(flower.rectTransform, 0.1f, 0.6f));
    }

    IEnumerator FinishAfterReward(bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(2.4f);
        done(firstTry, answer);
    }

    void Clear()
    {
        _active = false;
        foreach (var e in _eggs) if (e != null && e.rt != null) UnityEngine.Object.Destroy(e.rt.gameObject);
        _eggs.Clear();
        foreach (var e in _extras) if (e != null) UnityEngine.Object.Destroy(e);
        _extras.Clear();
    }
}
