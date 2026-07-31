using UnityEngine;
using System.Collections;

/// <summary>
/// Simple script to shrink a UI element to zero and then disable it.
/// </summary>
public class ShrinkAndDisappearEffect : MonoBehaviour
{
    public void Play(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(ShrinkRoutine(duration));
    }

    private IEnumerator ShrinkRoutine(float duration)
    {
        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Smoothly lerp scale to zero
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);

        // Reset scale back to original so it's ready if reactivated (e.g. game restart)
        transform.localScale = initialScale;
    }
}
