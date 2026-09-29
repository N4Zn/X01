using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ cấu hình Build Settings cho EduXplore 2.0.
///
/// Chạy: Tools → EduXplore → Configure Build Settings
///
/// Quy tắc:
///   - StartScene luôn ở đầu (index 0) — đây là scene khởi động app.
///   - Các scene game xếp sau theo thứ tự trong REQUIRED_SCENES.
///   - Scene nào đã tồn tại trong Build Settings sẽ được cập nhật path (không thêm trùng).
///   - Scene cũ không có trong danh sách sẽ bị vô hiệu hoá (enabled=false), KHÔNG xoá.
/// </summary>
public static class BuildSettingsConfigurator
{
    // ── Danh sách scene cần có, theo thứ tự ──────────────────────────────────
    // [0] StartScene: bắt buộc phải là scene đầu tiên (App start).
    static readonly (string sceneName, string path)[] REQUIRED_SCENES =
    {
        // ── Core flow ──────────────────────────────────────────────────────
        ("StartScene",         "Assets/Game/Scenes/StartScreen/StartScene.unity"),
        ("MenuScene",          "Assets/Game/Scenes/MenuScene/MenuScene.unity"),
        ("ScoreScene",         "Assets/Game/Scenes/ScoreScene/ScoreScene.unity"),

        // ── TongHop engine (CSV-based — tất cả dùng chung 1 scene) ───────
        ("TestTongHopGame",    "Assets/Game/Scenes/TestTongHopGame/TestTongHopGame.unity"),

        // ── Math engine ───────────────────────────────────────────────────
        ("AddUpGame",          "Assets/Game/Scenes/AddUpGame/AddUpGame.unity"),
        ("NumberAddUpGame",    "Assets/Game/Scenes/NumberAddUpGame/NumberAddUpGame.unity"),
        ("AddNumberGame",      "Assets/Game/Scenes/AddNumberGame/AddNumberGame.unity"),

        // ── Other engines ─────────────────────────────────────────────────
        ("TrainPathGame",      "Assets/Game/Scenes/TrainPathGame/TrainPathGame.unity"),
        ("PathFinderGame",     "Assets/Game/Scenes/PathFinderGame/PathFinderGame.unity"),
        ("PlanetOrderGame",    "Assets/Game/Scenes/PlanetOrderGame/PlanetOrderGame.unity"),
        ("PlanetAlphabetGame", "Assets/Game/Scenes/PlanetAlphabetGame/PlanetAlphabetGame.unity"),
        ("ListenGame",         "Assets/Game/Scenes/ListenGame/ListenGame.unity"),

        // ── Giữ lại (không dùng trong flow chính, nhưng giữ để không mất) ─
        ("TeamSelectScene",    "Assets/Game/Scenes/TeamSelectScene/TeamSelectScene.unity"),
        ("HomeScene",          "Assets/Game/Scenes/HomeScene/HomeScene.unity"),
        ("CharacterSelectScene","Assets/Game/Scenes/CharacterSelectScene/CharacterSelectScene.unity"),
    };

    [MenuItem("Tools/EduXplore/Configure Build Settings")]
    public static void ConfigureBuildSettings()
    {
        var result = new List<EditorBuildSettingsScene>();
        var log    = new System.Text.StringBuilder();
        log.AppendLine("[BuildSettingsConfigurator] Kết quả:");

        int added   = 0;
        int missing = 0;

        foreach (var (sceneName, path) in REQUIRED_SCENES)
        {
            bool fileExists = System.IO.File.Exists(path);
            if (!fileExists)
            {
                log.AppendLine($"  ⚠ MISSING  {sceneName} — file không tồn tại: {path}");
                missing++;
                continue;
            }

            // Kích hoạt StartScene và các scene game chính; vô hiệu hoá scene cũ
            bool enabled = sceneName != "HomeScene"
                        && sceneName != "CharacterSelectScene"
                        && sceneName != "TeamSelectScene";

            result.Add(new EditorBuildSettingsScene(path, enabled));

            string status = enabled ? "✓ enabled " : "  disabled";
            log.AppendLine($"  {status}  [{result.Count - 1}] {sceneName}");
            added++;
        }

        EditorBuildSettings.scenes = result.ToArray();
        AssetDatabase.SaveAssets();

        log.AppendLine($"\nTổng: {added} scene đã cấu hình, {missing} file không tìm thấy.");
        Debug.Log(log.ToString());

        EditorUtility.DisplayDialog(
            "Build Settings Configured",
            $"Đã cấu hình {added} scene.\n" +
            (missing > 0 ? $"⚠ {missing} scene không tìm thấy file (xem Console)." : "Tất cả scene đã sẵn sàng."),
            "OK"
        );
    }

    /// <summary>
    /// Hiển thị danh sách Build Settings hiện tại trong Console (không thay đổi gì).
    /// </summary>
    [MenuItem("Tools/EduXplore/Show Current Build Settings")]
    public static void ShowCurrentBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[BuildSettings] Hiện tại có {scenes.Length} scene:");
        for (int i = 0; i < scenes.Length; i++)
        {
            string status = scenes[i].enabled ? "ON " : "OFF";
            string name   = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
            sb.AppendLine($"  [{i}] {status}  {name}  ({scenes[i].path})");
        }
        Debug.Log(sb.ToString());
    }
}
