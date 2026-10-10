using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>FlowWorld — đối tượng cảnh, ràng buộc (bind), hoạt hình nhẹ và danh sách hành động. Bản C# của fs_act.js (PHẢI khớp).</summary>
public sealed partial class FlowWorld
{
    // ── vẽ ──
    string ImagePath(string name) => "StoryData/" + _gid + "/" + Path.GetFileNameWithoutExtension(name);

    public static Sprite SpriteOf(string name)
    {
        if (name == "rrect") return ShapeSprites.RoundedRect;
        if (name == "rect" || string.IsNullOrEmpty(name)) return ShapeSprites.Square;
        return ShapeSprites.ByName(name);
    }

    public Dictionary<string, object> FindItem(string id)
    {
        foreach (var it in _items) if (J.Str(it, "id") == id) return it as Dictionary<string, object>;
        return null;
    }

    /// <summary>Vẽ 1 vật theo diện mạo của item: ảnh &gt; icon dựng sẵn &gt; hình + màu. `size` = cạnh hộp.</summary>
    public FsNode DrawItem(FsNode parent, object item, float x, float y, float size)
    {
        string image = J.Str(item, "image");
        if (image != "")
        {
            var spr = StoryUI.Load(ImagePath(image));
            if (spr != null) return FsNode.Of(StoryUI.Pic(parent.rt, "Item", spr, Color.white, new Vector2(x, y), new Vector2(size, size)));
        }
        string icon = J.Str(item, "icon");
        if (icon != "" && Array.IndexOf(StoryIcons.Keys, icon) >= 0) return FsNode.Of(StoryIcons.Build(parent.rt, icon, new Vector2(x, y), size));
        string sh = J.Str(item, "shape", "circle");
        bool rect = sh == "rect";
        var im = StoryUI.Pic(parent.rt, "Item", SpriteOf(rect ? "square" : sh), Hex(J.Str(item, "color", "#90A4AE")), new Vector2(x, y),
                             new Vector2(size * (rect ? 1.5f : 1f), size * (rect ? 0.95f : 1f)));
        im.preserveAspect = false;
        return FsNode.Of(im);
    }

    public FsNode DrawLook(FsNode parent, object look, float x, float y, float w, float h, Dictionary<string, object> extra)
    {
        var lk = look as Dictionary<string, object> ?? new Dictionary<string, object>();
        string kind = J.Str(lk, "kind");
        var pos = new Vector2(x, y);
        if (kind == "hand")
        {
            var root2 = StoryUI.Rect(parent.rt, "Hand", pos, Vector2.zero);
            float len = (float)(FsExpr.ToNum(Ev(J.Get(lk, "len"), extra)) is double l0 && l0 != 0 ? l0 : 0.3) * U;
            float th = (float)(FsExpr.ToNum(Ev(J.Get(lk, "thick"), extra)) is double t0 && t0 != 0 ? t0 : 0.03) * U;
            var b = StoryUI.Pic(root2, "HandBar", ShapeSprites.Square, Hex(FsExpr.Str(Ev(J.Get(lk, "color"), extra)) is string c0 && c0 != "" ? c0 : "#2A2F55"), new Vector2(0, len / 2f), new Vector2(th, len));
            b.preserveAspect = false;
            return FsNode.Of(root2);
        }
        if (lk.ContainsKey("text"))
        {
            double fsv = FsExpr.ToNum(Ev(J.Get(lk, "fs"), extra));
            int fs = Mathf.RoundToInt((float)(fsv != 0 ? fsv : 0.06) * U);
            string col = FsExpr.Str(Ev(J.Get(lk, "color"), extra)); if (col == "") col = "#FFFFFF";
            bool outline = !(lk.ContainsKey("outline") && J.Num(lk, "outline", 1) == 0);
            var t = StoryUI.Label(parent.rt, FsExpr.Str(Ev(J.Get(lk, "text"), extra)), fs, Hex(col), pos, new Vector2(w != 0 ? w : U, h != 0 ? h : U * 0.2f), TextAnchor.MiddleCenter, outline);
            return FsNode.Of(t);
        }
        string itemId = lk.ContainsKey("item") ? FsExpr.Str(Ev(J.Get(lk, "item"), extra)) : "";
        if (itemId != "")
        {
            var it = FindItem(itemId);
            if (it != null)
            {
                float sz = Mathf.Min(w, h) != 0 ? Mathf.Min(w, h) : w;
                var n = DrawItem(parent, it, x, y, sz);
                if (w != 0 && h != 0 && J.Str(it, "image") == "" && J.Str(it, "icon") == "") n.SetSize(w, h);
                return n;
            }
        }
        string image = FsExpr.Str(Ev(J.Get(lk, "image"), extra));
        if (image != "")
        {
            var spr = StoryUI.Load(ImagePath(image));
            if (spr != null) return FsNode.Of(StoryUI.Pic(parent.rt, "Img", spr, Color.white, pos, new Vector2(w, h)));
        }
        string icon = FsExpr.Str(Ev(J.Get(lk, "icon"), extra));
        if (icon != "" && Array.IndexOf(StoryIcons.Keys, icon) >= 0) return FsNode.Of(StoryIcons.Build(parent.rt, icon, pos, Mathf.Min(w, h)));
        string sh = FsExpr.Str(Ev(J.Get(lk, "shape"), extra)); if (sh == "") sh = kind == "rrect" ? "rrect" : "square";
        string cs = FsExpr.Str(Ev(J.Get(lk, "color"), extra)); if (cs == "") cs = "#90A4AE";
        var im = StoryUI.Pic(parent.rt, "Shape", SpriteOf(sh), Hex(cs), pos, new Vector2(w, h));
        im.preserveAspect = false;
        if (sh == "rrect") im.type = Image.Type.Sliced;
        return FsNode.Of(im);
    }

    // ── đối tượng cảnh ──
    public FsObj MakeObj(Dictionary<string, object> spec, FsNode parent, List<FsObj> store, Dictionary<string, object> extra)
    {
        double N(string k, double d) => spec.ContainsKey(k) && spec[k] != null ? FsExpr.ToNum(Ev(spec[k], extra)) : d;
        double fx = N("x", 0.5), fy = N("y", 0.5);
        float w = (float)N("w", 0.2) * U, h = spec.ContainsKey("h") ? (float)N("h", 0.2) * U : w;
        bool rel = J.Num(spec, "rel") != 0;
        var pos = rel ? Vector2.zero : P((float)fx, (float)fy);
        string id = spec.ContainsKey("id") ? FsExpr.Str(Ev(spec["id"], extra)) : null;
        var o = new FsObj { spec = spec, extra = extra ?? new Dictionary<string, object>(), id = string.IsNullOrEmpty(id) ? null : id, role = J.Str(spec, "role") == "" ? null : J.Str(spec, "role"), t0 = (float)(_rng.Next() * 6.28) };
        var lk = J.Obj(spec, "look");
        if (J.Num(spec, "fill") != 0)
        {
            string itId = lk != null && lk.ContainsKey("item") ? FsExpr.Str(Ev(lk["item"], extra)) : "";
            var itm = itId != "" ? FindItem(itId) : null;
            string imgName = lk != null && lk.ContainsKey("image") ? FsExpr.Str(Ev(lk["image"], extra)) : (itm != null ? J.Str(itm, "image") : "");
            Sprite spr = imgName != "" ? StoryUI.Load(ImagePath(imgName)) : null;
            Image fill = spr != null ? StoryUI.Fill(parent.rt, "Fill", Color.white, spr) : StoryUI.Fill(parent.rt, "Fill", Hex(lk != null ? FsExpr.Str(Ev(J.Get(lk, "color"), extra)) : "#FFFFFF"));
            if (spr != null) fill.preserveAspect = false;
            o.node = FsNode.Of(fill);
        }
        else
        {
            float dx = (float)(spec.ContainsKey("dx") ? FsExpr.ToNum(Ev(spec["dx"], extra)) : 0) * U, dy = (float)(spec.ContainsKey("dy") ? FsExpr.ToNum(Ev(spec["dy"], extra)) : 0) * U;
            o.node = DrawLook(parent, lk, pos.x + dx, pos.y + dy, w, h, extra);
        }
        o.x0 = o.node.X; o.y0 = o.node.Y;
        if (spec.ContainsKey("rot")) o.node.Rot = (float)N("rot", 0);
        if (spec.ContainsKey("scale")) o.node.Scale = (float)N("scale", 1);
        if (spec.ContainsKey("alpha")) o.node.Alpha = (float)N("alpha", 1);
        if (spec.ContainsKey("show")) o.node.Active = FsExpr.Truthy(Ev(spec["show"], extra));
        store.Add(o);
        return o;
    }

    public void MakeObjs(List<object> specs, FsNode parent, List<FsObj> store, Dictionary<string, object> extra)
    {
        foreach (var so in specs)
        {
            var spec = so as Dictionary<string, object>; if (spec == null) continue;
            var fe = J.Obj(spec, "forEachItem");
            if (fe != null)
            {
                for (int idx = 0; idx < _items.Count; idx++)
                {
                    var ex = new Dictionary<string, object>(extra ?? new Dictionary<string, object>()) { { "item", _items[idx] }, { "idx", (double)idx } };
                    string where = J.Str(fe, "where");
                    if (where != "" && !FsExpr.Truthy(FsExpr.Eval(where, Scope(ex)))) continue;
                    MakeObj(spec, parent, store, ex);
                }
            }
            else if (J.Obj(spec, "repeat") != null)
            {
                var rp = J.Obj(spec, "repeat");
                int n = (int)Math.Round(Nv(J.Get(rp, "n"), 0, extra));
                for (int i = 0; i < n; i++)
                {
                    var s = new Dictionary<string, object>(spec);
                    s["x"] = (spec.ContainsKey("x") ? FsExpr.ToNum(Ev(spec["x"], extra)) : 0.5) + i * J.Num(rp, "dx");
                    s["y"] = (spec.ContainsKey("y") ? FsExpr.ToNum(Ev(spec["y"], extra)) : 0.5) + i * J.Num(rp, "dy");
                    s.Remove("repeat");
                    var ex = new Dictionary<string, object>(extra ?? new Dictionary<string, object>()) { { "i", (double)i }, { "n", (double)n } };
                    MakeObj(s, parent, store, ex);
                }
            }
            else MakeObj(spec, parent, store, extra);
        }
    }

    void UpdateObjs(List<FsObj> list, float dt, float time)
    {
        for (int li = 0; li < list.Count; li++)
        {
            var o = list[li];
            if (o.Dead) continue;
            var nd = o.node; var sp = o.spec;
            var bind = J.Obj(sp, "bind");
            if (bind != null)
            {
                float smooth = sp.ContainsKey("smooth") ? (float)J.Num(sp, "smooth") : 0.3f;
                float k = smooth <= 0f ? 1f : 1f - Mathf.Exp(-dt / (smooth / 3f));
                foreach (var kv in bind)
                {
                    object tv;
                    try { tv = FsExpr.Eval(FsExpr.Str(kv.Value), Scope(o.extra)); } catch (Exception) { continue; }
                    float num = (float)FsExpr.ToNum(tv);
                    switch (kv.Key)
                    {
                        case "show": nd.Active = FsExpr.Truthy(tv); break;
                        case "item":
                            { string s = FsExpr.Str(tv); if (!o.cur.Contains("item:" + s)) { o.cur.RemoveWhere(c => c.StartsWith("item:")); o.cur.Add("item:" + s); Relook(o, s); nd = o.node; } break; }
                        case "text": if (nd.txt != null) nd.txt.text = FsExpr.Str(tv); break;
                        case "color": { var c = Hex(FsExpr.Str(tv)); nd.Color = o.cur.Contains("color") ? Color.Lerp(nd.Color, c, k) : c; o.cur.Add("color"); break; }
                        case "x": { float px = P(num, 0).x; o.x0 = o.cur.Contains("x") ? o.x0 + (px - o.x0) * k : px; o.cur.Add("x"); if (!o.moving) nd.X = o.x0; break; }
                        case "y": { float py = P(0, num).y; o.y0 = o.cur.Contains("y") ? o.y0 + (py - o.y0) * k : py; o.cur.Add("y"); if (!o.moving) nd.Y = o.y0; break; }
                        case "w": { float v = num * U; nd.SetSize(o.cur.Contains("w") ? nd.W + (v - nd.W) * k : v, nd.H); o.cur.Add("w"); break; }
                        case "h": { float v = num * U; nd.SetSize(nd.W, o.cur.Contains("h") ? nd.H + (v - nd.H) * k : v); o.cur.Add("h"); break; }
                        case "scale": nd.Scale = o.cur.Contains("scale") ? nd.Scale + (num - nd.Scale) * k : num; o.cur.Add("scale"); break;
                        case "rot": nd.Rot = o.cur.Contains("rot") ? nd.Rot + (num - nd.Rot) * k : num; o.cur.Add("rot"); break;
                        case "alpha": nd.Alpha = o.cur.Contains("alpha") ? nd.Alpha + (num - nd.Alpha) * k : num; o.cur.Add("alpha"); break;
                    }
                }
            }
            var an = J.Obj(sp, "anim");
            if (an != null && o.animOn && !o.moving)
            {
                float amp = (float)(J.Num(an, "amp") != 0 ? J.Num(an, "amp") : 0.012) * U, speed = (float)(J.Num(an, "speed") != 0 ? J.Num(an, "speed") : 3);
                float s = Mathf.Sin(time * speed + o.t0);
                if (J.Str(an, "type") == "pulse") nd.Scale = 1f + (float)(J.Num(an, "amp") != 0 ? J.Num(an, "amp") : 0.06) * (0.5f + 0.5f * s);
                else nd.Y = o.y0 + s * amp;
            }
        }
    }

    void Relook(FsObj o, string itemId)
    {
        var old = o.node; var parent = FsNode.Of(old.rt.parent as RectTransform);
        float w = old.W, h = old.H, x = old.X, y = old.Y, rot = old.Rot;
        var n = DrawLook(parent, new Dictionary<string, object> { { "item", itemId } }, x, y, w, h, o.extra);
        n.Rot = rot; o.node = n; old.Destroy();
        if (!o.spec.ContainsKey("show")) n.Active = true;
    }

    public FsObj ObjById(string id) { foreach (var o in Objs) if (!o.Dead && o.id == id) return o; foreach (var o in TaskObjs) if (!o.Dead && o.id == id) return o; return null; }
    public FsObj ObjByRole(string role) { foreach (var o in Objs) if (!o.Dead && o.role == role) return o; foreach (var o in TaskObjs) if (!o.Dead && o.role == role) return o; return null; }
    FsObj ObjOf(FsNode n) { foreach (var o in Objs) if (o.node == n) return o; foreach (var o in TaskObjs) if (o.node == n) return o; return null; }

    // ── đích của hành động ──
    List<FsNode> NodesOf(object spec, ActCtx cx)
    {
        var res = new List<FsNode>();
        if (spec == null || spec is Dictionary<string, object>) return res;
        var sv = FsExpr.Val(spec, Scope(cx.ToScope()));
        if (!(sv is string s)) return res;
        if (s == "@tapped") { if (cx.tapped != null) res.Add(cx.tapped.node); return res; }
        if (s == "@target") { int t = (int)J.Num(Q, "target", -1); foreach (var e in Elems) if (e.key == t) res.Add(e.node); return res; }
        if (s == "@actor") { var a = ObjByRole("actor"); if (a != null) res.Add(a.node); return res; }
        var o = ObjById(s.Length > 0 && s[0] == '#' ? s.Substring(1) : s);
        if (o != null) res.Add(o.node);
        return res;
    }

    Vector2 PointOf(object spec, ActCtx cx)
    {
        if (spec is Dictionary<string, object> d)
        {
            var sc = cx.ToScope();
            if (d.ContainsKey("ref"))
            {
                var b = PointOf(d["ref"], cx);
                return new Vector2(b.x + (float)Nv(J.Get(d, "dx"), 0, sc) * U, b.y + (float)Nv(J.Get(d, "dy"), 0, sc) * U);
            }
            if (d.ContainsKey("fx"))
            {
                var p = P((float)Nv(d["fx"], 0, sc), (float)Nv(d["fy"], 0, sc));
                return new Vector2(p.x + (float)Nv(J.Get(d, "dx"), 0, sc) * U, p.y + (float)Nv(J.Get(d, "dy"), 0, sc) * U);
            }
        }
        var ns = NodesOf(spec, cx);
        if (ns.Count > 0) return new Vector2(ns[0].X, ns[0].Y);
        if (spec is string st && st == "@tap" && cx.tap.HasValue) return cx.tap.Value;
        return Vector2.zero;
    }

    IEnumerator MoveObj(FsNode node, Vector2 to, float sec)
    {
        var o = ObjOf(node);
        if (o != null) o.moving = true;
        yield return StoryUI.MoveTo(node.rt, to, sec);
        if (o != null) { o.x0 = to.x; o.y0 = to.y; o.moving = false; }
    }

    public static IEnumerator RotTo(RectTransform rt, float to, float dur)
    {
        if (rt == null) yield break;
        float from = rt.localEulerAngles.z > 180f ? rt.localEulerAngles.z - 360f : rt.localEulerAngles.z;
        if (dur <= 0f) { rt.localRotation = Quaternion.Euler(0, 0, to); yield break; }
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / dur; k = k * k * (3f - 2f * k);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(from, to, k));
            yield return null;
        }
        if (rt != null) rt.localRotation = Quaternion.Euler(0, 0, to);
    }

    // ── hành động ──
    public IEnumerator RunActs(List<object> list, ActCtx cx)
    {
        if (list == null) yield break;
        cx = cx ?? new ActCtx();
        foreach (var ao in list)
        {
            var a = ao as Dictionary<string, object>; if (a == null) continue;
            var sc = cx.ToScope();
            float Nz(string k, float d) => a.ContainsKey(k) ? (float)Nv(a[k], d, sc) : d;
            bool noWait = a.ContainsKey("wait") && J.Num(a, "wait") == 0;
            switch (J.Str(a, "do"))
            {
                case "sfx": { string k = FsExpr.Str(Ev(J.Get(a, "k"), sc)); Sfx(k == "" ? "tap" : k); break; }
                case "std": if (J.Str(a, "ev") == "wrong") yield return StdWrong(cx); else yield return StdRight(cx); break;
                case "say":
                    {
                        string col = FsExpr.Str(Ev(J.Get(a, "color"), sc)); if (col == "") col = "#FFFFFF";
                        Talk(FsExpr.Template(J.Str(a, "text"), J.Obj(Q, "vars"), Scope(sc)), P(0.5f, Nz("fy", 0.14f)), Hex(col), Nz("sec", 1.6f)); break;
                    }
                case "confetti": { var p = a.ContainsKey("at") ? PointOf(a["at"], cx) : Vector2.zero; Burst(p, (int)Nz("n", 12), !(a.ContainsKey("sound") && J.Num(a, "sound") == 0)); break; }
                case "bounce": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) Run(StoryUI.Bounce(nd.rt, Nz("amount", 0.25f), Nz("sec", 0.5f))); break;
                case "shake": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) Run(StoryUI.Shake(nd.rt, Nz("sec", 0.35f), Nz("amp", 10f))); break;
                case "pulse": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) Run(StoryUI.Pulse(nd.rt, () => true, 0.12f, 7f)); break;
                case "move":
                    {
                        var to = PointOf(J.Get(a, "to"), cx); float sec = Nz("sec", 0.5f);
                        var gens = new List<IEnumerator>(); foreach (var nd in NodesOf(J.Get(a, "on"), cx)) gens.Add(MoveObj(nd, to, sec));
                        if (noWait) foreach (var g in gens) Run(g);
                        else { for (int i = 1; i < gens.Count; i++) Run(gens[i]); if (gens.Count > 0) yield return gens[0]; }
                        break;
                    }
                case "scale":
                    {
                        var gens = new List<IEnumerator>(); foreach (var nd in NodesOf(J.Get(a, "on"), cx)) gens.Add(StoryUI.ScaleTo(nd.rt, Nz("to", 1f), Nz("sec", 0.4f), J.Num(a, "overshoot") != 0));
                        if (noWait) foreach (var g in gens) Run(g); else { for (int i = 1; i < gens.Count; i++) Run(gens[i]); if (gens.Count > 0) yield return gens[0]; }
                        break;
                    }
                case "fade":
                    {
                        var gens = new List<IEnumerator>(); foreach (var nd in NodesOf(J.Get(a, "on"), cx)) if (nd.G != null) gens.Add(StoryUI.Fade(nd.G, Nz("to", 0f), Nz("sec", 0.3f)));
                        if (noWait) foreach (var g in gens) Run(g); else { for (int i = 1; i < gens.Count; i++) Run(gens[i]); if (gens.Count > 0) yield return gens[0]; }
                        break;
                    }
                case "tint":
                    {
                        var c = Hex(FsExpr.Str(Ev(J.Get(a, "color"), sc)));
                        var gens = new List<IEnumerator>(); foreach (var nd in NodesOf(J.Get(a, "on"), cx)) if (nd.G != null) gens.Add(StoryUI.ColorTo(nd.G, c, Nz("sec", 0.3f)));
                        if (noWait) foreach (var g in gens) Run(g); else { for (int i = 1; i < gens.Count; i++) Run(gens[i]); if (gens.Count > 0) yield return gens[0]; }
                        break;
                    }
                case "rotate":
                    {
                        var gens = new List<IEnumerator>(); foreach (var nd in NodesOf(J.Get(a, "on"), cx)) gens.Add(RotTo(nd.rt, Nz("to", 0f), Nz("sec", 0.5f)));
                        if (noWait) foreach (var g in gens) Run(g); else { for (int i = 1; i < gens.Count; i++) Run(gens[i]); if (gens.Count > 0) yield return gens[0]; }
                        break;
                    }
                case "show": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) nd.Active = true; break;
                case "hide": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) nd.Active = false; break;
                case "pop": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) { nd.Active = true; Run(StoryUI.PopIn(nd.rt, Nz("sec", 0.3f))); } break;
                case "destroy": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) nd.Destroy(); break;
                case "spawn":
                    {
                        var p = a.ContainsKey("at") ? PointOf(a["at"], cx) : Vector2.zero;
                        float s = Nz("size", 0.2f) * U, sw = a.ContainsKey("w") ? Nz("w", 0.2f) * U : s, sh = a.ContainsKey("h") ? Nz("h", 0.2f) * U : s;
                        string sid = a.ContainsKey("id") ? FsExpr.Str(Ev(a["id"], sc)) : null;
                        var o = new FsObj { spec = new Dictionary<string, object>(), extra = new Dictionary<string, object>(), id = string.IsNullOrEmpty(sid) ? null : sid, role = J.Str(a, "role") == "" ? null : J.Str(a, "role") };
                        o.node = DrawLook(FsNode.Of(root), J.Get(a, "look"), p.x, p.y, sw, sh, sc); o.x0 = o.node.X; o.y0 = o.node.Y; TaskObjs.Add(o);
                        if (!(a.ContainsKey("pop") && J.Num(a, "pop") == 0)) Run(StoryUI.PopIn(o.node.rt, 0.3f));
                        break;
                    }
                case "set": Vars[FsExpr.Str(Ev(J.Get(a, "var"), sc))] = Ev(J.Get(a, "expr"), sc); break;
                case "inc":
                    {
                        string vn = FsExpr.Str(Ev(J.Get(a, "var"), sc));
                        double v = FsExpr.ToNum(Vars.ContainsKey(vn) ? Vars[vn] : 0.0) + Nz("by", 1f);
                        if (a.ContainsKey("mod")) { double m = Nv(a["mod"], 0, sc); if (m > 0) v = ((v % m) + m) % m; }
                        Vars[vn] = v; break;
                    }
                case "bag":
                    {
                        var vals = new List<string>(); foreach (var s0 in FsExpr.Str(Ev(J.Get(a, "values"), sc)).Split(',')) vals.Add(s0.Trim());
                        string key = J.Str(a, "var");
                        if (!_bags.TryGetValue(key, out var bag) || bag.Count == 0) { bag = new List<string>(vals); ShuffleList(bag); _bags[key] = bag; }
                        Vars[key] = bag[0]; bag.RemoveAt(0); break;
                    }
                case "pickItem":
                    {
                        string expr = "randItem(\"" + J.Str(a, "tag") + "\",\"" + J.Str(a, "value") + "\")";
                        string r = FsExpr.Str(FsExpr.Eval(expr, Scope(sc))); Vars[J.Str(a, "var")] = r; break;
                    }
                case "if":
                    {
                        bool c = FsExpr.Truthy(FsExpr.Eval(J.Str(a, "cond"), Scope(sc)));
                        yield return RunActs(c ? J.List(a, "then") : J.List(a, "else"), cx); break;
                    }
                case "wait": yield return new WaitForSeconds(Nz("sec", 0.3f)); break;
                case "text": foreach (var nd in NodesOf(J.Get(a, "on"), cx)) if (nd.txt != null) nd.txt.text = FsExpr.Template(J.Str(a, "text"), J.Obj(Q, "vars"), Scope(sc)); break;
                case "par":
                    {
                        var flags = new List<bool[]>();
                        foreach (var l in J.Arr(J.Get(a, "acts")))
                        {
                            var f = new bool[1]; flags.Add(f);
                            var acts = l is List<object> ll ? ll : new List<object> { l };
                            Run(ParRun(acts, cx, f));
                        }
                        while (flags.Exists(f => !f[0])) yield return null;
                        break;
                    }
                case "fly":
                    {
                        var from = cx.tapped != null ? new Vector2(cx.tapped.node.X, cx.tapped.node.Y) : Vector2.zero;
                        var to = PointOf(J.Get(a, "to"), cx); var ns = NodesOf(J.Get(a, "to"), cx);
                        float s0 = cx.tapped != null ? Mathf.Min(cx.tapped.node.W, cx.tapped.node.H) * 0.8f : U * 0.2f;
                        object look = J.Has(a, "look") ? a["look"] : new Dictionary<string, object> { { "item", "=tapped.item" } };
                        var ex = cx.ToScope();
                        if (cx.tapped != null) { var t2 = cx.tapped.ToScope(); t2["item"] = cx.tapped.itemId; ex["tapped"] = t2; }
                        var o = new FsObj { spec = new Dictionary<string, object>(), extra = new Dictionary<string, object>(), animOn = false };
                        o.node = DrawLook(FsNode.Of(root), look, from.x, from.y, s0, s0, ex); o.node.SetAsLast(); TaskObjs.Add(o);
                        float sx = ns.Count > 0 ? ns[0].W : s0, sy = ns.Count > 0 ? ns[0].H : s0;
                        yield return StoryUI.MoveResize(o.node.rt, to, new Vector2(sx, sy), Nz("sec", 0.5f));
                        o.node.Destroy(); break;
                    }
            }
        }
    }

    IEnumerator ParRun(List<object> acts, ActCtx cx, bool[] flag) { yield return RunActs(acts, cx); flag[0] = true; }

    // ── phản hồi mặc định ──
    public IEnumerator StdRight(ActCtx cx)
    {
        var T = J.Obj(Flow, "text") ?? new Dictionary<string, object>();
        SfxRight();
        int t = (int)J.Num(Q, "target", -1);
        foreach (var e in Elems)
        {
            if (e.key != t) continue;
            if (e.kind == "card" && e.node.img != null) e.node.Color = StoryUI.Hex("#C8F7C5");
            Run(StoryUI.Bounce((e.fig ?? e.node).rt, 0.3f, 0.6f)); Burst(new Vector2(e.node.X, e.node.Y), 10, false);
        }
        string say = J.Str(T, "say");
        if (say != "") Talk(FsExpr.Template(say, J.Obj(Q, "vars"), Scope(cx.ToScope())), P(0.5f, 0.14f), StoryUI.Hex("#1B5E20"), 1.6f);
        yield break;
    }

    public IEnumerator StdWrong(ActCtx cx)
    {
        var T = J.Obj(Flow, "text") ?? new Dictionary<string, object>();
        Sfx("tap");
        if (cx.tapped != null) Run(StoryUI.Shake(cx.tapped.node.rt, 0.35f, 10f));
        string wrong = J.Str(T, "wrong");
        if (wrong != "")
        {
            var qv = new Dictionary<string, object>(); var src = J.Obj(Q, "vars"); if (src != null) foreach (var kv in src) qv[kv.Key] = kv.Value;
            string nm = cx.tapped != null ? cx.tapped.name : ""; qv["name"] = nm; qv["NAME"] = nm;
            Talk(FsExpr.Template(wrong, qv, Scope(cx.ToScope())), P(0.5f, 0.14f), StoryUI.Hex("#BF360C"), 1.3f);
        }
        yield break;
    }
}
