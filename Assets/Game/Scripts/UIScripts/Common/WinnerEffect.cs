using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable winner-highlight animation for ScoreScene elements (cup icon, polygon, badge).
/// Supports bounce-in (scale 0 → 1.4 → 1.0) and continuous pulse loop.
/// Attach to the target GameObject and call PlayBounceIn() / StartPulse().
/// </summary>
public class WinnerEffect : MonoBehaviour
{
    private Coroutine _bounceRoutine;
    private Coroutine _pulseRoutine;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;

    private void EnsureBaseScale()
    {
        if (!_baseCaptured)
        {
            _baseScale = transform.localScale;
            _baseCaptured = true;
        }
    }

    public void PlayBounceIn(float duration = 0.6f)
    {
        EnsureBaseScale();
        if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
        _bounceRoutine = StartCoroutine(BounceInRoutine(duration));
    }

    public void StartPulse(float amplitude = 0.1f, float speed = 3f)
    {
        EnsureBaseScale();
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseRoutine(amplitude, speed));
    }

    public void Stop()
    {
        if (_bounceRoutine != null) { StopCoroutine(_bounceRoutine); _bounceRoutine = null; }
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        if (_baseCaptured) transform.localScale = _baseScale;
    }

    private IEnumerator BounceInRoutine(float duration)
    {
        // Phase 1: scale 0 → 1.4 over 60% of duration
        float growTime = duration * 0.6f;
        float t = 0f;
        while (t < growTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / growTime);
            float scale = Mathf.Lerp(0f, 1.4f, EaseOutBack(p));
            transform.localScale = _baseScale * scale;
            yield return null;
        }

        // Phase 2: 1.4 → 1.0 over remaining 40%
        float settleTime = duration * 0.4f;
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / settleTime);
            float scale = Mathf.Lerp(1.4f, 1f, p);
            transform.localScale = _baseScale * scale;
            yield return null;
        }

        transform.localScale = _baseScale;
        _bounceRoutine = null;
    }

    private IEnumerator PulseRoutine(float amplitude, float speed)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * speed;
            float pulse = 1f + amplitude * Mathf.Sin(t);
            transform.localScale = _baseScale * pulse;
            yield return null;
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float xm1 = x - 1f;
        return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
    }
}
