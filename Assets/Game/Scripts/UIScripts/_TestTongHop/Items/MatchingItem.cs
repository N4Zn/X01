using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MatchingItem : MonoBehaviour, IPointerClickHandler
{
    [Header("Media slots")]
    [SerializeField] GameObject      textSlot;
    [SerializeField] TextMeshProUGUI textLabel;
    [SerializeField] GameObject      imageSlot;
    [SerializeField] Image           imageHolder;
    [SerializeField] GameObject      iconSlot;
    [SerializeField] Transform       iconContainer;
    [SerializeField] GameObject      iconPrefab;

    [Header("Visual")]
    [SerializeField] Image bgImage;
    [SerializeField] Color colorNormal   = Color.white;
    [SerializeField] Color colorSelected = new Color(0.9f, 0.85f, 0.2f);
    [SerializeField] Color colorCorrect  = new Color(0.3f, 0.85f, 0.4f);
    [SerializeField] Color colorWrong    = new Color(0.9f, 0.3f, 0.3f);

    public int  AnswerIndex { get; private set; }
    public bool IsLeft      { get; private set; }

    Action<int, Team> _onClick;
    bool  _locked;
    float _lastClickTime = -1f;
    float _pairedTime    = -1f;   // Time.time sau khi ghép cặp; -1 = chưa ghép

    public void Setup(string value, AnswerMediaType mediaType, int index,
                      Action<int, Team> onClick, bool isLeft)
    {
        AnswerIndex    = index;
        IsLeft         = isLeft;
        _onClick       = onClick;
        _locked        = false;
        _lastClickTime = -1f;
        _pairedTime    = -1f;
        bgImage.color  = colorNormal;

        ItemMediaHelper.ApplyMedia(
            value, mediaType,
            textSlot, textLabel,
            imageSlot, imageHolder,
            iconSlot, iconContainer, iconPrefab);
    }

    public void SetState(ItemState state)
    {
        switch (state)
        {
            case ItemState.Normal:   bgImage.color = colorNormal;   _locked = false; _pairedTime = -1f; break;
            case ItemState.Selected: bgImage.color = colorSelected;                                     break;
            case ItemState.Correct:  bgImage.color = colorCorrect;  _locked = true;                     break;
            case ItemState.Wrong:    bgImage.color = colorWrong;    _locked = true;                     break;
            case ItemState.Locked:   bgImage.color = colorNormal;   _locked = true;                     break;
        }
    }

    /// <summary>
    /// Gọi sau khi ghép cặp thành công — giữ màu Normal nhưng block click
    /// cho đến khi hết matchingDeselectDelay (tránh chọn lại ngay sau khi nối).
    /// </summary>
    public void SetPaired()
    {
        bgImage.color = colorNormal;
        _locked       = false;
        _pairedTime   = Time.time;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_locked) return;
        float delay = TongHopConfig.Current.matchingDeselectDelay;
        // Debounce chung: chặn click lại trong vòng delay sau lần click trước
        if (Time.time - _lastClickTime < delay) return;
        // Bảo vệ thêm: block cả 2 item của một pair trong delay từ lúc nối xong
        if (_pairedTime >= 0 && Time.time - _pairedTime < delay) return;
        _lastClickTime = Time.time;
        Team team = eventData.position.x < Screen.width / 2f ? Team.Left : Team.Right;
        _onClick?.Invoke(AnswerIndex, team);
    }
}
