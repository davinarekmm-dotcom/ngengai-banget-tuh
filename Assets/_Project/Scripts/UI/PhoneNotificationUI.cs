using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Room0317.Apartment;
using Room0317.Player;
using Room0317.Audio;

namespace Room0317.UI
{
    /// <summary>
    /// UI-only phone notification pop-up featuring the Cracked Smartphone sprite.
    /// Displays an incoming SMS alert from 'Unknown Number' with screen vibration,
    /// audio chime/buzz, and click/key-to-unlock prompt before dialogue initiates.
    /// Pure screen UI — no physical object in the 2D world.
    /// </summary>
    public class PhoneNotificationUI : MonoBehaviour
    {
        public static PhoneNotificationUI Instance { get; private set; }

        private GameObject root;
        private RectTransform cardRt;
        private CanvasGroup canvasGroup;
        private Image phoneImage;
        private Outline notifOutline;
        private CanvasGroup notifBannerGroup;
        private Text senderText;
        private Text previewText;
        private Text promptText;

        private Sprite cachedPhoneSprite;
        private bool isShowing = false;
        private bool userDismissed = false;
        private float openedTime = -10f;
        private float lastClosedTime = -10f;

        public bool IsShowing => isShowing;
        public bool IsClosedRecently => (Time.unscaledTime - lastClosedTime) < 0.35f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildRuntimeUI();
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Root Screen Overlay (Dark dimming backdrop)
            root = new GameObject("PhoneNotification_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);

            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = root.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0.02f, 0.55f);
            bgImg.raycastTarget = true;

            var rootBtn = root.GetComponent<Button>();
            rootBtn.transition = Selectable.Transition.None;
            rootBtn.onClick.AddListener(OnUserAdvance);

            canvasGroup = root.GetComponent<CanvasGroup>();

            // 2. Centered Card Container (Phone + Notification)
            var cardGo = new GameObject("PhoneCard", typeof(RectTransform));
            cardGo.transform.SetParent(root.transform, false);
            cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, 15f);
            cardRt.sizeDelta = new Vector2(310f, 465f); // 2:3 aspect ratio matching Cracked Smartphone.png

            // 3. Cracked Smartphone Image
            var phoneImgGo = new GameObject("PhoneImage", typeof(RectTransform), typeof(Image));
            phoneImgGo.transform.SetParent(cardGo.transform, false);
            var phoneRt = (RectTransform)phoneImgGo.transform;
            phoneRt.anchorMin = Vector2.zero;
            phoneRt.anchorMax = Vector2.one;
            phoneRt.offsetMin = Vector2.zero;
            phoneRt.offsetMax = Vector2.zero;

            phoneImage = phoneImgGo.GetComponent<Image>();
            phoneImage.preserveAspect = true;
            phoneImage.raycastTarget = false;
            phoneImage.sprite = GetPhoneSprite();

            // 4. Notification Card (Overlaid on Phone Screen)
            var notifCardGo = new GameObject("NotificationBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
            notifCardGo.transform.SetParent(cardGo.transform, false);
            notifBannerGroup = notifCardGo.GetComponent<CanvasGroup>();
            var notifRt = (RectTransform)notifCardGo.transform;
            notifRt.anchorMin = new Vector2(0.5f, 0.5f);
            notifRt.anchorMax = new Vector2(0.5f, 0.5f);
            notifRt.pivot = new Vector2(0.5f, 0.5f);
            notifRt.anchoredPosition = new Vector2(0f, 35f);
            notifRt.sizeDelta = new Vector2(245f, 95f);

            var notifBg = notifCardGo.GetComponent<Image>();
            notifBg.color = new Color(0.06f, 0.06f, 0.09f, 0.94f);
            notifBg.raycastTarget = false;

            notifOutline = notifCardGo.GetComponent<Outline>();
            notifOutline.effectColor = new Color(1f, 0.28f, 0.28f, 0.65f);
            notifOutline.effectDistance = new Vector2(1f, -1f);

            // Header Row (Clock + Tag)
            var headerGo = new GameObject("HeaderRow", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(notifCardGo.transform, false);
            var headerText = headerGo.GetComponent<Text>();
            headerText.font = font;
            headerText.fontSize = 11;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(1f, 0.45f, 0.45f, 0.95f);
            headerText.text = "[ 03:17 AM ]  💬 INCOMING SMS";
            var headerRt = headerText.rectTransform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0f, 1f);
            headerRt.anchoredPosition = new Vector2(12f, -8f);
            headerRt.sizeDelta = new Vector2(-24f, 16f);

            // Sender Name (Unknown Number)
            var senderGo = new GameObject("SenderText", typeof(RectTransform), typeof(Text));
            senderGo.transform.SetParent(notifCardGo.transform, false);
            senderText = senderGo.GetComponent<Text>();
            senderText.font = font;
            senderText.fontSize = 17;
            senderText.fontStyle = FontStyle.Bold;
            senderText.color = new Color(0.98f, 0.98f, 0.98f, 1f);
            senderText.text = "Unknown Number";
            var senderRt = senderText.rectTransform;
            senderRt.anchorMin = new Vector2(0f, 1f);
            senderRt.anchorMax = new Vector2(1f, 1f);
            senderRt.pivot = new Vector2(0f, 1f);
            senderRt.anchoredPosition = new Vector2(12f, -28f);
            senderRt.sizeDelta = new Vector2(-24f, 22f);

            // Preview Snippet
            var previewGo = new GameObject("PreviewText", typeof(RectTransform), typeof(Text));
            previewGo.transform.SetParent(notifCardGo.transform, false);
            previewText = previewGo.GetComponent<Text>();
            previewText.font = font;
            previewText.fontSize = 13;
            previewText.fontStyle = FontStyle.Italic;
            previewText.color = new Color(0.85f, 0.85f, 0.88f, 0.92f);
            previewText.text = "\"You moved everything again.\"";
            var prevRt = previewText.rectTransform;
            prevRt.anchorMin = new Vector2(0f, 1f);
            prevRt.anchorMax = new Vector2(1f, 1f);
            prevRt.pivot = new Vector2(0f, 1f);
            prevRt.anchoredPosition = new Vector2(12f, -54f);
            prevRt.sizeDelta = new Vector2(-24f, 32f);

            // 5. Unlock Prompt
            var promptGo = new GameObject("UnlockPrompt", typeof(RectTransform), typeof(Text));
            promptGo.transform.SetParent(cardGo.transform, false);
            promptText = promptGo.GetComponent<Text>();
            promptText.font = font;
            promptText.fontSize = 12;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = new Color(1f, 0.9f, 0.65f, 0.95f);
            promptText.text = "[ TAP SCREEN OR PRESS SPACE / E TO UNLOCK ]";

            var promptRt = promptText.rectTransform;
            promptRt.anchorMin = new Vector2(0.5f, 0f);
            promptRt.anchorMax = new Vector2(0.5f, 0f);
            promptRt.pivot = new Vector2(0.5f, 0f);
            promptRt.anchoredPosition = new Vector2(0f, 30f);
            promptRt.sizeDelta = new Vector2(290f, 24f);

            root.SetActive(false);
        }

        private Sprite GetPhoneSprite()
        {
            if (cachedPhoneSprite != null) return cachedPhoneSprite;

#if UNITY_EDITOR
            try
            {
                var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Objects/Cracked Smartphone.png");
                if (spr != null)
                {
                    cachedPhoneSprite = spr;
                    return spr;
                }

                var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Objects/Cracked Smartphone.png");
                if (allAssets != null)
                {
                    foreach (var a in allAssets)
                    {
                        if (a is Sprite s)
                        {
                            cachedPhoneSprite = s;
                            return s;
                        }
                    }
                }
            }
            catch { }
#endif

            // Fallback: Read raw png bytes directly from disk
            try
            {
                string fullPath = System.IO.Path.Combine(Application.dataPath, "Objects/Cracked Smartphone.png");
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        cachedPhoneSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        return cachedPhoneSprite;
                    }
                }
            }
            catch { }

            return null;
        }

        private void Update()
        {
            if (!isShowing) return;
            if (Time.unscaledTime - openedTime < 0.28f) return;
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return;
            if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsShowing) return;
            if (EndingScreen.Instance != null && EndingScreen.Instance.IsShowing) return;

            if (CheckAdvanceInput())
            {
                OnUserAdvance();
            }
        }

        private void OnUserAdvance()
        {
            if (!isShowing) return;
            if (Time.unscaledTime - openedTime < 0.28f) return;
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return;
            if (GameOverScreen.Instance != null && GameOverScreen.Instance.IsShowing) return;
            if (EndingScreen.Instance != null && EndingScreen.Instance.IsShowing) return;
            userDismissed = true;
        }

        private static bool CheckAdvanceInput()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame)
                return true;

            return false;
        }

        /// <summary>
        /// Displays the phone notification pop-up before dialogue initiates.
        /// Yields until the player unlocks/reads or auto-dismiss triggers.
        /// </summary>
        public IEnumerator ShowNotificationRoutine(string sender, string preview, float autoDismissDelay = 3.5f)
        {
            while (isShowing) yield return null;

            if (root == null) BuildRuntimeUI();
            if (root == null) yield break;

            isShowing = true;
            userDismissed = false;
            openedTime = Time.unscaledTime;

            var player = FindFirstObjectByType<AdrianController>();
            bool prevCanMove = player != null && player.CanMove;
            if (player != null) player.SetCanMove(false);

            if (senderText != null) senderText.text = string.IsNullOrEmpty(sender) ? "Unknown Number" : sender;
            if (previewText != null) previewText.text = string.IsNullOrEmpty(preview) ? "1 New Message Received" : $"\"{preview}\"";

            if (phoneImage != null && phoneImage.sprite == null)
            {
                phoneImage.sprite = GetPhoneSprite();
            }

            // Play phone vibration and gently duck ambient audio to spotlight SMS alert
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAmbientDuck(0.42f, 0.25f);
                AudioManager.Instance.PlayPhoneVibration();
            }

            root.SetActive(true);
            Vector2 basePos = new Vector2(0f, 15f);
            cardRt.anchoredPosition = basePos;
            cardRt.localScale = new Vector3(0.75f, 0.75f, 1f);
            canvasGroup.alpha = 0f;
            if (notifBannerGroup != null) notifBannerGroup.alpha = 0f;

            // Spring Pop-in Animation
            float elapsed = 0f;
            float popDuration = 0.22f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(0.75f, 1.0f, eased);
                cardRt.localScale = new Vector3(scale, scale, 1f);
                canvasGroup.alpha = eased;
                if (notifBannerGroup != null)
                {
                    // Short anticipation beat before message banner reveals clearly
                    notifBannerGroup.alpha = Mathf.Clamp01((t - 0.35f) / 0.65f);
                }
                yield return null;
            }
            cardRt.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            if (notifBannerGroup != null) notifBannerGroup.alpha = 1f;

            // Vibration & waiting loop
            float timer = 0f;
            while (timer < autoDismissDelay && !userDismissed)
            {
                timer += Time.unscaledDeltaTime;

                // Restrained vibration shake for the first 0.65 seconds
                if (timer < 0.65f && ApartmentManager.ScreenShakeEnabled)
                {
                    float shakeX = UnityEngine.Random.Range(-3.0f, 3.0f);
                    float shakeY = UnityEngine.Random.Range(-3.0f, 3.0f);
                    cardRt.anchoredPosition = basePos + new Vector2(shakeX, shakeY);
                }
                else
                {
                    cardRt.anchoredPosition = basePos;
                }

                // Subtle supernatural crimson border pulse on Unknown Number alert
                if (notifOutline != null)
                {
                    float rimPulse = 0.35f + 0.50f * Mathf.PingPong(Time.unscaledTime * 2.4f, 1f);
                    notifOutline.effectColor = new Color(1f, 0.22f, 0.22f, rimPulse);
                }

                // Smooth prompt pulsing
                if (promptText != null)
                {
                    float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                    promptText.color = new Color(1f, 0.9f, 0.65f, pulse);
                }

                yield return null;
            }

            cardRt.anchoredPosition = basePos;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayInspectClose();
                AudioManager.Instance.SetAmbientDuck(1.0f, 0.55f);
            }

            // Smooth exit animation
            elapsed = 0f;
            float fadeOutDuration = 0.18f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                canvasGroup.alpha = 1f - t;
                float scale = Mathf.Lerp(1f, 0.92f, t);
                cardRt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            root.SetActive(false);
            lastClosedTime = Time.unscaledTime;
            isShowing = false;

            bool storyActive = Room0317.Story.StoryProgressionCoordinator.Instance != null && Room0317.Story.StoryProgressionCoordinator.Instance.IsStorySequenceActive;
            var dlg = Room0317.Dialogue.DialogueBox.ActiveInstance;
            bool dialogueActive = dlg != null && dlg.IsOpen;
            if (player != null && prevCanMove && !storyActive && !dialogueActive)
            {
                player.SetCanMove(true);
            }
        }
    }
}
