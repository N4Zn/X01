using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanetOrderDisplay : MonoBehaviour, IAnswerDisplay
{
    static readonly string[] PlanetNames = {
        "Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune"
    };
    static readonly Color[] PlanetColors = {
        new Color(0.60f, 0.58f, 0.55f),
        new Color(0.88f, 0.76f, 0.40f),
        new Color(0.20f, 0.48f, 0.82f),
        new Color(0.78f, 0.28f, 0.10f),
        new Color(0.78f, 0.58f, 0.34f),
        new Color(0.88f, 0.82f, 0.48f),
        new Color(0.38f, 0.82f, 0.88f),
        new Color(0.22f, 0.32f, 0.80f),
    };
    static readonly string[] TexPaths = {
        "SolarSystem/Textures/2k_mercury",
        "SolarSystem/Textures/2k_venus_atmosphere",
        "SolarSystem/Textures/2k_earth_daymap",
        "SolarSystem/Textures/2k_mars",
        "SolarSystem/Textures/2k_jupiter",
        "SolarSystem/Textures/2k_saturn",
        "SolarSystem/Textures/2k_uranus",
        "SolarSystem/Textures/2k_neptune",
    };
    static readonly float[] RotSpeeds = {
        -4.7f, 2.0f, -3.6f, -3.5f, -8.7f, -8.3f, -5.4f, -5.1f,
    };
    const int N = 8;
    const int SaturnIdx = 5;

    const float CanvasW  = 1024f;
    const float CanvasH  = 600f;
    const float PadT     = 8f;
    const float PadL     = 10f;
    const float PadR     = 10f;
    const float PadB     = 10f;
    const float SunSz    = 108f;
    const float SunGap   = 10f;
    const float ColGap   = 6f;
    const float RowGap   = 8f;
    const float RingTilt = -25f;

    static readonly Color CardBg   = new Color(0.06f, 0.10f, 0.20f);
    static readonly Color LabelCol = new Color(0.88f, 0.93f, 1.00f);

    // ── Layout ────────────────────────────────────────────────────────────────
    float   _colW, _btnH, _gridL, _gridBotY;
    Vector2 _sunCenter;

    // ── State ─────────────────────────────────────────────────────────────────
    Action<bool, Team, int[]> _onResult;
    Action<Team>              _onPlayerFailed;
    QuestionData              _q;
    int    _step;
    int[]  _trap;
    bool[] _topCorrect;
    bool   _locked;
    bool   _built;
    int    _successCount;

    // ── UI ────────────────────────────────────────────────────────────────────
    readonly Image[,]           _bgImg     = new Image[N, 2];
    readonly RawImage[,]        _tex       = new RawImage[N, 2];
    readonly TextMeshProUGUI[,] _lbl       = new TextMeshProUGUI[N, 2];
    readonly Image[,]           _over      = new Image[N, 2];
    readonly Button[,]          _btns      = new Button[N, 2];
    readonly Vector2[,]         _center    = new Vector2[N, 2];
    readonly GameObject[,]      _ringBack  = new GameObject[N, 2];
    readonly GameObject[,]      _ringFront = new GameObject[N, 2];
    readonly int[,]             _planetIdx = new int[N, 2];

    TextMeshProUGUI _countdownTxt;
    TextMeshProUGUI _successTxt;
    TextMeshProUGUI _timerTxt;
    Image           _flashImg;
    RawImage        _sunTex;
    float           _displayTimer = -1f; // -1 = not started yet

    // ── Astronaut ─────────────────────────────────────────────────────────────
    RectTransform _astronautRt;
    Vector2       _astronautPos;
    Coroutine     _astronautAnim;
    Coroutine     _breathingAnim;

    static Sprite _circleSpr;

    // ── IAnswerDisplay ────────────────────────────────────────────────────────

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        StopAllCoroutines();
        _q = q; _onResult = onResult; _onPlayerFailed = onPlayerFailed;
        _step = 0; _locked = false;
        Randomize();
        if (!_built) BuildUI();
        else         RefreshContent();
        gameObject.SetActive(true);
        MusicManager.Instance?.PlaySolarSystemMusic();
    }

    public void HidePlayerAnswers(Team team) { }

    public void Cleanup()
    {
        StopAllCoroutines();
        _q = null;
        // Keep active — black BG covers the split-screen during inter-question transition.
        if (_countdownTxt) _countdownTxt.text = "";
        if (_flashImg) { _flashImg.color = Color.clear; _flashImg.raycastTarget = false; }
        for (int col = 0; col < N; col++)
        for (int row = 0; row < 2; row++)
            if (_over[col, row] != null) _over[col, row].color = Color.clear;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_built) return;
        float dt = Time.deltaTime;

        // Planet + sun rotation
        for (int col = 0; col < N; col++)
        for (int row = 0; row < 2; row++)
        {
            var t = _tex[col, row];
            if (t != null) t.transform.Rotate(0f, 0f, RotSpeeds[_planetIdx[col, row]] * dt);
        }
        if (_sunTex != null) _sunTex.transform.Rotate(0f, 0f, -2.0f * dt);

        // Game timer countdown (mirrors controller timer visually)
        if (_displayTimer > 0f)
        {
            _displayTimer -= dt;
            if (_displayTimer < 0f) _displayTimer = 0f;
            if (_timerTxt != null)
            {
                int total = Mathf.CeilToInt(_displayTimer);
                int m = total / 60, s = total % 60;
                _timerTxt.text  = m > 0 ? $"{m}:{s:D2}" : $"{total}s";
                _timerTxt.color = _displayTimer < 15f
                    ? new Color(1f, 0.35f, 0.35f)
                    : new Color(0.50f, 0.90f, 1.00f);
            }
        }
    }

    // ── Randomise ─────────────────────────────────────────────────────────────

    void Randomize()
    {
        _trap = new int[N]; _topCorrect = new bool[N];
        for (int col = 0; col < N; col++)
        {
            int t;
            do { t = UnityEngine.Random.Range(0, N); } while (t == col);
            _trap[col] = t;
            _topCorrect[col] = UnityEngine.Random.value > 0.5f;
        }
    }

    // ── BuildUI ───────────────────────────────────────────────────────────────

    void BuildUI()
    {
        _built = true;
        if (_circleSpr == null) _circleSpr = MakeCircleSprite(128);

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        MakeRect("BG", transform, 0, 0, CanvasW, CanvasH, new Color(0.02f, 0.03f, 0.09f));

        float gridH = CanvasH - PadT - PadB;
        float sunCY = PadB + gridH * 0.5f;
        _sunCenter  = new Vector2(PadL + SunSz * 0.5f, sunCY);

        BuildSun(sunCY);

        _gridL    = PadL + SunSz + SunGap;
        float gridW = CanvasW - _gridL - PadR;
        _colW     = (gridW - (N - 1) * ColGap) / N;
        _btnH     = (gridH - RowGap) / 2f;
        _gridBotY = PadB;

        for (int col = 0; col < N; col++)
        {
            float x = _gridL + col * (_colW + ColGap);
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? (_gridBotY + _btnH + RowGap) : _gridBotY;
                _center[col, row] = new Vector2(x + _colW * 0.5f, y + _btnH * 0.5f + 10f);
                BuildButton(col, row, x, y);
            }
        }

        // Astronaut — above grid, below flash
        BuildAstronaut();

        // Flash — full-screen overlay
        var fGo = new GameObject("Flash");
        fGo.transform.SetParent(transform, false);
        var fRt = fGo.AddComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
        fRt.offsetMin = fRt.offsetMax = Vector2.zero;
        _flashImg = fGo.AddComponent<Image>();
        _flashImg.color = Color.clear; _flashImg.raycastTarget = false;

        // Countdown — above flash
        var cdGo = new GameObject("Countdown");
        cdGo.transform.SetParent(transform, false);
        var cdRt = cdGo.AddComponent<RectTransform>();
        cdRt.anchorMin = new Vector2(0.2f, 0.25f);
        cdRt.anchorMax = new Vector2(0.8f, 0.75f);
        cdRt.offsetMin = cdRt.offsetMax = Vector2.zero;
        _countdownTxt = cdGo.AddComponent<TextMeshProUGUI>();
        _countdownTxt.fontSize = 52f; _countdownTxt.fontStyle = FontStyles.Bold;
        _countdownTxt.color = Color.white;
        _countdownTxt.alignment = TextAlignmentOptions.Center;
        _countdownTxt.raycastTarget = false; _countdownTxt.text = "";

        // Stats panel (timer + score) — top-left, above flash
        BuildStatsPanel();

        // Init display timer once
        _displayTimer = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100f;

        RefreshContent();
    }

    void BuildSun(float sunCY)
    {
        var sunCont = new GameObject("Sun");
        sunCont.transform.SetParent(transform, false);
        var scntRt = sunCont.AddComponent<RectTransform>();
        scntRt.anchorMin = scntRt.anchorMax = Vector2.zero;
        scntRt.pivot = Vector2.zero;
        scntRt.anchoredPosition = new Vector2(PadL, sunCY - SunSz * 0.5f);
        scntRt.sizeDelta = new Vector2(SunSz, SunSz);

        var sunCircle = new GameObject("SunCircle");
        sunCircle.transform.SetParent(sunCont.transform, false);
        var sCircRt = sunCircle.AddComponent<RectTransform>();
        sCircRt.anchorMin = Vector2.zero; sCircRt.anchorMax = Vector2.one;
        sCircRt.offsetMin = sCircRt.offsetMax = Vector2.zero;
        var sCircImg = sunCircle.AddComponent<Image>();
        sCircImg.sprite = _circleSpr; sCircImg.raycastTarget = false;
        sunCircle.AddComponent<Mask>().showMaskGraphic = false;

        var sunBgGo = new GameObject("SunBg");
        sunBgGo.transform.SetParent(sunCircle.transform, false);
        var sbRt = sunBgGo.AddComponent<RectTransform>();
        sbRt.anchorMin = Vector2.zero; sbRt.anchorMax = Vector2.one;
        sbRt.offsetMin = sbRt.offsetMax = Vector2.zero;
        sunBgGo.AddComponent<Image>().color = new Color(1f, 0.88f, 0.12f);

        var sunTexGo = new GameObject("SunTex");
        sunTexGo.transform.SetParent(sunCircle.transform, false);
        var stRt = sunTexGo.AddComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one;
        stRt.offsetMin = stRt.offsetMax = Vector2.zero;
        _sunTex = sunTexGo.AddComponent<RawImage>();
        _sunTex.texture = Resources.Load<Texture2D>("SolarSystem/Textures/2k_sun");
        _sunTex.raycastTarget = false;

        var sunLbl = new GameObject("SunLbl");
        sunLbl.transform.SetParent(sunCont.transform, false);
        sunLbl.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var slRt = sunLbl.AddComponent<RectTransform>();
        slRt.anchorMin = Vector2.zero; slRt.anchorMax = Vector2.one;
        slRt.offsetMin = slRt.offsetMax = Vector2.zero;
        var sl = sunLbl.AddComponent<TextMeshProUGUI>();
        sl.text = "SUN"; sl.fontSize = 24f; sl.fontStyle = FontStyles.Bold;
        sl.color = LabelCol;
        sl.alignment = TextAlignmentOptions.Center;
        sl.raycastTarget = false;
    }

    void BuildStatsPanel()
    {
        float rowH = 26f, gap = 3f;
        float panelH = rowH * 2 + gap;
        float panelY = CanvasH - PadT - panelH;

        // Timer row (top)
        _timerTxt = StatRow("Timer", new Vector2(PadL, panelY + rowH + gap), new Vector2(90f, rowH),
                            new Color(0.50f, 0.90f, 1.00f));

        // Score row (bottom) — icon + "Saved: X"
        _successTxt = StatRowWithIcon("Score", new Vector2(PadL, panelY), new Vector2(112f, rowH),
                                      new Color(1f, 0.85f, 0.20f), "Space/Astronaut");
    }

    TextMeshProUGUI StatRow(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        var tGo = new GameObject("T");
        tGo.transform.SetParent(go.transform, false);
        var tRt = tGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 15f; tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    TextMeshProUGUI StatRowWithIcon(string name, Vector2 pos, Vector2 size, Color textColor, string iconResPath)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Icon
        float iconSz = size.y - 4f;
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot     = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(3f, 0f);
        iconRt.sizeDelta = new Vector2(iconSz, iconSz);
        var ri = iconGo.AddComponent<RawImage>();
        ri.texture = Resources.Load<Texture2D>(iconResPath);
        ri.raycastTarget = false;

        // Text — sits right of the icon
        var tGo = new GameObject("T");
        tGo.transform.SetParent(go.transform, false);
        var tRt = tGo.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(iconSz + 6f, 0f);
        tRt.offsetMax = Vector2.zero;
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 15f; tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    void BuildAstronaut()
    {
        var go = new GameObject("Astronaut");
        go.transform.SetParent(transform, false);
        _astronautRt = go.AddComponent<RectTransform>();
        _astronautRt.anchorMin = _astronautRt.anchorMax = Vector2.zero;
        _astronautRt.pivot     = new Vector2(0.5f, 0.5f);

        var tex = Resources.Load<Texture2D>("Space/2");
        // Preserve aspect ratio — base height 96 (80 × 1.2)
        float h = 96f;
        float w = (tex != null && tex.height > 0) ? h * ((float)tex.width / tex.height) : h;
        _astronautRt.sizeDelta = new Vector2(w, h);

        Vector2 startPos = AstronautStartPos;
        _astronautRt.anchoredPosition = startPos;
        _astronautPos = startPos;

        var ri = go.AddComponent<RawImage>();
        ri.texture       = tex;
        ri.raycastTarget = false;
    }

    // 30 px above the top edge of the planet circle
    Vector2 AstronautTarget(int col, int row)
    {
        float circleRadius = _colW * 0.34f; // circSz = _colW * 0.68f → radius = 0.34f
        return new Vector2(_center[col, row].x, _center[col, row].y + circleRadius + 30f);
    }

    // Start position: 30 px above the sun circle
    Vector2 AstronautStartPos => new Vector2(_sunCenter.x, _sunCenter.y + SunSz * 0.5f + 30f);

    void StartBreathing()
    {
        if (_breathingAnim != null) StopCoroutine(_breathingAnim);
        _breathingAnim = StartCoroutine(AstronautBreathing());
    }

    IEnumerator AstronautBreathing()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            // 1.0 → 1.5 → 1.0 cycle every 2s
            float s = 1f + 0.15f * Mathf.Sin(t * Mathf.PI); // period = 2s
            if (_astronautRt != null)
                _astronautRt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ── BuildButton ───────────────────────────────────────────────────────────

    void BuildButton(int col, int row, float x, float y)
    {
        var card = MakeRect($"Btn_{col}_{row}", transform, x, y, _colW, _btnH, CardBg);
        card.GetComponent<Image>().raycastTarget = false;

        float circSz = _colW * 0.68f;
        float ringW  = Mathf.Min(circSz * 1.60f, _colW - 4f);
        var circOff  = new Vector2(0f, 10f);
        var ringQ    = Quaternion.Euler(0f, 0f, RingTilt);

        var rbCont = MakeRingContainer("RingBack", card.transform, circOff, ringQ);
        AddRingBands(rbCont.transform, ringW, isFront: false);
        rbCont.SetActive(false);
        _ringBack[col, row] = rbCont;

        var circGo = new GameObject("Circle");
        circGo.transform.SetParent(card.transform, false);
        var cRt = circGo.AddComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = circOff;
        cRt.sizeDelta = new Vector2(circSz, circSz);
        var cImg = circGo.AddComponent<Image>();
        cImg.sprite = _circleSpr; cImg.raycastTarget = false;
        circGo.AddComponent<Mask>().showMaskGraphic = false;

        var bgGo = new GameObject("Bg");
        bgGo.transform.SetParent(circGo.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        _bgImg[col, row] = bgGo.AddComponent<Image>();
        _bgImg[col, row].raycastTarget = false;

        var texGo = new GameObject("Tex");
        texGo.transform.SetParent(circGo.transform, false);
        var texRt = texGo.AddComponent<RectTransform>();
        texRt.anchorMin = Vector2.zero; texRt.anchorMax = Vector2.one;
        texRt.offsetMin = texRt.offsetMax = Vector2.zero;
        _tex[col, row] = texGo.AddComponent<RawImage>();
        _tex[col, row].raycastTarget = false;

        var rfCont = MakeRingContainer("RingFront", card.transform, circOff, ringQ);
        AddRingBands(rfCont.transform, ringW, isFront: true);
        rfCont.SetActive(false);
        _ringFront[col, row] = rfCont;

        float lblSpan = Mathf.Min(_colW * 0.88f, _btnH * 0.38f);
        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(card.transform, false);
        var lRt = lblGo.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.5f, 0f);
        lRt.anchorMax = new Vector2(0.5f, 0f);
        lRt.pivot     = new Vector2(0.5f, 0.5f);
        lRt.anchoredPosition = new Vector2(0f, lblSpan * 0.5f + 4f);
        lRt.sizeDelta = new Vector2(lblSpan, 22f);
        lblGo.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 24f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        _lbl[col, row] = tmp;

        var overGo = new GameObject("Over");
        overGo.transform.SetParent(card.transform, false);
        var overRt = overGo.AddComponent<RectTransform>();
        overRt.anchorMin = Vector2.zero; overRt.anchorMax = Vector2.one;
        overRt.offsetMin = overRt.offsetMax = Vector2.zero;
        _over[col, row] = overGo.AddComponent<Image>();
        _over[col, row].color = Color.clear;
        _over[col, row].raycastTarget = false;

        var hitGo = new GameObject("Hit");
        hitGo.transform.SetParent(card.transform, false);
        var hitRt = hitGo.AddComponent<RectTransform>();
        hitRt.anchorMin = Vector2.zero; hitRt.anchorMax = Vector2.one;
        hitRt.offsetMin = hitRt.offsetMax = Vector2.zero;
        var hitImg = hitGo.AddComponent<Image>();
        hitImg.color = Color.clear;

        var btn = card.AddComponent<Button>();
        btn.targetGraphic = hitImg;
        btn.transition    = Selectable.Transition.None;
        int c = col, r = row;
        btn.onClick.AddListener(() => OnClick(c, r));
        _btns[col, row] = btn;
    }

    // ── RefreshContent ────────────────────────────────────────────────────────

    void RefreshContent()
    {
        for (int col = 0; col < N; col++)
        for (int row = 0; row < 2; row++)
        {
            bool isCorrect = (row == 0) == _topCorrect[col];
            int  planet    = isCorrect ? col : _trap[col];
            _planetIdx[col, row] = planet;

            var tex = Resources.Load<Texture2D>(TexPaths[planet]);
            if (_tex[col, row]   != null) { _tex[col, row].texture = tex; _tex[col, row].enabled = tex != null; }
            if (_bgImg[col, row] != null)   _bgImg[col, row].color = PlanetColors[planet];

            if (_lbl[col, row] != null)
            {
                _lbl[col, row].text  = PlanetNames[planet];
                _lbl[col, row].color = LabelCol;
            }

            bool isSaturn = (planet == SaturnIdx);
            if (_ringBack[col, row]  != null) _ringBack[col, row].SetActive(isSaturn);
            if (_ringFront[col, row] != null) _ringFront[col, row].SetActive(isSaturn);

            if (_over[col, row] != null) _over[col, row].color = Color.clear;
            // Only the current step column is active; future columns stay locked
            if (_btns[col, row] != null) _btns[col, row].interactable = (col == _step);
        }

        if (_flashImg)     { _flashImg.color = Color.clear; _flashImg.raycastTarget = false; }
        if (_countdownTxt)   _countdownTxt.text = "";
        if (_successTxt)     _successTxt.text = $"Saved: {_successCount}";

        ResetAstronaut();
        StartBreathing();
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    void OnClick(int col, int row)
    {
        if (_locked || _q == null) return;
        bool colCorrect = (col == _step);
        bool rowCorrect = (row == 0) == _topCorrect[col];
        if (colCorrect && rowCorrect) HandleCorrect(col, row);
        else                          StartCoroutine(WrongEffect(col, row));
    }

    void HandleCorrect(int col, int row)
    {
        _over[col, row].color = new Color(0.10f, 0.80f, 0.20f, 0.55f);
        _btns[col, 0].interactable = false;
        _btns[col, 1].interactable = false;
        MusicManager.Instance?.PlayCorrectSfx();
        MoveAstronaut(AstronautTarget(col, row));
        _step++;
        if (_step >= N)
        {
            _successCount++;
            if (_successTxt) _successTxt.text = $"Saved: {_successCount}";
            StartCoroutine(WinSequence());
        }
        else
        {
            // Unlock only the next column
            _btns[_step, 0].interactable = true;
            _btns[_step, 1].interactable = true;
        }
    }

    // ── Win sequence ──────────────────────────────────────────────────────────

    IEnumerator WinSequence()
    {
        _locked = true;
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FlashToWhiteAndCountdown());
        ZeroControllerDelays();
        _onResult?.Invoke(true, Team.Left, new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
    }

    // ── Wrong effect ──────────────────────────────────────────────────────────

    IEnumerator WrongEffect(int col, int row)
    {
        _locked = true;
        for (int c = 0; c < N; c++)
        for (int r = 0; r < 2; r++)
            if (_btns[c, r] != null) _btns[c, r].interactable = false;

        _over[col, row].color = new Color(0.90f, 0.10f, 0.10f, 0.60f);
        MusicManager.Instance?.PlayWrongSfx();
        // Astronaut flies above wrong planet in parallel with meteor
        MoveAstronaut(AstronautTarget(col, row));
        yield return StartCoroutine(MeteorFly(col, row));
        MusicManager.Instance?.PlayExplosionSfx();
        yield return StartCoroutine(ExplosionBurst(_center[col, row], isWin: false));
        yield return StartCoroutine(FlashToWhiteAndCountdown(skipFadeIn: true));

        ZeroControllerDelays();
        _onPlayerFailed?.Invoke(Team.Left);
        _onResult?.Invoke(false, Team.Left, null);
    }

    // ── Explosion burst ───────────────────────────────────────────────────────

    IEnumerator ExplosionBurst(Vector2 center, bool isWin)
    {
        const int   Count = 28;
        const float Dur   = 0.65f;

        var shards = new (RectTransform rt, Image img, Vector2 vel, Color baseCol)[Count];
        for (int i = 0; i < Count; i++)
        {
            float angle = i * (360f / Count) + UnityEngine.Random.Range(-7f, 7f);
            float speed = UnityEngine.Random.Range(90f, 260f);
            float sz    = UnityEngine.Random.Range(7f, 26f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                      Mathf.Sin(angle * Mathf.Deg2Rad));
            Color col = isWin
                ? Color.Lerp(new Color(1f, 0.90f, 0.15f), new Color(1f, 0.45f, 0.05f),
                             UnityEngine.Random.value)
                : Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.75f, 0.10f),
                             UnityEngine.Random.value);

            var go  = new GameObject("Shard");
            go.transform.SetParent(transform, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(sz, sz);
            var img = go.AddComponent<Image>();
            img.sprite = _circleSpr; img.color = col; img.raycastTarget = false;

            shards[i] = (rt, img, dir * speed, col);
        }

        // Also spawn a bright white flash circle that expands then fades (shockwave ring)
        var ringGo  = new GameObject("Shockwave");
        ringGo.transform.SetParent(transform, false);
        var ringRt  = ringGo.AddComponent<RectTransform>();
        ringRt.anchorMin = ringRt.anchorMax = Vector2.zero;
        ringRt.pivot     = new Vector2(0.5f, 0.5f);
        ringRt.anchoredPosition = center;
        ringRt.sizeDelta = new Vector2(10f, 10f);
        var ringImg = ringGo.AddComponent<Image>();
        ringImg.sprite = _circleSpr;
        ringImg.color  = isWin ? new Color(1f, 0.95f, 0.50f, 0.85f) : new Color(1f, 0.50f, 0.10f, 0.85f);
        ringImg.raycastTarget = false;

        // Screen fades to white in sync with the explosion
        if (_flashImg) { _flashImg.color = Color.clear; _flashImg.raycastTarget = true; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Dur;
            float e    = Mathf.Clamp01(t);
            float eFly = Mathf.Pow(e, 0.55f);   // fast burst, then decelerate
            float fade = 1f - Mathf.Pow(e, 1.8f);

            foreach (var (rt, img, vel, baseCol) in shards)
            {
                if (rt == null) continue;
                rt.anchoredPosition = center + vel * eFly;
                img.color = new Color(baseCol.r, baseCol.g, baseCol.b, fade);
            }

            // Shockwave ring expands 0→280px and fades
            if (ringRt != null)
            {
                float sz2 = 280f * eFly;
                ringRt.sizeDelta = new Vector2(sz2, sz2);
                ringImg.color    = new Color(ringImg.color.r, ringImg.color.g, ringImg.color.b,
                                             0.85f * (1f - e));
            }

            // Screen gradually brightens to white as explosion fades
            if (_flashImg) _flashImg.color = new Color(1f, 1f, 1f, e);

            yield return null;
        }

        if (_flashImg) _flashImg.color = Color.white;

        foreach (var (rt, _, _, _) in shards)
            if (rt != null) Destroy(rt.gameObject);
        if (ringGo != null) Destroy(ringGo);
    }

    // Zero out controller delays so FeedbackThenNext adds no extra wait
    static void ZeroControllerDelays()
    {
        TongHopConfig.Current.feedbackDelayCorrect = 0f;
        TongHopConfig.Current.feedbackDelayWrong   = 0f;
        TongHopConfig.Current.nextQuestionDelay    = 0;
    }

    // ── Flash → white → countdown 3 2 1 → fade out ───────────────────────────

    IEnumerator FlashToWhiteAndCountdown(bool skipFadeIn = false)
    {
        _flashImg.raycastTarget = true;
        float t = 0f;
        if (!skipFadeIn)
        {
            // Fade to white quickly
            while (t < 1f)
            {
                t += Time.deltaTime / 0.25f;
                _flashImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
        }
        _flashImg.color = Color.white;

        // Countdown on white — dark text so it's readable
        _countdownTxt.color = new Color(0.12f, 0.12f, 0.18f);
        for (int i = 3; i >= 1; i--)
        {
            _countdownTxt.text = $"Next in {i}...";
            yield return new WaitForSeconds(1f);
        }
        _countdownTxt.text  = "";
        _countdownTxt.color = Color.white; // reset

        // Fade back to clear
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.20f;
            _flashImg.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t));
            yield return null;
        }
        _flashImg.color = Color.clear;
        _flashImg.raycastTarget = false;
    }

    // ── Meteor ────────────────────────────────────────────────────────────────

    IEnumerator MeteorFly(int col, int row)
    {
        var tex = Resources.Load<Texture2D>("Space/1");

        // Cluster container
        var cluster = new GameObject("MeteorCluster");
        cluster.transform.SetParent(transform, false);
        var cRt = cluster.AddComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = Vector2.zero;
        cRt.pivot     = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = Vector2.zero;

        // Main meteor 3× size (62*3=186), 4 smaller satellites around it.
        // No rotation — keep upright as in the source image.
        (float sz, Vector2 off)[] defs = {
            (186f, new Vector2(  0f,   0f)),  // main
            ( 55f, new Vector2(-50f,  40f)),  // upper-left
            ( 42f, new Vector2( 40f,  35f)),  // upper-right
            ( 32f, new Vector2( -12f, 55f)),  // lower-right
            ( 26f, new Vector2(20f, 62f)),  // lower-left
        };

        var anims = new Coroutine[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var (sz, off) = defs[i];
            var go = new GameObject($"M{i}");
            go.transform.SetParent(cluster.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = off;
            rt.sizeDelta        = new Vector2(sz, sz);

            if (tex != null)
            {
                var ri = go.AddComponent<RawImage>();
                ri.texture = tex; ri.raycastTarget = false;
            }
            else
            {
                var img = go.AddComponent<Image>();
                img.sprite = _circleSpr;
                img.color  = new Color(1f, 0.45f, 0.08f);
                img.raycastTarget = false;
            }

            // Flame-flicker only (scale pulse) — no rotation
            float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var capturedRt = rt;
            anims[i] = StartCoroutine(FlickerMeteor(capturedRt, phase));
        }

        // Fall from top — 3s. Play highDown so it finishes exactly at impact.
        Vector2 startPos = new Vector2(_center[col, row].x, CanvasH + 100f);
        Vector2 endPos   = _center[col, row];
        float dur         = 3f;
        float clipLen     = MusicManager.Instance != null ? MusicManager.Instance.MeteorFallClipLength : 0f;
        float soundDelay  = Mathf.Max(0f, dur - clipLen);
        float elapsed     = 0f;
        bool  soundPlayed = false;
        float t = 0f;
        while (t < 1f)
        {
            t       += Time.deltaTime / dur;
            elapsed += Time.deltaTime;
            float e  = Mathf.Pow(Mathf.Clamp01(t), 1.8f);
            cRt.anchoredPosition = Vector2.Lerp(startPos, endPos, e);

            if (!soundPlayed && elapsed >= soundDelay)
            {
                soundPlayed = true;
                MusicManager.Instance?.PlayMeteorFallSfx();
            }

            yield return null;
        }

        foreach (var a in anims) if (a != null) StopCoroutine(a);
        Destroy(cluster);
    }

    // Flame-flicker: scale pulse only, no rotation
    IEnumerator FlickerMeteor(RectTransform rt, float phase)
    {
        float flickSpeed = UnityEngine.Random.Range(4f, 8f);
        float elapsed    = 0f;
        while (rt != null)
        {
            elapsed += Time.deltaTime;
            float s = 1f + 0.08f * Mathf.Sin(elapsed * flickSpeed + phase);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ── Astronaut ─────────────────────────────────────────────────────────────

    void MoveAstronaut(Vector2 target)
    {
        if (_astronautAnim != null) StopCoroutine(_astronautAnim);
        _astronautAnim = StartCoroutine(AnimAstronaut(_astronautPos, target));
    }

    IEnumerator AnimAstronaut(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        if (dir.sqrMagnitude > 0.01f)
        {
            // Image points right at 0° — no offset needed
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _astronautRt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        float dur = 0.4f, t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            _astronautRt.anchoredPosition = Vector2.Lerp(from, to, e);
            yield return null;
        }
        _astronautRt.anchoredPosition = to;
        _astronautPos = to;
        _astronautRt.localRotation = Quaternion.Euler(0f, 0f, 0f); // back to original angle
        _astronautAnim = null;
    }

    void ResetAstronaut()
    {
        if (_astronautAnim != null) { StopCoroutine(_astronautAnim); _astronautAnim = null; }
        if (_astronautRt == null) return;
        Vector2 resetPos = AstronautStartPos;
        _astronautRt.anchoredPosition = resetPos;
        _astronautRt.localRotation    = Quaternion.Euler(0f, 0f, 0f);
        _astronautPos = resetPos;
    }

    // ── Ring helpers ──────────────────────────────────────────────────────────

    static GameObject MakeRingContainer(string name, Transform parent, Vector2 offset, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = Vector2.zero;
        go.transform.localRotation = rot;
        return go;
    }

    static void AddRingBands(Transform parent, float ringW, bool isFront)
    {
        float m = isFront ? 0.36f : 1.0f;
        Ellipse(parent, ringW,          11f * m, new Color(0.80f, 0.68f, 0.42f, 0.65f));
        Ellipse(parent, ringW * 0.83f,   7f * m, new Color(CardBg.r, CardBg.g, CardBg.b, 1f));
        Ellipse(parent, ringW * 0.76f,  20f * m, new Color(0.96f, 0.90f, 0.64f, 0.92f));
        Ellipse(parent, ringW * 0.56f,  11f * m, new Color(CardBg.r, CardBg.g, CardBg.b, 1f));
        Ellipse(parent, ringW * 0.50f,  12f * m, new Color(0.70f, 0.58f, 0.36f, 0.62f));
    }

    static void Ellipse(Transform parent, float w, float h, Color c)
    {
        var go = new GameObject("E");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.sprite = _circleSpr; img.color = c; img.raycastTarget = false;
    }

    // ── General helpers ───────────────────────────────────────────────────────

    static GameObject MakeRect(string name, Transform parent, float x, float y, float w, float h, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot     = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        var img = go.AddComponent<Image>();
        img.color = col; img.raycastTarget = false;
        return go;
    }

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
}
