using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EduXplore 2.0 — App behaviour (non-launcher).
///
/// Gắn vào một GameObject trong MenuScene (hoặc mọi scene chính).
/// - Nếu đang ở scene game → Back quay về MenuScene.
/// - Nếu đang ở MenuScene → Back thoát app bình thường.
/// - Giữ màn hình luôn sáng (phù hợp thiết bị floor projection).
/// </summary>
public class LauncherBehaviour : MonoBehaviour
{
    [Tooltip("Tên scene chính (game select). Back button ở đây sẽ thoát app.")]
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
                // Ở màn hình chính → thoát app (hành vi app thường)
                Application.Quit();
            }
            else
            {
                // Đang trong game → quay về menu
                SceneManager.LoadScene(homeSceneName);
            }
        }
    }
}
