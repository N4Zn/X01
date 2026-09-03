using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class FloatingItem : MonoBehaviour, IPointerClickHandler
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
    [SerializeField] Image bgImage;          // Planet hoặc answer image — luôn là nền chính
    [SerializeField] Color colorNormal   = Color.white;
    [SerializeField] Color colorChosen   = new Color(0.15f, 0.15f, 0.15f, 1f);     // MultiSelect: đã chọn — tối hẳn, chữ trắng rõ
    [SerializeField] Color colorSelected = new Color(0.9f, 0.85f, 0.2f);            // OrderedSequence (unused in multi)
    [SerializeField] Color colorCorrect  = new Color(0.3f, 0.85f, 0.4f);
    [SerializeField] Color colorWrong    = new Color(0.9f, 0.3f, 0.3f);
    [SerializeField] Color colorLocked   = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    [Header("Animation")]
    [SerializeField] float breathScale = 1.08f;
    [SerializeField] float breathTime  = 0.9f;
    [SerializeField] float spinTime    = 8f;

    public int AnswerIndex { get; private set; }

    Action<int, Team> _onClick;
    Team  _team;
    bool  _locked;
    bool  _isImageAnswer;   // true → bgImage giữ answer image, không spin
    float _chosenTime = -1f; // Time.time khi SetChosen() gọi; -1 = chưa chọn
    Tween _breathTween;
    Tween _spinTween;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        _spinTween?.Kill();
        _breathTween?.Kill();
    }

    // ─── Setup ────────────────────────────────────────────────────────────────

    public void Setup(string value, AnswerMediaType mediaType, int index, Action<int, Team> onClick, Team team)
    {
        AnswerIndex          = index;
        _onClick             = onClick;
        _team                = team;   // gán tại setup — không detect lúc click
        _locked              = false;
        _isImageAnswer       = (mediaType == AnswerMediaType.Image);
        bgImage.color        = colorNormal;
        transform.localScale = Vector3.zero;

        // Ẩn hết các content slot
        textSlot.SetActive(false);
        imageSlot.SetActive(false);
        iconSlot.SetActive(false);

        if (_isImageAnswer)
        {
            // ── Dùng bgImage trực tiếp để chứa answer image (thay planet) ──
            // Kill spin cũ + reset rotation trước (phòng trường hợp prefab có rotation)
            _spinTween?.Kill();
            bgImage.transform.localRotation = Quaternion.identity;

            var sprite = AssetOverrideLoader.GetSprite(value);
            if (sprite != null)
            {
                bgImage.sprite         = sprite;
                bgImage.type           = Image.Type.Simple;
                bgImage.preserveAspect = true;
                bgImage.color          = colorNormal;
            }
            else
            {
                // Sprite chưa có → trong suốt bgImage + hiện text fallback
                // _isImageAnswer KHÔNG reset về false → SetPlanet() vẫn bị skip
                bgImage.sprite = null;
                bgImage.color  = Color.clear;
                Debug.LogWarning($"[FloatingItem] Sprite not found: {value}");
                textSlot.SetActive(true);
                textLabel.text = value;
            }
        }
        else
        {
            // ── Text / IconCompose: dùng content slot bình thường ──
            ItemMediaHelper.ApplyMedia(
                value, mediaType,
                textSlot, textLabel,
                imageSlot, imageHolder,
                iconSlot, iconContainer, iconPrefab);

            // Auto-size text để vừa với kích thước item (tránh tràn ra ngoài hình hành tinh)
            if (mediaType == AnswerMediaType.Text && textLabel != null)
            {
                textLabel.enableAutoSizing = true;
                textLabel.fontSizeMin      = 8f;
                textLabel.fontSizeMax      = 28f;
            }
        }
    }

    /// <summary>
    /// Gán planet sprite và bắt đầu spin.
    /// Bỏ qua nếu câu hỏi dùng answer image (bgImage đã dùng cho ảnh đó).
    /// </summary>
    public void SetPlanet(Sprite sprite)
    {
        if (_isImageAnswer) return;   // bgImage đã là answer image → không override
        if (bgImage == null || sprite == null) return;

        bgImage.sprite         = sprite;
        bgImage.type           = Image.Type.Simple;
        bgImage.preserveAspect = false;   // planet lấp đầy toàn bộ slot
        bgImage.color          = colorNormal;

        _spinTween?.Kill();
        _spinTween = bgImage.transform
            .DOLocalRotate(new Vector3(0, 0, -360), spinTime, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    public void PlaySpawnAnim(float delay = 0)
        => StartCoroutine(SpawnThenBreathe(delay));

    IEnumerator SpawnThenBreathe(float delay)
    {
        yield return StartCoroutine(ScaleTo(Vector3.one, 0.25f, delay));
        StartBreathe();
    }

    void StartBreathe()
    {
        _breathTween?.Kill();
        _breathTween = transform.DOScale(breathScale, breathTime)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void StopBreathe()
    {
        _breathTween?.Kill();
        _breathTween = null;
        transform.localScale = Vector3.one;
    }

    // ─── State ────────────────────────────────────────────────────────────────
    // SetState tint màu lên bgImage → hoạt động cho cả planet lẫn answer image

    /// <summary>MultiSelect: đã chọn — làm tối, dừng hiệu ứng, block click cho đến khi hết DeselectDelay.</summary>
    public void SetChosen()
    {
        bgImage.color = colorChosen;
        _chosenTime   = Time.time;
        StopBreathe();          // dừng nhịp thở khi đã chọn
        // _locked stays false → vẫn clickable sau delay (check trong OnPointerClick)
    }

    /// <summary>WrongFinal hiển thị tức thời: đỏ + shake nhưng KHÔNG khoá (dùng nội bộ nếu cần).</summary>
    public void SetWrongPartial()
    {
        bgImage.color = colorWrong;
        StartCoroutine(ShakeAnim());
    }

    /// <summary>Khoá click mà không đổi màu — dùng sau CorrectPartial của OrderedSequence.</summary>
    public void Lock() => _locked = true;

    public void SetState(ItemState state)
    {
        switch (state)
        {
            case ItemState.Normal:
                bgImage.color = colorNormal;
                _locked       = false;
                _chosenTime   = -1f;    // reset → cho phép chọn lại ngay
                StartBreathe();
                break;
            case ItemState.Selected:
                bgImage.color = colorSelected;
                break;
            case ItemState.Correct:
                StopBreathe();
                bgImage.color = colorCorrect;
                StartCoroutine(ScaleTo(Vector3.one * 1.2f, 0.15f));
                _locked = true;
                break;
            case ItemState.Wrong:
                bgImage.color = colorWrong;
                StartCoroutine(ShakeAnim());
                _locked = true;
                break;
            case ItemState.Revealed:
                bgImage.color = colorCorrect;
                _locked = true;
                break;
            case ItemState.Locked:
                StopBreathe();
                bgImage.color = colorLocked;
                _locked = true;
                break;
        }
    }

    // ─── Click ────────────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_locked) return;

        // Trong thời gian DeselectDelay sau khi SetChosen() → chặn hoàn toàn, không animation
        if (_chosenTime >= 0 &&
            Time.time - _chosenTime < TongHopConfig.Current.chooseDeselectDelay) return;

        _locked = true;
        StopBreathe();
        transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 0.5f)
            .OnComplete(() =>
            {
                _locked = false;
                _onClick?.Invoke(AnswerIndex, _team);
            });
    }

    // ─── Animations ───────────────────────────────────────────────────────────

    IEnumerator ScaleTo(Vector3 target, float dur, float delay = 0)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        Vector3 from = transform.localScale;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            transform.localScale = Vector3.Lerp(from, target, t);
            yield return null;
        }
        transform.localScale = target;
    }

    IEnumerator ShakeAnim()
    {
        Vector3 origin = transform.localPosition;
        for (int i = 0; i < 6; i++)
        {
            transform.localPosition = origin + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0, 0);
            yield return new WaitForSeconds(0.04f);
        }
        transform.localPosition = origin;
    }
}
