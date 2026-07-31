using UnityEngine;

/// <summary>
/// Creates a floating/bobbing effect using a Sine wave on the local Y axis.
/// </summary>
public class FloatingEffect : MonoBehaviour
{
    [Header("Floating Settings")]
    public float amplitude = 10f; // Pixels to move up/down
    public float speed = 1.5f;     // Speed of the motion

    private RectTransform _rectTransform;
    private Vector2 _startAnchoredPos;
    private float _randomOffset;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _randomOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        ResetStartPos();
    }

    public void ResetStartPos()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        _startAnchoredPos = _rectTransform.anchoredPosition;
    }

    private void Update()
    {
        if (_rectTransform == null) return;
        float offset = Mathf.Sin(Time.time * speed + _randomOffset) * amplitude;
        _rectTransform.anchoredPosition = _startAnchoredPos + new Vector2(0, offset);
    }
}
