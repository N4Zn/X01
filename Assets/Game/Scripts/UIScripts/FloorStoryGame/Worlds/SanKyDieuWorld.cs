using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Sàn kỳ diệu" (bé nhỏ 3-4 tuổi) — KHÔNG đúng/sai: dậm/chạm chỗ nào thì chỗ đó mọc hoa / sao / tim / bong bóng /
/// cây nấm kèm âm thanh. Học quan hệ nhân-quả, làm quen với sàn tương tác. Mỗi chủ đề chạm đủ 6 lần = 1 lượt
/// (+1 điểm), rồi sang chủ đề kế (đổi nền + hình). Vật hiện ra tự mờ dần nên màn hình không đầy.
/// Hình vẽ bằng code (ShapeSprites); hoa/cây dùng Kenney NatureKit nếu có.
/// </summary>
public sealed class SanKyDieuWorld : StoryWorld
{
    sealed class Theme
    {
        public string key, prompt, bgHex, sfx;
        public Func<SanKyDieuWorld, (Sprite sprite, Color color, float size)> make;
    }

    static readonly string[] FlowerFiles = { "flower_redA", "flower_redB", "flower_yellowA", "flower_yellowC", "flower_purpleA", "flower_purpleB" };
    static readonly string[] GardenFiles = { "tree_oak", "tree_default", "mushroom_red", "plant_bush", "flower_redC", "tree_oak_dark" };
    static readonly string[] Pinks = { "#FF6FA5", "#FF9AB8", "#E8457C", "#FFB3C9" };
    static readonly string[] Brights = { "#FFD93D", "#FF6B6B", "#6BCB77", "#4D96FF", "#FF9F45", "#C77DFF" };

    readonly List<Theme> _themes = new List<Theme>();
    readonly List<GameObject> _spawned = new List<GameObject>();
    Image _bg;
    Text _prompt;
    int _themeIndex = -1;
    Theme _theme;
    int _touches;
    bool _doneCalled;
    Action<bool, int[]> _done;
    float _lastSfx;
    const int TouchesPerRound = 6;

    protected override void Build()
    {
        _bg = StoryUI.Fill(root, "Bg", StoryUI.Hex("#CFF5C0"));
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.95f, H * 0.12f));

        _themes.Add(new Theme
        {
            key = "hoa", prompt = "Dậm chân cho hoa nở!", bgHex = "#CFF5C0", sfx = "up",
            make = w => { var s = StoryUI.Load("NatureKit/Isometric/" + FlowerFiles[w.Rand(0, FlowerFiles.Length)] + "_NE");
                          return s != null ? (s, Color.white, w.U * 0.20f) : (ShapeSprites.Flower, StoryUI.Hex(Pinks[w.Rand(0, Pinks.Length)]), w.U * 0.16f); }
        });
        _themes.Add(new Theme
        {
            key = "sao", prompt = "Dậm chân cho sao sáng!", bgHex = "#1C2B63", sfx = "star",
            make = w => (ShapeSprites.Star, StoryUI.Hex(Brights[w.Rand(0, 2)]), w.U * (0.10f + 0.07f * (float)w.rng.NextDouble()))
        });
        _themes.Add(new Theme
        {
            key = "tim", prompt = "Dậm chân cho tim bay!", bgHex = "#FFD9E8", sfx = "plop",
            make = w => (ShapeSprites.Heart, StoryUI.Hex(Pinks[w.Rand(0, Pinks.Length)]), w.U * (0.10f + 0.07f * (float)w.rng.NextDouble()))
        });
        _themes.Add(new Theme
        {
            key = "bong", prompt = "Dậm chân cho bóng nổi!", bgHex = "#BFE9FF", sfx = "pop",
            make = w => { var c = StoryUI.Hex(Brights[w.Rand(0, Brights.Length)]); c.a = 0.75f; return (ShapeSprites.Circle, c, w.U * (0.09f + 0.09f * (float)w.rng.NextDouble())); }
        });
        _themes.Add(new Theme
        {
            key = "vuon", prompt = "Dậm chân cho vườn mọc lên!", bgHex = "#DFF3B8", sfx = "wood",
            make = w => { var s = StoryUI.Load("NatureKit/Isometric/" + GardenFiles[w.Rand(0, GardenFiles.Length)] + "_NE");
                          return s != null ? (s, Color.white, w.U * 0.26f) : (ShapeSprites.Flower, StoryUI.Hex(Brights[w.Rand(0, Brights.Length)]), w.U * 0.16f); }
        });

        StoryUI.TapArea(root, OnTapAt);
        _prompt.transform.SetAsLastSibling();
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        _themeIndex = (_themeIndex + 1) % _themes.Count;
        _theme = _themes[_themeIndex];
        _touches = 0;
        _doneCalled = false;
        _done = done;
        Run(StoryUI.ColorTo(_bg, StoryUI.Hex(_theme.bgHex), 0.6f));
        _prompt.text = _theme.prompt;
        ctx.Voice("san_" + _theme.key);
        Describe(info, "SAN_" + _theme.key, "SanKyDieu", "Chạm sàn tạo " + _theme.key, new[] { "chạm" }, 0);
        foreach (var g in _spawned) if (g != null) UnityEngine.Object.Destroy(g);
        _spawned.Clear();
    }

    void OnTapAt(Vector2 local)
    {
        if (_theme == null) return;     // chưa bắt đầu lượt
        var (sprite, color, size) = _theme.make(this);
        var img = StoryUI.Pic(root, "Spawn", sprite, color, local, Vector2.one * size);
        img.transform.SetSiblingIndex(root.childCount - 2);   // dưới chữ hướng dẫn
        _spawned.Add(img.gameObject);
        Run(StoryUI.PopIn(img.rectTransform, 0.3f));
        if (_theme.key == "bong") Run(StoryUI.MoveTo(img.rectTransform, local + new Vector2(0f, U * 0.25f), 2f));
        Run(FadeAndDestroy(img, 2.4f, 0.8f));
        Confetti(local, 5, sound: false);
        if (Time.unscaledTime - _lastSfx > 0.08f) { _lastSfx = Time.unscaledTime; ctx.Sfx(_theme.sfx, 0.8f); }

        _touches++;
        if (_touches == TouchesPerRound && !_doneCalled)
        {
            _doneCalled = true;
            Say("Giỏi quá!", P(0.5f, 0.55f), Color.white, 1.2f);
            ctx.SfxRight();
            Run(FinishSoon());
        }
    }

    IEnumerator FinishSoon()
    {
        yield return new WaitForSeconds(0.9f);
        _done?.Invoke(true, new[] { 0 });
    }
}
