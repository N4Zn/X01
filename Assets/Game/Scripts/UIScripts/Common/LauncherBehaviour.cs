using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EduXplore 2.0 — Floor Projection Launcher behaviour.
///
/// Gắn vào một GameObject trong MenuScene (hoặc mọi scene chính).
/// - Chặn nút Back / Escape không thoát app (launcher không được tắt như app thường).
/// - Nếu đang ở scene game → Back quay về MenuScene thay vì thoát.
/// - Giữ màn hình luôn sáng (phù hợp thiết bị chạy liên tục chiếu sàn).
/// </summary>
public class LauncherBehaviour : MonoBehaviour
{
    [Tooltip("Tên scene chính (game select). Back button ở đây sẽ bị chặn.")]
    [SerializeField] string homeSceneName = "MenuScene";

    [Tooltip("Giữ màn hình luôn sáng — bật cho thiết bị floor projection.")]
    [SerializeField] bool keepScreenAwake = true;

    void Awake()
    {
        if (keepScreenAwake)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Đảm bảo landscape
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            string current = SceneManager.GetActiveScene().name;
            if (current == homeSceneName)
            {
                // Ở màn hình chính → không làm gì (launcher không thoát)
                return;
            }
            else
            {
                // Đang trong game → quay về menu
                SceneManager.LoadScene(homeSceneName);
            }
        }
    }

    void OnApplicationQuit()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Ngăn thoát hoàn toàn — đưa về background (hành vi launcher chuẩn)
        // Unity sẽ gọi MoveTaskToBack thông qua UnityPlayerActivity
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        activity.Call<bool>("moveTaskToBack", true);
        Application.CancelQuit();
#endif
    }
}
