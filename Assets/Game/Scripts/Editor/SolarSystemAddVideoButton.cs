using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Thêm nút "Video" phát Space.mp4 (giới thiệu không gian) ngay DƯỚI nút "Real Scale" (btnToggleScale)
/// trong SolarSystemHUD — cho cả 2 scene (gốc dùng "Space", SolarSystemVi dùng "Space_Vi"). Menu:
/// Tools > SolarSystem > Add Space Video Button.
///
/// Nhân bản NGUYÊN GameObject nút "Real Scale" (giữ đúng style/font/màu sẵn có trong scene) rồi đổi
/// vị trí xuống dưới + đổi label — không tự vẽ nút mới từ đầu vì scene này dựng thủ công trong
/// Editor (không có SceneBuilder script như các game MiniGameKit), phải thao tác trên scene thật.
/// Xoá onClick persistent copy theo khi nhân bản — logic click do SolarSystemHUD.WireButtons() gán
/// lại lúc runtime (AddListener non-persistent), không dựa vào Inspector.
/// </summary>
public static class SolarSystemAddVideoButton
{
    const string SourceScenePath = "Assets/Game/Scenes/SolarSystemScene/SolarSystemScene.unity";
    const string ViScenePath = "Assets/Game/Scenes/SolarSystemVi/SolarSystemVi.unity";

    [MenuItem("Tools/SolarSystem/Add Space Video Button")]
    public static void AddButton()
    {
        AddToScene(SourceScenePath, "Space");
        AddToScene(ViScenePath, "Space_Vi");
        AssetDatabase.SaveAssets();
    }

    static void AddToScene(string scenePath, string videoName)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogWarning($"[SolarSystemAddVideoButton] Không tìm thấy scene: {scenePath} — bỏ qua (chạy Tools > SolarSystem > Setup SolarSystemVi trước nếu đây là scene Vi).");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var hud = Object.FindFirstObjectByType<SolarSystemHUD>();
        if (hud == null)
        {
            Debug.LogWarning($"[SolarSystemAddVideoButton] Không tìm thấy SolarSystemHUD trong {scenePath}.");
            return;
        }

        var readSo = new SerializedObject(hud);
        var scaleBtn = readSo.FindProperty("btnToggleScale").objectReferenceValue as Button;
        if (scaleBtn == null)
        {
            Debug.LogWarning($"[SolarSystemAddVideoButton] btnToggleScale chưa gán trong {scenePath} — không có mốc để đặt nút mới ngay dưới.");
            return;
        }

        var existingBtn = readSo.FindProperty("btnPlaySpaceVideo").objectReferenceValue as Button;
        if (existingBtn == null)
        {
            existingBtn = DuplicateBelow(scaleBtn, "BtnPlaySpaceVideo", "Video");

            var wireSo = new SerializedObject(hud);
            wireSo.FindProperty("btnPlaySpaceVideo").objectReferenceValue = existingBtn;
            wireSo.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[SolarSystemAddVideoButton] {scenePath}: đã thêm nút Video mới.");
        }

        var nameSo = new SerializedObject(hud);
        nameSo.FindProperty("spaceVideoName").stringValue = videoName;
        nameSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SolarSystemAddVideoButton] {scenePath}: spaceVideoName = \"{videoName}\".");
    }

    static Button DuplicateBelow(Button source, string newName, string label)
    {
        var go = Object.Instantiate(source.gameObject, source.transform.parent);
        go.name = newName;

        var rect = go.GetComponent<RectTransform>();
        var srcRect = source.GetComponent<RectTransform>();
        rect.anchorMin = srcRect.anchorMin;
        rect.anchorMax = srcRect.anchorMax;
        rect.pivot = srcRect.pivot;
        rect.sizeDelta = srcRect.sizeDelta;
        rect.anchoredPosition = srcRect.anchoredPosition + new Vector2(0f, -(srcRect.sizeDelta.y + 10f));

        var btn = go.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent(); // bỏ onClick persistent copy theo bản gốc (nếu có)

        var tmpLabel = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null) tmpLabel.text = label;
        else
        {
            var legacyLabel = go.GetComponentInChildren<Text>(true);
            if (legacyLabel != null) legacyLabel.text = label;
        }

        return btn;
    }
}
