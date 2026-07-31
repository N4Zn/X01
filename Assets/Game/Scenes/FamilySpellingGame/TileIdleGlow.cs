using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn script này vào object "ButtonDisplay" (cùng chỗ với TileAppearAnimator).
/// Cho tất cả các ô đáp án con (LeftBtn_0..3, RightBtn_0..3) tự động:
///   - Nhấp nhô nhẹ liên tục (scale "thở" lên xuống), mỗi ô lệch pha khác nhau cho tự nhiên.
///   - Viền sáng (Outline) tự động thêm nếu chưa có, nhấp nháy độ sáng liên tục.
///
/// Hiệu ứng này CHẠY LIÊN TỤC ngay khi ô đang hiển thị (Idle), không cần chờ bấm — mục đích
/// để thu hút sự chú ý của bé, mời gọi bấm vào. Không ảnh hưởng hiệu ứng nảy vào lúc xuất hiện
/// (TileAppearAnimator) hay hiệu ứng đúng/sai (do ButtonItem xử lý) — script này chỉ chạy song
/// song, độc lập.
/// </summary>
public class TileIdleGlow : MonoBehaviour
{
    [Header("Nhấp nhô (breathing)")]
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float breathAmount = 0.05f; // 0.05 = phóng to/thu nhỏ 5%
    [SerializeField] private float breathSpeed = 1.5f;   // chu kỳ mỗi giây

    [Header("Viền sáng (glow outline)")]
    [SerializeField] private bool enableGlow = true;
    [SerializeField] private Color glowColor = new Color(1f, 0.95f, 0.4f); // vàng sáng
    [SerializeField] private Vector2 glowDistance = new Vector2(3f, -3f);
    [SerializeField] private float glowMinAlpha = 0.15f;
    [SerializeField] private float glowMaxAlpha = 0.85f;
    [SerializeField] private float glowSpeed = 1.2f;

    private readonly List<RectTransform> _tiles = new List<RectTransform>();
    private readonly List<Vector3> _originalScales = new List<Vector3>();
    private readonly List<Outline> _outlines = new List<Outline>();
    private readonly List<float> _phaseOffsets = new List<float>();

    private void OnEnable()
    {
        CacheTilesAndStart();
    }

    private void CacheTilesAndStart()
    {
        _tiles.Clear();
        _originalScales.Clear();
        _outlines.Clear();
        _phaseOffsets.Clear();

        foreach (Transform child in transform)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;

            _tiles.Add(rt);
            _originalScales.Add(rt.localScale);
            _phaseOffsets.Add(Random.Range(0f, Mathf.PI * 2f)); // lệch pha ngẫu nhiên cho tự nhiên

            Outline outline = child.GetComponent<Outline>();
            if (outline == null && enableGlow)
                outline = child.gameObject.AddComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor = glowColor;
                outline.effectDistance = glowDistance;
                outline.enabled = enableGlow;
            }
            _outlines.Add(outline);
        }

        StopAllCoroutines();
        StartCoroutine(IdleLoop());
    }

    private IEnumerator IdleLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;

            for (int i = 0; i < _tiles.Count; i++)
            {
                var rt = _tiles[i];
                if (rt == null) continue;

                float phase = t + _phaseOffsets[i];

                if (enableBreathing)
                {
                    float wave = Mathf.Sin(phase * breathSpeed * Mathf.PI * 2f);
                    float scaleFactor = 1f + wave * breathAmount;
                    rt.localScale = _originalScales[i] * scaleFactor;
                }

                if (enableGlow && _outlines[i] != null)
                {
                    float glowWave = (Mathf.Sin(phase * glowSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
                    float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, glowWave);
                    var c = glowColor;
                    c.a = alpha;
                    _outlines[i].effectColor = c;
                }
            }

            yield return null;
        }
    }

    /// <summary>Gọi lại nếu số lượng ô con thay đổi lúc runtime (ví dụ đổi số đáp án).</summary>
    public void RefreshTiles() => CacheTilesAndStart();
}
