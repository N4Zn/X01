using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for class management — list students with avatar, name, score, rank.
/// Design: rows with [#] [name bar] [avatar] [score] [rank] [X] [edit]
/// </summary>
public class PanelClassView : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private InputField classNameInput;
    [SerializeField] private Transform studentListContent;
    [SerializeField] private GameObject studentRowTemplate;
    [SerializeField] private Button addStudentButton;
    [SerializeField] private Button copyButton;
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
    public event Action OnCopy = delegate { };
    public event Action OnDeleteClass = delegate { };
    public event Action<string> OnClassNameChanged = delegate { };
    public event Action OnAddStudent = delegate { };
    public event Action<int> OnDeleteStudent = delegate { };
    public event Action<int> OnEditStudent = delegate { };

    private List<GameObject> _studentRows = new List<GameObject>();

    public void InitPanel()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => OnClose());
        if (saveButton != null) saveButton.onClick.AddListener(() => OnSave());
        if (copyButton != null) copyButton.onClick.AddListener(() => OnCopy());
        if (deleteButton != null) deleteButton.onClick.AddListener(() => OnDeleteClass());
        if (addStudentButton != null) addStudentButton.onClick.AddListener(() => OnAddStudent());
        if (classNameInput != null) classNameInput.onEndEdit.AddListener((val) => OnClassNameChanged(val));

        Hide();
    }

    public void Show(ClassData classData)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (classNameInput != null) classNameInput.text = classData.ClassName;
        UpdateStudentList(classData.Students);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void UpdateStudentList(List<PlayerInfo> students)
    {
        foreach (GameObject row in _studentRows) Destroy(row);
        _studentRows.Clear();

        if (studentListContent == null || studentRowTemplate == null) return;

        for (int i = 0; i < students.Count; i++)
        {
            GameObject row = Instantiate(studentRowTemplate, studentListContent);
            row.SetActive(true);

            // Number
            Transform numT = row.transform.Find("NumLabel");
            if (numT != null)
            {
                Text numText = numT.GetComponent<Text>();
                if (numText != null) numText.text = (i + 1).ToString();
            }

            // Name
            Transform nameBarT = row.transform.Find("NameBar");
            if (nameBarT != null)
            {
                Transform nameT = nameBarT.Find("NameText");
                if (nameT != null)
                {
                    Text nameText = nameT.GetComponent<Text>();
                    if (nameText != null) nameText.text = students[i].PlayerName;
                }
            }

            // Avatar
            Transform avatarT = row.transform.Find("Avatar");
            if (avatarT != null)
            {
                Image avatarImg = avatarT.GetComponent<Image>();
                if (avatarImg != null)
                {
                    int hairIdx = students[i].HairIndex;
                    Sprite[] hairArr = (students[i].Gender == 1 && charHairFemaleSprites != null && charHairFemaleSprites.Length > 0)
                        ? charHairFemaleSprites : charHairSprites;
                    Sprite bodyFallback = (students[i].Gender == 1 && femaleBodySprite != null) ? femaleBodySprite : charBodySprite;
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

                Transform glassesT = avatarT.Find("Glasses");
                if (glassesT != null)
                {
                    int glassesIdx = students[i].GlassesIndex;
                    if (glassesIdx >= 0 && bigGlassesSprites != null && glassesIdx < bigGlassesSprites.Length && bigGlassesSprites[glassesIdx] != null)
                    {
                        Image gImg = glassesT.GetComponent<Image>();
                        gImg.sprite = bigGlassesSprites[glassesIdx];
                        gImg.preserveAspect = true;
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

            // Score
            Transform scoreT = row.transform.Find("ScoreText");
            if (scoreT != null)
            {
                Text scoreText = scoreT.GetComponent<Text>();
                if (scoreText != null) scoreText.text = students[i].AccumulatedScore.ToString();
            }

            // Rank / Cups
            Transform rankT = row.transform.Find("RankText");
            if (rankT != null)
            {
                Text rankText = rankT.GetComponent<Text>();
                if (rankText != null) rankText.text = students[i].Cups.ToString();
            }

            // Delete button
            int delIndex = i;
            Transform delT = row.transform.Find("DeleteBtn");
            if (delT != null)
            {
                Button delBtn = delT.GetComponent<Button>();
                if (delBtn != null) delBtn.onClick.AddListener(() => OnDeleteStudent(delIndex));
            }

            // Edit button
            int editIndex = i;
            Transform editT = row.transform.Find("EditBtn");
            if (editT != null)
            {
                Button editBtn = editT.GetComponent<Button>();
                if (editBtn != null) editBtn.onClick.AddListener(() => OnEditStudent(editIndex));
            }

            _studentRows.Add(row);
        }
    }

    public string GetClassName()
    {
        return classNameInput != null ? classNameInput.text : "";
    }
}
