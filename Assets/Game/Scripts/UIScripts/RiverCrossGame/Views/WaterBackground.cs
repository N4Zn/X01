using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng nước chảy đơn giản — cuộn UV texture của RawImage theo thời gian.
///
/// Setup:
///   1. Tạo RawImage phủ toàn bộ canvas (hoặc vùng sông).
///   2. Gán texture nước có tiling.
///   3. Attach script này vào RawImage GameObject.
///   4. Chỉnh scrollSpeed.y âm để nước chảy từ trên xuống.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WaterBackground : MonoBehaviour
{
    [Tooltip("Tốc độ cuộn UV. Y âm = nước chảy từ trên xuống.")]
    public Vector2 scrollSpeed = new Vector2(0f, -0.08f);

    RawImage _img;
    Rect     _uvRect;

    void Awake()
    {
        _img    = GetComponent<RawImage>();
        _uvRect = _img.uvRect;
    }

    void Update()
    {
        _uvRect.position += scrollSpeed * Time.deltaTime;
        _img.uvRect       = _uvRect;
    }
}
