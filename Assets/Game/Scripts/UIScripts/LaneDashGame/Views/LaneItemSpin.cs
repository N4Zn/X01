using UnityEngine;

/// <summary>
/// Xoay liên tục quanh trục Y — hiệu ứng "lấp lánh" cho phần thưởng/vật phẩm đặc biệt (gem/coin...).
/// Chỉ dùng transform.Rotate (tương đối) — KHÔNG set position/scale tuyệt đối, nên an toàn khi kết
/// hợp với LaneRunnerItem (tự set position theo Z mỗi frame) và với pool (object tắt/bật lại nhiều
/// lần qua Spawn()/Recycle(), Start() chỉ chạy đúng 1 lần lúc kích hoạt đầu tiên).
/// </summary>
public class LaneItemSpin : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 120f;

    void Update() => transform.Rotate(Vector3.up * degreesPerSecond * Time.deltaTime, Space.World);
}
