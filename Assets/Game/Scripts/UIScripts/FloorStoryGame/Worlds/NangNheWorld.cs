using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Nặng hay nhẹ" (bé 3-4 tuổi, kiểu Heavyweight của Big Brain Academy) — các con vật đứng trên bập bênh; bên NẶNG hơn chúi xuống.
/// Mức 1: 1 bập bênh, 2 con → chạm con NẶNG hơn (thỉnh thoảng hỏi NHẸ hơn). Mức 2 (sau 4 lượt đúng ngay lần đầu):
/// 3 con trên 2 bập bênh nối nhau (A–B, B–C) → chạm con nặng nhất / nhẹ nhất (phải suy ra qua con ở giữa).
/// Con vật chia 3 nhóm nặng–vừa–nhẹ nên so sánh luôn rõ ràng. Ảnh: GameImages/Animal/* (có sẵn).
/// </summary>
public sealed class NangNheWorld : StoryWorld
{
    sealed class Item { public string file, vn; public int tier; public Item(string f, string v, int t) { file = f; vn = v; tier = t; } }

    static readonly Item[][] Tiers =
    {
        new[] { new Item("rabbit", "Thỏ", 1), new Item("duck", "Vịt", 1), new Item("owl", "Cú mèo", 1), new Item("frog", "Ếch", 1), new Item("chick", "Gà con", 1), new Item("parrot", "Vẹt", 1) },
        new[] { new Item("bear", "Gấu", 2), new Item("pig", "Lợn", 2), new Item("goat", "Dê", 2), new Item("dog", "Chó", 2), new Item("monkey", "Khỉ", 2), new Item("penguin", "Cánh cụt", 2) },
        new[] { new Item("elephant", "Voi", 3), new Item("hippo", "Hà mã", 3), new Item("rhino", "Tê giác", 3), new Item("buffalo", "Trâu", 3), new Item("cow", "Bò", 3), new Item("horse", "Ngựa", 3) },
    };

    sealed class Seesaw
    {
        public Vector2 pivot;
        public RectTransform plank;
        public readonly RectTransform[] animal = new RectTransform[2];
        public readonly Vector2[] local = new Vector2[2];
        public void SetAngle(float deg)
        {
            var q = Quaternion.Euler(0, 0, deg);
            plank.localRotation = q;
            for (int i = 0; i < 2; i++)
                if (animal[i] != null) animal[i].anchoredPosition = pivot + (Vector2)(q * (Vector3)local[i]);
        }
    }

    readonly List<GameObject> _items = new List<GameObject>();
    Text _prompt;
    bool _active;
    int _ok, _count;

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#E1F5FE"));
        StoryUI.Pic(root, "Hill", ShapeSprites.Circle, StoryUI.Hex("#A5D6A7"), P(0.5f, -0.22f), new Vector2(W * 1.5f, H * 0.7f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.94f, H * 0.13f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;
        _count++;

        int n = _ok < 4 ? 2 : 3;
        bool light = _ok >= 2 && _count % 3 == 0;

        // Chọn n con ở n nhóm khác nhau.
        var tierIdx = new List<int> { 0, 1, 2 };
        int[] chosenTiers;
        if (n == 3) chosenTiers = new[] { 0, 1, 2 };
        else if (_ok < 2) chosenTiers = new[] { 0, 2 };
        else
        {
            var t = tierIdx.ToArray(); Shuffle(t);
            chosenTiers = new[] { t[0], t[1] };
        }
        var items = new Item[n];
        for (int i = 0; i < n; i++) items[i] = Tiers[chosenTiers[i]][Rand(0, Tiers[chosenTiers[i]].Length)];
        // Xáo thứ tự để vị trí không lộ nặng/nhẹ.
        Shuffle(items);

        int target = 0;
        for (int i = 1; i < n; i++)
            if (light ? items[i].tier < items[target].tier : items[i].tier > items[target].tier) target = i;

        string word = n == 2 ? (light ? "NHẸ hơn" : "NẶNG hơn") : (light ? "NHẸ nhất" : "NẶNG nhất");
        var names = new string[n];
        for (int i = 0; i < n; i++) names[i] = items[i].vn;
        Describe(info, $"NANG_{items[target].file}_{(light ? "nhe" : "nang")}{n}", "NangNhe_SoSanhKhoiLuong",
                 $"Con nào {word.ToLower()}: {string.Join(", ", names)}", names, target);

        _prompt.text = $"Con nào {word}?";
        ctx.Voice((light ? "nhe_" : "nang_") + (n == 2 ? "hon" : "nhat"));

        // ── Dựng bập bênh ────────────────────────────────────────────────────
        var seesaws = new List<Seesaw>();
        var inst = new List<(RectTransform rt, int item)>();   // mọi con vật trên bập bênh
        float size = n == 2 ? U * 0.30f : U * 0.20f;
        float plankLen = W * (n == 2 ? 0.86f : 0.80f);
        float thick = U * 0.04f;
        float[] pivotY = n == 2 ? new[] { 0.30f } : new[] { 0.60f, 0.25f };
        int[][] pairs = n == 2 ? new[] { new[] { 0, 1 } } : new[] { new[] { 0, 1 }, new[] { 1, 2 } };

        bool ready = false, answered = false, hint = false;
        int mistakes = 0, firstWrong = -1;

        for (int s = 0; s < pairs.Length; s++)
        {
            var sw = new Seesaw { pivot = P(0.5f, pivotY[s]) };
            // chân đỡ tam giác
            var tri = StoryUI.Pic(root, "Pivot", ShapeSprites.Triangle, StoryUI.Hex("#8D6E63"), sw.pivot + new Vector2(0, -U * 0.055f), new Vector2(U * 0.14f, U * 0.13f));
            _items.Add(tri.gameObject);
            var plank = StoryUI.Pic(root, "Plank", ShapeSprites.RoundedRect, StoryUI.Hex("#C98B4B"), sw.pivot, new Vector2(plankLen, thick));
            plank.type = Image.Type.Sliced; plank.preserveAspect = false;
            sw.plank = plank.rectTransform;
            _items.Add(plank.gameObject);

            bool swap = Rand(0, 2) == 0;
            for (int side = 0; side < 2; side++)
            {
                int itemIdx = pairs[s][swap ? 1 - side : side];
                var it = items[itemIdx];
                var spr = StoryUI.Load("GameImages/Animal/" + it.file);
                var img = StoryUI.Pic(root, "Animal_" + it.file, spr ?? ShapeSprites.Circle, spr != null ? Color.white : StoryUI.Hex("#F6B26B"),
                                      sw.pivot, Vector2.one * size, raycast: true);
                if (n == 2)
                    StoryUI.Label(img.transform, it.vn, 30, Color.white, new Vector2(0, size * 0.62f), new Vector2(size * 1.6f, size * 0.3f));
                sw.animal[side] = img.rectTransform;
                sw.local[side] = new Vector2((side == 0 ? -1 : 1) * plankLen * 0.32f, thick * 0.5f + size * 0.5f);
                _items.Add(img.gameObject);
                inst.Add((img.rectTransform, itemIdx));
                int ii = itemIdx;
                StoryUI.OnTap(img.gameObject, () =>
                {
                    if (!_active || !ready || answered) return;
                    if (ii == target)
                    {
                        answered = true; hint = false;
                        ctx.SfxRight();
                        foreach (var (rt, idx) in inst)
                            if (idx == target) { Run(StoryUI.Bounce(rt, 0.25f, 0.6f)); Confetti(rt.anchoredPosition, 8, sound: false); }
                        Say($"{items[target].vn} {(light ? "nhẹ" : "nặng")} {(n == 2 ? "hơn" : "nhất")}!", P(0.5f, 0.10f), StoryUI.Hex("#1B5E20"), 1.8f);
                        Run(Finish(mistakes == 0, new[] { mistakes == 0 ? target : firstWrong }, done));
                    }
                    else
                    {
                        mistakes++;
                        if (firstWrong < 0) firstWrong = ii;
                        ctx.Sfx("tap");
                        Run(StoryUI.Shake(img.rectTransform, 0.35f, 10f));
                        Say("Nhìn bên nào chúi xuống nhé!", P(0.5f, 0.10f), StoryUI.Hex("#BF360C"), 1.4f);
                        if (mistakes >= 2 && !hint)
                        {
                            hint = true;
                            foreach (var (rt, idx) in inst)
                                if (idx == target) Run(StoryUI.Pulse(rt, () => hint && !answered, 0.14f, 7f));
                        }
                    }
                });
            }
            sw.SetAngle(0f);
            seesaws.Add(sw);
        }

        foreach (var (rt, _) in inst) Run(StoryUI.PopIn(rt, 0.35f));
        Run(Tilt(items, pairs, seesaws, () => ready = true));
    }

    // Bập bênh đứng cân bằng lắc nhẹ, rồi bên nặng chúi xuống.
    IEnumerator Tilt(Item[] items, int[][] pairs, List<Seesaw> seesaws, Action onReady)
    {
        const float A = 15f;
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float w = Mathf.Sin(t * 14f) * 2.5f;
            foreach (var sw in seesaws) if (sw.plank != null) sw.SetAngle(w);
            yield return null;
        }
        var targets = new float[seesaws.Count];
        for (int s = 0; s < seesaws.Count; s++)
        {
            // animal[0] ở bên trái; item của nó = items[pairs[s][?]] — suy ra từ tên ảnh trên con vật.
            var left = items[IndexOfAnimal(items, seesaws[s].animal[0])];
            var right = items[IndexOfAnimal(items, seesaws[s].animal[1])];
            targets[s] = left.tier > right.tier ? A : -A;     // góc dương = trái chúi xuống
        }
        ctx.Sfx("wood");
        const float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float e = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);   // OutBack
            for (int s = 0; s < seesaws.Count; s++) if (seesaws[s].plank != null) seesaws[s].SetAngle(targets[s] * e);
            yield return null;
        }
        for (int s = 0; s < seesaws.Count; s++) if (seesaws[s].plank != null) seesaws[s].SetAngle(targets[s]);
        onReady();
    }

    static int IndexOfAnimal(Item[] items, RectTransform rt)
    {
        string nm = rt.name.Substring("Animal_".Length);
        for (int i = 0; i < items.Length; i++) if (items[i].file == nm) return i;
        return 0;
    }

    IEnumerator Finish(bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(2.4f);
        if (firstTry) _ok++;
        done(firstTry, answer);
    }

    void Clear()
    {
        _active = false;
        foreach (var e in _items) if (e != null) UnityEngine.Object.Destroy(e);
        _items.Clear();
    }
}
