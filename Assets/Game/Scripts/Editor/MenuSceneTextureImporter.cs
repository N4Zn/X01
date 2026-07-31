using UnityEditor;
using UnityEngine;

public static class MenuSceneTextureImporter
{
    [MenuItem("Tools/MenuScene/Fix Texture Import Settings")]
    public static void FixTextureImportSettings()
    {
        string folder = "Assets/Game/Textures/MenuScene";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log("MenuSceneTextureImporter: Fixed " + path);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("MenuSceneTextureImporter: Fixed " + fixedCount + " textures out of " + guids.Length + " total.");
    }
}
