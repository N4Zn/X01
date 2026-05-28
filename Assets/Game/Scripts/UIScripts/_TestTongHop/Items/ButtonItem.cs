using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ButtonItem : MonoBehaviour, IPointerClickHandler
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
    [SerializeField] Color colorChosen   = new Color(0.6f, 0.6f, 0.6f);             // MultiSelect: đã chọn, chưa hé lộ
    [SerializeField] Color colorSelected = new Color(0.9f, 0.85f, 0.2f);            // OrderedSequence (unused in multi)
    [SerializeField] Color colorCorrect  = new Color(0.3f, 0.85f, 0.4f);
    [SerializeField] Color colorWrong    = new Color(0.9f, 0.3f, 0.3f);
    [SerializeField] Color colorLocked   = new Color(0.7f, 0.7f, 0.7f);

    public int AnswerIndex { get; private set; }
    Action<int, Team> _onClick;
    Team _team;
    bool _locked;

    public void Setup(string value, AnswerMediaType mediaType, int index, Action<int, Team> onClick, Team team)
    {
        AnswerIndex   = index;
        _onClick      = onClick;
        _team         = team;   // gán tại setup — không detect lúc click
        _locked       = false;
        bgImage.color = colorNormal;

        ItemMediaHelper.ApplyMedia(
            value, mediaType,
            textSlot, textLabel,
            imageSlot, imageHolder,
            iconSlot, iconContainer, iconPrefab);
    }

    /// <summary>MultiSelect: đã chọn — làm tối (xám), không hé lộ đúng/sai, vẫn clickable để deselect.</summary>
    public void SetChosen()
    {
        bgImage.color = colorChosen;
        // _locked stays false → vẫn clickable sau delay
    }

    /// <summary>Khoá click mà không đổi màu — dùng sau CorrectPartial của OrderedSequence.</summary>
    public void Lock() => _locked = true;

    public void SetState(ItemState state)
    {
        switch (state)
        {
            case ItemState.Normal:   bgImage.color = colorNormal;   _locked = false; break;
            case ItemState.Selected: bgImage.color = colorSelected;                  break;
            case ItemState.Correct:  bgImage.color = colorCorrect;  _locked = true;  break;
            case ItemState.Wrong:    bgImage.color = colorWrong;    _locked = true;  break;
            case ItemState.Revealed: bgImage.color = colorCorrect;  _locked = true;  break;
            case ItemState.Locked:   bgImage.color = colorLocked;   _locked = true;  break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_locked) return;
        _onClick?.Invoke(AnswerIndex, _team);
    }
}
