using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene "CalibScene" — chạy trên display máy chiếu, điều khiển hoàn toàn từ xa qua
/// CalibControlBridge (nút bấm thật nằm trên tablet — CalibActivity, display 0). Toàn bộ UI
/// (canvas, vòng tròn mục tiêu, chữ trạng thái) dựng bằng code trong Awake()/Start() — GIỐNG
/// cách GameControlBridge.CreateBlankOverlay() đã làm — để scene .unity chỉ cần rỗng (1
/// GameObject gắn script này), không cần dựng UI tay trong Editor.
///
/// Luồng: hiện 5 vòng tròn mục tiêu (4 góc + tâm vùng chiếu) → CalibActivity gọi
/// BeginCapture() khi giáo viên đặt xong 5 trụ và bấm "Bắt đầu calib" → LidarTouchBridge lắng
/// nghe ~CaptureDurationSeconds giây → OnCaptureDone hiện kết quả xác nhận (chấm đỏ tại vị trí
/// đã hiệu chỉnh so với mục tiêu) → giáo viên bấm Lưu (SavePending) hoặc bấm lại "Bắt đầu
/// calib" để làm lại từ đầu.
/// </summary>
public class CalibSceneController : MonoBehaviour
{
    public static CalibSceneController Current { get; private set; }

    // Vị trí 5 mục tiêu theo TỈ LỆ màn hình (0..1, gốc dưới-trái, khớp hệ toạ độ Screen thật) —
    // tính lại theo Screen.width/height lúc runtime nên tự đúng với mọi độ phân giải máy chiếu.
    // Margin 8% để trụ còn nằm trong vùng quét thực tế của cảm biến, không sát mép chiếu.
    private static readonly (string role, float fx, float fy)[] TargetFractions =
    {
        ("TopLeft",     0.08f, 0.85f),
        ("TopRight",    0.92f, 0.85f),
        ("BottomLeft",  0.08f, 0.15f),
        ("BottomRight", 0.92f, 0.15f),
        ("Center",      0.50f, 0.50f),
    };

    private const float CaptureDurationSeconds = 2.5f;
    private const float SinglePointCaptureDurationSeconds = 1.8f;

    private static readonly Dictionary<string, string> RoleLabelVi = new Dictionary<string, string>
    {
        ["TopLeft"] = "Góc trên-trái", ["TopRight"] = "Góc trên-phải",
        ["BottomLeft"] = "Góc dưới-trái", ["BottomRight"] = "Góc dưới-phải",
        ["Center"] = "Chính giữa",
    };
    private static string RoleLabel(string role) => RoleLabelVi.TryGetValue(role, out var v) ? v : role;

    private static readonly string IdleMessage =
        "Đặt 5 trụ vào đúng 5 vòng tròn (4 góc + giữa), rồi quay lại bấm \"Bắt đầu calib\" trên máy tính bảng " +
        "— hoặc chọn \"Calib từng điểm\" nếu chỉ có 1 trụ.";

    private Canvas _canvas;
    private Text _statusText;
    private readonly Dictionary<string, RectTransform> _targetMarkers = new Dictionary<string, RectTransform>();
    private readonly List<GameObject> _confirmDots = new List<GameObject>();
    private Sprite _ringSprite;
    private bool _capturing;

    // -1 = không ở chế độ tuần tự (1 trụ). 0..4 = đang chờ đo điểm thứ mấy trong TargetFractions.
    private int _sequentialIndex = -1;

    void Awake()
    {
        Current = this;
        BuildUi();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        ShowIdleState(IdleMessage);
    }

    // Screen.width/height của display phụ (máy chiếu, setLaunchDisplayId) có thể CHƯA ổn định
    // ngay khung hình đầu — tính vị trí 1 lần trong Awake()/BuildUi() có thể ra sai nếu lúc đó
    // Unity chưa nhận đúng kích thước thật. Tính lại liên tục (rẻ, chỉ vài phép gán Vector2 cho
    // tối đa 5 UI element) để tự sửa đúng ngay khi Screen.width/height ổn định, không phụ thuộc
    // thời điểm gọi nữa — không cần đợi giáo viên bấm nút mới thấy đúng vị trí.
    void Update()
    {
        if (!_capturing) RepositionTargets();
    }

    // ═════════════════════════════════════════════════════════════════════
    // API gọi từ CalibControlBridge (lệnh từ CalibActivity trên tablet)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Chế độ 5 trụ cùng lúc (nhanh) — giữ nguyên hành vi cũ.</summary>
    public void BeginCapture()
    {
        if (_capturing) return; // đang đo dở — bỏ qua bấm trùng, tránh 2 cửa sổ capture chồng nhau
        _sequentialIndex = -1; // đảm bảo không lẫn với chế độ tuần tự nếu đang dở dang

        ClearConfirmDots();
        SetTargetsVisible(true);
        _capturing = true;
        ShowIdleState("Đang đo... giữ nguyên 5 trụ tại chỗ.");

        var targets = BuildTargets();
        CalibControlBridge.Instance.PushListening(CaptureDurationSeconds);
        LidarTouchBridge.Instance.StartCalibrationCapture(CaptureDurationSeconds, targets, OnCaptureDone);
    }

    private void OnCaptureDone(LidarTouchBridge.CalibCaptureResult result)
    {
        _capturing = false;
        HandleFinalResult(result);
    }

    // ═════════════════════════════════════════════════════════════════════
    // Chế độ tuần tự (1 trụ) — đo từng điểm 1, TH chỉ có 1 trụ tròn: giáo viên đặt trụ vào
    // ĐÚNG 1 vòng tròn đang hiện, quay lại bấm "Đo điểm này", hệ thống tự chuyển sang điểm kế
    // tiếp. Dùng lại nguyên bộ máy gom cụm/giải affine — chỉ khác chỗ vai trò (role) của điểm
    // đang đo đã BIẾT TRƯỚC (mình đang chỉ định giáo viên đứng ở đâu), nên không cần đoán bằng
    // hình học như AssignRoles() của chế độ 5-trụ-cùng-lúc.
    // ═════════════════════════════════════════════════════════════════════

    public void BeginSequential()
    {
        if (_capturing) return;
        ClearConfirmDots();
        LidarTouchBridge.Instance.BeginSequentialCalibration();
        _sequentialIndex = 0;
        ShowSequentialStepUi();
    }

    public void CaptureSequentialStep()
    {
        if (_capturing) return;
        if (_sequentialIndex < 0 || _sequentialIndex >= TargetFractions.Length) return;

        _capturing = true;
        var (role, _, _) = TargetFractions[_sequentialIndex];
        ShowIdleState($"Đang đo điểm {_sequentialIndex + 1}/{TargetFractions.Length} ({RoleLabel(role)})... giữ nguyên trụ.");
        CalibControlBridge.Instance.PushListening(SinglePointCaptureDurationSeconds);
        LidarTouchBridge.Instance.StartSinglePointCapture(SinglePointCaptureDurationSeconds, OnSequentialStepCaptured);
    }

    /// <summary>Lùi lại 1 điểm để đo lại (vd giáo viên nghi ngờ điểm trước đặt lệch) — không cần
    /// làm lại từ đầu, vì LidarTouchBridge.AddSequentialPoint() cho phép ghi đè theo role.</summary>
    public void StepBackSequential()
    {
        if (_capturing) return;
        if (_sequentialIndex <= 0) return;
        _sequentialIndex--;
        ShowSequentialStepUi();
    }

    private void OnSequentialStepCaptured(LidarTouchBridge.SinglePointCaptureResult stepResult)
    {
        _capturing = false;

        if (!stepResult.success)
        {
            ShowIdleState($"Điểm {_sequentialIndex + 1}/{TargetFractions.Length} chưa đạt: {stepResult.failReason}");
            CalibControlBridge.Instance.PushSequentialStepResult(false, stepResult.failReason);
            return; // giữ nguyên _sequentialIndex — giáo viên bấm "Đo điểm này" lại cho đúng điểm này
        }

        var (role, fx, fy) = TargetFractions[_sequentialIndex];
        Vector2 target = new Vector2(fx * Screen.width, fy * Screen.height);
        LidarTouchBridge.Instance.AddSequentialPoint(role, stepResult.raw, target);
        CalibControlBridge.Instance.PushSequentialStepResult(true, null);

        _sequentialIndex++;
        if (_sequentialIndex < TargetFractions.Length) ShowSequentialStepUi();
        else FinishSequential();
    }

    private void ShowSequentialStepUi()
    {
        var (role, _, _) = TargetFractions[_sequentialIndex];
        SetTargetsVisible(false); // ẩn hết rồi chỉ bật đúng 1 mốc đang cần đo — đỡ rối, giáo viên biết chính xác đứng đâu
        if (_targetMarkers.TryGetValue(role, out var rt)) rt.gameObject.SetActive(true);

        string label = RoleLabel(role);
        ShowIdleState($"Điểm {_sequentialIndex + 1}/{TargetFractions.Length}: đặt trụ vào vòng tròn ({label}), " +
                       "quay lại bấm \"Đo điểm này\" trên máy tính bảng.");
        CalibControlBridge.Instance.PushSequentialStep(_sequentialIndex, TargetFractions.Length, role, label);
    }

    private void FinishSequential()
    {
        var result = LidarTouchBridge.Instance.FinishSequentialCalibration(TargetFractions.Length);
        _sequentialIndex = -1;
        HandleFinalResult(result);
    }

    // ═════════════════════════════════════════════════════════════════════

    public void SavePending()
    {
        bool ok = LidarTouchBridge.Instance.CommitPendingCalibration();
        if (ok)
        {
            ShowIdleState("Đã lưu calib. Có thể dọn trụ khỏi sàn.");
            CalibControlBridge.Instance.PushSaved();
        }
        else
        {
            Debug.LogWarning("[CalibSceneController] SavePending: không có kết quả nào đang chờ lưu (chưa capture thành công lần nào)");
        }
    }

    public void CancelAndReset()
    {
        _capturing = false;
        _sequentialIndex = -1;
        ClearConfirmDots();
        SetTargetsVisible(true);
        ShowIdleState(IdleMessage);
    }

    // ═════════════════════════════════════════════════════════════════════

    private LidarTouchBridge.CalibTarget[] BuildTargets()
    {
        var targets = new LidarTouchBridge.CalibTarget[TargetFractions.Length];
        for (int i = 0; i < TargetFractions.Length; i++)
        {
            var (role, fx, fy) = TargetFractions[i];
            Vector2 pos = new Vector2(fx * Screen.width, fy * Screen.height);
            targets[i] = new LidarTouchBridge.CalibTarget(role, pos);
        }
        return targets;
    }

    /// <summary>Dùng chung cho cả 2 chế độ (5-trụ-cùng-lúc VÀ tuần tự-1-trụ) — cả 2 đều kết
    /// thúc bằng 1 CalibCaptureResult giống hệt nhau (đã giải affine xong hay chưa).</summary>
    private void HandleFinalResult(LidarTouchBridge.CalibCaptureResult result)
    {
        if (!result.success)
        {
            ShowIdleState($"Chưa đạt: {result.failReason}");
            CalibControlBridge.Instance.PushResult(false, result.foundClusters, result.expectedPoints, 0f, result.failReason);
            return;
        }

        SetTargetsVisible(false);
        DrawConfirmDots(result);

        string quality = result.maxResidualPx < 15f ? "Tốt" : result.maxResidualPx < 30f ? "Khá — có thể lưu" : "Lệch nhiều — nên làm lại";
        ShowIdleState($"Kết quả: {quality} (sai số tối đa {result.maxResidualPx:F0}px). " +
                       "Xem chấm đỏ có trùng vòng tròn không rồi bấm \"Lưu\" trên máy tính bảng, hoặc bắt đầu lại để làm lại.");

        CalibControlBridge.Instance.PushResult(true, result.foundClusters, result.expectedPoints, result.maxResidualPx, null);
    }

    // ── UI dựng bằng code ────────────────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("[CalibCanvas]");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // dưới overlay che màn của GameControlBridge (32767) nhưng đủ cao cho scene riêng này

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color32(0x0C, 0x11, 0x1D, 0xFF); // khớp tông nền navy dùng chung của app
        bgImg.raycastTarget = false;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f); titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -20);
        titleRt.sizeDelta = new Vector2(900, 60);
        var title = titleGo.AddComponent<Text>();
        title.text = "Calib vùng tương tác";
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 30;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.raycastTarget = false;

        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(canvasGo.transform, false);
        var statusRt = statusGo.AddComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.5f, 0f); statusRt.anchorMax = new Vector2(0.5f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.anchoredPosition = new Vector2(0, 30);
        statusRt.sizeDelta = new Vector2(1400, 90);
        _statusText = statusGo.AddComponent<Text>();
        _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize = 22;
        _statusText.alignment = TextAnchor.MiddleCenter;
        _statusText.color = new Color(0.85f, 0.85f, 0.9f);
        _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _statusText.raycastTarget = false;

        foreach (var (role, fx, fy) in TargetFractions)
        {
            var markerGo = new GameObject($"Target_{role}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)markerGo.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.sizeDelta = new Vector2(90, 90);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = markerGo.GetComponent<Image>();
            img.sprite = GetRingSprite();
            img.color = new Color(1f, 0.85f, 0.2f, 0.9f); // vàng — dễ phân biệt với chấm xác nhận màu đỏ
            img.raycastTarget = false;
            _targetMarkers[role] = rt;
        }

        RepositionTargets();
    }

    private void RepositionTargets()
    {
        foreach (var (role, fx, fy) in TargetFractions)
        {
            if (_targetMarkers.TryGetValue(role, out var rt))
                rt.anchoredPosition = new Vector2(fx * Screen.width, fy * Screen.height);
        }
    }

    private void SetTargetsVisible(bool visible)
    {
        RepositionTargets(); // đề phòng Screen.width/height đổi giữa các lần (đổi máy chiếu/độ phân giải)
        foreach (var rt in _targetMarkers.Values) rt.gameObject.SetActive(visible);
    }

    private void ShowIdleState(string message)
    {
        if (_statusText != null) _statusText.text = message;
    }

    private void ClearConfirmDots()
    {
        foreach (var go in _confirmDots) if (go != null) Destroy(go);
        _confirmDots.Clear();
    }

    /// <summary>Vẽ tĩnh: với mỗi điểm mục tiêu, 1 chấm đỏ tại vị trí trụ THÔ sau khi áp transform
    /// vừa tính (chưa lưu) — giáo viên so bằng mắt chấm đỏ có trùng vòng tròn vàng mục tiêu
    /// không, không cần đứng lại lên sàn.</summary>
    private void DrawConfirmDots(LidarTouchBridge.CalibCaptureResult result)
    {
        foreach (var (role, raw, target, residualPx) in result.points)
        {
            Vector2 corrected = LidarTouchBridge.Instance.PreviewApplyPendingCalib(raw);

            var dotGo = new GameObject($"Confirm_{role}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)dotGo.transform;
            rt.SetParent(_canvas.transform, false);
            rt.sizeDelta = new Vector2(40, 40);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = corrected;
            var img = dotGo.GetComponent<Image>();
            img.sprite = GetRingSprite();
            img.color = residualPx < 15f ? new Color(0.3f, 0.9f, 0.4f) : residualPx < 30f ? new Color(1f, 0.8f, 0.2f) : new Color(0.95f, 0.25f, 0.25f);
            img.raycastTarget = false;
            _confirmDots.Add(dotGo);

            // Vẫn giữ target markers ẩn trong lúc xác nhận nhưng gắn thêm 1 vòng mờ tại vị trí
            // mục tiêu gốc để giáo viên dễ so sánh 2 vị trí cạnh nhau.
            var targetDotGo = new GameObject($"ConfirmTarget_{role}", typeof(RectTransform), typeof(Image));
            var trt = (RectTransform)targetDotGo.transform;
            trt.SetParent(_canvas.transform, false);
            trt.sizeDelta = new Vector2(90, 90);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.zero;
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = target;
            var timg = targetDotGo.GetComponent<Image>();
            timg.sprite = GetRingSprite();
            timg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            timg.raycastTarget = false;
            _confirmDots.Add(targetDotGo);
        }
    }

    private Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int size = 128;
        const float ringOuter = 0.5f;
        const float ringInner = 0.35f;

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
                else if (distNorm < ringInner) a = 40;
                else a = 255;
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _ringSprite;
    }
}
