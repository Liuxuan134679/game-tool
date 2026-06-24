using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shud
{
    public sealed class GridCellView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private FirstVersionGame game;
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private float clueFontScale = 1.35f;

        private int normalFontSize;

        public int Row => row;
        public int Column => column;

        public void Configure(FirstVersionGame owner, int rowIndex, int columnIndex)
        {
            game = owner;
            row = rowIndex;
            column = columnIndex;

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }

            if (label != null && normalFontSize <= 0)
            {
                normalFontSize = label.fontSize;
            }
        }

        public void SetText(string value)
        {
            SetText(value, !string.IsNullOrEmpty(value));
        }

        public void SetText(string value, bool emphasize)
        {
            if (label != null)
            {
                label.text = value;
                if (normalFontSize <= 0)
                {
                    normalFontSize = label.fontSize;
                }

                label.fontSize = emphasize
                    ? Mathf.RoundToInt(normalFontSize * clueFontScale)
                    : normalFontSize;
            }
        }

        public void SetColor(Color color)
        {
            if (background != null)
            {
                background.color = color;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (game != null)
            {
                game.BeginSelection(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (game != null)
            {
                game.UpdateSelectionFromScreen(eventData.position, eventData.pressEventCamera);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (game == null)
            {
                return;
            }

            game.UpdateSelectionFromScreen(eventData.position, eventData.pressEventCamera);
            game.CommitSelection();
        }

        private void Reset()
        {
            background = GetComponent<Image>();
            label = GetComponentInChildren<Text>();
            game = FindObjectOfType<FirstVersionGame>();
        }
    }
}
