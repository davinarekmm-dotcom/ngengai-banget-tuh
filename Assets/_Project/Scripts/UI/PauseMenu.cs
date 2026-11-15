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
using Room0317.Dialogue;
using Room0317.Inventory;
using Room0317.Objectives;

namespace Room0317.UI
{
    /// <summary>
    /// AAA Psychological Horror Pause Menu for ROOM 03:17.
    /// Matches the iconic visual identity of the Main Menu:
    /// - Glowing crimson selector notch and dark highlight plate
    /// - Off-white "ROOM" with breathing crimson "03:17" logo
    /// - Active objective tracker and controls guide
    /// - Sound & display settings overlay
    /// - Scene navigation (Resume, Settings, Restart, Main Menu, Quit)
    /// - Mouse, Keyboard, and Gamepad navigation with audio feedback
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        private GameObject root;
        private Text titleTimeText;
        private Text objectiveReminderText;
        private RectTransform highlightPlate;
        private GameObject settingsModal;
        private Button settingsCloseButton;
        private Slider masterSlider;
        private Slider ambienceSlider;
        private Toggle fullscreenToggle;
        private Toggle screenShakeToggle;

        private AudioSource uiAudioSource;
        private AudioClip hoverClip;
        private AudioClip selectClip;

        private DialogueBox dialogueBox;
        private bool isPaused = false;
        private bool isSettingsOpen = false;
        private int selectedIndex = 0;
        private float lastClickTime = -1f;

        private readonly List<PauseButtonEntry> menuButtons = new List<PauseButtonEntry>();
        private readonly Color timeRedDark = new Color(0.82f, 0.20f, 0.20f, 1f);
        private readonly Color timeRedBright = new Color(1.0f, 0.32f, 0.32f, 1f);

        public bool IsPaused => isPaused;

        public static event Action<bool> OnPauseStateChanged;

        private class PauseButtonEntry
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

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool pauseTriggered = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
                || (pad != null && (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame));

            if (pauseTriggered)
            {
                // If inspection modal is open, let it handle Esc first
                if (!isPaused && InspectionModal.Instance != null && InspectionModal.Instance.IsOpen)
                {
                    return;
                }

                // If dialogue is active, let it advance first
                if (!isPaused && dialogueBox != null && dialogueBox.IsOpen)
                {
                    return;
                }

                if (isPaused && isSettingsOpen)
                {
                    CloseSettings();
                    return;
                }

                TogglePause();
                return;
            }

            if (!isPaused) return;

            AnimateTitlePulse();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (isSettingsOpen)
            {
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
                {
                    CloseSettings();
                    return;
                }

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && settingsCloseButton != null)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)settingsCloseButton.transform, mousePos, null))
                    {
                        CloseSettings();
                        return;
                    }
                }
                return;
            }

            HandleMouseNavigation();
            HandleMenuNavigation();
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

        private void HandleMenuNavigation()
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

            if (playSound && uiAudioSource != null && hoverClip != null)
            {
                uiAudioSource.PlayOneShot(hoverClip, 0.45f);
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

        private void OnButtonClicked(int index)
        {
            if (index < 0 || index >= menuButtons.Count) return;
            if (Time.unscaledTime - lastClickTime < 0.15f) return;
            lastClickTime = Time.unscaledTime;

            if (uiAudioSource != null && selectClip != null)
            {
                uiAudioSource.PlayOneShot(selectClip, 0.65f);
            }

            string action = menuButtons[index].actionName;
            switch (action)
            {
                case "Resume":
                    SetPaused(false);
                    break;
                case "Settings":
                    OpenSettings();
                    break;
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

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool pause)
        {
            isPaused = pause;
            Time.timeScale = isPaused ? 0f : 1f;

            if (root != null)
            {
                root.SetActive(isPaused);
                if (isPaused)
                {
                    isSettingsOpen = false;
                    if (settingsModal != null) settingsModal.SetActive(false);
                    UpdateObjectiveReminder();
                    SetSelection(0, playSound: false, snap: true);
                }
            }

            OnPauseStateChanged?.Invoke(isPaused);
        }

        private void OpenSettings()
        {
            if (settingsModal == null) return;
            if (screenShakeToggle != null) screenShakeToggle.isOn = ApartmentManager.ScreenShakeEnabled;
            if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;
            isSettingsOpen = true;
            settingsModal.SetActive(true);
        }

        public void CloseSettings()
        {
            if (settingsModal == null) return;
            isSettingsOpen = false;
            settingsModal.SetActive(false);
            SetSelection(selectedIndex, playSound: false, snap: true);
        }

        private void AnimateTitlePulse()
        {
            if (titleTimeText == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.8f) + 1f) * 0.5f;
            titleTimeText.color = Color.Lerp(timeRedDark, timeRedBright, pulse);
        }

        private void UpdateObjectiveReminder()
        {
            if (objectiveReminderText != null)
            {
                if (ObjectiveManager.Instance != null && ObjectiveManager.Instance.CurrentObjective != null)
                {
                    var cur = ObjectiveManager.Instance.CurrentObjective;
                    string timeStr = ApartmentClock.Instance != null ? ApartmentClock.Instance.FormattedTime : "03:17 AM";
                    objectiveReminderText.text = $"<color=#F1FA8C><b>CURRENT OBJECTIVE:</b></color> {cur.Title}\n" +
                                                 $"<color=#A0A5B5>Time: {timeStr}   |   Hint: {cur.Hint}</color>";
                }
                else
                {
                    objectiveReminderText.text = "<color=#50FA7B><b>ROOM 03:17:</b></color> All current apartment objectives completed.";
                }
            }
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

            // Full-screen frosted dark vignette background
            root = new GameObject("PauseMenu_Root", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = root.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.035f, 0.94f);

            // Card Container
            var cardGo = new GameObject("CardContainer", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(root.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(760f, 660f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.06f, 0.055f, 0.08f, 0.98f);

            // Title Container: "ROOM 03:17"
            var titleRow = new GameObject("TitleRow", typeof(RectTransform));
            titleRow.transform.SetParent(cardGo.transform, false);
            var titleRowRt = (RectTransform)titleRow.transform;
            titleRowRt.anchorMin = new Vector2(0.5f, 1f);
            titleRowRt.anchorMax = new Vector2(0.5f, 1f);
            titleRowRt.pivot = new Vector2(0.5f, 1f);
            titleRowRt.anchoredPosition = new Vector2(0f, -18f);
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
            titleTimeText.color = new Color(0.85f, 0.22f, 0.22f, 1f);
            titleTimeText.alignment = TextAnchor.MiddleLeft;

            // Subtitle
            var subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(cardGo.transform, false);
            var subText = subGo.GetComponent<Text>();
            subText.font = font;
            subText.fontSize = 16;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.72f, 0.70f, 0.68f, 0.9f);
            subText.text = "✦ NIGHT SUSPENDED ✦";
            var subRt = subText.rectTransform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -68f);
            subRt.sizeDelta = new Vector2(0f, 22f);

            // Title Divider
            var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(cardGo.transform, false);
            var divRt = (RectTransform)divGo.transform;
            divRt.anchorMin = new Vector2(0.5f, 1f);
            divRt.anchorMax = new Vector2(0.5f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -96f);
            divRt.sizeDelta = new Vector2(620f, 2f);
            var divImg = divGo.GetComponent<Image>();
            divImg.color = new Color(0.38f, 0.16f, 0.16f, 0.85f);
            divImg.raycastTarget = false;

            // Objective Box
            var objBoxGo = new GameObject("ObjectiveBox", typeof(RectTransform), typeof(Image));
            objBoxGo.transform.SetParent(cardGo.transform, false);
            var objBoxRt = (RectTransform)objBoxGo.transform;
            objBoxRt.anchorMin = new Vector2(0.5f, 1f);
            objBoxRt.anchorMax = new Vector2(0.5f, 1f);
            objBoxRt.pivot = new Vector2(0.5f, 1f);
            objBoxRt.anchoredPosition = new Vector2(0f, -112f);
            objBoxRt.sizeDelta = new Vector2(620f, 68f);
            var objBoxImg = objBoxGo.GetComponent<Image>();
            objBoxImg.color = new Color(0.10f, 0.10f, 0.15f, 0.90f);

            var objTextGo = new GameObject("ObjectiveText", typeof(RectTransform), typeof(Text));
            objTextGo.transform.SetParent(objBoxGo.transform, false);
            objectiveReminderText = objTextGo.GetComponent<Text>();
            objectiveReminderText.font = font;
            objectiveReminderText.fontSize = 16;
            objectiveReminderText.alignment = TextAnchor.MiddleCenter;
            objectiveReminderText.color = new Color(0.95f, 0.95f, 0.9f, 1f);
            var objTextRt = objectiveReminderText.rectTransform;
            objTextRt.anchorMin = Vector2.zero;
            objTextRt.anchorMax = Vector2.one;
            objTextRt.offsetMin = new Vector2(14f, 4f);
            objTextRt.offsetMax = new Vector2(-14f, -4f);

            // Buttons Container
            var buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
            buttonsContainer.transform.SetParent(cardGo.transform, false);
            var buttonsContainerRt = (RectTransform)buttonsContainer.transform;
            buttonsContainerRt.anchorMin = new Vector2(0.5f, 1f);
            buttonsContainerRt.anchorMax = new Vector2(0.5f, 1f);
            buttonsContainerRt.pivot = new Vector2(0.5f, 1f);
            buttonsContainerRt.anchoredPosition = new Vector2(0f, -195f);
            buttonsContainerRt.sizeDelta = new Vector2(440f, 290f);

            // Highlight Plate with glowing red notch
            var highlightPlateGo = new GameObject("Highlight_Plate", typeof(RectTransform), typeof(Image));
            highlightPlateGo.transform.SetParent(buttonsContainer.transform, false);
            highlightPlate = (RectTransform)highlightPlateGo.transform;
            highlightPlate.anchorMin = new Vector2(0f, 1f);
            highlightPlate.anchorMax = new Vector2(0f, 1f);
            highlightPlate.pivot = new Vector2(0f, 0.5f);
            highlightPlate.sizeDelta = new Vector2(440f, 48f);
            var plateImg = highlightPlateGo.GetComponent<Image>();
            plateImg.color = new Color(0.25f, 0.08f, 0.08f, 0.82f);
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
            notchImg.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            notchImg.raycastTarget = false;

            // Buttons: Resume, Settings, Restart, Main Menu, Quit
            string[] buttonLabels = { "Resume Night", "Settings", "Restart Apartment", "Return to Main Menu", "Quit to Desktop" };
            string[] buttonActions = { "Resume", "Settings", "Restart", "MainMenu", "Quit" };
            float startY = -24f;
            float stepY = 52f;

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
                btnRt.sizeDelta = new Vector2(440f, 46f);

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
                lblRt.offsetMin = new Vector2(22f, 0f);
                lblRt.offsetMax = new Vector2(-10f, 0f);
                var lbl = lblGo.GetComponent<Text>();
                lbl.font = font;
                lbl.text = buttonLabels[i];
                lbl.fontSize = 22;
                lbl.color = (i == 0) ? Color.white : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.raycastTarget = false;

                menuButtons.Add(new PauseButtonEntry
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
                    sepRt.anchoredPosition = new Vector2(0f, btnY - 26f);
                    sepRt.sizeDelta = new Vector2(440f, 1.5f);
                    var sepImg = sepGo.GetComponent<Image>();
                    sepImg.color = new Color(0.25f, 0.16f, 0.16f, 0.65f);
                    sepImg.raycastTarget = false;
                }
            }

            // Controls Guide Footer Card
            var controlsCard = new GameObject("ControlsCard", typeof(RectTransform), typeof(Image));
            controlsCard.transform.SetParent(cardGo.transform, false);
            var controlsCardRt = (RectTransform)controlsCard.transform;
            controlsCardRt.anchorMin = new Vector2(0.5f, 0f);
            controlsCardRt.anchorMax = new Vector2(0.5f, 0f);
            controlsCardRt.pivot = new Vector2(0.5f, 0f);
            controlsCardRt.anchoredPosition = new Vector2(0f, 20f);
            controlsCardRt.sizeDelta = new Vector2(620f, 75f);
            var controlsCardImg = controlsCard.GetComponent<Image>();
            controlsCardImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            var controlsTextGo = new GameObject("ControlsText", typeof(RectTransform), typeof(Text));
            controlsTextGo.transform.SetParent(controlsCard.transform, false);
            var controlsText = controlsTextGo.GetComponent<Text>();
            controlsText.font = font;
            controlsText.fontSize = 15;
            controlsText.alignment = TextAnchor.MiddleCenter;
            controlsText.color = new Color(0.78f, 0.78f, 0.75f, 0.95f);
            controlsText.text = "<b>WASD / Arrows</b>: Move Adrian   |   <b>E / Space</b>: Interact / Examine   |   <b>Esc / P</b>: Pause";
            var controlsTextRt = controlsText.rectTransform;
            controlsTextRt.anchorMin = Vector2.zero;
            controlsTextRt.anchorMax = Vector2.one;

            // Settings Modal
            settingsModal = BuildSettingsModal(canvas.transform, font);
            settingsModal.SetActive(false);

            root.SetActive(false);
        }

        private GameObject BuildSettingsModal(Transform canvasParent, Font font)
        {
            var modalRoot = new GameObject("PauseSettings_Modal", typeof(RectTransform), typeof(Image));
            modalRoot.transform.SetParent(canvasParent, false);
            var modalRt = (RectTransform)modalRoot.transform;
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            var bg = modalRoot.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.03f, 0.92f);

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(modalRoot.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(580f, 490f);
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.07f, 0.10f, 0.98f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -24f);
            titleRt.sizeDelta = new Vector2(500f, 40f);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.text = "✦ SETTINGS ✦";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.92f, 0.88f, 0.80f);
            titleText.alignment = TextAnchor.MiddleCenter;

            masterSlider = CreateSliderRow(cardGo.transform, "Slider_Master", "Master Volume", -90f, font);
            ambienceSlider = CreateSliderRow(cardGo.transform, "Slider_Ambience", "Ambience Volume", -170f, font);
            fullscreenToggle = CreateToggleRow(cardGo.transform, "Toggle_Fullscreen", "Fullscreen Mode", -250f, font);
            screenShakeToggle = CreateToggleRow(cardGo.transform, "Toggle_ScreenShake", "Screen Shake", -315f, font);

            if (masterSlider != null)
            {
                masterSlider.value = AudioListener.volume;
                masterSlider.onValueChanged.AddListener((v) => AudioListener.volume = v);
            }

            if (ambienceSlider != null && AudioManager.Instance != null)
            {
                ambienceSlider.value = AudioManager.Instance.AmbientVolume;
                ambienceSlider.onValueChanged.AddListener((v) => AudioManager.Instance.AmbientVolume = v);
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener((isFull) => Screen.fullScreen = isFull);
            }

            if (screenShakeToggle != null)
            {
                screenShakeToggle.isOn = ApartmentManager.ScreenShakeEnabled;
                screenShakeToggle.onValueChanged.AddListener((enabled) => ApartmentManager.ScreenShakeEnabled = enabled);
            }

            var closeGo = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(cardGo.transform, false);
            var closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 30f);
            closeRt.sizeDelta = new Vector2(220f, 46f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.24f, 0.10f, 0.10f, 0.92f);

            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(CloseSettings);
            settingsCloseButton = closeBtn;

            var closeLblGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            closeLblGo.transform.SetParent(closeGo.transform, false);
            var closeLblRt = (RectTransform)closeLblGo.transform;
            closeLblRt.anchorMin = Vector2.zero;
            closeLblRt.anchorMax = Vector2.one;
            var closeLbl = closeLblGo.GetComponent<Text>();
            closeLbl.font = font;
            closeLbl.text = "Back to Menu";
            closeLbl.fontSize = 20;
            closeLbl.fontStyle = FontStyle.Bold;
            closeLbl.raycastTarget = false;
            closeLbl.color = Color.white;
            closeLbl.alignment = TextAnchor.MiddleCenter;

            return modalRoot;
        }

        private Slider CreateSliderRow(Transform parent, string name, string labelText, float yOffset, Font font)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yOffset);
            rowRt.sizeDelta = new Vector2(460f, 40f);

            var slider = rowGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(rowGo.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0f, 0.5f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.sizeDelta = new Vector2(180f, 32f);
            var lbl = lblGo.GetComponent<Text>();
            lbl.font = font;
            lbl.text = labelText;
            lbl.fontSize = 20;
            lbl.color = new Color(0.85f, 0.82f, 0.78f);
            lbl.alignment = TextAnchor.MiddleLeft;

            var barGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(rowGo.transform, false);
            var barRt = (RectTransform)barGo.transform;
            barRt.anchorMin = new Vector2(0.42f, 0.35f);
            barRt.anchorMax = new Vector2(1f, 0.65f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;
            var barImg = barGo.GetComponent<Image>();
            barImg.color = new Color(0.18f, 0.16f, 0.20f, 0.9f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(rowGo.transform, false);
            var fillAreaRt = (RectTransform)fillArea.transform;
            fillAreaRt.anchorMin = new Vector2(0.42f, 0.35f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);

            slider.fillRect = fillRt;
            return slider;
        }

        private Toggle CreateToggleRow(Transform parent, string name, string labelText, float yOffset, Font font)
        {
            var rowGo = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            rowGo.transform.SetParent(parent, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yOffset);
            rowRt.sizeDelta = new Vector2(460f, 40f);

            var toggle = rowGo.GetComponent<Toggle>();

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(rowGo.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0f, 0.5f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.sizeDelta = new Vector2(240f, 32f);
            var lbl = lblGo.GetComponent<Text>();
            lbl.font = font;
            lbl.text = labelText;
            lbl.fontSize = 20;
            lbl.color = new Color(0.85f, 0.82f, 0.78f);
            lbl.alignment = TextAnchor.MiddleLeft;

            var boxGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(rowGo.transform, false);
            var boxRt = (RectTransform)boxGo.transform;
            boxRt.anchorMin = new Vector2(1f, 0.5f);
            boxRt.anchorMax = new Vector2(1f, 0.5f);
            boxRt.pivot = new Vector2(1f, 0.5f);
            boxRt.sizeDelta = new Vector2(28f, 28f);
            var boxImg = boxGo.GetComponent<Image>();
            boxImg.color = new Color(0.18f, 0.16f, 0.20f, 0.95f);

            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(boxGo.transform, false);
            var checkRt = (RectTransform)checkGo.transform;
            checkRt.anchorMin = new Vector2(0.2f, 0.2f);
            checkRt.anchorMax = new Vector2(0.8f, 0.8f);
            var checkImg = checkGo.GetComponent<Image>();
            checkImg.color = new Color(0.95f, 0.25f, 0.25f, 1f);

            toggle.graphic = checkImg;
            toggle.targetGraphic = boxImg;
            toggle.isOn = Screen.fullScreen;
            return toggle;
        }

        private void GenerateProceduralAudio()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
                uiAudioSource.playOnAwake = false;
                uiAudioSource.ignoreListenerPause = true;
            }

            int sampleRate = 44100;
            int hoverSamples = (int)(sampleRate * 0.045f);
            float[] hoverData = new float[hoverSamples];
            for (int i = 0; i < hoverSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                hoverData[i] = Mathf.Sin(2f * Mathf.PI * 520f * t) * env * 0.5f;
            }
            hoverClip = AudioClip.Create("Pause_Hover", hoverSamples, 1, sampleRate, false);
            hoverClip.SetData(hoverData, 0);

            int selectSamples = (int)(sampleRate * 0.28f);
            float[] selectData = new float[selectSamples];
            for (int i = 0; i < selectSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9f);
                selectData[i] = (Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.3f) * env * 0.7f;
            }
            selectClip = AudioClip.Create("Pause_Select", selectSamples, 1, sampleRate, false);
            selectClip.SetData(selectData, 0);
        }
    }
}
