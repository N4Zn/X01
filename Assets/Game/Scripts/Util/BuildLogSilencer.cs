using UnityEngine;

/// <summary>
/// Tắt log + console nổi trên màn hình ở BẢN BUILD (K02 chiếu sàn không cần thấy lỗi/log). Development Build
/// tự bật "developer console" hiện chữ đỏ mỗi khi có Debug.LogError/exception — tắt cả hai ở đây.
/// KHÔNG ảnh hưởng Editor (vẫn đầy đủ log). Cần log trên thiết bị để debug: đặt <see cref="SilenceInBuild"/> = false.
/// </summary>
public static class BuildLogSilencer
{
    public const bool SilenceInBuild = true;

#if !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (!SilenceInBuild) return;
        Debug.developerConsoleVisible = false;
        Debug.unityLogger.logEnabled = false;
    }
#endif
}
