using UnityEngine;

/// <summary>
/// Camera touch control:
///   1 ngón kéo  → xoay quanh tâm (orbit)
///   2 ngón kẹp  → zoom in/out
/// D-Pad HUD dùng AddPhi / AddTheta (click).
/// Zoom HUD dùng ZoomStep (click, discrete levels).
///
/// Thứ tự mỗi frame (Unity):
///   1. PlanetOrbit.Update()  → planet di chuyển
///   2. SolarCameraController.Update() → input + distance lerp
///   3. Coroutine resume (FocusRoutine cập nhật _targetPos lerp)
///   4. SolarCameraController.LateUpdate() → tracking + ApplyCamera
///      → luôn đọc vị trí planet SAU khi chúng đã update → zero lag
/// </summary>
public class SolarCameraController : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField] Vector3 target           = Vector3.zero;
    [SerializeField] float   distance         = 30f;
    [SerializeField] float   minDistance      = 5f;
    [SerializeField] float   maxDistance      = 300f;
    [SerializeField] float   orbitSensitivity = 0.3f;
    [SerializeField] float   zoomSensitivity  = 0.05f;
    [SerializeField] float   smoothSpeed      = 8f;

    public float MaxDistance { get => maxDistance; set => maxDistance = value; }

    float     _theta = 45f;
    float     _phi   = 30f;
    float     _targetDist;
    float     _resetDist;       // distance khi reset về overview (set bởi SetZoomPresets)
    Vector3   _targetPos;
    bool      _isFocusing;
    float     _defaultNearClip;
    float     _defaultMinDist;
    Transform _trackedTarget;   // planet đang theo dõi (null = nhìn về tâm cố định)


    public static float CurrentDistance { get; private set; }

    void Awake()
    {
        _targetDist      = distance;
        _resetDist       = distance;
        _targetPos       = target;
        _defaultNearClip = GetComponent<Camera>().nearClipPlane;
        _defaultMinDist  = minDistance;
    }

    // ── Update: input + distance lerp (không gọi ApplyCamera ở đây) ──────────
    void Update()
    {
        if (!_isFocusing)
        {
#if UNITY_EDITOR
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        distance        = Mathf.Lerp(distance, _targetDist, Time.deltaTime * smoothSpeed);
        CurrentDistance = distance;
    }

    // ── LateUpdate: tracking + render (SAU khi mọi planet đã Update) ─────────
    void LateUpdate()
    {
        // Cập nhật _targetPos từ planet đang track — đây đã là vị trí frame hiện tại
        if (_trackedTarget != null)
            _targetPos = _trackedTarget.position;

        ApplyCamera();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                _theta -= t.deltaPosition.x * orbitSensitivity;
                _phi    = Mathf.Clamp(_phi + t.deltaPosition.y * orbitSensitivity, -30f, 89f);
            }
        }
        else if (Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            float curDist  = Vector2.Distance(t0.position, t1.position);
            float prevDist = Vector2.Distance(t0.position - t0.deltaPosition,
                                              t1.position - t1.deltaPosition);
            _targetDist = Mathf.Clamp(_targetDist + (prevDist - curDist) * zoomSensitivity,
                                      minDistance, maxDistance);
        }
    }

    void HandleMouseInput()
    {
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null
                   && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!overUI && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            _theta -= Input.GetAxis("Mouse X") * 3f;
            _phi    = Mathf.Clamp(_phi + Input.GetAxis("Mouse Y") * 3f, -30f, 89f);
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDist = Mathf.Clamp(_targetDist - scroll * 10f, minDistance, maxDistance);
    }

    // ── Camera position ───────────────────────────────────────────────────────

    void ApplyCamera()
    {
        float phiRad   = _phi   * Mathf.Deg2Rad;
        float thetaRad = _theta * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            distance * Mathf.Cos(phiRad) * Mathf.Sin(thetaRad),
            distance * Mathf.Sin(phiRad),
            distance * Mathf.Cos(phiRad) * Mathf.Cos(thetaRad)
        );

        transform.position = _targetPos + offset;
        transform.LookAt(_targetPos);
    }

    // ── Focus API ─────────────────────────────────────────────────────────────

    public void FocusOnPlanet(Transform planet, float focusDist = 5f)
    {
        if (_focusCoroutine != null) StopCoroutine(_focusCoroutine);
        _trackedTarget  = null;
        _focusCoroutine = StartCoroutine(FocusRoutine(planet, focusDist));
    }

    public void ResetView()
    {
        if (_focusCoroutine != null) { StopCoroutine(_focusCoroutine); _focusCoroutine = null; }
        _trackedTarget = null;
        _isFocusing    = false;
        _targetPos     = Vector3.zero;
        _targetDist    = _resetDist;
        _phi           = 30f;
    }

    // ── Camera config API ─────────────────────────────────────────────────────

    public void SetNearClip(float v)     => GetComponent<Camera>().nearClipPlane = v;
    public void RestoreNearClip()        => GetComponent<Camera>().nearClipPlane = _defaultNearClip;
    public void SetMinDistance(float v)  => minDistance = v;
    public void RestoreMinDistance()     => minDistance = _defaultMinDist;

    // ── D-Pad / Zoom API ──────────────────────────────────────────────────────

    public void AddPhi(float deg)        => _phi = Mathf.Clamp(_phi + deg, -30f, 89f);
    public void AddTheta(float deg)      => _theta += deg;
    public void SetTargetDist(float d)   => _targetDist = Mathf.Clamp(d, minDistance, maxDistance);

    /// <summary>Zoom in (dir=-1) / out (dir=+1) theo nhân đôi / chia đôi — không bị giới hạn bởi preset.</summary>
    public void ZoomStep(int dir)
    {
        _targetDist = Mathf.Clamp(_targetDist * (dir < 0 ? 0.5f : 2f), minDistance, maxDistance);
    }

    public void SetZoomPresets(float[] levels, int startIdx)
    {
        maxDistance = levels[levels.Length - 1];
        _resetDist  = levels[Mathf.Clamp(startIdx, 0, levels.Length - 1)];
        _targetDist = _resetDist;
        var camComp = GetComponent<Camera>();
        if (camComp != null)
            camComp.farClipPlane = maxDistance * 3f;
    }

    // ── Focus animation ───────────────────────────────────────────────────────

    Coroutine _focusCoroutine;

    System.Collections.IEnumerator FocusRoutine(Transform planet, float focusDist)
    {
        _isFocusing = true;
        float elapsed  = 0f, duration = 0.8f;
        Vector3 startPos  = _targetPos;
        float   startDist = distance;

        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            float t     = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            // Lerp đến vị trí HIỆN TẠI của planet (planet đang di chuyển trong khi bay)
            _targetPos  = Vector3.Lerp(startPos, planet.position, t);
            _targetDist = Mathf.Lerp(startDist, focusDist, t);
            distance    = _targetDist;
            // KHÔNG gọi ApplyCamera() — LateUpdate sẽ xử lý ở cuối frame
            yield return null;
        }

        _isFocusing    = false;
        _trackedTarget = planet;  // LateUpdate sẽ đọc planet.position mỗi frame
    }
}
