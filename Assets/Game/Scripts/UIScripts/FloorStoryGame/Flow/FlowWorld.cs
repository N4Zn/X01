using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thế giới "flow" — game FloorStory đọc từ dữ liệu fsData (zip xuất từ web builder). Bản C# của FlowWorld + fs_act.js trong
/// WebTools/GenericGameBuilder/fs/ (PHẢI khớp). 7 lớp: nguồn (FlowGens) → hiển thị (FlowViews) → chấm (FlowJudges) + phản hồi (actions)
/// + vars/scene (file này). Schema: docs/floor-story-mechanics.md. Ảnh: Resources/StoryData/&lt;gameId&gt;/&lt;tên&gt;.
/// </summary>
public sealed partial class FlowWorld : StoryWorld, IFlowCore
{
    readonly string _gid;
    readonly Dictionary<string, object> _data;
    readonly List<object> _items;
    FsRng _rng;
    public Dictionary<string, object> Vars;
    readonly Dictionary<string, List<string>> _bags = new Dictionary<string, List<string>>();
    public readonly List<FsObj> Objs = new List<FsObj>(), TaskObjs = new List<FsObj>();
    public List<FsElem> Elems = new List<FsElem>();
    public Dictionary<string, object> Q;                 // câu hỏi hiện tại (dạng dữ liệu)
    public Dictionary<string, object> Flow;              // flow đang dùng
    readonly List<FsNode> _tracked = new List<FsNode>();
    readonly List<Action> _readyCbs = new List<Action>();
    Text _prompt;
    public int TaskNo { get; set; }
    public int Ok { get; set; }
    public int GenId;
    public bool Active, Ready;
    int _sharedNo;
    public string LastTarget { get; set; }
    public string LastSubject { get; set; }
    public string LastAns { get; set; }

    public FlowWorld(string gameId, Dictionary<string, object> data)
    {
        _gid = gameId; _data = data;
        StoryUI.TextStyles = LoadTextStyles(J.Obj(data, "fonts"));
        _items = J.List(data, "items") ?? new List<object>();
        _rng = new FsRng(Environment.TickCount * 31L + gameId.GetHashCode());
        Vars = new Dictionary<string, object>();
        var v = J.Obj(data, "vars");
        if (v != null) foreach (var kv in v) Vars[kv.Key] = kv.Value;
    }

    // ── IFsEnv + truy cập cho view/judge ──
    public FsRng Rng => _rng;
    public List<object> Items => _items;
    public bool Sync => J.Num(_data, "sync") != 0;
    public override bool Synchronized => Sync;
    public StoryContext Context => ctx;
    public RectTransform Root => root;
    public float WorldW => W;
    public float WorldH => H;
    public float Unit => U;
    public Vector2 PP(float fx, float fy) => P(fx, fy);
    public Coroutine Go(IEnumerator e) => Run(e);
    /// <summary>"right"/"wrong" = tiếng đúng/sai: file riêng của game (flow.sounds → sounds) nếu có, không thì tiếng mặc định của app
    /// (MusicManager.PlayCorrectSfx/PlayWrongSfx — giống mọi game khác). Khoá khác = hiệu ứng Resources/StorySfx.</summary>
    public void Sfx(string k)
    {
        if (string.IsNullOrEmpty(k)) return;
        if (k == "right" || k == "wrong")
        {
            if (SoundOf(k) != "") PlayFile(SoundOf(k), false);
            else if (k == "right") ctx.SfxRight(); else ctx.SfxWrong();
            return;
        }
        ctx.Sfx(k);
    }
    public void SfxRight() => ctx.SfxRight();
    public void Point() => AddPoint();
    public void Burst(Vector2 c, int n, bool sound = true) => Confetti(c, n, sound);
    public void Talk(string text, Vector2 pos, Color color, float sec) { if (!string.IsNullOrEmpty(text)) Say(text, pos, color, sec); }
    public FsRng NextFsRng() { _sharedNo++; return new FsRng(ctx.sharedSeed * 7919L + _sharedNo * 104729L); }
    public bool Stale(Dictionary<string, object> q) => !ReferenceEquals(q, Q);
    public void Track(FsNode n) { if (n != null) _tracked.Add(n); }
    public void OnReadyOnce(Action cb) { if (Ready) cb(); else _readyCbs.Add(cb); }
    public IEnumerator Later(float sec, Action fn) { yield return new WaitForSeconds(sec); fn(); }
    public void ShuffleList<T>(List<T> a, FsRng r = null) { r = r ?? _rng; for (int i = a.Count - 1; i > 0; i--) { int j = r.Int(i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; } }

    public static Color Hex(object h) => StoryUI.Hex(h is string s && s.Length > 0 ? s : "#FF00FF");

    public string NameOf(object item)
    {
        string l = J.Str(item, "label");
        if (l != "") return l;
        string icon = J.Str(item, "icon");
        if (icon != "") return StoryIcons.VietnameseName(icon);
        return J.Str(item, "id");
    }

    // ── scope biểu thức ──
    public Dictionary<string, object> Scope(Dictionary<string, object> extra = null)
    {
        var q = new Dictionary<string, object>();
        if (Q != null)
        {
            foreach (var kv in Q) q[kv.Key] = kv.Value;
            var qv = J.Obj(Q, "v"); if (qv != null) foreach (var kv in qv) q[kv.Key] = kv.Value;
            var its = J.List(Q, "items"); int t = (int)J.Num(Q, "target", -1);
            q["targetItem"] = (its != null && t >= 0 && t < its.Count) ? its[t] : "";
        }
        else q["targetItem"] = "";
        var s = new Dictionary<string, object> { { "wr", (double)(W / U) }, { "hr", (double)(H / U) }, { "vars", Vars }, { "q", q }, { "w", this } };
        if (extra != null) foreach (var kv in extra) s[kv.Key] = kv.Value;
        return s;
    }
    public object Ev(object v, Dictionary<string, object> extra = null) => FsExpr.Val(v, Scope(extra));
    public double Nv(object v, double def, Dictionary<string, object> extra = null)
    {
        if (v == null || (v is string s && s == "")) return def;
        return FsExpr.ToNum(Ev(v, extra));
    }
    public bool Cond(string expr, Dictionary<string, object> extra = null) => FsExpr.Truthy(FsExpr.Eval(expr, Scope(extra)));

    // ── định dạng chữ (fsData.fonts / look.style) ──
    public static StoryUI.TextStyle StyleFrom(Dictionary<string, object> d)
    {
        if (d == null) return null;
        var st = new StoryUI.TextStyle();
        double sc = J.Num(d, "scale", 1); if (sc > 0) st.scale = (float)sc;
        string c = J.Str(d, "color"); if (c != "") { st.hasColor = true; st.color = StoryUI.Hex(c); }
        if (d.ContainsKey("bold")) { st.hasBold = true; st.bold = J.Num(d, "bold", 1) != 0; }
        st.italic = J.Num(d, "italic", 0) != 0;
        st.outline = J.Str(d, "outline");
        double ow = J.Num(d, "outlineW", 2); if (ow > 0) st.outlineW = (float)ow;
        st.shadow = J.Num(d, "shadow", 0) != 0;
        return st;
    }
    static Dictionary<string, StoryUI.TextStyle> LoadTextStyles(Dictionary<string, object> fonts)
    {
        if (fonts == null) return null;
        var r = new Dictionary<string, StoryUI.TextStyle>();
        foreach (var kv in fonts) { var st = StyleFrom(kv.Value as Dictionary<string, object>); if (st != null) r[kv.Key] = st; }
        return r;
    }

    // ── âm thanh: file người soạn thêm (item.sound, sounds.right / wrong / music) trong Resources/StoryData/<id>/ ──
    AudioSource _voiceSrc, _fxSrc, _musicSrc;
    AudioClip LoadClip(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        return Resources.Load<AudioClip>($"StoryData/{_gid}/{System.IO.Path.GetFileNameWithoutExtension(file)}");
    }
    AudioSource Src(ref AudioSource s) { if (s == null) s = root.gameObject.AddComponent<AudioSource>(); return s; }
    public void PlayFile(string file, bool voice)
    {
        var clip = LoadClip(file); if (clip == null) return;
        if (voice) { var v = Src(ref _voiceSrc); v.Stop(); v.clip = clip; v.Play(); }
        else Src(ref _fxSrc).PlayOneShot(clip);
    }
    public void PlayItem(object item) { var d = item as Dictionary<string, object>; if (d != null) PlayFile(J.Str(d, "sound"), true); }
    /// <summary>Tiếng của vật là đáp án (nếu câu hỏi có đáp án và vật đó có file tiếng).</summary>
    void PlayTargetItem()
    {
        if (Sync) return;   // chế độ chung: controller phát + lặp lại 1 lần cho cả 2 đội (PlayPrompt) — tránh 2 đội phát chồng nhau
        PlayFile(PromptSoundFile(), true);
    }
    /// <summary>File âm đọc câu hỏi: round.sound &gt; flow.sounds.prompt &gt; tiếng của vật là đáp án.</summary>
    string PromptSoundFile()
    {
        if (Q == null) return "";
        if (J.Str(Q, "rsound") != "") return J.Str(Q, "rsound");   // tiếng riêng của câu (round.sound)
        var fsnd = Flow != null ? J.Obj(Flow, "sounds") : null; string ps = fsnd != null ? J.Str(fsnd, "prompt") : "";
        if (ps != "") return ps;   // tiếng câu hỏi riêng của dạng câu hỏi
        int t = (int)J.Num(Q, "target", -1); var its = J.Arr(J.Get(Q, "items"));
        return t >= 0 && t < its.Count ? J.Str(its[t], "sound") : "";
    }
    public override float PlayPrompt()
    {
        string f = PromptSoundFile(); var clip = LoadClip(f); if (clip == null) return 0f;
        PlayFile(f, true); return clip.length;
    }
    string GameSound(string key) { var s = J.Obj(_data, "sounds"); return s == null ? "" : J.Str(s, key); }
    /// <summary>Tiếng của DẠNG CÂU HỎI hiện tại (flow.sounds[key]) nếu có, không thì tiếng chung cả game (sounds[key]).</summary>
    string SoundOf(string key) { var s = Flow != null ? J.Obj(Flow, "sounds") : null; if (s != null && J.Str(s, key) != "") return J.Str(s, key); return GameSound(key); }
    void StartMusic()
    {
        var clip = LoadClip(GameSound("music")); if (clip == null) return;
        var m = Src(ref _musicSrc); m.clip = clip; m.loop = true; m.volume = 0.5f; m.Play();
    }

    // ── dựng thế giới ──
    protected override void Build()
    {
        var th = J.Obj(_data, "theme");
        string bg = th != null && th.ContainsKey("bg") ? J.Str(th, "bg") : "#E3F2FD";
        string fl = th != null && th.ContainsKey("floor") ? J.Str(th, "floor") : "#BBDEFB";
        if (bg != "") StoryUI.Fill(root, "Bg", StoryUI.Hex(bg));
        if (fl != "") StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex(fl), P(0.5f, 0.06f), new Vector2(W, H * 0.14f)).preserveAspect = false;
        var scene = J.List(_data, "scene") ?? new List<object>();
        MakeObjs(scene.FindAll(o => J.Str(o, "z") != "front"), FsNode.Of(root), Objs, null);
        MakeObjs(scene.FindAll(o => J.Str(o, "z") == "front"), FsNode.Of(root), Objs, null);
        _prompt = StoryUI.Label(root, "", 40, Color.white, P(0.5f, 0.92f), new Vector2(W * 0.94f, H * 0.13f), TextAnchor.MiddleCenter, true, "prompt");
        if (team == Team.Left) StartMusic();
        ctx.Run(TickLoop());
    }

    IEnumerator TickLoop()
    {
        while (true) { UpdateObjs(Objs, Time.deltaTime, Time.time); UpdateObjs(TaskObjs, Time.deltaTime, Time.time); yield return null; }
    }

    static List<object> FlowsOf(Dictionary<string, object> d)
    {
        var f = J.List(d, "flows");
        if (f != null && f.Count > 0) return f;
        var one = J.Obj(d, "flow");
        return one != null ? new List<object> { one } : new List<object>();
    }

    readonly Dictionary<int, int> _roundCount = new Dictionary<int, int>(); readonly Dictionary<int, List<int>> _roundBag = new Dictionary<int, List<int>>();
    /// <summary>Chỉ số round viết tay của lượt này (flow.roundOrder: seq lặp vòng | shuffle xáo mỗi vòng). Khớp _nextRound (fs_flow.js).</summary>
    int NextRound(Dictionary<string, object> f, int fi, int n)
    {
        _roundCount.TryGetValue(fi, out int c); c++; _roundCount[fi] = c;
        if (J.Str(f, "roundOrder") == "shuffle")
        {
            if (!_roundBag.TryGetValue(fi, out var bag) || bag.Count == 0) { bag = new List<int>(); for (int i = 0; i < n; i++) bag.Add(i); ShuffleList(bag); _roundBag[fi] = bag; }
            int v = bag[0]; bag.RemoveAt(0); return v;
        }
        return (c - 1) % n;
    }

    void ClearTask()
    {
        GenId++; Active = false;
        foreach (var n in _tracked) n?.Destroy();
        _tracked.Clear(); _readyCbs.Clear();
        foreach (var o in TaskObjs) o.node?.Destroy();
        TaskObjs.Clear(); Elems = new List<FsElem>();
    }

    /// <summary>Xong lượt: dọn view/vật của câu (nền cảnh `scene[]` ở lại). Hoãn 1 khung hình để judge/hành động đang chạy kịp thoát; GenId đổi (đã sang lượt mới) thì bỏ qua.</summary>
    public override void EndTask()
    {
        int gen = GenId;
        Run(EndTaskNextFrame(gen));
    }
    IEnumerator EndTaskNextFrame(int gen)
    {
        yield return null;
        if (gen != GenId) yield break;
        ClearTask();
        if (_prompt != null) _prompt.text = "";
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearTask(); Active = true; TaskNo++; Ready = false;
        // Chơi chung: mọi random của lượt dùng cùng seed ở cả 2 đội (khớp JS) → cùng câu hỏi
        if (Sync) _rng = new FsRng(ctx.sharedSeed * 104729L + TaskNo * 7919L + 13);
        var flows = FlowsOf(_data);
        int fi = J.Str(_data, "flowPick") == "random" ? _rng.Int(flows.Count) : (TaskNo - 1) % flows.Count;
        Flow = (Dictionary<string, object>)flows[fi];
        int gen = GenId;
        var T = J.Obj(Flow, "text") ?? new Dictionary<string, object>();
        Vars["_task"] = (double)TaskNo; Vars["_ok"] = (double)Ok;
        var on = J.Obj(Flow, "on");
        if (on != null && J.List(on, "start") != null) ExecNow(J.List(on, "start"));
        Q = null; string rprompt = "";
        if (J.Num(Flow, "manual") != 0)   // câu hỏi viết tay (flow.rounds[]); round không hợp lệ → nguồn tự động
        {
            var rounds = J.List(Flow, "rounds");
            if (rounds != null && rounds.Count > 0)
            {
                int ri = NextRound(Flow, flows.IndexOf(Flow), rounds.Count);
                var rd = rounds[ri] as Dictionary<string, object>;
                Q = FlowGens.FromRound(this, J.Obj(Flow, "gen"), rd);
                var rv = Q != null ? J.Obj(rd, "vars") : null;   // biến đặt riêng cho câu này (khớp JS rd.vars)
                if (rv != null) foreach (var kv in rv) Vars[kv.Key] = kv.Value;
                if (Q != null) { rprompt = J.Str(rd, "prompt"); if (J.Str(rd, "sound") != "") Q["rsound"] = J.Str(rd, "sound"); }   // lời hỏi riêng của round (khớp JS)
            }
        }
        if (Q == null) Q = FlowGens.Make(this, J.Obj(Flow, "gen"));
        var answers = new List<string>(); foreach (var a in J.Arr(J.Get(Q, "answers"))) answers.Add(FsExpr.Str(a));
        Describe(info, J.Str(Q, "id"), J.Str(J.Obj(Flow, "gen"), "type"), J.Str(Q, "desc"), answers.ToArray(), Math.Max(0, J.Int(Q, "target", 0)));
        var objs = J.List(Flow, "objects") ?? new List<object>();
        MakeObjs(objs.FindAll(o => J.Str(o, "z") != "front"), FsNode.Of(root), TaskObjs, null);
        string first = rprompt != "" ? rprompt : (TaskNo == 1 && J.Str(T, "first") != "" ? J.Str(T, "first") : J.Str(T, "prompt"));
        _prompt.text = FsExpr.Template(first, J.Obj(Q, "vars"), Scope()); PlayTargetItem();
        var specs = J.List(Flow, "views") ?? new List<object>();
        var views = new List<FsView>();
        foreach (var s in specs) views.Add(FlowViews.Make(this, (Dictionary<string, object>)s, Q));
        MakeObjs(objs.FindAll(o => J.Str(o, "z") == "front"), FsNode.Of(root), TaskObjs, null);
        foreach (var o in Objs) if (J.Str(o.spec, "z") == "front" && !o.Dead) o.node.SetAsLast();
        _prompt.transform.SetAsLastSibling();
        Elems = new List<FsElem>();
        foreach (var v in views) { foreach (var e in v.Elems) { e.view = v; Elems.Add(e); } }
        if (on != null && J.List(on, "setup") != null) ExecNow(J.List(on, "setup"));
        var q0 = Q;
        Action<bool, int[]> finish = (ok, ans) => { if (ok) Ok++; done(ok, ans); };
        FlowJudges.Attach(this, J.Obj(Flow, "judge"), Q, views, finish);
        Run(Present(Flow, views, q0, gen, T));
    }

    IEnumerator Present(Dictionary<string, object> f, List<FsView> V, Dictionary<string, object> q, int gen, Dictionary<string, object> T)
    {
        var specs = J.List(f, "views") ?? new List<object>();
        var revs = new List<(FsView v, Dictionary<string, object> r)>();
        for (int i = 0; i < V.Count; i++)
        {
            var r = J.Obj(specs[i], "reveal");
            if (r != null && J.Str(r, "mode", "none") != "none") revs.Add((V[i], r));
        }
        var defer = new List<FsView>();
        if (revs.Count > 0) for (int i = 0; i < V.Count; i++) if (J.Str(specs[i], "appear") == "afterReveal") defer.Add(V[i]);
        foreach (var v in defer) foreach (var e in v.Elems) e.node.Active = false;
        foreach (var o in revs) o.v.HideAll();
        foreach (var v in V)
        {
            bool inRev = revs.Exists(o => o.v == v);
            if (!inRev && !defer.Contains(v)) Run(v.Intro());
        }
        if (revs.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            foreach (var o in revs)
            {
                if (gen != GenId) yield break;
                var spec = new Dictionary<string, object> { { "p", o.r } };
                double showSec = FsMod.Num("reveal", spec, "showSec"), hold = FsMod.Num("reveal", spec, "hold"), perItem = FsMod.Num("reveal", spec, "holdPerItem");
                string mode = J.Str(o.r, "mode"), then = FsMod.Str("reveal", spec, "then");
                var parts = o.v.Parts;
                if (mode == "all") { foreach (var p in parts) p.Show(); yield return new WaitForSeconds((float)showSec); }
                else foreach (var p in parts) { if (gen != GenId) yield break; p.Show(); Sfx("pop"); yield return new WaitForSeconds((float)showSec); }
                yield return new WaitForSeconds((float)(hold + perItem * parts.Count));
                if (gen != GenId) yield break;
                if (then == "hide") foreach (var p in parts) p.Hide(); else if (then != "keep") foreach (var p in parts) p.Cover();
                Sfx("plop");
            }
            foreach (var v in defer) foreach (var e in v.Elems) { e.node.Active = true; Run(StoryUI.PopIn(e.node.rt, 0.25f)); }
            if (J.Str(T, "ready") != "") { _prompt.text = FsExpr.Template(J.Str(T, "ready"), J.Obj(q, "vars"), Scope()); PlayTargetItem(); }
        }
        while (V.Exists(v => !v.Ready)) { if (gen != GenId) yield break; yield return null; }
        Ready = true;
        var cbs = new List<Action>(_readyCbs); _readyCbs.Clear();
        foreach (var cb in cbs) cb();
    }

    /// <summary>Chạy hành động của sự kiện `ev` (flow.on[ev]); không có thì chạy mặc định `std`.</summary>
    public IEnumerator Fire(string ev, ActCtx cx, Func<ActCtx, IEnumerator> std = null)
    {
        var on = J.Obj(Flow, "on");
        var list = on != null ? J.List(on, ev) : null;
        if (ev == "right") ctx.showIcon?.Invoke(team, true);   // icon ✔ mặc định của Kit (fallback chung)
        if (list != null && list.Count > 0)
        {
            if (ev == "right" && !list.Exists(a => J.Str(a, "do") == "sfx" || J.Str(a, "do") == "std")) Sfx("right");   // hành động riêng không có tiếng → vẫn có tiếng đúng mặc định
            yield return RunActs(list, cx);
        }
        else if (std != null) yield return std(cx);
    }

    void ExecNow(List<object> list)
    {
        var g = RunActs(list, new ActCtx());
        for (int i = 0; i < 1000 && g.MoveNext(); i++) { }
    }
}

/// <summary>Ngữ cảnh khi chạy hành động: phần tử vừa chạm, vị trí chạm, bước (judge order), cờ last.</summary>
public sealed class ActCtx
{
    public FsElem tapped;
    public Vector2? tap;
    public int step;
    public bool last;
    public int mistakes;
    public Dictionary<string, object> ToScope()
    {
        var d = new Dictionary<string, object> { { "step", (double)step }, { "last", last }, { "mistakes", (double)mistakes } };
        if (tapped != null) d["tapped"] = tapped.ToScope();
        if (tap.HasValue) d["tap"] = new Dictionary<string, object> { { "x", (double)tap.Value.x }, { "y", (double)tap.Value.y } };
        return d;
    }
}

/// <summary>1 phần (ô/thẻ/khối) của view có thể hiện/úp/ẩn theo dòng thời gian reveal.</summary>
public sealed class FsPart
{
    public Action Show = () => { }, Cover = () => { }, Hide = () => { };
}

/// <summary>Cách hiển thị 1 phần câu hỏi — tương ứng V trong fs_flow.js.</summary>
public abstract class FsView
{
    public List<FsElem> Elems = new List<FsElem>();
    public List<FsPart> Parts = new List<FsPart>();
    public bool Ready = true;
    public virtual void HideAll() { }
    public virtual void SetActive(bool b) { }
    public virtual IEnumerator Intro() { yield break; }
    public virtual void HintExtra() { }
}
