using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bọc 1 phần tử UI (RectTransform + Image/Text) — tương ứng class Node trong fs_core.js.</summary>
public sealed class FsNode
{
    public RectTransform rt;
    public Image img;
    public Text txt;
    public bool Dead => rt == null;
    public Vector2 Pos { get => rt.anchoredPosition; set => rt.anchoredPosition = value; }
    public float X { get => rt.anchoredPosition.x; set => rt.anchoredPosition = new Vector2(value, rt.anchoredPosition.y); }
    public float Y { get => rt.anchoredPosition.y; set => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, value); }
    public float W => rt.sizeDelta.x;
    public float H => rt.sizeDelta.y;
    public void SetSize(float w, float h) { rt.sizeDelta = new Vector2(w, h); }
    public float Scale { get => rt.localScale.x; set => rt.localScale = Vector3.one * value; }
    /// <summary>Góc quay (độ, dương = ngược chiều kim đồng hồ) — giống rot của web.</summary>
    public float Rot { get => rt.localEulerAngles.z > 180f ? rt.localEulerAngles.z - 360f : rt.localEulerAngles.z; set => rt.localRotation = Quaternion.Euler(0, 0, value); }
    public bool Active { get => rt.gameObject.activeSelf; set => rt.gameObject.SetActive(value); }
    public Graphic G => img != null ? (Graphic)img : txt;
    public Color Color { get => G != null ? G.color : Color.white; set { if (G != null) G.color = value; } }
    public float Alpha { get => G != null ? G.color.a : 1f; set { if (G != null) { var c = G.color; c.a = value; G.color = c; } } }
    public void SetAsLast() { if (rt != null) rt.SetAsLastSibling(); }
    public void Destroy() { if (rt != null) UnityEngine.Object.Destroy(rt.gameObject); }
    public static FsNode Of(RectTransform rt) => new FsNode { rt = rt, img = rt != null ? rt.GetComponent<Image>() : null, txt = rt != null ? rt.GetComponent<Text>() : null };
    public static FsNode Of(Image i) => new FsNode { rt = i.rectTransform, img = i };
    public static FsNode Of(Text t) => new FsNode { rt = t.rectTransform, txt = t };
}

/// <summary>Phần tử tương tác do view tạo ra (thẻ, vật, ô lật...) — tương ứng elem trong fs_flow.js.</summary>
public sealed class FsElem
{
    public FsNode node;
    public int key;
    public string kind = "";
    public string name = "";
    public FsNode fig;
    public Dictionary<string, object> item;
    public string itemId = "";
    // lưới thẻ (lật)
    public FsNode back, front;
    public bool faceUp, matched;
    public FsView view;

    /// <summary>Scope cho biểu thức: tapped.key / tapped.name / tapped.item / tapped.itemId.</summary>
    public Dictionary<string, object> ToScope() => new Dictionary<string, object>
    {
        { "key", (double)key }, { "name", name }, { "itemId", itemId }, { "item", (object)item ?? "" },
    };
}

/// <summary>Đối tượng cảnh (persistent hoặc theo lượt) — tương ứng obj trong fs_act.js.</summary>
public sealed class FsObj
{
    public Dictionary<string, object> spec;
    public Dictionary<string, object> extra;
    public string id, role;
    public FsNode node;
    public float x0, y0, t0;
    public bool animOn = true, moving;
    public readonly HashSet<string> cur = new HashSet<string>();
    public bool Dead => node == null || node.Dead;
}
