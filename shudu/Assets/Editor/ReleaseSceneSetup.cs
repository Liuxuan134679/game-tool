using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Shud;
using Object = UnityEngine.Object;

// One-time authoring tool. Builds use the saved scene and never call Create.
public static class ReleaseSceneSetup
{
    const string ScenePath = "Assets/Scenes/Release.unity";
    static Font font;
    static Sprite white, coral, panel, clock;
    static AudioManager audio;
    static readonly Color Ink = new Color32(13,51,96,255), Muted = new Color32(93,111,126,255);
    static readonly Color Blue = new Color32(63,149,220,255), Pale = new Color32(225,239,246,255);
    public static void PrepareAndVerify()
    {
        Create();
        VerifyReuse();
    }
    public static void BuildTeachingTest() { PrepareTeaching(); FirstReleaseBuild.Both(); }
    public static void BuildTargetTimeTest() { VerifyTeaching(); VerifyTargetTime(); VerifyReuse(); FirstReleaseBuild.Both(); }
    [MenuItem("Area Grid/Prepare three-level teaching")]
    public static void PrepareTeaching()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new Exception("Stop Play Mode and save the current scene before adding teaching.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var ui = Object.FindObjectOfType<ReleaseUI>();
        var game = Object.FindObjectOfType<FirstVersionGame>();
        var board = (RectTransform)ui.boardSlot.GetChild(0);
        font = ui.Status.font; white = ui.help.targetGraphic.GetComponent<Image>().sprite;
        audio = Object.FindObjectOfType<AudioManager>();
        if (ui.skipTutorial == null)
        {
            ui.skipTutorial = Button("Skip tutorial", ui.header, "Skip tutorial", false);
            ui.skipTutorial.transform.Find("Label").GetComponent<Text>().fontSize = 14;
        }
        if (ui.tutorialHand == null)
        {
            ui.tutorialHand = Group("Tutorial hand", board);
            ui.tutorialHand.anchorMin = ui.tutorialHand.anchorMax = new Vector2(.5f,.5f);
        }
        // Replace only the old hand shapes; keep the authored object and reference.
        foreach (Transform child in ui.tutorialHand.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        AssetDatabase.Refresh();
        var handSprite = Art("游戏ui/TutorialHand.png", new Rect(0,0,460,542), Vector4.zero, 1024);
        var handImage = ui.tutorialHand.GetComponent<Image>() ?? ui.tutorialHand.gameObject.AddComponent<Image>();
        handImage.sprite = handSprite; handImage.type = Image.Type.Simple;
        handImage.color = Color.white; handImage.preserveAspect = true; handImage.raycastTarget = false;
        ui.tutorialHand.pivot = new Vector2(187f/460,1-109f/542);
        ui.tutorialHand.sizeDelta = new Vector2(64,64f*542/460);
        ui.skipTutorial.gameObject.SetActive(false); ui.tutorialHand.gameObject.SetActive(false);
        Place(ui.skipTutorial.transform,285,-12,118,30);
        Validate();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save teaching objects.");
        VerifyTeaching();
        VerifyReuse();
    }
    [MenuItem("Area Grid/Verify three-level teaching")]
    public static void VerifyTeaching()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Validate();
        var game = Object.FindObjectOfType<FirstVersionGame>();
        var ui = Object.FindObjectOfType<ReleaseUI>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Action<string,object[]> call=(name,args)=>typeof(FirstVersionGame).GetMethod(name,flags).Invoke(game,args);
        Func<string,object> get=name=>typeof(FirstVersionGame).GetField(name,flags).GetValue(game);
        Action<string,object> set=(name,value)=>typeof(FirstVersionGame).GetField(name,flags).SetValue(game,value);
        Action<bool,string> require=(condition,message)=>{ if (!condition) throw new Exception(message); };
        set("restoring",true);
        call("BindSceneReferences",new object[0]); call("ConfigureRelease",new object[0]);
        call("LoadLevel",new object[]{0});
        float seconds=(float)get("remainingSeconds");
        call("Update",new object[0]); call("FreezeTimer",new object[0]);
        require((float)get("remainingSeconds")==seconds && !(bool)get("freezeUsed") && ui.Timer.text=="Tutorial", "Teaching consumed timer or freeze.");
        require(ui.skipTutorial.gameObject.activeSelf && !ui.Help.activeSelf, "Teaching unexpectedly opened a modal.");
        require(((System.Collections.Generic.List<PlayerRegion>)get("regions")).Count==0 && ui.progress.text=="0%", "Demo placed a rectangle.");
        call("PauseGame",new object[0]); call("TickTeaching",new object[0]);
        require(!ui.tutorialHand.gameObject.activeSelf && (bool)get("paused"), "Pause did not hide the hand.");
        call("ResumeGame",new object[0]); call("TickTeaching",new object[0]);
        require(ui.tutorialHand.gameObject.activeSelf, "Resume did not restore demonstration.");
        var cells=(GridCellView[,])get("cellByPosition");
        set("tutorialDemoTime",.25f); call("TickTeaching",new object[0]);
        require(Mathf.Approximately(cells[0,0].ClueLabel.transform.localScale.x,1.2f), "Teaching number did not reach 1.2 scale.");
        var preview=(SelectionPreviewView)get("selectionPreview");
        preview.Show(new RectInt(0,0,3,2),cells,new Color(1,1,1,.25f),Color.clear,"");
        var previewLabels=new SerializedObject(preview).FindProperty("previewClues");
        var previewLabel=(Text)previewLabels.GetArrayElementAtIndex(0).objectReferenceValue;
        require(Mathf.Approximately(previewLabel.transform.localScale.x,1.2f), "Preview lost teaching number emphasis.");
        set("tutorialDemoTime",.75f); call("TickTeaching",new object[0]);
        require(Mathf.Approximately(cells[0,0].ClueLabel.transform.localScale.x,.9f), "Teaching number did not reach .9 scale.");
        call("PointerDown",new object[]{cells[0,0],7});
        require(cells[0,0].ClueLabel.transform.localScale==Vector3.one && previewLabel.transform.localScale==Vector3.one, "Teaching scale leaked into player input.");
        require(!ui.tutorialHand.gameObject.activeSelf && ui.Status.text=="1 / 6 cells\nInclude both numbers.", "Input failed to take over the demo.");
        set("selectionEnd",cells[1,2]); call("PreviewSelection",new object[0]);
        require(ui.Status.text=="6 / 6 - Release!" && ui.Status.color.g > ui.Status.color.r, "Correct area feedback is missing.");
        call("ClearSelection",new object[0]); call("SkipTutorial",new object[0]);
        require(!(bool)get("teaching") && (bool)get("tutorialSeen") && (float)get("remainingSeconds")==60 && !ui.skipTutorial.gameObject.activeSelf, "Skip state is wrong.");
        set("tutorialSeen",false); call("StartTeaching",new object[0]); call("CompleteTeaching",new object[0]);
        require(!(bool)get("teaching") && !(bool)get("tutorialSeen") && (bool)get("tutorialSucceeded"), "First teaching step ended all teaching.");
        call("LoadLevel",new object[]{1});
        require((bool)get("teaching") && ui.Timer.text=="Tutorial" && ui.Status.text.Contains("3 + 5 = 8 cells"), "Second-level teaching is missing.");
        var level=(LevelData)get("level");
        var regions=(System.Collections.Generic.List<PlayerRegion>)get("regions");
        regions.Add(new PlayerRegion{rect=level.clues[0].SolutionRect});
        require((bool)get("teaching") && !(bool)get("tutorialSeen"), "One rectangle ended a multi-pair teaching level.");
        var next=(ClueData)typeof(FirstVersionGame).GetMethod("TeachingClue",flags).Invoke(game,null);
        require(next!=null && !next.SolutionRect.Equals(regions[0].rect),"Demo repeats an already placed rectangle.");
        call("LoadLevel",new object[]{2});
        require((bool)get("teaching") && ui.Status.text.Contains("5 + 3 = 8 cells"),"Third-level teaching is missing.");
        call("CompleteTeaching",new object[0]);
        require(!(bool)get("teaching") && (bool)get("tutorialSeen"),"Third-level completion did not finish teaching.");
        call("LoadLevel",new object[]{3});
        require(!(bool)get("teaching") && ui.Timer.text=="01:30", "Fourth-level target timer is missing.");
        call("LoadLevel",new object[]{1});
        require(!(bool)get("teaching"), "Completed/skipped teaching restarted.");
        string feedback=(string)typeof(FirstVersionGame).GetMethod("PairFeedback",flags).Invoke(game,new object[]{new RectInt(0,0,1,1),false});
        require(feedback=="Each rectangle needs exactly two numbers.", "Normal clue-count feedback is missing.");
        var first=ReleasePuzzleBank.TryGet(0,out var firstLevel) ? firstLevel : null;
        var old=new ReleaseSave{levelIndex=12,completedLevels=12,puzzle=ReleaseProgress.Fingerprint(first),seconds=31,tutorialSeen=true};
        require(ReleaseProgress.TryParse(JsonUtility.ToJson(old),out var parsed) && parsed.levelIndex==12 && parsed.tutorialSeen && parsed.seconds==31,"Old save format was not preserved.");
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../onboarding-tests.txt")),
            "PASS: teaching numbers pulse between .9 and 1.2 scale; preview follows emphasis; input returns to normal scale; first three teaching levels; fourth-level normal timer; multi-pair demonstration advances; teaching completion/skip flags; demo never places regions; timer/freeze protected; direct input; correct area feedback; pause/resume; old save schema; authored hand sprite.\n");
        EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("Area Grid/Verify target time")]
    public static void VerifyTargetTime()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var game = Object.FindObjectOfType<FirstVersionGame>();
        var ui = Object.FindObjectOfType<ReleaseUI>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Action<string,object[]> call = (name,args) => typeof(FirstVersionGame).GetMethod(name,flags).Invoke(game,args);
        Func<string,object> get = name => typeof(FirstVersionGame).GetField(name,flags).GetValue(game);
        Action<string,object> set = (name,value) => typeof(FirstVersionGame).GetField(name,flags).SetValue(game,value);
        Action<bool,string> require = (condition,message) => { if (!condition) throw new Exception(message); };
        set("restoring",true); set("tutorialSeen",true);
        call("BindSceneReferences",new object[0]); call("ConfigureRelease",new object[0]);
        for (int index = 0; index <= LevelBoardPlan.LastLevelIndex; index++)
        {
            call("LoadLevel",new object[]{index});
            require((float)get("remainingSeconds") == LevelBoardPlan.GetTargetSeconds(index), "Wrong target at " + (index + 1));
            if (index < 3)
            {
                call("Update",new object[0]); call("FreezeTimer",new object[0]);
                require(ui.Timer.text == "Tutorial" && (float)get("remainingSeconds") == 60 && !ui.Freeze.interactable,
                    "Skipped/completed teaching became timed.");
            }
        }
        call("LoadLevel",new object[]{3});
        call("FreezeTimer",new object[0]); call("Update",new object[0]);
        require((float)get("remainingSeconds") == 90 && (bool)get("freezeUsed"), "Freeze consumed target time.");
        call("PauseGame",new object[0]); float frozen = (float)get("freezeRemainingSeconds");
        call("Update",new object[0]);
        require((float)get("freezeRemainingSeconds") == frozen, "Paused freeze kept counting.");
        call("ResumeGame",new object[0]); call("ReachTargetTime",new object[0]); call("FreezeTimer",new object[0]);
        require(!(bool)get("locked") && !ui.Failure.activeSelf && !ui.Complete.activeSelf && ui.Timer.text == "Overtime"
            && !ui.Freeze.interactable && ui.Hint.interactable && ui.Restart.interactable, "Overtime blocked play.");
        require(ui.Timer.fontSize == 14 && ui.Timer.preferredWidth <= ui.Timer.rectTransform.rect.width - 8,
            "Overtime text does not fit the existing timer.");
        call("RestartLevel",new object[0]);
        require((float)get("remainingSeconds") == 90 && ui.Timer.text == "01:30" && ui.Freeze.interactable, "Restart did not restore target.");
        foreach (float seconds in new[] { 0f, 60f, 90f, 120f, 150f, 180f })
        {
            var saved = new ReleaseSave { levelIndex=31, puzzle=ReleaseProgress.Fingerprint((LevelData)get("level")), seconds=seconds };
            require(ReleaseProgress.TryParse(JsonUtility.ToJson(saved),out var parsed) && parsed.seconds == seconds, "Valid old/new timer save rejected.");
        }
        foreach (float seconds in new[] { -1f, 181f, float.NaN, float.PositiveInfinity })
        {
            var saved = new ReleaseSave { puzzle="pairs", seconds=seconds };
            require(!ReleaseProgress.TryParse(JsonUtility.ToJson(saved),out _), "Invalid timer save accepted.");
        }
        ui.SetCompletion(false,.01f); require(ui.completeTime.text == "Quick finish", "Positive remainder lost its evaluation.");
        ui.SetCompletion(false,0f); require(ui.completeTime.text == "Puzzle complete", "Overtime evaluation is wrong.");
        ui.SetCompletion(true,0f); require(ui.completeTitle.text == "All 32 puzzles complete!", "Last-level title changed.");
        File.AppendAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../onboarding-tests.txt")),
            "PASS: all 32 target times; skipped teaching untimed; freeze/pause; overtime keeps play unlocked; restart target; old/new save bounds and nonfinite rejection; positive/zero-time evaluations; final-level title.\n");
        EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("Area Grid/Apply capsule progress style")]
    public static void ApplyProgressStyle()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new Exception("Stop Play Mode and save your scene before updating progress style.");
        EditorSceneManager.OpenScene(ScenePath);
        Validate();
        var ui = Object.FindObjectOfType<ReleaseUI>();
        var capsule = Art("游戏ui/ProgressCapsule.png", new Rect(0,0,64,32), new Vector4(16,16,16,16), 64);
        foreach (var image in new[] { ui.progressTrack.GetComponent<Image>(), ui.progressFill })
        {
            image.sprite = capsule; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 8f/3; image.raycastTarget = false; // 16 source pixels become a 6-unit radius.
        }
        ui.progressTrack.GetComponent<Image>().color = new Color32(217,230,239,255);
        ui.progressFill.color = Blue;
        Stretch(ui.progressFill.rectTransform);
        ui.progress.alignment = TextAnchor.MiddleRight;
        ui.progress.color = Muted;
        VerifyProgress(ui);
        ui.SetProgress(0);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) throw new Exception("Cannot save progress style.");
    }
    [MenuItem("Area Grid/Verify capsule progress")]
    public static void VerifyProgressStyle()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new Exception("Stop Play Mode and save your scene before checking progress style.");
        EditorSceneManager.OpenScene(ScenePath);
        VerifyProgress(Object.FindObjectOfType<ReleaseUI>());
        EditorSceneManager.OpenScene(ScenePath);
    }
    static void VerifyProgress(ReleaseUI ui)
    {
        foreach (float value in new[] { -1f, 0f, 2f/36, .39f, 1f, 2f })
        {
            ui.SetProgress(value);
            Canvas.ForceUpdateCanvases();
            float amount = Mathf.Clamp01(value);
            if (ui.progress.text != Mathf.RoundToInt(amount * 100) + "%"
                || ui.progressFill.enabled != (amount > 0)
                || ui.progressFill.type != Image.Type.Sliced
                || Mathf.Abs(ui.progressFill.rectTransform.rect.width - ui.progressTrack.rect.width * amount) > .01f
                || Mathf.Abs(ui.progressFill.rectTransform.rect.height - 12) > .01f
                || Mathf.Abs(ui.progressFill.sprite.border.x / ui.progressFill.pixelsPerUnitMultiplier - 6) > .01f
                || ui.progressFill.sprite != ui.progressTrack.GetComponent<Image>().sprite)
                throw new Exception("Invalid capsule progress at " + value);
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../progress-ui-tests.txt")),
            "PASS: clamped empty/minimum/39%/full progress; percentage text, fill visibility, proportional width, 12-unit height, 6-unit corner radius, shared sliced sprite.\n");
    }
    [MenuItem("Area Grid/Create authored UI (one time)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode first.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Save your current scene before UI conversion.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (Object.FindObjectOfType<ReleaseUI>() != null) throw new Exception("Authored UI already exists; edit it in the hierarchy.");
        var game = Object.FindObjectOfType<FirstVersionGame>();
        var board = GameObject.Find("Board").GetComponent<RectTransform>();
        var canvas = board.GetComponentInParent<Canvas>();
        audio = Object.FindObjectOfType<AudioManager>();
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/AreaGridLatin.ttf");
        white = Art("游戏ui/8.png", new Rect(24,28,891,623), new Vector4(205,205,205,205), 512);
        coral = Art("主界面素材/开始背景.png", new Rect(9,18,522,191), new Vector4(65,65,65,65), 512);
        panel = Art("面板/面板.png", new Rect(52,49,784,1136), new Vector4(120,120,120,120), 1024);
        clock = Art("游戏ui/时间.png", new Rect(61,46,262,253), Vector4.zero, 256);
        var logoSprite = Art("游戏ui/image.png", new Rect(37,34,1438,165), Vector4.zero, 1024);
        var hintSprite = Art("游戏ui/8e.png", new Rect(9,3,146,207), Vector4.zero, 256);
        var restartSprite = Art("游戏ui/重置.png", new Rect(91,86,182,187), Vector4.zero, 256);
        var freezeSprite = Art("游戏ui/冻结5.png", new Rect(75,13,728,778), Vector4.zero, 512);
        var starSprite = Art("面板/next5.png", new Rect(33,47,989,760), Vector4.zero, 512);
        var soundSprite = Art("面板素材/音量.png", new Rect(0,0,493,492), Vector4.zero, 256);
        var arrowSprite = Art("主界面素材/开始按钮.png", new Rect(0,0,132,142), Vector4.zero, 128);
        var root = (RectTransform)canvas.transform;
        var scaler = canvas.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
        var ui = canvas.gameObject.AddComponent<ReleaseUI>();
        ui.canvas = canvas; ui.root = root;
        var bg = Surface("Background", root, null, new Color32(246,247,239,255)); Stretch(bg); bg.SetAsFirstSibling();
        ui.header = Group("Header", root);
        ui.logo = Icon("Area Grid", ui.header, logoSprite);
        ui.subtitle = Label("Subtitle", ui.header, "PAIR & FILL", 12, Muted, TextAnchor.MiddleCenter);
        ui.pause = Button("Pause", ui.header, "", false);
        for (int i=0;i<2;i++) { var bar=Surface("Pause bar " + (i+1),ui.pause.transform,null,Ink); Place(bar,17+i*9,-13,5,20); }
        ui.help = Button("How to play", ui.header, "How to play", false);
        InsetLabel(ui.help,36,5);
        var question = Label("Question icon",ui.help.transform,"?",25,Blue,TextAnchor.MiddleCenter); Place(question.transform,8,-4,24,30);
        ui.sound = Button("Sound",ui.header,"Sound on",false); InsetLabel(ui.sound,39,5);
        var speaker = Icon("Speaker",ui.sound.transform,soundSprite); Place(speaker,9,-7,25,25);
        var slash = Label("Muted slash",speaker,"/",28,Ink,TextAnchor.MiddleCenter); Stretch(slash.rectTransform); ui.soundOff=slash.gameObject; ui.soundOff.SetActive(false);
        ui.details = Group("Details",root);
        ui.Level = Label("Level",ui.details,"Level 1 / 32",23,Ink,TextAnchor.MiddleLeft);
        ui.timerPill = Surface("Time pill",ui.details,white,Pale);
        var timeIcon=Icon("Clock",ui.timerPill,clock); Place(timeIcon,10,-8,22,22);
        ui.Timer=Label("Time",ui.timerPill,"01:00",25,Ink,TextAnchor.MiddleCenter); Stretch(ui.Timer.rectTransform); ui.Timer.rectTransform.offsetMin=new Vector2(32,0);
        ui.progressTrack=Surface("Progress track",ui.details,white,new Color32(217,227,231,255));
        var progressRect=Surface("Fill",ui.progressTrack,white,Blue); Stretch(progressRect);
        ui.progressFill=progressRect.GetComponent<Image>(); ui.progressFill.type=Image.Type.Filled; ui.progressFill.fillMethod=Image.FillMethod.Horizontal; ui.progressFill.fillAmount=0;
        ui.progress=Label("Progress label",ui.details,"0% filled",13,Muted,TextAnchor.MiddleRight);
        ui.rule=Label("Rule",ui.details,"Two numbers. One rectangle.\nTheir sum must equal its area.",17,Muted,TextAnchor.UpperLeft);
        ui.boardSlot=Group("Board space",root); board.SetParent(ui.boardSlot,false);
        ui.tools=Group("Tools",root);
        ui.Hint=Tool("Hint",ui.tools,"Hint",hintSprite);
        ui.Restart=Tool("Restart",ui.tools,"Restart",restartSprite);
        ui.Freeze=Tool("Freeze",ui.tools,"Freeze (1)",freezeSprite);
        ui.Status=Label("Status",root,"Two numbers. One rectangle.\nTheir sum must equal its area.",15,Muted,TextAnchor.MiddleCenter);
        var dialogs=Group("Dialogs",root); Stretch(dialogs);
        ui.Help=Dialog("Help",dialogs,"How to play","Each rectangle needs TWO numbers. Their SUM must equal its area.\n\nExample: 2 + 4 = 6 cells (2 x 3).\n\nAim for the target time, or keep solving. Tap a rectangle to undo.\n\nFreeze gives one 10-second pause.",out ui.helpCard,out _,out _);
        ui.tutorialDone=Button("Start",ui.helpCard,"Let's play",true); Place(ui.tutorialDone.transform,24,-346,302,56);
        ui.Pause=Dialog("Pause",dialogs,"Take a break","Your puzzle is saved on this browser.\n\nThe timer is paused.",out ui.pauseCard,out _,out _);
        ui.resume=Button("Resume",ui.pauseCard,"Continue puzzle",true); Place(ui.resume.transform,24,-346,302,56);
        ui.ConfirmRestart=Dialog("Restart confirmation",dialogs,"Restart this puzzle?","Your rectangles will be cleared.\nThe timer and freeze will reset.",out ui.restartCard,out _,out _);
        ui.confirm=Button("Confirm",ui.restartCard,"Restart puzzle",true); Place(ui.confirm.transform,24,-278,302,56);
        ui.cancel=Button("Cancel",ui.restartCard,"Keep playing",false); Place(ui.cancel.transform,24,-346,302,56);
        ui.Complete=Dialog("Complete",dialogs,"Level complete!","A perfect fit.",out ui.completeCard,out ui.completeTitle,out ui.completeBody);
        ui.completeFade=ui.Complete.AddComponent<CanvasGroup>();
        var star=Icon("Star and confetti",ui.completeCard,starSprite); Place(star,62,-12,226,174);
        Place(ui.completeTitle.transform,20,-181,310,55); ui.completeTitle.fontSize=28;
        Place(ui.completeBody.transform,24,-236,302,26); ui.completeBody.alignment=TextAnchor.MiddleCenter;
        var timePill=Surface("Time remaining",ui.completeCard,white,Pale); Place(timePill,62,-277,226,42);
        var completeClock=Icon("Clock",timePill,clock); Place(completeClock,12,-10,22,22);
        ui.completeTime=Label("Time remaining label",timePill,"42 seconds to spare",16,Ink,TextAnchor.MiddleCenter); Stretch(ui.completeTime.rectTransform); ui.completeTime.rectTransform.offsetMin=new Vector2(33,0);
        ui.Next=Button("Next",ui.completeCard,"Next puzzle",true); Place(ui.Next.transform,24,-346,302,56); InsetLabel(ui.Next,0,40);
        var arrow=Icon("Next arrow",ui.Next.transform,arrowSprite); Place(arrow,255,-17,22,22);
        ui.Failure=Dialog("Failure",dialogs,"Time's up","Give this puzzle another try.\n\nLook for two numbers whose sum fits the space around them.",out ui.failureCard,out _,out _);
        ui.Retry=Button("Retry",ui.failureCard,"Try again",true); Place(ui.Retry.transform,24,-346,302,56);
        AuthorBoard(game,board);
        var so=new SerializedObject(game);
        Set(so,"releaseUI",ui); Set(so,"statusText",ui.Status); Set(so,"timerText",ui.Timer); Set(so,"levelText",ui.Level);
        Set(so,"hintButton",ui.Hint); Set(so,"resetButton",ui.Restart); Set(so,"freezeButton",ui.Freeze);
        Set(so,"failurePanel",ui.Failure); Set(so,"failureRestartButton",ui.Retry); Set(so,"completePanel",ui.Complete); Set(so,"nextLevelButton",ui.Next);
        Set(so,"stateBanner",null); so.FindProperty("forcePortrait").boolValue=false; so.ApplyModifiedPropertiesWithoutUndo();
        ui.Tick(true);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Validate();
        ApplyProgressStyle();
        Debug.Log("Authored Area Grid UI saved: 36 cells, 8 rectangles, 16 preview clues, 16 merging clues.");
    }

    static void AuthorBoard(FirstVersionGame game, RectTransform board)
    {
        var old=board.GetComponentsInChildren<GridCellView>(true);
        var template=old[0];
        foreach(var cell in old.Skip(1)) Object.DestroyImmediate(cell.gameObject);
        var pool=new System.Collections.Generic.List<GridCellView>{template};
        while(pool.Count<36) pool.Add(Object.Instantiate(template,board));
        for(int i=0;i<pool.Count;i++)
        {
            var cell=pool[i]; cell.name="Cell " + (i+1).ToString("00"); cell.Configure(game,i/6,i%6);
            var r=(RectTransform)cell.transform; r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            r.sizeDelta=new Vector2(52,52); r.anchoredPosition=new Vector2(-135+(i%6)*54,135-(i/6)*54);
            cell.ClueLabel.font=font; cell.ClueLabel.raycastTarget=false; cell.SetReleaseFontSize(21); cell.SetText(""); cell.gameObject.SetActive(true);
        }
        var region=board.GetComponentInChildren<PlacedRegionView>(true);
        var number=region.GetComponentInChildren<Text>(true); number.font=font; number.raycastTarget=false;
        var merging=new Text[2];
        for(int i=0;i<2;i++) { merging[i]=Object.Instantiate(number,number.transform.parent); merging[i].name="Merging clue " + (i+1); merging[i].gameObject.SetActive(false); }
        var rs=new SerializedObject(region); Array(rs,"clueLabels",merging); rs.ApplyModifiedPropertiesWithoutUndo();
        var regions=new PlacedRegionView[8]; regions[0]=region;
        for(int i=1;i<regions.Length;i++) regions[i]=Object.Instantiate(region,region.transform.parent);
        for(int i=0;i<regions.Length;i++) { regions[i].name="Rectangle " + (i+1).ToString("00"); regions[i].Initialize(); regions[i].Hide(); }
        var preview=board.GetComponentInChildren<SelectionPreviewView>(true);
        var clues=new Text[16];
        for(int i=0;i<clues.Length;i++) { clues[i]=Object.Instantiate(pool[0].ClueLabel,preview.transform); clues[i].name="Preview clue " + (i+1).ToString("00"); clues[i].gameObject.SetActive(false); }
        var ps=new SerializedObject(preview); Array(ps,"previewClues",clues); ps.ApplyModifiedPropertiesWithoutUndo(); preview.Initialize();
        var so=new SerializedObject(game); Array(so,"cellPool",pool.ToArray()); Array(so,"regionPool",regions); Array(so,"cells",pool.ToArray());
        Set(so,"selectionPreview",preview); Set(so,"placedRegionsRoot",region.transform.parent); so.ApplyModifiedPropertiesWithoutUndo();
        region.transform.parent.SetAsLastSibling(); preview.transform.SetAsLastSibling();
    }

    public static void Validate()
    {
        var game=Object.FindObjectOfType<FirstVersionGame>();
        var ui=Object.FindObjectOfType<ReleaseUI>();
        if(game==null || ui==null) throw new Exception("Open the authored Release scene first.");
        foreach(var component in new Component[]{game,ui})
        {
            var so=new SerializedObject(component); var p=so.GetIterator();
            while(p.NextVisible(true))
                if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue==null && p.name!="stateBanner" && p.name!="levelConfig")
                    throw new Exception(component.GetType().Name + ": Missing reference " + p.propertyPath);
        }
        var data=new SerializedObject(game);
        if(data.FindProperty("cellPool").arraySize!=36 || data.FindProperty("regionPool").arraySize!=8) throw new Exception("Invalid pool size.");
        foreach(var view in game.gameObject.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<PlacedRegionView>(true)))
        { var labels=new SerializedObject(view).FindProperty("clueLabels"); if(labels.arraySize!=2 || labels.GetArrayElementAtIndex(0).objectReferenceValue==null || labels.GetArrayElementAtIndex(1).objectReferenceValue==null) throw new Exception("Missing merging labels."); }
        var preview=new SerializedObject((SelectionPreviewView)data.FindProperty("selectionPreview").objectReferenceValue).FindProperty("previewClues");
        if(preview.arraySize!=16) throw new Exception("Expected 16 authored preview clues.");
        for(int i=0;i<preview.arraySize;i++)
            if(preview.GetArrayElementAtIndex(i).objectReferenceValue==null) throw new Exception("Missing preview clue "+i);
        for(int i=0;i<=LevelBoardPlan.LastLevelIndex;i++)
            if(!ReleasePuzzleBank.TryGet(i,out var level) || level.rows>6 || level.columns>6 || level.clues.Count>16) throw new Exception("Puzzle exceeds authored capacity: " + i);
    }
    [MenuItem("Area Grid/Verify authored scene reuse")]
    public static void VerifyReuse()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new Exception("Stop Play Mode and save your scene before checking reuse.");
        EditorSceneManager.OpenScene(ScenePath);
        Validate();
        var scene=EditorSceneManager.GetActiveScene();
        var game=Object.FindObjectOfType<FirstVersionGame>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Action<string,object[]> call=(name,args)=>typeof(FirstVersionGame).GetMethod(name,flags).Invoke(game,args);
        Func<string,object> get=name=>typeof(FirstVersionGame).GetField(name,flags).GetValue(game);
        typeof(FirstVersionGame).GetField("restoring",flags).SetValue(game,true);
        call("BindSceneReferences",new object[0]); call("ConfigureRelease",new object[0]);
        Func<int[]> ids=()=>scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject.GetInstanceID()).OrderBy(i=>i).ToArray();
        var before=ids();
        int placements=0;
        for(int index=0;index<=LevelBoardPlan.LastLevelIndex;index++)
        {
            call("LoadLevel",new object[]{index});
            var level=(LevelData)get("level");
            var cells=(GridCellView[,])get("cellByPosition");
            var preview=(SelectionPreviewView)get("selectionPreview");
            preview.Show(new RectInt(0,0,level.columns,level.rows),cells,new Color(1,1,1,.5f),Color.clear,""); preview.Hide();
            var regions=(System.Collections.Generic.List<PlayerRegion>)get("regions");
            var views=(System.Collections.Generic.List<PlacedRegionView>)get("regionViews");
            foreach(var entry in level.clues.Select((clue,i)=>new{clue,i}).GroupBy(x=>x.clue.SolutionRect).Select(g=>g.First()))
            {
                regions.Add(new PlayerRegion{clueId=entry.i,colorIndex=regions.Count,rect=entry.clue.SolutionRect});
                var view=(PlacedRegionView)typeof(FirstVersionGame).GetMethod("CreatePlacedRegionView",flags).Invoke(game,null);
                views.Add(view); placements++;
            }
            call("RebuildBoardVisuals",new object[0]);
            if(!RegionValidator.ValidateFinal(level,regions)) throw new Exception("Completed scene puzzle invalid: "+index);
            var removed=views[0]; call("DestroyRegionView",new object[]{0}); regions.RemoveAt(0);
            var reused=(PlacedRegionView)typeof(FirstVersionGame).GetMethod("CreatePlacedRegionView",flags).Invoke(game,null);
            if(reused!=removed) throw new Exception("Rectangle was not reused.");
            reused.Hide();
            if(!before.SequenceEqual(ids())) throw new Exception("Scene GameObjects changed at level "+index);
        }
        call("LoadLevel",new object[]{0});
        if(!before.SequenceEqual(ids())) throw new Exception("Reset changed scene objects.");
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../scene-reuse-tests.txt")),
            "PASS: 32 levels; "+placements+" placed rectangles; full-board clue previews; undo/reuse/reset; "+before.Length+" identical GameObject IDs. No runtime creation or destruction.\n");
        // Discard test-only scene state; keep the authored hierarchy exactly as saved.
        EditorSceneManager.OpenScene(ScenePath);
    }

    static Sprite Art(string name,Rect rect,Vector4 border,int maxSize)
    {
        string path="Assets/art/"+name;
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100; importer.mipmapEnabled=false; importer.isReadable=false; importer.maxTextureSize=maxSize;
        importer.spritesheet=new[]{new SpriteMetaData{name=Path.GetFileNameWithoutExtension(name),rect=rect,pivot=new Vector2(.5f,.5f),alignment=(int)SpriteAlignment.Center,border=border}};
        var web=importer.GetPlatformTextureSettings("WebGL"); web.overridden=true; web.maxTextureSize=maxSize; web.format=TextureImporterFormat.RGBA32; importer.SetPlatformTextureSettings(web);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    }
    static RectTransform Group(string name,Transform parent)
    { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); return r; }
    static RectTransform Surface(string name,Transform parent,Sprite sprite,Color color)
    { var r=Group(name,parent); var im=r.gameObject.AddComponent<Image>(); im.sprite=sprite; im.color=color; im.type=sprite==null?Image.Type.Simple:Image.Type.Sliced; im.pixelsPerUnitMultiplier=6; im.raycastTarget=false; return r; }
    static RectTransform Icon(string name,Transform parent,Sprite sprite)
    { var r=Surface(name,parent,sprite,Color.white); var im=r.GetComponent<Image>(); im.type=Image.Type.Simple; im.preserveAspect=true; return r; }
    static Text Label(string name,Transform parent,string value,int size,Color color,TextAnchor alignment)
    { var t=Group(name,parent).gameObject.AddComponent<Text>(); t.font=font; t.text=value; t.fontSize=size; t.color=color; t.alignment=alignment; t.raycastTarget=false; t.horizontalOverflow=HorizontalWrapMode.Wrap; return t; }
    static Button Button(string name,Transform parent,string label,bool primary)
    {
        var r=Surface(name,parent,primary?coral:white,Color.white); var image=r.GetComponent<Image>(); image.raycastTarget=true;
        var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=image;
        var colors=b.colors; colors.highlightedColor=new Color(.96f,.98f,1); colors.pressedColor=new Color(.80f,.88f,.94f); colors.disabledColor=new Color(.76f,.80f,.81f,.65f); b.colors=colors;
        var t=Label("Label",r,label,primary?21:16,primary?Color.white:Ink,TextAnchor.MiddleCenter); Stretch(t.rectTransform,4);
        if(audio!=null) UnityEventTools.AddPersistentListener(b.onClick,audio.PlayClickSound);
        return b;
    }
    static Button Tool(string name,Transform parent,string label,Sprite icon)
    { var b=Button(name,parent,label,false); var t=b.transform.Find("Label").GetComponent<RectTransform>(); t.anchorMin=new Vector2(0,0); t.anchorMax=new Vector2(1,0); t.pivot=new Vector2(.5f,0); t.anchoredPosition=new Vector2(0,6); t.sizeDelta=new Vector2(-4,24); var r=Icon("Icon",b.transform,icon); r.anchorMin=r.anchorMax=new Vector2(.5f,1); r.pivot=new Vector2(.5f,1); r.anchoredPosition=new Vector2(0,-8); r.sizeDelta=new Vector2(34,34); return b; }
    static void InsetLabel(Button button,float left,float right)
    { var r=button.transform.Find("Label").GetComponent<RectTransform>(); Stretch(r,4); r.offsetMin=new Vector2(left,4); r.offsetMax=new Vector2(-right,-4); }
    static GameObject Dialog(string name,Transform parent,string title,string body,out RectTransform card,out Text titleText,out Text bodyText)
    {
        var overlay=Surface(name,parent,null,new Color(.06f,.13f,.20f,.48f)); Stretch(overlay); overlay.GetComponent<Image>().raycastTarget=true;
        card=Surface("Card",overlay,panel,Color.white); card.sizeDelta=new Vector2(350,424);
        titleText=Label("Title",card,title,27,Ink,TextAnchor.MiddleCenter); Place(titleText.transform,24,-28,302,66);
        bodyText=Label("Body",card,body,17,Muted,TextAnchor.UpperLeft); Place(bodyText.transform,24,-112,302,228);
        overlay.gameObject.SetActive(false); return overlay.gameObject;
    }
    static void Stretch(RectTransform r,float padding=0)
    { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.one*padding; r.offsetMax=Vector2.one*-padding; }
    static void Place(Transform t,float x,float y,float w,float h)
    { var r=(RectTransform)t; r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(w,h); }
    static void Set(SerializedObject so,string name,Object value) { so.FindProperty(name).objectReferenceValue=value; }
    static void Array<T>(SerializedObject so,string name,T[] values) where T:Object
    { var p=so.FindProperty(name); p.arraySize=values.Length; for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; }
}
