using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shud
{
    public sealed class GridCellView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
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
        public int ClueFontSize => label != null ? label.fontSize : 20;
        public Text ClueLabel => label;

        public void MatchSurface(Image target, bool includeShadow)
        {
            if (target == null || background == null || background.sprite == null) return;
            target.sprite = background.sprite;
            target.type = Image.Type.Sliced;
            target.preserveAspect = false;
            // Simple cell art scales with the cell. Keep that exact corner scale on larger surfaces.
            float cellWidth = background.rectTransform.rect.width;
            float referencePpu = target.canvas != null ? target.canvas.referencePixelsPerUnit : 100f;
            target.pixelsPerUnitMultiplier = background.sprite.rect.width / Mathf.Max(1f, cellWidth)
                * referencePpu / background.sprite.pixelsPerUnit;
            if (!includeShadow) return;
            var sourceShadow = background.GetComponent<Shadow>();
            if (sourceShadow == null) return;
            var targetShadow = target.GetComponent<Shadow>();
            if (targetShadow == null) return;
            targetShadow.effectColor = sourceShadow.effectColor;
            targetShadow.effectDistance = sourceShadow.effectDistance;
            targetShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
        }

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

        public void SetReleaseFontSize(int size)
        {
            if (label == null) return;
            clueFontScale = 1f; normalFontSize = size; label.fontSize = size;
            label.resizeTextForBestFit = false;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = false; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (game != null)
            {
                if (eventData.button == PointerEventData.InputButton.Left) game.PointerDown(this, eventData.pointerId);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (game != null)
            {
                game.PointerDrag(eventData.pointerId, eventData.position, eventData.pressEventCamera);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (game == null)
            {
                return;
            }

            game.PointerUp(eventData.pointerId, eventData.position, eventData.pressEventCamera);
        }

        private void Reset()
        {
            background = GetComponent<Image>();
            label = GetComponentInChildren<Text>();
            game = FindObjectOfType<FirstVersionGame>();
        }
    }
}
