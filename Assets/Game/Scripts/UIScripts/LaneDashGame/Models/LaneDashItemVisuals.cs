using System.Collections.Generic;
using Benjathemaker;
using UnityEngine;

/// <summary>Biến thể cụ thể của vật cản/phần thưởng/vật phẩm đặc biệt trên cánh đồng.</summary>
public enum LaneItemVariant
{
    // Vật cản
    Bush, Rock, Buffalo, Cow, Pig, Chicken,
    // Phần thưởng
    RiceStalk, Flower, Coin, Ruby, GoldBar, StarGem, Diamond,
    // Vật phẩm đặc biệt
    Horse, Rocket,
}

/// <summary>
/// Nhà máy dựng visual 3D cho từng biến thể — dùng chung bởi LaneTrack (spawn/pool theo biến thể).
///
/// Cow/Pig/RiceStalk/Flower/Bush/Rock đã có mesh Kenney đúng loại (kenney_nature-kit,
/// kenney_cube-pets_1.0). Buffalo/Chicken/Horse/Rocket CHƯA có pack đúng loại — đang dùng mesh
/// gần giống nhất tạm thời (elephant/chick/deer/star, ghi chú tại từng dòng trong
/// MeshResourcePaths). Thay lại khi có pack đúng — chỉ cần sửa đường dẫn, không cần sửa gì khác.
/// </summary>
public static class LaneDashItemVisuals
{
    public static readonly LaneItemVariant[] ObstacleVariants =
    {
        LaneItemVariant.Bush, LaneItemVariant.Rock, LaneItemVariant.Buffalo,
        LaneItemVariant.Cow, LaneItemVariant.Pig, LaneItemVariant.Chicken,
    };

    public static readonly LaneItemVariant[] RewardVariants =
    {
        LaneItemVariant.RiceStalk, LaneItemVariant.Flower,
        LaneItemVariant.Coin, LaneItemVariant.Ruby, LaneItemVariant.GoldBar,
        LaneItemVariant.StarGem, LaneItemVariant.Diamond,
    };

    public static readonly LaneItemVariant[] PowerUpVariants =
    {
        LaneItemVariant.Horse, LaneItemVariant.Rocket,
    };

    /// <summary>Đường dẫn Resources.Load cho biến thể đã có mesh Kenney thật. Biến thể không có
    /// trong bảng này sẽ dùng primitive màu (xem GetPrimitive/GetColor).</summary>
    static readonly Dictionary<LaneItemVariant, string> MeshResourcePaths = new()
    {
        { LaneItemVariant.Bush,      "kenney_nature-kit/Models/FBX format/plant_bushLarge" },
        { LaneItemVariant.Rock,      "kenney_nature-kit/Models/FBX format/rock_largeA" },
        { LaneItemVariant.Cow,       "kenney_cube-pets_1.0/Models/FBX format/animal-cow" },
        { LaneItemVariant.Pig,       "kenney_cube-pets_1.0/Models/FBX format/animal-pig" },
        { LaneItemVariant.Chicken,   "kenney_cube-pets_1.0/Models/FBX format/animal-chick" },    // tạm: gà con, chưa có gà trưởng thành
        { LaneItemVariant.Buffalo,   "kenney_cube-pets_1.0/Models/FBX format/animal-elephant" },  // tạm: voi, chưa có trâu
        { LaneItemVariant.Horse,     "kenney_cube-pets_1.0/Models/FBX format/animal-deer" },      // tạm: hươu, chưa có ngựa
        { LaneItemVariant.Rocket,    "kenney_platformer-kit/Models/FBX format/star" },            // tạm: ngôi sao, chưa có tên lửa
        { LaneItemVariant.RiceStalk, "kenney_nature-kit/Models/FBX format/crops_wheatStageB" },
        { LaneItemVariant.Flower,    "kenney_nature-kit/Models/FBX format/flower_redA" },
        // Dùng thẳng Prefab (không phải .fbx thô) — prefab BTM đã có sẵn material màu đúng, chỉ gỡ
        // script SimpleGemsAnim đi kèm ở CreateVisual (xem chú thích ở đó) rồi thay bằng LaneItemSpin.
        { LaneItemVariant.Coin,      "BTM_Assets/BTM_Items_Gems/Prefabs/Coin" },
        { LaneItemVariant.Ruby,      "BTM_Assets/BTM_Items_Gems/Prefabs/Ruby" },
        { LaneItemVariant.GoldBar,   "BTM_Assets/BTM_Items_Gems/Prefabs/GoldBar" },
        { LaneItemVariant.StarGem,   "BTM_Assets/BTM_Items_Gems/Prefabs/StarGem1" },
        { LaneItemVariant.Diamond,   "BTM_Assets/BTM_Items_Gems/Prefabs/Diamondo" },
    };

    /// <summary>Hệ số scale bù thêm riêng từng biến thể (nhân thêm vào obstacleScale/rewardScale/
    /// powerUpScale chung trong Config) — vì mesh gốc của kenney_nature-kit (Bush/Rock/RiceStalk/
    /// Flower) và kenney_cube-pets_1.0 (gia súc) có tỉ lệ khác nhau, cùng 1 hệ số chung sẽ ra kích
    /// thước lệch nhau rõ rệt. Biến thể không có trong bảng này = hệ số 1 (không đổi).</summary>
    static readonly Dictionary<LaneItemVariant, float> ScaleMultipliers = new()
    {
        { LaneItemVariant.Bush,      3f },
        { LaneItemVariant.Rock,      3f },
        { LaneItemVariant.RiceStalk, 4f },
        { LaneItemVariant.Flower,    4f },
    };

    /// <summary>Góc xoay Y (độ) bù thêm cho mesh quay sai hướng lúc dựng — mesh Kenney Cube Pets
    /// mặc định quay mông về phía camera, cần xoay 180° để quay mặt về phía người chơi. Biến thể
    /// không có trong bảng này = không xoay (0°).</summary>
    static readonly Dictionary<LaneItemVariant, float> FacingRotationY = new()
    {
        { LaneItemVariant.Cow,     180f },
        { LaneItemVariant.Pig,     180f },
        { LaneItemVariant.Chicken, 180f },
        { LaneItemVariant.Buffalo, 180f },
        { LaneItemVariant.Horse,   180f },
    };

    public static float GetScaleMultiplier(LaneItemVariant v)
        => ScaleMultipliers.TryGetValue(v, out var m) ? m : 1f;

    public static Color GetColor(LaneItemVariant v) => v switch
    {
        LaneItemVariant.Buffalo   => new Color(0.22f, 0.22f, 0.24f),
        LaneItemVariant.Cow       => new Color(0.85f, 0.72f, 0.55f),
        LaneItemVariant.Pig       => new Color(0.95f, 0.70f, 0.75f),
        LaneItemVariant.Chicken   => new Color(0.98f, 0.90f, 0.75f),
        LaneItemVariant.RiceStalk => new Color(0.90f, 0.75f, 0.25f),
        LaneItemVariant.Flower    => new Color(0.85f, 0.45f, 0.75f),
        LaneItemVariant.Horse     => new Color(0.55f, 0.38f, 0.22f),
        LaneItemVariant.Rocket    => new Color(0.75f, 0.20f, 0.20f),
        _                         => Color.white,
    };

    static PrimitiveType GetPrimitive(LaneItemVariant v) => v switch
    {
        LaneItemVariant.RiceStalk or LaneItemVariant.Flower => PrimitiveType.Cylinder,
        _ => PrimitiveType.Capsule, // Buffalo/Cow/Pig/Chicken/Horse/Rocket — placeholder chung
    };

    /// <summary>
    /// Dựng 1 GameObject visual hoàn chỉnh cho biến thể (mesh Kenney thật nếu có, primitive màu
    /// nếu chưa) + gắn sẵn component LaneRunnerItem đã Configure(kind, variant). LaneTrack chỉ cần
    /// gọi Spawn()/Recycle() trên item trả về — không cần biết visual dựng từ đâu.
    ///
    /// layer: Unity Layer gán đệ quy cho cả cây con (mesh Kenney có thể có nhiều object con) —
    /// khớp cullingMask của Camera bên tương ứng để 2 bên không nhìn thấy vật thể của nhau.
    /// </summary>
    public static LaneRunnerItem CreateVisual(LaneItemKind kind, LaneItemVariant variant, Transform parent, int layer)
    {
        GameObject go;

        if (MeshResourcePaths.TryGetValue(variant, out var path))
        {
            var prefab = Resources.Load<GameObject>(path);
            go = prefab != null ? Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        }
        else
        {
            go = GameObject.CreatePrimitive(GetPrimitive(variant));
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = GetColor(variant);
                renderer.material = mat;
            }
        }

        // Collider có thể nằm ở object CON (prefab BTM) chứ không chỉ ở gốc — không dùng physics,
        // va chạm tính bằng vị trí nên gỡ hết, đệ quy.
        foreach (var col in go.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        // Prefab BTM có sẵn SimpleGemsAnim (rotate/float/scale) nhưng float/scale set thẳng
        // transform.position/localScale TUYỆT ĐỐI mỗi frame dựa trên vị trí lúc Start() — xung đột
        // với LaneRunnerItem.MoveForward() và với pool (object tắt/bật lại nhiều lần, Start() không
        // chạy lại nên vị trí cache bị cũ, đứng hình sai chỗ). Gỡ bỏ, thay bằng LaneItemSpin bên dưới.
        var gemAnim = go.GetComponentInChildren<SimpleGemsAnim>();
        if (gemAnim != null) Object.Destroy(gemAnim);

        go.name = $"LaneItem_{variant}";
        go.transform.SetParent(parent, false);
        if (FacingRotationY.TryGetValue(variant, out var rotY))
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        SetLayerRecursive(go, layer);

        // Chỉ phần thưởng/vật phẩm đặc biệt xoay lấp lánh — vật cản (bụi cây/gia súc) đứng yên tự nhiên.
        if (kind != LaneItemKind.Obstacle)
            go.AddComponent<LaneItemSpin>();

        var item = go.AddComponent<LaneRunnerItem>();
        item.Configure(kind, variant);
        return item;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
