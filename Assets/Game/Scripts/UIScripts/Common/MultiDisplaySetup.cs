using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Routes all game rendering to Display 1 (HDMI projector) when connected.
/// On tablets used for floor projection, Display 0 = built-in screen, Display 1 = HDMI.
/// FLAG_PRESENTATION on Android HDMI is expected and supported via Unity Presentation API.
/// </summary>
public static class MultiDisplaySetup
{
    public static int GameDisplay { get; private set; } = 0;
    private static bool _initialised;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_initialised) return;
        _initialised = true;

        // Mirror is handled at SurfaceFlinger level (layerStack=0 on HDMI).
        // Unity always renders to Display 0 (tablet main screen).
        GameDisplay = 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GameDisplay == 0) return;
        // Use a coroutine runner to wait one frame — objects may still be initialising
        CoroutineRunner.Run(RouteNextFrame());
    }

    static IEnumerator RouteNextFrame()
    {
        yield return null;
        RouteCurrentScene();
    }

    public static void RouteCurrentScene()
    {
        if (GameDisplay == 0) return;

        int d = GameDisplay;
        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
            cam.targetDisplay = d;

        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                canvas.targetDisplay = d;

        Debug.Log($"[MultiDisplay] Scene '{SceneManager.GetActiveScene().name}' routed to Display {d}");
    }
}

/// <summary>Minimal persistent MonoBehaviour used to run coroutines from static context.</summary>
public class CoroutineRunner : MonoBehaviour
{
    static CoroutineRunner _instance;

    static CoroutineRunner Get()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("[CoroutineRunner]");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<CoroutineRunner>();
        return _instance;
    }

    public static Coroutine Run(IEnumerator routine) => Get().StartCoroutine(routine);
}
