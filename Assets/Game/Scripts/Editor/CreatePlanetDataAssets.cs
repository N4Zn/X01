#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > SolarSystem > Create Planet Data Assets
/// Tools > SolarSystem > Update Planet Texture Paths
/// </summary>
public static class CreatePlanetDataAssets
{
    const string OutputPath = "Assets/Resources/SolarSystem/Data";

    // (name, nameVi, desc, texPath, orbitRadius, orbitSpeed, selfRotSpeed, scale,
    //  distStr, diamStr, moons, tempStr, hasSaturnRings, isSun, axialTilt, orbitInclination)
    // orbitInclination: độ nghiêng mặt phẳng quỹ đạo so với hoàng đạo (thực tế)
    static Def[] Defs() => new Def[]
    {
        // Sun — không có quỹ đạo quanh chính mình
        D("Sun","Mặt Trời",
          "Ngôi sao trung tâm hệ mặt trời, cung cấp ánh sáng và nhiệt lượng cho các hành tinh.",
          "SolarSystem/Textures/2k_sun",
          r:0, os:0, sr:3, sc:7.5f,
          dist:"—", diam:"1,392,700 km", moons:0, temp:"5,500°C (bề mặt)",
          rings:false, sun:true, axial:7.25f, incl:0f),

        // Mercury — quỹ đạo nghiêng nhất: 7°
        D("Mercury","Sao Thủy",
          "Hành tinh nhỏ nhất và gần Mặt Trời nhất. Không có khí quyển bảo vệ, nhiệt độ thay đổi cực lớn.",
          "SolarSystem/Textures/2k_mercury",
          r:6, os:4.7f, sr:10, sc:1.05f,
          dist:"57.9 triệu km", diam:"4,879 km", moons:0, temp:"-180°C đến 430°C",
          rings:false, sun:false, axial:0.03f, incl:7.0f),

        // Venus — nghiêng quỹ đạo 3.4°
        D("Venus","Sao Kim",
          "Hành tinh nóng nhất hệ mặt trời dù không gần Mặt Trời nhất. Bầu khí quyển CO₂ dày gây hiệu ứng nhà kính.",
          "SolarSystem/Textures/2k_venus_atmosphere",
          r:9, os:3.5f, sr:-7, sc:1.95f,
          dist:"108.2 triệu km", diam:"12,104 km", moons:0, temp:"465°C",
          rings:false, sun:false, axial:2.64f, incl:3.4f),

        // Earth — mặt phẳng chuẩn (0°)
        D("Earth","Trái Đất",
          "Hành tinh của chúng ta — nơi duy nhất trong hệ mặt trời được biết có sự sống.",
          "SolarSystem/Textures/2k_earth_daymap",
          r:12, os:3.0f, sr:50, sc:2.1f,
          dist:"149.6 triệu km", diam:"12,742 km", moons:1, temp:"-89°C đến 58°C",
          rings:false, sun:false, axial:23.4f, incl:0f),

        // Mars — nghiêng 1.9°
        D("Mars","Sao Hỏa",
          "Hành tinh đỏ — màu từ oxit sắt trên bề mặt. Có núi lửa cao nhất hệ mặt trời: Olympus Mons.",
          "SolarSystem/Textures/2k_mars",
          r:16, os:2.4f, sr:50, sc:1.35f,
          dist:"227.9 triệu km", diam:"6,779 km", moons:2, temp:"-153°C đến 20°C",
          rings:false, sun:false, axial:25.2f, incl:1.9f),

        // Jupiter — nghiêng 1.3°
        D("Jupiter","Sao Mộc",
          "Hành tinh lớn nhất hệ mặt trời. Vết đỏ lớn là cơn bão đã kéo dài hàng trăm năm.",
          "SolarSystem/Textures/2k_jupiter",
          r:24, os:1.3f, sr:120, sc:6.0f,
          dist:"778.5 triệu km", diam:"139,820 km", moons:95, temp:"-108°C (mây)",
          rings:false, sun:false, axial:3.1f, incl:1.3f),

        // Saturn — nghiêng 2.5°
        D("Saturn","Sao Thổ",
          "Nổi tiếng với hệ thống vành đai băng và đá tuyệt đẹp. Nhẹ đến mức có thể nổi trên nước.",
          "SolarSystem/Textures/2k_saturn",
          r:32, os:1.0f, sr:110, sc:5.25f,
          dist:"1,432 triệu km", diam:"116,460 km", moons:146, temp:"-178°C",
          rings:true, sun:false, axial:26.7f, incl:2.5f),

        // Uranus — nghiêng 0.8° (trục quay mới là thứ đặc biệt: 97.8°)
        D("Uranus","Sao Thiên Vương",
          "Hành tinh duy nhất tự quay nghiêng gần 90°. Bầu khí quyển methane cho màu xanh lam đặc trưng.",
          "SolarSystem/Textures/2k_uranus",
          r:40, os:0.7f, sr:-40, sc:3.75f,
          dist:"2,871 triệu km", diam:"50,724 km", moons:28, temp:"-224°C",
          rings:false, sun:false, axial:97.8f, incl:0.8f),

        // Neptune — nghiêng 1.8°
        D("Neptune","Sao Hải Vương",
          "Hành tinh xa Mặt Trời nhất. Có gió mạnh nhất hệ mặt trời — lên đến 2,100 km/h.",
          "SolarSystem/Textures/2k_neptune",
          r:48, os:0.5f, sr:55, sc:3.45f,
          dist:"4,495 triệu km", diam:"49,244 km", moons:16, temp:"-218°C",
          rings:false, sun:false, axial:28.3f, incl:1.8f),
    };

    [MenuItem("Tools/SolarSystem/Create Planet Data Assets")]
    static void CreateAll()    => Apply(Defs(), forceOverwrite: false);

    [MenuItem("Tools/SolarSystem/Update Planet Texture Paths")]
    static void UpdateAll()    => Apply(Defs(), forceOverwrite: true);

    static void Apply(Def[] defs, bool forceOverwrite)
    {
        System.IO.Directory.CreateDirectory(OutputPath);
        int created = 0, updated = 0;

        foreach (var d in defs)
        {
            string path     = $"{OutputPath}/{d.name}.asset";
            var    existing = AssetDatabase.LoadAssetAtPath<PlanetData>(path);

            PlanetData asset;
            if (existing != null && !forceOverwrite)
            {
                Debug.Log($"[PlanetData] Skip (exists): {d.name}");
                continue;
            }
            else if (existing != null) { asset = existing; updated++; }
            else { asset = ScriptableObject.CreateInstance<PlanetData>(); created++; }

            asset.planetName       = d.name;
            asset.planetNameVi     = d.nameVi;
            asset.descriptionVi    = d.desc;
            asset.texturePath      = d.tex;
            asset.orbitRadius      = d.r;
            asset.orbitSpeed       = d.os;
            asset.selfRotateSpeed  = d.sr;
            asset.scale            = d.sc;
            asset.distanceFromSun  = d.dist;
            asset.diameter         = d.diam;
            asset.numberOfMoons    = d.moons;
            asset.surfaceTemp      = d.temp;
            asset.hasSaturnRings   = d.rings;
            asset.isSun            = d.sun;
            asset.axialTilt        = d.axial;
            asset.orbitInclination = d.incl;

            if (existing == null) AssetDatabase.CreateAsset(asset, path);
            else                  EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PlanetData] ✓ Tạo {created} mới, cập nhật {updated} tại {OutputPath}");
    }

    // ── Helper để viết defs ngắn gọn ─────────────────────────────────────────

    struct Def
    {
        public string name, nameVi, desc, tex;
        public float  r, os, sr, sc;
        public string dist, diam;
        public int    moons;
        public string temp;
        public bool   rings, sun;
        public float  axial, incl;
    }

    static Def D(string name, string nameVi, string desc, string tex,
                 float r, float os, float sr, float sc,
                 string dist, string diam, int moons, string temp,
                 bool rings, bool sun, float axial, float incl)
        => new Def { name=name, nameVi=nameVi, desc=desc, tex=tex,
                     r=r, os=os, sr=sr, sc=sc,
                     dist=dist, diam=diam, moons=moons, temp=temp,
                     rings=rings, sun=sun, axial=axial, incl=incl };
}
#endif
