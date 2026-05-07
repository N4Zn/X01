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
    //NamNN edit with AS Agent on 04/05/2026 15:20
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

    [Header("=== Game Grid (3 rows x 6 cols) ===")]
    [SerializeField] private GameObject[] gameSlots;        // Drag 18 GameSlot GameObjects here
    private Button[] _gameButtons;                          // Found automatically
    private Image[] _gameIcons;                             // Found automatically
    private Image[] _gameHighlights;                        // Found automatically

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

    public const int MAX_GAMES_PER_PAGE = 18;
    public const int CATEGORY_COUNT = 6;

    public static readonly string[] CategoryNames = { "Tinh toan", "Phan tich", "Hinh anh", "Tri nho", "Nhan biet", "Am thanh" };

    /// <summary>
    /// Game names: [categoryIndex, gridIndex] — 6 categories, 18 games each.
    /// </summary>
    public static readonly string[,] GameNames = new string[CATEGORY_COUNT, MAX_GAMES_PER_PAGE]
    {
        { "AddUp", "NumberAddUp", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD" },
        { "TrainPath", "PathFinder", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD" },
        { "PlanetOrder", "PlanetAlphabet", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD" },
        { "PlanetAlphabet", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD" },
        { "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD"},
        { "ListenSelect", "ChuCai", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD", "TBD" }
    };

    /// <summary>
    /// Scene names: [categoryIndex, gridIndex]
    /// </summary>
    public static readonly string[,] GameSceneNames = new string[CATEGORY_COUNT, MAX_GAMES_PER_PAGE]
    {
        { "AddUpGame", "NumberAddUpGame", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null },
        { "TrainPathGame", "PathFinderGame", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null },
        { "PlanetOrderGame", "PlanetAlphabetGame", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null },
        { "PlanetAlphabetGame", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null },
        { null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null },
        { "ListenGame", "ChuCaiGame", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null }
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

        // Game grid slots
        //NamNN edit with AS Agent on 04/05/2026 15:20
        if (gameSlots != null)
        {
            _gameButtons = new Button[gameSlots.Length];
            _gameIcons = new Image[gameSlots.Length];
            _gameHighlights = new Image[gameSlots.Length];

            for (int i = 0; i < gameSlots.Length; i++)
            {
                int idx = i;
                if (gameSlots[i] != null)
                {
                    // Find Button (either on slot or in children)
                    _gameButtons[idx] = gameSlots[i].GetComponentInChildren<Button>();
                    if (_gameButtons[idx] != null)
                    {
                        _gameButtons[idx].onClick.RemoveAllListeners();
                        _gameButtons[idx].onClick.AddListener(() => onGameSelected(idx));
                    }

                    // Find Icon and Highlight by name in children
                    Transform iconTrans = gameSlots[i].transform.Find("Icon");
                    if (iconTrans != null) _gameIcons[i] = iconTrans.GetComponent<Image>();

                    Transform highlightTrans = gameSlots[i].transform.Find("Highlight");
                    if (highlightTrans != null) _gameHighlights[i] = highlightTrans.GetComponent<Image>();
                }
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
        // NamNN: Dọn dẹp an toàn để tránh lỗi SerializedObject trong Editor
        if (slots != null)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] != null)
                {
                    if (!Application.isPlaying) DestroyImmediate(slots[i]);
                    else Destroy(slots[i]);
                }
            }
            slots.Clear();
        }

        if (container == null || members == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            // Tạo Slot an toàn
            GameObject slot = new GameObject("Slot_" + i, typeof(RectTransform));
            slot.transform.SetParent(container, false);

            Image bgImg = slot.AddComponent<Image>();
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }

            // Character avatar inside
            GameObject avatar = new GameObject("Avatar", typeof(RectTransform));
            avatar.transform.SetParent(slot.transform, false);

            RectTransform avRT = avatar.GetComponent<RectTransform>();
            if (avRT != null)
            {
                avRT.anchorMin = new Vector2(0.10f, 0.10f);
                avRT.anchorMax = new Vector2(0.90f, 0.90f);
                avRT.offsetMin = Vector2.zero;
                avRT.offsetMax = Vector2.zero;
            }

            Image avImg = avatar.AddComponent<Image>();
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
    /// Update grid display for a specific category (tab).
    /// Hides buttons with no game name, sets interactable if scene is implemented.
    /// </summary>
    public void UpdateGrid(int categoryIndex)
    {
        //NamNN edit with AS Agent on 04/05/2026 16:15
        if (gameSlots == null || _gameButtons == null) return;
        for (int i = 0; i < gameSlots.Length; i++)
        {
            if (i >= MAX_GAMES_PER_PAGE)
            {
                gameSlots[i].SetActive(false);
                continue;
            }

            gameSlots[i].SetActive(true);

            string sceneName = GameSceneNames[categoryIndex, i];
            string gameName = GameNames[categoryIndex, i];

            bool hasGame = !string.IsNullOrEmpty(gameName) && gameName != "???";
            bool isImplemented = !string.IsNullOrEmpty(sceneName);

            // 1. Update Icon Sprite dynamically from Resources
            if (_gameIcons[i] != null)
            {
                // Sửa lại đoạn nạp Icon trong MenuSceneView.cs (khoảng dòng 230)
                if (hasGame)
                {
                    // 1. Luôn ưu tiên nạp trực tiếp bằng kiểu <Sprite>
                    Sprite loadedSprite = Resources.Load<Sprite>("GameIcons/" + gameName);

                    // 2. Nếu vẫn null, thử nạp kiểu Object để debug sâu
                    if (loadedSprite == null)
                    {
                        UnityEngine.Object raw = Resources.Load("GameIcons/" + gameName);
                        if (raw == null) {
                            Debug.LogError($"LỖI: Không thấy file tại Resources/GameIcons/{gameName}");
                        } else {
                            Debug.LogError($"LỖI: Tìm thấy file {gameName} nhưng nó là {raw.GetType().Name}. " +
                                           "HÃY ĐỔI 'Sprite Mode' THÀNH 'Single' VÀ NHẤN APPLY!");
                        }
                        // Fallback về icon mặc định
                        loadedSprite = Resources.Load<Sprite>("GameIcons/icon_unknown");
                    }

                    _gameIcons[i].sprite = loadedSprite;
                    Color c = _gameIcons[i].color;
                    c.a = 1f;
                    _gameIcons[i].color = c;
                }
                else
                {
                    // If "???", you can set a default "Locked" or "Question" sprite if you have one
                    // _gameIcons[i].sprite = defaultLockedSprite;
                    Color c = _gameIcons[i].color;
                    c.a = 0.35f;
                    _gameIcons[i].color = c;
                }
            }

            // 2. Set interactivity
            if (_gameButtons[i] != null)
            {
                _gameButtons[i].interactable = hasGame && isImplemented;
            }

            ApplyGlow(i, false);
            ApplyGameIconSelectionEffect(i, false);
        }
    }

    /// <summary>
    /// Set which games are highlighted (selected).
    /// Combines a circular glowing border + scale bounce/pulse on the icon.
    /// </summary>
    public void SetGameHighlights(HashSet<int> selectedIndices)
    {
        int count = gameSlots != null ? gameSlots.Length : 0;
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
        if (_gameHighlights == null || index < 0 || index >= _gameHighlights.Length) return;
        Image border = _gameHighlights[index];
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
        //NamNN edit with AS Agent on 04/05/2026 15:45
        if (_gameIcons == null || index < 0 || index >= _gameIcons.Length) return;
        Image icon = _gameIcons[index];
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
