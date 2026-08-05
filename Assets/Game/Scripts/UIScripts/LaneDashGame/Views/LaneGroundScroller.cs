using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuộn 1 nhóm object trang trí mặt đường (tile đường đất, bụi cỏ phân làn...) dọc trục Z theo
/// tốc độ được đẩy vào mỗi frame qua SetSpeed() — đồng bộ với LaneTrack.CurrentSpeed cùng bên,
/// giống LaneRunnerItem nhưng dùng kiểu "quấn vòng" (wrap) thay vì spawn/despawn: vì số lượng
/// tile/bụi cỏ cố định ngay từ lúc dựng scene (không ngẫu nhiên như vật cản), object nào trôi qua
/// khỏi người chơi chỉ cần dịch chuyển +trackLength để hiện lại ở đầu track, không cần pool.
/// </summary>
public class LaneGroundScroller : MonoBehaviour
{
    // PHẢI [SerializeField] — Init()/Register() chỉ chạy 1 lần lúc LaneDashSceneBuilder dựng scene
    // (Editor time). Field private thường KHÔNG được Unity lưu vào file scene, nên khi Play (scene
    // reload) 3 field này quay về rỗng/0, khiến Update() cuộn trên danh sách trống — tile đứng im
    // dù _speed vẫn đúng. Đánh dấu SerializeField để giá trị được ghi vào .unity và tồn tại qua Play.
    [SerializeField] List<Transform> _items = new();
    [SerializeField] float _wrapThresholdZ;
    [SerializeField] float _trackLength;

    float _speed; // m/s — 0 = đứng yên, được gán lại mỗi frame lúc chạy nên không cần serialize

    /// <summary>wrapThresholdZ: Z mà dưới ngưỡng này object bị coi là "đã qua khỏi người chơi".
    /// trackLength: quãng dịch chuyển khi wrap (thường = chiều dài toàn track).</summary>
    public void Init(float wrapThresholdZ, float trackLength)
    {
        _wrapThresholdZ = wrapThresholdZ;
        _trackLength = trackLength;
    }

    /// <summary>Đăng ký 1 Transform để cuộn cùng — gọi lúc dựng scene, mỗi tile/bụi cỏ 1 lần.</summary>
    public void Register(Transform t) => _items.Add(t);

    /// <summary>Gọi mỗi frame từ LaneDashController, đồng bộ với LaneTrack.CurrentSpeed cùng bên.</summary>
    public void SetSpeed(float metersPerSecond) => _speed = metersPerSecond;

    void Update()
    {
        if (_speed == 0f) return;

        float delta = _speed * Time.deltaTime;
        for (int i = 0; i < _items.Count; i++)
        {
            var t = _items[i];
            var pos = t.position;
            pos.z -= delta;
            if (pos.z < _wrapThresholdZ)
                pos.z += _trackLength;
            t.position = pos;
        }
    }
}
