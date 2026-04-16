using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for HomeScene — main menu with Start, Setting button, and Setting panel overlay.
/// </summary>
public class HomeSceneView : MonoBehaviour
{
    [Header("=== Main Screen ===")]
    [SerializeField] private Text gameTitleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text companyNameText;
    [SerializeField] private Text copyrightText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;

    [Header("=== Setting Panel ===")]
    [SerializeField] private GameObject settingPanelRoot;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Button sfxMuteButton;
    [SerializeField] private Text sfxMuteLabel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Button musicMuteButton;
    [SerializeField] private Text musicMuteLabel;
    [SerializeField] private Button time60Button;
    [SerializeField] private Button time90Button;
    [SerializeField] private Button time120Button;
    [SerializeField] private InputField timeCustomInput;
    [SerializeField] private Image time60Highlight;
    [SerializeField] private Image time90Highlight;
    [SerializeField] private Image time120Highlight;
    [SerializeField] private Button feedbackFastButton;
    [SerializeField] private Button feedbackMediumButton;
    [SerializeField] private Button feedbackSlowButton;
    [SerializeField] private Image feedbackFastHighlight;
    [SerializeField] private Image feedbackMediumHighlight;
    [SerializeField] private Image feedbackSlowHighlight;
    [SerializeField] private Button timeout10Button;
    [SerializeField] private Button timeout15Button;
    [SerializeField] private Button timeout20Button;
    [SerializeField] private Image timeout10Highlight;
    [SerializeField] private Image timeout15Highlight;
    [SerializeField] private Image timeout20Highlight;
    [SerializeField] private Button settingCloseButton;

    // Events
    public event Action onClickStart = delegate { };
    public event Action onClickSettings = delegate { };
    public event Action onSettingClose = delegate { };
    public event Action<float> onSfxVolumeChanged = delegate { };
    public event Action<float> onMusicVolumeChanged = delegate { };
    public event Action onSfxMuteToggle = delegate { };
    public event Action onMusicMuteToggle = delegate { };
    public event Action<int> onGameTimeSelected = delegate { };
    public event Action<FeedbackSpeed> onFeedbackSpeedSelected = delegate { };
    public event Action<int> onQuestionTimeoutSelected = delegate { };

    private Color _activeColor = Color.white;
    private Color _inactiveColor = Color.clear;

    public void InitView()
    {
        if (gameTitleText != null) gameTitleText.text = "Tiny Explorers";
        if (subtitleText != null) subtitleText.text = "Amazing Adventures";
        if (companyNameText != null) companyNameText.text = "EduXplore";
        if (copyrightText != null) copyrightText.text = "\u00a9 EduXplore 2026";

        // Main buttons
        if (startButton != null) startButton.onClick.AddListener(() => onClickStart());
        if (settingsButton != null) settingsButton.onClick.AddListener(() => onClickSettings());

        // Setting panel buttons
        if (settingCloseButton != null) settingCloseButton.onClick.AddListener(() => onSettingClose());

        // Sliders
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener((v) => onSfxVolumeChanged(v));
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener((v) => onMusicVolumeChanged(v));

        // Mute buttons
        if (sfxMuteButton != null) sfxMuteButton.onClick.AddListener(() => onSfxMuteToggle());
        if (musicMuteButton != null) musicMuteButton.onClick.AddListener(() => onMusicMuteToggle());

        // Time buttons
        if (time60Button != null) time60Button.onClick.AddListener(() => onGameTimeSelected(60));
        if (time90Button != null) time90Button.onClick.AddListener(() => onGameTimeSelected(90));
        if (time120Button != null) time120Button.onClick.AddListener(() => onGameTimeSelected(120));
        if (timeCustomInput != null)
        {
            timeCustomInput.onEndEdit.AddListener((val) =>
            {
                int t;
                if (int.TryParse(val, out t) && t > 0)
                {
                    onGameTimeSelected(t);
                }
            });
        }

        // Feedback speed buttons
        if (feedbackFastButton != null) feedbackFastButton.onClick.AddListener(() => onFeedbackSpeedSelected(FeedbackSpeed.Fast));
        if (feedbackMediumButton != null) feedbackMediumButton.onClick.AddListener(() => onFeedbackSpeedSelected(FeedbackSpeed.Medium));
        if (feedbackSlowButton != null) feedbackSlowButton.onClick.AddListener(() => onFeedbackSpeedSelected(FeedbackSpeed.Slow));

        // Question timeout buttons
        if (timeout10Button != null) timeout10Button.onClick.AddListener(() => onQuestionTimeoutSelected(10));
        if (timeout15Button != null) timeout15Button.onClick.AddListener(() => onQuestionTimeoutSelected(15));
        if (timeout20Button != null) timeout20Button.onClick.AddListener(() => onQuestionTimeoutSelected(20));

        HideSettingPanel();
    }

    // ===== Setting Panel =====

    public void ShowSettingPanel()
    {
        if (settingPanelRoot != null) settingPanelRoot.SetActive(true);
    }

    public void HideSettingPanel()
    {
        if (settingPanelRoot != null) settingPanelRoot.SetActive(false);
    }

    public void UpdateSettingUI(float sfxVol, float musicVol, int gameTime, FeedbackSpeed speed, int questionTimeout)
    {
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(sfxVol);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(musicVol);

        UpdateSfxMuteLabel(sfxVol <= 0f);
        UpdateMusicMuteLabel(musicVol <= 0f);

        // Time highlights
        SetTimeHighlight(gameTime);

        // Custom input
        if (timeCustomInput != null)
        {
            if (gameTime != 60 && gameTime != 90 && gameTime != 120)
                timeCustomInput.text = gameTime.ToString();
            else
                timeCustomInput.text = "";
        }

        // Feedback speed
        SetFeedbackHighlight(speed);

        // Question timeout
        SetQuestionTimeoutHighlight(questionTimeout);
    }

    public void UpdateSfxMuteLabel(bool isMuted)
    {
        if (sfxMuteLabel != null) sfxMuteLabel.text = isMuted ? "ON" : "OFF";
    }

    public void UpdateMusicMuteLabel(bool isMuted)
    {
        if (musicMuteLabel != null) musicMuteLabel.text = isMuted ? "ON" : "OFF";
    }

    public void SetTimeHighlight(int gameTime)
    {
        if (time60Highlight != null) time60Highlight.color = gameTime == 60 ? _activeColor : _inactiveColor;
        if (time90Highlight != null) time90Highlight.color = gameTime == 90 ? _activeColor : _inactiveColor;
        if (time120Highlight != null) time120Highlight.color = gameTime == 120 ? _activeColor : _inactiveColor;
    }

    public void SetFeedbackHighlight(FeedbackSpeed speed)
    {
        if (feedbackFastHighlight != null)
            feedbackFastHighlight.color = speed == FeedbackSpeed.Fast ? _activeColor : _inactiveColor;
        if (feedbackMediumHighlight != null)
            feedbackMediumHighlight.color = speed == FeedbackSpeed.Medium ? _activeColor : _inactiveColor;
        if (feedbackSlowHighlight != null)
            feedbackSlowHighlight.color = speed == FeedbackSpeed.Slow ? _activeColor : _inactiveColor;
    }

    public void SetQuestionTimeoutHighlight(int seconds)
    {
        if (timeout10Highlight != null) timeout10Highlight.color = seconds == 10 ? _activeColor : _inactiveColor;
        if (timeout15Highlight != null) timeout15Highlight.color = seconds == 15 ? _activeColor : _inactiveColor;
        if (timeout20Highlight != null) timeout20Highlight.color = seconds == 20 ? _activeColor : _inactiveColor;
    }
}
