using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn script này vào GameObject chứa hiệu ứng phản hồi (ví dụ object "WrongMark" chứa dấu X đỏ,
/// hoặc "CorrectMark" chứa dấu check xanh).
///
/// Cách dùng:
/// 1. Kéo script vào object dấu X đỏ (và làm tương tự cho dấu check xanh nếu có).
/// 2. Gọi feedback.PlayWrong() khi bé trả lời sai, hoặc feedback.PlayCorrect() khi trả lời đúng.
/// 3. Có thể để trống các trường AudioClip / ParticleSystem nếu chưa có, script vẫn chạy bình thường.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AnswerFeedbackEffect : MonoBehaviour
{
    [Header("Chung")]
    [SerializeField] private CanvasGroup canvasGroup;   // để fade in/out, có thể để trống -> tự thêm
    [SerializeField] private Graphic targetGraphic;      // Image chứa dấu X hoặc check, dùng để đổi màu chớp

    [Header("Âm thanh (tùy chọn)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip correctSfx;
    [SerializeField] private AudioClip wrongSfx;

    [Header("Hiệu ứng hạt (tùy chọn)")]
    [SerializeField] private ParticleSystem correctParticles; // ví dụ pháo giấy / sao lấp lánh

    [Header("Thông số hiệu ứng SAI")]
    [SerializeField] private float wrongShakeDuration = 0.4f;
    [SerializeField] private float wrongShakeStrength = 25f; // độ lệch pixel
    [SerializeField] private int wrongShakeVibrato = 6;
    [SerializeField] private Color wrongFlashColor = new Color(1f, 0.3f, 0.3f);

    [Header("Thông số hiệu ứng ĐÚNG")]
    [SerializeField] private float correctPunchScale = 1.35f;
    [SerializeField] private float correctPunchDuration = 0.5f;
    [SerializeField] private float correctSpinAngle = 15f;

    private RectTransform rt;
    private Vector2 originalAnchoredPos;
    private Vector3 originalScale;
    private Color originalColor;
    private Coroutine runningRoutine;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        originalAnchoredPos = rt.anchoredPosition;
        originalScale = rt.localScale;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();

        if (targetGraphic != null)
            originalColor = targetGraphic.color;
    }

    /// <summary>Gọi khi bé trả lời SAI.</summary>
    public void PlayWrong()
    {
        if (runningRoutine != null) StopCoroutine(runningRoutine);
        gameObject.SetActive(true);
        runningRoutine = StartCoroutine(WrongRoutine());
    }

    /// <summary>Gọi khi bé trả lời ĐÚNG.</summary>
    public void PlayCorrect()
    {
        if (runningRoutine != null) StopCoroutine(runningRoutine);
        gameObject.SetActive(true);
        runningRoutine = StartCoroutine(CorrectRoutine());
    }

    private IEnumerator WrongRoutine()
    {
        if (audioSource != null && wrongSfx != null)
            audioSource.PlayOneShot(wrongSfx);

        rt.anchoredPosition = originalAnchoredPos;
        rt.localScale = originalScale;
        canvasGroup.alpha = 1f;

        // Bật to nhanh rồi ổn định (pop-in)
        yield return ScaleTo(originalScale * 1.2f, 0.08f);
        yield return ScaleTo(originalScale, 0.08f);

        // Rung lắc ngang kiểu "sai rồi!"
        float elapsed = 0f;
        while (elapsed < wrongShakeDuration)
        {
            float strength = wrongShakeStrength * (1f - elapsed / wrongShakeDuration); // giảm dần biên độ
            float offsetX = Random.Range(-1f, 1f) * strength;
            rt.anchoredPosition = originalAnchoredPos + new Vector2(offsetX, 0f);

            // Chớp màu đỏ đậm/nhạt xen kẽ
            if (targetGraphic != null)
            {
                float t = Mathf.PingPong(elapsed * wrongShakeVibrato, 1f);
                targetGraphic.color = Color.Lerp(originalColor, wrongFlashColor, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = originalAnchoredPos;
        if (targetGraphic != null) targetGraphic.color = originalColor;

        // Giữ hình một chút cho bé nhìn rõ, rồi mờ dần biến mất
        yield return new WaitForSeconds(0.4f);
        yield return FadeTo(0f, 0.3f);

        gameObject.SetActive(false);
        canvasGroup.alpha = 1f; // reset để lần sau hiện lại bình thường
    }

    private IEnumerator CorrectRoutine()
    {
        if (audioSource != null && correctSfx != null)
            audioSource.PlayOneShot(correctSfx);

        rt.anchoredPosition = originalAnchoredPos;
        rt.localScale = Vector3.zero;
        rt.localEulerAngles = Vector3.zero;
        canvasGroup.alpha = 1f;

        if (correctParticles != null)
            correctParticles.Play();

        // Nảy vào (pop) kèm xoay nhẹ, tạo cảm giác vui nhộn
        float t = 0f;
        float dur = correctPunchDuration;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            // easing overshoot (kiểu "punch"): vượt quá 1 rồi về lại
            float scaleFactor = EaseOutBack(p) ;
            rt.localScale = originalScale * Mathf.LerpUnclamped(0f, correctPunchScale, scaleFactor) ;

            float angle = Mathf.Sin(p * Mathf.PI) * correctSpinAngle;
            rt.localEulerAngles = new Vector3(0, 0, angle);

            yield return null;
        }

        // Về đúng scale gốc, dừng lại mượt
        yield return ScaleTo(originalScale, 0.15f);
        rt.localEulerAngles = Vector3.zero;

        yield return new WaitForSeconds(0.5f);
        yield return FadeTo(0f, 0.3f);

        gameObject.SetActive(false);
        canvasGroup.alpha = 1f;
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = rt.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        rt.localScale = target;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = canvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    // Easing tạo cảm giác "nảy quá đà rồi ổn định" giống DOTween's OutBack
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
