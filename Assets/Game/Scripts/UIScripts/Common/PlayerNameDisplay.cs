using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Tên người chơi HIỂN THỊ trên màn chiếu. Khoá nội bộ (log, điểm, breakdown gửi ControlActivity) vẫn là
/// TÊN THẬT do nhận diện khuôn mặt trả về — chỉ gọi <see cref="Format"/> ở chỗ vẽ lên UI.
///  • Mặc định (<see cref="GameSettings.ShowRealName"/> = false): tên thường gọi (alias) lấy từ
///    /sdcard/EduXplore/roster.json; chưa có alias thì dùng 2 tiếng cuối của tên thật.
///  • Bật "tên thật": 2 tiếng cuối của tên thật (vd "Vũ Đình Khánh" → "Đình Khánh").
/// Tên ≤ 2 tiếng ("Player_1", "Blue_1", "Khánh Vy") giữ nguyên.
/// </summary>
public static class PlayerNameDisplay
{
    [Serializable] class RosterFile { public List<RosterClass> classes; }
    [Serializable] class RosterClass { public List<RosterStudent> students; }
    [Serializable] class RosterStudent { public string name; public string alias; }

    static Dictionary<string, string> _alias;   // tên thật -> alias (chỉ những bạn có alias)
    static string _aliasFile;
    static DateTime _aliasMtime;

    public static string Format(string realName)
    {
        if (string.IsNullOrWhiteSpace(realName)) return realName;
        bool showReal = GameSettings.Instance != null && GameSettings.Instance.ShowRealName;
        if (!showReal)
        {
            string alias = AliasOf(realName.Trim());
            if (!string.IsNullOrEmpty(alias)) return alias;
        }
        return LastTwoSyllables(realName);
    }

    /// <summary>2 tiếng cuối của tên (tên ≤ 2 tiếng giữ nguyên).</summary>
    public static string LastTwoSyllables(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        string[] parts = name.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return string.Join(" ", parts);
        return parts[parts.Length - 2] + " " + parts[parts.Length - 1];
    }

    static string AliasOf(string realName)
    {
        EnsureLoaded();
        return _alias != null && _alias.TryGetValue(realName, out string a) ? a : null;
    }

    // roster.json do app Quản lý lớp ghi lại sau mỗi thay đổi — nạp lại khi mtime đổi (rẻ: chỉ 1 lần stat/lần gọi).
    static void EnsureLoaded()
    {
        try
        {
            if (_aliasFile == null) _aliasFile = Path.Combine(SharedStorage.Dir, "roster.json");
            if (!File.Exists(_aliasFile)) { _alias = null; return; }
            DateTime mtime = File.GetLastWriteTimeUtc(_aliasFile);
            if (_alias != null && mtime == _aliasMtime) return;

            var map = new Dictionary<string, string>();
            var roster = JsonUtility.FromJson<RosterFile>(File.ReadAllText(_aliasFile));
            if (roster?.classes != null)
                foreach (var c in roster.classes)
                    if (c?.students != null)
                        foreach (var s in c.students)
                            if (s != null && !string.IsNullOrWhiteSpace(s.name) && !string.IsNullOrWhiteSpace(s.alias))
                                map[s.name.Trim()] = s.alias.Trim();
            _alias = map;
            _aliasMtime = mtime;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayerNameDisplay] đọc roster.json lỗi: " + e.Message);
            if (_alias == null) _alias = new Dictionary<string, string>();
        }
    }
}
