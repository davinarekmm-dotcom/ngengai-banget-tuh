using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Room0317.Objectives;

namespace Room0317.UI
{
    /// <summary>
    /// Narrative HUD displaying the creeping digital horror clock (03:12 -> 03:17)
    /// and current active story objective.
    /// Features auto-expanding dynamic card sizing with VerticalLayoutGroup and ContentSizeFitter,
    /// smooth completion flash animations, and runtime UI generation.
    /// Hotkeys: [H] Toggle Hint, [O] Toggle HUD visibility.
    /// </summary>
    public class ObjectiveHUD : MonoBehaviour
    {
        public static ObjectiveHUD Instance { get; private set; }

        private GameObject root;
        private RectTransform rootRt;
        private CanvasGroup rootCanvasGroup;
        private Text clockText;
        private Text objectiveTitleText;
        private Text hintText;
        private Image backgroundCard;
        private Coroutine activationAnimRoutine;

        private bool showHint = true;
        private bool isVisible = true;

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

        private void Start()
        {
            ObjectiveManager.OnObjectiveActivated += HandleObjectiveActivated;
            ObjectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
            ApartmentClock.OnTimeChanged += HandleTimeChanged;

            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentObjective != null)
            {
                UpdateObjectiveDisplay(ObjectiveManager.Instance.CurrentObjective);
            }
            if (ApartmentClock.Instance != null)
            {
                UpdateTimeDisplay(ApartmentClock.Instance.FormattedTime);
            }
        }

        private void OnDestroy()
        {
            ObjectiveManager.OnObjectiveActivated -= HandleObjectiveActivated;
            ObjectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
            ApartmentClock.OnTimeChanged -= HandleTimeChanged;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // [H] Toggle Hint
                if (kb.hKey.wasPressedThisFrame)
                {
                    showHint = !showHint;
                    if (hintText != null)
                    {
                        hintText.gameObject.SetActive(showHint);
                        RefreshLayout();
                    }
                }
                // [O] Toggle Objective HUD
                if (kb.oKey.wasPressedThisFrame)
                {
                    isVisible = !isVisible;
                    if (root != null) root.SetActive(isVisible);
                }
            }
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Main Root Container (Top-Left) with Card Background
            root = new GameObject("ObjectiveHUD_Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            root.transform.SetParent(canvas.transform, false);
            rootCanvasGroup = root.GetComponent<CanvasGroup>();

            rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(25f, -25f);
            rootRt.sizeDelta = new Vector2(580f, 115f);

            backgroundCard = root.GetComponent<Image>();
            backgroundCard.color = new Color(0.04f, 0.04f, 0.06f, 0.90f);

            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.18f, 0.22f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header Row (Clock + Tag)
            var headerGo = new GameObject("HeaderRow", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(root.transform, false);
            clockText = headerGo.GetComponent<Text>();
            clockText.font = font;
            clockText.fontSize = 19;
            clockText.fontStyle = FontStyle.Bold;
            clockText.color = new Color(1f, 0.35f, 0.35f, 0.95f);
            clockText.text = "[ 03:12 AM ]  ✦ CURRENT OBJECTIVE";
            clockText.horizontalOverflow = HorizontalWrapMode.Wrap;
            clockText.verticalOverflow = VerticalWrapMode.Overflow;

            // Objective Title
            var titleGo = new GameObject("ObjectiveTitle", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(root.transform, false);
            objectiveTitleText = titleGo.GetComponent<Text>();
            objectiveTitleText.font = font;
            objectiveTitleText.fontSize = 21;
            objectiveTitleText.fontStyle = FontStyle.Bold;
            objectiveTitleText.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            objectiveTitleText.lineSpacing = 1.15f;
            objectiveTitleText.text = "Check the Bedside Alarm Clock";
            objectiveTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectiveTitleText.verticalOverflow = VerticalWrapMode.Overflow;

            // Hint Text
            var hintGo = new GameObject("HintText", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(root.transform, false);
            hintText = hintGo.GetComponent<Text>();
            hintText.font = font;
            hintText.fontSize = 15;
            hintText.fontStyle = FontStyle.Italic;
            hintText.color = new Color(0.7f, 0.7f, 0.65f, 0.85f);
            hintText.lineSpacing = 1.1f;
            hintText.text = "Hint: Press [E] facing the clock.  ([H] Hide)";
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Overflow;

            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (rootRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            }
        }

        private void HandleTimeChanged(int hour, int minute)
        {
            UpdateTimeDisplay($"{hour:D2}:{minute:D2} AM");
        }

        private void UpdateTimeDisplay(string timeStr)
        {
            if (clockText != null)
            {
                clockText.text = $"[ {timeStr} ]  ✦ CURRENT OBJECTIVE";
                RefreshLayout();
            }
        }

        private void HandleObjectiveActivated(ObjectiveData obj)
        {
            UpdateObjectiveDisplay(obj);
            if (gameObject.activeInHierarchy)
            {
                if (activationAnimRoutine != null) StopCoroutine(activationAnimRoutine);
                activationAnimRoutine = StartCoroutine(AnimateObjectiveTransitionRoutine());
            }
        }

        private IEnumerator AnimateObjectiveTransitionRoutine()
        {
            float duration = 0.25f;
            float elapsed = 0f;
            Color highlightColor = new Color(1f, 0.90f, 0.65f, 1f);
            Color normalColor = new Color(0.96f, 0.94f, 0.88f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (rootRt != null)
                {
                    rootRt.anchoredPosition = new Vector2(Mathf.Lerp(13f, 25f, eased), -25f);
                }
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.alpha = Mathf.Lerp(0.3f, 1f, eased);
                }
                if (objectiveTitleText != null)
                {
                    objectiveTitleText.color = Color.Lerp(highlightColor, normalColor, eased);
                }
                yield return null;
            }

            if (rootRt != null) rootRt.anchoredPosition = new Vector2(25f, -25f);
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
            if (objectiveTitleText != null) objectiveTitleText.color = normalColor;
            activationAnimRoutine = null;
        }

        private void HandleObjectiveCompleted(ObjectiveData obj)
        {
            StartCoroutine(FlashCompletionRoutine(obj));
        }

        private void UpdateObjectiveDisplay(ObjectiveData obj)
        {
            if (obj == null)
            {
                if (objectiveTitleText != null) objectiveTitleText.text = "✦ All Objectives Complete";
                if (hintText != null) hintText.text = "You have escaped Room 03:17.";
                RefreshLayout();
                return;
            }

            if (objectiveTitleText != null)
            {
                objectiveTitleText.text = obj.Title;
                objectiveTitleText.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            }

            if (hintText != null)
            {
                hintText.text = string.IsNullOrEmpty(obj.Hint)
                    ? "[H] Toggle Hint"
                    : $"Hint: {obj.Hint}  ([H] Hide)";
            }

            RefreshLayout();
        }

        private IEnumerator FlashCompletionRoutine(ObjectiveData obj)
        {
            if (objectiveTitleText != null)
            {
                objectiveTitleText.text = $"✔ {obj.Title}";
                objectiveTitleText.color = new Color(0.35f, 1f, 0.55f, 1f);
            }

            if (backgroundCard != null)
            {
                backgroundCard.color = new Color(0.1f, 0.3f, 0.15f, 0.95f);
            }

            RefreshLayout();

            yield return new WaitForSeconds(1.2f);

            if (backgroundCard != null)
            {
                backgroundCard.color = new Color(0.04f, 0.04f, 0.06f, 0.90f);
            }

            RefreshLayout();
        }
    }
}
