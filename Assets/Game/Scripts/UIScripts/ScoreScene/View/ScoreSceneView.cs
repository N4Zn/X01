using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
}
