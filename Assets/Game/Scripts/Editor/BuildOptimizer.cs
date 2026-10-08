using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Rút ngắn thời gian build Android:
///  - Profile Release (IL2CPP, game chạy nhanh nhất). Không có profile Mono / IL2CPP-Debug vì game chạy chậm.
///  - Tools/Build/Optimize Android Textures: nén ASTC + tắt mipmap cho Sprite (Android).
/// Không tự chạy gì — chỉ chạy khi bấm menu.
/// </summary>
public static class BuildOptimizer
{
    static readonly NamedBuildTarget Android = NamedBuildTarget.Android;

    // ───────────────────────── Profile build ─────────────────────────

    [MenuItem("Tools/Build/Profile/Release cài K02 (IL2CPP, không Development)")]
    public static void ProfileRelease()
    {
        if (!CanChangeSettings()) return;
        PlayerSettings.SetScriptingBackend(Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetIl2CppCompilerConfiguration(Android, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetIl2CppCodeGeneration(Android, Il2CppCodeGeneration.OptimizeSpeed);
        EditorUserBuildSettings.development = false;
        Log("IL2CPP + C++ Release + Faster runtime + không Development (không hiện console lỗi trên màn hình).");
    }

    // ───────────────────────── Texture Android ─────────────────────────

    const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;
    const int MaxSizeCap = 2048;

    // Game đóng băng (handoff/FROZEN.txt): không đổi import settings.
    static readonly Regex[] Skip =
    {
        new Regex(@"(^|/)AddUpGame[/.]"),      new Regex(@"(^|/)NumberAddUpGame[/.]"),
        new Regex(@"(^|/)PathFinderGame[/.]"), new Regex(@"(^|/)TrainPathGame[/.]"),
        new Regex(@"(^|/)MonopolyGame[/.]"),   new Regex(@"(^|/)RiverCrossGame[/.]"),
        new Regex(@"(^|/)BalloonGame[/.]"),    new Regex(@"(^|/)HaiQuaGame[/.]"),
        new Regex(@"^Assets/(Plugins|Editor|Gizmos|TextMesh Pro|StreamingAssets)/"),
        new Regex(@"/Editor/"),
    };

    [MenuItem("Tools/Build/Optimize Android Textures (xem trước)")]
    public static void TexturesPreview() => ProcessTextures(false);

    [MenuItem("Tools/Build/Optimize Android Textures (áp dụng)")]
    public static void TexturesApply()
    {
        if (!CanChangeSettings()) return;
        if (!EditorUtility.DisplayDialog("Tối ưu texture Android",
                "Sẽ đổi import settings (Android: ASTC 6x6, tắt mipmap Sprite, max 2048) cho nhiều texture rồi reimport — mất vài phút.\n" +
                "Nên commit trước để hoàn tác dễ. Tiếp tục?", "Áp dụng", "Huỷ")) return;
        ProcessTextures(true);
    }

    static void ProcessTextures(bool apply)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int changed = 0, skipped = 0, noMipOnly = 0;

        if (apply) AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsSkipped(path)) { skipped++; continue; }
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { skipped++; continue; }
                if (imp.textureType != TextureImporterType.Sprite && imp.textureType != TextureImporterType.Default) { skipped++; continue; }

                bool dirty = false;

                if (imp.textureType == TextureImporterType.Sprite && imp.mipmapEnabled)
                {
                    imp.mipmapEnabled = false;
                    dirty = true;
                    noMipOnly++;
                }

                var ps = imp.GetPlatformTextureSettings("Android");
                int size = Mathf.Min(imp.maxTextureSize, MaxSizeCap);
                if (!ps.overridden || ps.format != AndroidFormat || ps.maxTextureSize != size)
                {
                    ps.overridden = true;
                    ps.format = AndroidFormat;
                    ps.maxTextureSize = size;
                    ps.compressionQuality = 50;
                    imp.SetPlatformTextureSettings(ps);
                    dirty = true;
                }

                if (!dirty) continue;
                changed++;
                if (apply) imp.SaveAndReimport();
            }
        }
        finally
        {
            if (apply) AssetDatabase.StopAssetEditing();
        }

        Debug.Log($"[BuildOptimizer] Texture: {(apply ? "đã đổi" : "sẽ đổi")} {changed}/{guids.Length} " +
                  $"(tắt mipmap {noMipOnly}, bỏ qua {skipped}). Format Android = {AndroidFormat}, max {MaxSizeCap}.");
        if (!apply && changed > 0)
            Debug.Log("[BuildOptimizer] Chạy 'Optimize Android Textures (áp dụng)' để thực hiện.");
    }

    static bool IsSkipped(string path)
    {
        foreach (var r in Skip) if (r.IsMatch(path)) return true;
        return false;
    }

    // ───────────────────────── Helpers ─────────────────────────

    static bool CanChangeSettings()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[BuildOptimizer] Đang build/compile — thử lại sau.");
            return false;
        }
        return true;
    }

    static void Log(string msg) => Debug.Log("[BuildOptimizer] " + msg);
}
