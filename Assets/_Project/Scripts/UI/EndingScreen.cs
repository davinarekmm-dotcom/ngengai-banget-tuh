using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Inventory;
using Room0317.Objectives;

namespace Room0317.UI
{
    /// <summary>
    /// Cinematic Epilogue & Game Ending Screen for ROOM 03:17.
    /// Triggered when Adrian unseals the front door deadbolt and escapes into the stairwell.
    /// Fades to pitch-black, presents the atmospheric psychological narrative conclusion,
    /// displays play statistics, and provides clean replay/quit actions.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        public static EndingScreen Instance { get; private set; }

        private GameObject root;
        private Text titleText;
        private Text subtitleText;
        private Text narrativeText;
        private Text statsText;
        private ScreenFader screenFader;
        private CanvasGroup cardCanvasGroup;
        private bool isShowing = false;
        private bool hasCustomText = false;

        public bool IsShowing => isShowing;

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
            screenFader = FindFirstObjectByType<ScreenFader>();
            ObjectiveManager.OnAllObjectivesCompleted += HandleAllObjectivesCompleted;
        }

        private void OnDestroy()
        {
            ObjectiveManager.OnAllObjectivesCompleted -= HandleAllObjectivesCompleted;
        }

        private void HandleAllObjectivesCompleted()
        {
            StartCoroutine(ShowEndingSequenceRoutine());
        }

        public void TriggerEndingManually()
        {
            StartCoroutine(ShowEndingSequenceRoutine());
        }

        public void SetCustomEnding(string title, string subtitle, string quoteNarrative)
        {
            hasCustomText = true;
            if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
            if (subtitleText != null && !string.IsNullOrEmpty(subtitle)) subtitleText.text = subtitle;
            if (narrativeText != null && !string.IsNullOrEmpty(quoteNarrative)) narrativeText.text = quoteNarrative;
            if (statsText != null && hasCustomText) statsText.gameObject.SetActive(false);
        }

        public void TriggerCustomEnding(string quote, string title = "ROOM 03:17", string subtitle = "Someone was here.")
        {
            SetCustomEnding(title, subtitle, quote);
            StartCoroutine(ShowEndingSequenceRoutine());
        }

        private IEnumerator ShowEndingSequenceRoutine()
        {
            isShowing = true;

            if (!hasCustomText)
            {
                // Fade to complete darkness for fallback/manual triggers
                if (screenFader != null)
                {
                    yield return screenFader.FadeOut(1.5f);
                }
                else
                {
                    yield return new WaitForSeconds(1.0f);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetHeartbeatIntensity(0f);
                    AudioManager.Instance.PlayHorrorStinger();
                }
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0f);
            }

            UpdateStatsDisplay();

            if (root != null)
            {
                if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;
                root.SetActive(true);
            }

            if (cardCanvasGroup != null)
            {
                float elapsed = 0f;
                float fadeDuration = 0.85f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cardCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                    yield return null;
                }
                cardCanvasGroup.alpha = 1f;
            }
        }

        private void UpdateStatsDisplay()
        {
            int cluesCollected = 0;
            if (InventoryManager.Instance != null)
            {
                cluesCollected = InventoryManager.Instance.Items.Count;
            }

            int shiftsEncountered = 0;
            if (ChangingRoomManager.Instance != null)
            {
                foreach (var tracker in ChangingRoomManager.Instance.Trackers.Values)
                {
                    if (tracker.Room != null && tracker.Room.CurrentState == RoomState.B) shiftsEncountered++;
                }
            }

            if (statsText != null)
            {
                statsText.text = $"<b>SURVIVAL RECORD:</b>\n" +
                                 $"• Clues Discovered: <color=#50FA7B>{cluesCollected} / 3</color>\n" +
                                 $"• Corrupted Rooms Witnessed: <color=#FF5555>{shiftsEncountered} / 5</color>\n" +
                                 $"• Time of Escape: <color=#F1FA8C>03:17 AM</color>";
            }
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Root Overlay
            root = new GameObject("EndingScreen_Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);

            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = root.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.03f, 0.98f);

            // Container Box
            var boxGo = new GameObject("EndingCard", typeof(RectTransform), typeof(CanvasGroup));
            boxGo.transform.SetParent(root.transform, false);
            cardCanvasGroup = boxGo.GetComponent<CanvasGroup>();
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;

            var boxRt = (RectTransform)boxGo.transform;
            boxRt.anchorMin = new Vector2(0.5f, 0.5f);
            boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.sizeDelta = new Vector2(850f, 650f);
            boxRt.anchoredPosition = Vector2.zero;

            // Main Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(boxGo.transform, false);
            titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.35f, 0.35f, 1f);
            titleText.text = "ROOM 03:17";

            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);
            titleRt.sizeDelta = new Vector2(0f, 50f);

            // Subtitle
            var subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(boxGo.transform, false);
            subtitleText = subGo.GetComponent<Text>();
            subtitleText.font = font;
            subtitleText.fontSize = 20;
            subtitleText.fontStyle = FontStyle.Bold;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = new Color(0.85f, 0.85f, 0.75f, 0.9f);
            subtitleText.text = "✦ YOU SURVIVED THE NIGHT ✦";

            var subRt = subtitleText.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -75f);
            subRt.sizeDelta = new Vector2(0f, 30f);

            // Epilogue Narrative Text
            var narGo = new GameObject("NarrativeText", typeof(RectTransform), typeof(Text));
            narGo.transform.SetParent(boxGo.transform, false);
            narrativeText = narGo.GetComponent<Text>();
            narrativeText.font = font;
            narrativeText.fontSize = 20;
            narrativeText.lineSpacing = 1.35f;
            narrativeText.alignment = TextAnchor.MiddleCenter;
            narrativeText.color = new Color(0.92f, 0.92f, 0.88f, 0.95f);
            narrativeText.text =
                "The heavy deadbolt gave way with a deafening rattle.\n" +
                "Cold autumn air hit your face like ice as you stumbled into the dark stairwell.\n\n" +
                "Outside, you look back up at the third floor.\n" +
                "The lights inside Room 03:17 flicker once... and extinguish into complete darkness.\n\n" +
                "The clock on your wrist ticks to <b>03:18 AM</b>.\n" +
                "You escaped. But deep inside... you wonder if you ever truly left.";

            var narRt = narrativeText.rectTransform;
            narRt.anchorMin = new Vector2(0.05f, 1f);
            narRt.anchorMax = new Vector2(0.95f, 1f);
            narRt.pivot = new Vector2(0.5f, 1f);
            narRt.anchoredPosition = new Vector2(0f, -125f);
            narRt.sizeDelta = new Vector2(0f, 220f);

            // Statistics Box
            var statsGo = new GameObject("StatsBox", typeof(RectTransform), typeof(Text));
            statsGo.transform.SetParent(boxGo.transform, false);
            statsText = statsGo.GetComponent<Text>();
            statsText.font = font;
            statsText.fontSize = 18;
            statsText.lineSpacing = 1.25f;
            statsText.alignment = TextAnchor.MiddleCenter;
            statsText.color = new Color(0.78f, 0.8f, 0.85f, 0.95f);

            var statsRt = statsText.rectTransform;
            statsRt.anchorMin = new Vector2(0.1f, 1f);
            statsRt.anchorMax = new Vector2(0.9f, 1f);
            statsRt.pivot = new Vector2(0.5f, 1f);
            statsRt.anchoredPosition = new Vector2(0f, -370f);
            statsRt.sizeDelta = new Vector2(0f, 100f);

            // Play Again Button
            CreateButton(boxGo.transform, "PlayAgainBtn", "▶ PLAY AGAIN", new Vector2(0f, -495f), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });

            // Quit Button
            CreateButton(boxGo.transform, "QuitBtn", "✕ EXIT GAME", new Vector2(0f, -555f), () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            });

            root.SetActive(false);
        }

        private void CreateButton(Transform parent, string name, string label, Vector2 posFromTop, Action onClick)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = posFromTop;
            btnRt.sizeDelta = new Vector2(380f, 44f);

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);

            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.12f, 0.14f, 0.2f, 0.95f);
            colors.highlightedColor = new Color(0.35f, 0.15f, 0.22f, 1f);
            colors.pressedColor = new Color(0.5f, 0.1f, 0.12f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var txt = textGo.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.96f, 0.96f, 0.92f);
            txt.text = label;

            var txtRt = txt.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }
    }
}
