using UnityEngine;

/// <summary>
/// Toàn bộ thông số điều chỉnh của BalloonGame.
/// Asset duy nhất tại: Assets/Game/Resources/GameConfig/BalloonGameConfig.asset
/// Runtime load: Resources.Load&lt;BalloonGameConfig&gt;("GameConfig/BalloonGameConfig")
/// </summary>
[CreateAssetMenu(menuName = "EduGame/BalloonGameConfig", fileName = "BalloonGameConfig")]
public class BalloonGameConfig : ScriptableObject
{
    [Header("── Columns (local x trong field RT, field width=462, center=0) ──")]
    [Tooltip("X tâm của 4 cột. Mép trái field = -231, mép phải = +231.")]
    public float[] columnXBase = { -171f, -51f, 69f, 189f };

    [Tooltip("Jitter ngẫu nhiên ±px trên trục x mỗi lần spawn.")]
    public float xJitter = 15f;

    [Header("── Bounds (local y của field RT) ──")]
    [Tooltip("Exit khi balloon center.y > topBound. Field top=270, balloon half=50 → 320.")]
    public float topBound = 320f;

    [Tooltip("Spawn point dưới màn hình. Field bottom=-270, balloon half=50 → -320.")]
    public float bottomBound = -320f;

    [Tooltip("Khoảng cách pixel giữa điểm spawn của 2 bóng liên tiếp trong 1 cột. Nhỏ = nhiều bóng. Gợi ý: 40 (dày đặc), 150 (vừa), 300 (thưa). Pool 12 items/field → tối đa ~3 bóng/cột cùng lúc.")]
    public float spawnGap = 150f;

    [Header("── Speed (px/s) ──")]
    public float speedBase = 85f;

    [Tooltip("Jitter tốc độ ngẫu nhiên ±px/s mỗi lần spawn.")]
    public float speedJitter = 10f;

    [Header("── Balloon breathing ──")]
    [Tooltip("Tần số nhấp nhô (rad/s).")]
    public float breathSpeed = 2.2f;

    [Tooltip("Biên độ nhấp nhô (scale offset, 0=tắt).")]
    public float breathAmount = 0.07f;

    [Header("── Game flow ──")]
    [Tooltip("Tổng số lần pop đúng (2 người cộng lại) để hoàn thành 1 round.")]
    public int popsRequired = 10;

    [Tooltip("Thời gian hiển thị màn hình RoundClear (giây).")]
    public float roundClearDuration = 1.5f;

    [Tooltip("Tổng thời gian game (giây). Bị override bởi GameSettings nếu có.")]
    public float defaultGameTime = 180f;

    [Header("── Audio ──")]
    [Tooltip("Khoảng cách giữa 2 lần phát âm thanh chữ cái (giây). Bị override bởi GameSettings.RoundEndDelay nếu muốn dùng chung.")]
    public float audioInterval = 4f;
}
