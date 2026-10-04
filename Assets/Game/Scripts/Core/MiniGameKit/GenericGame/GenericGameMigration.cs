using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đọc game.json (v1 HOẶC v2) và luôn trả về GenericGamePackageV2 đã Sanitize đầy đủ — GenericGameController
/// chỉ làm việc với V2. Game v1 (layout.slots/answerArea/randomArea/questionArea chung + round.answers[] riêng) được
/// chuyển đổi giống hệt bản web (migrateV1 trong game_builder.html): mỗi round nhận 1 bản sao bố cục chung,
/// đáp án iconCount>0 → icon + text=số lượng, âm thanh câu hỏi → sound của slot câu hỏi.
/// </summary>
public static class GenericGameMigration
{
    [Serializable] class VersionProbe { public int schemaVersion = 1; }

    public static GenericGamePackageV2 Parse(string json)
    {
        int version = 1;
        try { version = JsonUtility.FromJson<VersionProbe>(json).schemaVersion; } catch { }
        GenericGamePackageV2 pkg;
        if (version >= 2)
        {
            pkg = JsonUtility.FromJson<GenericGamePackageV2>(json);
        }
        else
        {
            var v1 = JsonUtility.FromJson<GenericGamePackage>(json);
            pkg = FromV1(v1);
        }
        Sanitize(pkg);
        return pkg;
    }

    // ── v1 → v2 ──────────────────────────────────────────────────────────────

    static GenericGamePackageV2 FromV1(GenericGamePackage v1)
    {
        if (v1 == null) return null;
        var layout = v1.layout ?? new GenericGameLayout();
        var slots = layout.slots ?? Array.Empty<RectPct>();
        var ra = layout.randomArea;
        bool randomMode = ra != null && ra.enabled && ra.count > 0;
        var aa = layout.answerArea;
        if (aa.wPct <= 0f) aa = new RectPct { xPct = 5, yPct = 50, wPct = 90, hPct = 45 };
        var qa = layout.questionArea;
        if (qa.wPct <= 0f && qa.hPct <= 0f) qa = new RectPct { xPct = 8, yPct = 8, wPct = 84, hPct = 40 };
        bool questionHidden = layout.questionArea.wPct <= 0f && layout.questionArea.hPct > 0f;
        string shape = string.IsNullOrEmpty(layout.slotShape) ? "rectangle" : layout.slotShape;
        int nPos = Mathf.Max(1, randomMode ? ra.count : slots.Length);

        RectPct[] randPos = null;
        if (randomMode) randPos = PickNonOverlapping(aa, nPos, ra.itemWPct, ra.itemHPct, ra.gapPct);

        var rounds = new List<RoundSpecV2>();
        foreach (var r in v1.rounds ?? Array.Empty<RoundSpec>())
        {
            var src = r.answers ?? Array.Empty<RoundAnswerSpec>();
            int n = Mathf.Min(src.Length, nPos);
            var aslots = new SlotSpec[n];
            for (int i = 0; i < n; i++)
            {
                var a = src[i];
                RectPct rect = randomMode ? randPos[i] : (i < slots.Length ? slots[i] : new RectPct { xPct = 10, yPct = 10, wPct = 20, hPct = 25 });
                var s = new SlotSpec { xPct = rect.xPct, yPct = rect.yPct, wPct = rect.wPct, hPct = rect.hPct, correct = a.correct, imagePool = a.imagePool, fx = new SlotFx() };
                if (a.iconCount > 0) { s.icon = a.image; s.text = a.iconCount.ToString(); }
                else { s.image = a.image; s.text = a.text ?? ""; }
                aslots[i] = s;
            }
            var ag = new GroupSpec
            {
                arrangement = randomMode ? "random" : "manual",
                area = aa,
                random = new GroupRandomSpec
                {
                    itemWPct = ra != null ? ra.itemWPct : 15f,
                    itemHPct = ra != null ? ra.itemHPct : 20f,
                    gapPct = ra != null ? ra.gapPct : 2f,
                },
                matrix = new GroupMatrixSpec(),
                shape = shape,
                slots = aslots,
            };

            var qslots = new List<SlotSpec>();
            if (!questionHidden)
            {
                var q = r.question ?? new RoundQuestionSpec();
                qslots.Add(new SlotSpec
                {
                    xPct = qa.xPct, yPct = qa.yPct, wPct = qa.wPct, hPct = qa.hPct,
                    text = q.text ?? "", image = q.image, sound = q.audio, fx = new SlotFx(),
                });
            }
            else if (r.question != null && !string.IsNullOrEmpty(r.question.audio))
            {
                // câu hỏi bị ẩn (wPct<=0) nhưng vẫn có âm thanh → giữ 1 slot rỗng chỉ để phát âm thanh
                qslots.Add(new SlotSpec { xPct = 0, yPct = 0, wPct = 1, hPct = 1, sound = r.question.audio, fx = new SlotFx() });
            }
            var qg = new GroupSpec { arrangement = "manual", area = qa, matrix = new GroupMatrixSpec(), random = new GroupRandomSpec(), shape = "rectangle", slots = qslots.ToArray() };

            rounds.Add(new RoundSpecV2 { target = r.target, question = qg, answers = ag });
        }

        // game v1 từng dùng questionGenerator (bộ sinh câu hỏi tự động) — đã bỏ, rounds soạn tay có thể trống: bỏ qua im lặng
        return new GenericGamePackageV2
        {
            schemaVersion = 2, meta = v1.meta, settings = v1.settings, layout = layout,
            effects = v1.effects, rounds = rounds.ToArray(), imagePools = v1.imagePools,
        };
    }

    // ── Sanitize ─────────────────────────────────────────────────────────────

    static void Sanitize(GenericGamePackageV2 p)
    {
        if (p == null) return;
        if (p.meta == null) p.meta = new GenericGameMeta();
        if (p.settings == null) p.settings = new GenericGameSettings();
        if (p.layout == null) p.layout = new GenericGameLayout();
        var l = p.layout;
        if (l.spawnFlow == null) l.spawnFlow = new SpawnFlowSpec();
        if (l.collectSlots == null) l.collectSlots = Array.Empty<RectPct>();
        if (l.collectCols <= 0) l.collectCols = 1;
        if (l.slotFrames == null) l.slotFrames = Array.Empty<string>();
        if (l.decorations == null) l.decorations = Array.Empty<DecorationSpec>();
        foreach (var d in l.decorations)
        {
            if (d.effect == null) d.effect = new EffectSpec();
            if (d.text == null) d.text = new DecorationText();
            if (d.fx == null) d.fx = new ItemFxSet();
            SanitizeFx(ref d.fx.onCorrect);
            SanitizeFx(ref d.fx.onPartial);
            SanitizeFx(ref d.fx.onWrong);
            SanitizeFx(ref d.fx.onClick);
        }
        if (p.effects == null) p.effects = new GenericGameEffects();
        SanitizeFx(ref p.effects.onIdle);
        SanitizeFx(ref p.effects.onCorrectTap);
        SanitizeFx(ref p.effects.onWrongTap);
        SanitizeFx(ref p.effects.onCorrectRemove);
        if (p.imagePools == null) p.imagePools = Array.Empty<ImagePool>();
        if (p.rounds == null) p.rounds = Array.Empty<RoundSpecV2>();
        if (p.settings.iconColumns <= 0) p.settings.iconColumns = 2;
        // File cũ: toạ độ câu hỏi theo NỬA màn hình; chế độ Combined (câu hỏi chung) cần toạ độ theo CẢ màn hình → quy đổi ×0.5.
        bool halveQuestion = p.settings.playMode == "Combined" && l.questionSpace != "stage";
        foreach (var r in p.rounds)
        {
            r.question = SanitizeGroup(r.question, new RectPct { xPct = 8, yPct = 8, wPct = 84, hPct = 40 });
            r.answers = SanitizeGroup(r.answers, new RectPct { xPct = 5, yPct = 50, wPct = 90, hPct = 45 });
            if (halveQuestion) HalveQuestion(r.question);
            // Vùng thu thập nay là riêng từng round; file cũ chỉ có layout.collect* cấp game → chép vào mọi round.
            bool hasCollect = r.collect != null && r.collect.area.wPct > 0f;
            r.collect = hasCollect ? SanitizeGroup(r.collect, r.collect.area) : LegacyCollect(l);
        }
    }

    static void HalveQuestion(GroupSpec g)
    {
        g.area.xPct *= 0.5f; g.area.wPct *= 0.5f;
        g.matrix.itemWPct *= 0.5f; g.random.itemWPct *= 0.5f;
        foreach (var s in g.slots) { s.xPct *= 0.5f; s.wPct *= 0.5f; }
    }

    static GroupSpec LegacyCollect(GenericGameLayout l)
    {
        var area = l.collectArea.wPct > 0f ? l.collectArea : new RectPct { xPct = 5, yPct = 5, wPct = 40, hPct = 30 };
        var slots = new List<SlotSpec>();
        foreach (var r in l.collectSlots ?? Array.Empty<RectPct>())
            slots.Add(new SlotSpec { xPct = r.xPct, yPct = r.yPct, wPct = r.wPct, hPct = r.hPct, fx = new SlotFx() });
        var g = new GroupSpec
        {
            arrangement = "manual", area = area, matrix = new GroupMatrixSpec { cols = Mathf.Max(1, l.collectCols) },
            random = new GroupRandomSpec(), autoStretch = l.collectAutoStretch, fillByValue = l.collectFillByValue,
            fillReverse = l.collectFillReverse, slots = slots.ToArray(),
        };
        return SanitizeGroup(g, area);
    }

    static GroupSpec SanitizeGroup(GroupSpec g, RectPct defaultArea)
    {
        if (g == null) g = new GroupSpec();
        if (string.IsNullOrEmpty(g.arrangement)) g.arrangement = "manual";
        if (g.area.wPct <= 0f || g.area.hPct <= 0f) g.area = defaultArea;
        if (g.matrix == null) g.matrix = new GroupMatrixSpec();
        if (g.random == null) g.random = new GroupRandomSpec();
        if (string.IsNullOrEmpty(g.shape)) g.shape = "rectangle";
        if (g.slots == null) g.slots = Array.Empty<SlotSpec>();
        foreach (var s in g.slots)
        {
            if (s.fx == null) s.fx = new SlotFx();
            SanitizeFx(ref s.fx.onIdle);
            SanitizeFx(ref s.fx.onCorrectTap);
            SanitizeFx(ref s.fx.onWrongTap);
            SanitizeFx(ref s.fx.onCorrectRemove);
        }
        return g;
    }

    static void SanitizeFx(ref ActionFx fx)
    {
        if (fx == null) fx = new ActionFx();
        if (fx.effects == null) fx.effects = Array.Empty<EffectSpec>();
        for (int i = 0; i < fx.effects.Length; i++)
            if (fx.effects[i] == null) fx.effects[i] = new EffectSpec();
    }

    // ── Random không đè nhau (dùng cho migrate game v1 random + GenericGameController) ──

    /// <summary>Mỗi ô thử 200 vị trí ngẫu nhiên trong `area`, GIỮ LẠI vị trí có tổng DIỆN TÍCH chồng lên các ô đã đặt
    /// NHỎ NHẤT (dừng sớm nếu chồng=0). `gapPct` = khoảng trống % THÊM giữa 2 ô liền kề. Khớp pickNonOverlapping của web.</summary>
    public static RectPct[] PickNonOverlapping(RectPct area, int count, float itemW, float itemH, float gapPct)
    {
        var placed = new List<RectPct>(count);
        float gap = Mathf.Max(0f, gapPct);
        const int maxAttempts = 200;
        for (int n = 0; n < count; n++)
        {
            RectPct best = default;
            float bestScore = float.PositiveInfinity;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = UnityEngine.Random.Range(area.xPct, Mathf.Max(area.xPct, area.xPct + area.wPct - itemW));
                float y = UnityEngine.Random.Range(area.yPct, Mathf.Max(area.yPct, area.yPct + area.hPct - itemH));
                var cand = new RectPct { xPct = x, yPct = y, wPct = itemW, hPct = itemH };
                float score = 0f;
                foreach (var b in placed) score += OverlapAreaWithGap(cand, b, gap);
                if (score < bestScore) { bestScore = score; best = cand; }
                if (score <= 0f) break;
            }
            placed.Add(best);
        }
        return placed.ToArray();
    }

    static float OverlapAreaWithGap(RectPct a, RectPct b, float gap)
    {
        float aMinX = a.xPct - gap / 2f, aMaxX = a.xPct + a.wPct + gap / 2f;
        float aMinY = a.yPct - gap / 2f, aMaxY = a.yPct + a.hPct + gap / 2f;
        float bMinX = b.xPct, bMaxX = b.xPct + b.wPct;
        float bMinY = b.yPct, bMaxY = b.yPct + b.hPct;
        float ox = Mathf.Max(0f, Mathf.Min(aMaxX, bMaxX) - Mathf.Max(aMinX, bMinX));
        float oy = Mathf.Max(0f, Mathf.Min(aMaxY, bMaxY) - Mathf.Max(aMinY, bMinY));
        return ox * oy;
    }
}
