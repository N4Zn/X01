using UnityEngine;

/// <summary>
/// Forces Android immersive sticky fullscreen mode — hides navigation bar
/// and status bar. Re-applies on every focus change so the bars stay hidden
/// even after the user swipes to reveal them temporarily.
/// Attach to a DontDestroyOnLoad object or let it auto-create via RuntimeInitializeOnLoadMethod.
/// </summary>
public class AndroidFullscreen : MonoBehaviour
{
    private static AndroidFullscreen _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        if (_instance != null) return;

        GameObject go = new GameObject("AndroidFullscreen");
        _instance = go.AddComponent<AndroidFullscreen>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        ApplyImmersiveMode();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyImmersiveMode();
        }
    }

    /// <summary>
    /// Set Android system UI to immersive sticky mode.
    /// Navigation bar and status bar are hidden; swiping from edge
    /// shows them temporarily as translucent overlays, then they auto-hide.
    /// </summary>
    private static void ApplyImmersiveMode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    var window = activity.Call<AndroidJavaObject>("getWindow");
                    var decorView = window.Call<AndroidJavaObject>("getDecorView");

                    // SYSTEM_UI_FLAG_IMMERSIVE_STICKY  = 0x00001000
                    // SYSTEM_UI_FLAG_FULLSCREEN         = 0x00000004
                    // SYSTEM_UI_FLAG_HIDE_NAVIGATION    = 0x00000002
                    // SYSTEM_UI_FLAG_LAYOUT_STABLE      = 0x00000100
                    // SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION = 0x00000200
                    // SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN  = 0x00000400
                    int flags = 0x00001000 | 0x00000004 | 0x00000002
                              | 0x00000100 | 0x00000200 | 0x00000400;

                    decorView.Call("setSystemUiVisibility", flags);
                }));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AndroidFullscreen] Failed to set immersive mode: " + e.Message);
        }
#else
        // Ensure fullscreen on other platforms too
        Screen.fullScreen = true;
#endif
    }
}
