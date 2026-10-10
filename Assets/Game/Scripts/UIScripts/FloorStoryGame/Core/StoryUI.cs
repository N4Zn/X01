using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Chạm (LiDAR bắn pointerDown/Up/Click cùng lúc tại 1 điểm — KHÔNG có kéo/giữ) → gọi onTap.</summary>
public sealed class StoryTap : MonoBehaviour, IPointerClickHandler
{
    public Action onTap;
    /// <summary>Có vị trí chạm (toạ độ màn hình) — để thả hiệu ứng đúng chỗ chân/tay chạm.</summary>
    public Action<Vector2> onTapAt;
    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke();
        onTapAt?.Invoke(eventData.position);
    }
}

/// <summary>Thẻ bấm: nền bo góc + (ảnh) + (chữ). Ảnh thiếu thì chỉ còn chữ → vẫn chơi được trước khi có ảnh thật.</summary>
public sealed class StoryCard
{
    public RectTransform rt;
    public Image bg;
    public Image icon;
    public Text label;
    public bool hasIcon;
    public Vector2 basePos;
}

/// <summary>Helper dựng UI + tween bằng code cho các game "FloorStory". Toạ độ = pixel canvas, gốc ở GIỮA world.</summary>
public static class StoryUI
{
    // ── Font / sprite ─────────────────────────────────────────────────────────

    static Font _font;
    public static Font Font
    {
        get
        {
            if (_font != null) return _font;
            _font = Resources.Load<Font>("Fonts/Quicksand/Quicksand-Bold");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    /// <summary>Tên game đang chạy (= tên scene). Ảnh trong `Resources/StoryPack/&lt;PackGame&gt;/&lt;path&gt;` (từ gói zip thay ảnh) thắng ảnh gốc.</summary>
    public static string PackGame = "";

    /// <summary>Tải sprite từ Resources (null nếu chưa có ảnh). Ưu tiên ảnh thay thế của gói (StoryPack/&lt;game&gt;/&lt;path&gt;, rồi StoryPack/_all/&lt;path&gt;).</summary>
    public static Sprite Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!string.IsNullOrEmpty(PackGame))
        {
            var o = LoadRaw("StoryPack/" + PackGame + "/" + path) ?? LoadRaw("StoryPack/_all/" + path);
            if (o != null) return o;
        }
        return LoadRaw(path);
    }

    /// <summary>Tải sprite từ Resources (null nếu chưa có ảnh). Chấp nhận cả texture chưa đặt kiểu Sprite.</summary>
    static Sprite LoadRaw(string path)
    {
        // Cache cả kết quả "không có ảnh" (null thật) để khỏi Resources.Load lặp lại mỗi lượt; sprite đã bị huỷ thì tải lại.
        if (Sprites.TryGetValue(path, out var s) && (s != null || ReferenceEquals(s, null))) return s;
        s = Resources.Load<Sprite>(path);
        if (s == null)
        {
            var t = Resources.Load<Texture2D>(path);
            if (t != null) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        Sprites[path] = s;
        return s;
    }

    public static Color Hex(string html)
    {
        return ColorUtility.TryParseHtmlString(html, out var c) ? c : Color.magenta;
    }

    // ── Dựng phần tử ──────────────────────────────────────────────────────────

    public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Pic(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool raycast = false)
    {
        var rt = Rect(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        return img;
    }

    /// <summary>Phủ kín `parent` (anchor 0..1, offset 0).</summary>
    public static Image Fill(Transform parent, string name, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    public static Text Label(Transform parent, string text, int fontSize, Color color, Vector2 pos, Vector2 size,
                             TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = true)
    {
        var rt = Rect(parent, "Label", pos, size);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(10, fontSize / 2);
        t.resizeTextMaxSize = fontSize;
        t.raycastTarget = false;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
        }
        return t;
    }

    public static void OnTap(GameObject go, Action cb)
    {
        var tap = go.GetComponent<StoryTap>();
        if (tap == null) tap = go.AddComponent<StoryTap>();
        tap.onTap = cb;
        var g = go.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;
    }

    /// <summary>Vùng chạm vô hình phủ kín `parent`; trả về vị trí chạm quy về toạ độ local của parent (gốc ở giữa).</summary>
    public static void TapArea(RectTransform parent, Action<Vector2> onLocalTap)
    {
        var img = Fill(parent, "TapArea", new Color(1f, 1f, 1f, 0f));
        var tap = img.gameObject.AddComponent<StoryTap>();
        img.raycastTarget = true;
        tap.onTapAt = screen =>
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
                onLocalTap(local);
        };
    }

    /// <summary>Thẻ bấm. `iconSprite` null → chỉ hiện chữ to giữa thẻ.</summary>
    public static StoryCard Card(Transform parent, string name, Sprite iconSprite, string label, Color bgColor,
                                 Vector2 pos, Vector2 size, Action onTap, int fontSize = 30)
    {
        var c = new StoryCard { basePos = pos };
        var bg = Pic(parent, name, ShapeSprites.RoundedRect, bgColor, pos, size, raycast: true);
        bg.type = Image.Type.Sliced;
        bg.preserveAspect = false;
        c.rt = bg.rectTransform;
        c.bg = bg;
        c.hasIcon = iconSprite != null;
        if (c.hasIcon)
        {
            float lh = string.IsNullOrEmpty(label) ? 0f : size.y * 0.24f;
            c.icon = Pic(bg.transform, "Icon", iconSprite, Color.white,
                         new Vector2(0f, lh * 0.5f), new Vector2(size.x * 0.86f, size.y * 0.9f - lh));
        }
        if (!string.IsNullOrEmpty(label))
        {
            if (c.hasIcon)
                c.label = Label(bg.transform, label, Mathf.Max(18, fontSize - 8), Color.white, new Vector2(0f, -size.y * 0.38f), new Vector2(size.x * 0.96f, size.y * 0.26f));
            else
                c.label = Label(bg.transform, label, fontSize, Color.white, Vector2.zero, new Vector2(size.x * 0.92f, size.y * 0.86f));
        }
        if (onTap != null) OnTap(bg.gameObject, onTap);
        return c;
    }

    public static void Destroy(UnityEngine.Object o) { if (o != null) UnityEngine.Object.Destroy(o); }

    public static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            c.SetParent(null);
            UnityEngine.Object.Destroy(c.gameObject);
        }
    }

    // ── Tween (coroutine) ─────────────────────────────────────────────────────

    static float Smooth(float t) => t * t * (3f - 2f * t);
    static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

    public static IEnumerator MoveTo(RectTransform rt, Vector2 to, float dur)
    {
        if (rt == null) yield break;
        var from = rt.anchoredPosition;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Smooth(t / dur));
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = to;
    }

    /// <summary>Bay tới vị trí mới đồng thời đổi kích thước (vd hình bay từ thẻ vào chỗ trống).</summary>
    public static IEnumerator MoveResize(RectTransform rt, Vector2 to, Vector2 toSize, float dur)
    {
        if (rt == null) yield break;
        var from = rt.anchoredPosition;
        var fromSize = rt.sizeDelta;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = Smooth(t / dur);
            rt.anchoredPosition = Vector2.Lerp(from, to, k);
            rt.sizeDelta = Vector2.Lerp(fromSize, toSize, k);
            yield return null;
        }
        if (rt != null) { rt.anchoredPosition = to; rt.sizeDelta = toSize; }
    }

    public static IEnumerator ScaleTo(RectTransform rt, float to, float dur, bool overshoot = false)
    {
        if (rt == null) yield break;
        float from = rt.localScale.x;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = t / dur;
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, overshoot ? OutBack(k) : Smooth(k));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one * to;
    }

    /// <summary>Hiện ra từ 0 → 1 có nảy.</summary>
    public static IEnumerator PopIn(RectTransform rt, float dur = 0.35f)
    {
        if (rt == null) yield break;
        rt.localScale = Vector3.zero;
        yield return ScaleTo(rt, 1f, dur, overshoot: true);
    }

    public static IEnumerator Shake(RectTransform rt, float dur = 0.4f, float amp = 14f)
    {
        if (rt == null) yield break;
        var origin = rt.anchoredPosition;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float damp = 1f - t / dur;
            rt.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 50f) * amp * damp, 0f);
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = origin;
    }

    /// <summary>Nhún nhảy (to lên rồi về) — dùng cho phản hồi "đúng".</summary>
    public static IEnumerator Bounce(RectTransform rt, float amount = 0.25f, float dur = 0.5f)
    {
        if (rt == null) yield break;
        var baseScale = rt.localScale;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (rt == null) yield break;
            float k = Mathf.Sin(t / dur * Mathf.PI);
            rt.localScale = baseScale * (1f + amount * k);
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
    }

    public static IEnumerator Fade(Graphic g, float to, float dur)
    {
        if (g == null) yield break;
        float from = g.color.a;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (g == null) yield break;
            var c = g.color; c.a = Mathf.Lerp(from, to, t / dur); g.color = c;
            yield return null;
        }
        if (g != null) { var c = g.color; c.a = to; g.color = c; }
    }

    public static IEnumerator ColorTo(Graphic g, Color to, float dur)
    {
        if (g == null) yield break;
        var from = g.color;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (g == null) yield break;
            g.color = Color.Lerp(from, to, Smooth(t / dur));
            yield return null;
        }
        if (g != null) g.color = to;
    }

    /// <summary>Lơ lửng lên xuống tới khi đối tượng bị huỷ hoặc `alive()` = false.</summary>
    public static IEnumerator Bob(RectTransform rt, float amp, float speed, float phase = 0f, Func<bool> alive = null)
    {
        if (rt == null) yield break;
        var origin = rt.anchoredPosition;
        float t = phase;
        while (rt != null && (alive == null || alive()))
        {
            t += Time.deltaTime * speed;
            rt.anchoredPosition = origin + new Vector2(0f, Mathf.Sin(t) * amp);
            yield return null;
        }
    }

    /// <summary>Nhấp nháy to/nhỏ (gợi ý) cho tới khi `alive()` = false hoặc bị huỷ.</summary>
    public static IEnumerator Pulse(RectTransform rt, Func<bool> alive, float amount = 0.12f, float speed = 6f)
    {
        if (rt == null) yield break;
        var baseScale = rt.localScale;
        float t = 0f;
        while (rt != null && alive())
        {
            t += Time.deltaTime * speed;
            rt.localScale = baseScale * (1f + amount * (0.5f + 0.5f * Mathf.Sin(t)));
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
    }
}

/// <summary>Cầu nối world ↔ controller: chạy coroutine, âm thanh.</summary>
public sealed class StoryContext
{
    public MonoBehaviour runner;
    public AudioSource voice;
    /// <summary>Seed chung của 2 đội (cho game Synchronized ra cùng câu hỏi).</summary>
    public int sharedSeed;
    /// <summary>Cộng 1 điểm cho đội (ScoreManager.AddPoint).</summary>
    public Action<Team> addPoint;

    public Coroutine Run(IEnumerator e) => runner != null ? runner.StartCoroutine(e) : null;

    /// <summary>Phát giọng đọc `Resources/StoryVoice/&lt;key&gt;` nếu có file (chưa có thì im lặng).</summary>
    public void Voice(string key)
    {
        if (voice == null || string.IsNullOrEmpty(key)) return;
        var c = Resources.Load<AudioClip>("StoryVoice/" + key);
        if (c == null) return;
        voice.Stop();
        voice.PlayOneShot(c);
    }

    /// <summary>Hiệu ứng ngắn `Resources/StorySfx/&lt;key&gt;` (pop, tap, wood, bell, powerup, star, up, plop — Kenney CC0).</summary>
    public void Sfx(string key, float volume = 1f)
    {
        if (voice == null) return;
        var c = Resources.Load<AudioClip>("StorySfx/" + key);
        if (c != null) voice.PlayOneShot(c, volume);
    }

    public void SfxRight() => MusicManager.Instance?.PlayCorrectSfx();
    public void SfxWrong() => MusicManager.Instance?.PlayWrongSfx();
}
