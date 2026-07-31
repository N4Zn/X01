using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tự động tính cellSize cho GridLayoutGroup để icons lấp đầy chiều ngang container.
/// - Được add vào iconContainer bằng code trong ItemMediaHelper (không cần cấu hình prefab)
/// - Tự disable và xoá HorizontalLayoutGroup / VerticalLayoutGroup đang có sẵn
/// - Chỉ cần chiều rộng (w) hợp lệ — chiều cao tự expand theo nội dung
/// </summary>
[DisallowMultipleComponent]
public class IconAutoGrid : MonoBehaviour
{
    public int columns = 2;

    GridLayoutGroup _grid;
    RectTransform   _rt;
    bool            _pending;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _rt = (RectTransform)transform;

        // QUAN TRỌNG: disabled = false có hiệu lực NGAY trong frame này
        // Destroy() chỉ có hiệu lực cuối frame — nếu chỉ Destroy mà không disable,
        // HorizontalLayoutGroup vẫn kịp sắp xếp icons thành 1 hàng ngang
        var h = GetComponent<HorizontalLayoutGroup>();
        if (h != null) { h.enabled = false; Destroy(h); }

        var v = GetComponent<VerticalLayoutGroup>();
        if (v != null) { v.enabled = false; Destroy(v); }

        // Lấy hoặc thêm GridLayoutGroup
        _grid = GetComponent<GridLayoutGroup>();
        if (_grid == null) _grid = gameObject.AddComponent<GridLayoutGroup>();

        _grid.enabled         = true;
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.childAlignment  = TextAnchor.MiddleCenter;
        _grid.padding         = new RectOffset(0, 0, 0, 0);
    }

    void OnEnable()                        => Schedule();
    void OnTransformChildrenChanged()      => Schedule();
    void OnRectTransformDimensionsChange() => Schedule();

    // ─── Schedule / Refresh ──────────────────────────────────────────────────

    void Schedule()
    {
        if (_pending || !gameObject.activeInHierarchy) return;
        _pending = true;
        StartCoroutine(RefreshNextFrame());
    }

    IEnumerator RefreshNextFrame()
    {
        // Chờ 1 frame để layout system tính xong kích thước thực
        yield return null;
        _pending = false;
        Refresh();
    }

    public void Refresh()
    {
        if (_grid == null) return;

        // Nếu width chưa sẵn sàng, thử force rebuild parent
        if (_rt.rect.width <= 1f && transform.parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform.parent);

        float w = _rt.rect.width;
        if (w <= 1f) return; // chỉ cần width — height tự expand

        int count = transform.childCount;
        if (count == 0) return;

        // Icon vuông: lấy cellW từ chiều ngang / số cột
        // height của container tự mở rộng theo số hàng
        float gap  = Mathf.Max(w * 0.04f, 2f);
        float cell = Mathf.Max((w - gap * (columns - 1)) / columns, 8f);

        _grid.spacing  = new Vector2(gap, gap);
        _grid.cellSize = new Vector2(cell, cell);
    }
}
