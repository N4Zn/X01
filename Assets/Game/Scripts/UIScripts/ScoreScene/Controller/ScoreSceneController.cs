using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreSceneController : MonoBehaviour
{
    [SerializeField] private ScoreSceneView scoreSceneView;
    protected CustomFSMManager _customFSMManager;

    void Start()
    {
        Debug.Log($"[ScoreSceneController][DEBUG] Start() — instance={GetInstanceID()} t={Time.unscaledTime:F1}");
        _customFSMManager = gameObject.AddComponent<CustomFSMManager>();
        _customFSMManager.fsmName = this.GetType().Name + "FSM";
        _customFSMManager.Initialize(typeof(ScoreSceneState), this.GetType(), false);
        _customFSMManager.StateMachineChange(ScoreSceneState.Initialize);

        scoreSceneView.InitView();
        scoreSceneView.onClickReplay += OnClickReplay;
        scoreSceneView.onClickChangeTeam += OnClickChangeTeam;
    }

    protected void StateMachineEnter_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineEnter_Initialize");

        var session = GameSessionManager.Instance;

        if (session.CurrentGameMode == GameMode.Team)
        {
            scoreSceneView.ShowTeamMode();
            scoreSceneView.DisplayTeamResults(
                SummaryTeamName(session, 0),
                session.Player1FinalScore,
                SummaryTeamName(session, 1),
                session.Player2FinalScore,
                session.BlueTeamPlayers,
                session.RedTeamPlayers
            );

            // MVP + Tia Chớp — tính từ số liệu từng người chơi thật (PlayerRecognitionService).
            ShowHighlights(session);
        }
        else
        {
            scoreSceneView.ShowOneVsOneMode();
            scoreSceneView.DisplayOneVsOneResults(
                session.GetDisplayName1(),
                session.Player1FinalScore,
                session.GetDisplayName2(),
                session.Player2FinalScore
            );
        }

        // Breakdown từng người chơi thật KHÔNG hiện ở đây nữa — đã chuyển hẳn sang tablet
        // (ControlActivity.renderSummary(), nút "TỔNG KẾT") theo yêu cầu thực tế: đây là màn
        // chiếu (display phụ) cho học sinh xem, chi tiết từng người/thời gian chỉ giáo viên cần
        // nên hợp lý hơn khi xem trên tablet. Màn chiếu chỉ cần tổng điểm mỗi bên như cũ.

        _customFSMManager.StateMachineChange(ScoreSceneState.DisplayResults);
    }

    protected void StateMachineExit_Initialize(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineExit_Initialize");
    }

    protected void StateMachineEnter_DisplayResults(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineEnter_DisplayResults");
    }

    protected void StateMachineExit_DisplayResults(Enum previousState, Dictionary<string, object> options)
    {
        Debug.Log("NDL: ScoreScene - StateMachineExit_DisplayResults");
    }

    // Tên hiển thị của mỗi bên ở màn tổng kết: đội có NHIỀU người chơi (số người camera nhận diện
    // trong ván, không có số liệu thì lấy danh sách TeamSelect) → tên đội (Blue/Red); chỉ 1 người
    // → tên người đó.
    // Chưa nhận diện/ghi nhận được ai trong ván → coi như 1 người chơi tên mặc định Blue_1/Red_1.
    private static string DefaultPlayerName(int slot) => slot == 0 ? "Blue_1" : "Red_1";

    private static string SummaryTeamName(GameSessionManager session, int slot)
    {
        var stats = PlayerRecognitionService.Instance?.GetPlayerStats(slot);
        if (stats == null || stats.Count == 0) return DefaultPlayerName(slot);
        if (stats.Count > 1) return slot == 0 ? session.GetTeamName1() : session.GetTeamName2();
        return stats[0].name;
    }

    // ── MVP + Tia Chớp ────────────────────────────────────────────────────────────────────
    // Quy ước: slot 0 (trái) = đội Blue = Player1; slot 1 (phải) = đội Red = Player2
    // (khớp SetupFromTeamSelect / DisplayTeamResults).
    //  • MVP      : người có nhiều câu ĐÚNG nhất trong cả 2 đội (= điểm cá nhân, vì 1 câu đúng =
    //               1 điểm); hoà thì ai trả lời đúng nhanh hơn (thời gian đúng trung bình thấp hơn).
    //  • Tia Chớp : mỗi đội lấy 1 bạn (tổng 2 người) có thời gian trả lời ĐÚNG trung bình nhanh nhất.

    private void ShowHighlights(GameSessionManager session)
    {
        var recog = PlayerRecognitionService.Instance;
        if (recog == null) return;

        var blueStats = recog.GetPlayerStats(0);
        var redStats = recog.GetPlayerStats(1);

        // Danh sách thành viên trong 2 khung: những bạn camera nhận diện và đã trả lời trong ván
        // (nhiều câu đúng nhất lên đầu). Không có số liệu nào thì dùng danh sách đội từ TeamSelect.
        scoreSceneView.DisplayRoster(
            BuildRoster(blueStats, session.BlueTeamPlayers, 0),
            BuildRoster(redStats, session.RedTeamPlayers, 1));

        PlayerHighlight mvp = PickMvp(blueStats, session.BlueTeamPlayers, redStats, session.RedTeamPlayers);
        List<PlayerHighlight> blueFast = PickFastest(blueStats, session.BlueTeamPlayers, 1);
        List<PlayerHighlight> redFast = PickFastest(redStats, session.RedTeamPlayers, 1);

        Debug.Log($"[ScoreSceneController] Highlights: MVP={(mvp != null ? mvp.name : "none")} " +
                  $"blueFast={blueFast.Count} redFast={redFast.Count} " +
                  $"(blueStats={blueStats.Count}, redStats={redStats.Count})");

        scoreSceneView.DisplayHighlights(mvp, blueFast, redFast);
    }

    private static List<PlayerHighlight> BuildRoster(
        List<PlayerRecognitionService.PlayerRoundStat> stats, List<PlayerInfo> teamList, int slot)
    {
        var result = new List<PlayerHighlight>();
        if (stats != null && stats.Count > 0)
            foreach (var s in stats) result.Add(ToHighlight(s, teamList));
        else
            result.Add(new PlayerHighlight { name = DefaultPlayerName(slot), hairIndex = -1 });
        return result;
    }

    private static PlayerHighlight PickMvp(
        List<PlayerRecognitionService.PlayerRoundStat> blueStats, List<PlayerInfo> blueRoster,
        List<PlayerRecognitionService.PlayerRoundStat> redStats, List<PlayerInfo> redRoster)
    {
        bool found = false;
        PlayerRecognitionService.PlayerRoundStat best = default;
        List<PlayerInfo> bestRoster = null;

        void Consider(List<PlayerRecognitionService.PlayerRoundStat> stats, List<PlayerInfo> roster)
        {
            if (stats == null) return;
            foreach (var s in stats)
            {
                if (s.correct <= 0) continue;
                bool better = !found
                    || s.correct > best.correct
                    || (s.correct == best.correct && s.AvgCorrectAnswerTimeSec < best.AvgCorrectAnswerTimeSec);
                if (better) { best = s; bestRoster = roster; found = true; }
            }
        }

        Consider(blueStats, blueRoster);
        Consider(redStats, redRoster);

        return found ? ToHighlight(best, bestRoster) : null;
    }

    private static List<PlayerHighlight> PickFastest(
        List<PlayerRecognitionService.PlayerRoundStat> stats, List<PlayerInfo> roster, int max)
    {
        var result = new List<PlayerHighlight>();
        if (stats == null) return result;

        var candidates = stats.FindAll(s => s.correct > 0);
        candidates.Sort((a, b) => a.AvgCorrectAnswerTimeSec.CompareTo(b.AvgCorrectAnswerTimeSec));

        for (int i = 0; i < candidates.Count && i < max; i++)
            result.Add(ToHighlight(candidates[i], roster));
        return result;
    }

    // Tìm HairIndex của người đó trong danh sách đội (theo tên) để dùng đúng avatar; không thấy
    // (vd tên mặc định "Player_1") → -1 → View dùng thân nhân vật mặc định.
    private static PlayerHighlight ToHighlight(PlayerRecognitionService.PlayerRoundStat s, List<PlayerInfo> roster)
    {
        int hair = -1;
        if (roster != null)
        {
            PlayerInfo info = roster.Find(p => p != null && p.PlayerName == s.name);
            if (info != null) hair = info.HairIndex;
        }
        return new PlayerHighlight
        {
            name = s.name,
            hairIndex = hair,
            bestTime = s.AvgCorrectAnswerTimeSec,
            score = s.correct   // số câu trả lời ĐÚNG của riêng người này (1 câu đúng = 1 điểm)
        };
    }

    private void OnClickReplay()
    {
        Debug.Log("NDL: ScoreScene - OnClickReplay");
        string lastGame = GameSessionManager.Instance.LastPlayedGame;
        if (!string.IsNullOrEmpty(lastGame))
            SceneManager.LoadScene(lastGame);
        else
            SceneManager.LoadScene("MenuScene");
    }

    private void OnClickChangeTeam()
    {
        // "Bắt Đầu" — KHÔNG load MenuScene nữa (màn Menu cũ của Unity đã bị thay thế hoàn
        // toàn bởi ControlActivity từ Track A). Tái dùng đúng cơ chế đã có cho case "hết giờ
        // tự nhiên": báo ControlActivity tự quay Menu + tự nổi lên trước — màn chiếu vẫn giữ
        // nguyên ScoreScene đang hiện kết quả, không cần đổi gì ở đây.
        Debug.Log("NDL: ScoreScene - OnClickChangeTeam - PushGameEnded (về Menu trên ControlActivity)");
        GameControlBridge.Instance?.PushGameEnded();
    }
}