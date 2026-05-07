using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for ChuCai game — split screen, each player needs to find a specific letter.
/// </summary>
public class ChuCaiView : MonoBehaviour
{
    [Header("=== Common UI ===")]
    [SerializeField] private Text timerText;
    [SerializeField] private Text questionText; // Overall instruction
    [SerializeField] private Button backButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverResultText;
    [SerializeField] private Button retryButton;

    [Header("=== Team Bar Names ===")]
    [SerializeField] private Text p1BarNameText;
    [SerializeField] private Text p2BarNameText;

    [Header("=== Score ===")]
    [SerializeField] private Image p1ScoreBarFill;
    [SerializeField] private Image p2ScoreBarFill;
    [SerializeField] private Text p1ScoreLabel;
    [SerializeField] private Text p2ScoreLabel;
    [SerializeField] private int maxScore = 10;

    [Header("=== Player 1 Boxes (Left) ===")]
    [SerializeField] private Button[] p1Boxes;
    [SerializeField] private Text[] p1Texts;
    [SerializeField] private Image[] p1Images;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;

    [Header("=== Player 2 Boxes (Right) ===")]
    [SerializeField] private Button[] p2Boxes;
    [SerializeField] private Text[] p2Texts;
    [SerializeField] private Image[] p2Images;
    [SerializeField] private GameObject p2CorrectIcon;
    [SerializeField] private GameObject p2WrongIcon;

    [Header("=== Planet Sprites ===")]
    [SerializeField] private Sprite[] planetSprites;

    [Header("=== Countdown (Team Mode) ===")]
    [SerializeField] private Text p1CountdownText;
    [SerializeField] private Text p2CountdownText;

    private Color _normalTextColor = new Color(1f, 0.95f, 0.3f, 1f);
    private Color _correctTextColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    private Color _wrongTextColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    public event Action<int, int> OnBoxTapped = delegate { };
    public event Action OnBackClicked = delegate { };
    public event Action OnHomeClicked = delegate { };
    public event Action OnSettingClicked = delegate { };
    public event Action OnRetryClicked = delegate { };

    public void InitView()
    {
        SetupButtonListeners(p1Boxes, 0);
        SetupButtonListeners(p2Boxes, 1);

        if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked());
        if (homeButton != null) homeButton.onClick.AddListener(() => OnHomeClicked());
        if (settingButton != null) settingButton.onClick.AddListener(() => OnSettingClicked());
        if (retryButton != null) retryButton.onClick.AddListener(() => OnRetryClicked());

        HideAllBoxes();
        HideFeedback();
        HideGameOver();
    }

    private void SetupButtonListeners(Button[] boxes, int playerIdx)
    {
        if (boxes == null) return;
        for (int i = 0; i < boxes.Length; i++)
        {
            int idx = i;
            if (boxes[i] != null)
            {
                boxes[i].onClick.RemoveAllListeners();
                boxes[i].onClick.AddListener(() => OnBoxTapped(playerIdx, idx));
            }
        }
    }

    public void SetPlayerNames(string p1, string p2)
    {
        if (p1BarNameText != null) p1BarNameText.text = p1;
        if (p2BarNameText != null) p2BarNameText.text = p2;
    }

    public void UpdateTimer(float time)
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(time).ToString();
    }

    public void UpdateScores(int p1Score, int p2Score)
    {
        if (p1ScoreLabel != null) p1ScoreLabel.text = p1Score.ToString();
        if (p2ScoreLabel != null) p2ScoreLabel.text = p2Score.ToString();
        float m = maxScore > 0 ? maxScore : 10f;
        if (p1ScoreBarFill != null) p1ScoreBarFill.fillAmount = Mathf.Clamp01((float)p1Score / m);
        if (p2ScoreBarFill != null) p2ScoreBarFill.fillAmount = Mathf.Clamp01((float)p2Score / m);
    }

    public void SetQuestionText(string text)
    {
        if (questionText != null) questionText.text = text;
    }

    public void SetMaxScore(int value)
    {
        this.maxScore = value;
        UpdateScores(0, 0);
    }

    public void ShowBoxes(int playerIndex, string[] values)
    {
        Button[] btns = GetButtons(playerIndex);
        Text[] txts = GetTexts(playerIndex);
        Image[] imgs = GetImages(playerIndex);
        int count = Mathf.Min(values.Length, btns.Length);

        for (int i = 0; i < btns.Length; i++)
        {
            if (btns[i] != null)
            {
                btns[i].gameObject.SetActive(i < count);
                btns[i].transform.localScale = Vector3.one;
                btns[i].interactable = true;

                // Reset effects
                ShrinkAndDisappearEffect oldEff = btns[i].gameObject.GetComponent<ShrinkAndDisappearEffect>();
                if (oldEff != null) oldEff.StopAllCoroutines();
            }
        }

        float halfLeft = (playerIndex == 0) ? 0.04f : 0.54f;
        float halfRight = (playerIndex == 0) ? 0.46f : 0.96f;
        float yMin = 0.10f;
        float yMax = 0.85f;

        float sizeScale = (count <= 3) ? 1f : (count == 4) ? 0.85f : 0.75f;
        float baseBoxW = 0.18f * sizeScale;
        float baseBoxH = 0.31f * sizeScale;

        float[] boxWs = new float[count];
        float[] boxHs = new float[count];
        for (int i = 0; i < count; i++)
        {
            float s = UnityEngine.Random.Range(0.9f, 1.1f);
            boxWs[i] = baseBoxW * s;
            boxHs[i] = baseBoxH * s;
        }

        Vector2[] positions = GenerateRandomPositions(count, halfLeft, halfRight, yMin, yMax, boxWs, boxHs);

        for (int i = 0; i < count; i++)
        {
            if (btns[i] != null)
            {
                RectTransform rt = btns[i].GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(positions[i].x, positions[i].y);
                rt.anchorMax = new Vector2(positions[i].x + boxWs[i], positions[i].y + boxHs[i]);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                FloatingEffect fe = btns[i].gameObject.GetComponent<FloatingEffect>();
                if (fe == null) fe = btns[i].gameObject.AddComponent<FloatingEffect>();
                fe.amplitude = UnityEngine.Random.Range(5f, 12f);
                fe.speed = UnityEngine.Random.Range(1f, 2f);
                fe.ResetStartPos();
            }
            if (imgs[i] != null && planetSprites != null && planetSprites.Length > 0)
            {
                imgs[i].sprite = planetSprites[UnityEngine.Random.Range(0, planetSprites.Length)];
                imgs[i].preserveAspect = true;
                imgs[i].raycastTarget = true;

                RotateEffect rotate = imgs[i].gameObject.GetComponent<RotateEffect>();
                if (rotate == null) rotate = imgs[i].gameObject.AddComponent<RotateEffect>();
                rotate.rotationSpeed = UnityEngine.Random.Range(10f, 25f);
            }
            if (txts[i] != null)
            {
                txts[i].text = values[i];
                txts[i].color = _normalTextColor;
                if (txts[i].gameObject.GetComponent<KeepUpright>() == null)
                    txts[i].gameObject.AddComponent<KeepUpright>();
            }
        }
    }

    private Vector2[] GenerateRandomPositions(int count, float xMin, float xMax, float yMin, float yMax, float[] boxWs, float[] boxHs)
    {
        Vector2[] result = new Vector2[count];
        float areaW = xMax - xMin;
        float areaH = yMax - yMin;

        float[][,] layouts3 = new float[][,]
        {
            new float[,] { {0.15f, 0.75f}, {0.50f, 0.25f}, {0.85f, 0.70f} },
            new float[,] { {0.20f, 0.25f}, {0.80f, 0.25f}, {0.50f, 0.75f} }
        };
        float[][,] layouts4 = new float[][,]
        {
            new float[,] { {0.20f, 0.25f}, {0.80f, 0.25f}, {0.20f, 0.75f}, {0.80f, 0.75f} },
            new float[,] { {0.50f, 0.15f}, {0.15f, 0.50f}, {0.85f, 0.50f}, {0.50f, 0.85f} }
        };
        float[][,] layouts5 = new float[][,]
        {
            new float[,] { {0.20f, 0.20f}, {0.80f, 0.20f}, {0.50f, 0.50f}, {0.20f, 0.80f}, {0.80f, 0.80f} },
            new float[,] { {0.50f, 0.15f}, {0.15f, 0.42f}, {0.85f, 0.42f}, {0.25f, 0.80f}, {0.75f, 0.80f} }
        };

        float[][,] pool = (count <= 3) ? layouts3 : (count == 4) ? layouts4 : layouts5;
        float[,] layout = pool[UnityEngine.Random.Range(0, pool.Length)];

        for (int i = 0; i < count; i++)
        {
            float cx = xMin + layout[i, 0] * areaW;
            float cy = yMin + layout[i, 1] * areaH;
            float x = Mathf.Clamp(cx - boxWs[i] * 0.5f, xMin, xMax - boxWs[i]);
            float y = Mathf.Clamp(cy - boxHs[i] * 0.5f, yMin, yMax - boxHs[i]);
            result[i] = new Vector2(x, y);
        }
        return result;
    }

    public void SetBoxCorrect(int playerIndex, int boxIndex)
    {
        Text[] txts = GetTexts(playerIndex);
        Button[] btns = GetButtons(playerIndex);
        if (boxIndex < txts.Length && txts[boxIndex] != null) txts[boxIndex].color = _correctTextColor;
        if (boxIndex < btns.Length && btns[boxIndex] != null)
        {
            btns[boxIndex].interactable = false;
            ShrinkAndDisappearEffect effect = btns[boxIndex].gameObject.GetComponent<ShrinkAndDisappearEffect>();
            if (effect == null) effect = btns[boxIndex].gameObject.AddComponent<ShrinkAndDisappearEffect>();
            effect.Play(0.8f);
        }
    }

    public void SetBoxWrong(int playerIndex, int boxIndex)
    {
        Text[] txts = GetTexts(playerIndex);
        if (boxIndex < txts.Length && txts[boxIndex] != null) txts[boxIndex].color = _wrongTextColor;
    }

    public void SetPlayerInteractable(int playerIndex, bool interactable)
    {
        Button[] btns = GetButtons(playerIndex);
        foreach (var b in btns) if (b != null) b.interactable = interactable;
    }

    public void ShowFeedback(int playerIndex, bool correct)
    {
        GameObject icon = correct ? (playerIndex == 0 ? p1CorrectIcon : p2CorrectIcon) : (playerIndex == 0 ? p1WrongIcon : p2WrongIcon);
        if (icon != null) { icon.SetActive(true); icon.GetComponent<FeedbackEffect>()?.Play(correct); }
    }

    public void HideFeedback(int playerIndex)
    {
        if (playerIndex == 0) { p1CorrectIcon?.SetActive(false); p1WrongIcon?.SetActive(false); }
        else { p2CorrectIcon?.SetActive(false); p2WrongIcon?.SetActive(false); }
    }

    public void HideFeedback() { p1CorrectIcon?.SetActive(false); p1WrongIcon?.SetActive(false); p2CorrectIcon?.SetActive(false); p2WrongIcon?.SetActive(false); }

    public void HideAllBoxes() { foreach (var b in p1Boxes) if (b != null) b.gameObject.SetActive(false); foreach (var b in p2Boxes) if (b != null) b.gameObject.SetActive(false); }

    public void HideGameOver() { if (gameOverPanel != null) gameOverPanel.SetActive(false); }

    public void ShowCountdown(int playerIndex, int seconds)
    {
        Text txt = (playerIndex == 0) ? p1CountdownText : p2CountdownText;
        if (txt != null) { txt.text = "Next in " + seconds + "s"; txt.gameObject.SetActive(true); }
    }

    public void HideCountdown(int playerIndex) { if (playerIndex == 0) p1CountdownText?.gameObject.SetActive(false); else p2CountdownText?.gameObject.SetActive(false); }

    private Button[] GetButtons(int p) => p == 0 ? p1Boxes : p2Boxes;
    private Text[] GetTexts(int p) => p == 0 ? p1Texts : p2Texts;
    private Image[] GetImages(int p) => p == 0 ? p1Images : p2Images;
}
