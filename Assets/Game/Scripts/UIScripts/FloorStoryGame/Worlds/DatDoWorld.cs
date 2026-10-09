using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Đặt đồ vào chỗ" — vị trí TRÊN / DƯỚI / BÊN TRÁI / BÊN PHẢI. Khác kiểu chọn đáp án: bé phải chạm ĐÚNG VỊ TRÍ
/// trong cảnh (4 vòng sáng quanh cái bàn), không có thẻ chữ. Con vật bay tới chỗ vừa chạm; đúng thì nhảy mừng,
/// sai thì "Không phải chỗ này!" và quay về (sau 2 lần sai vòng đúng nhấp nháy).
/// Lưu ý trái/phải: tính theo MÀN HÌNH (bé đứng quay mặt vào màn chiếu) — kèm mũi tên để dễ hiểu.
/// Ảnh: GameImages/Animal/* (sẵn có); bàn + vòng sáng vẽ bằng code.
/// </summary>
public sealed class DatDoWorld : StoryWorld
{
    enum Pos { Tren, Duoi, Trai, Phai }
    static readonly string[] PosName = { "TRÊN", "DƯỚI", "BÊN TRÁI", "BÊN PHẢI" };
    static readonly string[] PosLabel = { "Trên", "Dưới", "Bên trái", "Bên phải" };
    static readonly string[] PosVoice = { "vitri_tren", "vitri_duoi", "vitri_trai", "vitri_phai" };

    static readonly (string file, string vn)[] Items =
    {
        ("rabbit", "chú thỏ"), ("bear", "chú gấu"), ("dog", "chú chó"), ("chick", "chú gà con"), ("pig", "chú lợn"), ("duck", "chú vịt"), ("panda", "gấu trúc"),
    };

    readonly Vector2[] _zonePos = new Vector2[4];
    readonly List<Image> _zones = new List<Image>();
    readonly List<GameObject> _temp = new List<GameObject>();
    Text _prompt;
    RectTransform _arrow;
    bool _locked;
    int _last = -1;

    protected override void Build()
    {
        StoryUI.Fill(root, "Wall", StoryUI.Hex("#FFF1D6"));
        StoryUI.Pic(root, "Floor", ShapeSprites.Square, StoryUI.Hex("#E7C08A"), P(0.5f, 0.11f), new Vector2(W, H * 0.24f)).preserveAspect = false;

        // Cái bàn ở giữa.
        float topY = 0.50f;
        StoryUI.Pic(root, "Leg1", ShapeSprites.Square, StoryUI.Hex("#8A5A2B"), new Vector2(-U * 0.17f, P(0f, topY).y - U * 0.115f), new Vector2(U * 0.04f, U * 0.17f)).preserveAspect = false;
        StoryUI.Pic(root, "Leg2", ShapeSprites.Square, StoryUI.Hex("#8A5A2B"), new Vector2(U * 0.17f, P(0f, topY).y - U * 0.115f), new Vector2(U * 0.04f, U * 0.17f)).preserveAspect = false;
        var top = StoryUI.Pic(root, "TableTop", ShapeSprites.RoundedRect, StoryUI.Hex("#B9763A"), P(0.5f, topY), new Vector2(U * 0.46f, U * 0.06f));
        top.type = Image.Type.Sliced; top.preserveAspect = false;

        _zonePos[(int)Pos.Tren] = P(0.5f, topY) + new Vector2(0f, U * 0.13f);
        _zonePos[(int)Pos.Duoi] = P(0.5f, topY) + new Vector2(0f, -U * 0.14f);
        _zonePos[(int)Pos.Trai] = P(0.5f, topY) + new Vector2(-U * 0.40f, 0f);
        _zonePos[(int)Pos.Phai] = P(0.5f, topY) + new Vector2(U * 0.40f, 0f);

        _prompt = StoryUI.Label(root, "", 36, Color.white, P(0.5f, 0.915f), new Vector2(W * 0.97f, H * 0.12f));
        _arrow = StoryUI.Pic(root, "Arrow", ShapeSprites.Triangle, Color.white, P(0.5f, 0.82f), Vector2.one * U * 0.07f).rectTransform;
        _arrow.gameObject.SetActive(false);
    }

    public override void BeginTask(QuestionData info, Action<bool, int[]> done)
    {
        ClearTemp();
        _locked = false;
        int target;
        do { target = Rand(0, 4); } while (target == _last);
        _last = target;
        var item = Items[Rand(0, Items.Length)];

        var labels = (string[])PosLabel.Clone();
        Describe(info, $"VITRI_{(Pos)target}_{item.file}", "DatDoVaoCho_ViTri", $"Đặt {item.vn} {PosName[target].ToLower()} cái bàn", labels, target);
        _prompt.text = $"Đặt {item.vn} {PosName[target]} cái bàn";
        ctx.Voice(PosVoice[target]);

        // Mũi tên chỉ hướng khi là trái/phải/trên/dưới (tam giác xoay).
        _arrow.gameObject.SetActive(true);
        float[] rot = { 0f, 180f, 90f, -90f };     // tam giác mặc định chỉ lên
        _arrow.localRotation = Quaternion.Euler(0f, 0f, rot[target]);
        _arrow.anchoredPosition = P(0.5f, 0.82f);

        // 4 vòng sáng = 4 vị trí có thể chạm.
        _zones.Clear();
        int mistakes = 0, firstWrong = -1;
        bool hint = false;
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var z = Track(StoryUI.Pic(root, "Zone" + i, ShapeSprites.Circle, new Color(1f, 1f, 1f, 0.38f), _zonePos[i], Vector2.one * U * 0.20f, raycast: true));
            Track(StoryUI.Pic(z.transform, "Ring", ShapeSprites.Circle, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, Vector2.one * U * 0.15f));
            _zones.Add(z);
            Run(StoryUI.Pulse(z.rectTransform, () => z != null && !_locked, 0.06f, 3f + i * 0.4f));
            StoryUI.OnTap(z.gameObject, () =>
            {
                if (_locked) return;
                _locked = true;
                if (idx == target)
                {
                    hint = false;
                    ctx.SfxRight();
                    Run(Settle(_item, idx, item.vn, mistakes == 0, new[] { mistakes == 0 ? target : firstWrong }, done));
                }
                else
                {
                    mistakes++;
                    if (firstWrong < 0) firstWrong = idx;
                    ctx.SfxWrong();
                    Run(WrongTry(idx, () =>
                    {
                        _locked = false;
                        if (mistakes >= 2 && !hint) { hint = true; Run(StoryUI.Pulse(_zones[target].rectTransform, () => hint && !_locked, 0.2f, 7f)); }
                    }));
                }
            });
        }

        // Con vật cần đặt: đứng dưới màn hình, nhún nhẹ.
        var spr = StoryUI.Load("GameImages/Animal/" + item.file);
        var img = Track(StoryUI.Pic(root, "Item", spr ?? ShapeSprites.Circle, spr != null ? Color.white : StoryUI.Hex("#F6B26B"), P(0.5f, 0.13f), Vector2.one * U * 0.20f));
        img.transform.SetAsLastSibling();
        _item = img.rectTransform;
        Run(StoryUI.Bob(_item, U * 0.012f, 3f, 0f, () => _item != null && !_locked));
        _prompt.transform.SetAsLastSibling();
    }

    RectTransform _item;

    IEnumerator WrongTry(int zone, Action unlock)
    {
        var home = _item.anchoredPosition;
        yield return StoryUI.MoveTo(_item, _zonePos[zone], 0.45f);
        Say("Không phải chỗ này!", P(0.5f, 0.74f), StoryUI.Hex("#FFE27A"), 1.2f);
        yield return StoryUI.Shake(_item, 0.35f, 10f);
        yield return StoryUI.MoveTo(_item, home, 0.4f);
        unlock();
    }

    IEnumerator Settle(RectTransform item, int zone, string vn, bool firstTry, int[] answer, Action<bool, int[]> done)
    {
        yield return StoryUI.MoveTo(item, _zonePos[zone], 0.55f);
        ctx.Sfx("plop");
        Run(StoryUI.Bounce(item, 0.2f, 0.5f));
        Say($"Đúng rồi! {Cap(vn)} ở {PosName[zone].ToLower()} cái bàn", P(0.5f, 0.74f), Color.white, 2f);
        Confetti(_zonePos[zone], 10);
        yield return new WaitForSeconds(1.3f);
        done(firstTry, answer);
    }

    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    Image Track(Image img) { _temp.Add(img.gameObject); return img; }

    void ClearTemp()
    {
        foreach (var g in _temp) if (g != null) UnityEngine.Object.Destroy(g);
        _temp.Clear();
        _item = null;
        if (_arrow != null) _arrow.gameObject.SetActive(false);
    }
}
