using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Cổng hình" — nhận biết hình tròn / vuông / tam giác / chữ nhật bằng cách ĐI QUA CỔNG: 4 cổng khổng lồ hình
/// khác nhau quanh 1 con vật. Nghe/đọc tên hình rồi dậm vào đúng cổng → con vật chạy xuyên qua, cổng sáng lên.
/// Độ khó tăng dần trong 1 ván: 1 cổng → 2 cổng liên tiếp → 3 cổng liên tiếp (nhớ thứ tự). Vị trí cổng xáo mỗi lượt
/// (không thể nhớ "góc trái là hình tròn"). Sai thì cổng rung + nói tên hình vừa chạm, không mất tiến độ.
/// Không cần ảnh — hình vẽ bằng code; con vật dùng GameImages/Animal/*.
/// </summary>
public sealed class CongHinhWorld : StoryWorld
{
    enum Kind { Tron, Vuong, TamGiac, ChuNhat }
    static readonly string[] KindName = { "Hình tròn", "Hình vuông", "Hình tam giác", "Hình chữ nhật" };
    static readonly string[] ShortName = { "tròn", "vuông", "tam giác", "chữ nhật" };
    static readonly string[] KindVoice = { "hinh_tron", "hinh_vuong", "hinh_tamgiac", "hinh_chunhat" };
    static readonly string[] KindHex = { "#FF8A3D", "#4D96FF", "#E0453F", "#2FB26B" };
    static readonly string[] AnimalFiles = { "rabbit", "dog", "duck", "pig", "chick", "bear" };

    static readonly Vector2[] Slots = { new Vector2(0.26f, 0.64f), new Vector2(0.74f, 0.64f), new Vector2(0.26f, 0.26f), new Vector2(0.74f, 0.26f) };

    sealed class Gate { public Kind kind; public Image outer, inner; public Vector2 center; }

    readonly List<GameObject> _temp = new List<GameObject>();
    readonly List<Gate> _gates = new List<Gate>();
    readonly List<Image> _seqIcons = new List<Image>();
    Text _prompt;
    int _taskCount;
    bool _locked;
    RectTransform _animal;

    protected override void Build()
    {
        StoryUI.Fill(root, "Sky", StoryUI.Hex("#D8F0FF"));
        StoryUI.Pic(root, "Ground", ShapeSprites.Square, StoryUI.Hex("#8ED36B"), P(0.5f, 0.08f), new Vector2(W, H * 0.18f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 34, Color.white, P(0.5f, 0.935f), new Vector2(W * 0.98f, H * 0.10f));
    }

    static string SpriteKey(Kind k) => k == Kind.Tron ? "circle" : k == Kind.TamGiac ? "triangle" : "square";

    static Vector2 SizeOf(Kind k, float s)
    {
        switch (k)
        {
            case Kind.ChuNhat: return new Vector2(s * 1.2f, s * 0.78f);
            case Kind.TamGiac: return new Vector2(s * 1.05f, s * 0.95f);
            default: return new Vector2(s, s);
        }
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearTemp();
        _locked = false;
        int task = _taskCount++;
        int len = task < 3 ? 1 : task < 6 ? 2 : 3;

        // Xáo vị trí 4 hình vào 4 góc.
        var kinds = new[] { Kind.Tron, Kind.Vuong, Kind.TamGiac, Kind.ChuNhat }; Shuffle(kinds);
        var seq = new List<Kind>();
        var pool = (Kind[])kinds.Clone(); Shuffle(pool);
        for (int i = 0; i < len; i++) seq.Add(pool[i]);

        float s = U * 0.27f;
        _gates.Clear();
        for (int i = 0; i < 4; i++)
        {
            var g = new Gate { kind = kinds[i], center = P(Slots[i].x, Slots[i].y) };
            var spr = ShapeSprites.ByName(SpriteKey(g.kind));
            var size = SizeOf(g.kind, s);
            g.outer = Track(StoryUI.Pic(root, "Gate" + i, spr, StoryUI.Hex(KindHex[(int)g.kind]), g.center, size, raycast: true));
            g.outer.preserveAspect = false;
            g.inner = Track(StoryUI.Pic(g.outer.transform, "Opening", spr, StoryUI.Hex("#1B2442"), g.kind == Kind.TamGiac ? new Vector2(0f, -size.y * 0.06f) : Vector2.zero, size * 0.62f));
            g.inner.preserveAspect = false;
            Track(StoryUI.Label(root, KindName[(int)g.kind], 26, Color.white, g.center + new Vector2(0f, -size.y * 0.5f - U * 0.045f), new Vector2(U * 0.5f, U * 0.08f)));
            _gates.Add(g);
        }

        // Con vật ở giữa.
        var an = AnimalFiles[Rand(0, AnimalFiles.Length)];
        var aspr = StoryUI.Load("GameImages/Animal/" + an);
        var animalImg = Track(StoryUI.Pic(root, "Animal", aspr ?? ShapeSprites.Circle, aspr != null ? Color.white : StoryUI.Hex("#F6B26B"), P(0.5f, 0.45f), Vector2.one * U * 0.17f));
        _animal = animalImg.rectTransform;
        animalImg.transform.SetAsLastSibling();
        Run(StoryUI.Bob(_animal, U * 0.012f, 3f, 0f, () => _animal != null && !_locked));

        // Hàng hình cần đi qua (hiện ở trên) + câu hướng dẫn.
        _seqIcons.Clear();
        float iconS = U * 0.075f, gap = U * 0.095f;
        for (int i = 0; i < seq.Count; i++)
        {
            var spr = ShapeSprites.ByName(SpriteKey(seq[i]));
            var pos = P(0.5f, 0.835f) + new Vector2((i - (seq.Count - 1) * 0.5f) * gap * 1.3f, 0f);
            var icon = Track(StoryUI.Pic(root, "Seq" + i, spr, StoryUI.Hex(KindHex[(int)seq[i]]), pos, SizeOf(seq[i], iconS)));
            icon.preserveAspect = false;
            _seqIcons.Add(icon);
        }
        var names = new List<string>();
        foreach (var k in seq) names.Add(ShortName[(int)k]);
        _prompt.text = len == 1 ? $"Tìm cổng hình {names[0]}" : "Đi qua cổng: " + string.Join(", rồi ", names);
        ctx.Voice(len == 1 ? KindVoice[(int)seq[0]] : "cong_di_qua");

        // Log: đáp án = tên 4 hình theo vị trí cổng; đúng = các vị trí theo thứ tự cần đi.
        var labels = new string[4];
        for (int i = 0; i < 4; i++) labels[i] = KindName[(int)kinds[i]];
        var correctPos = new int[len];
        for (int i = 0; i < len; i++) correctPos[i] = Array.IndexOf(kinds, seq[i]);
        Describe(info, $"CONG_{len}_{string.Join("", seq.ConvertAll(k => ((int)k).ToString()))}", "CongHinh_NhanBietHinh",
                 len == 1 ? $"Tìm cổng hình {names[0]}" : "Đi qua cổng: " + string.Join(" > ", names), labels, correctPos[0]);
        info.correctAnswers = correctPos;

        int step = 0, mistakes = 0, firstWrong = -1;
        bool hint = false;
        Run(StoryUI.Pulse(_seqIcons[0].rectTransform, () => step == 0 && !_locked, 0.18f, 6f));
        for (int gi = 0; gi < 4; gi++)
        {
            int idx = gi;
            var gate = _gates[gi];
            StoryUI.OnTap(gate.outer.gameObject, () =>
            {
                if (_locked) return;
                if (gate.kind == seq[step])
                {
                    hint = false;
                    ctx.SfxRight();
                    int myStep = step++;
                    bool finished = step >= len;
                    if (finished) _locked = true;
                    Run(PassThrough(gate, myStep, finished, mistakes == 0, mistakes == 0 ? correctPos : new[] { firstWrong }, done, seq));
                    if (!finished)
                    {
                        int cur = step;
                        Run(StoryUI.Pulse(_seqIcons[cur].rectTransform, () => step == cur && !_locked, 0.18f, 6f));
                    }
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(StoryUI.Shake(gate.outer.rectTransform, 0.35f, 12f));
                    Say($"Đây là {KindName[(int)gate.kind].ToLower()}. Tìm hình {ShortName[(int)seq[step]]} nhé!", P(0.5f, 0.74f), StoryUI.Hex("#FFE27A"), 1.5f);
                    if (mistakes >= 2 && !hint)
                    {
                        hint = true;
                        var target = _gates.Find(x => x.kind == seq[step]);
                        if (target != null) Run(StoryUI.Pulse(target.outer.rectTransform, () => hint && !_locked, 0.15f, 7f));
                    }
                }
            });
        }
    }

    IEnumerator PassThrough(Gate gate, int stepIdx, bool finished, bool firstTry, int[] answer, Action<bool, int[]> done, List<Kind> seq)
    {
        // Con vật chạy tới cổng, cổng sáng, rồi quay lại giữa (hoặc ăn mừng nếu qua hết).
        if (stepIdx < _seqIcons.Count) Run(StoryUI.Fade(_seqIcons[stepIdx], 0.35f, 0.3f));
        Run(StoryUI.ColorTo(gate.inner, StoryUI.Hex("#FFF3A0"), 0.25f));
        ctx.Sfx("up");
        yield return StoryUI.MoveTo(_animal, gate.center, 0.45f);
        Run(StoryUI.Bounce(gate.outer.rectTransform, 0.12f, 0.4f));
        Say($"{KindName[(int)gate.kind]}!", P(0.5f, 0.74f), Color.white, 1.0f);
        if (finished)
        {
            Confetti(gate.center, 14);
            yield return new WaitForSeconds(1.2f);
            done(firstTry, answer);
        }
        else
        {
            yield return StoryUI.MoveTo(_animal, P(0.5f, 0.45f), 0.4f);
        }
    }

    Image Track(Image img) { _temp.Add(img.gameObject); return img; }
    Text Track(Text t) { _temp.Add(t.gameObject); return t; }

    void ClearTemp()
    {
        foreach (var g in _temp) if (g != null) UnityEngine.Object.Destroy(g);
        _temp.Clear();
        _animal = null;
    }
}
