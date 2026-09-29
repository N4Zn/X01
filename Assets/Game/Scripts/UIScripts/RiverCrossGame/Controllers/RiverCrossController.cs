using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller game River Cross — vận động chiếu sàn.
///
/// Flow: Countdown → Playing (vô hạn) → TimeUp → MenuScene
///
/// Chơi liên tục, nhiều người:
///   • Qua sông → "ting ting" + đếm số lần, reset bờ trái, chơi tiếp ngay
///   • Rơi nước → hiệu ứng splash, quay bờ trái, không mất điểm
///   • Timer từ GameSettings.GameTime (chung toàn app)
///   • Khi hết giờ → hiện kết quả → về MenuScene
/// </summary>
public class RiverCrossController : MonoBehaviour
{
    // ── Inspector refs ────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] RiverCrossConfigData config = new RiverCrossConfigData();

    [Header("Scene layout")]
    [SerializeField] Canvas        mainCanvas;
    [SerializeField] RectTransform laneContainer;
    [SerializeField] PlayerAvatar  player;

    [Header("Prefabs")]
    [SerializeField] RaftLane raftLanePrefab;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI countdownText;   // 3-2-1-GO
    [SerializeField] TextMeshProUGUI timerText;        // thời gian còn lại
    [SerializeField] TextMeshProUGUI crossCountText;   // "Đã qua: N lần"
    [SerializeField] TextMeshProUGUI celebrationText;  // "🎉" ting ting flash
    [SerializeField] GameObject      resultPanel;
    [SerializeField] TextMeshProUGUI resultText;
    [SerializeField] GameObject      splashEffect;     // particle, optional

    // ── Internal state ────────────────────────────────────────────────────────

    RiverCrossConfigData _cfg;

    RaftLane[] _lanes;
    float[]    _laneX;

    int      _currentLane;   // -1=bờ trái | 0..N-1=lane | N=bờ phải
    float    _playerY;
    RaftItem _mountedRaft;

    float _timeRemaining;
    int   _crossCount;
    bool  _isPlaying;
    bool  _inputLocked;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _cfg = config;

        // Fallback: nếu lanes chưa được serialize (scene cũ / mới tạo component)
        if (_cfg.lanes == null || _cfg.lanes.Length == 0)
            _cfg.lanes = RiverCrossConfigData.DefaultLanes();

        SetupLanes();
    }

    void Start() => StartCoroutine(RunGame());

    void Update()
    {
        if (!_isPlaying || _inputLocked) return;

        // Đếm ngược timer (lấy từ GameSettings)
        _timeRemaining -= Time.deltaTime;
        UpdateTimerUI();

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _isPlaying     = false;
            StartCoroutine(TimeUp());
            return;
        }

        TrackMountedRaft();
        ReadInput();
    }

    // ── Lane setup ────────────────────────────────────────────────────────────

    void SetupLanes()
    {
        int n = _cfg.lanes.Length;

        // Vùng phân bổ lane: lùi vào từ tâm mỗi bờ một khoảng laneAreaPadding
        // → lane đầu/cuối cách bờ bằng đúng khoảng cách giữa các lane
        float laneLeft  = _cfg.leftBankX  + _cfg.laneAreaPadding;
        float laneRight = _cfg.rightBankX - _cfg.laneAreaPadding;
        float step      = (laneRight - laneLeft) / (n + 1);

        _lanes = new RaftLane[n];
        _laneX = new float[n];

        // rect.height có thể = 0 trong Awake (trước layout pass)
        // → dùng canvas reference height cố định (1024×600 theo ProjectSettings)
        const float CANVAS_HALF_H = 300f;
        float spawnY = CANVAS_HALF_H + _cfg.laneSpawnBuffer;

        for (int i = 0; i < n; i++)
        {
            float x   = laneLeft + step * (i + 1);
            _laneX[i] = x;

            var lane = Instantiate(raftLanePrefab, laneContainer);
            lane.Init(x, _cfg.lanes[i], _cfg.raftThickness, _cfg.landingTolerance, spawnY);
            _lanes[i] = lane;
        }
    }

    // ── Game loop ─────────────────────────────────────────────────────────────

    IEnumerator RunGame()
    {
        if (resultPanel)    resultPanel.SetActive(false);
        if (celebrationText) celebrationText.gameObject.SetActive(false);

        // Lấy thời gian từ setting chung
        _timeRemaining = GameSettings.Instance != null
                         ? GameSettings.Instance.GameTime
                         : 300f;
        _crossCount    = 0;

        ResetPlayerToLeftBank();
        UpdateTimerUI();
        UpdateCrossUI();

        yield return StartCoroutine(Countdown());

        _isPlaying = true;
    }

    IEnumerator Countdown()
    {
        if (countdownText) countdownText.gameObject.SetActive(true);

        for (int i = _cfg.countdownSeconds; i >= 1; i--)
        {
            if (countdownText) countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText)
        {
            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.5f);
            countdownText.gameObject.SetActive(false);
        }
    }

    // ── Raft tracking ─────────────────────────────────────────────────────────

    void TrackMountedRaft()
    {
        if (_currentLane < 0 || _currentLane >= _lanes.Length) return;

        if (_mountedRaft != null && !_mountedRaft.IsAlive)
        {
            // Bè trôi khỏi màn hình → rơi xuống nước
            _mountedRaft = null;
            StartCoroutine(HandleFall(player.X, _playerY - 40f));
            return;
        }

        if (_mountedRaft != null)
        {
            _playerY = _mountedRaft.CenterY;
            player.SetY(_playerY);
        }
        else
        {
            _mountedRaft = _lanes[_currentLane].GetRaftUnder(_playerY);
            if (_mountedRaft != null)
            {
                _playerY = _mountedRaft.CenterY;
                player.SetY(_playerY);
            }
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void ReadInput()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            ProcessTouchAt(Input.mousePosition);
#else
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began)
                ProcessTouchAt(Input.GetTouch(i).position);
#endif
    }

    void ProcessTouchAt(Vector2 screenPos)
    {
        if (!_isPlaying || _inputLocked || player.IsJumping) return;

        Vector2 cp = ScreenToCanvas(screenPos);
        float   tx = cp.x, ty = cp.y;

        int targetLane = GetLaneFromX(tx);

        // Không được nhảy cóc về phía trước — chỉ tiến 1 lane mỗi lần
        // (nhảy lùi tự do: targetLane < _currentLane là hợp lệ)
        if (targetLane > _currentLane + 1) return;

        // Bờ phải → QUA SÔNG
        if (targetLane >= _cfg.lanes.Length)
        {
            StartCoroutine(HandleCrossSuccess());
            return;
        }

        // Bờ trái → đất liền, luôn an toàn
        if (targetLane == -1)
        {
            StartCoroutine(HandleJump(-1, _cfg.leftBankX, _playerY));
            return;
        }

        // Có bè → nhảy; không có → rơi
        if (_lanes[targetLane].HasRaftAt(ty))
            StartCoroutine(HandleJump(targetLane, _laneX[targetLane], _lanes[targetLane].GetRaftCenterY(ty)));
        else
            StartCoroutine(HandleFall(tx, ty));
    }

    // ── Jump ─────────────────────────────────────────────────────────────────

    IEnumerator HandleJump(int targetLane, float targetX, float landY)
    {
        _inputLocked = true;
        bool done    = false;

        player.JumpTo(targetX, landY, _cfg.jumpDuration, _cfg.arcHeight, () => done = true);
        yield return new WaitUntil(() => done);

        _currentLane = targetLane;
        _playerY     = landY;
        _mountedRaft = (targetLane >= 0 && targetLane < _lanes.Length)
                       ? _lanes[targetLane].GetRaftUnder(landY)
                       : null;
        _inputLocked = false;
    }

    // ── Rơi xuống nước — chỉ hiệu ứng, không mất điểm ───────────────────────

    IEnumerator HandleFall(float touchX, float touchY)
    {
        _inputLocked = true;
        _mountedRaft = null;

        // Nhảy ngắn đến điểm rơi
        bool done = false;
        player.JumpTo(touchX, touchY, _cfg.jumpDuration * 0.5f, _cfg.fallArcHeight, () => done = true);
        yield return new WaitUntil(() => done);

        // Rơi xuống
        done = false;
        player.FallIntoWater(_cfg.fallDuration, _cfg.fallAcceleration, () => done = true);
        yield return new WaitUntil(() => done);

        // Hiệu ứng splash particle
        SpawnSplash();

        yield return new WaitForSeconds(0.5f);

        // Reset bờ trái — tiếp tục chơi ngay, không trừ điểm
        ResetPlayerToLeftBank();
        _inputLocked = false;
    }

    // ── Qua sông thành công ───────────────────────────────────────────────────

    IEnumerator HandleCrossSuccess()
    {
        _inputLocked = true;

        // Nhảy đến bờ phải
        bool done = false;
        player.JumpTo(_cfg.rightBankX, _playerY, _cfg.jumpDuration, _cfg.arcHeight, () => done = true);
        yield return new WaitUntil(() => done);

        // Đếm + âm thanh ting ting
        _crossCount++;
        UpdateCrossUI();
        MusicManager.Instance?.PlayCorrectSfx();

        // Flash "🎉" ngắn
        yield return StartCoroutine(ShowCelebration());

        // Reset bờ trái — chơi tiếp ngay
        ResetPlayerToLeftBank();
        _inputLocked = false;
    }

    IEnumerator ShowCelebration()
    {
        if (celebrationText == null) yield break;

        celebrationText.text = $"🎉 Qua sông! ({_crossCount} lần)";
        celebrationText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        celebrationText.gameObject.SetActive(false);
    }

    // ── Hết giờ ──────────────────────────────────────────────────────────────

    IEnumerator TimeUp()
    {
        _inputLocked = true;

        if (resultPanel) resultPanel.SetActive(true);
        if (resultText)
            resultText.text = $"Hết giờ!\nĐã qua sông: {_crossCount} lần 🎉";

        yield return new WaitForSeconds(_cfg.resultDisplaySeconds);

        SceneManager.LoadScene("MenuScene");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void ResetPlayerToLeftBank()
    {
        _currentLane = -1;
        _playerY     = 0f;
        _mountedRaft = null;
        player.SetPosition(_cfg.leftBankX, _playerY);
    }

    void SpawnSplash()
    {
        if (splashEffect == null) return;
        // Spawn ở vị trí hiện tại của player (canvas-space → world-space)
        var canvasRT = mainCanvas != null
                       ? mainCanvas.transform as RectTransform
                       : null;
        Vector3 worldPos = canvasRT != null
                           ? canvasRT.TransformPoint(new Vector3(player.X, player.Y, 0f))
                           : Vector3.zero;
        Instantiate(splashEffect, worldPos, Quaternion.identity);
    }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int sec = Mathf.CeilToInt(_timeRemaining);
        timerText.text = $"{sec / 60:00}:{sec % 60:00}";
        timerText.color = sec <= 10 ? Color.red : Color.white;
    }

    void UpdateCrossUI()
    {
        if (crossCountText)
            crossCountText.text = $"Đã qua: {_crossCount} lần";
    }

    // ── Coordinate conversion ─────────────────────────────────────────────────

    Vector2 ScreenToCanvas(Vector2 screenPos)
    {
        if (mainCanvas == null) return screenPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mainCanvas.transform as RectTransform,
            screenPos,
            mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera,
            out var lp
        );
        return lp;
    }

    int GetLaneFromX(float x)
    {
        if (x <= _cfg.leftBankX  + _cfg.bankDetectMargin) return -1;
        if (x >= _cfg.rightBankX - _cfg.bankDetectMargin) return _cfg.lanes.Length;

        int   nearest = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < _laneX.Length; i++)
        {
            float d = Mathf.Abs(x - _laneX[i]);
            if (d < minDist) { minDist = d; nearest = i; }
        }
        return nearest;
    }
}
