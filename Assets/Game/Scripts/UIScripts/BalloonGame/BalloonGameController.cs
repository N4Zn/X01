using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum BalloonGameSceneState
{
    Initialize,
    ShowRound,
    Popping,
    RoundClear,
    GameOver
}

/// <summary>
/// FSM controller cho BalloonGame.
///
/// Flow: Initialize → ShowRound → Popping → RoundClear ↩ ShowRound
///                                        ↓ (timer hết / hết chữ)
///                                      GameOver → ScoreScene
///
/// Scene hierarchy (manual setup):
///   Canvas
///   ├── LeftField   [BalloonField] — nửa trái, 12 BalloonItem con
///   ├── RightField  [BalloonField] — nửa phải, 12 BalloonItem con
///   ├── CenterStrip [BalloonLetterDisplay]
///   ├── GameHUD     [GameHUD]
///   └── RoundClearPanel
///       └── RoundClearLabel [TextMeshProUGUI]
///
/// Sequence: BalloonAlpha → A-Z (26 rounds tối đa)
///           BalloonNumber → 0-9 (10 rounds tối đa)
/// Timer kết thúc game trước khi hết sequence nếu cần.
///
/// Scoring: mỗi correct pop +1 cho team đó (ScoreManager → GameHUD live update).
/// </summary>
public class BalloonGameController : MonoBehaviour
{
    [Header("Fields")]
    [SerializeField] BalloonField         leftField;
    [SerializeField] BalloonField         rightField;

    [Header("Center display")]
    [SerializeField] BalloonLetterDisplay centerDisplay;

    [Header("HUD")]
    [SerializeField] GameHUD              gameHud;

    [Header("Round clear UI")]
    [SerializeField] GameObject           roundClearPanel;
    [SerializeField] TextMeshProUGUI      roundClearLabel;

    [Header("Config")]
    [SerializeField] BalloonGameConfig config;

    [Header("Letter assets (index = sequence position, optional)")]
    [Tooltip("Sprite[0]=A/0, Sprite[1]=B/1 ... dùng làm Mask cho fill bar")]
    [SerializeField] Sprite[]    letterSprites;
    [Tooltip("AudioClip[0]=A/0 ... phát lặp trong round")]
    [SerializeField] AudioClip[] letterAudios;

    // ── Sequences ─────────────────────────────────────────────────────────────

    static readonly string[] AlphaSeq =
    {
        "A","B","C","D","E","F","G","H","I","J","K","L","M",
        "N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
    };
    static readonly string[] NumSeq =
    {
        "0","1","2","3","4","5","6","7","8","9"
    };

    // ── Runtime ───────────────────────────────────────────────────────────────

    CustomFSMManager _fsm;
    ScoreManager     _score;
    string[]         _sequence;
    int              _roundIdx;
    float            _timer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (config == null)
            config = Resources.Load<BalloonGameConfig>("GameConfig/BalloonGameConfig");
        if (config == null)
        {
            Debug.LogError("[BalloonGameController] BalloonGameConfig null. Chạy Tools > BalloonGame > Build Scene.", this);
            return;
        }

        string gameName = GameSessionManager.Instance?.SelectedGameName ?? "BalloonAlpha";
        _sequence = gameName == "BalloonNumber" ? NumSeq : AlphaSeq;
        _roundIdx = 0;

        _score = new ScoreManager();

        float cfgTime = GameSettings.Instance != null ? GameSettings.Instance.GameTime : config.defaultGameTime;
        _timer = cfgTime;

        _fsm = gameObject.AddComponent<CustomFSMManager>();
        _fsm.fsmName = "BalloonGameFSM";
        _fsm.Initialize(typeof(BalloonGameSceneState), typeof(BalloonGameController), false);
        _fsm.StateMachineChange(BalloonGameSceneState.Initialize);
    }

    void Update()
    {
        if (GetState() != BalloonGameSceneState.Popping) return;

        _timer -= Time.deltaTime;
        gameHud?.UpdateTimer(_timer);

        if (_timer <= 0f)
        {
            _timer = 0f;
            _fsm.StateMachineChange(BalloonGameSceneState.GameOver);
        }
    }

    // ── FSM: Initialize ───────────────────────────────────────────────────────

    protected void StateMachineEnter_Initialize(System.Enum prev, Dictionary<string, object> opts)
    {
        string p1   = GameSessionManager.Instance?.GetDisplayName1() ?? "Player 1";
        string p2   = GameSessionManager.Instance?.GetDisplayName2() ?? "Player 2";
        int    maxS = _sequence.Length * 5; // ~5 pops per team per round, ước tính
        gameHud?.Initialize(_score, p1, p2, maxS);

        if (roundClearPanel != null) roundClearPanel.SetActive(false);
        centerDisplay?.Hide();

        _fsm.StateMachineChange(BalloonGameSceneState.ShowRound);
    }

    protected void StateMachineExit_Initialize(System.Enum next, Dictionary<string, object> opts) { }

    // ── FSM: ShowRound ────────────────────────────────────────────────────────

    protected void StateMachineEnter_ShowRound(System.Enum prev, Dictionary<string, object> opts)
    {
        if (_roundIdx >= _sequence.Length)
        {
            _fsm.StateMachineChange(BalloonGameSceneState.GameOver);
            return;
        }

        string letter = _sequence[_roundIdx];

        Sprite    sprite = (letterSprites != null && _roundIdx < letterSprites.Length)
                           ? letterSprites[_roundIdx] : null;
        AudioClip audio  = Resources.Load<AudioClip>("Audio/ChuCai/" + letter.ToLower());

        centerDisplay.ShowRound(letter, sprite, audio);

        string[] wrongPool = BuildWrongPool(letter, 10);
        leftField.Setup( letter, wrongPool, Team.Left,  OnCorrectPop);
        rightField.Setup(letter, wrongPool, Team.Right, OnCorrectPop);

        _fsm.StateMachineChange(BalloonGameSceneState.Popping);
    }

    protected void StateMachineExit_ShowRound(System.Enum next, Dictionary<string, object> opts) { }

    // ── FSM: Popping ──────────────────────────────────────────────────────────

    protected void StateMachineEnter_Popping(System.Enum prev, Dictionary<string, object> opts) { }

    protected void StateMachineExit_Popping(System.Enum next, Dictionary<string, object> opts) { }

    // ── FSM: RoundClear ───────────────────────────────────────────────────────

    protected void StateMachineEnter_RoundClear(System.Enum prev, Dictionary<string, object> opts)
    {
        leftField.StopField();
        rightField.StopField();
        centerDisplay.StopAudio();

        string completedLetter = _sequence[_roundIdx]; // lấy trước khi tăng
        _roundIdx++;

        if (roundClearPanel != null) roundClearPanel.SetActive(true);

        StartCoroutine(RoundClearRoutine());
    }

    protected void StateMachineExit_RoundClear(System.Enum next, Dictionary<string, object> opts)
    {
        if (roundClearPanel != null) roundClearPanel.SetActive(false);
    }

    // ── FSM: GameOver ─────────────────────────────────────────────────────────

    protected void StateMachineEnter_GameOver(System.Enum prev, Dictionary<string, object> opts)
    {
        leftField.StopField();
        rightField.StopField();
        centerDisplay.Hide();
        if (roundClearPanel != null) roundClearPanel.SetActive(false);

        var session = GameSessionManager.Instance;
        if (session != null)
        {
            session.RecordScores(_score.ScoreLeft, _score.ScoreRight);
            session.LastPlayedGame = session.SelectedGameName ?? "BalloonAlpha";
        }

        SceneManager.LoadScene("ScoreScene");
    }

    protected void StateMachineExit_GameOver(System.Enum next, Dictionary<string, object> opts) { }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    void OnCorrectPop(Team team)
    {
        // FSM guard: tránh double-fire nếu 2 pop đến cùng frame
        if (GetState() != BalloonGameSceneState.Popping) return;

        centerDisplay.RegisterPop(team);
        _score.AddPoint(team); // cập nhật HUD qua ScoreManager.OnScoreChanged

        if (centerDisplay.IsComplete)
            _fsm.StateMachineChange(BalloonGameSceneState.RoundClear);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    IEnumerator RoundClearRoutine()
    {
        float delay = GameSettings.Instance != null ? GameSettings.Instance.RoundEndDelay : config.roundClearDuration;
        int countSeconds = Mathf.Max(1, Mathf.RoundToInt(delay));
        for (int i = countSeconds; i >= 1; i--)
        {
            if (roundClearLabel != null) roundClearLabel.text = "Next in " + i + "s";
            yield return new WaitForSeconds(1f);
        }
        _fsm.StateMachineChange(BalloonGameSceneState.ShowRound);
    }

    BalloonGameSceneState GetState()
    {
        var s = _fsm?.GetCurrentState();
        return s == null ? BalloonGameSceneState.Initialize : (BalloonGameSceneState)s;
    }

    /// <summary>Trả về tối đa <c>count</c> ký tự sai (không phải <c>correct</c>), đã shuffle.</summary>
    string[] BuildWrongPool(string correct, int count)
    {
        var pool = new List<string>(_sequence.Length);
        foreach (var s in _sequence)
            if (s != correct) pool.Add(s);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int take = Mathf.Min(count, pool.Count);
        return pool.GetRange(0, take).ToArray();
    }
}
