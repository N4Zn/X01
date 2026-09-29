using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Thông tin 1 người chơi để hiện ở khối MVP / Tia Chớp.</summary>
[Serializable]
public class PlayerHighlight
{
    public string name;
    public int hairIndex = -1;   // chọn sprite avatar (giống PlayerInfo.HairIndex), -1 = dùng thân mặc định
    public float bestTime;       // giây — lần trả lời đúng nhanh nhất (0 = không có)
    public int score;            // số câu trả lời ĐÚNG của riêng người này trong ván (1 câu đúng = 1 điểm)
}

/// <summary>1 ô Tia Chớp: khung avatar tròn + nhãn tên (A, C, F, G trong ảnh mẫu).</summary>
[Serializable]
public class HighlightSlot
{
    public GameObject root;
    public Image avatar;
    public Text label;
}

/// <summary>
/// View for ScoreScene — supports two display modes: 1v1 and Team.
/// Both modes share buttons (Choi lai, Doi doi). Mode panels are toggled by Controller.
/// </summary>
public class ScoreSceneView : MonoBehaviour
{
    [Header("=== Mode Panels ===")]
    [SerializeField] private GameObject oneVsOnePanel;
    [SerializeField] private GameObject teamPanel;

    [Header("=== 1v1 Mode ===")]
    [SerializeField] private Text ovoPlayer1NameText;
    [SerializeField] private Text ovoPlayer1ScoreText;
    [SerializeField] private Text ovoPlayer2NameText;
    [SerializeField] private Text ovoPlayer2ScoreText;
    [SerializeField] private Text ovoWinnerText;
    [SerializeField] private GameObject ovoConfetti;
    [SerializeField] private Image ovoCupIcon;
    [SerializeField] private RectTransform ovoP1Polygon;
    [SerializeField] private RectTransform ovoP2Polygon;
    [SerializeField] private GameObject ovoWinnerBadge;
    [SerializeField] private Text ovoWinnerBadgeText;

    [Header("=== Team Mode ===")]
    [SerializeField] private Text teamBlueNameText;
    [SerializeField] private Text teamRedNameText;
    [SerializeField] private Text teamBlueScoreText;
    [SerializeField] private Text teamRedScoreText;
    [SerializeField] private Transform teamBlueAvatarContainer;
    [SerializeField] private Transform teamRedAvatarContainer;
    [SerializeField] private GameObject teamAvatarSlotTemplate;
    [SerializeField] private Image teamCupIcon;
    [SerializeField] private GameObject teamConfetti;
    [SerializeField] private RectTransform teamBlueStar;
    [SerializeField] private RectTransform teamRedStar;
    [SerializeField] private Sprite avatarBgBlue;
    [SerializeField] private Sprite avatarBgRed;
    [SerializeField] private Sprite[] charHairSprites;
    [SerializeField] private Sprite charBodySprite;

    [Header("=== Team Mode: MVP (tên người điểm cao nhất trong khung tròn) ===")]
    [SerializeField] private GameObject mvpBadge;
    [SerializeField] private Text mvpNameText;
    [SerializeField] private Image mvpAvatarImage;   // tuỳ chọn — để trống nếu chỉ muốn hiện tên

    [Header("=== Team Mode: Tia Chớp (nhanh nhất + đúng, mỗi đội 2 bạn) ===")]
    [SerializeField] private GameObject tiaChopBadge;
    [SerializeField] private HighlightSlot[] blueFastSlots;   // bên trái (A, C)
    [SerializeField] private HighlightSlot[] redFastSlots;    // bên phải (F, G)

    [Header("=== Team Mode: Vòng tròn thành viên trong khung Đội 1 / Đội 2 (chữ cái đầu tên) ===")]
    [SerializeField] private Transform blueRosterGrid;    // MemberGrid (GridLayoutGroup) trong khung trái
    [SerializeField] private Transform redRosterGrid;     // MemberGrid (GridLayoutGroup) trong khung phải
    [SerializeField] private int maxMembersPerTeam = 5;   // tối đa 5 bạn mỗi đội — chỗ thiếu người vẽ vòng nét đứt
    [SerializeField] private int circlesPerRow = 3;       // 5 bạn → hàng trên 3 vòng, hàng dưới 2 vòng (căn giữa)
    [SerializeField] private float circleSize = 68f;      // đường kính vòng tròn (đơn vị canvas)
    [SerializeField] private float circleSpacing = 12f;   // khoảng cách giữa các vòng
    [SerializeField] private Font rosterFont;             // để trống = dùng font của tên đội

    private Sprite _letterCircleSprite;
    private Sprite _emptyRingSprite;

    [Header("=== Shared Buttons ===")]
    [SerializeField] private Button replayButton;
    [SerializeField] private Button changeTeamButton;

    public event Action onClickReplay;
    public event Action onClickChangeTeam;

    private List<GameObject> _blueAvatarSlots = new List<GameObject>();
    private List<GameObject> _redAvatarSlots = new List<GameObject>();

    public void InitView()
    {
        if (replayButton != null)
            replayButton.onClick.AddListener(() => onClickReplay?.Invoke());
        if (changeTeamButton != null)
            changeTeamButton.onClick.AddListener(() => onClickChangeTeam?.Invoke());
    }

    // ===== Mode Switching =====

    public void ShowOneVsOneMode()
    {
        if (oneVsOnePanel != null) oneVsOnePanel.SetActive(true);
        if (teamPanel != null) teamPanel.SetActive(false);
    }

    public void ShowTeamMode()
    {
        if (oneVsOnePanel != null) oneVsOnePanel.SetActive(false);
        if (teamPanel != null) teamPanel.SetActive(true);
    }

    // ===== 1v1 Display =====

    public void DisplayOneVsOneResults(string p1Name, int p1Score, string p2Name, int p2Score)
    {
        if (ovoPlayer1NameText != null) ovoPlayer1NameText.text = p1Name;
        if (ovoPlayer2NameText != null) ovoPlayer2NameText.text = p2Name;

        bool isTie = p1Score == p2Score;
        bool p1Wins = p1Score > p2Score;

        // Winner text — only used for tie case
        if (ovoWinnerText != null)
            ovoWinnerText.text = isTie ? "Hoà!" : "";
        if (ovoWinnerText != null)
            ovoWinnerText.gameObject.SetActive(isTie);

        // Confetti only when there is a winner
        if (ovoConfetti != null) ovoConfetti.SetActive(!isTie);

        // Cup position — over the winner's polygon, hidden on tie
        if (ovoCupIcon != null)
        {
            ovoCupIcon.gameObject.SetActive(!isTie);
            if (!isTie)
            {
                RectTransform cupRT = ovoCupIcon.rectTransform;
                if (p1Wins)
                {
                    cupRT.anchorMin = new Vector2(0.02f, 0.66f);
                    cupRT.anchorMax = new Vector2(0.12f, 0.79f);
                }
                else
                {
                    cupRT.anchorMin = new Vector2(0.88f, 0.66f);
                    cupRT.anchorMax = new Vector2(0.98f, 0.79f);
                }
                cupRT.offsetMin = Vector2.zero;
                cupRT.offsetMax = Vector2.zero;
                PlayBounceWithPulse(ovoCupIcon.gameObject, 0.6f, 0.08f, 3f);
            }
        }

        // Winner badge ("CHIẾN THẮNG!") next to winner polygon
        if (ovoWinnerBadge != null)
        {
            ovoWinnerBadge.SetActive(!isTie);
            if (!isTie)
            {
                if (ovoWinnerBadgeText != null)
                    ovoWinnerBadgeText.text = (p1Wins ? p1Name : p2Name) + "\nCHIẾN THẮNG!";
                RectTransform bRT = ovoWinnerBadge.GetComponent<RectTransform>();
                if (bRT != null)
                {
                    if (p1Wins)
                    {
                        bRT.anchorMin = new Vector2(0.08f, 0.78f);
                        bRT.anchorMax = new Vector2(0.42f, 0.86f);
                    }
                    else
                    {
                        bRT.anchorMin = new Vector2(0.58f, 0.78f);
                        bRT.anchorMax = new Vector2(0.92f, 0.86f);
                    }
                    bRT.offsetMin = Vector2.zero;
                    bRT.offsetMax = Vector2.zero;
                }
                PlayBounceWithPulse(ovoWinnerBadge, 0.6f, 0.05f, 4f);
            }
        }

        // Winner polygon pulse
        ResetPolygonScale(ovoP1Polygon);
        ResetPolygonScale(ovoP2Polygon);
        if (!isTie)
        {
            RectTransform winner = p1Wins ? ovoP1Polygon : ovoP2Polygon;
            if (winner != null)
            {
                WinnerEffect fx = winner.gameObject.GetComponent<WinnerEffect>();
                if (fx == null) fx = winner.gameObject.AddComponent<WinnerEffect>();
                fx.StartPulse(0.06f, 2.5f);
            }
        }

        // Score count-up animation
        if (ovoPlayer1ScoreText != null)
            StartCoroutine(ScoreCountUp.Animate(ovoPlayer1ScoreText, 0, p1Score, 1f));
        if (ovoPlayer2ScoreText != null)
            StartCoroutine(ScoreCountUp.Animate(ovoPlayer2ScoreText, 0, p2Score, 1f));
    }

    private void ResetPolygonScale(RectTransform poly)
    {
        if (poly == null) return;
        WinnerEffect fx = poly.GetComponent<WinnerEffect>();
        if (fx != null) fx.Stop();
        poly.localScale = Vector3.one;
    }

    private void PlayBounceWithPulse(GameObject go, float bounceDuration, float pulseAmplitude, float pulseSpeed)
    {
        if (go == null) return;
        WinnerEffect fx = go.GetComponent<WinnerEffect>();
        if (fx == null) fx = go.AddComponent<WinnerEffect>();
        fx.Stop();
        fx.PlayBounceIn(bounceDuration);
        StartCoroutine(StartPulseAfter(fx, bounceDuration, pulseAmplitude, pulseSpeed));
    }

    private System.Collections.IEnumerator StartPulseAfter(WinnerEffect fx, float delay, float amplitude, float speed)
    {
        yield return new WaitForSeconds(delay);
        if (fx != null) fx.StartPulse(amplitude, speed);
    }

    // ===== Team Display =====

    public void DisplayTeamResults(string blueName, int blueScore, string redName, int redScore,
        List<PlayerInfo> bluePlayers, List<PlayerInfo> redPlayers)
    {
        // MVP / Tia Chớp chỉ hiện khi Controller gọi DisplayHighlights() — mặc định ẩn hết.
        HideHighlights();

        if (teamBlueNameText != null) teamBlueNameText.text = blueName;
        if (teamRedNameText != null) teamRedNameText.text = redName;

        bool isTie = blueScore == redScore;
        bool blueWins = blueScore > redScore;

        if (teamConfetti != null) teamConfetti.SetActive(!isTie);

        // Show cup on the winning side with bounce-in + pulse
        if (teamCupIcon != null)
        {
            teamCupIcon.gameObject.SetActive(!isTie);
            if (!isTie)
            {
                RectTransform cupRect = teamCupIcon.rectTransform;
                if (blueWins)
                {
                    cupRect.anchorMin = new Vector2(0.15f, 0.55f);
                    cupRect.anchorMax = new Vector2(0.35f, 0.75f);
                }
                else
                {
                    cupRect.anchorMin = new Vector2(0.65f, 0.55f);
                    cupRect.anchorMax = new Vector2(0.85f, 0.75f);
                }
                cupRect.offsetMin = Vector2.zero;
                cupRect.offsetMax = Vector2.zero;
                PlayBounceWithPulse(teamCupIcon.gameObject, 0.6f, 0.08f, 3f);
            }
        }

        // Winner star pulse
        ResetPolygonScale(teamBlueStar);
        ResetPolygonScale(teamRedStar);
        if (!isTie)
        {
            RectTransform winner = blueWins ? teamBlueStar : teamRedStar;
            if (winner != null)
            {
                WinnerEffect fx = winner.gameObject.GetComponent<WinnerEffect>();
                if (fx == null) fx = winner.gameObject.AddComponent<WinnerEffect>();
                fx.StartPulse(0.06f, 2.5f);
            }
        }

        // Populate avatars (same style as TeamSelect topbar)
        UpdateTeamAvatars(teamBlueAvatarContainer, bluePlayers, avatarBgBlue, ref _blueAvatarSlots);
        UpdateTeamAvatars(teamRedAvatarContainer, redPlayers, avatarBgRed, ref _redAvatarSlots);

        // Score count-up animation
        if (teamBlueScoreText != null)
            StartCoroutine(ScoreCountUp.Animate(teamBlueScoreText, 0, blueScore, 1f));
        if (teamRedScoreText != null)
            StartCoroutine(ScoreCountUp.Animate(teamRedScoreText, 0, redScore, 1f));
    }

    private void UpdateTeamAvatars(Transform container, List<PlayerInfo> members, Sprite bgSprite, ref List<GameObject> slots)
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        if (container == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            // Slot with bg sprite
            GameObject slot = new GameObject("Slot_" + i, typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(container, false);
            Image bgImg = slot.GetComponent<Image>();
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }

            // Character avatar inside
            GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(slot.transform, false);
            RectTransform avRT = avatar.GetComponent<RectTransform>();
            avRT.anchorMin = new Vector2(0.10f, 0.10f);
            avRT.anchorMax = new Vector2(0.90f, 0.90f);
            avRT.offsetMin = Vector2.zero;
            avRT.offsetMax = Vector2.zero;

            Image avImg = avatar.GetComponent<Image>();
            avImg.preserveAspect = true;
            avImg.raycastTarget = false;

            int hairIdx = members[i].HairIndex;
            if (hairIdx >= 0 && charHairSprites != null && hairIdx < charHairSprites.Length && charHairSprites[hairIdx] != null)
            {
                avImg.sprite = charHairSprites[hairIdx];
                avImg.color = Color.white;
            }
            else if (charBodySprite != null)
            {
                avImg.sprite = charBodySprite;
                avImg.color = Color.white;
            }

            slots.Add(slot);
        }
    }

    // ===== Team Display: MVP + Tia Chớp =====

    /// <summary>Ẩn khối MVP và Tia Chớp (gọi tự động đầu DisplayTeamResults).</summary>
    public void HideHighlights()
    {
        if (mvpBadge != null) mvpBadge.SetActive(false);
        if (tiaChopBadge != null) tiaChopBadge.SetActive(false);
        HideSlots(blueFastSlots);
        HideSlots(redFastSlots);
    }

    /// <summary>
    /// Hiện MVP (tên trong khung tròn) và Tia Chớp (tối đa số ô đã gắn trong Inspector cho mỗi đội).
    /// mvp = null → ẩn MVP; danh sách rỗng/null → ẩn ô tương ứng, cả 2 đội rỗng → ẩn luôn khối Tia Chớp.
    /// </summary>
    public void DisplayHighlights(PlayerHighlight mvp, List<PlayerHighlight> blueFast, List<PlayerHighlight> redFast)
    {
        // MVP
        if (mvpBadge != null)
        {
            bool hasMvp = mvp != null && !string.IsNullOrEmpty(mvp.name);
            mvpBadge.SetActive(hasMvp);
            if (hasMvp)
            {
                if (mvpNameText != null) mvpNameText.text = mvp.name;
                SetAvatar(mvpAvatarImage, mvp.hairIndex);
                PlayBounceWithPulse(mvpBadge, 0.6f, 0.05f, 3f);
            }
        }

        // Tia Chớp
        FillSlots(blueFastSlots, blueFast);
        FillSlots(redFastSlots, redFast);
        if (tiaChopBadge != null)
        {
            bool any = (blueFast != null && blueFast.Count > 0) || (redFast != null && redFast.Count > 0);
            tiaChopBadge.SetActive(any);
            if (any) PlayBounceWithPulse(tiaChopBadge, 0.6f, 0.05f, 3f);
        }
    }

    private void FillSlots(HighlightSlot[] slots, List<PlayerHighlight> list)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            HighlightSlot s = slots[i];
            if (s == null || s.root == null) continue;

            bool has = list != null && i < list.Count && list[i] != null;
            s.root.SetActive(has);
            if (!has) continue;

            if (s.label != null) s.label.text = list[i].name;
            SetAvatar(s.avatar, list[i].hairIndex);
        }
    }

    private void HideSlots(HighlightSlot[] slots)
    {
        if (slots == null) return;
        foreach (HighlightSlot s in slots)
            if (s != null && s.root != null) s.root.SetActive(false);
    }

    // Dùng cùng nguồn sprite với avatar đội (charHairSprites theo HairIndex, không có thì dùng thân mặc định)
    private void SetAvatar(Image img, int hairIdx)
    {
        if (img == null) return;
        Sprite sp = null;
        if (hairIdx >= 0 && charHairSprites != null && hairIdx < charHairSprites.Length)
            sp = charHairSprites[hairIdx];
        if (sp == null) sp = charBodySprite;
        img.sprite = sp;
        img.enabled = sp != null;
        img.preserveAspect = true;
    }

    // ===== Team Display: vòng tròn thành viên trong 2 khung =====

    /// <summary>Mỗi thành viên = 1 vòng tròn màu đội, bên trong là CHỮ CÁI ĐẦU tên gọi (từ cuối của tên).
    /// Ô còn thiếu người vẽ vòng tròn nét đứt.</summary>
    public void DisplayRoster(List<PlayerHighlight> blue, List<PlayerHighlight> red)
    {
        BuildRoster(blueRosterGrid, blue, new Color(0.25f, 0.55f, 1f, 1f), new Color(0.55f, 0.78f, 1f, 0.9f));
        BuildRoster(redRosterGrid, red, new Color(0.95f, 0.30f, 0.28f, 1f), new Color(1f, 0.55f, 0.50f, 0.9f));
    }

    private void BuildRoster(Transform grid, List<PlayerHighlight> members, Color teamColor, Color emptyColor)
    {
        if (grid == null) return;

        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            GameObject old = grid.GetChild(i).gameObject;
            old.SetActive(false);   // tránh GridLayout còn tính ô cũ trong frame này
            Destroy(old);
        }

        Font font = rosterFont != null ? rosterFont
                  : (teamBlueNameText != null ? teamBlueNameText.font : null);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Code tự căn vị trí (hàng cuối được căn giữa) nên tắt GridLayoutGroup nếu lỡ có gắn.
        var glg = grid.GetComponent<GridLayoutGroup>();
        if (glg != null) glg.enabled = false;

        int count = members != null ? members.Count : 0;
        int total = Mathf.Max(1, maxMembersPerTeam);
        int perRow = Mathf.Clamp(circlesPerRow, 1, total);
        int rows = Mathf.CeilToInt(total / (float)perRow);

        for (int i = 0; i < total; i++)
        {
            RectTransform cell = i < count
                ? CreateLetterCell(grid, members[i], teamColor, font)
                : CreateEmptyCell(grid, emptyColor);
            PlaceCell(cell, i, total, perRow, rows);
        }
    }

    // Xếp vòng tròn quanh tâm MemberGrid: mỗi hàng căn giữa (hàng thiếu người vẫn nằm giữa khung).
    private void PlaceCell(RectTransform cell, int index, int total, int perRow, int rows)
    {
        int row = index / perRow;
        int col = index % perRow;
        int inRow = Mathf.Min(perRow, total - row * perRow);
        float step = circleSize + circleSpacing;

        cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.pivot = new Vector2(0.5f, 0.5f);
        cell.sizeDelta = new Vector2(circleSize, circleSize);
        cell.anchoredPosition = new Vector2(
            (col - (inRow - 1) / 2f) * step,
            ((rows - 1) / 2f - row) * step);
    }

    private static RectTransform NewChild(string name, Transform parent, params Type[] extra)
    {
        var types = new List<Type> { typeof(RectTransform) };
        types.AddRange(extra);
        var go = new GameObject(name, types.ToArray());
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Tên gọi của người Việt là từ CUỐI ("Nguyễn Văn An" → "A"); tên 1 từ thì lấy chính nó.
    private static string GetInitial(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";
        string[] parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string given = parts[parts.Length - 1];
        return char.ToUpperInvariant(given[0]).ToString();
    }

    private RectTransform CreateLetterCell(Transform grid, PlayerHighlight m, Color teamColor, Font font)
    {
        RectTransform cell = NewChild("Member_" + m.name, grid, typeof(Image));
        Image circle = cell.GetComponent<Image>();
        circle.sprite = GetLetterCircleSprite();
        circle.color = teamColor;
        circle.preserveAspect = true;
        circle.raycastTarget = false;

        RectTransform letterRt = NewChild("Letter", cell, typeof(Text));
        Stretch(letterRt, 0f, 0f, 1f, 1f);
        Text t = letterRt.GetComponent<Text>();
        t.font = font;
        t.text = GetInitial(m.name);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 44;
        t.raycastTarget = false;

        Outline ol = letterRt.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.55f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        if (m.score > 0) CreateScoreBadge(cell, m.score);
        return cell;
    }

    // Số điểm riêng của người đó (số câu đúng), hiện thành 1 chấm tròn nhỏ màu vàng ở góc dưới-phải
    // vòng tròn tên — chỉ vẽ khi > 0 để tránh rối mắt với những bạn chưa trả lời câu nào.
    private void CreateScoreBadge(RectTransform parentCell, int score)
    {
        RectTransform badge = NewChild("ScoreBadge", parentCell, typeof(Image));
        badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(circleSize * 0.42f, circleSize * 0.42f);
        // Đặt tâm chấm điểm gần VIỀN vòng tròn tên (theo đường chéo dưới-phải), không phải ở góc
        // hộp bao ngoài — trước đó neo (1,0) rơi ra ngoài mép tròn nên nhìn tách rời, xa tên.
        float d = circleSize * 0.30f;
        badge.anchoredPosition = new Vector2(d, -d);

        Image bg = badge.GetComponent<Image>();
        bg.sprite = GetLetterCircleSprite();
        bg.color = new Color(1f, 0.78f, 0.15f, 1f); // vàng — tách biệt màu đội để dễ thấy
        bg.raycastTarget = false;

        Font font = rosterFont != null ? rosterFont
                  : (teamBlueNameText != null ? teamBlueNameText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        RectTransform numRt = NewChild("Num", badge, typeof(Text));
        Stretch(numRt, 0f, 0f, 1f, 1f);
        Text nt = numRt.GetComponent<Text>();
        nt.font = font;
        nt.text = score.ToString();
        nt.fontStyle = FontStyle.Bold;
        nt.alignment = TextAnchor.MiddleCenter;
        nt.color = new Color(0.35f, 0.18f, 0f, 1f);
        nt.resizeTextForBestFit = true;
        nt.resizeTextMinSize = 6;
        nt.resizeTextMaxSize = 22;
        nt.raycastTarget = false;
    }

    private RectTransform CreateEmptyCell(Transform grid, Color emptyColor)
    {
        RectTransform cell = NewChild("Empty", grid, typeof(Image));
        Image ring = cell.GetComponent<Image>();
        ring.sprite = GetEmptyRingSprite();
        ring.color = emptyColor;
        ring.preserveAspect = true;
        ring.raycastTarget = false;
        return cell;
    }

    // Hình tròn đặc (màu do Image.color quyết định) + viền tối hơn 1 chút — vẽ bằng code, không cần sprite ngoài.
    private Sprite GetLetterCircleSprite()
    {
        if (_letterCircleSprite != null) return _letterCircleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float c = size / 2f;
        Vector2 center = new Vector2(c, c);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(c - d);                          // mép mềm
                float rimT = Mathf.Clamp01((d - (c - 10f)) / 3f);         // viền 10px phía ngoài
                float v = Mathf.Lerp(1f, 0.72f, rimT);
                px[y * size + x] = new Color(v, v, v, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _letterCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _letterCircleSprite;
    }

    // Vòng tròn nét đứt cho ô còn trống (màu do Image.color quyết định).
    private Sprite GetEmptyRingSprite()
    {
        if (_emptyRingSprite != null) return _emptyRingSprite;

        const int size = 64;
        const float outer = 30f, inner = 27f;
        const int dashes = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        Vector2 c = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float d = p.magnitude;
                float ring = Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f);
                float ang = (Mathf.Atan2(p.y, p.x) + Mathf.PI) / (2f * Mathf.PI);
                float dashMask = (ang * dashes) % 1f < 0.55f ? 1f : 0f;
                px[y * size + x] = new Color(1f, 1f, 1f, ring * dashMask);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _emptyRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _emptyRingSprite;
    }

    // ===== Công cụ chỉnh layout 2 thanh đội bằng 1 cú bấm =====
    // Ảnh thanh (665x375) đã có sẵn NGÔI SAO ở 1 đầu và chừa nhiều vùng trong suốt phía trên/dưới,
    // nên khung ảnh phải cao hơn thanh thấy được. Vị trí ô tên / ô điểm đo từ chính ảnh đó.
    // Chạy ở EDIT MODE (tắt Play): ⋮ trên component Score Scene View → "Fix layout thanh đội".
    // Xong bấm Ctrl+S để lưu scene.
    [Header("=== Công cụ chỉnh thanh đội (chỉ dùng lúc dựng giao diện) ===")]
    [SerializeField] private bool blueBarStarAtLeft = true;    // thanh xanh: sao nằm ở đầu TRÁI của ảnh
    [SerializeField] private bool redBarStarAtLeft = false;    // thanh đỏ: sao nằm ở đầu PHẢI của ảnh (tick nếu sao ở TRÁI)

    [ContextMenu("Fix layout thanh đội (làm ở Edit mode)")]
    private void FixTeamBarLayout()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ScoreSceneView] Hãy TẮT Play rồi chạy lệnh này — chỉnh lúc đang Play sẽ bị mất khi thoát.");
            return;
        }

        LayoutTeamBar(teamBlueNameText, teamBlueScoreText,
            new Vector2(0.02f, 0.664f), new Vector2(0.43f, 1.058f), blueBarStarAtLeft);
        LayoutTeamBar(teamRedNameText, teamRedScoreText,
            new Vector2(0.57f, 0.664f), new Vector2(0.98f, 1.058f), redBarStarAtLeft);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        Debug.Log("[ScoreSceneView] Đã chỉnh xong layout 2 thanh đội — bấm Ctrl+S để lưu scene.");
    }

    private void LayoutTeamBar(Text nameText, Text scoreText, Vector2 barMin, Vector2 barMax, bool starAtLeft)
    {
        if (nameText == null || scoreText == null)
        {
            Debug.LogWarning("[ScoreSceneView] Thiếu ô Team Blue/Red Name hoặc Score trong Inspector — không chỉnh được thanh.");
            return;
        }

        RectTransform bar = nameText.rectTransform.parent as RectTransform;
        if (bar == null) return;
        if (scoreText.rectTransform.parent != bar) scoreText.rectTransform.SetParent(bar, false);

        SetRect(bar, barMin, barMax);
        Image barImg = bar.GetComponent<Image>();
        if (barImg != null)
        {
            barImg.type = Image.Type.Simple;
            barImg.preserveAspect = true;
            barImg.raycastTarget = false;
        }

        // Ô tên = khối tối, ô điểm = viên thuốc sáng. Sao ở TRÁI → ảnh lật ngang so với bản gốc → ô tên nằm bên phải.
        Vector2 nameMin, nameMax, scoreMin, scoreMax;
        if (starAtLeft)
        {
            nameMin = new Vector2(0.705f, 0.405f); nameMax = new Vector2(0.91f, 0.595f);
            scoreMin = new Vector2(0.39f, 0.39f);  scoreMax = new Vector2(0.66f, 0.60f);
        }
        else
        {
            nameMin = new Vector2(0.09f, 0.405f);  nameMax = new Vector2(0.295f, 0.595f);
            scoreMin = new Vector2(0.34f, 0.39f);  scoreMax = new Vector2(0.61f, 0.60f);
        }

        SetRect(nameText.rectTransform, nameMin, nameMax);
        StyleBarText(nameText, 30);
        SetRect(scoreText.rectTransform, scoreMin, scoreMax);
        StyleBarText(scoreText, 40);
    }

    private static void SetRect(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;   // Left/Bottom = 0
        rt.offsetMax = Vector2.zero;   // Right/Top = 0
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);
    }

    private static void StyleBarText(Text t, int maxSize)
    {
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = maxSize;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;

        Outline ol = t.GetComponent<Outline>();
        if (ol == null) ol = t.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.55f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // Test giao diện bằng dữ liệu giả: Play ScoreScene → click phải component ScoreSceneView → "Demo highlights".
    [ContextMenu("Demo highlights (Play mode)")]
    private void DemoHighlights()
    {
        ShowTeamMode();
        DisplayHighlights(
            new PlayerHighlight { name = "An", hairIndex = 0, bestTime = 1.2f },
            new List<PlayerHighlight>
            {
                new PlayerHighlight { name = "An",   hairIndex = 0, bestTime = 1.2f },
                new PlayerHighlight { name = "Công", hairIndex = 1, bestTime = 1.6f },
            },
            new List<PlayerHighlight>
            {
                new PlayerHighlight { name = "Phúc",  hairIndex = 2, bestTime = 1.4f },
                new PlayerHighlight { name = "Giang", hairIndex = 3, bestTime = 1.9f },
            });

        DisplayRoster(
            new List<PlayerHighlight>
            {
                new PlayerHighlight { name = "An",    hairIndex = 0, score = 9 },
                new PlayerHighlight { name = "Bình",  hairIndex = 1, score = 7 },
                new PlayerHighlight { name = "Công",  hairIndex = 2, score = 6 },
                new PlayerHighlight { name = "Dương", hairIndex = 3, score = 4 },
                new PlayerHighlight { name = "Em",    hairIndex = 4, score = 2 },
            },
            new List<PlayerHighlight>
            {
                new PlayerHighlight { name = "Phúc",  hairIndex = 3, score = 8 },
                new PlayerHighlight { name = "Giang", hairIndex = 4, score = 5 },
            });
    }
}