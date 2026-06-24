using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    public sealed class PlacedRegionView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image fill;
        [SerializeField] private Text labelText;

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
        }

        public void Show(RectInt rect, GridCellView[,] cells, Color color, string label)
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

            if (fill != null)
            {
                fill.color = color;
            }

            if (labelText != null)
            {
                labelText.text = label;
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
