using UnityEngine;
using UnityEditor;
using System.IO;

public class ScreenshotCapture
{
    [MenuItem("Tools/Capture Game View Screenshot")]
    public static void CaptureGameView()
    {
        string folder = Path.Combine(Application.dataPath, "Screenshots");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "gameview_capture.png");

        if (EditorApplication.isPlaying)
        {
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[ScreenshotCapture] Screenshot saved to: " + path);
        }
        else
        {
            Debug.LogWarning("[ScreenshotCapture] Enter Play mode first.");
        }
    }

    [MenuItem("Tools/Capture Setting Panel Screenshot")]
    public static void CaptureSettingPanel()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[ScreenshotCapture] Enter Play mode first.");
            return;
        }

        // Find and activate the setting panel
        var settingPanel = GameObject.Find("SettingPanelRoot");
        if (settingPanel == null)
        {
            // Try finding inactive
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var t = canvas.transform.Find("SettingPanelRoot");
                if (t != null) settingPanel = t.gameObject;
            }
        }

        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            Debug.Log("[ScreenshotCapture] SettingPanelRoot activated.");
        }
        else
        {
            Debug.LogWarning("[ScreenshotCapture] SettingPanelRoot not found.");
            return;
        }

        // Capture after one frame
        EditorApplication.delayCall += () =>
        {
            string folder = Path.Combine(Application.dataPath, "Screenshots");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "setting_panel_capture.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[ScreenshotCapture] Setting panel screenshot saved to: " + path);
        };
    }
}
