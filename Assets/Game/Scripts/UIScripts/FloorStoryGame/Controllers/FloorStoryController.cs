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
        int mine = team == Team.Left ? 0 : 1, other = 1 - mine;
        int taskIdx = _begun[mine]++;      // lượt thứ taskIdx (0-based) của đội này
        bool finished = false;
        Action begin = () => world.BeginTask(q, (correct, answer) =>
        {
            if (finished) return;
            finished = true;
            _finished[mine]++;
            onDone(correct, team, answer);
        });

        // Chế độ chung: lượt k chỉ bắt đầu khi đội kia đã xong k lượt (đội xong trước chờ). Giữ BeginTask chứ không giữ onDone
        // để thời gian trả lời ghi log vẫn đúng.
        if (!world.Synchronized || _finished[other] >= taskIdx) begin();
        else StartCoroutine(WaitPartner(world, other, taskIdx, begin));
    }

    readonly int[] _begun = new int[2];
    readonly int[] _finished = new int[2];

    System.Collections.IEnumerator WaitPartner(StoryWorld world, int other, int taskIdx, Action begin)
    {
        world.SetWaiting(true);
        while (_finished[other] < taskIdx) yield return null;
        world.SetWaiting(false);
        begin();
    }
}
