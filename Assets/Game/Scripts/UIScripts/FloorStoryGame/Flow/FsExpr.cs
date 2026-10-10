using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Random mulberry32 — CÙNG thuật toán với `Rng` trong fs_core.js (cùng seed → cùng dãy số web/Unity).</summary>
public sealed class FsRng
{
    uint _s;
    public FsRng(long seed) { _s = (uint)(seed & 0xFFFFFFFFL); if (_s == 0) _s = 1; }
    public double Next()
    {
        unchecked
        {
            _s += 0x6D2B79F5u;
            uint t = _s;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
    public int Int(int n) => (int)Math.Floor(Next() * n);
    public int Range(int a, int b) => a + Int(b - a);
}

/// <summary>Môi trường cho hàm biểu thức cần random + kho vật.</summary>
public interface IFsEnv
{
    FsRng Rng { get; }
    List<object> Items { get; }
}

/// <summary>
/// Ngôn ngữ biểu thức nhỏ — bản C# của fs_expr.js (PHẢI cho cùng kết quả). Kiểu: double, string, bool, List&lt;object&gt;, Dictionary.
/// Scope = Dictionary&lt;string,object&gt; (khoá: vars, q, item, i, n, tapped, opt, size, last, wr, hr ...).
/// </summary>
public static class FsExpr
{
    sealed class Tok { public char K; public string S; public double N; }   // K: n num, s str, v var, i id, o op
    sealed class Node { public char T; public string S; public double N; public Node A, B, C; public List<Node> Args; }

    static readonly Dictionary<string, Node> Cache = new Dictionary<string, Node>();

    // ── tiện ích kiểu ──
    public static string NumToString(double d)
    {
        if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
        return d.ToString("R", CultureInfo.InvariantCulture);
    }
    public static string Str(object v)
    {
        switch (v)
        {
            case null: return "";
            case string s: return s;
            case bool b: return b ? "true" : "false";
            case double d: return NumToString(d);
            case int n: return n.ToString(CultureInfo.InvariantCulture);
            case List<object> l: { var p = new List<string>(); foreach (var x in l) p.Add(Str(x)); return string.Join(",", p); }
            default: return v.ToString();
        }
    }
    static bool IsNumLike(object v)
    {
        if (v is double || v is bool || v is int) return true;
        return v is string s && s.Length > 0 && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
    public static double ToNum(object v)
    {
        switch (v)
        {
            case double d: return d;
            case int n: return n;
            case bool b: return b ? 1 : 0;
            case string s: return s.Length > 0 && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
            default: return 0;
        }
    }
    public static bool Truthy(object v)
    {
        switch (v)
        {
            case null: return false;
            case List<object> l: return l.Count > 0;
            case string s: return s != "" && s != "0" && s != "false";
            case bool b: return b;
            case double d: return d != 0 && !double.IsNaN(d);
            case int n: return n != 0;
            default: return true;
        }
    }
    public static bool Eq(object a, object b) => (IsNumLike(a) && IsNumLike(b)) ? ToNum(a) == ToNum(b) : Str(a) == Str(b);

    // ── tokenizer ──
    static List<Tok> Tokenize(string s)
    {
        var t = new List<Tok>(); int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == ' ' || c == '\t' || c == '\n' || c == '\r') { i++; continue; }
            if ((c >= '0' && c <= '9') || (c == '.' && i + 1 < s.Length && s[i + 1] >= '0' && s[i + 1] <= '9'))
            {
                int j = i; while (j < s.Length && ((s[j] >= '0' && s[j] <= '9') || s[j] == '.')) j++;
                t.Add(new Tok { K = 'n', N = double.Parse(s.Substring(i, j - i), CultureInfo.InvariantCulture) }); i = j; continue;
            }
            if (c == '"' || c == '\'')
            {
                int j = i + 1; var sb = new StringBuilder(); while (j < s.Length && s[j] != c) { sb.Append(s[j]); j++; }
                t.Add(new Tok { K = 's', S = sb.ToString() }); i = j + 1; continue;
            }
            if (c == '$' || char.IsLetter(c) && c < 128 || c == '_')
            {
                int j = i + 1; while (j < s.Length && ((s[j] < 128 && char.IsLetterOrDigit(s[j])) || s[j] == '_' || s[j] == '.')) j++;
                t.Add(new Tok { K = c == '$' ? 'v' : 'i', S = s.Substring(c == '$' ? i + 1 : i, j - (c == '$' ? i + 1 : i)) }); i = j; continue;
            }
            if (i + 1 < s.Length)
            {
                string two = s.Substring(i, 2);
                if (two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "&&" || two == "||") { t.Add(new Tok { K = 'o', S = two }); i += 2; continue; }
            }
            if ("+-*/%<>!?:(),".IndexOf(c) >= 0) { t.Add(new Tok { K = 'o', S = c.ToString() }); i++; continue; }
            throw new FormatException("Biểu thức: ký tự lạ '" + c + "' trong: " + s);
        }
        return t;
    }

    // ── parser ──
    sealed class Parser
    {
        readonly List<Tok> _t; readonly string _src; int _p;
        public Parser(List<Tok> t, string src) { _t = t; _src = src; }
        bool IsOp(string v) => _p < _t.Count && _t[_p].K == 'o' && _t[_p].S == v;
        void Expect(string v) { if (!IsOp(v)) throw new FormatException("Biểu thức: thiếu '" + v + "' trong: " + _src); _p++; }
        public Node Run() { var e = Ternary(); if (_p < _t.Count) throw new FormatException("Biểu thức: thừa ký tự trong: " + _src); return e; }
        Node Ternary()
        {
            var c = Bin(0);
            if (IsOp("?")) { _p++; var a = Ternary(); Expect(":"); var b = Ternary(); return new Node { T = '?', A = c, B = a, C = b }; }
            return c;
        }
        static readonly string[][] Levels =
        {
            new[] { "||" }, new[] { "&&" }, new[] { "==", "!=" }, new[] { "<", "<=", ">", ">=" }, new[] { "+", "-" }, new[] { "*", "/", "%" },
        };
        Node Bin(int lv)
        {
            if (lv >= Levels.Length) return Unary();
            var l = Bin(lv + 1);
            while (_p < _t.Count && _t[_p].K == 'o' && Array.IndexOf(Levels[lv], _t[_p].S) >= 0)
            {
                string o = _t[_p++].S;
                l = new Node { T = 'b', S = o, A = l, B = Bin(lv + 1) };
            }
            return l;
        }
        Node Unary()
        {
            if (IsOp("!")) { _p++; return new Node { T = '!', A = Unary() }; }
            if (IsOp("-")) { _p++; return new Node { T = 'g', A = Unary() }; }
            return Prim();
        }
        Node Prim()
        {
            if (_p >= _t.Count) throw new FormatException("Biểu thức: thiếu vế phải: " + _src);
            var t = _t[_p++];
            if (t.K == 'n') return new Node { T = 'n', N = t.N };
            if (t.K == 's') return new Node { T = 's', S = t.S };
            if (t.K == 'v') return new Node { T = 'v', S = t.S };
            if (t.K == 'i')
            {
                if (IsOp("("))
                {
                    _p++; var args = new List<Node>();
                    if (!IsOp(")")) { while (true) { args.Add(Ternary()); if (IsOp(",")) _p++; else break; } }
                    Expect(")");
                    return new Node { T = 'f', S = t.S, Args = args };
                }
                return new Node { T = 'i', S = t.S };
            }
            if (t.K == 'o' && t.S == "(") { var e = Ternary(); Expect(")"); return e; }
            throw new FormatException("Biểu thức: lỗi cú pháp gần '" + (t.S ?? "") + "' trong: " + _src);
        }
    }

    // ── đánh giá ──
    static object Path(Dictionary<string, object> scope, string dotted)
    {
        object cur = scope;
        foreach (var k in dotted.Split('.'))
        {
            if (cur is Dictionary<string, object> d) { if (!d.TryGetValue(k, out cur)) return ""; }
            else return "";
            if (cur == null) return "";
        }
        return cur ?? "";
    }

    static Dictionary<string, object> Vars(Dictionary<string, object> scope) => scope.TryGetValue("vars", out var v) ? v as Dictionary<string, object> : null;
    static IFsEnv Env(Dictionary<string, object> scope) => scope.TryGetValue("w", out var v) ? v as IFsEnv : null;

    static object Ev(Node n, Dictionary<string, object> s)
    {
        switch (n.T)
        {
            case 'n': return n.N;
            case 's': return n.S;
            case 'v': { var vs = Vars(s); return vs != null && vs.TryGetValue(n.S, out var v) && v != null ? v : 0.0; }
            case 'i': return Path(s, n.S);
            case 'g': return -ToNum(Ev(n.A, s));
            case '!': return !Truthy(Ev(n.A, s));
            case '?': return Truthy(Ev(n.A, s)) ? Ev(n.B, s) : Ev(n.C, s);
            case 'f': { var args = new List<object>(); foreach (var a in n.Args) args.Add(Ev(a, s)); return Call(n.S, args, s); }
            case 'b':
                {
                    if (n.S == "&&") { var l0 = Ev(n.A, s); return Truthy(l0) ? Ev(n.B, s) : l0; }
                    if (n.S == "||") { var l0 = Ev(n.A, s); return Truthy(l0) ? l0 : Ev(n.B, s); }
                    var l = Ev(n.A, s); var r = Ev(n.B, s);
                    switch (n.S)
                    {
                        case "+": return (l is string || r is string) ? (object)(Str(l) + Str(r)) : ToNum(l) + ToNum(r);
                        case "-": return ToNum(l) - ToNum(r);
                        case "*": return ToNum(l) * ToNum(r);
                        case "/": return ToNum(r) == 0 ? 0.0 : ToNum(l) / ToNum(r);
                        case "%": return ToNum(r) == 0 ? 0.0 : ToNum(l) % ToNum(r);
                        case "==": return Eq(l, r);
                        case "!=": return !Eq(l, r);
                        case "<": return ToNum(l) < ToNum(r);
                        case "<=": return ToNum(l) <= ToNum(r);
                        case ">": return ToNum(l) > ToNum(r);
                        case ">=": return ToNum(l) >= ToNum(r);
                    }
                    break;
                }
        }
        throw new FormatException("Biểu thức: nút lạ " + n.T);
    }

    static Dictionary<string, object> FindItem(Dictionary<string, object> s, string id)
    {
        var env = Env(s); if (env == null) return null;
        foreach (var it in env.Items) if (J.Str(it, "id") == id) return it as Dictionary<string, object>;
        return null;
    }

    static object Call(string f, List<object> a, Dictionary<string, object> s)
    {
        double N(int i) => i < a.Count ? ToNum(a[i]) : 0;
        switch (f)
        {
            case "min": { double m = double.PositiveInfinity; foreach (var x in a) m = Math.Min(m, ToNum(x)); return m; }
            case "max": { double m = double.NegativeInfinity; foreach (var x in a) m = Math.Max(m, ToNum(x)); return m; }
            case "abs": return Math.Abs(N(0));
            case "floor": return Math.Floor(N(0));
            case "ceil": return Math.Ceiling(N(0));
            case "round": return Math.Floor(N(0) + 0.5);   // = Math.round của JS
            case "sqrt": return Math.Sqrt(N(0));
            case "sin": return Math.Sin(N(0));
            case "cos": return Math.Cos(N(0));
            case "rand": return Env(s).Rng.Next();
            case "randi": return N(0) + Env(s).Rng.Int((int)Math.Max(1, N(1) - N(0)));
            case "pick": { int i = (int)Math.Max(0, Math.Min(a.Count - 2, Math.Floor(N(0)))); return a[1 + i]; }
            case "has": { foreach (var p in Str(a[0]).Split(',')) if (p.Trim() == Str(a[1])) return true; return false; }
            case "len": return a[0] is List<object> l ? (double)l.Count : (double)Str(a[0]).Length;
            case "at": { var l2 = a[0] as List<object>; int i = (int)Math.Floor(N(1)); return l2 != null && i >= 0 && i < l2.Count ? l2[i] : ""; }
            case "list": return new List<object>(a);
            case "str": return Str(a[0]);
            case "num": return N(0);
            case "upper": return Str(a[0]).ToUpperInvariant();
            case "lower": return Str(a[0]).ToLowerInvariant();
            case "cap": { var x = Str(a[0]); return x.Length == 0 ? x : char.ToUpperInvariant(x[0]) + x.Substring(1); }
            case "shuf":
                {
                    List<object> l3;
                    if (a.Count == 1 && a[0] is List<object> ll) l3 = new List<object>(ll);
                    else if (a.Count == 1 && a[0] is string str) { l3 = new List<object>(); foreach (var p in str.Split(',')) { var tp = p.Trim(); l3.Add(IsNumLike(tp) ? (object)ToNum(tp) : tp); } }
                    else l3 = new List<object>(a);
                    var rng = Env(s).Rng;
                    for (int i = l3.Count - 1; i > 0; i--) { int j = rng.Int(i + 1); var tmp = l3[i]; l3[i] = l3[j]; l3[j] = tmp; }
                    return l3;
                }
            case "var": { var vs = Vars(s); return vs != null && vs.TryGetValue(Str(a[0]), out var v) && v != null ? v : 0.0; }
            case "item": return (object)FindItem(s, Str(a[0])) ?? "";
            case "label": { var it = FindItem(s, Str(a[0])); return it == null ? "" : (J.Has(it, "label") && J.Str(it, "label") != "" ? J.Str(it, "label") : J.Str(it, "id")); }
            case "tag": { var it = FindItem(s, Str(a[0])); var tg = J.Obj(it, "tags"); return tg != null && tg.TryGetValue(Str(a[1]), out var v) && v != null ? v : ""; }
            case "randItem":
                {
                    var env = Env(s); var l4 = new List<object>();
                    foreach (var it in env.Items) { var tg = J.Obj(it, "tags"); object v = tg != null && tg.TryGetValue(Str(a[0]), out var vv) ? vv : null; if (Eq(v, a[1])) l4.Add(it); }
                    return l4.Count > 0 ? J.Str(l4[env.Rng.Int(l4.Count)], "id") : "";
                }
        }
        throw new FormatException("Hàm không có: " + f);
    }

    // ── API ──
    public static object Eval(string src, Dictionary<string, object> scope)
    {
        if (!Cache.TryGetValue(src, out var ast)) { ast = new Parser(Tokenize(src), src).Run(); Cache[src] = ast; }
        return Ev(ast, scope);
    }

    /// <summary>Tham số: chuỗi bắt đầu bằng "=" là biểu thức; còn lại giữ nguyên (số, chuỗi, bool...).</summary>
    public static object Val(object v, Dictionary<string, object> scope)
    {
        if (v is string s && s.Length > 0 && s[0] == '=') return Eval(s.Substring(1), scope);
        return v;
    }

    /// <summary>Mẫu câu: {{biểu thức}} tính theo scope; {tên} lấy từ qvars (viết HOA = in hoa).</summary>
    public static string Template(string tpl, Dictionary<string, object> qvars, Dictionary<string, object> scope)
    {
        if (tpl == null) return "";
        string r = Regex.Replace(tpl, @"\{\{(.+?)\}\}", m => { try { return Str(Eval(m.Groups[1].Value, scope)); } catch (Exception) { return m.Value; } }, RegexOptions.ECMAScript);
        return Regex.Replace(r, @"\{(\w+)\}", m =>
        {
            string k = m.Groups[1].Value;
            if (qvars != null && qvars.TryGetValue(k, out var v) && v != null) return Str(v);
            string lk = k.ToLowerInvariant();
            if (k == k.ToUpperInvariant() && qvars != null && qvars.TryGetValue(lk, out var v2) && v2 != null) return Str(v2).ToUpperInvariant();
            return m.Value;
        }, RegexOptions.ECMAScript);
    }
}
