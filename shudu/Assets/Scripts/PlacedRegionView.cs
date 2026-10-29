using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    public sealed class PlacedRegionView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image fill;
        [SerializeField] private Text labelText;
        [SerializeField] private Text[] clueLabels;
        private readonly Vector2[] cluePositions = new Vector2[2];
        private const float RevealSeconds = .2f;
        private static readonly Color Ink = new Color(.055f, .09f, .10f, 1);
        private Color targetColor;
        private float revealStartedAt = -1;

        public void Initialize()
        {
            if (root == null)
            {
                root = (RectTransform)transform;
            }

            if (fill == null)
            {
                Transform fillTransform = root.Find("Fill");
                fill = fillTransform != null ? fillTransform.GetComponent<Image>() : GetComponentInChildren<Image>(true);
            }

            if (labelText == null)
            {
                labelText = GetComponentInChildren<Text>(true);
            }

            SetRaycastTarget(false);
            if (fill != null)
            {
                // Match the single-cell artwork; Show supplies its current corner scale.
                fill.type = Image.Type.Sliced;
                fill.preserveAspect = false;
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = Vector2.one * -2f;
                fill.rectTransform.offsetMax = Vector2.one * 2f;
            }
            if (labelText != null) labelText.resizeTextForBestFit = false;
        }

        public void Show(RectInt rect, GridCellView[,] cells, Color color, LevelData level)
        {
            if (root == null)
            {
                Initialize();
            }

            if (!TryGetLocalBounds(rect, cells, out Vector2 center, out Vector2 size))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = center;
            root.sizeDelta = size;

            GridCellView clueCell = cells[rect.yMin, rect.xMin];
            if (fill != null)
            {
                clueCell.MatchSurface(fill, false);
                targetColor = color;
            }

            if (labelText != null)
            {
                int index = 0, sum = 0;
                foreach (var clue in level.clues)
                {
                    if (!RegionValidator.Contains(rect, clue.row, clue.column)) continue;
                    if (index >= clueLabels.Length) break;
                    var text = clueLabels[index];
                    var cell = cells[clue.row, clue.column];
                    var clueRect = (RectTransform)cell.transform;
                    sum += clue.value;
                    text.text = clue.value.ToString();
                    text.fontSize = cell.ClueFontSize;
                    text.alignment = TextAnchor.MiddleCenter;
                    var labelRect = text.rectTransform;
                    labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
                    cluePositions[index++] = clueRect.anchoredPosition - center;
                    labelRect.sizeDelta = clueRect.sizeDelta;
                    labelRect.localScale = Vector3.one;
                }
                labelText.gameObject.SetActive(true);
                labelText.text = sum.ToString();
                labelText.fontSize = Mathf.RoundToInt(clueCell.ClueFontSize * 1.15f);
                labelText.alignment = TextAnchor.MiddleCenter;
                var sumRect = labelText.rectTransform;
                sumRect.anchorMin = sumRect.anchorMax = sumRect.pivot = new Vector2(.5f, .5f);
                sumRect.anchoredPosition = Vector2.zero;
                sumRect.sizeDelta = size;
            }
            ApplyReveal();
        }

        public void Hide()
        {
            revealStartedAt = -1;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        public void Reveal() { revealStartedAt = Time.unscaledTime; ApplyReveal(); }
        private void Update()
        {
            if (revealStartedAt < 0) return;
            ApplyReveal();
            if (Time.unscaledTime - revealStartedAt >= RevealSeconds) revealStartedAt = -1;
        }
        private void ApplyReveal()
        {
            float progress = revealStartedAt < 0 ? 1 : Mathf.Clamp01((Time.unscaledTime - revealStartedAt) / RevealSeconds);
            if (fill != null) fill.color = Color.Lerp(Color.Lerp(Color.white, targetColor, .55f), targetColor, progress);
            float merge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .7f));
            for (int i = 0; i < clueLabels.Length; i++)
            {
                if (clueLabels[i] == null) continue;
                clueLabels[i].gameObject.SetActive(merge < 1);
                clueLabels[i].rectTransform.anchoredPosition = Vector2.Lerp(cluePositions[i], Vector2.zero, merge);
                clueLabels[i].color = new Color(Ink.r, Ink.g, Ink.b, 1 - merge);
            }
            if (labelText != null)
            {
                float appear = Mathf.Clamp01((progress - .55f) / .45f);
                labelText.color = new Color(Ink.r, Ink.g, Ink.b, appear);
                labelText.rectTransform.localScale = Vector3.one * Mathf.Lerp(.9f, 1, appear);
            }
        }

        private static bool TryGetLocalBounds(RectInt rect, GridCellView[,] cells, out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero;
            size = Vector2.zero;

            if (cells == null || rect.width <= 0 || rect.height <= 0)
            {
                return false;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int row = rect.yMin; row < rect.yMax; row++)
            {
                for (int column = rect.xMin; column < rect.xMax; column++)
                {
                    if (row < 0 || row >= cells.GetLength(0) || column < 0 || column >= cells.GetLength(1) || cells[row, column] == null)
                    {
                        return false;
                    }

                    RectTransform cellRect = (RectTransform)cells[row, column].transform;
                    Vector2 position = cellRect.anchoredPosition;
                    Vector2 cellSize = cellRect.sizeDelta;
                    minX = Mathf.Min(minX, position.x - cellSize.x * cellRect.pivot.x);
                    maxX = Mathf.Max(maxX, position.x + cellSize.x * (1f - cellRect.pivot.x));
                    minY = Mathf.Min(minY, position.y - cellSize.y * cellRect.pivot.y);
                    maxY = Mathf.Max(maxY, position.y + cellSize.y * (1f - cellRect.pivot.y));
                }
            }

            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            size = new Vector2(maxX - minX, maxY - minY);
            return true;
        }

        private void SetRaycastTarget(bool value)
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = value;
            }
        }
    }
}
