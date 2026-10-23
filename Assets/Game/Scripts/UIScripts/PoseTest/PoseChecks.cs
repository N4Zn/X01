using UnityEngine;
using P = PoseBridgeClient;

/// <summary>
/// Nhận tư thế bằng hình học landmark — port từ nhan-dien-khung-xuong_9_xephanggon (1).html (6 tư thế).
/// Toạ độ chuẩn hoá 0..1, y hướng xuống. Mỗi hàm trả false nếu điểm cần dùng bị che (vis thấp).
/// Ngưỡng giữ nguyên bản HTML; chỉnh theo khoảng cách camera thật khi thử trên K02.
/// </summary>
public static class PoseChecks
{
    public delegate bool Check(P.Person p);

    public struct Def { public string id; public string label; public Check check; }

    public const float MinVis = 0.35f;

    public static readonly Def[] All =
    {
        new Def { id = "hands_raised",    label = "Giơ hai tay lên cao",         check = HandsRaised },
        new Def { id = "squat",           label = "Ngồi xổm xuống",              check = Squatting },
        new Def { id = "arms_cross",      label = "Khoanh tay chữ X trước ngực", check = ArmsCrossed },
        new Def { id = "hands_on_hips",   label = "Chống tay lên hông",          check = HandsOnHips },
        new Def { id = "one_hand_raised", label = "Giơ một tay lên cao",         check = OneHandRaised },
        new Def { id = "one_leg_stand",   label = "Đứng một chân",               check = OneLegStand },
    };

    static bool Seen(P.Person p, params int[] idx)
    {
        foreach (int i in idx) if (p.vis[i] < MinVis) return false;
        return true;
    }

    static bool HandsRaised(P.Person p)
    {
        if (!Seen(p, P.LShoulder, P.RShoulder, P.LWrist, P.RWrist)) return false;
        const float m = 0.03f;
        return p.pt[P.LWrist].y < p.pt[P.LShoulder].y - m && p.pt[P.RWrist].y < p.pt[P.RShoulder].y - m;
    }

    static bool Squatting(P.Person p)
    {
        if (!Seen(p, P.LHip, P.RHip, P.LKnee, P.RKnee)) return false;
        float gap = (p.pt[P.LKnee].y + p.pt[P.RKnee].y) * 0.5f - (p.pt[P.LHip].y + p.pt[P.RHip].y) * 0.5f;
        return gap < 0.12f;
    }

    static bool ArmsCrossed(P.Person p)
    {
        if (!Seen(p, P.LShoulder, P.RShoulder, P.LWrist, P.RWrist, P.LHip, P.RHip)) return false;
        float shoulderY = (p.pt[P.LShoulder].y + p.pt[P.RShoulder].y) * 0.5f;
        float hipY = (p.pt[P.LHip].y + p.pt[P.RHip].y) * 0.5f;
        float chestY = shoulderY + (hipY - shoulderY) * 0.5f;
        const float vMargin = 0.16f;
        bool atChest = Mathf.Abs(p.pt[P.LWrist].y - chestY) < vMargin && Mathf.Abs(p.pt[P.RWrist].y - chestY) < vMargin;
        float minX = Mathf.Min(p.pt[P.LShoulder].x, p.pt[P.RShoulder].x), maxX = Mathf.Max(p.pt[P.LShoulder].x, p.pt[P.RShoulder].x);
        float width = Mathf.Max(maxX - minX, 0.001f);
        float lx = p.pt[P.LWrist].x, rx = p.pt[P.RWrist].x;
        bool inside = lx > minX && lx < maxX && rx > minX && rx < maxX;
        bool close = Mathf.Abs(lx - rx) < width * 0.6f;
        return atChest && inside && close;
    }

    static bool HandsOnHips(P.Person p)
    {
        if (!Seen(p, P.LHip, P.RHip, P.LWrist, P.RWrist)) return false;
        const float m = 0.09f;
        bool left = Mathf.Abs(p.pt[P.LWrist].x - p.pt[P.LHip].x) < m && Mathf.Abs(p.pt[P.LWrist].y - p.pt[P.LHip].y) < m;
        bool right = Mathf.Abs(p.pt[P.RWrist].x - p.pt[P.RHip].x) < m && Mathf.Abs(p.pt[P.RWrist].y - p.pt[P.RHip].y) < m;
        return left && right;
    }

    static bool OneHandRaised(P.Person p)
    {
        if (!Seen(p, P.LShoulder, P.RShoulder, P.LWrist, P.RWrist)) return false;
        const float m = 0.03f;
        bool l = p.pt[P.LWrist].y < p.pt[P.LShoulder].y - m;
        bool r = p.pt[P.RWrist].y < p.pt[P.RShoulder].y - m;
        return l != r;
    }

    static bool OneLegStand(P.Person p)
    {
        if (!Seen(p, P.LKnee, P.RKnee, P.LAnkle, P.RAnkle)) return false;
        const float m = 0.1f;
        bool l = p.pt[P.LAnkle].y < p.pt[P.RKnee].y - m;
        bool r = p.pt[P.RAnkle].y < p.pt[P.LKnee].y - m;
        return l != r;
    }
}
