using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avatar nhân vật — di chuyển bằng arc jump animation.
///
/// Trạng thái:
///   Idle    → đứng yên trên bè (hoặc bờ)
///   Jumping → đang trong cung nhảy (chặn input)
///   Falling → rơi xuống nước (animation ngắn)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PlayerAvatar : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] Image  characterImage;
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite jumpSprite;
    [SerializeField] Sprite fallSprite;

    RectTransform _rt;

    // ── State ──────────────────────────────────────────────────────────────────

    public bool IsJumping { get; private set; }

    public float X => _rt.anchoredPosition.x;
    public float Y => _rt.anchoredPosition.y;

    // ── Init ───────────────────────────────────────────────────────────────────

    void Awake() => _rt = GetComponent<RectTransform>();

    /// <summary>Teleport không có animation (dùng khi reset).</summary>
    public void SetPosition(float x, float y)
        => _rt.anchoredPosition = new Vector2(x, y);

    /// <summary>Chỉ cập nhật Y — bám theo bè đang trôi.</summary>
    public void SetY(float y)
        => _rt.anchoredPosition = new Vector2(X, y);

    // ── Jump ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Arc jump từ vị trí hiện tại đến (targetX, targetY).
    /// onLanded gọi sau khi animation xong.
    /// </summary>
    public void JumpTo(float targetX, float targetY,
                       float duration, float arcH,
                       Action onLanded)
    {
        if (IsJumping) return;
        StartCoroutine(JumpCoroutine(targetX, targetY, duration, arcH, onLanded));
    }

    IEnumerator JumpCoroutine(float tx, float ty, float duration, float arcH, Action onLanded)
    {
        IsJumping = true;
        SetSprite(jumpSprite);

        float fromX = X, fromY = Y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Lerp X+Y thẳng, thêm sin arc lên đỉnh rồi về
            float x = Mathf.Lerp(fromX, tx, t);
            float y = Mathf.Lerp(fromY, ty, t) + arcH * Mathf.Sin(t * Mathf.PI);
            _rt.anchoredPosition = new Vector2(x, y);

            // Flip sprite theo hướng nhảy ngang
            if (characterImage != null)
                characterImage.transform.localScale = new Vector3(tx > fromX ? 1f : -1f, 1f, 1f);

            yield return null;
        }

        _rt.anchoredPosition = new Vector2(tx, ty);
        IsJumping = false;
        SetSprite(idleSprite);
        onLanded?.Invoke();
    }

    // ── Fall ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Animation rơi xuống nước — bé rơi nhanh dần rồi biến mất.
    /// duration: tổng thời gian rơi (giây).
    /// acceleration: gia tốc — y = y₀ - acceleration × t² (canvas units/s²).
    /// onDone gọi sau khi xong.
    /// </summary>
    public void FallIntoWater(float duration, float acceleration, Action onDone)
    {
        StartCoroutine(FallCoroutine(duration, acceleration, onDone));
    }

    IEnumerator FallCoroutine(float duration, float acceleration, Action onDone)
    {
        IsJumping = true;
        SetSprite(fallSprite ?? idleSprite);

        float elapsed = 0f;
        float startY  = Y;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Gia tốc rơi: y = y₀ - a·t²
            SetY(startY - acceleration * elapsed * elapsed);
            yield return null;
        }

        IsJumping = false;
        onDone?.Invoke();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    void SetSprite(Sprite s)
    {
        if (characterImage != null && s != null)
            characterImage.sprite = s;
    }
}
