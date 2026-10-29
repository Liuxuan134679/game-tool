using System;
using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    // All objects are authored in Release.unity. Only responsive layout and state change at runtime.
    public sealed class ReleaseUI : MonoBehaviour
    {
        [Header("Layout")]
        public Canvas canvas;
        public RectTransform root, boardSlot, header, tools, details, logo, timerPill, progressTrack;
        public Text subtitle, rule, progress;
        public Image progressFill;
        [Header("Game")]
        public Text Status, Timer, Level;
        public Button Hint, Restart, Freeze, Retry, Next, help, pause, sound, resume, tutorialDone, confirm, cancel;
        public GameObject soundOff;
        [Header("Onboarding")]
        public RectTransform tutorialHand;
        public Button skipTutorial;
        [Header("Panels")]
        public GameObject Failure, Complete, Help, Pause, ConfirmRestart;
        public RectTransform pauseCard, helpCard, completeCard, failureCard, restartCard;
        public Text completeTitle, completeBody, completeTime;
        public CanvasGroup completeFade;
        private Action relayout;
        private float completionReveal = 1, completeCardScale = 1;
        private int lastWidth, lastHeight;
        private Rect lastSafe;
        private int timerFontSize;
        public Vector2 BoardSpace => boardSlot.rect.size;

        public void Initialize(Action onLayout, Action onHelp, Action onPause, Action onResume,
            Action onTutorialDone, Action onSound, Action onRestart, Action onCancelRestart, Action onSkipTutorial)
        {
            relayout = onLayout;
            timerFontSize = Timer.fontSize;
            skipTutorial.gameObject.SetActive(false);
            tutorialHand.gameObject.SetActive(false);
            skipTutorial.onClick.AddListener(() => onSkipTutorial());
            help.onClick.AddListener(() => onHelp());
            pause.onClick.AddListener(() => onPause());
            resume.onClick.AddListener(() => onResume());
            tutorialDone.onClick.AddListener(() => onTutorialDone());
            sound.onClick.AddListener(() => onSound());
            confirm.onClick.AddListener(() => onRestart());
            cancel.onClick.AddListener(() => onCancelRestart());
            Tick(true);
        }

        public void Tick(bool force = false)
        {
            if (!force && lastWidth == Screen.width && lastHeight == Screen.height && lastSafe == Screen.safeArea) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            bool portrait = Screen.width < Screen.height * 1.12f;
            canvas.scaleFactor = Mathf.Max(.1f, Mathf.Min(Screen.width / (portrait ? 390f : 900f), Screen.height / (portrait ? 780f : 540f)));
            Canvas.ForceUpdateCanvases();
            float w = root.rect.width, h = root.rect.height;
            float top = Mathf.Max(16, (Screen.height - Screen.safeArea.yMax) / canvas.scaleFactor + 10);
            float bottom = Mathf.Max(16, Screen.safeArea.yMin / canvas.scaleFactor + 10);
            float available = h - top - bottom;
            float contentWidth = Mathf.Min(w - 36, portrait ? 480 : 1060);
            float left = (w - contentWidth) * .5f;
            Place(header, left, -top, contentWidth, portrait ? 110 : 62);
            Place(logo, 0, 0, portrait ? 254 : 265, 35);
            Place(subtitle.transform, 1, -40, 235, 18);
            Place(pause.transform, contentWidth - 46, 0, 46, 46);
            Place(help.transform, 0, -76, 154, 38);
            Place(sound.transform, contentWidth - 148, -76, 148, 38);
            Place(skipTutorial.transform, contentWidth - 118, portrait ? -42 : -48, 118, 30);
            if (!portrait)
            {
                Place(help.transform, contentWidth - 372, 0, 154, 44);
                Place(sound.transform, contentWidth - 206, 0, 148, 44);
            }
            if (portrait)
            {
                Place(details, left, -(top + 132), contentWidth, 66);
                Place(Level.transform, 0, 0, contentWidth - 116, 32);
                Place(timerPill, contentWidth - 112, -1, 112, 36);
                rule.gameObject.SetActive(false);
                float boardHeight = Mathf.Max(100, Mathf.Min(contentWidth, available - 348));
                Place(boardSlot, left, -(top + 210), contentWidth, boardHeight);
                Place(Status.transform, left, -(top + 210 + boardHeight + 22), contentWidth, 46);
                Place(tools, left, -(h - bottom - 76), contentWidth, 76);
                float bw = (contentWidth - 20) / 3;
                Place(Hint.transform, 0, 0, bw, 76);
                Place(Restart.transform, bw + 10, 0, bw, 76);
                Place(Freeze.transform, (bw + 10) * 2, 0, bw, 76);
            }
            else
            {
                float side = 276, gap = 32;
                float boardHeight = Mathf.Max(120, available - 86);
                float boardWidth = Mathf.Min(contentWidth - side - gap, boardHeight);
                float start = (w - boardWidth - gap - side) * .5f;
                float infoX = start + boardWidth + gap;
                Place(boardSlot, start, -(top + 80), boardWidth, boardHeight);
                Place(details, infoX, -(top + 80), side, 242);
                Place(Level.transform, 0, 0, side, 34);
                Place(timerPill, 0, -53, 150, 46);
                rule.gameObject.SetActive(true);
                Place(rule.transform, 0, -162, side, 68);
                Place(tools, infoX, -(h - bottom - 76), side, 76);
                float bw = (side - 16) / 3;
                Place(Hint.transform, 0, 0, bw, 76);
                Place(Restart.transform, bw + 8, 0, bw, 76);
                Place(Freeze.transform, (bw + 8) * 2, 0, bw, 76);
                Place(Status.transform, infoX, -(h - bottom - 134), side, 48);
            }
            foreach (var card in new[] { helpCard, pauseCard, completeCard, failureCard, restartCard })
            {
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
                card.anchoredPosition = Vector2.zero;
                float scale = Mathf.Min(1, (w - 28) / card.sizeDelta.x, available / card.sizeDelta.y);
                card.localScale = Vector3.one * Mathf.Max(.1f, scale);
                if (card == completeCard) completeCardScale = card.localScale.x;
            }
            SetCompletionReveal(completionReveal);
            Canvas.ForceUpdateCanvases();
            relayout?.Invoke();
            LayoutProgress();
        }

        public void SetProgress(float value)
        {
            float amount = Mathf.Clamp01(value);
            progress.text = Mathf.RoundToInt(amount * 100) + "%";
            progressFill.enabled = amount > 0;
            progressFill.rectTransform.anchorMax = new Vector2(amount, 1);
            LayoutProgress();
        }
        private void LayoutProgress()
        {
            bool portrait = Screen.width < Screen.height * 1.12f;
            float width = portrait ? Mathf.Min(details.rect.width, ((RectTransform)boardSlot.GetChild(0)).rect.width) : details.rect.width;
            float inset = portrait ? (details.rect.width - width) * .5f : 0;
            float top = portrait ? -48 : -122;
            Place(progressTrack, inset, top, width - 60, 12);
            Place(progress.transform, inset + width - 48, top + 8, 48, 28);
        }
        public void SetFreeze(bool used, float remaining)
        { SetButtonText(Freeze, remaining > 0 ? "Frozen " + Mathf.CeilToInt(remaining) : used ? "Used" : "Freeze (1)"); }
        public void SetTimerCompact(bool active)
        { Timer.fontSize = active ? 16 : timerFontSize; }
        public void SetMuted(bool muted)
        { SetButtonText(sound, muted ? "Sound off" : "Sound on"); soundOff.SetActive(muted); }
        public void SetCompletion(bool last, float seconds)
        {
            completeTitle.fontSize = last ? 23 : 28;
            completeTitle.text = last ? "All " + (LevelBoardPlan.LastLevelIndex + 1) + " puzzles complete!" : "Level complete!";
            completeBody.text = last ? "You found every pair." : "A perfect fit.";
            completeTime.text = seconds > 0 ? "Quick finish" : "Puzzle complete";
            SetButtonText(Next, last ? "Play again" : "Next puzzle");
        }
        public void SetCompletionReveal(float amount)
        {
            completionReveal = Mathf.Clamp01(amount);
            completeFade.alpha = completionReveal;
            completeCard.localScale = Vector3.one * (completeCardScale * Mathf.Lerp(.95f, 1, completionReveal));
            Next.enabled = completionReveal >= 1;
        }
        public void HideAuxiliary() { Help.SetActive(false); Pause.SetActive(false); ConfirmRestart.SetActive(false); }
        public static void Show(GameObject panel) { panel.SetActive(true); panel.transform.SetAsLastSibling(); }
        public static void SetButtonText(Button button, string value)
        { var text = button.transform.Find("Label").GetComponent<Text>(); if (text.text != value) text.text = value; }
        private static void Place(Transform transform, float x, float y, float width, float height)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
