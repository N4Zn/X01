using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

/// <summary>
/// In-app video player using LibVLC (libvlc-all-3.6.3.aar in Assets/Plugins/Android/).
/// LibVLC uses its own H.264 decoder, bypassing Android's broken mediaswcodec APEX.
/// </summary>
public static class LibVLCVideoPlayer
{
    static MonoBehaviour _runner;

    public static void Play(string videoName, System.Action onClose = null)
    {
        if (_runner == null)
        {
            var go = new GameObject("[LibVLCVideoPlayer]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CoroutineRunner>();
        }
        _runner.StartCoroutine(CoPrepareAndPlay(videoName, onClose));
    }

    // ── Coroutine: copy file then hand off to Android ─────────────────────────

    static IEnumerator CoPrepareAndPlay(string videoName, System.Action onClose)
    {
        string filename = videoName + ".mp4";
        string destDir  = System.IO.Path.Combine(Application.persistentDataPath, "Video");
        string destPath = System.IO.Path.Combine(destDir, filename);

        if (!System.IO.File.Exists(destPath))
        {
            System.IO.Directory.CreateDirectory(destDir);
            string srcUrl = Application.streamingAssetsPath + "/Video/" + filename;
            using var req = UnityWebRequest.Get(srcUrl);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[LibVLC] Copy failed: " + req.error);
                onClose?.Invoke();
                yield break;
            }
            System.IO.File.WriteAllBytes(destPath, req.downloadHandler.data);
            Debug.Log("[LibVLC] Copied: " + destPath);
        }

        // Dismiss Unity loading modal NOW — LibVLC overlay will cover the game.
        // If LibVLC fails below the user still gets the game back.
        onClose?.Invoke();
        yield return null; // one frame so modal closes before overlay appears

#if UNITY_ANDROID && !UNITY_EDITOR
        ShowOverlay(destPath);
#else
        Debug.Log("[LibVLC] Editor — would play: " + destPath);
#endif
    }

    // ── Android overlay ────────────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR

    static void ShowOverlay(string filePath)
    {
        using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = up.GetStatic<AndroidJavaObject>("currentActivity");

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            try   { BuildAndPlay(activity, filePath); }
            catch (System.Exception e)
            {
                Debug.LogError("[LibVLC] BuildAndPlay failed: " + e.Message + "\n" + e.StackTrace);
            }
        }));
    }

    static void BuildAndPlay(AndroidJavaObject activity, string filePath)
    {
        // ── Root container ────────────────────────────────────────────────────
        using var window = activity.Call<AndroidJavaObject>("getWindow");
        using var decor  = window.Call<AndroidJavaObject>("getDecorView");
        var rootFrame = decor.Call<AndroidJavaObject>("findViewById", 0x01020002);

        var wrapper = new AndroidJavaObject("android.widget.FrameLayout", activity);
        wrapper.Call("setBackgroundColor", unchecked((int)0xFF000000));
        using var matchParent = new AndroidJavaObject("android.view.ViewGroup$LayoutParams", -1, -1);
        rootFrame.Call("addView", wrapper, matchParent);

        // ── Status label (shown until video starts) ───────────────────────────
        var statusLabel = new AndroidJavaObject("android.widget.TextView", activity);
        statusLabel.Call("setText", "Đang khởi động LibVLC...");
        statusLabel.Call("setTextColor",   unchecked((int)0xFFFFFFFF));
        statusLabel.Call("setTextSize",    2 /* SP */, 18f);
        statusLabel.Call("setGravity",     17); // CENTER
        using var labelLp = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", -1, -1);
        wrapper.Call("addView", statusLabel, labelLp);

        // ── SurfaceView ───────────────────────────────────────────────────────
        var surfaceView = new AndroidJavaObject("android.view.SurfaceView", activity);
        using var svLp = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", -1, -1);
        svLp.Set("gravity", 17);
        wrapper.Call("addView", surfaceView, svLp);

        // ── Close button ──────────────────────────────────────────────────────
        float density   = GetDensity(activity);
        int   btnSizePx = (int)(64 * density);
        int   marginPx  = (int)(12 * density);

        var closeBtn = new AndroidJavaObject("android.widget.Button", activity);
        closeBtn.Call("setText", "✕");
        closeBtn.Call("setTextSize",        2, 20f);
        closeBtn.Call("setTextColor",       unchecked((int)0xFFFFFFFF));
        closeBtn.Call("setBackgroundColor", unchecked((int)0xCCD32027));
        using var closeLp = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams",
                                                   btnSizePx, btnSizePx);
        closeLp.Set("gravity",    unchecked((int)0x00800005));
        closeLp.Set("topMargin",  marginPx);
        closeLp.Set("rightMargin",marginPx);
        wrapper.Call("addView", closeBtn, closeLp);

        // ── LibVLC init ───────────────────────────────────────────────────────
        Debug.Log("[LibVLC] Creating LibVLC instance...");
        // Use single-arg constructor — safest across 3.x versions
        var libVLC      = new AndroidJavaObject("org.videolan.libvlc.LibVLC", activity);
        var mediaPlayer = new AndroidJavaObject("org.videolan.libvlc.MediaPlayer", libVLC);
        Debug.Log("[LibVLC] LibVLC + MediaPlayer created OK");

        // ── Dismiss proxy (handles close button + playback end) ───────────────
        var dismissProxy = new DismissProxy(activity, rootFrame, wrapper,
                                            libVLC, mediaPlayer);
        mediaPlayer.Call("setEventListener", dismissProxy);
        closeBtn.Call("setOnClickListener",  dismissProxy);

        // ── SurfaceHolder callback — attach surface + start playback ──────────
        var holderProxy = new SurfaceProxy(activity, libVLC, mediaPlayer,
                                           filePath, statusLabel);
        var holder = surfaceView.Call<AndroidJavaObject>("getHolder");
        holder.Call("addCallback", holderProxy);

        Debug.Log("[LibVLC] SurfaceHolder callback registered, waiting for surface...");
    }

    static float GetDensity(AndroidJavaObject ctx)
    {
        try
        {
            using var res     = ctx.Call<AndroidJavaObject>("getResources");
            using var metrics = res.Call<AndroidJavaObject>("getDisplayMetrics");
            return metrics.Get<float>("density");
        }
        catch { return 2f; }
    }

    // ── SurfaceHolder.Callback — attach LibVLC surface and start playback ─────

    class SurfaceProxy : AndroidJavaProxy
    {
        readonly AndroidJavaObject _activity;
        readonly AndroidJavaObject _libVLC;
        readonly AndroidJavaObject _mediaPlayer;
        readonly AndroidJavaObject _statusLabel;
        readonly string            _filePath;
        bool _started;

        public SurfaceProxy(AndroidJavaObject activity, AndroidJavaObject libVLC,
                             AndroidJavaObject mediaPlayer, string filePath,
                             AndroidJavaObject statusLabel)
            : base("android.view.SurfaceHolder$Callback")
        {
            _activity    = activity;
            _libVLC      = libVLC;
            _mediaPlayer = mediaPlayer;
            _filePath    = filePath;
            _statusLabel = statusLabel;
        }

        public void surfaceCreated(AndroidJavaObject holder)
        {
            if (_started) return;
            _started = true;
            Debug.Log("[LibVLC] surfaceCreated — attaching VLC vout...");

            try
            {
                _statusLabel?.Call("setVisibility", 8); // GONE

                var surface = holder.Call<AndroidJavaObject>("getSurface");
                var vout    = _mediaPlayer.Call<AndroidJavaObject>("getVLCVout");
                vout.Call("setVideoSurface", surface, holder);
                vout.Call("attachViews");

                string uriStr = "file://" + _filePath.Replace('\\', '/');
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                var uri   = uriClass.CallStatic<AndroidJavaObject>("parse", uriStr);
                var media = new AndroidJavaObject("org.videolan.libvlc.Media", _libVLC, uri);
                _mediaPlayer.Call("setMedia", media);
                media.Call("release");
                _mediaPlayer.Call("play");
                Debug.Log("[LibVLC] play() called for: " + _filePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LibVLC] surfaceCreated error: " + e.Message);
            }
        }

        public void surfaceChanged(AndroidJavaObject holder, int format, int w, int h) { }

        public void surfaceDestroyed(AndroidJavaObject holder)
        {
            try
            {
                _mediaPlayer.Call<AndroidJavaObject>("getVLCVout").Call("detachViews");
            }
            catch { }
        }
    }

    // ── MediaPlayer.EventListener + View.OnClickListener ─────────────────────

    class DismissProxy : AndroidJavaProxy
    {
        readonly AndroidJavaObject _activity;
        readonly AndroidJavaObject _rootFrame;
        readonly AndroidJavaObject _wrapper;
        readonly AndroidJavaObject _libVLC;
        readonly AndroidJavaObject _mediaPlayer;
        bool _done;

        const int EndReached      = 0x100;
        const int Stopped         = 0x104;
        const int EncounteredError= 0x200;

        public DismissProxy(AndroidJavaObject activity, AndroidJavaObject rootFrame,
            AndroidJavaObject wrapper, AndroidJavaObject libVLC, AndroidJavaObject mediaPlayer)
            : base("org.videolan.libvlc.MediaPlayer$EventListener")
        {
            _activity    = activity;
            _rootFrame   = rootFrame;
            _wrapper     = wrapper;
            _libVLC      = libVLC;
            _mediaPlayer = mediaPlayer;
        }

        public void onEvent(AndroidJavaObject evt)
        {
            int type = evt.Get<int>("type");
            if (type == EndReached || type == Stopped || type == EncounteredError)
            {
                if (type == EncounteredError) Debug.LogError("[LibVLC] Playback error event.");
                Dismiss();
            }
        }

        public void onClick(AndroidJavaObject view) => Dismiss();

        void Dismiss()
        {
            if (_done) return;
            _done = true;
            _activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    _mediaPlayer.Call("stop");
                    _mediaPlayer.Call<AndroidJavaObject>("getVLCVout").Call("detachViews");
                    _mediaPlayer.Call("release");
                    _libVLC.Call("release");
                    _rootFrame.Call("removeView", _wrapper);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[LibVLC] Dismiss cleanup: " + e.Message);
                }
            }));
        }
    }

#endif // UNITY_ANDROID && !UNITY_EDITOR

    class CoroutineRunner : MonoBehaviour { }
}
