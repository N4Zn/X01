using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reusable tutorial panel — shows a video tutorial with sound before game starts.
/// Attach to a panel GameObject with VideoPlayer, RawImage, AudioSource, and Start button.
/// </summary>
public class TutorialPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Button startButton;
    [SerializeField] private Text titleText;

    /// <summary>Fired when user presses Start Game button.</summary>
    public event Action OnStartGame = delegate { };

    private RenderTexture _renderTexture;

    void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(HandleStartClicked);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Show the tutorial panel with a video clip.
    /// </summary>
    /// <param name="clip">Video clip to play (converted from GIF).</param>
    /// <param name="title">Title text shown above the video.</param>
    public void Show(VideoClip clip, string title = "Hướng dẫn")
    {
        Debug.Log($"NDL: TutorialPanel.Show() called — panelRoot={panelRoot != null}, clip={clip != null}, title={title}");

        if (panelRoot == null)
        {
            Debug.LogWarning("NDL: TutorialPanel.Show() — panelRoot is NULL, aborting.");
            return;
        }

        if (titleText != null)
            titleText.text = title;

        panelRoot.SetActive(true);
        Debug.Log($"NDL: TutorialPanel — panelRoot.activeSelf={panelRoot.activeSelf}");

        if (clip != null)
            StartCoroutine(PlayVideoNextFrame(clip));
    }

    /// <summary>
    /// Show the tutorial panel without a video (placeholder mode).
    /// Useful when video clip is not yet available.
    /// </summary>
    /// <param name="title">Title text shown on the panel.</param>
    public void ShowPlaceholder(string title = "Hướng dẫn")
    {
        if (panelRoot == null) return;

        if (titleText != null)
            titleText.text = title;

        if (videoDisplay != null)
            videoDisplay.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        panelRoot.SetActive(true);
    }

    /// <summary>
    /// Show the tutorial panel with a static image instead of video.
    /// </summary>
    public void ShowImage(Sprite tutorialSprite, string title = "Huong dan")
    {
        if (panelRoot == null) return;

        if (titleText != null) titleText.text = title;

        if (videoDisplay != null && tutorialSprite != null)
        {
            // Convert sprite to texture for RawImage
            videoDisplay.texture = tutorialSprite.texture;
            videoDisplay.color = Color.white;
        }

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public bool IsVisible
    {
        get { return panelRoot != null && panelRoot.activeSelf; }
    }

    private IEnumerator PlayVideoNextFrame(VideoClip clip)
    {
        yield return null; // wait 1 frame for GameObject to fully activate
        SetupVideo(clip);
    }

    private void SetupVideo(VideoClip clip)
    {
        if (videoPlayer == null || videoDisplay == null || clip == null) return;

        // Create RenderTexture matching video dimensions
        _renderTexture = new RenderTexture(
            (int)clip.width,
            (int)clip.height,
            0
        );
        _renderTexture.Create();

        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = _renderTexture;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;

        // Route audio through AudioSource for volume control
        if (audioSource != null)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSource);
            audioSource.volume = GameSettings.Instance.SfxVolume;
        }

        videoDisplay.texture = _renderTexture;
        videoDisplay.color = Color.white;

        videoPlayer.Play();
    }

    private void HandleStartClicked()
    {
        Hide();
        OnStartGame();
    }

    void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStartClicked);

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }
}
