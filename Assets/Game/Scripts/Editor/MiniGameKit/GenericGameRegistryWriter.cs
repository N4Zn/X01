using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Đăng ký game vừa import vào GameRegistry.cs (chèn trên marker &lt;GENERIC-GAMES&gt;) và game_registry.json của ControlUiAndroidLib.
/// Không tự rebuild aar — importer chỉ nhắc lệnh ở cuối log.</summary>
public static class GenericGameRegistryWriter
{
    const string RegistryCs = "Assets/Game/Scripts/UIScripts/Common/GameRegistry.cs";
    const string RegistryJson = "NativePlugins/ControlUiAndroidLib/controlui/src/main/assets/game_registry.json";
    const string Marker = "// <GENERIC-GAMES>";
    const string PrefCat = "GenericGame.LastCategory", PrefGroup = "GenericGame.LastGroup";

    static string CsPath => Path.Combine(ProjectRoot, RegistryCs);
    static string JsonPath => Path.Combine(ProjectRoot, RegistryJson);
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    public static bool IsRegistered(string gameId)
        => File.Exists(CsPath) && Regex.IsMatch(File.ReadAllText(CsPath, Encoding.UTF8), @"Set\(\s*\d+\s*,\s*\d+\s*,\s*""" + Regex.Escape(gameId) + @"""");

    public struct Result { public bool registered, jsonChanged; public string line; }

    /// <summary>Hỏi category/group rồi ghi vào GameRegistry.cs + game_registry.json. Đã đăng ký rồi thì bỏ qua (registered=true).</summary>
    public static Result RegisterWithPrompt(string gameId, string displayName)
    {
        var res = new Result();
        if (IsRegistered(gameId)) { res.registered = true; return res; }
        if (!RegistryPrompt.Ask(gameId, ref displayName, out int cat, out string group)) return res;
        return Register(gameId, displayName, cat, group);
    }

    public static Result Register(string gameId, string displayName, int cat, string group)
    {
        var res = new Result();
        if (!File.Exists(CsPath)) { Debug.LogError("[GenericGameRegistry] Không thấy " + RegistryCs); return res; }
        string cs = File.ReadAllText(CsPath, Encoding.UTF8);
        string nl = cs.Contains("\r\n") ? "\r\n" : "\n";
        int mk = cs.IndexOf(Marker, StringComparison.Ordinal);

        int max = -1;
        foreach (Match m in Regex.Matches(cs, @"Set\(\s*" + cat + @"\s*,\s*(\d+)\s*,")) max = Mathf.Max(max, int.Parse(m.Groups[1].Value));
        int idx = max + 1;
        if (idx >= GameRegistry.MAX_PER_CATEGORY) { Debug.LogError($"[GenericGameRegistry] Category {cat} đã đầy ({GameRegistry.MAX_PER_CATEGORY} game) — tăng MAX_PER_CATEGORY hoặc chọn category khác."); return res; }

        string g = string.IsNullOrEmpty(group) ? "" : $", group: \"{Cs(group)}\"";
        res.line = $"Set({cat}, {idx}, \"{gameId}\", \"{Cs(displayName)}\", \"{gameId}\", Engine.MiniGameKit{g});";

        if (mk < 0)
        {
            Debug.LogWarning($"[GenericGameRegistry] Thiếu marker '{Marker}' trong GameRegistry.cs — hãy dán tay dòng sau:\n  {res.line}");
        }
        else
        {
            int ls = cs.LastIndexOf('\n', mk) + 1; // đầu dòng chứa marker → chèn NGAY TRÊN dòng đó, giữ nguyên thụt lề
            cs = cs.Insert(ls, "        " + res.line + nl);
            File.WriteAllText(CsPath, cs, new UTF8Encoding(false));
            res.registered = true;
        }

        res.jsonChanged = AddToJson(gameId, displayName, cat, group);
        EditorPrefs.SetInt(PrefCat, cat);
        EditorPrefs.SetString(PrefGroup, group ?? "");
        return res;
    }

    static bool AddToJson(string gameId, string displayName, int cat, string group)
    {
        if (!File.Exists(JsonPath)) { Debug.LogWarning("[GenericGameRegistry] Không thấy " + RegistryJson); return false; }
        string txt = File.ReadAllText(JsonPath, Encoding.UTF8);
        if (Regex.IsMatch(txt, "\"name\"\\s*:\\s*\"" + Regex.Escape(gameId) + "\"")) return false;
        string nl = txt.Contains("\r\n") ? "\r\n" : "\n";
        int last = txt.LastIndexOf(']'); // ngoặc đóng của mảng "games" (mảng cuối file)
        if (last < 0) { Debug.LogWarning("[GenericGameRegistry] game_registry.json sai định dạng, bỏ qua."); return false; }
        string grp = string.IsNullOrEmpty(group) ? "" : $", \"group\": \"{Js(group)}\"";
        string entry = $"{{\"name\": \"{gameId}\", \"displayName\": \"{Js(displayName)}\"{grp}, \"sceneName\": \"{gameId}\", \"category\": {cat}}}";
        string head = txt.Substring(0, last).TrimEnd();
        File.WriteAllText(JsonPath, head + "," + nl + "    " + entry + nl + "  " + txt.Substring(last), new UTF8Encoding(false));
        return true;
    }

    static string Cs(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    static string Js(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Hộp thoại modal: tên hiển thị + category + group.</summary>
    class RegistryPrompt : EditorWindow
    {
        string _gameId, _display, _group;
        int _cat;
        bool _ok;
        List<string> _groups = new List<string>();

        public static bool Ask(string gameId, ref string displayName, out int cat, out string group)
        {
            var w = CreateInstance<RegistryPrompt>();
            w.titleContent = new GUIContent("Đăng ký game vào GameRegistry");
            w._gameId = gameId; w._display = displayName;
            w._cat = Mathf.Clamp(EditorPrefs.GetInt(PrefCat, 0), 0, GameRegistry.CATEGORY_COUNT - 1);
            w._group = EditorPrefs.GetString(PrefGroup, "");
            w.RefreshGroups();
            w.minSize = w.maxSize = new Vector2(440, 190);
            w.ShowModalUtility();
            displayName = w._display; cat = w._cat; group = (w._group ?? "").Trim();
            return w._ok;
        }

        void RefreshGroups()
        {
            _groups.Clear();
            for (int i = 0; i < GameRegistry.MAX_PER_CATEGORY; i++)
            {
                var e = GameRegistry.Games[_cat, i];
                if (!e.IsEmpty && !string.IsNullOrEmpty(e.group) && !_groups.Contains(e.group)) _groups.Add(e.group);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Game: " + _gameId, EditorStyles.boldLabel);
            _display = EditorGUILayout.TextField("Tên hiển thị", _display);
            int newCat = EditorGUILayout.Popup("Category (môn)", _cat, GameRegistry.CategoryNames);
            if (newCat != _cat) { _cat = newCat; RefreshGroups(); }
            EditorGUILayout.BeginHorizontal();
            _group = EditorGUILayout.TextField("Group (nhóm)", _group);
            if (_groups.Count > 0)
            {
                int pick = EditorGUILayout.Popup(0, Prepend("Chọn…", _groups), GUILayout.Width(80));
                if (pick > 0) _group = _groups[pick - 1];
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Group để trống = game đứng riêng, không thuộc nhóm.", MessageType.None);
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Bỏ qua", GUILayout.Width(90))) Close();
            GUI.enabled = !string.IsNullOrWhiteSpace(_display);
            if (GUILayout.Button("Đăng ký", GUILayout.Width(90))) { _ok = true; Close(); }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        static string[] Prepend(string first, List<string> rest)
        {
            var a = new string[rest.Count + 1];
            a[0] = first;
            for (int i = 0; i < rest.Count; i++) a[i + 1] = rest[i];
            return a;
        }
    }
}
