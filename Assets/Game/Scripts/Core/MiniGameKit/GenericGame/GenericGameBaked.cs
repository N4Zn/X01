using UnityEngine;

/// <summary>Gốc của prefab layout 1 game: giữ game.json + gameId và áp toạ độ các marker (GenericBakedRect) vào package lúc chạy.</summary>
[ExecuteAlways]
public class GenericGameBaked : MonoBehaviour
{
    public TextAsset gameJson;
    public string gameId;
    [Tooltip("Round đang hiện trong Scene view (các round khác bị ẩn để khỏi chồng lên nhau). Chỉ để xem/chỉnh, không ảnh hưởng lúc chơi.")]
    public int previewRound;

    public void ApplyTo(GenericGamePackageV2 pkg)
    {
        if (pkg == null) return;
        var conflicts = new System.Collections.Generic.List<string>();
        foreach (var m in GetComponentsInChildren<GenericBakedRect>(true))
        {
            var p = m.ReadPct();
            var old = Current(pkg, m);
            if (old.HasValue && Differs(old.Value, p)) conflicts.Add($"{m.name} ({m.kind} r{m.round}#{m.index}): json=({old.Value.xPct:0.#},{old.Value.yPct:0.#},{old.Value.wPct:0.#},{old.Value.hPct:0.#}) scene=({p.xPct:0.#},{p.yPct:0.#},{p.wPct:0.#},{p.hPct:0.#})");
            switch (m.kind)
            {
                case BakedKind.Deco:
                    if (pkg.layout.decorations == null) break;
                    foreach (var d in pkg.layout.decorations)
                        if (d.id == m.decoId) { d.xPct = p.xPct; d.yPct = p.yPct; d.wPct = p.wPct; d.hPct = p.hPct; }
                    break;
                case BakedKind.Question: SetSlot(Group(pkg, m.round, g => g.question), m.index, p); break;
                case BakedKind.Answer: SetSlot(Group(pkg, m.round, g => g.answers), m.index, p); break;
                case BakedKind.Collect: SetSlot(Group(pkg, m.round, g => g.collect), m.index, p); break;
                case BakedKind.QuestionArea: { var g = Group(pkg, m.round, r => r.question); if (g != null) g.area = p; break; }
                case BakedKind.AnswerArea: { var g = Group(pkg, m.round, r => r.answers); if (g != null) g.area = p; break; }
                case BakedKind.CollectArea: { var g = Group(pkg, m.round, r => r.collect); if (g != null) g.area = p; break; }
            }
        }
        if (conflicts.Count > 0)
            Debug.LogWarning($"[GenericGameBaked] XUNG ĐỘT: {conflicts.Count} marker trong scene lệch so với game.json (đang dùng vị trí SCENE). Cần thống nhất (sửa json trên web, hoặc re-import để lấy lại json):\n" + string.Join("\n", conflicts));
    }

    static bool Differs(RectPct a, RectPct b) => Mathf.Abs(a.xPct - b.xPct) > 0.1f || Mathf.Abs(a.yPct - b.yPct) > 0.1f || Mathf.Abs(a.wPct - b.wPct) > 0.1f || Mathf.Abs(a.hPct - b.hPct) > 0.1f;

    static RectPct? Current(GenericGamePackageV2 pkg, GenericBakedRect m)
    {
        GroupSpec g = null;
        switch (m.kind)
        {
            case BakedKind.Deco:
                if (pkg.layout.decorations != null) foreach (var d in pkg.layout.decorations) if (d.id == m.decoId) return new RectPct { xPct = d.xPct, yPct = d.yPct, wPct = d.wPct, hPct = d.hPct };
                return null;
            case BakedKind.Question: g = Group(pkg, m.round, r => r.question); break;
            case BakedKind.Answer: g = Group(pkg, m.round, r => r.answers); break;
            case BakedKind.Collect: g = Group(pkg, m.round, r => r.collect); break;
            case BakedKind.QuestionArea: return Group(pkg, m.round, r => r.question)?.area;
            case BakedKind.AnswerArea: return Group(pkg, m.round, r => r.answers)?.area;
            case BakedKind.CollectArea: return Group(pkg, m.round, r => r.collect)?.area;
        }
        if (g == null || g.slots == null || m.index < 0 || m.index >= g.slots.Length) return null;
        var sl = g.slots[m.index];
        return new RectPct { xPct = sl.xPct, yPct = sl.yPct, wPct = sl.wPct, hPct = sl.hPct };
    }

    static GroupSpec Group(GenericGamePackageV2 pkg, int round, System.Func<RoundSpecV2, GroupSpec> pick)
        => pkg.rounds != null && round >= 0 && round < pkg.rounds.Length ? pick(pkg.rounds[round]) : null;

    static void SetSlot(GroupSpec g, int index, RectPct p)
    {
        if (g == null || g.slots == null || index < 0 || index >= g.slots.Length) return;
        var s = g.slots[index];
        s.xPct = p.xPct; s.yPct = p.yPct; s.wPct = p.wPct; s.hPct = p.hPct;
    }

    public void HideAtRuntime() => gameObject.SetActive(false);

#if UNITY_EDITOR
    void OnValidate() { UnityEditor.EditorApplication.delayCall += ApplyPreviewRound; }

    void ApplyPreviewRound()
    {
        if (this == null || Application.isPlaying) return;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Round_")) continue;
            if (!int.TryParse(t.name.Substring(6), out int r)) continue;
            if (t.gameObject.activeSelf != (r == previewRound)) t.gameObject.SetActive(r == previewRound);
        }
    }
#endif
}
