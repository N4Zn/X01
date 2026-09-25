using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cầu nối 2 chiều giữa ControlActivity (Java, display 0 — nút Pause/Stop) và game đang
/// chạy trong Unity (display máy chiếu) — CÙNG process, khác Activity/task/display.
///
/// Chiều vào (Java → C#): ControlActivity gọi UnityPlayer.UnitySendMessage("GameControlBridge",
/// "OnPauseRequested"/"OnResumeRequested", "") qua reflection (tránh phụ thuộc biên dịch,
/// giống LidarUsbBridge.java) — GameObject PHẢI tên đúng "GameControlBridge".
///
/// Chiều ra (C# → Java): PushReport() gọi AndroidJavaClass("com.eduxplore.control.ControlActivity")
/// .CallStatic("UpdateReport", ...) — AndroidJavaClass luôn resolve qua reflection lúc chạy,
/// không có vấn đề phụ thuộc biên dịch chiều nào (khác UnitySendMessage ở trên, đây là C#
/// gọi Java, không phải Java gọi C#).
///
/// Singleton&lt;T&gt; (cùng pattern LidarTouchBridge) — RuntimeInitializeOnLoadMethod warm-up
/// để đảm bảo GameObject tồn tại TRƯỚC khi ControlActivity có thể gửi lệnh (nút Pause chỉ
/// bấm được sau khi UnityPlayerActivity đã khởi động, nhưng cứ warm-up sớm cho chắc, khớp
/// pattern đã dùng cho LidarTouchBridge).
/// </summary>
public class GameControlBridge : Singleton<GameControlBridge>
{
    private const string ControlActivityClass = "com.eduxplore.control.ControlActivity";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WarmUp() => _ = Instance;

    protected override void OnCreated()
    {
        gameObject.name = "GameControlBridge";
        Debug.Log("[GameControlBridge] OnCreated — singleton sống, DontDestroyOnLoad.");

        // Tạo sẵn blank overlay ngay từ lúc khởi động (ẩn), thay vì tạo MỚI lúc bấm Stop —
        // nguyên nhân treo máy thật đã xác nhận qua log Dev Build trên K02: main thread Unity
        // ngừng tick HẲN ngay sau khi OnStopRequested() chạy xong dòng log cuối cùng — đúng lúc
        // CreateBlankOverlay() lần đầu dựng 1 Canvas hoàn toàn mới trên display phụ (kiến trúc
        // 2-display tuỳ biến của K02). Dựng sẵn lúc app còn đang ổn định (ngay sau khi warm-up),
        // Stop chỉ cần SetActive(true) trên object đã có sẵn — rẻ, không tạo GameObject/Canvas/
        // load Sprite mới giữa lúc đang xử lý sự kiện Stop.
        _blankOverlay = CreateBlankOverlay();
        _blankOverlay.gameObject.SetActive(false);
        Debug.Log("[GameControlBridge] Blank overlay đã tạo sẵn (ẩn) lúc khởi động.");
    }

    // ── DEBUG: heartbeat — bằng chứng trực tiếp main thread Unity còn chạy hay không (log
    // mỗi ~2s từ 1 object DontDestroyOnLoad, sống xuyên suốt mọi scene). Nếu log này NGỪNG xuất
    // hiện trong lúc app "treo" (màn chiếu đứng hình, ControlActivity gửi lệnh không phản hồi)
    // → xác nhận main thread Unity thực sự bị block/deadlock, không phải lỗi hiển thị/display.
    // Xoá khối DEBUG này sau khi tìm ra nguyên nhân treo (xem CLAUDE.md/phiên debug "Chơi lại").
    float _lastHeartbeatLog;
    void Update()
    {
        if (Time.unscaledTime - _lastHeartbeatLog < 2f) return;
        _lastHeartbeatLog = Time.unscaledTime;
        Debug.Log($"[GameControlBridge][HEARTBEAT] t={Time.unscaledTime:F1} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} frame={Time.frameCount}");
    }

    /// <summary>Gọi từ ControlActivity (nút "Xoá lịch sử" trong tab TỔNG KẾT) — xoá file
    /// GameSessionManager.GameHistory (lịch sử điểm nhiều game). ControlActivity tự cập nhật UI
    /// về rỗng ngay lập tức phía nó (không đợi round-trip), gọi đây chỉ để đồng bộ file lưu thật
    /// phía Unity, tránh dữ liệu cũ sống lại nếu app bị kill rồi mở lại.</summary>
    public void OnClearGameHistoryRequested(string unused)
    {
        GameSessionManager.Instance?.ClearHistory();
        Debug.Log("[GameControlBridge] OnClearGameHistoryRequested — đã xoá GameHistory.");
    }

    /// <summary>Gọi từ ControlActivity (UnitySendMessage) khi bấm Pause. Dừng timer/logic
    /// của minigame đang chạy (nếu có) VÀ tắt Lidar touch cùng lúc — 2 việc luôn đi đôi vì
    /// nguồn touch duy nhất trên thiết bị là Lidar (xem LidarTouchBridge).</summary>
    public void OnPauseRequested(string unused)
    {
        MiniGameControllerBase.Current?.Pause();
        // TestTongHopController (game chính, engine TongHopGame) có FSM/state hoàn toàn
        // riêng, KHÔNG kế thừa MiniGameControllerBase — phải gọi thêm nhánh này, nếu không
        // Pause/report luôn no-op với game chính (đã xác nhận qua test thực tế).
        TestTongHopController.Current?.Pause();
        LidarTouchBridge.Instance.SetTouchEnabled(false);
        MusicManager.Instance?.PauseBgm();
        Debug.Log("[GameControlBridge] OnPauseRequested");
    }

    public void OnResumeRequested(string unused)
    {
        MiniGameControllerBase.Current?.Resume();
        TestTongHopController.Current?.Resume();
        LidarTouchBridge.Instance.SetTouchEnabled(true);
        MusicManager.Instance?.ResumeBgm();
        Debug.Log("[GameControlBridge] OnResumeRequested");
    }

    /// <summary>Gọi từ ControlActivity khi bấm Stop — KHÔNG destroy/finish UnityPlayerActivity
    /// (khác finishUnityTask() cũ). Unity tự kill() cả process (dùng chung với ControlActivity)
    /// khi Activity của nó bị destroy — hành vi engine, không sửa được từ code app (xem lịch sử
    /// bug "Stop thoát cả app"). Né hẳn vấn đề bằng cách giữ UnityPlayerActivity SỐNG NGUYÊN,
    /// chỉ dừng game + che đen display — giống hệt việc bấm Home để app chạy nền, chỉ khác là
    /// phải tự làm thủ công vì 2 display riêng biệt không có 1 cử chỉ Home áp dụng đúng cho
    /// riêng display máy chiếu.</summary>
    public void OnStopRequested(string unused)
    {
        MiniGameControllerBase.Current?.Pause();
        TestTongHopController.Current?.Pause();
        LidarTouchBridge.Instance.SetTouchEnabled(false);
        MusicManager.Instance?.PauseBgm();
        ShowBlankOverlay(true);
        Debug.Log("[GameControlBridge] OnStopRequested");
    }

    /// <summary>Gọi từ ControlActivity khi bấm Start LẦN 2 TRỞ ĐI — UnityPlayerActivity đã
    /// sống sẵn từ lần chơi trước (Stop không còn destroy nó nữa), nên chỉ cần lệnh nạp game
    /// mới qua message thay vì khởi động lại Activity (Intent extra chỉ đọc được 1 lần lúc
    /// cold-boot — xem ControlBridge.Init()). payload: "sceneName|gameName".</summary>
    public void OnLoadGameRequested(string payload)
    {
        Debug.Log($"[GameControlBridge][DEBUG] OnLoadGameRequested NHẬN được payload='{payload}' (chứng minh UnitySendMessage TỚI được C#).");
        if (string.IsNullOrEmpty(payload)) { Debug.LogWarning("[GameControlBridge][DEBUG] payload rỗng — return sớm, KHÔNG load gì cả."); return; }
        int sep = payload.IndexOf('|');
        string sceneName = sep >= 0 ? payload.Substring(0, sep) : payload;
        string gameName  = sep >= 0 ? payload.Substring(sep + 1) : null;
        Debug.Log($"[GameControlBridge][DEBUG] Parsed sceneName='{sceneName}' gameName='{gameName}'");

        ShowBlankOverlay(false);
        Debug.Log("[GameControlBridge][DEBUG] ShowBlankOverlay(false) xong.");
        LidarTouchBridge.Instance.SetTouchEnabled(true);
        Debug.Log("[GameControlBridge][DEBUG] SetTouchEnabled(true) xong. Gọi ControlBridge.LoadGame()...");
        ControlBridge.LoadGame(sceneName, gameName);
        Debug.Log("[GameControlBridge][DEBUG] ControlBridge.LoadGame() ĐÃ RETURN (không có nghĩa là scene đã load xong, chỉ là lệnh gọi không bị treo/exception ở tầng này).");
    }

    // ── Blank overlay — che display máy chiếu lúc Stop, thay cho việc destroy Activity ──────

    Canvas _blankOverlay;

    void ShowBlankOverlay(bool visible)
    {
        // _blankOverlay giờ luôn được tạo sẵn từ OnCreated() — không còn nhánh "tạo mới lúc
        // này" nữa (xem lý do đầy đủ ở OnCreated()). Log cảnh báo nếu vì lý do gì đó vẫn null,
        // để không im lặng bỏ qua Stop.
        if (_blankOverlay != null) _blankOverlay.gameObject.SetActive(visible);
        else Debug.LogWarning("[GameControlBridge] ShowBlankOverlay: _blankOverlay vẫn null — chưa tạo được lúc khởi động?");
    }

    // Nền màn hình che display máy chiếu lúc Stop — cùng ảnh logo dùng cho lúc mới mở app
    // (ControlActivity.showLogoOnSecondaryDisplay(), NativePlugins/ControlUiAndroidLib) để
    // đồng nhất: Stop trông giống "quay lại màn chờ", không phải màn đen như tắt máy.
    const string LogoResourcePath = "ui/splash/Logo_Full";

    static Canvas CreateBlankOverlay()
    {
        var go = new GameObject("[GameControlBridge_BlankOverlay]");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // đè lên mọi UI khác, kể cả debug touch indicator

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        // Khớp màu nền trong ảnh logo (navy) thay vì đen thui — logo có viền cùng tông nên
        // không bị "khung" lệch màu nếu tỉ lệ khung hình không khớp đúng ảnh.
        bgImg.color = new Color32(0x0C, 0x11, 0x1D, 0xFF);
        bgImg.raycastTarget = false; // không cần chặn touch riêng — LidarTouchBridge đã tắt touch

        var logoSprite = Resources.Load<Sprite>(LogoResourcePath);
        if (logoSprite != null)
        {
            var logoGo = new GameObject("Logo");
            logoGo.transform.SetParent(go.transform, false);
            var logoRt = logoGo.AddComponent<RectTransform>();
            // Full màn hình (khớp logo lúc "vào game" ở ControlActivity.showLogoOnSecondaryDisplay()
            // — trước đây chỉ chiếm 50%x40% giữa màn, không đồng nhất). preserveAspect vẫn co ảnh
            // vừa khít khung mà không méo dù neo full-rect. Neo theo tỉ lệ % (không sizeDelta cố
            // định theo pixel) — canvas này không có CanvasScaler nên px cố định sẽ to/nhỏ khác
            // nhau tuỳ độ phân giải máy chiếu thật.
            logoRt.anchorMin = Vector2.zero;
            logoRt.anchorMax = Vector2.one;
            logoRt.offsetMin = logoRt.offsetMax = Vector2.zero;
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning($"[GameControlBridge] Không load được logo tại Resources/{LogoResourcePath} — chỉ hiện nền màu.");
        }

        return canvas;
    }

    /// <summary>Báo ControlActivity game vừa TỰ kết thúc (hết giờ/hết vòng — GameOver tự
    /// nhiên, KHÔNG phải do bấm Stop) — gọi từ StateMachineEnter_GameOver của
    /// TestTongHopController/MiniGameControllerBase. ControlActivity tự quay Menu để chọn game
    /// tiếp theo, coi như hết 1 round, không cần user bấm Stop thủ công. Display máy chiếu
    /// KHÔNG bị đụng tới — Unity tự chuyển ScoreScene như bình thường, vẫn hiện kết quả.</summary>
    public void PushGameEnded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("OnGameEnded");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushGameEnded lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    /// <summary>Đẩy report (giây còn lại, tên + điểm TỪNG BÊN trái/phải riêng) sang
    /// ControlActivity — gọi từ MiniGameControllerBase.Update()/TestTongHopController.Update(),
    /// throttle 1 lần/giây ở phía gọi. Trước đây chỉ gộp 1 tổng điểm, không có tên — sửa lại
    /// tách riêng + kèm tên (GameSessionManager.GetDisplayName1/2, cập nhật liên tục từ
    /// PlayerRecognitionService) vì control panel cần biết "ai" đang chơi, không chỉ điểm số.</summary>
    public void PushReport(int secondsLeft, string leftName, int leftScore, string rightName, int rightScore)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("UpdateReport", secondsLeft + "s",
                    leftName, leftScore.ToString(), rightName, rightScore.ToString());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushReport lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    [System.Serializable] class LivePlayerDto { public string name; public int correct; public int answered; public float avgTime; public float avgCorrectTime; }
    [System.Serializable] class LivePlayerListDto { public System.Collections.Generic.List<LivePlayerDto> players; }

    /// <summary>Đẩy điểm/số liệu THẬT từng người chơi đã nhận diện được (PlayerRecognitionService.
    /// GetPlayerStats) sang ControlActivity — cùng nhịp throttle 1s với PushReport() ở nơi gọi.
    /// Thay cho roster MockData.classData(...).playedStudents cũ trên panel_live (danh sách CẢ
    /// LỚP với số liệu random, không liên quan ván đang chơi — đã xác nhận qua báo cáo thực tế
    /// trên K02: số liệu hiện ra không khớp điểm ván đang chơi).</summary>
    public void PushPlayerBreakdown(System.Collections.Generic.List<PlayerRecognitionService.PlayerRoundStat> leftStats,
        System.Collections.Generic.List<PlayerRecognitionService.PlayerRoundStat> rightStats)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("UpdateLivePlayers", ToJson(leftStats), ToJson(rightStats));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushPlayerBreakdown lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }

    static string ToJson(System.Collections.Generic.List<PlayerRecognitionService.PlayerRoundStat> stats)
    {
        var dto = new LivePlayerListDto { players = new System.Collections.Generic.List<LivePlayerDto>() };
        if (stats != null)
        {
            foreach (var s in stats)
            {
                dto.players.Add(new LivePlayerDto
                {
                    name = s.name,
                    correct = s.correct,
                    answered = s.answered,
                    avgTime = s.AvgAnswerTimeSec,
                    avgCorrectTime = s.AvgCorrectAnswerTimeSec
                });
            }
        }
        return JsonUtility.ToJson(dto);
    }

    [System.Serializable] class HistoryEntryDto { public string gameDisplayName; public string leftName; public int leftScore; public string rightName; public int rightScore; public string timestamp; }
    [System.Serializable] class HistoryListDto { public System.Collections.Generic.List<HistoryEntryDto> entries; }

    /// <summary>Đẩy TOÀN BỘ lịch sử điểm nhiều game (GameSessionManager.GameHistory, lưu file,
    /// tích luỹ qua nhiều lần mở app) sang ControlActivity — gọi ngay sau
    /// GameSessionManager.AppendGameHistory() mỗi khi 1 ván kết thúc, để mục "TỔNG KẾT" hiện lần
    /// lượt từng game + tổng hợp toàn bộ, thay vì chỉ hiện game gần nhất như trước.</summary>
    public void PushGameHistory(System.Collections.Generic.List<GameHistoryEntry> history)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var dto = new HistoryListDto { entries = new System.Collections.Generic.List<HistoryEntryDto>() };
            if (history != null)
            {
                foreach (var h in history)
                {
                    dto.entries.Add(new HistoryEntryDto
                    {
                        gameDisplayName = h.gameDisplayName,
                        leftName = h.leftName,
                        leftScore = h.leftScore,
                        rightName = h.rightName,
                        rightScore = h.rightScore,
                        timestamp = h.timestamp
                    });
                }
            }
            string json = JsonUtility.ToJson(dto);
            using (var controlActivityClass = new AndroidJavaClass(ControlActivityClass))
            {
                controlActivityClass.CallStatic("UpdateGameHistory", json);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameControlBridge] PushGameHistory lỗi (ControlActivity có thể chưa chạy/khác display): {e.Message}");
        }
#endif
    }
}
