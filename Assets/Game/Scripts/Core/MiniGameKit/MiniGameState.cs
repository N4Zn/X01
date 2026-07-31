/// <summary>
/// State canonical dùng chung cho mọi mini-game dựng từ MiniGameControllerBase.
/// Đặt tên khác GameState (GameTypes.cs, riêng của TestTongHopGame) để tránh đụng độ —
/// codebase này không dùng namespace, mọi type nằm chung 1 global scope.
/// </summary>
public enum MiniGameState
{
    Initialize,
    Tutorial,
    ShowQuestion,
    WaitAnswer,
    Feedback,
    GameOver,
    GameResult
}
