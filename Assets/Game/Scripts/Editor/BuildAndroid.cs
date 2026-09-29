using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class BuildAndroid
{
    [MenuItem("Tools/Build/Build Android APK Now")]
    public static void BuildAPK()
    {
        // Set keystore passwords
        PlayerSettings.Android.keystorePass = "edugame123";
        PlayerSettings.Android.keyaliasPass = "edugame123";

        // Get all enabled scenes from build settings
        List<string> scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("BuildAndroid: No scenes in Build Settings!");
            return;
        }

        string outputPath = "Builds/Android/EduGame.apk";

        // Ensure output directory exists
        string dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        Debug.Log("BuildAndroid: Starting build with " + scenes.Count + " scenes...");

        BuildPlayerOptions buildOptions = new BuildPlayerOptions();
        buildOptions.scenes = scenes.ToArray();
        buildOptions.locationPathName = outputPath;
        buildOptions.target = BuildTarget.Android;
        buildOptions.options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(buildOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("BuildAndroid: SUCCESS! APK at " + outputPath + " (" + (report.summary.totalSize / 1024 / 1024) + " MB)");
        }
        else
        {
            Debug.LogError("BuildAndroid: FAILED! " + report.summary.totalErrors + " errors");
        }
    }
}
