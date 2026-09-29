using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller game LaneDash — né vật cản (bụi cây/đá/trâu/bò/lợn/gà) kiểu subway-surfer 3D trên
/// cánh đồng, nhặt phần thưởng (bông lúa/hoa) + vật phẩm đặc biệt (ngựa/tên lửa). 2 người chơi
/// mỗi bên nửa màn hình (2 Camera 3D split-screen, tách bằng Layer/cullingMask — xem
/// LaneDashSceneBuilder), mỗi bên 3 làn (trái/giữa/phải), xuất phát ở làn giữa.
///
/// Vận động thuần, không có câu hỏi/CSV — không kế thừa MiniGameControllerBase (xem
/// MiniGameKit/README.md: game liên tục/phi lượt tự lo lấy ScoreManager+GameHUD trực tiếp,
/// giống RiverCrossGame). HUD/countdown/kết quả vẫn là 1 Canvas ScreenSpaceOverlay phủ lên trên 2
/// Camera 3D (đúng cách SolarSystemScene tách UI khỏi world 3D).
///
/// Input KHÔNG cần raycast 3D — vẫn là toán tọa độ màn hình thuần (X màn hình → bên nào → làn
/// nào), chỉ đổi từ canvas-unit (bản 2D cũ) sang screen-pixel-fraction.
///
/// Flow: Countdown → Playing (đến hết GameSettings.GameTime) → ghi điểm → ScoreScene.
/// </summary>
public class LaneDashController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] LaneDashConfigData config = new LaneDashConfigData();

    [Header("World 3D")]
    [SerializeField] LaneTrack trackLeft;
    [SerializeField] LaneTrack trackRight;
    [SerializeField] LaneRunnerAvatar avatarLeft;
    [SerializeField] LaneRunnerAvatar avatarRight;
    [SerializeField] LaneGroundScroller groundScrollLeft;
    [SerializeField] LaneGroundScroller groundScrollRight;

    [Header("HUD")]
    [SerializeField] GameHUD gameHud;

    [Header("UI")]
    [SerializeField] TextMeshProUGUI countdownText;
    [SerializeField] GameObject resultPanel;
    [SerializeField] TextMeshProUGUI resultText;

    [Header("Thông số (tốc độ/quãng đường/phần thưởng)")]
    [SerializeField] TextMeshProUGUI rewardTextLeft;
    [SerializeField] TextMeshProUGUI speedTextLeft;
    [SerializeField] TextMeshProUGUI distanceTextLeft;
    [SerializeField] TextMeshProUGUI rewardTextRight;
    [SerializeField] TextMeshProUGUI speedTextRight;
    [SerializeField] TextMeshProUGUI distanceTextRight;

    const float MPS_TO_KMH = 3.6f;

    LaneDashConfigData _cfg;
    ScoreManager _scoreManager;
    float[] _laneX; // dùng chung cho cả 2 bên — tách bằng Camera cullingMask, không cần offset X
    float _timeRemaining;
    bool _isPlaying;
    int _rewardCountLeft;
    int _rewardCountRight;
    float _laneCooldownRemainingLeft;
    float _laneCooldownRemainingRight;
    float _elapsedPlaying;
    int _currentLevel = -1; // -1 để lần đầu Update() luôn coi là "vừa đổi cấp" (áp dụng level 0 + set pitch nhạc lần đầu)
    int _eventIndexLeft;
    int _eventIndexRight;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _cfg = config;
        _scoreManager = new ScoreManager();
        SetupLanes();
    }

    void Start() => StartCoroutine(RunGame());

    void Update()
    {
        if (!_isPlaying) return;

        _timeRemaining -= Time.deltaTime;
        if (gameHud) gameHud.UpdateTimer(Mathf.Max(_timeRemaining, 0f));

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _isPlaying = false;
            StartCoroutine(TimeUp());
            return;
        }

        if (_laneCooldownRemainingLeft > 0f) _laneCooldownRemainingLeft -= Time.deltaTime;
        if (_laneCooldownRemainingRight > 0f) _laneCooldownRemainingRight -= Time.deltaTime;

        ReadInput();
        SyncStats();
        UpdateLevel();
    }

    /// <summary>Tính cấp độ khó theo thời gian đã chơi (CHUNG cho cả 2 bên, không tính riêng theo
    /// quãng đường mỗi bên) — đẩy vào cả 2 LaneTrack + tăng pitch nhạc nền mỗi khi cấp độ đổi, kiểu
    /// Subway Surfers (càng chơi lâu, world càng trôi nhanh, item càng dày, nhạc càng gấp).</summary>
    void UpdateLevel()
    {
        _elapsedPlaying += Time.deltaTime;
        int level = Mathf.Min(Mathf.FloorToInt(_elapsedPlaying / _cfg.levelDuration), _cfg.maxLevel);
        if (level == _currentLevel) return;

        _currentLevel = level;
        trackLeft?.SetLevel(level);
        trackRight?.SetLevel(level);

        float pitch = Mathf.Min(Mathf.Pow(_cfg.levelMusicPitchMultiplier, level), _cfg.maxMusicPitch);
        MusicManager.Instance?.SetMusicPitch(pitch);
    }

    /// <summary>Đồng bộ scroll mặt đường + hiển thị UI (tốc độ km/h, quãng đường m, số phần
    /// thưởng) theo LaneTrack mỗi bên — vật cản, phần thưởng và mặt đường luôn dừng/tăng tốc cùng
    /// lúc.</summary>
    void SyncStats()
    {
        if (trackLeft)
        {
            float speed = trackLeft.CurrentSpeed;
            groundScrollLeft?.SetSpeed(speed);
            if (speedTextLeft) speedTextLeft.text = $"{speed * MPS_TO_KMH:0.0} km/h";
            if (distanceTextLeft) distanceTextLeft.text = $"{trackLeft.DistanceMeters:0} m";
            if (rewardTextLeft) rewardTextLeft.text = $"{_rewardCountLeft}";
        }
        if (trackRight)
        {
            float speed = trackRight.CurrentSpeed;
            groundScrollRight?.SetSpeed(speed);
            if (speedTextRight) speedTextRight.text = $"{speed * MPS_TO_KMH:0.0} km/h";
            if (distanceTextRight) distanceTextRight.text = $"{trackRight.DistanceMeters:0} m";
            if (rewardTextRight) rewardTextRight.text = $"{_rewardCountRight}";
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupLanes()
    {
        int n = _cfg.laneCount;
        _laneX = new float[n];
        float center = (n - 1) / 2f;
        for (int i = 0; i < n; i++)
            _laneX[i] = (i - center) * _cfg.laneWidth;

        if (trackLeft)
        {
            trackLeft.Init(_cfg, _laneX);
            trackLeft.OnObstacleHit      += () => HandleHit(Team.Left, avatarLeft);
            trackLeft.OnObstacleDodged   += () => HandleDodge(Team.Left);
            trackLeft.OnRewardCollected  += () => HandleReward(Team.Left);
            trackLeft.OnPowerUpCollected += variant => HandlePowerUp(avatarLeft, variant);
        }
        if (trackRight)
        {
            trackRight.Init(_cfg, _laneX);
            trackRight.OnObstacleHit      += () => HandleHit(Team.Right, avatarRight);
            trackRight.OnObstacleDodged   += () => HandleDodge(Team.Right);
            trackRight.OnRewardCollected  += () => HandleReward(Team.Right);
            trackRight.OnPowerUpCollected += variant => HandlePowerUp(avatarRight, variant);
        }
    }

    IEnumerator RunGame()
    {
        if (resultPanel) resultPanel.SetActive(false);

        int centerLane = _cfg.laneCount / 2;
        if (avatarLeft)
        {
            avatarLeft.SetPosition(centerLane, _laneX[centerLane], _cfg.playerGroundY, _cfg.playerZ);
            avatarLeft.transform.localScale = Vector3.one * _cfg.playerScale;
        }
        if (avatarRight)
        {
            avatarRight.SetPosition(centerLane, _laneX[centerLane], _cfg.playerGroundY, _cfg.playerZ);
            avatarRight.transform.localScale = Vector3.one * _cfg.playerScale;
        }
        trackLeft?.SetPlayerLane(centerLane);
        trackRight?.SetPlayerLane(centerLane);

        string leftName  = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName1() : "Left";
        string rightName = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetDisplayName2() : "Right";
        if (gameHud) gameHud.Initialize(_scoreManager, leftName, rightName, _cfg.targetScoreForHud);

        _timeRemaining = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 90f;

        PlayerRecognitionService.Instance.BeginGameSession(GameSessionManager.ResolveActiveGameName("LaneDashGame"));
        yield return StartCoroutine(Countdown());

        _isPlaying = true;
        _elapsedPlaying = 0f;
        _currentLevel = -1;
        trackLeft?.SetRunning(true);
        trackRight?.SetRunning(true);
        MusicManager.Instance?.PlayGameplayMusic();
    }

    IEnumerator Countdown()
    {
        if (countdownText) countdownText.gameObject.SetActive(true);

        // Headless recognition (no camera preview/bounding box) running the whole countdown so
        // both players are identified before the run starts.
        PlayerRecognitionService.Instance.RecognizeSlot(0, _ => RefreshPlayerNames());
        PlayerRecognitionService.Instance.RecognizeSlot(1, _ => RefreshPlayerNames());

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

    void RefreshPlayerNames()
    {
        if (gameHud == null || GameSessionManager.Instance == null) return;
        gameHud.UpdatePlayerNames(GameSessionManager.Instance.GetDisplayName1(), GameSessionManager.Instance.GetDisplayName2());
    }

    // ── Input (thuần screen-space, không raycast 3D) ────────────────────────────

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
        bool isLeftSide = screenPos.x < Screen.width * 0.5f;

        var avatar = isLeftSide ? avatarLeft : avatarRight;
        var track  = isLeftSide ? trackLeft  : trackRight;

        if (avatar == null || track == null || avatar.IsStunned) return;

        bool onCooldown = isLeftSide ? _laneCooldownRemainingLeft > 0f : _laneCooldownRemainingRight > 0f;
        if (onCooldown) return;

        int lane = GetLaneFromX(screenPos.x, isLeftSide);
        int currentLane = avatar.CurrentLane;
        if (lane == currentLane) return;

        // Đang ở làn trái/phải (không phải giữa) thì chỉ được nhảy về làn giữa — không nhảy
        // thẳng sang làn đối diện (trái↔phải) mà phải qua giữa trước.
        int centerLane = _cfg.laneCount / 2;
        if (currentLane != centerLane && lane != centerLane) return;

        avatar.SlideToLane(lane, _laneX[lane], _cfg.laneSwitchDuration);
        track.SetPlayerLane(lane);

        if (isLeftSide) _laneCooldownRemainingLeft = _cfg.laneSwitchCooldown;
        else _laneCooldownRemainingRight = _cfg.laneSwitchCooldown;
    }

    int GetLaneFromX(float x, bool isLeftSide)
    {
        float halfWidth = Screen.width * 0.5f;
        float localX = isLeftSide ? x : x - halfWidth; // 0..halfWidth trong nửa của mình
        int n = _cfg.laneCount;
        int lane = Mathf.FloorToInt(localX / (halfWidth / n));
        return Mathf.Clamp(lane, 0, n - 1);
    }

    // ── Score events ──────────────────────────────────────────────────────────

    void HandleHit(Team team, LaneRunnerAvatar avatar)
    {
        avatar?.PlayStun(_cfg.stunDuration);
        MusicManager.Instance?.PlayWrongSfx();
        LogLaneEvent(team, "obstacle", "hit", false);
    }

    void HandleDodge(Team team)
    {
        _scoreManager.AddPoints(team, _cfg.dodgePoints);
        LogLaneEvent(team, "obstacle", "dodge", true);
    }

    void HandleReward(Team team)
    {
        _scoreManager.AddPoints(team, _cfg.rewardPoints);
        if (team == Team.Left) _rewardCountLeft++; else _rewardCountRight++;
        MusicManager.Instance?.PlayCorrectSfx();
        LogLaneEvent(team, "reward", "collected", true);
    }

    /// <summary>LaneDash chạy liên tục (không có câu hỏi rời rạc) — mỗi sự kiện né/va/thu thập
    /// được ghi thành 1 round-entry riêng, đánh số theo thứ tự xảy ra ở mỗi bên.</summary>
    void LogLaneEvent(Team team, string eventName, string outcome, bool correct)
    {
        int slot = team == Team.Left ? 0 : 1;
        int idx = team == Team.Left ? ++_eventIndexLeft : ++_eventIndexRight;
        PlayerRecognitionService.Instance.LogRound(slot, idx, eventName, outcome, correct, 0f);
    }

    void HandlePowerUp(LaneRunnerAvatar avatar, LaneItemVariant variant)
    {
        if (variant == LaneItemVariant.Horse) avatar?.PlayBoost(_cfg.horseBoostDuration);
        else if (variant == LaneItemVariant.Rocket) avatar?.PlayFly(_cfg.rocketFlyDuration);
        MusicManager.Instance?.PlayCorrectSfx();
    }

    // ── Hết giờ ──────────────────────────────────────────────────────────────

    IEnumerator TimeUp()
    {
        trackLeft?.SetRunning(false);
        trackRight?.SetRunning(false);
        MusicManager.Instance?.StopMusic();

        if (resultPanel) resultPanel.SetActive(true);
        if (resultText)
            resultText.text = $"Hết giờ!\nTrái: {_scoreManager.ScoreLeft}   Phải: {_scoreManager.ScoreRight}";

        GameSessionManager.Instance?.RecordScores(_scoreManager.ScoreLeft, _scoreManager.ScoreRight);

        yield return new WaitForSeconds(_cfg.resultDisplaySeconds);

        SceneManager.LoadScene("ScoreScene");
    }
}
