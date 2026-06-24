using UnityEngine;
using UnityEngine.UI;

namespace Shud
{
    public enum LevelBannerState
    {
        Playing,
        Completed,
        Failed
    }

    public sealed class LevelStateBanner : MonoBehaviour
    {
        [SerializeField] private Image bannerImage;
        [SerializeField] private Sprite playingSprite;
        [SerializeField] private Sprite completedSprite;
        [SerializeField] private Sprite failedSprite;
        [SerializeField] private Image progressFill;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Text dotsText;
        [SerializeField] private float dotStepSeconds = 0.35f;

        private LevelBannerState state;
        private float dotTimer;
        private int dotCount = 1;

        public void SetState(LevelBannerState nextState)
        {
            state = nextState;

            if (bannerImage != null)
            {
                if (state == LevelBannerState.Playing)
                {
                    bannerImage.sprite = playingSprite;
                }
                else if (state == LevelBannerState.Completed)
                {
                    bannerImage.sprite = completedSprite;
                }
                else
                {
                    bannerImage.sprite = failedSprite;
                }
            }

            bool showPlayingParts = state == LevelBannerState.Playing;
            if (progressRoot != null)
            {
                progressRoot.SetActive(showPlayingParts);
            }

            if (dotsText != null)
            {
                dotsText.gameObject.SetActive(showPlayingParts);
                dotsText.text = ".";
            }

            dotTimer = 0f;
            dotCount = 1;
        }

        public void SetProgress(float value)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(value);
            }
        }

        private void Update()
        {
            if (state != LevelBannerState.Playing || dotsText == null)
            {
                return;
            }

            dotTimer += Time.deltaTime;
            if (dotTimer < dotStepSeconds)
            {
                return;
            }

            dotTimer = 0f;
            dotCount++;
            if (dotCount > 3)
            {
                dotCount = 1;
            }

            dotsText.text = new string('.', dotCount);
        }
    }
}
