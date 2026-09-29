using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý vật cản (bụi cây/đá/trâu/bò/lợn/gà), phần thưởng (bông lúa/hoa) và vật phẩm đặc biệt
/// (ngựa/tên lửa) trôi dọc trục Z trên 3 làn của 1 bên (Left hoặc Right) trong world 3D. Cũng là
/// chủ sở hữu DUY NHẤT của "tốc độ" bên đó — vật cản, phần thưởng lẫn vệt cuộn mặt đường
/// (LaneDashController đọc CurrentSpeed để đồng bộ LaneGroundScroller) đều di chuyển theo cùng 1 giá
/// trị này, nên luôn dừng/tăng tốc cùng lúc ("vật cản gắn với mặt đường").
///
/// - Tự spawn theo chu kỳ, làn ngẫu nhiên, biến thể ngẫu nhiên trong nhóm Obstacle/Reward/PowerUp
///   (xem LaneDashItemVisuals). PowerUp hiếm hơn (powerUpChance), tách riêng khỏi tỉ lệ
///   reward/obstacle thường (rewardChance).
/// - Pool theo TỪNG BIẾN THỂ (không dùng 1 pool phẳng như bản 2D cũ) — vì mỗi biến thể có visual
///   3D khác nhau (mesh Kenney hoặc primitive riêng), dựng 1 lần rồi tái dùng đúng biến thể đó.
/// - Tốc độ mục tiêu tăng dần theo thời gian chạy (rampPerSecond), có trần (maxSpeed) — nhân
///   thêm horseBoostMultiplier khi đang cưỡi ngựa (trần cũng nhân theo).
/// - Va vật cản cùng làn → tốc độ về 0 ngay, đứng hình hoàn toàn trong stunDuration giây (không
///   di chuyển, không spawn — "mặt đường cũng dừng lại"), huỷ luôn hiệu lực cưỡi ngựa (ngã ngựa).
///   Hết choáng → tốc độ tăng dần trở lại tốc độ mục tiêu theo recoverAccel (không bật lại ngay
///   lập tức) — mô phỏng "đứng dậy chạy lại". Đang bay (tên lửa) → tự động né mọi vật cản, không
///   bị choáng.
/// - Cộng dồn quãng đường đã chạy (DistanceMeters) mỗi frame theo delta di chuyển thực tế (world
///   unit = mét) — tự dừng cộng khi đứng hình vì delta = 0 lúc đó.
/// - Phát hiện va/né/nhặt bằng 1 lần duy nhất: thời điểm item băng qua playerZ trong frame đó
///   (không dùng vùng dung sai — tránh bỏ sót ở tốc độ cao vì mỗi item chỉ được xử lý đúng 1 lần
///   rồi thu hồi ngay).
/// </summary>
public class LaneTrack : MonoBehaviour
{
    [Tooltip("Unity Layer gán cho vật thể spawn ra — phải khớp cullingMask của Camera bên này " +
             "(xem LaneDashSceneBuilder) để không lẫn sang màn hình bên kia.")]
    [SerializeField] int itemLayer;

    LaneDashConfigData _cfg;
    float[] _laneX;
    float _despawnZ;
    float _elapsedRunning;    // chỉ tăng khi không choáng — dùng cho ramp khó dần dài hạn
    float _stunTimer;         // > 0 = đang đứng hình sau va chạm
    float _boostTimer;        // > 0 = đang cưỡi ngựa (tốc độ nhân thêm)
    float _invulnTimer;       // > 0 = đang bay (tự động né mọi vật cản)
    float _currentSpeed;
    float _distanceMeters;
    float _spawnTimer;
    int _currentPlayerLane;
    bool _running;
    int _level;

    readonly List<LaneRunnerItem> _active = new();
    readonly Dictionary<LaneItemVariant, Queue<LaneRunnerItem>> _pools = new();

    public event Action OnObstacleHit;
    public event Action OnObstacleDodged;
    public event Action OnRewardCollected;
    public event Action<LaneItemVariant> OnPowerUpCollected;

    /// <summary>Tốc độ hiện tại (m/s) — 0 khi đang choáng. LaneDashController đọc giá trị này mỗi
    /// frame để: (1) hiển thị UI tốc độ/quãng đường, (2) đồng bộ scroll vật liệu mặt đường.</summary>
    public float CurrentSpeed => _currentSpeed;

    /// <summary>Quãng đường đã chạy (mét) — chỉ dùng để hiển thị.</summary>
    public float DistanceMeters => _distanceMeters;

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(LaneDashConfigData cfg, float[] laneX)
    {
        _cfg = cfg;
        _laneX = laneX;
        _despawnZ = cfg.playerZ - 5f;
        _spawnTimer = cfg.spawnInterval;
        _elapsedRunning = 0f;
        _currentSpeed = 0f;
        _distanceMeters = 0f;
        _stunTimer = 0f;
        _boostTimer = 0f;
        _invulnTimer = 0f;
    }

    public void SetPlayerLane(int lane) => _currentPlayerLane = lane;

    public void SetRunning(bool running) => _running = running;

    /// <summary>Cấp độ khó hiện tại — LaneDashController tính chung theo thời gian trận đấu (xem
    /// LaneDashConfigData.levelDuration) rồi đẩy vào cả 2 bên cùng lúc, để 2 người chơi luôn đối
    /// mặt độ khó như nhau bất kể ai đang chạy nhanh/chậm hơn. Ảnh hưởng trần tốc độ (maxSpeed) và
    /// mật độ spawn (spawnInterval hiệu lực) — xem EffectiveMaxSpeed()/EffectiveSpawnInterval().</summary>
    public void SetLevel(int level) => _level = Mathf.Clamp(level, 0, _cfg.maxLevel);

    float EffectiveMaxSpeed() => _cfg.maxSpeed + _level * _cfg.levelMaxSpeedBonus;

    float EffectiveSpawnInterval() => Mathf.Max(
        _cfg.minSpawnInterval,
        _cfg.spawnInterval * Mathf.Pow(_cfg.levelSpawnIntervalMultiplier, _level));

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_running) return;

        if (_boostTimer > 0f) _boostTimer -= Time.deltaTime;
        if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;

        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            _currentSpeed = 0f;
            return; // đứng hình hoàn toàn: không di chuyển vật thể, không spawn mới
        }

        _elapsedRunning += Time.deltaTime;
        bool boosting = _boostTimer > 0f;
        float maxSpeed = EffectiveMaxSpeed();
        float speedCap = boosting ? maxSpeed * _cfg.horseBoostMultiplier : maxSpeed;
        float baseTarget = _cfg.baseSpeed + _cfg.rampPerSecond * _elapsedRunning;
        float targetSpeed = Mathf.Min(boosting ? baseTarget * _cfg.horseBoostMultiplier : baseTarget, speedCap);
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _cfg.recoverAccel * Time.deltaTime);

        float delta = _currentSpeed * Time.deltaTime;
        _distanceMeters += delta;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var item = _active[i];
            float beforeZ = item.CenterZ;
            item.MoveForward(delta);
            float afterZ = item.CenterZ;

            bool crossedPlayer = beforeZ > _cfg.playerZ && afterZ <= _cfg.playerZ;
            if (crossedPlayer)
            {
                ResolveItem(item);
                Recycle(i);
                continue;
            }

            if (afterZ < _despawnZ)
                Recycle(i);
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnRandom();
            _spawnTimer = EffectiveSpawnInterval();
        }
    }

    void ResolveItem(LaneRunnerItem item)
    {
        bool sameLane = item.Lane == _currentPlayerLane;

        switch (item.Kind)
        {
            case LaneItemKind.Obstacle:
                if (sameLane && _invulnTimer <= 0f)
                {
                    _stunTimer = _cfg.stunDuration;
                    _currentSpeed = 0f;
                    _boostTimer = 0f; // ngã ngựa nếu đang cưỡi
                    OnObstacleHit?.Invoke();
                }
                else
                {
                    OnObstacleDodged?.Invoke(); // né thường, hoặc đang bay qua nhờ tên lửa
                }
                break;

            case LaneItemKind.Reward:
                if (sameLane) OnRewardCollected?.Invoke();
                // Bỏ lỡ phần thưởng: không phạt, im lặng.
                break;

            case LaneItemKind.PowerUp:
                if (sameLane)
                {
                    if (item.Variant == LaneItemVariant.Horse) _boostTimer = _cfg.horseBoostDuration;
                    else if (item.Variant == LaneItemVariant.Rocket) _invulnTimer = _cfg.rocketFlyDuration;
                    OnPowerUpCollected?.Invoke(item.Variant);
                }
                // Bỏ lỡ vật phẩm đặc biệt: không phạt, im lặng.
                break;
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void SpawnRandom()
    {
        int lane = UnityEngine.Random.Range(0, _laneX.Length);

        LaneItemKind kind;
        LaneItemVariant[] variantPool;

        if (UnityEngine.Random.value < _cfg.powerUpChance)
        {
            kind = LaneItemKind.PowerUp;
            variantPool = LaneDashItemVisuals.PowerUpVariants;
        }
        else if (UnityEngine.Random.value < _cfg.rewardChance)
        {
            kind = LaneItemKind.Reward;
            variantPool = LaneDashItemVisuals.RewardVariants;
        }
        else
        {
            kind = LaneItemKind.Obstacle;
            variantPool = LaneDashItemVisuals.ObstacleVariants;
        }

        var variant = variantPool[UnityEngine.Random.Range(0, variantPool.Length)];
        float baseScale = kind switch
        {
            LaneItemKind.Reward  => _cfg.rewardScale,
            LaneItemKind.PowerUp => _cfg.powerUpScale,
            _                    => _cfg.obstacleScale,
        };
        float scale = baseScale * LaneDashItemVisuals.GetScaleMultiplier(variant);

        var item = GetFromPool(kind, variant);
        item.Spawn(lane, _laneX[lane], _cfg.itemGroundY, _cfg.spawnDistanceZ, scale);
        _active.Add(item);
    }

    LaneRunnerItem GetFromPool(LaneItemKind kind, LaneItemVariant variant)
    {
        if (!_pools.TryGetValue(variant, out var queue))
        {
            queue = new Queue<LaneRunnerItem>();
            _pools[variant] = queue;
        }
        return queue.Count > 0 ? queue.Dequeue() : LaneDashItemVisuals.CreateVisual(kind, variant, transform, itemLayer);
    }

    void Recycle(int index)
    {
        var item = _active[index];
        item.Recycle();
        _pools[item.Variant].Enqueue(item);
        _active.RemoveAt(index);
    }
}
