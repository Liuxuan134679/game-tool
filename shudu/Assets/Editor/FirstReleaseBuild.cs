using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;
using Shud;

public static class FirstReleaseBuild
{
    private static string Root => Path.GetFullPath(Environment.GetEnvironmentVariable("AREA_GRID_RELEASE_ROOT")
        ?? Path.Combine(Application.dataPath, "../Builds/FirstRelease"));
    [MenuItem("Area Grid/Build WebGL release")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Production();
    }
    public static void Development() => Build(true);
    public static void Production() => Build(false);
    public static void Both() { Build(true); Build(false); }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Build(bool development)
    {
        Directory.CreateDirectory(Root);
        BakeAndVerify();
        PrepareScene();
        PlayerSettings.companyName = "Area Grid";
        PlayerSettings.productName = "Area Grid Pairs Prototype";
        PlayerSettings.bundleVersion = "0.3.1";
        PlayerSettings.WebGL.template = "PROJECT:AreaGrid";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.initialMemorySize = 32;
        PlayerSettings.runInBackground = false;
        PlayerSettings.stripEngineCode = true;
        UnityEditor.Analytics.AnalyticsSettings.enabled = false;
        UnityEditor.Analytics.AnalyticsSettings.initializeOnStartup = false;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Release.unity", true) };
        AssetDatabase.SaveAssets();
        var destination = Path.Combine(Root, development ? "webgl-qa" : "webgl-release");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Release.unity" }, locationPathName=destination, target=BuildTarget.WebGL,
            options=development ? BuildOptions.Development : BuildOptions.None
        });
        var messages = report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.content);
        File.WriteAllText(Path.Combine(Root, development ? "qa-build.txt" : "release-build.txt"),
            $"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}\n"+string.Join("\n",messages));
        Require(report.summary.result==BuildResult.Succeeded,"Build failed");
    }
    private static void BakeAndVerify()
    {
        Directory.CreateDirectory("Assets/Resources");
        BakeClickPcm();
        var csv = new StringBuilder("level,rows,columns,solutions,pairs,legal_rectangles\n");
        var actual = new List<LevelData>();
        for (int i = 0; i <= LevelBoardPlan.LastLevelIndex; i++)
        {
            Require(ReleasePuzzleBank.TryGet(i, out var level), "Missing pair puzzle " + i);
            int count = PuzzleSolver.CountSolutions(level, 2);
            var solution = level.clues.Select((c,n) => new PlayerRegion {clueId=n,colorIndex=n/2,rect=c.SolutionRect})
                .GroupBy(region => region.rect).Select(group => group.First()).ToList();
            Require(count == 1 && RegionValidator.ValidateFinal(level, solution), "Invalid pair puzzle " + i);
            Require(solution.Count * 2 == level.clues.Count, "Expected two clues per solution rectangle");
            int legal = 0;
            var empty = new PlayerRegion[0];
            for (int y=0; y<level.rows; y++) for (int x=0; x<level.columns; x++)
                for (int h=1; h<=level.rows-y; h++) for (int w=1; w<=level.columns-x; w++)
                {
                    var rect = new RectInt(x,y,w,h);
                    var inside = level.clues.Where(c => c.row>=y && c.row<y+h && c.column>=x && c.column<x+w).ToArray();
                    bool expected = inside.Length==2 && inside.Sum(c=>c.value)==w*h;
                    bool valid = RegionValidator.TryValidateLocal(level,rect,empty,out _);
                    Require(valid==expected, "Pair/area validation mismatch");
                    if (valid) legal++;
                }
            Require(!RegionValidator.TryValidateLocal(level,solution[0].rect,solution,out _), "Overlap accepted");
            Require(!RegionValidator.TryValidateLocal(level,new RectInt(-1,0,1,1),empty,out _), "Outside board accepted");
            Require(!RegionValidator.ValidateFinal(level,solution.Take(solution.Count-1).ToList()), "Partial coverage accepted");
            var completed = new ReleaseSave {levelIndex=i,completedLevels=i+1,puzzle=ReleaseProgress.Fingerprint(level),seconds=37.5f,tutorialSeen=true};
            foreach (var region in solution) completed.rectangles.Add(new SavedRectangle(region.rect));
            Require(ReleaseProgress.TryParse(JsonUtility.ToJson(completed),out var restoredSave)
                && ReleaseProgress.TryRestoreRegions(restoredSave,level,out var restoredRegions)
                && RegionValidator.ValidateFinal(level,restoredRegions), "Completed puzzle save failed: " + (i+1));
            actual.Add(level);
            csv.AppendLine($"{i+1},{level.rows},{level.columns},{count},{solution.Count},{legal}");
        }
        Require(!ReleasePuzzleBank.TryGet(LevelBoardPlan.LastLevelIndex + 1,out _), "Unexpected extra level");
        // Two valid domino tilings: final validation must not enforce a hidden solution.
        var alternate = new LevelData {rows=2,columns=2};
        for (int y=0;y<2;y++) for (int x=0;x<2;x++) alternate.clues.Add(new ClueData {row=y,column=x,value=1});
        Require(PuzzleSolver.CountSolutions(alternate,3)==2, "Alternative solution count is wrong");
        Require(RegionValidator.ValidateFinal(alternate,new[] {new PlayerRegion {rect=new RectInt(0,0,1,2)},new PlayerRegion {rect=new RectInt(1,0,1,2)}}), "Legal vertical pairing rejected");
        Require(RegionValidator.ValidateFinal(alternate,new[] {new PlayerRegion {rect=new RectInt(0,0,2,1)},new PlayerRegion {rect=new RectInt(0,1,2,1)}}), "Legal horizontal pairing rejected");
        File.WriteAllText(Path.Combine(Root,"puzzle-verification.csv"),csv.ToString());
        var first=actual[0];
        var save=new ReleaseSave{puzzle=ReleaseProgress.Fingerprint(first),seconds=42.5f,tutorialSeen=true,freezeUsed=true,frozenSeconds=3};
        save.rectangles.Add(new SavedRectangle(first.clues[0].SolutionRect));
        Require(ReleaseProgress.TryParse(JsonUtility.ToJson(save),out var parsed),"Save roundtrip failed");
        Require(ReleaseProgress.TryRestoreRegions(parsed,first,out var placed)&&placed.Count==1&&parsed.seconds==42.5f&&parsed.freezeUsed,"Restore state failed");
        Require(!ReleaseProgress.TryParse("{broken",out _),"Corrupt JSON accepted");
        Require(!ReleaseProgress.TryParse("{}",out _),"Empty JSON accepted");
        parsed.version=99; Require(!ReleaseProgress.TryParse(JsonUtility.ToJson(parsed),out _),"Unknown schema accepted");
        parsed.version=1; parsed.rectangles.Add(new SavedRectangle(first.clues[0].SolutionRect));
        Require(!ReleaseProgress.TryRestoreRegions(parsed,first,out _),"Overlapping saved rectangles accepted");
        parsed.rectangles.RemoveAt(1); parsed.rectangles[0].width=int.MaxValue;
        Require(!ReleaseProgress.TryRestoreRegions(parsed,first,out _),"Out-of-range saved rectangle accepted");
        parsed.rectangles.Clear(); parsed.puzzle="different";
        Require(!ReleaseProgress.TryRestoreRegions(parsed,first,out _),"Changed puzzle accepted");
        File.WriteAllText(Path.Combine(Root,"logic-tests.txt"),$"PASS: {actual.Count} unique pair-sum puzzles, all rectangle counts/sums, overlap/bounds/partial rejection, both alternate tilings accepted.\nPASS: Every completed level saves and restores; save roundtrip, timer/freeze restore, corrupt/empty JSON rejection, future schema rejection, overlap rejection, bounds rejection, changed-puzzle rejection.\n");
        AssetDatabase.Refresh();
    }
    private static void PrepareScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Release.unity");
        ReleaseSceneSetup.Validate();
    }

    private static void BakeClickPcm()
    {
        // WebGL's normal AudioClip pipeline re-encodes to AAC, which can alter this 91 ms transient.
        // Store decoded samples as a tiny resource and create the WebGL clip without a lossy encoder.
        var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/art/Audio/click1.ogg");
        if (importer.ContainsSampleSettingsOverride("WebGL"))
        { importer.ClearSampleSettingOverride("WebGL"); importer.SaveAndReimport(); }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/art/Audio/click1.ogg");
        Require(clip != null && clip.LoadAudioData(), "Cannot load original click audio");
        var samples = new float[clip.samples * clip.channels];
        Require(clip.GetData(samples, 0), "Cannot read original click samples");
        Require(samples.Length > 0 && samples.Any(s => Mathf.Abs(s) > .1f), "Click audio is silent");
        using (var writer = new BinaryWriter(File.Create("Assets/Resources/AreaGridClickPcm.bytes")))
        {
            writer.Write(clip.frequency); writer.Write(clip.channels); writer.Write(clip.samples);
            foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
        }
        File.WriteAllText(Path.Combine(Root,"audio-verification.txt"),
            $"PCM16: {clip.frequency} Hz, {clip.channels} channels, {clip.samples} frames, {clip.length:F6} seconds.\nPeak: {samples.Max(s=>Mathf.Abs(s)):F6}. No gain or pitch change; AAC encoding bypassed in WebGL.\n");
    }
}
