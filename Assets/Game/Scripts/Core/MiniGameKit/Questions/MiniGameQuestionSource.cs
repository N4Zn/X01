using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Question pool cho MỘT mini-game độc lập (khác với QuestionPool của TestTongHopGame,
/// vốn được thiết kế để 1 scene phục vụ nhiều "variant" qua GameSessionManager.SelectedGameName).
///
/// Gán trực tiếp 1-2 TextAsset (Choose và/hoặc Matching) trong Inspector.
/// Cùng column layout với CSV của TestTongHopGame — xem CsvQuestionLoader.
/// </summary>
public class MiniGameQuestionSource : MonoBehaviour
{
    [Header("CSV")]
    [SerializeField] TextAsset chooseCsv;
    [SerializeField] TextAsset matchingCsv;
    [SerializeField] int difficulty = 1;

    readonly List<QuestionData> _choosePool = new();
    readonly List<QuestionData> _matchingPool = new();
    int _chooseIdx;
    int _matchingIdx;

    void Awake() => BuildPool();

    public bool HasChoose => _choosePool.Count > 0;
    public bool HasMatching => _matchingPool.Count > 0;
    public bool IsEmpty() => !HasChoose && !HasMatching;

    public void SetDifficulty(int d)
    {
        difficulty = d;
        BuildPool();
    }

    public QuestionData GetNextChoose()
    {
        if (_choosePool.Count == 0) return null;
        if (_chooseIdx >= _choosePool.Count) { Shuffle(_choosePool); _chooseIdx = 0; }
        return _choosePool[_chooseIdx++];
    }

    public QuestionData GetNextMatching()
    {
        if (_matchingPool.Count == 0) return null;
        if (_matchingIdx >= _matchingPool.Count) { Shuffle(_matchingPool); _matchingIdx = 0; }
        return _matchingPool[_matchingIdx++];
    }

    void BuildPool()
    {
        _choosePool.Clear();
        _matchingPool.Clear();
        _chooseIdx = _matchingIdx = 0;

        var allChoose = chooseCsv != null ? CsvQuestionLoader.ParseChoose(chooseCsv.text) : new List<QuestionData>();
        var allMatching = matchingCsv != null ? CsvQuestionLoader.ParseMatching(matchingCsv.text) : new List<QuestionData>();

        _choosePool.AddRange(allChoose.Where(q => q.difficulty == difficulty));
        if (_choosePool.Count == 0 && allChoose.Count > 0)
        {
            Debug.LogWarning($"[MiniGameQuestionSource] Không có câu Choose ở difficulty={difficulty} — dùng toàn bộ {allChoose.Count} câu.");
            _choosePool.AddRange(allChoose);
        }
        Shuffle(_choosePool);

        _matchingPool.AddRange(allMatching.Where(q => q.difficulty == difficulty));
        if (_matchingPool.Count == 0 && allMatching.Count > 0)
        {
            Debug.LogWarning($"[MiniGameQuestionSource] Không có câu Matching ở difficulty={difficulty} — dùng toàn bộ {allMatching.Count} câu.");
            _matchingPool.AddRange(allMatching);
        }
        Shuffle(_matchingPool);
    }

    static void Shuffle(List<QuestionData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
