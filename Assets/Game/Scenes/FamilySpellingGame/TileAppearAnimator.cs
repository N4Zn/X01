using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn script này vào object "ButtonDisplay" (object cha chứa LeftBtn_0..3, RightBtn_0..3).
///
/// Cách dùng:
/// - Trong code hiện tại của bạn, chỗ nào đang gán chữ vào các ô (LeftBtn_0.TextSlot.text = "M"; v.v.)
///   và làm các ô hiện lên, hãy gọi thêm:
///       tileAnimator.PlayAppearAnimation();
///   (lấy component TileAppearAnimator trên object ButtonDisplay)
///
/// - Nếu bạn không chắc chỗ nào gọi, có thể tạm thời gọi nó trong Start() của chính script này
///   để test thử trước (đã có sẵn dòng gọi ở dưới, bạn có thể bật/tắt bằng playOnStart).
/// </summary>
public class TileAppearAnimator : MonoBehaviour
{
    [Header("Cấu hình")]
    [Tooltip("Nếu bật, script sẽ tự chạy hiệu ứng ngay khi scene load (dùng để test nhanh).")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("Thời gian trễ giữa các ô nảy vào lần lượt (giây).")]
    [SerializeField] private float staggerDelay = 0.06f;

    [Tooltip("Thời gian nảy vào của mỗi ô (giây).")]
    [SerializeField] private float popDuration = 0.35f;

    [Tooltip("Độ phóng to quá đà khi nảy (1 = không phóng, 1.15 = phóng thêm 15%).")]
    [SerializeField] private float overshoot = 1.15f;

    // Danh sách các ô sẽ animate - tự động lấy tất cả các object con trực tiếp
    private List<RectTransform> tiles = new List<RectTransform>();
    private Dictionary<RectTransform, Vector3> originalScales = new Dictionary<RectTransform, Vector3>();

    private void Awake()
    {
        CacheTiles();
    }

    private void Start()
    {
        if (playOnStart)
            PlayAppearAnimation();
    }

    private void CacheTiles()
    {
        tiles.Clear();
        originalScales.Clear();

        // Lấy tất cả các object con trực tiếp của ButtonDisplay
        // (LeftBtn_0, LeftBtn_1, ..., RightBtn_3)
        foreach (Transform child in transform)
        {
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            tiles.Add(rt);
            originalScales[rt] = rt.localScale;
        }
    }

    /// <summary>
    /// Gọi hàm này mỗi khi câu hỏi mới bắt đầu / các ô chữ vừa được set nội dung xong,
    /// để chúng nảy vào lần lượt từ trái qua phải.
    /// </summary>
    public void PlayAppearAnimation()
    {
        // Đề phòng trường hợp có ô được thêm/xóa sau Awake (ví dụ đổi số lượng đáp án)
        if (tiles.Count == 0) CacheTiles();

        StopAllCoroutines();
        StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        // Set tất cả về scale 0 trước
        foreach (var rt in tiles)
        {
            if (rt == null) continue;
            rt.localScale = Vector3.zero;
        }

        // Cho từng ô nảy vào, cách nhau staggerDelay giây
        for (int i = 0; i < tiles.Count; i++)
        {
            RectTransform rt = tiles[i];
            if (rt == null) continue;

            StartCoroutine(PopOne(rt));
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    private IEnumerator PopOne(RectTransform rt)
    {
        Vector3 target = originalScales.TryGetValue(rt, out var s) ? s : Vector3.one;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popDuration);
            float e = EaseOutBack(p);
            rt.localScale = target * Mathf.LerpUnclamped(0f, overshoot, e);
            yield return null;
        }

        rt.localScale = target;
    }

    // Easing "nảy vào rồi ổn định" - giống hiệu ứng bóng nảy
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
