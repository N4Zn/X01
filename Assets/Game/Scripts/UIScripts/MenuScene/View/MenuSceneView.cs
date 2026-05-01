using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for MenuScene — game selection screen with category tabs, 2x5 game grid,
/// team banner (same style as TeamSelect), random selection, and navigation.
/// </summary>
public class MenuSceneView : MonoBehaviour
{
    [Header("=== Top Bar ===")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button settingsButton;

    [Header("=== Team Banner ===")]
    [SerializeField] private Text blueTeamNameText;
    [SerializeField] private Text redTeamNameText;
    [SerializeField] private Transform blueTeamAvatarContainer;
    [SerializeField] private Transform redTeamAvatarContainer;
    [SerializeField] private GameObject teamAvatarSlotTemplate;
    [SerializeField] private Sprite avatarBgBlue;
    [SerializeField] private Sprite avatarBgRed;
    [SerializeField] private Sprite[] charHairSprites;
    [SerializeField] private Sprite charBodySprite;

    [Header("=== Category Tabs ===")]
    [SerializeField] private Button[] categoryTabButtons;   // 5 tabs
    [SerializeField] private Image[] categoryTabImages;     // 5 tab images (for highlight swap)

    [Header("=== Game Grid (2 rows x 5 cols) ===")]
    [SerializeField] private Button[] gameButtons;          // 10 buttons (row-major: [row*5+col])
    [SerializeField] private Image[] gameIcons;             // 10 icon images
    [SerializeField] private Image[] gameHighlights;        // 10 selection highlights

    [Header("=== Bottom Bar ===")]
    [SerializeField] private Button randomSelectButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Text startButtonText;

    // Events
    public event Action onClickBack = delegate { };
    public event Action onClickHome = delegate { };
    public event Action onClickSettings = delegate { };
    public event Action<int> onCategorySelected = delegate { };     // 0-4
    public event Action<int> onGameSelected = delegate { };         // 0-9 (grid index)
    public event Action onClickRandomSelect = delegate { };
    public event Action onClickStart = delegate { };

    private Color _selectedGameColor = new Color(1f, 0.95f, 0.35f, 1f);    // bright yellow glow border
    private Color _unselectedGameColor = new Color(0f, 0f, 0f, 0f);        // fully transparent
    private readonly Dictionary<int, Coroutine> _glowRoutines = new Dictionary<int, Coroutine>();

    private List<GameObject> _blueAvatarSlots = new List<GameObject>();
    private List<GameObject> _redAvatarSlots = new List<GameObject>();

    public const int ROWS = 2;
    public const int COLS = 5;

    public static readonly string[] CategoryNames = { "Tinh toan", "Phan tich", "Hinh anh", "Tri nho", "Nhan biet" };

    /// <summary>
    /// Game names grid: [category, row] — 5 categories, 2 games each.
    /// </summary>
    public static readonly string[,] GameNames =
    {
        { "AddUp", "NumberAddUp" },
        { "TrainPath", "PathFinder" },
        { "PlanetOrder", "PlanetAlphabet" },
        { "???", "???" },
        { "???", "???" }
    };

    /// <summary>
    /// Scene name mapping. null = not implemented.
    /// </summary>
    public static readonly string[,] GameSceneNames =
    {
        { "AddUpGame", "NumberAddUpGame" },
        { "TrainPathGame", "PathFinderGame" },
        { "PlanetOrderGame", "PlanetAlphabetGame" },
        { null, null },
        { null, null }
    };

    public void InitView()
    {
        // Navigation buttons
        if (backButton != null) backButton.onClick.AddListener(() => onClickBack());
        if (homeButton != null) homeButton.onClick.AddListener(() => onClickHome());
        if (settingsButton != null) settingsButton.onClick.AddListener(() => onClickSettings());
        if (startButton != null) startButton.onClick.AddListener(() => onClickStart());
        if (randomSelectButton != null) randomSelectButton.onClick.AddListener(() => onClickRandomSelect());

        // Category tabs
        if (categoryTabButtons != null)
        {
            for (int i = 0; i < categoryTabButtons.Length; i++)
            {
                int idx = i;
                if (categoryTabButtons[i] != null)
                    categoryTabButtons[i].onClick.AddListener(() => onCategorySelected(idx));
            }
        }

        // Game grid buttons
        if (gameButtons != null)
        {
            for (int i = 0; i < gameButtons.Length; i++)
            {
                int idx = i;
                if (gameButtons[i] != null)
                    gameButtons[i].onClick.AddListener(() => onGameSelected(idx));
            }
        }
    }

    // ===== Team Banner =====

    public void UpdateTeamInfo(string blueTeamName, string redTeamName)
    {
        if (blueTeamNameText != null) blueTeamNameText.text = blueTeamName;
        if (redTeamNameText != null) redTeamNameText.text = redTeamName;
    }

    /// <summary>
    /// Populate blue team avatars from player list (same pattern as TeamSelectView).
    /// </summary>
    public void UpdateBlueTeamAvatars(List<PlayerInfo> members)
    {
        UpdateTeamAvatars(blueTeamAvatarContainer, members, avatarBgBlue, ref _blueAvatarSlots);
    }

    public void UpdateRedTeamAvatars(List<PlayerInfo> members)
    {
        UpdateTeamAvatars(redTeamAvatarContainer, members, avatarBgRed, ref _redAvatarSlots);
    }

    private void UpdateTeamAvatars(Transform container, List<PlayerInfo> members, Sprite bgSprite, ref List<GameObject> slots)
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        if (container == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            // Slot with bg sprite (Blue-button / Red-button)
            GameObject slot = new GameObject("Slot_" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(container, false);
            Image bgImg = slot.GetComponent<Image>();
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }

            // Character avatar inside
            GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(slot.transform, false);
            RectTransform avRT = avatar.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0.10f, 0.10f);
            avRT.anchorMax = new Vector2(0.90f, 0.90f);
            avRT.offsetMin = Vector2.zero;
            avRT.offsetMax = Vector2.zero;

            Image avImg = avatar.GetComponent<Image>();
            avImg.preserveAspect = true;
            avImg.raycastTarget = false;

            int hairIdx = members[i].HairIndex;
            if (hairIdx >= 0 && charHairSprites != null && hairIdx < charHairSprites.Length && charHairSprites[hairIdx] != null)
            {
                avImg.sprite = charHairSprites[hairIdx];
                avImg.color = Color.white;
            }
            else if (charBodySprite != null)
            {
                avImg.sprite = charBodySprite;
                avImg.color = Color.white;
            }

            slots.Add(slot);
        }
    }

    // ===== Category Tabs =====

    public void SetActiveCategory(int categoryIndex)
    {
        if (categoryTabImages == null) return;
        for (int i = 0; i < categoryTabImages.Length; i++)
        {
            if (categoryTabImages[i] != null)
            {
                float alpha = (i == categoryIndex) ? 1f : 0.5f;
                Color c = categoryTabImages[i].color;
                c.a = alpha;
                categoryTabImages[i].color = c;
            }
        }
    }

    // ===== Game Grid =====

    /// <summary>
    /// Update game grid enabled/disabled state based on which games are implemented.
    /// </summary>
    public void UpdateGameGridInteractable()
    {
        if (gameButtons == null) return;
        for (int col = 0; col < COLS; col++)
        {
            for (int row = 0; row < ROWS; row++)
            {
                int idx = row * COLS + col;
                if (idx < gameButtons.Length && gameButtons[idx] != null)
                {
                    bool implemented = GameSceneNames[col, row] != null;
                    gameButtons[idx].interactable = implemented;
                }
            }
        }
    }

    /// <summary>
    /// Set which games are highlighted (selected).
    /// Combines a circular glowing border + scale bounce/pulse on the icon.
    /// </summary>
    public void SetGameHighlights(HashSet<int> selectedIndices)
    {
        int count = 0;
        if (gameHighlights != null) count = Mathf.Max(count, gameHighlights.Length);
        if (gameIcons != null) count = Mathf.Max(count, gameIcons.Length);
        for (int i = 0; i < count; i++)
        {
            bool selected = selectedIndices.Contains(i);
            ApplyGlow(i, selected);
            ApplyGameIconSelectionEffect(i, selected);
        }
    }

    /// <summary>
    /// Highlight a single game.
    /// </summary>
    public void SetSingleGameHighlight(int index, bool selected)
    {
        ApplyGlow(index, selected);
        ApplyGameIconSelectionEffect(index, selected);
    }

    private void ApplyGlow(int index, bool selected)
    {
        if (gameHighlights == null || index < 0 || index >= gameHighlights.Length) return;
        Image border = gameHighlights[index];
        if (border == null) return;

        if (_glowRoutines.TryGetValue(index, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            _glowRoutines[index] = null;
        }

        if (selected)
        {
            _glowRoutines[index] = StartCoroutine(GlowRoutine(border));
        }
        else
        {
            border.color = _unselectedGameColor;
        }
    }

    private void ApplyGameIconSelectionEffect(int index, bool selected)
    {
        if (gameIcons == null || index < 0 || index >= gameIcons.Length) return;
        Image icon = gameIcons[index];
        if (icon == null) return;

        WinnerEffect fx = icon.GetComponent<WinnerEffect>();
        if (selected)
        {
            if (fx == null) fx = icon.gameObject.AddComponent<WinnerEffect>();
            fx.Stop();
            fx.PlayBounceIn(0.35f);
            StartCoroutine(StartIconPulseAfter(fx, 0.35f));
        }
        else
        {
            if (fx != null) fx.Stop();
            icon.transform.localScale = Vector3.one;
        }
    }

    private System.Collections.IEnumerator StartIconPulseAfter(WinnerEffect fx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fx != null) fx.StartPulse(0.08f, 3f);
    }

    private System.Collections.IEnumerator GlowRoutine(Image border)
    {
        float t = 0f;
        const float speed = 3.5f;  // radians per second
        const float minAlpha = 0.35f;
        const float maxAlpha = 1f;
        while (true)
        {
            t += Time.deltaTime * speed;
            float s = 0.5f * (1f + Mathf.Sin(t));
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, s);
            Color c = _selectedGameColor;
            c.a = alpha;
            border.color = c;
            yield return null;
        }
    }

    // ===== Start Button =====

    public void SetStartButtonEnabled(bool enabled)
    {
        if (startButton != null)
        {
            startButton.interactable = enabled;
            Image img = startButton.GetComponent<Image>();
            if (img != null)
                img.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
    }
}
