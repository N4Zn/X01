using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "FloorStory" — khung chung cho các game dạng "thế giới có trạng thái" cho bé 3-5 tuổi (Ngày của bé, Xây nhà
/// hình học, Chăm bạn Bi, Vỡ bóng bay, Sàn kỳ diệu, Cổng hình, ...). MỖI GAME 1 SCENE RIÊNG (cùng controller,
/// khác field <c>gameName</c> → chọn <see cref="StoryWorld"/> tương ứng); scene do FloorStorySceneBuilder dựng.
///
/// Chế độ Independent: mỗi đội 1 <see cref="StoryWorld"/> riêng ở nửa màn hình của mình, nhịp riêng.
/// Mỗi "round" của Kit = 1 lượt (task) của đội đó; điểm +1 nếu đúng ngay lần đầu (chạm sai vẫn được thử lại,
/// có gợi ý — kết quả log là đúng/sai của LẦN ĐẦU).
/// Mọi UI do code dựng lúc chạy; ảnh thiếu thì dùng hình/chữ thay thế (xem StoryUI/ShapeSprites).
/// </summary>
public class FloorStoryController : MiniGameControllerBase
{
    [Header("FloorStory — refs")]
    [SerializeField] RectTransform leftWorldRoot;
    [SerializeField] RectTransform rightWorldRoot;
    [SerializeField] AudioSource voiceSource;
    [SerializeField] Button backButton;
    [Tooltip("Tên game của scene này (NgayCuaBe, HinhHoc, ...) — mỗi game 1 scene riêng, do FloorStorySceneBuilder gán. " +
             "Để trống thì lấy theo GameSessionManager.SelectedGameName.")]
    [SerializeField] string gameName = "";

    StoryWorld _left, _right;
    int _taskNumber;
    bool _worldsShown;

    protected override void Start()
    {
        base.Start();
        if (backButton != null) backButton.onClick.AddListener(GoBackToMenu);

        string game = !string.IsNullOrEmpty(gameName) ? gameName : GameSessionManager.ResolveActiveGameName("NgayCuaBe");
        StoryUI.PackGame = game;   // ảnh thay thế của gói zip: Resources/StoryPack/<game>/...
        var ctx = new StoryContext
        {
            runner = this, voice = voiceSource,
            sharedSeed = Environment.TickCount & 0xFFFFF,
            addPoint = t => ScoreManager?.AddPoint(t),
            showIcon = (t, ok) => ShowFeedbackIcon(t, ok),
            hideIcon = t => HideFeedbackIcon(t),
        };
        _left = CreateWorld(game);
        _right = CreateWorld(game);
        _left.Init(ctx, leftWorldRoot, Team.Left);
        _right.Init(ctx, rightWorldRoot, Team.Right);
        // Chưa hiện thế giới trong lúc "Start in 3" — chỉ nền.
        leftWorldRoot.gameObject.SetActive(false);
        rightWorldRoot.gameObject.SetActive(false);
    }

    static StoryWorld CreateWorld(string game)
    {
        // Game "flow" soạn trên web builder: Resources/StoryData/<game>/game.json (FloorStoryPackImporter) → FlowWorld.
        var flow = Resources.Load<TextAsset>("StoryData/" + game + "/game");
        if (flow != null)
        {
            try { return new FlowWorld(game, (System.Collections.Generic.Dictionary<string, object>)FsJson.Parse(flow.text)); }
            catch (Exception e) { Debug.LogError($"[FloorStory] Dữ liệu flow '{game}' lỗi, dùng world mặc định: {e.Message}"); }
        }
        StoryUI.TextStyles = null;   // world code tay không dùng định dạng chữ của game flow trước đó
        switch (game)
        {
            case "NgayCuaBe":  return new NgayCuaBeWorld();
            case "HinhHoc":    return new HinhHocWorld();
            case "BeKhoeManh": return new BeKhoeManhWorld();
            case "VoBongBay":  return new VoBongBayWorld();
            case "SanKyDieu":  return new SanKyDieuWorld();
            case "CauBacQua":  return new CauBacQuaWorld();
            case "DatDoVaoCho": return new DatDoWorld();
            case "DongHoKhongLo": return new DongHoKhongLoWorld();
            case "CongHinh":   return new CongHinhWorld();
            case "TrungMauSac":   return new TrungMauWorld();
            case "HinhDonGian":   return new HinhDonGianWorld();
            case "NhoChuoiHinh":  return new NhoChuoiHinhWorld();
            case "LatTheNhoGiong": return new LatTheWorld();
            case "NangNhe":       return new NangNheWorld();
            case "DemKhoiHop":    return new DemKhoiWorld();
            default:
                Debug.LogWarning($"[FloorStory] Không biết game '{game}', dùng NgayCuaBe.");
                return new NgayCuaBeWorld();
        }
    }

    // Âm đúng + icon ✔ do FlowWorld tự phát lúc chạm đúng (fallback = MusicManager.PlayCorrectSfx + icon mặc định của Kit,
    // trừ khi game có sounds.right riêng) → tắt bản mặc định của base để không phát 2 lần / trễ theo finishSec.
    protected override bool UseDefaultFeedbackFx => false;

    protected override IAnswerDisplay GetDisplayForQuestion(QuestionData q) => null;   // Independent: không dùng display của Kit

    // Mỗi đội tự sinh nội dung trong SetupIndependentDisplay (PullNextQuestion không biết đội nào) → trả khung rỗng.
    protected override QuestionData PullNextQuestion()
    {
        _taskNumber++;
        return new QuestionData { id = "FS_" + _taskNumber, topic = "FloorStory" };
    }

    protected override void SetupIndependentDisplay(Team team, QuestionData q, Action<bool, Team, int[]> onDone)
    {
        if (!_worldsShown)
        {
            _worldsShown = true;
            leftWorldRoot.gameObject.SetActive(true);
            rightWorldRoot.gameObject.SetActive(true);
        }
        var world = team == Team.Left ? _left : _right;
        HideFeedbackIcon(team);
        bool finished = false;
        world.BeginTask(q, (correct, answer) =>
        {
            if (finished) return;
            finished = true;
            onDone(correct, team, answer);
        });
    }

    // Nhịp chuyển lượt, đồng bộ 2 đội, âm câu hỏi lặp 4s, dọn câu khi xong lượt: TẤT CẢ do MiniGameControllerBase lo (cơ chế chung) —
    // ở đây chỉ nối vào world.
    protected override bool SyncIndependentRounds => _left != null && _left.Synchronized;
    protected override void OnWaitingForPartner(Team team, bool on) => (team == Team.Left ? _left : _right).SetWaiting(on);
    protected override void CleanupIndependentDisplay(Team team) => (team == Team.Left ? _left : _right).EndTask();
    protected override float PlayQuestionAudioOnce(QuestionData q) => _left.PlayPrompt();
}
