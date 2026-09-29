using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectView : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Button oneVsOneButton;
    [SerializeField] private Button teamModeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button nextButton;

    // Mode select panel
    [SerializeField] private GameObject modeSelectPanel;

    // Character select panel (shown after mode selection)
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private Text selectingPlayerText;
    [SerializeField] private Text player1CharLabel;
    [SerializeField] private Text player2CharLabel;
    [SerializeField] private Image player1CharIcon;
    [SerializeField] private Image player2CharIcon;

    // Character grid (4x3 = 12 buttons)
    [SerializeField] private Button[] characterButtons = new Button[12];
    [SerializeField] private Image[] characterImages = new Image[12];
    [SerializeField] private Text[] characterNameTexts = new Text[12];

    // Highlight border for selected characters
    private int _p1SelectedIndex = -1;
    private int _p2SelectedIndex = -1;

    private static readonly Color NormalBorderColor = new Color(0.3f, 0.35f, 0.5f, 1f);
    private static readonly Color P1SelectedColor = new Color(0.2f, 0.6f, 1f, 1f);
    private static readonly Color P2SelectedColor = new Color(1f, 0.4f, 0.3f, 1f);
    private static readonly Color DisabledColor = new Color(0.2f, 0.2f, 0.25f, 0.5f);

    public event Action onClickOneVsOne;
    public event Action onClickTeamMode;
    public event Action onClickBack;
    public event Action onClickNext;
    public event Action<int> onCharacterSelected;

    public void InitView()
    {
        if (oneVsOneButton != null)
            oneVsOneButton.onClick.AddListener(() => onClickOneVsOne?.Invoke());
        if (teamModeButton != null)
            teamModeButton.onClick.AddListener(() => onClickTeamMode?.Invoke());
        if (backButton != null)
            backButton.onClick.AddListener(() => onClickBack?.Invoke());
        if (nextButton != null)
            nextButton.onClick.AddListener(() => onClickNext?.Invoke());

        // Wire character buttons
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null)
            {
                int index = i;
                characterButtons[i].onClick.AddListener(() => onCharacterSelected?.Invoke(index));
            }
        }

        // Set character names and colors
        for (int i = 0; i < 12; i++)
        {
            if (i < characterNameTexts.Length && characterNameTexts[i] != null)
                characterNameTexts[i].text = CharacterDatabase.CharacterNames[i];
            if (i < characterImages.Length && characterImages[i] != null)
                characterImages[i].color = CharacterDatabase.CharacterColors[i];
        }

        ShowModeSelectPanel();
    }

    public void ShowModeSelectPanel()
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(true);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (titleText != null) titleText.text = "Choose Mode";
    }

    public void ShowCharacterSelect(bool isTeamMode)
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (titleText != null) titleText.text = isTeamMode ? "Team Mode" : "1 vs 1";

        _p1SelectedIndex = -1;
        _p2SelectedIndex = -1;
        ResetAllCharacterBorders();
        UpdateSelectingPlayer(1);

        if (player1CharLabel != null) player1CharLabel.text = "P1: ?";
        if (player2CharLabel != null) player2CharLabel.text = "P2: ?";
        if (player1CharIcon != null) player1CharIcon.color = NormalBorderColor;
        if (player2CharIcon != null) player2CharIcon.color = NormalBorderColor;
    }

    public void UpdateSelectingPlayer(int playerNumber)
    {
        if (selectingPlayerText != null)
        {
            selectingPlayerText.text = "Player " + playerNumber + " - Choose your character!";
            selectingPlayerText.color = playerNumber == 1 ? P1SelectedColor : P2SelectedColor;
        }
    }

    public void SetPlayer1Character(int charIndex)
    {
        _p1SelectedIndex = charIndex;
        if (player1CharLabel != null)
            player1CharLabel.text = "P1: " + CharacterDatabase.CharacterNames[charIndex];
        if (player1CharIcon != null)
            player1CharIcon.color = CharacterDatabase.CharacterColors[charIndex];

        UpdateCharacterBorders();
    }

    public void SetPlayer2Character(int charIndex)
    {
        _p2SelectedIndex = charIndex;
        if (player2CharLabel != null)
            player2CharLabel.text = "P2: " + CharacterDatabase.CharacterNames[charIndex];
        if (player2CharIcon != null)
            player2CharIcon.color = CharacterDatabase.CharacterColors[charIndex];

        UpdateCharacterBorders();
    }

    public void ShowNextButton()
    {
        if (nextButton != null) nextButton.gameObject.SetActive(true);
    }

    public void DisableCharacter(int charIndex)
    {
        if (charIndex >= 0 && charIndex < characterButtons.Length && characterButtons[charIndex] != null)
        {
            characterButtons[charIndex].interactable = false;
            if (characterImages[charIndex] != null)
            {
                Color c = characterImages[charIndex].color;
                characterImages[charIndex].color = new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.5f);
            }
        }
    }

    public void EnableAllCharacters()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null)
            {
                characterButtons[i].interactable = true;
            }
        }
    }

    private void ResetAllCharacterBorders()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null)
            {
                characterButtons[i].interactable = true;
                if (i < characterImages.Length && characterImages[i] != null)
                    characterImages[i].color = CharacterDatabase.CharacterColors[i];
            }
        }
    }

    private void UpdateCharacterBorders()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] == null) continue;

            var outline = characterButtons[i].GetComponent<Outline>();
            if (outline == null)
                outline = characterButtons[i].gameObject.AddComponent<Outline>();

            if (i == _p1SelectedIndex)
            {
                outline.effectColor = P1SelectedColor;
                outline.effectDistance = new Vector2(3, 3);
                outline.enabled = true;
            }
            else if (i == _p2SelectedIndex)
            {
                outline.effectColor = P2SelectedColor;
                outline.effectDistance = new Vector2(3, 3);
                outline.enabled = true;
            }
            else
            {
                outline.enabled = false;
            }
        }
    }
}
