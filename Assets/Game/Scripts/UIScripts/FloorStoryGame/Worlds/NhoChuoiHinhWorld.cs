using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Nhớ chuỗi hình" (bé 3 tuổi) — trí nhớ. Hiện một hàng hình (mặt trời, mặt trăng, hoa, bánh, kem, cốc...) lần lượt từng hình,
/// rồi úp lại thành dấu "?". Bé chạm 4 hình ở dưới theo ĐÚNG THỨ TỰ đã thấy, từ TRÁI sang PHẢI; mỗi bước đúng thì ô tương ứng
/// lật ra. Chuỗi dài 1 → 6 hình (tăng dần mỗi 2 lượt đúng ngay lần đầu); chỉ có 4 loại hình nên hình sẽ lặp lại.
/// Sai thì rung nhẹ và thử lại đúng bước đó; sau 2 lần sai ở 1 bước thì hình đúng nhấp nháy. Không cần ảnh.
/// </summary>
public sealed class NhoChuoiHinhWorld : StoryWorld
{
    readonly List<GameObject> _items = new List<GameObject>();
    Text _prompt;
    bool _active;
    int _ok;     // số lượt đúng ngay lần đầu → quyết định độ dài chuỗi
    int _gen;    // thế hệ lượt, để coroutine cũ tự dừng

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#FFF3E0"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#FFE0B2"), P(0.5f, 0.06f), new Vector2(W, H * 0.14f)).preserveAspect = false;
        _prompt = StoryUI.Label(root, "", 38, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.94f, H * 0.12f));
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        Clear();
        _active = true;
        int gen = ++_gen;

        int len = Mathf.Min(6, 1 + _ok / 2);
        // 4 loại hình của lượt này.
        var keys = (string[])StoryIcons.Keys.Clone(); Shuffle(keys);
        var pool = new[] { keys[0], keys[1], keys[2], keys[3] };
        // Chuỗi: không có 3 hình giống nhau liên tiếp; chuỗi ≤ 3 hình thì khác nhau hết (dễ cho lượt đầu).
        var seq = new int[len];
        for (int i = 0; i < len; i++)
        {
            int v;
            int guard = 0;
            do
            {
                v = Rand(0, 4);
                guard++;
            }
            while (guard < 30 && ((len <= 3 && Array.IndexOf(seq, v, 0, i) >= 0) || (i >= 2 && seq[i - 1] == v && seq[i - 2] == v)));
            seq[i] = v;
        }

        var names = new string[4];
        for (int i = 0; i < 4; i++) names[i] = StoryIcons.VietnameseName(pool[i]);
        var seqNames = new string[len];
        for (int i = 0; i < len; i++) seqNames[i] = pool[seq[i]];
        Describe(info, $"NHO_{len}_{string.Join("-", seqNames)}", "NhoChuoiHinh_TriNho",
                 $"Nhớ chuỗi {len} hình: {string.Join(", ", seqNames)}", names, seq[0]);
        info.answerMode = AnswerMode.OrderedSequence;
        info.correctAnswers = (int[])seq.Clone();

        Run(Play(info, pool, seq, gen, done));
    }

    IEnumerator Play(QuestionData info, string[] pool, int[] seq, int gen, Action<bool, int[]> done)
    {
        int len = seq.Length;

        // ── Khung ô + hàng lựa chọn ──────────────────────────────────────────
        float slot = Mathf.Min(U * 0.2f, W * 0.92f / len * 0.9f);
        float gap = slot * 0.1f;
        float rowW = len * slot + (len - 1) * gap;
        var slotBg = new Image[len];
        var slotFace = new RectTransform[len];
        var slotQ = new Text[len];
        for (int i = 0; i < len; i++)
        {
            var pos = new Vector2(-rowW * 0.5f + slot * 0.5f + i * (slot + gap), H * 0.20f);
            var bg = StoryUI.Pic(root, "Slot", ShapeSprites.RoundedRect, new Color(1f, 1f, 1f, 0.9f), pos, Vector2.one * slot);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            slotBg[i] = bg;
            _items.Add(bg.gameObject);
            var q = StoryUI.Label(bg.transform, "?", Mathf.RoundToInt(slot * 0.6f), StoryUI.Hex("#90A4AE"), Vector2.zero, Vector2.one * slot, outline: false);
            q.gameObject.SetActive(false);
            slotQ[i] = q;
            slotFace[i] = StoryIcons.Build(bg.transform, pool[seq[i]], Vector2.zero, slot * 0.82f);
            slotFace[i].gameObject.SetActive(false);
        }

        float cs = Mathf.Min(U * 0.2f, W * 0.2f);
        var choice = new StoryCard[4];
        int cur = 0, stepMistakes = 0, totalMistakes = 0;
        bool hint = false, finished = false, accept = false;
        var taps = new List<int>();
        for (int c = 0; c < 4; c++)
        {
            int ci = c;
            var pos = new Vector2(-W * 0.5f + W * (0.14f + c * 0.24f), -H * 0.17f);
            var card = StoryUI.Card(root, "Choice_" + pool[c], null, "", new Color(1f, 1f, 1f, 0.95f), pos, Vector2.one * cs, null);
            card.rt.gameObject.SetActive(false);
            StoryIcons.Build(card.rt, pool[c], Vector2.zero, cs * 0.8f);
            choice[c] = card;
            _items.Add(card.rt.gameObject);
            StoryUI.OnTap(card.rt.gameObject, () =>
            {
                if (!_active || !accept || finished) return;
                taps.Add(ci);
                if (ci == seq[cur])
                {
                    hint = false; stepMistakes = 0;
                    ctx.SfxRight();
                    slotFace[cur].gameObject.SetActive(true);
                    slotQ[cur].gameObject.SetActive(false);
                    Run(StoryUI.PopIn(slotFace[cur], 0.3f));
                    slotBg[cur].color = StoryUI.Hex("#C8F7C5");
                    Run(StoryUI.Bounce(card.rt, 0.15f, 0.3f));
                    cur++;
                    if (cur >= len)
                    {
                        finished = true;
                        bool perfect = totalMistakes == 0;
                        if (perfect) _ok++;
                        Confetti(new Vector2(0, H * 0.05f), 14);
                        Say(perfect ? "Giỏi quá!" : "Đúng rồi!", P(0.5f, 0.55f), StoryUI.Hex("#1B5E20"), 1.6f);
                        Run(Finish(perfect, taps.ToArray(), done));
                    }
                    else
                    {
                        int nextSlot = cur;
                        Run(StoryUI.Pulse(slotBg[nextSlot].rectTransform, () => _active && !finished && cur == nextSlot, 0.06f, 6f));
                    }
                }
                else
                {
                    totalMistakes++; stepMistakes++;
                    ctx.Sfx("tap");
                    Run(StoryUI.Shake(card.rt, 0.35f, 10f));
                    if (stepMistakes >= 2 && !hint)
                    {
                        hint = true;
                        int want = seq[cur], at = cur;
                        Run(StoryUI.Pulse(choice[want].rt, () => hint && cur == at && !finished, 0.15f, 7f));
                    }
                }
            });
        }

        // ── Pha 1: xem chuỗi ────────────────────────────────────────────────
        _prompt.text = "Nhìn kỹ nè! Nhớ thứ tự nhé";
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < len; i++)
        {
            if (gen != _gen) yield break;
            slotFace[i].gameObject.SetActive(true);
            ctx.Sfx("pop");
            Run(StoryUI.PopIn(slotFace[i], 0.3f));
            yield return new WaitForSeconds(0.6f);
        }
        yield return new WaitForSeconds(1.2f + 0.35f * len);
        if (gen != _gen) yield break;

        // ── Úp lại ──────────────────────────────────────────────────────────
        for (int i = 0; i < len; i++)
        {
            slotFace[i].gameObject.SetActive(false);
            slotQ[i].gameObject.SetActive(true);
        }
        ctx.Sfx("plop");

        // ── Pha 2: chọn theo thứ tự ─────────────────────────────────────────
        _prompt.text = "Chọn theo thứ tự, từ TRÁI sang PHẢI";
        ctx.Voice("nho_chon");
        for (int c = 0; c < 4; c++)
        {
            choice[c].rt.gameObject.SetActive(true);
            Run(StoryUI.PopIn(choice[c].rt, 0.25f));
        }
        accept = true;
        Run(StoryUI.Pulse(slotBg[0].rectTransform, () => _active && !finished && cur == 0, 0.06f, 6f));
    }

    IEnumerator Finish(bool perfect, int[] answer, Action<bool, int[]> done)
    {
        yield return new WaitForSeconds(2.2f);
        done(perfect, answer);
    }

    void Clear()
    {
        _active = false;
        _gen++;
        foreach (var e in _items) if (e != null) UnityEngine.Object.Destroy(e);
        _items.Clear();
    }
}
