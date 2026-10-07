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

    /// <summary>Sprite đang hiện trên item (AnswerMediaType.Image) — null nếu đang hiện dạng chữ/
    /// icon-compose, hoặc chưa load được sprite. Dùng cho hiệu ứng phụ cần LẤY LẠI đúng ảnh đang
    /// hiện (vd hiệu ứng bay lên rổ ở HaiQua/DemQua — bay bằng chính ảnh quả, không phải icon rời).</summary>
    public Sprite CurrentSprite => imageHolder != null ? imageHolder.sprite : null;

    Action<int, Team> _onClick;
    Team _team;
    bool _locked;

    // Sprite/type NỀN gốc của prefab — để SetBackgroundSprite(null)/ApplyShape("rectangle") trả nền về đúng như ban đầu.
    Sprite _baseSprite;
    Image.Type _baseType;
    bool _basePreserveAspect;
    bool _baseCaptured;
    bool _circleShape;

    void CaptureBase()
    {
        if (_baseCaptured || bgImage == null) return;
        _baseSprite = bgImage.sprite;
        _baseType = bgImage.type;
        _basePreserveAspect = bgImage.preserveAspect;
        _baseCaptured = true;
    }

    void Awake()
    {
        CaptureBase();
        // Chỉ bgImage nhận raycast; Label/Image content không được chặn click
        if (textLabel  != null) textLabel.raycastTarget  = false;
        if (imageHolder != null) imageHolder.raycastTarget = false;
    }

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

    /// <summary>Ghi đè cỡ chữ của textLabel — dùng cho GenericGameController (settings.answerFontSize),
    /// game khác trong Kit không gọi hàm này nên không ảnh hưởng gì (cỡ chữ gốc trong prefab giữ
    /// nguyên nếu không gọi). `size<=0` bị bỏ qua (coi là "không ghi đè").</summary>
    /// <summary>Ghi đè màu chữ của textLabel (settings.answerTextColor của GenericGame). Chỉ gọi khi có cấu hình.</summary>
    public void SetTextColor(Color color)
    {
        if (textLabel != null) textLabel.color = color;
    }

    public void SetFontSize(float size)
    {
        if (textLabel == null || size <= 0) return;
        // Prefab bật auto-size (max 22) → gán fontSize bị kẹp về ≤22, nhìn bé tí. Có ghi đè thì tắt auto-size.
        textLabel.enableAutoSizing = false;
        textLabel.fontSize = size;
    }

    /// <summary>Bật/tắt nền thẻ bài (card trắng mặc định) — dùng cho đáp án ảnh thật không có
    /// khung/nền (xem MakeButtonGroupBackgroundTransparent ở Editor cho game dựng sẵn lúc build;
    /// hàm này là bản tương đương gọi được lúc RUNTIME, cho GenericGameController quyết định theo
    /// từng answer có ảnh hay chỉ có chữ).</summary>
    public void SetBackgroundTransparent(bool transparent)
    {
        if (bgImage == null) return;
        // Cập nhật luôn colorNormal — SetState(Normal)/Setup() ở round sau phải tiếp tục trong
        // suốt, không chỉ đổi màu tức thời rồi bị ghi đè lại opaque ở round kế tiếp.
        colorNormal = transparent ? new Color(1f, 1f, 1f, 0f) : new Color(1f, 1f, 1f, 1f);
        bgImage.color = colorNormal;
    }

    /// <summary>Đổi hình dạng NỀN nút — dùng cho GenericGameController (xem
    /// GenericGame/GenericGameTypes.cs's slotShape). CHỈ "circle" tạo khác biệt thật (đổi
    /// bgImage.sprite sang hình tròn sinh runtime, xem RuntimeShapeSprites) — "square"/"rectangle"
    /// không cần đổi gì ở đây, đã là chuyện kích thước wPct/hPct quyết định từ phía layout.
    /// GIỚI HẠN ĐÃ BIẾT: ảnh người dùng tải lên (imageHolder, AnswerMediaType.Image) KHÔNG bị clip
    /// tròn theo — chỉ nền nút tròn, ảnh vẫn hiện dạng chữ nhật đè lên trên.</summary>
    public void ApplyShape(string shape)
    {
        if (bgImage == null) return;
        CaptureBase();
        // GenericGame v2 đổi hình dạng THEO ROUND (mỗi round có thể khác) — nên "rectangle" phải TRẢ nền về sprite gốc
        // (trước đây no-op vì hình dạng chỉ đặt 1 lần cho cả game).
        _circleShape = shape == "circle";
        if (_circleShape)
        {
            bgImage.sprite = RuntimeShapeSprites.GetCircle();
            bgImage.type = Image.Type.Simple;
        }
        else
        {
            bgImage.sprite = _baseSprite;
            bgImage.type = _baseType;
        }
        bgImage.preserveAspect = _basePreserveAspect;
    }

    /// <summary>Đặt ảnh NỀN riêng của slot (GenericGame v2: `slot.image`/ảnh nền chung nhóm/ảnh ô đáp án xoay vòng) — nền vẫn là
    /// bgImage nên các state Correct/Wrong/Locked vẫn nhân màu lên ảnh như trước. `preserveAspect=true` = ảnh giữ tỉ lệ (hợp ảnh
    /// vật thể), false = kéo kín nút (hợp ảnh khung). `sprite=null` = trả về nền mặc định (trắng/tròn theo ApplyShape).</summary>
    public void SetBackgroundSprite(Sprite sprite, bool preserveAspect)
    {
        if (bgImage == null) return;
        CaptureBase();
        if (sprite != null)
        {
            bgImage.sprite = sprite;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = preserveAspect;
        }
        else
        {
            bgImage.sprite = _circleShape ? RuntimeShapeSprites.GetCircle() : _baseSprite;
            bgImage.type = _circleShape ? Image.Type.Simple : _baseType;
            bgImage.preserveAspect = _basePreserveAspect;
        }
    }

    /// <summary>Nội dung hiện trên nền: có `iconValue` ("path:count", định dạng IconCompose của Kit) thì hiện N icon (CHỮ KHÔNG hiện),
    /// không thì hiện `text`. Gọi SAU Setup() (Setup tự ApplyMedia với giá trị tạm của QuestionData).</summary>
    public void SetContent(string text, string iconValue, int iconColumns = 0, int iconCount = 0)
    {
        if (!string.IsNullOrEmpty(iconValue))
        {
            ItemMediaHelper.ApplyMedia(iconValue, AnswerMediaType.IconCompose,
                textSlot, textLabel, imageSlot, imageHolder, iconSlot, iconContainer, iconPrefab);
            ArrangeIconGrid(iconColumns, iconCount);
        }
        else
            ItemMediaHelper.ApplyMedia(text ?? "", AnswerMediaType.Text,
                textSlot, textLabel, imageSlot, imageHolder, iconSlot, iconContainer, iconPrefab);
    }

    /// <summary>Thay ảnh NỀN thẻ (bgImage) bằng 1 ảnh khung riêng, kéo giãn kín nút — dùng cho
    /// GenericGameController (layout.slotFrames, nhiều ảnh gán xoay vòng theo thứ tự ô). Chỉ nên gọi cho
    /// đáp án dạng CHỮ; chữ (textLabel) vẫn vẽ ĐÈ lên trên như bình thường. Màu nền colorNormal giữ trắng
    /// → ảnh hiện đúng màu gốc, các state Correct/Wrong/Locked vẫn nhân màu lên ảnh như trước.</summary>
    public void SetFrameSprite(Sprite sprite)
    {
        if (bgImage == null || sprite == null) return;
        bgImage.sprite = sprite;
        bgImage.type = Image.Type.Simple;
        bgImage.preserveAspect = false;
    }

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

    /// <summary>Xếp N icon thành lưới `cols` cột (GridLayoutGroup có sẵn trong prefab), mỗi ô = (rộng/cột) × (cao/số hàng) — icon giữ tỉ lệ.</summary>
    void ArrangeIconGrid(int cols, int count)
    {
        if (cols <= 0 || count <= 0 || iconContainer == null) return;
        var grid = iconContainer.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        var rt = (RectTransform)iconContainer;
        var rect = rt.rect;
        if (rect.width <= 1f || rect.height <= 1f) rect = ((RectTransform)transform).rect;
        int c = Mathf.Min(cols, count);
        int rows = Mathf.CeilToInt(count / (float)c);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = c;
        grid.childAlignment = TextAnchor.MiddleCenter;
        float w = (rect.width - grid.padding.horizontal - grid.spacing.x * (c - 1)) / c;
        float h = (rect.height - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;
        if (w > 1f && h > 1f) grid.cellSize = new Vector2(w, h);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var rt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4]; // [0]=BL [1]=TL [2]=TR [3]=BR
        rt.GetWorldCorners(corners);

        // Bounds của bgImage
        string bgBounds = "null";
        if (bgImage != null)
        {
            var bgRt = bgImage.GetComponent<RectTransform>();
            if (bgRt != null)
            {
                Vector3[] bgC = new Vector3[4];
                bgRt.GetWorldCorners(bgC);
                bgBounds = $"x[{bgC[0].x:F0}~{bgC[2].x:F0}]";
            }
        }

        // Bounds của textLabel (cái bị hit)
        string labelBounds = "null";
        if (textLabel != null)
        {
            var lbRt = textLabel.GetComponent<RectTransform>();
            if (lbRt != null)
            {
                Vector3[] lbC = new Vector3[4];
                lbRt.GetWorldCorners(lbC);
                labelBounds = $"x[{lbC[0].x:F0}~{lbC[2].x:F0}]y[{lbC[0].y:F0}~{lbC[1].y:F0}]";
            }
        }

        string hitObj = eventData.pointerCurrentRaycast.gameObject != null
            ? eventData.pointerCurrentRaycast.gameObject.name : "null";

        string displayVal = textLabel != null && textSlot != null && textSlot.activeSelf ? textLabel.text : "?";
        Debug.Log($"[C5:RawClick] sib={transform.GetSiblingIndex()} idx={AnswerIndex} val=\"{displayVal}\" locked={_locked}" +
                  $" | click=({eventData.position.x:F1},{eventData.position.y:F1})" +
                  $" | myBounds=x[{corners[0].x:F0}~{corners[2].x:F0}]" +
                  $" | bgBounds={bgBounds}" +
                  $" | labelBounds={labelBounds}" +
                  $" | hitObj={hitObj}");
        if (_locked) return;
        _onClick?.Invoke(AnswerIndex, _team);
    }
}
