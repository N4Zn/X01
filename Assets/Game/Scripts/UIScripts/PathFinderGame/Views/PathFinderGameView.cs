using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for PathFinder game — renders a maze grid with character at a junction.
/// Player chooses Left/Straight/Right to navigate toward the destination.
/// Split-screen 2-player layout (1024x600).
/// </summary>
public class PathFinderGameView : MonoBehaviour
{
    [Header("=== Timer ===")]
    [SerializeField] private Text timerText;

    [Header("=== Back Button ===")]
    [SerializeField] private Button backButton;

    [Header("=== Player 1 (Left) ===")]
    [SerializeField] private Text p1NameText;
    [SerializeField] private Text p1ScoreText;
    [SerializeField] private RectTransform p1MazeContainer;
    [SerializeField] private Text p1PromptText;
    [SerializeField] private Button p1OptionLeft;
    [SerializeField] private Button p1OptionStraight;
    [SerializeField] private Button p1OptionRight;
    [SerializeField] private Image p1OptionLeftBorder;
    [SerializeField] private Image p1OptionStraightBorder;
    [SerializeField] private Image p1OptionRightBorder;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;

    [Header("=== Player 2 (Right) ===")]
    [SerializeField] private Text p2NameText;
    [SerializeField] private Text p2ScoreText;
    [SerializeField] private RectTransform p2MazeContainer;
    [SerializeField] private Text p2PromptText;
    [SerializeField] private Button p2OptionLeft;
    [SerializeField] private Button p2OptionStraight;
    [SerializeField] private Button p2OptionRight;
    [SerializeField] private Image p2OptionLeftBorder;
    [SerializeField] private Image p2OptionStraightBorder;
    [SerializeField] private Image p2OptionRightBorder;
    [SerializeField] private GameObject p2CorrectIcon;
    [SerializeField] private GameObject p2WrongIcon;

    [Header("=== Countdown (Team Mode) ===")]
    [SerializeField] private Text p1CountdownText;
    [SerializeField] private Text p2CountdownText;

    [Header("=== Star Column ===")]
    [SerializeField] private Text p1StarText;
    [SerializeField] private Text p2StarText;

    [Header("=== Score Bars ===")]
    [SerializeField] private Image p1ScoreBarFill;
    [SerializeField] private Image p2ScoreBarFill;
    [SerializeField] private int maxScore = 10;

    // Events
    public event Action<int, int> OnAnswerSelected = delegate { }; // (playerIndex, choice: 0=Left, 1=Straight, 2=Right)
    public event Action OnBackClicked = delegate { };

    // Runtime cell references
    private List<Image> _p1Cells = new List<Image>();
    private List<Image> _p2Cells = new List<Image>();
    private MazeGenerator.MazePuzzle _p1Puzzle;
    private MazeGenerator.MazePuzzle _p2Puzzle;

    // Colors
    private readonly Color _wallColor = new Color(0.15f, 0.20f, 0.30f, 1f);
    private readonly Color _pathColor = new Color(0.75f, 0.82f, 0.70f, 1f);
    private readonly Color _startColor = new Color(0.3f, 0.6f, 1f, 1f);
    private readonly Color _endColor = new Color(1f, 0.75f, 0.2f, 1f);
    private readonly Color _junctionColor = new Color(0.2f, 0.85f, 0.4f, 1f);
    private readonly Color _solutionHintColor = new Color(0.6f, 0.8f, 0.55f, 1f);
    private readonly Color _neutralBorder = new Color(0.5f, 0.5f, 0.55f, 1f);
    private readonly Color _selectedBorder = new Color(1f, 0.8f, 0.2f, 1f);
    private readonly Color _correctBorder = new Color(0.2f, 0.8f, 0.2f, 1f);
    private readonly Color _wrongBorder = new Color(0.8f, 0.2f, 0.2f, 1f);
    private readonly Color _correctPathColor = new Color(0.3f, 0.9f, 0.4f, 0.7f);
    private readonly Color _wrongPathColor = new Color(0.9f, 0.3f, 0.3f, 0.5f);

    /// <summary>Refreshes the two name labels — reusable after PlayerRecognitionService
    /// recognizes someone mid-game, not just at InitView().</summary>
    public void SetPlayerNames(string p1Name, string p2Name)
    {
        if (p1NameText != null) p1NameText.text = p1Name;
        if (p2NameText != null) p2NameText.text = p2Name;
    }

    public void InitView()
    {
        string p1Name = GameSessionManager.Instance.GetDisplayName1();
        string p2Name = GameSessionManager.Instance.GetDisplayName2();
        SetPlayerNames(p1Name, p2Name);

        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => OnBackClicked()); }

        if (p1OptionLeft != null) { p1OptionLeft.onClick.RemoveAllListeners(); p1OptionLeft.onClick.AddListener(() => OnAnswerSelected(0, 0)); }
        if (p1OptionStraight != null) { p1OptionStraight.onClick.RemoveAllListeners(); p1OptionStraight.onClick.AddListener(() => OnAnswerSelected(0, 1)); }
        if (p1OptionRight != null) { p1OptionRight.onClick.RemoveAllListeners(); p1OptionRight.onClick.AddListener(() => OnAnswerSelected(0, 2)); }

        if (p2OptionLeft != null) { p2OptionLeft.onClick.RemoveAllListeners(); p2OptionLeft.onClick.AddListener(() => OnAnswerSelected(1, 0)); }
        if (p2OptionStraight != null) { p2OptionStraight.onClick.RemoveAllListeners(); p2OptionStraight.onClick.AddListener(() => OnAnswerSelected(1, 1)); }
        if (p2OptionRight != null) { p2OptionRight.onClick.RemoveAllListeners(); p2OptionRight.onClick.AddListener(() => OnAnswerSelected(1, 2)); }

        HideFeedback(0);
        HideFeedback(1);
    }

    // ===== Timer & Score =====

    public void UpdateTimer(float seconds)
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(seconds).ToString();
    }

    public void UpdateScore(int playerIndex, int score)
    {
        Text t = playerIndex == 0 ? p1ScoreText : p2ScoreText;
        if (t != null) t.text = "\u2605 " + score;

        Image fill = playerIndex == 0 ? p1ScoreBarFill : p2ScoreBarFill;
        if (fill != null) fill.fillAmount = Mathf.Clamp01((float)score / maxScore);
    }

    public void UpdateStars(int playerIndex, int stars)
    {
        Text t = playerIndex == 0 ? p1StarText : p2StarText;
        if (t != null) t.text = "\u2605 " + stars;

        Image fill = playerIndex == 0 ? p1ScoreBarFill : p2ScoreBarFill;
        if (fill != null) fill.fillAmount = Mathf.Clamp01((float)stars / maxScore);
    }

    // NamNN change with Android Studio Agent
    public void SetMaxScore(int value)
    {
        this.maxScore = value;
        UpdateScore(0, 0);
        UpdateScore(1, 0);
    }

    // ===== Render Maze =====

    public void DisplayMaze(int playerIndex, MazeGenerator.MazePuzzle puzzle)
    {
        RectTransform container = playerIndex == 0 ? p1MazeContainer : p2MazeContainer;
        List<Image> cells = playerIndex == 0 ? _p1Cells : _p2Cells;

        if (playerIndex == 0) _p1Puzzle = puzzle;
        else _p2Puzzle = puzzle;

        // Clear previous cells
        ClearCells(container, cells);

        int rows = puzzle.RenderHeight;
        int cols = puzzle.RenderWidth;

        // Calculate cell size based on container
        float containerW = container.rect.width;
        float containerH = container.rect.height;
        if (containerW <= 0) containerW = 200f;
        if (containerH <= 0) containerH = 200f;

        float cellSize = Mathf.Min(containerW / cols, containerH / rows);

        // Center the grid
        float gridW = cellSize * cols;
        float gridH = cellSize * rows;
        float offsetX = (containerW - gridW) * 0.5f;
        float offsetY = (containerH - gridH) * 0.5f;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject cellGo = new GameObject("Cell_" + r + "_" + c, typeof(RectTransform), typeof(Image));
                cellGo.transform.SetParent(container, false);

                RectTransform rt = cellGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(
                    offsetX + c * cellSize,
                    containerH - offsetY - r * cellSize
                );

                Image img = cellGo.GetComponent<Image>();

                // Determine cell color
                if (r == puzzle.RenderJunction.x && c == puzzle.RenderJunction.y)
                {
                    img.color = _junctionColor;
                }
                else if (r == puzzle.RenderStart.x && c == puzzle.RenderStart.y)
                {
                    img.color = _startColor;
                }
                else if (r == puzzle.RenderEnd.x && c == puzzle.RenderEnd.y)
                {
                    img.color = _endColor;
                }
                else if (puzzle.Grid[r, c])
                {
                    img.color = _pathColor;
                }
                else
                {
                    img.color = _wallColor;
                }

                cells.Add(img);

                // Add labels for special cells
                if (r == puzzle.RenderStart.x && c == puzzle.RenderStart.y)
                {
                    AddCellLabel(cellGo, "\u25b2", cellSize); // triangle (character)
                }
                else if (r == puzzle.RenderEnd.x && c == puzzle.RenderEnd.y)
                {
                    AddCellLabel(cellGo, "\u2605", cellSize); // star (destination)
                }
                else if (r == puzzle.RenderJunction.x && c == puzzle.RenderJunction.y)
                {
                    AddCellLabel(cellGo, "?", cellSize);
                }
            }
        }

        // Update prompt
        Text prompt = playerIndex == 0 ? p1PromptText : p2PromptText;
        if (prompt != null) prompt.text = "Ch\u1ecdn h\u01b0\u1edbng \u0111i \u0111\u1ec3 \u0111\u1ebfn \u2605";

        // Reset UI
        ResetOptionBorders(playerIndex);
        SetOptionsInteractable(playerIndex, true);
        HideFeedback(playerIndex);

        //NamNN change with Android Studio Agent
        container.gameObject.SetActive(true);
        if (playerIndex == 0)
        {
            if (p1OptionLeft != null) p1OptionLeft.gameObject.SetActive(true);
            if (p1OptionStraight != null) p1OptionStraight.gameObject.SetActive(true);
            if (p1OptionRight != null) p1OptionRight.gameObject.SetActive(true);
            if (p1PromptText != null) p1PromptText.gameObject.SetActive(true);
        }
        else
        {
            if (p2OptionLeft != null) p2OptionLeft.gameObject.SetActive(true);
            if (p2OptionStraight != null) p2OptionStraight.gameObject.SetActive(true);
            if (p2OptionRight != null) p2OptionRight.gameObject.SetActive(true);
            if (p2PromptText != null) p2PromptText.gameObject.SetActive(true);
        }
    }

    private void AddCellLabel(GameObject cellGo, string label, float cellSize)
    {
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(cellGo.transform, false);
        RectTransform rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Text text = labelGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = Mathf.Max(8, (int)(cellSize * 0.6f));
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void ClearCells(RectTransform container, List<Image> cells)
    {
        foreach (Image img in cells)
        {
            if (img != null) Destroy(img.gameObject);
        }
        cells.Clear();

        // Also destroy any remaining children
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    // ===== Answer Result =====

    public void ShowAnswerResult(int playerIndex, int selectedChoice, bool isCorrect)
    {
        MazeGenerator.MazePuzzle puzzle = playerIndex == 0 ? _p1Puzzle : _p2Puzzle;
        int correctChoice = puzzle != null ? puzzle.CorrectChoice : 1;

        // Highlight option borders
        Image borderL = playerIndex == 0 ? p1OptionLeftBorder : p2OptionLeftBorder;
        Image borderS = playerIndex == 0 ? p1OptionStraightBorder : p2OptionStraightBorder;
        Image borderR = playerIndex == 0 ? p1OptionRightBorder : p2OptionRightBorder;
        Image[] borders = { borderL, borderS, borderR };

        for (int i = 0; i < 3; i++)
        {
            if (borders[i] == null) continue;
            if (i == correctChoice)
                borders[i].color = _correctBorder;
            else if (i == selectedChoice && !isCorrect)
                borders[i].color = _wrongBorder;
            else
                borders[i].color = _neutralBorder;
        }

        // Highlight solution path on maze
        if (puzzle != null)
        {
            HighlightSolutionPath(playerIndex, puzzle);
        }

        ShowFeedback(playerIndex, isCorrect);
        SetOptionsInteractable(playerIndex, false);
    }

    private void HighlightSolutionPath(int playerIndex, MazeGenerator.MazePuzzle puzzle)
    {
        List<Image> cells = playerIndex == 0 ? _p1Cells : _p2Cells;
        int cols = puzzle.RenderWidth;

        // Highlight solution path cells in the render grid
        for (int i = 0; i < puzzle.SolutionPath.Count; i++)
        {
            Vector2Int logical = puzzle.SolutionPath[i];
            Vector2Int render = puzzle.ToRender(logical);
            int cellIdx = render.x * cols + render.y;

            if (cellIdx >= 0 && cellIdx < cells.Count && cells[cellIdx] != null)
            {
                cells[cellIdx].color = _correctPathColor;
            }

            // Also highlight passage between this cell and next
            if (i + 1 < puzzle.SolutionPath.Count)
            {
                Vector2Int nextLogical = puzzle.SolutionPath[i + 1];
                Vector2Int nextRender = puzzle.ToRender(nextLogical);
                int passageR = (render.x + nextRender.x) / 2;
                int passageC = (render.y + nextRender.y) / 2;
                int passageIdx = passageR * cols + passageC;

                if (passageIdx >= 0 && passageIdx < cells.Count && cells[passageIdx] != null)
                {
                    cells[passageIdx].color = _correctPathColor;
                }
            }
        }

        // Mark destination and junction
        int endIdx = puzzle.RenderEnd.x * cols + puzzle.RenderEnd.y;
        if (endIdx >= 0 && endIdx < cells.Count && cells[endIdx] != null)
            cells[endIdx].color = _endColor;

        int juncIdx = puzzle.RenderJunction.x * cols + puzzle.RenderJunction.y;
        if (juncIdx >= 0 && juncIdx < cells.Count && cells[juncIdx] != null)
            cells[juncIdx].color = _junctionColor;
    }

    // ===== Feedback =====

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

    public void HideFeedback(int playerIndex)
    {
        StopFx(playerIndex == 0 ? p1CorrectIcon : p2CorrectIcon);
        StopFx(playerIndex == 0 ? p1WrongIcon : p2WrongIcon);
    }

    private void StopFx(GameObject icon)
    {
        if (icon == null) return;
        FeedbackEffect fx = icon.GetComponent<FeedbackEffect>();
        if (fx != null) fx.StopEffect();
        icon.SetActive(false);
    }

    // ===== Option Helpers =====

    public void HighlightSelectedOption(int playerIndex, int choice)
    {
        Image borderL = playerIndex == 0 ? p1OptionLeftBorder : p2OptionLeftBorder;
        Image borderS = playerIndex == 0 ? p1OptionStraightBorder : p2OptionStraightBorder;
        Image borderR = playerIndex == 0 ? p1OptionRightBorder : p2OptionRightBorder;
        Image[] borders = { borderL, borderS, borderR };

        for (int i = 0; i < 3; i++)
        {
            if (borders[i] != null)
                borders[i].color = (i == choice) ? _selectedBorder : _neutralBorder;
        }
    }

    private void ResetOptionBorders(int playerIndex)
    {
        Image borderL = playerIndex == 0 ? p1OptionLeftBorder : p2OptionLeftBorder;
        Image borderS = playerIndex == 0 ? p1OptionStraightBorder : p2OptionStraightBorder;
        Image borderR = playerIndex == 0 ? p1OptionRightBorder : p2OptionRightBorder;

        if (borderL != null) borderL.color = _neutralBorder;
        if (borderS != null) borderS.color = _neutralBorder;
        if (borderR != null) borderR.color = _neutralBorder;
    }

    private void SetOptionsInteractable(int playerIndex, bool interactable)
    {
        if (playerIndex == 0)
        {
            if (p1OptionLeft != null) p1OptionLeft.interactable = interactable;
            if (p1OptionStraight != null) p1OptionStraight.interactable = interactable;
            if (p1OptionRight != null) p1OptionRight.interactable = interactable;
        }
        else
        {
            if (p2OptionLeft != null) p2OptionLeft.interactable = interactable;
            if (p2OptionStraight != null) p2OptionStraight.interactable = interactable;
            if (p2OptionRight != null) p2OptionRight.interactable = interactable;
        }
    }

    // ===== Countdown (Team Mode) =====

    //NamNN change with Android Studio Agent
    public void HideQuestion(int playerIndex)
    {
        RectTransform container = playerIndex == 0 ? p1MazeContainer : p2MazeContainer;
        if (container != null) container.gameObject.SetActive(false);

        if (playerIndex == 0)
        {
            if (p1OptionLeft != null) p1OptionLeft.gameObject.SetActive(false);
            if (p1OptionStraight != null) p1OptionStraight.gameObject.SetActive(false);
            if (p1OptionRight != null) p1OptionRight.gameObject.SetActive(false);
            if (p1PromptText != null) p1PromptText.gameObject.SetActive(false);
        }
        else
        {
            if (p2OptionLeft != null) p2OptionLeft.gameObject.SetActive(false);
            if (p2OptionStraight != null) p2OptionStraight.gameObject.SetActive(false);
            if (p2OptionRight != null) p2OptionRight.gameObject.SetActive(false);
            if (p2PromptText != null) p2PromptText.gameObject.SetActive(false);
        }
    }

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
