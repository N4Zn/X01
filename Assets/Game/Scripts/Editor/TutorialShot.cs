using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Chụp màn chơi (đang Play) để làm tutorial trong tutorial_builder: bấm F12 hoặc menu Tools/Tutorial.
/// Ảnh lưu ở thư mục TutorialShots/ cạnh Assets (không bị import vào game), tên theo scene + số thứ tự.</summary>
public static class TutorialShot
{
    [MenuItem("Tools/Tutorial/Chụp màn để làm tutorial _F12")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[TutorialShot] Bấm Play trước rồi mới chụp."); return; }
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TutorialShots");
        Directory.CreateDirectory(folder);
        string scene = SceneManager.GetActiveScene().name;
        int n = 1;
        string path;
        do { path = Path.Combine(folder, $"{scene}_{n:00}.png"); n++; } while (File.Exists(path));
        ScreenCapture.CaptureScreenshot(path, 1);
        Debug.Log("[TutorialShot] Đã chụp: " + path);
    }

    [MenuItem("Tools/Tutorial/Mở thư mục ảnh đã chụp")]
    public static void OpenFolder()
    {
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TutorialShots");
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder + Path.DirectorySeparatorChar);
    }
}
