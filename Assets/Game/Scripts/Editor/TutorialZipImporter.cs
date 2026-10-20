using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import file zip xuất từ tutorial_builder (HTML): tutorial.json + tutorial/ (ảnh màn, giọng đọc, ảnh bàn chân).
/// Chép vào Assets/Game/Resources/Tutorials/{gameId}/ — TutorialPlayer tự phát khi vào game có tên gameId
/// (tên trong GameRegistry, vd AddNumber5). Import lại cùng gameId sẽ thay bản cũ.
/// </summary>
public static class TutorialZipImporter
{
    const string Root = "Assets/Game/Resources/" + TutorialPlayer.ResourcesRoot;

    [Serializable] class Head { public string kind; public string gameId; }

    [MenuItem("Tools/Tutorial/Import Tutorial Zip...", priority = 1)]
    static void ImportMenu()
    {
        string zip = EditorUtility.OpenFilePanel("Chọn file zip tutorial", "", "zip");
        if (string.IsNullOrEmpty(zip)) return;
        try
        {
            string id = Import(zip);
            EditorUtility.DisplayDialog("Import tutorial", $"Đã import tutorial cho game \"{id}\".\n\nVào game {id} (hoặc chạy thẳng scene của nó) là tutorial tự chạy. Bấm BẮT ĐẦU (Editor: Space/Enter) để vào chơi.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Import tutorial", "Không import được: " + e.Message, "OK");
        }
    }

    /// <summary>Trả về gameId đã import.</summary>
    public static string Import(string zipPath)
    {
        using var za = ZipFile.OpenRead(zipPath);
        var json = za.GetEntry("tutorial.json") ?? throw new Exception("zip không có tutorial.json (zip này không phải do tutorial_builder xuất ra).");
        string text;
        using (var sr = new StreamReader(json.Open(), Encoding.UTF8)) text = sr.ReadToEnd();
        var head = JsonUtility.FromJson<Head>(text);
        string id = head != null && !string.IsNullOrEmpty(head.gameId) ? head.gameId.Trim() : null;
        if (string.IsNullOrEmpty(id)) throw new Exception("tutorial.json chưa có Game ID — nhập Game ID trong tool rồi xuất lại.");
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new Exception("Game ID có ký tự không hợp lệ: " + id);

        string dir = $"{Root}/{id}";
        if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "tutorial.json"), text, new UTF8Encoding(false));
        int files = 0;
        foreach (var e in za.Entries)
        {
            if (!e.FullName.StartsWith("tutorial/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(e.Name)) continue;
            string ext = Path.GetExtension(e.Name).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".wav" && ext != ".mp3" && ext != ".ogg") continue;
            using var src = e.Open();
            using var dst = File.Create(Path.Combine(dir, e.Name));
            src.CopyTo(dst);
            files++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[TutorialZipImporter] {Path.GetFileName(zipPath)} → {dir} ({files} file ảnh/âm thanh)");
        return id;
    }

    [MenuItem("Tools/Tutorial/Mở thư mục tutorial đã import", priority = 2)]
    static void Reveal()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root);
    }

    [MenuItem("Tools/Tutorial/Bỏ qua tutorial khi Play (Editor)", priority = 20)]
    static void ToggleSkip() => EditorPrefs.SetBool(TutorialPlayer.SkipPrefKey, !EditorPrefs.GetBool(TutorialPlayer.SkipPrefKey, false));

    [MenuItem("Tools/Tutorial/Bỏ qua tutorial khi Play (Editor)", true)]
    static bool ToggleSkipCheck()
    {
        Menu.SetChecked("Tools/Tutorial/Bỏ qua tutorial khi Play (Editor)", EditorPrefs.GetBool(TutorialPlayer.SkipPrefKey, false));
        return true;
    }
}

/// <summary>Ảnh màn tutorial: giữ nét (không nén, không mipmap, không ép kích thước lũy thừa 2).</summary>
class TutorialTextureImportProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/" + TutorialPlayer.ResourcesRoot + "/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
    }
}
