using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lớp 4/5 — CHẠM và CHẤM: gắn xử lý chạm vào các phần tử của view và quyết định đúng/sai. Bản C# của JUDGE trong fs_flow.js (PHẢI khớp).
/// Phản hồi (đúng/sai/từng bước/xong) chạy qua hành động flow.on.* hoặc mặc định (FlowWorld.StdRight/StdWrong).
/// </summary>
public static class FlowJudges
{
    static double P(object spec, string key) => FsMod.Num("judge", spec, key);

    public static void Attach(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q, List<FsView> views, Action<bool, int[]> finish)
    {
        switch (J.Str(spec, "type"))
        {
            case "equals": Equals(w, spec, q, views, finish); break;
            case "order": Order(w, spec, q, views, finish); break;
            case "pairs": Pairs(w, spec, q, views, finish); break;
            case "taps": Taps(w, spec, q, views, finish); break;
            default: throw new FormatException("Cách chấm không có: " + J.Str(spec, "type"));
        }
    }

    /// <summary>Gắn chạm; nếu phần tử chưa có Graphic (vd icon ghép bằng code) thì thêm Image trong suốt để nhận chạm.</summary>
    static void Tap(FsNode n, Action cb)
    {
        var go = n.rt.gameObject;
        if (go.GetComponent<Graphic>() == null)
        {
            var im = go.AddComponent<Image>(); im.color = new Color(1, 1, 1, 0); im.raycastTarget = true;
        }
        StoryUI.OnTap(go, cb);
    }

    static ActCtx Cx(FsElem el, int step = 0, int mistakes = 0) => new ActCtx { tapped = el, tap = new Vector2(el.node.X, el.node.Y), step = step, mistakes = mistakes };

    static List<FsElem> Keyed(FlowWorld w) => w.Elems.FindAll(e => e.key >= 0);

    // ── equals ──
    static void Equals(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q, List<FsView> views, Action<bool, int[]> finish)
    {
        int mistakes = 0, firstWrong = -1; bool hint = false, answered = false, busy = false;
        int target = (int)J.Num(q, "target", -1);
        IEnumerator OnTap(FsElem el)
        {
            if (!w.Active || answered || busy || !w.Ready) yield break;
            var cx = Cx(el, 0, mistakes);
            if (el.key == target)
            {
                answered = true; hint = false; busy = true;
                yield return w.Fire("right", cx, w.StdRight);
                var stack = views.Find(v => v is FlowViews.StackView) as FlowViews.StackView;
                if (stack != null)
                {
                    bool done = false; w.Go(stack.CountAloud(() => done = true));
                    while (!done) { if (w.Stale(q)) yield break; yield return null; }
                    w.Burst(stack.SolvedAt, 14);
                }
                yield return new WaitForSeconds((float)P(spec, "finishSec"));
                if (w.Stale(q)) yield break;
                yield return w.Fire("end", cx);
                finish(mistakes == 0, new[] { mistakes == 0 ? target : firstWrong });
            }
            else
            {
                mistakes++; if (firstWrong < 0) firstWrong = el.key; busy = true;
                yield return w.Fire("wrong", cx, w.StdWrong);
                busy = false;
                if (w.Stale(q)) yield break;
                if (mistakes >= P(spec, "hintAfter") && !hint)
                {
                    hint = true; foreach (var v in views) v.HintExtra();
                    foreach (var e in Keyed(w)) if (e.key == target) w.Go(StoryUI.Pulse(e.node.rt, () => hint && !answered, 0.12f, 7f));
                }
            }
        }
        foreach (var el in Keyed(w)) { var e = el; Tap(e.node, () => w.Go(OnTap(e))); }
    }

    // ── order ──
    static void Order(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q, List<FsView> views, Action<bool, int[]> finish)
    {
        var T = J.Obj(w.Flow, "text") ?? new Dictionary<string, object>();
        var strip = views.Find(v => v is FlowViews.StripView) as FlowViews.StripView;
        var seq = new List<int>(); foreach (var s in J.Arr(J.Get(q, "seq"))) seq.Add((int)FsExpr.ToNum(s));
        int len = seq.Count, cur = 0, stepMist = 0, total = 0; bool hint = false, finished = false, busy = false; var taps = new List<int>();
        IEnumerator OnTap(FsElem el)
        {
            if (!w.Active || !w.Ready || finished || busy) yield break;
            taps.Add(el.key);
            var cx = Cx(el, cur);
            if (el.key == seq[cur])
            {
                hint = false; stepMist = 0; int my = cur; cur++;
                strip?.Mark(my);
                bool last = cur >= len; cx.last = last; if (last) finished = true;
                yield return w.Fire("step", cx, c => StepStd(w, el));
                if (last)
                {
                    bool perfect = total == 0;
                    yield return w.Fire("right", cx, c => OrderRightStd(w, q, T, perfect, c));
                    yield return new WaitForSeconds((float)P(spec, "finishSec"));
                    if (w.Stale(q)) yield break;
                    yield return w.Fire("end", cx);
                    finish(perfect, taps.ToArray());
                }
                else if (strip != null) { int nx = cur; strip.PulseSlot(nx, () => w.Active && !finished && cur == nx); }
            }
            else
            {
                total++; stepMist++; busy = true;
                yield return w.Fire("wrong", cx, c => OrderWrongStd(w, q, T, el, c));
                busy = false;
                if (w.Stale(q)) yield break;
                if (stepMist >= P(spec, "hintAfter") && !hint)
                {
                    hint = true; int at = cur; var want = Keyed(w).Find(e => e.key == seq[cur]);
                    if (want != null) w.Go(StoryUI.Pulse(want.node.rt, () => hint && cur == at && !finished, 0.15f, 7f));
                }
            }
        }
        foreach (var el in Keyed(w)) { var e = el; Tap(e.node, () => w.Go(OnTap(e))); }
        if (strip != null) w.OnReadyOnce(() => strip.PulseSlot(0, () => w.Active && !finished && cur == 0));
    }

    static IEnumerator StepStd(FlowWorld w, FsElem el) { w.SfxRight(); w.Go(StoryUI.Bounce(el.node.rt, 0.15f, 0.3f)); yield break; }

    static IEnumerator OrderRightStd(FlowWorld w, Dictionary<string, object> q, Dictionary<string, object> T, bool perfect, ActCtx cx)
    {
        w.Burst(new Vector2(0, w.WorldH * 0.05f), 14);
        string say = perfect ? J.Str(T, "say") : (J.Str(T, "sayOk") != "" ? J.Str(T, "sayOk") : J.Str(T, "say"));
        w.Talk(FsExpr.Template(say, J.Obj(q, "vars"), w.Scope(cx.ToScope())), w.PP(0.5f, 0.55f), StoryUI.Hex("#1B5E20"), 1.6f);
        yield break;
    }

    static IEnumerator OrderWrongStd(FlowWorld w, Dictionary<string, object> q, Dictionary<string, object> T, FsElem el, ActCtx cx)
    {
        w.Sfx("tap"); w.Go(StoryUI.Shake(el.node.rt, 0.35f, 10f));
        string wrong = J.Str(T, "wrong");
        if (wrong != "")
        {
            var qv = new Dictionary<string, object>(); var src = J.Obj(q, "vars"); if (src != null) foreach (var kv in src) qv[kv.Key] = kv.Value; qv["name"] = el.name;
            string t = FsExpr.Template(wrong, qv, w.Scope(cx.ToScope()));
            if (t != "") w.Talk(t, w.PP(0.5f, 0.14f), StoryUI.Hex("#BF360C"), 1.3f);
        }
        yield break;
    }

    // ── pairs ──
    static void Pairs(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q, List<FsView> views, Action<bool, int[]> finish)
    {
        var T = J.Obj(w.Flow, "text") ?? new Dictionary<string, object>();
        var grid = views.Find(v => v is FlowViews.GridView) as FlowViews.GridView;
        if (grid == null) throw new FormatException("Cách chấm \"ghép cặp\" cần 1 view \"Lưới thẻ\".");
        int mistakes = 0, matched = 0, pairs = (int)J.Num(q, "pairs"); bool busy = false; var open = new List<FsElem>();
        IEnumerator Resolve(FsElem a, FsElem b)
        {
            yield return new WaitForSeconds(0.45f);
            if (!w.Active || w.Stale(q)) yield break;
            if (a.key == b.key)
            {
                a.matched = b.matched = true; matched++; w.SfxRight(); w.Point();
                w.Burst(new Vector2(a.node.X, a.node.Y), 6, false); w.Burst(new Vector2(b.node.X, b.node.Y), 6, false);
                w.Go(StoryUI.Bounce(a.node.rt, 0.25f, 0.4f)); yield return StoryUI.Bounce(b.node.rt, 0.25f, 0.4f);
                w.Go(StoryUI.ScaleTo(a.node.rt, 0f, 0.25f)); yield return StoryUI.ScaleTo(b.node.rt, 0f, 0.25f);
                if (!a.node.Dead) a.node.Active = false; if (!b.node.Dead) b.node.Active = false;
                busy = false;
                if (matched >= pairs)
                {
                    bool perfect = mistakes == 0;
                    w.Burst(Vector2.zero, 16);
                    string say = perfect ? J.Str(T, "say") : (J.Str(T, "sayOk") != "" ? J.Str(T, "sayOk") : J.Str(T, "say"));
                    w.Talk(FsExpr.Template(say, J.Obj(q, "vars"), w.Scope()), w.PP(0.5f, 0.5f), StoryUI.Hex("#1B5E20"), 1.8f);
                    yield return new WaitForSeconds((float)P(spec, "finishSec"));
                    finish(P(spec, "bonus") != 0 && perfect, new[] { mistakes });
                }
            }
            else
            {
                mistakes++; w.Sfx("plop"); yield return StoryUI.Shake(b.node.rt, 0.3f, 8f);
                yield return new WaitForSeconds(Mathf.Max(0f, (float)P(spec, "hideSec") - 0.75f));
                if (!w.Active || w.Stale(q)) yield break;
                w.Go(grid.Flip(a, false)); yield return grid.Flip(b, false); busy = false;
            }
        }
        foreach (var el in Keyed(w))
        {
            var e = el;
            Tap(e.node, () =>
            {
                if (!w.Active || !w.Ready || busy || e.matched || e.faceUp) return;
                w.Sfx("tap"); w.Go(grid.Flip(e, true)); open.Add(e);
                if (open.Count < 2) return;
                busy = true; var a = open[0]; var b = open[1]; open.Clear(); w.Go(Resolve(a, b));
            });
        }
    }

    // ── taps ──
    static void Taps(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q, List<FsView> views, Action<bool, int[]> finish)
    {
        var fl = views.Find(v => v is FlowViews.FloorView) as FlowViews.FloorView;
        if (fl == null) throw new FormatException("Cách chấm \"đếm lượt chạm\" cần view \"Nền chạm\".");
        int count = 0; bool done = false;
        fl.OnTap = p =>
        {
            if (!w.Active || done || !w.Ready) return;
            count++; var cx = new ActCtx { tap = p, step = count - 1 };
            w.Go(Step(cx));
        };
        IEnumerator Step(ActCtx cx)
        {
            yield return w.Fire("step", cx);
            if (count >= P(spec, "count") && !done)
            {
                done = true;
                yield return w.Fire("right", cx, c => RightSfx(w));
                yield return new WaitForSeconds((float)P(spec, "finishSec"));
                if (w.Stale(q)) yield break;
                yield return w.Fire("end", cx);
                finish(true, new[] { count });
            }
        }
    }

    static IEnumerator RightSfx(FlowWorld w) { w.SfxRight(); yield break; }
}
