using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for the AddNumber game - handles all UI display and user interaction.
/// New layout: equation boxes (A + B = C) with hidden operand + 4 answer buttons.
/// Split screen for 2 players, resolution 1024x600.
/// </summary>
public class AddNumberGameView : MonoBehaviour
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
    [SerializeField] private GameObject p1HiddenOverlayC;
    [SerializeField] private Button p1Answer1Button;
    [SerializeField] private Button p1Answer2Button;
    [SerializeField] private Button p1Answer3Button;
    [SerializeField] private Button p1Answer4Button;
    [SerializeField] private Transform p1Answer1ImageContainer;
    [SerializeField] private Transform p1Answer2ImageContainer;
    [SerializeField] private Transform p1Answer3ImageContainer;
    [SerializeField] private Transform p1Answer4ImageContainer;
    [SerializeField] private Image p1Answer1Border;
    [SerializeField] private Image p1Answer2Border;
    [SerializeField] private Image p1Answer3Border;
    [SerializeField] private Image p1Answer4Border;
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
    [SerializeField] private GameObject p2HiddenOverlayC;
    [SerializeField] private Button p2Answer1Button;
    [SerializeField] private Button p2Answer2Button;
    [SerializeField] private Button p2Answer3Button;
    [SerializeField] private Button p2Answer4Button;
    [SerializeField] private Transform p2Answer1ImageContainer;
    [SerializeField] private Transform p2Answer2ImageContainer;
    [SerializeField] private Transform p2Answer3ImageContainer;
    [SerializeField] private Transform p2Answer4ImageContainer;
    [SerializeField] private Image p2Answer1Border;
    [SerializeField] private Image p2Answer2Border;
    [SerializeField] private Image p2Answer3Border;
    [SerializeField] private Image p2Answer4Border;
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
        p1Answer1Button.onClick.RemoveAllListeners();
        p1Answer1Button.onClick.AddListener(() => OnAnswerSelected(0, 0));
        p1Answer2Button.onClick.RemoveAllListeners();
        p1Answer2Button.onClick.AddListener(() => OnAnswerSelected(0, 1));
        p1Answer3Button.onClick.RemoveAllListeners();
        p1Answer3Button.onClick.AddListener(() => OnAnswerSelected(0, 2));
        if (p1Answer4Button != null)
        {
            p1Answer4Button.onClick.RemoveAllListeners();
            p1Answer4Button.onClick.AddListener(() => OnAnswerSelected(0, 3));
        }

        // Setup answer button listeners for Player 2
        p2Answer1Button.onClick.RemoveAllListeners();
        p2Answer1Button.onClick.AddListener(() => OnAnswerSelected(1, 0));
        p2Answer2Button.onClick.RemoveAllListeners();
        p2Answer2Button.onClick.AddListener(() => OnAnswerSelected(1, 1));
        p2Answer3Button.onClick.RemoveAllListeners();
        p2Answer3Button.onClick.AddListener(() => OnAnswerSelected(1, 2));
        if (p2Answer4Button != null)
        {
            p2Answer4Button.onClick.RemoveAllListeners();
            p2Answer4Button.onClick.AddListener(() => OnAnswerSelected(1, 3));
        }

        // Retry and Back buttons
        if (retryButton != null) { retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => OnRetryClicked()); }
        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => OnBackClicked()); }
        if (homeButton != null) { homeButton.onClick.RemoveAllListeners(); homeButton.onClick.AddListener(() => OnHomeClicked()); }
        if (settingButton != null) { settingButton.onClick.RemoveAllListeners(); settingButton.onClick.AddListener(() => OnSettingClicked()); }

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

    // NamNN change with Android Studio Agent
    public void SetMaxScore(int value)
    {
        this.maxScore = value;
        UpdateScores(0, 0);
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
    /// Display the equation boxes based on hidden position string ("a", "b", "c", "ab", etc.)
    /// </summary>
    public void DisplayEquation(int playerIndex, int numberA, int numberB, int numberC, string hiddenPosition, string imageType)
    {
        Transform boxA = playerIndex == 0 ? p1BoxAImageContainer : p2BoxAImageContainer;
        Transform boxB = playerIndex == 0 ? p1BoxBImageContainer : p2BoxBImageContainer;
        Transform boxC = playerIndex == 0 ? p1BoxCImageContainer : p2BoxCImageContainer;

        if (boxA != null) boxA.parent.gameObject.SetActive(true);
        if (boxB != null) boxB.parent.gameObject.SetActive(true);
        if (boxC != null) boxC.parent.gameObject.SetActive(true);

        Text plus = playerIndex == 0 ? p1PlusLabel : p2PlusLabel;
        Text equals = playerIndex == 0 ? p1EqualsLabel : p2EqualsLabel;
        if (plus != null) plus.gameObject.SetActive(true);
        if (equals != null) equals.gameObject.SetActive(true);

        GameObject overlayA = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;
        GameObject overlayC = playerIndex == 0 ? p1HiddenOverlayC : p2HiddenOverlayC;

        if (overlayA != null) overlayA.SetActive(false);
        if (overlayB != null) overlayB.SetActive(false);
        if (overlayC != null) overlayC.SetActive(false);

        // Display A
        if (hiddenPosition.Contains("a"))
        {
            ClearContainer(boxA);
            PositionOverlayOnBox(overlayA, boxA);
        }
        else
        {
            DisplayItemsInContainer(boxA, numberA, imageType);
        }

        // Display B
        if (hiddenPosition.Contains("b"))
        {
            ClearContainer(boxB);
            PositionOverlayOnBox(overlayB, boxB);
        }
        else
        {
            DisplayItemsInContainer(boxB, numberB, imageType);
        }

        // Display C
        if (hiddenPosition.Contains("c"))
        {
            ClearContainer(boxC);
            PositionOverlayOnBox(overlayC, boxC);
        }
        else
        {
            DisplayItemsInContainer(boxC, numberC, imageType);
        }
    }

    /// <summary>
    /// Level 4: display "? + ? = target" — both operands hidden, show target in BoxC.
    /// </summary>
    public void DisplayEquationLevel4(int playerIndex, int target, string imageType)
    {
        // This is deprecated by the new generic DisplayEquation
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
    public void DisplayAnswers(int playerIndex, int[] answers, string imageType)
    {
        Transform[] containers = GetAnswerImageContainers(playerIndex);
        Button[] buttons = GetAnswerButtons(playerIndex);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < answers.Length)
            {
                if (buttons[i] != null) buttons[i].gameObject.SetActive(true);
                DisplayItemsInContainer(containers[i], answers[i], imageType);
            }
            else
            {
                if (buttons[i] != null) buttons[i].gameObject.SetActive(false);
            }
        }

        // Reset answer border colors
        ResetAnswerBorders(playerIndex);
    }

    /// <summary>
    /// Highlight the selected answer as selected (blue/cyan)
    /// </summary>
    public void SetSelectedAnswerBorder(int playerIndex, int answerIndex, bool isSelected)
    {
        Image border = GetAnswerBorder(playerIndex, answerIndex);
        if (border != null)
        {
            border.color = isSelected ? Color.cyan : Color.white;
        }
    }

    public void UpdateHiddenValues(int playerIndex, List<int> selectedIndices, int[] answers, string hiddenPosition, string imageType)
    {
        Transform boxA = (playerIndex == 0) ? p1BoxAImageContainer : p2BoxAImageContainer;
        Transform boxB = (playerIndex == 0) ? p1BoxBImageContainer : p2BoxBImageContainer;
        Transform boxSum = (playerIndex == 0) ? p1BoxCImageContainer : p2BoxCImageContainer;

        GameObject overlayA = (playerIndex == 0) ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = (playerIndex == 0) ? p1HiddenOverlayB : p2HiddenOverlayB;
        GameObject overlaySum = (playerIndex == 0) ? p1HiddenOverlayC : p2HiddenOverlayC;

        // Reset: Ensure all originally hidden slots are covered and containers cleared
        if (hiddenPosition.Contains("a")) { overlayA.SetActive(true); ClearContainer(boxA); }
        if (hiddenPosition.Contains("b")) { overlayB.SetActive(true); ClearContainer(boxB); }
        if (hiddenPosition.Contains("c")) { overlaySum.SetActive(true); ClearContainer(boxSum); }

        // Find which slots were supposed to be hidden in order: A, B, C
        List<string> hiddenSlots = new List<string>();
        if (hiddenPosition.Contains("a")) hiddenSlots.Add("a");
        if (hiddenPosition.Contains("b")) hiddenSlots.Add("b");
        if (hiddenPosition.Contains("c")) hiddenSlots.Add("c");

        // Fill selected values into the slots that are supposed to be hidden, in order
        for (int i = 0; i < selectedIndices.Count && i < hiddenSlots.Count; i++)
        {
            int val = answers[selectedIndices[i]];
            string slot = hiddenSlots[i];

            if (slot == "a") { overlayA.SetActive(false); DisplayItemsInContainer(boxA, val, imageType); }
            else if (slot == "b") { overlayB.SetActive(false); DisplayItemsInContainer(boxB, val, imageType); }
            else if (slot == "c") { overlaySum.SetActive(false); DisplayItemsInContainer(boxSum, val, imageType); }
        }
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
        // 1. Load sprite from the new Animal folder
        Sprite sprite = Resources.Load<Sprite>("GameImages/Animal/" + imageType);

        // 2. Configure GridLayoutGroup for 2 columns and dynamic sizing to prevent overflow
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            RectTransform rt = container.GetComponent<RectTransform>();
            float spacingX = grid.spacing.x;
            float spacingY = grid.spacing.y;
            float paddingX = grid.padding.left + grid.padding.right;
            float paddingY = grid.padding.top + grid.padding.bottom;

            // Calculate max width for 2 columns
            float cellW = (rt.rect.width - paddingX - spacingX) / 2f;

            // Calculate max height based on number of rows needed
            int rows = Mathf.CeilToInt(count / 2f);
            if (rows < 1) rows = 1;
            float cellH = (rt.rect.height - paddingY - (rows - 1) * spacingY) / rows;

            // Use the smaller dimension to ensure it fits both ways and stays square
            float size = Mathf.Min(cellW, cellH);
            grid.cellSize = new Vector2(size, size);

            // Center the grid content
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject imgObj = Instantiate(imagePrefab, container);
            imgObj.SetActive(true);

            // 3. Remove backgrounds (circles) and masks to show only the animal icon
            Transform iconT = imgObj.transform.Find("IconImage");
            Image targetImg = null;

            if (iconT != null)
            {
                targetImg = iconT.GetComponent<Image>();

                // Disable all other images in the prefab (like the Circle background)
                Image[] allImages = imgObj.GetComponentsInChildren<Image>(true);
                foreach (var im in allImages)
                {
                    if (im != targetImg) im.enabled = false;
                }

                // Disable masks that might be clipping the animal
                Mask[] masks = imgObj.GetComponentsInChildren<Mask>(true);
                foreach (var m in masks) m.enabled = false;
            }
            else
            {
                // Fallback for simple prefabs
                targetImg = imgObj.GetComponent<Image>();
            }

            if (targetImg != null && sprite != null)
            {
                targetImg.enabled = true;
                targetImg.sprite = sprite;
                targetImg.preserveAspect = true; // Ensure animal doesn't stretch

                // Make the image fill its grid cell area
                RectTransform targetRT = targetImg.GetComponent<RectTransform>();
                targetRT.anchorMin = Vector2.zero;
                targetRT.anchorMax = Vector2.one;
                targetRT.offsetMin = Vector2.zero;
                targetRT.offsetMax = Vector2.zero;
            }
        }
    }

    private Transform[] GetAnswerImageContainers(int playerIndex)
    {
        if (playerIndex == 0)
            return new Transform[] { p1Answer1ImageContainer, p1Answer2ImageContainer, p1Answer3ImageContainer, p1Answer4ImageContainer };
        else
            return new Transform[] { p2Answer1ImageContainer, p2Answer2ImageContainer, p2Answer3ImageContainer, p2Answer4ImageContainer };
    }

    private Button[] GetAnswerButtons(int playerIndex)
    {
        if (playerIndex == 0)
            return new Button[] { p1Answer1Button, p1Answer2Button, p1Answer3Button, p1Answer4Button };
        else
            return new Button[] { p2Answer1Button, p2Answer2Button, p2Answer3Button, p2Answer4Button };
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
                case 3: return p1Answer4Border;
            }
        }
        else
        {
            switch (answerIndex)
            {
                case 0: return p2Answer1Border;
                case 1: return p2Answer2Border;
                case 2: return p2Answer3Border;
                case 3: return p2Answer4Border;
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

    //NamNN change with Android Studio Agent
    public void HideQuestion(int playerIndex)
    {
        Transform boxA = playerIndex == 0 ? p1BoxAImageContainer : p2BoxAImageContainer;
        Transform boxB = playerIndex == 0 ? p1BoxBImageContainer : p2BoxBImageContainer;
        Transform boxC = playerIndex == 0 ? p1BoxCImageContainer : p2BoxCImageContainer;
        GameObject overlay = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;
        Text plus = playerIndex == 0 ? p1PlusLabel : p2PlusLabel;
        Text equals = playerIndex == 0 ? p1EqualsLabel : p2EqualsLabel;

        if (boxA != null) boxA.parent.gameObject.SetActive(false);
        if (boxB != null) boxB.parent.gameObject.SetActive(false);
        if (boxC != null) boxC.parent.gameObject.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        if (overlayB != null) overlayB.SetActive(false);
        if (plus != null) plus.gameObject.SetActive(false);
        if (equals != null) equals.gameObject.SetActive(false);

        Button[] buttons = GetAnswerButtons(playerIndex);
        foreach (var btn in buttons) if (btn != null) btn.gameObject.SetActive(false);
    }

    // ===== Countdown (Team Mode) =====

    //NamNN change with Android Studio Agent
    public void ShowCountdown(int playerIndex, int seconds)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null)
        {
            txt.resizeTextForBestFit = true;
            txt.text = "Next in " + seconds + "s";
            txt.gameObject.SetActive(true);
        }
    }

    public void HideCountdown(int playerIndex)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null) txt.gameObject.SetActive(false);
    }
}
