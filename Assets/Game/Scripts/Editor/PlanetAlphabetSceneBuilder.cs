using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlanetAlphabetSceneBuilder
{
    private const string SceneFolder = "Assets/Game/Scenes/PlanetAlphabetGame";
    private const string ScenePath = "Assets/Game/Scenes/PlanetAlphabetGame/PlanetAlphabetGame.unity";
    private const string ArtFolder = "Assets/Game/Textures/AddUpGame";

    private static Sprite LoadArt(string name)
    {
        string path = ArtFolder + "/" + name + ".png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) { TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter; if (imp != null) { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false; imp.SaveAndReimport(); s = AssetDatabase.LoadAssetAtPath<Sprite>(path); } }
        return s;
    }

    [MenuItem("Tools/PlanetOrder/Build Alphabet Game Scene")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("PlanetAlphabetSceneBuilder: Cannot build in Play mode."); return; }
        EnsureFolder("Assets/Game"); EnsureFolder("Assets/Game/Scenes"); EnsureFolder(SceneFolder);
        BuildScene();
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("PlanetAlphabetSceneBuilder: finished.");
    }

    private static void BuildScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        GameObject cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera"; Camera c = cam.GetComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.96f, 0.91f, 0.82f, 1f); c.orthographic = true; c.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);

        // Canvas
        GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler sc = canvasGo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1024, 600); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject root = new GameObject("PlanetAlphabetRoot");
        PlanetAlphabetController ctrl = root.AddComponent<PlanetAlphabetController>();
        PlanetAlphabetView view = root.AddComponent<PlanetAlphabetView>();

        // === BG, Team bars, Timer, Score bars, Nav buttons ===
        Sprite bgSpr = LoadArt("bg_gameplay");
        GameObject bg = CSI("Background", canvasGo.transform, bgSpr); bg.GetComponent<Image>().raycastTarget = false; SA(bg, 0, 0, 1, 1);

        Sprite tbSpr = LoadArt("team_bar_blue"); Sprite trSpr = LoadArt("team_bar_red");
        GameObject tb = CSI("TeamBlueBar", canvasGo.transform, tbSpr); tb.GetComponent<Image>().raycastTarget = false;
        RectTransform tbR = tb.GetComponent<RectTransform>(); tbR.anchorMin = new Vector2(0.01f, 0.88f); tbR.anchorMax = new Vector2(0.35f, 0.99f); tbR.anchoredPosition = new Vector2(89f, -9f); tbR.sizeDelta = new Vector2(-53.12f, 17.22f);
        GameObject p1N = CT("P1BarName", tb.transform, "Player 1", 16, TextAnchor.MiddleCenter); SA(p1N, 0.15f, 0.05f, 0.9f, 0.95f); p1N.GetComponent<Text>().color = Color.white; p1N.GetComponent<Text>().fontStyle = FontStyle.Bold;

        GameObject tr = CSI("TeamRedBar", canvasGo.transform, trSpr); tr.GetComponent<Image>().raycastTarget = false;
        RectTransform trR = tr.GetComponent<RectTransform>(); trR.anchorMin = new Vector2(0.65f, 0.88f); trR.anchorMax = new Vector2(0.99f, 0.99f); trR.anchoredPosition = new Vector2(-59f, -7f); trR.sizeDelta = new Vector2(-74f, 9.88f);
        GameObject p2N = CT("P2BarName", tr.transform, "Player 2", 16, TextAnchor.MiddleCenter); SA(p2N, 0.10f, 0.05f, 0.85f, 0.95f); p2N.GetComponent<Text>().color = Color.white; p2N.GetComponent<Text>().fontStyle = FontStyle.Bold;

        Sprite tcSpr = LoadArt("timer_circle");
        GameObject tmBg = CSI("TimerBg", canvasGo.transform, tcSpr); tmBg.GetComponent<Image>().raycastTarget = false;
        RectTransform tmR = tmBg.GetComponent<RectTransform>(); tmR.anchorMin = tmR.anchorMax = new Vector2(0.5f, 1f); tmR.anchoredPosition = new Vector2(0, -41); tmR.sizeDelta = new Vector2(60, 60);
        GameObject tmT = CT("TimerText", tmBg.transform, "100", 22, TextAnchor.MiddleCenter); SA(tmT, 0, 0, 1, 1); tmT.GetComponent<Text>().color = Color.white; tmT.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Score bars
        Sprite bwS = LoadArt("bar_white"); Sprite bbS = LoadArt("bar_blue"); Sprite brS = LoadArt("bar_red"); Sprite stS = LoadArt("icon_star");
        GameObject lbT = CSI("LeftScoreBar", canvasGo.transform, bwS); lbT.GetComponent<Image>().color = new Color(0.9f,0.9f,0.9f,0.8f); lbT.GetComponent<Image>().raycastTarget = false; lbT.GetComponent<Image>().preserveAspect = false;
        RectTransform lbR = lbT.GetComponent<RectTransform>(); lbR.anchorMin = new Vector2(0.005f,0.05f); lbR.anchorMax = new Vector2(0.03f,0.86f); lbR.anchoredPosition = new Vector2(483f,-34f); lbR.sizeDelta = new Vector2(-8f,-167.96f);
        GameObject lbF = CSI("LeftFill", lbT.transform, bbS); Image lbFI = lbF.GetComponent<Image>(); lbFI.type = Image.Type.Filled; lbFI.fillMethod = Image.FillMethod.Vertical; lbFI.fillOrigin = 0; lbFI.fillAmount = 0; lbFI.raycastTarget = false; lbFI.preserveAspect = false;
        RectTransform lbFR = lbF.GetComponent<RectTransform>(); lbFR.anchorMin = Vector2.zero; lbFR.anchorMax = Vector2.one; lbFR.offsetMin = Vector2.zero; lbFR.offsetMax = Vector2.zero;
        GameObject rbT = CSI("RightScoreBar", canvasGo.transform, bwS); rbT.GetComponent<Image>().color = new Color(0.9f,0.9f,0.9f,0.8f); rbT.GetComponent<Image>().raycastTarget = false; rbT.GetComponent<Image>().preserveAspect = false;
        RectTransform rbR = rbT.GetComponent<RectTransform>(); rbR.anchorMin = new Vector2(0.97f,0.05f); rbR.anchorMax = new Vector2(0.995f,0.86f); rbR.anchoredPosition = new Vector2(-475.86f,-32.34f); rbR.sizeDelta = new Vector2(-5.83f,-173.92f);
        GameObject rbF = CSI("RightFill", rbT.transform, brS); Image rbFI = rbF.GetComponent<Image>(); rbFI.type = Image.Type.Filled; rbFI.fillMethod = Image.FillMethod.Vertical; rbFI.fillOrigin = 0; rbFI.fillAmount = 0; rbFI.raycastTarget = false; rbFI.preserveAspect = false;
        RectTransform rbFR = rbF.GetComponent<RectTransform>(); rbFR.anchorMin = Vector2.zero; rbFR.anchorMax = Vector2.one; rbFR.offsetMin = Vector2.zero; rbFR.offsetMax = Vector2.zero;

        // Star icons + score labels
        GameObject lSt = CSI("LS", canvasGo.transform, stS); lSt.GetComponent<Image>().raycastTarget = false; RectTransform lStR = lSt.GetComponent<RectTransform>(); lStR.anchorMin = lStR.anchorMax = new Vector2(0, 0.86f); lStR.pivot = new Vector2(0.5f, 0f); lStR.anchoredPosition = new Vector2(185f, -100f); lStR.sizeDelta = new Vector2(72, 72);
        GameObject lSc = CT("P1ScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter); RectTransform lScR = lSc.GetComponent<RectTransform>(); lScR.anchorMin = lScR.anchorMax = new Vector2(0, 0.86f); lScR.pivot = new Vector2(0.5f, 0f); lScR.anchoredPosition = new Vector2(291f, -100f); lScR.sizeDelta = new Vector2(120, 60); lSc.GetComponent<Text>().color = new Color(0.3f, 0.5f, 0.9f); lSc.GetComponent<Text>().fontStyle = FontStyle.Bold; lSc.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        GameObject rSt = CSI("RS", canvasGo.transform, stS); rSt.GetComponent<Image>().raycastTarget = false; RectTransform rStR = rSt.GetComponent<RectTransform>(); rStR.anchorMin = rStR.anchorMax = new Vector2(1, 0.86f); rStR.pivot = new Vector2(0.5f, 0f); rStR.anchoredPosition = new Vector2(-185f, -100f); rStR.sizeDelta = new Vector2(72, 72);
        GameObject rSc = CT("P2ScoreLabel", canvasGo.transform, "0", 48, TextAnchor.MiddleCenter); RectTransform rScR = rSc.GetComponent<RectTransform>(); rScR.anchorMin = rScR.anchorMax = new Vector2(1, 0.86f); rScR.pivot = new Vector2(0.5f, 0f); rScR.anchoredPosition = new Vector2(-291f, -100f); rScR.sizeDelta = new Vector2(120, 60); rSc.GetComponent<Text>().color = new Color(0.9f, 0.3f, 0.3f); rSc.GetComponent<Text>().fontStyle = FontStyle.Bold; rSc.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;

        // Divider
        GameObject div = CI("Divider", canvasGo.transform, new Color(0.35f, 0.6f, 0.9f, 0.6f));
        RectTransform dR = div.GetComponent<RectTransform>(); dR.anchorMin = new Vector2(0.5f, 0.05f); dR.anchorMax = new Vector2(0.5f, 0.82f); dR.anchoredPosition = new Vector2(2, 0); dR.sizeDelta = new Vector2(4, 0);

        // Nav buttons
        Sprite bkS = LoadArt("btn_back"); Sprite hmS = LoadArt("btn_home"); Sprite stBS = LoadArt("btn_setting");
        GameObject bk = CSB("BackButton", canvasGo.transform, bkS); RectTransform bkR = bk.GetComponent<RectTransform>(); bkR.anchorMin = bkR.anchorMax = new Vector2(0, 1); bkR.pivot = new Vector2(0, 1); bkR.anchoredPosition = new Vector2(8, -5); bkR.sizeDelta = new Vector2(40, 40);
        GameObject hm = CSB("HomeButton", canvasGo.transform, hmS); RectTransform hmR = hm.GetComponent<RectTransform>(); hmR.anchorMin = hmR.anchorMax = new Vector2(1, 1); hmR.pivot = new Vector2(1, 1); hmR.anchoredPosition = new Vector2(-8, -5); hmR.sizeDelta = new Vector2(40, 40);
        GameObject st = CSB("SettingButton", canvasGo.transform, stBS); RectTransform stR = st.GetComponent<RectTransform>(); stR.anchorMin = stR.anchorMax = new Vector2(1, 1); stR.pivot = new Vector2(1, 1); stR.anchoredPosition = new Vector2(-52, -5); stR.sizeDelta = new Vector2(40, 40);

        // === PLAYER PANELS ===
        Sprite greenCard = LoadArt("card_green");

        // Player 1 (left half) — 5 boxes
        Button[] p1Btns = new Button[5]; Text[] p1Txts = new Text[5]; Image[] p1Imgs = new Image[5];
        CreatePlayerBoxes("P1", canvasGo.transform, greenCard, true, p1Btns, p1Txts, p1Imgs);

        // Player 2 (right half) — 5 boxes
        Button[] p2Btns = new Button[5]; Text[] p2Txts = new Text[5]; Image[] p2Imgs = new Image[5];
        CreatePlayerBoxes("P2", canvasGo.transform, greenCard, false, p2Btns, p2Txts, p2Imgs);

        // Feedback icons per player
        GameObject p1Ok = FeedbackIconBuilder.Create("P1CorrectIcon", canvasGo.transform, true, new Vector2(0.17f, 0.30f), new Vector2(0.33f, 0.60f));
        GameObject p1Bad = FeedbackIconBuilder.Create("P1WrongIcon", canvasGo.transform, false, new Vector2(0.17f, 0.30f), new Vector2(0.33f, 0.60f));
        GameObject p2Ok = FeedbackIconBuilder.Create("P2CorrectIcon", canvasGo.transform, true, new Vector2(0.67f, 0.30f), new Vector2(0.83f, 0.60f));
        GameObject p2Bad = FeedbackIconBuilder.Create("P2WrongIcon", canvasGo.transform, false, new Vector2(0.67f, 0.30f), new Vector2(0.83f, 0.60f));

        // Countdown texts
        GameObject p1Cd = CT("P1Countdown", canvasGo.transform, "3", 48, TextAnchor.MiddleCenter); SA(p1Cd, 0.15f, 0.35f, 0.35f, 0.55f); p1Cd.GetComponent<Text>().color = new Color(1f, 0.3f, 0.3f); p1Cd.GetComponent<Text>().fontStyle = FontStyle.Bold; p1Cd.SetActive(false);
        GameObject p2Cd = CT("P2Countdown", canvasGo.transform, "3", 48, TextAnchor.MiddleCenter); SA(p2Cd, 0.65f, 0.35f, 0.85f, 0.55f); p2Cd.GetComponent<Text>().color = new Color(1f, 0.3f, 0.3f); p2Cd.GetComponent<Text>().fontStyle = FontStyle.Bold; p2Cd.SetActive(false);

        // Game over panel
        GameObject goP = CI("GameOverPanel", canvasGo.transform, new Color(0, 0, 0, 0.75f)); SA(goP, 0.2f, 0.2f, 0.8f, 0.8f);
        GameObject goT = CT("ResultText", goP.transform, "", 28, TextAnchor.MiddleCenter); SA(goT, 0.08f, 0.45f, 0.92f, 0.88f); goT.GetComponent<Text>().fontStyle = FontStyle.Bold;
        GameObject goR = CreateStyledBtn("RetryButton", goP.transform, "Choi lai", new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.3f), new Color(0.2f, 0.7f, 0.3f));
        goP.SetActive(false);

        // Tutorial
        GameObject tutC = new GameObject("TutorialContainer", typeof(RectTransform)); tutC.transform.SetParent(canvasGo.transform, false); SA(tutC, 0, 0, 1, 1);
        GameObject tutP = CI("TutorialPanel", tutC.transform, new Color(0, 0, 0, 0.7f)); SA(tutP, 0, 0, 1, 1);
        Sprite startSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Textures/MenuScene/btn_start.png");
        GameObject tutS = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button)); tutS.transform.SetParent(tutP.transform, false);
        if (startSpr != null) { tutS.GetComponent<Image>().sprite = startSpr; tutS.GetComponent<Image>().preserveAspect = true; } tutS.GetComponent<Image>().color = Color.white; SA(tutS, 0.35f, 0.14f, 0.65f, 0.26f);
        TutorialPanel tutComp = tutC.AddComponent<TutorialPanel>();
        SerializedObject tpSo = new SerializedObject(tutComp); tpSo.FindProperty("panelRoot").objectReferenceValue = tutP; tpSo.FindProperty("startButton").objectReferenceValue = tutS.GetComponent<Button>(); tpSo.ApplyModifiedPropertiesWithoutUndo();
        tutP.SetActive(false);

        // === WIRE ===
        SerializedObject vSo = new SerializedObject(view);
        vSo.FindProperty("timerText").objectReferenceValue = tmT.GetComponent<Text>();
        vSo.FindProperty("backButton").objectReferenceValue = bk.GetComponent<Button>();
        vSo.FindProperty("homeButton").objectReferenceValue = hm.GetComponent<Button>();
        vSo.FindProperty("settingButton").objectReferenceValue = st.GetComponent<Button>();
        vSo.FindProperty("gameOverPanel").objectReferenceValue = goP;
        vSo.FindProperty("gameOverResultText").objectReferenceValue = goT.GetComponent<Text>();
        vSo.FindProperty("retryButton").objectReferenceValue = goR.GetComponent<Button>();
        vSo.FindProperty("p1BarNameText").objectReferenceValue = p1N.GetComponent<Text>();
        vSo.FindProperty("p2BarNameText").objectReferenceValue = p2N.GetComponent<Text>();
        vSo.FindProperty("p1ScoreBarFill").objectReferenceValue = lbF.GetComponent<Image>();
        vSo.FindProperty("p2ScoreBarFill").objectReferenceValue = rbF.GetComponent<Image>();
        vSo.FindProperty("p1ScoreLabel").objectReferenceValue = lSc.GetComponent<Text>();
        vSo.FindProperty("p2ScoreLabel").objectReferenceValue = rSc.GetComponent<Text>();
        // P1 boxes
        SerializedProperty p1BoxArr = vSo.FindProperty("p1Boxes"); p1BoxArr.arraySize = 5;
        SerializedProperty p1TxtArr = vSo.FindProperty("p1Texts"); p1TxtArr.arraySize = 5;
        SerializedProperty p1ImgArr = vSo.FindProperty("p1Images"); p1ImgArr.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            p1BoxArr.GetArrayElementAtIndex(i).objectReferenceValue = p1Btns[i];
            p1TxtArr.GetArrayElementAtIndex(i).objectReferenceValue = p1Txts[i];
            p1ImgArr.GetArrayElementAtIndex(i).objectReferenceValue = p1Imgs[i];
        }
        // P2 boxes
        SerializedProperty p2BoxArr = vSo.FindProperty("p2Boxes"); p2BoxArr.arraySize = 5;
        SerializedProperty p2TxtArr = vSo.FindProperty("p2Texts"); p2TxtArr.arraySize = 5;
        SerializedProperty p2ImgArr = vSo.FindProperty("p2Images"); p2ImgArr.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            p2BoxArr.GetArrayElementAtIndex(i).objectReferenceValue = p2Btns[i];
            p2TxtArr.GetArrayElementAtIndex(i).objectReferenceValue = p2Txts[i];
            p2ImgArr.GetArrayElementAtIndex(i).objectReferenceValue = p2Imgs[i];
        }
        // Feedback
        vSo.FindProperty("p1CorrectIcon").objectReferenceValue = p1Ok; vSo.FindProperty("p1WrongIcon").objectReferenceValue = p1Bad;
        vSo.FindProperty("p2CorrectIcon").objectReferenceValue = p2Ok; vSo.FindProperty("p2WrongIcon").objectReferenceValue = p2Bad;
        // Countdown
        vSo.FindProperty("p1CountdownText").objectReferenceValue = p1Cd.GetComponent<Text>();
        vSo.FindProperty("p2CountdownText").objectReferenceValue = p2Cd.GetComponent<Text>();
        // Planet sprites (reusing from PlanetOrder)
        SerializedProperty psArr = vSo.FindProperty("planetSprites");
        psArr.arraySize = 10;
        for (int i = 0; i < 10; i++)
        {
            string path = "Assets/Game/Textures/PlanetOrder/planet_" + i + ".png";
            psArr.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        vSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject cSo = new SerializedObject(ctrl);
        cSo.FindProperty("gameView").objectReferenceValue = view;
        cSo.FindProperty("tutorialPanel").objectReferenceValue = tutComp;
        cSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root); EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    private static void CreatePlayerBoxes(string prefix, Transform parent, Sprite greenCard, bool isLeft,
        Button[] btns, Text[] txts, Image[] imgs)
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject box = new GameObject(prefix + "Box" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            box.transform.SetParent(parent, false);
            Image img = box.GetComponent<Image>();
            img.color = Color.white;
            RectTransform boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.2f, 0.3f);
            boxRT.anchorMax = new Vector2(0.35f, 0.6f);
            boxRT.sizeDelta = Vector2.zero;

            // Shadow
            GameObject sh = CT("Shadow", box.transform, "", 64, TextAnchor.MiddleCenter);
            SA(sh, 0, 0, 1, 1); sh.GetComponent<Text>().color = new Color(0.1f, 0.3f, 0.1f, 0.5f);
            sh.GetComponent<Text>().fontStyle = FontStyle.Bold; sh.GetComponent<Text>().raycastTarget = false;
            RectTransform shR = sh.GetComponent<RectTransform>(); shR.offsetMin = new Vector2(2, -2); shR.offsetMax = new Vector2(2, -2);

            // Value
            GameObject val = CT("Value", box.transform, "", 64, TextAnchor.MiddleCenter);
            SA(val, 0.15f, 0.05f, 0.9f, 0.95f); val.GetComponent<Text>().color = new Color(1f, 0.95f, 0.3f);
            val.GetComponent<Text>().fontStyle = FontStyle.Bold;

            btns[i] = box.GetComponent<Button>();
            txts[i] = val.GetComponent<Text>();
            imgs[i] = img;

            box.SetActive(false);
        }
    }

    private static GameObject CSI(string n, Transform p, Sprite s) { GameObject g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(p, false); Image i = g.GetComponent<Image>(); if (s != null) { i.sprite = s; i.preserveAspect = true; } i.color = Color.white; return g; }
    private static GameObject CSB(string n, Transform p, Sprite s) { GameObject g = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(Button)); g.transform.SetParent(p, false); Image i = g.GetComponent<Image>(); if (s != null) { i.sprite = s; i.preserveAspect = true; } i.color = Color.white; return g; }
    private static GameObject CI(string n, Transform p, Color c) { GameObject g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(p, false); g.GetComponent<Image>().color = c; return g; }
    private static GameObject CT(string n, Transform p, string t, int sz, TextAnchor a) { GameObject g = new GameObject(n, typeof(RectTransform), typeof(Text)); g.transform.SetParent(p, false); Text tx = g.GetComponent<Text>(); tx.text = t; tx.fontSize = sz; tx.alignment = a; tx.color = Color.white; Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf"); tx.font = f; return g; }
    private static GameObject CreateStyledBtn(string n, Transform p, string label, Vector2 aMin, Vector2 aMax, Color bg) { GameObject g = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(Button)); g.transform.SetParent(p, false); g.GetComponent<Image>().color = bg; SA(g, aMin.x, aMin.y, aMax.x, aMax.y); GameObject t = CT("Text", g.transform, label, 22, TextAnchor.MiddleCenter); SA(t, 0, 0, 1, 1); return g; }
    private static void SA(GameObject g, float x0, float y0, float x1, float y1) { RectTransform r = g.GetComponent<RectTransform>(); r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
    private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string par = Path.GetDirectoryName(path).Replace("\\", "/"); string nm = Path.GetFileName(path); if (!AssetDatabase.IsValidFolder(par)) EnsureFolder(par); AssetDatabase.CreateFolder(par, nm); }
    private static void AddSceneToBuildSettings(string tp) { var ss = EditorBuildSettings.scenes; foreach (var s in ss) if (s.path == tp) return; var ns = new EditorBuildSettingsScene[ss.Length + 1]; for (int i = 0; i < ss.Length; i++) ns[i] = ss[i]; ns[ss.Length] = new EditorBuildSettingsScene(tp, true); EditorBuildSettings.scenes = ns; }
}
