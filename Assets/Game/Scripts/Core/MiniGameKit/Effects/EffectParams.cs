/// <summary>Loại hiệu ứng có trong thư viện — khớp 1-1 với "type" trong capabilities.json/JSON
/// export từ web tool. Thêm giá trị mới ở đây PHẢI thêm case tương ứng trong EffectLibrary.Create()
/// và mục mô tả trong capabilities.json.</summary>
public enum EffectType
{
    None,
    Punch,      // phóng to rồi co lại tại chỗ — dùng khi chạm đúng
    Shake,      // rung lắc nhẹ tại chỗ — dùng khi chạm sai
    FadeOut,    // mờ dần rồi ẩn — đáp án đúng biến mất sau khi chọn (kiểu MultiSelect)
    FlyOff,     // bay theo hướng+khoảng cách rồi mờ dần — mô phỏng "bay vào giỏ/rổ" không cần biết điểm đến chính xác
    FlyTo,      // bay tới 1 ĐIỂM CỐ ĐỊNH (targetXPct/targetYPct) rồi co nhỏ+mờ dần — dùng khi cần set đúng vị trí "rổ"/bộ đếm
    FlyToStay,  // bay tới 1 ĐIỂM CỐ ĐỊNH rồi Ở LẠI VĨNH VIỄN (không mờ/không bị huỷ) — khác FlyTo: dùng khi muốn đáp án đúng "đọng lại" thành 1 vùng hiển thị mới (vd giỏ chứa chất đầy dần cả game), không phải biến mất
    Breathing,  // phóng to-nhỏ lặp lại liên tục — hiệu ứng chờ/mời gọi trước khi chạm
}

/// <summary>Bộ tham số dùng chung cho mọi effect — field nào effect không dùng thì bỏ qua.
/// Đơn vị: duration/speed tính bằng giây, distancePct/amplitudePct/targetXPct/targetYPct tính
/// theo % trong NỬA màn hình (0,0 = góc trên-trái, giống quy ước RectPct) để không phụ thuộc độ
/// phân giải thật. targetXPct tự được GenericGameController mirror ngang cho bên phải — xem
/// GenericGameController.MirrorForTeam(), không cần set riêng cho mỗi bên.</summary>
[System.Serializable]
public struct EffectParams
{
    public float duration;
    public float scale;
    public float distancePct;
    public float angleDeg;
    public float amplitudePct;
    public float speed;
    public int loops; // -1 = lặp vô hạn tới khi bị dừng ngoài (StopCoroutine)
    public float targetXPct;
    public float targetYPct;
    /// <summary>CHỈ dùng cho FlyToStay khi có cấu hình `layout.collectSlots` — kích thước Ô ĐÍCH
    /// (% nửa màn hình) lấy từ chính slot đó, GHI ĐÈ cách tính "scale theo kích thước item" của
    /// FlyTo/FlyToStay không-cấu-hình-slot. 0 = không dùng, giữ hành vi cũ (item giữ nguyên kích
    /// thước hiện tại rồi nhân `scale`).</summary>
    public float targetWPct;
    public float targetHPct;
    /// <summary>CHỈ dùng cho FlyToStay: &gt; 0 = bay tới xong đứng yên ngần ấy giây rồi mờ đi và biến mất. 0 = ở lại tới khi round mới (như cũ).</summary>
    public float stayFor;

    public static EffectParams Default(EffectType type)
    {
        switch (type)
        {
            case EffectType.Punch:     return new EffectParams { duration = 0.3f, scale = 1.25f };
            case EffectType.Shake:     return new EffectParams { duration = 0.3f, amplitudePct = 1.5f, speed = 30f };
            case EffectType.FadeOut:   return new EffectParams { duration = 0.25f };
            case EffectType.FlyOff:    return new EffectParams { duration = 0.45f, distancePct = 20f, angleDeg = 90f, scale = 0.4f };
            case EffectType.FlyTo:     return new EffectParams { duration = 0.45f, targetXPct = 50f, targetYPct = 90f, scale = 0.3f };
            case EffectType.FlyToStay: return new EffectParams { duration = 0.45f, targetXPct = 50f, targetYPct = 90f, scale = 1f };
            case EffectType.Breathing: return new EffectParams { duration = 0.6f, scale = 1.08f, loops = -1 };
            default:                   return new EffectParams { duration = 0f };
        }
    }
}
