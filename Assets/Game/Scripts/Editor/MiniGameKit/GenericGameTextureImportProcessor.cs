using UnityEditor;

/// <summary>
/// Tự ép Texture Type = Sprite (2D and UI) cho MỌI ảnh copy vào thư mục
/// Resources/TestTongHop/images/GenericGames/ — ảnh xuất từ web tool kéo-thả được copy thẳng vào
/// đây bằng tay (không qua wizard import của Unity), nếu không ép type thì mặc định "Default"
/// khiến Resources.Load&lt;Sprite&gt;() trả về null lặng lẽ (đúng bug thật đã gặp với carrot.png/
/// Bracket.png ở HaiQua/DemQua — phải sửa .meta tay). Xử lý 1 lần ở đây để các game sau không còn
/// dính lại gotcha này.
/// </summary>
public class GenericGameTextureImportProcessor : AssetPostprocessor
{
    const string WatchedFolder = "/TestTongHop/images/GenericGames/";

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains(WatchedFolder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
    }
}
