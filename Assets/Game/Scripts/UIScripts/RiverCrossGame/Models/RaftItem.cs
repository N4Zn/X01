using UnityEngine;

/// <summary>
/// Một bè — RectTransform UI di chuyển từ trên xuống dưới liên tục.
/// RaftLane sở hữu và quản lý vòng đời.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RaftItem : MonoBehaviour
{
    RectTransform _rt;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>True khi bè đang được sử dụng (không nằm trong pool).</summary>
    public bool IsAlive { get; private set; }

    /// <summary>Y tâm bè trong canvas space.</summary>
    public float CenterY => _rt.anchoredPosition.y;

    /// <summary>X tâm bè trong canvas space.</summary>
    public float CenterX => _rt.anchoredPosition.x;

    // ── Init ──────────────────────────────────────────────────────────────────

    void Awake() => _rt = GetComponent<RectTransform>();

    /// <summary>Khởi tạo khi lấy từ pool.</summary>
    public void Spawn(float x, float y, float width, float thickness)
    {
        // Reset anchor về center trước khi set sizeDelta
        // (tránh trường hợp prefab/pool object có anchor stretch → size sai)
        _rt.anchorMin        = new Vector2(0.5f, 0.5f);
        _rt.anchorMax        = new Vector2(0.5f, 0.5f);
        _rt.pivot            = new Vector2(0.5f, 0.5f);
        _rt.sizeDelta        = new Vector2(width, thickness);
        _rt.anchoredPosition = new Vector2(x, y);
        IsAlive              = true;
        gameObject.SetActive(true);
    }

    /// <summary>Trả về pool — gọi bởi RaftLane.</summary>
    public void Recycle()
    {
        IsAlive = false;
        gameObject.SetActive(false);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    /// <summary>Di chuyển xuống delta canvas units.</summary>
    public void MoveDown(float delta)
        => _rt.anchoredPosition += Vector2.down * delta;

    // ── Collision ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Trả về true nếu y nằm trong vùng bè.
    /// thickness: chiều dày bè, tolerance: dung sai thêm.
    /// </summary>
    public bool ContainsY(float y, float thickness, float tolerance)
    {
        float half = thickness * 0.5f + tolerance;
        return y >= CenterY - half && y <= CenterY + half;
    }
}
