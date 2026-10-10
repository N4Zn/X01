using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Lật thẻ giống nhau" (bé 3 tuổi) — trí nhớ. Các thẻ úp; bé chạm 2 thẻ (mỗi lần chỉ 2): giống nhau thì "ăn" (cặp biến mất)
/// và +1 điểm NGAY, khác nhau thì sau 1.5 giây tự úp lại. Bảng đầu chỉ 4 thẻ (2 cặp), mỗi bảng xong thêm 1 cặp, tối đa 12 thẻ.
/// Xong bảng mà không lật nhầm lần nào: thưởng thêm 1 điểm (kết quả "đúng ngay lần đầu" của Kit). Không cần ảnh.
/// </summary>
public sealed class LatTheWorld : StoryWorld
{
    const float MismatchShowSeconds = 1.5f;

    sealed class Card
    {
        public RectTransform holder;
        public GameObject back, front;
        public string key;
        public bool faceUp, matched;
    }

    readonly List<Card> _cards = new List<Card>();
    readonly List<GameObject> _items = new List<GameObject>();
    Text _prompt;
    bool _active;
    int _boards;   // số bảng đã xong → quyết định số cặp

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#E8F5E9"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#C8E6C9"), P(0.5f, 0.05f), new Vector2(W, H * 0.12f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 38, Color.white, P(0.5f, 0.93f), new Vector2(W * 0.94f, H * 0.1f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;

        int pairs = Mathf.Min(6, 2 + _boards);
        int total = pairs * 2;
        int cols, rows;
        switch (pairs)
        {
            case 2: cols = 2; rows = 2; break;
            case 3: cols = 3; rows = 2; break;
            case 4: cols = 4; rows = 2; break;
            case 5: cols = 5; rows = 2; break;
            default: cols = 4; rows = 3; break;
        }

        var keys = (string[])StoryIcons.Keys.Clone(); Shuffle(keys);
        var faces = new string[total];
        for (int i = 0; i < pairs; i++) { faces[2 * i] = keys[i]; faces[2 * i + 1] = keys[i]; }
        Shuffle(faces);

        Describe(info, $"LATTHE_{pairs}", "LatThe_TriNho", $"Lật thẻ giống nhau: {pairs} cặp",
                 new[] { string.Join(",", faces) }, 0);

        _prompt.text = pairs == 2 && _boards == 0 ? "Chạm 2 thẻ giống nhau nhé!" : "Tìm các cặp giống nhau!";

        float areaW = W * 0.94f, areaH = H * 0.76f;
        float cs = Mathf.Min(areaW / cols, areaH / rows) * 0.88f;
        float stepX = areaW / cols, stepY = areaH / rows;
        Vector2 origin = P(0.5f, 0.47f);

        int mistakes = 0, matched = 0;
        bool busy = false;
        var open = new List<Card>();
        for (int i = 0; i < total; i++)
        {
            int r = i / cols, c = i % cols;
            var pos = origin + new Vector2((c - (cols - 1) * 0.5f) * stepX, ((rows - 1) * 0.5f - r) * stepY);
            var card = MakeCard(faces[i], pos, cs);
            _cards.Add(card);
            Run(StoryUI.PopIn(card.holder, 0.25f));
            StoryUI.OnTap(card.holder.gameObject, () =>
            {
                if (!_active || busy || card.matched || card.faceUp) return;
                ctx.Sfx("tap");
                Run(Flip(card, true));
                open.Add(card);
                if (open.Count < 2) return;

                busy = true;
                var a = open[0]; var b = open[1];
                open.Clear();
                Run(Resolve(a, b));
            });
        }

        IEnumerator Resolve(Card a, Card b)
        {
            yield return new WaitForSeconds(0.45f);
            if (!_active) yield break;
            if (a.key == b.key)
            {
                a.matched = b.matched = true;
                matched++;
                ctx.SfxRight();
                AddPoint();
                Confetti(a.holder.anchoredPosition, 6, sound: false);
                Confetti(b.holder.anchoredPosition, 6, sound: false);
                Run(StoryUI.Bounce(a.holder, 0.25f, 0.4f));
                yield return StoryUI.Bounce(b.holder, 0.25f, 0.4f);
                Run(StoryUI.ScaleTo(a.holder, 0f, 0.25f));
                yield return StoryUI.ScaleTo(b.holder, 0f, 0.25f);
                if (a.holder != null) a.holder.gameObject.SetActive(false);
                if (b.holder != null) b.holder.gameObject.SetActive(false);
                busy = false;
                if (matched >= pairs)
                {
                    _boards++;
                    bool perfect = mistakes == 0;
                    Confetti(Vector2.zero, 16);
                    Say(perfect ? "Giỏi quá! Không nhầm lần nào!" : "Tìm đủ rồi!", P(0.5f, 0.5f), StoryUI.Hex("#1B5E20"), 1.8f);
                    yield return new WaitForSeconds(2.2f);
                    done(perfect, new[] { mistakes });
                }
            }
            else
            {
                mistakes++;
                ctx.Sfx("plop");
                yield return StoryUI.Shake(b.holder, 0.3f, 8f);
                yield return new WaitForSeconds(MismatchShowSeconds - 0.45f - 0.3f > 0f ? MismatchShowSeconds - 0.75f : 0f);
                if (!_active) yield break;
                Run(Flip(a, false));
                yield return Flip(b, false);
                busy = false;
            }
        }
    }

    Card MakeCard(string key, Vector2 pos, float cs)
    {
        var holder = StoryUI.Pic(root, "Card", null, new Color(1f, 1f, 1f, 0f), pos, Vector2.one * cs, raycast: true);
        holder.preserveAspect = false;
        var c = new Card { holder = holder.rectTransform, key = key };

        var back = StoryUI.Pic(holder.transform, "Back", ShapeSprites.RoundedRect, StoryUI.Hex("#26A69A"), Vector2.zero, Vector2.one * cs);
        back.type = Image.Type.Sliced; back.preserveAspect = false;
        StoryUI.Label(back.transform, "?", Mathf.RoundToInt(cs * 0.6f), Color.white, Vector2.zero, Vector2.one * cs);
        c.back = back.gameObject;

        var front = StoryUI.Pic(holder.transform, "Front", ShapeSprites.RoundedRect, Color.white, Vector2.zero, Vector2.one * cs);
        front.type = Image.Type.Sliced; front.preserveAspect = false;
        StoryIcons.Build(front.transform, key, Vector2.zero, cs * 0.82f);
        front.gameObject.SetActive(false);
        c.front = front.gameObject;
        return c;
    }

    IEnumerator Flip(Card c, bool faceUp)
    {
        c.faceUp = faceUp;
        var rt = c.holder;
        if (rt == null) yield break;
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            rt.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.12f), 1f, 1f);
            yield return null;
        }
        if (rt == null) yield break;
        c.back.SetActive(!faceUp);
        c.front.SetActive(faceUp);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            rt.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / 0.12f), 1f, 1f);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    void Clear()
    {
        _active = false;
        foreach (var c in _cards) if (c != null && c.holder != null) UnityEngine.Object.Destroy(c.holder.gameObject);
        _cards.Clear();
        foreach (var e in _items) if (e != null) UnityEngine.Object.Destroy(e);
        _items.Clear();
    }
}
