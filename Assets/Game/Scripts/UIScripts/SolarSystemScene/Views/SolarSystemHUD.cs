using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// HUD overlay cho SolarSystem scene.
/// Gán các Button / TMP label từ scene vào Inspector — script chỉ wire logic.
/// </summary>
public class SolarSystemHUD : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] SolarCameraController cam;
    [SerializeField] SolarSystemSpawner    spawner;

    [Header("D-Pad")]
    [SerializeField] Button btnUp;
    [SerializeField] Button btnDown;
    [SerializeField] Button btnLeft;
    [SerializeField] Button btnRight;

    [Header("Speed Bar")]
    [SerializeField] Button            btnPause;
    [SerializeField] TextMeshProUGUI   lblPause;     // text trên nút pause ("||" / ">")
    [SerializeField] Button            btnSpeedDown;
    [SerializeField] Button            btnSpeedUp;
    [SerializeField] TextMeshProUGUI   lblSpeed;     // hiển thị tốc độ hiện tại

    [Header("Zoom")]
    [SerializeField] Button btnZoomIn;
    [SerializeField] Button btnZoomOut;

    [Header("Top-Right")]
    [SerializeField] Button          btnRestart;
    [SerializeField] Button          btnFocusEarth;  // toggle: Earth ↔ toàn cảnh
    [SerializeField] Button          btnToggleScale;
    [SerializeField] TextMeshProUGUI lblScaleMode;   // "Mô phỏng" / "Tỉ lệ thật"

    [Header("Video giới thiệu không gian")]
    [SerializeField] Button btnPlaySpaceVideo;
    [Tooltip("Tên file trong Assets/StreamingAssets/Video/ (không có .mp4) — bản gốc \"Space\", bản SolarSystemVi đổi thành \"Space_Vi\".")]
    [SerializeField] string spaceVideoName = "Space";

    // ── State ─────────────────────────────────────────────────────────────────
    bool  _paused;
    float _speedAtPause = 1f;
    int   _speedIdx     = 10;   // SpeedSteps[10] = 1×
    bool  _realScale;
    bool  _focusingEarth;

    static readonly float[] SpeedSteps =
        { -8f, -4f, -2f, -1f, -0.5f, -0.25f, -0.1f,
           0.1f, 0.25f, 0.5f, 1f, 2f, 4f, 8f };

    // ── Real scale data ────────────────────────────────────────────────────────
    static readonly Dictionary<string, (float orbit, float size)> RealData =
        new Dictionary<string, (float, float)>
    {
        { "Sun",     (0f,       7.5f)    },
        { "Mercury", (312f,     0.0263f) },
        { "Venus",   (583f,     0.0652f) },
        { "Earth",   (806f,     0.0686f) },
        { "Mars",    (1228f,    0.0365f) },
        { "Jupiter", (4192f,    0.753f)  },
        { "Saturn",  (7683f,    0.627f)  },
        { "Uranus",  (15458f,   0.273f)  },
        { "Neptune", (24223f,   0.265f)  },
        // Moon quanh Earth: orbit 2.07 units, size 0.0187 (tỉ lệ thật so với Earth orbit 806)
        { "Moon",    (2.07f,    0.0187f) },
    };

    static readonly float[] SimZoomLevels  = {   5f,  10f,   20f,   40f,   80f,  120f,  160f,   300f };
    static readonly float[] RealZoomLevels = {   5f,  10f, 1000f, 5000f, 10000f, 15000f, 20000f, 30000f };

    // ── Init ──────────────────────────────────────────────────────────────────

    void Start()
    {
        if (cam == null)     cam     = FindObjectOfType<SolarCameraController>();
        if (spawner == null) spawner = FindObjectOfType<SolarSystemSpawner>();

        PlanetOrbit.TimeScale = 1f;
        _paused       = false;
        _speedIdx     = 10;
        _realScale    = false;
        _focusingEarth = false;

        cam?.SetZoomPresets(SimZoomLevels, 3);

        WireButtons();
        RefreshSpeedLabel();
        RefreshPauseLabel();
    }

    void WireButtons()
    {
        Btn(btnUp,          () => cam?.AddPhi( 5f));
        Btn(btnDown,        () => cam?.AddPhi(-5f));
        Btn(btnLeft,        () => cam?.AddTheta(-10f));
        Btn(btnRight,       () => cam?.AddTheta( 10f));

        Btn(btnPause,       TogglePause);
        Btn(btnSpeedDown,   SpeedDown);
        Btn(btnSpeedUp,     SpeedUp);

        Btn(btnZoomIn,      () => cam?.ZoomStep(-1));
        Btn(btnZoomOut,     () => cam?.ZoomStep( 1));

        Btn(btnRestart,     RestartScene);
        Btn(btnFocusEarth,  ToggleFocusEarth);
        Btn(btnToggleScale, ToggleScale);
        Btn(btnPlaySpaceVideo, () => NativeVideoPlayer.Play(spaceVideoName));
    }

    // ── Logic: Pause / Speed ──────────────────────────────────────────────────

    void TogglePause()
    {
        _paused = !_paused;
        if (_paused) { _speedAtPause = PlanetOrbit.TimeScale; PlanetOrbit.TimeScale = 0f; }
        else           PlanetOrbit.TimeScale = _speedAtPause;
        RefreshPauseLabel();
    }

    void SpeedDown() { if (_speedIdx > 0)                      _speedIdx--; ApplySpeed(); }
    void SpeedUp()   { if (_speedIdx < SpeedSteps.Length - 1)  _speedIdx++; ApplySpeed(); }

    void ApplySpeed()
    {
        float s = SpeedSteps[_speedIdx];
        _speedAtPause = s;
        if (!_paused) PlanetOrbit.TimeScale = s;
        RefreshSpeedLabel();
    }

    void RefreshSpeedLabel()
    {
        if (lblSpeed != null)
            lblSpeed.text = $"{SpeedSteps[_speedIdx]:0.##}×";
    }

    void RefreshPauseLabel()
    {
        if (lblPause != null)
            lblPause.text = _paused ? ">" : "||";
    }

    // ── Logic: Focus / Reset / Restart ───────────────────────────────────────

    void ToggleFocusEarth()
    {
        var earth = FindPlanetOrbit("Earth");
        if (!_focusingEarth)
        {
            if (earth != null)
            {
                float focusDist = earth.transform.localScale.x * 6f;
                if (_realScale)
                {
                    cam?.SetNearClip(0.001f);
                    cam?.SetMinDistance(0.001f);
                }
                cam?.FocusOnPlanet(earth.transform, focusDist);
                // Ẩn orbit ring của Earth khi đang focus — tránh visual artifact
                earth.SetOrbitLineVisible(false);
                _focusingEarth = true;
            }
        }
        else
        {
            cam?.RestoreNearClip();
            cam?.RestoreMinDistance();
            cam?.ResetView();
            // Khôi phục orbit ring
            earth?.SetOrbitLineVisible(true);
            _focusingEarth = false;
        }
    }

    void RestartScene() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    // ── Logic: Scale toggle ───────────────────────────────────────────────────

    void ToggleScale()
    {
        _realScale = !_realScale;
        if (lblScaleMode != null)
            lblScaleMode.text = _realScale ? "Tỉ lệ thật" : "Mô phỏng";

        foreach (var orbit in FindObjectsOfType<PlanetOrbit>())
        {
            if (orbit.Data == null) continue;
            if (_realScale)
            {
                if (RealData.TryGetValue(orbit.Data.planetName, out var d))
                    orbit.ApplyScale(d.orbit, d.size);
            }
            else
            {
                orbit.ResetToSimScale();
            }
        }

        var belt = GameObject.Find("AsteroidBelt");
        if (belt != null) belt.SetActive(!_realScale);

        // Tăng range ánh sáng Mặt Trời để chiếu tới tất cả hành tinh ở tỉ lệ thật
        spawner?.SetRealScaleLight(_realScale);

        cam?.SetZoomPresets(_realScale ? RealZoomLevels : SimZoomLevels, _realScale ? 2 : 3);
        cam?.ResetView();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static PlanetOrbit FindPlanetOrbit(string planetName)
    {
        foreach (var o in FindObjectsOfType<PlanetOrbit>())
            if (o.Data != null && o.Data.planetName == planetName) return o;
        return null;
    }

    void Btn(Button btn, Action cb)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => { cb(); StartCoroutine(Cooldown(btn)); });
    }

    IEnumerator Cooldown(Button btn)
    {
        btn.interactable = false;
        yield return new WaitForSeconds(0.4f);
        btn.interactable = true;
    }
}
