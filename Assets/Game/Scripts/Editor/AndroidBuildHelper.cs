using UnityEditor;
using UnityEngine;

public static class AndroidBuildHelper
{
    [MenuItem("Tools/Build/Android APK")]
    public static void BuildAndroidAPK()
    {
        // Set keystore passwords
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = "Build/edugame.keystore";
        PlayerSettings.Android.keystorePass = "android";
        PlayerSettings.Android.keyaliasName = "androiddebugkey";
        PlayerSettings.Android.keyaliasPass = "android";

        // Ensure target SDK is set
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // Get all enabled scenes
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }

        string outputPath = "Builds/Android/EduGame.apk";

        // Create output directory
        string dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };

        Debug.Log("AndroidBuildHelper: Starting Android APK build...");
        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("AndroidBuildHelper: BUILD SUCCEEDED — " + outputPath + " (" + (report.summary.totalSize / 1024 / 1024) + " MB)");
        }
        else
        {
            Debug.LogError("AndroidBuildHelper: BUILD FAILED — " + report.summary.totalErrors + " errors");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        Debug.LogError("  " + msg.content);
                }
            }
        }
    }
}
