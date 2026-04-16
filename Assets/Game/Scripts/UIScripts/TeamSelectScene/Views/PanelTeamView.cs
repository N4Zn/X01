using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for team management — edit team name and member grid (3 per row with character sprites).
/// </summary>
public class PanelTeamView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image teamBarImage;
    [SerializeField] private Sprite teamBarBlueSprite;
    [SerializeField] private Sprite teamBarRedSprite;
    [SerializeField] private Text teamNameText;
    [SerializeField] private InputField teamNameInput;
    [SerializeField] private Transform memberListContent;
    [SerializeField] private GameObject memberCardTemplate;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button closeButton;

    [Header("=== Character Sprites ===")]
    [SerializeField] private Sprite[] charHairSprites;
    [SerializeField] private Sprite charBodySprite;
    [SerializeField] private Sprite[] charHairFemaleSprites;
    [SerializeField] private Sprite femaleBodySprite;
    [SerializeField] private Sprite[] bigGlassesSprites;

    public event Action OnClose = delegate { };
    public event Action OnSave = delegate { };
    public event Action OnDeleteTeam = delegate { };
    public event Action<string> OnTeamNameChanged = delegate { };
    public event Action<int> OnDeleteMember = delegate { };

    private List<GameObject> _memberCards = new List<GameObject>();

    public void InitPanel()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => OnClose());
        if (saveButton != null) saveButton.onClick.AddListener(() => OnSave());
        if (deleteButton != null) deleteButton.onClick.AddListener(() => OnDeleteTeam());
        if (teamNameInput != null) teamNameInput.onEndEdit.AddListener((val) => OnTeamNameChanged(val));

        Hide();
    }

    public void Show(string teamName, List<PlayerInfo> members, bool isBlueTeam = true)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (teamNameInput != null) teamNameInput.text = teamName;
        if (teamNameText != null) teamNameText.text = teamName;

        // Set team bar color
        if (teamBarImage != null)
        {
            Sprite barSprite = isBlueTeam ? teamBarBlueSprite : teamBarRedSprite;
            if (barSprite != null) teamBarImage.sprite = barSprite;
        }

        UpdateMemberList(members);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void UpdateMemberList(List<PlayerInfo> members)
    {
        foreach (GameObject card in _memberCards) Destroy(card);
        _memberCards.Clear();

        if (memberListContent == null || memberCardTemplate == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            GameObject row = Instantiate(memberCardTemplate, memberListContent);
            row.SetActive(true);

            // Set row number
            Transform numT = row.transform.Find("NumLabel");
            if (numT != null)
            {
                Text numText = numT.GetComponent<Text>();
                if (numText != null) numText.text = (i + 1).ToString();
            }

            // Set name in name bar
            Transform nameBarT = row.transform.Find("NameBar");
            if (nameBarT != null)
            {
                Transform nameT = nameBarT.Find("NameText");
                if (nameT != null)
                {
                    Text nameText = nameT.GetComponent<Text>();
                    if (nameText != null) nameText.text = members[i].PlayerName;
                }
            }

            // Set character avatar
            Transform avatarT = row.transform.Find("Avatar");
            if (avatarT != null)
            {
                Image avatarImg = avatarT.GetComponent<Image>();
                if (avatarImg != null)
                {
                    int hairIdx = members[i].HairIndex;
                    Sprite[] hairArr = (members[i].Gender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                        ? charHairFemaleSprites : charHairSprites;
                    Sprite bodyFallback = (members[i].Gender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;
                    if (hairIdx >= 0 && hairArr != null && hairIdx < hairArr.Length && hairArr[hairIdx] != null)
                    {
                        avatarImg.sprite = hairArr[hairIdx];
                        avatarImg.preserveAspect = true;
                        avatarImg.color = Color.white;
                    }
                    else if (bodyFallback != null)
                    {
                        avatarImg.sprite = bodyFallback;
                        avatarImg.preserveAspect = true;
                        avatarImg.color = Color.white;
                    }
                }

                // Glasses overlay
                Transform glassesT = avatarT.Find("Glasses");
                if (glassesT != null)
                {
                    int glassesIdx = members[i].GlassesIndex;
                    if (glassesIdx >= 0 && bigGlassesSprites != null && glassesIdx < bigGlassesSprites.Length && bigGlassesSprites[glassesIdx] != null)
                    {
                        Image glassesImg = glassesT.GetComponent<Image>();
                        glassesImg.sprite = bigGlassesSprites[glassesIdx];
                        glassesImg.preserveAspect = true;
                        glassesT.gameObject.SetActive(true);
                        RectTransform gRT = glassesT.GetComponent<RectTransform>();
                        if (glassesIdx == 2)
                        {
                            gRT.offsetMin = new Vector2(1.5f, -13.1f);
                            gRT.offsetMax = new Vector2(-34f, 11.4f);
                        }
                    }
                    else
                    {
                        glassesT.gameObject.SetActive(false);
                    }
                }
            }

            // Wire delete button
            int index = i;
            Transform delT = row.transform.Find("DeleteBtn");
            if (delT != null)
            {
                Button delBtn = delT.GetComponent<Button>();
                if (delBtn != null) delBtn.onClick.AddListener(() => OnDeleteMember(index));
            }

            _memberCards.Add(row);
        }
    }

    public string GetTeamName()
    {
        return teamNameInput != null ? teamNameInput.text : "";
    }
}
