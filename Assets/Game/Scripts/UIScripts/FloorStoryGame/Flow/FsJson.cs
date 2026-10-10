using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// JSON tối giản (không phụ thuộc gói ngoài, chạy được cả trong test dotnet thuần) cho dữ liệu game "flow" soạn trên web builder.
/// Kiểu: object = Dictionary&lt;string,object&gt;, array = List&lt;object&gt;, số = double, chuỗi = string, bool, null.
/// Bản JS tương ứng là JSON.parse. Các hàm J.* đọc mềm: thiếu/sai kiểu thì trả mặc định (giống cách JS coi undefined → mặc định).
/// </summary>
public static class FsJson
{
    public static object Parse(string s)
    {
        int i = 0;
        var v = Value(s, ref i);
        Ws(s, ref i);
        if (i < s.Length) throw new FormatException("JSON thừa ký tự ở " + i);
        return v;
    }

    static void Ws(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿')) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length) throw new FormatException("JSON hết sớm");
        char c = s[i];
        if (c == '{')
        {
            i++; var d = new Dictionary<string, object>();
            Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i);
                if (s[i++] != ':') throw new FormatException("JSON thiếu ':' ở " + i);
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("JSON sai object ở " + i);
            }
        }
        if (c == '[')
        {
            i++; var l = new List<object>();
            Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("JSON sai array ở " + i);
            }
        }
        if (c == '"') return Str(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int j = i;
        while (j < s.Length && "+-0123456789.eE".IndexOf(s[j]) >= 0) j++;
        if (j == i) throw new FormatException("JSON ký tự lạ '" + c + "' ở " + i);
        double n = double.Parse(s.Substring(i, j - i), CultureInfo.InvariantCulture);
        i = j;
        return n;
    }

    static string Str(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException("JSON thiếu '\"' ở " + i);
        i++;
        var sb = new StringBuilder();
        while (s[i] != '"')
        {
            char c = s[i++];
            if (c == '\\')
            {
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;   // \" \\ \/
                }
            }
            else sb.Append(c);
        }
        i++;
        return sb.ToString();
    }

    public static string Stringify(object o)
    {
        var sb = new StringBuilder(); Write(sb, o); return sb.ToString();
    }

    static void Write(StringBuilder sb, object o)
    {
        switch (o)
        {
            case null: sb.Append("null"); break;
            case string s:
                sb.Append('"');
                foreach (char c in s)
                {
                    if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\"); else if (c == '\n') sb.Append("\\n"); else if (c == '\r') sb.Append("\\r"); else if (c == '\t') sb.Append("\\t");
                    else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                }
                sb.Append('"'); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case List<object> l:
                sb.Append('['); for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(','); Write(sb, l[i]); } sb.Append(']'); break;
            case Dictionary<string, object> m:
                sb.Append('{'); bool first = true;
                foreach (var kv in m) { if (!first) sb.Append(','); first = false; Write(sb, kv.Key); sb.Append(':'); Write(sb, kv.Value); }
                sb.Append('}'); break;
            default: Write(sb, o.ToString()); break;
        }
    }
}

/// <summary>Đọc mềm từ cây JSON (object/array/số/chuỗi).</summary>
public static class J
{
    public static Dictionary<string, object> Obj(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
    public static List<object> List(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v as List<object> : null;
    public static object Get(object o, string k) => o is Dictionary<string, object> d && d.TryGetValue(k, out var v) ? v : null;
    public static bool Has(object o, string k) => o is Dictionary<string, object> d && d.ContainsKey(k) && d[k] != null;

    public static string Str(object o, string k, string def = "")
    {
        var v = Get(o, k);
        return v == null ? def : v is string s ? s : v is bool b ? (b ? "true" : "false") : v is double dd ? FsExpr.NumToString(dd) : v.ToString();
    }

    public static double Num(object o, string k, double def = 0)
    {
        var v = Get(o, k);
        if (v is double d) return d;
        if (v is bool b) return b ? 1 : 0;
        if (v is string s && s.Length > 0 && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return r;
        return def;
    }

    public static int Int(object o, string k, int def = 0) => (int)Math.Round(Num(o, k, def));
    public static List<object> Arr(object v) => v as List<object> ?? new List<object>();
}
