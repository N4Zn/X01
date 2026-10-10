using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Đếm khối hộp" (bé 3-4 tuổi, kiểu Cube Count của Big Brain Academy) — một đống khối hộp 3D (đẳng cự) hiện ra, bé chạm số
/// đúng trong 3 thẻ số. Số khối tăng dần theo số lượt đúng ngay lần đầu (1–3 → … → tối đa 10). Mọi khối đều nhìn thấy được
/// (không có khối bị che hoàn toàn). Sai 2 lần thì các khối được đếm "1, 2, 3..." lần lượt để gợi ý; đúng thì đếm to ăn mừng.
/// Hộp vẽ bằng mesh (StoryCube) — không cần ảnh. Giọng đọc: StoryVoice/dem_khoi, so_1..so_10 (tuỳ chọn).
/// </summary>
public sealed class DemKhoiWorld : StoryWorld
{
    static readonly string[] Palette = { "#FF7043", "#42A5F5", "#66BB6A", "#FFCA28", "#AB47BC", "#26C6DA" };

    readonly List<GameObject> _items = new List<GameObject>();
    Text _prompt;
    bool _active;
    int _ok, _gen;

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#F3E5F5"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#E1BEE7"), P(0.5f, 0.05f), new Vector2(W, H * 0.12f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.94f, H * 0.13f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;
        int gen = ++_gen;

        // Số khối tăng dần.
        int lv = _ok / 2;
        int lo = Mathf.Min(1 + lv, 4), hi = Mathf.Min(3 + 2 * lv, 10);
        int count = Rand(lo, hi + 1);

        var heights = MakeStack(count);
        var color = StoryUI.Hex(Palette[Rand(0, Palette.Length)]);

        // Đáp án: 3 số khác nhau, ≥ 1, gần đáp án đúng.
        var opts = new List<int> { count };
        int guard = 0;
        while (opts.Count < 3 && guard++ < 50)
        {
            int v = count + Rand(-2, 3);
            if (v >= 1 && v <= 12 && !opts.Contains(v)) opts.Add(v);
        }
        var arr = opts.ToArray(); Shuffle(arr);
        int correctPos = Array.IndexOf(arr, count);
        var labels = new string[arr.Length];
        for (int i = 0; i < arr.Length; i++) labels[i] = arr[i].ToString();
        Describe(info, $"KHOI_{count}", "DemKhoi_DemSo", $"Đếm {count} khối hộp", labels, correctPos);

        _prompt.text = "Có bao nhiêu khối hộp?";
        ctx.Voice("dem_khoi");

        // ── Dựng đống khối ───────────────────────────────────────────────────
        float s = Mathf.Min(W * 0.9f / 6.2f, H * 0.52f / 6.2f, U * 0.11f);   // nửa cạnh hộp; hộp vuông 2s × 2s
        var cells = new List<Vector3Int>();
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < heights[x, y]; z++) cells.Add(new Vector3Int(x, y, z));
        cells.Sort((a, b) => (a.x + a.y + a.z).CompareTo(b.x + b.y + b.z));    // vẽ xa → gần

        Vector2 Iso(Vector3Int c) => new Vector2((c.x - c.y) * s, -(c.x + c.y) * s * 0.5f + c.z * s);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var c in cells)
        {
            var p = Iso(c);
            minX = Mathf.Min(minX, p.x - s); maxX = Mathf.Max(maxX, p.x + s);
            minY = Mathf.Min(minY, p.y - s); maxY = Mathf.Max(maxY, p.y + s);
        }
        Vector2 center = P(0.5f, 0.60f) - new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        var cubes = new List<RectTransform>();
        for (int i = 0; i < cells.Count; i++)
        {
            var rt = StoryUI.Rect(root, "Cube", center + Iso(cells[i]), Vector2.one * 2f * s);
            var g = rt.gameObject.AddComponent<StoryCube>();
            g.color = color;
            g.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.35f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            _items.Add(rt.gameObject);
            cubes.Add(rt);
            rt.localScale = Vector3.zero;
        }
        Run(Appear(cubes, gen));

        // ── Thẻ số ───────────────────────────────────────────────────────────
        float cs = Mathf.Min(U * 0.22f, W * 0.26f);
        int mistakes = 0, firstWrong = -1;
        bool hint = false, answered = false, counting = false;
        var cards = new StoryCard[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            int idx = i;
            var card = StoryUI.Card(root, "Num_" + arr[i], null, arr[i].ToString(), StoryUI.Hex("#7E57C2"), P(0.20f + 0.30f * i, 0.17f), new Vector2(cs, cs), null, 72);
            cards[i] = card;
            _items.Add(card.rt.gameObject);
            Run(StoryUI.PopIn(card.rt, 0.3f));
            StoryUI.OnTap(card.rt.gameObject, () =>
            {
                if (!_active || answered) return;
                if (idx == correctPos)
                {
                    answered = true; hint = false;
                    ctx.SfxRight();
                    card.bg.color = StoryUI.Hex("#43A047");
                    Run(StoryUI.Bounce(card.rt, 0.2f, 0.5f));
                    Run(CountAloud(cubes, center, s, gen, () =>
                    {
                        if (gen != _gen) return;
                        Confetti(center + new Vector2(0, s), 14);
                        Say($"{count} khối hộp!", P(0.5f, 0.38f), StoryUI.Hex("#4A148C"), 1.6f);
                        ctx.Voice("so_" + count);
                        Run(Finish(mistakes == 0, new[] { mistakes == 0 ? correctPos : firstWrong }, done));
                    }));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.Sfx("tap");
                    Run(StoryUI.Shake(card.rt, 0.35f, 10f));
                    if (mistakes >= 2 && !counting)
                    {
                        counting = true;   // gợi ý: đếm lần lượt từng khối (1 lần)
                        Run(CountAloud(cubes, center, s, gen, null));
                        if (!hint) { hint = true; Run(StoryUI.Pulse(cards[correctPos].rt, () => hint && !answered, 0.1f, 7f)); }
                    }
                }
            });
        }
    }

    // Sinh heightmap 3×3 (cao ≤ 3) với đúng `count` khối, mỗi khối nhìn thấy ít nhất 1 mặt.
    int[,] MakeStack(int count)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            var h = new int[3, 3];
            int placed = 0;
            int x0 = Rand(0, 3), y0 = Rand(0, 3);
            h[x0, y0] = 1; placed = 1;
            int guard = 0;
            while (placed < count && guard++ < 200)
            {
                int x = Rand(0, 3), y = Rand(0, 3);
                if (h[x, y] >= 3) continue;
                bool support = h[x, y] > 0;
                if (!support)
                {
                    // ô mới phải kề một cột đã có (kể cả chéo)
                    for (int dx = -1; dx <= 1 && !support; dx++)
                    for (int dy = -1; dy <= 1 && !support; dy++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < 3 && ny >= 0 && ny < 3 && h[nx, ny] > 0) support = true;
                    }
                }
                if (!support) continue;
                h[x, y]++; placed++;
            }
            if (placed == count && NoHidden(h)) return h;
        }
        var fallback = new int[3, 3];
        for (int i = 0; i < count; i++) fallback[i % 3, (i / 3) % 3]++;
        return fallback;
    }

    static bool NoHidden(int[,] h)
    {
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < h[x, y]; z++)
        {
            bool top = h[x, y] > z + 1;
            bool right = x + 1 < 3 && h[x + 1, y] > z;
            bool left = y + 1 < 3 && h[x, y + 1] > z;
            if (top && right && left) return false;
        }
        return true;
    }

    IEnumerator Appear(List<RectTransform> cubes, int gen)
    {
        foreach (var c in cubes)
        {
            if (gen != _gen || c == null) yield break;
            Run(StoryUI.PopIn(c, 0.25f));
            yield return new WaitForSeconds(0.07f);
        }
    }

    // Số 1, 2, 3... nổi lần lượt trên từng khối.
    IEnumerator CountAloud(List<RectTransform> cubes, Vector2 center, float s, int gen, Action onDone)
    {
        var badges = new List<GameObject>();
        for (int i = 0; i < cubes.Count; i++)
        {
            if (gen != _gen || cubes[i] == null) yield break;
            var pos = cubes[i].anchoredPosition + new Vector2(0, s * 0.5f);
            var b = StoryUI.Pic(root, "Badge", ShapeSprites.Circle, new Color(0.15f, 0.1f, 0.3f, 0.85f), pos, Vector2.one * s * 0.85f);
            StoryUI.Label(b.transform, (i + 1).ToString(), Mathf.RoundToInt(s * 0.7f), Color.white, Vector2.zero, Vector2.one * s * 0.85f);
            badges.Add(b.gameObject);
            _items.Add(b.gameObject);
            Run(StoryUI.PopIn(b.rectTransform, 0.2f));
            ctx.Sfx("tap");
            yield return new WaitForSeconds(0.4f);
        }
        onDone?.Invoke();
        yield return new WaitForSeconds(1.6f);
        foreach (var b in badges) if (b != null) UnityEngine.Object.Destroy(b);
    }

    IEnumerator Finish(bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(2.2f);
        if (firstTry) _ok++;
        done(firstTry, answer);
    }

    void Clear()
    {
        _active = false;
        _gen++;
        foreach (var e in _items) if (e != null) UnityEngine.Object.Destroy(e);
        _items.Clear();
    }
}
