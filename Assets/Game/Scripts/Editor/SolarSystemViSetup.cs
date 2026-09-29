using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dựng bản "SolarSystemVi" (video tiếng Việt) từ scene SolarSystem gốc — KHÔNG đụng gì tới scene
/// gốc. Menu: Tools > SolarSystem > Setup SolarSystemVi.
///
/// Vì sao cần script này thay vì Ctrl+D scene thủ công: SolarSystemController.planetDataList và
/// SolarSystemSpawner.planets tham chiếu THẲNG (GUID) tới 9 PlanetData asset gốc trong
/// Resources/SolarSystem/Data/ — Ctrl+D scene KHÔNG tự nhân bản asset đi kèm, bản Vi sẽ vẫn trỏ về
/// đúng 9 asset gốc (chung videoPath với bản EN, đổi 1 bên đổi luôn cả 2). Script này nhân bản 9
/// PlanetData sang Resources/SolarSystem/DataVi/, đổi videoPath thêm hậu tố "_Vi", RỒI mới nhân bản
/// scene và tự động nối lại 2 field trên trỏ về bộ asset Vi mới — 2 bản hoàn toàn độc lập.
///
/// planetNameVi/descriptionVi đã có sẵn tiếng Việt trong asset gốc (dùng chung, không cần nhân bản
/// vì không đổi) — CHỈ videoPath khác giữa 2 bản.
/// </summary>
public static class SolarSystemViSetup
{
    const string SourceDataFolder = "Assets/Resources/SolarSystem/Data";
    const string ViDataFolder = "Assets/Resources/SolarSystem/DataVi";
    const string SourceScenePath = "Assets/Game/Scenes/SolarSystemScene/SolarSystemScene.unity";
    const string ViSceneFolder = "Assets/Game/Scenes/SolarSystemVi";
    const string ViScenePath = "Assets/Game/Scenes/SolarSystemVi/SolarSystemVi.unity";

    static readonly string[] PlanetAssetNames =
        { "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune", "Sun" };

    [MenuItem("Tools/SolarSystem/Setup SolarSystemVi (duplicate + video Vi)")]
    public static void Setup()
    {
        var viAssets = DuplicatePlanetData();
        if (viAssets == null) return;

        if (!DuplicateScene()) return;

        var scene = EditorSceneManager.OpenScene(ViScenePath, OpenSceneMode.Single);

        // QUAN TRỌNG: dùng FindObjectsByType/GetComponentInChildren THEO ĐÚNG scene vừa mở — KHÔNG
        // dùng FindFirstObjectByType<T>() trơn, vì hàm đó tìm xuyên suốt TẤT CẢ scene đang load cùng
        // lúc trong Editor (nếu người dùng đang mở thêm scene khác — vd scene gốc — additive), có
        // thể tóm NHẦM component ở scene KHÁC thay vì scene Vi vừa mở, khiến scene Vi thật không bao
        // giờ được nối dây (bug thực tế đã gặp — Planets[] trong scene Vi vẫn "None" dù log báo
        // "Xong").
        var controller = FindInScene<SolarSystemController>(scene);
        if (controller != null)
        {
            var so = new SerializedObject(controller);
            AssignPlanetArray(so, "planetDataList", viAssets);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var spawner = FindInScene<SolarSystemSpawner>(scene);
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            AssignPlanetArray(so, "planets", viAssets);
            so.FindProperty("useVietnameseLabels").boolValue = true; // panel thông tin hiện nhãn tiếng Việt (Khoảng cách/Đường kính/Số vệ tinh/Nhiệt độ)
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[SolarSystemViSetup] Đã nối {viAssets.Length} PlanetData vào '{spawner.name}' trong {scene.name}: " +
                       string.Join(", ", System.Array.ConvertAll(viAssets, d => d != null ? d.name : "NULL")));
        }

        if (controller == null && spawner == null)
            Debug.LogWarning("[SolarSystemViSetup] Không tìm thấy SolarSystemController lẫn SolarSystemSpawner trong scene — kiểm tra lại cấu trúc scene gốc.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(ViScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SolarSystemViSetup] Xong.\n" +
                   $"- Scene: {ViScenePath}\n" +
                   $"- PlanetData Vi: {ViDataFolder}/ (videoPath đã thêm hậu tố \"_Vi\")\n" +
                   "- Copy video .mp4 tiếng Việt vào: Assets/StreamingAssets/Video/\n" +
                   "  Tên file đúng videoPath mới: Mercury_Vi.mp4, Venus_Vi.mp4, TheEarth_Vi.mp4 (Earth), " +
                   "Mars_Vi.mp4, Jupiter_Vi.mp4, Saturn_Vi.mp4, Uranus_Vi.mp4, Neptune_Vi.mp4, Sun_Vi.mp4");
    }

    static PlanetData[] DuplicatePlanetData()
    {
        if (!AssetDatabase.IsValidFolder(ViDataFolder))
            AssetDatabase.CreateFolder("Assets/Resources/SolarSystem", "DataVi");

        var result = new PlanetData[PlanetAssetNames.Length];
        for (int i = 0; i < PlanetAssetNames.Length; i++)
        {
            string planetAssetName = PlanetAssetNames[i];
            string src = $"{SourceDataFolder}/{planetAssetName}.asset";
            string dst = $"{ViDataFolder}/{planetAssetName}.asset";

            if (AssetDatabase.LoadAssetAtPath<PlanetData>(dst) == null)
            {
                if (!AssetDatabase.CopyAsset(src, dst))
                {
                    Debug.LogError($"[SolarSystemViSetup] Copy thất bại: {src} — kiểm tra asset gốc còn tồn tại không.");
                    return null;
                }
            }

            var data = AssetDatabase.LoadAssetAtPath<PlanetData>(dst);
            if (data == null)
            {
                Debug.LogError($"[SolarSystemViSetup] Không load được PlanetData vừa copy: {dst}");
                return null;
            }

            if (!string.IsNullOrEmpty(data.videoPath) && !data.videoPath.EndsWith("_Vi"))
            {
                data.videoPath += "_Vi";
                EditorUtility.SetDirty(data);
            }
            result[i] = data;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    static bool DuplicateScene()
    {
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game");
        MiniGameSceneBuilderHelpers.EnsureFolder("Assets/Game/Scenes");
        MiniGameSceneBuilderHelpers.EnsureFolder(ViSceneFolder);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ViScenePath) != null)
            return true; // đã có sẵn từ lần chạy trước — chỉ cần nối lại field

        if (!File.Exists(Path.GetFullPath(SourceScenePath)))
        {
            Debug.LogError($"[SolarSystemViSetup] Không tìm thấy scene gốc: {SourceScenePath}");
            return false;
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, ViScenePath))
        {
            Debug.LogError($"[SolarSystemViSetup] Copy scene thất bại: {SourceScenePath} → {ViScenePath}");
            return false;
        }
        AssetDatabase.Refresh();
        return true;
    }

    /// <summary>Tìm component CHỈ trong đúng scene truyền vào (không lan sang scene khác đang load
    /// cùng lúc trong Editor) — duyệt root GameObjects của scene rồi GetComponentInChildren
    /// (true = kể cả object đang tắt).</summary>
    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static void AssignPlanetArray(SerializedObject so, string propName, PlanetData[] values)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogWarning($"[SolarSystemViSetup] Không tìm thấy field '{propName}' trên {so.targetObject.GetType().Name} — có thể tên field đã đổi, kiểm tra lại.");
            return;
        }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
