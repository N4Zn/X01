using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Import gói .zip xuất từ game_builder_v2.html (game.json + assets/) thành 1 GAME RIÊNG có scene riêng:
///   Assets/Game/GenericGames/&lt;id&gt;/game.json            — TextAsset, nhúng vào prefab
///   Assets/Game/GenericGames/&lt;id&gt;/&lt;id&gt;_LayoutBase.prefab — SINH TỰ ĐỘNG từ game.json (marker từng ô/item/vùng của mọi round)
///   Assets/Game/GenericGames/&lt;id&gt;/&lt;id&gt;_Layout.prefab     — PREFAB VARIANT của Base: chỉnh tay (kéo/resize marker) lưu ở đây
///   Assets/Game/Scenes/_GenericGame/Games/&lt;id&gt;.unity      — scene riêng (nhân bản từ scene mẫu GenericGamePlayer) chứa variant
///   Assets/Game/Resources/TestTongHop/{images,audio}/GenericGames/&lt;id&gt;/ — ảnh/âm thanh (GenericGameController tải theo tên file)
/// Re-import (cùng id): ghi lại game.json + Base + asset, GIỮ NGUYÊN Variant và scene → phần chỉnh tay còn nguyên
/// (best-effort: Unity chỉ giữ override cho object còn tồn tại trong Base mới).
/// Lúc chạy GenericGameController đọc game.json trong prefab rồi ghi đè toạ độ bằng vị trí các marker (GenericGameBaked.ApplyTo).
/// </summary>
public static class GenericGameZipImporter
{
    const string GamesRoot = "Assets/Game/GenericGames";
    const string SceneFolder = "Assets/Game/Scenes/_GenericGame";
    const string TemplateScene = SceneFolder + "/GenericGamePlayer.unity";
    const string ImagesRoot = "Assets/Game/Resources/TestTongHop/images/GenericGames";
    const string AudioRoot = "Assets/Game/Resources/TestTongHop/audio/GenericGames";

    static readonly Color QColor = new Color(0.25f, 0.55f, 1f, 0.35f);
    static readonly Color AColor = new Color(0.2f, 0.8f, 0.35f, 0.35f);
    static readonly Color CColor = new Color(1f, 0.6f, 0.15f, 0.35f);
    static readonly Color DColor = new Color(0.8f, 0.3f, 0.9f, 0.35f);

    [MenuItem("Tools/GenericGame/Import Zip (1 file)...")]
    public static void ImportOne()
    {
        string zip = EditorUtility.OpenFilePanel("Chọn gói game .zip", "", "zip");
        if (string.IsNullOrEmpty(zip)) return;
        ImportAll(new[] { zip });
    }

    [MenuItem("Tools/GenericGame/Import Zips (cả thư mục)...")]
    public static void ImportFolder()
    {
        string dir = EditorUtility.OpenFolderPanel("Chọn thư mục chứa các gói .zip", "", "");
        if (string.IsNullOrEmpty(dir)) return;
        ImportAll(Directory.GetFiles(dir, "*.zip"));
    }

    static void ImportAll(string[] zips)
    {
        if (zips.Length == 0) { Debug.LogWarning("[GenericGameZipImporter] Không có file .zip nào."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath(TemplateScene)))
            GenericGamePlayerSceneBuilder.BuildScene(); // scene mẫu chưa có → dựng 1 lần

        var done = new List<string>();
        foreach (var zip in zips)
        {
            try { var id = ImportZip(zip); if (id != null) done.Add(id); }
            catch (Exception e) { Debug.LogError($"[GenericGameZipImporter] Lỗi import '{zip}': {e}"); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GenericGameZipImporter] Xong " + done.Count + "/" + zips.Length + " game.");
    }

    // ── 1 zip ────────────────────────────────────────────────────────────────

    static string ImportZip(string zipPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(zipPath);
        string gameId = ToGameId(fileName);
        string displayName = Regex.Replace(fileName, @"\s+", " ").Trim();
        string gameDir = $"{GamesRoot}/{gameId}";
        string imgDir = $"{ImagesRoot}/{gameId}";
        string audDir = $"{AudioRoot}/{gameId}";
        foreach (var d in new[] { gameDir, imgDir, audDir, SceneFolder + "/Games" }) MiniGameSceneBuilderHelpers.EnsureFolder(d);

        bool exists = File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath($"{gameDir}/game.json"))
            || File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath($"{SceneFolder}/Games/{gameId}.unity"));
        if (exists && !EditorUtility.DisplayDialog("Cập nhật game đã có",
                $"Game '{gameId}' đã được import trước đó.\n\n" +
                "Cập nhật sẽ GHI ĐÈ: game.json, ảnh, âm thanh, LayoutBase.prefab.\n" +
                "GIỮ NGUYÊN: scene và Variant (Layout.prefab) — chỉnh tay của bạn vẫn còn, " +
                "marker lệch so với json mới sẽ báo XUNG ĐỘT khi chạy.",
                "Cập nhật", "Bỏ qua"))
        {
            Debug.Log($"[GenericGameZipImporter] Bỏ qua '{gameId}' (đã tồn tại, người dùng không cập nhật).");
            return null;
        }

        string json = null;
        using (var za = ZipFile.OpenRead(zipPath))
        {
            foreach (var e in za.Entries)
            {
                string name = e.FullName.Replace('\\', '/');
                if (string.IsNullOrEmpty(e.Name)) continue;
                if (name.Equals("game.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(e.Open(), Encoding.UTF8);
                    json = sr.ReadToEnd();
                    continue;
                }
                if (!name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(e.Name).ToLowerInvariant();
                string destDir = ext == ".png" || ext == ".jpg" || ext == ".jpeg" ? imgDir
                    : ext == ".mp3" || ext == ".ogg" || ext == ".wav" ? audDir : null;
                if (destDir == null) { Debug.LogWarning($"[GenericGameZipImporter] Bỏ qua file lạ: {name}"); continue; }
                e.ExtractToFile(MiniGameSceneBuilderHelpers.ToAbsolutePath($"{destDir}/{e.Name}"), true);
            }
        }
        if (json == null) throw new Exception("Không thấy game.json ở gốc zip.");
        json = SetFirst(json, "gameId", gameId);
        json = SetFirst(json, "displayName", displayName);
        AssetDatabase.Refresh(); // import ảnh (GenericGameTextureImportProcessor ép Sprite) TRƯỚC khi load làm preview

        string jsonPath = $"{gameDir}/game.json";
        File.WriteAllText(MiniGameSceneBuilderHelpers.ToAbsolutePath(jsonPath), json, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(jsonPath);
        var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);

        var pkg = GenericGameMigration.Parse(json); // toạ độ ĐÃ chuẩn hoá (v1→v2, Combined stage...) — cùng thứ controller thấy lúc chạy

        string basePath = $"{gameDir}/{gameId}_LayoutBase.prefab";
        string variantPath = $"{gameDir}/{gameId}_Layout.prefab";
        BuildBasePrefab(pkg, gameId, jsonAsset, imgDir, basePath);

        bool variantExists = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null;
        if (!variantExists)
        {
            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
            PrefabUtility.SaveAsPrefabAsset(inst, variantPath); // lưu instance của prefab = tạo Prefab Variant
            UnityEngine.Object.DestroyImmediate(inst);
        }

        string scenePath = $"{SceneFolder}/Games/{gameId}.unity";
        if (!File.Exists(MiniGameSceneBuilderHelpers.ToAbsolutePath(scenePath)))
            BuildScene(scenePath, variantPath);

        // Ảnh trùng nội dung với ảnh đã có (cùng game hoặc _shared) → gom về Resources/.../_shared, game.json/prefab tự đổi tham chiếu.
        GenericGameAssetDedupe.Run(apply: true, onlyGameId: gameId, interactive: false);

        MiniGameSceneBuilderHelpers.AddSceneToBuildSettings(scenePath);
        var reg =GenericGameRegistryWriter.RegisterWithPrompt(gameId, displayName);
        string regNote = reg.line != null
            ? (reg.registered ? $"\n  GameRegistry.cs: đã thêm `{reg.line}`" : $"\n  GameRegistry.cs: CHƯA thêm — dán tay `{reg.line}`")
            : reg.registered ? "\n  GameRegistry: đã có sẵn." : "\n  GameRegistry: bỏ qua (chưa đăng ký — import lại để đăng ký).";
        if (reg.jsonChanged) regNote += "\n  game_registry.json đã sửa → cần rebuild controlui-release.aar (xem CLAUDE.md) rồi build lại APK K02.";
        Debug.Log($"[GenericGameZipImporter] '{displayName}' → id={gameId}, scene={scenePath}{regNote}");
        return gameId;
    }

    /// <summary>Đổi giá trị chuỗi của key đầu tiên tìm thấy (nằm trong "meta") — không parse/ghi lại cả file để không mất field lạ.</summary>
    static string SetFirst(string json, string key, string value)
    {
        string safe = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return new Regex("(\"" + key + "\"\\s*:\\s*\")[^\"]*(\")").Replace(json, "${1}" + safe.Replace("$", "$$") + "${2}", 1);
    }

    /// <summary>"cửa hàng kem ( trước , sau)" → "CuaHangKemTruocSau".</summary>
    static string ToGameId(string name)
    {
        var sb = new StringBuilder();
        foreach (char c0 in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c0) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c0 == 'đ' ? 'd' : c0 == 'Đ' ? 'D' : c0);
        }
        var sbId = new StringBuilder();
        foreach (var w in Regex.Split(sb.ToString(), @"[^A-Za-z0-9]+"))
            if (w.Length > 0) sbId.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1));
        string id = sbId.ToString();
        if (id.Length == 0 || char.IsDigit(id[0])) id = "Game" + id;
        return id;
    }

    // ── Prefab layout ──────────────────────────────────────────────────────────

    static void BuildBasePrefab(GenericGamePackageV2 pkg, string gameId, TextAsset jsonAsset, string imgDir, string basePath)
    {
        var root = NewRect("GameLayout", null);
        Stretch((RectTransform)root.transform, 0f, 1f);
        var baked = root.AddComponent<GenericGameBaked>();
        baked.gameJson = jsonAsset;
        baked.gameId = gameId;
        baked.previewRound = 0;

        // Left = NỬA TRÁI (bên phải tự mirror lúc chạy), Stage = cả màn hình (câu hỏi chung ở chế độ Combined).
        var left = NewRect("Left_HalfCanvas", root.transform); Stretch((RectTransform)left.transform, 0f, 0.5f);
        var stage = NewRect("Stage_FullCanvas", root.transform); Stretch((RectTransform)stage.transform, 0f, 1f);
        bool combined = pkg.settings.playMode != "Independent";

        // Nền (chỉ xem, không phải marker)
        if (!string.IsNullOrEmpty(pkg.layout.background))
        {
            var bg = NewRect("Background (xem)", left.transform);
            Stretch((RectTransform)bg.transform, 0f, 1f);
            var img = bg.AddComponent<Image>();
            img.sprite = LoadSprite(imgDir, pkg.layout.background);
            img.raycastTarget = false;
        }

        // Item trang trí — chung mọi round
        var decoRoot = NewRect("Decorations", left.transform); Stretch((RectTransform)decoRoot.transform, 0f, 1f);
        foreach (var d in pkg.layout.decorations)
        {
            var go = Marker($"Deco_{d.id}", decoRoot.transform, BakedKind.Deco, -1, -1, DColor,
                new RectPct { xPct = d.xPct, yPct = d.yPct, wPct = d.wPct, hPct = d.hPct }, LoadSprite(imgDir, d.image), d.id);
            go.GetComponent<GenericBakedRect>().decoId = d.id;
        }

        for (int r = 0; r < pkg.rounds.Length; r++)
        {
            var rd = pkg.rounds[r];
            var rl = NewRect($"Round_{r}", left.transform); Stretch((RectTransform)rl.transform, 0f, 1f);
            var rs = NewRect($"Round_{r}", stage.transform); Stretch((RectTransform)rs.transform, 0f, 1f);

            // Câu hỏi: Combined → toạ độ theo cả màn hình (Stage); Independent → theo nửa (Left)
            var qParent = combined ? rs.transform : rl.transform;
            BuildGroup(rd.question, r, qParent, "Q", BakedKind.Question, BakedKind.QuestionArea, QColor, imgDir, areaToo: rd.question.arrangement == "random");
            BuildGroup(rd.answers, r, rl.transform, "A", BakedKind.Answer, BakedKind.AnswerArea, AColor, imgDir, areaToo: rd.answers.arrangement == "random" || pkg.layout.spawnFlow.enabled);
            if (rd.collect != null)
                BuildGroup(rd.collect, r, rl.transform, "C", BakedKind.Collect, BakedKind.CollectArea, CColor, imgDir, areaToo: rd.collect.autoStretch);
        }

        PrefabUtility.SaveAsPrefabAsset(root, basePath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void BuildGroup(GroupSpec g, int round, Transform parent, string tag, BakedKind slotKind, BakedKind areaKind, Color color, string imgDir, bool areaToo)
    {
        if (g == null) return;
        if (areaToo)
            Marker($"{tag}_Area", parent, areaKind, round, -1, color, g.area, null, "vùng");
        if (g.slots == null) return;
        // random/spawn: toạ độ từng slot bị bỏ qua lúc chạy → chỉ bake vùng (Area), không bake ô
        if (g.arrangement == "random" && tag != "C") return;
        for (int i = 0; i < g.slots.Length; i++)
        {
            var s = g.slots[i];
            string label = !string.IsNullOrEmpty(s.text) ? s.text : tag + i;
            string img = !string.IsNullOrEmpty(s.image) ? s.image : !string.IsNullOrEmpty(s.icon) ? s.icon : g.bgImage;
            string mark = s.correct ? " ✔" : "";
            Marker($"{tag}{i}_{Sanitize(label)}{mark}", parent, slotKind, round, i, color,
                new RectPct { xPct = s.xPct, yPct = s.yPct, wPct = s.wPct, hPct = s.hPct }, LoadSprite(imgDir, img), label + mark);
        }
    }

    static string Sanitize(string s) => Regex.Replace(s ?? "", @"[^\w\-\+=\? ]", "").Trim();

    static GameObject Marker(string name, Transform parent, BakedKind kind, int round, int index, Color color, RectPct p, Sprite sprite, string label)
    {
        var go = NewRect(name, parent);
        var m = go.AddComponent<GenericBakedRect>();
        m.kind = kind; m.round = round; m.index = index;
        m.WritePct(p);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        if (sprite != null) { img.sprite = sprite; img.color = new Color(1f, 1f, 1f, 0.85f); }
        else img.color = color;
        if (!string.IsNullOrEmpty(label))
        {
            var t = NewRect("Label", go.transform);
            Stretch((RectTransform)t.transform, 0f, 1f);
            var txt = t.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            txt.fontStyle = FontStyle.Bold;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 8;
            txt.resizeTextMaxSize = 40;
            txt.raycastTarget = false;
        }
        return go;
    }

    static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt, float xMin, float xMax)
    {
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite LoadSprite(string imgDir, string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        // "_shared/x.png" (ảnh dùng chung, xem GenericGameAssetDedupe) → thư mục anh em của imgDir.
        string norm = file.Replace('\\', '/');
        string dir = norm.Contains("/") ? $"{ImagesRoot}/{norm.Substring(0, norm.LastIndexOf('/'))}" : imgDir;
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{Path.GetFileName(file)}");
    }

    // ── Scene riêng ─────────────────────────────────────────────────────────────

    static void BuildScene(string scenePath, string variantPath)
    {
        if (!AssetDatabase.CopyAsset(TemplateScene, scenePath))
            throw new Exception($"Không nhân bản được scene mẫu {TemplateScene}");
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var controller = UnityEngine.Object.FindFirstObjectByType<GenericGameController>();
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (controller == null || canvas == null) throw new Exception("Scene mẫu thiếu GenericGameController/Canvas — chạy lại Tools/GenericGame/Build Player Scene.");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        inst.transform.SetAsLastSibling();

        var so = new SerializedObject(controller);
        so.FindProperty("baked").objectReferenceValue = inst.GetComponent<GenericGameBaked>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
