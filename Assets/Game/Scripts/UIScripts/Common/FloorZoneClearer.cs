using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Floor-projector: hiện "Go to START position!" và chờ người chơi bước ra vùng
/// (không có click trong EXIT_DELAY giây). Khi xong gọi onDone.
/// Countdown "Next in X" do TestTongHopController xử lý sau đó.
///
/// Await          — 1 zone (nửa màn hình): dùng khi chỉ 1 bên cần clear.
/// AwaitBothSides — toàn màn hình, 2 nhãn: dùng khi cả 2 bên cùng cần clear.
/// </summary>
[RequireComponent(typeof(Image))]
public class FloorZoneClearer : MonoBehaviour, IPointerClickHandler
{
    const float EXIT_DELAY = 1f;

    Action _onDone;
    float  _lastClickTime;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Zone clearer trên 1 nửa màn hình (<paramref name="zone"/>).
    /// Sau EXIT_DELAY giây không click → gọi <paramref name="onDone"/> rồi tự hủy.
    /// </summary>
    public static FloorZoneClearer Await(RectTransform zone, Action onDone)
    {
        zone = ResolveRoot(zone);
        if (zone == null) { onDone?.Invoke(); return null; }
        zone.SetAsLastSibling();
        return Create(zone, onDone, labelXMin: 0f, labelXMax: 1f);
    }

    /// <summary>
    /// Await specific side (Left/Right) of a root RectTransform.
    /// </summary>
    public static FloorZoneClearer AwaitSide(Team team, RectTransform root, Action onDone)
    {
        root = ResolveRoot(root);
        if (root == null) { onDone?.Invoke(); return null; }
        var go = new GameObject($"_FloorZoneClearer_{team}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(root, false);

        if (team == Team.Left)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
        }

        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 20;
        go.AddComponent<GraphicRaycaster>();

        var img = go.AddComponent<Image>();
        img.color = Color.clear;

        BuildLabel(rt, 0f, 1f);

        var c = go.AddComponent<FloorZoneClearer>();
        c._onDone = onDone;
        c._lastClickTime = Time.time;
        c.StartCoroutine(c.Run());
        return c;
    }

    /// <summary>
    /// Zone clearer toàn màn hình — 2 nhãn trái/phải (1 nửa mỗi bên).
    /// Bất kỳ click nào đều reset timer. Sau EXIT_DELAY giây không click → gọi onDone.
    /// </summary>
    public static FloorZoneClearer AwaitBothSides(RectTransform root, Action onDone)
    {
        root = ResolveRoot(root);
        if (root == null) { onDone?.Invoke(); return null; }
        var go = new GameObject("_FloorZoneClearer_Both");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 20;
        go.AddComponent<GraphicRaycaster>();

        var img = go.AddComponent<Image>();
        img.color = Color.clear;

        BuildLabel(rt, 0f,   0.5f); // nhãn nửa trái
        BuildLabel(rt, 0.5f, 1f);   // nhãn nửa phải

        var c = go.AddComponent<FloorZoneClearer>();
        c._onDone        = onDone;
        c._lastClickTime = Time.time;
        c.StartCoroutine(c.Run());
        return c;
    }

    /// <summary>
    /// Chờ clear nếu GameSettings.WaitForClear bật, ngược lại gọi callback ngay.
    /// </summary>
    public static void AwaitIfEnabled(RectTransform root, Action onDone)
    {
        if (GameSettings.Instance != null && GameSettings.Instance.WaitForClear)
        {
            AwaitBothSides(root, onDone);
        }
        else
        {
            onDone?.Invoke();
        }
    }

    /// <summary>
    /// Chờ clear 1 bên nếu GameSettings.WaitForClear bật, ngược lại gọi callback ngay.
    /// </summary>
    public static void AwaitSideIfEnabled(Team team, RectTransform root, Action onDone)
    {
        if (GameSettings.Instance != null && GameSettings.Instance.WaitForClear)
        {
            AwaitSide(team, root, onDone);
        }
        else
        {
            onDone?.Invoke();
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Nhiều controller truyền gameView.GetComponent&lt;RectTransform&gt;(), nhưng view có thể nằm trên
    /// object gốc chỉ có Transform thường (vd AddNumberGame/SubNumberGame) → null, hoặc nằm ngoài Canvas.
    /// Khi đó vùng clear bị tạo ngoài Canvas (phủ cả 2 bên, nhận click của bên kia → không bao giờ clear)
    /// hoặc crash. Fallback: dùng Canvas gốc (không phải World Space) của scene.
    /// </summary>
    static RectTransform ResolveRoot(RectTransform root)
    {
        if (root != null && root.GetComponentInParent<Canvas>() != null) return root;

        foreach (var c in FindObjectsOfType<Canvas>())
        {
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                return c.transform as RectTransform;
        }
        Debug.LogWarning("NDL: FloorZoneClearer — không tìm thấy Canvas gốc, bỏ qua chờ clear.");
        return null;
    }

    static FloorZoneClearer Create(RectTransform parent, Action onDone, float labelXMin, float labelXMax)
    {
        var go = new GameObject("_FloorZoneClearer");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 20;
        go.AddComponent<GraphicRaycaster>();

        var img = go.AddComponent<Image>();
        img.color = Color.clear;

        BuildLabel(rt, labelXMin, labelXMax);

        var c = go.AddComponent<FloorZoneClearer>();
        c._onDone        = onDone;
        c._lastClickTime = Time.time;
        c.StartCoroutine(c.Run());
        return c;
    }

    static void BuildLabel(RectTransform parent, float xMin, float xMax)
    {
        var go = new GameObject("HintLabel");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(xMin, 0.1f);
        rt.anchorMax = new Vector2(xMax, 0.4f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bgImg = go.AddComponent<Image>();
        bgImg.color         = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = false;

        var textGo = new GameObject("Text");
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text          = "Go to START position!";
        tmp.fontSize      = 48f;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = new Color(1f, 0.92f, 0.2f, 1f);
        tmp.raycastTarget = false;
    }

    public void OnPointerClick(PointerEventData _) => _lastClickTime = Time.time;

    IEnumerator Run()
    {
        while (Time.time - _lastClickTime < EXIT_DELAY)
            yield return null;

        var cb = _onDone;
        Destroy(gameObject);
        cb?.Invoke();
    }
}
