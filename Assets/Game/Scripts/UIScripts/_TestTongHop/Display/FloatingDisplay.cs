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

    // ─── IAnswerDisplay ───────────────────────────────────────────────────────

    public void Setup(QuestionData q, Action<bool, Team, int[]> onResult, Action<Team> onPlayerFailed)
    {
        _current             = q;
        _onResult            = onResult;
        _onPlayerFailed      = onPlayerFailed;
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

        int[] leftOrder    = ShuffledIndices(count);
        int[] rightOrder   = ShuffledIndices(count);
        var   leftPlanets  = PickRandomPlanets(count);
        var   rightPlanets = PickRandomPlanets(count);

        float cx = TongHopConfig.Current.orbitCenterX;

        // ── Orbit items (0 .. orbitCount-1) ──────────────────────────────────
        for (int i = 0; i < orbitCount; i++)
        {
            // Góc chia đều: 3→120°, 4→90°, 5→72°, 6→60°
            _leftAngles[i]  = 360f * i / orbitCount;
            _rightAngles[i] = 360f * i / orbitCount;

            int lIdx  = validIndices[leftOrder[i]];
            var lItem = SpawnOneItem(lIdx, leftPlanets[i], delay: i * 0.1f, Team.Left);
            var lRt   = lItem.GetComponent<RectTransform>();
            lRt.anchoredPosition = OrbitPos(-cx, _leftAngles[i]);
            _leftItems.Add(lItem);
            _leftRts.Add(lRt);      // thêm vào RTs → sẽ xoay theo orbit

            int rIdx  = validIndices[rightOrder[i]];
            var rItem = SpawnOneItem(rIdx, rightPlanets[i], delay: i * 0.1f + 0.05f, Team.Right);
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
            var lItem = SpawnOneItem(lIdx, leftPlanets[i], delay, Team.Left);
            lItem.GetComponent<RectTransform>().anchoredPosition = new Vector2(-cx, 0f);
            _leftItems.Add(lItem);
            // KHÔNG thêm vào _leftRts → cố định, không xoay

            int rIdx  = validIndices[rightOrder[i]];
            var rItem = SpawnOneItem(rIdx, rightPlanets[i], delay + 0.05f, Team.Right);
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
        bool        isLeft      = team == Team.Left;
        if (isLeft ? _leftFinalised : _rightFinalised) return;

        var         validator   = isLeft ? _leftValidator   : _rightValidator;
        var         myItems     = isLeft ? _leftItems        : _rightItems;
        var         theirItems  = isLeft ? _rightItems       : _leftItems;
        var         selectTimes = isLeft ? _leftSelectTimes  : _rightSelectTimes;

        // ── Interact guard (chống bấm nhầm nhanh) — chỉ áp dụng cho MultiSelect ──
        if (_current.answerMode == AnswerMode.MultiSelect)
        {
            float t = selectTimes.TryGetValue(answerIndex, out var st) ? st : 0f;
            if (Time.time - t < TongHopConfig.Current.chooseDeselectDelay) return;
        }

        var result = validator.RegisterClick(answerIndex);
        GameLogger.Current?.LogClick(team, answerIndex, result);

        switch (result)
        {
            case ClickResult.WrongPartial:
                // Xám — không hé lộ sai, vẫn deselect được sau delay
                GetItemFrom(myItems, answerIndex)?.SetChosen();
                selectTimes[answerIndex] = Time.time;
                break;

            case ClickResult.WrongFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                _orbitRunning = false;
                if (_current.answerMode == AnswerMode.OrderedSequence)
                    ApplyOrderedFinalState(myItems, validator, answerIndex);
                else
                    ApplyMultiFinalState(myItems, validator, _current.correctAnswers);
                if (isLeft) _leftAnsweredWrong  = true;
                else        _rightAnsweredWrong = true;
                _onPlayerFailed?.Invoke(team);
                CheckBothWrong();
                break;

            case ClickResult.CorrectPartial:
            {
                var picked = GetItemFrom(myItems, answerIndex);
                if (_current.answerMode == AnswerMode.OrderedSequence)
                {
                    picked?.SetState(ItemState.Correct);  // xanh ngay
                    picked?.Lock();                        // khoá — không click lại
                }
                else
                {
                    picked?.SetChosen();                   // xám — chưa hé lộ đúng/sai
                }
                selectTimes[answerIndex] = Time.time;
                break;
            }

            case ClickResult.Deselected:
                GetItemFrom(myItems, answerIndex)?.SetState(ItemState.Normal);
                selectTimes[answerIndex] = Time.time;
                break;

            case ClickResult.CorrectFinal:
                if (isLeft) _leftFinalised = true; else _rightFinalised = true;
                _orbitRunning = false;
                ApplyFinalState(myItems, _current.correctAnswers);
                foreach (var item in theirItems) item.SetState(ItemState.Locked);
                _onResult?.Invoke(true, team, _current.correctAnswers);
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

    Sprite[] PickRandomPlanets(int count)
    {
        if (planetSprites == null || planetSprites.Length == 0)
            return new Sprite[count];

        var pool   = new List<Sprite>(planetSprites);
        var result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) pool = new List<Sprite>(planetSprites);
            int pick  = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[pick];
            pool.RemoveAt(pick);
        }
        return result;
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
