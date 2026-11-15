using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Room0317.Apartment
{
    /// <summary>
    /// Handles smooth black screen fades for door transitions and scene changes.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fadeImage;

        public bool IsFading { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (fadeImage == null)
            {
                fadeImage = GetComponent<Image>();
                if (fadeImage == null) fadeImage = gameObject.AddComponent<Image>();
                fadeImage.color = Color.black;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        public void Configure(CanvasGroup cg, Image img)
        {
            canvasGroup = cg;
            fadeImage = img;
            if (fadeImage != null) fadeImage.color = Color.black;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public IEnumerator FadeOut(float duration = 0.2f)
        {
            EnsureComponents();
            IsFading = true;
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            IsFading = false;
        }

        public IEnumerator FadeIn(float duration = 0.2f)
        {
            EnsureComponents();
            IsFading = true;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            IsFading = false;
        }

        public void SetInstantBlack()
        {
            EnsureComponents();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        public void SetInstantClear()
        {
            EnsureComponents();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
