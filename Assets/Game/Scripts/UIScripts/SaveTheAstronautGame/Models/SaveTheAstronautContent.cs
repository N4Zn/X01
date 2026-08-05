/// <summary>
/// Nội dung cố định 12 chặng của "Save The Astronaut" — thứ tự thiên văn từ rìa hệ mặt trời vào
/// Trái Đất (7 hành tinh trừ Trái Đất + 5 mặt trăng nổi tiếng bù đủ 12). Thứ tự này GIỐNG NHAU ở
/// cả 2 làn — chỉ có bên đúng/sai (trái/phải) mỗi chặng là random riêng theo từng đội (xem
/// SaveTheAstronautLane._correctIsLeft).
///
/// Ảnh + audio thuyết minh lấy tại Resources/SaveTheAstronaut/{spriteKey}.png|.mp3 — game tự bỏ
/// qua (không lỗi) nếu asset chưa có, xem SaveTheAstronautLane.RevealPlanet/PlayNarration.
/// </summary>
public static class SaveTheAstronautContent
{
    public struct StepInfo
    {
        public string displayName;
        public string spriteKey;

        public StepInfo(string displayName, string spriteKey)
        {
            this.displayName = displayName;
            this.spriteKey = spriteKey;
        }
    }

    public static readonly StepInfo[] Steps =
    {
        new StepInfo("Hải Vương Tinh", "Neptune"),
        new StepInfo("Triton", "Triton"),
        new StepInfo("Thiên Vương Tinh", "Uranus"),
        new StepInfo("Thổ Tinh", "Saturn"),
        new StepInfo("Titan", "Titan"),
        new StepInfo("Mộc Tinh", "Jupiter"),
        new StepInfo("Europa", "Europa"),
        new StepInfo("Hoả Tinh", "Mars"),
        new StepInfo("Phobos", "Phobos"),
        new StepInfo("Kim Tinh", "Venus"),
        new StepInfo("Thuỷ Tinh", "Mercury"),
        new StepInfo("Mặt Trăng", "Moon"),
    };
}
