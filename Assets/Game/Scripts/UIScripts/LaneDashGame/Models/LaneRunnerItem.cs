using UnityEngine;

/// <summary>Loại vật thể trên cánh đồng: vật cản (bụi cây/đá/gia súc), phần thưởng, hoặc
/// vật phẩm đặc biệt (ngựa/tên lửa).</summary>
public enum LaneItemKind { Obstacle, Reward, PowerUp }

/// <summary>
/// Một vật thể 3D trên cánh đồng (bụi cây/đá/trâu/bò/lợn/gà, bông lúa/hoa, hoặc ngựa/tên lửa) —
/// Transform trôi dọc trục Z (tiến về phía người chơi). Visual (mesh Kenney thật hoặc primitive
/// màu) được dựng 1 lần bởi LaneDashItemVisuals.CreateVisual — component này chỉ lo vị trí/vòng
/// đời, không tự tạo hình. LaneTrack sở hữu và quản lý pool theo từng biến thể (mỗi biến thể có
/// visual khác nhau nên không dùng chung 1 pool phẳng như bản 2D cũ).
/// </summary>
[DisallowMultipleComponent]
public class LaneRunnerItem : MonoBehaviour
{
    public bool IsAlive { get; private set; }
    public LaneItemKind Kind { get; private set; }
    public LaneItemVariant Variant { get; private set; }
    public int Lane { get; private set; }

    /// <summary>Vị trí dọc track (world Z) — dùng để so sánh với playerZ khi tính va/né.</summary>
    public float CenterZ => transform.position.z;

    /// <summary>Gán loại + biến thể — gọi 1 lần lúc tạo (LaneDashItemVisuals.CreateVisual), không
    /// đổi trong suốt vòng đời vì visual đã cố định theo biến thể này.</summary>
    public void Configure(LaneItemKind kind, LaneItemVariant variant)
    {
        Kind = kind;
        Variant = variant;
    }

    /// <summary>Đặt lại vị trí + kích hoạt khi lấy từ pool.</summary>
    public void Spawn(int lane, float x, float y, float z, float scale)
    {
        Lane = lane;
        transform.localScale = Vector3.one * scale;
        transform.position = new Vector3(x, y, z);
        IsAlive = true;
        gameObject.SetActive(true);
    }

    /// <summary>Trả về pool — gọi bởi LaneTrack.</summary>
    public void Recycle()
    {
        IsAlive = false;
        gameObject.SetActive(false);
    }

    /// <summary>Tiến về phía người chơi (world Z giảm dần) — delta tính bằng mét.</summary>
    public void MoveForward(float delta) => transform.position += Vector3.back * delta;
}
