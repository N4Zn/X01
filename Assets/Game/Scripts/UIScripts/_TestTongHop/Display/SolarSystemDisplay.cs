using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Solar-system answer display.
/// 9 planet buttons (0=Mercury … 7=Neptune, 8=Sun).
/// Sun at bottom-center (half visible). Planets fanned above in narrow arc.
/// </summary>
public class SolarSystemDisplay : MonoBehaviour, IAnswerDisplay, IRevealable
{
    const int N   = 9;
    const int SUN = 8;

    // Sun center well below screen bottom → only a curved sliver visible at bottom
    static readonly Vector2 SunPos = new Vector2(0f, -340f); // -270 trước → -340 để hiện ~1/3 mặt trời
    const float SunDiam = 210f; // reduced so Mercury/Venus orbit outside Sun disk; still > Jupiter 115px

    protected static readonly string[] NameEn = {
        "Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune","Sun"
    };
    protected static readonly string[] NameVi = {
        "Sao Thủy","Sao Kim","Trái Đất","Sao Hỏa","Sao Mộc","Sao Thổ",
        "Sao Thiên Vương","Sao Hải Vương","Mặt Trời"
    };

    // Orbit radii — scaled up for better screen coverage
    static readonly float[] OrbitR = { 258f, 320f, 406f, 492f, 593f, 686f, 750f, 820f, 0f };
    // Mercury right; Venus/Saturn at 90° (center axis); Earth left; Neptune 100°
    static readonly float[] IniAng = {  130f,  60f, 116f,  72f, 107f,  75f,  89f, 100f, 0f };
    // Mercury/Venus enlarged for better visibility; Sun SunDiam > Jupiter 115px
    static readonly float[] Sz = { 52f, 70f, 50f, 38f, 115f, 100f, 70f, 65f, SunDiam };
    // UV scroll ×5 for lively rotation
    static readonly float[] UvSpd = {
        0.200f, 0.140f, 0.240f, 0.200f, 0.100f, 0.070f, 0.050f, 0.040f, 0.030f
    };
    static readonly Color[] PCol = {
        new Color(0.72f,0.70f,0.67f), new Color(0.90f,0.75f,0.50f),
        new Color(0.20f,0.50f,0.85f), new Color(0.80f,0.30f,0.15f),
        new Color(0.80f,0.60f,0.40f), new Color(0.90f,0.85f,0.60f),
        new Color(0.40f,0.85f,0.90f), new Color(0.20f,0.35f,0.85f),
        new Color(1.00f,0.90f,0.20f),
    };
    static readonly string[] TexPath = {
        "SolarSystem/Textures/2k_mercury",
        "SolarSystem/Textures/2k_venus_atmosphere",
        "SolarSystem/Textures/2k_earth_daymap",
        "SolarSystem/Textures/2k_mars",
        "SolarSystem/Textures/2k_jupiter",
        "SolarSystem/Textures/2k_saturn",
        "SolarSystem/Textures/2k_uranus",
        "SolarSystem/Textures/2k_neptune",
        "SolarSystem/Textures/2k_sun",
    };

    const float MoonR      = 40f;  // larger orbit radius
    const float MoonSz     = 10f;
    const float MoonSpd    = 55f;
    // Ellipse Y-scale for 3D perspective (orbit plane viewed at an angle)
    const float OrbElp     = 0.65f; // planet orbit lines
    const float MoonOrbElp = 0.42f; // Moon orbit (tighter for clarity)
// NamNN Comment lại
    [Header("Hit Area")]
    [Tooltip("Mở rộng vùng click của hành tinh ra ngoài mỗi cạnh (px). Tăng lên để click dễ hơn.")]
    public float planetHitPadding = 15f;
	public float minHitSize = 90f;


    protected virtual string[] Names => NameVi;

    // ── Runtime state ─────────────────────────────────────────────────────────

    Action<bool,Team,int[]> _onResult;
    Action<Team>            _onPlayerFailed;
    QuestionData            _q;
    bool _lFin, _rFin, _lWrong, _rWrong;

    readonly RawImage[] _lTex  = new RawImage[N];
    readonly RawImage[] _rTex  = new RawImage[N];
    readonly Image[]    _lOver = new Image[N];
    readonly Image[]    _rOver = new Image[N];
    readonly Button[]   _lBtn  = new Button[N];
    readonly Button[]   _rBtn  = new Button[N];
    readonly float[]    _uv    = new float[N];

    RectTransform _lMoon, _rMoon;
    Image         _lMoonImg, _rMoonImg;
    float _moonAng;
    bool  _built;

    // Shared procedural assets (never destroyed, created once)
    static Sprite    _circleSpr;
    static Sprite    _orbitSpr;
    static Sprite    _moonOrbitSpr;
    static Texture2D _saturnRingTex;
    static Texture2D _glowTex;
    static Texture2D _starfieldTex;

    // ── IAnswerDisplay ────────────────────────────────────────────────────────

    public void Setup(QuestionData q, Action<bool,Team,int[]> onResult, Action<Team> onPlayerFailed)
    {
        _q = q;
        _onResult       = onResult;
        _onPlayerFailed = onPlayerFailed;
        _lFin = _rFin = _lWrong = _rWrong = false;
        _moonAng = 0f;
        for (int i = 0; i < N; i++) _uv[i] = IniAng[i] / 360f;

        if (!_built) BuildUI();

        ResetSide(_lTex, _lOver, _lBtn);
        ResetSide(_rTex, _rOver, _rBtn);
        gameObject.SetActive(true);
    }

    public void HidePlayerAnswers(Team team)
    {
        var over = team == Team.Left ? _lOver : _rOver;
        foreach (var o in over)
            if (o != null) o.transform.parent.gameObject.SetActive(false);
    }

    public void Cleanup() { _q = null; gameObject.SetActive(false); }

    public void RevealCorrectAnswer()
    {
        if (_q?.correctAnswers == null || _q.correctAnswers.Length == 0) return;
        int correct = _q.correctAnswers[0];
        if (correct < 0 || correct >= N) return;
        if (_lOver[correct] != null) ApplyState(_lOver[correct], ItemState.Correct);
        if (_rOver[correct] != null) ApplyState(_rOver[correct], ItemState.Correct);
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_built) return;
        float dt = Time.deltaTime;

        for (int i = 0; i < N; i++)
        {
            _uv[i] = (_uv[i] + UvSpd[i] * dt) % 1f;
            Scroll(_lTex[i], _uv[i]);
            Scroll(_rTex[i], _uv[i]);
        }

        _moonAng = (_moonAng + MoonSpd * dt) % 360f;
        float mRad  = _moonAng * Mathf.Deg2Rad;
        float mSin  = Mathf.Sin(mRad);
        var   mp    = new Vector2(Mathf.Cos(mRad) * MoonR, mSin * MoonR * MoonOrbElp);
        // mSin>0 = Moon on back arc — quadratic keeps Moon bright until ~sin=0.7 then fades
        float mAlpha = mSin > 0f ? Mathf.Clamp01(1f - mSin * mSin * 2f) : 1f;
        var   mCol   = new Color(0.78f, 0.78f, 0.75f, mAlpha);
        if (_lMoon != null) { _lMoon.anchoredPosition = mp; if (_lMoonImg) _lMoonImg.color = mCol; }
        if (_rMoon != null) { _rMoon.anchoredPosition = mp; if (_rMoonImg) _rMoonImg.color = mCol; }
    }

    static void Scroll(RawImage ri, float uvX)
    {
        if (ri == null || ri.texture == null) return;
        var uv = ri.uvRect; uv.x = uvX; ri.uvRect = uv;
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    void OnClick(int idx, Team team)
    {
        bool isL = team == Team.Left;
        if (isL ? _lFin : _rFin) return;
        if (_q == null) return;

        int  correct = _q.correctAnswers?.Length > 0 ? _q.correctAnswers[0] : -1;
        bool ok      = idx == correct;

        var myOver = isL ? _lOver : _rOver;
        var myTex  = isL ? _lTex  : _rTex;
        var myBtn  = isL ? _lBtn  : _rBtn;

        if (ok)
        {
            if (isL) _lFin = true; else _rFin = true;
            LockAll(_lTex, _lOver, _lBtn);
            LockAll(_rTex, _rOver, _rBtn);
            if (correct >= 0 && correct < N)
            {
                ApplyState(_lOver[correct], ItemState.Correct);
                ApplyState(_rOver[correct], ItemState.Correct);
            }
            MusicManager.Instance?.PlayCorrectSfx();
            _onResult?.Invoke(true, team, _q.correctAnswers);
        }
        else
        {
            if (isL) { _lFin = true; _lWrong = true; }
            else     { _rFin = true; _rWrong = true; }
            LockAll(myTex, myOver, myBtn);
            if (idx >= 0 && idx < N) ApplyState(myOver[idx], ItemState.Wrong);
            // Correct answer (green) is deferred until the other player also answers
            _onPlayerFailed?.Invoke(team);
            if (_lWrong && _rWrong)
            {
                if (correct >= 0 && correct < N)
                {
                    ApplyState(_lOver[correct], ItemState.Correct);
                    ApplyState(_rOver[correct], ItemState.Correct);
                }
                _onResult?.Invoke(false, Team.Left, _q.correctAnswers);
            }
        }
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        _built = true;
        EnsureSprites();

        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        BuildHalf(new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                  _lTex, _lOver, _lBtn, ref _lMoon, ref _lMoonImg, Team.Left);
        BuildHalf(new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                  _rTex, _rOver, _rBtn, ref _rMoon, ref _rMoonImg, Team.Right);
        AddDivider();
    }

    void BuildHalf(Vector2 anchorMin, Vector2 anchorMax,
                   RawImage[] texArr, Image[] overArr, Button[] btnArr,
                   ref RectTransform moonRt, ref Image moonImg, Team team)
    {
        // Container covers exactly this half; RectMask2D clips sun's lower half
        var bgGo = new GameObject("BG_" + team);
        bgGo.transform.SetParent(transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = anchorMin;
        bgRt.anchorMax = anchorMax;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bgGo.AddComponent<RectMask2D>();

        // Dark base
        var baseGo = new GameObject("BgBase");
        baseGo.transform.SetParent(bgGo.transform, false);
        FullStretch(baseGo);
        var baseImg = baseGo.AddComponent<Image>();
        baseImg.color = Color.clear; // trong suốt → background phía dưới (2k_stars) hiện xuyên qua
        baseImg.raycastTarget = false;

        // Starfield
        var sfGo = new GameObject("Starfield");
        sfGo.transform.SetParent(bgGo.transform, false);
        FullStretch(sfGo);
        var sfRi = sfGo.AddComponent<RawImage>();
        sfRi.texture = _starfieldTex;
        sfRi.color   = new Color(1f, 1f, 1f, 0.9f);
        sfRi.raycastTarget = false;

        // Orbit rings (rendered behind all planets)
        for (int i = 0; i < N - 1; i++) AddOrbitRing(bgGo.transform, OrbitR[i]);

        // Planets + Sun
        for (int i = 0; i < N; i++)
        {
            float ang = IniAng[i] * Mathf.Deg2Rad;
            float r   = OrbitR[i];
            // Apply OrbElp to Y so planet sits on its elliptical orbit ring
            Vector2 pos = SunPos + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * OrbElp);
            float sz = Sz[i];

            var root = new GameObject(NameEn[i]);
            root.transform.SetParent(bgGo.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchoredPosition = pos;
            rootRt.sizeDelta = new Vector2(sz, sz);

            // Sun: radial glow halo rendered before everything else
            if (i == SUN) AddSunGlow(root.transform, sz);

            // Saturn: back-ring (behind planet body)
            if (i == 5) AddSaturnRingBack(root.transform, sz);

            // Earth: back arc of Moon orbit (top half of ellipse, hidden behind Earth)
            if (i == 2) AddMoonOrbitBack(root.transform);

            // Planet body: circular Mask → solid colour + texture
            var maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(root.transform, false);
            FullStretch(maskGo);
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.sprite = _circleSpr;
            maskImg.color  = Color.white;
            maskImg.raycastTarget = false;
            maskGo.AddComponent<Mask>().showMaskGraphic = false;

            var bgLayer = new GameObject("Bg");
            bgLayer.transform.SetParent(maskGo.transform, false);
            FullStretch(bgLayer);
            var bgLayerImg = bgLayer.AddComponent<Image>();
            bgLayerImg.color = PCol[i];
            bgLayerImg.raycastTarget = false;

            var texGo = new GameObject("Tex");
            texGo.transform.SetParent(maskGo.transform, false);
            FullStretch(texGo);
            var ri = texGo.AddComponent<RawImage>();
            ri.raycastTarget = false;
            var tex2d = Resources.Load<Texture2D>(TexPath[i]);
            if (tex2d != null)
            {
                tex2d.filterMode = FilterMode.Bilinear;
                tex2d.wrapMode   = TextureWrapMode.Repeat;
                ri.texture = tex2d;
            }
            else ri.enabled = false;
            texArr[i] = ri;

            // Saturn: front-ring arc — near side of ring, appears over planet
            if (i == 5) AddSaturnRingFront(root.transform, sz);

            // Earth: front arc of Moon orbit (bottom half of ellipse, in front of Earth)
            if (i == 2) AddMoonOrbitFront(root.transform);

            // Feedback overlay
            var overGo = new GameObject("Over");
            overGo.transform.SetParent(root.transform, false);
            FullStretch(overGo);
            var overImg = overGo.AddComponent<Image>();
            overImg.sprite = _circleSpr;
            overImg.color  = Color.clear;
            overImg.raycastTarget = false;
            overArr[i] = overImg;

            // Click-target (transparent circle on top)
            var hitGo = new GameObject("Hit");
            hitGo.transform.SetParent(root.transform, false);
            FullStretch(hitGo);
            var hitImg = hitGo.AddComponent<Image>();
            hitImg.sprite = _circleSpr;
            hitImg.color  = Color.clear;

            var btn = root.AddComponent<Button>();
            btn.targetGraphic = hitImg;
            btn.colors = ColorBlock.defaultColorBlock;
            int cap = i;
            btn.onClick.AddListener(() => OnClick(cap, team));
            btnArr[i] = btn;

            // Mặt Trời không phải đáp án — tắt click hoàn toàn
            if (i == SUN)
            {
                hitImg.raycastTarget = false;
                btn.interactable = false;
            }
            else
            {
                // Mở rộng vùng click ra ngoài mỗi cạnh — chỉnh planetHitPadding trong Inspector
                float p = planetHitPadding;
                //hitImg.raycastPadding = new Vector4(-p, -p, -p, -p);
				
				float targetSize = Mathf.Max(sz, minHitSize);
				float padding = (targetSize - sz) * 0.5f;

				hitImg.raycastPadding = new Vector4(
					-padding,
					-padding,
					-padding,
					-padding
				);
            }

            // Earth: Moon body rendered above Hit (in front of planet)
            if (i == 2)
            {
                var (mrt, mimg) = BuildMoon(root.transform);
                moonRt  = mrt;
                moonImg = mimg;
            }

            // Label: always above the planet body for readability
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(root.transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchoredPosition = new Vector2(0f, sz * 0.5f + 8f);
            lblRt.sizeDelta        = new Vector2(180f, 26f);
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text               = Names[i];
            tmp.fontSize           = 18f;
            tmp.fontStyle          = FontStyles.Bold;
            tmp.color              = new Color(1f, 1f, 1f, 0.95f);
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.raycastTarget      = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
        }
    }

    // ── Orbit rings ───────────────────────────────────────────────────────────

    static void AddOrbitRing(Transform parent, float radius)
    {
        var go = new GameObject("Orbit");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = SunPos;
        // Elliptical for 3D perspective — same OrbElp used for planet Y positions
        rt.sizeDelta = new Vector2(radius * 2f, radius * 2f * OrbElp);
        // texSz = nextPow2 >= radius → lineW = 3*texSz/(radius*2) >= 1.5px → screen width exactly 3px
        int texSz = Mathf.NextPowerOfTwo(Mathf.CeilToInt(radius));
        float lineW = 5f * texSz / (radius * 2f);
        var img = go.AddComponent<Image>();
        img.sprite = MakeOrbitSprite(texSz, lineW);
        //img.color  = new Color(0.55f, 0.78f, 1f, 0.22f);
        img.color  = new Color(1f, 1f, 1f, 0.22f);
		img.raycastTarget = false;
    }

    // ── Moon orbit ring — split like Saturn ring ──────────────────────────────

    // Back arc (top half of ellipse): added before Mask → behind Earth body
    static void AddMoonOrbitBack(Transform earthRoot)
    {
        var go = new GameObject("MoonOrbitBack");
        go.transform.SetParent(earthRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(MoonR * 2f, MoonR * 2f * MoonOrbElp);
        var img = go.AddComponent<Image>();
        img.sprite     = _moonOrbitSpr;
        img.color      = new Color(0.65f, 0.75f, 0.90f, 0.70f);
        img.raycastTarget = false;
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Top;
        img.fillAmount = 0.5f;
    }

    // Front arc (bottom half of ellipse): added after Mask → in front of Earth body
    static void AddMoonOrbitFront(Transform earthRoot)
    {
        var go = new GameObject("MoonOrbitFront");
        go.transform.SetParent(earthRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(MoonR * 2f, MoonR * 2f * MoonOrbElp);
        var img = go.AddComponent<Image>();
        img.sprite     = _moonOrbitSpr;
        img.color      = new Color(0.65f, 0.75f, 0.90f, 0.70f);
        img.raycastTarget = false;
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Bottom;
        img.fillAmount = 0.5f;
    }

    // ── Sun glow ──────────────────────────────────────────────────────────────

    static void AddSunGlow(Transform sunRoot, float sunDiam)
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(sunRoot, false);
        go.transform.SetAsFirstSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(sunDiam * 2.0f, sunDiam * 2.0f);
        var ri = go.AddComponent<RawImage>();
        ri.texture      = _glowTex;
        ri.color        = Color.white;
        ri.raycastTarget = false;
    }

    // ── Saturn ring ───────────────────────────────────────────────────────────

    // Back ring: full ellipse behind planet body
    static void AddSaturnRingBack(Transform root, float sz)
    {
        float w = sz * 2.5f, h = sz * 0.55f;
        var go = new GameObject("RingBack");
        go.transform.SetParent(root, false);
        go.transform.SetAsFirstSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(w, h);
        var ri = go.AddComponent<RawImage>();
        ri.texture       = _saturnRingTex;
        ri.raycastTarget = false;
    }

    // Front ring: only the near (bottom) arc rendered over the planet body,
    // giving the appearance that the ring crosses in front of Saturn.
    static void AddSaturnRingFront(Transform root, float sz)
    {
        float w      = sz * 2.5f;
        float h      = sz * 0.55f;
        float frontH = h * 0.42f; // bottom 42 % of ellipse = near-side arc

        var go = new GameObject("RingFront");
        go.transform.SetParent(root, false);
        // NOT SetAsFirstSibling → last sibling, rendered over Mask/Overlay
        var rt = go.AddComponent<RectTransform>();
        // Position so the strip aligns with the bottom of the full ring
        rt.anchoredPosition = new Vector2(0f, -h * 0.5f + frontH * 0.5f);
        rt.sizeDelta        = new Vector2(w, frontH);
        var ri = go.AddComponent<RawImage>();
        ri.texture       = _saturnRingTex;
        ri.uvRect        = new Rect(0f, 0f, 1f, 0.42f); // bottom 42 % of texture
        ri.color         = new Color(1f, 1f, 1f, 0.88f);
        ri.raycastTarget = false;
    }

    // ── Moon ─────────────────────────────────────────────────────────────────

    (RectTransform, Image) BuildMoon(Transform earthRoot)
    {
        var go = new GameObject("Moon");
        go.transform.SetParent(earthRoot, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(MoonSz, MoonSz);
        rt.anchoredPosition = new Vector2(MoonR, 0f);
        var img = go.AddComponent<Image>();
        img.sprite = _circleSpr;
        img.color  = new Color(0.78f, 0.78f, 0.75f, 1f);
        img.raycastTarget = false;
        return (rt, img);
    }

    // ── Divider ───────────────────────────────────────────────────────────────

    void AddDivider()
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(2f, 600f);
        var img = go.AddComponent<Image>();
        img.color         = new Color(1f, 1f, 1f, 0.12f);
        img.raycastTarget = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void FullStretch(GameObject go)
    {
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void ResetSide(RawImage[] texArr, Image[] overArr, Button[] btnArr)
    {
        for (int i = 0; i < N; i++)
        {
            // texArr[i]: Tex(child of Mask, child of root) → .parent = Mask → .parent = root
            if (texArr[i] != null)
                texArr[i].transform.parent.parent.gameObject.SetActive(true);
            ApplyState(overArr[i], ItemState.Normal);
            if (btnArr[i] != null) btnArr[i].interactable = true;
        }
    }

    static void LockAll(RawImage[] texArr, Image[] overArr, Button[] btnArr)
    {
        for (int i = 0; i < N; i++)
        {
            ApplyState(overArr[i], ItemState.Locked);
            if (btnArr[i] != null) btnArr[i].interactable = false;
        }
    }

    static void ApplyState(Image over, ItemState state)
    {
        if (over == null) return;
        over.color = state switch
        {
            ItemState.Correct => new Color(0.15f, 0.90f, 0.25f, 0.60f),
            ItemState.Wrong   => new Color(0.90f, 0.15f, 0.15f, 0.60f),
            ItemState.Locked  => new Color(0f,    0f,    0f,    0.55f),
            _                 => Color.clear,
        };
    }

    // ── Procedural asset generation ───────────────────────────────────────────

    static void EnsureSprites()
    {
        if (_circleSpr     == null) _circleSpr     = MakeCircleSprite(256);
        if (_orbitSpr      == null) _orbitSpr      = MakeOrbitSprite(512, 3f);
        if (_moonOrbitSpr  == null)
        {
            int mSz = Mathf.NextPowerOfTwo(Mathf.CeilToInt(MoonR));
            _moonOrbitSpr = MakeOrbitSprite(mSz, 3f * mSz / (MoonR * 2f));
        }
        if (_saturnRingTex == null) _saturnRingTex  = MakeSaturnRingTex(256, 80);
        if (_glowTex       == null) _glowTex        = MakeGlowTex(256);
        if (_starfieldTex  == null)
        {
            _starfieldTex = Resources.Load<Texture2D>("SolarSystem/Textures/2k_stars");
            if (_starfieldTex == null)
                _starfieldTex = MakeStarfieldTex(512, 1400);
        }
    }

    // Filled circle, smooth 2 px anti-aliased edge
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            byte  a  = (byte)(Mathf.Clamp01((r - d) * 0.5f) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // Thin hollow ring, 3 px anti-aliased — orbit lines
    static Sprite MakeOrbitSprite(int size, float lineWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float r = size * 0.5f - lineWidth * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - size * 0.5f + 0.5f, dy = y - size * 0.5f + 0.5f;
            float d    = Mathf.Sqrt(dx * dx + dy * dy);
            float edge = Mathf.Abs(d - r);
            byte  a    = (byte)(Mathf.Clamp01(lineWidth - edge) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // Ellipse ring for Saturn — warm golden tones with soft edges
    static Texture2D MakeSaturnRingTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        float cx = w * 0.5f, cy = h * 0.5f;
        float outerA = w * 0.5f - 1f, outerB = h * 0.5f - 1f;
        float innerA = outerA * 0.48f, innerB = outerB * 0.48f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
            float outerV = (dx/outerA)*(dx/outerA) + (dy/outerB)*(dy/outerB);
            float innerV = (dx/innerA)*(dx/innerA) + (dy/innerB)*(dy/innerB);
            float outerEdge = Mathf.Clamp01((1f - outerV) * outerA * 0.14f);
            float innerEdge = Mathf.Clamp01((innerV - 1f) * innerA * 0.18f);
            byte  a = (byte)(Mathf.Min(outerEdge, innerEdge) * 230f);
            // Colour banding: outer ring slightly darker/cooler
            float bt = Mathf.Clamp01((float)(x - cx + 0.5f) / (outerA * 0.5f));
            byte rr = (byte)Mathf.Lerp(240f, 200f, bt * bt);
            byte gg = (byte)Mathf.Lerp(215f, 175f, bt * bt);
            byte bb = (byte)Mathf.Lerp(140f,  95f, bt * bt);
            px[y * w + x] = new Color32(rr, gg, bb, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    // Radial gradient: warm white centre → orange → transparent rim
    static Texture2D MakeGlowTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float t  = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r); // 0=centre 1=rim

            // Power-curve: smooth, no banding
            float alpha = Mathf.Pow(1f - t, 2.0f);
            byte  a     = (byte)(alpha * 230f);

            // White-hot centre → orange → pale-yellow rim
            byte rr = 255;
            byte gg = (byte)Mathf.Lerp(255f,  90f, t * t);
            byte bb = (byte)Mathf.Lerp(220f,   0f, Mathf.Clamp01(t * 1.6f));
            px[y * size + x] = new Color32(rr, gg, bb, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    // Procedural starfield: ~1400 stars with brightness tinting on deep-space bg
    static Texture2D MakeStarfieldTex(int size, int numStars)
    {
        var rng = new System.Random(42);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Repeat;
        var px = new Color32[size * size];

        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(2, 4, 18, 255);

        void AddPx(int bx, int by, byte v)
        {
            if ((uint)bx >= (uint)size || (uint)by >= (uint)size) return;
            var p = px[by * size + bx];
            px[by * size + bx] = new Color32(
                (byte)Mathf.Min(255, p.r + v),
                (byte)Mathf.Min(255, p.g + v),
                (byte)Mathf.Min(255, p.b + v),
                255);
        }

        for (int s = 0; s < numStars; s++)
        {
            int   sx = rng.Next(size), sy = rng.Next(size);
            float br = (float)(rng.NextDouble() * 0.75 + 0.25);
            byte  b  = (byte)(br * 255);
            // Random colour temperature
            float tint = (float)rng.NextDouble();
            byte  rr   = tint > 0.60f ? (byte)Mathf.Min(255, b + 20) : b;
            byte  bb2  = tint < 0.40f ? (byte)Mathf.Min(255, b + 20) : b;
            px[sy * size + sx] = new Color32(rr, (byte)(b * 0.92f), bb2, 255);

            if (br > 0.60f)
            {
                byte bloom = (byte)(b * 0.22f);
                AddPx(sx - 1, sy, bloom);
                AddPx(sx + 1, sy, bloom);
                AddPx(sx, sy - 1, bloom);
                AddPx(sx, sy + 1, bloom);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }
}
