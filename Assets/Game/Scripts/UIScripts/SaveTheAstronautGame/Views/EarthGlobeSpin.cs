using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Giả lập "quả cầu Trái Đất xoay 3D" bằng cách cuộn UV ngang của 1 RawImage (bản đồ Trái Đất
/// dạng equirectangular) — cùng kỹ thuật ảo giác xoay cầu mà SolarSystemDisplay đã dùng cho các
/// hành tinh trong game TongHop, tránh phải dựng thêm Camera/RenderTexture riêng cho 1 UI thuần
/// 2D. Gán texture tại Resources/SaveTheAstronaut/earth_map.png (equirectangular) vào RawImage —
/// chưa có asset thì object vẫn xoay, chỉ không thấy hoạ tiết.
/// </summary>
public class EarthGlobeSpin : MonoBehaviour
{
    [SerializeField] RawImage targetImage;
    [SerializeField] float spinSpeed = 0.08f; // UV/giây

    void Update()
    {
        if (targetImage == null) return;
        var r = targetImage.uvRect;
        r.x += spinSpeed * Time.deltaTime;
        targetImage.uvRect = r;
    }
}
