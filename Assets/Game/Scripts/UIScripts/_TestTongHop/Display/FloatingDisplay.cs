using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingDisplay : MonoBehaviour, IAnswerDisplay
{
    [SerializeField] FloatingItem  itemPrefab;
    [SerializeField] RectTransform spawnParent;
    [SerializeField] Sprite[]      planetSprites;
    // orbitCenterX / orbitRadius / orbitSpeed → TongHopConfig.Current (gameconfig.json)

    // planetSprites[] (Inspector) là override tuỳ chọn — nếu để trống, tự load từ Resources.
    // Thứ tự mảng phải khớp _planetTexPaths bên dưới: 0=Mercury … 7=Neptune, 8=Sun
    static readonly System.Collections.Generic.Dictionary<string, int> _planetNameIdx =
        new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Mercury",    0 }, { "Sao Thủy",   0 },
            { "Venus",      1 }, { "Sao Kim",     1 },
            { "Earth",      2 }, { "Trái Đất",    2 },
            { "Mars",       3 }, { "Sao Hỏa",     3 },
            { "Jupiter",    4 }, { "Sao Mộc",     4 },
            { "Saturn",     5 }, { "Sao Thổ",     5 },
            { "Uranus",     6 }, { "Thiên Vương", 6 },
            { "Neptune",    7 }, { "Hải Vương",   7 },
            { "Sun",        8 }, { "Mặt Trời",    8 },
        };

    static readonly string[] _planetTexPaths =
    {
        "SolarSystem/Mercury",
        "SolarSystem/Venus",
        "SolarSystem/Earth",
        "SolarSystem/Mars",
        "SolarSystem/Jupiter",
        "SolarSystem/Saturn",
        "SolarSystem/Uranus",
        "SolarSystem/Neptune",
        "SolarSystem/Textures/2k_sun",   // Sun chưa có sprite riêng
    };

    // Cache sprite tạo từ Texture2D để tránh tạo lại mỗi câu hỏi
    static readonly Sprite[] _cachedPlanetSprites = new Sprite[9];

    // Hai nhóm item hoàn toàn độc lập
    readonly List<FloatingItem>  _leftItems  = new();
    readonly List<FloatingItem>  _rightItems = new();
    readonly List<RectTransform> _leftRts    = new();
    readonly List<RectTransform> _rightRts   = new();
    float[] _leftAngles;
    float[] _rightAngles;

    AnswerValidator           _leftValidator;
    AnswerValidator           _rightValidator;
    Action<bool, Team, int[]> _onResult;
    Action<Team>              _onPlayerFailed;
    Action<Team>              _onPartialCorrect;
    QuestionData              _current;
    bool                      _orbitRunning;
    Coroutine                 _orbitCoroutine;
    bool                      _leftAnsweredWrong;
    bool                      _rightAnsweredWrong;
    bool                      _leftFinalised;
    bool                      _rightFinalised;

    // selectTime[answerIndex] = Time.time khi item được chọn (ChooseDeselectDelay guard)
    readonly System.Collections.Generic.Dictionary<int, float> _leftSelectTimes  = new();
    readonly System.Collections.Generic.Dictionary<int, float> _rightSelectTimes = new();

    // ── Independent play mode ─────────────────────────────────────────────────
    bool                      _isIndependent;
    QuestionData              _leftQuestionInd;
    QuestionData              _rightQuestionInd;
    Action<bool, Team, int[]> _leftOnDoneInd;
    Action<bool, Team, int[]> _rightOnDoneInd;

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void SetPartialCorrectCallback(Action<Team> cb) => _onPartialCorrect = cb;

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current             = q;
        _onResult            = onResult;
        _onPlayerFailed      = onPlayerFailed;
        _onPartialCorrect    = null;
        _leftValidator       = new AnswerValidator(q);
        _rightValidator      = new AnswerValidator(q);
        _leftAnsweredWrong   = false;
        _rightAnsweredWrong  = false;
        _leftFinalised       = false;
        _rightFinalised      = false;
        _leftSelectTimes.Clear();
        _rightSelectTimes.Clear();
        SpawnItems();
        _orbitRunning   = true;
        _orbitCoroutine = StartCoroutine(OrbitCoroutine());
    }

    public void HidePlayerAnswers(Team team)
    {
        var items = team == Team.Left ? _leftItems : _rightItems;
        foreach (var item in items)
            if (item != null) item.gameObject.SetActive(false);
    }

    public void Cleanup()
    {
        _orbitRunning = false;
        if (_orbitCoroutine != null) StopCoroutine(_orbitCoroutine);
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
        foreach (var item in _leftItems)  if (item) Destroy(item.gameObject);
        foreach (var item in _rightItems) if (item) Destroy(item.gameObject);
        _leftItems.Clear();  _rightItems.Clear();
        _leftRts.Clear();    _rightRts.Clear();
        _isIndependent    = false;
        _leftQuestionInd  = null;
        _rightQuestionInd = null;
        _leftOnDoneInd    = null;
        _rightOnDoneInd   = null;
        _onPartialCorrect = null;
    }

    // ─── Independent play ─────────────────────────────────────────────────────

    public void SetupPlayerIndependent(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        _isIndependent = true;
        gameObject.SetActive(true);  // activate first — spawnParent and coroutines need active GO
        bool isLeft    = team == Team.Left;

        var items = isLeft ? _leftItems : _rightItems;
        var rts   = isLeft ? _leftRts   : _rightRts;
        foreach (var item in items) if (item) Destroy(item.gameObject);
        items.Clear();
        rts.Clear();

        if (isLeft)
        {
            _leftQuestionInd   = q;
            _leftOnDoneInd     = onDone;
            _leftValidator     = new AnswerValidator(q);
            _leftFinalised     = false;
            _leftAnsweredWrong = false;
            _leftSelectTimes.Clear();
        }
        else
        {
            _rightQuestionInd   = q;
            _rightOnDoneInd     = onDone;
            _rightValidator     = new AnswerValidator(q);
            _rightFinalised     = false;
            _rightAnsweredWrong = false;
            _rightSelectTimes.Clear();
        }

        SpawnItemsForTeam(team, q);

        if (!_orbitRunning)
        {
            _orbitRunning   = true;
            _orbitCoroutine = StartCoroutine(OrbitCoroutine());
        }
    }

    // ─── Spawn ────────────────────────────────────────────────────────────────

    void SpawnItems()
    {
        // Lọc đáp án không rỗng — hỗ trợ 3‒7 đáp án
        var validIndices = new List<int>();
        for (int i = 0; i < _current.answers.Length; i++)
            if (!string.IsNullOrWhiteSpace(_current.answers[i]))
                validIndices.Add(i);

        int count      = validIndices.Count;
        int orbitCount = Mathf.Min(count, 6);   // tối đa 6 item trên orbit
        // Đáp án thứ 7 (index 6) sẽ đặt cố định ở tâm, không xoay

        _leftAngles  = new float[orbitCount];
        _rightAngles = new float[orbitCount];

        int[] leftOrder  = ShuffledIndices(count);
        int[] rightOrder = ShuffledIndices(count);

        float cx = TongHopConfig.Current.orbitCenterX;

        // ── Orbit items (0 .. orbitCount-1) ──────────────────────────────────
        for (int i = 0; i < orbitCount; i++)
        {
            // Góc chia đều: 3→120°, 4→90°, 5→72°, 6→60°
            _leftAngles[i]  = 360f * i / orbitCount;
            _rightAngles[i] = 360f * i / orbitCount;

            int lIdx  = validIndices[leftOrder[i]];
            var lItem = SpawnOneItem(lIdx, GetPlanetSprite(_current.answers[lIdx]), delay: i * 0.1f, Team.Left);
            var lRt   = lItem.GetComponent<RectTransform>();
            lRt.anchoredPosition = OrbitPos(-cx, _leftAngles[i]);
            _leftItems.Add(lItem);
            _leftRts.Add(lRt);      // thêm vào RTs → sẽ xoay theo orbit

            int rIdx  = validIndices[rightOrder[i]];
            var rItem = SpawnOneItem(rIdx, GetPlanetSprite(_current.answers[rIdx]), delay: i * 0.1f + 0.05f, Team.Right);
            var rRt   = rItem.GetComponent<RectTransform>();
            rRt.anchoredPosition = OrbitPos(cx, _rightAngles[i]);
            _rightItems.Add(rItem);
            _rightRts.Add(rRt);
        }

        // ── Center items (index orbitCount .. count-1, tức đáp án thứ 7) ────
        for (int i = orbitCount; i < count; i++)
        {
            float delay = i * 0.1f;

            int lIdx  = validIndices[leftOrder[i]];
            var lItem = SpawnOneItem(lIdx, GetPlanetSprite(_current.answers[lIdx]), delay, Team.Left);
            lItem.GetComponent<RectTransform>().anchoredPosition = new Vector2(-cx, 0f);
            _leftItems.Add(lItem);
            // KHÔNG thêm vào _leftRts → cố định, không xoay

            int rIdx  = validIndices[rightOrder[i]];
            var rItem = SpawnOneItem(rIdx, GetPlanetSprite(_current.answers[rIdx]), delay + 0.05f, Team.Right);
            rItem.GetComponent<RectTransform>().anchoredPosition = new Vector2(cx, 0f);
            _rightItems.Add(rItem);
            // KHÔNG thêm vào _rightRts
        }
    }

    FloatingItem SpawnOneItem(int answerIndex, Sprite planet, float delay, Team team)
    {
        var item = Instantiate(itemPrefab, spawnParent);

        // Áp kích thước từ config — override prefab default
        float size = TongHopConfig.Current.floatingSize;
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        item.Setup(_current.answers[answerIndex], _current.answerMediaType, answerIndex, OnItemClicked, team);
        item.SetPlanet(planet);
        item.PlaySpawnAnim(delay);
        return item;
    }

    void SpawnItemsForTeam(Team team, QuestionData q)
    {
        bool  isLeft = team == Team.Left;
        float cx     = TongHopConfig.Current.orbitCenterX;

        var validIndices = new List<int>();
        for (int i = 0; i < q.answers.Length; i++)
            if (!string.IsNullOrWhiteSpace(q.answers[i]))
                validIndices.Add(i);

        int count      = validIndices.Count;
        int orbitCount = Mathf.Min(count, 6);

        float[] angles = new float[orbitCount];
        if (isLeft) _leftAngles = angles; else _rightAngles = angles;

        int[] order = ShuffledIndices(count);

        var items = isLeft ? _leftItems : _rightItems;
        var rts   = isLeft ? _leftRts   : _rightRts;

        for (int i = 0; i < orbitCount; i++)
        {
            angles[i] = 360f * i / orbitCount;
            int  idx  = validIndices[order[i]];
            var  item = SpawnOneItemWith(q, idx, GetPlanetSprite(q.answers[idx]), i * 0.1f, team);
            var  rt   = item.GetComponent<RectTransform>();
            rt.anchoredPosition = OrbitPos(isLeft ? -cx : cx, angles[i]);
            items.Add(item);
            rts.Add(rt);
        }
        for (int i = orbitCount; i < count; i++)
        {
            int idx  = validIndices[order[i]];
            var item = SpawnOneItemWith(q, idx, GetPlanetSprite(q.answers[idx]), i * 0.1f, team);
            item.GetComponent<RectTransform>().anchoredPosition = new Vector2(isLeft ? -cx : cx, 0f);
            items.Add(item);
        }
    }

    FloatingItem SpawnOneItemWith(QuestionData q, int answerIndex, Sprite planet, float delay, Team team)
    {
        var item = Instantiate(itemPrefab, spawnParent);
        float size = TongHopConfig.Current.floatingSize;
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
        item.Setup(q.answers[answerIndex], q.answerMediaType, answerIndex, OnItemClicked, team);
        item.SetPlanet(planet);
        item.PlaySpawnAnim(delay);
        return item;
    }

    // ─── Orbit ────────────────────────────────────────────────────────────────

    IEnumerator OrbitCoroutine()
    {
        while (_orbitRunning)
        {
            float speed = TongHopConfig.Current.orbitSpeed;
            float cx    = TongHopConfig.Current.orbitCenterX;
            for (int i = 0; i < _leftRts.Count; i++)
            {
                if (_leftRts[i] != null)
                {
                    _leftAngles[i] += speed * Time.deltaTime;
                    _leftRts[i].anchoredPosition = OrbitPos(-cx, _leftAngles[i]);
                }
            }
            for (int i = 0; i < _rightRts.Count; i++)
            {
                if (_rightRts[i] != null)
                {
                    _rightAngles[i] += speed * Time.deltaTime;
                    _rightRts[i].anchoredPosition = OrbitPos(cx, _rightAngles[i]);
                }
            }
            yield return null;
        }
    }

    Vector2 OrbitPos(float centerX, float angleDeg)
    {
        float rad    = angleDeg * Mathf.Deg2Rad;
        float radius = TongHopConfig.Current.orbitRadius;
        return new Vector2(centerX + Mathf.Cos(rad) * radius,
                                     Mathf.Sin(rad) * radius);
    }

    // ─── Click handling ───────────────────────────────────────────────────────

    void OnItemClicked(int answerIndex, Team team)
    {
        bool isLeft = team == Team.Left;
        if (isLeft ? _leftFinalised : _rightFinalised) return;

        var q = _isIndependent
            ? (isLeft ? _leftQuestionInd : _rightQuestionInd)
            : _current;
        if (q == null) return;

        var validator  = isLeft ? _leftValidator : _rightValidator;
        var myItems    = isLeft ? _leftItems     : _rightItems;
        var theirItems = isLeft ? _rightItems    : _leftItems;

        var result = validator.RegisterClick(answerIndex);
        GameLogger.Current?.LogClick(team, answerIndex, result);

        switch (result)
        {
            case ClickResult.WrongFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                if (!_isIndependent) _orbitRunning = false;
                if (q.answerMode == AnswerMode.OrderedSequence)
                    ApplyOrderedFinalState(myItems, validator, answerIndex);
                else
                    ApplyMultiFinalState(myItems, validator, q.correctAnswers);
                if (_isIndependent)
                    (isLeft ? _leftOnDoneInd : _rightOnDoneInd)?.Invoke(false, team, q.correctAnswers);
                else
                {
                    if (isLeft) _leftAnsweredWrong = true; else _rightAnsweredWrong = true;
                    _onPlayerFailed?.Invoke(team);
                    CheckBothWrong();
                }
                break;

            case ClickResult.CorrectPartial:
            {
                var picked = GetItemFrom(myItems, answerIndex);
                if (q.answerMode == AnswerMode.OrderedSequence)
                {
                    picked?.SetState(ItemState.Correct);
                    picked?.Lock();
                    MusicManager.Instance?.PlayCorrectSfx();
                }
                else // MultiSelect: ẩn item + +1 điểm + SFX, round tiếp tục
                {
                    if (picked != null) picked.gameObject.SetActive(false);
                    _onPartialCorrect?.Invoke(team);
                }
                break;
            }

            case ClickResult.CorrectFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                if (!_isIndependent) _orbitRunning = false;
                ApplyFinalState(myItems, q.correctAnswers);
                if (_isIndependent)
                    (isLeft ? _leftOnDoneInd : _rightOnDoneInd)?.Invoke(true, team, q.correctAnswers);
                else
                {
                    foreach (var item in theirItems) item.SetState(ItemState.Locked);
                    _onResult?.Invoke(true, team, q.correctAnswers);
                }
                break;

            // WrongInSequence không còn dùng — ValidateOrdered trả WrongFinal trực tiếp
        }
    }

    // OrderedSequence WrongFinal: đã chọn đúng → Correct, item gây fail → Wrong, còn lại → Locked
    void ApplyOrderedFinalState(List<FloatingItem> items, AnswerValidator validator, int wrongIndex)
    {
        foreach (var item in items)
        {
            if (validator.IsSelected(item.AnswerIndex))
                item.SetState(ItemState.Correct);
            else if (item.AnswerIndex == wrongIndex)
                item.SetState(ItemState.Wrong);
            else
                item.SetState(ItemState.Locked);
        }
    }

    void ApplyMultiFinalState(List<FloatingItem> items, AnswerValidator validator, int[] correctAnswers)
    {
        foreach (var item in items)
        {
            if (validator.IsSelected(item.AnswerIndex))
            {
                bool ok = System.Array.IndexOf(correctAnswers, item.AnswerIndex) >= 0;
                item.SetState(ok ? ItemState.Correct : ItemState.Wrong);
            }
            else
            {
                item.SetState(ItemState.Locked);
            }
        }
    }

    // ─── Both-wrong check ────────────────────────────────────────────────────

    void CheckBothWrong()
    {
        if (_leftAnsweredWrong && _rightAnsweredWrong)
        {
            _orbitRunning = false;
            _onResult?.Invoke(false, Team.Left, System.Array.Empty<int>());
        }
    }

    void ApplyFinalState(List<FloatingItem> items, int[] correctAnswers)
    {
        foreach (var item in items)
        {
            bool ok = System.Array.IndexOf(correctAnswers, item.AnswerIndex) >= 0;
            item.SetState(ok ? ItemState.Correct : ItemState.Locked);
        }
    }

    FloatingItem GetItemFrom(List<FloatingItem> items, int answerIndex)
        => items.Find(i => i.AnswerIndex == answerIndex);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Trả về sprite đúng với tên hành tinh (Vi/En).
    /// Ưu tiên: Inspector planetSprites[] → tự load Texture2D từ Resources → random fallback.
    /// </summary>
    Sprite GetPlanetSprite(string answerText)
    {
        if (!string.IsNullOrWhiteSpace(answerText) &&
            _planetNameIdx.TryGetValue(answerText.Trim(), out int idx))
        {
            // 1. Load sprite/texture đúng với tên hành tinh
            if (_cachedPlanetSprites[idx] == null && idx < _planetTexPaths.Length)
            {
                string path = _planetTexPaths[idx];
                // Thử Sprite trước (PNG import type Sprite 2D)
                var spr = Resources.Load<Sprite>(path);
                if (spr != null)
                {
                    _cachedPlanetSprites[idx] = spr;
                }
                else
                {
                    // Fallback: Texture2D (JPG hoặc PNG import type Default)
                    var tex = Resources.Load<Texture2D>(path);
                    if (tex != null)
                        _cachedPlanetSprites[idx] = Sprite.Create(
                            tex,
                            new Rect(0f, 0f, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f),
                            100f, 0, SpriteMeshType.FullRect);
                }
            }
            if (_cachedPlanetSprites[idx] != null)
                return _cachedPlanetSprites[idx];
        }

        // Fallback: random từ Inspector (game không phải SolarOrder)
        if (planetSprites != null && planetSprites.Length > 0)
            return planetSprites[UnityEngine.Random.Range(0, planetSprites.Length)];
        return null;
    }

    int[] ShuffledIndices(int n)
    {
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        return idx;
    }
}
