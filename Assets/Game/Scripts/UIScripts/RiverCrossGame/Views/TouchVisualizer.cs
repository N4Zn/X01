using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị phản hồi thị giác cho mọi touch trên màn chiếu sàn.
///
/// Hoàn toàn độc lập với game logic — chỉ đọc Input.touches.
///   • Touch bắt đầu  → hiện vòng tròn tại vị trí chân
///   • Touch di chuyển → vòng tròn đi theo
///   • Touch kết thúc  → vòng tròn biến mất ngay
///
/// Setup: attach vào bất kỳ GameObject nào trong scene.
/// Tự tạo Canvas overlay riêng (sortingOrder = 999) để luôn hiện trên mọi UI.
///
/// Sau này: thay sprite thành bàn chân bằng cách gán footSprite trên Inspector.
/// </summary>
public class TouchVisualizer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Appearance")]
    [Tooltip("Để trống = dùng vòng tròn procedural")]
    [SerializeField] Sprite customSprite;

    [SerializeField] Color  circleColor = new Color(1f, 0.95f, 0.55f, 0.72f); // vàng nhạt
    [SerializeField] float  diameter    = 96f;    // px (canvas units)
    [SerializeField] int    poolSize    = 10;     // tối đa N touch đồng thời

    // ── Internals ─────────────────────────────────────────────────────────────

    RectTransform              _canvasRT;
    List<RectTransform>        _pool   = new List<RectTransform>();
    Dictionary<int, RectTransform> _active = new Dictionary<int, RectTransform>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Canvas overlay riêng — luôn vẽ trên cùng, không cần GraphicRaycaster
        var cvGO = new GameObject("_TouchFeedbackCanvas");
        cvGO.transform.SetParent(transform, false);

        var cv          = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 999;
        cvGO.AddComponent<CanvasScaler>();

        _canvasRT = cvGO.GetComponent<RectTransform>();

        // Sprite: custom nếu có, không thì tạo vòng tròn procedural
        var sprite = customSprite != null ? customSprite : MakeCircleSprite(128);

        // Khởi tạo pool
        for (int i = 0; i < poolSize; i++)
        {
            var go  = new GameObject("fp", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cvGO.transform, false);

            var img           = go.GetComponent<Image>();
            img.sprite        = sprite;
            img.color         = circleColor;
            img.raycastTarget = false;

            var rt            = go.GetComponent<RectTransform>();
            rt.anchorMin      = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot          = new Vector2(0.5f, 0.5f);
            rt.sizeDelta      = new Vector2(diameter, diameter);

            go.SetActive(false);
            _pool.Add(rt);
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
#if UNITY_EDITOR
        UpdateMouse();
#else
        UpdateTouches();
#endif
    }

    void UpdateTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    Activate(t.fingerId, t.position);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    MoveTo(t.fingerId, t.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    Deactivate(t.fingerId);
                    break;
            }
        }
    }

    void UpdateMouse()
    {
        // Editor: chuột trái giả lập 1 touch
        const int ID = -999;
        if      (Input.GetMouseButtonDown(0)) Activate  (ID, Input.mousePosition);
        else if (Input.GetMouseButton(0))     MoveTo    (ID, Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))   Deactivate(ID);
    }

    // ── Pool helpers ──────────────────────────────────────────────────────────

    void Activate(int id, Vector2 screenPos)
    {
        if (_active.ContainsKey(id) || _pool.Count == 0) return;

        var rt = _pool[_pool.Count - 1];
        _pool.RemoveAt(_pool.Count - 1);

        _active[id] = rt;
        rt.gameObject.SetActive(true);
        PlaceAt(rt, screenPos);
    }

    void MoveTo(int id, Vector2 screenPos)
    {
        if (_active.TryGetValue(id, out var rt))
            PlaceAt(rt, screenPos);
    }

    void Deactivate(int id)
    {
        if (!_active.TryGetValue(id, out var rt)) return;
        _active.Remove(id);
        rt.gameObject.SetActive(false);
        _pool.Add(rt);
    }

    void PlaceAt(RectTransform rt, Vector2 screenPos)
    {
        // ScreenSpaceOverlay: truyền camera = null
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, screenPos, null, out Vector2 localPos);
        rt.anchoredPosition = localPos;
    }

    // ── Procedural circle sprite ──────────────────────────────────────────────

    /// <summary>
    /// Tạo sprite vòng tròn mềm: lõi sáng đục dần ra rìa, anti-alias ở cạnh ngoài.
    /// Dùng Color.white → tint bằng Image.color trên Inspector.
    /// </summary>
    static Sprite MakeCircleSprite(int size)
    {
        var tex            = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode     = FilterMode.Bilinear;
        tex.wrapMode       = TextureWrapMode.Clamp;

        float r      = size * 0.5f;
        float rSolid = r * 0.50f;   // bán kính vùng đặc (alpha = 1)
        float rFade  = r * 0.92f;   // bán kính bắt đầu anti-alias cạnh
        float rAA    = r;           // bán kính ngoài cùng (alpha = 0)

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx   = x + 0.5f - r;
                float dy   = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= rAA) { tex.SetPixel(x, y, Color.clear); continue; }

                float alpha;
                if (dist <= rSolid)
                {
                    // Lõi: hoàn toàn đục
                    alpha = 1f;
                }
                else if (dist <= rFade)
                {
                    // Vùng gradient: đục → mờ nhẹ
                    alpha = Mathf.SmoothStep(1f, 0.25f, (dist - rSolid) / (rFade - rSolid));
                }
                else
                {
                    // Rìa ngoài: anti-alias
                    alpha = Mathf.Lerp(0.25f, 0f, (dist - rFade) / (rAA - rFade));
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: size);
    }
}
