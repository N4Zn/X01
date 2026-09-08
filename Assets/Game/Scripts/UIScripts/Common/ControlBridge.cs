using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Đọc tên scene + tên game (variant key) được chọn từ ControlActivity (Java, display 0)
/// qua Intent extra lúc UnityPlayerActivity khởi động trên display máy chiếu — xem
/// ControlActivity.EXTRA_SCENE_NAME/EXTRA_GAME_NAME / onStartClicked().
///
/// Nhờ đây, UnityPlayerActivity load THẲNG scene minigame đã chọn, không qua MenuScene —
/// đúng yêu cầu "máy chiếu chỉ để chơi game" (MenuScene giờ là ControlActivity, native, ở
/// display 0). Không có extra (vd chạy trực tiếp lúc dev/test) → không làm gì, giữ hành vi
/// mặc định hiện tại (scene đầu tiên trong Build Settings, đang là MenuScene).
///
/// Set GameSessionManager.SelectedGameName TRƯỚC khi LoadScene — bắt buộc, vì nhiều game
/// (engine TongHopGame) dùng CHUNG 1 scene nhưng khác bộ câu hỏi CSV theo key này
/// (QuestionPool đọc Resources/TongHop/{SelectedGameName}/) — y hệt việc
/// MenuSceneController.OnClickStart() đã làm, chỉ khác nguồn dữ liệu (Intent extra thay vì
/// UI Canvas click).
/// </summary>
public static class ControlBridge
{
    private const string ExtraSceneName = "com.eduxplore.control.SCENE_NAME";
    private const string ExtraGameName = "com.eduxplore.control.GAME_NAME";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                string sceneName = intent.Call<string>("getStringExtra", ExtraSceneName);
                string gameName = intent.Call<string>("getStringExtra", ExtraGameName);

                if (!string.IsNullOrEmpty(sceneName))
                    LoadGame(sceneName, gameName);
                else
                    Debug.Log("[ControlBridge] Không có Intent extra scene — giữ scene mặc định (Build Settings)");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ControlBridge] Đọc Intent extra lỗi: {e}");
        }
#endif
    }

    /// <summary>
    /// Nạp 1 game cụ thể — dùng chung cho 2 nguồn gọi: Intent extra lúc cold-boot (Init() ở
    /// trên) VÀ GameControlBridge.OnLoadGameRequested (khi UnityPlayerActivity đã sống sẵn từ
    /// lần chơi trước, ControlActivity chỉ gửi lệnh nạp game mới qua UnitySendMessage thay vì
    /// khởi động lại Activity — xem lý do đầy đủ ở GameControlBridge.cs, bug "Stop thoát cả
    /// app" do Unity tự kill() process lúc Activity destroy).
    /// </summary>
    public static void LoadGame(string sceneName, string gameName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        Debug.Log($"[ControlBridge] LoadGame: name={gameName} scene={sceneName}");

        if (!string.IsNullOrEmpty(gameName) && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.SelectedGameName = gameName;
            GameSessionManager.Instance.LastPlayedGame = sceneName;
            // SelectedEntry (GameRegistry.GameEntry đầy đủ, gồm engine/bgm/...) — tra lại
            // từ GameRegistry theo name, vì ControlActivity (Java) chỉ gửi được name+scene
            // dạng string, không gửi được cả struct.
            TryResolveSelectedEntry(gameName);
        }

        SceneManager.LoadScene(sceneName);
    }

    static void TryResolveSelectedEntry(string gameName)
    {
        for (int cat = 0; cat < GameRegistry.CATEGORY_COUNT; cat++)
        {
            for (int idx = 0; idx < GameRegistry.MAX_PER_CATEGORY; idx++)
            {
                var entry = GameRegistry.Games[cat, idx];
                if (!entry.IsEmpty && entry.name == gameName)
                {
                    GameSessionManager.Instance.SelectedEntry = entry;
                    return;
                }
            }
        }
        Debug.LogWarning($"[ControlBridge] Không tìm thấy GameEntry cho name={gameName} trong GameRegistry — SelectedEntry giữ giá trị cũ/mặc định");
    }
}
