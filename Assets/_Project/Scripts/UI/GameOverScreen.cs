using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Inventory;
using Room0317.Objectives;

namespace Room0317.UI
{
    /// <summary>
    /// AAA Psychological Horror Game Over Screen for ROOM 03:17.
    /// Triggers when the player's sanity breaks, fails an escape condition,
    /// or when the anomaly consumes Adrian at 03:17 AM.
    /// Features:
    /// - Blood-red vignette overlay and pulsing 03:17 logo
    /// - Atmospheric psychological lore epilogue
    /// - Investigation and survival record statistics
    /// - Interactive navigation buttons with the iconic glowing crimson notch
    /// - Procedural horror audio (low drone + stinger + UI blips)
    /// - Mouse, Keyboard, and Gamepad support
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }

        private GameObject root;
        private Text titleTimeText;
        private Text narrativeText;
        private Text statsText;
        private RectTransform highlightPlate;

        private AudioSource audioSource;
        private AudioClip stingerClip;
        private AudioClip droneClip;
        private AudioClip hoverClip;
        private AudioClip selectClip;

        private bool isShowing = false;
        private int selectedIndex = 0;

        private readonly List<GameOverButtonEntry> menuButtons = new List<GameOverButtonEntry>();
        private readonly Color timeRedDark = new Color(0.82f, 0.18f, 0.18f, 1f);
        private readonly Color timeRedBright = new Color(1.0f, 0.35f, 0.35f, 1f);

        public bool IsShowing => isShowing;

        public static event Action OnGameOverTriggered;

        private class GameOverButtonEntry
        {
            public Button button;
            public Text label;
            public RectTransform rectTransform;
            public string actionName;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildRuntimeUI();
            GenerateProceduralAudio();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                if (!isShowing) TriggerGameOver();
                else HideGameOver();
            }
#endif

            if (!isShowing) return;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            AnimateTitlePulse();
            HandleMouseNavigation();
            HandleInputNavigation();
        }

        private void HandleMouseNavigation()
        {
            if (menuButtons.Count == 0 || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            bool clicked = Mouse.current.leftButton.wasPressedThisFrame;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                var entry = menuButtons[i];
                if (entry.rectTransform != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(entry.rectTransform, mousePos, null))
                {
                    if (selectedIndex != i)
                    {
                        SetSelection(i, playSound: true);
                    }

                    if (clicked)
                    {
                        OnButtonClicked(i);
                    }
                    break;
                }
            }
        }

        private void HandleInputNavigation()
        {
            if (menuButtons.Count == 0) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool up = false;
            bool down = false;
            bool submit = false;

            if (kb != null)
            {
                up |= kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                down |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                submit |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
            }

            if (pad != null)
            {
                up |= pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
                submit |= pad.buttonSouth.wasPressedThisFrame;
            }

            if (up)
            {
                int prev = (selectedIndex - 1 + menuButtons.Count) % menuButtons.Count;
                SetSelection(prev, playSound: true);
            }
            else if (down)
            {
                int next = (selectedIndex + 1) % menuButtons.Count;
                SetSelection(next, playSound: true);
            }
            else if (submit)
            {
                OnButtonClicked(selectedIndex);
            }
        }

        public void SetSelection(int index, bool playSound = true, bool snap = false)
        {
            if (index < 0 || index >= menuButtons.Count) return;

            selectedIndex = index;

            if (playSound && audioSource != null && hoverClip != null)
            {
                audioSource.PlayOneShot(hoverClip, 0.45f);
            }

            for (int i = 0; i < menuButtons.Count; i++)
            {
                if (menuButtons[i].label != null)
                {
                    menuButtons[i].label.color = (i == selectedIndex)
                        ? Color.white
                        : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                }
            }

            if (highlightPlate != null && menuButtons[selectedIndex].rectTransform != null)
            {
                var targetRt = menuButtons[selectedIndex].rectTransform;
                Vector2 targetPos = targetRt.anchoredPosition;

                if (snap)
                {
                    highlightPlate.anchoredPosition = targetPos;
                    highlightPlate.sizeDelta = targetRt.sizeDelta;
                }
                else
                {
                    if (highlightRoutine != null) StopCoroutine(highlightRoutine);
                    highlightRoutine = StartCoroutine(SmoothMoveHighlight(targetPos, targetRt.sizeDelta));
                }
            }
        }

        private Coroutine highlightRoutine;
        private IEnumerator SmoothMoveHighlight(Vector2 targetPos, Vector2 targetSize)
        {
            Vector2 startPos = highlightPlate.anchoredPosition;
            Vector2 startSize = highlightPlate.sizeDelta;
            float duration = 0.12f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.SmoothStep(0f, 1f, t);
                highlightPlate.anchoredPosition = Vector2.Lerp(startPos, targetPos, s);
                highlightPlate.sizeDelta = Vector2.Lerp(startSize, targetSize, s);
                yield return null;
            }

            highlightPlate.anchoredPosition = targetPos;
            highlightPlate.sizeDelta = targetSize;
            highlightRoutine = null;
        }

        private float lastClickTime = -1f;

        private void OnButtonClicked(int index)
        {
            if (index < 0 || index >= menuButtons.Count) return;
            if (Time.unscaledTime - lastClickTime < 0.15f) return;
            lastClickTime = Time.unscaledTime;

            if (audioSource != null && selectClip != null)
            {
                audioSource.PlayOneShot(selectClip, 0.7f);
            }

            string action = menuButtons[index].actionName;
            switch (action)
            {
                case "Restart":
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    break;
                case "MainMenu":
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("00_MainMenu");
                    break;
                case "Quit":
                    Time.timeScale = 1f;
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    break;
            }
        }

        public void TriggerGameOver(string customLore = null)
        {
            if (isShowing) return;
            StartCoroutine(ShowGameOverRoutine(customLore));
        }

        private IEnumerator ShowGameOverRoutine(string customLore)
        {
            isShowing = true;
            OnGameOverTriggered?.Invoke();

            // Mute active gameplay sounds
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0f);
            }

            // Play jarring tension stinger and atmospheric drone
            if (audioSource != null)
            {
                if (stingerClip != null) audioSource.PlayOneShot(stingerClip, 0.85f);
                if (droneClip != null)
                {
                    audioSource.clip = droneClip;
                    audioSource.loop = true;
                    audioSource.volume = 0.4f;
                    audioSource.Play();
                }
            }

            if (!string.IsNullOrEmpty(customLore) && narrativeText != null)
            {
                narrativeText.text = customLore;
            }

            UpdateStatsDisplay();

            if (root != null)
            {
                root.SetActive(true);
            }

            Time.timeScale = 0f;
            SetSelection(0, playSound: false, snap: true);
            yield return null;
        }

        public void HideGameOver()
        {
            isShowing = false;
            Time.timeScale = 1f;
            if (root != null) root.SetActive(false);
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        }

        private void UpdateStatsDisplay()
        {
            if (statsText == null) return;

            int clues = 0;
            if (InventoryManager.Instance != null)
            {
                clues = InventoryManager.Instance.Items.Count;
            }

            string currentObj = "Investigating Apartment";
            if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentObjective != null)
            {
                currentObj = ObjectiveManager.Instance.CurrentObjective.Title;
            }

            statsText.text = $"<b>TIMELINE BREAKDOWN:</b>\n" +
                             $"• Evidence Gathered: <color=#50FA7B>{clues} clues logged</color>\n" +
                             $"• Last Known Memory: <color=#FFB86C>{currentObj}</color>\n" +
                             $"• Time of Collapse: <color=#FF5555>03:17 AM</color>";
        }

        private void AnimateTitlePulse()
        {
            if (titleTimeText == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.5f;
            titleTimeText.color = Color.Lerp(timeRedDark, timeRedBright, pulse);
        }

        private void BuildRuntimeUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var uiMod = esGo.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (uiMod != null) uiMod.AssignDefaultActions();
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Root Overlay (Deep dark crimson horror vignette)
            root = new GameObject("GameOverScreen_Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = root.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.015f, 0.02f, 0.96f);

            // Center Card Container
            var cardGo = new GameObject("CardContainer", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(root.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(780f, 660f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.07f, 0.045f, 0.055f, 0.98f);

            // Title Row: "ROOM 03:17"
            var titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(cardGo.transform, false);
            var titleRowRt = (RectTransform)titleRow.transform;
            titleRowRt.anchorMin = new Vector2(0.5f, 1f);
            titleRowRt.anchorMax = new Vector2(0.5f, 1f);
            titleRowRt.pivot = new Vector2(0.5f, 1f);
            titleRowRt.anchoredPosition = new Vector2(0f, -22f);
            titleRowRt.sizeDelta = new Vector2(380f, 48f);

            var roomTxtGo = new GameObject("Text_Room", typeof(RectTransform), typeof(Text));
            roomTxtGo.transform.SetParent(titleRow.transform, false);
            var roomTxtRt = (RectTransform)roomTxtGo.transform;
            roomTxtRt.anchorMin = new Vector2(0f, 0f);
            roomTxtRt.anchorMax = new Vector2(0f, 1f);
            roomTxtRt.pivot = new Vector2(0f, 0.5f);
            roomTxtRt.anchoredPosition = Vector2.zero;
            roomTxtRt.sizeDelta = new Vector2(165f, 48f);
            var roomTxt = roomTxtGo.GetComponent<Text>();
            roomTxt.font = font;
            roomTxt.text = "ROOM";
            roomTxt.fontSize = 42;
            roomTxt.fontStyle = FontStyle.Bold;
            roomTxt.color = new Color(0.93f, 0.91f, 0.88f, 1f);
            roomTxt.alignment = TextAnchor.MiddleRight;

            var timeTxtGo = new GameObject("Text_Time", typeof(RectTransform), typeof(Text));
            timeTxtGo.transform.SetParent(titleRow.transform, false);
            var timeTxtRt = (RectTransform)timeTxtGo.transform;
            timeTxtRt.anchorMin = new Vector2(0f, 0f);
            timeTxtRt.anchorMax = new Vector2(0f, 1f);
            timeTxtRt.pivot = new Vector2(0f, 0.5f);
            timeTxtRt.anchoredPosition = new Vector2(175f, 0f);
            timeTxtRt.sizeDelta = new Vector2(195f, 48f);
            titleTimeText = timeTxtGo.GetComponent<Text>();
            titleTimeText.font = font;
            titleTimeText.text = "03:17";
            titleTimeText.fontSize = 42;
            titleTimeText.fontStyle = FontStyle.Bold;
            titleTimeText.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            titleTimeText.alignment = TextAnchor.MiddleLeft;

            // Subtitle
            var subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(cardGo.transform, false);
            var subText = subGo.GetComponent<Text>();
            subText.font = font;
            subText.fontSize = 17;
            subText.fontStyle = FontStyle.Bold;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.95f, 0.35f, 0.35f, 0.95f);
            subText.text = "✦ THE LOOP RESTARTS ✦";
            var subRt = subText.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -74f);
            subRt.sizeDelta = new Vector2(0f, 24f);

            // Divider Line
            var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(cardGo.transform, false);
            var divRt = (RectTransform)divGo.transform;
            divRt.anchorMin = new Vector2(0.5f, 1f);
            divRt.anchorMax = new Vector2(0.5f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -104f);
            divRt.sizeDelta = new Vector2(640f, 2f);
            var divImg = divGo.GetComponent<Image>();
            divImg.color = new Color(0.42f, 0.16f, 0.16f, 0.85f);
            divImg.raycastTarget = false;

            // Narrative Quote Box
            var narGo = new GameObject("NarrativeBox", typeof(RectTransform), typeof(Text));
            narGo.transform.SetParent(cardGo.transform, false);
            narrativeText = narGo.GetComponent<Text>();
            narrativeText.font = font;
            narrativeText.fontSize = 17;
            narrativeText.lineSpacing = 1.35f;
            narrativeText.alignment = TextAnchor.MiddleCenter;
            narrativeText.color = new Color(0.88f, 0.86f, 0.82f, 0.95f);
            narrativeText.text =
                "\"The cold air tightens around your chest.\n" +
                "The floorboards creak behind you, but when you look, only your shadow remains.\n" +
                "You whisper that you can forget... but the apartment always remembers.\n\n" +
                "The room shifts back into place. Your eyes flutter open in bed once more.\"";
            var narRt = narrativeText.rectTransform;
            narRt.anchorMin = new Vector2(0.06f, 1f);
            narRt.anchorMax = new Vector2(0.94f, 1f);
            narRt.pivot = new Vector2(0.5f, 1f);
            narRt.anchoredPosition = new Vector2(0f, -118f);
            narRt.sizeDelta = new Vector2(0f, 150f);

            // Stats Box
            var statsBoxGo = new GameObject("StatsBox", typeof(RectTransform), typeof(Image));
            statsBoxGo.transform.SetParent(cardGo.transform, false);
            var statsBoxRt = (RectTransform)statsBoxGo.transform;
            statsBoxRt.anchorMin = new Vector2(0.5f, 1f);
            statsBoxRt.anchorMax = new Vector2(0.5f, 1f);
            statsBoxRt.pivot = new Vector2(0.5f, 1f);
            statsBoxRt.anchoredPosition = new Vector2(0f, -282f);
            statsBoxRt.sizeDelta = new Vector2(620f, 90f);
            var statsBoxImg = statsBoxGo.GetComponent<Image>();
            statsBoxImg.color = new Color(0.12f, 0.08f, 0.09f, 0.92f);

            var statsTextGo = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
            statsTextGo.transform.SetParent(statsBoxGo.transform, false);
            statsText = statsTextGo.GetComponent<Text>();
            statsText.font = font;
            statsText.fontSize = 16;
            statsText.lineSpacing = 1.25f;
            statsText.alignment = TextAnchor.MiddleCenter;
            statsText.color = new Color(0.9f, 0.9f, 0.86f, 1f);
            var statsTextRt = statsText.rectTransform;
            statsTextRt.anchorMin = Vector2.zero;
            statsTextRt.anchorMax = Vector2.one;
            statsTextRt.offsetMin = new Vector2(12f, 4f);
            statsTextRt.offsetMax = new Vector2(-12f, -4f);

            // Buttons Container
            var buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
            buttonsContainer.transform.SetParent(cardGo.transform, false);
            var buttonsContainerRt = (RectTransform)buttonsContainer.transform;
            buttonsContainerRt.anchorMin = new Vector2(0.5f, 1f);
            buttonsContainerRt.anchorMax = new Vector2(0.5f, 1f);
            buttonsContainerRt.pivot = new Vector2(0.5f, 1f);
            buttonsContainerRt.anchoredPosition = new Vector2(0f, -395f);
            buttonsContainerRt.sizeDelta = new Vector2(460f, 220f);

            // Highlight Plate with glowing red notch
            var highlightPlateGo = new GameObject("Highlight_Plate", typeof(RectTransform), typeof(Image));
            highlightPlateGo.transform.SetParent(buttonsContainer.transform, false);
            highlightPlate = (RectTransform)highlightPlateGo.transform;
            highlightPlate.anchorMin = new Vector2(0f, 1f);
            highlightPlate.anchorMax = new Vector2(0f, 1f);
            highlightPlate.pivot = new Vector2(0f, 0.5f);
            highlightPlate.sizeDelta = new Vector2(460f, 52f);
            var plateImg = highlightPlateGo.GetComponent<Image>();
            plateImg.color = new Color(0.28f, 0.08f, 0.08f, 0.85f);
            plateImg.raycastTarget = false;

            var notchGo = new GameObject("Glow_Notch", typeof(RectTransform), typeof(Image));
            notchGo.transform.SetParent(highlightPlateGo.transform, false);
            var notchRt = (RectTransform)notchGo.transform;
            notchRt.anchorMin = Vector2.zero;
            notchRt.anchorMax = new Vector2(0f, 1f);
            notchRt.pivot = new Vector2(0f, 0.5f);
            notchRt.anchoredPosition = Vector2.zero;
            notchRt.sizeDelta = new Vector2(4f, 0f);
            var notchImg = notchGo.GetComponent<Image>();
            notchImg.color = new Color(0.98f, 0.22f, 0.22f, 1f);
            notchImg.raycastTarget = false;

            // Buttons: Awaken Again (Restart), Return to Main Menu, Quit
            string[] buttonLabels = { "Awaken Again (Restart)", "Return to Main Menu", "Quit to Desktop" };
            string[] buttonActions = { "Restart", "MainMenu", "Quit" };
            float startY = -28f;
            float stepY = 60f;

            menuButtons.Clear();
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                float btnY = startY - (i * stepY);
                int index = i;

                var btnGo = new GameObject("Btn_" + buttonActions[i], typeof(RectTransform), typeof(Button), typeof(Image));
                btnGo.transform.SetParent(buttonsContainer.transform, false);
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.anchorMin = new Vector2(0f, 1f);
                btnRt.anchorMax = new Vector2(0f, 1f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0f, btnY);
                btnRt.sizeDelta = new Vector2(460f, 50f);

                var raycastImg = btnGo.GetComponent<Image>();
                raycastImg.color = Color.clear;

                var btn = btnGo.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnButtonClicked(index));

                var trigger = btnGo.AddComponent<EventTrigger>();
                var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                pointerEnter.callback.AddListener((data) => SetSelection(index, playSound: true));
                trigger.triggers.Add(pointerEnter);

                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(btnGo.transform, false);
                var lblRt = (RectTransform)lblGo.transform;
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(24f, 0f);
                lblRt.offsetMax = new Vector2(-10f, 0f);
                var lbl = lblGo.GetComponent<Text>();
                lbl.font = font;
                lbl.text = buttonLabels[i];
                lbl.fontSize = 24;
                lbl.color = (i == 0) ? Color.white : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.raycastTarget = false;

                menuButtons.Add(new GameOverButtonEntry
                {
                    button = btn,
                    label = lbl,
                    rectTransform = btnRt,
                    actionName = buttonActions[i]
                });

                if (i < buttonLabels.Length - 1)
                {
                    var sepGo = new GameObject("Divider_" + (i + 1), typeof(RectTransform), typeof(Image));
                    sepGo.transform.SetParent(buttonsContainer.transform, false);
                    var sepRt = (RectTransform)sepGo.transform;
                    sepRt.anchorMin = new Vector2(0f, 1f);
                    sepRt.anchorMax = new Vector2(0f, 1f);
                    sepRt.pivot = new Vector2(0f, 0.5f);
                    sepRt.anchoredPosition = new Vector2(0f, btnY - 30f);
                    sepRt.sizeDelta = new Vector2(460f, 1.5f);
                    var sepImg = sepGo.GetComponent<Image>();
                    sepImg.color = new Color(0.28f, 0.16f, 0.16f, 0.65f);
                    sepImg.raycastTarget = false;
                }
            }

            root.SetActive(false);
        }

        private void GenerateProceduralAudio()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.ignoreListenerPause = true;
            }

            int sampleRate = 44100;

            // 1. Horror Collapse Stinger
            int stingerSamples = (int)(sampleRate * 2.2f);
            float[] stingerData = new float[stingerSamples];
            for (int i = 0; i < stingerSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 1.5f);
                float f1 = Mathf.Sin(2f * Mathf.PI * 55f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 58.7f * t); // Dissonant low horror clash
                float noise = (Mathf.PerlinNoise(t * 12f, 0.3f) - 0.5f) * 0.25f;
                stingerData[i] = (f1 * 0.6f + f2 * 0.4f + noise) * env * 0.9f;
            }
            stingerClip = AudioClip.Create("GO_Stinger", stingerSamples, 1, sampleRate, false);
            stingerClip.SetData(stingerData, 0);

            // 2. Lingering Low Horror Drone
            int droneSamples = sampleRate * 4;
            float[] droneData = new float[droneSamples];
            for (int i = 0; i < droneSamples; i++)
            {
                float t = (float)i / sampleRate;
                float w1 = Mathf.Sin(2f * Mathf.PI * 40f * t);
                float w2 = Mathf.Sin(2f * Mathf.PI * 43f * t) * 0.5f;
                droneData[i] = (w1 + w2) * 0.35f;
            }
            droneClip = AudioClip.Create("GO_Drone", droneSamples, 1, sampleRate, false);
            droneClip.SetData(droneData, 0);

            // 3. UI Hover
            int hoverSamples = (int)(sampleRate * 0.045f);
            float[] hoverData = new float[hoverSamples];
            for (int i = 0; i < hoverSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                hoverData[i] = Mathf.Sin(2f * Mathf.PI * 480f * t) * env * 0.5f;
            }
            hoverClip = AudioClip.Create("GO_Hover", hoverSamples, 1, sampleRate, false);
            hoverClip.SetData(hoverData, 0);

            // 4. UI Select
            int selectSamples = (int)(sampleRate * 0.25f);
            float[] selectData = new float[selectSamples];
            for (int i = 0; i < selectSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 8f);
                selectData[i] = Mathf.Sin(2f * Mathf.PI * 140f * t) * env * 0.7f;
            }
            selectClip = AudioClip.Create("GO_Select", selectSamples, 1, sampleRate, false);
            selectClip.SetData(selectData, 0);
        }
    }
}
