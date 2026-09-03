// Assets/Editor/AndroidBuild.cs
// Usage (command line):
//   Unity.exe -batchmode -quit -projectPath "..." -buildTarget Android
//             -executeMethod AndroidBuild.BuildRelease
// Reads BUILD_APK_PATH env var for output path, falls back to Builds/X01a.apk
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuild
{
    [MenuItem("Tools/Build/Android Release")]
    public static void BuildRelease()
    {
        string outDir = Path.Combine(Application.dataPath, "..", "Builds");
        string outPath = Environment.GetEnvironmentVariable("BUILD_APK_PATH")
                         ?? Path.Combine(outDir, "X01a.apk");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        // Use all enabled scenes from Build Settings
        var enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) enabledScenes.Add(s.path);

        Debug.Log($"[AndroidBuild] Building {enabledScenes.Count} scenes → {outPath}");

        var opts = new BuildPlayerOptions
        {
            scenes           = enabledScenes.ToArray(),
            locationPathName = outPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.None,
        };

        var report  = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AndroidBuild] SUCCESS → {outPath} ({summary.totalSize / 1024 / 1024} MB)");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[AndroidBuild] FAILED: {summary.result} ({summary.totalErrors} errors)");
            EditorApplication.Exit(1);
        }
    }
}
#endif
