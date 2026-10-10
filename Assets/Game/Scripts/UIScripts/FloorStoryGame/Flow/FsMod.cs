using System;
using System.Collections.Generic;

/// <summary>
/// Đọc tham số của mô-đun flow (gen/view/judge/reveal) với mặc định từ <see cref="FsModData"/> (sinh từ MOD của fs_flow.js).
/// Bản C# của `par()`/`jpar()` trong fs_flow.js: số thiếu/sai → mặc định; text/select/json thiếu → mặc định, có thì giữ nguyên giá trị.
/// </summary>
public static class FsMod
{
    static Dictionary<string, object> _all;
    static Dictionary<string, object> All => _all ?? (_all = (Dictionary<string, object>)FsJson.Parse(FsModData.Json));

    static Dictionary<string, object> Def(string kind, string type, string key)
    {
        var k = J.Obj(All, kind);
        if (k == null) return null;
        if (kind == "reveal") return J.Obj(k, key);
        return J.Obj(J.Obj(k, type), key);
    }

    /// <summary>Giá trị tham số thô (object): kiểu số → double; text/select/json → giá trị gốc (hoặc mặc định).</summary>
    public static object Par(string kind, object spec, string key)
    {
        string type = kind == "reveal" ? "" : J.Str(spec, "type");
        var d = Def(kind, type, key);
        var p = J.Obj(spec, "p");
        object v = p != null && p.TryGetValue(key, out var vv) ? vv : null;
        string t = d != null ? J.Str(d, "t") : "";
        object def = d != null ? J.Get(d, "d") : null;
        if (t == "text" || t == "select" || t == "json") return v ?? def;
        if (v == null || (v is string s0 && s0 == "")) return def ?? 0.0;
        double n = FsExpr.ToNum(v);
        if (v is string s1 && !double.TryParse(s1, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) return def ?? 0.0;
        return n;
    }

    public static double Num(string kind, object spec, string key) => FsExpr.ToNum(Par(kind, spec, key));
    public static int Int(string kind, object spec, string key) => (int)Math.Round(Num(kind, spec, key));
    public static bool Flag(string kind, object spec, string key) => Num(kind, spec, key) != 0;
    public static string Str(string kind, object spec, string key) => FsExpr.Str(Par(kind, spec, key));

    /// <summary>Tham số JSON (object hoặc chuỗi JSON) → cây; lỗi → `def`.</summary>
    public static object JsonPar(string kind, object spec, string key, object def)
    {
        var v = Par(kind, spec, key);
        if (v is Dictionary<string, object> || v is List<object>) return v;
        if (v is string s && s.Trim().Length > 0) { try { return FsJson.Parse(s); } catch (Exception) { return def; } }
        return def;
    }
}
