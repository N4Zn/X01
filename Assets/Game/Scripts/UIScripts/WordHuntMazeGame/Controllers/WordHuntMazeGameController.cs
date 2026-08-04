using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "WORD HUNT MAZE" — nhận diện mặt chữ: bảng chữ cái 7x7 (mirror 2 bên), mỗi bảng giấu 3 từ
/// Family (1 ngang, 1 dọc, 1 chéo — xem WordGridGenerator). KHÔNG có gợi ý — học sinh tự nhận diện
/// mặt chữ hoàn toàn bằng mắt, dẫm xuôi hoặc ngược đều được (xem WordGridDisplay). Tìm đủ cả 3 từ
/// trong 1 bảng → thắng round → sang bảng (round) mới.
///
/// Dựng từ MiniGameControllerBase, chế độ Combined (mặc định) — không dùng CSV/MiniGameQuestionSource
/// vì game này không có "đáp án lựa chọn" (không phải Choose/Matching), chỉ có 1 danh sách từ cố
/// định. Override PullNextQuestion() để tự sinh QuestionData mang 3 từ (nối dấu phẩy trong id — cùng
/// convention với FamilySpellingGameController: dữ liệu riêng của game nằm trong id, không cần thêm
/// cột CSV hay đụng CsvQuestionLoader/QuestionData của Kit). GetDisplayForQuestion() luôn trả về
/// đúng 1 WordGridDisplay — bảng chữ cái CHÍNH LÀ màn hiện đáp án.
/// </summary>
public class WordHuntMazeGameController : MiniGameControllerBase
{
    [Header("WordHuntMaze — refs")]
    [SerializeField] WordGridDisplay gridDisplay;
    [SerializeField] Button backButton;

    [Header("WordHuntMaze — điểm số")]
    [Tooltip("Đội thắng (tìm đủ cả 3 từ trước): 3 x pointsPerWord + winnerBonus. Đội thua: số từ ĐÃ tìm được (tại thời điểm round kết thúc) x pointsPerWord.")]
    [SerializeField] int pointsPerWord = 1;
    [SerializeField] int winnerBonus = 2;

    const int WordsPerBoard = 3; // phải khớp WordGridDisplay.WordCount

    static readonly string[] FamilyWords = { "MOM", "DAD", "BABY", "SISTER", "BROTHER", "GRANDMA", "GRANDPA",
                                              "HAPPY", "SAD", "COLD", "HOT", "THIRSTY", "HUNGRY" };

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);
    }

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => gridDisplay;

    // Tự tính điểm ở OnRoundResult (thắng 3+thưởng, thua theo số từ đã tìm được) — tắt +1 mặc định
    // của Kit để tránh cộng điểm 2 lần.
    protected override void AwardDefaultPoint(Team team) { }

    /// <summary>playerAnswer[0] = số từ đội THUA đã tìm được tại thời điểm round kết thúc (do
    /// WordGridDisplay tính sẵn) — đội thắng luôn đủ cả 3 từ nên không cần truyền riêng.</summary>
    protected override void OnRoundResult(bool correct, Team team, int[] playerAnswer)
    {
        if (!correct) return;

        int loserWordCount = playerAnswer != null && playerAnswer.Length > 0 ? playerAnswer[0] : 0;
        Team loser = team == Team.Left ? Team.Right : Team.Left;

        ScoreManager.AddPoints(team, WordsPerBoard * pointsPerWord + winnerBonus);
        if (loserWordCount > 0) ScoreManager.AddPoints(loser, loserWordCount * pointsPerWord);
    }

    /// <summary>Không có CSV — mỗi round chọn ngẫu nhiên 3 từ KHÔNG TRÙNG từ danh sách Family, nối
    /// bằng dấu phẩy vào id (vd "MOM,SISTER,GRANDPA") cho WordGridDisplay tách ra dựng bảng.</summary>
    protected override QuestionData PullNextQuestion()
        => new QuestionData { id = string.Join(",", PickThreeDistinct(FamilyWords)) };

    static string[] PickThreeDistinct(string[] source)
    {
        var pool = new List<string>(source);
        var result = new string[Mathf.Min(3, pool.Count)];
        for (int i = 0; i < result.Length; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }
}
