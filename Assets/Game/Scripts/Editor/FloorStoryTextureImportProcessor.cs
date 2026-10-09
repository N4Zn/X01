using UnityEditor;

/// <summary>
/// Ép Texture Type = Sprite cho mọi ảnh thả vào Resources/Story/ (game FloorStory đọc bằng Resources.Load).
/// Thả file PNG vào đúng thư mục là chạy — không phải chỉnh import tay. Tắt mipmap, giữ alpha trong suốt.
/// </summary>
public class FloorStoryTextureImportProcessor : AssetPostprocessor
{
    const string WatchedFolder = "/Resources/Story/";

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains(WatchedFolder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 1024;
    }
}
