using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper coroutine to tween a Text score label from one integer value to another.
/// </summary>
public static class ScoreCountUp
{
    public static IEnumerator Animate(Text target, int from, int to, float duration = 1f)
    {
        if (target == null) yield break;
        if (duration <= 0f || from == to)
        {
            target.text = to.ToString();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // Ease out cubic for a nice deceleration
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            target.text = current.ToString();
            yield return null;
        }
        target.text = to.ToString();
    }
}
