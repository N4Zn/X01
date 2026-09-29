using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Detects long-press on a UI element. Attach to any GameObject with a RectTransform.
/// Fires OnLongPress after holding for the threshold duration.
/// Also fires OnClick for short taps.
/// </summary>
public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float LongPressThreshold = 0.5f;

    public event Action OnClick = delegate { };
    public event Action OnLongPress = delegate { };

    private bool _isPressed;
    private float _pressStartTime;
    private bool _longPressTriggered;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        _pressStartTime = Time.unscaledTime;
        _longPressTriggered = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isPressed && !_longPressTriggered)
        {
            OnClick();
        }
        _isPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPressed = false;
    }

    void Update()
    {
        if (_isPressed && !_longPressTriggered)
        {
            if (Time.unscaledTime - _pressStartTime >= LongPressThreshold)
            {
                _longPressTriggered = true;
                OnLongPress();
            }
        }
    }
}
