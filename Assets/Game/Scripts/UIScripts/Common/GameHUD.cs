using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reusable 2-player HUD: name labels, score numbers, score fill-bars, and a countdown timer.
///
/// HOW TO USE IN ANY GAME:
///   1. Drag the GameHUD prefab into the scene.
///   2. Wire up the 7 UI slots in the Inspector.
///   3. Call Initialize(scoreManager, leftName, rightName, maxScore) from your controller.
///   4. Call UpdateTimer(remaining) from Update().
///
/// HOW TO WIRE A SCORE BAR:
///   - Add a child Image (e.g. "LeftBar").
///   - Set Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.
///   - Set fillAmount = 0 in the Inspector (bar starts empty).
///   - Drag it into the leftScoreBar slot.
///   - The script drives fillAmount automatically.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Left Player")]
    [SerializeField] protected TextMeshProUGUI leftNameText;
    [SerializeField] protected TextMeshProUGUI leftScoreText;
    [SerializeField] protected Image           leftScoreBar;    // Image.Type.Filled (Horizontal)

    [Header("Right Player")]
    [SerializeField] protected TextMeshProUGUI rightNameText;
    [SerializeField] protected TextMeshProUGUI rightScoreText;
    [SerializeField] protected Image           rightScoreBar;   // Image.Type.Filled (Horizontal)

    [Header("Timer")]
    [SerializeField] protected TextMeshProUGUI timerText;

    [Header("Settings")]
    [Tooltip("Maximum score either player can reach (used to normalise fill bars). " +
             "Override via Initialize() — Inspector value is the default.")]
    [SerializeField] protected int maxScore = 10;

    ScoreManager _scoreManager;
    Image        _backgroundImage;

    void Awake()
    {
        var canvasRoot = GetComponentInParent<Canvas>()?.transform;

        // Tách Background ra Canvas root index 0 — render dưới tất cả content
        var bgChild = transform.Find("Background");
        if (bgChild != null)
        {
            _backgroundImage = bgChild.GetComponent<Image>();
            if (canvasRoot != null)
            {
                bgChild.SetParent(canvasRoot, false);
                bgChild.SetAsFirstSibling();
            }
        }

        // Override sorting: HUD luôn render trên cùng bất kể sibling order.
        // SetAsLastSibling() không đủ vì SolarSystemDisplay / FloorZoneClearer
        // được tạo lazy sau Awake và tự động trở thành last sibling.
        var cv = gameObject.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 10;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once at game start.
    /// </summary>
    /// <param name="scoreManager">The game's ScoreManager instance.</param>
    /// <param name="leftName">Display name for the left player.</param>
    /// <param name="rightName">Display name for the right player.</param>
    /// <param name="maxScore">
    /// Maximum reachable score (e.g. total rounds).
    /// Pass 0 to keep the Inspector default.
    /// </param>
    public void Initialize(ScoreManager scoreManager,
                           string leftName, string rightName,
                           int maxScore = 0)
    {
        _scoreManager = scoreManager;
        if (_scoreManager != null)
            _scoreManager.OnScoreChanged += HandleScoreChanged;

        if (maxScore > 0) this.maxScore = maxScore;

        if (leftNameText)  leftNameText.text  = leftName;
        if (rightNameText) rightNameText.text = rightName;

        RefreshScore(0, 0);
        ResetBars();
    }

    /// <summary>
    /// Call from the game controller's Update() while the timer is running.
    /// </summary>
    public void UpdateTimer(float timeRemaining)
    {
        if (timerText) timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
    }

    /// <summary>
    /// Đổi ảnh nền bằng Sprite (png import type Sprite 2D and UI).
    /// null → giữ nguyên sprite mặc định trong prefab.
    /// </summary>
    public void SetBackground(Sprite sprite)
    {
        if (_backgroundImage == null || sprite == null) return;
        _backgroundImage.sprite = sprite;
    }

    /// <summary>
    /// Đổi ảnh nền bằng Texture2D (jpg import type Texture — không cần Read/Write enabled).
    /// Sprite.Create() wrap texture cho Image hiện có, không thêm component mới.
    /// </summary>
    public void SetBackground(Texture2D texture)
    {
        if (_backgroundImage == null || texture == null) return;
        _backgroundImage.sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>
    /// Ẩn score bar (fill bar trái/phải) và star icon — dùng cho SolarQuiz
    /// nơi điểm số hiển thị theo số câu đúng, không cần thanh tiến trình.
    /// </summary>
    public void HideScoreBars()
    {
        // leftScoreBar/rightScoreBar là Image trên LeftScoreBarFill — .parent = container LeftScoreBar
        if (leftScoreBar  != null) leftScoreBar.transform.parent.gameObject.SetActive(false);
        if (rightScoreBar != null) rightScoreBar.transform.parent.gameObject.SetActive(false);
        transform.Find("LeftStarIcon")?.gameObject.SetActive(false);
    }

    /// <summary>Ẩn timer khi game kết thúc theo số vòng thay vì countdown.</summary>
    public void HideTimer()
    {
        if (timerText) timerText.gameObject.SetActive(false);
    }

    /// <summary>Cộng 1 điểm cho một bên — dùng cho FRTest (không qua ScoreManager).</summary>
    public void AddScore(Team team)
    {
        if (_scoreManager == null) return;
        _scoreManager.AddPoints(team, 1);
    }

    /// <summary>Update điểm trực tiếp — dùng khi không có ScoreManager (vd: FRTest).</summary>
    public void SetScore(int left, int right) => RefreshScore(left, right);

    /// <summary>Cập nhật tên người chơi sau nhận diện — dùng cho FRTest.</summary>
    public void UpdatePlayerNames(string left, string right)
    {
        if (leftNameText  && left  != null) leftNameText.text  = left;
        if (rightNameText && right != null) rightNameText.text = right;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void HandleScoreChanged(Team team, int left, int right)
        => RefreshScore(left, right);

    protected virtual void RefreshScore(int left, int right)
    {
        if (leftScoreText)  leftScoreText.text  = left.ToString();
        if (rightScoreText) rightScoreText.text = right.ToString();

        float leftFill  = maxScore > 0 ? Mathf.Clamp01((float)left  / maxScore) : 0f;
        float rightFill = maxScore > 0 ? Mathf.Clamp01((float)right / maxScore) : 0f;

        if (leftScoreBar)  leftScoreBar.fillAmount  = leftFill;
        if (rightScoreBar) rightScoreBar.fillAmount = rightFill;
    }

    void ResetBars()
    {
        if (leftScoreBar)  leftScoreBar.fillAmount  = 0f;
        if (rightScoreBar) rightScoreBar.fillAmount = 0f;
    }

    void OnDestroy()
    {
        if (_scoreManager != null)
            _scoreManager.OnScoreChanged -= HandleScoreChanged;
    }

}
