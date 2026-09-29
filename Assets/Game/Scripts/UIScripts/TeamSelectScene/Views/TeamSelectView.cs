using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main view for TeamSelect scene — manages main screen UI and delegates to panel sub-views.
/// </summary>
public class TeamSelectView : MonoBehaviour
{
    [Header("=== Top Bar ===")]
    [SerializeField] private Button backButton;
    [SerializeField] private Dropdown classDropdown;
    [SerializeField] private Button newClassButton;
    [SerializeField] private Button settingButton;

    [Header("=== Team Info Bar ===")]
    [SerializeField] private Button blueTeamButton;
    [SerializeField] private Image blueTeamBorder;
    [SerializeField] private Transform blueTeamAvatarContainer;
    [SerializeField] private Text blueTeamLabel;
    [SerializeField] private Button redTeamButton;
    [SerializeField] private Image redTeamBorder;
    [SerializeField] private Transform redTeamAvatarContainer;
    [SerializeField] private Text redTeamLabel;
    [SerializeField] private Button oneVsOneButton;   // single toggle button (same as teamModeButton)
    [SerializeField] private Button teamModeButton;   // same reference as oneVsOneButton
    [SerializeField] private Image oneVsOneHighlight;  // red border highlight
    [SerializeField] private Image teamModeHighlight;  // same reference as oneVsOneHighlight

    [Header("=== Mode Toggle ===")]
    [SerializeField] private Sprite modeSprite1vs1;
    [SerializeField] private Sprite modeSpriteTeam;
    [SerializeField] private Text modeText1vs1;
    [SerializeField] private Text modeTextTeam;
    private GameMode _currentMode = GameMode.OneVsOne;

    [Header("=== Player Grid ===")]
    [SerializeField] private Transform playerGridContent;
    [SerializeField] private GameObject playerIconTemplate;
    [SerializeField] private Button addPlayerButton;
    [SerializeField] private ScrollRect playerScrollRect;

    [Header("=== Bottom Bar ===")]
    [SerializeField] private Button savedTeamsButton;
    [SerializeField] private Button startButton;

    [Header("=== Sub Panels ===")]
    [SerializeField] private PanelClassView panelClassView;
    [SerializeField] private PanelPlayerView panelPlayerView;
    [SerializeField] private PanelTeamView panelTeamView;
    [SerializeField] private PanelSavedTeamView panelSavedTeamView;

    [Header("=== Avatar Template ===")]
    [SerializeField] private GameObject teamAvatarSlotTemplate;

    [Header("=== Character Sprites ===")]
    [SerializeField] private Sprite[] charHairSprites;    // 6 full character sprites (body+hair)
    [SerializeField] private Sprite charBodySprite;       // bald body sprite
    [SerializeField] private Sprite[] charHairFemaleSprites; // 6 female character sprites
    [SerializeField] private Sprite femaleBodySprite;        // female bald body
    [SerializeField] private Sprite[] bigGlassesSprites;  // 6 big glasses overlay sprites
    [SerializeField] private Sprite avatarBgBlue;         // blue rounded square bg
    [SerializeField] private Sprite avatarBgRed;          // red rounded square bg
    [SerializeField] private Sprite playerCardWhite;      // white dashed border (unselected)
    [SerializeField] private Sprite playerCardBlue;       // blue filled (blue team)
    [SerializeField] private Sprite playerCardRed;        // red filled (red team)

    // Main screen events
    public event Action OnClickBack = delegate { };
    public event Action OnClickStart = delegate { };
    public event Action OnClickSetting = delegate { };
    public event Action OnClickSavedTeams = delegate { };
    public event Action OnClickAddPlayer = delegate { };
    public event Action<string> OnClassSelected = delegate { };
    public event Action OnClickNewClass = delegate { };
    public event Action<GameMode> OnModeChanged = delegate { };
    public event Action<int> OnPlayerTapped = delegate { };
    public event Action<int> OnPlayerLongPressed = delegate { };
    public event Action OnBlueTeamTapped = delegate { };
    public event Action OnRedTeamTapped = delegate { };
    public event Action OnBlueTeamLongPressed = delegate { };
    public event Action OnRedTeamLongPressed = delegate { };
    public event Action OnClassLongPressed = delegate { };

    // Colors
    private Color _blueColor = new Color(0.3f, 0.5f, 1f, 0.4f);
    private Color _redColor = new Color(1f, 0.3f, 0.3f, 0.4f);
    private Color _blueLight = new Color(0.7f, 0.85f, 1f, 0f);
    private Color _redLight = new Color(1f, 0.75f, 0.75f, 0f);
    private Color _normalBg = new Color(0.92f, 0.92f, 0.92f, 1f);
    private Color _selectedBlue = new Color(0.6f, 0.78f, 1f, 1f);
    private Color _selectedRed = new Color(1f, 0.65f, 0.65f, 1f);
    private Color _modeActive = new Color(0.9f, 0.2f, 0.2f, 1f);   // red border for selected mode
    private Color _modeInactive = Color.clear;                       // invisible when not selected

    private List<GameObject> _playerIcons = new List<GameObject>();
    private List<GameObject> _blueAvatarSlots = new List<GameObject>();
    private List<GameObject> _redAvatarSlots = new List<GameObject>();
    private Coroutine _blinkCoroutine;
    private bool _blueHasMembers;
    private bool _redHasMembers;

    // Panel accessors for controller
    public PanelClassView PanelClass { get { return panelClassView; } }
    public PanelPlayerView PanelPlayer { get { return panelPlayerView; } }
    public PanelTeamView PanelTeam { get { return panelTeamView; } }
    public PanelSavedTeamView PanelSavedTeam { get { return panelSavedTeamView; } }

    public void InitView()
    {
        // Main buttons
        if (backButton != null) backButton.onClick.AddListener(() => OnClickBack());
        if (startButton != null) startButton.onClick.AddListener(() => OnClickStart());
        if (settingButton != null) settingButton.onClick.AddListener(() => OnClickSetting());
        if (savedTeamsButton != null) savedTeamsButton.onClick.AddListener(() => OnClickSavedTeams());
        if (addPlayerButton != null) addPlayerButton.onClick.AddListener(() => OnClickAddPlayer());
        if (newClassButton != null) newClassButton.onClick.AddListener(() => OnClickNewClass());

        // Mode toggle — single button, click toggles between 1vs1 and team
        if (oneVsOneButton != null)
        {
            oneVsOneButton.onClick.AddListener(() =>
            {
                _currentMode = _currentMode == GameMode.OneVsOne ? GameMode.Team : GameMode.OneVsOne;
                OnModeChanged(_currentMode);
            });
        }

        // Team buttons with LongPressButton
        SetupTeamButton(blueTeamButton, () => OnBlueTeamTapped(), () => OnBlueTeamLongPressed());
        SetupTeamButton(redTeamButton, () => OnRedTeamTapped(), () => OnRedTeamLongPressed());

        // Class dropdown — long-press opens PanelClass for editing
        if (classDropdown != null)
        {
            classDropdown.onValueChanged.AddListener((idx) =>
            {
                if (idx >= 0 && idx < classDropdown.options.Count)
                {
                    OnClassSelected(classDropdown.options[idx].text);
                }
            });

            LongPressButton classLpb = classDropdown.gameObject.GetComponent<LongPressButton>();
            if (classLpb == null) classLpb = classDropdown.gameObject.AddComponent<LongPressButton>();
            classLpb.OnLongPress += () => OnClassLongPressed();
        }

        // Init sub panels
        if (panelClassView != null) panelClassView.InitPanel();
        if (panelPlayerView != null) panelPlayerView.InitPanel();
        if (panelTeamView != null) panelTeamView.InitPanel();
        if (panelSavedTeamView != null) panelSavedTeamView.InitPanel();

        // Defaults
        SetStartButtonInteractable(false);
        SetMode(GameMode.OneVsOne);

        if (playerIconTemplate != null) playerIconTemplate.SetActive(false);
        if (teamAvatarSlotTemplate != null) teamAvatarSlotTemplate.SetActive(false);
    }

    private void SetupTeamButton(Button btn, Action onClick, Action onLongPress)
    {
        if (btn == null) return;
        LongPressButton lpb = btn.gameObject.GetComponent<LongPressButton>();
        if (lpb == null) lpb = btn.gameObject.AddComponent<LongPressButton>();
        lpb.OnClick += onClick;
        lpb.OnLongPress += onLongPress;
        // Disable default button click to avoid double-firing
        btn.onClick.RemoveAllListeners();
    }

    // ===== Class Dropdown =====

    public void PopulateClassDropdown(List<string> classNames, string selectedClass)
    {
        if (classDropdown == null) return;

        classDropdown.ClearOptions();
        classDropdown.AddOptions(new List<string>(classNames));

        int idx = classNames.IndexOf(selectedClass);
        if (idx >= 0) classDropdown.SetValueWithoutNotify(idx);
    }

    // ===== Player Grid =====

    public void PopulatePlayerGrid(List<PlayerInfo> players, List<string> bluePlayerIds, List<string> redPlayerIds)
    {
        // Clear existing icons
        foreach (GameObject icon in _playerIcons)
        {
            Destroy(icon);
        }
        _playerIcons.Clear();

        if (playerGridContent == null || playerIconTemplate == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            GameObject icon = Instantiate(playerIconTemplate, playerGridContent);
            icon.SetActive(true);

            // Set character body+hair sprite
            Transform avatarT = icon.transform.Find("Avatar");
            if (avatarT != null)
            {
                Image avatarImg = avatarT.GetComponent<Image>();
                if (avatarImg != null)
                {
                    int hairIdx = players[i].HairIndex;
                    Sprite[] hairArr = (players[i].Gender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                        ? charHairFemaleSprites : charHairSprites;
                    Sprite bodyFallback = (players[i].Gender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;
                    if (hairIdx >= 0 && hairArr != null && hairIdx < hairArr.Length && hairArr[hairIdx] != null)
                    {
                        avatarImg.sprite = hairArr[hairIdx];
                        avatarImg.preserveAspect = true;
                        avatarImg.color = Color.white;
                    }
                    else if (bodyFallback != null)
                    {
                        avatarImg.sprite = bodyFallback;
                        avatarImg.preserveAspect = true;
                        avatarImg.color = Color.white;
                    }
                }

                // Set glasses overlay
                Transform glassesT = avatarT.Find("Glasses");
                if (glassesT != null)
                {
                    Image glassesImg = glassesT.GetComponent<Image>();
                    int glassesIdx = players[i].GlassesIndex;
                    if (glassesIdx >= 0 && bigGlassesSprites != null && glassesIdx < bigGlassesSprites.Length && bigGlassesSprites[glassesIdx] != null)
                    {
                        glassesImg.sprite = bigGlassesSprites[glassesIdx];
                        glassesImg.preserveAspect = true;
                        glassesT.gameObject.SetActive(true);
                        // Monocle (index 2) — shift to one eye
                        RectTransform gRT = glassesT.GetComponent<RectTransform>();
                        if (glassesIdx == 2)
                        {
                            gRT.offsetMin = new Vector2(1.5f, -13.1f);
                            gRT.offsetMax = new Vector2(-34f, 11.4f);
                        }
                    }
                    else
                    {
                        glassesT.gameObject.SetActive(false);
                    }
                }
            }

            // Set name text
            Text nameText = icon.GetComponentInChildren<Text>();
            if (nameText != null) nameText.text = players[i].PlayerName;

            // Swap card background sprite based on team
            Image cardImg = icon.GetComponent<Image>();
            if (cardImg != null)
            {
                if (bluePlayerIds.Contains(players[i].PlayerId) && playerCardBlue != null)
                    cardImg.sprite = playerCardBlue;
                else if (redPlayerIds.Contains(players[i].PlayerId) && playerCardRed != null)
                    cardImg.sprite = playerCardRed;
                else if (playerCardWhite != null)
                    cardImg.sprite = playerCardWhite;
                cardImg.color = Color.white;
            }

            // LongPressButton for tap and long-press
            int index = i;
            LongPressButton lpb = icon.GetComponent<LongPressButton>();
            if (lpb == null) lpb = icon.AddComponent<LongPressButton>();
            lpb.OnClick += () => OnPlayerTapped(index);
            lpb.OnLongPress += () => OnPlayerLongPressed(index);

            _playerIcons.Add(icon);
        }
    }

    public void SetPlayerIconTeamColor(int index, string teamColor)
    {
        if (index < 0 || index >= _playerIcons.Count) return;
        Image cardImg = _playerIcons[index].GetComponent<Image>();
        if (cardImg == null) return;

        if (teamColor == "blue" && playerCardBlue != null)
            cardImg.sprite = playerCardBlue;
        else if (teamColor == "red" && playerCardRed != null)
            cardImg.sprite = playerCardRed;
        else if (playerCardWhite != null)
            cardImg.sprite = playerCardWhite;
        cardImg.color = Color.white;
    }

    // ===== Team Info Bar =====

    public void UpdateBlueTeam(List<PlayerInfo> members)
    {
        _blueHasMembers = members.Count > 0;
        UpdateTeamAvatars(blueTeamAvatarContainer, members, avatarBgBlue, ref _blueAvatarSlots);
    }

    public void UpdateRedTeam(List<PlayerInfo> members)
    {
        _redHasMembers = members.Count > 0;
        UpdateTeamAvatars(redTeamAvatarContainer, members, avatarBgRed, ref _redAvatarSlots);
    }

    private void UpdateTeamAvatars(Transform container, List<PlayerInfo> members, Sprite bgSprite, ref List<GameObject> slots)
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        if (container == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            // Create slot with bg sprite
            GameObject slot = new GameObject("Slot_" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(container, false);
            Image bgImg = slot.GetComponent<Image>();
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = Color.white;
            }

            // Character avatar (child, inset inside bg)
            GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(slot.transform, false);
            RectTransform avatarRT = avatar.GetComponent<RectTransform>();
            avatarRT.anchorMin = new Vector2(0.10f, 0.10f);
            avatarRT.anchorMax = new Vector2(0.90f, 0.90f);
            avatarRT.offsetMin = Vector2.zero;
            avatarRT.offsetMax = Vector2.zero;

            Image avatarImg = avatar.GetComponent<Image>();
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;

            int hairIdx = members[i].HairIndex;
            Sprite[] hairArr = (members[i].Gender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                ? charHairFemaleSprites : charHairSprites;
            Sprite bodyFallback = (members[i].Gender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;
            if (hairIdx >= 0 && hairArr != null && hairIdx < hairArr.Length && hairArr[hairIdx] != null)
            {
                avatarImg.sprite = hairArr[hairIdx];
                avatarImg.color = Color.white;
            }
            else if (bodyFallback != null)
            {
                avatarImg.sprite = bodyFallback;
                avatarImg.color = Color.white;
            }

            slots.Add(slot);
        }
    }

    // ===== Mode Toggle =====

    public void SetMode(GameMode mode)
    {
        _currentMode = mode;

        // Swap background sprite
        if (oneVsOneButton != null)
        {
            Image btnImage = oneVsOneButton.GetComponent<Image>();
            if (btnImage != null)
                btnImage.sprite = mode == GameMode.OneVsOne ? modeSprite1vs1 : modeSpriteTeam;
        }

        // Active text = white (on dark half), inactive = dark gray (on light half)
        Color activeColor = Color.red;
        Color inactiveColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        if (modeText1vs1 != null)
            modeText1vs1.color = mode == GameMode.OneVsOne ? activeColor : inactiveColor;
        if (modeTextTeam != null)
            modeTextTeam.color = mode == GameMode.Team ? activeColor : inactiveColor;

        // Hide saved teams button in 1v1 mode
        if (savedTeamsButton != null)
            savedTeamsButton.gameObject.SetActive(mode == GameMode.Team);
    }

    // ===== Blink Animation =====

    /// <summary>
    /// Set team panel brightness: active=bright, inactive=dim, both=both bright
    /// </summary>
    public void SetTeamPanelHighlight(bool blueActive, bool redActive)
    {
        if (blueTeamButton != null)
        {
            Image img = blueTeamButton.GetComponent<Image>();
            if (img != null) img.color = blueActive ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
        if (redTeamButton != null)
        {
            Image img = redTeamButton.GetComponent<Image>();
            if (img != null) img.color = redActive ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
    }

    public void SetBlueSlotBlinking(bool blinking)
    {
        // No blinking — handled by SetTeamPanelHighlight alpha
    }

    public void SetRedSlotBlinking(bool blinking)
    {
        // No blinking — handled by SetTeamPanelHighlight alpha
    }

    // ===== Start Button =====

    public void SetTeamLabels(string blueName, string redName)
    {
        if (blueTeamLabel != null) blueTeamLabel.text = blueName;
        if (redTeamLabel != null) redTeamLabel.text = redName;
    }

    public void SetStartButtonInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
    }
}
