using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Dài – ngắn, to – bé" — 2 kiểu lượt xen kẽ:
///  A) BẮC CẦU CHO THỎ: sông rộng khác nhau mỗi lượt, 3 khúc gỗ dài ngắn khác nhau. Bé chọn khúc gỗ DÀI hơn sông;
///     khúc ngắn đặt lên thì rơi "tõm" xuống nước (thấy tận mắt "ngắn hơn" thay vì đọc chữ). Đúng → Thỏ chạy qua ăn cà rốt.
///  B) CHUI CỬA: con vật (gà con bé / chó vừa / voi to) phải đi vào cửa vừa cỡ — chọn 1 trong 3 cửa to–vừa–bé.
/// Ảnh: GameImages/Animal/{rabbit,chick,dog,elephant}, Fruit/carrot (sẵn có); hình còn lại vẽ bằng code.
/// </summary>
public sealed class CauBacQuaWorld : StoryWorld
{
    readonly List<GameObject> _temp = new List<GameObject>();
    Text _prompt;
    int _taskCount;
    bool _locked;

    // Cảnh bắc cầu (tạo 1 lần, đổi vị trí mỗi lượt).
    Image _bankL, _bankR, _river;
    RectTransform _rabbit, _carrot;
    const float BankTop = 0.60f;

    protected override void Build()
    {
        StoryUI.Fill(root, "Sky", StoryUI.Hex("#D9F1FF"));
        _prompt = StoryUI.Label(root, "", 36, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.97f, H * 0.12f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearTemp();
        _locked = false;
        bool bridge = (_taskCount++ % 2) == 0;
        if (bridge) BeginBridge(info, done); else BeginDoors(info, done);
    }

    // ── A) Bắc cầu ────────────────────────────────────────────────────────────

    void BeginBridge(QuestionData info, Action<bool, int[]> done)
    {
        float[] gaps = { 0.30f, 0.40f, 0.50f };
        float g = gaps[Rand(0, gaps.Length)];           // độ rộng sông (phân số của W)
        float gapPx = g * W;
        float edgeL = 0.5f - g / 2f, edgeR = 0.5f + g / 2f;

        _bankL = Track(StoryUI.Pic(root, "BankL", ShapeSprites.Square, StoryUI.Hex("#6CC24A"), P(edgeL - 0.25f, BankTop - 0.08f), new Vector2(W * 0.5f, H * 0.16f)));
        _bankR = Track(StoryUI.Pic(root, "BankR", ShapeSprites.Square, StoryUI.Hex("#6CC24A"), P(edgeR + 0.25f, BankTop - 0.08f), new Vector2(W * 0.5f, H * 0.16f)));
        _bankL.preserveAspect = _bankR.preserveAspect = false;
        _river = Track(StoryUI.Pic(root, "River", ShapeSprites.Square, StoryUI.Hex("#4DA6FF"), P(0.5f, BankTop - 0.10f), new Vector2(gapPx, H * 0.20f)));
        _river.preserveAspect = false;

        var rabbitSpr = StoryUI.Load("GameImages/Animal/rabbit");
        var rabbit = Track(StoryUI.Pic(root, "Rabbit", rabbitSpr ?? ShapeSprites.Circle, rabbitSpr != null ? Color.white : StoryUI.Hex("#F3F3F3"),
                                       P(edgeL - 0.08f, BankTop + 0.07f), Vector2.one * U * 0.17f));
        var carrotSpr = StoryUI.Load("Fruit/carrot");
        var carrot = Track(StoryUI.Pic(root, "Carrot", carrotSpr ?? ShapeSprites.Triangle, carrotSpr != null ? Color.white : StoryUI.Hex("#FF8A3D"),
                                       P(edgeR + 0.08f, BankTop + 0.05f), Vector2.one * U * 0.13f));
        _rabbit = rabbit.rectTransform; _carrot = carrot.rectTransform;
        Run(StoryUI.Bob(_carrot, U * 0.012f, 3f, 0f, () => _carrot != null && !_locked));

        // 3 khúc gỗ: 1 dài hơn sông (đúng), 2 ngắn hơn (sai) — độ ngắn khác nhau để phân biệt được.
        float[] shortRatios = { 0.50f, 0.65f, 0.80f };
        var sr = (float[])shortRatios.Clone(); Shuffle(sr);
        var lens = new[] { gapPx * (1.18f + 0.12f * (float)rng.NextDouble()), gapPx * sr[0], gapPx * sr[1] };
        var order = new[] { 0, 1, 2 }; Shuffle(order);
        var labels = new string[3];
        int correctPos = 0;
        for (int i = 0; i < 3; i++) { labels[i] = order[i] == 0 ? "Dài" : "Ngắn"; if (order[i] == 0) correctPos = i; }
        Describe(info, $"CAU_{Mathf.RoundToInt(g * 100)}", "CauBacQua_DaiNgan", "Chọn khúc gỗ dài hơn con sông", labels, correctPos);
        _prompt.text = "Chọn khúc gỗ DÀI hơn sông!";
        ctx.Voice("cau_dai");

        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        var bars = new List<RectTransform>();
        float barH = H * 0.07f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float len = lens[order[i]];
            var pos = P(0.5f, 0.36f - i * 0.115f);
            var bar = Track(StoryUI.Pic(root, "Log" + i, ShapeSprites.RoundedRect, StoryUI.Hex("#A0652D"), pos, new Vector2(len, barH), raycast: true));
            bar.type = Image.Type.Sliced; bar.preserveAspect = false;
            Track(StoryUI.Pic(bar.transform, "CapL", ShapeSprites.Circle, StoryUI.Hex("#D9A066"), new Vector2(-len / 2f + barH * 0.35f, 0f), Vector2.one * barH * 0.62f));
            Track(StoryUI.Pic(bar.transform, "CapR", ShapeSprites.Circle, StoryUI.Hex("#D9A066"), new Vector2(len / 2f - barH * 0.35f, 0f), Vector2.one * barH * 0.62f));
            bars.Add(bar.rectTransform);
            StoryUI.OnTap(bar.gameObject, () =>
            {
                if (_locked) return;
                bool ok = idx == correctPos;
                _locked = true; hint = false;
                if (ok) { ctx.SfxRight(); Run(BridgeSuccess(bar.rectTransform, g, mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done)); }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(BridgeFail(bar, len, gapPx, () =>
                    {
                        _locked = false;
                        if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(bars[correctPos], () => hint && !_locked)); }
                    }));
                }
            });
        }
    }

    IEnumerator BridgeFail(Image bar, float len, float gapPx, Action unlock)
    {
        var rt = bar.rectTransform;
        yield return StoryUI.MoveTo(rt, P(0.5f, BankTop + 0.015f), 0.5f);
        Say("Khúc gỗ NGẮN quá! Rơi xuống nước rồi", P(0.5f, 0.80f), StoryUI.Hex("#FFE27A"), 1.5f);
        ctx.Sfx("plop");
        Run(StoryUI.MoveTo(rt, P(0.5f, 0.38f), 0.5f));
        yield return StoryUI.Fade(bar, 0f, 0.5f);
        if (rt != null) rt.gameObject.SetActive(false);
        unlock();
    }

    IEnumerator BridgeSuccess(RectTransform bar, float g, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return StoryUI.MoveTo(bar, P(0.5f, BankTop + 0.015f), 0.5f);
        Say("Khúc gỗ DÀI nên bắc qua được!", P(0.5f, 0.80f), Color.white, 2f);
        ctx.Sfx("wood");
        // Thỏ chạy qua cầu tới cà rốt.
        float rx = P(0.5f + g / 2f + 0.03f, 0f).x;
        Run(StoryUI.Bounce(_rabbit, 0.12f, 0.9f));
        yield return StoryUI.MoveTo(_rabbit, new Vector2(rx, _rabbit.anchoredPosition.y), 1.0f);
        ctx.Sfx("plop");
        if (_carrot != null) Run(StoryUI.ScaleTo(_carrot, 0f, 0.3f));
        Confetti(_rabbit.anchoredPosition, 10);
        yield return new WaitForSeconds(0.9f);
        done(firstTry, answer);
    }

    // ── B) Chui cửa to – vừa – bé ─────────────────────────────────────────────

    static readonly (string file, string name, int size)[] DoorAnimals =
    {
        ("chick", "Gà con", 0), ("dog", "Chó", 1), ("elephant", "Voi", 2),
    };
    static readonly string[] SizeWord = { "BÉ", "VỪA", "TO" };

    void BeginDoors(QuestionData info, Action<bool, int[]> done)
    {
        var an = DoorAnimals[Rand(0, DoorAnimals.Length)];
        var sizes = new[] { 0, 1, 2 }; Shuffle(sizes);   // sizes[i] = cỡ cửa ở vị trí i
        int correctPos = Array.IndexOf(sizes, an.size);
        var labels = new string[3];
        for (int i = 0; i < 3; i++) labels[i] = "Cửa " + SizeWord[sizes[i]].ToLower();
        Describe(info, $"CUA_{an.file}", "CauBacQua_ToBe", $"{an.name} {SizeWord[an.size].ToLower()}: đi cửa nào?", labels, correctPos);
        _prompt.text = $"{an.name} {SizeWord[an.size]} — đi cửa nào?";
        ctx.Voice("cua_" + an.file);

        float[] doorW = { 0.14f, 0.21f, 0.30f }, doorH = { 0.22f, 0.32f, 0.44f };
        float[] xs = { 0.20f, 0.50f, 0.80f };
        float baseY = 0.43f;                              // chân cửa
        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        var doors = new List<Image>();
        for (int i = 0; i < 3; i++)
        {
            int idx = i; int s = sizes[i];
            var size = new Vector2(doorW[s] * U, doorH[s] * U);
            var pos = new Vector2(P(xs[i], 0f).x, P(0f, baseY).y + size.y / 2f);
            var door = Track(StoryUI.Pic(root, "Door" + i, ShapeSprites.RoundedRect, StoryUI.Hex(s == 0 ? "#6FB7E9" : s == 1 ? "#F2B24C" : "#E0627F"), pos, size, raycast: true));
            door.type = Image.Type.Sliced; door.preserveAspect = false;
            Track(StoryUI.Pic(door.transform, "Knob", ShapeSprites.Circle, StoryUI.Hex("#FFF4D6"), new Vector2(size.x * 0.28f, 0f), Vector2.one * U * 0.025f));
            StoryUI.Label(door.transform, SizeWord[s], 26, Color.white, new Vector2(0f, size.y * 0.30f), new Vector2(size.x, size.y * 0.2f));
            doors.Add(door);
            StoryUI.OnTap(door.gameObject, () =>
            {
                if (_locked) return;
                if (idx == correctPos)
                {
                    _locked = true; hint = false;
                    ctx.SfxRight();
                    Run(DoorSuccess(animalRt, door, an.name, SizeWord[an.size], mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(StoryUI.Shake(door.rectTransform));
                    Say(s < an.size ? $"Cửa BÉ quá, {an.name} không lọt!" : $"Cửa TO quá, {an.name} tìm cửa vừa hơn nhé!", P(0.5f, 0.80f), StoryUI.Hex("#FFE27A"), 1.6f);
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(doors[correctPos].rectTransform, () => hint && !_locked)); }
                }
            });
        }

        // Con vật đứng dưới các cửa, to/bé đúng tỉ lệ để bé nhìn thấy mà so sánh.
        float[] animalSize = { 0.15f, 0.22f, 0.36f };
        var spr = StoryUI.Load("GameImages/Animal/" + an.file);
        var animal = Track(StoryUI.Pic(root, "Animal", spr ?? ShapeSprites.Circle, spr != null ? Color.white : StoryUI.Hex("#F6B26B"),
                                       P(0.5f, 0.20f), Vector2.one * U * animalSize[an.size]));
        animalRt = animal.rectTransform;
        animalRt.anchoredPosition = new Vector2(0f, P(0f, 0.17f).y + U * animalSize[an.size] / 2f - U * 0.04f);
        Run(StoryUI.Bob(animalRt, U * 0.012f, 3f, 0f, () => animalRt != null && !_locked));
    }

    RectTransform animalRt;

    IEnumerator DoorSuccess(RectTransform animal, Image door, string name, string size, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        Say($"{name} {size} đi cửa {size} vừa khít!", P(0.5f, 0.80f), Color.white, 2f);
        Run(StoryUI.ScaleTo(animal, 0.5f, 0.7f));
        yield return StoryUI.MoveTo(animal, door.rectTransform.anchoredPosition, 0.7f);
        ctx.Sfx("wood");
        if (animal != null) Run(StoryUI.Fade(animal.GetComponent<Image>(), 0f, 0.25f));
        Run(StoryUI.Bounce(door.rectTransform, 0.1f, 0.4f));
        Confetti(door.rectTransform.anchoredPosition, 10);
        yield return new WaitForSeconds(1.0f);
        done(firstTry, answer);
    }

    // ── tiện ích ──────────────────────────────────────────────────────────────

    Image Track(Image img) { _temp.Add(img.gameObject); return img; }
    Text Track(Text t) { _temp.Add(t.gameObject); return t; }

    void ClearTemp()
    {
        foreach (var g in _temp) if (g != null) UnityEngine.Object.Destroy(g);
        _temp.Clear();
        animalRt = null;
    }
}
