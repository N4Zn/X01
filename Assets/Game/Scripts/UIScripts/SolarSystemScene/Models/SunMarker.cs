using UnityEngine;

/// Đảm bảo Mặt Trời luôn hiển thị là 1 chấm tròn sáng dù camera ở bất kỳ khoảng cách nào.
/// Scale tự động để giữ kích thước cố định trên màn hình (screenFraction × chiều cao view).
public class SunMarker : MonoBehaviour
{
    [SerializeField] float screenFraction = 0.022f; // 2.2% chiều cao màn hình

    Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main ?? FindObjectOfType<Camera>();
        if (_cam == null) return;

        float dist  = Vector3.Distance(transform.position, _cam.transform.position);
        float halfH = dist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float size  = Mathf.Max(halfH * screenFraction * 2f, 0.01f);
        transform.localScale = Vector3.one * size;
    }
}
