using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller chính: spawn hành tinh, xử lý tap, hiển thị info panel.
/// Gắn lên GameObject "SolarSystemController" trong scene.
/// </summary>
public class SolarSystemController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] PlanetData[] planetDataList;

    [Header("Prefabs & References")]
    [SerializeField] GameObject      planetPrefab;    // Sphere + PlanetOrbit
    [SerializeField] Transform       sunTransform;
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

    readonly List<PlanetOrbit> _planets = new();
    PlanetOrbit                _focusedOrbit;
    Camera                     _mainCam;

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
    }

    void SpawnPlanets()
    {
        if (planetPrefab == null || planetDataList == null) return;

        foreach (var data in planetDataList)
        {
            if (data == null) continue;

            var go = Instantiate(planetPrefab);
            go.name = data.planetName;
            go.layer = LayerMask.NameToLayer("Planet");

            // Load texture
            var tex = Resources.Load<Texture2D>(data.texturePath);
            if (tex != null)
                go.GetComponent<Renderer>().material.mainTexture = tex;

            var orbit = go.AddComponent<PlanetOrbit>();
            orbit.Init(data, sunTransform);
            _planets.Add(orbit);
        }
    }

    void Update()
    {
        HandleTap();
    }

    void HandleTap()
    {
        // Chỉ xử lý tap đơn (1 ngón, không phải pinch)
        bool tapped = false;
        Vector2 tapPos = Vector2.zero;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { tapped = true; tapPos = Input.mousePosition; }
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
        { tapped = true; tapPos = Input.GetTouch(0).position; }
#endif
        if (!tapped) return;

        // Bỏ qua nếu tap vào UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject((int)(tapped ? -1 : 0)))
            return;

        Ray ray = _mainCam.ScreenPointToRay(tapPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, LayerMask.GetMask("Planet")))
        {
            var orbit = hit.transform.GetComponent<PlanetOrbit>();
            if (orbit != null) FocusPlanet(orbit);
        }
        else
        {
            CloseInfoPanel();
        }
    }

    void FocusPlanet(PlanetOrbit orbit)
    {
        _focusedOrbit = orbit;
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
    }

    void CloseInfoPanel()
    {
        infoPanel?.SetActive(false);
        _focusedOrbit = null;
    }

    void OnSpeedChanged(float val)
    {
        PlanetOrbit.TimeScale = val;
        if (speedLabel != null) speedLabel.text = $"{val:F1}x";
    }
}
