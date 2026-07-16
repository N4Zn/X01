using UnityEngine;
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Reset static để tránh TimeScale = 0 còn sót từ lần chạy trước (HUD.Start cũng reset)
        PlanetOrbit.TimeScale = 1f;
        MusicManager.Instance?.PlaySolarSystemMusic();
        SpawnAll();
    }

    void Update()
    {
        if (sunTransform != null)
            sunTransform.Rotate(Vector3.up,
                sunRotateSpeed * PlanetOrbit.TimeScale * Time.deltaTime, Space.World);

        if (_asteroidBeltTransform != null)
            _asteroidBeltTransform.Rotate(Vector3.up,
                asteroidOrbitSpeed * PlanetOrbit.TimeScale * Time.deltaTime, Space.World);
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
        Destroy(go.GetComponent<SphereCollider>());
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
        Destroy(moonGo.GetComponent<SphereCollider>());

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
        moonData.planetNameVi    = "Mặt Trăng";
        moonData.orbitRadius     = moonOrbitRadius;
        moonData.orbitSpeed      = moonOrbitSpeed;
        moonData.selfRotateSpeed = 0f;   // không dùng — khóa thủy triều xử lý rotation
        moonData.scale           = moonScale;
        moonData.axialTilt       = 0f;   // LookRotation tự xử lý hướng, axialTilt sẽ ghi đè

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
