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
        zone.SetAsLastSibling();
        return Create(zone, onDone, labelXMin: 0f, labelXMax: 1f);
    }

    /// <summary>
    /// Zone clearer toàn màn hình — 2 nhãn trái/phải (1 nửa mỗi bên).
    /// Bất kỳ click nào đều reset timer. Sau EXIT_DELAY giây không click → gọi onDone.
    /// </summary>
    public static FloorZoneClearer AwaitBothSides(RectTransform root, Action onDone)
    {
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

    // ── Internals ─────────────────────────────────────────────────────────────

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
