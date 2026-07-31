using UnityEngine;

/// <summary>
/// Pool loài vật ngẫu nhiên dùng riêng cho game "Counting".
///
/// Cách dùng:
///   1. Đặt sprite ảnh loài vật vào: Assets/Resources/Counting/Animals/
///      (cat.png, dog.png, rabbit.png, ...)
///   2. Trong CSV choose.csv của Counting, đặt questionMediaValue = "RANDOM:N"
///      (N = số lượng cần đếm; tên loài vật sẽ được chọn ngẫu nhiên khi hiển thị)
///   3. QuestionMediaDisplay.Show() tự phát hiện "RANDOM" và gọi GetRandom().
///
/// Đặc điểm:
///   - Lazy-load: load sprites lần đầu khi GetRandom() được gọi (không cần Preload).
///   - Anti-repeat: đảm bảo không bao giờ trả về cùng con vật 2 lần liên tiếp (nếu pool ≥ 2).
///   - Thread-safe đủ dùng cho Unity main thread.
/// </summary>
public static class CountingAnimalPicker
{
    /// <summary>
    /// Đường dẫn Resources chứa sprite loài vật.
    /// → Assets/Resources/Counting/*.png
    /// </summary>
    public const string ResourcePath = "Counting";

    static Sprite[] _pool;
    static int      _lastIdx = -1;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Trả về 1 sprite loài vật ngẫu nhiên.
    /// Tự load lần đầu nếu chưa load. Không bao giờ trùng sprite liên tiếp (pool ≥ 2).
    /// </summary>
    public static Sprite GetRandom()
    {
        EnsureLoaded();

        if (_pool.Length == 0)
        {
            Debug.LogWarning($"[CountingAnimalPicker] Không có sprite nào tại Resources/{ResourcePath}/\n" +
                             "Hãy thêm ảnh loài vật (cat.png, dog.png, ...) vào thư mục đó.");
            return null;
        }

        if (_pool.Length == 1) return _pool[0];

        // Anti-repeat: loop cho đến khi chọn được index khác lần trước
        int idx;
        do { idx = Random.Range(0, _pool.Length); }
        while (idx == _lastIdx);

        _lastIdx = idx;
        return _pool[idx];
    }

    /// <summary>
    /// Preload sprites trước khi game bắt đầu — tuỳ chọn,
    /// GetRandom() cũng tự load nếu chưa load.
    /// </summary>
    public static void Preload() => EnsureLoaded();

    /// <summary>
    /// Reset cache — gọi khi thêm/xoá sprite lúc runtime (hiếm dùng).
    /// </summary>
    public static void Reset()
    {
        _pool    = null;
        _lastIdx = -1;
        Debug.Log("[CountingAnimalPicker] Cache đã reset.");
    }

    /// <summary>Số lượng sprite đã load (0 nếu chưa load hoặc thư mục trống).</summary>
    public static int Count
    {
        get { EnsureLoaded(); return _pool.Length; }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    static void EnsureLoaded()
    {
        if (_pool != null) return;

        _pool = Resources.LoadAll<Sprite>(ResourcePath);

        if (_pool.Length > 0)
            Debug.Log($"[CountingAnimalPicker] Loaded {_pool.Length} animal sprite(s) " +
                      $"from Resources/{ResourcePath}/");
        else
            Debug.LogWarning($"[CountingAnimalPicker] 0 sprite tại Resources/{ResourcePath}/ — " +
                             "thêm ảnh loài vật vào thư mục đó.");
    }
}
