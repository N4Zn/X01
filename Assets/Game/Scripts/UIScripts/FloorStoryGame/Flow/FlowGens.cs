using System;
using System.Collections.Generic;

/// <summary>Phần FlowWorld mà các nguồn (gen) cần — tách ra để kiểm thử logic bằng .NET thuần (không cần Unity).</summary>
public interface IFlowCore : IFsEnv
{
    bool Sync { get; }
    int TaskNo { get; }
    int Ok { get; }
    string LastTarget { get; set; }
    string LastSubject { get; set; }
    string LastAns { get; set; }
    FsRng NextFsRng();
    void ShuffleList<T>(List<T> a, FsRng r = null);
    Dictionary<string, object> Scope(Dictionary<string, object> extra = null);
    string NameOf(object item);
}

/// <summary>
/// Lớp 1 — NGUỒN: bốc vật từ kho sinh ra câu hỏi Q (dạng dữ liệu). Bản C# của GEN trong fs_flow.js — PHẢI khớp từng bước,
/// kể cả thứ tự gọi random (FsRng cùng thuật toán → cùng seed cho ra cùng câu hỏi ở web và Unity).
/// Q = { items[], target, subject?, seq?, faces?, pairs?, number?, options?, optv?, v{}, vars{}, id, desc, answers[] }.
/// </summary>
public static class FlowGens
{
    static double P(object spec, string key) => FsMod.Num("gen", spec, key);
    static string S(object spec, string key) => FsMod.Str("gen", spec, key);

    public static Dictionary<string, object> Make(IFlowCore w, object spec)
    {
        switch (J.Str(spec, "type"))
        {
            case "set": return Set(w, spec);
            case "match": return Match(w, spec);
            case "calc": return Calc(w, spec);
            case "values": return Values(w, spec);
            case "sequence": return Sequence(w, spec);
            case "pairs": return Pairs(w, spec);
            case "number": return Number(w, spec);
            case "none": return Base(new List<object>(), -1, new Dictionary<string, object>(), "NONE", "", new List<object>());
            default: throw new FormatException("Nguồn không có: " + J.Str(spec, "type"));
        }
    }

    static Dictionary<string, object> Base(List<object> items, int target, Dictionary<string, object> vars, string id, string desc, List<object> answers)
        => new Dictionary<string, object> { { "items", items }, { "target", (double)target }, { "vars", vars }, { "id", id }, { "desc", desc }, { "answers", answers } };

    static List<object> Names(IFlowCore w, List<object> items) { var l = new List<object>(); foreach (var i in items) l.Add(w.NameOf(i)); return l; }

    static bool Ok(IFlowCore w, object item, string expr) => expr == "" || FsExpr.Truthy(FsExpr.Eval(expr, w.Scope(new Dictionary<string, object> { { "item", item } })));
    static object TagOf(object item, string k) { var t = J.Obj(item, "tags"); return t != null && t.TryGetValue(k, out var v) ? v : null; }

    static Dictionary<string, object> Set(IFlowCore w, object spec)
    {
        var r = w.Sync ? w.NextFsRng() : w.Rng; var items = w.Items;
        int n = (int)Math.Min(P(spec, "n"), items.Count);
        int allowed = (int)Math.Min(items.Count, Math.Max(n, P(spec, "startItems") + P(spec, "addItems") * Math.Floor((w.TaskNo - 1) / Math.Max(1.0, P(spec, "every")))));
        string tw = S(spec, "targetWhere"), ow = S(spec, "otherWhere"), dist = S(spec, "distinctBy");
        if (tw == "" && ow == "" && dist == "")
        {
            var idx = new List<int>(); for (int i = 0; i < allowed; i++) idx.Add(i);
            w.ShuffleList(idx, r);
            var picks = new List<object>(); for (int i = 0; i < n; i++) picks.Add(items[idx[i]]);
            int t = r.Int(n); string name = w.NameOf(picks[t]);
            var q = Base(picks, t, new Dictionary<string, object> { { "label", name } }, "SET_" + (J.Str(picks[t], "id") != "" ? J.Str(picks[t], "id") : name), "Chọn " + name, Names(w, picks));
            return q;
        }
        var pool = items.GetRange(0, allowed);
        var tp = pool.FindAll(it => Ok(w, it, tw)); if (tp.Count == 0) tp = new List<object>(pool);
        if (P(spec, "noRepeat") != 0 && !string.IsNullOrEmpty(w.LastTarget) && tp.Count > 1) tp = tp.FindAll(it => J.Str(it, "id") != w.LastTarget);
        var target = tp[r.Int(tp.Count)]; w.LastTarget = J.Str(target, "id");
        var others = pool.FindAll(it => it != target && Ok(w, it, ow)); w.ShuffleList(others, r);
        if (dist != "")
        {
            var used = new HashSet<string> { FsExpr.Str(TagOf(target, dist)) }; var chosen = new List<object>();
            foreach (var it in others) { if (chosen.Count < n - 1 && !used.Contains(FsExpr.Str(TagOf(it, dist)))) { used.Add(FsExpr.Str(TagOf(it, dist))); chosen.Add(it); } }
            foreach (var it in others) { if (chosen.Count < n - 1 && !chosen.Contains(it)) chosen.Add(it); }
            others = chosen;
        }
        else if (others.Count > n - 1) others = others.GetRange(0, Math.Max(0, n - 1));
        var all = new List<object> { target }; all.AddRange(others);
        w.ShuffleList(all, r);
        string nm = w.NameOf(target);
        return Base(all, all.IndexOf(target), new Dictionary<string, object> { { "label", nm } }, "SET_" + (J.Str(target, "id") != "" ? J.Str(target, "id") : nm), "Chọn " + nm, Names(w, all));
    }

    static Dictionary<string, object> Match(IFlowCore w, object spec)
    {
        var r = w.Sync ? w.NextFsRng() : w.Rng; string sw = S(spec, "subjectWhere"), ow = S(spec, "optionsWhere");
        var subjPool = w.Items.FindAll(it => Ok(w, it, sw)); var optPool = w.Items.FindAll(it => Ok(w, it, ow));
        var sp = subjPool.Count > 0 ? subjPool : new List<object>(w.Items);
        if (P(spec, "noRepeat") != 0 && !string.IsNullOrEmpty(w.LastSubject) && sp.Count > 1) sp = sp.FindAll(it => J.Str(it, "id") != w.LastSubject);
        var subject = sp[r.Int(sp.Count)]; w.LastSubject = J.Str(subject, "id");
        var pool = optPool.FindAll(it => it != subject); int n = (int)Math.Min(P(spec, "n"), pool.Count); string tag = S(spec, "answerTag");
        object ans = null;
        if (tag != "") { string want = FsExpr.Str(TagOf(subject, tag)); ans = pool.Find(it => J.Str(it, "id") == want); }
        if (ans == null)
        {
            var c = pool;
            if (P(spec, "noRepeat") != 0 && !string.IsNullOrEmpty(w.LastAns) && c.Count > 1) c = c.FindAll(it => J.Str(it, "id") != w.LastAns);
            ans = c[r.Int(c.Count)];
        }
        w.LastAns = J.Str(ans, "id");
        List<object> options;
        if (P(spec, "shuffle") != 0)
        {
            var rest = pool.FindAll(it => it != ans); w.ShuffleList(rest, r);
            options = new List<object> { ans }; options.AddRange(rest.GetRange(0, Math.Min(rest.Count, Math.Max(0, n - 1))));
            w.ShuffleList(options, r);
        }
        else
        {
            options = pool.GetRange(0, n);
            if (options.IndexOf(ans) < 0) options[options.Count - 1] = ans;
        }
        var q = Base(options, options.IndexOf(ans), new Dictionary<string, object> { { "subject", w.NameOf(subject) }, { "label", w.NameOf(ans) } },
                     "MATCH_" + J.Str(subject, "id") + "_" + J.Str(ans, "id"), "Ghép " + w.NameOf(subject), Names(w, options));
        q["subject"] = subject;
        return q;
    }

    static Dictionary<string, object> Calc(IFlowCore w, object spec)
    {
        var r = w.Sync ? w.NextFsRng() : w.Rng; var qv = new Dictionary<string, object>();
        var defs = FsMod.JsonPar("gen", spec, "defs", new Dictionary<string, object>()) as Dictionary<string, object> ?? new Dictionary<string, object>();
        var opts = FsMod.JsonPar("gen", spec, "options", new List<object>()) as List<object> ?? new List<object>();
        Dictionary<string, object> Sc()
        {
            var q = new Dictionary<string, object> { { "v", qv } }; foreach (var kv in qv) q[kv.Key] = kv.Value;
            return w.Scope(new Dictionary<string, object> { { "q", q } });
        }
        foreach (var kv in new List<KeyValuePair<string, object>>(defs)) qv[kv.Key] = FsExpr.Eval(FsExpr.Str(kv.Value), Sc());
        var list = new List<(object item, double v)>();
        foreach (var o in opts)
        {
            string id = FsExpr.Str(FsExpr.Val(J.Get(o, "item"), Sc()));
            object it = w.Items.Find(i => J.Str(i, "id") == id) ?? (w.Items.Count > 0 ? w.Items[0] : null);
            list.Add((it, FsExpr.ToNum(FsExpr.Eval(J.Str(o, "v"), Sc()))));
        }
        if (P(spec, "shuffle") != 0) w.ShuffleList(list, r);
        int ti = 0; bool min = S(spec, "answer") == "min";
        for (int i = 0; i < list.Count; i++) if (min ? list[i].v < list[ti].v : list[i].v > list[ti].v) ti = i;
        var items = new List<object>(); var optv = new List<object>();
        foreach (var l in list) { items.Add(l.item); optv.Add(l.v); }
        var q2 = Base(items, ti, new Dictionary<string, object> { { "label", w.NameOf(list[ti].item) } }, "CALC_" + ti, "Chọn theo giá trị", Names(w, items));
        q2["optv"] = optv; q2["v"] = qv;
        return q2;
    }

    static Dictionary<string, object> Values(IFlowCore w, object spec)
    {
        var byVal = new Dictionary<double, List<object>>(); var vals = new List<double>();
        foreach (var it in w.Items) { double v = J.Num(it, "value"); if (!byVal.TryGetValue(v, out var l)) { byVal[v] = l = new List<object>(); vals.Add(v); } l.Add(it); }
        vals.Sort();
        int n = (w.Ok >= P(spec, "threeAfter") && vals.Count >= 3) ? 3 : 2;
        bool less = w.Ok >= P(spec, "lessAfter") && w.TaskNo % (int)P(spec, "lessEvery") == 0;
        List<double> chosen;
        if (n == 3)
        {
            if (vals.Count == 3) chosen = new List<double>(vals);
            else { var c = new List<double>(vals); w.ShuffleList(c); chosen = c.GetRange(0, 3); chosen.Sort(); }
        }
        else if (w.Ok < 2) chosen = new List<double> { vals[0], vals[vals.Count - 1] };
        else { var c = new List<double>(vals); w.ShuffleList(c); chosen = c.GetRange(0, 2); }
        var items = new List<object>();
        foreach (var v in chosen) { var l = byVal[v]; items.Add(l[w.Rng.Range(0, l.Count)]); }
        w.ShuffleList(items);
        int target = 0;
        for (int i = 1; i < n; i++) if (less ? J.Num(items[i], "value") < J.Num(items[target], "value") : J.Num(items[i], "value") > J.Num(items[target], "value")) target = i;
        string word = less ? S(spec, "wordLess") : S(spec, "wordMore"), suffix = n == 2 ? S(spec, "suffix2") : S(spec, "suffix3");
        string idt = J.Str(items[target], "id") != "" ? J.Str(items[target], "id") : target.ToString();
        return Base(items, target, new Dictionary<string, object> { { "word", word }, { "suffix", suffix }, { "label", w.NameOf(items[target]) } },
                    "VAL_" + idt + (less ? "_min" : "_max") + n, "Con nào " + word + " " + suffix, Names(w, items));
    }

    static Dictionary<string, object> Sequence(IFlowCore w, object spec)
    {
        string where = S(spec, "where");
        var src = where == "" ? w.Items : w.Items.FindAll(it => Ok(w, it, where));
        int kinds = (int)Math.Min(P(spec, "kinds"), src.Count);
        double cnt = S(spec, "lenBy") == "task" ? (w.TaskNo - 1) : w.Ok;
        int len = (int)Math.Min(P(spec, "maxLen"), P(spec, "startLen") + Math.Floor(cnt / Math.Max(1.0, P(spec, "growEvery"))));
        var all = new List<int>(); for (int i = 0; i < src.Count; i++) all.Add(i);
        w.ShuffleList(all);
        var items = new List<object>(); for (int i = 0; i < kinds; i++) items.Add(src[all[i]]);
        var seq = new List<int>();
        if (P(spec, "distinct") != 0)
        {
            var idx = new List<int>(); for (int i = 0; i < items.Count; i++) idx.Add(i);
            w.ShuffleList(idx); seq = idx.GetRange(0, Math.Min(len, kinds));
        }
        else
        {
            for (int i = 0; i < len; i++)
            {
                int v, g = 0;
                do { v = w.Rng.Range(0, kinds); g++; }
                while (g < 30 && ((len <= 3 && seq.IndexOf(v) >= 0) || (i >= 2 && seq[i - 1] == v && seq[i - 2] == v)));
                seq.Add(v);
            }
        }
        var names = new List<string>(); foreach (var i in seq) names.Add(w.NameOf(items[i]).ToLowerInvariant());
        var seqL = new List<object>(); foreach (var i in seq) seqL.Add((double)i);
        var q = Base(items, seq[0], new Dictionary<string, object> { { "len", (double)seq.Count }, { "names", string.Join(", rồi ", names) } },
                     "SEQ_" + seq.Count + "_" + string.Join("-", seq), "Chuỗi " + seq.Count + " hình", Names(w, items));
        q["seq"] = seqL;
        return q;
    }

    static Dictionary<string, object> Pairs(IFlowCore w, object spec)
    {
        int pairs = (int)Math.Max(2, Math.Min(Math.Min(P(spec, "maxPairs"), P(spec, "startPairs") + (w.TaskNo - 1)), w.Items.Count));
        var all = new List<int>(); for (int i = 0; i < w.Items.Count; i++) all.Add(i);
        w.ShuffleList(all);
        var faces = new List<int>(); for (int i = 0; i < pairs; i++) { faces.Add(i); faces.Add(i); }
        w.ShuffleList(faces);
        var items = new List<object>(); for (int i = 0; i < pairs; i++) items.Add(w.Items[all[i]]);
        var facesL = new List<object>(); foreach (var f in faces) facesL.Add((double)f);
        var q = Base(items, 0, new Dictionary<string, object> { { "pairs", (double)pairs } }, "PAIRS_" + pairs, "Lật thẻ giống nhau: " + pairs + " cặp", new List<object> { string.Join(",", faces) });
        q.Remove("target"); q["target"] = 0.0; q["faces"] = facesL; q["pairs"] = (double)pairs;
        return q;
    }

    static Dictionary<string, object> Number(IFlowCore w, object spec)
    {
        double lv = Math.Floor(w.Ok / Math.Max(1.0, P(spec, "growEvery"))); double maxC = Math.Min(12, P(spec, "maxCount"));
        double lo = Math.Min(P(spec, "minStart") + lv, 4), hi = Math.Min(P(spec, "startMax") + 2 * lv, maxC);
        int count = w.Rng.Range((int)Math.Min(lo, hi), (int)hi + 1);
        var opts = new List<int> { count }; int g = 0;
        while (opts.Count < P(spec, "options") && g++ < 80) { int v = count + w.Rng.Range(-2, 3); if (v >= 1 && v <= 12 && !opts.Contains(v)) opts.Add(v); }
        w.ShuffleList(opts);
        string oid = S(spec, "objectId");
        object obj = oid == "" ? null : w.Items.Find(i => J.Str(i, "id") == oid);
        var items = new List<object>(); if (obj != null) items.Add(obj);
        var optL = new List<object>(); var ans = new List<object>(); foreach (var o in opts) { optL.Add((double)o); ans.Add(o.ToString()); }
        var q = Base(items, opts.IndexOf(count), new Dictionary<string, object> { { "count", (double)count } }, "NUM_" + count, "Đếm " + count, ans);
        q["options"] = optL; q["number"] = (double)count;
        return q;
    }
}
