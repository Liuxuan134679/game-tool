using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shud
{
    public sealed class FirstVersionGame : MonoBehaviour
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
        [SerializeField] private GridCellView cellPrefab;
        [SerializeField] private SelectionPreviewView selectionPreview;
        [SerializeField] private RectTransform placedRegionsRoot;
        [SerializeField] private PlacedRegionView placedRegionTemplate;
        [SerializeField] private GridCellView[] cells;

        [Header("Level Generation")]
        [SerializeField] private int generationAttempts = 300;
        [SerializeField] private int levelSeed = 20260611;

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
        private GridCellView resolvedCellTemplate;
        private Vector2 boardMaxSize;
        private bool pendingClear;
        private int pendingClearRegionIndex;
        private Coroutine invalidRoutine;
        private Coroutine failureRoutine;
        private float remainingSeconds;
        private float freezeRemainingSeconds;
        private int currentLevelIndex = LevelBoardPlan.FirstLevelIndex;
        private bool locked;

        private void Awake()
        {
            if (forcePortrait)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }

            Application.targetFrameRate = 60;
            BindSceneReferences();
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
                resetButton.onClick.AddListener(ResetBoard);
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
            LoadLevel(currentLevelIndex);
        }

        private void Update()
        {
            if (locked)
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
            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                remainingSeconds = 0f;
                UpdateTimerText();
                FailLevel();
                return;
            }

            UpdateTimerText();
        }

        private void OnDisable()
        {
            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(ShowHint);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetBoard);
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
            if (locked)
            {
                return;
            }

            int regionIndex = regionByCell[cell.Row, cell.Column];
            if (regionIndex >= 0)
            {
                pendingClear = true;
                pendingClearRegionIndex = regionIndex;
                selectionStart = cell;
                selectionEnd = cell;
                SetStatus("松手复原");
                return;
            }

            pendingClear = false;
            pendingClearRegionIndex = -1;
            selectionStart = cell;
            selectionEnd = cell;
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
                SetStatus("已复原");
                return;
            }

            RectInt rect = BuildSelectionRect();
            bool valid = RegionValidator.TryValidateLocal(level, rect, regions, out int clueId);
            if (valid)
            {
                int colorIndex = AllocateRegionColorIndex();
                if (colorIndex < 0)
                {
                    SetStatus("颜色数量不足");
                    return;
                }

                regions.Add(new PlayerRegion { clueId = clueId, colorIndex = colorIndex, rect = rect });
                regionViews.Add(CreatePlacedRegionView());
                ClearSelection();
                RebuildBoardVisuals();
                if (AllCellsFilled())
                {
                    if (RegionValidator.ValidateFinal(level, regions))
                    {
                        CompleteLevel();
                    }
                    else
                    {
                        SetStatus("当前划分不成立");
                    }
                }
                else
                {
                    SetStatus("已落子");
                }

                return;
            }

            ClearSelection();
            ShowInvalid(rect);
            SetStatus(level.strictMode ? "StrictMode: 不落子" : "区域不成立");
        }

        private void BindSceneReferences()
        {
            if (boardRoot == null)
            {
                boardRoot = FindComponentInScene<RectTransform>("Board");
            }

            if (boardRoot != null && boardMaxSize == Vector2.zero)
            {
                boardMaxSize = boardRoot.rect.size;
                if (boardMaxSize.x <= 0f || boardMaxSize.y <= 0f)
                {
                    boardMaxSize = boardRoot.sizeDelta;
                }
            }

            if (selectionPreview == null && boardRoot != null)
            {
                selectionPreview = boardRoot.GetComponentInChildren<SelectionPreviewView>(true);
            }

            EnsurePlacedRegionTemplate();

            if (completePanel == null)
            {
                completePanel = FindGameObjectInScene("NextPanel");
            }

            if (levelText == null)
            {
                levelText = FindLabelUnder("LevelPill");
            }

            if (nextLevelButton == null && completePanel != null)
            {
                nextLevelButton = FindOrCreateButton(completePanel.transform, "NextLevelButton");
                if (nextLevelButton == null)
                {
                    nextLevelButton = FindOrCreateButton(completePanel.transform, "FailureAddTime");
                }
            }

            ConnectNextLevelButton();
        }

        private void LoadLevel(int levelIndex)
        {
            currentLevelIndex = Mathf.Clamp(levelIndex, LevelBoardPlan.FirstLevelIndex, LevelBoardPlan.LastLevelIndex);
            level = GenerateLevel(currentLevelIndex);
            cellByPosition = new GridCellView[level.rows, level.columns];
            regionByCell = new int[level.rows, level.columns];
            BuildCells();
            remainingSeconds = levelSeconds;
            ResetBoard();
        }

        private LevelData GenerateLevel(int levelIndex)
        {
            Vector2Int boardSize = LevelBoardPlan.GetBoardSize(levelIndex);
            int rows = boardSize.x;
            int columns = boardSize.y;
            int seed = levelSeed + levelIndex * 10007;
            int maxRegionCount = regionColors != null ? regionColors.Length : 0;
            RegionAreaProfile areaProfile = LevelBoardPlan.GetRegionAreaProfile(levelIndex);

            if (LevelGenerator.TryGenerate(rows, columns, seed, areaProfile, maxRegionCount, generationAttempts, out LevelData generated))
            {
                return generated;
            }

            RegionAreaProfile fallbackProfile = LevelBoardPlan.GetFallbackRegionAreaProfile();
            if (LevelGenerator.TryGenerate(rows, columns, seed + 17, fallbackProfile, maxRegionCount, generationAttempts, out generated))
            {
                Debug.LogWarning("Level generation failed for the target profile. Using safe small-area fallback. rows=" + rows + ", columns=" + columns + ", maxRegionCount=" + maxRegionCount + ", allowedAreas=" + string.Join(",", areaProfile.AllowedAreas));
                return generated;
            }

            if (LevelGenerator.TryGenerateTiledFallback(rows, columns, seed + 31, fallbackProfile, maxRegionCount, out generated))
            {
                Debug.LogWarning("Level generation failed for random attempts. Using deterministic tiled fallback. rows=" + rows + ", columns=" + columns + ", maxRegionCount=" + maxRegionCount);
                return generated;
            }

            Debug.LogError("Level generation failed and no safe fallback was available. Using single-region fallback only because all safe paths failed. rows=" + rows + ", columns=" + columns);
            return LevelData.CreateSingleRegion(rows, columns, seed);
        }

        private void BuildCells()
        {
            ResolveCellTemplate();
            if (boardRoot == null || resolvedCellTemplate == null)
            {
                CacheCells();
                return;
            }

            ClearOldCells();
            if (resolvedCellTemplate.transform.parent == boardRoot)
            {
                resolvedCellTemplate.gameObject.SetActive(false);
            }

            EnsureSelectionPreview();
            EnsurePlacedRegionTemplate();
            cells = new GridCellView[level.rows * level.columns];
            Vector2 maxSize = boardMaxSize == Vector2.zero ? boardRoot.rect.size : boardMaxSize;
            float availableWidth = maxSize.x - boardPadding * 2f - cellGap * (level.columns - 1);
            float availableHeight = maxSize.y - boardPadding * 2f - cellGap * (level.rows - 1);
            float cellSize = Mathf.Floor(Mathf.Min(availableWidth / level.columns, availableHeight / level.rows));
            cellSize = Mathf.Max(1f, cellSize);
            float step = cellSize + cellGap;
            float totalWidth = level.columns * cellSize + (level.columns - 1) * cellGap;
            float totalHeight = level.rows * cellSize + (level.rows - 1) * cellGap;
            boardRoot.sizeDelta = new Vector2(totalWidth + boardPadding * 2f, totalHeight + boardPadding * 2f);

            for (int row = 0; row < level.rows; row++)
            {
                for (int column = 0; column < level.columns; column++)
                {
                    int index = row * level.columns + column;
                    GridCellView cell = Instantiate(resolvedCellTemplate, boardRoot);
                    cell.gameObject.name = "Cell_" + row + "_" + column;
                    cell.gameObject.SetActive(true);
                    cell.Configure(this, row, column);

                    RectTransform rect = (RectTransform)cell.transform;
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(cellSize, cellSize);
                    rect.anchoredPosition = new Vector2(
                        -totalWidth * 0.5f + cellSize * 0.5f + column * step,
                        totalHeight * 0.5f - cellSize * 0.5f - row * step);

                    cells[index] = cell;
                    cellByPosition[row, column] = cell;
                }
            }

            if (placedRegionsRoot != null)
            {
                placedRegionsRoot.SetAsLastSibling();
            }

            if (selectionPreview != null)
            {
                selectionPreview.transform.SetAsLastSibling();
            }
        }

        private void ResolveCellTemplate()
        {
            if (resolvedCellTemplate != null)
            {
                return;
            }

            if (cellPrefab != null)
            {
                resolvedCellTemplate = cellPrefab;
                return;
            }

            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != null)
                    {
                        resolvedCellTemplate = cells[i];
                        return;
                    }
                }
            }

            if (boardRoot != null)
            {
                resolvedCellTemplate = boardRoot.GetComponentInChildren<GridCellView>(true);
            }
        }

        private void ClearOldCells()
        {
            GridCellView[] oldCells = boardRoot.GetComponentsInChildren<GridCellView>(true);
            for (int i = oldCells.Length - 1; i >= 0; i--)
            {
                GridCellView oldCell = oldCells[i];
                if (oldCell == null || oldCell == resolvedCellTemplate)
                {
                    continue;
                }

                oldCell.gameObject.SetActive(false);
                Destroy(oldCell.gameObject);
            }
        }

        private void EnsureSelectionPreview()
        {
            if (selectionPreview == null && boardRoot != null)
            {
                Transform previewTransform = FindChildRecursive(boardRoot, "SelectionPreview");
                selectionPreview = previewTransform != null ? previewTransform.GetComponent<SelectionPreviewView>() : null;
            }

            if (selectionPreview != null)
            {
                selectionPreview.Initialize();
                selectionPreview.Hide();
            }
            else
            {
                Debug.LogWarning("SelectionPreview is missing under Board.");
            }
        }

        private void EnsurePlacedRegionTemplate()
        {
            if (boardRoot == null)
            {
                return;
            }

            if (placedRegionsRoot == null)
            {
                Transform root = FindChildRecursive(boardRoot, "PlacedRegions");
                placedRegionsRoot = root != null ? (RectTransform)root : null;
            }

            if (placedRegionTemplate == null && placedRegionsRoot != null)
            {
                placedRegionTemplate = placedRegionsRoot.GetComponentInChildren<PlacedRegionView>(true);
            }

            if (placedRegionTemplate != null)
            {
                placedRegionTemplate.Initialize();
                placedRegionTemplate.gameObject.SetActive(false);
            }
        }

        private void CacheCells()
        {
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                GridCellView cell = cells[i];
                if (cell == null || cell.Row < 0 || cell.Row >= level.rows || cell.Column < 0 || cell.Column >= level.columns)
                {
                    continue;
                }

                cell.Configure(this, cell.Row, cell.Column);
                cellByPosition[cell.Row, cell.Column] = cell;
            }
        }

        private void PreviewSelection()
        {
            if (selectionStart == null || selectionEnd == null)
            {
                return;
            }

            RectInt rect = BuildSelectionRect();
            bool valid = RegionValidator.TryValidateLocal(level, rect, regions, out int clueId);
            RebuildBoardVisuals();
            ShowSelectionPreview(rect, valid, clueId);
            SetStatus(valid ? "可落子" : "区域不成立");
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
            if (locked)
            {
                return;
            }

            ClueData clue = FindFirstUnfilledClue();
            SetStatus("提示: " + clue.value + " = " + clue.solutionHeight + "*" + clue.solutionWidth + " 行*列");
        }

        private void FreezeTimer()
        {
            if (locked || freezeRemainingSeconds > 0f || remainingSeconds <= 0f)
            {
                return;
            }

            freezeRemainingSeconds = freezeSeconds;
            UpdateFreezeButtonState();
        }

        private void RestartLevel()
        {
            remainingSeconds = levelSeconds;
            ResetBoard();
        }

        private void AdvanceToNextLevel()
        {
            if (!HasNextLevel())
            {
                SetStatus("已完成全部关卡");
                return;
            }

            LoadLevel(currentLevelIndex + 1);
        }

        private bool HasNextLevel()
        {
            return currentLevelIndex < LevelBoardPlan.LastLevelIndex;
        }

        private ClueData FindFirstUnfilledClue()
        {
            for (int clueId = 0; clueId < level.clues.Count; clueId++)
            {
                bool filled = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].clueId == clueId)
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
            locked = false;
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

            SetStatus("");
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
                    regionViews[regionIndex].Show(region.rect, cellByPosition, color, (region.rect.width * region.rect.height).ToString());
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
                    }
                }
            }

            if (stateBanner != null)
            {
                stateBanner.SetProgress(GetBoardProgress());
            }
        }

        private void CompleteLevel()
        {
            locked = true;
            freezeRemainingSeconds = 0f;
            ClearSelection();
            SetStatus("");
            UpdateFreezeButtonState();
            if (stateBanner != null)
            {
                stateBanner.SetProgress(1f);
                stateBanner.SetState(LevelBannerState.Completed);
            }

            if (completePanel != null)
            {
                completePanel.SetActive(true);
                completePanel.transform.SetAsLastSibling();
                ConnectNextLevelButton();
            }

            if (!HasNextLevel())
            {
                SetStatus("已完成全部关卡");
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

        private void UpdateTimerText()
        {
            if (timerText == null)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(remainingSeconds);
            int minutes = seconds / 60;
            int remainder = seconds % 60;
            timerText.text = minutes.ToString("00") + ":" + remainder.ToString("00");
        }

        private void UpdateFreezeButtonState()
        {
            if (freezeButton != null)
            {
                freezeButton.interactable = !locked && freezeRemainingSeconds <= 0f && remainingSeconds > 0f;
            }
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
            string label = Mathf.Max(0, rect.width * rect.height).ToString();
            selectionPreview.Show(rect, cellByPosition, fill, border, label);
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
                levelText.text = "Level " + currentLevelIndex;
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
            EnsurePlacedRegionTemplate();
            if (placedRegionsRoot == null || placedRegionTemplate == null)
            {
                Debug.LogWarning("PlacedRegionTemplate is missing under Board/PlacedRegions.");
                return null;
            }

            PlacedRegionView view = Instantiate(placedRegionTemplate, placedRegionsRoot);
            view.gameObject.name = "PlacedRegion_" + regionViews.Count;
            view.Initialize();
            view.gameObject.SetActive(true);
            return view;
        }

        private void DestroyRegionView(int regionIndex)
        {
            if (regionIndex < 0 || regionIndex >= regionViews.Count)
            {
                return;
            }

            if (regionViews[regionIndex] != null)
            {
                Destroy(regionViews[regionIndex].gameObject);
            }

            regionViews.RemoveAt(regionIndex);
        }

        private void ClearPlacedRegionViews()
        {
            for (int i = 0; i < regionViews.Count; i++)
            {
                if (regionViews[i] != null)
                {
                    Destroy(regionViews[i].gameObject);
                }
            }

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

            return null;
        }

        private void ClearSelection()
        {
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
                nextLevelButton = FindOrCreateButton(completePanel.transform, "NextLevelButton");
                if (nextLevelButton == null)
                {
                    nextLevelButton = FindOrCreateButton(completePanel.transform, "FailureAddTime");
                }
            }

            if (nextLevelButton == null)
            {
                return;
            }

            nextLevelButton.enabled = true;
            nextLevelButton.interactable = HasNextLevel();
            if (nextLevelButton.targetGraphic != null)
            {
                nextLevelButton.targetGraphic.raycastTarget = true;
            }

            nextLevelButton.onClick.RemoveListener(AdvanceToNextLevel);
            nextLevelButton.onClick.AddListener(AdvanceToNextLevel);
        }

        private static Button FindOrCreateButton(Transform root, string objectName)
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

            button = target.gameObject.AddComponent<Button>();
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
                button.targetGraphic = graphic;
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
