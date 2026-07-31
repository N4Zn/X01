using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý toàn bộ bè trong 1 lane dọc.
///
/// - Bè spawn ở đỉnh màn hình, trôi xuống, despawn ở đáy.
/// - Object pool tránh GC.
/// - Phase offset đảm bảo bè lệch pha với lane kề, buộc player canh thời điểm.
/// </summary>
public class RaftLane : MonoBehaviour
{
    [SerializeField] RaftItem raftPrefab;

    float _laneX;
    float _speed;
    float _spacing;
    float _thickness;
    float _width;
    float _tolerance;
    float _spawnY;     // tính từ Controller (nửa canvas + buffer)
    float _despawnY;   // âm của _spawnY

    readonly List<RaftItem>  _active = new();
    readonly Queue<RaftItem> _pool   = new();

    // ── Init ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Khởi tạo lane từ config per-lane + thông số global.
    /// laneCfg   : speed, spacing, raftWidth, phaseOffset riêng của lane này.
    /// thickness : chiều dày bè (global — đồng nhất collision).
    /// tolerance : dung sai landing (global).
    /// spawnY    : Y ngoài màn hình nơi bè xuất hiện (nửa canvas + buffer).
    /// </summary>
    public void Init(float laneX, LaneConfigData laneCfg,
                     float thickness, float tolerance, float spawnY)
    {
        _laneX     = laneX;
        _speed     = laneCfg.speed;
        _spacing   = laneCfg.spacing;
        _thickness = thickness;
        _width     = laneCfg.raftWidth;
        _tolerance = tolerance;
        _spawnY    = spawnY;
        _despawnY  = -spawnY;

        // Gieo bè ban đầu điền đầy màn hình, áp dụng lệch pha
        float startY = _spawnY - laneCfg.phaseOffset * _spacing;
        while (startY > _despawnY)
        {
            SpawnAt(startY);
            startY -= _spacing;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        float delta = _speed * Time.deltaTime;

        // Di chuyển và thu hồi bè thoát đáy
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].MoveDown(delta);
            if (_active[i].CenterY < _despawnY)
            {
                _active[i].Recycle();
                _pool.Enqueue(_active[i]);
                _active.RemoveAt(i);
            }
        }

        // Spawn bè mới ở đỉnh khi cần
        if (TopmostY() < _spawnY - _spacing * 0.5f)
            SpawnAt(_spawnY);
    }

    // ── Public queries ────────────────────────────────────────────────────────

    /// <summary>Có bè nào ở vị trí Y không?</summary>
    public bool HasRaftAt(float y)
    {
        foreach (var r in _active)
            if (r.ContainsY(y, _thickness, _tolerance)) return true;
        return false;
    }

    /// <summary>
    /// Trả về Y tâm của bè tại vị trí y (dùng để snap landing).
    /// Trả về y gốc nếu không tìm thấy.
    /// </summary>
    public float GetRaftCenterY(float y)
    {
        foreach (var r in _active)
            if (r.ContainsY(y, _thickness, _tolerance)) return r.CenterY;
        return y;
    }

    /// <summary>
    /// Trả về RaftItem mà player đang đứng (gần playerY nhất trong vùng hợp lệ).
    /// Dùng để player "bám" theo bè khi đứng yên.
    /// </summary>
    public RaftItem GetRaftUnder(float playerY)
    {
        RaftItem best  = null;
        float    minD  = float.MaxValue;
        float    extra = _tolerance + 15f;   // vùng bám rộng hơn landing một chút

        foreach (var r in _active)
        {
            if (!r.ContainsY(playerY, _thickness, extra)) continue;
            float d = Mathf.Abs(r.CenterY - playerY);
            if (d < minD) { minD = d; best = r; }
        }
        return best;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void SpawnAt(float y)
    {
        RaftItem item = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(raftPrefab, transform);
        item.Spawn(_laneX, y, _width, _thickness);
        _active.Add(item);
    }

    float TopmostY()
    {
        float top = float.MinValue;
        foreach (var r in _active) if (r.CenterY > top) top = r.CenterY;
        return top;
    }
}
