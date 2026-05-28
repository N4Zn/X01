using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable feedback animation for correct/wrong answer display.
/// Attach to feedback icon/text GameObjects. Call Play() to trigger.
/// Handles: scale bounce in, pulse, shake (wrong), fade out.
/// </summary>
public class FeedbackEffect : MonoBehaviour
{
    // Visual size multiplier for the feedback icon (1.5 = 50% bigger than the icon's base size)
    private const float BaseScale = 1.5f;
    private static readonly Vector3 BaseScaleVec = Vector3.one * BaseScale;

    private Coroutine _activeRoutine;
    private CanvasGroup _canvasGroup;

    private void EnsureCanvasGroup()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Play feedback animation. Call after SetActive(true).
    /// </summary>
    /// <param name="isCorrect">true = bounce+pulse, false = bounce+shake</param>
    /// <param name="duration">Tổng thời gian animation (không tính fade out)</param>
    /// <param name="withFadeOut">false = giữ nguyên icon sau animation, không tự fade</param>
    public void Play(bool isCorrect, float duration = 1.5f, bool withFadeOut = true)
    {
        EnsureCanvasGroup();
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);

        // Reset state
        transform.localScale = BaseScaleVec;
        _canvasGroup.alpha = 1f;

        _activeRoutine = StartCoroutine(RunEffect(isCorrect, duration, withFadeOut));
    }

    public void StopEffect()
    {
        if (_activeRoutine != null) { StopCoroutine(_activeRoutine); _activeRoutine = null; }
        transform.localScale = BaseScaleVec;
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private IEnumerator RunEffect(bool isCorrect, float duration, bool withFadeOut)
    {
        Vector3 basePos = transform.localPosition;

        // Phase 1: "Explosion" Pop (0 → 2.0 → 1.5) over 0.25s
        float bounceTime = 0.25f;
        float t = 0f;
        while (t < bounceTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / bounceTime);
            float scale;
            if (p < 0.6f) // Rapidly expand to 2.0 (60% of time)
                scale = Mathf.Lerp(0f, 2.0f, p / 0.6f);
            else // Shrink back to 1.5 (40% of time)
                scale = Mathf.Lerp(2.0f, 1.5f, (p - 0.6f) / 0.4f);

            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one * 1.5f;

        // Phase 2: Shake (wrong) or Pulse (correct)
        // If duration is short (like 0.25s for wrong answer), skip further phases
        float holdTime = duration - bounceTime;
        if (holdTime <= 0) { _activeRoutine = null; yield break; }

        if (!isCorrect)
        {
            // Shake: rapid oscillations over 0.35s
            float shakeTime = 0.35f;
            float shakeMag = 10f;
            t = 0f;
            while (t < shakeTime)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / shakeTime);
                float offset = Mathf.Sin(t * 45f) * shakeMag * decay;
                transform.localPosition = basePos + new Vector3(offset, 0f, 0f);
                yield return null;
            }
            transform.localPosition = basePos;

            float remaining = holdTime - shakeTime;
            if (remaining > 0f) yield return new WaitForSeconds(remaining);
        }
        else
        {
            // Pulse: gentle scale oscillation around BaseScale
            t = 0f;
            while (t < holdTime)
            {
                t += Time.deltaTime;
                float pulse = 1f + 0.1f * Mathf.Sin(t * 7f);
                transform.localScale = BaseScaleVec * pulse;
                yield return null;
            }
            transform.localScale = BaseScaleVec;
        }

        // Phase 3: Fade out over 0.3s (chỉ chạy nếu withFadeOut = true)
        if (withFadeOut)
        {
            float fadeTime = 0.3f;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / fadeTime);
                _canvasGroup.alpha = 1f - progress;
                transform.localScale = BaseScaleVec * Mathf.Lerp(1f, 0.6f, progress);
                yield return null;
            }
        }

        // Reset — let controller handle SetActive(false)
        transform.localScale = BaseScaleVec;
        _canvasGroup.alpha = 1f;
        _activeRoutine = null;
    }
}
