using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

/// <summary>
/// Copies MP4 from StreamingAssets/Video/ to external files dir, then opens VLC.
/// User presses Back in VLC to return to the game.
///
/// Flow:
///   1. Copy video to getExternalFilesDir("Video") — accessible to VLC on Android 10.
///   2. Call onClose immediately (hides Unity overlay before VLC opens).
///   3. Launch VLC via Intent with file:// URI.
/// </summary>
public static class NativeVideoPlayer
{
    static MonoBehaviour _runner;

    public static void Play(string videoName, System.Action onClose = null)
    {
        Debug.Log($"[NativeVideoPlayer] Play called — videoName={videoName}");
        if (_runner == null)
        {
            var go = new GameObject("[NativeVideoPlayer]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CoroutineRunner>();
        }
        _runner.StartCoroutine(CoPrepareAndLaunch(videoName, onClose));
    }

    static IEnumerator CoPrepareAndLaunch(string videoName, System.Action onClose)
    {
        string filename = videoName + ".mp4";
        string destPath = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        // getExternalFilesDir("Video") → /sdcard/Android/data/<pkg>/files/Video/
        // VLC can read this path on Android 10 (API 29) without extra permissions.
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            using var extDir = activity.Call<AndroidJavaObject>("getExternalFilesDir", "Video");
            if (extDir == null)
            {
                Debug.LogError("[NativeVideoPlayer] getExternalFilesDir returned null — external storage unavailable. Falling back to persistentDataPath.");
                string fallbackDir = System.IO.Path.Combine(Application.persistentDataPath, "Video");
                destPath = System.IO.Path.Combine(fallbackDir, filename);
            }
            else
            {
                string absPath = extDir.Call<string>("getAbsolutePath");
                Debug.Log("[NativeVideoPlayer] extDir = " + absPath);
                destPath = System.IO.Path.Combine(absPath, filename);
            }
        }
#else
        destPath = System.IO.Path.Combine(Application.persistentDataPath, "Video", filename);
#endif

        if (!System.IO.File.Exists(destPath))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destPath));
            string srcUrl = Application.streamingAssetsPath + "/Video/" + filename;
            using var req = UnityWebRequest.Get(srcUrl);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[NativeVideoPlayer] Copy failed: " + req.error);
                onClose?.Invoke();
                yield break;
            }
            System.IO.File.WriteAllBytes(destPath, req.downloadHandler.data);
            Debug.Log("[NativeVideoPlayer] Copied to: " + destPath);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Hide Unity modal BEFORE opening VLC (no callback needed after return).
        onClose?.Invoke();
        yield return null; // let modal close render for 1 frame
        LaunchVLC(destPath);
#else
        Debug.Log("[NativeVideoPlayer] Editor: would open VLC for " + destPath);
        onClose?.Invoke();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static void LaunchVLC(string filePath)
    {
        try
        {
            using var playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = playerClass.GetStatic<AndroidJavaObject>("currentActivity");

            // Use FileProvider to generate a content:// URI.
            // This is the correct Android 7+ approach — no FileUriExposedException,
            // no StrictMode hacks, and VLC gets explicit read permission via the intent flag.
            using var file             = new AndroidJavaObject("java.io.File", filePath);
            using var fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider");
            string    authority        = Application.identifier + ".fileprovider";
            using var contentUri       = fileProviderClass.CallStatic<AndroidJavaObject>(
                                             "getUriForFile", activity, authority, file);

            Debug.Log("[NativeVideoPlayer] content URI = " + contentUri.Call<string>("toString"));

            using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");
            intent.Call<AndroidJavaObject>("setDataAndType", contentUri, "video/mp4");
            intent.Call<AndroidJavaObject>("setPackage", "org.videolan.vlc");
            // FLAG_ACTIVITY_NEW_TASK | FLAG_GRANT_READ_URI_PERMISSION
            intent.Call<AndroidJavaObject>("addFlags", 0x10000001);
            // Force software decoding — hardware codec (MediaCodec) crashes on some MTK devices.
            intent.Call<AndroidJavaObject>("putExtra", "extra_vlc_options", new string[] { "--avcodec-hw=none" });

            activity.Call("startActivity", intent);
            Debug.Log("[NativeVideoPlayer] Launched VLC for: " + filePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[NativeVideoPlayer] Failed to launch VLC: " + e.Message);
        }
    }
#endif

    class CoroutineRunner : MonoBehaviour { }
}

/// <summary>Runs actions on Unity's main thread. Required by LibVLC integration (future).</summary>
public static class UnityMainThreadDispatcher
{
    static readonly System.Collections.Generic.Queue<System.Action> _queue =
        new System.Collections.Generic.Queue<System.Action>();
    static DispatcherBehaviour _instance;

    public static void Enqueue(System.Action action)
    {
        lock (_queue) _queue.Enqueue(action);
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("[UnityMainThreadDispatcher]");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<DispatcherBehaviour>();
    }

    class DispatcherBehaviour : MonoBehaviour
    {
        void Awake() { _instance = this; }
        void Update()
        {
            while (true)
            {
                System.Action action;
                lock (_queue)
                {
                    if (_queue.Count == 0) break;
                    action = _queue.Dequeue();
                }
                try { action(); } catch (System.Exception e) { Debug.LogException(e); }
            }
        }
    }
}
