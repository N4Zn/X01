using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for the TrainPath game - 3x3 grid map with hidden cell
/// Split screen for 2 players, resolution 1024x600
/// Shows track direction strips in each cell, train animation, and visual tile answer options
/// </summary>
public class TrainPathGameView : MonoBehaviour
{
    [Header("=== Common UI ===")]
    [SerializeField] private Text timerText;
    [SerializeField] private Button backButton;

    [Header("=== Player 1 (Left) ===")]
    [SerializeField] private Text p1NameText;
    [SerializeField] private Text p1ScoreText;
    // 3x3 grid cells (row-major: [0]=r0c0, [1]=r0c1, ... [8]=r2c2)
    [SerializeField] private Image[] p1GridCells = new Image[16];
    [SerializeField] private Text[] p1GridTexts = new Text[16];
    // Each cell has 2 child Images for track path strips (entry strip + exit strip)
    [SerializeField] private Image[] p1TrackStripsA = new Image[16]; // entry direction strip
    [SerializeField] private Image[] p1TrackStripsB = new Image[16]; // exit direction strip
    // Answer option tiles (visual track type previews)
    [SerializeField] private Button p1OptionLeftBtn;
    [SerializeField] private Button p1OptionStraightBtn;
    [SerializeField] private Button p1OptionRightBtn;
    [SerializeField] private Image p1OptionLeftBorder;
    [SerializeField] private Image p1OptionStraightBorder;
    [SerializeField] private Image p1OptionRightBorder;
    [SerializeField] private Image p1OptionLeftTile;     // colored tile inside option
    [SerializeField] private Image p1OptionStraightTile;
    [SerializeField] private Image p1OptionRightTile;
    [SerializeField] private GameObject p1CorrectIcon;
    [SerializeField] private GameObject p1WrongIcon;
    // Train, end marker, and grid rotation
    [SerializeField] private Image p1TrainIcon;
    [SerializeField] private Image[] p1EndMarkers = new Image[4]; // Top, Bottom, Left, Right
    [SerializeField] private Text[] p1EndMarkerTexts = new Text[4];
    [SerializeField] private RectTransform p1GridArea; // rotates during gameplay

    [Header("=== Player 2 (Right) ===")]
    [SerializeField] private Text p2NameText;
    [SerializeField] private Text p2ScoreText;
    [SerializeField] private Image[] p2GridCells = new Image[16];
    [SerializeField] private Text[] p2GridTexts = new Text[16];
    [SerializeField] private Image[] p2TrackStripsA = new Image[16];
    [SerializeField] private Image[] p2TrackStripsB = new Image[16];
    [SerializeField] private Button p2OptionLeftBtn;
    [SerializeField] private Button p2OptionStraightBtn;
    [SerializeField] private Button p2OptionRightBtn;
    [SerializeField] private Image p2OptionLeftBorder;
    [SerializeField] private Image p2OptionStraightBorder;
    [SerializeField] private Image p2OptionRightBorder;
    [SerializeField] private Image p2OptionLeftTile;
    [SerializeField] private Image p2OptionStraightTile;
    [SerializeField] private Image p2OptionRightTile;
    [SerializeField] private GameObject p2CorrectIcon;
    [SerializeField] private GameObject p2WrongIcon;
    [SerializeField] private Image p2TrainIcon;
    [SerializeField] private Image[] p2EndMarkers = new Image[4];
    [SerializeField] private Text[] p2EndMarkerTexts = new Text[4];
    [SerializeField] private RectTransform p2GridArea;

    [Header("=== Countdown (Team Mode) ===")]
    [SerializeField] private Text p1CountdownText;
    [SerializeField] private Text p2CountdownText;

    [Header("=== Score Bars ===")]
    [SerializeField] private Image p1ScoreBarFill;
    [SerializeField] private Image p2ScoreBarFill;
    [SerializeField] private int maxScore = 10;

    [Header("=== Cell Sprites ===")]
    [SerializeField] private Sprite bushSprite;        // tree icon
    [SerializeField] private Sprite hiddenSprite;      // bubble question
    [SerializeField] private Sprite trainSprite;       // bus icon
    [SerializeField] private Sprite endpointSprite;    // school icon
    [SerializeField] private Sprite arrowSprite;       // green arrow
    [SerializeField] private Sprite tileStraightH;         // tile_straight_h (title_2) — left↔right
    [SerializeField] private Sprite tileStraightV;         // tile_straight_v (title_8) — top↔bottom
    [SerializeField] private Sprite tileCornerTopLeft;     // tile_corner_top_left (title_5) — top+left
    [SerializeField] private Sprite tileCornerTopRight;    // tile_corner_top_right (title_7) — top+right
    [SerializeField] private Sprite tileCornerBottomRight; // tile_corner_bottom_right (title_3) — bottom+right
    [SerializeField] private Sprite tileCornerBottomLeft;  // tile_corner_bottom_left (title_9) — bottom+left
    [SerializeField] private Sprite tileBush;              // tile_bush (title_1) — green grass

    // Colors
    private Color normalBorderColor = new Color(0.75f, 0.75f, 0.8f, 1f);
    private Color selectedBorderColor = new Color(1f, 0.8f, 0.2f, 1f);
    private Color correctBorderColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    private Color wrongBorderColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    private Color hiddenCellColor = new Color(0.45f, 0.45f, 0.55f, 1f);
    private Color endMarkerColor = new Color(0.9f, 0.2f, 0.15f, 1f);
    private Color trackStripColor = new Color(0.55f, 0.42f, 0.22f, 1f); // brown track strip
    private Color bushColor = new Color(0.25f, 0.6f, 0.25f, 1f);
    private Color bushDarkColor = new Color(0.15f, 0.45f, 0.15f, 1f);

    // Animation
    private float trainMoveSpeed = 0.25f;
    private Coroutine _p1AnimCoroutine;
    private Coroutine _p2AnimCoroutine;

    // Grid rotation
    private float _gridRotationSpeed = 0f; // disabled - no grid rotation

    // Events
    public event Action<int, int> OnOptionSelected = delegate { };
    public event Action OnRetryClicked = delegate { };
    public event Action OnBackClicked = delegate { };

    public void InitView()
    {
        if (p1OptionLeftBtn != null) { p1OptionLeftBtn.onClick.RemoveAllListeners(); p1OptionLeftBtn.onClick.AddListener(() => OnOptionSelected(0, 0)); }
        if (p1OptionStraightBtn != null) { p1OptionStraightBtn.onClick.RemoveAllListeners(); p1OptionStraightBtn.onClick.AddListener(() => OnOptionSelected(0, 1)); }
        if (p1OptionRightBtn != null) { p1OptionRightBtn.onClick.RemoveAllListeners(); p1OptionRightBtn.onClick.AddListener(() => OnOptionSelected(0, 2)); }

        if (p2OptionLeftBtn != null) { p2OptionLeftBtn.onClick.RemoveAllListeners(); p2OptionLeftBtn.onClick.AddListener(() => OnOptionSelected(1, 0)); }
        if (p2OptionStraightBtn != null) { p2OptionStraightBtn.onClick.RemoveAllListeners(); p2OptionStraightBtn.onClick.AddListener(() => OnOptionSelected(1, 1)); }
        if (p2OptionRightBtn != null) { p2OptionRightBtn.onClick.RemoveAllListeners(); p2OptionRightBtn.onClick.AddListener(() => OnOptionSelected(1, 2)); }

        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => OnBackClicked()); }

        SetPlayerNames(
            GameSessionManager.Instance.GetDisplayName1(),
            GameSessionManager.Instance.GetDisplayName2()
        );
        UpdateScores(0, 0);
        HideFeedbackIcons();
        HideAllEndMarkers();
        HideTrainIcons();

        // Apply train sprite
        if (trainSprite != null)
        {
            if (p1TrainIcon != null) { p1TrainIcon.sprite = trainSprite; p1TrainIcon.preserveAspect = true; p1TrainIcon.color = Color.white; }
            if (p2TrainIcon != null) { p2TrainIcon.sprite = trainSprite; p2TrainIcon.preserveAspect = true; p2TrainIcon.color = Color.white; }
        }

        // Apply endpoint sprite
        if (endpointSprite != null)
        {
            ApplyEndpointSprites(p1EndMarkers);
            ApplyEndpointSprites(p2EndMarkers);
        }
    }

    public void SetPlayerNames(string name1, string name2)
    {
        if (p1NameText != null) p1NameText.text = name1;
        if (p2NameText != null) p2NameText.text = name2;
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
        if (p1ScoreText != null) p1ScoreText.text = "\u2605 " + p1Score;
        if (p2ScoreText != null) p2ScoreText.text = "\u2605 " + p2Score;

        if (p1ScoreBarFill != null) p1ScoreBarFill.fillAmount = Mathf.Clamp01((float)p1Score / maxScore);
        if (p2ScoreBarFill != null) p2ScoreBarFill.fillAmount = Mathf.Clamp01((float)p2Score / maxScore);
    }

    public void UpdateStars(int playerIndex, int stars)
    {
        Text t = playerIndex == 0 ? p1ScoreText : p2ScoreText;
        if (t != null) t.text = "\u2605 " + stars;

        Image fill = playerIndex == 0 ? p1ScoreBarFill : p2ScoreBarFill;
        if (fill != null) fill.fillAmount = Mathf.Clamp01((float)stars / maxScore);
    }

    // NamNN change with Android Studio Agent
    public void SetMaxScore(int value)
    {
        this.maxScore = value;
        UpdateScores(0, 0);
    }

    /// <summary>
    /// Display a puzzle on the 3x3 grid with track direction strips
    /// </summary>
    public void DisplayPuzzle(int playerIndex, TrainPathPuzzle puzzle)
    {
        Image[] cells = playerIndex == 0 ? p1GridCells : p2GridCells;
        Text[] texts = playerIndex == 0 ? p1GridTexts : p2GridTexts;
        Image[] stripsA = playerIndex == 0 ? p1TrackStripsA : p2TrackStripsA;
        Image[] stripsB = playerIndex == 0 ? p1TrackStripsB : p2TrackStripsB;

        for (int r = 0; r < TrainPathPuzzle.GRID_SIZE; r++)
        {
            for (int c = 0; c < TrainPathPuzzle.GRID_SIZE; c++)
            {
                int idx = r * TrainPathPuzzle.GRID_SIZE + c;
                if (idx >= cells.Length || cells[idx] == null) continue;

                bool isHidden = (r == puzzle.HiddenRow && c == puzzle.HiddenCol);
                TrainCellType cellType = puzzle.Grid[r, c];

                // Hide track strips by default
                if (idx < stripsA.Length && stripsA[idx] != null) stripsA[idx].gameObject.SetActive(false);
                if (idx < stripsB.Length && stripsB[idx] != null) stripsB[idx].gameObject.SetActive(false);

                if (isHidden)
                {
                    // Hidden cell: bubble question sprite
                    Sprite spr = hiddenSprite;
                    if (spr != null) { cells[idx].sprite = spr; cells[idx].preserveAspect = true; cells[idx].color = Color.white; }
                    else cells[idx].color = hiddenCellColor;

                    if (idx < stripsA.Length && stripsA[idx] != null) stripsA[idx].gameObject.SetActive(false);
                }
                else if (cellType == TrainCellType.Bush)
                {
                    // Bush cell: green grass tile + tree icon overlay
                    if (tileBush != null) { cells[idx].sprite = tileBush; cells[idx].preserveAspect = false; cells[idx].color = Color.white; }
                    else cells[idx].color = bushColor;

                    // Overlay tree icon using stripA
                    if (idx < stripsA.Length && stripsA[idx] != null && bushSprite != null)
                    {
                        stripsA[idx].gameObject.SetActive(true);
                        stripsA[idx].sprite = bushSprite;
                        stripsA[idx].preserveAspect = true;
                        stripsA[idx].color = Color.white;
                        stripsA[idx].type = Image.Type.Simple;
                        stripsA[idx].rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
                        stripsA[idx].rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
                        stripsA[idx].rectTransform.offsetMin = Vector2.zero;
                        stripsA[idx].rectTransform.offsetMax = Vector2.zero;
                    }
                    else if (idx < stripsA.Length && stripsA[idx] != null)
                    {
                        stripsA[idx].gameObject.SetActive(false);
                    }
                }
                else
                {
                    // Track cell: road tile sprite
                    Sprite spr = GetTileSprite(cellType, puzzle, idx);
                    if (spr != null) { cells[idx].sprite = spr; cells[idx].preserveAspect = false; cells[idx].color = Color.white; }
                    else cells[idx].color = new Color(0.78f, 0.72f, 0.55f, 1f);

                    if (idx < stripsA.Length && stripsA[idx] != null) stripsA[idx].gameObject.SetActive(false);
                }

                // Hide stripB and text for all cells
                if (idx < stripsB.Length && stripsB[idx] != null) stripsB[idx].gameObject.SetActive(false);
                if (idx < texts.Length && texts[idx] != null) texts[idx].text = "";
            }
        }

        // Grid stays upright (no rotation)
        SetGridRotation(playerIndex, 0f);

        //NamNN change with Android Studio Agent
        RectTransform gridArea = playerIndex == 0 ? p1GridArea : p2GridArea;
        if (gridArea != null) gridArea.gameObject.SetActive(true);
        if (playerIndex == 0)
        {
            if (p1OptionLeftBtn != null) p1OptionLeftBtn.gameObject.SetActive(true);
            if (p1OptionStraightBtn != null) p1OptionStraightBtn.gameObject.SetActive(true);
            if (p1OptionRightBtn != null) p1OptionRightBtn.gameObject.SetActive(true);
        }
        else
        {
            if (p2OptionLeftBtn != null) p2OptionLeftBtn.gameObject.SetActive(true);
            if (p2OptionStraightBtn != null) p2OptionStraightBtn.gameObject.SetActive(true);
            if (p2OptionRightBtn != null) p2OptionRightBtn.gameObject.SetActive(true);
        }

        ShowTrainAtCell(playerIndex, puzzle.StartRow, puzzle.StartCol);
        ShowEndMarker(playerIndex, puzzle.ExitSide, puzzle.ExitEdgeCell);
        ResetOptionBorders(playerIndex);
    }

    /// <summary>
    /// Set a track strip Image anchors to show direction from/to center
    /// Strip goes from edge to center (entry) or center to edge (exit)
    /// dir: (1,0)=down, (-1,0)=up, (0,1)=right, (0,-1)=left
    /// </summary>
    private void SetTrackStrip(Image[] strips, int idx, Vector2Int dir, bool isEntry)
    {
        if (idx >= strips.Length || strips[idx] == null) return;

        strips[idx].gameObject.SetActive(true);
        strips[idx].color = trackStripColor;

        RectTransform rt = strips[idx].rectTransform;
        float thickness = 0.3f; // strip width as fraction of cell
        float halfT = thickness / 2f;

        // Calculate anchors: strip goes from edge to center (entry) or center to edge (exit)
        if (dir.y != 0) // horizontal (left/right)
        {
            float yMin = 0.5f - halfT;
            float yMax = 0.5f + halfT;
            if (isEntry)
            {
                // From edge to center
                float xMin = dir.y > 0 ? 0f : 0.5f;
                float xMax = dir.y > 0 ? 0.5f : 1f;
                SetRect(rt, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            }
            else
            {
                // From center to edge
                float xMin = dir.y > 0 ? 0.5f : 0f;
                float xMax = dir.y > 0 ? 1f : 0.5f;
                SetRect(rt, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            }
        }
        else // vertical (up/down)
        {
            float xMin = 0.5f - halfT;
            float xMax = 0.5f + halfT;
            if (isEntry)
            {
                // dir.x > 0 means moving down (row increases = visual down)
                // In UI: y=0 is bottom, y=1 is top
                // Moving down means entering from top (y=1)
                float yMin = dir.x > 0 ? 0.5f : 0f;
                float yMax = dir.x > 0 ? 1f : 0.5f;
                SetRect(rt, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            }
            else
            {
                float yMin = dir.x > 0 ? 0f : 0.5f;
                float yMax = dir.x > 0 ? 0.5f : 1f;
                SetRect(rt, new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            }
        }
    }

    private void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Get a direction arrow symbol for a track cell
    /// </summary>
    private string GetDirectionArrow(TrainPathPuzzle puzzle, int row, int col)
    {
        int key = row * TrainPathPuzzle.GRID_SIZE + col;
        if (!puzzle.CellDirections.ContainsKey(key)) return "";

        Vector2Int[] dirs = puzzle.CellDirections[key];
        Vector2Int outDir = dirs[1];

        // Show exit direction arrow
        if (outDir.x > 0) return "v";
        if (outDir.x < 0) return "^";
        if (outDir.y > 0) return ">";
        if (outDir.y < 0) return "<";
        return "";
    }

    private void ShowTrainAtCell(int playerIndex, int row, int col)
    {
        Image trainIcon = playerIndex == 0 ? p1TrainIcon : p2TrainIcon;
        Image[] cells = playerIndex == 0 ? p1GridCells : p2GridCells;

        if (trainIcon == null) return;

        int idx = row * TrainPathPuzzle.GRID_SIZE + col;
        if (idx >= cells.Length || cells[idx] == null) return;

        trainIcon.gameObject.SetActive(true);
        // Use localPosition since train and cells are siblings in gridContainer
        trainIcon.rectTransform.localPosition = cells[idx].rectTransform.localPosition;
    }

    private void ShowEndMarker(int playerIndex, PathExitSide exitSide, Vector2Int exitEdgeCell)
    {
        Image[] markers = playerIndex == 0 ? p1EndMarkers : p2EndMarkers;
        Image[] cells = playerIndex == 0 ? p1GridCells : p2GridCells;
        HideEndMarkers(playerIndex);

        int sideIdx = (int)exitSide;
        if (sideIdx < markers.Length && markers[sideIdx] != null)
        {
            markers[sideIdx].gameObject.SetActive(true);
            markers[sideIdx].color = (endpointSprite != null) ? Color.white : endMarkerColor;

            // Position OUTSIDE the map, aligned with the exit cell
            int exitIdx = exitEdgeCell.x * TrainPathPuzzle.GRID_SIZE + exitEdgeCell.y;
            if (exitIdx >= 0 && exitIdx < cells.Length && cells[exitIdx] != null)
            {
                RectTransform markerRT = markers[sideIdx].GetComponent<RectTransform>();
                RectTransform cellRT = cells[exitIdx].rectTransform;

                markerRT.anchorMin = new Vector2(0.5f, 0.5f);
                markerRT.anchorMax = new Vector2(0.5f, 0.5f);
                markerRT.sizeDelta = new Vector2(52f, 52f);

                // Get cell position in gridArea local space
                Transform gridAreaT = markerRT.parent;
                Vector3 cellLocal = gridAreaT.InverseTransformPoint(cellRT.position);

                // Offset outside the grid edge based on exit side
                float offset = 35f; // pixels outside the grid
                switch (exitSide)
                {
                    case PathExitSide.Top:
                        cellLocal.y += offset;
                        break;
                    case PathExitSide.Bottom:
                        cellLocal.y -= offset;
                        break;
                    case PathExitSide.Left:
                        cellLocal.x -= offset;
                        break;
                    case PathExitSide.Right:
                        cellLocal.x += offset;
                        break;
                }
                markerRT.localPosition = cellLocal;
            }

            // Hide text when using sprite
            Text[] texts = playerIndex == 0 ? p1EndMarkerTexts : p2EndMarkerTexts;
            if (endpointSprite != null && sideIdx < texts.Length && texts[sideIdx] != null)
                texts[sideIdx].text = "";
        }
    }

    private void HideEndMarkers(int playerIndex)
    {
        Image[] markers = playerIndex == 0 ? p1EndMarkers : p2EndMarkers;
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) markers[i].gameObject.SetActive(false);
    }

    private void HideAllEndMarkers()
    {
        HideEndMarkers(0);
        HideEndMarkers(1);
    }

    /// <summary>
    /// Pick the correct tile sprite based on cell type and direction.
    /// Straight: vertical or horizontal.
    /// Turn: pick corner sprite based on which 2 edges are connected.
    /// </summary>
    private Sprite GetTileSprite(TrainCellType cellType, TrainPathPuzzle puzzle, int idx)
    {
        if (cellType == TrainCellType.Bush)
            return tileBush;

        if (cellType == TrainCellType.Straight)
        {
            if (puzzle.CellDirections.ContainsKey(idx))
            {
                Vector2Int inDir = puzzle.CellDirections[idx][0];
                if (inDir.x != 0) return tileStraightV; // up/down
                return tileStraightH;                     // left/right
            }
            return tileStraightV;
        }

        // Turn — determine which 2 edges of the cell are connected
        // inDir = direction vector of movement entering this cell
        //   (1,0)=moving down → enters from TOP edge
        //   (-1,0)=moving up → enters from BOTTOM edge
        //   (0,1)=moving right → enters from LEFT edge
        //   (0,-1)=moving left → enters from RIGHT edge
        // outDir = direction vector leaving this cell
        //   (1,0)=moving down → exits from BOTTOM edge
        //   (-1,0)=moving up → exits from TOP edge
        //   (0,1)=moving right → exits from RIGHT edge
        //   (0,-1)=moving left → exits from LEFT edge
        if (puzzle.CellDirections.ContainsKey(idx))
        {
            Vector2Int inDir = puzzle.CellDirections[idx][0];
            Vector2Int outDir = puzzle.CellDirections[idx][1];

            bool hasTop = (inDir.x == 1) || (outDir.x == -1);     // enters from top OR exits to top
            bool hasBottom = (inDir.x == -1) || (outDir.x == 1);   // enters from bottom OR exits to bottom
            bool hasLeft = (inDir.y == 1) || (outDir.y == -1);     // enters from left OR exits to left
            bool hasRight = (inDir.y == -1) || (outDir.y == 1);    // enters from right OR exits to right

            if (hasTop && hasLeft) return tileCornerTopRight;
            if (hasTop && hasRight) return tileCornerTopLeft;
            if (hasBottom && hasRight) return tileCornerBottomLeft;
            if (hasBottom && hasLeft) return tileCornerBottomRight;
        }

        return tileStraightV; // fallback
    }

    private void ApplyEndpointSprites(Image[] markers)
    {
        if (markers == null || endpointSprite == null) return;
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                markers[i].sprite = endpointSprite;
                markers[i].preserveAspect = true;
                markers[i].color = Color.white;
            }
        }
    }

    private void HideTrainIcons()
    {
        if (p1TrainIcon != null) p1TrainIcon.gameObject.SetActive(false);
        if (p2TrainIcon != null) p2TrainIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// Reveal the hidden cell after answer - show track strips
    /// </summary>
    public void RevealHiddenCell(int playerIndex, TrainPathPuzzle puzzle, bool wasCorrect)
    {
        Image[] cells = playerIndex == 0 ? p1GridCells : p2GridCells;
        Text[] texts = playerIndex == 0 ? p1GridTexts : p2GridTexts;
        Image[] stripsA = playerIndex == 0 ? p1TrackStripsA : p2TrackStripsA;
        Image[] stripsB = playerIndex == 0 ? p1TrackStripsB : p2TrackStripsB;

        int idx = puzzle.HiddenRow * TrainPathPuzzle.GRID_SIZE + puzzle.HiddenCol;
        if (idx >= cells.Length || cells[idx] == null) return;

        TrainCellType answer = puzzle.HiddenAnswer;
        Color tint = wasCorrect ? new Color(0.7f, 1f, 0.7f, 1f) : new Color(1f, 0.7f, 0.7f, 1f);

        // Reveal with correct tile sprite + tint
        Sprite spr = GetTileSprite(answer, puzzle, idx);
        if (spr != null) { cells[idx].sprite = spr; cells[idx].preserveAspect = false; }
        cells[idx].color = tint;

        if (idx < texts.Length && texts[idx] != null)
        {
            texts[idx].text = "";
            texts[idx].color = Color.white;
            texts[idx].fontSize = 20;
        }
    }

    /// <summary>
    /// Animate the train along the path cells
    /// </summary>
    public void AnimateTrain(int playerIndex, TrainPathPuzzle puzzle)
    {
        if (playerIndex == 0)
        {
            if (_p1AnimCoroutine != null) StopCoroutine(_p1AnimCoroutine);
            _p1AnimCoroutine = StartCoroutine(TrainAnimationCoroutine(playerIndex, puzzle));
        }
        else
        {
            if (_p2AnimCoroutine != null) StopCoroutine(_p2AnimCoroutine);
            _p2AnimCoroutine = StartCoroutine(TrainAnimationCoroutine(playerIndex, puzzle));
        }
    }

    private IEnumerator TrainAnimationCoroutine(int playerIndex, TrainPathPuzzle puzzle)
    {
        Image trainIcon = playerIndex == 0 ? p1TrainIcon : p2TrainIcon;
        Image[] cells = playerIndex == 0 ? p1GridCells : p2GridCells;
        Image[] endMarkers = playerIndex == 0 ? p1EndMarkers : p2EndMarkers;

        if (trainIcon == null || puzzle.PathCells == null || puzzle.PathCells.Count < 2)
            yield break;

        trainIcon.gameObject.SetActive(true);

        // Use localPosition since train and cells share gridContainer as parent
        for (int i = 0; i < puzzle.PathCells.Count - 1; i++)
        {
            Vector2Int from = puzzle.PathCells[i];
            Vector2Int to = puzzle.PathCells[i + 1];
            int fromIdx = from.x * TrainPathPuzzle.GRID_SIZE + from.y;
            int toIdx = to.x * TrainPathPuzzle.GRID_SIZE + to.y;

            if (fromIdx >= cells.Length || toIdx >= cells.Length) continue;
            if (cells[fromIdx] == null || cells[toIdx] == null) continue;

            Vector3 startLocal = cells[fromIdx].rectTransform.localPosition;
            Vector3 endLocal = cells[toIdx].rectTransform.localPosition;

            float elapsed = 0f;
            while (elapsed < trainMoveSpeed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / trainMoveSpeed);
                trainIcon.rectTransform.localPosition = Vector3.Lerp(startLocal, endLocal, t);
                yield return null;
            }
            trainIcon.rectTransform.localPosition = endLocal;
        }

        // Move to end marker (convert end marker position to gridContainer local space)
        int exitSideIdx = (int)puzzle.ExitSide;
        if (exitSideIdx < endMarkers.Length && endMarkers[exitSideIdx] != null
            && endMarkers[exitSideIdx].gameObject.activeSelf)
        {
            Vector3 lastLocal = trainIcon.rectTransform.localPosition;
            // End marker is child of gridArea, train is child of gridContainer
            // Convert end marker world pos to gridContainer local space
            Transform gridContainer = trainIcon.rectTransform.parent;
            Vector3 endMarkerLocal = gridContainer.InverseTransformPoint(
                endMarkers[exitSideIdx].rectTransform.position);

            float elapsed = 0f;
            while (elapsed < trainMoveSpeed)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / trainMoveSpeed);
                trainIcon.rectTransform.localPosition = Vector3.Lerp(lastLocal, endMarkerLocal, t);
                yield return null;
            }
            trainIcon.rectTransform.localPosition = endMarkerLocal;
        }
    }

    public void StopTrainAnimation(int playerIndex)
    {
        if (playerIndex == 0 && _p1AnimCoroutine != null)
        {
            StopCoroutine(_p1AnimCoroutine);
            _p1AnimCoroutine = null;
        }
        else if (playerIndex == 1 && _p2AnimCoroutine != null)
        {
            StopCoroutine(_p2AnimCoroutine);
            _p2AnimCoroutine = null;
        }
    }

    public void ResetOptionBorders(int playerIndex)
    {
        if (playerIndex == 0)
        {
            if (p1OptionLeftBorder != null) p1OptionLeftBorder.color = normalBorderColor;
            if (p1OptionStraightBorder != null) p1OptionStraightBorder.color = normalBorderColor;
            if (p1OptionRightBorder != null) p1OptionRightBorder.color = normalBorderColor;
        }
        else
        {
            if (p2OptionLeftBorder != null) p2OptionLeftBorder.color = normalBorderColor;
            if (p2OptionStraightBorder != null) p2OptionStraightBorder.color = normalBorderColor;
            if (p2OptionRightBorder != null) p2OptionRightBorder.color = normalBorderColor;
        }
    }

    public void UpdateOptionSelection(int playerIndex, int selectedIndex)
    {
        Image[] borders = GetOptionBorders(playerIndex);
        for (int i = 0; i < 3; i++)
        {
            if (borders[i] != null)
                borders[i].color = (i == selectedIndex) ? selectedBorderColor : normalBorderColor;
        }
    }

    public void SetCorrectAnswerBorderColor(int playerIndex, int selectedIndex)
    {
        Image[] borders = GetOptionBorders(playerIndex);
        if (selectedIndex >= 0 && selectedIndex < 3 && borders[selectedIndex] != null)
            borders[selectedIndex].color = correctBorderColor;
    }

    public void SetWrongAnswerBorderColor(int playerIndex, int selectedIndex, int correctIndex)
    {
        Image[] borders = GetOptionBorders(playerIndex);
        if (selectedIndex >= 0 && selectedIndex < 3 && borders[selectedIndex] != null)
            borders[selectedIndex].color = wrongBorderColor;
        if (correctIndex >= 0 && correctIndex < 3 && borders[correctIndex] != null)
            borders[correctIndex].color = correctBorderColor;
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

    public void SetPlayerOptionsInteractable(int playerIndex, bool interactable)
    {
        if (playerIndex == 0)
        {
            if (p1OptionLeftBtn != null) p1OptionLeftBtn.interactable = interactable;
            if (p1OptionStraightBtn != null) p1OptionStraightBtn.interactable = interactable;
            if (p1OptionRightBtn != null) p1OptionRightBtn.interactable = interactable;
        }
        else
        {
            if (p2OptionLeftBtn != null) p2OptionLeftBtn.interactable = interactable;
            if (p2OptionStraightBtn != null) p2OptionStraightBtn.interactable = interactable;
            if (p2OptionRightBtn != null) p2OptionRightBtn.interactable = interactable;
        }
    }

    public void ShowGameOverPanel(int p1Score, int p2Score) { }
    public void HideGameOverPanel() { }

    // ===== Grid Rotation =====

    /// <summary>
    /// Set the grid to a specific rotation angle (used when loading a new puzzle)
    /// </summary>
    public void SetGridRotation(int playerIndex, float angleDegrees)
    {
        RectTransform gridArea = playerIndex == 0 ? p1GridArea : p2GridArea;
        if (gridArea != null)
            gridArea.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
    }

    /// <summary>
    /// Continuously rotate the grid (called from Controller Update)
    /// </summary>
    public void UpdateGridRotation(int playerIndex, float deltaTime)
    {
        RectTransform gridArea = playerIndex == 0 ? p1GridArea : p2GridArea;
        if (gridArea != null)
            gridArea.Rotate(0f, 0f, _gridRotationSpeed * deltaTime);
    }

    /// <summary>
    /// Get current grid rotation angle
    /// </summary>
    public float GetGridRotation(int playerIndex)
    {
        RectTransform gridArea = playerIndex == 0 ? p1GridArea : p2GridArea;
        if (gridArea != null)
            return gridArea.localEulerAngles.z;
        return 0f;
    }

    private Image[] GetOptionBorders(int playerIndex)
    {
        if (playerIndex == 0)
            return new Image[] { p1OptionLeftBorder, p1OptionStraightBorder, p1OptionRightBorder };
        else
            return new Image[] { p2OptionLeftBorder, p2OptionStraightBorder, p2OptionRightBorder };
    }

    // ===== Countdown (Team Mode) =====

    //NamNN change with Android Studio Agent
    public void HideQuestion(int playerIndex)
    {
        RectTransform gridArea = playerIndex == 0 ? p1GridArea : p2GridArea;
        if (gridArea != null) gridArea.gameObject.SetActive(false);

        if (playerIndex == 0)
        {
            if (p1OptionLeftBtn != null) p1OptionLeftBtn.gameObject.SetActive(false);
            if (p1OptionStraightBtn != null) p1OptionStraightBtn.gameObject.SetActive(false);
            if (p1OptionRightBtn != null) p1OptionRightBtn.gameObject.SetActive(false);
            if (p1TrainIcon != null) p1TrainIcon.gameObject.SetActive(false);
        }
        else
        {
            if (p2OptionLeftBtn != null) p2OptionLeftBtn.gameObject.SetActive(false);
            if (p2OptionStraightBtn != null) p2OptionStraightBtn.gameObject.SetActive(false);
            if (p2OptionRightBtn != null) p2OptionRightBtn.gameObject.SetActive(false);
            if (p2TrainIcon != null) p2TrainIcon.gameObject.SetActive(false);
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
