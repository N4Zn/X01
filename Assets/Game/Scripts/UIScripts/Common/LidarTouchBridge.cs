using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cầu nối Lidar → touch NGAY TRONG process Unity — không đi qua Android InputManager/
/// AccessibilityService/dispatchGesture/injectInputEvent. Đọc dữ liệu qua liblidar_unity.so
/// (native plugin port từ MyNativeApp_v4, xem NativePlugins/LidarUnity/) và tự bắn
/// PointerEventData vào EventSystem của scene hiện tại — y hệt 1 cú chạm chuột/tay thật,
/// nhưng nguồn là toạ độ Lidar. Không cần root, không cần quyền OS đặc biệt nào, vì không
/// còn "inject" sang tiến trình/app khác nữa.
///
/// Singleton&lt;T&gt; (cùng pattern với FaceRecognitionPlugin) — DontDestroyOnLoad, tự tạo
/// GameObject khi lần đầu chạm .Instance. Không cần kéo-thả gì trong Editor — chỉ cần 1 lần
/// chạm .Instance sớm (xem StartMainController.Awake(), y hệt cách FaceRecognitionPlugin
/// được warm-up) là đủ, pipeline USB+Lidar tự khởi động từ đó.
///
/// LidarUsbBridge.java (build từ NativePlugins/LidarNativeAndroidLib/, đóng gói thành
/// Assets/Plugins/Android/lidarlib-release.aar) gọi
/// UnitySendMessage("LidarTouchBridge", ...) theo TÊN GAMEOBJECT CỐ ĐỊNH — OnCreated() ép
/// tên GameObject về đúng "LidarTouchBridge" (đè tên mặc định "(singleton) LidarTouchBridge"
/// của Singleton&lt;T&gt;), nếu không callback từ Java sẽ rơi mất lặng lẽ.
/// </summary>
public class LidarTouchBridge : Singleton<LidarTouchBridge>
{
    private const string LibName = "lidar_unity";

    [DllImport(LibName)] private static extern void Lidar_SetUartFd(int fd);
    [DllImport(LibName)] private static extern void Lidar_Init();
    [DllImport(LibName)] private static extern void Lidar_SendConfig(
        int halfX, int hightFloor, int ymax, int shiftXFloor, int shiftY, int shiftX,
        int offsetAngle, int numsPointReport);
    [DllImport(LibName)] private static extern void Lidar_SetTouchEnabled(int enabled);
    [DllImport(LibName)] private static extern int Lidar_PollTouch(out int x, out int y);

    // Không gian tham chiếu native trả về — xem convert_to_1024x600() trong liblidar.cpp.
    // Trùng với Canvas Scaler reference resolution 1024x600 của toàn bộ UI (xem CLAUDE.md) —
    // nhờ vậy 1 native lib chạy đúng trên mọi độ phân giải display thật (tablet, máy chiếu...),
    // vì phép quy đổi cuối cùng sang Screen.width/height nằm ở RefToScreen() bên dưới.
    private const int RefWidth = 1024;
    private const int RefHeight = 600;

    // File JSON editable trên máy, KHÔNG cần rebuild app — giữ nguyên tên field/hành vi với
    // ConfigManager.java gốc (MyNativeApp_v4) để tooling/quy trình sửa file cũ dùng lại được:
    //   adb pull /sdcard/Android/data/<package>/files/lidar_config.json
    //   adb push lidar_config.json /sdcard/Android/data/<package>/files/
    // (rồi gọi LidarTouchBridge.Instance.ReloadConfig() hoặc restart app để áp dụng).
    // Field bên dưới là giá trị mặc định/hardcode — chỉ dùng khi CHƯA có file (lần chạy đầu,
    // file tự sinh ra với đúng các giá trị này). Cơ chế calib chỉnh trực quan (sau này) nên
    // ghi đè vào file này rồi gọi ReloadConfig(), không cần sửa field ở đây nữa.
    [System.Serializable]
    private class LidarConfigJson
    {
        public int half_x = 1000;
        public int hight_floor = 0;
        public int ymax = 1500;
        public int shift_x_floor = 0;
        public int shift_y = 0;
        public int shift_x = 0;
        public int offset_angle = 0;
        public int nums_point_report = 1;
    }

    private const string ConfigFileName = "lidar_config.json";
    private LidarConfigJson _config = new LidarConfigJson();

    // ── Calib "vùng tương tác" — hiệu chỉnh phần mềm, TÁCH RIÊNG khỏi lidar_config.json ────────
    // lidar_config.json (half_x/hight_floor/offset_angle/...) là hình học vật lý của cảm biến,
    // do kỹ thuật viên chỉnh 1 lần lúc lắp — KHÔNG đụng vào đây. Calib bên dưới là 1 phép biến
    // đổi affine áp SAU khi đã có toạ độ màn hình thô (RawRefToScreen), bù lệch/xoay/co giãn do
    // máy chiếu lắp đặt từng phòng khác nhau — giáo viên tự làm lại bất cứ lúc nào, không cần
    // hiểu gì về cảm biến. Xem CalibSceneController (Assets/Game/Scripts/UIScripts/Calib) cho
    // luồng UX: đặt 5 trụ xốp vào 4 góc + tâm vùng chiếu, bấm 1 nút, hệ thống tự nhận diện.
    [Serializable]
    private class CalibConfigJson
    {
        public bool calibrated = false;
        // corrected = M * raw (toạ độ MÀN HÌNH thô, không phải ref 1024x600) — identity mặc định.
        public float m00 = 1f, m01 = 0f, m02 = 0f;
        public float m10 = 0f, m11 = 1f, m12 = 0f;
    }

    private const string CalibFileName = "interaction_area_calib.json";
    private CalibConfigJson _calib = new CalibConfigJson();
    private string CalibFilePath => Path.Combine(Application.persistentDataPath, CalibFileName);

    private void LoadCalib()
    {
        try
        {
            if (!File.Exists(CalibFilePath))
            {
                Debug.Log("[LidarTouchBridge] LoadCalib: chưa calib lần nào — dùng identity (không hiệu chỉnh)");
                return;
            }
            _calib = JsonUtility.FromJson<CalibConfigJson>(File.ReadAllText(CalibFilePath));
            Debug.Log($"[LidarTouchBridge] LoadCalib: calibrated={_calib.calibrated} " +
                      $"m=[{_calib.m00:F4},{_calib.m01:F4},{_calib.m02:F1} / {_calib.m10:F4},{_calib.m11:F4},{_calib.m12:F1}]");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LidarTouchBridge] LoadCalib lỗi: {e.Message} — dùng identity");
            _calib = new CalibConfigJson();
        }
    }

    private void SaveCalib()
    {
        try
        {
            File.WriteAllText(CalibFilePath, JsonUtility.ToJson(_calib, true));
            Debug.Log($"[LidarTouchBridge] SaveCalib: đã lưu {CalibFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LidarTouchBridge] SaveCalib lỗi: {e.Message}");
        }
    }

    /// <summary>Bỏ calib hiện tại, quay về identity (raw, không hiệu chỉnh) — dùng khi kết quả
    /// calib mới tệ hơn hoặc muốn làm lại từ đầu.</summary>
    public void ClearCalibration()
    {
        _calib = new CalibConfigJson();
        SaveCalib();
    }

    public bool HasCalibration => _calib.calibrated;

    private Vector2 ApplyCalib(Vector2 rawScreenPos)
    {
        return new Vector2(
            _calib.m00 * rawScreenPos.x + _calib.m01 * rawScreenPos.y + _calib.m02,
            _calib.m10 * rawScreenPos.x + _calib.m11 * rawScreenPos.y + _calib.m12);
    }

    [Header("Nút cứng bật/tắt Lidar — van an toàn (Lidar có thể bắn touch rất nhanh/nhiều)")]
    [Tooltip("Đã xác nhận trên K02 thật (09/2026): nút cứng KEY_SELECT bắn ra cả JoystickButton0 " +
             "và Joystick1Button0 cùng lúc (Android map odm:gpio_key thành joystick-like device, " +
             "không phải keyboard) — dùng JoystickButton0 (aggregate, không phân biệt index) để bắt.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.JoystickButton0;
    [SerializeField] private float toggleDebounceMs = 300f;

    [Header("Debug — Show touch (thay cho \"Show taps\" của Android Developer Options, hiện KHÔNG dùng được)")]
    [SerializeField] private bool showTouchIndicator = true;
    [SerializeField] private float indicatorDiameter = 105f; // x1.5 so với 70f gốc
    [SerializeField] private float indicatorLifetime = 0.45f;
    [SerializeField] private Color indicatorColor = new Color(1f, 0.15f, 0.1f, 0.85f);

    // Mặc định OFF — khớp hành vi gốc (LidarService.touchEnabled AtomicBoolean(false) trong
    // MyNativeApp_v4): Lidar không tự sinh touch ngay khi kết nối, phải bấm nút cứng bật
    // trước. Native (g_touch_enabled{false} trong native-lib.cpp) cũng mặc định false sẵn,
    // field này chỉ theo dõi state phía C# để log đúng ON/OFF (native không có getter).
    private bool _touchEnabled = false;
    private float _lastToggleTime = -999f;

    private Canvas _debugCanvas;
    private Sprite _circleSprite;

    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();

    // Không dựa vào StartMainController.Awake() (StartScene) để warm-up — StartScene hiện
    // đang bị TẮT trong Build Settings (enabled: 0), MenuScene mới là scene chạy đầu tiên
    // thật sự trong build. RuntimeInitializeOnLoadMethod chạy 1 lần lúc app khởi động, không
    // phụ thuộc scene nào là scene đầu — đúng pattern MultiDisplaySetup.cs đã dùng trong
    // chính project này. Giữ nguyên dòng gọi trong StartMainController làm phòng hờ (vô hại,
    // .Instance idempotent) phòng khi StartScene được bật lại sau này.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "LidarTouchBridge"; // Singleton<T> đặt "(singleton) ..." — UnitySendMessage cần đúng tên này
        LoadConfig();
        LoadCalib();
    }

    private string ConfigFilePath => Path.Combine(Application.persistentDataPath, ConfigFileName);

    private void LoadConfig()
    {
        string path = ConfigFilePath;
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonUtility.ToJson(_config, true));
                Debug.Log($"[LidarTouchBridge] LoadConfig: chưa có file, tạo mặc định tại {path}");
                return;
            }

            string json = File.ReadAllText(path);
            _config = JsonUtility.FromJson<LidarConfigJson>(json);
            Debug.Log($"[LidarTouchBridge] LoadConfig: đã đọc {path} — half_x={_config.half_x} hight_floor={_config.hight_floor} " +
                      $"ymax={_config.ymax} shift_x_floor={_config.shift_x_floor} shift_y={_config.shift_y} shift_x={_config.shift_x} " +
                      $"offset_angle={_config.offset_angle} nums_point_report={_config.nums_point_report}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LidarTouchBridge] LoadConfig lỗi ({path}): {e.Message} — dùng giá trị mặc định");
            _config = new LidarConfigJson();
        }
    }

    /// <summary>
    /// Đọc lại file config từ đĩa và áp dụng ngay vào native (không cần restart app) — dùng
    /// cho cơ chế calib sau này (UI chỉnh tay → ghi file → gọi hàm này). An toàn gọi bất cứ
    /// lúc nào, kể cả trước khi Lidar đã kết nối (khi đó chỉ cập nhật _config, Lidar_SendConfig
    /// sẽ dùng giá trị mới ở lần OnUartFdReady/gọi lại tiếp theo).
    /// </summary>
    public void ReloadConfig()
    {
        LoadConfig();
        if (_uartReady) ApplyConfigToNative();
    }

    private void ApplyConfigToNative()
    {
        Lidar_SendConfig(_config.half_x, _config.hight_floor, _config.ymax, _config.shift_x_floor,
            _config.shift_y, _config.shift_x, _config.offset_angle, _config.nums_point_report);
    }

    private bool _uartReady;

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var bridgeClass = new AndroidJavaClass("com.eduxplore.lidar.LidarUsbBridge"))
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            bridgeClass.CallStatic("start", activity);
        }
#endif
    }

    /// <summary>
    /// Gọi từ Java (LidarUsbBridge.sendToUnity qua UnitySendMessage) khi pipe đọc USB đã sẵn
    /// sàng. fd truyền dạng string vì UnitySendMessage chỉ nhận được string.
    /// </summary>
    public void OnUartFdReady(string fdStr)
    {
        if (!int.TryParse(fdStr, out int fd))
        {
            Debug.LogError($"[LidarTouchBridge] OnUartFdReady: fd không hợp lệ '{fdStr}'");
            return;
        }

        Lidar_SetUartFd(fd);
        ApplyConfigToNative();
        _uartReady = true;
        Lidar_SetTouchEnabled(_touchEnabled ? 1 : 0); // mặc định OFF — xem _touchEnabled ở trên
        Lidar_Init();
        Debug.Log($"[LidarTouchBridge] Đã khởi tạo pipeline Lidar, fd={fd}, touchEnabled={_touchEnabled}");
    }

    /// <summary>Bật/tắt touch Lidar — dùng cho Pause/Resume từ control panel (Track B) hoặc nút cứng.</summary>
    public void SetTouchEnabled(bool enabled)
    {
        _touchEnabled = enabled;
        Lidar_SetTouchEnabled(enabled ? 1 : 0);
        Debug.Log($"[LidarTouchBridge] SetTouchEnabled → {(enabled ? "ON" : "OFF")}");
    }

    void Update()
    {
        HandleHardwareToggleKey();

        // Rút hết điểm đang chờ mỗi frame (queue tối đa 32 điểm — xem native-lib.cpp).
        while (Lidar_PollTouch(out int refX, out int refY) != 0)
        {
            // Chế độ calib đang lắng nghe (StartCalibrationCapture) — gom điểm THÔ (chưa hiệu
            // chỉnh) vào buffer riêng, KHÔNG dispatch thành tap UI/hiện vòng tròn đỏ bình
            // thường, để không vô tình bấm trúng nút trên CalibScene trong lúc đang đo.
            if (_capturing)
            {
                _captureBuffer.Add(RawRefToScreen(refX, refY));
                continue;
            }

            Vector2 screenPos = ApplyCalib(RawRefToScreen(refX, refY));
            Debug.Log($"[LidarTouchBridge] Poll: ref=({refX},{refY}) → screen=({screenPos.x:F0},{screenPos.y:F0}) Screen=({Screen.width}x{Screen.height})");
            if (showTouchIndicator)
            {
                try { ShowTouchIndicator(screenPos); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            DispatchTap(screenPos);
        }
    }

    // Thay thế KeyService.onKeyEvent() (AccessibilityService) của MyNativeApp_v4 — bản gốc
    // chặn phím ở tầng OS toàn cục vì service đó không có Activity riêng. X01 CÓ Activity
    // riêng (UnityPlayerActivity) nên dùng thẳng Input.GetKeyDown() là đủ, KHÔNG cần
    // AccessibilityService nữa — miễn UnityPlayerActivity đang có focus (đúng với single-
    // display; với chế độ 2-display sau này (Track A) cần verify lại phím cứng có tới đúng
    // Activity đang giữ focus hay không).
    void HandleHardwareToggleKey()
    {
        if (!Input.GetKeyDown(toggleKey)) return;
        if (Time.unscaledTime - _lastToggleTime < toggleDebounceMs / 1000f) return;
        _lastToggleTime = Time.unscaledTime;

        SetTouchEnabled(!_touchEnabled);
    }

    /// <summary>Quy đổi toạ độ ref (1024x600) của native sang pixel màn hình THÔ — CHƯA áp
    /// calib "vùng tương tác" (xem ApplyCalib). Dùng trực tiếp cho capture buffer lúc calib
    /// (cần toạ độ thô để giải ma trận hiệu chỉnh, không phải toạ độ đã hiệu chỉnh).</summary>
    private static Vector2 RawRefToScreen(int refX, int refY)
    {
        float sx = (float)refX / RefWidth * Screen.width;
        float sy = (float)refY / RefHeight * Screen.height;
        // Native trả toạ độ kiểu evdev/Android (gốc trên-trái, Y tăng xuống); EventSystem
        // của Unity dùng toạ độ màn hình gốc dưới-trái (Y tăng lên) — cần lật Y.
        sy = Screen.height - sy;
        return new Vector2(sx, sy);
    }

    /// <summary>Mô phỏng LẠI ĐÚNG công thức convert_to_1024x600() của native (liblidar.cpp) bằng
    /// C#, dùng _config (half_x/hight_floor/ymax/shift_x_floor/shift_x/shift_y) đang áp dụng —
    /// để biết "nếu có 1 điểm thật ở toạ độ (mmX,mmY) theo hệ trục LiDAR thì nó sẽ hiện ra ở đâu
    /// trên màn hình thô (chưa hiệu chỉnh calib)". Dùng cho bộ lọc gợi ý vị trí (xem
    /// FilterNearHints) — KHÔNG dùng để tính calib trực tiếp, vì bản thân _config có thể đang
    /// lệch (đó chính là lý do cần calib) — chỉ dùng để KHOANH VÙNG lọc nhiễu xa (vd tường).</summary>
    private Vector2 MmToRawScreen(float mmX, float mmY)
    {
        float xmin = -_config.half_x + _config.shift_x_floor;
        float widthFloor = 2f * _config.half_x;
        float ymin = _config.ymax - _config.hight_floor;

        float p100x = (mmX - (xmin + _config.shift_x_floor)) / widthFloor * RefWidth + _config.shift_x;
        float p100y = RefHeight - (mmY - ymin) / _config.hight_floor * RefHeight + _config.shift_y;

        return RawRefToScreen(Mathf.RoundToInt(p100x), Mathf.RoundToInt(p100y));
    }

    /// <summary>Lọc bớt điểm thô ở QUÁ XA mọi gợi ý vị trí (targets có expectedRawMm) trước khi
    /// gom cụm — tránh nhiễu ở xa (tường, vật cản khác trong phòng nhỏ) bị coi là "cụm ổn định"
    /// thay vì trụ thật. Target nào chưa có expectedRawMm (chưa đo) thì KHÔNG góp phần lọc —
    /// nếu KHÔNG target nào có gợi ý, trả về nguyên vẹn danh sách (giữ hành vi cũ, không lọc).</summary>
    private List<Vector2> FilterNearHints(List<Vector2> rawPoints, IEnumerable<Vector2?> hintsMm, float windowRadiusMm)
    {
        var windows = new List<(float minX, float maxX, float minY, float maxY)>();
        foreach (var hint in hintsMm)
        {
            if (hint == null) continue;
            Vector2 c1 = MmToRawScreen(hint.Value.x - windowRadiusMm, hint.Value.y - windowRadiusMm);
            Vector2 c2 = MmToRawScreen(hint.Value.x + windowRadiusMm, hint.Value.y + windowRadiusMm);
            var win = (Mathf.Min(c1.x, c2.x), Mathf.Max(c1.x, c2.x), Mathf.Min(c1.y, c2.y), Mathf.Max(c1.y, c2.y));
            windows.Add(win);
            Debug.Log($"[LidarTouchBridge] FilterNearHints: gợi ý mm({hint.Value.x:F0},{hint.Value.y:F0}) → cửa sổ màn hình thô " +
                      $"x=[{win.Item1:F0},{win.Item2:F0}] y=[{win.Item3:F0},{win.Item4:F0}]");
        }
        if (windows.Count == 0) return rawPoints; // chưa target nào đo mm — giữ hành vi cũ

        var result = new List<Vector2>();
        foreach (var p in rawPoints)
        {
            foreach (var w in windows)
            {
                if (p.x >= w.minX && p.x <= w.maxX && p.y >= w.minY && p.y <= w.maxY) { result.Add(p); break; }
            }
        }
        return result;
    }

    // Sai lệch cho phép quanh toạ độ mm dự kiến — theo đề xuất thực tế (200-300mm), lấy giữa.
    private const float HintWindowRadiusMm = 250f;

    // Down + Up ngay lập tức tại vị trí Lidar phát hiện — chỉ touch điểm rời rạc, không có
    // drag/swipe (độ phân giải Lidar hiện tại không đủ cho việc đó, đã thống nhất bỏ qua).
    private static void DispatchTap(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[LidarTouchBridge][DEBUG] DispatchTap: EventSystem.current NULL — không có EventSystem trong scene hiện tại, tap bị bỏ qua hoàn toàn.");
            return;
        }

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, RaycastResults);
        if (RaycastResults.Count == 0) return; // bình thường — đa số điểm quét không trúng UI nào, không log để tránh spam

        pointerData.pointerPressRaycast = RaycastResults[0];
        GameObject target = RaycastResults[0].gameObject;
        Debug.Log($"[LidarTouchBridge][DEBUG] DispatchTap: TRÚNG '{target.name}' tại ({screenPosition.x:F0},{screenPosition.y:F0})");

        GameObject pressed = ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
        GameObject upTarget = pressed != null ? pressed : target;
        ExecuteEvents.ExecuteHierarchy(upTarget, pointerData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(upTarget, pointerData, ExecuteEvents.pointerClickHandler);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Debug — show touch: 1 vòng tròn hiện brief tại mỗi điểm Lidar phát hiện, GIỐNG "Show
    // taps" của Android Developer Options (hiện không dùng được vì X01 không có quyền chỉnh
    // Settings hệ thống). Vẽ ở TẤT CẢ điểm Lidar poll được — kể cả điểm không trúng UI nào —
    // để calib: nhìn vòng tròn hiện đúng đâu so với chỗ đặt vật, từ đó chỉnh half_x/hight_floor/
    // shift_x/shift_y trong lidar_config.json cho khớp. Không raycastTarget nên không chặn
    // touch thật của người dùng.
    // ─────────────────────────────────────────────────────────────────────

    private void ShowTouchIndicator(Vector2 screenPosition)
    {
        EnsureDebugCanvas();

        var go = new GameObject("TouchIndicator", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_debugCanvas.transform, false);
        rt.sizeDelta = new Vector2(indicatorDiameter, indicatorDiameter);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = screenPosition;

        var img = go.GetComponent<Image>();
        img.sprite = GetCircleSprite();
        img.color = indicatorColor;
        img.raycastTarget = false;

        StartCoroutine(FadeAndDestroy(rt, img));
    }

    private IEnumerator FadeAndDestroy(RectTransform rt, Image img)
    {
        Color baseColor = img.color;
        float t = 0f;
        while (t < indicatorLifetime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / indicatorLifetime);
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, k); // nở nhẹ ra, giống hiệu ứng ripple gốc Android
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private void EnsureDebugCanvas()
    {
        if (_debugCanvas != null) return;

        var canvasGo = new GameObject("[LidarTouchDebugCanvas]");
        canvasGo.transform.SetParent(transform, false);
        _debugCanvas = canvasGo.AddComponent<Canvas>();
        _debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _debugCanvas.sortingOrder = 32760; // vẽ đè lên mọi UI khác trong scene
        // Không thêm GraphicRaycaster — canvas debug này không cần (và không được) nhận raycast.
    }

    private Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int size = 128;
        const float ringOuter = 0.5f;   // bán kính ngoài (đơn vị: tỉ lệ so với size/2)
        const float ringInner = 0.35f;  // bán kính trong — tạo hiệu ứng viền tròn rỗng giữa

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radiusPx = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distNorm = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radiusPx;
                byte a;
                if (distNorm > ringOuter) a = 0;
                else if (distNorm < ringInner) a = 60; // tâm mờ, thấy rõ hơn vị trí chính xác là viền
                else a = 255;
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

    // ═════════════════════════════════════════════════════════════════════
    // CALIB — capture window + gom cụm + gán vai trò + giải affine.
    //
    // Luồng: giáo viên đặt 5 trụ xốp tĩnh (4 góc + tâm) vào đúng 5 điểm mốc CalibSceneController
    // chiếu lên máy chiếu, quay lại tablet bấm 1 nút. StartCalibrationCapture() mở 1 cửa sổ
    // lắng nghe raw touch (~2-3s) — mỗi trụ tĩnh sẽ được native báo lặp lại nhiều lần (bộ lọc
    // ổn định của native ưu tiên vật thể đứng yên, xem CLAUDE.md). Sau khi đóng cửa sổ: gom các
    // điểm thô thành cụm theo khoảng cách, kỳ vọng đúng 5 cụm, gán mỗi cụm vào 1 vai trò (4 góc +
    // tâm) bằng vị trí tương đối so với trọng tâm chung — KHÔNG dựa vào calib cũ (có thể đang
    // sai) — rồi giải 1 phép affine tối thiểu bình phương từ 5 cặp (thô ↔ mục tiêu).
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>1 điểm mốc calib: vai trò (để hiển thị/log) + vị trí mục tiêu trên màn hình
    /// (pixel, hệ toạ độ Screen thật — gốc dưới-trái, khớp RawRefToScreen) + GỢI Ý toạ độ thô
    /// (mm, theo hệ trục LiDAR — đo thật ngoài đời, KHÔNG phải suy từ lidar_config.json vì
    /// chính config đó có thể đang lệch, xem CLAUDE.md) để lọc bớt nhiễu xa (vd tường) trước
    /// khi gom cụm. null = chưa đo, bỏ qua bộ lọc này cho điểm đó (vẫn hoạt động như trước).</summary>
    public struct CalibTarget
    {
        public string role;
        public Vector2 targetScreenPos;
        public Vector2? expectedRawMm;

        public CalibTarget(string role, Vector2 targetScreenPos, Vector2? expectedRawMm = null)
        {
            this.role = role;
            this.targetScreenPos = targetScreenPos;
            this.expectedRawMm = expectedRawMm;
        }
    }

    /// <summary>Kết quả sau 1 lần capture — CalibSceneController dùng để hiện xác nhận trực
    /// quan (vòng tròn đỏ tại vị trí đã hiệu chỉnh) + quyết định cho Lưu hay bắt làm lại.</summary>
    public class CalibCaptureResult
    {
        public bool success;
        public string failReason;          // lý do fail, tiếng Việt, hiện thẳng lên UI được
        public int expectedPoints;
        public int foundClusters;
        public List<(string role, Vector2 raw, Vector2 target, float residualPx)> points
            = new List<(string, Vector2, Vector2, float)>();
        public float maxResidualPx;
    }

    private bool _capturing;
    private List<Vector2> _captureBuffer;
    private Coroutine _captureCoroutine;

    // Cụm cách nhau dưới ngưỡng này (px màn hình) bị gộp làm 1 — chọn nhỏ hơn nhiều so với
    // khoảng cách thực tế giữa 2 trụ kề nhau (thường > 1/4 bề rộng vùng chiếu) để không gộp
    // nhầm 2 trụ khác nhau, nhưng đủ lớn để gom hết rung/nhiễu của cùng 1 trụ.
    private const float ClusterMergeRadiusPx = 70f;
    // Dùng ở cả 2 chế độ để loại noise vụn (cụm 1-2 điểm rời rạc, không đáng tin) trước khi gom
    // thành danh sách ứng viên — KHÔNG còn dùng để tự "đoán" cụm nào là trụ (chế độ tuần tự đã
    // bỏ việc tự chọn, đẩy quyết định cuối cho giáo viên, xem javadoc StartSinglePointCapture).
    // Hạ từ 4 xuống 3 theo yêu cầu thật (2026-09-17) — khớp luôn với min_cluster_size=3 hardcode
    // ở tầng native (find_toe_points(clusters,3), native-lib.cpp), tránh bỏ sót cụm trụ thật có
    // ít điểm hơn do throttle 150ms native chia sẻ giữa nhiều vật cùng lúc trong vùng quét.
    private const int MinSamplesPerCluster = 3;

    /// <summary>Bắt đầu 1 cửa sổ lắng nghe calib — xem region header phía trên. An toàn gọi lại
    /// (huỷ lần đang chạy dở nếu có). onDone luôn được gọi đúng 1 lần, kể cả khi fail.</summary>
    public void StartCalibrationCapture(float durationSeconds, CalibTarget[] targets, Action<CalibCaptureResult> onDone)
    {
        // Chỉ huỷ capture cũ (nếu đang dở dang) — KHÔNG dùng StopAllCoroutines(), tránh giết
        // luôn coroutine FadeAndDestroy() của vòng tròn debug touch đang chạy song song.
        if (_captureCoroutine != null) StopCoroutine(_captureCoroutine);
        _capturing = false; // StopCoroutine không chạy nốt phần code sau yield — tự reset cờ
        _captureCoroutine = StartCoroutine(CaptureRoutine(durationSeconds, targets, onDone));
    }

    private IEnumerator CaptureRoutine(float durationSeconds, CalibTarget[] targets, Action<CalibCaptureResult> onDone)
    {
        _captureBuffer = new List<Vector2>();
        _capturing = true;
        yield return new WaitForSecondsRealtime(durationSeconds);
        _capturing = false;

        var result = ProcessCapture(_captureBuffer, targets);
        onDone?.Invoke(result);
    }

    private CalibCaptureResult ProcessCapture(List<Vector2> rawPoints, CalibTarget[] targets)
    {
        var result = new CalibCaptureResult { expectedPoints = targets.Length };

        // expectedRawMm CHỈ dùng để log so sánh — KHÔNG dùng để loại điểm nữa (xem javadoc
        // StartSinglePointCapture cho lịch sử bug: MmToRawScreen dựa vào chính hình học
        // lidar_config.json đang lệch, lọc cứng theo đó có thể loại nhầm cụm đúng).
        if (System.Array.Exists(targets, t => t.expectedRawMm != null))
        {
            var hints = new Vector2?[targets.Length];
            for (int i = 0; i < targets.Length; i++) hints[i] = targets[i].expectedRawMm;
            FilterNearHints(rawPoints, hints, HintWindowRadiusMm);
        }

        var clusters = ClusterPoints(rawPoints, ClusterMergeRadiusPx);
        Debug.Log($"[LidarTouchBridge] ProcessCapture: {rawPoints.Count} điểm thô → {clusters.Count} cụm " +
                  $"[{string.Join(", ", clusters.ConvertAll(c => $"({c.centroid.x:F0},{c.centroid.y:F0})x{c.count}"))}] " +
                  $"(ngưỡng tối thiểu {MinSamplesPerCluster} điểm/cụm)");
        clusters.RemoveAll(c => c.count < MinSamplesPerCluster);
        result.foundClusters = clusters.Count;

        if (clusters.Count > targets.Length)
        {
            // Nhiều hơn cần thiết (nhiễu xa như tường) → giữ lại đúng N cụm NHIỀU ĐIỂM NHẤT,
            // loại các cụm nhỏ/thoáng qua còn lại — không fail ngay chỉ vì có nhiễu phụ.
            clusters.Sort((a, b) => b.count.CompareTo(a.count));
            clusters.RemoveRange(targets.Length, clusters.Count - targets.Length);
            result.foundClusters = clusters.Count;
        }

        if (clusters.Count != targets.Length)
        {
            result.success = false;
            result.failReason = $"Chỉ nhận diện được {clusters.Count}/{targets.Length} điểm — kiểm tra lại trụ có đứng vững, đúng vị trí, không bị che khuất, rồi bấm lại.";
            return result;
        }

        if (!AssignRoles(clusters, targets, out var assigned))
        {
            result.success = false;
            result.failReason = "Vị trí 5 trụ chưa đúng hình 4 góc + tâm (bị lệch/xô), đặt lại theo đúng vòng tròn trên máy chiếu rồi bấm lại.";
            return result;
        }

        return FinalizeFromAssignedPoints(assigned, targets.Length);
    }

    // ── Chế độ tuần tự (1 trụ) — TH chỉ có 1 trụ tròn: đo từng điểm 1, mỗi lần "Đo điểm này"
    // chỉ mong đợi ĐÚNG 1 cụm trong vùng quét (không cần gán vai trò bằng hình học như
    // AssignRoles(), vì CalibSceneController đã BIẾT TRƯỚC đang đo điểm nào — nó chỉ định giáo
    // viên đứng ở đâu). Tích luỹ đủ N điểm (mỗi lần AddSequentialPoint) rồi mới giải affine 1
    // lần duy nhất ở FinishSequentialCalibration(), dùng CHUNG code giải affine với chế độ
    // 5-trụ-cùng-lúc (FinalizeFromAssignedPoints). ────────────────────────────────────────────

    /// <summary>Kết quả 1 lần đo — CalibSceneController dùng cho chế độ tuần tự. KHÔNG tự quyết
    /// định cụm nào là trụ nữa (xem lịch sử bug bên dưới) — trả về TOÀN BỘ cụm tìm được (sắp
    /// theo số điểm giảm dần) để giáo viên tự chọn bằng mắt (số đánh trên máy chiếu).</summary>
    public struct SinglePointCaptureResult
    {
        public bool anyDetected;
        public string failReason;
        public List<(Vector2 centroid, int count)> candidates; // rỗng nếu !anyDetected
    }

    // Trần số ứng viên hiện lên UI — nhiều hơn nữa chỉ là nhiễu vụn, không cần hiện hết.
    private const int MaxCandidates = 6;

    /// <summary>Mở 1 cửa sổ lắng nghe ngắn cho chế độ tuần tự (1 trụ). Vẫn gom cụm + lọc theo
    /// MinSamplesPerCluster như cũ (loại noise vụn) — nhưng KHÔNG tự đoán cụm nào là trụ nữa,
    /// trả về HẾT các cụm ĐẠT ngưỡng (sau khi lọc bớt cụm rõ ràng sai góc phần tư màn hình), để
    /// CalibSceneController hiện lên máy chiếu (đánh số) cho giáo viên tự chọn đúng vị trí trụ.
    ///
    /// Lịch sử 2 lần sửa hỏng trước khi tới bản này (test thật trên K02, 2026-09-17):
    /// 1) Lọc cứng theo expectedMm (CalibTarget.expectedRawMm) qua MmToRawScreen — SAI, vì phép
    ///    quy đổi mm→màn hình dùng CHÍNH hình học lidar_config.json đang lệch (đó LÀ lý do cần
    ///    calib), lọc cứng theo 1 phép quy đổi đã biết sai → loại nhầm sạch cả cụm đúng.
    /// 2) Tự chọn "cụm nhiều điểm nhất" — CŨNG SAI: phòng nhỏ có vật tĩnh (tường/góc phòng)
    ///    phản xạ mạnh/liên tục hơn 1 trụ nhỏ, luôn thắng "nhiều điểm nhất" dù không phải trụ —
    ///    xác nhận thật: 4/5 điểm đo ra cùng 1 vị trí bất kể trụ đặt ở đâu, vòng đỏ debug hiện
    ///    sai chỗ. Không có cách tự động phân biệt "trụ" với "vật tĩnh" từ phía thuật toán —
    ///    CHỈ người đứng đó mới biết trụ đang ở đâu, nên đẩy quyết định cho con người.
    ///
    /// role: "TopLeft"/"TopRight"/"BottomLeft"/"BottomRight"/"Center" — dùng để loại bớt ứng
    /// viên rõ ràng SAI PHÍA màn hình (vd đang đo góc trên-trái mà ra ứng viên ở góc dưới-phải).
    /// Đây CHỈ là lọc thô theo 1/4 màn hình (so với tâm), KHÔNG dùng toạ độ mm/hình học đã chứng
    /// minh không đáng tin — xem lịch sử bug trong SinglePointCaptureRoutine). Quyết định CUỐI
    /// vẫn luôn là con người (đánh số lên máy chiếu, giáo viên tự chọn) — lọc góc chỉ đỡ rối
    /// màn hình chọn, không tự ý quyết định thay.</summary>
    public void StartSinglePointCapture(float durationSeconds, string role, Action<SinglePointCaptureResult> onDone)
    {
        if (_captureCoroutine != null) StopCoroutine(_captureCoroutine);
        _capturing = false;
        _captureCoroutine = StartCoroutine(SinglePointCaptureRoutine(durationSeconds, role, onDone));
    }

    /// <summary>Ứng viên có nằm ĐÚNG 1/4 màn hình ứng với role đang đo không — so với TÂM màn
    /// hình thô (Screen.width/2, Screen.height/2), hệ Y-up (khớp RawRefToScreen). Chỉ áp dụng
    /// cho 4 góc — "Center" không lọc (có thể ở bất kỳ đâu gần giữa).</summary>
    private static bool InExpectedQuadrant(Vector2 rawScreenPos, string role)
    {
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        switch (role)
        {
            case "TopLeft":     return rawScreenPos.x < cx && rawScreenPos.y > cy;
            case "TopRight":    return rawScreenPos.x > cx && rawScreenPos.y > cy;
            case "BottomLeft":  return rawScreenPos.x < cx && rawScreenPos.y < cy;
            case "BottomRight": return rawScreenPos.x > cx && rawScreenPos.y < cy;
            default: return true; // Center hoặc role lạ — không lọc
        }
    }

    private IEnumerator SinglePointCaptureRoutine(float durationSeconds, string role, Action<SinglePointCaptureResult> onDone)
    {
        _captureBuffer = new List<Vector2>();
        _capturing = true;
        yield return new WaitForSecondsRealtime(durationSeconds);
        _capturing = false;

        Debug.Log($"[LidarTouchBridge] SinglePointCapture: điểm thô = [{string.Join(", ", _captureBuffer.ConvertAll(p => $"({p.x:F0},{p.y:F0})"))}]");

        // Vẫn gom cụm + lọc theo MinSamplesPerCluster như cũ (loại noise vụn 1-2 điểm rời rạc)
        // — chỉ khác chỗ KHÔNG tự chọn "cụm nhiều điểm nhất" nữa (đã xác nhận sai, xem javadoc
        // trên), mà giữ lại HẾT các cụm ĐẠT ngưỡng làm ứng viên cho người dùng tự chọn.
        var clusters = ClusterPoints(_captureBuffer, ClusterMergeRadiusPx);
        clusters.Sort((a, b) => b.count.CompareTo(a.count));
        Debug.Log($"[LidarTouchBridge] SinglePointCapture: {_captureBuffer.Count} điểm thô → {clusters.Count} cụm " +
                  $"[{string.Join(", ", clusters.ConvertAll(c => $"({c.centroid.x:F0},{c.centroid.y:F0})x{c.count}"))}] " +
                  $"(ngưỡng tối thiểu {MinSamplesPerCluster} điểm/cụm)");
        clusters.RemoveAll(c => c.count < MinSamplesPerCluster);

        // Lọc thô theo 1/4 màn hình — CÓ dự phòng: nếu lọc xong rỗng (lỡ mọi ứng viên đều "sai
        // phía" — có thể do trụ thật cũng bị lệch phía do hình học quá tệ), KHÔNG chặn cứng,
        // quay lại danh sách đầy đủ trước lọc kèm cảnh báo trong log, để không bỏ sót trụ thật.
        var beforeQuadrantFilter = new List<(Vector2 centroid, int count)>(clusters);
        clusters.RemoveAll(c => !InExpectedQuadrant(c.centroid, role));
        if (clusters.Count == 0 && beforeQuadrantFilter.Count > 0)
        {
            Debug.LogWarning($"[LidarTouchBridge] SinglePointCapture: lọc góc phần tư ({role}) loại hết {beforeQuadrantFilter.Count} cụm " +
                              "— có thể hình học đang lệch nặng, giữ nguyên danh sách đầy đủ thay vì báo 0 kết quả.");
            clusters = beforeQuadrantFilter;
        }

        if (clusters.Count > MaxCandidates) clusters.RemoveRange(MaxCandidates, clusters.Count - MaxCandidates);

        var result = new SinglePointCaptureResult { candidates = clusters };
        if (clusters.Count == 0)
        {
            result.anyDetected = false;
            result.failReason = "Không phát hiện được vật gì trong vùng quét — kiểm tra trụ có đặt đúng vị trí, đứng vững, không bị che khuất, rồi bấm lại.";
        }
        else
        {
            result.anyDetected = true;
        }
        onDone?.Invoke(result);
    }

    private List<(string role, Vector2 raw, Vector2 target)> _sequentialPoints;

    /// <summary>Bắt đầu 1 phiên đo tuần tự mới — xoá sạch điểm đã đo trước đó (nếu có).</summary>
    public void BeginSequentialCalibration()
    {
        _sequentialPoints = new List<(string, Vector2, Vector2)>();
    }

    /// <summary>Ghi nhận 1 điểm vừa đo được trong phiên tuần tự. Gọi lại với CÙNG role sẽ GHI ĐÈ
    /// (cho phép "đo lại" 1 điểm mà không cần huỷ cả phiên) — xem StepBackSequential() phía
    /// CalibSceneController.</summary>
    public void AddSequentialPoint(string role, Vector2 raw, Vector2 target)
    {
        if (_sequentialPoints == null) _sequentialPoints = new List<(string, Vector2, Vector2)>();
        _sequentialPoints.RemoveAll(p => p.role == role);
        _sequentialPoints.Add((role, raw, target));
    }

    /// <summary>Đã đo đủ N điểm của phiên tuần tự chưa — giải affine luôn bằng đúng code dùng
    /// chung với chế độ 5-trụ-cùng-lúc.</summary>
    public CalibCaptureResult FinishSequentialCalibration(int expectedPoints)
    {
        return FinalizeFromAssignedPoints(_sequentialPoints ?? new List<(string, Vector2, Vector2)>(), expectedPoints);
    }

    // ── Dùng chung cho cả 2 chế độ: nhận N cặp (vai trò, toạ độ thô, toạ độ mục tiêu) đã biết
    // rõ ràng (không còn mơ hồ vai trò nào ứng với cụm nào) → giải affine + tính sai số. ───────
    private CalibCaptureResult FinalizeFromAssignedPoints(List<(string role, Vector2 raw, Vector2 target)> assigned, int expectedPoints)
    {
        var result = new CalibCaptureResult { expectedPoints = expectedPoints, foundClusters = assigned.Count };

        if (assigned.Count != expectedPoints)
        {
            result.success = false;
            result.failReason = $"Mới đo được {assigned.Count}/{expectedPoints} điểm — chưa đủ để tính toán.";
            return result;
        }

        // Giải affine tối thiểu bình phương: target = M * raw, từ N cặp điểm (N = 5).
        if (!SolveAffine(assigned, out var m))
        {
            result.success = false;
            result.failReason = "Các điểm đặt quá gần/thẳng hàng, không đủ để tính toán — dàn rộng ra đúng 4 góc + tâm rồi làm lại.";
            return result;
        }

        float maxResidual = 0f;
        foreach (var (role, raw, target) in assigned)
        {
            Vector2 predicted = new Vector2(
                m.m00 * raw.x + m.m01 * raw.y + m.m02,
                m.m10 * raw.x + m.m11 * raw.y + m.m12);
            float residual = Vector2.Distance(predicted, target);
            maxResidual = Mathf.Max(maxResidual, residual);
            result.points.Add((role, raw, target, residual));
        }
        result.maxResidualPx = maxResidual;
        result.success = true;

        _pendingCalib = m; // chưa lưu — chờ CalibSceneController gọi CommitPendingCalibration() sau bước xác nhận
        return result;
    }

    private CalibConfigJson _pendingCalib;

    /// <summary>Áp dụng + lưu kết quả calib vừa tính (ProcessCapture đã thành công) — gọi sau
    /// khi giáo viên xem bước xác nhận trực quan và bấm "Lưu". Không tự động lưu ngay lúc
    /// capture xong để còn cơ hội "Làm lại" nếu overlay xác nhận lệch.</summary>
    public bool CommitPendingCalibration()
    {
        if (_pendingCalib == null) return false;
        _pendingCalib.calibrated = true;
        _calib = _pendingCalib;
        _pendingCalib = null;
        SaveCalib();
        return true;
    }

    /// <summary>Xem trước kết quả calib vừa tính (chưa lưu) — CalibSceneController dùng để vẽ
    /// vòng tròn xác nhận đúng bằng transform MỚI trước khi giáo viên bấm Lưu.</summary>
    public Vector2 PreviewApplyPendingCalib(Vector2 rawScreenPos)
    {
        var m = _pendingCalib ?? _calib;
        return new Vector2(
            m.m00 * rawScreenPos.x + m.m01 * rawScreenPos.y + m.m02,
            m.m10 * rawScreenPos.x + m.m11 * rawScreenPos.y + m.m12);
    }

    // ── Gom cụm theo khoảng cách (online, centroid chạy) ────────────────────────────────────
    private static List<(Vector2 centroid, int count)> ClusterPoints(List<Vector2> points, float mergeRadius)
    {
        var sums = new List<Vector2>();   // tổng dồn từng cụm (để tính centroid chính xác)
        var counts = new List<int>();

        foreach (var p in points)
        {
            int best = -1;
            float bestDist = mergeRadius;
            for (int i = 0; i < counts.Count; i++)
            {
                Vector2 centroid = sums[i] / counts[i];
                float d = Vector2.Distance(centroid, p);
                if (d <= bestDist) { bestDist = d; best = i; }
            }

            if (best >= 0) { sums[best] += p; counts[best]++; }
            else { sums.Add(p); counts.Add(1); }
        }

        var result = new List<(Vector2, int)>();
        for (int i = 0; i < counts.Count; i++) result.Add((sums[i] / counts[i], counts[i]));
        return result;
    }

    // ── Gán 5 cụm → 5 vai trò bằng vị trí tương đối so với trọng tâm chung — KHÔNG phụ thuộc
    // calib cũ (có thể đang sai nặng), chỉ dựa vào hình dạng "4 góc quanh 1 tâm" tự nó. ──────
    private static bool AssignRoles(List<(Vector2 centroid, int count)> clusters, CalibTarget[] targets,
        out List<(string role, Vector2 raw, Vector2 target)> assigned)
    {
        assigned = new List<(string, Vector2, Vector2)>();
        if (clusters.Count != 5 || targets.Length != 5) return false;

        Vector2 center = Vector2.zero;
        foreach (var c in clusters) center += c.centroid;
        center /= clusters.Count;

        // Cụm gần trọng tâm nhất = "Center". 4 cụm còn lại phân vào 4 góc phần tư quanh trọng
        // tâm — mỗi góc phần tư phải có ĐÚNG 1 cụm, nếu không (trụ đặt lệch méo) → fail rõ ràng
        // thay vì đoán bừa.
        int centerIdx = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < clusters.Count; i++)
        {
            float d = Vector2.Distance(clusters[i].centroid, center);
            if (d < bestDist) { bestDist = d; centerIdx = i; }
        }

        var remaining = new List<Vector2>();
        for (int i = 0; i < clusters.Count; i++) if (i != centerIdx) remaining.Add(clusters[i].centroid);

        // (dx<0,dy>0)=TopLeft (dx>0,dy>0)=TopRight (dx<0,dy<0)=BottomLeft (dx>0,dy<0)=BottomRight
        // — khớp quy ước targets đặt tên trong CalibSceneController (Screen space, Y-up).
        var quadrantOf = new Dictionary<string, Vector2?>
        {
            ["TopLeft"] = null, ["TopRight"] = null, ["BottomLeft"] = null, ["BottomRight"] = null,
        };
        foreach (var p in remaining)
        {
            Vector2 d = p - center;
            string q = d.x < 0
                ? (d.y >= 0 ? "TopLeft" : "BottomLeft")
                : (d.y >= 0 ? "TopRight" : "BottomRight");
            if (quadrantOf[q] != null) return false; // 2 cụm cùng 1 góc phần tư — đặt lệch méo
            quadrantOf[q] = p;
        }
        if (quadrantOf["TopLeft"] == null || quadrantOf["TopRight"] == null ||
            quadrantOf["BottomLeft"] == null || quadrantOf["BottomRight"] == null) return false;

        var rawByRole = new Dictionary<string, Vector2>
        {
            ["Center"] = clusters[centerIdx].centroid,
            ["TopLeft"] = quadrantOf["TopLeft"].Value,
            ["TopRight"] = quadrantOf["TopRight"].Value,
            ["BottomLeft"] = quadrantOf["BottomLeft"].Value,
            ["BottomRight"] = quadrantOf["BottomRight"].Value,
        };

        foreach (var t in targets)
        {
            if (!rawByRole.TryGetValue(t.role, out Vector2 raw)) return false; // role lạ, không khớp 5 tên chuẩn
            assigned.Add((t.role, raw, t.targetScreenPos));
        }
        return true;
    }

    // ── Affine tối thiểu bình phương: [tx,ty] = M * [rawX,rawY,1] — giải qua 2 hệ 3x3 độc lập
    // (Cramer's rule), dùng chung 1 ma trận thiết kế A cho cả tx và ty. ─────────────────────
    private static bool SolveAffine(List<(string role, Vector2 raw, Vector2 target)> pairs, out CalibConfigJson m)
    {
        m = null;
        int n = pairs.Count;
        if (n < 3) return false;

        // Normal equations: (AᵀA) x = Aᵀb, với hàng A = [rawX, rawY, 1]
        double sxx = 0, sxy = 0, sx = 0, syy = 0, sy = 0, s1 = n;
        double sxtx = 0, sytx = 0, stx = 0;
        double sxty = 0, syty = 0, sty = 0;

        foreach (var (_, raw, target) in pairs)
        {
            double x = raw.x, y = raw.y, tx = target.x, ty = target.y;
            sxx += x * x; sxy += x * y; sx += x;
            syy += y * y; sy += y;
            sxtx += x * tx; sytx += y * tx; stx += tx;
            sxty += x * ty; syty += y * ty; sty += ty;
        }

        // Ma trận đối xứng 3x3 dùng chung cho cả 2 hệ (tx và ty):
        //  | sxx sxy sx | |a|   |sxtx|      | sxx sxy sx | |d|   |sxty|
        //  | sxy syy sy | |b| = |sytx|  và  | sxy syy sy | |e| = |syty|
        //  | sx  sy  s1 | |c|   |stx |      | sx  sy  s1 | |f|   |sty |
        double det = Det3(sxx, sxy, sx, sxy, syy, sy, sx, sy, s1);
        if (Math.Abs(det) < 1e-6) return false; // 5 điểm gần thẳng hàng — không giải được

        double a = Det3(sxtx, sxy, sx, sytx, syy, sy, stx, sy, s1) / det;
        double b = Det3(sxx, sxtx, sx, sxy, sytx, sy, sx, stx, s1) / det;
        double c = Det3(sxx, sxy, sxtx, sxy, syy, sytx, sx, sy, stx) / det;

        double d = Det3(sxty, sxy, sx, syty, syy, sy, sty, sy, s1) / det;
        double e = Det3(sxx, sxty, sx, sxy, syty, sy, sx, sty, s1) / det;
        double f = Det3(sxx, sxy, sxty, sxy, syy, syty, sx, sy, sty) / det;

        m = new CalibConfigJson
        {
            m00 = (float)a, m01 = (float)b, m02 = (float)c,
            m10 = (float)d, m11 = (float)e, m12 = (float)f,
        };
        return true;
    }

    private static double Det3(double a, double b, double c, double d, double e, double f, double g, double h, double i)
        => a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
}
