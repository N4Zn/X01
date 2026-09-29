using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1 làn (1 đội) của "Save The Astronaut" — TOÀN BỘ 12 chặng (xem SaveTheAstronautContent) + Trái
/// Đất tồn tại ĐỒNG THỜI dưới dạng 1 "băng chuyền" liên tục (kiểu LaneDash thật: xem LaneTrack —
/// vật thể trôi liên tục theo 1 mốc thời gian chung, dừng hẳn khi cần xử lý va chạm/kết quả, không
/// bao giờ snap/reset vị trí khi tiếp tục). Chặng xa thì nhỏ/mờ, càng gần càng to/rõ dần.
///
/// Mốc chung duy nhất: S = stepIndex + t/travelDuration (float liên tục). Độ sâu của chặng k =
/// (k+1) - S — chặng đang ở gần mặt trước nhất (depth≈0) là chặng ĐANG chơi, duy nhất tương tác
/// được; các chặng sau chỉ hiện thị, không bấm được, nhưng vẫn hiện ĐÚNG dữ liệu thật (ô nào đã từng
/// biết sai thì hiện sẵn cảnh báo).
///
/// Đúng → lộ hành tinh/mặt trăng đúng thứ tự thiên văn (chớp nhoáng, KHÔNG lưu dấu — chặng đó quay
/// lại trạng thái ẩn ở lần đi qua sau), tiến 1 chặng, S tiếp tục trôi liên tục từ đúng vị trí đó
/// (KHÔNG giật lùi). Đi hết 12 chặng → +1 lượt về đích, sinh pattern đúng/sai MỚI + xoá hết dấu sai
/// cũ (chỉ đổi pattern lúc này, S reset về 0).
///
/// Sai (bấm sai HOẶC không kịp bấm trong travelDuration giây) → lộ hố đen/sao viền đỏ VĨNH VIỄN tại
/// đúng chặng đó, "chết" → S reset về 0 (toàn bộ băng chuyền lùi lại từ đầu), nhưng dấu sai + pattern
/// đúng/sai GIỮ NGUYÊN.
/// </summary>
public class SaveTheAstronautLane : MonoBehaviour
{
    [Header("12 hàng thật (mỗi hàng 2 nút hình thang) — index 0..11 cố định theo chặng")]
    [SerializeField] Button[] leftButtons;
    [SerializeField] Button[] rightButtons;
    [SerializeField] RectTransform[] leftRects;
    [SerializeField] RectTransform[] rightRects;
    [SerializeField] TrapezoidImage[] leftTileImages;
    [SerializeField] TrapezoidImage[] rightTileImages;
    [SerializeField] Image[] leftRevealImages;
    [SerializeField] Image[] rightRevealImages;
    [SerializeField] Outline[] leftOutlines;
    [SerializeField] Outline[] rightOutlines;

    [Header("Vị trí trôi — spawnY = điểm tụ phối cảnh (depth→∞ hội tụ về đây), captureY = vị trí mặt trước (depth=0)")]
    [Tooltip("captureY phải để đủ chỗ cho nửa chiều cao hàng mặt trước (TileHeight/2 trong SceneBuilder) không tràn khỏi đáy làn (local Y -264).")]
    [SerializeField] float spawnY = 240f;
    [SerializeField] float captureY = -140f;

    [Tooltip("Ẩn hẳn khi depth vượt quá mốc này (kích thước lúc đó đã quá nhỏ để đáng hiển thị).")]
    [SerializeField] float visibleDepthSlots = 8f;

    [Header("Phi hành gia — nghiêng sang bên vừa chọn")]
    [SerializeField] RectTransform astronautRect;
    [SerializeField] UnityEngine.UI.Image astronautImage; // assign trực tiếp trong Inspector
    [SerializeField] float astronautLeanX = 90f;


    [Header("Trái Đất (đích) — hàng ảo thứ 13, cùng cơ chế trôi/to dần như 12 hàng trên")]
    [SerializeField] RectTransform earthRect;
    [SerializeField] TextMeshProUGUI stepNameText;

    static readonly Color HiddenColor = new(0.24f, 0.27f, 0.45f, 1f);
    static readonly Color CorrectColor = new(0.25f, 0.85f, 0.45f, 1f);
    static readonly Color WrongColor = new(0.85f, 0.20f, 0.20f, 1f);
    static readonly Color OutlineNormalColor = new(1f, 1f, 1f, 0.6f);
    static readonly Color OutlineDangerColor = new(0.95f, 0.15f, 0.15f, 1f);

    SaveTheAstronautConfigData _cfg;
    bool[] _correctIsLeft;
    bool[] _revealedWrong;
    CanvasGroup[] _leftGroups;
    CanvasGroup[] _rightGroups;
    RectTransform _startBannerContainer;
    CanvasGroup _startBannerCg;
    bool _startBannerDone;
    int _lapCount;
    int _stepIndex;
    float _t;          // giây đã trôi trong chặng hiện tại (0..travelDuration)
    bool _running;
    bool _paused;       // true trong lúc reveal/đếm ngược — toàn bộ băng chuyền đứng hình
    bool _resolved;

    public event Action OnLapCompleted;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Init(SaveTheAstronautConfigData cfg)
    {
        _cfg = cfg;
        int n = leftButtons != null ? leftButtons.Length : 0;
        for (int k = 0; k < n; k++)
        {
            int cap = k;
            if (leftTileImages[k] != null) leftTileImages[k].topWidthRatio = cfg.trapezoidTopWidthRatio;
            if (rightTileImages[k] != null) rightTileImages[k].topWidthRatio = cfg.trapezoidTopWidthRatio;
            leftButtons[k].onClick.AddListener(() => OnTap(cap, true));
            rightButtons[k].onClick.AddListener(() => OnTap(cap, false));
        }

        // CanvasGroup cho fade-out khi hàng trôi qua mặt trước
        _leftGroups  = new CanvasGroup[n];
        _rightGroups = new CanvasGroup[n];
        for (int k = 0; k < n; k++)
        {
            _leftGroups[k]  = GetOrAddCanvasGroup(leftRects[k]);
            _rightGroups[k] = GetOrAddCanvasGroup(rightRects[k]);
        }

        // Earth — tải texture, xóa tint xanh, bọc trong mask tròn
        if (earthRect != null)
        {
            var raw = earthRect.GetComponent<UnityEngine.UI.RawImage>();
            if (raw != null)
            {
                raw.color = Color.white;
                if (raw.texture == null)
                {
                    var tex = Resources.Load<Texture2D>("SolarSystem/Textures/2k_earth_daymap");
                    if (tex != null) { tex.wrapMode = TextureWrapMode.Repeat; raw.texture = tex; }
                }
            }
            // Tìm sibling index nhỏ nhất của tile rows để đặt Earth trước chúng
            int minTileIdx = int.MaxValue;
            if (leftRects  != null) foreach (var r in leftRects)  if (r) minTileIdx = Mathf.Min(minTileIdx, r.GetSiblingIndex());
            if (rightRects != null) foreach (var r in rightRects) if (r) minTileIdx = Mathf.Min(minTileIdx, r.GetSiblingIndex());
            if (minTileIdx == int.MaxValue) minTileIdx = 0;
            earthRect = WrapEarthInCircleMask(earthRect, minTileIdx);
        }

        // Astronaut back image
        if (astronautImage != null)
        {
            var spr = Resources.Load<Sprite>("Space/AstronautBack");
            if (spr != null) astronautImage.sprite = spr;
        }

        BuildStartBanner();
        NewPattern();
    }

    void NewPattern()
    {
        _correctIsLeft = new bool[_cfg.stepCount];
        for (int i = 0; i < _correctIsLeft.Length; i++)
            _correctIsLeft[i] = UnityEngine.Random.value < 0.5f;
        _lapCount = 0;
        ResetForLap();
    }

    // Reset đầu mỗi lap (giữ nguyên _correctIsLeft và _lapCount)
    void ResetForLap()
    {
        _revealedWrong = new bool[_cfg.stepCount];
        _stepIndex = 0;
        _t = -_cfg.travelDuration;
        _resolved = false;
        _startBannerDone = false;
        SetupSlotDefault(0);
    }

    public void SetRunning(bool running) => _running = running;

    // ── Băng chuyền liên tục ─────────────────────────────────────────────────

    void Update()
    {
        // Breathing — luôn chạy để astronaut sống động ngay cả khi chờ
        if (astronautRect != null)
        {
            float breathScale = 1f + Mathf.Sin(Time.time * Mathf.PI * 0.8f) * 0.03f;
            var s = astronautRect.localScale;
            astronautRect.localScale = new Vector3(s.x, breathScale, s.z);
        }

        if (!_running || _paused) return;

        _t += Time.deltaTime;
        RefreshBoard();

        if (_t >= _cfg.travelDuration && !_resolved && _stepIndex < _cfg.stepCount)
        {
            _resolved = true;
            StartCoroutine(ResolveCurrentStep(null)); // không bấm kịp — buộc phải chọn
        }
    }

    void RefreshBoard()
    {
        float s = _stepIndex + Mathf.Min(_t / _cfg.travelDuration, 1f);
        int n = leftRects.Length;

        for (int k = 0; k < n; k++)
        {
            // Ẩn hàng ngoài phạm vi stepCount (scene có thể có nhiều hàng hơn config)
            if (k >= _cfg.stepCount) { SetSlotActive(k, false); continue; }

            float depth = (k + 1) - s;
            // Cho depth âm ĐI QUA (PassPastSlots) trước khi ẩn hẳn — hàng vừa xong TRÔI TIẾP qua mặt
            // trước rồi mới biến mất, không pop/biến mất đột ngột ngay khi vừa đúng.
            bool visible = depth >= -PassPastSlots && depth <= visibleDepthSlots + 1f;
            SetSlotActive(k, visible);
            if (!visible) continue;

            ApplyTravel(leftRects[k], depth);
            ApplyTravel(rightRects[k], depth);

            // Fade-out tuyến tính khi hàng đã qua mặt trước (depth < 0)
            float alpha = (k < _stepIndex && depth < 0f)
                ? Mathf.Clamp01(1f + depth / PassPastSlots)
                : 1f;
            SetSlotAlpha(k, alpha);

            bool isFront = k == _stepIndex && _t >= 0f; // không interactive khi đang phase Start banner
            leftButtons[k].interactable = isFront;
            rightButtons[k].interactable = isFront;

            // k <= _stepIndex: hàng đang chơi (tự set màu qua SetupSlotDefault lúc bắt đầu chặng) HOẶC
            // hàng VỪA xong đang trôi qua (giữ nguyên màu reveal vừa set trong ResolveCurrentStep) —
            // cả 2 trường hợp đều KHÔNG được ghi đè mỗi frame ở đây.
            if (k <= _stepIndex) continue;
            RefreshQueuedSlotVisual(k);
        }

        float earthDepth = (_cfg.stepCount + 1) - s;
        bool earthVisible = earthDepth >= -PassPastSlots && earthDepth <= visibleDepthSlots + 1f;
        if (earthRect != null)
        {
            earthRect.gameObject.SetActive(earthVisible);
            if (earthVisible) ApplyTravel(earthRect, earthDepth);
        }

        // Start banner — ở depth=1 khi game mới bắt đầu (s=-1 do _t=-T), cuộn xuống player
        // trước step 0, fade và biến mất. Chỉ hiện lúc NewPattern, không hiện lại khi chết.
        if (_startBannerContainer != null)
        {
            if (!_startBannerDone)
            {
                float sd = -s; // sd=1 khi s=-1 (t=-T), sd=0 khi s=0, sd âm khi đã qua player
                if (sd < -PassPastSlots)
                {
                    _startBannerDone = true;
                    _startBannerContainer.gameObject.SetActive(false);
                }
                else
                {
                    _startBannerContainer.gameObject.SetActive(true);
                    ApplyTravel(_startBannerContainer, sd);
                    if (_startBannerCg != null)
                        _startBannerCg.alpha = sd < 0f ? Mathf.Clamp01(1f + sd / PassPastSlots) : 1f;
                }
            }
            else
            {
                _startBannerContainer.gameObject.SetActive(false);
            }
        }
    }

    // Số "khoảng chặng" cho hàng vừa xong trôi tiếp qua mặt trước trước khi ẩn hẳn.
    // 0.7 ≈ hàng trôi xuống qua đáy canvas rồi mới biến mất, đồng thời fade alpha về 0.
    const float PassPastSlots = 0.7f;

    void SetSlotActive(int k, bool active)
    {
        if (leftRects[k].gameObject.activeSelf != active) leftRects[k].gameObject.SetActive(active);
        if (rightRects[k].gameObject.activeSelf != active) rightRects[k].gameObject.SetActive(active);
    }

    // Chặng CHƯA tới lượt (phía sau hàng đang chơi) — chỉ hiện đúng dữ liệu thật đã biết (dấu sai cũ
    // nếu có), không cần set lại mỗi frame nhưng vô hại vì rẻ — đơn giản hoá code hơn là cache trạng thái.
    void RefreshQueuedSlotVisual(int k)
    {
        SetupSlotDefault(k);
    }

    void SetupSlotDefault(int k)
    {
        bool leftWrongSide = !_correctIsLeft[k];
        bool rightWrongSide = _correctIsLeft[k];

        SetTileDefault(leftTileImages[k], leftRevealImages[k], leftOutlines[k], leftWrongSide && _revealedWrong[k]);
        SetTileDefault(rightTileImages[k], rightRevealImages[k], rightOutlines[k], rightWrongSide && _revealedWrong[k]);
    }

    void SetTileDefault(TrapezoidImage tileImg, Image revealImg, Outline outline, bool showDangerUpfront)
    {
        if (showDangerUpfront) RevealDanger(revealImg, tileImg, outline);
        else
        {
            tileImg.color = HiddenColor;
            if (outline != null) { outline.enabled = true; outline.effectColor = OutlineNormalColor; outline.effectDistance = new Vector2(2f, -2f); }
            if (revealImg != null) revealImg.enabled = false;
        }
    }

    void OnTap(int k, bool tappedLeft)
    {
        if (k != _stepIndex || _resolved) return;
        _resolved = true;
        StartCoroutine(ResolveCurrentStep(tappedLeft));
    }

    // MẤU CHỐT để các hàng ghép liền mạch thành 1 khối duy nhất, KHÔNG hở/KHÔNG đè (xem ảnh minh hoạ
    // user gửi) — phối cảnh 1 điểm tụ THẬT: kích thước (CẢ 2 trục — xa thì nhỏ đều, không chỉ hẹp bề
    // ngang) VÀ vị trí Y đều co theo ĐÚNG CÙNG 1 tỉ lệ cấp số nhân (ratio^depth) thay vì vị trí tăng
    // ĐỀU (tuyến tính, bug cũ) trong khi kích thước co theo tốc độ khác — 2 tốc độ lệch nhau là lý do
    // trước đây các hàng tách rời/chồng lên nhau. Khi cùng chung 1 tỉ lệ, khoảng cách giữa 2 hàng liền
    // kề LUÔN co đúng bằng đúng tỉ lệ kích thước của chúng → mép khớp tuyệt đối ở MỌI độ sâu, hội tụ
    // về đúng 1 điểm (spawnY) khi depth→∞, đúng 1 điểm tụ phối cảnh thật.
    // depth âm (đang trôi qua sau khi đúng, xem PassPastSlots) → ratio^depth > 1 tự nhiên → to hơn cả
    // captureScale, tiếp tục tiến gần thêm chút rồi mới ẩn, không dừng khựng lại.
    void ApplyTravel(RectTransform rect, float depth)
    {
        float scale = Mathf.Pow(_cfg.trapezoidTopWidthRatio, depth);
        float y = spawnY + (captureY - spawnY) * scale;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        rect.localScale = Vector3.one * scale;
    }

    // ── Reveal / kết quả ─────────────────────────────────────────────────────

    IEnumerator ResolveCurrentStep(bool? tappedLeft)
    {
        int step = _stepIndex;

        if (leftButtons[step] != null) leftButtons[step].interactable = false;
        if (rightButtons[step] != null) rightButtons[step].interactable = false;
        if (tappedLeft.HasValue) StartCoroutine(LeanAstronaut(tappedLeft.Value));

        bool correctIsLeft = _correctIsLeft[step];
        bool correct = tappedLeft.HasValue && tappedLeft.Value == correctIsLeft;

        if (correct)
        {
            // Không pause — băng chuyền cuộn liên tục, hàng vừa đúng trôi tiếp rồi fade
            var info = SaveTheAstronautContent.Steps[step];
            bool isLeft = tappedLeft!.Value;
            RevealPlanet(isLeft ? leftRevealImages[step] : rightRevealImages[step], isLeft ? leftTileImages[step] : rightTileImages[step], info);
            if (stepNameText != null) stepNameText.text = info.displayName;
            MusicManager.Instance?.PlayCorrectSfx();
            PlayNarration(info.spriteKey);

            _stepIndex++;
            _t -= _cfg.travelDuration; // giữ vị trí liên tục — không giật
            _resolved = false;
            if (_stepIndex >= _cfg.stepCount)
                yield return StartCoroutine(CompleteLap());
            else
                SetupSlotDefault(_stepIndex);
        }
        else
        {
            _paused = true; // chỉ freeze khi sai — hiện lỗi + đếm ngược retry
            bool wrongIsLeft = !correctIsLeft;
            RevealDanger(wrongIsLeft ? leftRevealImages[step] : rightRevealImages[step], wrongIsLeft ? leftTileImages[step] : rightTileImages[step], wrongIsLeft ? leftOutlines[step] : rightOutlines[step]);
            _revealedWrong[step] = true;
            MusicManager.Instance?.PlayWrongSfx();

            yield return new WaitForSeconds(_cfg.wrongRevealSeconds);
            if (stepNameText != null) stepNameText.text = "";
            yield return new WaitForSeconds(_cfg.retryDelaySeconds);

            _stepIndex = 0;
            _t = 0f;
            _startBannerDone = true;
            _resolved = false;
            SetupSlotDefault(0);
            _paused = false;
        }
    }

    IEnumerator CompleteLap()
    {
        _paused = true; // freeze trong lúc celebrate — tránh timeout fire khi stepIndex out of bounds
        _lapCount++;
        OnLapCompleted?.Invoke();
        if (stepNameText != null) stepNameText.text = "Trái Đất";

        yield return new WaitForSeconds(_cfg.earthRevealSeconds);
        yield return StartCoroutine(CountdownMessage("Start in"));

        if (_lapCount >= _cfg.lapsPerPattern)
            NewPattern(); // đủ 5 lap → random lại thứ tự
        else
            ResetForLap(); // giữ nguyên pattern, chỉ reset vị trí + dấu sai
        _paused = false;
    }

    IEnumerator CountdownMessage(string label)
    {
        for (int i = 3; i >= 1; i--)
        {
            if (stepNameText != null) stepNameText.text = $"{label} {i}";
            yield return new WaitForSeconds(1f);
        }
        if (stepNameText != null) stepNameText.text = "";
    }

    IEnumerator LeanAstronaut(bool left)
    {
        if (astronautRect == null) yield break;
        float target = left ? -astronautLeanX : astronautLeanX;
        yield return StartCoroutine(MoveAstronautX(target, 0.25f));
    }

    IEnumerator MoveAstronautX(float targetX, float duration)
    {
        float startX = astronautRect.anchoredPosition.x;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, Mathf.Clamp01(t / duration));
            astronautRect.anchoredPosition = new Vector2(x, astronautRect.anchoredPosition.y);
            yield return null;
        }
        astronautRect.anchoredPosition = new Vector2(targetX, astronautRect.anchoredPosition.y);
    }

    void RevealPlanet(Image revealImg, TrapezoidImage tileImg, SaveTheAstronautContent.StepInfo info)
    {
        tileImg.color = CorrectColor;
        if (revealImg == null) return;
        var sprite = Resources.Load<Sprite>("SaveTheAstronaut/" + info.spriteKey);
        revealImg.sprite = sprite;
        revealImg.enabled = sprite != null;
    }

    void RevealDanger(Image revealImg, TrapezoidImage tileImg, Outline outline)
    {
        tileImg.color = WrongColor;
        if (outline != null) { outline.enabled = true; outline.effectColor = OutlineDangerColor; outline.effectDistance = new Vector2(4f, -4f); }
        if (revealImg == null) return;
        var sprite = Resources.Load<Sprite>("SaveTheAstronaut/BlackHole");
        revealImg.sprite = sprite;
        revealImg.enabled = sprite != null;
    }

    void PlayNarration(string spriteKey)
    {
        var clip = Resources.Load<AudioClip>("SaveTheAstronaut/" + spriteKey);
        if (clip != null) MusicManager.Instance?.PlaySfx(clip);
    }

    void SetSlotAlpha(int k, float alpha)
    {
        if (_leftGroups  != null && k < _leftGroups.Length  && _leftGroups[k]  != null) _leftGroups[k].alpha  = alpha;
        if (_rightGroups != null && k < _rightGroups.Length && _rightGroups[k] != null) _rightGroups[k].alpha = alpha;
    }

    // ── Earth circle mask ─────────────────────────────────────────────────────

    // Bọc RawImage EarthPlaceholder trong 1 parent có Mask hình tròn (Knob sprite).
    // Trả về RectTransform của parent mới để earthRect trỏ vào — ApplyTravel sẽ
    // scale/position đúng, EarthGlobeSpin vẫn trỏ thẳng vào RawImage nên không đổi.
    RectTransform WrapEarthInCircleMask(RectTransform source, int insertBefore = 0)
    {
        var maskGO = new GameObject("EarthCircle");
        var maskRT = maskGO.AddComponent<RectTransform>();
        maskRT.SetParent(source.parent, false);
        maskRT.SetSiblingIndex(insertBefore); // render trước tiles → ở phía sau về mặt visual
        maskRT.anchorMin        = source.anchorMin;
        maskRT.anchorMax        = source.anchorMax;
        maskRT.pivot            = source.pivot;
        maskRT.anchoredPosition = source.anchoredPosition;
        maskRT.sizeDelta        = source.sizeDelta;
        maskRT.localRotation    = source.localRotation;
        maskRT.localScale       = source.localScale;
        maskGO.SetActive(false);

        var img = maskGO.AddComponent<UnityEngine.UI.Image>();
        img.sprite = CreateCircleSprite(128);
        img.color  = Color.white;

        var mask = maskGO.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false;

        // Reparent RawImage vào mask, fill full; phải bật lại vì scene lưu m_IsActive: 0
        source.SetParent(maskRT, false);
        source.anchorMin  = Vector2.zero;
        source.anchorMax  = Vector2.one;
        source.offsetMin  = Vector2.zero;
        source.offsetMax  = Vector2.zero;
        source.localScale = Vector3.one;
        source.gameObject.SetActive(true);

        return maskRT;
    }

    static Sprite CreateCircleSprite(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float cx = res * 0.5f, r = res * 0.5f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dx = x - cx, dy = y - cx;
            tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f);
    }

    // ── Start Banner (build tự động, không cần setup trong Inspector) ─────────

    void BuildStartBanner()
    {
        if (leftRects == null || leftRects.Length == 0 || rightRects == null || rightRects.Length == 0) return;

        var refL = leftRects[0];
        var refR = rightRects[0];
        var parent = refL.parent;

        // Container — dùng để ApplyTravel + CanvasGroup alpha toàn banner
        var cGO = new GameObject("StartBanner");
        _startBannerContainer = cGO.AddComponent<RectTransform>();
        _startBannerContainer.SetParent(parent, false);
        _startBannerContainer.anchorMin = _startBannerContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _startBannerContainer.pivot = new Vector2(0.5f, 0.5f);
        _startBannerContainer.sizeDelta = new Vector2(refL.sizeDelta.x + refR.sizeDelta.x, refL.sizeDelta.y);
        _startBannerContainer.anchoredPosition = new Vector2(0f, refL.anchoredPosition.y);
        _startBannerCg = cGO.AddComponent<CanvasGroup>();
        _startBannerCg.interactable = false;
        _startBannerCg.blocksRaycasts = false;

        // "Start" text — trải dài toàn chiều ngang container, giữa đứng
        var tGO = new GameObject("StartLabel");
        tGO.transform.SetParent(cGO.transform, false);
        var tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        var tmp = tGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "Start";
        tmp.fontSize = 52;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Đặt vị trí ban đầu: depth=1 (s=-1 do _t=-T) — nằm trong queue ngay trước step 0
        ApplyTravel(_startBannerContainer, 1f);
    }

    RectTransform CreateBannerHalf(Transform parent, Vector2 size, bool leftEdgeVertical)
    {
        var go = new GameObject(leftEdgeVertical ? "BL" : "BR");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        // CanvasRenderer phải có trước MaskableGraphic (TrapezoidImage)
        go.AddComponent<CanvasRenderer>();
        var trap = go.AddComponent<TrapezoidImage>();
        trap.topWidthRatio = _cfg.trapezoidTopWidthRatio;
        trap.verticalEdgeOnLeft = leftEdgeVertical;
        trap.color = new Color(0.18f, 0.62f, 0.82f, 1f);
        return rt;
    }

    static CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
    {
        if (rt == null) return null;
        return rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
    }
}
