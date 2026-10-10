using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lớp 2 — HIỂN THỊ: vẽ câu hỏi Q thành các phần tử trên màn hình (view). Bản C# của VIEW trong fs_flow.js (PHẢI khớp).
/// Bập bênh, đống khối chỉ là view (cách vẽ dữ liệu), không phải "game".
/// </summary>
public static class FlowViews
{
    static double P(object spec, string key) => FsMod.Num("view", spec, key);
    static string S(object spec, string key) => FsMod.Str("view", spec, key);
    static readonly string[] Palette = { "#FF7043", "#42A5F5", "#66BB6A", "#FFCA28", "#AB47BC", "#26C6DA" };

    public static FsView Make(FlowWorld w, Dictionary<string, object> spec, Dictionary<string, object> q)
    {
        switch (J.Str(spec, "type"))
        {
            case "cards": return new CardsView(w, spec, q);
            case "props": return new PropsView(w, spec, q);
            case "floor": return new FloorView(w);
            case "strip": return new StripView(w, spec, q);
            case "grid": return new GridView(w, spec, q);
            case "seesaw": return new SeesawView(w, spec, q);
            case "stack": return new StackView(w, spec, q);
            default: throw new FormatException("View không có: " + J.Str(spec, "type"));
        }
    }

    // ── cards ──
    sealed class CardsView : FsView
    {
        readonly FlowWorld _w;
        public CardsView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; bool isNum = S(spec, "source") == "options";
            var list = isNum ? J.Arr(J.Get(q, "options")) : J.Arr(J.Get(q, "items")); int n = list.Count;
            float spacing = n <= 3 ? 0.31f : n == 4 ? 0.24f : 0.72f / (n - 1), U = w.Unit;
            float cs = Mathf.Min(U * (float)P(spec, "sizeU"), w.WorldW * spacing * 0.93f), tall = (float)P(spec, "tall");
            for (int i = 0; i < n; i++)
            {
                var pos = w.PP(0.5f + (i - (n - 1) * 0.5f) * spacing, (float)P(spec, "y"));
                StoryCard card; FsNode fig = null; var it = isNum ? null : list[i];
                if (isNum) card = StoryUI.Card(w.Root, "Num", null, FsExpr.Str(list[i]), StoryUI.Hex("#7E57C2"), pos, new Vector2(cs, cs * tall), null, 72);
                else
                {
                    card = StoryUI.Card(w.Root, "Card", null, "", FlowWorld.Hex(S(spec, "bg")), pos, new Vector2(cs, cs * tall), null);
                    var c = card.bg.color; c.a = 0.96f; card.bg.color = c;
                    bool lbl = P(spec, "label") != 0;
                    fig = w.DrawItem(FsNode.Of(card.rt), it, 0, lbl ? cs * tall * 0.1f : 0f, cs * (float)P(spec, "fig"));
                    if (lbl) StoryUI.Label(card.rt, w.NameOf(it), 22, StoryUI.Hex("#3A2A12"), new Vector2(0, -cs * tall * 0.38f), new Vector2(cs * 0.96f, cs * tall * 0.26f), TextAnchor.MiddleCenter, false);
                }
                var node = FsNode.Of(card.rt); w.Track(node);
                Elems.Add(new FsElem { node = node, key = i, kind = "card", name = isNum ? FsExpr.Str(list[i]) : w.NameOf(it), fig = fig, item = it as Dictionary<string, object>, itemId = isNum ? "" : J.Str(it, "id") });
            }
        }
        public override void SetActive(bool b) { foreach (var e in Elems) { e.node.Active = b; if (b) _w.Go(StoryUI.PopIn(e.node.rt, 0.25f)); } }
        public override IEnumerator Intro() { foreach (var e in Elems) _w.Go(StoryUI.PopIn(e.node.rt, 0.3f)); yield break; }
    }

    // ── props ──
    const string PresetBare = "[{\"look\":{\"item\":\"=opt.id\"},\"w\":\"=size\"}]";
    const string PresetCard = "[{\"look\":{\"kind\":\"rrect\",\"color\":\"#FFFFFF\"},\"w\":\"=size\",\"h\":\"=size*1.1\",\"alpha\":0.96},{\"look\":{\"item\":\"=opt.id\"},\"w\":\"=size*.66\"}]";
    const string PresetZone = "[{\"look\":{\"shape\":\"circle\",\"color\":\"#FFFFFF\"},\"w\":\"=size\",\"alpha\":0.38},{\"look\":{\"shape\":\"circle\",\"color\":\"#FFFFFF\"},\"w\":\"=size*.75\",\"alpha\":0.35}]";

    sealed class PropsView : FsView
    {
        readonly FlowWorld _w;
        public PropsView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; var list = J.Arr(J.Get(q, "items")); int n = list.Count; float U = w.Unit; string layout = S(spec, "layout");
            var tplIn = FsMod.JsonPar("view", spec, "tpl", new List<object>()) as List<object>;
            List<object> tpl;
            if (tplIn != null && tplIn.Count > 0) tpl = tplIn;
            else
            {
                string pre = S(spec, "preset");
                tpl = (List<object>)FsJson.Parse(pre == "card" ? PresetCard : pre == "zone" ? PresetZone : pre == "none" ? "[]" : PresetBare);
            }
            var posIn = FsMod.JsonPar("view", spec, "pos", new List<object>()) as List<object> ?? new List<object>();
            float tall = (float)P(spec, "tall"); var optv = J.Arr(J.Get(q, "optv"));
            Dictionary<string, object> OptOf(int i, object it)
            {
                var d = new Dictionary<string, object>(it as Dictionary<string, object> ?? new Dictionary<string, object>());
                d["v"] = i < optv.Count ? optv[i] : "";
                return d;
            }
            float SizeOf(int i, object it)
            {
                var ex = new Dictionary<string, object> { { "opt", OptOf(i, it) }, { "i", (double)i }, { "n", (double)n }, { "item", it } };
                double v = FsExpr.ToNum(w.Ev(FsMod.Par("view", spec, "size"), ex)); return (float)(v != 0 ? v : 0.2);
            }
            float x0 = (float)P(spec, "x0"), x1 = (float)P(spec, "x1"), y0 = (float)P(spec, "y0"), y1 = (float)P(spec, "y1");
            var pts = new List<Vector2>();
            if (layout == "row") for (int i = 0; i < n; i++) pts.Add(w.PP(n > 1 ? x0 + (x1 - x0) * i / (n - 1) : (x0 + x1) / 2f, y0));
            else if (layout == "col") for (int i = 0; i < n; i++) pts.Add(w.PP(x0, n > 1 ? y0 + (y1 - y0) * i / (n - 1) : y0));
            else if (layout == "circle")
                for (int i = 0; i < n; i++)
                {
                    float a = ((float)P(spec, "startDeg") + i * 360f / n) * Mathf.Deg2Rad; var c = w.PP((float)P(spec, "cx"), (float)P(spec, "cy"));
                    pts.Add(new Vector2(c.x + Mathf.Sin(a) * (float)P(spec, "r") * U, c.y + Mathf.Cos(a) * (float)P(spec, "r") * U));
                }
            else if (layout == "grid")
            {
                int cols = (int)P(spec, "cols"), rows = Mathf.CeilToInt(n / (float)cols);
                for (int i = 0; i < n; i++) { int r = i / cols, c = i % cols; pts.Add(w.PP(x0 + (x1 - x0) * (cols > 1 ? c / (float)(cols - 1) : 0.5f), y0 + (y1 - y0) * (rows > 1 ? r / (float)(rows - 1) : 0f))); }
            }
            else if (layout == "fixed")
                for (int i = 0; i < n; i++) { var p = i < posIn.Count ? J.Arr(posIn[i]) : null; pts.Add(w.PP(p != null && p.Count > 1 ? (float)FsExpr.ToNum(p[0]) : 0.5f, p != null && p.Count > 1 ? (float)FsExpr.ToNum(p[1]) : 0.5f)); }
            else
            {
                var placed = new List<Vector2>(); float s0 = n > 0 ? SizeOf(0, list[0]) * U : U * 0.2f;
                for (int k = 0; k < n; k++)
                {
                    Vector2 best = Vector2.zero; double bs = 1e18;
                    for (int at = 0; at < 80; at++)
                    {
                        double rx = w.Rng.Next(), ry = w.Rng.Next();
                        var c = w.PP((float)(x0 + rx * (x1 - x0)), (float)(y0 + ry * (y1 - y0))); double sc = 0;
                        foreach (var qq in placed) { float d = Vector2.Distance(c, qq); if (d < s0) sc += s0 - d; }
                        if (sc < bs) { bs = sc; best = c; if (sc == 0) break; }
                    }
                    placed.Add(best); pts.Add(best);
                }
            }
            string yExpr = S(spec, "yExpr"); double hU = P(spec, "hU");
            for (int i = 0; i < n; i++)
            {
                var it = list[i]; float s = SizeOf(i, it);
                var ex = new Dictionary<string, object> { { "opt", OptOf(i, it) }, { "i", (double)i }, { "n", (double)n }, { "item", it }, { "size", (double)s } };
                float hh = hU > 0 ? (float)hU * U : s * U * tall;
                float py = yExpr != "" ? w.PP(0, (float)FsExpr.ToNum(w.Ev(yExpr, ex))).y : pts[i].y;
                var cont = StoryUI.Pic(w.Root, "Prop", null, new Color(1, 1, 1, 0), new Vector2(pts[i].x, py), new Vector2(s * U, hh), raycast: true);
                cont.preserveAspect = false; var contN = FsNode.Of(cont); w.Track(contN);
                var tplRel = new List<object>();
                foreach (var t in tpl) { var d = new Dictionary<string, object>((Dictionary<string, object>)t); d["rel"] = 1.0; tplRel.Add(d); }
                w.MakeObjs(tplRel, contN, w.TaskObjs, ex);
                if (P(spec, "labelBelow") != 0) StoryUI.Label(cont.rectTransform, w.NameOf(it), 26, Color.white, new Vector2(0, -hh * 0.5f - U * 0.045f), new Vector2(U * 0.5f, U * 0.08f));
                string an = S(spec, "anim");
                if (an != "none")
                {
                    var o = new FsObj { spec = new Dictionary<string, object> { { "anim", new Dictionary<string, object> { { "type", an == "pulse" ? "pulse" : "bob" }, { "amp", an == "float" ? 0.02 : 0.012 }, { "speed", an == "float" ? 1.6 : 3.0 } } } },
                                        extra = new Dictionary<string, object>(), node = contN, x0 = contN.X, y0 = contN.Y, t0 = (float)(w.Rng.Next() * 6.28) };
                    w.TaskObjs.Add(o);
                }
                Elems.Add(new FsElem { node = contN, key = i, kind = "prop", name = w.NameOf(it), item = it as Dictionary<string, object>, itemId = J.Str(it, "id") });
            }
        }
        public override void SetActive(bool b) { foreach (var e in Elems) { e.node.Active = b; if (b) _w.Go(StoryUI.PopIn(e.node.rt, 0.25f)); } }
        public override IEnumerator Intro() { foreach (var e in Elems) _w.Go(StoryUI.PopIn(e.node.rt, 0.3f)); yield break; }
    }

    // ── floor ──
    public sealed class FloorView : FsView
    {
        public Action<Vector2> OnTap;
        public FloorView(FlowWorld w)
        {
            int before = w.Root.childCount;
            StoryUI.TapArea(w.Root, p => OnTap?.Invoke(p));
            if (w.Root.childCount > before) w.Track(FsNode.Of(w.Root.GetChild(w.Root.childCount - 1) as RectTransform));
        }
    }

    // ── strip ──
    public sealed class StripView : FsView
    {
        readonly FlowWorld _w; readonly List<Image> _bg = new List<Image>(); readonly List<FsNode> _face = new List<FsNode>(); readonly List<Text> _q = new List<Text>(); readonly bool _plain;
        public StripView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; var seq = J.Arr(J.Get(q, "seq")); var items = J.Arr(J.Get(q, "items")); int len = seq.Count; _plain = P(spec, "plain") != 0;
            float slot = Mathf.Min(w.Unit * (float)P(spec, "slotU"), w.WorldW * 0.92f / len * 0.9f), gap = slot * 0.1f, rowW = len * slot + (len - 1) * gap, y = w.PP(0.5f, (float)P(spec, "y")).y;
            for (int i = 0; i < len; i++)
            {
                var b = StoryUI.Pic(w.Root, "Slot", ShapeSprites.RoundedRect, _plain ? new Color(1, 1, 1, 0) : new Color(1, 1, 1, 0.9f), new Vector2(-rowW * 0.5f + slot * 0.5f + i * (slot + gap), y), Vector2.one * slot);
                b.type = Image.Type.Sliced; b.preserveAspect = false; w.Track(FsNode.Of(b)); _bg.Add(b);
                var qq = StoryUI.Label(b.transform, "?", Mathf.RoundToInt(slot * 0.6f), StoryUI.Hex("#90A4AE"), Vector2.zero, Vector2.one * slot, TextAnchor.MiddleCenter, false); qq.gameObject.SetActive(false); _q.Add(qq);
                _face.Add(w.DrawItem(FsNode.Of(b), items[(int)FsExpr.ToNum(seq[i])], 0, 0, slot * 0.82f));
            }
            for (int i = 0; i < len; i++)
            {
                int k = i;
                Parts.Add(new FsPart
                {
                    Show = () => { _face[k].Active = true; _q[k].gameObject.SetActive(false); _w.Go(StoryUI.PopIn(_face[k].rt, 0.3f)); },
                    Cover = () => { _face[k].Active = false; _q[k].gameObject.SetActive(true); },
                    Hide = () => { _face[k].Active = false; _q[k].gameObject.SetActive(false); },
                });
            }
        }
        public override void HideAll() { for (int i = 0; i < _face.Count; i++) { _face[i].Active = false; _q[i].gameObject.SetActive(false); } }
        public void Mark(int i)
        {
            _face[i].Active = true; _q[i].gameObject.SetActive(false); _w.Go(StoryUI.PopIn(_face[i].rt, 0.3f));
            if (!_plain) _bg[i].color = StoryUI.Hex("#C8F7C5"); else if (_face[i].G != null) _w.Go(StoryUI.Fade(_face[i].G, 0.4f, 0.3f));
        }
        public void PulseSlot(int i, Func<bool> alive) { _w.Go(StoryUI.Pulse(_bg[i].rectTransform, alive, 0.06f, 6f)); }
    }

    // ── grid ──
    public sealed class GridView : FsView
    {
        readonly FlowWorld _w;
        public GridView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; var faces = J.Arr(J.Get(q, "faces")); var items = J.Arr(J.Get(q, "items")); int total = faces.Count, pairs = (int)J.Num(q, "pairs");
            int rows = pairs <= 5 ? 2 : 3, cols = Mathf.CeilToInt(total / (float)rows);
            float areaW = w.WorldW * 0.94f, areaH = w.WorldH * 0.76f, cs = Mathf.Min(areaW / cols, areaH / rows) * 0.88f, stepX = areaW / cols, stepY = areaH / rows; var origin = w.PP(0.5f, 0.47f);
            bool covered = P(spec, "covered") != 0;
            for (int i = 0; i < total; i++)
            {
                int fi = (int)FsExpr.ToNum(faces[i]), r = i / cols, c = i % cols;
                var holder = StoryUI.Pic(w.Root, "Card", null, new Color(1, 1, 1, 0), origin + new Vector2((c - (cols - 1) * 0.5f) * stepX, ((rows - 1) * 0.5f - r) * stepY), Vector2.one * cs, raycast: true);
                holder.preserveAspect = false; var hn = FsNode.Of(holder); w.Track(hn);
                var back = StoryUI.Pic(holder.transform, "Back", ShapeSprites.RoundedRect, StoryUI.Hex("#26A69A"), Vector2.zero, Vector2.one * cs); back.type = Image.Type.Sliced; back.preserveAspect = false;
                StoryUI.Label(back.transform, "?", Mathf.RoundToInt(cs * 0.6f), Color.white, Vector2.zero, Vector2.one * cs);
                var front = StoryUI.Pic(holder.transform, "Front", ShapeSprites.RoundedRect, Color.white, Vector2.zero, Vector2.one * cs); front.type = Image.Type.Sliced; front.preserveAspect = false;
                w.DrawItem(FsNode.Of(front), items[fi], 0, 0, cs * 0.82f);
                back.gameObject.SetActive(covered); front.gameObject.SetActive(!covered);
                Elems.Add(new FsElem { node = hn, key = fi, kind = "flip", back = FsNode.Of(back), front = FsNode.Of(front), faceUp = !covered, name = w.NameOf(items[fi]) });
            }
            foreach (var e in Elems)
            {
                var el = e;
                Parts.Add(new FsPart
                {
                    Show = () => { el.back.Active = false; el.front.Active = true; el.faceUp = true; },
                    Cover = () => { el.back.Active = true; el.front.Active = false; el.faceUp = false; },
                    Hide = () => { el.node.Active = false; },
                });
            }
        }
        public override void HideAll() { foreach (var e in Elems) { e.back.Active = true; e.front.Active = false; e.faceUp = false; } }
        public override IEnumerator Intro() { foreach (var e in Elems) _w.Go(StoryUI.PopIn(e.node.rt, 0.25f)); yield break; }
        public IEnumerator Flip(FsElem e, bool up)
        {
            e.faceUp = up; var rt = e.node.rt; if (rt == null) yield break;
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { if (rt == null) yield break; rt.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.12f), 1f, 1f); yield return null; }
            if (rt == null) yield break; e.back.Active = !up; e.front.Active = up;
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { if (rt == null) yield break; rt.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / 0.12f), 1f, 1f); yield return null; }
            if (rt != null) rt.localScale = Vector3.one;
        }
    }

    // ── seesaw ──
    sealed class Saw { public Vector2 pivot; public Image plank; public FsNode[] animal = new FsNode[2]; public Vector2[] local = new Vector2[2]; public object[] items = new object[2]; }

    public sealed class SeesawView : FsView
    {
        readonly FlowWorld _w; readonly List<Saw> _saws = new List<Saw>();
        public SeesawView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; Ready = false; var items = J.Arr(J.Get(q, "items")); int n = items.Count; float U = w.Unit, W = w.WorldW;
            float size = n == 2 ? U * 0.30f : U * 0.20f, plankLen = W * (n == 2 ? 0.86f : 0.80f), thick = U * 0.04f;
            float[] pivotY = n == 2 ? new[] { 0.30f } : new[] { 0.60f, 0.25f };
            int[][] pairs = n == 2 ? new[] { new[] { 0, 1 } } : new[] { new[] { 0, 1 }, new[] { 1, 2 } };
            for (int s = 0; s < pairs.Length; s++)
            {
                var pv = w.PP(0.5f, pivotY[s]); var sw = new Saw { pivot = pv };
                w.Track(FsNode.Of(StoryUI.Pic(w.Root, "Pivot", ShapeSprites.Triangle, StoryUI.Hex("#8D6E63"), pv + new Vector2(0, -U * 0.055f), new Vector2(U * 0.14f, U * 0.13f))));
                var plank = StoryUI.Pic(w.Root, "Plank", ShapeSprites.RoundedRect, StoryUI.Hex("#C98B4B"), pv, new Vector2(plankLen, thick)); plank.type = Image.Type.Sliced; plank.preserveAspect = false;
                w.Track(FsNode.Of(plank)); sw.plank = plank;
                bool swap = w.Rng.Range(0, 2) == 0;
                for (int side = 0; side < 2; side++)
                {
                    int ii = pairs[s][swap ? 1 - side : side]; var it = items[ii];
                    var a = w.DrawItem(FsNode.Of(w.Root), it, pv.x, pv.y, size); w.Track(a);
                    if (n == 2) StoryUI.Label(a.rt, w.NameOf(it), 30, Color.white, new Vector2(0, size * 0.62f), new Vector2(size * 1.6f, size * 0.3f));
                    sw.animal[side] = a; sw.items[side] = it; sw.local[side] = new Vector2((side == 0 ? -1 : 1) * plankLen * 0.32f, thick * 0.5f + size * 0.5f);
                    if (a.G != null) a.G.raycastTarget = true;
                    Elems.Add(new FsElem { node = a, key = ii, kind = "item", name = w.NameOf(it), item = it as Dictionary<string, object>, itemId = J.Str(it, "id") });
                }
                SetAngle(sw, 0f); _saws.Add(sw);
            }
        }
        static void SetAngle(Saw sw, float deg)
        {
            var qt = Quaternion.Euler(0, 0, deg); sw.plank.rectTransform.localRotation = qt;
            for (int i = 0; i < 2; i++) if (sw.animal[i] != null && !sw.animal[i].Dead) sw.animal[i].Pos = sw.pivot + (Vector2)(qt * (Vector3)sw.local[i]);
        }
        public override IEnumerator Intro()
        {
            foreach (var e in Elems) _w.Go(StoryUI.PopIn(e.node.rt, 0.35f));
            const float A = 15f;
            yield return new WaitForSeconds(0.5f);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime) { float wob = Mathf.Sin(t * 14f) * 2.5f; foreach (var sw in _saws) if (sw.plank != null) SetAngle(sw, wob); yield return null; }
            var tg = new float[_saws.Count];
            for (int s = 0; s < _saws.Count; s++) tg[s] = J.Num(_saws[s].items[0], "value") > J.Num(_saws[s].items[1], "value") ? A : -A;
            _w.Sfx("wood");
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = t / 0.9f, e = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);
                for (int s = 0; s < _saws.Count; s++) if (_saws[s].plank != null) SetAngle(_saws[s], tg[s] * e);
                yield return null;
            }
            for (int s = 0; s < _saws.Count; s++) if (_saws[s].plank != null) SetAngle(_saws[s], tg[s]);
            Ready = true;
        }
    }

    // ── stack ──
    public sealed class StackView : FsView
    {
        readonly FlowWorld _w; readonly List<RectTransform> _cubes = new List<RectTransform>(); readonly float _s; bool _counted;
        public Vector2 SolvedAt;
        public StackView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; int count = (int)J.Num(q, "number"); float U = w.Unit, W = w.WorldW, H = w.WorldH;
            var heights = MakeStack(w, count); string cp = S(spec, "color");
            var color = StoryUI.Hex(cp == "random" ? Palette[w.Rng.Range(0, Palette.Length)] : cp);
            var its = J.Arr(J.Get(q, "items")); object obj = its.Count > 0 ? its[0] : null;
            float s = Mathf.Min(W * 0.9f / 6.2f, H * 0.52f / 6.2f, U * 0.11f); _s = s;
            var cells = new List<Vector3Int>();
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) for (int z = 0; z < heights[x, y]; z++) cells.Add(new Vector3Int(x, y, z));
            var sorted = new List<Vector3Int>(cells);
            // sắp xếp ổn định theo (x+y+z) tăng dần — khớp Array.sort của JS (ổn định)
            for (int i = 1; i < sorted.Count; i++) { var c = sorted[i]; int j = i - 1; while (j >= 0 && (sorted[j].x + sorted[j].y + sorted[j].z) > (c.x + c.y + c.z)) { sorted[j + 1] = sorted[j]; j--; } sorted[j + 1] = c; }
            Vector2 Iso(Vector3Int c) => new Vector2((c.x - c.y) * s, -(c.x + c.y) * s * 0.5f + c.z * s);
            float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
            foreach (var c in sorted) { var p = Iso(c); minX = Mathf.Min(minX, p.x - s); maxX = Mathf.Max(maxX, p.x + s); minY = Mathf.Min(minY, p.y - s); maxY = Mathf.Max(maxY, p.y + s); }
            var pc = w.PP(0.5f, 0.60f); var center = pc - new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            foreach (var c in sorted)
            {
                var p = Iso(c); RectTransform rt;
                if (obj != null) rt = w.DrawItem(FsNode.Of(w.Root), obj, center.x + p.x, center.y + p.y, 2f * s * 0.9f).rt;
                else
                {
                    rt = StoryUI.Rect(w.Root, "Cube", center + p, Vector2.one * 2f * s);
                    var g = rt.gameObject.AddComponent<StoryCube>(); g.color = color; g.raycastTarget = false;
                }
                rt.localScale = Vector3.zero; w.Track(FsNode.Of(rt)); _cubes.Add(rt);
            }
            SolvedAt = new Vector2(center.x, center.y + s);
            foreach (var cb in _cubes) { var r = cb; Parts.Add(new FsPart { Show = () => _w.Go(StoryUI.PopIn(r, 0.25f)), Cover = () => r.localScale = Vector3.zero, Hide = () => r.localScale = Vector3.zero }); }
        }
        public override void HideAll() { foreach (var c in _cubes) c.localScale = Vector3.zero; }
        public override IEnumerator Intro() { foreach (var c in _cubes) { if (c == null) yield break; _w.Go(StoryUI.PopIn(c, 0.25f)); yield return new WaitForSeconds(0.07f); } }
        public IEnumerator CountAloud(Action onDone)
        {
            var badges = new List<GameObject>();
            for (int i = 0; i < _cubes.Count; i++)
            {
                if (_cubes[i] == null) yield break;
                var pos = _cubes[i].anchoredPosition + new Vector2(0, _s * 0.5f);
                var b = StoryUI.Pic(_w.Root, "Badge", ShapeSprites.Circle, new Color(0.15f, 0.1f, 0.3f, 0.85f), pos, Vector2.one * _s * 0.85f);
                StoryUI.Label(b.transform, (i + 1).ToString(), Mathf.RoundToInt(_s * 0.7f), Color.white, Vector2.zero, Vector2.one * _s * 0.85f);
                badges.Add(b.gameObject); _w.Track(FsNode.Of(b)); _w.Go(StoryUI.PopIn(b.rectTransform, 0.2f)); _w.Sfx("tap");
                yield return new WaitForSeconds(0.4f);
            }
            onDone?.Invoke();
            yield return new WaitForSeconds(1.6f);
            foreach (var b in badges) if (b != null) UnityEngine.Object.Destroy(b);
        }
        public override void HintExtra() { if (!_counted) { _counted = true; _w.Go(CountAloud(null)); } }

        static int[,] MakeStack(FlowWorld w, int count)
        {
            int R(int n) => w.Rng.Range(0, n);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var h = new int[3, 3]; int a0 = R(3), b0 = R(3); h[a0, b0] = 1; int placed = 1, g = 0;
                while (placed < count && g++ < 200)
                {
                    int x = R(3), y = R(3); if (h[x, y] >= 3) continue;
                    bool sup = h[x, y] > 0;
                    if (!sup) for (int dx = -1; dx <= 1 && !sup; dx++) for (int dy = -1; dy <= 1 && !sup; dy++) { int nx = x + dx, ny = y + dy; if (nx >= 0 && nx < 3 && ny >= 0 && ny < 3 && h[nx, ny] > 0) sup = true; }
                    if (!sup) continue; h[x, y]++; placed++;
                }
                if (placed == count && NoHidden(h)) return h;
            }
            var f = new int[3, 3]; for (int i = 0; i < count; i++) f[i % 3, (i / 3) % 3]++; return f;
        }
        static bool NoHidden(int[,] h)
        {
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) for (int z = 0; z < h[x, y]; z++)
                if (h[x, y] > z + 1 && x + 1 < 3 && h[x + 1, y] > z && y + 1 < 3 && h[x, y + 1] > z) return false;
            return true;
        }
    }
}
