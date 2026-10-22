using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phát tutorial làm bằng tutorial_builder (HTML) cho BẤT KỲ game nào: khung ở giữa hiện ảnh màn chơi (thu nhỏ),
/// 2 bàn chân đứng dưới khung nhảy lên chỗ dẫm, nút BẮT ĐẦU góc dưới phải; lặp tới khi cô bấm bắt đầu.
///
/// Dữ liệu: Resources/Tutorials/{gameId}/tutorial.json + ảnh/âm thanh cùng thư mục
/// (menu Tools/Tutorial/Import Tutorial Zip... chép vào). gameId = tên game trong GameRegistry (vd AddNumber5).
/// Tự chạy khi vào scene nếu game đang chọn có tutorial; trong lúc chạy game phía sau dừng (timeScale = 0).
/// Toạ độ trong json: % CẢ màn hình, gốc trên-trái (giống tool HTML).
/// </summary>
public class TutorialPlayer : MonoBehaviour
{
    // ── Dữ liệu tutorial.json ───────────────────────────────────────────────
    [Serializable] public class Pt { public float xPct, yPct; public bool IsSet => xPct != 0f || yPct != 0f; }
    [Serializable] public class Box { public float xPct, yPct, wPct, hPct; }
    [Serializable] public class StartBox : Box { public string text; }
    [Serializable] public class FootInfo { public string image; public float wPct, hPct, tipFromTop = 0.4f; public Pt homeLeft, homeRight; }
    [Serializable] public class Step
    {
        public string type;            // tap | say | scene | wait | home
        public Pt left, right;         // tap: chỗ dẫm (% màn hình trong ẢNH = trong khung); (0,0) = chân đó đứng yên
        public string mark;            // tap: none | correct | wrong
        public string after;           // tap: ảnh hiện sau khi dẫm
        public string audio, text;     // say
        public bool wait;              // say: đọc xong mới đi tiếp
        public string image;           // scene
        public float sec;              // wait
    }
    [Serializable] public class Data
    {
        public int schemaVersion;
        public string gameId;
        public Box frame;
        public float dim = 0.62f, musicVolume = 0.35f;
        public StartBox startButton;
        public FootInfo foot;
        public string firstImage;
        public Step[] steps;
    }

    public const string ResourcesRoot = "Tutorials";
    const int SortingOrder = 600;

    /// <summary>Editor: game chạy thẳng scene (không qua máy điều khiển) báo tên biến thể ở đây.</summary>
    public static string EditorGameId;
    public static TutorialPlayer Current { get; private set; }

    /// <summary>Cô bấm "VÀO CHƠI" trên máy điều khiển (GameControlBridge.OnStartPlayRequested).</summary>
    public static void RequestStart() { if (Current != null) Current.Finish(); }

    public static bool Has(string gameId) =>
        !string.IsNullOrEmpty(gameId) && Resources.Load<TextAsset>($"{ResourcesRoot}/{gameId}/tutorial") != null;

    // ── Tự chạy khi vào scene ───────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Launch(); };
        Launch();
    }

    static void Launch()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorPrefs.GetBool(SkipPrefKey, false)) return;
#endif
        if (Current != null) Current.Finish();
        var p = new GameObject("TutorialPlayer").AddComponent<TutorialPlayer>();
        p.StartCoroutine(p.WaitThenPlay());
    }
    public const string SkipPrefKey = "EduXplore.Tutorial.SkipInEditor";

    IEnumerator WaitThenPlay()
    {
        // Chờ vài frame cho controller của game đọc biến thể (SelectedGameName / EditorGameId).
        for (int i = 0; i < 3; i++) yield return null;
        var self = this;
        string id = GameSessionManager.Instance != null ? GameSessionManager.Instance.SelectedGameName : null;
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(id)) id = EditorGameId;
#endif
        // Cô tắt "Hiện hướng dẫn" ở tab Cài đặt của máy điều khiển → vào thẳng game.
        if (GameSettings.Instance != null && !GameSettings.Instance.ShowTutorial) { Destroy(self.gameObject); yield break; }
        var ta = string.IsNullOrEmpty(id) ? null : Resources.Load<TextAsset>($"{ResourcesRoot}/{id}/tutorial");
        if (ta == null) { Destroy(self.gameObject); yield break; }
        Data data = null;
        try { data = JsonUtility.FromJson<Data>(ta.text); }
        catch (Exception e) { Debug.LogError($"[TutorialPlayer] tutorial.json của {id} lỗi: {e.Message}"); }
        if (data == null || data.frame == null || data.frame.wPct <= 0f) { Destroy(self.gameObject); yield break; }
        self.Begin(id, data);
    }

    // ── Trạng thái ──────────────────────────────────────────────────────────
    Data _d;
    string _dir;
    RectTransform _root;
    RawImage _imA, _imB, _front;
    readonly RectTransform[] _feet = new RectTransform[2];
    readonly Vector2[] _tips = new Vector2[2];
    readonly Coroutine[] _footMove = new Coroutine[2];
    RectTransform _fx, _startBtn;
    AudioSource _voice;
    string _curImage;
    float _oldTimeScale = 1f;
    float _beginTime;
    Image _startImg;
    /// <summary>Nút BẮT ĐẦU bị khoá chừng này giây đầu: chạm sàn lúc vừa chọn game (hoặc chạm nhanh) trúng nút ở góc làm bỏ qua cả tutorial.</summary>
    const float StartGraceSec = 2f;
    bool _finished;
    readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    static Texture2D _ring, _disc, _check, _cross;

    void Begin(string id, Data d)
    {
        _d = d;
        _dir = $"{ResourcesRoot}/{id}/";
        Current = this;
        _beginTime = Time.unscaledTime;
        _oldTimeScale = Time.timeScale;
        Time.timeScale = 0f; // dừng game phía sau tới khi bấm bắt đầu
        GameControlBridge.PushTutorialRunning(true);
        MusicManager.Instance?.SetMusicVolumeMultiplier(Mathf.Clamp01(d.musicVolume));
        Build();
        PreloadVoices();
        StartCoroutine(Run());
        Debug.Log($"[TutorialPlayer] Chạy tutorial {id} ({d.steps?.Length ?? 0} bước)");
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        if (Current == this) Current = null;
        StopAllCoroutines();
        if (_voice != null) _voice.Stop();
        Time.timeScale = _oldTimeScale <= 0f ? 1f : _oldTimeScale;
        GameControlBridge.PushTutorialRunning(false);
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Rời scene giữa chừng: trả lại tốc độ game.
        if (!_finished && _root != null) { Time.timeScale = _oldTimeScale <= 0f ? 1f : _oldTimeScale; MusicManager.Instance?.SetMusicVolumeMultiplier(1f); GameControlBridge.PushTutorialRunning(false); }
        if (Current == this) Current = null;
    }

    void Update()
    {
        if (_finished || _root == null) return;
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) { Finish(); return; }
#endif
        if (_startBtn != null)
        {
            bool ready = Time.unscaledTime - _beginTime >= StartGraceSec;
            _startBtn.localScale = ready ? Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f)) : Vector3.one;
            if (_startImg != null) _startImg.color = new Color(0.16f, 0.72f, 0.33f, ready ? 1f : 0.35f); // mờ = chưa bấm được
        }
    }

    // ── Dựng giao diện ──────────────────────────────────────────────────────
    void Build()
    {
        int display = 0;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { display = c.targetDisplay; break; }
        if (display == 0 && Camera.main != null) display = Camera.main.targetDisplay;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        canvas.targetDisplay = display;
        gameObject.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)transform;

        var dim = MakeRaw(_root, "Dim", null, new Color(0f, 0f, 0f, Mathf.Clamp01(_d.dim)));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; // chặn chạm xuống game

        var f = _d.frame;
        var border = MakeRaw(_root, "FrameBorder", null, new Color(1f, 0.78f, 0.24f, 1f));
        Place(border.rectTransform, f.xPct - 0.8f, f.yPct - 1.4f, f.wPct + 1.6f, f.hPct + 2.8f);
        var sh = border.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(6f, -8f);
        var frame = MakeRaw(_root, "Frame", null, new Color(0.04f, 0.08f, 0.19f, 1f));
        Place(frame.rectTransform, f.xPct, f.yPct, f.wPct, f.hPct);
        _imA = MakeRaw(frame.rectTransform, "ImageA", null, Color.white); Stretch(_imA.rectTransform);
        _imB = MakeRaw(frame.rectTransform, "ImageB", null, Color.white); Stretch(_imB.rectTransform);
        _imB.color = new Color(1f, 1f, 1f, 0f);
        _front = _imA;

        Texture2D foot = !string.IsNullOrEmpty(_d.foot?.image) ? Tex(_d.foot.image) : null;
        if (foot == null) foot = Resources.Load<Texture2D>("ui/common_ui/icon_foot");
        for (int s = 0; s < 2; s++)
        {
            var img = MakeRaw(_root, s == 0 ? "FootLeft" : "FootRight", foot, Color.white);
            if (foot == null) img.color = new Color(1f, 0.85f, 0.2f, 1f);
            _feet[s] = img.rectTransform;
        }

        _fx = new GameObject("Fx", typeof(RectTransform)).GetComponent<RectTransform>();
        _fx.SetParent(_root, false); Stretch(_fx);

        BuildStartButton();

        _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.ignoreListenerPause = true;
        _voice.volume = GameSettings.Instance != null ? GameSettings.Instance.SfxVolume : 1f;
    }

    void BuildStartButton()
    {
        var b = _d.startButton ?? new StartBox { xPct = 89.5f, yPct = 86f, wPct = 9f, hPct = 11f };
        var go = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        _startBtn = (RectTransform)go.transform;
        _startBtn.SetParent(_root, false);
        Place(_startBtn, b.xPct, b.yPct, b.wPct, b.hPct);
        var img = go.GetComponent<Image>();
        _startImg = img;
        img.color = new Color(0.16f, 0.72f, 0.33f, 0.35f);
        var o = go.AddComponent<Outline>(); o.effectColor = Color.white; o.effectDistance = new Vector2(4f, -4f);
        var s = go.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.6f); s.effectDistance = new Vector2(4f, -8f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { if (Time.unscaledTime - _beginTime >= StartGraceSec) Finish(); });
        var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var trt = (RectTransform)tgo.transform; trt.SetParent(_startBtn, false);
        trt.anchorMin = new Vector2(0.06f, 0.08f); trt.anchorMax = new Vector2(0.94f, 0.92f); trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = tgo.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = string.IsNullOrEmpty(b.text) ? "BẮT ĐẦU" : b.text;
        t.color = Color.white; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
        t.resizeTextForBestFit = true; t.resizeTextMinSize = 8; t.resizeTextMaxSize = 80; t.raycastTarget = false;
        var to = tgo.AddComponent<Outline>(); to.effectColor = new Color(0.08f, 0.43f, 0.2f, 1f); to.effectDistance = new Vector2(2f, -2f);
    }

    // ── Kịch bản ────────────────────────────────────────────────────────────
    IEnumerator Run()
    {
        var steps = _d.steps ?? new Step[0];
        while (!_finished)
        {
            ShowImage(_d.firstImage, true);
            for (int s = 0; s < 2; s++) PlaceFoot(s, Home(s), 1f);
            var pending = new List<Coroutine>();
            foreach (var st in steps)
            {
                switch (st.type)
                {
                    case "say":
                        var c = StartCoroutine(Say(st.audio));
                        if (st.wait) yield return c; else pending.Add(c);
                        break;
                    case "wait":
                        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, st.sec));
                        break;
                    case "scene":
                        ShowImage(st.image, false);
                        yield return new WaitForSecondsRealtime(0.25f);
                        break;
                    case "home":
                        yield return Both(Home(0), Home(1));
                        break;
                    case "tap":
                        bool l = st.left != null && st.left.IsSet, r = st.right != null && st.right.IsSet;
                        if (!l && !r) break;
                        Vector2? a = l ? ToScreen(st.left) : (Vector2?)null, b = r ? ToScreen(st.right) : (Vector2?)null;
                        yield return Both(a, b);
                        string mark = string.IsNullOrEmpty(st.mark) ? "none" : st.mark;
                        if (mark == "wrong") MusicManager.Instance?.PlayWrongSfx();
                        if (mark == "correct") MusicManager.Instance?.PlayCorrectSfx();
                        var fx = new List<Coroutine>();
                        if (a.HasValue) { fx.Add(StartCoroutine(Press(0))); fx.Add(StartCoroutine(Burst(a.Value, mark))); }
                        if (b.HasValue) { fx.Add(StartCoroutine(Press(1))); fx.Add(StartCoroutine(Burst(b.Value, mark))); }
                        foreach (var co in fx) yield return co;
                        if (!string.IsNullOrEmpty(st.after)) ShowImage(st.after, false);
                        break;
                }
            }
            foreach (var co in pending) yield return co;
            yield return Both(Home(0), Home(1));
            yield return new WaitForSecondsRealtime(0.6f);
        }
    }

    IEnumerator Both(Vector2? left, Vector2? right)
    {
        var cs = new List<Coroutine>();
        if (left.HasValue) cs.Add(StartCoroutine(Jump(0, left.Value)));
        if (right.HasValue) cs.Add(StartCoroutine(Jump(1, right.Value)));
        foreach (var c in cs) yield return c;
    }

    /// <summary>Nạp trước mọi giọng đọc của kịch bản (import mặc định preloadAudioData = false: clip chỉ giải nén ở lần Play đầu
    /// → mất đoạn đầu). Gọi ngay khi bắt đầu để tới lúc đọc thì đã sẵn sàng.</summary>
    void PreloadVoices()
    {
        if (_d.steps == null) return;
        foreach (var st in _d.steps)
            if (st != null && st.type == "say" && !string.IsNullOrEmpty(st.audio)) Voice(st.audio);
    }

    AudioClip Voice(string path)
    {
        if (_clips.TryGetValue(path, out var c)) return c;
        c = Resources.Load<AudioClip>(ResPath(path));
        if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
        _clips[path] = c;
        return c;
    }

    IEnumerator Say(string path)
    {
        var clip = string.IsNullOrEmpty(path) ? null : Voice(path);
        if (clip == null) { yield return new WaitForSecondsRealtime(1.4f); yield break; }
        // Chờ dữ liệu sẵn sàng (tối đa ~1s) rồi mới phát; phát hẹn giờ theo DSP để đầu câu không bị cắt khi luồng âm thanh còn đang nạp.
        for (float w = 0f; clip.loadState == AudioDataLoadState.Loading && w < 1f; w += Time.unscaledDeltaTime) yield return null;
        const double lead = 0.15;
        _voice.Stop();
        _voice.clip = clip;
        _voice.PlayScheduled(AudioSettings.dspTime + lead);
        yield return new WaitForSecondsRealtime(clip.length + (float)lead + 0.03f);
    }

    Vector2 Home(int side)
    {
        var p = side == 0 ? _d.foot?.homeLeft : _d.foot?.homeRight;
        if (p != null && p.IsSet) return new Vector2(p.xPct, p.yPct);
        var f = _d.frame;
        return new Vector2(f.xPct + f.wPct * (side == 0 ? 0.25f : 0.75f), Mathf.Min(95f, f.yPct + f.hPct * 1.11f));
    }

    /// <summary>% trong ảnh màn (= % trong khung) → % màn hình.</summary>
    Vector2 ToScreen(Pt p) => new Vector2(_d.frame.xPct + p.xPct * _d.frame.wPct / 100f, _d.frame.yPct + p.yPct * _d.frame.hPct / 100f);

    Vector2 FootSize()
    {
        var f = _d.foot;
        if (f != null && f.wPct > 0f && f.hPct > 0f) return new Vector2(f.wPct, f.hPct);
        return new Vector2(_d.frame.wPct / 2f * 0.10f, _d.frame.hPct * 0.135f);
    }

    void PlaceFoot(int side, Vector2 tip, float scale)
    {
        var fs = FootSize();
        float tipK = _d.foot != null && _d.foot.tipFromTop > 0f ? _d.foot.tipFromTop : 0.4f;
        Place(_feet[side], tip.x - fs.x / 2f, tip.y - fs.y * tipK, fs.x, fs.y);
        _feet[side].localScale = new Vector3(side == 1 ? -scale : scale, scale, 1f);
        _tips[side] = tip;
    }

    IEnumerator Jump(int side, Vector2 to, float dur = 0.45f)
    {
        var from = _tips[side];
        float arc = Mathf.Clamp(Mathf.Abs(to.y - from.y) * 0.25f + 4f, 4f, 14f) * _d.frame.hPct / 100f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float u = t / dur, k = Mathf.Sin(u * Mathf.PI), e = Mathf.SmoothStep(0f, 1f, u);
            var p = Vector2.LerpUnclamped(from, to, e);
            p.y -= arc * k;
            PlaceFoot(side, p, 1f + 0.3f * k);
            _tips[side] = from;
            yield return null;
        }
        PlaceFoot(side, to, 1f);
    }

    IEnumerator Press(int side)
    {
        var tip = _tips[side];
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            PlaceFoot(side, tip, 1f - 0.08f * Mathf.Sin(t / 0.3f * Mathf.PI));
            yield return null;
        }
        PlaceFoot(side, tip, 1f);
    }

    /// <summary>Vòng sáng lan ra tại chỗ dẫm; Đúng = tích xanh, Sai = dấu X đỏ (rung).</summary>
    IEnumerator Burst(Vector2 tip, string mark)
    {
        Color col = mark == "correct" ? new Color(0.24f, 0.86f, 0.61f) : mark == "wrong" ? new Color(1f, 0.35f, 0.31f) : new Color(1f, 0.85f, 0.29f);
        float px = Screen.height / 100f; // 1% chiều cao màn ≈ px (chỉ dùng tính kích thước vòng)
        var ring = MakeRaw(_fx, "Ring", RingTex(), col);
        RawImage icon = null;
        if (mark == "correct" || mark == "wrong")
        {
            icon = MakeRaw(_fx, "Mark", DiscTex(), col);
            var sym = MakeRaw(icon.rectTransform, "Sym", mark == "correct" ? CheckTex() : CrossTex(), Color.white);
            Stretch(sym.rectTransform);
        }
        float dur = mark == "wrong" ? 0.7f : 0.55f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float u = t / dur, d = (2f + 9f * u) * 1.6f; // đường kính theo % chiều cao
            SetCircle(ring.rectTransform, tip, d);
            ring.color = new Color(col.r, col.g, col.b, 1f - u);
            if (icon != null)
            {
                float s = u < 0.2f ? u / 0.2f * 1.15f : 1.15f - 0.15f * Mathf.Min(1f, (u - 0.2f) / 0.2f);
                float shake = mark == "wrong" ? Mathf.Sin(u * 40f) * (1f - u) * 0.8f : 0f;
                SetCircle(icon.rectTransform, new Vector2(tip.x + shake * 9f / 16f, tip.y - 6f), 7f * s);
            }
            yield return null;
        }
        Destroy(ring.gameObject);
        if (icon == null) yield break;
        // timeScale = 0 trong tutorial → Destroy(obj, delay) không bao giờ chạy; chờ theo giờ thật.
        yield return new WaitForSecondsRealtime(0.25f);
        if (icon != null) Destroy(icon.gameObject);
    }

    /// <summary>Đặt hình tròn đường kính `dPct` (% chiều cao màn) tâm tại `c` (% màn hình), giữ tròn theo tỉ lệ màn.</summary>
    void SetCircle(RectTransform rt, Vector2 c, float dPct)
    {
        float aspect = _root.rect.height > 0f ? _root.rect.width / _root.rect.height : 16f / 9f;
        float w = dPct / aspect;
        Place(rt, c.x - w / 2f, c.y - dPct / 2f, w, dPct);
    }

    void ShowImage(string path, bool instant)
    {
        if (string.IsNullOrEmpty(path) || path == _curImage) return;
        var tex = Tex(path);
        if (tex == null) return;
        _curImage = path;
        var back = _front == _imA ? _imB : _imA;
        back.texture = tex;
        back.transform.SetAsLastSibling();
        if (instant) { back.color = Color.white; _front.color = new Color(1f, 1f, 1f, 0f); }
        else StartCoroutine(Fade(back, _front));
        _front = back;
    }

    IEnumerator Fade(RawImage show, RawImage hide)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            show.color = new Color(1f, 1f, 1f, t / 0.25f);
            yield return null;
        }
        show.color = Color.white;
        hide.color = new Color(1f, 1f, 1f, 0f);
    }

    // ── Tiện ích ────────────────────────────────────────────────────────────
    string ResPath(string jsonPath)
    {
        string name = jsonPath.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);
        int dot = name.LastIndexOf('.');
        if (dot > 0) name = name.Substring(0, dot);
        return _dir + name;
    }

    Texture2D Tex(string jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath)) return null;
        if (_tex.TryGetValue(jsonPath, out var t)) return t;
        t = Resources.Load<Texture2D>(ResPath(jsonPath));
        if (t == null) Debug.LogWarning($"[TutorialPlayer] Thiếu ảnh {ResPath(jsonPath)}");
        _tex[jsonPath] = t;
        return t;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(x / 100f, 1f - (y + h) / 100f);
        rt.anchorMax = new Vector2((x + w) / 100f, 1f - y / 100f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static RawImage MakeRaw(RectTransform parent, string name, Texture tex, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var img = go.GetComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Ảnh vẽ sẵn bằng code (không cần asset): vòng tròn, hình tròn đặc, dấu tích, dấu X.
    static Texture2D RingTex() => _ring != null ? _ring : (_ring = Draw((x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01((0.06f - Mathf.Abs(r - 0.9f)) * 40f); }));
    static Texture2D DiscTex() => _disc != null ? _disc : (_disc = Draw((x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 40f)));
    static Texture2D CheckTex() => _check != null ? _check : (_check = Draw((x, y) => Stroke(x, y, new Vector2(-0.45f, 0.02f), new Vector2(-0.12f, -0.32f), new Vector2(0.45f, 0.35f))));
    static Texture2D CrossTex() => _cross != null ? _cross : (_cross = Draw((x, y) => Mathf.Max(
        Stroke(x, y, new Vector2(-0.38f, -0.38f), new Vector2(0.38f, 0.38f)), Stroke(x, y, new Vector2(-0.38f, 0.38f), new Vector2(0.38f, -0.38f)))));

    static float Stroke(float x, float y, params Vector2[] pts)
    {
        float best = 9f; var p = new Vector2(x, y);
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1], ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (a + ab * k - p).magnitude);
        }
        return Mathf.Clamp01((0.11f - best) * 40f);
    }

    static Texture2D Draw(Func<float, float, float> alpha)
    {
        const int N = 128;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float a = alpha((i + 0.5f) / N * 2f - 1f, (j + 0.5f) / N * 2f - 1f);
                px[j * N + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
