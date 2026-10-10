using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import "gói thay ảnh" của game FloorStory (zip mở/sửa được bằng game_builder_v2.html; tạo bằng Tools/floorstory_pack/make_packs.py).
/// zip: game.json (meta.kind = "floorstory", meta.floorStory.images[] = {key, label, group, file, default}) + assets/*.
/// Ảnh nào bị thay (file != default) → Assets/Game/Resources/StoryPack/&lt;Game&gt;/&lt;key&gt;.&lt;ext&gt; (StoryUI.Load ưu tiên thư mục này);
/// ảnh về mặc định (file == default) → xoá bản thay thế cũ nếu có. Không đụng scene/registry/aar. Sau import: build lại APK.
/// </summary>
public static class FloorStoryPackImporter
{
    const string PackRoot = "Assets/Game/Resources/StoryPack";

    [Serializable] class Image { public string key, label, group, file, @default; }
    [Serializable] class Pack { public string game; public int version; public Image[] images; }
    [Serializable] class Meta { public string gameId, kind; public Pack floorStory; }
    [Serializable] class Root { public Meta meta; }

    [MenuItem("Tools/FloorStoryGame/Import Pack Zips (thay ảnh)...")]
    public static void ImportFolder()
    {
        string dir = EditorUtility.OpenFolderPanel("Chọn thư mục chứa các gói FloorStory .zip", "", "");
        if (string.IsNullOrEmpty(dir)) return;
        int n = 0, files = 0;
        foreach (var zip in Directory.GetFiles(dir, "*.zip"))
        {
            if (!IsPack(zip)) continue;
            try { files += ImportZip(zip); n++; }
            catch (Exception e) { Debug.LogError($"[FloorStoryPack] Lỗi import '{zip}': {e}"); }
        }
        AssetDatabase.Refresh();
        Debug.Log($"[FloorStoryPack] Xong {n} gói, {files} ảnh thay thế.");
    }

    [MenuItem("Tools/FloorStoryGame/Import Pack Zip (1 file)...")]
    public static void ImportOne()
    {
        string zip = EditorUtility.OpenFilePanel("Chọn gói FloorStory .zip", "", "zip");
        if (string.IsNullOrEmpty(zip)) return;
        int files = ImportZip(zip);
        AssetDatabase.Refresh();
        Debug.Log($"[FloorStoryPack] Xong: {files} ảnh thay thế.");
    }

    /// <summary>zip có game.json với meta.kind == "floorstory" không (GenericGameZipImporter dùng để chuyển hướng).</summary>
    public static bool IsPack(string zipPath)
    {
        try
        {
            using (var z = ZipFile.OpenRead(zipPath))
            {
                var e = z.GetEntry("game.json");
                if (e == null) return false;
                using (var r = new StreamReader(e.Open()))
                {
                    var root = JsonUtility.FromJson<Root>(r.ReadToEnd());
                    return root != null && root.meta != null && (root.meta.kind == "floorstory" || root.meta.kind == "fsdata");
                }
            }
        }
        catch { return false; }
    }

    /// <summary>Trả về số ảnh thay thế đã ghi.</summary>
    public static int ImportZip(string zipPath)
    {
        int written = 0;
        using (var z = ZipFile.OpenRead(zipPath))
        {
            var je = z.GetEntry("game.json");
            if (je == null) throw new Exception("Không có game.json");
            Root root;
            using (var r = new StreamReader(je.Open())) root = JsonUtility.FromJson<Root>(r.ReadToEnd());
            if (root?.meta?.kind == "fsdata") return ImportFlow(z, zipPath);
            var pack = root?.meta?.floorStory;
            if (pack == null || string.IsNullOrEmpty(pack.game) || pack.images == null) throw new Exception("Không phải gói FloorStory (thiếu meta.floorStory)");

            foreach (var img in pack.images)
            {
                if (string.IsNullOrEmpty(img.key)) continue;
                string ext = Path.GetExtension(string.IsNullOrEmpty(img.file) ? img.@default : img.file);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                string baseRel = $"{PackRoot}/{pack.game}/{img.key}";

                bool replaced = !string.IsNullOrEmpty(img.file) && img.file != img.@default;
                if (!replaced)
                {
                    foreach (var e in new[] { ".png", ".jpg", ".jpeg" })
                        if (File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath(baseRel + e))) AssetDatabase.DeleteAsset(baseRel + e);
                    continue;
                }

                var entry = z.GetEntry("assets/" + img.file);
                if (entry == null) { Debug.LogWarning($"[FloorStoryPack] {pack.game}: thiếu assets/{img.file} cho '{img.key}'"); continue; }
                string rel = baseRel + ext.ToLowerInvariant();
                string abs = MiniGameSceneBuilderHelpers.ToAbsolutePath(rel);
                Directory.CreateDirectory(Path.GetDirectoryName(abs));
                // Đổi đuôi (png ↔ jpg) thì xoá bản cũ để Resources.Load không nhập nhằng.
                foreach (var e in new[] { ".png", ".jpg", ".jpeg" })
                    if (e != ext.ToLowerInvariant() && File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath(baseRel + e))) AssetDatabase.DeleteAsset(baseRel + e);
                using (var src = entry.Open()) using (var dst = File.Create(abs)) src.CopyTo(dst);
                AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
                written++;
            }
        }
        return written;
    }

    const string FlowRoot = "Assets/Game/Resources/StoryData";

    /// <summary>
    /// Gói "flow" (meta.kind = "fsdata", soạn trên web builder; docs/floor-story-mechanics.md): ghi dữ liệu + ảnh vào
    /// Resources/StoryData/&lt;gameId&gt;/ (game.json = meta.fsData), dựng scene nếu chưa có, đăng ký game nếu chưa có.
    /// FloorStoryController thấy StoryData/&lt;gameId&gt;/game thì chạy FlowWorld thay cho world code tay. Trả về số file ghi.
    /// </summary>
    static int ImportFlow(ZipArchive z, string zipPath)
    {
        var je = z.GetEntry("game.json");
        string text; using (var r = new StreamReader(je.Open())) text = r.ReadToEnd();
        var rootObj = FsJson.Parse(text);
        var meta = J.Obj(rootObj, "meta"); var fsData = J.Obj(meta, "fsData");
        if (fsData == null) throw new Exception("Thiếu meta.fsData");
        string gid = J.Str(meta, "gameId"); if (string.IsNullOrEmpty(gid)) gid = Path.GetFileNameWithoutExtension(zipPath);
        string title = J.Str(fsData, "title", J.Str(meta, "displayName", gid));
        string dir = $"{FlowRoot}/{gid}";
        string absDir = MiniGameSceneBuilderHelpers.ToAbsolutePath(dir);
        if (Directory.Exists(absDir)) AssetDatabase.DeleteAsset(dir);
        Directory.CreateDirectory(absDir);
        File.WriteAllText(absDir + "/game.json", FsJson.Stringify(fsData), new System.Text.UTF8Encoding(false));
        int n = 1;
        foreach (var e in z.Entries)
        {
            if (!e.FullName.StartsWith("assets/") || e.FullName.EndsWith("/")) continue;
            using (var src = e.Open()) using (var dst = File.Create(absDir + "/" + Path.GetFileName(e.FullName))) src.CopyTo(dst);
            n++;
        }
        AssetDatabase.Refresh();
        string scenePath = "Assets/Game/Scenes/FloorStory/" + gid + ".unity";
        if (!File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath(scenePath)))
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) FloorStorySceneBuilder.BuildScene(gid);
        }
        if (!GenericGameRegistryWriter.IsRegistered(gid))
        {
            var rr = GenericGameRegistryWriter.Register(gid, title, 0, "Thử nghiệm");
            if (rr.registered) Debug.Log($"[FloorStoryPack] Đã đăng ký '{gid}'. Nhớ build lại aar: sh Tools/build-aar.sh controlui (K02 mới thấy game).");
        }
        Debug.Log($"[FloorStoryPack] Flow '{gid}': {n} file → {dir}");
        return n;
    }
}
