using System;
using UnityEngine;

/// <summary>
/// Chọn display phù hợp dựa trên 3 weight, tự lấy câu hỏi từ QuestionPool.
///
/// Weight ý nghĩa:
///   weightFloating  → % câu hỏi Choose hiện bằng FloatingDisplay
///   weightButton    → % câu hỏi Choose hiện bằng ButtonDisplay
///   weightMatching  → % câu hỏi Matching hiện bằng MatchingDisplay
///
/// Ví dụ: 0 / 0 / 100  → chỉ Matching
///        40 / 60 / 0  → chỉ Choose (40% Floating, 60% Button)
///        34 / 33 / 33 → hỗn hợp
///
/// Nếu pool không có câu hỏi loại được chọn, tự fallback sang loại còn lại.
/// </summary>
public class AnswerDisplayManager : MonoBehaviour
{
    [Header("Display references")]
    [SerializeField] FloatingDisplay    floatingDisplay;
    [SerializeField] ButtonDisplay      buttonDisplay;
    [SerializeField] MatchingDisplay    matchingDisplay;
    [SerializeField] SolarSystemDisplay  solarSystemDisplay;   // optional — auto-created if null
    [SerializeField] SolarSystemDisplay  solarSystemEnDisplay; // English version — auto-created if null
    [SerializeField] PlanetOrderDisplay  planetOrderDisplay;   // single-player column game

    [Header("Solar System — hit area")]
    [Tooltip("Mở rộng vùng click của mỗi hành tinh ra ngoài mỗi cạnh (px). Tăng để click dễ hơn.")]
    [SerializeField] float solarHitPadding = 15f;

    [Header("Question pool")]
    [SerializeField] QuestionPool questionPool;

    // Weights đọc từ TongHopConfig.Current (gameconfig.json)
    // weightFloating / weightButton / weightMatching

    IAnswerDisplay _active;
    IAnswerDisplay _independentDisplay;

    /// <summary>Câu hỏi đang được hiển thị — Controller đọc để log.</summary>
    public QuestionData CurrentQuestion { get; private set; }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Chọn display + câu hỏi dựa trên weight, bắt đầu hiển thị.
    /// onPlayerFailed: callback ngay khi 1 player hết lượt (sai) — trước khi cả 2 xong.
    /// onPartialCorrect: MultiSelect — mỗi lần click đúng 1 đáp án (+1 điểm, SFX, không icon).
    /// </summary>
    public void Show(Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed = null,
                     Action<Team> onPartialCorrect = null)
    {
        HideAll();

        // 1. Chọn slot display dựa trên weight
        int slot = PickSlot();           // 0 = Floating, 1 = Button, 2 = Matching

        // 2. Lấy câu hỏi đúng loại; nếu pool trống thì fallback
        QuestionData q = GetQuestionForSlot(slot);
        if (q == null)
        {
            Debug.LogWarning("[AnswerDisplayManager] Pool rỗng hoàn toàn!");
            return;
        }

        // 3. Chọn display:
        //    - Matching → luôn dùng MatchingDisplay
        //    - Choose   → ưu tiên displayMode ghi trong câu hỏi (Button/Floating)
        //                 nếu Auto → dùng slot weight đã chọn (0=Floating, 1=Button)
        if (q.questionType == QuestionType.Matching)
        {
            _active = matchingDisplay;
        }
        else
        {
            _active = q.displayMode switch
            {
                ChooseDisplayMode.Button        => buttonDisplay,
                ChooseDisplayMode.Floating      => floatingDisplay,
                ChooseDisplayMode.SolarSystem   => GetOrCreateSolarSystem(),
                ChooseDisplayMode.SolarSystemEn => GetOrCreateSolarSystemEn(),
                ChooseDisplayMode.PlanetOrder   => GetOrCreatePlanetOrder(),
                _                               => slot == 0 ? (IAnswerDisplay)floatingDisplay : buttonDisplay
            };
        }

        CurrentQuestion = q;

        (_active as MonoBehaviour)?.gameObject.SetActive(true);
        _active.Setup(q, onResult, onPlayerFailed);
        (_active as FloatingDisplay)?.SetPartialCorrectCallback(onPartialCorrect);
        (_active as ButtonDisplay)?.SetPartialCorrectCallback(onPartialCorrect);
    }

    /// <summary>
    /// Independent mode: setup button group của MỘT player với câu hỏi riêng.
    /// Player kia không bị ảnh hưởng.
    /// </summary>
    public void SetupPlayerIndependent(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        if (q != null && q.displayMode == ChooseDisplayMode.Floating)
        {
            floatingDisplay?.SetupPlayerIndependent(team, q, onDone);
            _independentDisplay = floatingDisplay;
        }
        else
        {
            buttonDisplay?.SetupPlayerIndependent(team, q, onDone);
            _independentDisplay = buttonDisplay;
        }
    }

    /// <summary>
    /// Independent play: lấy 1 câu hỏi tiếp theo từ pool mà không chạy display.
    /// Ưu tiên Choose; fallback sang Matching nếu không có.
    /// </summary>
    public QuestionData GetNextQuestion()
    {
        if (questionPool.HasChoose)   return questionPool.GetNextChoose();
        if (questionPool.HasMatching) return questionPool.GetNextMatching();
        return null;
    }

    public void Cleanup()
    {
        _active?.Cleanup();
        if (_independentDisplay != null && _independentDisplay != _active)
            _independentDisplay.Cleanup();
        // Don't call HideAll() here — each display's Cleanup() handles its own visibility.
        // PlanetOrderDisplay intentionally stays active to cover the split-screen during
        // the inter-question transition. Show() calls HideAll() before the next question.
        _active             = null;
        _independentDisplay = null;
        CurrentQuestion     = null;
    }

    /// <summary>Ẩn phần đáp án của 1 player — giữ nguyên bên player kia.
    /// Shared mode: qua _active. Independent mode: gọi thẳng buttonDisplay.</summary>
    public void HidePlayerAnswers(Team team)
    {
        if (_active != null)
            _active.HidePlayerAnswers(team);
        else
            (_independentDisplay ?? (IAnswerDisplay)buttonDisplay)?.HidePlayerAnswers(team);
    }

    // ─── Slot selection ───────────────────────────────────────────────────────

    /// <summary>
    /// Weighted random: trả về 0 (Floating), 1 (Button), hoặc 2 (Matching).
    /// </summary>
    int PickSlot()
    {
        var cfg   = TongHopConfig.Current;
        int total = cfg.weightFloating + cfg.weightButton + cfg.weightMatching;
        if (total <= 0) return 1; // fallback = Button

        int roll = UnityEngine.Random.Range(0, total);
        if (roll < cfg.weightFloating)                          return 0;
        if (roll < cfg.weightFloating + cfg.weightButton)       return 1;
        return 2;
    }

    /// <summary>
    /// Lấy câu hỏi phù hợp với slot.
    /// Fallback chỉ xảy ra khi weight của loại đó > 0 — tránh hiện Matching khi weightMatching=0.
    /// </summary>
    QuestionData GetQuestionForSlot(int slot)
    {
        bool wantMatching  = (slot == 2);
        var  cfg           = TongHopConfig.Current;
        bool matchingAllowed = cfg.weightMatching > 0;
        bool chooseAllowed   = cfg.weightFloating + cfg.weightButton > 0;

        if (wantMatching)
        {
            if (questionPool.HasMatching) return questionPool.GetNextMatching();
            if (chooseAllowed && questionPool.HasChoose) return questionPool.GetNextChoose();   // fallback
        }
        else
        {
            if (questionPool.HasChoose)   return questionPool.GetNextChoose();
            if (matchingAllowed && questionPool.HasMatching) return questionPool.GetNextMatching(); // fallback
        }
        return null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    void HideAll()
    {
        floatingDisplay.gameObject.SetActive(false);
        buttonDisplay.gameObject.SetActive(false);
        matchingDisplay.gameObject.SetActive(false);
        solarSystemDisplay?.gameObject.SetActive(false);
        solarSystemEnDisplay?.gameObject.SetActive(false);
        planetOrderDisplay?.gameObject.SetActive(false);
    }

    SolarSystemDisplay GetOrCreateSolarSystem()
    {
        if (solarSystemDisplay != null) return solarSystemDisplay;

        var go = new GameObject("SolarSystemDisplay");
        go.transform.SetParent(transform.parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        solarSystemDisplay = go.AddComponent<SolarSystemDisplay>();
        solarSystemDisplay.planetHitPadding = solarHitPadding;
        go.SetActive(false);
        return solarSystemDisplay;
    }

    SolarSystemDisplay GetOrCreateSolarSystemEn()
    {
        if (solarSystemEnDisplay != null) return solarSystemEnDisplay;

        var go = new GameObject("SolarSystemEnDisplay");
        go.transform.SetParent(transform.parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        solarSystemEnDisplay = go.AddComponent<SolarSystemEnDisplay>();
        solarSystemEnDisplay.planetHitPadding = solarHitPadding;
        go.SetActive(false);
        return solarSystemEnDisplay;
    }

    PlanetOrderDisplay GetOrCreatePlanetOrder()
    {
        if (planetOrderDisplay != null) return planetOrderDisplay;

        var go = new GameObject("PlanetOrderDisplay");
        go.transform.SetParent(transform.parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        planetOrderDisplay = go.AddComponent<PlanetOrderDisplay>();
        go.SetActive(false);
        return planetOrderDisplay;
    }
}

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IAnswerDisplay
{
    /// <summary>
    /// onResult: khi cả 2 player hoàn thành (đúng hoặc cả 2 sai).
    /// onPlayerFailed: ngay khi 1 player hết lượt + sai, trước khi player kia xong.
    /// </summary>
    void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed);
    /// <summary>Ẩn đáp án của 1 bên player sau khi họ fail — không ảnh hưởng bên kia.</summary>
    void HidePlayerAnswers(Team team);
    void Cleanup();
}
