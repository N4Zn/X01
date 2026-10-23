using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PoseTest — mini-game thử nhận diện khung xương (MediaPipe Pose, posebridge-release.aar) trên K02.
///
/// 2 người chơi, mỗi người một nửa khung hình (trái/phải theo vị trí hông, ảnh đã lật gương như camera nhận mặt).
/// Mỗi vòng ra một tư thế (6 tư thế port từ bản HTML chạy trên PC); ai khớp tư thế đủ <see cref="holdResults"/> kết quả liên tiếp
/// trước hết giờ thì ghi điểm theo thứ tự: nhất 3 điểm, nhì 2 điểm. Hết thời gian game → ScoreScene.
///
/// Tốc độ nhận diện trên K02 chỉ ~5-6 kết quả/giây (GPU, 2 người) nên vòng dài hơn bản PC (mặc định 6s) và phải giữ tư thế ~0.4s.
/// Controller viết thẳng như FRTest (không qua MiniGameKit): đây là màn công cụ/thử nghiệm, chưa phải nội dung học.
/// Scene dựng bằng Tools → PoseTest → Build Scene (PoseTestSceneBuilder).
/// </summary>
public class PoseTestController : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] GameHUD hud;

    [Header("Camera + khung xương")]
    [SerializeField] PoseBridgeClient client;
    [SerializeField] RawImage cameraImage;
    [SerializeField] PoseSkeletonGraphic skeleton;

    [Header("Chữ")]
    [SerializeField] TextMeshProUGUI poseText;      // tên tư thế (giữa trên)
    [SerializeField] GameObject sampleRoot;         // khung hình mẫu (giữa, 40% trên)
    [SerializeField] PoseSilhouetteGraphic sampleFigure;
    [SerializeField] TextMeshProUGUI countdownText; // "Start in 3s" / thông báo chờ
    [SerializeField] TextMeshProUGUI leftStatusText;
    [SerializeField] TextMeshProUGUI rightStatusText;
    [SerializeField] TextMeshProUGUI infoText;      // thông số debug (dưới)

    [Header("Luật")]
    [Tooltip("Thời gian mỗi tư thế (s). Dài hơn bản PC vì chỉ ~5 kết quả/giây trên K02.")]
    [SerializeField] float roundSeconds = 6f;
    [Tooltip("Số kết quả liên tiếp phải khớp tư thế mới tính hoàn thành (lọc nhiễu).")]
    [SerializeField] int holdResults = 2;
    [SerializeField] float resultPauseSeconds = 1.5f;
    [Tooltip("Quá số giây này không thấy người trong vùng thì coi như vắng.")]
    [SerializeField] float absentAfterSeconds = 1.5f;

    static readonly Color LeftColor  = new(0.3f, 0.9f, 1f);
    static readonly Color RightColor = new(1f, 0.65f, 0.2f);
    static readonly Color DoneColor  = new(0.35f, 1f, 0.45f);
    static readonly Color OtherColor = new(0.7f, 0.7f, 0.7f);

    class Slot
    {
        public PoseBridgeClient.Person person;
        public float lastSeen = -999f;
        public bool completed;
        public int hold;
        public float completedAt = -1f;
    }

    readonly Slot[] _slots = { new Slot(), new Slot() };
    readonly List<PoseBridgeClient.Person> _others = new();
    readonly int[] _score = new int[2];
    readonly List<RoundRecord> _rounds = new();

    string _leftName = "Người chơi 1", _rightName = "Người chơi 2";
    bool _running, _roundActive, _logExported;
    float _timeRemaining;
    PoseChecks.Def _pose;
    int _completedCount;
    float _roundStart;
    float _hzSum; int _hzSamples;

    int _prevFrameRate;

    /// <summary>Tắt mọi việc nhận diện mặt đang chạy ngầm (ván trước có thể để slot bật). Không tạo plugin mới nếu chưa có.</summary>
    static void SuspendFaceRecognition()
    {
        var face = FindFirstObjectByType<FaceRecognitionPlugin>();
        if (face == null) return;
        face.StopRound();
        face.SetSlotRecognitionEnabled(0, false);
        face.SetSlotRecognitionEnabled(1, false);
        face.SetBoundingBoxEnabled(false);
    }

    void ShowNames() => hud.UpdatePlayerNames(PlayerNameDisplay.Format(_leftName), PlayerNameDisplay.Format(_rightName));

    void Start()
    {
        var session = GameSessionManager.Instance;
        if (session != null)
        {
            _leftName = session.GetDisplayName1();
            _rightName = session.GetDisplayName2();
        }
        _timeRemaining = GameSettings.Instance != null ? GameSettings.Instance.GameTime : 100;

        hud.Initialize(null, _leftName, _rightName);
        hud.HideScoreBars();
        ShowNames();

        // Game này chỉ cần camera cho khung xương: tắt nhận diện mặt (đang dùng chung camera + CPU) và chạm LiDAR.
        SuspendFaceRecognition();
        LidarTouchBridge.Instance?.SetTouchEnabled(false);
        _prevFrameRate = Application.targetFrameRate;
        Application.targetFrameRate = 30; // nội dung chỉ ~6 kq/s: không cần 60 fps, nhường GPU cho suy luận

        poseText.gameObject.SetActive(false);
        sampleRoot.SetActive(false);
        leftStatusText.text = rightStatusText.text = "";
        client.ResultUpdated += OnResult;

        _running = true;
        StartCoroutine(GameLoop());
    }

    void Update()
    {
        if (client.CameraTexture != null && cameraImage.texture != client.CameraTexture)
        {
            cameraImage.texture = client.CameraTexture;
            cameraImage.color = Color.white;
        }
        cameraImage.uvRect = client.UvRect; // luồng camera chưa xoay/lật: bù bằng uvRect (khung xương đã tính trên ảnh xoay/lật)

        if (_running && _gameClockOn)
        {
            _timeRemaining -= Time.deltaTime;
            hud.UpdateTimer(_timeRemaining);
            if (_timeRemaining <= 0f) { _timeRemaining = 0f; _running = false; OnGameEnd(); }
        }

        for (int i = 0; i < 2; i++)
            if (_slots[i].person != null && Time.time - _slots[i].lastSeen > absentAfterSeconds) _slots[i].person = null;

        UpdateInfo();

        if (Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene("MenuScene");
    }

    bool _gameClockOn;

    // ── Nhận kết quả mới từ native ────────────────────────────────────────────

    void OnResult()
    {
        _hzSum += client.ResultHz; _hzSamples++;

        var best = new PoseBridgeClient.Person[2];
        Assign(client.People, best, null);

        for (int i = 0; i < 2; i++)
        {
            var s = _slots[i];
            if (best[i] == null) continue;
            s.person = best[i];
            s.lastSeen = Time.time;
            if (_roundActive && !s.completed)
            {
                s.hold = _pose.check(best[i]) ? s.hold + 1 : 0;
                if (s.hold >= holdResults) Complete(i);
            }
        }
    }

    /// <summary>Gán mỗi người vào nửa trái/phải theo vị trí hông; nếu một nửa có >1 người thì lấy người gần tâm nửa đó nhất, còn lại vào <paramref name="others"/>.</summary>
    static void Assign(IList<PoseBridgeClient.Person> people, PoseBridgeClient.Person[] best, List<PoseBridgeClient.Person> others)
    {
        var bestDist = new[] { float.MaxValue, float.MaxValue };
        others?.Clear();
        foreach (var p in people)
        {
            float x = p.vis[PoseBridgeClient.LHip] > 0.2f && p.vis[PoseBridgeClient.RHip] > 0.2f ? p.HipCenter.x : p.Centroid().x;
            int z = x < 0.5f ? 0 : 1;
            float d = Mathf.Abs(x - (z == 0 ? 0.25f : 0.75f));
            if (d < bestDist[z])
            {
                if (best[z] != null) others?.Add(best[z]);
                best[z] = p; bestDist[z] = d;
            }
            else others?.Add(p);
        }
    }

    void LateUpdate() => RedrawSkeleton();

    void Complete(int slot)
    {
        var s = _slots[slot];
        s.completed = true;
        s.completedAt = Time.time - _roundStart;
        int pts = _completedCount == 0 ? 3 : 2; // nhất 3 điểm, nhì 2 điểm (như bản HTML 3/2/1)
        _completedCount++;
        _score[slot] += pts;
        hud.SetScore(_score[0], _score[1]);
        (slot == 0 ? leftStatusText : rightStatusText).text = $"✓ +{pts}";
    }

    void RedrawSkeleton()
    {
        // Vẽ khung xương nội suy khớp với ảnh đang hiển thị (không phải kết quả mới nhất).
        var best = new PoseBridgeClient.Person[2];
        Assign(client.DisplayPeople, best, _others);
        skeleton.Clear();
        for (int i = 0; i < 2; i++)
            if (best[i] != null)
                skeleton.Add(best[i], _slots[i].completed ? DoneColor : (i == 0 ? LeftColor : RightColor));
        foreach (var o in _others) skeleton.Add(o, OtherColor);
        skeleton.Commit();
    }

    // ── Vòng chơi ─────────────────────────────────────────────────────────────

    IEnumerator GameLoop()
    {
        // Chờ native nạp model + biên dịch shader GPU (lần đầu ~13s sau khi mở app).
        float t0 = Time.time;
        while (!client.IsReady)
        {
            if (client.Status.StartsWith("error"))
            {
                ShowMessage(client.Status);
                yield break;
            }
            ShowMessage($"Đang khởi tạo nhận diện khung xương... {Time.time - t0:0}s");
            yield return null;
        }

        for (int i = 3; i >= 1; i--)
        {
            ShowMessage(CountdownStyle.Format("Start", i));
            yield return new WaitForSeconds(1f);
        }
        countdownText.gameObject.SetActive(false);
        _gameClockOn = true;
        // Nhạc nền lấy từ bản HTML (Resources/audio/Music/posetest.mp3)
        MusicManager.Instance?.PlayCustomBgm(Resources.Load<AudioClip>("audio/Music/posetest"), "posetest");

        var order = new List<int>();
        int round = 0;
        while (_running)
        {
            if (order.Count == 0) order = ShuffledPoseOrder();
            _pose = PoseChecks.All[order[0]]; order.RemoveAt(0);
            round++;

            foreach (var s in _slots) { s.completed = false; s.hold = 0; s.completedAt = -1f; }
            _completedCount = 0;
            leftStatusText.text = rightStatusText.text = "";
            poseText.text = _pose.label;
            poseText.gameObject.SetActive(true);
            sampleFigure.SetPose(_pose.id);
            sampleRoot.SetActive(true);
            _roundStart = Time.time;
            _roundActive = true;
            bool leftPresent = false, rightPresent = false;

            float elapsed = 0f;
            float allDoneAt = -1f;
            while (elapsed < roundSeconds && _running)
            {
                elapsed += Time.deltaTime;
                int active = 0, done = 0;
                for (int i = 0; i < 2; i++)
                    if (_slots[i].person != null) { active++; if (_slots[i].completed) done++; }
                leftPresent |= _slots[0].person != null; rightPresent |= _slots[1].person != null;
                // Mọi người đang có mặt đều xong → kết thúc sớm sau một nhịp ngắn.
                if (active > 0 && done == active) { if (allDoneAt < 0) allDoneAt = elapsed; else if (elapsed - allDoneAt > 0.8f) break; }
                else allDoneAt = -1f;
                yield return null;
            }
            _roundActive = false;
            if (!_running) break;

            _rounds.Add(new RoundRecord
            {
                round = round, poseId = _pose.id,
                leftPresent = leftPresent, rightPresent = rightPresent,
                leftDoneSec = _slots[0].completed ? (float)Math.Round(_slots[0].completedAt, 2) : -1f,
                rightDoneSec = _slots[1].completed ? (float)Math.Round(_slots[1].completedAt, 2) : -1f,
            });

            yield return new WaitForSeconds(resultPauseSeconds);
        }
    }

    static List<int> ShuffledPoseOrder()
    {
        var a = new List<int>();
        for (int i = 0; i < PoseChecks.All.Length; i++) a.Add(i);
        for (int i = a.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }
        return a;
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void ShowMessage(string msg)
    {
        CountdownStyle.Apply(countdownText);
        countdownText.gameObject.SetActive(true);
        countdownText.text = msg;
    }

    void UpdateInfo()
    {
        if (!infoText) return;
        infoText.text = $"{(client.Gpu ? "GPU" : "CPU")}  {client.ResultHz:0.0} kq/s  nguồn {client.Source} {client.SourceFps:0}fps  giải mã {client.DecodeMs}ms suy luận {client.InferMs}ms  " +
                        $"trễ {client.DisplayDelay * 1000f:0}ms  ảnh {(client.CameraTexture != null ? client.CameraTexture.width + "x" + client.CameraTexture.height : "-")}  " +
                        $"fps {1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f):0}  người: {client.People.Count}  [{client.Status}]";
    }

    // ── Kết thúc + log ────────────────────────────────────────────────────────

    void OnGameEnd()
    {
        _roundActive = false;
        MusicManager.Instance?.StopMusic();
        ExportLog();
        var session = GameSessionManager.Instance;
        if (session != null)
        {
            session.RecordScores(_score[0], _score[1]);
            session.LastPlayedGame = "PoseTestGame";
        }
        SceneManager.LoadScene("ScoreScene");
    }

    void OnDestroy()
    {
        _running = false;
        Application.targetFrameRate = _prevFrameRate;
        if (client != null) client.ResultUpdated -= OnResult;
    }

    void ExportLog()
    {
        if (_logExported || _rounds.Count == 0) return;
        _logExported = true;
        var session = new PoseTestSession
        {
            gameName = "PoseTest",
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            time = DateTime.Now.ToString("HH:mm:ss"),
            scoreLeft = _score[0], scoreRight = _score[1],
            totalRounds = _rounds.Count,
            gpu = client.Gpu,
            avgResultHz = _hzSamples > 0 ? (float)Math.Round(_hzSum / _hzSamples, 2) : 0f,
            rounds = _rounds,
        };
        string path = Path.Combine(SharedStorage.LogsDir, $"{DateTime.Now:yyyy-MM-dd_HHmmss}_PoseTest.json");
        try { File.WriteAllText(path, JsonUtility.ToJson(session, true)); Debug.Log($"[PoseTest] Log saved → {path}"); }
        catch (Exception e) { Debug.LogWarning($"[PoseTest] Write failed: {e.Message}"); }
    }

    [Serializable]
    class PoseTestSession
    {
        public string gameName, date, time;
        public int scoreLeft, scoreRight, totalRounds;
        public bool gpu;
        public float avgResultHz;
        public List<RoundRecord> rounds;
    }

    [Serializable]
    class RoundRecord
    {
        public int round;
        public string poseId;
        public bool leftPresent, rightPresent;
        public float leftDoneSec;   // -1 = không hoàn thành
        public float rightDoneSec;  // -1 = không hoàn thành
    }
}
