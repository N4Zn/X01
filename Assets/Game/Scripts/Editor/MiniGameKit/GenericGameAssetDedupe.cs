using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gom ẢNH TRÙNG NỘI DUNG của các GenericGame về 1 thư mục dùng chung
/// Resources/TestTongHop/images/GenericGames/_shared/&lt;md5-12&gt;.png (giảm dung lượng build + thời gian build).
///
/// An toàn cho game cũ:
///  - File được DI CHUYỂN bằng AssetDatabase (giữ nguyên GUID); các bản trùng bị xoá nhưng mọi tham chiếu GUID
///    trong .prefab/.unity/.asset được đổi sang bản dùng chung.
///  - game.json đổi "a0_0_4.png" → "_shared/&lt;hash&gt;.png"; runtime (GenericGameController.ResolveFolder) hiểu tiền tố thư mục,
///    tên không có '/' vẫn chạy như cũ.
///  - Idempotent: chạy lại không làm gì thêm. Game có ảnh mà không tìm thấy game.json thì bị bỏ qua.
///  - Zip xuất từ web builder KHÔNG đổi định dạng; import zip tự gọi lại tool này cho game vừa import.
/// Chỉ ảnh (png/jpg); âm thanh nhỏ nên không gom.
/// </summary>
public static class GenericGameAssetDedupe
{
    const string ImagesRoot = "Assets/Game/Resources/TestTongHop/images/GenericGames";
    const string GamesRoot = "Assets/Game/GenericGames";
    const string YamlRoot = "Assets/Game";
    public const string SharedFolder = "_shared";
    static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg" };
    static readonly string[] YamlExts = { ".prefab", ".unity", ".asset" };

    class Item
    {
        public string path, owner, name, hash; public long size;
    }

    [MenuItem("Tools/GenericGame/Dọn ảnh trùng (xem trước)")]
    static void Preview() => Run(false, null, true);

    [MenuItem("Tools/GenericGame/Dọn ảnh trùng (áp dụng)")]
    static void Apply()
    {
        if (!EditorUtility.DisplayDialog("Dọn ảnh trùng GenericGame",
                "Sẽ gom ảnh trùng nội dung về thư mục _shared, sửa game.json và đổi tham chiếu trong prefab/scene.\n" +
                "Nên commit trước để hoàn tác dễ. Tiếp tục?", "Áp dụng", "Huỷ")) return;
        Run(true, null, true);
    }

    /// <param name="onlyGameId">null = tất cả game; ngược lại chỉ xét ảnh của game đó + _shared.</param>
    public static void Run(bool apply, string onlyGameId, bool interactive)
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling)
        { Debug.LogWarning("[Dedupe] Đang Play/build/compile — thử lại sau."); return; }
        if (apply && interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string absImages = Path.GetFullPath(ImagesRoot);
        if (!Directory.Exists(absImages)) return;

        // 1. Liệt kê ảnh + hash
        var items = new List<Item>();
        var skippedOwners = new HashSet<string>();
        foreach (var dir in Directory.GetDirectories(absImages))
        {
            string owner = Path.GetFileName(dir);
            if (onlyGameId != null && owner != onlyGameId && owner != SharedFolder) continue;
            if (owner != SharedFolder && !File.Exists($"{GamesRoot}/{owner}/game.json"))
            { skippedOwners.Add(owner); continue; } // không có game.json để sửa tham chiếu → không đụng
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (!ImageExts.Contains(ext)) continue;
                items.Add(new Item
                {
                    path = $"{ImagesRoot}/{owner}/{Path.GetFileName(f)}",
                    owner = owner, name = Path.GetFileName(f), size = new FileInfo(f).Length, hash = Md5(f),
                });
            }
        }

        // 2. Lập kế hoạch
        var jsonMap = new Dictionary<string, Dictionary<string, string>>(); // owner → {tên cũ → "_shared/mới"}
        var guidMap = new Dictionary<string, string>();                      // guid bị xoá → guid giữ lại
        var toDelete = new List<string>();
        var toMove = new List<(string from, string to)>();
        long saved = 0; int groups = 0;

        foreach (var g in items.GroupBy(i => i.hash).Where(g => g.Count() > 1))
        {
            groups++;
            var members = g.ToList();
            var shared = members.FirstOrDefault(m => m.owner == SharedFolder);
            string ext = Path.GetExtension(members[0].name).ToLowerInvariant();
            string sharedName = shared != null ? shared.name : g.Key.Substring(0, 12) + ext;
            string sharedPath = $"{ImagesRoot}/{SharedFolder}/{sharedName}";
            Item keeper = shared ?? members[0];

            foreach (var m in members)
            {
                if (m.owner == SharedFolder) continue;
                if (!jsonMap.TryGetValue(m.owner, out var map)) jsonMap[m.owner] = map = new Dictionary<string, string>();
                map[m.name] = $"{SharedFolder}/{sharedName}";
            }

            if (shared == null) toMove.Add((keeper.path, sharedPath));
            string keepGuid = AssetDatabase.AssetPathToGUID(keeper.path);
            foreach (var m in members)
            {
                if (m == keeper || m.owner == SharedFolder) continue;
                string guid = AssetDatabase.AssetPathToGUID(m.path);
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(keepGuid)) guidMap[guid] = keepGuid;
                toDelete.Add(m.path);
                saved += m.size;
            }
        }

        string scope = onlyGameId ?? "tất cả game";
        string summary = $"[Dedupe] {scope}: {items.Count} ảnh, {groups} nhóm trùng → xoá {toDelete.Count} file, tiết kiệm ~{saved / 1048576f:F1} MB." +
                         (skippedOwners.Count > 0 ? $" Bỏ qua (không có game.json): {string.Join(", ", skippedOwners)}." : "");
        if (!apply) { Debug.Log(summary + (groups > 0 ? " Chạy '(áp dụng)' để thực hiện." : "")); return; }
        if (groups == 0) { if (interactive) Debug.Log(summary); return; }

        // 3. Sửa game.json (chỉ đổi giá trị chuỗi khớp TÊN FILE ĐÚNG — không đụng text khác)
        var q = new Regex("\"([^\"\\\\/]+\\.(?:png|jpg|jpeg))\"", RegexOptions.IgnoreCase);
        foreach (var kv in jsonMap)
        {
            string jp = $"{GamesRoot}/{kv.Key}/game.json";
            string text = File.ReadAllText(jp, Encoding.UTF8);
            string fixedText = q.Replace(text, mt => kv.Value.TryGetValue(mt.Groups[1].Value, out var to) ? "\"" + to + "\"" : mt.Value);
            if (fixedText != text) File.WriteAllText(jp, fixedText, new UTF8Encoding(false));
        }

        // 4. Đổi tham chiếu GUID trong prefab/scene/asset
        if (guidMap.Count > 0)
        {
            var gx = new Regex("guid: ([0-9a-f]{32})");
            foreach (var f in Directory.EnumerateFiles(Path.GetFullPath(YamlRoot), "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (!YamlExts.Contains(ext)) continue;
                string text = File.ReadAllText(f);
                if (!text.Contains("guid: ")) continue;
                string fixedText = gx.Replace(text, mt => guidMap.TryGetValue(mt.Groups[1].Value, out var to) ? "guid: " + to : mt.Value);
                if (fixedText != text) File.WriteAllText(f, fixedText, new UTF8Encoding(false));
            }
        }

        // 5. Di chuyển bản giữ lại → _shared (giữ GUID), xoá bản trùng
        if (toMove.Count > 0 && !AssetDatabase.IsValidFolder($"{ImagesRoot}/{SharedFolder}"))
            AssetDatabase.CreateFolder(ImagesRoot, SharedFolder);
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var (from, to) in toMove)
            {
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) Debug.LogError($"[Dedupe] Không chuyển được {from} → {to}: {err}");
            }
            foreach (var p in toDelete) AssetDatabase.DeleteAsset(p);
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(summary.Replace("→ xoá", "→ ĐÃ xoá"));
    }

    static string Md5(string file)
    {
        using var md5 = MD5.Create();
        using var fs = File.OpenRead(file);
        return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }
}
