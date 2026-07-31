using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for browsing and selecting saved teams.
/// Vertical list: team name on left, member icons on right, delete button on far right.
/// User selects 2 teams: first becomes Blue, second becomes Red.
/// </summary>
public class PanelSavedTeamView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform teamGridContent;
    [SerializeField] private GameObject teamCardTemplate;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;

    public event Action OnClose = delegate { };
    public event Action OnConfirm = delegate { };
    public event Action<string> OnTeamSelected = delegate { };
    public event Action<string> OnDeleteTeam = delegate { };

    [Header("=== Team Bar Sprites ===")]
    [SerializeField] private Sprite teamBarGray;
    [SerializeField] private Sprite teamBarBlue;
    [SerializeField] private Sprite teamBarRed;
    [SerializeField] private Sprite avatarBgWhite;        // unselected slot bg
    [SerializeField] private Sprite avatarBgBlue;         // blue team slot bg
    [SerializeField] private Sprite avatarBgRed;          // red team slot bg
    [SerializeField] private Sprite[] charHairSprites;    // character hair sprites
    [SerializeField] private Sprite charBodySprite;       // bald body
    [SerializeField] private Sprite[] charHairFemaleSprites;
    [SerializeField] private Sprite femaleBodySprite;

    private List<GameObject> _teamCards = new List<GameObject>();
    private List<string> _teamIds = new List<string>();
    private string _selectedBlueId;
    private string _selectedRedId;

    public void InitPanel()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => OnClose());
        if (backButton != null) backButton.onClick.AddListener(() => OnClose());
        if (confirmButton != null) confirmButton.onClick.AddListener(() => OnConfirm());

        Hide();
    }

    public void Show(List<TeamData> teams)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        _selectedBlueId = null;
        _selectedRedId = null;
        if (confirmButton != null) confirmButton.interactable = false;

        // Clear old cards
        foreach (GameObject card in _teamCards)
        {
            Destroy(card);
        }
        _teamCards.Clear();
        _teamIds.Clear();

        if (teamGridContent == null || teamCardTemplate == null) return;

        foreach (TeamData team in teams)
        {
            GameObject card = Instantiate(teamCardTemplate, teamGridContent);
            card.SetActive(true);

            // Set team name (TeamName child is anchored at left 0.02-0.30)
            Transform nameT = card.transform.Find("TeamName");
            if (nameT != null)
            {
                Text nameText = nameT.GetComponent<Text>();
                if (nameText != null) nameText.text = team.TeamName;
            }

            // Create member avatar icons on right side of the row
            CreateMemberIcons(card.transform, team.MemberPlayerIds);

            string teamId = team.TeamId;

            // Card tap for team selection
            Button btn = card.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    SelectTeam(teamId);
                    OnTeamSelected(teamId);
                });
            }

            _teamCards.Add(card);
            _teamIds.Add(team.TeamId);
        }
    }

    private void CreateMemberIcons(Transform cardTransform, List<string> memberIds)
    {
        // Avatar grid inside the team bar (3 columns, like topbar)
        GameObject iconGrid = new GameObject("MemberIcons", typeof(RectTransform), typeof(GridLayoutGroup));
        iconGrid.transform.SetParent(cardTransform, false);

        RectTransform gridRT = iconGrid.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.35f, 0.02f);
        gridRT.anchorMax = new Vector2(0.98f, 0.98f);
        gridRT.offsetMin = Vector2.zero;
        gridRT.offsetMax = Vector2.zero;

        GridLayoutGroup glg = iconGrid.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(50f, 50f);
        glg.spacing = new Vector2(2f, 1f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.MiddleCenter;

        int maxIcons = Mathf.Min(memberIds.Count, 6);
        for (int i = 0; i < maxIcons; i++)
        {
            PlayerInfo player = DataManager.Instance.GetPlayer(memberIds[i]);
            CreateAvatarSlot(iconGrid.transform, player, null);
        }
    }

    private void CreateAvatarSlot(Transform parent, PlayerInfo player, Sprite bgSprite)
    {
        // Background slot (white/blue/red)
        GameObject slot = new GameObject("Slot", typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);
        slot.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);

        Image slotImg = slot.GetComponent<Image>();
        Sprite bg = bgSprite != null ? bgSprite : avatarBgWhite;
        if (bg != null) { slotImg.sprite = bg; slotImg.color = Color.white; }
        else slotImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

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

        if (player != null)
        {
            int hairIdx = player.HairIndex;
            Sprite[] hairArr = (player.Gender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                ? charHairFemaleSprites : charHairSprites;
            Sprite bodyFallback = (player.Gender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;
            if (hairIdx >= 0 && hairArr != null && hairIdx < hairArr.Length && hairArr[hairIdx] != null)
            {
                avImg.sprite = hairArr[hairIdx];
                avImg.color = Color.white;
            }
            else if (bodyFallback != null)
            {
                avImg.sprite = bodyFallback;
                avImg.color = Color.white;
            }
        }
    }

    private void CreateDeleteButton(Transform cardTransform, string teamId)
    {
        GameObject delGo = new GameObject("DeleteBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        delGo.transform.SetParent(cardTransform, false);

        RectTransform delRT = delGo.GetComponent<RectTransform>();
        delRT.anchorMin = new Vector2(0.87f, 0.15f);
        delRT.anchorMax = new Vector2(0.98f, 0.85f);
        delRT.offsetMin = Vector2.zero;
        delRT.offsetMax = Vector2.zero;

        Image delImg = delGo.GetComponent<Image>();
        delImg.color = new Color(0.9f, 0.3f, 0.3f, 1f);

        // "X" label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(delGo.transform, false);
        RectTransform labelRT = labelGo.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        Text labelText = labelGo.GetComponent<Text>();
        labelText.text = "X";
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 16;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = Color.white;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelText.font == null)
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Button delBtn = delGo.GetComponent<Button>();
        delBtn.onClick.AddListener(() => OnDeleteTeam(teamId));
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void SelectTeam(string teamId)
    {
        if (_selectedBlueId == null)
        {
            _selectedBlueId = teamId;
        }
        else if (_selectedRedId == null && teamId != _selectedBlueId)
        {
            _selectedRedId = teamId;
        }
        else
        {
            // Reset and start over
            _selectedBlueId = teamId;
            _selectedRedId = null;
        }

        UpdateCardColors();
        if (confirmButton != null)
        {
            confirmButton.interactable = (_selectedBlueId != null && _selectedRedId != null);
        }
    }

    private void UpdateCardColors()
    {
        for (int i = 0; i < _teamCards.Count && i < _teamIds.Count; i++)
        {
            // Update card bar sprite
            Image img = _teamCards[i].GetComponent<Image>();
            if (img == null) continue;

            Sprite barSprite;
            Sprite slotBg;
            if (_teamIds[i] == _selectedBlueId)
            {
                barSprite = teamBarBlue;
                slotBg = avatarBgBlue;
            }
            else if (_teamIds[i] == _selectedRedId)
            {
                barSprite = teamBarRed;
                slotBg = avatarBgRed;
            }
            else
            {
                barSprite = teamBarGray;
                slotBg = avatarBgWhite;
            }

            if (barSprite != null) img.sprite = barSprite;
            img.color = Color.white;

            // Update all avatar slot backgrounds in this card
            Transform iconsT = _teamCards[i].transform.Find("MemberIcons");
            if (iconsT != null && slotBg != null)
            {
                foreach (Transform slotT in iconsT)
                {
                    Image slotImg = slotT.GetComponent<Image>();
                    if (slotImg != null) { slotImg.sprite = slotBg; slotImg.color = Color.white; }
                }
            }
        }
    }

    public string GetSelectedBlueTeamId() { return _selectedBlueId; }
    public string GetSelectedRedTeamId() { return _selectedRedId; }
}
