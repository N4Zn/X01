using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cầu nối tới posebridge-release.aar (MediaPipe Pose Landmarker trên K02).
///
/// Preview mượt + khớp khung xương: native đẩy ảnh JPEG theo tốc độ luồng camera (nhiều fps) và kết quả khung xương thưa hơn (~6/s),
/// mỗi kết quả gắn với ảnh đã dùng để suy luận. Ở đây hiển thị video CHẬM một khoảng <see cref="DisplayDelay"/> (≈ chu kỳ kết quả + độ trễ suy luận)
/// để lúc nào cũng có kết quả ở CẢ HAI phía của thời điểm hiển thị → khung xương nội suy giữa hai kết quả, trùng với ảnh đang hiện.
/// <see cref="People"/> (kết quả mới nhất, không nội suy) dùng cho luật chơi; <see cref="DisplayPeople"/> (nội suy) chỉ để vẽ.
/// Sở hữu theo scene (không DontDestroyOnLoad): thoát scene chỉ tạm dừng native (<c>setActive(false)</c>) nên vào lại không phải chờ biên dịch GPU.
/// Chi tiết giao thức: NativePlugins/PoseAndroidLib/.../PoseBridge.kt, docs/pose-k02.md.
/// </summary>
public class PoseBridgeClient : MonoBehaviour
{
    /// <summary>Thứ tự 13 điểm gửi từ native (khớp PoseBridge.KEYPOINTS).</summary>
    public const int Nose = 0, LShoulder = 1, RShoulder = 2, LElbow = 3, RElbow = 4, LWrist = 5, RWrist = 6,
                     LHip = 7, RHip = 8, LKnee = 9, RKnee = 10, LAnkle = 11, RAnkle = 12, PointCount = 13;

    /// <summary>Một người: toạ độ chuẩn hoá 0..1 (gốc trên-trái, trên ảnh ĐÃ xoay/lật theo ipcamera.json) + độ tin cậy.</summary>
    public class Person
    {
        public readonly Vector2[] pt = new Vector2[PointCount];
        public readonly float[] vis = new float[PointCount];
        public Vector2 HipCenter => (pt[LHip] + pt[RHip]) * 0.5f;

        public Vector2 Centroid()
        {
            Vector2 sum = Vector2.zero; int n = 0;
            for (int i = 0; i < PointCount; i++) if (vis[i] > 0.2f) { sum += pt[i]; n++; }
            return n > 0 ? sum / n : new Vector2(0.5f, 0.5f);
        }
    }

    [Serializable] class PoseDto { public float[] p; }
    [Serializable] class ResultDto { public int seq; public int fid; public long ft; public long ms; public long decodeMs; public bool gpu; public List<PoseDto> poses; }

    class FrameRec { public double t; public byte[] jpg; }
    class ResultRec { public double t; public List<Person> people; }

    /// <summary>Texture đang hiển thị (đã trễ <see cref="DisplayDelay"/>). Gán vào RawImage cùng <see cref="UvRect"/>.</summary>
    public Texture2D CameraTexture { get; private set; }
    /// <summary>uvRect bù xoay 180°/lật gương của camera (ảnh gốc từ luồng chưa xoay/lật).</summary>
    public Rect UvRect { get; private set; } = new Rect(0, 0, 1, 1);
    /// <summary>Kết quả mới nhất, không nội suy (dùng cho luật chơi).</summary>
    public readonly List<Person> People = new();
    /// <summary>Khung xương nội suy tại thời điểm đang hiển thị ảnh (chỉ để vẽ).</summary>
    public readonly List<Person> DisplayPeople = new();
    public string Status { get; private set; } = "init";
    public bool Gpu { get; private set; }
    public long InferMs { get; private set; }
    public long DecodeMs { get; private set; }
    public string Source { get; private set; } = "-";
    public float SourceFps { get; private set; }
    /// <summary>Độ trễ video so với thực tế (giây) để khớp khung xương.</summary>
    public float DisplayDelay => (float)_delay;
    /// <summary>Số kết quả mới mỗi giây (đo trung bình ~1s).</summary>
    public float ResultHz { get; private set; }
    public bool IsReady => Status == "ready";
    /// <summary>Phát mỗi khi có kết quả mới (sau khi People đã cập nhật).</summary>
    public event Action ResultUpdated;

    const float MinDelay = 0.12f, MaxDelay = 0.7f;
    const float StaleSeconds = 1.5f;

    readonly List<FrameRec> _frames = new();
    readonly List<ResultRec> _results = new();
    int _lastFrameId, _lastSeq;
    double _delay = 0.3, _interval = 0.17, _latency = 0.15, _lastResultT = -1;
    double _hzWindowStart; int _hzCount;
    float _statusPoll, _slowPoll;
    FrameRec _shown;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaClass _bridge;
#endif

    void Start()
    {
        _hzWindowStart = Time.realtimeSinceStartupAsDouble;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            _bridge = new AndroidJavaClass("com.eduxplore.posebridge.PoseBridge");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _bridge.CallStatic("initialize", activity);
            int flip = _bridge.CallStatic<int>("getFlip"); // bit1 = xoay 180°, bit2 = lật gương
            bool rot = (flip & 1) != 0, mir = (flip & 2) != 0;
            // Xoay 180° = lật cả hai trục; cộng lật gương = chỉ lật dọc.
            bool flipX = rot ^ mir, flipY = rot;
            UvRect = new Rect(flipX ? 1 : 0, flipY ? 1 : 0, flipX ? -1 : 1, flipY ? -1 : 1);
            Debug.Log($"[PoseBridge] initialize flip={flip} uv={UvRect}");
        }
        catch (Exception e)
        {
            Status = "error: " + e.Message;
            Debug.LogError($"[PoseBridge] init failed: {e}");
        }
#else
        Status = "editor (không có plugin)";
#endif
    }

    /// <summary>Bật/tắt suy luận (tắt thì chỉ còn preview).</summary>
    public void SetDetectionEnabled(bool on)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { _bridge?.CallStatic("setDetectionEnabled", on); } catch { }
#endif
    }

    void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_bridge == null) return;
        try
        {
            if (!IsReady && Time.unscaledTime >= _statusPoll)
            {
                _statusPoll = Time.unscaledTime + 0.25f;
                Status = _bridge.CallStatic<string>("getStatus");
            }
            if (Time.unscaledTime >= _slowPoll)
            {
                _slowPoll = Time.unscaledTime + 0.5f;
                Source = _bridge.CallStatic<string>("getSource");
                SourceFps = _bridge.CallStatic<float>("getSourceFps");
            }
            PollFrame(now);
            PollResult(now);
        }
        catch (Exception e) { Debug.LogWarning($"[PoseBridge] poll error: {e.Message}"); }
#endif
        UpdateDelay();
        UpdateDisplay(now);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void PollFrame(double now)
    {
        int id = _bridge.CallStatic<int>("getFrameId");
        if (id == _lastFrameId || id == 0) return;
        sbyte[] raw = _bridge.CallStatic<sbyte[]>("getFrameJpeg");
        long ageMs = _bridge.CallStatic<long>("getFrameAgeMs");
        _lastFrameId = id;
        if (raw == null || raw.Length == 0) return;
        var jpg = new byte[raw.Length];
        Buffer.BlockCopy(raw, 0, jpg, 0, raw.Length);
        _frames.Add(new FrameRec { t = now - Math.Max(0, ageMs) / 1000.0, jpg = jpg });
        if (_frames.Count > 40) _frames.RemoveRange(0, _frames.Count - 40);
    }

    void PollResult(double now)
    {
        int seq = _bridge.CallStatic<int>("getSeq");
        if (seq == _lastSeq) return;
        _lastSeq = seq;
        var dto = JsonUtility.FromJson<ResultDto>(_bridge.CallStatic<string>("getResultJson"));
        if (dto == null) return;
        long nativeNow = _bridge.CallStatic<long>("getNowMs");
        double t = now - Math.Max(0, nativeNow - dto.ft) / 1000.0; // thời điểm (đồng hồ Unity) của ảnh được dùng cho kết quả này

        Gpu = dto.gpu; InferMs = dto.ms; DecodeMs = dto.decodeMs;
        var list = ParsePoses(dto.poses);
        _results.Add(new ResultRec { t = t, people = list });
        if (_results.Count > 12) _results.RemoveAt(0);

        if (_lastResultT > 0) _interval = Lerp(_interval, Math.Clamp(t - _lastResultT, 0.03, 1.0), 0.2);
        _latency = Lerp(_latency, Math.Clamp(now - t, 0.03, 1.0), 0.2);
        _lastResultT = t;

        People.Clear(); People.AddRange(list);
        _hzCount++;
        if (now - _hzWindowStart >= 1.0)
        {
            ResultHz = (float)(_hzCount / (now - _hzWindowStart));
            _hzCount = 0; _hzWindowStart = now;
        }
        ResultUpdated?.Invoke();
    }
#endif

    static double Lerp(double a, double b, double k) => a + (b - a) * k;

    /// <summary>Độ trễ hiển thị = chu kỳ kết quả + độ trễ suy luận + biên nhỏ (lọc mượt để video không co giãn tốc độ).</summary>
    void UpdateDelay()
    {
        double target = Math.Clamp(_interval + _latency + 0.03, MinDelay, MaxDelay);
        _delay = Lerp(_delay, target, 0.02);
    }

    void UpdateDisplay(double now)
    {
        double td = now - _delay;

        // Ảnh: lấy ảnh mới nhất có t ≤ td, bỏ ảnh cũ hơn.
        int idx = -1;
        for (int i = 0; i < _frames.Count; i++) { if (_frames[i].t <= td) idx = i; else break; }
        if (idx >= 0)
        {
            var f = _frames[idx];
            if (f != _shown)
            {
                if (CameraTexture == null) CameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                CameraTexture.LoadImage(f.jpg);
                _shown = f;
            }
            if (idx > 0) _frames.RemoveRange(0, idx);
        }

        // Khung xương: nội suy giữa kết quả ngay trước và ngay sau td.
        DisplayPeople.Clear();
        if (_results.Count == 0 || now - _results[_results.Count - 1].t > StaleSeconds) return;
        ResultRec a = null, b = null;
        for (int i = 0; i < _results.Count; i++)
        {
            if (_results[i].t <= td) a = _results[i];
            else { b = _results[i]; break; }
        }
        if (a == null) a = _results[0];
        if (b == null || a == b) { DisplayPeople.AddRange(a.people); return; }
        float alpha = Mathf.Clamp01((float)((td - a.t) / Math.Max(1e-3, b.t - a.t)));
        Blend(a.people, b.people, alpha, DisplayPeople);
    }

    /// <summary>Ghép từng người của A với người gần nhất của B (cùng người) rồi nội suy; không ghép được thì giữ ở bên gần hơn.</summary>
    static void Blend(List<Person> a, List<Person> b, float alpha, List<Person> outList)
    {
        var used = new bool[b.Count];
        foreach (var pa in a)
        {
            int best = -1; float bd = 0.25f;
            var ca = pa.Centroid();
            for (int j = 0; j < b.Count; j++)
            {
                if (used[j]) continue;
                float d = Vector2.Distance(ca, b[j].Centroid());
                if (d < bd) { bd = d; best = j; }
            }
            if (best < 0) { if (alpha < 0.5f) outList.Add(pa); continue; }
            used[best] = true;
            var pb = b[best];
            var p = new Person();
            for (int i = 0; i < PointCount; i++)
            {
                p.pt[i] = Vector2.Lerp(pa.pt[i], pb.pt[i], alpha);
                p.vis[i] = Mathf.Lerp(pa.vis[i], pb.vis[i], alpha);
            }
            outList.Add(p);
        }
        if (alpha >= 0.5f) for (int j = 0; j < b.Count; j++) if (!used[j]) outList.Add(b[j]);
    }

    static List<Person> ParsePoses(List<PoseDto> poses)
    {
        var res = new List<Person>();
        if (poses == null) return res;
        foreach (var d in poses)
        {
            if (d?.p == null || d.p.Length < PointCount * 3) continue;
            var person = new Person();
            for (int i = 0; i < PointCount; i++)
            {
                person.pt[i] = new Vector2(d.p[i * 3], d.p[i * 3 + 1]);
                person.vis[i] = d.p[i * 3 + 2];
            }
            res.Add(person);
        }
        return res;
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { _bridge?.CallStatic("setActive", false); } catch { }
#endif
        if (CameraTexture != null) Destroy(CameraTexture);
    }
}
