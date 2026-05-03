using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for PlanetAlphabet game — split screen, each player has boxes with letters.
/// Player taps boxes in alphabetical order.
/// </summary>
public class PlanetAlphabetView : MonoBehaviour
{
    [Header("=== Common UI ===")]
    [SerializeField] private Text timerText;
    [SerializeField] private Text questionText;
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

    [Header("=== Player 1 Boxes (Left, up to 5) ===")]
    [SerializeField] private Button[] p1Boxes;
    [SerializeField] private Text[] p1Texts;
    [SerializeField] private Image[] p1Images;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;

    [Header("=== Player 2 Boxes (Right, up to 5) ===")]
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

    private Color _normalTextColor = new Color(1f, 0.95f, 0.3f, 1f);   // yellow
    private Color _correctTextColor = new Color(0.2f, 0.9f, 0.3f, 1f); // green
    private Color _wrongTextColor = new Color(0.9f, 0.3f, 0.3f, 1f);   // red

    public event Action<int, int> OnBoxTapped = delegate { };
    public event Action OnBackClicked = delegate { };
    public event Action OnHomeClicked = delegate { };
    public event Action OnSettingClicked = delegate { };
    public event Action OnRetryClicked = delegate { };

    public void InitView()
    {
        if (p1Boxes != null)
            for (int i = 0; i < p1Boxes.Length; i++)
            {
                int idx = i;
                if (p1Boxes[i] != null) p1Boxes[i].onClick.AddListener(() => OnBoxTapped(0, idx));
            }
        if (p2Boxes != null)
            for (int i = 0; i < p2Boxes.Length; i++)
            {
                int idx = i;
                if (p2Boxes[i] != null) p2Boxes[i].onClick.AddListener(() => OnBoxTapped(1, idx));
            }

        if (backButton != null) backButton.onClick.AddListener(() => OnBackClicked());
        if (homeButton != null) homeButton.onClick.AddListener(() => OnHomeClicked());
        if (settingButton != null) settingButton.onClick.AddListener(() => OnSettingClicked());
        if (retryButton != null) retryButton.onClick.AddListener(() => OnRetryClicked());

        HideAllBoxes();
        HideFeedback();
        HideGameOver();
    }
    // NamNN add function Hideboxes
    public void HideBoxes(int playerIndex)
    {
        // Lấy danh sách các nút của người chơi 0 hoặc 1
        Button[] btns = GetButtons(playerIndex);
        if (btns != null)
        {
            foreach (var b in btns)
            {
                // Tắt từng nút đi
                if (b != null) b.gameObject.SetActive(false);
            }
        }
    }


    private void HideAllBoxes()
    {
        if (p1Boxes != null) foreach (var b in p1Boxes) if (b != null) b.gameObject.SetActive(false);
        if (p2Boxes != null) foreach (var b in p2Boxes) if (b != null) b.gameObject.SetActive(false);
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
        if (p1ScoreBarFill != null) p1ScoreBarFill.fillAmount = Mathf.Clamp01((float)p1Score / maxScore);
        if (p2ScoreBarFill != null) p2ScoreBarFill.fillAmount = Mathf.Clamp01((float)p2Score / maxScore);
    }

    public void SetQuestionText(string text)
    {
        if (questionText != null) questionText.text = text;
    }

    public void ShowBoxes(int playerIndex, string[] values)
    {
        Button[] btns = GetButtons(playerIndex);
        Text[] txts = GetTexts(playerIndex);
        Image[] imgs = GetImages(playerIndex);
        int count = values.Length;

        for (int i = 0; i < btns.Length; i++)
            if (btns[i] != null) btns[i].gameObject.SetActive(i < count);
        //NamNN change box size
        float halfLeft = (playerIndex == 0) ? 0.04f : 0.54f;
        float halfRight = (playerIndex == 0) ? 0.46f : 0.96f;
        float yMin = 0.05f;
        float yMax = 0.75f;

        float sizeScale = (count <= 3) ? 1f : (count == 4) ? 0.9f : 0.85f;
        float baseBoxW = 0.16f * sizeScale;
        float baseBoxH = 0.28f * sizeScale;

        float[] boxWs = new float[count];
        float[] boxHs = new float[count];
        float maxW = 0f, maxH = 0f;
        for (int i = 0; i < count; i++)
        {
            float s = UnityEngine.Random.Range(0.85f, 1.15f);
            boxWs[i] = baseBoxW * s;
            boxHs[i] = baseBoxH * s;
            if (boxWs[i] > maxW) maxW = boxWs[i];
            if (boxHs[i] > maxH) maxH = boxHs[i];
        }

        Vector2[] positions = GenerateRandomPositions(count, halfLeft, halfRight, yMin, yMax, maxW, maxH);

        for (int i = 0; i < count; i++)
        {
            if (btns[i] != null)
            {
                btns[i].interactable = true;
                RectTransform rt = btns[i].GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(positions[i].x, positions[i].y);
                rt.anchorMax = new Vector2(positions[i].x + boxWs[i], positions[i].y + boxHs[i]);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            if (imgs[i] != null && planetSprites != null && planetSprites.Length > 0)
            {
                int randIdx = UnityEngine.Random.Range(0, planetSprites.Length);
                imgs[i].sprite = planetSprites[randIdx];
                imgs[i].preserveAspect = true;
                imgs[i].color = Color.white;
            }
            if (txts[i] != null)
            {
                txts[i].text = values[i];
                txts[i].color = _normalTextColor;
                Transform shadow = txts[i].transform.parent.Find("Shadow");
                if (shadow != null) { Text st = shadow.GetComponent<Text>(); if (st != null) st.text = values[i]; }
            }
        }
    }

    private Vector2[] GenerateRandomPositions(int count, float xMin, float xMax, float yMin, float yMax, float boxW, float boxH)
    {
        Vector2[] result = new Vector2[count];
        float areaW = xMax - xMin;
        float areaH = yMax - yMin;

        float[][,] layouts3 = new float[][,]
        {
            new float[,] { {0.15f, 0.75f}, {0.50f, 0.25f}, {0.85f, 0.70f} },
            new float[,] { {0.20f, 0.25f}, {0.80f, 0.25f}, {0.50f, 0.75f} },
            new float[,] { {0.50f, 0.20f}, {0.20f, 0.75f}, {0.80f, 0.75f} },
        };
        float[][,] layouts4 = new float[][,]
        {
            new float[,] { {0.20f, 0.25f}, {0.80f, 0.25f}, {0.20f, 0.75f}, {0.80f, 0.75f} },
            new float[,] { {0.50f, 0.15f}, {0.15f, 0.50f}, {0.85f, 0.50f}, {0.50f, 0.85f} },
        };
        float[][,] layouts5 = new float[][,]
        {
            new float[,] { {0.20f, 0.20f}, {0.80f, 0.20f}, {0.50f, 0.50f}, {0.20f, 0.80f}, {0.80f, 0.80f} },
            new float[,] { {0.50f, 0.15f}, {0.15f, 0.42f}, {0.85f, 0.42f}, {0.25f, 0.80f}, {0.75f, 0.80f} },
        };

        float[][,] pool;
        if (count <= 3) pool = layouts3;
        else if (count == 4) pool = layouts4;
        else pool = layouts5;

        int layoutIdx = UnityEngine.Random.Range(0, pool.Length);
        float[,] layout = pool[layoutIdx];

        float jitterX = areaW * 0.08f;
        float jitterY = areaH * 0.08f;

        for (int i = 0; i < count; i++)
        {
            float cx = xMin + layout[i, 0] * areaW;
            float cy = yMin + layout[i, 1] * areaH;
            cx += UnityEngine.Random.Range(-jitterX, jitterX);
            cy += UnityEngine.Random.Range(-jitterY, jitterY);
            float x = Mathf.Clamp(cx - boxW * 0.5f, xMin, xMax - boxW);
            float y = Mathf.Clamp(cy - boxH * 0.5f, yMin, yMax - boxH);
            result[i] = new Vector2(x, y);
        }
        return result;
    }

    public void SetBoxCorrect(int playerIndex, int boxIndex)
    {
        Text[] txts = GetTexts(playerIndex);
        Button[] btns = GetButtons(playerIndex);
        if (boxIndex < txts.Length && txts[boxIndex] != null) txts[boxIndex].color = _correctTextColor;
        if (boxIndex < btns.Length && btns[boxIndex] != null) btns[boxIndex].interactable = false;
    }

    public void SetBoxWrong(int playerIndex, int boxIndex)
    {
        Text[] txts = GetTexts(playerIndex);
        if (boxIndex < txts.Length && txts[boxIndex] != null) txts[boxIndex].color = _wrongTextColor;
    }

    public void SetPlayerInteractable(int playerIndex, bool interactable)
    {
        Button[] btns = GetButtons(playerIndex);
        for (int i = 0; i < btns.Length; i++)
            if (btns[i] != null) btns[i].interactable = interactable;
    }

    public void ShowFeedback(int playerIndex, bool correct)
    {
        GameObject icon = correct
            ? (playerIndex == 0 ? p1CorrectIcon : p2CorrectIcon)
            : (playerIndex == 0 ? p1WrongIcon : p2WrongIcon);
        if (icon != null)
        {
            icon.SetActive(true);
            FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
            if (fx == null) fx = icon.AddComponent<FeedbackEffect>();
            fx.Play(correct);
        }
    }

    public void HideFeedback()
    {
        StopFx(p1CorrectIcon); StopFx(p1WrongIcon);
        StopFx(p2CorrectIcon); StopFx(p2WrongIcon);
    }

    public void HideFeedback(int playerIndex)
    {
        if (playerIndex == 0) { StopFx(p1CorrectIcon); StopFx(p1WrongIcon); }
        else { StopFx(p2CorrectIcon); StopFx(p2WrongIcon); }
    }

    private void StopFx(GameObject icon)
    {
        if (icon == null) return;
        FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
        if (fx != null) fx.StopEffect();
        icon.SetActive(false);
    }

    public void ShowGameOver(int p1Score, int p2Score)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverResultText != null)
            {
                if (p1Score > p2Score) gameOverResultText.text = "Player 1 Thắng!\n" + p1Score + " - " + p2Score;
                else if (p2Score > p1Score) gameOverResultText.text = "Player 2 Thắng!\n" + p1Score + " - " + p2Score;
                else gameOverResultText.text = "Hòa!\n" + p1Score + " - " + p2Score;
            }
        }
    }

    public void HideGameOver() { if (gameOverPanel != null) gameOverPanel.SetActive(false); }

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

    private Button[] GetButtons(int p) { return p == 0 ? p1Boxes : p2Boxes; }
    private Text[] GetTexts(int p) { return p == 0 ? p1Texts : p2Texts; }
    private Image[] GetImages(int p) { return p == 0 ? p1Images : p2Images; }
}
