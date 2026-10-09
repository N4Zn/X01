using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Đồng hồ khổng lồ" — giờ đúng (kim dài luôn chỉ số 12). Mặt đồng hồ phủ gần hết nửa màn hình, 12 số là 12 ô
/// to để bé dậm vào (đứng ở số 3 thì đồng hồ chỉ 3 giờ). 2 kiểu lượt xen kẽ:
///  ĐẶT GIỜ: "Na đi học lúc 7 giờ — đặt đồng hồ 7 giờ!" → dậm số 7 → kim ngắn xoay tới 7.
///  ĐỌC GIỜ: kim ngắn đang chỉ 4 → "Mấy giờ rồi?" → dậm số 4.
/// Sai thì kim vẫn xoay tới số bé vừa dậm để bé THẤY mình đặt sai, rồi quay lại. Không cần ảnh (vẽ bằng code).
/// </summary>
public sealed class DongHoKhongLoWorld : StoryWorld
{
    static readonly (int hour, string story)[] Stories =
    {
        (6, "Na thức dậy lúc 6 giờ"), (7, "Na đi học lúc 7 giờ"), (8, "Lớp học bắt đầu lúc 8 giờ"), (9, "Na vẽ tranh lúc 9 giờ"),
        (10, "Na ăn trái cây lúc 10 giờ"), (11, "Na rửa tay lúc 11 giờ"), (12, "Cả nhà ăn cơm trưa lúc 12 giờ"), (1, "Na ngủ trưa lúc 1 giờ"),
        (2, "Na dậy chơi lúc 2 giờ"), (3, "Na ăn bánh lúc 3 giờ"), (4, "Na chơi ngoài sân lúc 4 giờ"), (5, "Na tắm lúc 5 giờ"),
    };

    readonly List<Image> _tiles = new List<Image>();
    readonly List<Text> _tileText = new List<Text>();
    RectTransform _hourHand;
    Text _prompt, _digital;
    bool _locked;
    int _taskCount, _lastHour = -1, _curHour = 12;
    float _handAngle;                   // độ, 0 = số 12, tăng theo chiều kim đồng hồ

    protected override void Build()
    {
        StoryUI.Fill(root, "Bg", StoryUI.Hex("#FFF1D6"));
        var c = P(0.5f, 0.46f);
        StoryUI.Pic(root, "Rim", ShapeSprites.Circle, StoryUI.Hex("#3A3F6B"), c, Vector2.one * U * 0.86f);
        StoryUI.Pic(root, "Face", ShapeSprites.Circle, Color.white, c, Vector2.one * U * 0.80f);

        // 12 ô số (ô to để bé dậm).
        for (int n = 1; n <= 12; n++)
        {
            int hour = n;
            float ang = (n % 12) * 30f * Mathf.Deg2Rad;
            var pos = c + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * U * 0.325f;
            var tile = StoryUI.Pic(root, "Num" + n, ShapeSprites.Circle, StoryUI.Hex(n % 3 == 0 ? "#FFD27A" : "#CFE8FF"), pos, Vector2.one * U * 0.15f, raycast: true);
            var label = StoryUI.Label(tile.transform, n.ToString(), 44, StoryUI.Hex("#2A2F55"), Vector2.zero, Vector2.one * U * 0.14f, outline: false);
            _tiles.Add(tile);
            _tileText.Add(label);
            StoryUI.OnTap(tile.gameObject, () => OnNumber(hour));
        }

        // Kim dài (cố định ở 12) + kim ngắn (xoay) — pivot ở đáy, đặt ở tâm.
        MakeHand("MinuteHand", U * 0.022f, U * 0.28f, "#2A2F55", c);
        _hourHand = MakeHand("HourHand", U * 0.034f, U * 0.20f, "#E0453F", c);
        StoryUI.Pic(root, "Cap", ShapeSprites.Circle, StoryUI.Hex("#2A2F55"), c, Vector2.one * U * 0.05f);

        _prompt = StoryUI.Label(root, "", 34, Color.white, P(0.5f, 0.925f), new Vector2(W * 0.98f, H * 0.12f));
        _digital = StoryUI.Label(root, "", 44, StoryUI.Hex("#2A2F55"), P(0.5f, 0.06f), new Vector2(W * 0.5f, H * 0.1f), outline: false);
        SetHand(12f * 30f, instant: true);
    }

    RectTransform MakeHand(string name, float w, float h, string hex, Vector2 center)
    {
        var img = StoryUI.Pic(root, name, ShapeSprites.Square, StoryUI.Hex(hex), center, new Vector2(w, h));
        img.preserveAspect = false;
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = center;
        return rt;
    }

    void SetHand(float angle, bool instant)
    {
        _handAngle = angle;
        if (instant) _hourHand.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    IEnumerator RotateHand(float toAngle, float dur)
    {
        float from = _handAngle;
        // quay theo đường ngắn nhất
        float delta = Mathf.DeltaAngle(from, toAngle);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (_hourHand == null) yield break;
            float a = from + delta * Mathf.SmoothStep(0f, 1f, t / dur);
            _hourHand.localRotation = Quaternion.Euler(0f, 0f, -a);
            yield return null;
        }
        if (_hourHand != null) _hourHand.localRotation = Quaternion.Euler(0f, 0f, -(from + delta));
        _handAngle = from + delta;
    }

    // ── Lượt chơi ─────────────────────────────────────────────────────────────

    Action<int> _onNumber;
    void OnNumber(int hour) { if (!_locked) _onNumber?.Invoke(hour); }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        _locked = false;
        ResetTileColors();
        bool setMode = (_taskCount % 2) == 0;
        int task = _taskCount++;

        // Vài lượt đầu chỉ dùng 12/3/6/9 (dễ), sau đó đủ 12 giờ.
        var pool = new List<(int hour, string story)>();
        foreach (var s in Stories)
            if (task >= 4 || s.hour % 3 == 0) pool.Add(s);
        (int hour, string story) pick;
        do { pick = pool[Rand(0, pool.Count)]; } while (pick.hour == _lastHour && pool.Count > 1);
        _lastHour = pick.hour;

        var labels = new string[12];
        for (int i = 0; i < 12; i++) labels[i] = (i + 1).ToString();
        Describe(info, $"GIO_{(setMode ? "dat" : "doc")}_{pick.hour}", "DongHoKhongLo_" + (setMode ? "DatGio" : "DocGio"),
                 setMode ? $"Đặt đồng hồ {pick.hour} giờ" : $"Đồng hồ chỉ mấy giờ? ({pick.hour} giờ)", labels, pick.hour - 1);

        if (setMode)
        {
            SetHand(12f * 30f, instant: true);
            _digital.text = "";
            _prompt.text = $"{pick.story}\nĐặt đồng hồ {pick.hour} giờ!";
            ctx.Voice("gio_dat_" + pick.hour);
        }
        else
        {
            SetHand((pick.hour % 12) * 30f, instant: true);
            _digital.text = "";
            _prompt.text = "Mấy giờ rồi?";
            ctx.Voice("gio_doc");
        }

        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        _onNumber = tapped =>
        {
            ctx.Sfx("tap");
            if (tapped == pick.hour)
            {
                _locked = true; hint = false;
                ctx.SfxRight();
                Run(Right(pick.hour, setMode, mistakes == 0, new[] { mistakes == 0 ? pick.hour - 1 : firstWrong }, done));
            }
            else
            {
                mistakes++;
                if (firstWrong < 0) firstWrong = tapped - 1;
                _locked = true;
                ctx.SfxWrong();
                Run(Wrong(tapped, pick.hour, setMode, () =>
                {
                    _locked = false;
                    if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_tiles[pick.hour - 1].rectTransform, () => hint && !_locked, 0.18f, 7f)); }
                }));
            }
        };
    }

    IEnumerator Right(int hour, bool setMode, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        if (setMode) yield return RotateHand((hour % 12) * 30f, 0.7f);
        _digital.text = $"{hour}:00";
        _tiles[hour - 1].color = StoryUI.Hex("#7BD88F");
        Run(StoryUI.Bounce(_tiles[hour - 1].rectTransform, 0.2f, 0.5f));
        Say($"{hour} giờ! Giỏi quá!", P(0.5f, 0.12f), Color.white, 1.8f);
        Confetti(P(0.5f, 0.46f), 12);
        yield return new WaitForSeconds(1.5f);
        done(firstTry, answer);
    }

    IEnumerator Wrong(int tapped, int hour, bool setMode, Action unlock)
    {
        _tiles[tapped - 1].color = StoryUI.Hex("#FF9A9A");
        float start = _handAngle;
        if (setMode)
        {
            yield return RotateHand((tapped % 12) * 30f, 0.6f);       // cho bé thấy kim chỉ đúng số mình vừa dậm
            Say($"Kim đang chỉ {tapped}, chưa phải {hour} giờ!", P(0.5f, 0.12f), StoryUI.Hex("#FFE27A"), 1.4f);
            yield return new WaitForSeconds(1.0f);
            yield return RotateHand(start, 0.5f);
        }
        else
        {
            Say($"Chưa đúng! Nhìn kim ngắn nhé", P(0.5f, 0.12f), StoryUI.Hex("#FFE27A"), 1.4f);
            Run(StoryUI.Shake(_tiles[tapped - 1].rectTransform, 0.35f, 10f));
            yield return new WaitForSeconds(0.9f);
        }
        ResetTileColors();
        unlock();
    }

    void ResetTileColors()
    {
        for (int n = 1; n <= 12; n++) _tiles[n - 1].color = StoryUI.Hex(n % 3 == 0 ? "#FFD27A" : "#CFE8FF");
    }
}
