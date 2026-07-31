public enum TestTongHopSceneState
{
    Initialize,    // Setup, kết nối singleton, play music
    ShowQuestion,  // Lấy câu hỏi từ model, hiển thị media + answers
    WaitAnswer,    // Chờ người chơi click (callback từ AnswerDisplayManager)
    Feedback,      // Hiện kết quả đúng/sai, chờ delay rồi quay lại ShowQuestion
    GameOver       // Hết rounds: lưu điểm, navigate sang ScoreScene
}
