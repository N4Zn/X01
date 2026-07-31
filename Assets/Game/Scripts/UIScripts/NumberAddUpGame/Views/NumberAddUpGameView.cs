using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for NumberAddUp game — same as AddUp but displays numbers as Text instead of item images.
/// Equation: A + B = C with one hidden, 3 answer choices shown as numbers.
/// </summary>
public class NumberAddUpGameView : MonoBehaviour
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
    [SerializeField] private Text p1BoxAText;
    [SerializeField] private Text p1BoxBText;
    [SerializeField] private Text p1BoxCText;
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
    [SerializeField] private Text p1Answer1Text;
    [SerializeField] private Text p1Answer2Text;
    [SerializeField] private Text p1Answer3Text;
    [SerializeField] private Image p1Answer1Border;
    [SerializeField] private Image p1Answer2Border;
    [SerializeField] private Image p1Answer3Border;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;

    [Header("=== Player 2 (Right) ===")]
    [SerializeField] private Text p2NameText;
    [SerializeField] private Text p2ScoreText;
    [SerializeField] private Text p2BoxAText;
    [SerializeField] private Text p2BoxBText;
    [SerializeField] private Text p2BoxCText;
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
    [SerializeField] private Text p2Answer1Text;
    [SerializeField] private Text p2Answer2Text;
    [SerializeField] private Text p2Answer3Text;
    [SerializeField] private Image p2Answer1Border;
    [SerializeField] private Image p2Answer2Border;
    [SerializeField] private Image p2Answer3Border;
    [SerializeField] private GameObject p2CorrectIcon;
    [SerializeField] private GameObject p2WrongIcon;

    [Header("=== Countdown (Team Mode) ===")]
    [SerializeField] private Text p1CountdownText;
    [SerializeField] private Text p2CountdownText;

    private Color correctBorderColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private Color wrongBorderColor = new Color(0.6f, 0.2f, 0.8f, 1f);
    private Color defaultAnswerColor = Color.white;

    public event Action<int, int> OnAnswerSelected = delegate { };
    public event Action OnRetryClicked = delegate { };
    public event Action OnBackClicked = delegate { };
    public event Action OnHomeClicked = delegate { };
    public event Action OnSettingClicked = delegate { };

    public void InitView()
    {
        p1Answer1Button.onClick.RemoveAllListeners();
        p1Answer1Button.onClick.AddListener(() => OnAnswerSelected(0, 0));
        p1Answer2Button.onClick.RemoveAllListeners();
        p1Answer2Button.onClick.AddListener(() => OnAnswerSelected(0, 1));
        p1Answer3Button.onClick.RemoveAllListeners();
        p1Answer3Button.onClick.AddListener(() => OnAnswerSelected(0, 2));

        p2Answer1Button.onClick.RemoveAllListeners();
        p2Answer1Button.onClick.AddListener(() => OnAnswerSelected(1, 0));
        p2Answer2Button.onClick.RemoveAllListeners();
        p2Answer2Button.onClick.AddListener(() => OnAnswerSelected(1, 1));
        p2Answer3Button.onClick.RemoveAllListeners();
        p2Answer3Button.onClick.AddListener(() => OnAnswerSelected(1, 2));

        if (retryButton != null) { retryButton.onClick.RemoveAllListeners(); retryButton.onClick.AddListener(() => OnRetryClicked()); }
        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => OnBackClicked()); }
        if (homeButton != null) { homeButton.onClick.RemoveAllListeners(); homeButton.onClick.AddListener(() => OnHomeClicked()); }
        if (settingButton != null) { settingButton.onClick.RemoveAllListeners(); settingButton.onClick.AddListener(() => OnSettingClicked()); }

        SetQuestionText("Dien so con thieu!");
        SetPlayerNames("Player 1", "Player 2");
        UpdateScores(0, 0);
        HideGameOverPanel();
        HideFeedbackIcons();
        //NamNN change with Android Studio Agent
        HideQuestion(0);
        HideQuestion(1);
    }

    public void SetQuestionText(string text)
    {
        if (questionText != null) questionText.text = text;
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
        if (timerText != null) timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
    }

    public void UpdateScores(int p1Score, int p2Score)
    {
        if (p1ScoreText != null) p1ScoreText.text = p1Score.ToString();
        if (p2ScoreText != null) p2ScoreText.text = p2Score.ToString();
        if (p1ScoreBarFill != null) p1ScoreBarFill.fillAmount = Mathf.Clamp01((float)p1Score / maxScore);
        if (p2ScoreBarFill != null) p2ScoreBarFill.fillAmount = Mathf.Clamp01((float)p2Score / maxScore);
    }

    public void UpdateStars(int playerIndex, int stars)
    {
        Text t = playerIndex == 0 ? p1ScoreText : p2ScoreText;
        if (t != null) t.text = stars.ToString();
        Image fill = playerIndex == 0 ? p1ScoreBarFill : p2ScoreBarFill;
        if (fill != null) fill.fillAmount = Mathf.Clamp01((float)stars / maxScore);
    }

    /// <summary>
    /// Display equation as numbers: e.g. "3 + ? = 7" or "? + 4 = 9"
    /// </summary>
    public void DisplayEquation(int playerIndex, int numberA, int numberB, string hiddenPosition)
    {
        //NamNN change with Android Studio Agent
        if (playerIndex == 0)
        {
            if (p1BoxAText != null) p1BoxAText.transform.parent.gameObject.SetActive(true);
            if (p1BoxBText != null) p1BoxBText.transform.parent.gameObject.SetActive(true);
            if (p1BoxCText != null) p1BoxCText.transform.parent.gameObject.SetActive(true);
            if (p1PlusLabel != null) p1PlusLabel.gameObject.SetActive(true);
            if (p1EqualsLabel != null) p1EqualsLabel.gameObject.SetActive(true);
        }
        else
        {
            if (p2BoxAText != null) p2BoxAText.transform.parent.gameObject.SetActive(true);
            if (p2BoxBText != null) p2BoxBText.transform.parent.gameObject.SetActive(true);
            if (p2BoxCText != null) p2BoxCText.transform.parent.gameObject.SetActive(true);
            if (p2PlusLabel != null) p2PlusLabel.gameObject.SetActive(true);
            if (p2EqualsLabel != null) p2EqualsLabel.gameObject.SetActive(true);
        }

        int sum = numberA + numberB;
        Text boxA = playerIndex == 0 ? p1BoxAText : p2BoxAText;
        Text boxB = playerIndex == 0 ? p1BoxBText : p2BoxBText;
        Text boxC = playerIndex == 0 ? p1BoxCText : p2BoxCText;
        GameObject overlay = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;

        // Hide second overlay (only used in Level 4)
        if (overlayB != null) overlayB.SetActive(false);

        SetNumberWithShadow(boxC, sum.ToString());

        if (hiddenPosition == "a")
        {
            SetNumberWithShadow(boxA, "");
            SetNumberWithShadow(boxB, numberB.ToString());
            PositionOverlay(overlay, boxA);
        }
        else
        {
            SetNumberWithShadow(boxA, numberA.ToString());
            SetNumberWithShadow(boxB, "");
            PositionOverlay(overlay, boxB);
        }
    }

    /// <summary>
    /// Level 4: display "? + ? = target" — both operands hidden.
    /// </summary>
    public void DisplayEquationLevel4(int playerIndex, int target)
    {
        Text boxA = playerIndex == 0 ? p1BoxAText : p2BoxAText;
        Text boxB = playerIndex == 0 ? p1BoxBText : p2BoxBText;
        Text boxC = playerIndex == 0 ? p1BoxCText : p2BoxCText;
        GameObject overlay = playerIndex == 0 ? p1HiddenOverlay : p2HiddenOverlay;
        GameObject overlayB = playerIndex == 0 ? p1HiddenOverlayB : p2HiddenOverlayB;

        SetNumberWithShadow(boxA, "");
        SetNumberWithShadow(boxB, "");
        SetNumberWithShadow(boxC, target.ToString());

        PositionOverlay(overlay, boxA);
        if (overlayB != null) PositionOverlay(overlayB, boxB);
    }

    private void SetNumberWithShadow(Text numberText, string value)
    {
        if (numberText == null) return;
        numberText.text = value;
        // Update shadow sibling if exists
        Transform parent = numberText.transform.parent;
        if (parent != null)
        {
            Transform shadow = parent.Find("Shadow");
            if (shadow != null)
            {
                Text shadowText = shadow.GetComponent<Text>();
                if (shadowText != null) shadowText.text = value;
            }
        }
    }

    private void PositionOverlay(GameObject overlay, Text targetBox)
    {
        if (overlay == null || targetBox == null) return;
        overlay.SetActive(true);

        // targetBox = NumberText (child of BoxA/BoxB)
        // targetBox.parent = BoxA/BoxB
        // targetBox.parent.parent = Player panel
        Transform box = targetBox.transform.parent;  // BoxA or BoxB
        Transform panel = box.parent;                 // Player panel

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
    /// Display 3 answer choices as numbers
    /// </summary>
    public void DisplayAnswers(int playerIndex, int answer1, int answer2, int answer3)
    {
        //NamNN change with Android Studio Agent
        foreach (var b in GetAnswerButtons(playerIndex)) if (b != null) b.gameObject.SetActive(true);

        Text[] texts = GetAnswerTexts(playerIndex);
        int[] answers = { answer1, answer2, answer3 };
        for (int i = 0; i < 3; i++)
        {
            SetNumberWithShadow(texts[i], answers[i].ToString());
        }
        ResetAnswerBorders(playerIndex);
    }

    public void SetCorrectAnswerBorder(int playerIndex, int answerIndex)
    {
        Image border = GetAnswerBorder(playerIndex, answerIndex);
        if (border != null) border.color = correctBorderColor;
    }

    public void SetWrongAnswerBorder(int playerIndex, int answerIndex)
    {
        Image border = GetAnswerBorder(playerIndex, answerIndex);
        if (border != null) border.color = wrongBorderColor;
    }

    public void ResetAnswerBorders(int playerIndex)
    {
        for (int i = 0; i < 3; i++)
        {
            Image border = GetAnswerBorder(playerIndex, i);
            if (border != null) border.color = defaultAnswerColor;
        }
    }

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
                if (p1Score > p2Score) gameOverResultText.text = "Player 1 Thang!\n" + p1Score + " - " + p2Score;
                else if (p2Score > p1Score) gameOverResultText.text = "Player 2 Thang!\n" + p1Score + " - " + p2Score;
                else gameOverResultText.text = "Hoa!\n" + p1Score + " - " + p2Score;
            }
        }
    }

    public void HideGameOverPanel()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public void SetPlayerAnswersInteractable(int playerIndex, bool interactable)
    {
        Button[] buttons = GetAnswerButtons(playerIndex);
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].interactable = interactable;
    }

    public void SetAnswerButtonInteractable(int playerIndex, int answerIndex, bool interactable)
    {
        Button[] buttons = GetAnswerButtons(playerIndex);
        if (answerIndex < 0 || answerIndex >= buttons.Length) return;
        if (buttons[answerIndex] != null) buttons[answerIndex].interactable = interactable;
    }

    //NamNN change with Android Studio Agent
    public void HideQuestion(int playerIndex)
    {
        // Hide Main equation boxes
        if (playerIndex == 0)
        {
            if (p1BoxAText != null) p1BoxAText.transform.parent.gameObject.SetActive(false);
            if (p1BoxBText != null) p1BoxBText.transform.parent.gameObject.SetActive(false);
            if (p1BoxCText != null) p1BoxCText.transform.parent.gameObject.SetActive(false);
            if (p1PlusLabel != null) p1PlusLabel.gameObject.SetActive(false);
            if (p1EqualsLabel != null) p1EqualsLabel.gameObject.SetActive(false);
            if (p1HiddenOverlay != null) p1HiddenOverlay.SetActive(false);
            if (p1HiddenOverlayB != null) p1HiddenOverlayB.SetActive(false);
            foreach (var b in GetAnswerButtons(0)) if (b != null) b.gameObject.SetActive(false);
        }
        else
        {
            if (p2BoxAText != null) p2BoxAText.transform.parent.gameObject.SetActive(false);
            if (p2BoxBText != null) p2BoxBText.transform.parent.gameObject.SetActive(false);
            if (p2BoxCText != null) p2BoxCText.transform.parent.gameObject.SetActive(false);
            if (p2PlusLabel != null) p2PlusLabel.gameObject.SetActive(false);
            if (p2EqualsLabel != null) p2EqualsLabel.gameObject.SetActive(false);
            if (p2HiddenOverlay != null) p2HiddenOverlay.SetActive(false);
            if (p2HiddenOverlayB != null) p2HiddenOverlayB.SetActive(false);
            foreach (var b in GetAnswerButtons(1)) if (b != null) b.gameObject.SetActive(false);
        }
    }

    private Text[] GetAnswerTexts(int playerIndex)
    {
        if (playerIndex == 0) return new Text[] { p1Answer1Text, p1Answer2Text, p1Answer3Text };
        return new Text[] { p2Answer1Text, p2Answer2Text, p2Answer3Text };
    }

    private Button[] GetAnswerButtons(int playerIndex)
    {
        if (playerIndex == 0) return new Button[] { p1Answer1Button, p1Answer2Button, p1Answer3Button };
        return new Button[] { p2Answer1Button, p2Answer2Button, p2Answer3Button };
    }

    private Image GetAnswerBorder(int playerIndex, int answerIndex)
    {
        if (playerIndex == 0)
        {
            switch (answerIndex) { case 0: return p1Answer1Border; case 1: return p1Answer2Border; case 2: return p1Answer3Border; }
        }
        else
        {
            switch (answerIndex) { case 0: return p2Answer1Border; case 1: return p2Answer2Border; case 2: return p2Answer3Border; }
        }
        return null;
    }

    // ===== Countdown (Team Mode) =====

    //NamNN change with Android Studio Agent
    public void ShowCountdown(int playerIndex, int seconds)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null)
        {
            txt.resizeTextForBestFit = true;
            txt.text = "Next question in " + seconds + "s";
            txt.gameObject.SetActive(true);
        }
    }

    public void HideCountdown(int playerIndex)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null) txt.gameObject.SetActive(false);
    }
}
