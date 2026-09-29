using System;
using UnityEngine;

/// <summary>
/// Tất cả tham số tuning cho LaneDashGame (né vật cản kiểu subway-surfer 3D, 2 bên, 3 làn/bên).
/// Chỉnh trực tiếp trên Inspector (foldout Config trong LaneDashController).
///
/// Đơn vị tốc độ/khoảng cách là world unit Unity, quy ước = mét (1 world unit = 1 m), nên tốc độ
/// đã là mét/giây trực tiếp — không cần hệ số quy đổi riêng như bản 2D cũ (canvasUnitsPerMeter).
/// </summary>
[Serializable]
public class LaneDashConfigData
{
    [Header("Lanes")]
    [Tooltip("Số làn mỗi bên (trái/giữa/phải = 3).")]
    public int laneCount = 3;

    [Tooltip("Khoảng cách giữa tâm 2 làn liền kề (mét).")]
    public float laneWidth = 2f;

    [Header("Spawn timing")]
    [Tooltip("Giây giữa 2 lần spawn vật cản/phần thưởng trên 1 bên.")]
    public float spawnInterval = 1.1f;

    [Tooltip("Xác suất item spawn ra là phần thưởng thay vì vật cản.")]
    [Range(0f, 1f)]
    public float rewardChance = 0.25f;

    [Header("Speed ramp (mét/giây)")]
    [Tooltip("Tốc độ lúc bắt đầu (m/s) — ~2.5 m/s ≈ 9 km/h.")]
    public float baseSpeed = 2.5f;

    [Tooltip("Tốc độ tối đa (m/s) — độ khó ngừng tăng sau ngưỡng này. ~5.6 m/s ≈ 20 km/h.")]
    public float maxSpeed = 5.6f;

    [Tooltip("Tốc độ tăng thêm mỗi giây sống sót (m/s²).")]
    public float rampPerSecond = 0.07f;

    [Header("Item scale")]
    [Tooltip("Hệ số scale áp lên mesh/primitive vật cản (bụi cây/đá/gia súc).")]
    public float obstacleScale = 1f;

    [Tooltip("Hệ số scale áp lên mesh/primitive phần thưởng (bông lúa/hoa).")]
    public float rewardScale = 0.6f;

    [Tooltip("Hệ số scale áp lên mesh/primitive vật phẩm đặc biệt (ngựa/tên lửa).")]
    public float powerUpScale = 0.8f;

    [Header("Vật phẩm đặc biệt (ngựa/tên lửa)")]
    [Tooltip("Xác suất 1 lượt spawn là vật phẩm đặc biệt thay vì vật cản/phần thưởng thường — tách riêng, hiếm hơn rewardChance.")]
    [Range(0f, 1f)]
    public float powerUpChance = 0.08f;

    [Tooltip("Hệ số nhân tốc độ khi cưỡi ngựa (vd 1.6 = nhanh hơn 60%).")]
    public float horseBoostMultiplier = 1.6f;

    [Tooltip("Giây hiệu lực khi cưỡi ngựa.")]
    public float horseBoostDuration = 4f;

    [Tooltip("Giây bất tử (bay qua mọi vật cản, tự động né) khi nhặt tên lửa.")]
    public float rocketFlyDuration = 3f;

    [Header("Va chạm (bụi cây/đá/gia súc)")]
    [Tooltip("Giây đứng hình hoàn toàn (tốc độ = 0, mặt đường dừng) sau khi va vật cản — khoá đổi làn trong lúc này.")]
    public float stunDuration = 0.8f;

    [Tooltip("Gia tốc hồi phục (m/s²) — sau khi hết choáng, tốc độ tăng dần từ 0 về lại tốc độ đang chạy thay vì bật ngay lập tức.")]
    public float recoverAccel = 3f;

    [Header("Điểm")]
    public int dodgePoints = 1;
    public int rewardPoints = 3;

    [Tooltip("Điểm mốc để fill-bar trên GameHUD đầy 100% — chỉ ảnh hưởng hiển thị, không giới hạn điểm thật.")]
    public int targetScoreForHud = 40;

    [Header("Layout (world space, mét)")]
    [Tooltip("Z cố định của nhân vật — vật cản băng qua ngưỡng này thì tính va/né.")]
    public float playerZ = 0f;

    [Tooltip("Z nơi vật thể spawn (xa, phía trước) — càng lớn track càng dài, càng lâu tới người chơi.")]
    public float spawnDistanceZ = 40f;

    [Tooltip("Y đặt vật cản/thưởng/vật phẩm trên mặt đường — mesh Kenney (Bush/Rock) thường có " +
             "pivot đã ở đáy (Y=0 là đúng), primitive placeholder có thể cần chỉnh nếu bị lún đất.")]
    public float itemGroundY = 0f;

    [Tooltip("Y đặt nhân vật trên mặt đường.")]
    public float playerGroundY = 0f;

    [Tooltip("Hệ số scale nhân vật (model gốc quá to, che hết làn) — chỉnh nhỏ lại cho vừa laneWidth.")]
    public float playerScale = 0.45f;

    [Tooltip("Giây trượt sang làn mới khi đổi làn.")]
    public float laneSwitchDuration = 0.15f;

    [Tooltip("Giây tối thiểu phải chờ sau khi vừa đổi làn mới được đổi làn tiếp (chống bấm liên tục/nhảy 2 làn 1 lúc).")]
    public float laneSwitchCooldown = 0.2f;

    [Header("Timing chung")]
    public int countdownSeconds = 3;
    public float resultDisplaySeconds = 3f;

    [Header("Level (độ khó tăng dần theo thời gian — kiểu Subway Surfers)")]
    [Tooltip("Giây giữa mỗi lần tăng cấp độ khó — dùng CHUNG cho cả 2 bên và nhạc nền (cùng lúc, " +
             "không tính riêng theo quãng đường mỗi bên, để 2 người chơi luôn đối mặt độ khó như nhau).")]
    public float levelDuration = 20f;

    [Tooltip("Cấp độ tối đa — độ khó ngừng tăng sau cấp này (khớp maxSpeed vẫn là trần tuyệt đối).")]
    public int maxLevel = 8;

    [Tooltip("Mỗi cấp độ: trần tốc độ (maxSpeed) cộng thêm bấy nhiêu m/s.")]
    public float levelMaxSpeedBonus = 0.5f;

    [Tooltip("Mỗi cấp độ: spawnInterval nhân thêm hệ số này (<1 → item xuất hiện dày dần).")]
    [Range(0.5f, 1f)]
    public float levelSpawnIntervalMultiplier = 0.92f;

    [Tooltip("spawnInterval tối thiểu (giây) — chặn không cho dày tới mức không né kịp.")]
    public float minSpawnInterval = 0.55f;

    [Tooltip("Mỗi cấp độ: cao độ (pitch) nhạc nền nhân thêm hệ số này (nhạc nhanh/cao dần theo độ khó).")]
    public float levelMusicPitchMultiplier = 1.04f;

    [Tooltip("Pitch nhạc nền tối đa.")]
    public float maxMusicPitch = 1.5f;
}
