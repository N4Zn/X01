using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for the AddUp game - handles all UI display and user interaction.
/// New layout: equation boxes (A + B = C) with hidden operand + 4 answer buttons.
/// Split screen for 2 players, resolution 1024x600.
/// </summary>
public class AddUpGameView : MonoBehaviour
{
    [Header("=== Common UI ===")]
    [SerializeField] private Text questionText;
    [SerializeField] private Text timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverResultText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button settingButton;

    [Header("=== Team Bar Names ===")]
    [SerializeField] private Text p1BarNameText;
    [SerializeField] private Text p2BarNameText;

    [Header("=== Score Bars ===")]
    [SerializeField] private Image p1ScoreBarFill;
    [SerializeField] private Image p2ScoreBarFill;
    [SerializeField] private int maxScore = 10;

    [Header("=== Player 1 (Left) ===")]
    [SerializeField] private Text p1NameText;
    [SerializeField] private Text p1ScoreText;
    [SerializeField] private Transform p1BoxAImageContainer;
    [SerializeField] private Transform p1BoxBImageContainer;
    [SerializeField] private Transform p1BoxCImageContainer;
    [SerializeField] private Image p1BoxABorder;
    [SerializeField] private Image p1BoxBBorder;
    [SerializeField] private Image p1BoxCBorder;
    [SerializeField] private Text p1PlusLabel;
    [SerializeField] private Text p1EqualsLabel;
    [SerializeField] private GameObject p1HiddenOverlay;
    [SerializeField] private GameObject p1HiddenOverlayB;
    [SerializeField] private Button p1Answer1Button;
    [SerializeField] private Button p1Answer2Button;
    [SerializeField] private Button p1Answer3Button;
    [SerializeField] private Transform p1Answer1ImageContainer;
    [SerializeField] private Transform p1Answer2ImageContainer;
    [SerializeField] private Transform p1Answer3ImageContainer;
    [SerializeField] private Image p1Answer1Border;
    [SerializeField] private Image p1Answer2Border;
    [SerializeField] private Image p1Answer3Border;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;

    [Header("=== Player 2 (Right) ===")]
    [SerializeField] private Text p2NameText;
    [SerializeField] private Text p2ScoreText;
    [SerializeField] private Transform p2BoxAImageContainer;
    [SerializeField] private Transform p2BoxBImageContainer;
    [SerializeField] private Transform p2BoxCImageContainer;
    [SerializeField] private Image p2BoxABorder;
    [SerializeField] private Image p2BoxBBorder;
    [SerializeField] private Image p2BoxCBorder;
    [SerializeField] private Text p2PlusLabel;
    [SerializeField] private Text p2EqualsLabel;
    [SerializeField] private GameObject p2HiddenOverlay;
    [SerializeField] private GameObject p2HiddenOverlayB;
    [SerializeField] private Button p2Answer1Button;
    [SerializeField] private Button p2Answer2Button;
    [SerializeField] private Button p2Answer3Button;
    [SerializeField] private Transform p2Answer1ImageContainer;
    [SerializeField] private Transform p2Answer2ImageContainer;
    [SerializeField] private Transform p2Answer3ImageContainer;
    [SerializeField] private Image p2Answer1Border;
    [SerializeField] private Image p2Answer2Border;
    [SerializeField] private Image p2Answer3Border;
    [SerializeField] private GameObject p2CorrectIcon;
    [SerializeField] private GameObject p2WrongIcon;

    [Header("=== Countdown (Team Mode) ===")]
    [SerializeField] private Text p1CountdownText;
    [SerializeField] private Text p2CountdownText;

    [Header("=== Image Prefab ===")]
    [SerializeField] private GameObject imagePrefab;

    // Colors
    private Color normalBorderColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    private Color correctBorderColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private Color wrongBorderColor = new Color(0.6f, 0.2f, 0.8f, 1f);
    private Color defaultAnswerColor = Color.white;
    private Color hiddenBoxColor = Color.white;

    // Events
    public event Action<int, int> OnAnswerSelected = delegate { }; // playerIndex, answerIndex (0-3)
    public event Action OnRetryClicked = delegate { };
    public event Action OnBackClicked = delegate { };
    public event Action OnHomeClicked = delegate { };
    public event Action OnSettingClicked = delegate { };

    public void InitView()
    {
        // Setup answer button listeners for Player 1
        p1Answer1Button.onClick.AddListener(() => OnAnswerSelected(0, 0));
        p1Answer2Button.onClick.AddListener(() => OnAnswerSelected(0, 1));
        p1Answer3Button.onClick.AddListener(() => OnAnswerSelected(0, 2));

        // Setup answer button listeners for Player 2
        p2Answer1Button.onClick.AddListener(() => OnAnswerSelected(1, 0));
        p2Answer2Button.onClick.AddListener(() => OnAnswerSelected(1, 1));
        p2Answer3Button.onClick.AddListener(() => OnAnswerSelected(1, 2));

        // Retry and Back buttons
        if (retryButton != null) retryButton.onClick.AddListener(() => OnRetryClicked());
        if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked());
        if (homeButton != null) homeButton.onClick.AddListener(() => OnHomeClicked());
        if (settingButton != null) settingButton.onClick.AddListener(() => OnSettingClicked());

        // Set initial UI
        SetQuestionText("Fill in the missing number!");
        if (GameSessionManager.Instance != null)
            SetPlayerNames(GameSessionManager.Instance.GetDisplayName1(), GameSessionManager.Instance.GetDisplayName2());
        else
            SetPlayerNames("Player 1", "Player 2");
        UpdateScores(0, 0);
        HideGameOverPanel();
        HideFeedbackIcons();
    }

    public void SetQuestionText(string text)
    {
        if (questionText != null)
            questionText.text = text;
    }

    public void SetPlayerNames(string name1, string name2)
    {
        if (p1NameText != null) p1NameText.text = name1;
        if (p2NameText != null) p2NameText.text = name2;
        if (p1BarNameText != null) p1BarNameText.text = name1;
        if (p2BarNameText != null) p2BarNameText.text = name2;
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(timeRemaining);
            timerText.text = seconds.ToString();
        }
    }

    public void UpdateScores(int p1Score, int p2Score)
    {
        if (p1ScoreText != null) p1ScoreText.text = p1Score.ToString();
        if (p2ScoreText != null) p2ScoreText.text = p2Score.ToString();

        // Update progress bars (fill from bottom)
        if (p1ScoreBarFill != null)
            p1ScoreBarFill.fillAmount = Mathf.Clamp01((float)p1Score / maxScore);
        if (p2ScoreBarFill != null)
            p2ScoreBarFill.fillAmount = Mathf.Clamp01((float)p2Score / maxScore);
    }

    public void UpdateStars(int playerIndex, int stars)
    {
        Text t = playerIndex == 0 ? p1ScoreText : p2ScoreText;
        if (t != null) t.text = stars.ToString();

        Image fill = playerIndex == 0 ? p1ScoreBarFill : p2ScoreBarFill;
        if (fill != null)
            fill.fillAmount = Mathf.Clamp01((float)stars / maxScore);
    }

    /// <summary>
    /// Display the equation boxes: A + B = C with one hidden
    /// </summary>
    public void DisplayEquation(int playerIndex, int numberA, int numberB, string hiddenPosition, string imageType)
    {
        int sum = numberA + numberB;

        Transform boxA = playerIndex == 0 ? p1BoxAImageContainer : p2BoxAImageContainer;
        Transform boxB = playerIndex == 0 ? p1BoxBImageContainer : p2BoxBImageContainer;
        Transform boxC = playerIndex == 0 ? p1BoxCImageContainer : p2BoxCImageContainer;
        GameObject overlay = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;

        // Hide second overlay (only used in Level 4)
        if (overlayB != null) overlayB.SetActive(false);

        // Display items in visible boxes
        if (hiddenPosition == "a")
        {
            ClearContainer(boxA);
            DisplayItemsInContainer(boxB, numberB, imageType);
            DisplayItemsInContainer(boxC, sum, imageType);
            PositionOverlayOnBox(overlay, boxA);
        }
        else
        {
            DisplayItemsInContainer(boxA, numberA, imageType);
            ClearContainer(boxB);
            DisplayItemsInContainer(boxC, sum, imageType);
            PositionOverlayOnBox(overlay, boxB);
        }
    }

    /// <summary>
    /// Level 4: display "? + ? = target" — both operands hidden, show target in BoxC.
    /// </summary>
    public void DisplayEquationLevel4(int playerIndex, int target, string imageType)
    {
        Transform boxA = playerIndex == 0 ? p1BoxAImageContainer : p2BoxAImageContainer;
        Transform boxB = playerIndex == 0 ? p1BoxBImageContainer : p2BoxBImageContainer;
        Transform boxC = playerIndex == 0 ? p1BoxCImageContainer : p2BoxCImageContainer;
        GameObject overlay = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;

        // Both A and B hidden, C shows target
        ClearContainer(boxA);
        ClearContainer(boxB);
        DisplayItemsInContainer(boxC, target, imageType);

        // Show ? overlay on both BoxA and BoxB
        PositionOverlayOnBox(overlay, boxA);
        if (overlayB != null) PositionOverlayOnBox(overlayB, boxB);
    }

    /// <summary>
    /// Position the hidden overlay exactly on top of a box (BoxA or BoxB).
    /// boxContainer = the ImageContainer (child of BoxA/BoxB).
    /// Overlay becomes sibling of BoxA/BoxB in the panel, matching the box's anchors.
    /// </summary>
    private void PositionOverlayOnBox(GameObject overlay, Transform boxContainer)
    {
        if (overlay == null || boxContainer == null) return;
        overlay.SetActive(true);

        // boxContainer is ImageContainer inside BoxA/BoxB
        // boxContainer.parent = BoxA/BoxB (the Image with green card sprite)
        Transform box = boxContainer.parent; // BoxA or BoxB
        Transform panel = box.parent;        // Player panel

        overlay.transform.SetParent(panel, false);
        RectTransform overlayRT = overlay.GetComponent<RectTransform>();
        RectTransform boxRT = box.GetComponent<RectTransform>();

        if (overlayRT != null && boxRT != null)
        {
            overlayRT.anchorMin = boxRT.anchorMin;
            overlayRT.anchorMax = boxRT.anchorMax;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Display answer choices (4 buttons with item images)
    /// </summary>
    public void DisplayAnswers(int playerIndex, int answer1, int answer2, int answer3, string imageType)
    {
        Transform[] containers = GetAnswerImageContainers(playerIndex);
        int[] answers = { answer1, answer2, answer3 };
        for (int i = 0; i < 3; i++)
        {
            DisplayItemsInContainer(containers[i], answers[i], imageType);
        }

        // Reset answer border colors
        ResetAnswerBorders(playerIndex);
    }

    /// <summary>
    /// Highlight the selected answer as correct (green)
    /// </summary>
    public void SetCorrectAnswerBorder(int playerIndex, int answerIndex)
    {
        Image border = GetAnswerBorder(playerIndex, answerIndex);
        if (border != null)
        {
            border.color = correctBorderColor;
        }
    }

    /// <summary>
    /// Highlight the selected answer as wrong (purple)
    /// </summary>
    public void SetWrongAnswerBorder(int playerIndex, int answerIndex)
    {
        Image border = GetAnswerBorder(playerIndex, answerIndex);
        if (border != null)
        {
            border.color = wrongBorderColor;
        }
    }

    /// <summary>
    /// Reset all answer borders to default
    /// </summary>
    public void ResetAnswerBorders(int playerIndex)
    {
        for (int i = 0; i < 4; i++)
        {
            Image border = GetAnswerBorder(playerIndex, i);
            if (border != null)
            {
                border.color = defaultAnswerColor;
            }
        }
    }

    /// <summary>
    /// Show correct/wrong feedback icon for a player
    /// </summary>
    public void ShowFeedback(int playerIndex, bool isCorrect)
    {
        GameObject icon = isCorrect
            ? (playerIndex == 0 ? p1CorrectIcon : p2CorrectIcon)
            : (playerIndex == 0 ? p1WrongIcon : p2WrongIcon);
        GameObject other = isCorrect
            ? (playerIndex == 0 ? p1WrongIcon : p2WrongIcon)
            : (playerIndex == 0 ? p1CorrectIcon : p2CorrectIcon);

        if (other != null) other.SetActive(false);
        if (icon != null)
        {
            icon.SetActive(true);
            FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
            if (fx == null) fx = icon.AddComponent<FeedbackEffect>();
            fx.Play(isCorrect);
        }
    }

    public void HideFeedbackIcons()
    {
        StopFeedbackEffect(p1CorrectIcon); StopFeedbackEffect(p1WrongIcon);
        StopFeedbackEffect(p2CorrectIcon); StopFeedbackEffect(p2WrongIcon);
    }

    public void HideFeedbackIcon(int playerIndex)
    {
        if (playerIndex == 0) { StopFeedbackEffect(p1CorrectIcon); StopFeedbackEffect(p1WrongIcon); }
        else { StopFeedbackEffect(p2CorrectIcon); StopFeedbackEffect(p2WrongIcon); }
    }

    private void StopFeedbackEffect(GameObject icon)
    {
        if (icon == null) return;
        FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
        if (fx != null) fx.StopEffect();
        icon.SetActive(false);
    }

    public void ShowGameOverPanel(int p1Score, int p2Score)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverResultText != null)
            {
                if (p1Score > p2Score)
                    gameOverResultText.text = "Player 1 Wins!\n" + p1Score + " - " + p2Score;
                else if (p2Score > p1Score)
                    gameOverResultText.text = "Player 2 Wins!\n" + p1Score + " - " + p2Score;
                else
                    gameOverResultText.text = "It's a Draw!\n" + p1Score + " - " + p2Score;
            }
        }
    }

    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// Enable or disable answer buttons for a player
    /// </summary>
    public void SetPlayerAnswersInteractable(int playerIndex, bool interactable)
    {
        Button[] buttons = GetAnswerButtons(playerIndex);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null) buttons[i].interactable = interactable;
        }
    }

    public void SetAnswerButtonInteractable(int playerIndex, int answerIndex, bool interactable)
    {
        Button[] buttons = GetAnswerButtons(playerIndex);
        if (answerIndex < 0 || answerIndex >= buttons.Length) return;
        if (buttons[answerIndex] != null) buttons[answerIndex].interactable = interactable;
    }

    // ===== Helper methods =====

    private void DisplayItemsInContainer(Transform container, int count, string imageType)
    {
        ClearContainer(container);
        Sprite sprite = Resources.Load<Sprite>("ui/addup/" + imageType);
        for (int i = 0; i < count; i++)
        {
            GameObject img = Instantiate(imagePrefab, container);
            img.SetActive(true);
            // Set sprite on child IconImage (circle-masked prefab)
            Transform iconT = img.transform.Find("IconImage");
            if (iconT != null)
            {
                Image iconImg = iconT.GetComponent<Image>();
                if (iconImg != null && sprite != null) iconImg.sprite = sprite;
            }
            else
            {
                // Fallback: direct image (old prefab)
                Image imgComponent = img.GetComponent<Image>();
                if (imgComponent != null && sprite != null) imgComponent.sprite = sprite;
            }
        }
    }

    private Transform[] GetAnswerImageContainers(int playerIndex)
    {
        if (playerIndex == 0)
            return new Transform[] { p1Answer1ImageContainer, p1Answer2ImageContainer, p1Answer3ImageContainer };
        else
            return new Transform[] { p2Answer1ImageContainer, p2Answer2ImageContainer, p2Answer3ImageContainer };
    }

    private Button[] GetAnswerButtons(int playerIndex)
    {
        if (playerIndex == 0)
            return new Button[] { p1Answer1Button, p1Answer2Button, p1Answer3Button };
        else
            return new Button[] { p2Answer1Button, p2Answer2Button, p2Answer3Button };
    }

    private Image GetAnswerBorder(int playerIndex, int answerIndex)
    {
        if (playerIndex == 0)
        {
            switch (answerIndex)
            {
                case 0: return p1Answer1Border;
                case 1: return p1Answer2Border;
                case 2: return p1Answer3Border;
            }
        }
        else
        {
            switch (answerIndex)
            {
                case 0: return p2Answer1Border;
                case 1: return p2Answer2Border;
                case 2: return p2Answer3Border;
            }
        }
        return null;
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    // ===== Countdown (Team Mode) =====

    public void ShowCountdown(int playerIndex, int seconds)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null)
        {
            txt.text = seconds.ToString();
            txt.gameObject.SetActive(true);
        }
    }

    public void HideCountdown(int playerIndex)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null) txt.gameObject.SetActive(false);
    }
}
