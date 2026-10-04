using System.Collections;
using UnityEngine;

/// <summary>
/// Hợp đồng chung cho 1 hiệu ứng hình ảnh tái dùng được (thư viện Effects/) — mỗi effect chỉ
/// thao tác lên RectTransform của chính item được truyền vào (KHÔNG cần biết "rổ"/"đích" nào
/// khác của 1 game cụ thể), để dùng chung được cho mọi game thay vì viết riêng FlyToBasket/
/// PunchBasket như HaiQuaController/DemQuaController đã làm trước đây.
///
/// Đăng ký effect mới: implement interface này + thêm 1 case trong EffectLibrary.Create(type).
/// Khi thêm effect mới, nhớ cập nhật luôn capabilities.json (Assets/Game/Scripts/Core/MiniGameKit/
/// GenericGame/capabilities.json) — đó là nguồn duy nhất mà web tool đọc để hiện lựa chọn effect,
/// không tự suy ra từ code C#.
/// </summary>
public interface IVisualEffect
{
    /// <summary>Chạy hiệu ứng trên 1 RectTransform, gọi onDone khi xong (hoặc ngay lập tức nếu
    /// effect không có coroutine). target không bị destroy bởi effect — ẩn/hiện là việc của
    /// caller sau khi onDone chạy.</summary>
    IEnumerator Play(RectTransform target, EffectParams p, MonoBehaviour runner);
}
