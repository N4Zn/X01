using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller chính: spawn hành tinh, xử lý tap, hiển thị info panel + video modal.
/// </summary>
public class SolarSystemController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] PlanetData[] planetDataList;

    [Header("Prefabs & References")]
    [SerializeField] GameObject           planetPrefab;
    [SerializeField] Transform            sunTransform;
    [SerializeField] SolarCameraController cameraCtrl;

    [Header("Info Panel UI")]
    [SerializeField] GameObject      infoPanel;
    [SerializeField] TextMeshProUGUI infoPlanetName;
    [SerializeField] TextMeshProUGUI infoDescription;
    [SerializeField] TextMeshProUGUI infoDistance;
    [SerializeField] TextMeshProUGUI infoDiameter;
    [SerializeField] TextMeshProUGUI infoMoons;
    [SerializeField] TextMeshProUGUI infoTemp;
    [SerializeField] Button          btnCloseInfo;
    [SerializeField] Button          btnBack;
    [SerializeField] Button          btnResetView;

    [Header("Speed Control")]
    [SerializeField] Slider          speedSlider;
    [SerializeField] TextMeshProUGUI speedLabel;

    // ── Runtime state ──────────────────────────────────────────────────────────
    readonly List<PlanetOrbit> _planets = new();
    PlanetOrbit _focusedOrbit;
    Camera      _mainCam;

    // ── Video modal (built at runtime) ─────────────────────────────────────────
    GameObject      _videoModal;
    TextMeshProUGUI _loadingLabel;
    Button          _btnWatchVideo;
    PlanetData      _currentData;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        _mainCam = Camera.main;
        SpawnPlanets();

        btnCloseInfo?.onClick.AddListener(CloseInfoPanel);
        btnBack?.onClick.AddListener(() => SceneManager.LoadScene("MenuScene"));
        btnResetView?.onClick.AddListener(() =>
        {
            _focusedOrbit = null;
            cameraCtrl?.ResetView();
            CloseInfoPanel();
        });

        speedSlider?.onValueChanged.AddListener(OnSpeedChanged);
        if (speedSlider != null) speedSlider.value = 1f;

        infoPanel?.SetActive(false);

        BuildVideoModal();
    }

    // ── Spawn ──────────────────────────────────────────────────────────────────

    void SpawnPlanets()
    {
        if (planetPrefab == null || planetDataList == null) return;

        foreach (var data in planetDataList)
        {
            if (data == null) continue;

            var go = Instantiate(planetPrefab);
            go.name = data.planetName;

            // Assign layer if it exists; fallback gracefully
            int layerId = LayerMask.NameToLayer("Planet");
            if (layerId >= 0) go.layer = layerId;

            // Ensure collider exists for raycasting
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<SphereCollider>();

            var tex = Resources.Load<Texture2D>(data.texturePath);
            if (tex != null)
            {
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.mainTexture = tex;
            }

            var orbit = go.AddComponent<PlanetOrbit>();
            orbit.Init(data, sunTransform);
            _planets.Add(orbit);
        }
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    void Update()
    {
        HandleTap();
    }

    void HandleTap()
    {
        bool tapped = false;
        Vector2 tapPos = Vector2.zero;
        int pointerId = -1;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { tapped = true; tapPos = Input.mousePosition; pointerId = -1; }
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            var t = Input.GetTouch(0);
            tapped = true; tapPos = t.position; pointerId = t.fingerId;
        }
#endif
        if (!tapped) return;

        // Ignore taps on UI elements
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject(pointerId))
            return;

        Ray ray = _mainCam.ScreenPointToRay(tapPos);

        // No layer mask — check for PlanetOrbit component after hit
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            var orbit = hit.transform.GetComponent<PlanetOrbit>();
            if (orbit != null) { FocusPlanet(orbit); return; }
        }

        CloseInfoPanel();
    }

    // ── Info panel ─────────────────────────────────────────────────────────────

    void FocusPlanet(PlanetOrbit orbit)
    {
        _focusedOrbit = orbit;
        _currentData  = orbit.Data;
        cameraCtrl?.FocusOnPlanet(orbit.transform, orbit.Data.scale * 3f);
        ShowInfoPanel(orbit.Data);
    }

    void ShowInfoPanel(PlanetData data)
    {
        infoPanel?.SetActive(true);
        if (infoPlanetName  != null) infoPlanetName.text  = $"{data.planetNameVi}\n<size=70%>{data.planetName}</size>";
        if (infoDescription != null) infoDescription.text = data.descriptionVi;
        if (infoDistance    != null) infoDistance.text    = $"Khoảng cách Mặt Trời: {data.distanceFromSun}";
        if (infoDiameter    != null) infoDiameter.text    = $"Đường kính: {data.diameter}";
        if (infoMoons       != null) infoMoons.text       = $"Số mặt trăng: {data.numberOfMoons}";
        if (infoTemp        != null) infoTemp.text        = $"Nhiệt độ bề mặt: {data.surfaceTemp}";

        // Show "Watch Video" button only when a video file exists for this planet
        if (_btnWatchVideo != null)
            _btnWatchVideo.gameObject.SetActive(!string.IsNullOrEmpty(data.videoPath));
    }

    void CloseInfoPanel()
    {
        infoPanel?.SetActive(false);
        _focusedOrbit = null;
    }

    // ── Video modal ────────────────────────────────────────────────────────────

    void BuildVideoModal()
    {
        // Find the canvas that hosts infoPanel
        var canvas = infoPanel != null
            ? infoPanel.GetComponentInParent<Canvas>()
            : FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // ── "▶ Xem Video" button injected at the bottom of infoPanel ──────────
        if (infoPanel != null)
        {
            var btnGo = new GameObject("BtnWatchVideo");
            btnGo.transform.SetParent(infoPanel.transform, false);

            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot     = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 8f);
            btnRt.sizeDelta = new Vector2(-16f, 36f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.10f, 0.45f, 0.90f, 0.92f);

            var lblGo = new GameObject("Lbl");
            lblGo.transform.SetParent(btnGo.transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "▶  Xem Video";
            lbl.fontSize = 16f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;

            _btnWatchVideo = btnGo.AddComponent<Button>();
            _btnWatchVideo.targetGraphic = btnImg;
            _btnWatchVideo.onClick.AddListener(OpenVideoModal);
            btnGo.SetActive(false); // hidden until planet with video is selected
        }

        // ── Video modal overlay ───────────────────────────────────────────────
        _videoModal = new GameObject("VideoModal");
        _videoModal.transform.SetParent(canvas.transform, false);

        var modalRt = _videoModal.AddComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero; modalRt.anchorMax = Vector2.one;
        modalRt.offsetMin = modalRt.offsetMax = Vector2.zero;

        var modalBg = _videoModal.AddComponent<Image>();
        modalBg.color = new Color(0f, 0f, 0f, 0.94f);
        modalBg.raycastTarget = true;

        // Loading label
        var loadGo = new GameObject("LoadingLabel");
        loadGo.transform.SetParent(_videoModal.transform, false);
        var loadRt = loadGo.AddComponent<RectTransform>();
        loadRt.anchorMin = new Vector2(0.1f, 0.4f); loadRt.anchorMax = new Vector2(0.9f, 0.6f);
        loadRt.offsetMin = loadRt.offsetMax = Vector2.zero;
        _loadingLabel = loadGo.AddComponent<TextMeshProUGUI>();
        _loadingLabel.text = "Đang tải video...";
        _loadingLabel.fontSize = 28f;
        _loadingLabel.fontStyle = FontStyles.Bold;
        _loadingLabel.color = Color.white;
        _loadingLabel.alignment = TextAlignmentOptions.Center;
        _loadingLabel.raycastTarget = false;

        // Close button (top-right)
        var closeGo = new GameObject("BtnClose");
        closeGo.transform.SetParent(_videoModal.transform, false);
        var closeRt = closeGo.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot     = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(48f, 48f);

        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.85f, 0.15f, 0.10f, 0.90f);

        var closeLbl = new GameObject("X");
        closeLbl.transform.SetParent(closeGo.transform, false);
        var clRt = closeLbl.AddComponent<RectTransform>();
        clRt.anchorMin = Vector2.zero; clRt.anchorMax = Vector2.one;
        clRt.offsetMin = clRt.offsetMax = Vector2.zero;
        var clTxt = closeLbl.AddComponent<TextMeshProUGUI>();
        clTxt.text = "✕"; clTxt.fontSize = 22f; clTxt.fontStyle = FontStyles.Bold;
        clTxt.color = Color.white; clTxt.alignment = TextAlignmentOptions.Center;
        clTxt.raycastTarget = false;

        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseVideoModal);

        _videoModal.SetActive(false);
    }

    void OpenVideoModal()
    {
        Debug.Log($"[SolarSystemCtrl] OpenVideoModal — data={_currentData?.videoPath ?? "NULL"}");
        if (_currentData == null || string.IsNullOrEmpty(_currentData.videoPath))
        {
            Debug.LogWarning("[SolarSystemCtrl] OpenVideoModal: _currentData null or videoPath empty — abort");
            return;
        }
        if (_loadingLabel) _loadingLabel.text = "Đang tải video...";
        _videoModal.SetActive(true);
        NativeVideoPlayer.Play(_currentData.videoPath, CloseVideoModal);
    }

    void CloseVideoModal()
    {
        _videoModal?.SetActive(false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    void OnSpeedChanged(float val)
    {
        PlanetOrbit.TimeScale = val;
        if (speedLabel != null) speedLabel.text = $"{val:F1}x";
    }

    void OnDestroy() { }
}
