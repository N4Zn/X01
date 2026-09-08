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
            Vector2 screenPos = RefToScreen(refX, refY);
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

    private static Vector2 RefToScreen(int refX, int refY)
    {
        float sx = (float)refX / RefWidth * Screen.width;
        float sy = (float)refY / RefHeight * Screen.height;
        // Native trả toạ độ kiểu evdev/Android (gốc trên-trái, Y tăng xuống); EventSystem
        // của Unity dùng toạ độ màn hình gốc dưới-trái (Y tăng lên) — cần lật Y.
        sy = Screen.height - sy;
        return new Vector2(sx, sy);
    }

    // Down + Up ngay lập tức tại vị trí Lidar phát hiện — chỉ touch điểm rời rạc, không có
    // drag/swipe (độ phân giải Lidar hiện tại không đủ cho việc đó, đã thống nhất bỏ qua).
    private static void DispatchTap(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return;

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, RaycastResults);
        if (RaycastResults.Count == 0) return;

        pointerData.pointerPressRaycast = RaycastResults[0];
        GameObject target = RaycastResults[0].gameObject;

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
}
