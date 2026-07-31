using UnityEngine;

/// <summary>
/// Gắn lên mỗi planet/moon object. Quay quanh tâm + tự quay quanh trục nghiêng.
/// Hỗ trợ ApplyScale() để HUD toggle giữa tỉ lệ mô phỏng và tỉ lệ thật.
/// </summary>
public class PlanetOrbit : MonoBehaviour
{
    public PlanetData Data { get; private set; }

    Transform    _sun;
    float        _orbitAngle;
    GameObject   _orbitLineGo;
    LineRenderer _orbitLr;
    GameObject   _glowLineGo;   // glow ring (rộng hơn, trong suốt)
    LineRenderer _glowLr;

    // Runtime values — thay đổi khi toggle tỉ lệ
    float _orbitR;   // orbit radius hiện tại (đơn vị Unity)
    float _scale;    // object scale hiện tại

    public static float TimeScale = 1f;

    /// <summary>
    /// Khóa thủy triều: 1 mặt luôn hướng về _sun (cha).
    /// Mặt Trăng thực tế bị khóa với Trái Đất — luôn thấy cùng 1 mặt.
    /// </summary>
    public bool TidallyLocked { get; set; }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(PlanetData data, Transform sun, bool showOrbitLine = true)
    {
        Data        = data;
        _sun        = sun;
        _orbitAngle = Random.Range(0f, 360f);
        _orbitR     = data.orbitRadius;
        _scale      = data.scale;

        if (data.axialTilt != 0f)
            transform.rotation = Quaternion.AngleAxis(data.axialTilt, Vector3.right);

        transform.position   = CalculateOrbitPosition(_orbitAngle);
        transform.localScale = Vector3.one * _scale;

        if (showOrbitLine && _orbitR > 0f)
            DrawOrbitLine();
    }

    // ── Orbit line visibility ─────────────────────────────────────────────────

    /// <summary>Ẩn/hiện đường quỹ đạo — dùng khi camera focus vào planet.</summary>
    public void SetOrbitLineVisible(bool visible)
    {
        if (_orbitLineGo != null) _orbitLineGo.SetActive(visible);
        if (_glowLineGo  != null) _glowLineGo.SetActive(visible);
    }

    // ── Scale toggle (gọi từ SolarSystemHUD) ─────────────────────────────────

    /// <summary>Đặt tỉ lệ mới. orbitR = 0 → ẩn object (vd: Moon trong real scale).</summary>
    public void ApplyScale(float orbitR, float objScale)
    {
        if (orbitR <= 0f)
        {
            gameObject.SetActive(false);
            if (_orbitLineGo != null) _orbitLineGo.SetActive(false);
            if (_glowLineGo  != null) _glowLineGo.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        if (_orbitLineGo != null) _orbitLineGo.SetActive(true);
        if (_glowLineGo  != null) _glowLineGo.SetActive(true);
        _orbitR = orbitR;
        _scale  = objScale;
        transform.localScale = Vector3.one * _scale;
        RebuildOrbitLine();
    }

    /// <summary>Khôi phục về tỉ lệ mô phỏng gốc (lấy từ Data).</summary>
    public void ResetToSimScale()
    {
        gameObject.SetActive(true);
        _orbitR = Data.orbitRadius;
        _scale  = Data.scale;
        transform.localScale = Vector3.one * _scale;
        RebuildOrbitLine();
    }

    // ── Orbit line ────────────────────────────────────────────────────────────

    void RebuildOrbitLine()
    {
        if (_orbitLineGo != null) { Destroy(_orbitLineGo); _orbitLineGo = null; _orbitLr = null; }
        if (_glowLineGo  != null) { Destroy(_glowLineGo);  _glowLineGo  = null; _glowLr  = null; }
        if (_orbitR > 0f) DrawOrbitLine();
    }

    void DrawOrbitLine()
    {
        // ── Shader fallback (Android-safe) ───────────────────────────────────
        var shader = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Unlit/Transparent")
                  ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning($"[PlanetOrbit] No shader for orbit line ({Data.planetName}) — skipping.");
            return;
        }

        const int   segments = 128;
        const float initW    = 0.24f; // 3× original 0.08

        // ── Inner orbit ring ─────────────────────────────────────────────────
        _orbitLineGo = new GameObject($"{Data.planetName}_Orbit");
        _orbitLineGo.transform.position = _sun != null ? _sun.position : Vector3.zero;

        _orbitLr                   = _orbitLineGo.AddComponent<LineRenderer>();
        _orbitLr.useWorldSpace     = false;
        _orbitLr.loop              = true;
        _orbitLr.positionCount     = segments;
        _orbitLr.startWidth        = initW;
        _orbitLr.endWidth          = initW;
        _orbitLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _orbitLr.receiveShadows    = false;

        var matInner = new Material(shader);
        matInner.color = new Color(0.75f, 0.88f, 1f, 0.60f); // blue-white, 60% alpha
        _orbitLr.material = matInner;

        // ── Glow ring (rộng hơn × 7, rất trong suốt) ─────────────────────────
        _glowLineGo = new GameObject($"{Data.planetName}_Glow");
        _glowLineGo.transform.position = _orbitLineGo.transform.position;

        _glowLr                   = _glowLineGo.AddComponent<LineRenderer>();
        _glowLr.useWorldSpace     = false;
        _glowLr.loop              = true;
        _glowLr.positionCount     = segments;
        _glowLr.startWidth        = initW * 7f;
        _glowLr.endWidth          = initW * 7f;
        _glowLr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _glowLr.receiveShadows    = false;

        var matGlow = new Material(shader);
        matGlow.color = new Color(0.6f, 0.8f, 1f, 0.09f); // hào quang mờ
        _glowLr.material = matGlow;

        // ── Vẽ điểm (dùng chung cho cả 2 ring) ──────────────────────────────
        var tilt = Quaternion.AngleAxis(Data.orbitInclination, Vector3.right);
        for (int i = 0; i < segments; i++)
        {
            float rad  = (i / (float)segments) * 2f * Mathf.PI;
            var   flat = new Vector3(Mathf.Cos(rad) * _orbitR, 0f, Mathf.Sin(rad) * _orbitR);
            var   pos  = Data.orbitInclination != 0f ? tilt * flat : flat;
            _orbitLr.SetPosition(i, pos);
            _glowLr.SetPosition(i, pos);
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_sun == null || Data == null) return;

        if (_orbitLineGo != null)
        {
            _orbitLineGo.transform.position = _sun.position;
            if (_orbitLr != null)
            {
                // 3× original — tỉ lệ với khoảng cách camera để trông nhất quán
                float w = Mathf.Clamp(SolarCameraController.CurrentDistance * 0.006f, 0.03f, 150f);
                _orbitLr.startWidth = _orbitLr.endWidth = w;
            }
        }
        if (_glowLineGo != null)
        {
            _glowLineGo.transform.position = _sun != null ? _sun.position : Vector3.zero;
            if (_glowLr != null)
            {
                float wGlow = Mathf.Clamp(SolarCameraController.CurrentDistance * 0.042f, 0.21f, 1050f);
                _glowLr.startWidth = _glowLr.endWidth = wGlow;
            }
        }

        _orbitAngle += Data.orbitSpeed * TimeScale * Time.deltaTime;
        if (_orbitAngle > 360f) _orbitAngle -= 360f;
        transform.position = CalculateOrbitPosition(_orbitAngle);

        if (TidallyLocked)
        {
            // Khóa thủy triều: +Z luôn hướng về _sun (Trái Đất với Mặt Trăng)
            Vector3 toParent = (_sun.position - transform.position).normalized;
            if (toParent.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toParent, Vector3.up);
        }
        else
        {
            transform.Rotate(Vector3.up, Data.selfRotateSpeed * TimeScale * Time.deltaTime, Space.Self);
        }
    }

    // ── Position calc ─────────────────────────────────────────────────────────

    Vector3 CalculateOrbitPosition(float angleDeg)
    {
        float rad  = angleDeg * Mathf.Deg2Rad;
        var   flat = new Vector3(Mathf.Cos(rad) * _orbitR, 0f, Mathf.Sin(rad) * _orbitR);
        if (Data.orbitInclination != 0f)
            flat = Quaternion.AngleAxis(Data.orbitInclination, Vector3.right) * flat;
        return _sun != null ? _sun.position + flat : flat;
    }
}
