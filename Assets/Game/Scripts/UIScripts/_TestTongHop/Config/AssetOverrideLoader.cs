using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Load sprite / audio clip từ persistentDataPath trước, fallback về Resources nếu không có.
///
/// Cho phép cập nhật ảnh / âm thanh trên thiết bị mà không rebuild APK:
///
///   ─── Đường dẫn override (Android) ──────────────────────────────────────────
///   /sdcard/Android/data/&lt;package&gt;/files/TestTongHop/images/animals/cat.png
///   /sdcard/Android/data/&lt;package&gt;/files/TestTongHop/audio/cat_sound.mp3
///
///   ─── ADB push ────────────────────────────────────────────────────────────────
///   adb push cat.png \
///     "/sdcard/Android/data/&lt;package&gt;/files/TestTongHop/images/animals/cat.png"
///
///   ─── Key quy ước ─────────────────────────────────────────────────────────────
///   File: persistentDataPath/TestTongHop/images/animals/cat.png
///   Key : TestTongHop/images/animals/cat          ← giống đường dẫn trong CSV
///
///   ─── Format file hỗ trợ ──────────────────────────────────────────────────────
///   Image: .png  .jpg
///   Audio: .mp3  .wav  .ogg
///
///   ─── Thêm ảnh / audio mới (chưa có trong APK) ────────────────────────────────
///   1. Thêm vào CSV (questionMediaValue / answerMediaValue)
///   2. Push file lên persistentDataPath
///   3. Chạy lại game — KHÔNG cần rebuild APK
/// </summary>
public static class AssetOverrideLoader
{
    // ── Caches ────────────────────────────────────────────────────────────────

    static readonly Dictionary<string, Sprite>    _sprites = new();
    static readonly Dictionary<string, AudioClip> _clips   = new();

    static readonly string[] _imageExts = { ".png", ".jpg" };
    static readonly string[] _audioExts = { ".mp3", ".wav", ".ogg" };

    /// <summary>Thư mục gốc chứa asset override trên thiết bị.</summary>
    public static string OverrideRoot
        => Path.Combine(Application.persistentDataPath, "TestTongHop");

    // ── Preload (gọi từ StartMainController) ──────────────────────────────────

    /// <summary>
    /// Preload toàn bộ audio trong OverrideRoot (async).
    /// Sprites load on-demand (sync nên không cần preload).
    /// </summary>
    public static IEnumerator PreloadAudio()
    {
        if (!Directory.Exists(OverrideRoot))
        {
            Debug.Log("[AssetOverrideLoader] No override folder found — using Resources only.");
            yield break;
        }

        var audioFiles = Directory.GetFiles(OverrideRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => _audioExts.Any(ext =>
                f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        int count = 0;
        foreach (var file in audioFiles)
        {
            string key = FileToKey(file);
            if (_clips.ContainsKey(key)) continue;

            yield return LoadAudioFromFile(file, key);
            count++;
        }

        if (count > 0)
            Debug.Log($"[AssetOverrideLoader] Preloaded {count} audio override(s).");
    }

    // ── Public getters (drop-in thay Resources.Load) ──────────────────────────

    /// <summary>
    /// Lấy Sprite: ưu tiên file trong persistentDataPath, fallback về Resources.
    /// Sync — dùng thay Resources.Load&lt;Sprite&gt;(path) ở bất kỳ đâu.
    /// Tự động prepend imageRoot nếu path chưa bắt đầu bằng root đó.
    /// </summary>
    public static Sprite GetSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        resourcePath = ResolveImage(resourcePath);

        if (_sprites.TryGetValue(resourcePath, out var cached))
            return cached;

        // Thử file override trước
        foreach (var ext in _imageExts)
        {
            string filePath = Path.Combine(Application.persistentDataPath,
                                           resourcePath.Replace('/', Path.DirectorySeparatorChar) + ext);
            if (!File.Exists(filePath)) continue;

            var sprite = LoadSpriteFromFile(filePath);
            if (sprite != null)
            {
                _sprites[resourcePath] = sprite;
                Debug.Log($"[AssetOverrideLoader] Sprite override: {resourcePath}");
                return sprite;
            }
        }

        // Fallback: Resources
        var res = Resources.Load<Sprite>(resourcePath);
        if (res != null) _sprites[resourcePath] = res;
        else Debug.LogWarning($"[AssetOverrideLoader] Sprite not found: {resourcePath}");
        return res;
    }

    /// <summary>
    /// Lấy AudioClip: ưu tiên clip đã preload, fallback về Resources.
    /// Tự động prepend audioRoot nếu path chưa bắt đầu bằng root đó.
    /// </summary>
    public static AudioClip GetClip(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        resourcePath = ResolveAudio(resourcePath);

        if (_clips.TryGetValue(resourcePath, out var cached))
            return cached;

        // Fallback: Resources
        var res = Resources.Load<AudioClip>(resourcePath);
        if (res == null)
            Debug.LogWarning($"[AssetOverrideLoader] AudioClip not found: {resourcePath}");
        return res;
    }

    /// <summary>Xoá cache — gọi khi muốn reload lại asset từ file.</summary>
    public static void ClearCache()
    {
        _sprites.Clear();
        _clips.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    static Sprite LoadSpriteFromFile(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex,
                                 new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f),
                                 pixelsPerUnit: 100f);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetOverrideLoader] Sprite load error ({filePath}): {e.Message}");
            return null;
        }
    }

    static IEnumerator LoadAudioFromFile(string filePath, string key)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        AudioType audioType = ext switch
        {
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            _      => AudioType.WAV
        };

        // "file://" prefix hoạt động trên tất cả platform kể cả Android (persistentDataPath)
        string url = "file://" + filePath.Replace('\\', '/');

        using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip != null)
            {
                _clips[key] = clip;
                Debug.Log($"[AssetOverrideLoader] Audio override: {key}");
            }
        }
        else
        {
            Debug.LogWarning($"[AssetOverrideLoader] Audio load error ({filePath}): {req.error}");
        }
    }

    // ── Path resolution ───────────────────────────────────────────────────────

    /// <summary>
    /// Nếu path ngắn (chưa có root prefix) → prepend imageRoot.
    /// Backward-compat: path đầy đủ vẫn hoạt động bình thường.
    ///   "animals/cat"                    → "TestTongHop/images/animals/cat"
    ///   "TestTongHop/images/animals/cat" → "TestTongHop/images/animals/cat" (unchanged)
    /// </summary>
    static string ResolveImage(string path)
        => Resolve(path, TongHopConfig.Current.imageRoot);

    /// <summary>
    /// Nếu path ngắn → prepend audioRoot.
    ///   "cat_sound"                   → "TestTongHop/audio/cat_sound"
    ///   "TestTongHop/audio/cat_sound" → unchanged
    /// </summary>
    static string ResolveAudio(string path)
        => Resolve(path, TongHopConfig.Current.audioRoot);

    /// <summary>
    /// Core helper: prepend root nếu path chưa bắt đầu bằng root.
    /// Empty root → path dùng nguyên (off switch).
    /// </summary>
    static string Resolve(string path, string root)
    {
        if (string.IsNullOrEmpty(root)) return path;
        // Đã có đầy đủ prefix → dùng nguyên (backward-compat)
        if (path.StartsWith(root + "/") || path == root) return path;
        return root + "/" + path;
    }

    /// <summary>
    /// Chuyển đường dẫn file tuyệt đối → key khớp với CSV path.
    /// Ví dụ: persistentDataPath/TestTongHop/audio/cat_sound.mp3 → TestTongHop/audio/cat_sound
    /// </summary>
    static string FileToKey(string absolutePath)
    {
        string relative = absolutePath
            .Replace(Application.persistentDataPath, "")
            .Replace('\\', '/')
            .TrimStart('/');

        return Path.ChangeExtension(relative, null);
    }
}
