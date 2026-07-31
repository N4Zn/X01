using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tạo sprite + audio placeholder và cấu trúc thư mục mẫu cho TestTongHop.
/// Menu: Tools → TestTongHop → Generate Sample Assets
///
/// Sprite: hình cầu bóng màu khác nhau cho mỗi con vật / trái cây.
/// Audio : tone sóng sin tổng hợp (placeholder) — thay bằng file thu âm thật sau.
/// </summary>
public class SampleAssetGenerator
{
    const int SampleRate = 22050;

    // ── Sprite list ───────────────────────────────────────────────────────────

    static readonly (string path, Color color)[] SpriteList =
    {
        ("TestTongHop/images/animals/cat",   new Color(1.00f, 0.55f, 0.15f)),
        ("TestTongHop/images/animals/dog",   new Color(0.55f, 0.35f, 0.15f)),
        ("TestTongHop/images/animals/fish",  new Color(0.10f, 0.75f, 0.85f)),
        ("TestTongHop/images/animals/bird",  new Color(0.25f, 0.50f, 1.00f)),
        ("TestTongHop/images/fruits/apple",  new Color(0.95f, 0.15f, 0.15f)),
        ("TestTongHop/images/fruits/banana", new Color(1.00f, 0.90f, 0.10f)),
    };

    // ── Audio list ────────────────────────────────────────────────────────────
    // (đường dẫn, loại âm thanh)
    // Thay file WAV thật vào cùng đường dẫn là xong — code không cần sửa.

    static readonly (string path, AudioShape shape)[] AudioList =
    {
        // Tiếng mèo: cao, ngắn, sweeping xuống
        ("TestTongHop/audio/cat_sound", AudioShape.CatMeow),
        // Tiếng chó: thấp, ngắn, 2 nốt burst
        ("TestTongHop/audio/dog_sound", AudioShape.DogBark),
        // Tiếng chim: rất cao, chirp nhanh
        ("TestTongHop/audio/bird_sound", AudioShape.BirdChirp),
    };

    enum AudioShape { CatMeow, DogBark, BirdChirp }

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/TestTongHop/Generate Sample Assets")]
    static void Generate()
    {
        string resourcesRoot = Path.Combine(Application.dataPath, "Resources");

        // ── Sprites ───────────────────────────────────────────────────────────
        foreach (var (relPath, color) in SpriteList)
        {
            string fullPath = Path.Combine(resourcesRoot,
                relPath.Replace('/', Path.DirectorySeparatorChar) + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            WritePng(fullPath, color, size: 256);
        }

        // ── Audio ─────────────────────────────────────────────────────────────
        foreach (var (relPath, shape) in AudioList)
        {
            string fullPath = Path.Combine(resourcesRoot,
                relPath.Replace('/', Path.DirectorySeparatorChar) + ".wav");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            float[] samples = BuildSamples(shape);
            File.WriteAllBytes(fullPath, EncodeWav(samples, SampleRate));
        }

        AssetDatabase.Refresh();

        // ── Import settings ───────────────────────────────────────────────────
        foreach (var (relPath, _) in SpriteList)
            SetSpriteImport($"Assets/Resources/{relPath}.png");

        foreach (var (relPath, _) in AudioList)
            SetAudioImport($"Assets/Resources/{relPath}.wav");

        Debug.Log("[SampleAssetGenerator] Done — sprites + audio tạo xong.");
        EditorUtility.DisplayDialog("Xong!",
            "Tạo xong asset mẫu.\n\n" +
            "Sprite : Assets/Resources/TestTongHop/images/\n" +
            "Audio  : Assets/Resources/TestTongHop/audio/\n\n" +
            "Thay file thu âm thật vào cùng đường dẫn khi cần.", "OK");
    }

    // =========================================================================
    // PNG generation
    // =========================================================================

    static void WritePng(string fullPath, Color baseColor, int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f, cy = size * 0.5f, R = size * 0.42f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > R) { pixels[y * size + x] = Color.clear; continue; }

            float edgeT = Mathf.Clamp01((R - dist) / (R * 0.10f));
            float hlDx  = dx + R * 0.28f, hlDy = dy + R * 0.32f;
            float hl    = Mathf.Exp(-(hlDx * hlDx + hlDy * hlDy) / (R * R * 0.11f));
            Color c     = Color.Lerp(baseColor * 0.55f, baseColor, edgeT);
            c = Color.Lerp(c, Color.white, hl * 0.60f);
            c.a = 1f;
            pixels[y * size + x] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply(false);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // =========================================================================
    // Audio generation  —  tất cả là sóng sin tổng hợp, đủ để test hệ thống
    // =========================================================================

    static float[] BuildSamples(AudioShape shape)
    {
        return shape switch
        {
            AudioShape.CatMeow  => CatMeow(),
            AudioShape.DogBark  => DogBark(),
            AudioShape.BirdChirp => BirdChirp(),
            _ => new float[SampleRate]
        };
    }

    // Tiếng mèo: 2 nốt liền nhau, sweeping 750→500 Hz mỗi nốt
    static float[] CatMeow()
    {
        float noteDur = 0.25f;
        int   noteLen = (int)(SampleRate * noteDur);
        float[] s     = new float[noteLen * 2];

        for (int note = 0; note < 2; note++)
        for (int i = 0; i < noteLen; i++)
        {
            float t    = (float)i / SampleRate;
            float freq = Mathf.Lerp(750f, 500f, (float)i / noteLen);
            float env  = SmoothEnv(i, noteLen, attack: 0.05f, release: 0.3f);
            s[note * noteLen + i] = Mathf.Sin(2 * Mathf.PI * freq * t) * env * 0.8f;
        }
        return s;
    }

    // Tiếng chó: 2 burst ngắn ở tần số thấp 200 Hz
    static float[] DogBark()
    {
        float burstDur = 0.15f, gap = 0.08f;
        int   burstLen = (int)(SampleRate * burstDur);
        int   gapLen   = (int)(SampleRate * gap);
        int   total    = (burstLen + gapLen) * 2;
        float[] s      = new float[total];

        for (int burst = 0; burst < 2; burst++)
        {
            int offset = burst * (burstLen + gapLen);
            for (int i = 0; i < burstLen; i++)
            {
                float t   = (float)i / SampleRate;
                float env = SmoothEnv(i, burstLen, attack: 0.02f, release: 0.5f);
                // Harmonics ở 200Hz + 400Hz để nghe "bark" hơn
                s[offset + i] = (Mathf.Sin(2 * Mathf.PI * 200f * t) * 0.7f
                               + Mathf.Sin(2 * Mathf.PI * 400f * t) * 0.3f) * env * 0.85f;
            }
        }
        return s;
    }

    // Tiếng chim: 5 chirp nhanh ở 1400 Hz
    static float[] BirdChirp()
    {
        float chirpDur = 0.07f, gap = 0.04f;
        int   chirpLen = (int)(SampleRate * chirpDur);
        int   gapLen   = (int)(SampleRate * gap);
        int   total    = (chirpLen + gapLen) * 5;
        float[] s      = new float[total];

        for (int c = 0; c < 5; c++)
        {
            int offset = c * (chirpLen + gapLen);
            for (int i = 0; i < chirpLen; i++)
            {
                float t    = (float)i / SampleRate;
                float freq = Mathf.Lerp(1400f, 1800f, (float)i / chirpLen);
                float env  = SmoothEnv(i, chirpLen, attack: 0.1f, release: 0.3f);
                s[offset + i] = Mathf.Sin(2 * Mathf.PI * freq * t) * env * 0.75f;
            }
        }
        return s;
    }

    // Envelope: linear attack + linear release, flat sustain giữa
    static float SmoothEnv(int i, int length, float attack = 0.1f, float release = 0.3f)
    {
        float t       = (float)i / length;
        float attackV = Mathf.Clamp01(t / attack);
        float releaseV = Mathf.Clamp01((1f - t) / release);
        return Mathf.Min(attackV, releaseV);
    }

    // =========================================================================
    // WAV encoder (PCM 16-bit mono, không cần thư viện ngoài)
    // =========================================================================

    static byte[] EncodeWav(float[] samples, int sampleRate)
    {
        int channels = 1, bitsPerSample = 16;
        int byteRate  = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize  = samples.Length * blockAlign;

        var wav = new byte[44 + dataSize];

        // RIFF chunk
        WriteStr(wav, 0,  "RIFF");
        WriteI32(wav, 4,  36 + dataSize);
        WriteStr(wav, 8,  "WAVE");

        // fmt sub-chunk
        WriteStr(wav, 12, "fmt ");
        WriteI32(wav, 16, 16);                  // sub-chunk size
        WriteI16(wav, 20, 1);                   // PCM = 1
        WriteI16(wav, 22, (short)channels);
        WriteI32(wav, 24, sampleRate);
        WriteI32(wav, 28, byteRate);
        WriteI16(wav, 32, (short)blockAlign);
        WriteI16(wav, 34, (short)bitsPerSample);

        // data sub-chunk
        WriteStr(wav, 36, "data");
        WriteI32(wav, 40, dataSize);

        // PCM samples (float → int16 little-endian)
        int off = 44;
        foreach (float f in samples)
        {
            short v = (short)Mathf.Clamp(Mathf.RoundToInt(f * 32767f), -32768, 32767);
            wav[off++] = (byte)(v & 0xFF);
            wav[off++] = (byte)((v >> 8) & 0xFF);
        }
        return wav;
    }

    static void WriteStr(byte[] b, int o, string s) { foreach (char c in s) b[o++] = (byte)c; }
    static void WriteI32(byte[] b, int o, int  v)
    {
        b[o]=(byte)v; b[o+1]=(byte)(v>>8); b[o+2]=(byte)(v>>16); b[o+3]=(byte)(v>>24);
    }
    static void WriteI16(byte[] b, int o, short v) { b[o]=(byte)v; b[o+1]=(byte)(v>>8); }

    // =========================================================================
    // Import settings
    // =========================================================================

    static void SetSpriteImport(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;
        imp.textureType         = TextureImporterType.Sprite;
        imp.spriteImportMode    = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled       = false;
        imp.filterMode          = FilterMode.Bilinear;
        imp.maxTextureSize      = 512;
        imp.SaveAndReimport();
    }

    static void SetAudioImport(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (imp == null) return;
        var settings                = imp.defaultSampleSettings;
        settings.loadType           = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat  = AudioCompressionFormat.PCM;
        imp.defaultSampleSettings   = settings;
        imp.forceToMono             = true;
        imp.SaveAndReimport();
    }
}
