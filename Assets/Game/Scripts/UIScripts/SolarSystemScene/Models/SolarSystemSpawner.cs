using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Reflection;

public class SolarSystemSpawner : MonoBehaviour
{
    [Header("Data & References")]
    [SerializeField] PlanetData[] planets;
    [SerializeField] Transform    sunTransform;

    [Header("Mặt Trời")]
    [SerializeField, Range(1f, 30f)]    float sunRotateSpeed        = 12f;
    [SerializeField, Range(0.5f, 6f)]   float sunHaloSizeMultiplier = 2.5f;
    [SerializeField]                    Color sunHaloColor           = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField, Range(50f, 1000f)] float sunPointLightRange     = 400f;
    [SerializeField, Range(0.1f, 15f)]  float sunPointLightIntensity = 5f;

    [Header("Mặt Trăng")]
    [SerializeField, Range(1f, 15f)]  float moonOrbitRadius  = 4.5f;
    [SerializeField, Range(5f, 120f)] float moonOrbitSpeed   = 35f;
    [SerializeField, Range(0.1f, 3f)] float moonScale        = 0.65f;
    [SerializeField, Range(0f, 40f)]  float moonSelfRotSpeed = 8f;
    [SerializeField, Range(0f, 30f)]  float moonAxialTilt    = 6.7f;

    [Header("Vành Tiểu Hành Tinh")]
    [SerializeField, Range(14f, 22f)] float asteroidInnerR       = 18.5f;
    [SerializeField, Range(18f, 28f)] float asteroidOuterR       = 22.0f;
    [SerializeField, Range(0.1f, 4f)] float asteroidHeightSpread = 0.8f;
    [SerializeField, Range(50, 600)]  int   asteroidCount        = 280;
    [SerializeField, Range(0.1f, 5f)] float asteroidOrbitSpeed   = 1.8f;

    [Header("Bầu Trời")]
    [SerializeField, Range(0.05f, 5f)] float skyboxExposure = 1.4f;

    static readonly Color[] FallbackColors =
    {
        new Color(0.60f, 0.60f, 0.60f), new Color(0.85f, 0.70f, 0.30f),
        new Color(0.20f, 0.50f, 0.85f), new Color(0.80f, 0.30f, 0.15f),
        new Color(0.80f, 0.60f, 0.40f), new Color(0.90f, 0.85f, 0.60f),
        new Color(0.40f, 0.85f, 0.90f), new Color(0.20f, 0.35f, 0.85f),
    };

    int       _colorIndex            = 0;
    Transform _earthTransform        = null;
    Transform _asteroidBeltTransform = null;
    Light     _sunLight              = null;

    // ── Tap + Info panel ──────────────────────────────────────────────────────
    Camera          _mainCam;
    GameObject      _infoPanel;
    TextMeshProUGUI _infoPlanetName, _infoDesc, _infoStats;
    Button          _btnWatchVideo;
    PlanetData      _currentData;
    // Video modal
    GameObject      _videoModal;
    TextMeshProUGUI _loadingLabel;
    RawImage        _videoImage;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        PlanetOrbit.TimeScale = 1f;
        MusicManager.Instance?.PlaySolarSystemMusic();
        _mainCam = Camera.main ?? FindObjectOfType<Camera>();
        SpawnAll();
        WireBtnBack();
    }

    void Update()
    {
        if (sunTransform != null)
            sunTransform.Rotate(Vector3.up,
                sunRotateSpeed * PlanetOrbit.TimeScale * Time.deltaTime, Space.World);

        if (_asteroidBeltTransform != null)
            _asteroidBeltTransform.Rotate(Vector3.up,
                asteroidOrbitSpeed * PlanetOrbit.TimeScale * Time.deltaTime, Space.World);

        HandleTap();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void SpawnAll()
    {
        if (planets == null) return;
        _colorIndex = 0;

        foreach (var data in planets)
        {
            if (data == null) continue;
            try
            {
                if (data.isSun) { SetupSun(data); continue; }
                Debug.Log($"[SolarSystem] Spawn {data.planetName} | orbitR={data.orbitRadius} scale={data.scale}");
                var orbit = SpawnPlanet(data);
                if (data.planetName == "Earth") _earthTransform = orbit.transform;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SolarSystem] FAILED to spawn '{data.planetName}': {e.Message}\n{e.StackTrace}");
            }
        }

        // Fallback: tìm Earth theo tên nếu chưa được gán trong vòng lặp
        if (_earthTransform == null)
        {
            var earthGo = GameObject.Find("Earth");
            if (earthGo != null) { _earthTransform = earthGo.transform; Debug.Log("[SolarSystem] Earth found via fallback Find"); }
            else Debug.LogError("[SolarSystem] Earth not found — Moon will not spawn!");
        }

        if (_earthTransform != null) SpawnMoon(_earthTransform);
        SpawnAsteroidBelt();
        SetupSkybox();
        BuildInfoPanel();
    }

    // ── Mặt Trời ─────────────────────────────────────────────────────────────

    void SetupSun(PlanetData data)
    {
        if (sunTransform == null) return;
        sunTransform.localScale = Vector3.one * data.scale;

        var sunRenderer = sunTransform.GetComponent<Renderer>();
        if (sunRenderer != null)
        {
            var mat = new Material(sunRenderer.sharedMaterial ?? new Material(Shader.Find("Standard")));
            var tex = Resources.Load<Texture2D>(data.texturePath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", tex);
                mat.SetColor("_EmissionColor", Color.white * 1.8f);
            }
            else
            {
                mat.color = new Color(1f, 0.85f, 0.1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0f) * 2f);
            }
            mat.SetFloat("_Glossiness", 0f);
            sunRenderer.sharedMaterial = mat;
        }

        // Shadow quality: đặt trước khi assign light để có hiệu lực ngay
        QualitySettings.shadows         = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance  = 500f;   // đủ lớn để cover toàn hệ mô phỏng

        foreach (var l in FindObjectsOfType<Light>())
        {
            if (l.type == LightType.Point)
            {
                _sunLight              = l;
                l.range                = sunPointLightRange;
                l.intensity            = sunPointLightIntensity;
                l.color                = new Color(1f, 0.95f, 0.85f);
                // Shadow: Soft + resolution cao nhất để tránh bóng răng cưa
                l.shadows              = LightShadows.Soft;
                l.shadowStrength       = 1.0f;
                l.shadowCustomResolution = 4096;  // override VeryHigh → cube face 4096px
                l.shadowBias           = 0.02f;   // tránh self-shadow acne
                l.shadowNormalBias     = 0.1f;    // tránh bóng dính vào bề mặt
            }
        }

        // Ambient cực thấp: khi Moon nằm sau Earth (nguyệt thực) → Moon tối hẳn
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.04f, 0.06f);

        // Sun dot marker: chấm sáng scale theo camera để luôn visible ở tỉ lệ thật
        var dotGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotGo.name = "SunDot";
        dotGo.transform.position = Vector3.zero;
        Destroy(dotGo.GetComponent<SphereCollider>());
        var dotShader = Shader.Find("Unlit/Color") ?? Shader.Find("Unlit/Transparent");
        if (dotShader == null) { Destroy(dotGo); dotGo = null; Debug.LogWarning("[SolarSystem] SunDot shader not found"); }
        if (dotGo != null)
        {
            var dotMat = new Material(dotShader);
            dotMat.color = new Color(1f, 0.95f, 0.65f);
            var dotMr = dotGo.GetComponent<MeshRenderer>();
            dotMr.sharedMaterial    = dotMat;
            dotMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotMr.receiveShadows    = false;
            dotGo.AddComponent<SunMarker>();
        }

        // Unity built-in Halo — internal class, phải dùng Type.GetType (không dùng <Halo>)
        var haloType = System.Type.GetType("UnityEngine.Halo, UnityEngine");
        if (haloType != null)
        {
            var halo = sunTransform.gameObject.AddComponent(haloType) as Behaviour;
            if (halo != null)
            {
                try
                {
                    var t = halo.GetType();
                    t.GetField("color", BindingFlags.Public | BindingFlags.Instance)
                     ?.SetValue(halo, sunHaloColor);
                    t.GetField("size", BindingFlags.Public | BindingFlags.Instance)
                     ?.SetValue(halo, data.scale * sunHaloSizeMultiplier);
                }
                catch { }
            }
        }

        // Make Sun tappable — add PlanetOrbit with null parent so it never moves
        if (sunTransform.GetComponent<Collider>() == null)
            sunTransform.gameObject.AddComponent<SphereCollider>();

        if (sunTransform.GetComponent<PlanetOrbit>() == null)
        {
            // Copy educational data; zero out orbit so Sun stays at origin
            var sunInfo = ScriptableObject.CreateInstance<PlanetData>();
            sunInfo.planetName      = data.planetName;
            sunInfo.planetNameVi    = string.IsNullOrEmpty(data.planetNameVi) ? "Mặt Trời" : data.planetNameVi;
            sunInfo.descriptionVi   = data.descriptionVi;
            sunInfo.distanceFromSun = data.distanceFromSun;
            sunInfo.diameter        = data.diameter;
            sunInfo.numberOfMoons   = data.numberOfMoons;
            sunInfo.surfaceTemp     = data.surfaceTemp;
            sunInfo.videoPath       = data.videoPath;
            sunInfo.scale           = data.scale;
            sunInfo.orbitRadius     = 0f;  // không quỹ đạo
            sunInfo.orbitSpeed      = 0f;
            sunTransform.gameObject.AddComponent<PlanetOrbit>().Init(sunInfo, null, false);
        }
    }

    // ── Light scale API ───────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ SolarSystemHUD khi toggle tỉ lệ thật/mô phỏng.
    /// Real scale: Earth ở 806 units, Neptune ở 24223 units — range phải lớn hơn con số này.
    /// </summary>
    public void SetRealScaleLight(bool realScale)
    {
        if (_sunLight == null) return;
        // Range: real scale cần cover Neptune 24223 units; sim scale 400 units đủ
        _sunLight.range = realScale ? 30000f : sunPointLightRange;
        // Intensity: real scale giảm 5× (tránh lóa khi nhìn gần Earth)
        _sunLight.intensity = realScale ? sunPointLightIntensity * 0.2f : sunPointLightIntensity;
    }

    // ── Bầu Trời ─────────────────────────────────────────────────────────────

    void SetupSkybox()
    {
        var tex = Resources.Load<Texture2D>("SolarSystem/Textures/2k_stars_milky_way")
               ?? Resources.Load<Texture2D>("SolarSystem/Textures/2k_stars");

        float exposure = skyboxExposure;
        if (tex == null)
        {
            tex      = GenerateStarTexture(2048, 3000);
            exposure = 1f; // texture tự sinh đã đúng độ sáng, không cần dim
        }

        var skyShader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Skybox/Cubemap");
        if (skyShader == null) { Debug.LogWarning("[SolarSystem] Skybox shader not found — skipping skybox"); return; }
        var skyMat = new Material(skyShader);
        skyMat.SetTexture("_MainTex",   tex);
        skyMat.SetFloat("_Mapping",    1f);   // 1 = Latitude Longitude (equirectangular)
        skyMat.SetFloat("_ImageType",  0f);   // 0 = 360 Degrees (full sphere)
        skyMat.SetFloat("_Exposure",   exposure);
        skyMat.SetFloat("_Rotation",   0f);
        skyMat.SetColor("_Tint",       Color.white);

        RenderSettings.skybox = skyMat;
        DynamicGI.UpdateEnvironment();

        var cam = Camera.main ?? FindObjectOfType<Camera>();
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
    }

    // Sinh panorama equirectangular (2:1) với sao ngẫu nhiên — dùng khi không có file texture
    static Texture2D GenerateStarTexture(int width, int starCount)
    {
        int height = width / 2;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var px  = new Color32[width * height];
        var rng = new System.Random(42);

        for (int i = 0; i < starCount; i++)
        {
            int  x      = rng.Next(width);
            int  y      = rng.Next(height);
            byte bright = (byte)(120 + rng.Next(136)); // 120–255

            // Tint nhẹ: trắng / xanh lạnh / vàng ấm
            byte r = bright, g = bright, b = bright;
            int  tint = rng.Next(3);
            if (tint == 1) r = (byte)(bright * 0.82f);                    // cool blue
            if (tint == 2) b = (byte)(bright * 0.72f);                    // warm yellow

            // Sao sáng: hào quang mờ 3×3 xung quanh
            if (bright > 200)
            {
                byte dim = (byte)(bright / 4);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = Mathf.Clamp(x + dx, 0, width  - 1);
                    int ny = Mathf.Clamp(y + dy, 0, height - 1);
                    px[ny * width + nx] = new Color32(dim, dim, dim, 255);
                }
            }
            px[y * width + x] = new Color32(r, g, b, 255);
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    // ── Vành Tiểu Hành Tinh ──────────────────────────────────────────────────

    void SpawnAsteroidBelt()
    {
        var beltGo = new GameObject("AsteroidBelt");
        _asteroidBeltTransform = beltGo.transform;

        var sharedMat = new Material(Shader.Find("Standard"));
        sharedMat.color = new Color(0.48f, 0.43f, 0.38f);
        sharedMat.SetFloat("_Glossiness", 0f);
        sharedMat.SetFloat("_Metallic",   0f);

        for (int i = 0; i < asteroidCount; i++)
        {
            var ast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ast.name = "Asteroid";
            ast.transform.SetParent(beltGo.transform, false);
            Destroy(ast.GetComponent<Collider>());

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Random.Range(asteroidInnerR, asteroidOuterR);
            float y     = Random.Range(-asteroidHeightSpread, asteroidHeightSpread);
            ast.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);

            float sc = Random.Range(0.04f, 0.16f);
            ast.transform.localScale = new Vector3(
                sc * Random.Range(0.6f, 1.4f),
                sc * Random.Range(0.6f, 1.4f),
                sc * Random.Range(0.6f, 1.4f));
            ast.transform.localRotation = Random.rotation;
            ast.GetComponent<MeshRenderer>().sharedMaterial = sharedMat;
        }
    }

    // ── Hành Tinh ─────────────────────────────────────────────────────────────

    PlanetOrbit SpawnPlanet(PlanetData data)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = data.planetName;
        // Keep SphereCollider for tap/raycast detection — do NOT destroy it
        go.transform.localScale = Vector3.one * data.scale;

        var pShader = Shader.Find("Standard") ?? Shader.Find("Diffuse") ?? Shader.Find("Unlit/Color");
        var mat = new Material(pShader != null ? pShader : Shader.Find("Unlit/Color"));
        if (pShader != null && pShader.name != "Unlit/Color") mat.SetFloat("_Glossiness", 0.1f);

        var tex = Resources.Load<Texture2D>(data.texturePath);
        if (tex != null) mat.mainTexture = tex;
        else
        {
            Color c = _colorIndex < FallbackColors.Length ? FallbackColors[_colorIndex] : Color.gray;
            mat.color = c;
        }
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial    = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows    = true;
        _colorIndex++;

        if (data.hasSaturnRings) AddRings(go.transform, data);

        var orbit = go.AddComponent<PlanetOrbit>();
        orbit.Init(data, sunTransform);
        return orbit;
    }

    // ── Mặt Trăng ─────────────────────────────────────────────────────────────

    void SpawnMoon(Transform earthTransform)
    {
        Debug.Log($"[SolarSystem] Spawning Moon (earthTransform={earthTransform?.name})");
        var moonGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moonGo.name = "Moon";
        // Keep SphereCollider for tap detection

        var mShader = Shader.Find("Standard") ?? Shader.Find("Diffuse") ?? Shader.Find("Unlit/Color");
        var mat = new Material(mShader ?? Shader.Find("Unlit/Color"));
        if (mShader != null && mShader.name == "Standard") mat.SetFloat("_Glossiness", 0.05f);
        var tex = Resources.Load<Texture2D>("SolarSystem/Textures/2k_moon");
        if (tex != null) mat.mainTexture = tex;
        else mat.color = new Color(0.72f, 0.72f, 0.68f);
        var moonMr = moonGo.GetComponent<MeshRenderer>();
        moonMr.sharedMaterial    = mat;
        moonMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        moonMr.receiveShadows    = true;   // cần để nhận bóng tối từ Trái Đất (nguyệt thực)

        var moonData = ScriptableObject.CreateInstance<PlanetData>();
        moonData.planetName      = "Moon";
        moonData.planetNameVi    = "Moon";
        moonData.descriptionVi   = "Earth's only natural satellite, 384,400 km away. Its gravitational pull drives ocean tides and stabilizes Earth's axial tilt.";
        moonData.distanceFromSun = "384,400 km";
        moonData.diameter        = "3,474 km";
        moonData.numberOfMoons   = 0;
        moonData.surfaceTemp     = "-173°C to 127°C";
        moonData.orbitRadius     = moonOrbitRadius;
        moonData.orbitSpeed      = moonOrbitSpeed;
        moonData.selfRotateSpeed = 0f;
        moonData.scale           = moonScale;
        moonData.axialTilt       = 0f;

        var orbit = moonGo.AddComponent<PlanetOrbit>();
        orbit.Init(moonData, earthTransform);
        orbit.TidallyLocked = true;      // Mặt Trăng bị khóa thủy triều với Trái Đất
    }

    // ── Vành Đai Sao Thổ ─────────────────────────────────────────────────────

    void AddRings(Transform planet, PlanetData data)
    {
        var ringGo = new GameObject("Rings");
        ringGo.transform.SetParent(planet, false);
        ringGo.transform.localPosition = Vector3.zero;
        ringGo.transform.localRotation = Quaternion.identity;
        ringGo.transform.localScale    = Vector3.one;

        var mf = ringGo.AddComponent<MeshFilter>();
        var mr = ringGo.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mf.mesh = CreateRingMesh(0.6f, 1.3f, 128);

        // Ring dùng unlit để tránh tối do normal vuông góc với ánh sáng
        var rShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Color");
        if (rShader == null) { Debug.LogWarning("[SolarSystem] No shader for Saturn rings"); return; }
        var mat = new Material(rShader);
        var ringTex = Resources.Load<Texture2D>("SolarSystem/Textures/2k_saturn_ring_alpha");
        if (ringTex != null)
        {
            mat.mainTexture = ringTex;
            mat.color = new Color(1f, 0.93f, 0.72f, 0.85f);  // warm tint, hơi trong
        }
        else
        {
            mat.color = new Color(0.88f, 0.78f, 0.58f, 0.75f);  // golden fallback
        }
        mat.renderQueue = 3000;
        mr.sharedMaterial = mat;
    }

    static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments = 128)
    {
        var mesh      = new Mesh();
        var vertices  = new Vector3[segments * 2];
        var uvs       = new Vector2[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            vertices[i*2]   = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
            uvs[i*2]        = new Vector2(0f, i / (float)segments);
            vertices[i*2+1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
            uvs[i*2+1]      = new Vector2(1f, i / (float)segments);
            int next = (i + 1) % segments, t = i * 6;
            triangles[t]=i*2; triangles[t+1]=i*2+1; triangles[t+2]=next*2;
            triangles[t+3]=next*2; triangles[t+4]=i*2+1; triangles[t+5]=next*2+1;
        }
        mesh.vertices = vertices; mesh.uv = uvs; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // ── Back button ──────────────────────────────────────────────────────────

    void WireBtnBack()
    {
        var go = GameObject.Find("BtnBack");
        if (go == null) return;

        // Rename label to English
        var txt = go.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = "← Back";

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SceneManager.LoadScene("MenuScene"));
        }
    }

    // ── Tap detection ────────────────────────────────────────────────────────

    void HandleTap()
    {
        bool    tapped    = false;
        Vector2 tapPos    = Vector2.zero;
        int     pointerId = -1;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { tapped = true; tapPos = Input.mousePosition; }
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            var t = Input.GetTouch(0);
            tapped = true; tapPos = t.position; pointerId = t.fingerId;
        }
#endif
        if (!tapped) return;

        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject(pointerId)) return;

        if (_mainCam == null) return;
        var ray = _mainCam.ScreenPointToRay(tapPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            var orbit = hit.transform.GetComponent<PlanetOrbit>()
                     ?? hit.transform.GetComponentInParent<PlanetOrbit>();
            if (orbit != null && orbit.Data != null)
            {
                ShowPlanetInfo(orbit.Data);
                return;
            }
        }
        HidePlanetInfo();
    }

    // ── Info panel (built at runtime) ────────────────────────────────────────

    void BuildInfoPanel()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        _infoPanel = new GameObject("PlanetInfoPanel");
        _infoPanel.transform.SetParent(canvas.transform, false);

        var rt = _infoPanel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);
        rt.sizeDelta        = new Vector2(270f, 320f);
        rt.anchoredPosition = new Vector2(-8f, 0f);

        var bg = _infoPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.18f, 0.94f);
        bg.raycastTarget = true;

        // Close button
        var closeGo = new GameObject("BtnClose");
        closeGo.transform.SetParent(_infoPanel.transform, false);
        var closeRt = closeGo.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-4f, -4f);
        closeRt.sizeDelta = new Vector2(32f, 32f);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.80f, 0.18f, 0.10f, 0.85f);
        var clLblGo = new GameObject("X"); clLblGo.transform.SetParent(closeGo.transform, false);
        var clRt = clLblGo.AddComponent<RectTransform>();
        clRt.anchorMin = Vector2.zero; clRt.anchorMax = Vector2.one;
        clRt.offsetMin = clRt.offsetMax = Vector2.zero;
        var clTxt = clLblGo.AddComponent<TextMeshProUGUI>();
        clTxt.text = "X"; clTxt.fontSize = 18f; clTxt.fontStyle = FontStyles.Bold;
        clTxt.color = Color.white; clTxt.alignment = TextAlignmentOptions.Center;
        clTxt.raycastTarget = false;
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(HidePlanetInfo);

        // Planet name
        var nameGo = new GameObject("PlanetName");
        nameGo.transform.SetParent(_infoPanel.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f); nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.anchoredPosition = new Vector2(0f, -8f);
        nameRt.sizeDelta = new Vector2(-16f, 72f);
        _infoPlanetName = nameGo.AddComponent<TextMeshProUGUI>();
        _infoPlanetName.enableAutoSizing = true;
        _infoPlanetName.fontSizeMin = 16f;
        _infoPlanetName.fontSizeMax = 38f;
        _infoPlanetName.fontStyle = FontStyles.Bold;
        _infoPlanetName.color = new Color(1f, 0.87f, 0.30f);
        _infoPlanetName.alignment = TextAlignmentOptions.Center;
        _infoPlanetName.raycastTarget = false;

        // Description
        var descGo = new GameObject("Desc");
        descGo.transform.SetParent(_infoPanel.transform, false);
        var descRt = descGo.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 1f); descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 1f);
        descRt.anchoredPosition = new Vector2(0f, -88f);
        descRt.sizeDelta = new Vector2(-16f, 80f);
        _infoDesc = descGo.AddComponent<TextMeshProUGUI>();
        _infoDesc.enableAutoSizing = true;
        _infoDesc.fontSizeMin = 10f;
        _infoDesc.fontSizeMax = 22f;
        _infoDesc.fontStyle = FontStyles.Bold;
        _infoDesc.color = new Color(0.82f, 0.88f, 1f);
        _infoDesc.alignment = TextAlignmentOptions.TopLeft;
        _infoDesc.enableWordWrapping = true;
        _infoDesc.raycastTarget = false;

        // Stats
        var statsGo = new GameObject("Stats");
        statsGo.transform.SetParent(_infoPanel.transform, false);
        var statsRt = statsGo.AddComponent<RectTransform>();
        statsRt.anchorMin = new Vector2(0f, 1f); statsRt.anchorMax = new Vector2(1f, 1f);
        statsRt.pivot = new Vector2(0.5f, 1f);
        statsRt.anchoredPosition = new Vector2(0f, -176f);
        statsRt.sizeDelta = new Vector2(-16f, 100f);
        _infoStats = statsGo.AddComponent<TextMeshProUGUI>();
        _infoStats.enableAutoSizing = true;
        _infoStats.fontSizeMin = 10f;
        _infoStats.fontSizeMax = 22f;
        _infoStats.fontStyle = FontStyles.Bold;
        _infoStats.color = new Color(0.75f, 0.85f, 1f);
        _infoStats.alignment = TextAlignmentOptions.TopLeft;
        _infoStats.enableWordWrapping = true;
        _infoStats.raycastTarget = false;

        // Watch Video button
        var vidBtnGo = new GameObject("BtnWatchVideo");
        vidBtnGo.transform.SetParent(_infoPanel.transform, false);
        var vidBtnRt = vidBtnGo.AddComponent<RectTransform>();
        vidBtnRt.anchorMin = new Vector2(0f, 0f); vidBtnRt.anchorMax = new Vector2(1f, 0f);
        vidBtnRt.pivot = new Vector2(0.5f, 0f);
        vidBtnRt.anchoredPosition = new Vector2(0f, 8f);
        vidBtnRt.sizeDelta = new Vector2(-16f, 36f);
        var vidBtnImg = vidBtnGo.AddComponent<Image>();
        vidBtnImg.color = new Color(0.10f, 0.45f, 0.90f, 0.92f);
        var vidLblGo = new GameObject("Lbl"); vidLblGo.transform.SetParent(vidBtnGo.transform, false);
        var vidLblRt = vidLblGo.AddComponent<RectTransform>();
        vidLblRt.anchorMin = Vector2.zero; vidLblRt.anchorMax = Vector2.one;
        vidLblRt.offsetMin = vidLblRt.offsetMax = Vector2.zero;
        var vidLbl = vidLblGo.AddComponent<TextMeshProUGUI>();
        vidLbl.text = "> Play Video"; vidLbl.fontSize = 16f;
        vidLbl.fontStyle = FontStyles.Bold; vidLbl.color = Color.white;
        vidLbl.alignment = TextAlignmentOptions.Center; vidLbl.raycastTarget = false;
        _btnWatchVideo = vidBtnGo.AddComponent<Button>();
        _btnWatchVideo.targetGraphic = vidBtnImg;
        _btnWatchVideo.onClick.AddListener(OpenVideoModal);

        _infoPanel.SetActive(false);
        BuildVideoModal(canvas);
    }

    void ShowPlanetInfo(PlanetData data)
    {
        _currentData = data;
        if (_infoPanel == null) return;
        _infoPanel.SetActive(true);

        if (_infoPlanetName != null)
        {
            bool hasVi = !string.IsNullOrEmpty(data.planetNameVi) && data.planetNameVi != data.planetName;
            _infoPlanetName.text = hasVi
                ? $"{data.planetName} ({data.planetNameVi})"
                : data.planetName;
        }

        if (_infoDesc != null)
            _infoDesc.text = data.descriptionVi;

        if (_infoStats != null)
        {
            bool isMoon = data.planetName == "Moon";
            bool isSun  = data.planetName == "Sun";
            var sb = new System.Text.StringBuilder();

            if (isSun)
                sb.AppendLine("Dist. from Milky Way:  ~26,000 light-years");
            else if (!string.IsNullOrEmpty(data.distanceFromSun))
                sb.AppendLine(isMoon
                    ? $"Dist. from Earth:  {data.distanceFromSun}"
                    : $"Distance from Sun:  {data.distanceFromSun}");

            if (!string.IsNullOrEmpty(data.diameter))    sb.AppendLine($"Diameter:  {data.diameter}");
            if (!isMoon && !isSun)                       sb.AppendLine($"Moons:  {data.numberOfMoons}");
            if (!string.IsNullOrEmpty(data.surfaceTemp)) sb.AppendLine($"Temp:  {data.surfaceTemp}");
            _infoStats.text = sb.ToString().TrimEnd();
        }

        if (_btnWatchVideo != null)
            _btnWatchVideo.gameObject.SetActive(!string.IsNullOrEmpty(data.videoPath));
    }

    void HidePlanetInfo()
    {
        _infoPanel?.SetActive(false);
        _currentData = null;
    }

    // ── Video modal ───────────────────────────────────────────────────────────

    void BuildVideoModal(Canvas canvas)
    {
        _videoModal = new GameObject("VideoModal");
        _videoModal.transform.SetParent(canvas.transform, false);

        var rt = _videoModal.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = _videoModal.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        bg.raycastTarget = true;

        // Loading label shown while copying video from APK
        var lblGo = new GameObject("LoadingLabel");
        lblGo.transform.SetParent(_videoModal.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0.1f, 0.4f); lblRt.anchorMax = new Vector2(0.9f, 0.6f);
        lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
        _loadingLabel = lblGo.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = "Đang tải video...";
        _loadingLabel.fontSize = 28f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.color = Color.white;
        _loadingLabel.alignment = TextAlignmentOptions.Center;
        _loadingLabel.raycastTarget = false;

        // Keep _videoImage reference (unused but field still declared)
        var vidGo = new GameObject("VideoImage");
        vidGo.transform.SetParent(_videoModal.transform, false);
        _videoImage = vidGo.AddComponent<RawImage>();
        _videoImage.color = Color.clear;

        // Cancel button
        var closeGo = new GameObject("BtnClose");
        closeGo.transform.SetParent(_videoModal.transform, false);
        var closeRt = closeGo.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(48f, 48f);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.85f, 0.15f, 0.10f, 0.90f);
        var clLblGo = new GameObject("X"); clLblGo.transform.SetParent(closeGo.transform, false);
        var clLblRt = clLblGo.AddComponent<RectTransform>();
        clLblRt.anchorMin = Vector2.zero; clLblRt.anchorMax = Vector2.one;
        clLblRt.offsetMin = clLblRt.offsetMax = Vector2.zero;
        var clTxt = clLblGo.AddComponent<TextMeshProUGUI>();
        clTxt.text = "X"; clTxt.fontSize = 22f; clTxt.fontStyle = FontStyles.Bold;
        clTxt.color = Color.white; clTxt.alignment = TextAlignmentOptions.Center;
        clTxt.raycastTarget = false;
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseVideoModal);

        _videoModal.SetActive(false);
    }

    void OpenVideoModal()
    {
        Debug.Log($"[SolarSystem] OpenVideoModal — data={_currentData?.videoPath ?? "NULL"}");
        if (_currentData == null || string.IsNullOrEmpty(_currentData.videoPath))
        {
            Debug.LogWarning("[SolarSystem] OpenVideoModal: _currentData null or videoPath empty — abort");
            return;
        }
        if (_loadingLabel) _loadingLabel.text = "Đang tải video...";
        _videoModal.SetActive(true);
        MusicManager.Instance?.SetMusicVolumeMultiplier(0.2f);
        NativeVideoPlayer.Play(_currentData.videoPath, CloseVideoModal);
    }

    void CloseVideoModal()
    {
        _videoModal?.SetActive(false);
        MusicManager.Instance?.SetMusicVolumeMultiplier(1f);
    }

    void OnDestroy() { }

    // ── Gizmos (Scene view) ───────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Vành tiểu hành tinh
        Gizmos.color = new Color(1f, 0.65f, 0.2f, 0.8f);
        DrawGizmoCircle(Vector3.zero, asteroidInnerR);
        DrawGizmoCircle(Vector3.zero, asteroidOuterR);
        Gizmos.color = new Color(1f, 0.65f, 0.2f, 0.06f);
        for (int i = 1; i <= 5; i++)
            DrawGizmoCircle(Vector3.zero, Mathf.Lerp(asteroidInnerR, asteroidOuterR, i / 6f));

        // Quỹ đạo Mặt Trăng — khi Play: theo Earth thực; khi Edit: ước lượng tại X = orbitRadius
        Vector3 earthPos = Vector3.zero;
        if (_earthTransform != null)
            earthPos = _earthTransform.position;
        else if (planets != null)
            foreach (var p in planets)
                if (p != null && p.planetName == "Earth") { earthPos = new Vector3(p.orbitRadius, 0f, 0f); break; }

        Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.55f);
        DrawGizmoCircle(earthPos, moonOrbitRadius);
        Gizmos.DrawWireSphere(earthPos, 0.5f);

        // Quỹ đạo các hành tinh (mờ)
        if (planets != null)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.08f);
            foreach (var p in planets)
                if (p != null && !p.isSun && p.orbitRadius > 0f)
                    DrawGizmoCircle(Vector3.zero, p.orbitRadius);
        }
    }

    static void DrawGizmoCircle(Vector3 center, float radius, int steps = 72)
    {
        for (int i = 0; i < steps; i++)
        {
            float a0 = i       / (float)steps * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius),
                center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius));
        }
    }
}
