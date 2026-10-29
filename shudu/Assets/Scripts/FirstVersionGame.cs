using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shud
{
    public sealed partial class FirstVersionGame : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text timerText;
        [SerializeField] private Button hintButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button freezeButton;
        [SerializeField] private Button failureRestartButton;
        [SerializeField] private LevelStateBanner stateBanner;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private GameObject completePanel;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Text levelText;
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private GridCellView[] cellPool;
        [SerializeField] private PlacedRegionView[] regionPool;
        [SerializeField] private SelectionPreviewView selectionPreview;
        [SerializeField] private RectTransform placedRegionsRoot;
        [SerializeField] private GridCellView[] cells;

        [Header("Level Generation")]
        [SerializeField] private int generationAttempts = 300;
        [SerializeField] private int levelSeed = 20260611;

        [Header("Level Config (数据驱动关卡参数)")]
        [Tooltip("勾选后，若 levelConfig 中存在对应关卡的条目，则使用其参数；否则回退到 LevelBoardPlan（与原行为一致）。")]
        [SerializeField] private bool useLevelConfig = false;
        [Tooltip("关卡配置资源（Create → Shud → Level Config）。为空或未勾选 useLevelConfig 时走原程序化生成。")]
        [SerializeField] private LevelConfig levelConfig;

        [Header("Board Layout")]
        [SerializeField] private float cellGap = 5f;
        [SerializeField] private float boardPadding = 18f;

        [Header("Visuals")]
        [SerializeField] private Color emptyColor = new Color(0.92f, 0.93f, 0.90f);
        [SerializeField] private Color selectionFillColor = new Color(1f, 0.58f, 0.14f, 0.28f);
        [SerializeField] private Color selectionBorderColor = new Color(1f, 0.55f, 0f, 1f);
        [SerializeField] private Color invalidSelectionFillColor = new Color(0.94f, 0.28f, 0.24f, 0.18f);
        [SerializeField] private Color invalidSelectionBorderColor = new Color(0.94f, 0.28f, 0.24f, 1f);
        [SerializeField] private Color[] regionColors =
        {
            new Color(0.74f, 0.91f, 0.80f),
            new Color(1.00f, 0.78f, 0.70f),
            new Color(0.70f, 0.85f, 1.00f),
            new Color(0.86f, 0.78f, 0.96f),
            new Color(1.00f, 0.91f, 0.60f),
            new Color(1.00f, 0.73f, 0.82f),
            new Color(0.70f, 0.49f, 0.46f),
            new Color(0.51f, 0.49f, 0.77f),
            new Color(1.00f, 0.76f, 0.31f),
            new Color(0.91f, 0.44f, 0.64f),
            new Color(0.71f, 0.84f, 0.63f),
            new Color(0.85f, 0.95f, 0.87f),
            new Color(0.96f, 0.44f, 0.49f)
        };

        private readonly List<int> availableRegionColorIndices = new List<int>();
        private readonly List<PlacedRegionView> regionViews = new List<PlacedRegionView>();
        [Header("Runtime")]
        [SerializeField] private bool forcePortrait = true;
        [SerializeField] private float invalidFlashSeconds = 0.45f;
        [SerializeField] private float levelSeconds = 60f;
        [SerializeField] private float failurePanelDelaySeconds = 1f;
        [SerializeField] private float freezeSeconds = 10f;

        private readonly List<PlayerRegion> regions = new List<PlayerRegion>();
        private LevelData level;
        private GridCellView[,] cellByPosition;
        private int[,] regionByCell;
        private GridCellView selectionStart;
        private GridCellView selectionEnd;
        private int selectionColorIndex = -1;
        private Vector2 boardMaxSize;
        private bool pendingClear;
        private int pendingClearRegionIndex;
        private Coroutine invalidRoutine;
        private Coroutine failureRoutine;
        private Coroutine completionRoutine;
        private float remainingSeconds;
        private float freezeRemainingSeconds;
        private float activeLevelSeconds;
        private float activeFreezeSeconds;
        private int currentLevelIndex = LevelBoardPlan.FirstLevelIndex;
        private bool locked;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BindSceneReferences();
            ConfigureRelease();
            HidePanels();
        }

        private void OnEnable()
        {
            if (hintButton != null)
            {
                hintButton.onClick.AddListener(ShowHint);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(AskRestart);
            }

            if (freezeButton != null)
            {
                freezeButton.onClick.AddListener(FreezeTimer);
            }

            if (failureRestartButton != null)
            {
                failureRestartButton.onClick.AddListener(RestartLevel);
            }

            if (nextLevelButton != null)
            {
                ConnectNextLevelButton();
            }
        }

        private void Start()
        {
            StartRelease();
        }

        private void Update()
        {
            TickRelease();
            if (locked || paused || currentLevelIndex < TutorialLevelCount)
            {
                return;
            }

            if (freezeRemainingSeconds > 0f)
            {
                freezeRemainingSeconds = Mathf.Max(0f, freezeRemainingSeconds - Time.deltaTime);
                UpdateFreezeButtonState();
                UpdateTimerText();
                return;
            }

            UpdateFreezeButtonState();
            if (remainingSeconds <= 0f) return;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds == 0f) ReachTargetTime();
            UpdateTimerText();
        }

        private void OnDisable()
        {
            StopCompletionAnimation();
            SaveRelease();
            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(ShowHint);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(AskRestart);
            }

            if (freezeButton != null)
            {
                freezeButton.onClick.RemoveListener(FreezeTimer);
            }

            if (failureRestartButton != null)
            {
                failureRestartButton.onClick.RemoveListener(RestartLevel);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(AdvanceToNextLevel);
            }
        }

        internal void BeginSelection(GridCellView cell)
        {
            if (locked || paused)
            {
                return;
            }
            if (invalidRoutine != null) { StopCoroutine(invalidRoutine); invalidRoutine = null; }
            HideSelectionPreview();

            int regionIndex = regionByCell[cell.Row, cell.Column];
            if (regionIndex >= 0)
            {
                pendingClear = true;
                pendingClearRegionIndex = regionIndex;
                selectionStart = cell;
                selectionEnd = cell;
                SetStatus("Release to remove this rectangle.");
                return;
            }

            pendingClear = false;
            pendingClearRegionIndex = -1;
            selectionStart = cell;
            selectionEnd = cell;
            selectionColorIndex = availableRegionColorIndices.Count > 0
                ? availableRegionColorIndices[Random.Range(0, availableRegionColorIndices.Count)] : -1;
            PreviewSelection();
        }

        internal void UpdateSelectionFromScreen(Vector2 screenPosition, Camera eventCamera)
        {
            if (locked || selectionStart == null || pendingClear)
            {
                return;
            }

            GridCellView cell = FindCellAtScreenPosition(screenPosition, eventCamera);
            if (cell == null || cell == selectionEnd)
            {
                return;
            }

            selectionEnd = cell;
            PreviewSelection();
        }

        internal void CommitSelection()
        {
            if (locked || selectionStart == null)
            {
                return;
            }

            if (pendingClear)
            {
                RemoveRegion(pendingClearRegionIndex);
                ClearSelection();
                SetStatus("Rectangle removed.");
                return;
            }

            RectInt rect = BuildSelectionRect();
            bool valid = RegionValidator.TryValidateLocal(level, rect, regions, out int clueId);
            if (valid)
            {
                int colorIndex = selectionColorIndex;
                if (!availableRegionColorIndices.Remove(colorIndex)) colorIndex = AllocateRegionColorIndex();
                if (colorIndex < 0)
                {
                    ClearSelection();
                    SetStatus("Remove a rectangle before adding another.");
                    return;
                }

                regions.Add(new PlayerRegion { clueId = clueId, colorIndex = colorIndex, rect = rect });
                regionViews.Add(CreatePlacedRegionView());
                ClearSelection();
                RebuildBoardVisuals();
                regionViews[regionViews.Count - 1]?.Reveal();
                if (AllCellsFilled())
                {
                    if (RegionValidator.ValidateFinal(level, regions))
                    {
                        CompleteLevel();
                    }
                    else
                    {
                        SetStatus("Check the rectangles and try again.");
                    }
                }
                else
                {
                    SetStatus(teaching ? TutorialPrompt : PairEquation(rect) + ". Nice pair!");
                }

                return;
            }

            ClearSelection();
            ShowInvalid(rect);
            SetStatus(PairFeedback(rect, false));
        }

        private void BindSceneReferences()
        {
            if (boardRoot == null || selectionPreview == null || releaseUI == null)
                throw new System.InvalidOperationException("Release scene references are missing.");
            boardMaxSize = boardRoot.rect.size;
            selectionPreview.Initialize();
            foreach (var view in regionPool) { view.Initialize(); view.Hide(); }
        }

        private void LoadLevel(int levelIndex)
        {
            if (teaching) HideTeachingDemo();
            teaching = false;
            releaseUI.skipTutorial.gameObject.SetActive(false);
            currentLevelIndex = Mathf.Clamp(levelIndex, LevelBoardPlan.FirstLevelIndex, GetLastLevelIndex());
            level = GenerateLevel(currentLevelIndex);
            cellByPosition = new GridCellView[level.rows, level.columns];
            regionByCell = new int[level.rows, level.columns];
            BuildCells();
            ResolveActiveTimers();
            remainingSeconds = activeLevelSeconds;
            ResetBoard();
            RelayoutReleaseBoard();
            if (currentLevelIndex < TutorialLevelCount && !tutorialSeen) StartTeaching();
        }

        private LevelData GenerateLevel(int levelIndex)
        {
            if (ReleasePuzzleBank.TryGet(levelIndex, out var baked)) return baked;
            throw new System.InvalidOperationException("Pair puzzle data is missing: " + levelIndex);
        }

        private bool IsLevelConfigActive(int levelIndex)
        {
            return useLevelConfig && levelConfig != null && levelConfig.HasLevel(levelIndex);
        }

        private int GetLastLevelIndex()
        {
            if (useLevelConfig && levelConfig != null && levelConfig.LevelCount > 0)
            {
                return levelConfig.LevelCount - 1;
            }

            return LevelBoardPlan.LastLevelIndex;
        }

        private void ResolveActiveTimers()
        {
            float targetSeconds = currentLevelIndex < TutorialLevelCount ? levelSeconds : LevelBoardPlan.GetTargetSeconds(currentLevelIndex);
            if (IsLevelConfigActive(currentLevelIndex))
            {
                activeLevelSeconds = levelConfig.ResolveLevelSeconds(currentLevelIndex, targetSeconds);
                activeFreezeSeconds = levelConfig.ResolveFreezeSeconds(currentLevelIndex, freezeSeconds);
            }
            else
            {
                activeLevelSeconds = targetSeconds;
                activeFreezeSeconds = freezeSeconds;
            }
        }

        private void BuildCells()
        {
            // ponytail: the authored scene supports 6x6 and 8 pairs; enlarge the pools for bigger puzzles.
            int count = level.rows * level.columns;
            if (level.rows > 6 || level.columns > 6 || count > cellPool.Length || level.clues.Count > regionPool.Length * 2)
                throw new System.InvalidOperationException("Puzzle exceeds the authored scene capacity.");
            foreach (var cell in cellPool) cell.gameObject.SetActive(false);
            selectionPreview.Hide();
            cells = new GridCellView[count];
            for (int row = 0; row < level.rows; row++)
                for (int column = 0; column < level.columns; column++)
                {
                    int index = row * level.columns + column;
                    var cell = cellPool[index];
                    cell.Configure(this, row, column);
                    cell.gameObject.SetActive(true);
                    cells[index] = cell;
                    cellByPosition[row, column] = cell;
                }
            placedRegionsRoot.SetAsLastSibling();
            selectionPreview.transform.SetAsLastSibling();
        }

        private void PreviewSelection()
        {
            if (selectionStart == null || selectionEnd == null)
            {
                return;
            }

            RectInt rect = BuildSelectionRect();
            bool valid = RegionValidator.TryValidateLocal(level, rect, regions, out int clueId);
            // One translucent surface spans the entire rectangle, including internal gutters.
            Color preview = Color.Lerp(Color.white, GetRegionColor(selectionColorIndex), .55f);
            preview.a = .55f;
            if (selectionPreview != null)
                selectionPreview.Show(rect, cellByPosition, preview, Color.clear, "");
            SetStatus(PairFeedback(rect, valid));
            if (teaching && valid) statusText.color = new Color32(32, 145, 98, 255);
        }

        private void ShowInvalid(RectInt rect)
        {
            if (invalidRoutine != null)
            {
                StopCoroutine(invalidRoutine);
            }

            invalidRoutine = StartCoroutine(ShowInvalidRoutine(rect));
        }

        private IEnumerator ShowInvalidRoutine(RectInt rect)
        {
            RebuildBoardVisuals();
            ShowSelectionPreview(rect, false, -1);
            yield return new WaitForSeconds(invalidFlashSeconds);
            HideSelectionPreview();
            RebuildBoardVisuals();
            invalidRoutine = null;
        }

        private void ShowHint()
        {
            if (locked || paused)
            {
                return;
            }
            ClearSelection();
            if (invalidRoutine != null) { StopCoroutine(invalidRoutine); invalidRoutine = null; }
            for (int i = 0; i < regions.Count; i++)
            {
                var placed = regions[i];
                if (!placed.rect.Equals(level.clues[placed.clueId].SolutionRect))
                {
                    SetStatus("This rectangle blocks the solution. Tap it to remove it.");
                    invalidRoutine = StartCoroutine(ShowHintRegion(placed.rect, false));
                    return;
                }
            }
            ClueData clue = FindFirstUnfilledClue();
            SetStatus(PairEquation(clue.SolutionRect) + ". Try the highlighted rectangle.");
            invalidRoutine = StartCoroutine(ShowHintRegion(clue.SolutionRect, true));
        }

        private IEnumerator ShowHintRegion(RectInt rect, bool valid)
        {
            ShowSelectionPreview(rect, valid, -1);
            yield return new WaitForSeconds(3f);
            HideSelectionPreview(); invalidRoutine = null;
        }

        private void FreezeTimer()
        {
            if (locked || paused || currentLevelIndex < TutorialLevelCount || freezeUsed || freezeRemainingSeconds > 0f || remainingSeconds <= 0f)
            {
                return;
            }

            freezeRemainingSeconds = activeFreezeSeconds;
            freezeUsed = true;
            SetStatus("Time frozen for 10 seconds. One freeze per puzzle.");
            UpdateFreezeButtonState();
            SaveRelease();
        }

        private void RestartLevel()
        {
            remainingSeconds = activeLevelSeconds;
            ResetBoard();
        }

        private void AdvanceToNextLevel()
        {
            if (!HasNextLevel())
            {
                LoadLevel(LevelBoardPlan.FirstLevelIndex);
                return;
            }

            LoadLevel(currentLevelIndex + 1);
        }

        private bool HasNextLevel()
        {
            return currentLevelIndex < GetLastLevelIndex();
        }

        private ClueData FindFirstUnfilledClue()
        {
            for (int clueId = 0; clueId < level.clues.Count; clueId++)
            {
                bool filled = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    if (RegionValidator.Contains(regions[i].rect, level.clues[clueId].row, level.clues[clueId].column))
                    {
                        filled = true;
                        break;
                    }
                }

                if (!filled)
                {
                    return level.clues[clueId];
                }
            }

            return level.clues[0];
        }

        private void ResetBoard()
        {
            StopCompletionAnimation();
            tutorialSucceeded = false;
            locked = false;
            if (hintButton != null) hintButton.interactable = true;
            if (resetButton != null) resetButton.interactable = true;
            paused = false;
            freezeUsed = false;
            freezeRemainingSeconds = 0f;
            if (failureRoutine != null)
            {
                StopCoroutine(failureRoutine);
                failureRoutine = null;
            }

            if (invalidRoutine != null)
            {
                StopCoroutine(invalidRoutine);
                invalidRoutine = null;
            }

            HidePanels();
            ClearPlacedRegionViews();
            regions.Clear();
            ResetRegionColorPool();
            ClearSelection();
            RebuildBoardVisuals();
            UpdateTimerText();
            UpdateLevelText();
            UpdateFreezeButtonState();
            if (stateBanner != null)
            {
                stateBanner.SetState(LevelBannerState.Playing);
                stateBanner.SetProgress(GetBoardProgress());
            }

            SetStatus(IntroForLevel());
            if (teaching) StartTeaching();
            SaveRelease();
        }

        private void RemoveRegion(int regionIndex)
        {
            if (regionIndex >= 0 && regionIndex < regions.Count)
            {
                ReleaseRegionColorIndex(regions[regionIndex].colorIndex);
                DestroyRegionView(regionIndex);
                regions.RemoveAt(regionIndex);
            }

            RebuildBoardVisuals();
            if (teaching) SetStatus(TutorialPrompt);
            SaveRelease();
        }

        private void RebuildBoardVisuals()
        {
            for (int row = 0; row < level.rows; row++)
            {
                for (int column = 0; column < level.columns; column++)
                {
                    if (cellByPosition[row, column] == null)
                    {
                        continue;
                    }

                    regionByCell[row, column] = -1;
                    int clueId = level.FindClueIdAt(row, column);
                    cellByPosition[row, column].SetText(clueId >= 0 ? level.clues[clueId].value.ToString() : "", clueId >= 0);
                    cellByPosition[row, column].SetColor(emptyColor);
                }
            }

            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                PlayerRegion region = regions[regionIndex];
                Color color = GetRegionColor(region.colorIndex);
                if (regionIndex < regionViews.Count && regionViews[regionIndex] != null)
                {
                    regionViews[regionIndex].Show(region.rect, cellByPosition, color, level);
                }

                for (int row = region.rect.yMin; row < region.rect.yMax; row++)
                {
                    for (int column = region.rect.xMin; column < region.rect.xMax; column++)
                    {
                        if (cellByPosition[row, column] == null)
                        {
                            continue;
                        }

                        regionByCell[row, column] = regionIndex;
                        // The region replaces these cell surfaces; retain their pointer targets.
                        cellByPosition[row, column].SetColor(Color.clear);
                        cellByPosition[row, column].SetText("");
                    }
                }
            }

            if (stateBanner != null)
            {
                stateBanner.SetProgress(GetBoardProgress());
            }
            releaseUI?.SetProgress(GetBoardProgress());
        }

        private void CompleteLevel(bool animate = true)
        {
            CompleteTeaching();
            StopCompletionAnimation();
            locked = true;
            if (hintButton != null) hintButton.interactable = false;
            if (resetButton != null) resetButton.interactable = false;
            completedLevels = Mathf.Max(completedLevels, currentLevelIndex + 1);
            releaseUI.SetCompletion(!HasNextLevel(), remainingSeconds);
            if (currentLevelIndex < TutorialLevelCount) releaseUI.completeTime.text = "Tutorial " + (currentLevelIndex + 1) + " / " + TutorialLevelCount + " complete";
            freezeRemainingSeconds = 0f;
            ClearSelection();
            SetStatus("");
            if (tutorialSucceeded) SetStatus(currentLevelIndex == TutorialLevelCount - 1 ? "Aim for the target time. Overtime lets you keep playing." : "Great! Let's try the next teaching puzzle.");
            UpdateFreezeButtonState();
            if (stateBanner != null)
            {
                stateBanner.SetProgress(1f);
                stateBanner.SetState(LevelBannerState.Completed);
            }

            SaveRelease();
            if (animate) completionRoutine = StartCoroutine(ShowCompletionAfterWave());
            else ShowCompletionPanel();
        }

        private IEnumerator ShowCompletionAfterWave()
        {
            // Let the last pair finish merging before celebrating the whole board.
            yield return new WaitForSecondsRealtime(.2f);
            var order = new List<int>();
            for (int i = 0; i < regionViews.Count; i++) order.Add(i);
            order.Sort((a, b) => {
                int row = regions[a].rect.yMin.CompareTo(regions[b].rect.yMin);
                return row != 0 ? row : regions[a].rect.xMin.CompareTo(regions[b].rect.xMin);
            });

            const float pulseSeconds = .24f, staggerSeconds = .06f;
            float startedAt = Time.unscaledTime;
            float duration = pulseSeconds + staggerSeconds * Mathf.Max(0, order.Count - 1);
            while (Time.unscaledTime - startedAt < duration)
            {
                float elapsed = Time.unscaledTime - startedAt;
                for (int i = 0; i < order.Count; i++)
                {
                    var view = regionViews[order[i]];
                    if (view == null) continue;
                    float progress = (elapsed - i * staggerSeconds) / pulseSeconds;
                    bool active = progress > 0 && progress < 1;
                    view.transform.localScale = Vector3.one * (active ? 1 + .1f * Mathf.Sin(progress * Mathf.PI) : 1);
                    if (active) view.transform.SetAsLastSibling();
                }
                yield return null;
            }

            foreach (var view in regionViews)
                if (view != null) view.transform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(.5f);
            releaseUI.SetCompletionReveal(0);
            ShowCompletionPanel();
            startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < .22f)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / .22f);
                releaseUI.SetCompletionReveal(1 - Mathf.Pow(1 - progress, 3));
                yield return null;
            }
            releaseUI.SetCompletionReveal(1);
            completionRoutine = null;
        }

        private void StopCompletionAnimation()
        {
            if (completionRoutine == null) return;
            StopCoroutine(completionRoutine);
            completionRoutine = null;
            releaseUI.SetCompletionReveal(1);
            foreach (var view in regionViews)
                if (view != null) view.transform.localScale = Vector3.one;
        }

        private void ShowCompletionPanel()
        {
            if (completePanel != null)
            {
                completePanel.SetActive(true);
                completePanel.transform.SetAsLastSibling();
                ConnectNextLevelButton();
            }

            if (!HasNextLevel())
            {
                SetStatus("All puzzles complete. Well played!");
            }
        }

        private void FailLevel()
        {
            locked = true;
            freezeRemainingSeconds = 0f;
            ClearSelection();
            SetStatus("");
            UpdateFreezeButtonState();
            if (stateBanner != null)
            {
                stateBanner.SetState(LevelBannerState.Failed);
            }

            if (failureRoutine != null)
            {
                StopCoroutine(failureRoutine);
            }

            failureRoutine = StartCoroutine(ShowFailurePanelAfterDelay());
            SaveRelease();
        }

        private IEnumerator ShowFailurePanelAfterDelay()
        {
            yield return new WaitForSeconds(failurePanelDelaySeconds);
            if (failurePanel != null)
            {
                failurePanel.SetActive(true);
            }

            failureRoutine = null;
        }

        private float GetBoardProgress()
        {
            int coveredCells = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                coveredCells += regions[i].rect.width * regions[i].rect.height;
            }

            return (float)coveredCells / (level.rows * level.columns);
        }

        private void ReachTargetTime()
        {
            remainingSeconds = 0f;
            freezeRemainingSeconds = 0f;
            UpdateTimerText(); UpdateFreezeButtonState();
            if (selectionStart == null) SetStatus("Target time reached. Keep solving!");
            SaveRelease();
        }

        private void UpdateTimerText()
        {
            if (timerText == null)
            {
                return;
            }

            bool tutorial = currentLevelIndex < TutorialLevelCount;
            releaseUI.SetTimerCompact(tutorial || remainingSeconds <= 0f);
            if (tutorial) { timerText.text = "Tutorial"; return; }
            if (remainingSeconds <= 0f) { timerText.fontSize = 14; timerText.text = "Overtime"; return; }
            int seconds = Mathf.CeilToInt(remainingSeconds);
            int minutes = seconds / 60;
            int remainder = seconds % 60;
            timerText.text = minutes.ToString("00") + ":" + remainder.ToString("00");
        }

        private void UpdateFreezeButtonState()
        {
            if (freezeButton != null)
            {
                freezeButton.interactable = !locked && !paused && currentLevelIndex >= TutorialLevelCount && !freezeUsed && freezeRemainingSeconds <= 0f && remainingSeconds > 0f;
            }
            releaseUI?.SetFreeze(freezeUsed, freezeRemainingSeconds);
        }

        private void HidePanels()
        {
            if (failurePanel != null)
            {
                failurePanel.SetActive(false);
            }

            if (completePanel != null)
            {
                completePanel.SetActive(false);
            }
        }

        private void ShowSelectionPreview(RectInt rect, bool valid, int clueId)
        {
            if (selectionPreview == null)
            {
                return;
            }

            Color fill = valid ? selectionFillColor : invalidSelectionFillColor;
            Color border = valid ? selectionBorderColor : invalidSelectionBorderColor;
            selectionPreview.Show(rect, cellByPosition, fill, border, "");
        }

        private void HideSelectionPreview()
        {
            if (selectionPreview != null)
            {
                selectionPreview.Hide();
            }
        }

        private void UpdateLevelText()
        {
            if (levelText != null)
            {
                levelText.text = "Level " + (currentLevelIndex + 1) + " / " + (GetLastLevelIndex() + 1);
            }
        }

        private void ResetRegionColorPool()
        {
            availableRegionColorIndices.Clear();
            if (regionColors == null)
            {
                return;
            }

            for (int i = 0; i < regionColors.Length; i++)
            {
                availableRegionColorIndices.Add(i);
            }
        }

        private int AllocateRegionColorIndex()
        {
            if (availableRegionColorIndices.Count == 0)
            {
                return -1;
            }

            int poolIndex = Random.Range(0, availableRegionColorIndices.Count);
            int colorIndex = availableRegionColorIndices[poolIndex];
            availableRegionColorIndices.RemoveAt(poolIndex);
            return colorIndex;
        }

        private void ReleaseRegionColorIndex(int colorIndex)
        {
            if (regionColors == null || colorIndex < 0 || colorIndex >= regionColors.Length || availableRegionColorIndices.Contains(colorIndex))
            {
                return;
            }

            availableRegionColorIndices.Add(colorIndex);
        }

        private Color GetRegionColor(int colorIndex)
        {
            if (regionColors == null || colorIndex < 0 || colorIndex >= regionColors.Length)
            {
                return emptyColor;
            }

            return regionColors[colorIndex];
        }

        private PlacedRegionView CreatePlacedRegionView()
        {
            foreach (var view in regionPool)
                if (!regionViews.Contains(view))
                {
                    view.Hide();
                    view.gameObject.SetActive(true);
                    return view;
                }
            throw new System.InvalidOperationException("No free authored rectangle in Release scene.");
        }

        private void DestroyRegionView(int regionIndex)
        {
            if (regionIndex < 0 || regionIndex >= regionViews.Count) return;
            regionViews[regionIndex].Hide();
            regionViews.RemoveAt(regionIndex);
        }

        private void ClearPlacedRegionViews()
        {
            foreach (var view in regionViews) view.Hide();
            regionViews.Clear();
        }

        private RectInt BuildSelectionRect()
        {
            int minRow = Mathf.Min(selectionStart.Row, selectionEnd.Row);
            int maxRow = Mathf.Max(selectionStart.Row, selectionEnd.Row);
            int minColumn = Mathf.Min(selectionStart.Column, selectionEnd.Column);
            int maxColumn = Mathf.Max(selectionStart.Column, selectionEnd.Column);
            return new RectInt(minColumn, minRow, maxColumn - minColumn + 1, maxRow - minRow + 1);
        }

        private bool AllCellsFilled()
        {
            for (int row = 0; row < level.rows; row++)
            {
                for (int column = 0; column < level.columns; column++)
                {
                    if (regionByCell[row, column] < 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private GridCellView FindCellAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                GridCellView cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                RectTransform rect = (RectTransform)cell.transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
                {
                    return cell;
                }
            }

            // Treat the narrow internal gutters as part of the closest cell, so a
            // finger released between two tiles doesn't cancel an otherwise valid drag.
            if (cellByPosition != null && cellByPosition.Length > 0 && cellByPosition[0, 0] != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, screenPosition, eventCamera, out Vector2 local))
            {
                var first = (RectTransform)cellByPosition[0, 0].transform;
                var last = (RectTransform)cellByPosition[level.rows - 1, level.columns - 1].transform;
                Vector2 half = first.sizeDelta * .5f;
                if (local.x >= first.anchoredPosition.x - half.x && local.x <= last.anchoredPosition.x + half.x &&
                    local.y <= first.anchoredPosition.y + half.y && local.y >= last.anchoredPosition.y - half.y)
                {
                    int column = Mathf.Clamp(Mathf.RoundToInt((local.x - first.anchoredPosition.x) / (first.sizeDelta.x + cellGap)), 0, level.columns - 1);
                    int row = Mathf.Clamp(Mathf.RoundToInt((first.anchoredPosition.y - local.y) / (first.sizeDelta.y + cellGap)), 0, level.rows - 1);
                    return cellByPosition[row, column];
                }
            }
            return null;
        }

        private void ClearSelection()
        {
            selectionColorIndex = -1;
            activePointer = int.MinValue;
            selectionStart = null;
            selectionEnd = null;
            pendingClear = false;
            pendingClearRegionIndex = -1;
            HideSelectionPreview();
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
                statusText.color = new Color32(93, 111, 126, 255);
            }
        }

        private void ConnectNextLevelButton()
        {
            if (completePanel == null)
            {
                completePanel = FindGameObjectInScene("NextPanel");
            }

            if (nextLevelButton == null && completePanel != null)
            {
                nextLevelButton = FindExistingButton(completePanel.transform, "NextLevelButton");
                if (nextLevelButton == null)
                {
                    nextLevelButton = FindExistingButton(completePanel.transform, "FailureAddTime");
                }
            }

            if (nextLevelButton == null)
            {
                return;
            }

            nextLevelButton.enabled = true;
            nextLevelButton.interactable = true;
            if (nextLevelButton.targetGraphic != null)
            {
                nextLevelButton.targetGraphic.raycastTarget = true;
            }

            nextLevelButton.onClick.RemoveListener(AdvanceToNextLevel);
            nextLevelButton.onClick.AddListener(AdvanceToNextLevel);
        }

        private static Button FindExistingButton(Transform root, string objectName)
        {
            Transform target = FindChildRecursive(root, objectName);
            if (target == null)
            {
                return null;
            }

            Button button = target.GetComponent<Button>();
            if (button != null)
            {
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                }

                return button;
            }

            return button;
        }

        private static T FindComponentInScene<T>(string objectName) where T : Component
        {
            GameObject target = FindGameObjectInScene(objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static Text FindLabelUnder(string rootName)
        {
            GameObject root = FindGameObjectInScene(rootName);
            if (root == null)
            {
                return null;
            }

            Transform label = FindChildRecursive(root.transform, "Label");
            return label != null ? label.GetComponent<Text>() : null;
        }

        private static GameObject FindGameObjectInScene(string objectName)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindChildRecursive(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
