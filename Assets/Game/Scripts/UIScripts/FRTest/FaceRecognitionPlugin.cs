using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton wrapper around the UnityFaceBridge Android plugin (AAR).
/// Handles camera texture updates and recognition polling.
///
/// DontDestroyOnLoad (via Singleton&lt;T&gt;) so any mini-game scene can use it without
/// re-initializing the camera — it survives scene loads, auto-creating itself on first
/// access if no game has touched it yet (e.g. going straight into a mini-game without ever
/// visiting FRTest).
///
/// Usage:
///   FaceRecognitionPlugin.Instance.CameraTexture  — assign to RawImage.texture (FRTest only)
///   FaceRecognitionPlugin.Instance.StartRound()   — clear votes, enable recognition
///   FaceRecognitionPlugin.Instance.StopRound()    — disable recognition (save CPU)
///   FaceRecognitionPlugin.Instance.GetConfirmed() — (leftName, rightName), null = not confirmed
/// </summary>
public class FaceRecognitionPlugin : Singleton<FaceRecognitionPlugin>
{
    [Tooltip("Target refresh rate of the camera preview texture (fps)")]
    [SerializeField] float previewFps = 15f;

    public Texture2D CameraTexture { get; private set; }

    /// <summary>
    /// True once UnityFaceBridge.initialize() has actually run. Start() is NOT guaranteed to
    /// have executed yet on the same frame Singleton&lt;T&gt; first creates this object — a caller
    /// touching .Instance for the very first time (e.g. a mini-game's "Start in 3,2,1" firing
    /// before FRTest was ever visited) could otherwise call StartRound()/ClearRoundVotes()/etc.
    /// while still false, which silently no-ops and permanently loses that round's recognition
    /// (the native slot-enabled flag never gets set, and nothing retries it).
    /// </summary>
    public bool IsInitialized => _initialized;

    bool _initialized;
    float _nextFrameTime;
    byte[] _rgbaBuffer;
    bool _firstFrameLogged;
    int _nullFrameCount;
    // Mirrors SetBoundingBoxEnabled — only FRTest sets this true. Skips the JNI pull + texture
    // upload entirely for headless games rather than calling it every frame just to get null back.
    bool _previewEnabled;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaClass _bridge;
#endif

    protected override void OnCreated()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // A scene-authored instance (e.g. FRTestSceneBuilder) doesn't go through Singleton<T>'s
        // own duplicate check, so if a DontDestroyOnLoad instance from an earlier scene visit is
        // already alive, self-destruct instead of calling UnityFaceBridge.initialize() a second
        // time on top of an already-running session (that's what caused the zombie detectionExecutor
        // threads found earlier — each stray initialize() leaks another one).
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Covers both creation paths: Singleton<T>'s own auto-create already calls this via
        // OnCreated(), but a scene-placed instance never goes through that hook, so it must
        // also be marked here or it dies on the next scene load.
        DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            _bridge = new AndroidJavaClass("com.eduxplore.faceplugin.UnityFaceBridge");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _bridge.CallStatic("initialize", activity);
            _initialized = true;
            Debug.Log("[FacePlugin] initialized");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FacePlugin] init failed: {e.Message}");
        }
#else
        Debug.Log("[FacePlugin] Editor mode — plugin disabled");
#endif
    }

    void Update()
    {
        if (!_initialized || !_previewEnabled) return;
        if (Time.time < _nextFrameTime) return;
        _nextFrameTime = Time.time + 1f / previewFps;
        UpdateCameraTexture();
    }

    void UpdateCameraTexture()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            sbyte[] rgbaSigned = _bridge.CallStatic<sbyte[]>("getLatestFrameRgba");
            if (rgbaSigned == null)
            {
                _nullFrameCount++;
                // Log every 5 seconds (previewFps=15 → every 75 calls)
                if (_nullFrameCount == 1 || _nullFrameCount % 75 == 0)
                {
                    int cnt = _bridge.CallStatic<int>("getFrameRgbaCount");
                    Debug.LogError($"[FacePlugin] null frame #{_nullFrameCount} (Kotlin rgbaCount={cnt})");
                }
                return;
            }

            int w = _bridge.CallStatic<int>("getFrameWidth");
            int h = _bridge.CallStatic<int>("getFrameHeight");
            if (w == 0 || h == 0) { Debug.LogError($"[FacePlugin] bad size {w}x{h}"); return; }

            if (!_firstFrameLogged)
            {
                _firstFrameLogged = true;
                Debug.LogError($"[FacePlugin] FIRST FRAME {w}x{h} bytes={rgbaSigned.Length}");
            }

            if (CameraTexture == null || CameraTexture.width != w || CameraTexture.height != h)
            {
                if (CameraTexture != null) Destroy(CameraTexture);
                CameraTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _rgbaBuffer = null;
                Debug.LogError($"[FacePlugin] texture created {w}x{h}");
            }
            if (_rgbaBuffer == null || _rgbaBuffer.Length != rgbaSigned.Length)
                _rgbaBuffer = new byte[rgbaSigned.Length];
            // sbyte[] and byte[] have identical memory layout — BlockCopy reinterprets bits directly.
            System.Buffer.BlockCopy(rgbaSigned, 0, _rgbaBuffer, 0, rgbaSigned.Length);
            CameraTexture.LoadRawTextureData(_rgbaBuffer);
            CameraTexture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FacePlugin] frame update error: {e.GetType().Name}: {e.Message}");
        }
#endif
    }

    /// <summary>Clear vote history, reload frtest_config.json, and enable recognition — call at
    /// start of countdown. FRTest-only: reloading config is a blocking disk read, fine for
    /// FRTest's own ~8s round cadence but too slow to run on every slot's start in other games
    /// (see PlayerRecognitionService, which uses ClearRoundVotes()/ClearSlotVote() instead —
    /// those never touch disk).</summary>
    public void StartRound()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("clearRoundVotes");
        _bridge.CallStatic("reloadConfig");
        _bridge.CallStatic("setRecognitionEnabled", true);
#endif
    }

    /// <summary>Clears both slots' lock history without touching either slot's enabled state.</summary>
    public void ClearRoundVotes()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("clearRoundVotes");
#endif
    }

    /// <summary>
    /// Enables/disables recognition for just one slot (0=left, 1=right) — the align+embed+match
    /// cost is only paid for slots actually enabled, so a caller only waiting on one slot never
    /// pays for the other one too.
    /// </summary>
    public void SetSlotRecognitionEnabled(int slot, bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("setSlotRecognitionEnabled", slot, enabled);
#endif
    }

    /// <summary>
    /// Clears just one slot's lock (0=left, 1=right) without disturbing the other — for games
    /// with independent per-player timing where recognition is already active for one slot and
    /// a full StartRound() would wipe the other slot's already-locked-but-unread result.
    /// </summary>
    public void ClearSlotVote(int slot)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("clearSlotVote", slot);
#endif
    }

    /// <summary>
    /// Opt in to running detection purely for bounding-box display even while recognition
    /// itself is off. Only FRTest (which shows the camera preview + boxes) should call this —
    /// headless games must never touch it, or detection runs every frame for boxes nobody reads.
    /// </summary>
    public void SetBoundingBoxEnabled(bool enabled)
    {
        _previewEnabled = enabled;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("setBoundingBoxEnabled", enabled);
#endif
    }

    /// <summary>Disable recognition — call during question phase to save CPU.</summary>
    public void StopRound()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return;
        _bridge.CallStatic("setRecognitionEnabled", false);
#endif
    }

    /// <summary>
    /// Returns the confirmed (left, right) player names.
    /// Either can be null if not yet confirmed this round.
    /// </summary>
    public (string left, string right) GetConfirmed()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return (null, null);
        try
        {
            string json = _bridge.CallStatic<string>("getConfirmedNames");
            return ParseConfirmedJson(json);
        }
        catch { return (null, null); }
#else
        return (null, null);
#endif
    }

    /// <summary>Seconds a round has to recognize both players before falling back to default names.</summary>
    public float GetTimeoutSec()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return 4f;
        try { return _bridge.CallStatic<float>("getTimeoutSec"); }
        catch { return 4f; }
#else
        return 4f;
#endif
    }

    public string GetDefaultNameLeft()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return "Player_1";
        try { return _bridge.CallStatic<string>("getDefaultNameLeft"); }
        catch { return "Player_1"; }
#else
        return "Player_1";
#endif
    }

    public string GetDefaultNameRight()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized) return "Player_2";
        try { return _bridge.CallStatic<string>("getDefaultNameRight"); }
        catch { return "Player_2"; }
#else
        return "Player_2";
#endif
    }

    // Minimal JSON parse: {"left":"Name","right":null}
    static (string, string) ParseConfirmedJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return (null, null);
        string left  = ExtractField(json, "left");
        string right = ExtractField(json, "right");
        return (left, right);
    }

    static string ExtractField(string json, string key)
    {
        string search = $"\"{key}\":";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return null;
        start += search.Length;
        while (start < json.Length && json[start] == ' ') start++;
        if (start >= json.Length) return null;
        if (json[start] == 'n') return null;  // null
        if (json[start] == '"')
        {
            int end = json.IndexOf('"', start + 1);
            return end > start ? json.Substring(start + 1, end - start - 1) : null;
        }
        return null;
    }

    // Release the USB camera when X01 goes to background so FA (or any other app)
    // can open it. Reinitialize when we come back to the foreground.
    void OnApplicationPause(bool paused)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (paused)
        {
            if (_initialized)
            {
                _bridge?.CallStatic("setRecognitionEnabled", false);
                _bridge?.CallStatic("shutdown");
                _initialized = false;
                Debug.Log("[FacePlugin] paused — camera released");
            }
        }
        else
        {
            if (!_initialized && _bridge != null)
            {
                try
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    _bridge.CallStatic("initialize", activity);
                    _initialized = true;
                    Debug.Log("[FacePlugin] resumed — camera reinitialized");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FacePlugin] reinit on resume failed: {e.Message}");
                }
            }
        }
#endif
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_initialized) _bridge?.CallStatic("shutdown");
        if (CameraTexture != null) Destroy(CameraTexture);
#endif
    }
}
