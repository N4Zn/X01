using UnityEngine;

/// <summary>
/// Dữ liệu mỗi hành tinh — tạo asset qua Create > SolarSystem > PlanetData.
/// </summary>
[CreateAssetMenu(fileName = "PlanetData", menuName = "SolarSystem/PlanetData")]
public class PlanetData : ScriptableObject
{
    [Header("Hiển thị")]
    public string planetName;           // Tên tiếng Anh
    public string planetNameVi;         // Tên tiếng Việt
    [TextArea(2, 4)]
    public string descriptionVi;        // Mô tả ngắn tiếng Việt

    [Header("Texture")]
    public string texturePath;          // Resources path, vd: "SolarSystem/Textures/earth"

    [Header("Quỹ đạo")]
    public float orbitRadius;           // Khoảng cách từ Mặt Trời (Unity units)
    public float orbitSpeed;            // Độ/giây quay quanh Mặt Trời
    public float selfRotateSpeed;       // Độ/giây tự quay

    [Header("Kích thước")]
    public float scale = 1f;            // Scale sphere

    [Header("Thông số giáo dục")]
    public string distanceFromSun;      // VD: "150 triệu km"
    public string diameter;             // VD: "12,742 km"
    public int    numberOfMoons;
    public string surfaceTemp;          // VD: "15°C trung bình"

    [Header("Video")]
    [Tooltip("Tên file video trong StreamingAssets/Videos/ (không có .mp4). Bỏ trống nếu chưa có.")]
    public string videoPath;

    [Header("Special")]
    public bool   hasSaturnRings;
    public bool   isSun;

    [Header("Trục quay & Quỹ đạo")]
    [Tooltip("Độ nghiêng trục quay so với phương thẳng đứng (Trái Đất = 23.4°, Sao Thiên Vương = 97.8°)")]
    public float axialTilt;        // degrees

    [Tooltip("Độ nghiêng mặt phẳng quỹ đạo so với hoàng đạo (Sao Thủy = 7°, các hành tinh khác < 3.5°)")]
    public float orbitInclination; // degrees
}
