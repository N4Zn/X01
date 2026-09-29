using System;
using UnityEngine;

// ─── Per-lane config ──────────────────────────────────────────────────────────

/// <summary>
/// Thông số riêng cho từng lane bè.
/// Thêm/bớt lane = thêm/bớt element trong mảng Lanes trên Inspector.
/// </summary>
[Serializable]
public class LaneConfigData
{
    [Tooltip("Lệch pha ban đầu (0..1 × spacing). " +
             "0 = bè xuất hiện ngay trên đỉnh. 0.5 = lệch nửa chu kỳ.")]
    public float phaseOffset = 0f;

    [Tooltip("Tốc độ bè trôi xuống (canvas units/s). " +
             "Khác nhau giữa các lane → tự nhiên lệch pha theo thời gian.")]
    public float speed = 110f;

    [Tooltip("Khoảng cách tâm-tâm giữa 2 bè liên tiếp trong lane này (canvas units). " +
             "Nhỏ = dày, lớn = thưa.")]
    public float spacing = 250f;

    [Tooltip("Chiều rộng bè theo trục X (canvas units). Hẹp hơn = khó hơn.")]
    public float raftWidth = 160f;
}

// ─── Global config ────────────────────────────────────────────────────────────

/// <summary>
/// Tất cả tham số tuning cho River Cross.
/// Chỉnh trực tiếp trên Inspector (foldout Config trong RiverCrossController).
///
/// Số lane = Lanes.Length — thêm/xoá element trực tiếp trên Inspector,
/// không cần sửa code.
/// </summary>
[Serializable]
public class RiverCrossConfigData
{
    [Header("Lanes")]
    [Tooltip("Mỗi element = 1 lane. Thêm/xoá để đổi số lane.")]
    public LaneConfigData[] lanes = DefaultLanes();

    [Header("Banks")]
    [Tooltip("X tâm bờ trái — vị trí xuất phát của player (canvas units).")]
    public float leftBankX  = -460f;

    [Tooltip("X tâm bờ phải — đích đến để tính điểm (canvas units).")]
    public float rightBankX =  460f;

    [Header("Raft (global)")]
    [Tooltip("Độ dày bè theo trục Y — vùng chân đáp an toàn (canvas units). " +
             "Giữ global để collision nhất quán.")]
    public float raftThickness = 55f;

    [Tooltip("Dung sai Y thêm khi kiểm tra chân chạm bè (canvas units).")]
    public float landingTolerance = 22f;

    [Header("Player jump")]
    [Tooltip("Thời gian arc jump (giây).")]
    public float jumpDuration = 0.28f;

    [Tooltip("Độ cao đỉnh cung nhảy chính (canvas units).")]
    public float arcHeight = 80f;

    [Header("Fall animation")]
    [Tooltip("Thời gian animation rơi xuống nước (giây).")]
    public float fallDuration = 0.45f;

    [Tooltip("Gia tốc rơi — y = y₀ - fallAcceleration × t² (canvas units/s²).")]
    public float fallAcceleration = 300f;

    [Tooltip("Độ cao cung nhảy nhỏ khi rơi (canvas units).")]
    public float fallArcHeight = 25f;

    [Header("Detection")]
    [Tooltip("Vùng đệm tính từ mép bờ vào (canvas units). " +
             "Touch trong vùng này = bấm lên bờ.")]
    public float bankDetectMargin = 60f;

    [Header("Spawn & layout")]
    [Tooltip("Lùi vào từ tâm mỗi bờ trước khi phân bổ lane (canvas units). " +
             "Tăng nếu lane đầu/cuối quá sát bờ. Mặc định 50 ≈ nửa chiều rộng bờ.")]
    public float laneAreaPadding = 50f;

    [Tooltip("Buffer ngoài màn hình khi spawn/despawn bè (canvas units). " +
             "Cộng vào nửa chiều cao canvas (300). Mặc định 80 → spawn tại Y = 380.")]
    public float laneSpawnBuffer = 80f;

    [Header("Timing")]
    [Tooltip("Giây đếm ngược trước khi bắt đầu.")]
    public int countdownSeconds = 3;

    [Tooltip("Giây hiển thị kết quả trước khi về MenuScene.")]
    public float resultDisplaySeconds = 3f;

    // ── Default lanes ─────────────────────────────────────────────────────────

    /// <summary>
    /// 4 lane mặc định — tốc độ xen kẽ nhanh/chậm để tự lệch pha theo thời gian.
    /// Lane 0 (gần bờ trái) dễ nhất, lane 3 khó nhất.
    /// </summary>
    public static LaneConfigData[] DefaultLanes() => new[]
    {
        new LaneConfigData { phaseOffset = 0.00f, speed =  90f, spacing = 220f, raftWidth = 180f },
        new LaneConfigData { phaseOffset = 0.50f, speed = 115f, spacing = 260f, raftWidth = 160f },
        new LaneConfigData { phaseOffset = 0.25f, speed =  95f, spacing = 240f, raftWidth = 150f },
        new LaneConfigData { phaseOffset = 0.75f, speed = 125f, spacing = 280f, raftWidth = 140f },
    };
}

// ─── Static holder ────────────────────────────────────────────────────────────

/// <summary>Truy cập config từ bất kỳ đâu nếu cần: RiverCrossConfig.Current</summary>
public static class RiverCrossConfig
{
    public static RiverCrossConfigData Current { get; set; } = new RiverCrossConfigData();
}
