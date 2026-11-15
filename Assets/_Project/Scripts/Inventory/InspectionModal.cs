using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Room0317.Audio;
using Room0317.Player;

namespace Room0317.Inventory
{
    /// <summary>
    /// Cinematic close-up inspection viewer for clues and evidence in ROOM 03:17.
    /// When triggered:
    /// 1. The background blurs into dark frosted depth-of-field vignette.
    /// 2. The clue object pops up onto the player's screen with an elastic spring animation.
    /// 3. Displays detailed psychological lore, the object description, and clear guidance
    ///    on how clues are collected and accessed in the Evidence Bar.
    /// </summary>
    public class InspectionModal : MonoBehaviour
    {
        private static InspectionModal _instance;
        public static InspectionModal Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<InspectionModal>();
                    if (_instance == null)
                    {
                        var go = new GameObject("InspectionModal_Manager");
                        _instance = go.AddComponent<InspectionModal>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("UI Hierarchy")]
        [SerializeField] private GameObject root;
        [SerializeField] private Image previewImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text clueGuideText;

        private RectTransform cardBoxRt;
        private RectTransform previewImageRt;
        private CanvasGroup cardCanvasGroup;
        private Image backgroundVignette;

        private Action onCollectCallback;
        private Action onCloseCallback;
        private bool isCollectible;
        private bool isClosingOrCollecting = false;
        private AdrianController player;
        private Coroutine activePopRoutine;
        private Coroutine collectRoutine;
        private Coroutine closeRoutine;
        private float lastClosedTime = -10f;
        private float openedTime = -10f;

        // Flip Mechanics (e.g. flipping picture frame to reveal hidden note)
        private bool isFlippable = false;
        private bool isFlipped = false;
        private Sprite frontSprite;
        private string frontTitle;
        private string frontDesc;
        private Sprite backSprite;
        private string backTitle;
        private string backDesc;
        private Action onFlippedCallback;
        private GameObject flipButtonGo;
        private Text flipButtonText;
        private Coroutine flipRoutine;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsClosedRecently => (Time.unscaledTime - lastClosedTime) < 0.35f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (root == null)
            {
                BuildRuntimeUI();
            }
            else
            {
                root.SetActive(false);
            }
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Modal Root - Full screen overlay
            root = new GameObject("InspectionModal_Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Blur & Frosted Vignette Background
            backgroundVignette = root.GetComponent<Image>();
            var vignetteTex = CreateVignetteTexture();
            var vignetteSprite = Sprite.Create(vignetteTex, new Rect(0, 0, vignetteTex.width, vignetteTex.height), new Vector2(0.5f, 0.5f));
            backgroundVignette.sprite = vignetteSprite;
            backgroundVignette.color = new Color(0.015f, 0.015f, 0.025f, 0.94f);

            // Container Box with Spring Scaling
            var boxGo = new GameObject("CardBox", typeof(RectTransform), typeof(CanvasGroup));
            boxGo.transform.SetParent(root.transform, false);
            cardBoxRt = (RectTransform)boxGo.transform;
            cardBoxRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardBoxRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardBoxRt.pivot = new Vector2(0.5f, 0.5f);
            cardBoxRt.sizeDelta = new Vector2(920f, 780f);
            cardBoxRt.anchoredPosition = Vector2.zero;

            cardCanvasGroup = boxGo.GetComponent<CanvasGroup>();

            // Title Text
            var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(boxGo.transform, false);
            titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.94f, 0.75f);
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(0f, 44f);

            // Preview Image Frame
            var imgGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(boxGo.transform, false);
            previewImage = imgGo.GetComponent<Image>();
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            previewImageRt = previewImage.rectTransform;
            previewImageRt.anchorMin = new Vector2(0.5f, 0.5f);
            previewImageRt.anchorMax = new Vector2(0.5f, 0.5f);
            previewImageRt.pivot = new Vector2(0.5f, 0.5f);
            previewImageRt.anchoredPosition = new Vector2(0f, 75f);
            previewImageRt.sizeDelta = new Vector2(620f, 380f);

            // Description Box
            var descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(Text));
            descGo.transform.SetParent(boxGo.transform, false);
            descriptionText = descGo.GetComponent<Text>();
            descriptionText.font = font;
            descriptionText.fontSize = 20;
            descriptionText.lineSpacing = 1.25f;
            descriptionText.color = new Color(0.92f, 0.92f, 0.88f);
            descriptionText.alignment = TextAnchor.MiddleCenter;
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            var descRt = descriptionText.rectTransform;
            descRt.anchorMin = new Vector2(0.06f, 0f);
            descRt.anchorMax = new Vector2(0.94f, 0f);
            descRt.pivot = new Vector2(0.5f, 0f);
            descRt.anchoredPosition = new Vector2(0f, 130f);
            descRt.sizeDelta = new Vector2(0f, 85f);

            // Clue Collection Guidance Box (Highlights how clues work)
            var guideGo = new GameObject("ClueGuideCard", typeof(RectTransform), typeof(Image));
            guideGo.transform.SetParent(boxGo.transform, false);
            var guideRt = (RectTransform)guideGo.transform;
            guideRt.anchorMin = new Vector2(0.06f, 0f);
            guideRt.anchorMax = new Vector2(0.94f, 0f);
            guideRt.pivot = new Vector2(0.5f, 0f);
            guideRt.anchoredPosition = new Vector2(0f, 50f);
            guideRt.sizeDelta = new Vector2(0f, 70f);

            var guideBg = guideGo.GetComponent<Image>();
            guideBg.color = new Color(0.10f, 0.12f, 0.18f, 0.92f);

            var guideTextGo = new GameObject("GuideText", typeof(RectTransform), typeof(Text));
            guideTextGo.transform.SetParent(guideGo.transform, false);
            clueGuideText = guideTextGo.GetComponent<Text>();
            clueGuideText.font = font;
            clueGuideText.fontSize = 16;
            clueGuideText.alignment = TextAnchor.MiddleCenter;
            clueGuideText.color = new Color(0.9f, 0.88f, 0.8f);

            var guideTextRt = clueGuideText.rectTransform;
            guideTextRt.anchorMin = Vector2.zero;
            guideTextRt.anchorMax = Vector2.one;
            guideTextRt.offsetMin = new Vector2(10f, 4f);
            guideTextRt.offsetMax = new Vector2(-10f, -4f);

            // Action / Hotkey Hint Prompt
            var hintGo = new GameObject("HintText", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(boxGo.transform, false);
            hintText = hintGo.GetComponent<Text>();
            hintText.font = font;
            hintText.fontSize = 18;
            hintText.fontStyle = FontStyle.Bold;
            hintText.color = new Color(0.75f, 0.75f, 0.70f);
            hintText.alignment = TextAnchor.MiddleCenter;
            var hintRt = hintText.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 10f);
            hintRt.sizeDelta = new Vector2(0f, 32f);

            // Flip Button (for inspectables with a back side, like the picture frame)
            flipButtonGo = new GameObject("FlipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            flipButtonGo.transform.SetParent(boxGo.transform, false);
            var flipRt = (RectTransform)flipButtonGo.transform;
            flipRt.anchorMin = new Vector2(0.5f, 0.5f);
            flipRt.anchorMax = new Vector2(0.5f, 0.5f);
            flipRt.pivot = new Vector2(0.5f, 0.5f);
            flipRt.anchoredPosition = new Vector2(0f, -105f);
            flipRt.sizeDelta = new Vector2(250f, 38f);

            var flipImg = flipButtonGo.GetComponent<Image>();
            flipImg.color = new Color(0.18f, 0.22f, 0.32f, 0.95f);

            var flipBtn = flipButtonGo.GetComponent<Button>();
            flipBtn.onClick.AddListener(TriggerFlip);

            var flipTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            flipTextGo.transform.SetParent(flipButtonGo.transform, false);
            flipButtonText = flipTextGo.GetComponent<Text>();
            flipButtonText.font = font;
            flipButtonText.fontSize = 16;
            flipButtonText.fontStyle = FontStyle.Bold;
            flipButtonText.alignment = TextAnchor.MiddleCenter;
            flipButtonText.color = new Color(1f, 0.95f, 0.75f);
            flipButtonText.text = "⟳ Turn Over / Look Behind [F]";

            var flipTextRt = flipButtonText.rectTransform;
            flipTextRt.anchorMin = Vector2.zero;
            flipTextRt.anchorMax = Vector2.one;
            flipTextRt.offsetMin = Vector2.zero;
            flipTextRt.offsetMax = Vector2.zero;

            flipButtonGo.SetActive(false);

            root.SetActive(false);
        }

        public void Configure(GameObject rootObj, Image preview, Text title, Text desc, Text hint)
        {
            root = rootObj;
            previewImage = preview;
            titleText = title;
            descriptionText = desc;
            hintText = hint;
            if (root != null) root.SetActive(false);
        }

        public void Show(Sprite sprite, string title, string description, Action onCollect = null, Action onClose = null)
        {
            if (root == null)
            {
                BuildRuntimeUI();
            }

            if (collectRoutine != null)
            {
                StopCoroutine(collectRoutine);
                collectRoutine = null;
            }
            if (closeRoutine != null)
            {
                StopCoroutine(closeRoutine);
                closeRoutine = null;
            }
            isClosingOrCollecting = false;

            openedTime = Time.unscaledTime;
            if (player == null) player = FindFirstObjectByType<AdrianController>();
            if (player != null) player.CanMove = false;

            isFlippable = false;
            isFlipped = false;
            if (flipButtonGo != null) flipButtonGo.SetActive(false);

            onCollectCallback = onCollect;
            onCloseCallback = onClose;
            isCollectible = (onCollect != null);

            if (sprite == null && !string.IsNullOrEmpty(title))
            {
                sprite = Room0317.Story.StoryProgressionCoordinator.ResolveClueSprite(title);
            }

            if (previewImage != null)
            {
                previewImage.sprite = sprite;
                previewImage.color = Color.white;
                previewImage.gameObject.SetActive(sprite != null);
            }

            if (titleText != null)
            {
                titleText.text = $"✦ {title.ToUpper()} ✦";
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
            }

            // Guidance on how this clue is collected and stored
            if (clueGuideText != null)
            {
                if (isCollectible)
                {
                    clueGuideText.text =
                        "<color=#50FA7B><b>✦ CLUE DISCOVERED:</b></color> Press <b>[E]</b> to take this clue into your <b>Evidence Bar</b>.\n" +
                        "<color=#A0A5B5>Collected clues unlock room shifts and can be re-examined at any time.</color>";
                }
                else
                {
                    clueGuideText.text =
                        "<color=#F1FA8C><b>✦ EVIDENCE LOGGED:</b></color> This item is securely kept in your Evidence Inventory.\n" +
                        "<color=#A0A5B5>Study details closely to piece together the events of Room 03:17.</color>";
                }
            }

            if (hintText != null)
            {
                hintText.text = isCollectible
                    ? "<b>[E]</b> Take Clue into Inventory   |   <b>[Space / Esc]</b> Put Back"
                    : "<b>[E / Space / Esc]</b> Close Inspection";
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            // Play Pop-Up Animation and Sound
            if (activePopRoutine != null) StopCoroutine(activePopRoutine);
            activePopRoutine = StartCoroutine(AnimatePopUpRoutine());

            if (AudioManager.Instance != null)
            {
                if (title.ToLower().Contains("key")) AudioManager.Instance.PlayKeyPickup();
                else if (title.ToLower().Contains("note") || title.ToLower().Contains("page")) AudioManager.Instance.PlayNotePickup();
                else AudioManager.Instance.PlayInspectOpen();
            }
        }

        public void ShowItem(InventoryItem item, Action onCollect = null, Action onClose = null)
        {
            if (item == null) return;
            var spr = item.inspectionSprite != null ? item.inspectionSprite : item.icon;
            if (spr == null) spr = Room0317.Story.StoryProgressionCoordinator.ResolveClueSprite(item.id);
            Show(spr,
                 item.displayName,
                 item.description,
                 onCollect,
                 onClose);
        }

        public void ShowFlippable(
            Sprite frontSpr, string frontTtl, string frontDsc,
            Sprite backSpr, string backTtl, string backDsc,
            Action onCollect = null, Action onClose = null, Action onFlipped = null)
        {
            if (frontSpr == null) frontSpr = Room0317.Story.StoryProgressionCoordinator.ResolveObjectSprite("Assets/Objects/eerie_frame.png", "eerie_frame_0");
            if (backSpr == null) backSpr = Room0317.Story.StoryProgressionCoordinator.ResolveObjectSprite("Assets/Objects/hidden_note.png", "hidden_note_0");

            isFlippable = true;
            isFlipped = false;
            frontSprite = frontSpr;
            frontTitle = frontTtl;
            frontDesc = frontDsc;
            backSprite = backSpr;
            backTitle = backTtl;
            backDesc = backDsc;
            onFlippedCallback = onFlipped;

            Show(frontSpr, frontTtl, frontDsc, onCollect, onClose);

            // Re-enable flip button
            isFlippable = true;
            if (flipButtonGo != null)
            {
                flipButtonGo.SetActive(true);
                if (flipButtonText != null) flipButtonText.text = "⟳ Turn Over / Look Behind [F]";
            }

            if (hintText != null)
            {
                hintText.text = "<b>[F / Click Arrow]</b> Turn Over   |   <b>[Space / Esc]</b> Close";
            }
        }

        public void TriggerFlip()
        {
            if (!IsOpen || !isFlippable) return;
            if (flipRoutine != null) StopCoroutine(flipRoutine);
            flipRoutine = StartCoroutine(FlipCardRoutine());
        }

        private IEnumerator FlipCardRoutine()
        {
            float halfDuration = 0.14f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (cardBoxRt != null) cardBoxRt.localScale = new Vector3(Mathf.Cos(t * Mathf.PI * 0.5f), 1f, 1f);
                yield return null;
            }

            isFlipped = !isFlipped;
            UpdateFlippedContent();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayNotePickup();
            }

            onFlippedCallback?.Invoke();

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (cardBoxRt != null) cardBoxRt.localScale = new Vector3(Mathf.Sin(t * Mathf.PI * 0.5f), 1f, 1f);
                yield return null;
            }

            if (cardBoxRt != null) cardBoxRt.localScale = Vector3.one;
        }

        private void UpdateFlippedContent()
        {
            if (isFlipped)
            {
                if (previewImage != null) previewImage.sprite = backSprite;
                if (titleText != null) titleText.text = $"✦ {backTitle.ToUpper()} ✦";
                if (descriptionText != null) descriptionText.text = backDesc;
                if (flipButtonText != null) flipButtonText.text = "⟳ View Front [F]";
                if (hintText != null)
                {
                    hintText.text = isCollectible
                        ? "<b>[E]</b> Take Hidden Evidence   |   <b>[F]</b> Flip Front   |   <b>[Space / Esc]</b> Close"
                        : "<b>[F]</b> Flip Front   |   <b>[Space / Esc]</b> Close";
                }
            }
            else
            {
                if (previewImage != null) previewImage.sprite = frontSprite;
                if (titleText != null) titleText.text = $"✦ {frontTitle.ToUpper()} ✦";
                if (descriptionText != null) descriptionText.text = frontDesc;
                if (flipButtonText != null) flipButtonText.text = "⟳ Turn Over / Look Behind [F]";
                if (hintText != null)
                {
                    hintText.text = "<b>[F / Click Arrow]</b> Turn Over   |   <b>[Space / Esc]</b> Close";
                }
            }
        }

        private IEnumerator AnimatePopUpRoutine()
        {
            float duration = 0.24f;
            float elapsed = 0f;

            if (cardBoxRt != null) cardBoxRt.localScale = new Vector3(0.75f, 0.75f, 1f);
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;
            if (backgroundVignette != null) backgroundVignette.color = new Color(0.015f, 0.015f, 0.025f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                // Smooth, restrained ease-out scale from 0.85 -> 1.0 (avoid bouncy arcade overshoot)
                float scale = Mathf.Lerp(0.85f, 1.0f, eased);

                if (cardBoxRt != null) cardBoxRt.localScale = new Vector3(scale, scale, 1f);
                if (cardCanvasGroup != null) cardCanvasGroup.alpha = eased;
                if (backgroundVignette != null) backgroundVignette.color = new Color(0.015f, 0.015f, 0.025f, 0.94f * eased);
                yield return null;
            }

            if (cardBoxRt != null) cardBoxRt.localScale = Vector3.one;
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = 1f;
            if (backgroundVignette != null) backgroundVignette.color = new Color(0.015f, 0.015f, 0.025f, 0.94f);

            // Gentle suspended hovering + subtle breathing scale while viewing
            while (IsOpen && closeRoutine == null)
            {
                if (previewImageRt != null)
                {
                    float hover = Mathf.Sin(Time.unscaledTime * 2.0f) * 5f;
                    previewImageRt.anchoredPosition = new Vector2(0f, 75f + hover);
                    float breath = 1f + 0.012f * Mathf.Sin(Time.unscaledTime * 1.4f);
                    previewImageRt.localScale = new Vector3(breath, breath, 1f);
                }
                yield return null;
            }
        }

        private void Update()
        {
            if (!IsOpen || isClosingOrCollecting) return;
            if (Room0317.UI.PauseMenu.Instance != null && Room0317.UI.PauseMenu.Instance.IsPaused) return;
            if (Room0317.UI.PhoneNotificationUI.Instance != null && Room0317.UI.PhoneNotificationUI.Instance.IsShowing) return;

            // Debounce: prevent same-frame input from closing or collecting the modal immediately
            if (Time.unscaledTime - openedTime < 0.28f) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            bool escPressed = (kb != null && kb.escapeKey.wasPressedThisFrame)
                || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame));

            if (escPressed)
            {
                Close();
                return;
            }

            // Flip hotkey [F] or [Q]
            bool flipPressed = (kb != null && (kb.fKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                || (pad != null && (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame));

            if (isFlippable && flipPressed)
            {
                TriggerFlip();
                return;
            }

            // If front of flippable card is showing, [Space] closes, [E] turns it over
            if (isFlippable && !isFlipped)
            {
                if (kb != null && kb.spaceKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    TriggerFlip();
                    return;
                }
                return;
            }

            bool actionPressed = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame));

            if (isCollectible)
            {
                if (actionPressed)
                {
                    isClosingOrCollecting = true;
                    collectRoutine = StartCoroutine(ExecuteCollectSequence());
                    return;
                }
            }
            else
            {
                if (actionPressed)
                {
                    Close();
                    return;
                }
            }
        }

        private IEnumerator ExecuteCollectSequence()
        {
            isClosingOrCollecting = true;

            // Visual feedback that clue has been logged
            if (clueGuideText != null)
            {
                clueGuideText.text = "<color=#50FA7B><b>✔ CLUE COLLECTED!</b> Added to Evidence Bar.</color>";
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayObjectiveChime();
            }

            yield return new WaitForSecondsRealtime(0.32f);

            var callback = onCollectCallback;
            collectRoutine = null;
            CloseInternal(callback, playCloseSound: false);
        }

        public void Close()
        {
            CloseInternal(null, playCloseSound: true);
        }

        private void CloseInternal(Action extraCallback, bool playCloseSound)
        {
            if (!IsOpen || closeRoutine != null) return;

            isClosingOrCollecting = true;
            if (collectRoutine != null)
            {
                StopCoroutine(collectRoutine);
                collectRoutine = null;
            }
            if (activePopRoutine != null)
            {
                StopCoroutine(activePopRoutine);
                activePopRoutine = null;
            }

            if (playCloseSound && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayInspectClose();
            }

            if (gameObject.activeInHierarchy)
            {
                closeRoutine = StartCoroutine(CloseFadeOutRoutine(extraCallback));
            }
            else
            {
                FinalizeClose(extraCallback);
            }
        }

        private IEnumerator CloseFadeOutRoutine(Action extraCallback)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            float startCardAlpha = cardCanvasGroup != null ? cardCanvasGroup.alpha : 1f;
            float startVigAlpha = backgroundVignette != null ? backgroundVignette.color.a : 0.94f;
            Vector3 startScale = cardBoxRt != null ? cardBoxRt.localScale : Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t;

                if (cardCanvasGroup != null) cardCanvasGroup.alpha = Mathf.Lerp(startCardAlpha, 0f, eased);
                if (backgroundVignette != null) backgroundVignette.color = new Color(0.015f, 0.015f, 0.025f, Mathf.Lerp(startVigAlpha, 0f, eased));
                if (cardBoxRt != null) cardBoxRt.localScale = Vector3.Lerp(startScale, new Vector3(0.92f, 0.92f, 1f), eased);
                yield return null;
            }

            closeRoutine = null;
            FinalizeClose(extraCallback);
        }

        private void FinalizeClose(Action extraCallback)
        {
            isClosingOrCollecting = false;
            lastClosedTime = Time.unscaledTime;
            if (root != null) root.SetActive(false);

            var cb = onCloseCallback;
            onCollectCallback = null;
            onCloseCallback = null;
            isCollectible = false;

            if (player == null) player = FindFirstObjectByType<AdrianController>();
            bool storyActive = Room0317.Story.StoryProgressionCoordinator.Instance != null && Room0317.Story.StoryProgressionCoordinator.Instance.IsStorySequenceActive;
            bool phoneActive = Room0317.UI.PhoneNotificationUI.Instance != null && Room0317.UI.PhoneNotificationUI.Instance.IsShowing;
            var dlg = Room0317.Dialogue.DialogueBox.ActiveInstance;
            bool dialogueActive = dlg != null && dlg.IsOpen;
            if (player != null && !storyActive && !phoneActive && !dialogueActive)
            {
                player.CanMove = true;
            }

            extraCallback?.Invoke();
            cb?.Invoke();
        }

        // ====================================================================
        // PROCEDURAL FROSTED VIGNETTE BLUR GENERATOR
        // ====================================================================

        private static Texture2D CreateVignetteTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f * 1.35f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    // Cinematic radial optical blur: center is translucent, edges are dense dark
                    float alpha = Mathf.SmoothStep(0.72f, 0.99f, t);
                    tex.SetPixel(x, y, new Color(0.015f, 0.015f, 0.025f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
