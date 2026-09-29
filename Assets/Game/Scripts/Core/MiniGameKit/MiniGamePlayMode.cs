/// <summary>
/// 3 chế độ chơi mà MiniGameControllerBase hỗ trợ sẵn:
///
/// Combined    — 2 đội cùng thấy 1 câu hỏi, ai trả lời đúng trước ghi điểm cho đội mình.
///               Mặc định, không cần code gì thêm ngoài 4 layer sẵn có.
/// Solo        — 1 luồng chơi, không thi đua Trái/Phải. Kỹ thuật: vẫn chạy y hệt Combined
///               (dùng chung IAnswerDisplay.Setup), chỉ khác ở chỗ SceneBuilder không tạo cột
///               điểm/tên bên phải (GameHUD tự bỏ qua field null) và GameOver ghi cùng 1 điểm
///               vào cả 2 slot của GameSessionManager.
/// Independent — 2 đội tự nhịp câu hỏi riêng, không chờ nhau (giống
///               TestTongHopController.StartIndependentPlay). Bắt buộc override
///               SetupIndependentDisplay() ở subclass — display cụ thể (ButtonDisplay/
///               FloatingDisplay) đã có sẵn method SetupPlayerIndependent(Team, QuestionData,
///               callback) để gọi thẳng.
/// </summary>
public enum MiniGamePlayMode
{
    Combined,
    Solo,
    Independent
}
