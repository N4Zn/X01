using System;

/// <summary>
/// Model khớp 1-1 với file game.json do web tool (xem SCHEMA.md) xuất ra — parse bằng
/// JsonUtility. KHÔNG dùng Dictionary (JsonUtility không hỗ trợ) — mọi "chỗ cắm" (effect slot) là
/// field tên cố định thay vì key tuỳ ý.
///
/// HAI PHIÊN BẢN FILE (xem GenericGameMigration.Parse):
///   • schemaVersion 2 (hiện tại) → GenericGamePackageV2: mỗi round có 2 nhóm slot (question/answers),
///     mỗi slot = vị trí + nội dung (ảnh/chữ/icon/âm thanh/hiệu ứng riêng). Controller chỉ làm việc với bản này.
///   • schemaVersion 1 (game cũ) → GenericGamePackage (+RoundSpec/RoundAnswerSpec...) — chỉ còn để PARSE rồi
///     CHUYỂN sang V2 ngay lúc nạp (GenericGameMigration.FromV1), không dùng ở runtime.
///
/// Field "params" của 1 effect bị đổi tên thành "effectParams" trong JSON vì "params" là từ khoá
/// C#, không đặt tên field được — web tool PHẢI xuất đúng tên "effectParams".
/// </summary>
[Serializable]
public class GenericGameMeta
{
    public string gameId;       // PascalCase, khớp tên dùng trong GameRegistry + tên folder Resources
    public string displayName;
    public string category;     // chỉ để tham khảo, không dùng để quyết định logic
}

[Serializable]
public class GenericGameSettings
{
    public string playMode = "Combined";     // "Combined" | "Independent" — khớp MiniGamePlayMode
    public string answerMode = "Single";     // "Single" | "MultiSelect" | "OrderedSequence" | "SumToTarget" — khớp AnswerMode
    public string scoringMode = "flat";      // "flat" | "perCorrectCount"
    public int flatPoints = 1;
    public string countdownMode = "none";    // "none" | "nextInN"
    public int countdownSeconds = 3;         // CHƯA dùng thật — Kit hiện hardcode 3s, giữ field để dành tương lai
    /// <summary>true (mặc định, hành vi gốc của Kit) = chạm SAI kết thúc round ngay. false = cho
    /// thử lại — bên vừa sai được mở khoá lại NGAY ô đó sau khoảng feedbackDelayWrong, round KHÔNG
    /// tính là kết thúc, chơi tiếp cùng câu hỏi tới khi đúng. Combined mode: nếu CẢ 2 bên cùng sai
    /// thì Kit vẫn kết thúc round (giới hạn đã biết, xem SCHEMA.md) — false chỉ có tác dụng khi còn
    /// ít nhất 1 bên chưa sai.</summary>
    public bool wrongEndsRound = true;
    /// <summary>true (mặc định, hành vi gốc của Kit) = ô đáp án tự tô XANH (đúng)/ĐỎ (sai)/XÁM
    /// (khoá) khi round kết thúc — màu nền (bgImage.color) có sẵn trong ButtonItem.SetState().
    /// false = tắt hẳn 3 màu này (GenericGameController ghi đè lại màu về bình thường NGAY SAU khi
    /// Kit tô, 1 frame sau — không sửa được tận gốc trong ButtonDisplay.cs vì dùng chung mọi game).</summary>
    public bool showCorrectWrongTint = true;
    /// <summary>Số điểm mục tiêu HIỂN THỊ LÊN MÀN HÌNH qua decoration bind="targetScore" — CHỈ để
    /// hiện, KHÔNG có logic thắng/thua/kết thúc game khi đạt tới.</summary>
    public bool hasTargetScore;
    public float targetScore;
    /// <summary>true = câu hỏi CHỈ phát âm thanh (âm thanh của các slot câu hỏi) — ẩn hẳn chữ/ảnh/icon
    /// câu hỏi (dạng "nghe và chọn"). Âm thanh câu hỏi CHỈ phát khi playMode="Combined".</summary>
    public bool questionAudioOnly;
    /// <summary>Cỡ chữ (point size) của chữ trong slot câu hỏi — 0 (mặc định) = tự co vừa ô. > 0 = ghi đè.</summary>
    public float questionFontSize;
    /// <summary>Cỡ chữ (point size) của chữ trên mỗi nút đáp án (TextMeshProUGUI) — 0 (mặc định) =
    /// giữ nguyên cỡ chữ đặt sẵn trong prefab. > 0 = ghi đè qua ButtonItem.SetFontSize().</summary>
    public float answerFontSize;
    /// <summary>Màu chữ câu hỏi, hex "#RRGGBB"/"#RRGGBBAA". Rỗng (mặc định) = trắng như cũ.</summary>
    public string questionTextColor = "";
    /// <summary>Màu chữ đáp án, hex "#RRGGBB"/"#RRGGBBAA". Rỗng (mặc định) = giữ màu đặt sẵn trong prefab nút đáp án.</summary>
    public string answerTextColor = "";
    /// <summary>Số cột icon trong 1 slot (câu hỏi lẫn đáp án) — mặc định 2.</summary>
    public int iconColumns = 2;
}

/// <summary>Toạ độ/kích thước tính theo % — dùng CHUNG cho cả 2 nửa màn hình (bên phải tự mirror
/// ngang quanh tâm nửa đó, xem GenericGameController.MirrorRect). 0,0 = góc trên-trái của NỬA màn
/// hình (không phải toàn màn hình).</summary>
[Serializable]
public struct RectPct
{
    public float xPct, yPct, wPct, hPct;
}

/// <summary>[v1] Vị trí ngẫu nhiên lại MỖI ROUND trong `answerArea` — CHỈ còn để parse game v1, xem
/// GenericGameMigration.FromV1. Ở v2 thay bằng GroupSpec.arrangement="random" + GroupSpec.random.</summary>
[Serializable]
public class RandomAreaSpec
{
    public bool enabled;
    public int count;
    public float itemWPct;
    public float itemHPct;
    public float gapPct;
}

/// <summary>"Spawn liên tục" — đáp án xuất hiện định kỳ, trôi theo 1 hướng cố định qua hết vùng
/// (GroupSpec.area của nhóm đáp án ở round hiện tại) rồi tự biến mất nếu không chạm trúng (KHÔNG
/// phạt điểm). Là cờ CHUNG của cả game (`layout.spawnFlow.enabled`): bật = mọi round đều dùng
/// spawn, GHI ĐÈ cách sắp xếp (manual/matrix/random) của nhóm đáp án.</summary>
[Serializable]
public class SpawnFlowSpec
{
    public bool enabled;
    public string direction = "BottomToTop"; // "BottomToTop" | "TopToBottom" | "LeftToRight" | "RightToLeft"
    public float itemWPct = 15f;
    public float itemHPct = 15f;
    /// <summary>% chiều di chuyển (theo trục chính của direction) mà item đi được MỖI GIÂY — vd
    /// speedPct=25 với hướng dọc nghĩa là mất ~4s để đi hết 100% chiều cao.</summary>
    public float speedPct = 25f;
    public float spawnIntervalSec = 1f;
    /// <summary>Số item tối đa cùng lúc trên màn hình (1 bên) — chặn spawn thêm nếu đã đạt mức này.</summary>
    public int maxConcurrent = 4;
}

/// <summary>Bố cục/giao diện CẤP GAME (chung mọi round): nền, nhạc nền, khung, vùng thu thập, spawn, item
/// trang trí. Bố cục câu hỏi/đáp án nằm ở từng round (RoundSpecV2). Các field `slots/answerArea/
/// randomArea/questionArea/slotShape` là của game v1 — chỉ còn để parse rồi chuyển đổi.</summary>
[Serializable]
public class GenericGameLayout
{
    // ── [v1] chỉ để parse game cũ ─────────────────────────────────────────────
    public RectPct[] slots;
    public RectPct answerArea;
    public RandomAreaSpec randomArea;
    public RectPct questionArea;
    public string slotShape = "rectangle";
    /// <summary>"stage" = toạ độ slot câu hỏi (chế độ Combined) tính theo CẢ màn hình. Thiếu = file cũ (theo nửa màn hình) → Migration quy đổi.</summary>
    public string questionSpace;

    // ── Cấp game (v1 + v2) ────────────────────────────────────────────────────
    public SpawnFlowSpec spawnFlow;     // khác null + enabled = đáp án dạng spawn liên tục ở MỌI round
    /// <summary>Mảng Ô ĐÍCH cho hiệu ứng FlyToStay ("Bay tới & Ở lại") — KHÁC HẲN slot đáp án (nơi hiện
    /// đáp án để bấm): đây là nơi đáp án ĐÃ CHỌN ĐÚNG bay tới rồi Ở LẠI (vd giỏ chứa chất dần trong
    /// round). RESET mỗi round mới — xem GenericGameController.ResetCollectSlots.</summary>
    public RectPct[] collectSlots;
    /// <summary>true = BỎ QUA `collectSlots` tĩnh, tự tính lại 1 lưới MỚI mỗi round lấp đầy `collectArea` vừa khít
    /// đúng số đáp án ĐÚNG của round đó.</summary>
    public bool collectAutoStretch;
    public RectPct collectArea;
    public int collectCols = 1;
    public bool collectFillReverse;
    /// <summary>true = đáp án có giá trị SỐ N chiếm N ô LIỀN NHAU trong `collectSlots` (thanh nhiên liệu). Tự bật khi
    /// `settings.answerMode="SumToTarget"`.</summary>
    public bool collectFillByValue;
    /// <summary>Tên file ảnh khung 9-slice đặt DƯỚI chữ của slot câu hỏi (null/rỗng = không khung).</summary>
    public string qFrame;
    /// <summary>Nhiều ảnh nền ô đáp án, gán XOAY VÒNG theo thứ tự ô cho đáp án CHƯA có ảnh nền (riêng/chung nhóm).</summary>
    public string[] slotFrames;
    public string background;
    public string backgroundAudio;
    /// <summary>Thứ tự vẽ DƯỚI→TRÊN (nền luôn dưới cùng, không nằm trong mảng): "question" | "answers" | "collect" (đáp án bay sau khi chọn) |
    /// "feedback" (icon ✔/✖) | "deco:&lt;id&gt;" (từng item). Thiếu/rỗng → mặc định question &lt; item &lt; answers &lt; collect &lt; feedback.
    /// Khoá còn thiếu được bổ sung giống web (GenericGameController.NormalizeLayerKeys).</summary>
    public string[] layerOrder;
    /// <summary>ITEM — nhân vật/đồ vật trang trí cho CẢ GAME (vd con thỏ), luôn hiện ở mọi round.</summary>
    public DecorationSpec[] decorations;
}

/// <summary>Chữ hiển thị trên 1 item, bind vào 1 biến điểm số có sẵn thay vì gõ tay. "" = không hiện chữ.</summary>
[Serializable]
public class DecorationText
{
    public string bind = "";    // "" | "roundScore" (điểm round gần nhất, +N) | "totalScore" | "targetScore"
    public float fontSizePct = 7f; // % chiều CAO nửa màn hình — quy đổi ra px lúc runtime
}

/// <summary>Phản hồi của ITEM theo sự kiện (mặc định KHÔNG có hiệu ứng nào) — chỉ ở bên vừa trả lời:
/// onCorrect (đúng, hết round) / onPartial (đúng 1 phần, còn đáp án phải chọn) / onWrong (sai) /
/// onClick (item bị chạm vào — cần raycast). FlyOff/FlyTo/FlyToStay bị BỎ QUA ở item.</summary>
[Serializable]
public class ItemFxSet
{
    public ActionFx onCorrect;
    public ActionFx onPartial;
    public ActionFx onWrong;
    public ActionFx onClick;
}

/// <summary>1 item trang trí đặt cố định trên map. Hiệu ứng chờ `effect` mặc định Breathing; thêm `fx` = phản hồi theo
/// sự kiện (xem ItemFxSet). Không có `fx.onClick` thì KHÔNG bắt chạm (raycastTarget=false).</summary>
[Serializable]
public class DecorationSpec
{
    public string id;
    public string image;    // tên file trong assets/, null = không có ảnh (chỉ hiện chữ nếu có)
    /// <summary>true (mặc định) = ảnh tự lật ngang ở nửa PHẢI — CHỈ ảnh, KHÔNG áp dụng cho text.</summary>
    public bool mirrorImage = true;
    public float xPct, yPct, wPct, hPct;
    /// <summary>true = item này (vd tên lửa) bay thẳng LÊN khỏi màn hình khi bên đó THẮNG round.</summary>
    public bool launchOnComplete;
    public EffectSpec effect;
    public ItemFxSet fx;
    public DecorationText text;
}

[Serializable]
public class EffectSpec
{
    public string type = "None";    // khớp enum EffectType — "None"/"Punch"/"Shake"/"FadeOut"/"FlyOff"/"FlyTo"/"FlyToStay"/"Breathing"
    public EffectParams effectParams;
}

/// <summary>1 "chỗ cắm" (vd onCorrectTap) chạy ĐƯỢC NHIỀU effect CÙNG LÚC + 1 âm thanh tuỳ chọn.</summary>
[Serializable]
public class ActionFx
{
    public EffectSpec[] effects;
    public string sound; // tên file trong gói assets (vd "correct.mp3"), null = không phát âm thanh
    /// <summary>CHỈ có ý nghĩa với onCorrectTap/onWrongTap/onCorrectRemove (đáp án). true (mặc định) = có phát âm thanh —
    /// dùng `sound` nếu đã set, KHÔNG thì âm thanh đúng/sai MẶC ĐỊNH của Kit. false = tắt hẳn.</summary>
    public bool playSound = true;
    /// <summary>CHỈ có ý nghĩa với onCorrectTap/onWrongTap/onCorrectRemove. true (mặc định) = hiện icon ✔/✖ bounce của Kit.</summary>
    public bool showIcon = true;
}

[Serializable]
public class GenericGameEffects
{
    public ActionFx onIdle;
    public ActionFx onCorrectTap;
    public ActionFx onWrongTap;
    public ActionFx onCorrectRemove;
}

/// <summary>1 tập ảnh đặt tên, dùng chung cho nhiều slot qua SlotSpec.imagePool — soạn 1 lần.</summary>
[Serializable]
public class ImagePool
{
    public string id;
    public string name;
    public string[] images; // tên file trong assets/, KHÔNG rỗng (rỗng thì bỏ qua random, giữ nguyên image cũ nếu có)
}

// ══════════════════════════════════════════════════════════════════════════════
// schemaVersion 2 — mỗi round có 2 nhóm slot
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Hiệu ứng GHI ĐÈ riêng 1 slot đáp án/câu hỏi: trigger nào có ≥1 effect hoặc âm thanh riêng thì THAY HẲN hiệu ứng
/// chung (GenericGameEffects) ở trigger đó; trống = dùng hiệu ứng chung. Slot câu hỏi chỉ dùng `onIdle`.</summary>
[Serializable]
public class SlotFx
{
    public ActionFx onIdle;
    public ActionFx onCorrectTap;
    public ActionFx onWrongTap;
    public ActionFx onCorrectRemove;
}

/// <summary>1 slot (câu hỏi HOẶC đáp án) = vị trí + nội dung. Toạ độ % của NỬA TRÁI (nửa phải tự mirror), bỏ qua ở arrangement="random"
/// (vị trí xáo mới mỗi round trong GroupSpec.area). Nội dung: `image` = nền (không có thì lấy GroupSpec.bgImage), `icon` = icon nhỏ nằm
/// trên nền (không có thì lấy GroupSpec.bgIcon), `text` = chữ — nhưng CÓ icon thì `text` là SỐ LƯỢNG icon và KHÔNG hiện chữ
/// (chữ và icon không bao giờ hiện cùng lúc). `sound`: slot đáp án = phát khi bấm; slot câu hỏi = âm thanh câu hỏi (chỉ Combined).</summary>
[Serializable]
public class SlotSpec
{
    public float xPct, yPct, wPct, hPct;
    public string image;
    /// <summary>id của 1 ImagePool — nếu set, BỎ QUA `image`, random 1 ảnh trong pool MỖI LẦN game khởi động.</summary>
    public string imagePool;
    public string text;
    public string icon;
    public string sound;
    public bool correct;
    public SlotFx fx;
}

[Serializable]
public class GroupMatrixSpec
{
    public int cols = 3;
    public float itemWPct = 22f;
    public float itemHPct = 22f;
}

[Serializable]
public class GroupRandomSpec
{
    public float itemWPct = 15f;
    public float itemHPct = 20f;
    /// <summary>Khoảng trống % THÊM giữa 2 ô liền kề (0 = chỉ cần không đè lên nhau).</summary>
    public float gapPct = 2f;
}

/// <summary>Nhóm slot của 1 round (câu hỏi hoặc đáp án). `arrangement`: "manual" | "matrix" (vị trí đã tính sẵn vào slots[]) |
/// "random" (Unity xáo vị trí mỗi round trong `area`, cỡ ô = random.itemWPct×itemHPct). `shape` "circle" đổi nền nút đáp án sang tròn.</summary>
[Serializable]
public class GroupSpec
{
    public string arrangement = "manual";
    public RectPct area;
    public GroupMatrixSpec matrix;
    public GroupRandomSpec random;
    public string shape = "rectangle";
    public string bgImage;
    public string bgIcon;
    /// <summary>Slot câu hỏi (Independent): true = nửa phải lật đối xứng, false = giữ chiều đọc. Combined: câu hỏi chung 1 vùng, bỏ qua.</summary>
    public bool mirror = true;
    // Chỉ dùng cho nhóm collect (vùng thu thập của round, hiệu ứng FlyToStay):
    public bool autoStretch;   // tự chia area thành đúng số đáp án đúng của round
    public bool fillByValue;   // đáp án "N" chiếm N ô liền nhau (thanh nhiên liệu)
    public bool fillReverse;   // số ô điền ngược (đã bake vào toạ độ khi matrix; dùng cho autoStretch)
    public SlotSpec[] slots;
}

[Serializable]
public class RoundSpecV2
{
    /// <summary>Mục tiêu tổng của round — chỉ dùng khi settings.answerMode="SumToTarget". 0 = mặc định bằng SỐ Ô của collectSlots.</summary>
    public int target;
    public GroupSpec question;
    public GroupSpec answers;
    /// <summary>Vùng thu thập của round (ô đích của FlyToStay) — toạ độ nửa trái, nửa phải mirror. Slot chỉ cần toạ độ.</summary>
    public GroupSpec collect;
}

[Serializable]
public class GenericGamePackageV2
{
    public int schemaVersion = 2;
    public GenericGameMeta meta;
    public GenericGameSettings settings;
    public GenericGameLayout layout;
    public GenericGameEffects effects;
    public RoundSpecV2[] rounds;
    public ImagePool[] imagePools;
}

// ══════════════════════════════════════════════════════════════════════════════
// schemaVersion 1 — CHỈ để parse game cũ rồi chuyển sang V2 (GenericGameMigration.FromV1)
// ══════════════════════════════════════════════════════════════════════════════

[Serializable]
public class RoundQuestionSpec
{
    public string text;
    public string image;
    public string audio;
}

[Serializable]
public class RoundAnswerSpec
{
    public string text;
    public string image;
    public string imagePool;
    /// <summary>[v1] > 0 = hiện đáp án dạng "N icon lặp lại" (`image` là file icon). v2: icon + text=số lượng.</summary>
    public int iconCount;
    public bool correct;
}

[Serializable]
public class RoundSpec
{
    public int target;
    public RoundQuestionSpec question;
    public RoundAnswerSpec[] answers;
}

[Serializable]
public class GenericGamePackage
{
    public int schemaVersion = 1;
    public GenericGameMeta meta;
    public GenericGameSettings settings;
    public GenericGameLayout layout;
    public GenericGameEffects effects;
    public RoundSpec[] rounds;
    public ImagePool[] imagePools;
}
