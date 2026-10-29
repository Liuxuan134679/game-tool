using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    public sealed class SelectionPreviewView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image fill;
        [SerializeField] private Image frame;
        [SerializeField] private RectTransform labelRoot;
        [SerializeField] private Text labelText;
        [SerializeField] private float fillInset = 5f;
        [SerializeField] private float labelOverlap = 0.32f;
        [SerializeField] private Text[] previewClues;

        public void Initialize()
        {
            if (root == null)
            {
                root = (RectTransform)transform;
            }

            if (fill == null)
            {
                Transform fillTransform = root.Find("Fill");
                fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            }

            if (frame == null)
            {
                Transform frameTransform = root.Find("Frame");
                frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
            }

            if (labelRoot == null)
            {
                Transform labelTransform = root.Find("Label");
                labelRoot = labelTransform != null ? (RectTransform)labelTransform : null;
            }

            if (labelText == null && labelRoot != null)
            {
                labelText = labelRoot.GetComponentInChildren<Text>(true);
            }

            SetRaycastTarget(false);
            fillInset = -2f;
            if (fill != null) fill.type = Image.Type.Sliced;
            if (frame != null) { frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 32; }
            Hide();
        }

        public void Show(RectInt rect, GridCellView[,] cells, Color fillColor, Color borderColor, string label)
        {
            if (root == null)
            {
                Initialize();
            }

            if (!TryGetLocalBounds(rect, cells, out Vector2 center, out Vector2 size))
            {
                Hide();
                return;
            }

            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = center;
            root.sizeDelta = size;

            cells[rect.yMin, rect.xMin].MatchSurface(fill, false);
            LayoutFill(fillColor);
            LayoutFrame(borderColor);
            LayoutLabel(size, label);
            ShowClues(rect, cells, center);
        }

        private void ShowClues(RectInt rect, GridCellView[,] cells, Vector2 center)
        {
            // Keep the original clue legible above the translucent selection surface.
            int count = 0;
            for (int row = rect.yMin; row < rect.yMax; row++)
                for (int column = rect.xMin; column < rect.xMax; column++)
                {
                    Text source = cells[row, column].ClueLabel;
                    if (source == null || string.IsNullOrEmpty(source.text)) continue;
                    if (count >= previewClues.Length) throw new System.InvalidOperationException("Selection clue pool is too small.");
                    Text clue = previewClues[count++];
                    clue.gameObject.SetActive(true);
                    clue.text = source.text; clue.font = source.font;
                    clue.fontSize = source.fontSize; clue.fontStyle = source.fontStyle;
                    clue.color = source.color; clue.raycastTarget = false;
                    clue.resizeTextForBestFit = false;
                    var cellRect = (RectTransform)cells[row, column].transform;
                    var clueRect = clue.rectTransform;
                    clueRect.anchorMin = clueRect.anchorMax = clueRect.pivot = new Vector2(.5f, .5f);
                    clueRect.anchoredPosition = cellRect.anchoredPosition - center;
                    clueRect.sizeDelta = cellRect.sizeDelta;
                    clueRect.localScale = source.rectTransform.localScale;
                    clueRect.SetAsLastSibling();
                }
            for (int i = count; i < previewClues.Length; i++) previewClues[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private bool TryGetLocalBounds(RectInt rect, GridCellView[,] cells, out Vector2 center, out Vector2 size)
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

        private void LayoutFill(Color fillColor)
        {
            if (fill == null)
            {
                return;
            }

            fill.color = fillColor;
            RectTransform fillRect = fill.rectTransform;
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(fillInset, fillInset);
            fillRect.offsetMax = new Vector2(-fillInset, -fillInset);
        }

        private void LayoutFrame(Color borderColor)
        {
            if (frame == null)
            {
                return;
            }

            frame.color = new Color(1f, 1f, 1f, borderColor.a);
            Stretch(frame.rectTransform);
        }

        private void LayoutLabel(Vector2 size, string label)
        {
            if (labelRoot == null)
            {
                return;
            }

            bool showLabel = !string.IsNullOrEmpty(label);
            labelRoot.gameObject.SetActive(showLabel);
            if (!showLabel)
            {
                return;
            }

            labelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            labelRoot.sizeDelta = new Vector2(48, 32);
            labelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            labelRoot.pivot = new Vector2(0.5f, 0.5f);
            labelRoot.anchoredPosition = new Vector2(0f, size.y * 0.5f + labelRoot.sizeDelta.y * (0.5f - labelOverlap));
            labelRoot.SetAsLastSibling();

            if (labelText != null)
            {
                labelText.text = label;
                labelText.fontSize = 18;
            }
        }

        private void SetRaycastTarget(bool value)
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = value;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
