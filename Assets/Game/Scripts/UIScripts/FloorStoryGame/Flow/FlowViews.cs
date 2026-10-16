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
    /// <summary>Vị trí ghi đè từng phần tử của view (trình soạn kéo thả web): spec.p.ov = { "<chỉ số>": [fx, fy] }, toạ độ 0..1 của world. Khớp ovPos trong fs_flow.js.</summary>
    static Vector2? OvPos(FlowWorld w, object spec, int i)
    {
        var ov = J.Obj(J.Obj(spec, "p"), "ov"); if (ov == null) return null;
        var v = J.Arr(J.Get(ov, i.ToString())); if (v.Count < 2) return null;
        return w.PP((float)FsExpr.ToNum(v[0]), (float)FsExpr.ToNum(v[1]));
    }
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
                var pos = OvPos(w, spec, i) ?? w.PP((float)P(spec, "x") + (i - (n - 1) * 0.5f) * spacing, (float)P(spec, "y"));
                StoryCard card; FsNode fig = null; var it = isNum ? null : list[i];
                if (isNum) card = StoryUI.Card(w.Root, "Num", null, FsExpr.Str(list[i]), StoryUI.Hex("#7E57C2"), pos, new Vector2(cs, cs * tall), null, Mathf.RoundToInt(72f * Mathf.Max(1f, cs * tall / (U * 0.22f) * 0.85f)));   // thẻ cao hơn chuẩn → chữ to tương ứng
                else
                {
                    string bgv = (S(spec, "bg") ?? "").Trim(); bool noBg = bgv == "" || bgv.ToLowerInvariant() == "none";   // trống / none = thẻ trong suốt
                    card = StoryUI.Card(w.Root, "Card", null, "", noBg ? new Color(1, 1, 1, 0) : FlowWorld.Hex(bgv), pos, new Vector2(cs, cs * tall), null);
                    if (!noBg) { var c = card.bg.color; c.a = 0.96f; card.bg.color = c; }
                    var cardSpr = w.ImageSprite(S(spec, "cardImg"));   // ảnh nền thẻ là con của thẻ (nằm dưới hình) để chỉnh tỉ lệ riêng
                    if (cardSpr != null)
                    {
                        card.bg.color = new Color(1, 1, 1, 0);
                        float ik = (float)P(spec, "cardImgScale"); var ci = StoryUI.Pic(card.rt, "CardImg", cardSpr, Color.white, Vector2.zero, new Vector2(cs * ik, cs * tall * ik)); ci.preserveAspect = false;
                    }
                    bool lbl = P(spec, "label") != 0;
                    fig = w.DrawItem(FsNode.Of(card.rt), it, 0, lbl ? cs * tall * 0.1f : 0f, cs * (float)P(spec, "fig"));
                    if (lbl) StoryUI.Label(card.rt, w.NameOf(it), 22, StoryUI.Hex("#3A2A12"), new Vector2(0, -cs * tall * 0.38f), new Vector2(cs * 0.96f, cs * tall * 0.26f), TextAnchor.MiddleCenter, false, "card");
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
                var ovp = OvPos(w, spec, i);
                float py = ovp.HasValue ? ovp.Value.y : yExpr != "" ? w.PP(0, (float)FsExpr.ToNum(w.Ev(yExpr, ex))).y : pts[i].y;
                var cont = StoryUI.Pic(w.Root, "Prop", null, new Color(1, 1, 1, 0), new Vector2(ovp.HasValue ? ovp.Value.x : pts[i].x, py), new Vector2(s * U, hh), raycast: true);
                cont.preserveAspect = false; var contN = FsNode.Of(cont); w.Track(contN);
                var tplRel = new List<object>();
                foreach (var t in tpl) { var d = new Dictionary<string, object>((Dictionary<string, object>)t); d["rel"] = 1.0; tplRel.Add(d); }
                w.MakeObjs(tplRel, contN, w.TaskObjs, ex);
                if (P(spec, "labelBelow") != 0) StoryUI.Label(cont.rectTransform, w.NameOf(it), 26, Color.white, new Vector2(0, -hh * 0.5f - U * 0.045f), new Vector2(U * 0.5f, U * 0.08f), TextAnchor.MiddleCenter, true, "card");
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
        readonly FlowWorld _w; readonly List<Image> _bg = new List<Image>(); readonly List<FsNode> _face = new List<FsNode>(); readonly Dictionary<int, Image> _glow = new Dictionary<int, Image>(); readonly List<GameObject> _q = new List<GameObject>(); readonly bool _plain; readonly bool _hasSlotImg;
        public StripView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; var seq = J.Arr(J.Get(q, "seq")); var items = J.Arr(J.Get(q, "items")); int len = seq.Count; _plain = P(spec, "plain") != 0;
            float slot = Mathf.Min(w.Unit * (float)P(spec, "slotU"), w.WorldW * 0.92f / len * 0.9f), gap = slot * 0.1f, rowW = len * slot + (len - 1) * gap, y = w.PP(0.5f, (float)P(spec, "y")).y;
            var slotSpr = w.ImageSprite(S(spec, "slotImg")); var coverSpr = w.ImageSprite(S(spec, "coverImg")); _hasSlotImg = slotSpr != null;
            for (int i = 0; i < len; i++)
            {
                var sp0 = OvPos(w, spec, i);
                var b = StoryUI.Pic(w.Root, "Slot", ShapeSprites.RoundedRect, _plain ? new Color(1, 1, 1, 0) : new Color(1, 1, 1, 0.9f), sp0 ?? new Vector2(-rowW * 0.5f + slot * 0.5f + i * (slot + gap), y), Vector2.one * slot);
                b.type = Image.Type.Sliced; b.preserveAspect = false; w.Track(FsNode.Of(b)); _bg.Add(b);
                if (slotSpr != null)   // ảnh nền ô là con của ô (nằm dưới hình) để chỉnh tỉ lệ riêng
                {
                    b.color = new Color(1, 1, 1, 0);
                    var bi = StoryUI.Pic(b.transform, "SlotImg", slotSpr, Color.white, Vector2.zero, Vector2.one * slot * (float)P(spec, "slotImgScale")); bi.preserveAspect = false;
                }
                GameObject qq;
                if (coverSpr != null) { var ci = StoryUI.Pic(b.transform, "Cover", coverSpr, Color.white, Vector2.zero, Vector2.one * slot * (float)P(spec, "coverImgScale")); ci.preserveAspect = false; qq = ci.gameObject; }
                else qq = StoryUI.Label(b.transform, "?", Mathf.RoundToInt(slot * 0.6f), StoryUI.Hex("#90A4AE"), Vector2.zero, Vector2.one * slot, TextAnchor.MiddleCenter, false).gameObject;
                qq.SetActive(false); _q.Add(qq);
                // vật có khung riêng (bgImage): vẽ cùng cỡ ô "?" để lật ra không bị nhỏ đi (khớp fs_flow.js strip)
                var it = items[(int)FsExpr.ToNum(seq[i])];
                bool own = J.Str(it, "bgImage") != "" && w.ImageSprite(J.Str(it, "bgImage")) != null;
                _face.Add(w.DrawItem(FsNode.Of(b), it, 0, 0, own ? (coverSpr != null ? slot * (float)P(spec, "coverImgScale") : slot) : slot * 0.82f));
            }
            for (int i = 0; i < len; i++)
            {
                int k = i;
                Parts.Add(new FsPart
                {
                    Show = () => { _face[k].Active = true; _q[k].SetActive(false); _w.Go(StoryUI.PopIn(_face[k].rt, 0.3f)); },
                    Cover = () => { _face[k].Active = false; _q[k].SetActive(true); SetGlow(k, false); },
                    Hide = () => { _face[k].Active = false; _q[k].SetActive(false); SetGlow(k, false); },
                });
            }
        }
        public override void HideAll() { for (int i = 0; i < _face.Count; i++) { _face[i].Active = false; _q[i].SetActive(false); SetGlow(i, false); } }
        void SetGlow(int i, bool on) { if (_glow.TryGetValue(i, out var g) && g != null) g.gameObject.SetActive(on); }
        public void Mark(int i)
        {
            _face[i].Active = true; _q[i].SetActive(false); _w.Go(StoryUI.PopIn(_face[i].rt, 0.3f));
            if (!_plain && !_hasSlotImg) _bg[i].color = StoryUI.Hex("#C8F7C5");
            if ((_plain || _hasSlotImg) && _face[i].G is Image src && src.sprite != null)   // đã chọn đúng: quầng sáng vàng quanh hình (bóng viền theo hình, như drop-shadow bên web) — KHÔNG dùng Outline (nó vẽ 4 bản sao lệch → nhìn như 2 viền)
            {
                if (!_glow.TryGetValue(i, out var gi) || gi == null)
                {
                    var go = new GameObject("Glow", typeof(RectTransform), typeof(Image)); var grt = go.GetComponent<RectTransform>();
                    grt.SetParent(src.rectTransform.parent, false);
                    grt.anchorMin = src.rectTransform.anchorMin; grt.anchorMax = src.rectTransform.anchorMax; grt.pivot = src.rectTransform.pivot;
                    grt.anchoredPosition = src.rectTransform.anchoredPosition; grt.sizeDelta = src.rectTransform.sizeDelta * 1.14f;
                    grt.SetSiblingIndex(src.rectTransform.GetSiblingIndex());   // nằm ngay dưới hình
                    gi = go.GetComponent<Image>(); gi.sprite = src.sprite; gi.type = src.type; gi.preserveAspect = src.preserveAspect; gi.raycastTarget = false;
                    gi.color = new Color(1f, 0.88f, 0.3f, 0f); _glow[i] = gi;
                }
                gi.gameObject.SetActive(true); _w.Go(StoryUI.Fade(gi, 0.8f, 0.3f));
            }
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
            bool uShape = S(spec, "shape") == "u" && total >= 4;   // u: k thẻ mỗi bên (8 thẻ → hàng trên 4, mỗi bên 2), còn lại nằm hết ở hàng trên
            int uk = uShape ? Mathf.Max(1, total / 4) : 0, rows = uShape ? uk + 1 : (pairs <= 5 ? 2 : 3), cols = uShape ? total - 2 * uk : Mathf.CeilToInt(total / (float)rows);
            float areaW = w.WorldW * 0.94f, areaH = w.WorldH * 0.76f, cs = Mathf.Min(areaW / cols, areaH / rows) * 0.88f * (float)P(spec, "cardScale"), stepX = areaW / cols, stepY = areaH / rows; var origin = w.PP(0.5f, 0.47f);
            bool covered = P(spec, "covered") != 0; var backSpr = w.ImageSprite(S(spec, "backImg")); float backScale = (float)P(spec, "backImgScale");
            for (int i = 0; i < total; i++)
            {
                int fi = (int)FsExpr.ToNum(faces[i]), r = i / cols, c = i % cols;
                if (uShape && i >= cols) { r = 1 + (i - cols) / 2; c = (i - cols) % 2 != 0 ? cols - 1 : 0; }
                var holder = StoryUI.Pic(w.Root, "Card", null, new Color(1, 1, 1, 0), OvPos(w, spec, i) ?? (origin + new Vector2((c - (cols - 1) * 0.5f) * stepX, ((rows - 1) * 0.5f - r) * stepY)), Vector2.one * cs, raycast: true);
                holder.preserveAspect = false; var hn = FsNode.Of(holder); w.Track(hn);
                Image back;
                if (backSpr != null)
                {
                    back = StoryUI.Pic(holder.transform, "Back", null, new Color(1, 1, 1, 0), Vector2.zero, Vector2.one * cs); back.preserveAspect = false;
                    var bi = StoryUI.Pic(back.transform, "BackImg", backSpr, Color.white, Vector2.zero, Vector2.one * cs * backScale); bi.preserveAspect = false;
                }
                else
                {
                    back = StoryUI.Pic(holder.transform, "Back", ShapeSprites.RoundedRect, StoryUI.Hex("#26A69A"), Vector2.zero, Vector2.one * cs); back.type = Image.Type.Sliced; back.preserveAspect = false;
                    StoryUI.Label(back.transform, "?", Mathf.RoundToInt(cs * 0.6f), Color.white, Vector2.zero, Vector2.one * cs);
                }
                // vật có ảnh nền riêng (bgImage): mặt ngửa trong suốt, khung của vật thay thẻ trắng
                bool own = J.Str(items[fi], "bgImage") != "" && w.ImageSprite(J.Str(items[fi], "bgImage")) != null;
                var front = StoryUI.Pic(holder.transform, "Front", ShapeSprites.RoundedRect, own ? new Color(1, 1, 1, 0) : Color.white, Vector2.zero, Vector2.one * cs); front.type = Image.Type.Sliced; front.preserveAspect = false;
                w.DrawItem(FsNode.Of(front), items[fi], 0, 0, own ? cs * backScale : cs * 0.82f);
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
                    if (n == 2) StoryUI.Label(a.rt, w.NameOf(it), 30, Color.white, new Vector2(0, size * 0.62f), new Vector2(size * 1.6f, size * 0.3f), TextAnchor.MiddleCenter, true, "card");
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

    // ── stack ── (khớp view stack của fs_flow.js: cụm khối sát nhau, không xếp tầng; ô chọn bởi FlowGens.CubeCells; số đếm vẽ ngoài hình có gạch chỉ)
    public sealed class StackView : FsView
    {
        readonly FlowWorld _w; readonly List<RectTransform> _cubes = new List<RectTransform>(); readonly List<Vector3Int> _cells = new List<Vector3Int>(); readonly float _s, _minX, _maxX, _rowY; readonly Vector2 _center; bool _counted;
        public Vector2 SolvedAt;
        public StackView(FlowWorld w, object spec, Dictionary<string, object> q)
        {
            _w = w; int count = (int)J.Num(q, "number"); float U = w.Unit, W = w.WorldW, H = w.WorldH;
            var cellList = FlowGens.CubeCells(w, count); string cp = S(spec, "color");
            var color = StoryUI.Hex(cp == "random" ? Palette[w.Rng.Range(0, Palette.Length)] : cp);
            var its = J.Arr(J.Get(q, "items")); object obj = its.Count > 0 ? its[0] : null;
            var sorted = new List<Vector3Int>();
            foreach (var cc in cellList) sorted.Add(new Vector3Int(cc[0], cc[1], 0));
            // sắp xếp ổn định theo (x+y+z) tăng dần — khớp Array.sort (ổn định) của JS
            for (int i = 1; i < sorted.Count; i++) { var c = sorted[i]; int j = i - 1; while (j >= 0 && (sorted[j].x + sorted[j].y + sorted[j].z) > (c.x + c.y + c.z)) { sorted[j + 1] = sorted[j]; j--; } sorted[j + 1] = c; }
            float Ux(Vector3Int c) => c.x - c.y; float Uy(Vector3Int c) => -(c.x + c.y) * 0.5f + c.z;
            float uMinX = 1e9f, uMaxX = -1e9f, uMinY = 1e9f, uMaxY = -1e9f;
            foreach (var c in sorted) { uMinX = Mathf.Min(uMinX, Ux(c) - 1f); uMaxX = Mathf.Max(uMaxX, Ux(c) + 1f); uMinY = Mathf.Min(uMinY, Uy(c) - 1f); uMaxY = Mathf.Max(uMaxY, Uy(c) + 1f); }
            float uRow = uMaxY + 1.35f, uTop = uRow + 0.6f + (RowTiers(sorted, out _) - 1) * RowStep; int n0 = sorted.Count;   // số xếp theo HÀNG: mỗi hàng khối (cùng x) 1 hàng số
            // Cỡ khối chỉ phụ thuộc SỐ KHỐI (không phụ thuộc cách xếp) để cùng số khối thì cùng cỡ ở mọi ô và cả 2 đội; tính theo cách xếp xấu nhất của số đó.
            // Vùng trống phía trên hàng đáp án ≈ 0,62H (gồm cả hàng số đếm).
            WorstUnits(n0, out float wU, out float hU);
            float s = Mathf.Min(W * 0.9f / wU, H * 0.60f / hU, U * 0.17f); _s = s;
            _minX = uMinX * s; _maxX = uMaxX * s; _rowY = uRow * s;
            var pc = w.PP(0.5f, 0.665f); var center = new Vector2(pc.x - (_minX + _maxX) * 0.5f, pc.y - (uMinY + uTop) * s * 0.5f); _center = center;
            foreach (var c in sorted)
            {
                float px = center.x + Ux(c) * s, py = center.y + Uy(c) * s; RectTransform rt;
                if (obj != null) rt = w.DrawItem(FsNode.Of(w.Root), obj, px, py, 2f * s * 0.9f).rt;
                else
                {
                    rt = StoryUI.Rect(w.Root, "Cube", new Vector2(px, py), Vector2.one * 2f * s);
                    var g = rt.gameObject.AddComponent<StoryCube>(); g.color = color; g.raycastTarget = false;
                }
                rt.localScale = Vector3.zero; w.Track(FsNode.Of(rt)); _cubes.Add(rt); _cells.Add(c);
            }
            SolvedAt = new Vector2(center.x + (_minX + _maxX) * 0.5f, center.y + s);
            foreach (var cb in _cubes) { var r = cb; Parts.Add(new FsPart { Show = () => _w.Go(StoryUI.PopIn(r, 0.25f)), Cover = () => r.localScale = Vector3.zero, Hide = () => r.localScale = Vector3.zero }); }
        }
        // Bề rộng / bề cao (đơn vị s, gồm cả các hàng số đếm) của cách xếp XẤU NHẤT mà FlowGens.CubeCells có thể sinh cho n khối (cụm trong lưới 3×3 + khối lẻ ngoài lưới).
        static void WorstUnits(int n, out float wU, out float hU)
        {
            wU = n * 1.1f; hU = 0f;
            int outMin = n >= 10 ? n - 9 : 0, outMax = n >= 10 ? n - 9 : n >= 4 ? Mathf.Min(3, n - 3) : 0;
            for (int o = outMin; o <= outMax; o++)
            {
                int c = Mathf.Min(n - o, 9), rows = Mathf.Min(c, 3); float span = Mathf.Min(c - 1, 4);
                float w = span + 2f + (o > 0 ? 4f * ((o + 1) / 2) : 0f);
                float cubesH = o >= 2 ? 6.5f : span * 0.5f + 2f;   // khối lẻ thứ 2 của mỗi cột nằm thấp hơn 3 đơn vị
                wU = Mathf.Max(wU, w); hU = Mathf.Max(hU, cubesH + 1.35f + 0.6f + (rows - 1) * RowStep);
            }
        }
        public override void HideAll() { foreach (var c in _cubes) c.localScale = Vector3.zero; }
        public override IEnumerator Intro() { foreach (var c in _cubes) { if (c == null) yield break; _w.Go(StoryUI.PopIn(c, 0.25f)); yield return new WaitForSeconds(0.07f); } }
        // Thứ tự đếm: cụm chính (trong lưới 3×3) trước, khối lẻ (tách riêng ngoài lưới) sau. Cụm chính đếm theo "hàng" đẳng cự (cùng x, từ hàng trên/xa
        // xuống hàng dưới/gần), mỗi hàng đếm từ trái sang phải (y giảm dần). Khớp JS `countOrder`.
        public static List<int> CountOrder(List<Vector3Int> cells)
        {
            var main = new List<int>(); var rest = new List<int>();
            for (int i = 0; i < cells.Count; i++) { var c = cells[i]; if (c.x >= 0 && c.x <= 2 && c.y >= 0 && c.y <= 2) main.Add(i); else rest.Add(i); }
            if (main.Count == 0) { main = rest; rest = new List<int>(); }
            Comparison<int> byRow = (a, b) => { int d = cells[a].x.CompareTo(cells[b].x); return d != 0 ? d : cells[b].y.CompareTo(cells[a].y); };
            // khối lẻ (tách hẳn sang 1 bên): từ trái sang phải, trên xuống dưới
            Comparison<int> bySide = (a, b) => { int d = (cells[a].x - cells[a].y).CompareTo(cells[b].x - cells[b].y); return d != 0 ? d : (cells[a].x + cells[a].y).CompareTo(cells[b].x + cells[b].y); };
            main.Sort(byRow); rest.Sort(bySide);
            main.AddRange(rest); return main;
        }

        // Số đếm xếp theo HÀNG: mỗi hàng khối của cụm chính (cùng x) có 1 hàng số nằm ngang thẳng nhau (1-2-3 / 4-5-6 / 7-8-9); hàng đầu cao nhất, hàng sau thấp dần;
        // khối lẻ ngang hàng với hàng cuối. tier[i] = chỉ số hàng (0 = trên cùng); trả số hàng. Khớp JS `rowTiers`.
        const float RowStep = 1.2f;   // khoảng cách các hàng số (số rộng 1,1s)
        public static int RowTiers(List<Vector3Int> cells, out int[] tier)
        {
            var main = new List<int>(); var rest = new List<int>();
            for (int i = 0; i < cells.Count; i++) { var c = cells[i]; if (c.x >= 0 && c.x <= 2 && c.y >= 0 && c.y <= 2) main.Add(i); else rest.Add(i); }
            if (main.Count == 0) { main = rest; rest = new List<int>(); }
            var xs = new List<int>(); foreach (int i in main) if (!xs.Contains(cells[i].x)) xs.Add(cells[i].x);
            xs.Sort(); tier = new int[cells.Count];
            foreach (int i in main) tier[i] = xs.IndexOf(cells[i].x);
            foreach (int i in rest) tier[i] = xs.Count - 1;
            return xs.Count;
        }

        // Dây thẳng đứng: x của dây mỗi khối = tâm khối ± lệch theo cột (khối đếm trước lệch trái, đếm sau lệch phải), rồi dãn để 2 dây kề nhau cách ≥ 0,65s
        // (dây không đụng số khác) và 2 số CÙNG hàng cách ≥ 1,2s (số không chồng nhau). Khớp JS `badgeLayout`.
        void BadgeLayout(List<int> order, int[] tier, out float[] lx)
        {
            int n = order.Count; float s = _s; lx = new float[n];
            var colCount = new Dictionary<int, int>(); var colRank = new Dictionary<int, int>(); var rank = new int[n];
            foreach (int i in order) { int u = _cells[i].x - _cells[i].y; colCount[u] = colCount.TryGetValue(u, out var m0) ? m0 + 1 : 1; }
            var want = new float[n];
            foreach (int i in order)
            {
                int u = _cells[i].x - _cells[i].y, m = colCount[u], r = colRank.TryGetValue(u, out var rr) ? rr : 0; colRank[u] = r + 1; rank[i] = r;
                want[i] = _cubes[i].anchoredPosition.x + (r - (m - 1) * 0.5f) * (m <= 2 ? 0.6f : 0.5f) * s;
            }
            var idx = new List<int>(); for (int i = 0; i < n; i++) idx.Add(i);
            idx.Sort((a, b) => { int d = want[a].CompareTo(want[b]); if (d != 0) return d; d = (_cells[a].x - _cells[a].y).CompareTo(_cells[b].x - _cells[b].y); return d != 0 ? d : rank[a].CompareTo(rank[b]); });
            float prev = float.NegativeInfinity; var lastT = new Dictionary<int, float>();
            foreach (int i in idx)
            {
                float x = Mathf.Max(want[i], prev + 0.65f * s); int t = tier[i];
                if (lastT.TryGetValue(t, out var lt)) x = Mathf.Max(x, lt + 1.2f * s);
                lx[i] = prev = lastT[t] = x;
            }
        }

        public IEnumerator CountAloud(Action onDone)
        {
            // số nằm ngoài hình (hàng trên), dây THẲNG ĐỨNG chỉ xuống mặt trên của đúng khối đó
            var order = CountOrder(_cells); int rows = RowTiers(_cells, out var tiers); BadgeLayout(order, tiers, out var lxs);
            int n = order.Count; float s = _s, by = _center.y + _rowY;
            var shapes = new List<GameObject>();
            for (int k = 0; k < n; k++)
            {
                int idx = order[k]; var cb = _cubes[idx]; if (cb == null) yield break;
                // Dây xuất phát đúng từ tâm mặt trên của khối (không bị BadgeLayout đẩy lệch sang khối bên cạnh) rồi nghiêng nhẹ tới chân số.
                float lx = lxs[idx], cx = cb.anchoredPosition.x, ty = cb.anchoredPosition.y + s * 0.5f, bY = by + (rows - 1 - tiers[idx]) * RowStep * s;
                float ey = bY - s * 0.55f, dx = lx - cx, dy = Mathf.Max(1f, ey - ty), len = Mathf.Sqrt(dx * dx + dy * dy);
                var ln = StoryUI.Pic(_w.Root, "Line", ShapeSprites.Square, new Color(0.15f, 0.1f, 0.3f, 0.7f), new Vector2((cx + lx) * 0.5f, ty + dy * 0.5f), new Vector2(Mathf.Max(2f, s * 0.06f), len));
                ln.preserveAspect = false;   // Pic mặc định giữ tỉ lệ → đoạn thẳng mảnh co thành chấm nhỏ
                ln.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                ln.rectTransform.localScale = Vector3.zero; _w.Track(FsNode.Of(ln)); shapes.Add(ln.gameObject); _w.Go(StoryUI.PopIn(ln.rectTransform, 0.15f));
                var b = StoryUI.Pic(_w.Root, "Badge", ShapeSprites.Circle, new Color(0.15f, 0.1f, 0.3f, 0.9f), new Vector2(lx, bY), Vector2.one * s * 1.1f);
                StoryUI.Label(b.transform, (k + 1).ToString(), Mathf.RoundToInt(s * 0.91f), Color.white, Vector2.zero, Vector2.one * s * 1.1f);
                shapes.Add(b.gameObject); _w.Track(FsNode.Of(b)); _w.Go(StoryUI.PopIn(b.rectTransform, 0.2f)); _w.Sfx("tap");
                yield return new WaitForSeconds(0.4f);
            }
            onDone?.Invoke();
            yield return new WaitForSeconds(1.6f);
            foreach (var b in shapes) if (b != null) UnityEngine.Object.Destroy(b);
        }
        public override void HintExtra() { if (!_counted) { _counted = true; _w.Go(CountAloud(null)); } }
    }
}
