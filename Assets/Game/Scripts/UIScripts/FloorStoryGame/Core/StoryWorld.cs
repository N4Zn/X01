using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Thế giới" của MỘT đội trong game FloorStory (nửa trái hoặc nửa phải màn hình). Mỗi game = 1 lớp con.
/// Thế giới GIỮ trạng thái qua các lượt (bầu trời đổi buổi, ngôi nhà được xây dần, bé lớn lên...) — khác kiểu
/// "câu hỏi tĩnh + thẻ đáp án".
/// </summary>
public abstract class StoryWorld
{
    protected StoryContext ctx;
    protected RectTransform root;
    protected Team team;
    protected float W = 512f, H = 500f;
    protected System.Random rng = new System.Random();

    /// <summary>Cạnh nhỏ của world — dùng làm đơn vị kích thước.</summary>
    protected float U => Mathf.Min(W, H);

    public void Init(StoryContext c, RectTransform r, Team t)
    {
        ctx = c; root = r; team = t;
        Canvas.ForceUpdateCanvases();
        if (r.rect.width > 10f && r.rect.height > 10f) { W = r.rect.width; H = r.rect.height; }
        rng = new System.Random(Environment.TickCount + (t == Team.Left ? 17 : 91));
        Build();
    }

    /// <summary>Dựng phần cố định của thế giới (1 lần).</summary>
    protected abstract void Build();

    /// <summary>Bắt đầu 1 lượt. Điền `info` (id, topic, questionMediaValue = mô tả câu, answers, correctAnswers) để log,
    /// rồi gọi `done(đúngNgayLầnĐầu, đáp án đã chọn)` ĐÚNG 1 LẦN khi lượt kết thúc.</summary>
    public abstract void BeginTask(QuestionData info, Action<bool, int[]> done);

    protected Vector2 P(float fx, float fy) => new Vector2((fx - 0.5f) * W, (fy - 0.5f) * H);

    protected int Rand(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);

    protected void Shuffle<T>(T[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    protected Coroutine Run(IEnumerator e) => ctx.Run(e);

    /// <summary>Điền các field chung cho log.</summary>
    protected static void Describe(QuestionData info, string id, string topic, string description, string[] answers, int correct)
    {
        info.id = id;
        info.topic = topic;
        info.difficulty = 1;
        info.questionType = QuestionType.Choose;
        info.questionMediaType = QuestionMediaType.Text;
        info.questionMediaValue = description;
        info.answerMediaType = AnswerMediaType.Text;
        info.answers = answers;
        info.answerMode = AnswerMode.Single;
        info.correctAnswers = new[] { correct };
    }

    static readonly string[] ConfettiColors = { "#FFD93D", "#FF6B6B", "#6BCB77", "#4D96FF", "#FF9F45", "#C77DFF" };

    /// <summary>Pháo hoa giấy: sao/tim/tròn toả ra rồi mờ dần — ăn mừng khi xong 1 phần lớn.</summary>
    protected void Confetti(Vector2 center, int count = 16, bool sound = true)
    {
        string[] shapes = { "star", "heart", "circle" };
        for (int i = 0; i < count; i++)
        {
            var img = StoryUI.Pic(root, "Confetti", ShapeSprites.ByName(shapes[i % 3]), StoryUI.Hex(ConfettiColors[i % ConfettiColors.Length]),
                                  center, Vector2.one * U * 0.055f);
            img.transform.SetAsLastSibling();
            float ang = (i / (float)count) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
            float dist = U * (0.22f + (float)rng.NextDouble() * 0.28f);
            Run(StoryUI.MoveTo(img.rectTransform, center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist, 0.8f));
            Run(FadeAndDestroy(img, 0.5f, 0.5f));
        }
        if (sound) ctx.Sfx("powerup");
    }

    protected IEnumerator FadeAndDestroy(Graphic g, float delay, float dur)
    {
        yield return new WaitForSeconds(delay);
        if (g != null) yield return StoryUI.Fade(g, 0f, dur);
        if (g != null) UnityEngine.Object.Destroy(g.gameObject);
    }

    /// <summary>Hộp thoại ngắn nổi lên rồi tự tan — phản hồi vui khi chọn sai / khen khi đúng.</summary>
    protected void Say(string text, Vector2 pos, Color color, float seconds = 1.6f)
    {
        var t = StoryUI.Label(root, text, 34, color, pos, new Vector2(W * 0.92f, 90f));
        t.transform.SetAsLastSibling();
        Run(SayRoutine(t, seconds));
    }

    IEnumerator SayRoutine(Text t, float seconds)
    {
        yield return StoryUI.PopIn(t.rectTransform, 0.25f);
        yield return new WaitForSeconds(seconds);
        if (t != null) yield return StoryUI.Fade(t, 0f, 0.3f);
        if (t != null) UnityEngine.Object.Destroy(t.gameObject);
    }
}
