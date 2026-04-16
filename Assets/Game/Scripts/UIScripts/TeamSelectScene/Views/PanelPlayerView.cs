using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for editing a single player — hair, glasses, name, character preview.
/// Design: left side has name input + tabbed accessory grid (hair/glasses),
/// right side has character preview with layered sprites.
/// </summary>
public class PanelPlayerView : MonoBehaviour
{
    [Header("=== Panel Root ===")]
    [SerializeField] private GameObject panelRoot;

    [Header("=== Name & Info ===")]
    [SerializeField] private InputField nameInput;
    [SerializeField] private Text scoreText;
    [SerializeField] private Dropdown classDropdown;

    [Header("=== Accessory Tabs ===")]
    [SerializeField] private Button hairTabButton;
    [SerializeField] private Button glassesTabButton;
    [SerializeField] private Image hairTabImage;
    [SerializeField] private Image glassesTabImage;
    [SerializeField] private Image tabContainerBg;      // background image that swaps
    [SerializeField] private Sprite hairPopupSprite;     // hair tab container bg
    [SerializeField] private Sprite glassesPopupSprite;  // glasses tab container bg
    [SerializeField] private GameObject hairGrid;       // parent for 6 hair buttons
    [SerializeField] private GameObject glassesGrid;    // parent for 6 glasses buttons
    [SerializeField] private Button[] hairButtons;      // 6 hair style buttons
    [SerializeField] private Image[] hairCheckmarks;    // 6 checkmark overlays
    [SerializeField] private Button[] glassesButtons;   // 6 glasses buttons
    [SerializeField] private Image[] glassesCheckmarks; // 6 checkmark overlays

    [Header("=== Character Preview ===")]
    [SerializeField] private Image characterBodyImage;   // base body or body+hair
    [SerializeField] private Image characterGlassesImage; // glasses overlay
    [SerializeField] private Sprite[] charHairSprites;   // 6 full character sprites (body+hair)
    [SerializeField] private Sprite charBodySprite;      // bald body sprite
    [SerializeField] private Sprite[] bigGlassesSprites; // 6 big glasses overlay sprites

    [Header("=== Gender Toggle ===")]
    [SerializeField] private Button genderButton;
    [SerializeField] private Image genderIcon;
    [SerializeField] private Sprite maleBodySprite;    // body_male
    [SerializeField] private Sprite femaleBodySprite;  // body_female
    [SerializeField] private Sprite[] charHairMaleSprites;   // male hair variants
    [SerializeField] private Sprite[] charHairFemaleSprites; // female hair variants
    [SerializeField] private Sprite[] thumbHairMaleSprites;   // male hair grid thumbnails
    [SerializeField] private Sprite[] thumbHairFemaleSprites; // female hair grid thumbnails

    [Header("=== Buttons ===")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button closeButton;

    // Events
    public event Action OnClose = delegate { };
    public event Action OnSave = delegate { };
    public event Action OnDelete = delegate { };
    public event Action<string> OnNameChanged = delegate { };
    public event Action<string> OnClassChanged = delegate { };

    private int _selectedHairIndex = 0;
    private int _selectedGlassesIndex = -1;
    private int _selectedGender = 0; // 0=male, 1=female
    private bool _showingHairTab = true;

    public void InitPanel()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => OnClose());
        if (saveButton != null) saveButton.onClick.AddListener(() => OnSave());
        if (deleteButton != null) deleteButton.onClick.AddListener(() => OnDelete());
        if (nameInput != null) nameInput.onEndEdit.AddListener((val) => OnNameChanged(val));

        if (classDropdown != null)
        {
            classDropdown.onValueChanged.AddListener((idx) =>
            {
                if (idx >= 0 && idx < classDropdown.options.Count)
                    OnClassChanged(classDropdown.options[idx].text);
            });
        }

        // Gender toggle
        if (genderButton != null) genderButton.onClick.AddListener(() => ToggleGender());

        // Tab switching
        if (hairTabButton != null) hairTabButton.onClick.AddListener(() => SwitchToHairTab());
        if (glassesTabButton != null) glassesTabButton.onClick.AddListener(() => SwitchToGlassesTab());

        // Hair buttons
        if (hairButtons != null)
        {
            for (int i = 0; i < hairButtons.Length; i++)
            {
                int idx = i;
                if (hairButtons[i] != null)
                    hairButtons[i].onClick.AddListener(() => SelectHair(idx));
            }
        }

        // Glasses buttons
        if (glassesButtons != null)
        {
            for (int i = 0; i < glassesButtons.Length; i++)
            {
                int idx = i;
                if (glassesButtons[i] != null)
                    glassesButtons[i].onClick.AddListener(() => SelectGlasses(idx));
            }
        }

        Hide();
    }

    // ===== Show / Hide =====

    public void Show(PlayerInfo player, List<string> classNames, bool isNewPlayer)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        // Name
        if (nameInput != null)
        {
            nameInput.text = player.PlayerName;
            Image inputBg = nameInput.GetComponent<Image>();
            if (inputBg != null) inputBg.color = Color.white;
            if (nameInput.placeholder != null)
            {
                Text pt = nameInput.placeholder.GetComponent<Text>();
                if (pt != null) { pt.text = "Nhap ten..."; pt.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); }
            }
        }

        // Score
        if (scoreText != null) scoreText.text = "Score " + player.AccumulatedScore;

        // Class dropdown
        if (classDropdown != null)
        {
            classDropdown.ClearOptions();
            classDropdown.AddOptions(classNames);
            int classIdx = classNames.IndexOf(player.ClassName);
            if (classIdx >= 0) classDropdown.value = classIdx;
        }

        // Set selections
        _selectedGender = player.Gender;
        _selectedHairIndex = player.HairIndex >= 0 ? player.HairIndex : 0;
        _selectedGlassesIndex = player.GlassesIndex;

        // Show hair tab by default
        SwitchToHairTab();
        UpdateHairGridThumbnails();
        UpdateHairCheckmarks();
        UpdateGlassesCheckmarks();
        UpdateCharacterPreview();

        if (deleteButton != null) deleteButton.gameObject.SetActive(!isNewPlayer);
    }

    public void Hide()
    {
        if (nameInput != null) nameInput.text = "";
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ===== Getters =====

    public string GetPlayerName()
    {
        return nameInput != null ? nameInput.text : "";
    }

    public string GetSelectedClass()
    {
        if (classDropdown != null && classDropdown.value >= 0 && classDropdown.value < classDropdown.options.Count)
            return classDropdown.options[classDropdown.value].text;
        return "";
    }

    public int GetSelectedHair() { return _selectedHairIndex; }
    public int GetSelectedGlasses() { return _selectedGlassesIndex; }
    public int GetSelectedGender() { return _selectedGender; }

    // Keep backward compatibility
    public int GetSelectedAvatar() { return 0; }
    public int GetSelectedBackgroundColor() { return -1; }

    // ===== Tab Switching =====

    private void SwitchToHairTab()
    {
        _showingHairTab = true;
        if (hairGrid != null) hairGrid.SetActive(true);
        if (glassesGrid != null) glassesGrid.SetActive(false);
        if (tabContainerBg != null && hairPopupSprite != null) tabContainerBg.sprite = hairPopupSprite;
    }

    private void SwitchToGlassesTab()
    {
        _showingHairTab = false;
        if (hairGrid != null) hairGrid.SetActive(false);
        if (glassesGrid != null) glassesGrid.SetActive(true);
        if (tabContainerBg != null && glassesPopupSprite != null) tabContainerBg.sprite = glassesPopupSprite;
    }

    // ===== Selection =====

    private void SelectHair(int index)
    {
        _selectedHairIndex = index;
        UpdateHairCheckmarks();
        UpdateCharacterPreview();
    }

    private void SelectGlasses(int index)
    {
        // Toggle: click same glasses again to remove
        if (_selectedGlassesIndex == index)
            _selectedGlassesIndex = -1;
        else
            _selectedGlassesIndex = index;
        UpdateGlassesCheckmarks();
        UpdateCharacterPreview();
    }

    // ===== Gender Toggle =====

    private void ToggleGender()
    {
        _selectedGender = (_selectedGender == 0) ? 1 : 0;
        UpdateHairGridThumbnails();
        UpdateCharacterPreview();
    }

    // ===== UI Updates =====

    private void UpdateHairGridThumbnails()
    {
        if (hairButtons == null) return;
        Sprite[] thumbs = (_selectedGender == 1 && thumbHairFemaleSprites != null && thumbHairFemaleSprites.Length > 0)
            ? thumbHairFemaleSprites : thumbHairMaleSprites;
        if (thumbs == null) return;
        for (int i = 0; i < hairButtons.Length && i < thumbs.Length; i++)
        {
            if (hairButtons[i] != null && thumbs[i] != null)
            {
                Image img = hairButtons[i].GetComponent<Image>();
                if (img != null) img.sprite = thumbs[i];
            }
        }
    }

    private void UpdateHairCheckmarks()
    {
        if (hairCheckmarks == null) return;
        for (int i = 0; i < hairCheckmarks.Length; i++)
        {
            if (hairCheckmarks[i] != null)
                hairCheckmarks[i].gameObject.SetActive(i == _selectedHairIndex);
        }
    }

    private void UpdateGlassesCheckmarks()
    {
        if (glassesCheckmarks == null) return;
        for (int i = 0; i < glassesCheckmarks.Length; i++)
        {
            if (glassesCheckmarks[i] != null)
                glassesCheckmarks[i].gameObject.SetActive(i == _selectedGlassesIndex);
        }
    }

    private void UpdateCharacterPreview()
    {
        // Body + hair layer (gender-aware)
        if (characterBodyImage != null)
        {
            Sprite[] hairSprites = (_selectedGender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                ? charHairFemaleSprites : charHairSprites;
            Sprite fallbackBody = (_selectedGender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;

            if (_selectedHairIndex >= 0 && hairSprites != null &&
                _selectedHairIndex < hairSprites.Length && hairSprites[_selectedHairIndex] != null)
                characterBodyImage.sprite = hairSprites[_selectedHairIndex];
            else if (fallbackBody != null)
                characterBodyImage.sprite = fallbackBody;
        }

        // Glasses overlay layer
        if (characterGlassesImage != null)
        {
            if (_selectedGlassesIndex >= 0 && bigGlassesSprites != null &&
                _selectedGlassesIndex < bigGlassesSprites.Length && bigGlassesSprites[_selectedGlassesIndex] != null)
            {
                characterGlassesImage.sprite = bigGlassesSprites[_selectedGlassesIndex];
                characterGlassesImage.gameObject.SetActive(true);

                // Adjust rect per glasses type
                RectTransform gRT = characterGlassesImage.rectTransform;
                if (_selectedGlassesIndex == 2) // Monocle — shift to one eye
                {
                    gRT.anchorMin = new Vector2(0.62f, 0.46f);
                    gRT.anchorMax = new Vector2(0.75f, 0.64f);
                    gRT.offsetMin = new Vector2(69f, -88f);
                    gRT.offsetMax = new Vector2(12f, -91f);
                }
                else // Normal glasses — default position
                {
                    gRT.anchorMin = new Vector2(0.62f, 0.48f);
                    gRT.anchorMax = new Vector2(0.82f, 0.62f);
                    gRT.anchoredPosition = new Vector2(39.16f, -65.50f);
                    gRT.sizeDelta = new Vector2(-98.85f, -36.42f);
                }
            }
            else
            {
                characterGlassesImage.gameObject.SetActive(false);
            }
        }
    }

    public void ShowNameError(string message)
    {
        if (nameInput == null) return;
        Image inputBg = nameInput.GetComponent<Image>();
        if (inputBg != null) inputBg.color = new Color(1f, 0.8f, 0.8f, 1f);
        if (nameInput.placeholder != null)
        {
            Text pt = nameInput.placeholder.GetComponent<Text>();
            if (pt != null) { pt.text = message; pt.color = new Color(0.9f, 0.2f, 0.2f, 1f); }
        }
        nameInput.ActivateInputField();
    }
}
