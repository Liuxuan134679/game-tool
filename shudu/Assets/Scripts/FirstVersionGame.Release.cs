using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    public sealed partial class FirstVersionGame
    {
        [SerializeField] private ReleaseUI releaseUI;
        private bool paused, freezeUsed, tutorialSeen, muted, restoring;
        private bool teaching, tutorialSucceeded;
        private const int TutorialLevelCount = 3;
        private float tutorialDemoTime;
        private readonly Color tutorialInk = new Color32(13, 51, 96, 255);
        private int completedLevels;
        private int activePointer = int.MinValue;
        private float lastSaveTime;
        private bool saveWarningShown;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AreaGridInstallLifecycle(string gameObjectName);
        [DllImport("__Internal")] private static extern void AreaGridSetAudioMuted(int muted);
#endif
        private void ConfigureRelease()
        {
            gameObject.name = "AreaGridRuntime";
            releaseUI.Initialize(RelayoutReleaseBoard, ShowHelp, PauseGame, ResumeGame,
                FinishTutorial, ToggleSound, ConfirmRestart, CancelRestart, SkipTutorial);
            statusText = releaseUI.Status; timerText = releaseUI.Timer; levelText = releaseUI.Level;
            hintButton = releaseUI.Hint; resetButton = releaseUI.Restart; freezeButton = releaseUI.Freeze;
            failurePanel = releaseUI.Failure; failureRestartButton = releaseUI.Retry;
            completePanel = releaseUI.Complete; nextLevelButton = releaseUI.Next; stateBanner = null;
            RelayoutReleaseBoard();
#if UNITY_WEBGL && !UNITY_EDITOR
            AreaGridInstallLifecycle(gameObject.name);
#endif
        }

        private void StartRelease()
        {
            restoring = true;
            bool haveSave = ReleaseProgress.TryLoad(out var saved);
            if (haveSave) { tutorialSeen = saved.tutorialSeen; muted = saved.muted; completedLevels = saved.completedLevels; }
            AudioListener.volume = muted ? 0 : 1;
            SyncBrowserAudio();
            releaseUI.SetMuted(muted);
            LoadLevel(haveSave ? saved.levelIndex : 0);
            bool resumeTeaching = currentLevelIndex < TutorialLevelCount && !tutorialSeen;
            if (haveSave && ReleaseProgress.TryRestoreRegions(saved, level, out var restored) && restored.Count <= regionColors.Length)
            {
                remainingSeconds = Mathf.Min(saved.seconds, activeLevelSeconds); freezeRemainingSeconds = saved.frozenSeconds; freezeUsed = saved.freezeUsed;
                if (currentLevelIndex < TutorialLevelCount) { freezeRemainingSeconds = 0; freezeUsed = false; }
                foreach (var region in restored)
                {
                    region.colorIndex = AllocateRegionColorIndex();
                    regions.Add(region); regionViews.Add(CreatePlacedRegionView());
                }
                RebuildBoardVisuals(); UpdateTimerText(); UpdateFreezeButtonState();
                if (RegionValidator.ValidateFinal(level, regions)) CompleteLevel(false);
                else
                {
                    if (currentLevelIndex >= TutorialLevelCount && remainingSeconds <= 0) ReachTargetTime();
                    PauseGame();
                }
            }
            // Existing saves keep their progress; unfinished teaching resumes on its saved level.
            if (!locked && resumeTeaching)
            {
                freezeRemainingSeconds = 0; freezeUsed = false;
                paused = false; releaseUI.HideAuxiliary(); StartTeaching();
            }
            restoring = false;
            SaveRelease();
        }

        private void TickRelease()
        {
            releaseUI?.Tick();
            TickTeaching();
            if (!restoring && level != null && !locked && !paused && Time.unscaledTime - lastSaveTime >= 5) SaveRelease();
        }

        private void SaveRelease()
        {
            if (restoring || level == null) return;
            var saved = new ReleaseSave {
                levelIndex = currentLevelIndex, completedLevels = completedLevels, puzzle = ReleaseProgress.Fingerprint(level),
                seconds = Mathf.Clamp(remainingSeconds, 0, LevelBoardPlan.MaximumTargetSeconds), frozenSeconds = Mathf.Clamp(freezeRemainingSeconds, 0, 10),
                freezeUsed = freezeUsed, tutorialSeen = tutorialSeen, muted = muted
            };
            foreach (var region in regions) saved.rectangles.Add(new SavedRectangle(region.rect));
            if (!ReleaseProgress.Store(saved) && !saveWarningShown)
            { saveWarningShown = true; SetStatus("Saving is unavailable in this browser. Keep this tab open to continue."); }
            lastSaveTime = Time.unscaledTime;
        }

        private void ShowHelp()
        {
            if (locked) return;
            paused = true; ClearSelection(); releaseUI.HideAuxiliary(); ReleaseUI.Show(releaseUI.Help); SaveRelease();
        }
        private void FinishTutorial() { ResumeGame(); }
        private void PauseGame()
        {
            if (locked || releaseUI == null) return;
            paused = true; ClearSelection(); releaseUI.HideAuxiliary(); ReleaseUI.Show(releaseUI.Pause); SaveRelease();
        }
        private void ResumeGame()
        { releaseUI.HideAuxiliary(); paused = false; if (teaching) SetStatus(TutorialPrompt); SaveRelease(); }
        private void AskRestart()
        {
            if (locked) return;
            paused = true; ClearSelection(); ReleaseUI.Show(releaseUI.ConfirmRestart); SaveRelease();
        }
        private void ConfirmRestart() { releaseUI.HideAuxiliary(); paused = false; RestartLevel(); }
        private void CancelRestart() { releaseUI.ConfirmRestart.SetActive(false); paused = false; }
        private void ToggleSound() { muted = !muted; AudioListener.volume = muted ? 0 : 1; SyncBrowserAudio(); releaseUI.SetMuted(muted); SaveRelease(); }
        private void SyncBrowserAudio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AreaGridSetAudioMuted(muted ? 1 : 0);
#endif
        }
        private void OnApplicationFocus(bool focus) { if (!focus) { PauseGame(); SaveRelease(); } }
        private void OnApplicationPause(bool state) { if (state) { PauseGame(); SaveRelease(); } }
        private void OnApplicationQuit() { SaveRelease(); }
        // Called only by the small WebGL visibility bridge.
        public void BrowserHidden() { PauseGame(); SaveRelease(); }

        private void StartTeaching()
        {
            teaching = true; tutorialSucceeded = false; tutorialDemoTime = 0;
            releaseUI.skipTutorial.gameObject.SetActive(true);
            UpdateTimerText(); UpdateFreezeButtonState();
            SetStatus(TutorialPrompt);
        }

        private ClueData TeachingClue()
        {
            foreach (var clue in level.clues)
                if (RegionValidator.TryValidateLocal(level, clue.SolutionRect, regions, out _)) return clue;
            return null;
        }

        private string TutorialPrompt
        {
            get
            {
                var clue = TeachingClue();
                if (clue == null) return "Tap a rectangle to undo.\nLeave room for every pair.";
                return PairEquation(clue.SolutionRect) + " cells\n" + (currentLevelIndex == 0
                    ? "Drag from 2 to 4." : currentLevelIndex == 1
                    ? "Fill every cell with rectangles." : "Leave room for the remaining pairs.");
            }
        }

        private void HideTeachingDemo()
        {
            releaseUI.tutorialHand.gameObject.SetActive(false);
            if (level == null) return;
            foreach (var clue in level.clues)
            {
                var label = cellByPosition[clue.row, clue.column].ClueLabel;
                label.color = new Color(.08f, .1f, .1f);
                label.transform.localScale = Vector3.one;
            }
            if (selectionStart == null && invalidRoutine == null) HideSelectionPreview();
        }

        private void TickTeaching()
        {
            if (!teaching) return;
            if (paused || locked || selectionStart != null || invalidRoutine != null)
            { HideTeachingDemo(); tutorialDemoTime = 0; return; }
            var clue = TeachingClue();
            if (clue == null) { HideTeachingDemo(); return; }
            var target = clue.SolutionRect;
            float cycle = tutorialDemoTime % 2.8f;
            tutorialDemoTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(cycle / 2);
            var start = (RectTransform)cellByPosition[target.yMin, target.xMin].transform;
            var end = (RectTransform)cellByPosition[target.yMax - 1, target.xMax - 1].transform;
            var hand = releaseUI.tutorialHand;
            hand.gameObject.SetActive(cycle < 2.4f);
            hand.anchoredPosition = Vector2.Lerp(start.anchoredPosition, end.anchoredPosition, progress);
            hand.localScale = Vector3.one * Mathf.Clamp(start.rect.width / 90, .65f, 1);
            hand.anchoredPosition += new Vector2(18, -40) * hand.localScale.x;
            int column = Mathf.RoundToInt(progress * (target.width - 1));
            int row = Mathf.RoundToInt(progress * (target.height - 1));
            if (cycle < 2.4f)
                selectionPreview.Show(new RectInt(target.xMin, target.yMin, column + 1, row + 1), cellByPosition,
                    new Color(.70f, .85f, 1, .25f), Color.clear, "");
            else HideSelectionPreview();
            hand.SetAsLastSibling();
            // Preview is only a visual demonstration; it never submits a player rectangle.
            foreach (var number in level.clues)
            {
                if (!RegionValidator.Contains(target, number.row, number.column)) continue;
                var label = cellByPosition[number.row, number.column].ClueLabel;
                label.color = tutorialInk;
                label.transform.localScale = Vector3.one * (1.05f + .15f * Mathf.Sin(cycle * Mathf.PI * 2));
            }
        }

        private void SkipTutorial()
        {
            if (!teaching || locked || paused) return;
            HideTeachingDemo(); ClearSelection();
            if (invalidRoutine != null) { StopCoroutine(invalidRoutine); invalidRoutine = null; }
            HideSelectionPreview();
            teaching = false; tutorialSeen = true;
            releaseUI.skipTutorial.gameObject.SetActive(false);
            freezeRemainingSeconds = 0; freezeUsed = false;
            SetStatus(IntroForLevel()); UpdateTimerText(); UpdateFreezeButtonState(); SaveRelease();
        }

        private void CompleteTeaching()
        {
            if (!teaching) return;
            HideTeachingDemo(); teaching = false; tutorialSeen = currentLevelIndex == TutorialLevelCount - 1; tutorialSucceeded = true;
            releaseUI.skipTutorial.gameObject.SetActive(false);
        }

        private void RelayoutReleaseBoard()
        {
            if (releaseUI == null) return;
            boardMaxSize = releaseUI.BoardSpace;
            boardPadding = 8; cellGap = 2;
            if (level == null || cellByPosition == null) return;
            ClearSelection();
            float cellSize = Mathf.Max(1, Mathf.Floor(Mathf.Min(
                (boardMaxSize.x - boardPadding * 2 - cellGap * (level.columns - 1)) / level.columns,
                (boardMaxSize.y - boardPadding * 2 - cellGap * (level.rows - 1)) / level.rows)));
            float width = level.columns * cellSize + (level.columns - 1) * cellGap;
            float height = level.rows * cellSize + (level.rows - 1) * cellGap;
            boardRoot.anchorMin = boardRoot.anchorMax = boardRoot.pivot = new Vector2(.5f,.5f);
            boardRoot.anchoredPosition = Vector2.zero;
            boardRoot.sizeDelta = new Vector2(width + boardPadding * 2, height + boardPadding * 2);
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                var rect = (RectTransform)cell.transform;
                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = new Vector2(-width/2 + cellSize/2 + cell.Column*(cellSize+cellGap), height/2-cellSize/2-cell.Row*(cellSize+cellGap));
                cell.SetReleaseFontSize(Mathf.Clamp(Mathf.RoundToInt(cellSize * .4f), 17, 38));
            }
            RebuildBoardVisuals();
        }

        private string IntroForLevel()
        {
            switch (currentLevelIndex)
            {
                case 0: return "2 + 4 = 6. Drag from 2 to 4 to cover all six cells.";
                case 1: return "Fill the whole grid with rectangles.";
                case 2: return "Leave room: every number needs a partner. Tap a rectangle to undo.";
                case 3: return "Choose a partner: check both the sum and the space around it.";
                case 4: return "Look farther: the nearest number is not always the right partner.";
                case 5: return "Around the corner: leave room for pairs along the edges.";
                case 6: return "Wide or tall? The same area can have different shapes.";
                case 7: return "Use the edge: look for a rectangle that has nowhere else to go.";
                case 8: return "Turn the corner: check what your rectangle leaves behind.";
                case 9: return "One opens another: find a certain pair and follow the new edges.";
                case 10: return "Follow the chain: solve the tight spaces before the open ones.";
                case 11: return "Bring it together: each solved pair reveals the next step.";
                case 12: return "Find the anchor: start with a pair that has only one place to fit.";
                case 13: return "Split the long edge: a long side can belong to more than one pair.";
                case 14: return "Read the narrow lane: check how much room a pair needs.";
                case 15: return "Across the middle: one rectangle can separate two spaces.";
                case 16: return "Protect the corner: leave a way to fill every corner cell.";
                case 17: return "Two turns: follow the new edges left by each rectangle.";
                case 18: return "A fresh opening: find one certain pair, then build from it.";
                case 19: return "Compare the pairs: the same sum can make different shapes.";
                case 20: return "See the full height: a partner may be farther away than it looks.";
                case 21: return "Separate the bands: check which pairs can cross each row.";
                case 22: return "Work around the center: use the outside to narrow your choices.";
                case 23: return "Eight pieces: solve one pair at a time and keep room for the rest.";
                case 24: return "Reset your rhythm: a smaller board, a clear first step.";
                case 25: return "The small key: a small rectangle can unlock a larger space.";
                case 26: return "Follow the bend: check the space on both sides of each pair.";
                case 27: return "Break the symmetry: similar spaces may need different partners.";
                case 28: return "Room for the last pair: watch the cells left behind.";
                case 29: return "A chain of choices: each certain rectangle opens the next.";
                case 30: return "One clear step: take your time finding a certain pair.";
                default: return "The final fit: bring every pair together and fill the grid.";
            }
        }

        private string PairEquation(RectInt rect)
        {
            int first = -1, second = 0;
            foreach (var clue in level.clues)
                if (RegionValidator.Contains(rect, clue.row, clue.column))
                { if (first < 0) first = clue.value; else second = clue.value; }
            return first + " + " + second + " = " + (first + second);
        }

        private string PairFeedback(RectInt rect, bool valid)
        {
            int count = 0, sum = 0;
            foreach (var clue in level.clues)
                if (RegionValidator.Contains(rect, clue.row, clue.column)) { count++; sum += clue.value; }
            int area = rect.width * rect.height;
            if (count != 2)
            {
                var target = teaching ? TeachingClue() : null;
                return target != null ? area + " / " + target.SolutionRect.width * target.SolutionRect.height + " cells\nInclude both numbers."
                    : "Each rectangle needs exactly two numbers.";
            }
            if (valid) return teaching ? area + " / " + sum + " - Release!" : PairEquation(rect) + ". Release to place.";
            if (area == sum) return "Rectangles cannot overlap.";
            return teaching && selectionStart != null ? area + " / " + sum + " cells"
                : PairEquation(rect) + ". You selected " + area + " cells. You need " + sum + ".";
        }

        internal void PointerDown(GridCellView cell, int pointer)
        {
            if (locked || paused || activePointer != int.MinValue) return;
            if (teaching) HideTeachingDemo();
            activePointer = pointer; BeginSelection(cell);
        }
        internal void PointerDrag(int pointer, Vector2 position, Camera camera)
        { if (pointer == activePointer && !paused) UpdateSelectionFromScreen(position, camera); }
        internal void PointerUp(int pointer, Vector2 position, Camera camera)
        {
            if (pointer != activePointer) return;
            if (paused || FindCellAtScreenPosition(position, camera) == null) { ClearSelection(); return; }
            UpdateSelectionFromScreen(position, camera); CommitSelection(); activePointer = int.MinValue; SaveRelease();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void AuditState()
        {
            Debug.Log("AREA_GRID_QA " + JsonUtility.ToJson(new AuditSnapshot {
                level = currentLevelIndex + 1, seconds = remainingSeconds, target = activeLevelSeconds, frozen = freezeRemainingSeconds,
                freezeUsed = freezeUsed, paused = paused, locked = locked, regions = regions.Count,
                pointer = activePointer, status = statusText.text, teaching = teaching, tutorialSeen = tutorialSeen,
                demoVisible = releaseUI.tutorialHand.gameObject.activeSelf,
                timer = timerText.text, rating = releaseUI.completeTime.text,
                failureVisible = failurePanel.activeSelf, freezeEnabled = freezeButton.interactable,
                completeVisible = completePanel.activeSelf, rows = level.rows, columns = level.columns,
                cells = Array.ConvertAll(cells, c => AuditPoint((RectTransform)c.transform)),
                buttons = new[] { AuditPoint((RectTransform)releaseUI.skipTutorial.transform),
                    AuditPoint((RectTransform)releaseUI.pause.transform), AuditPoint((RectTransform)releaseUI.resume.transform),
                    AuditPoint((RectTransform)releaseUI.help.transform), AuditPoint((RectTransform)releaseUI.tutorialDone.transform),
                    AuditPoint((RectTransform)nextLevelButton.transform), AuditPoint((RectTransform)freezeButton.transform),
                    AuditPoint((RectTransform)resetButton.transform), AuditPoint((RectTransform)releaseUI.confirm.transform) }
            }));
        }
        private static Vector2 AuditPoint(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            return new Vector2(point.x / Screen.width, 1 - point.y / Screen.height);
        }
        [Serializable] private sealed class AuditSnapshot
        { public int level, regions, pointer, rows, columns; public float seconds, target, frozen; public bool freezeUsed, paused, locked, teaching, tutorialSeen, demoVisible, failureVisible, freezeEnabled, completeVisible; public string status, timer, rating; public Vector2[] cells, buttons; }
        public void AuditLevel(string value)
        {
            if (!int.TryParse(value, out int index) || index < 0 || index > GetLastLevelIndex()) return;
            releaseUI.HideAuxiliary(); paused = false; LoadLevel(index);
        }
        public void AuditComplete()
        {
            ClearPlacedRegionViews(); regions.Clear(); ResetRegionColorPool();
            for (int i=0;i<level.clues.Count;i+=2)
            { regions.Add(new PlayerRegion {clueId=i,colorIndex=AllocateRegionColorIndex(),rect=level.clues[i].SolutionRect}); regionViews.Add(CreatePlacedRegionView()); }
            RebuildBoardVisuals(); CompleteLevel();
        }
        public void AuditExpire() { ReachTargetTime(); }
        public void AuditSeconds(string value)
        {
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds > activeLevelSeconds) return;
            remainingSeconds = seconds;
            if (seconds == 0) ReachTargetTime();
            else { UpdateTimerText(); UpdateFreezeButtonState(); }
        }
#endif
    }
}
