using System;
using UnityEngine;

/// <summary>
/// BalloonField — 4 cột bóng bay độc lập, spawn theo vị trí.
/// Mọi thông số điều chỉnh nằm trong BalloonGameConfig.
///
/// Spawn trigger: khi head.y > config.bottomBound + config.spawnGap → spawn ngay.
/// Gap = spawnGap px (khoảng cách pixel thực giữa 2 điểm spawn liên tiếp).
/// Correct guarantee: khi 0 balloon đúng đang bay → spawn đúng ngay.
/// </summary>
public class BalloonField : MonoBehaviour
{
    [SerializeField] BalloonItem[]     items;
    [SerializeField] BalloonGameConfig config;

    const int COLS          = 4;
    const int CORRECT_SLOTS = 2;

    string       _correctValue;
    string[]     _wrongValues;
    Team         _team;
    bool         _active;
    Action<Team> _onCorrectPop;

    int[] _pool;
    int   _poolCursor;

    readonly BalloonItem[] _columnHead = new BalloonItem[COLS];

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Setup(string correctValue, string[] wrongValues, Team team,
                      Action<Team> onCorrectPop)
    {
        if (config == null)
            config = UnityEngine.Resources.Load<BalloonGameConfig>("GameConfig/BalloonGameConfig");
        if (config == null)
        {
            Debug.LogError($"[BalloonField] {name}: không tìm thấy BalloonGameConfig. Chạy Tools > BalloonGame > Build Scene.", this);
            return;
        }

        _correctValue = correctValue;
        _wrongValues  = wrongValues;
        _team         = team;
        _onCorrectPop = onCorrectPop;

        BuildPool();
        Array.Clear(_columnHead, 0, COLS);

        foreach (var it in items)
        {
            it.gameObject.SetActive(false);
            it.SetCallbacks(OnItemClicked, null);
        }

        _active = true;
        gameObject.SetActive(true);

        for (int col = 0; col < COLS; col++)
            SpawnInColumn(col);
    }

    public void StopField()
    {
        _active = false;
        foreach (var it in items)
            it.gameObject.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_active) return;

        foreach (var it in items)
        {
            if (!it.IsAlive) continue;
            it.Tick(Time.deltaTime);
            if (it.AnchoredPos.y > config.topBound) it.ForceExit();
        }

        for (int col = 0; col < COLS; col++)
        {
            var head = _columnHead[col];
            if (head == null || !head.IsAlive || head.AnchoredPos.y > config.bottomBound + config.spawnGap)
                SpawnInColumn(col);
        }
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    void OnItemClicked(BalloonItem item)
    {
        if (!_active) return;
        bool correct = item.Value == _correctValue;
        item.Pop(correct);
        if (correct) _onCorrectPop?.Invoke(_team);
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void SpawnInColumn(int col)
    {
        var item = GetFreeItem();
        if (item == null) return;

        float x = config.columnXBase[col % config.columnXBase.Length]
                  + UnityEngine.Random.Range(-config.xJitter, config.xJitter);
        item.Activate(NextAssignment(), null, RandomSpeed(), new Vector2(x, config.bottomBound));
        _columnHead[col] = item;
    }

    BalloonItem GetFreeItem()
    {
        foreach (var it in items)
            if (!it.IsAlive) return it;
        return null;
    }

    // ── Assignment ────────────────────────────────────────────────────────────

    int CountCorrectFloating()
    {
        int n = 0;
        foreach (var it in items)
            if (it.IsAlive && it.Value == _correctValue) n++;
        return n;
    }

    string NextAssignment()
    {
        if (CountCorrectFloating() == 0)
            return _correctValue;

        if (_poolCursor >= _pool.Length) { Shuffle(_pool); _poolCursor = 0; }
        int slot = _pool[_poolCursor++];
        return slot == 0
            ? _correctValue
            : _wrongValues[(slot - 1) % Mathf.Max(1, _wrongValues.Length)];
    }

    void BuildPool()
    {
        int wrongSlots = items.Length - CORRECT_SLOTS;
        _pool = new int[items.Length];
        for (int i = 0; i < CORRECT_SLOTS; i++) _pool[i] = 0;
        for (int i = 0; i < wrongSlots;   i++) _pool[CORRECT_SLOTS + i] = i + 1;
        Shuffle(_pool);
        _poolCursor = 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float RandomSpeed() =>
        config.speedBase + UnityEngine.Random.Range(-config.speedJitter, config.speedJitter);

    static void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
