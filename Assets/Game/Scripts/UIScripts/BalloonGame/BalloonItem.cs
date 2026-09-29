using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Một balloon di chuyển từ dưới lên trên. BalloonField gọi Tick() mỗi frame.
/// Thông số breathing đọc từ BalloonGameConfig.
/// </summary>
public class BalloonItem : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image            balloonBg;
    [SerializeField] TextMeshProUGUI  label;
    [SerializeField] Image            iconImage;
    [SerializeField] BalloonGameConfig config;

    [SerializeField] Color correctColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] Color wrongColor   = new Color(1f, 0.4f, 0.4f);

    public string Value    { get; private set; }
    public bool   IsPopped { get; private set; }
    public bool   IsAlive  => gameObject.activeSelf && !IsPopped;

    RectTransform       _rt;
    float               _speed;
    Color               _currentColor;
    float               _breathOffset;
    Action<BalloonItem> _onClick;
    Action<BalloonItem> _onReadyForRespawn;

    static Sprite _circleSprite;

    static readonly Color[] Palette =
    {
        new Color(1.00f, 0.32f, 0.32f),
        new Color(1.00f, 0.60f, 0.15f),
        new Color(1.00f, 0.90f, 0.15f),
        new Color(0.25f, 0.85f, 0.35f),
        new Color(0.20f, 0.75f, 1.00f),
        new Color(0.40f, 0.40f, 1.00f),
        new Color(0.80f, 0.30f, 1.00f),
        new Color(1.00f, 0.35f, 0.75f),
    };

    void Awake() => _rt = (RectTransform)transform;

    // ── Setup ────────────────────────────────────────────────────────────────

    public void SetCallbacks(Action<BalloonItem> onClick, Action<BalloonItem> onReadyForRespawn)
    {
        _onClick           = onClick;
        _onReadyForRespawn = onReadyForRespawn;
    }

    // ── Activate ─────────────────────────────────────────────────────────────

    public void Activate(string value, Sprite icon, float speed, Vector2 pos)
    {
        if (_rt == null) _rt = (RectTransform)transform;

        _currentColor = Palette[UnityEngine.Random.Range(0, Palette.Length)];
        _breathOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        if (balloonBg != null && balloonBg.sprite == null)
            balloonBg.sprite = GetOrCreateCircle();

        Value    = value;
        IsPopped = false;
        _speed   = speed;

        bool useIcon = icon != null && iconImage != null;
        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = useIcon; }
        if (label != null)
        {
            label.enabled = !useIcon;
            if (!useIcon) label.text = value;
        }

        _rt.anchoredPosition = pos;
        transform.localScale = Vector3.one;
        ResetVisuals();
        gameObject.SetActive(true);
    }

    // ── Driven by BalloonField.Update ────────────────────────────────────────

    public Vector2 AnchoredPos => _rt.anchoredPosition;

    public void Tick(float dt)
    {
        if (!IsAlive) return;

        _rt.anchoredPosition += Vector2.up * _speed * dt;

        float bs = config != null ? config.breathSpeed  : 2.2f;
        float ba = config != null ? config.breathAmount : 0.07f;
        float breath = 1f + Mathf.Sin(Time.time * bs + _breathOffset) * ba;
        transform.localScale = Vector3.one * breath;
    }

    public void ForceExit()
    {
        IsPopped = true;
        gameObject.SetActive(false);
        _onReadyForRespawn?.Invoke(this);
    }

    // ── Click ────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData _)
    {
        if (!IsAlive) return;
        _onClick?.Invoke(this);
    }

    public void Pop(bool isCorrect)
    {
        if (IsPopped) return;
        IsPopped = true;
        StartCoroutine(PopRoutine(isCorrect));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    IEnumerator PopRoutine(bool isCorrect)
    {
        if (balloonBg != null) balloonBg.color = isCorrect ? correctColor : wrongColor;

        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float p = t / 0.22f;
            transform.localScale = Vector3.one * (1f + p * 0.7f);
            SetAlpha(1f - p);
            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        ResetVisuals();
        _onReadyForRespawn?.Invoke(this);
    }

    void SetAlpha(float a)
    {
        if (balloonBg != null) { var c = balloonBg.color; c.a = a; balloonBg.color = c; }
        if (label     != null) label.alpha = a;
        if (iconImage != null) { var c = iconImage.color; c.a = a; iconImage.color = c; }
    }

    void ResetVisuals()
    {
        if (balloonBg != null) balloonBg.color = _currentColor;
        if (label     != null) label.alpha     = 1f;
        if (iconImage != null) { var c = iconImage.color; c.a = 1f; iconImage.color = c; }
    }

    // ── Circle sprite (shared) ────────────────────────────────────────────────

    static Sprite GetOrCreateCircle()
    {
        if (_circleSprite != null) return _circleSprite;

        const int S  = 128;
        const float R = S / 2f - 1.5f;
        float cx = S / 2f, cy = S / 2f;

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
            float alpha = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 1.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }
}
