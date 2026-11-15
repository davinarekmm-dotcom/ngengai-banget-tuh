using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Room0317.UI
{
    /// <summary>
    /// Master controller for ROOM 03:17 Main Menu.
    /// Manages the cinematic title screen, interactive menu buttons,
    /// glowing red cursor indicator, settings modal, and scene transitions.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [SerializeField] private string gameSceneName = "03_Room0317_Game";

        [Header("UI References")]
        [SerializeField] private Text titleTimeText;
        [SerializeField] private RectTransform highlightPlate;
        [SerializeField] private Image highlightGlowEdge;
        [SerializeField] private Image screenFader;
        [SerializeField] private List<MainMenuButtonEntry> menuButtons = new List<MainMenuButtonEntry>();

        [Header("Settings Modal")]
        [SerializeField] private GameObject settingsModal;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider ambienceSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsCloseButton;

        [Header("Audio")]
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource sfxSource;

        private int selectedIndex = 0;
        private bool isTransitioning = false;
        private bool isSettingsOpen = false;

        private readonly Color timeRedDark = new Color(0.82f, 0.20f, 0.20f, 1f);
        private readonly Color timeRedBright = new Color(1.0f, 0.32f, 0.32f, 1f);

        private AudioClip hoverSound;
        private AudioClip selectSound;
        private AudioClip stingerSound;

        [Serializable]
        public class MainMenuButtonEntry
        {
            public Button button;
            public Text label;
            public RectTransform rectTransform;
            public string actionName; // "Start", "Continue", "Settings", "Quit"
        }

        private void Awake()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            var uiMod = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiMod != null) uiMod.AssignDefaultActions();

            if (menuButtons == null || menuButtons.Count == 0 || highlightPlate == null)
            {
                BuildRuntimeUIIfEmpty();
            }

            GenerateProceduralAudio();
            ConfigureSettingsDefaults();
        }

        private void Start()
        {
            if (screenFader != null)
            {
                screenFader.gameObject.SetActive(true);
                StartCoroutine(FadeFromBlack(0.7f));
            }

            if (ambientSource != null && !ambientSource.isPlaying)
            {
                ambientSource.Play();
            }

            // Bind button clicks and hover triggers
            for (int i = 0; i < menuButtons.Count; i++)
            {
                int index = i;
                var entry = menuButtons[i];
                if (entry.button != null)
                {
                    entry.button.onClick.AddListener(() => OnButtonClicked(index));

                    // Add EventTrigger for mouse pointer enter
                    var trigger = entry.button.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null) trigger = entry.button.gameObject.AddComponent<EventTrigger>();

                    var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    pointerEnter.callback.AddListener((data) => OnButtonHovered(index));
                    trigger.triggers.Add(pointerEnter);
                }
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.AddListener(CloseSettings);
            }

            SetSelection(0, playSound: false, snapPosition: true);
        }

        private void Update()
        {
            AnimateTitlePulse();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (isTransitioning) return;

            if (isSettingsOpen)
            {
                var kbEsc = Keyboard.current;
                var padEsc = Gamepad.current;
                if ((kbEsc != null && kbEsc.escapeKey.wasPressedThisFrame) ||
                    (padEsc != null && padEsc.buttonEast.wasPressedThisFrame))
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
            HandleInputNavigation();
        }

        private void HandleMouseNavigation()
        {
            if (menuButtons.Count == 0 || Mouse.current == null || isTransitioning) return;

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

        public void SetSelection(int index, bool playSound = true, bool snapPosition = false)
        {
            if (index < 0 || index >= menuButtons.Count) return;

            selectedIndex = index;

            if (playSound && sfxSource != null && hoverSound != null)
            {
                sfxSource.PlayOneShot(hoverSound, 0.45f);
            }

            // Update text colors
            for (int i = 0; i < menuButtons.Count; i++)
            {
                if (menuButtons[i].label != null)
                {
                    menuButtons[i].label.color = (i == selectedIndex)
                        ? Color.white
                        : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                }
            }

            // Move highlight plate to selected button
            if (highlightPlate != null && menuButtons[selectedIndex].rectTransform != null)
            {
                var targetRt = menuButtons[selectedIndex].rectTransform;
                Vector2 targetPos = targetRt.anchoredPosition;

                if (snapPosition)
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

        public void OnButtonHovered(int index)
        {
            if (isTransitioning || isSettingsOpen) return;
            if (index != selectedIndex)
            {
                SetSelection(index, playSound: true);
            }
        }

        private float lastClickTime = -1f;

        public void OnButtonClicked(int index)
        {
            if (isTransitioning || isSettingsOpen) return;
            if (Time.unscaledTime - lastClickTime < 0.15f) return;
            lastClickTime = Time.unscaledTime;

            if (index < 0 || index >= menuButtons.Count) return;
            string action = menuButtons[index].actionName;

            switch (action.ToLowerInvariant())
            {
                case "start":
                case "continue":
                    StartCoroutine(ExecuteStartGame());
                    break;
                case "settings":
                    OpenSettings();
                    break;
                case "quit":
                    QuitGame();
                    break;
            }
        }

        private IEnumerator ExecuteStartGame()
        {
            isTransitioning = true;

            if (sfxSource != null && selectSound != null)
            {
                sfxSource.PlayOneShot(selectSound, 0.85f);
            }

            if (sfxSource != null && stingerSound != null)
            {
                sfxSource.PlayOneShot(stingerSound, 0.7f);
            }

            // Cinematic fade to black
            if (screenFader != null)
            {
                yield return StartCoroutine(FadeToBlack(0.85f));
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // Smooth async load of the main game scene
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                var op = SceneManager.LoadSceneAsync(gameSceneName);
                if (op != null)
                {
                    while (!op.isDone) yield return null;
                }
            }
        }

        private void OpenSettings()
        {
            if (settingsModal == null) return;
            isSettingsOpen = true;
            settingsModal.SetActive(true);

            if (sfxSource != null && selectSound != null)
            {
                sfxSource.PlayOneShot(selectSound, 0.6f);
            }
        }

        public void CloseSettings()
        {
            if (settingsModal == null) return;
            isSettingsOpen = false;
            settingsModal.SetActive(false);

            if (sfxSource != null && hoverSound != null)
            {
                sfxSource.PlayOneShot(hoverSound, 0.4f);
            }
        }

        private void QuitGame()
        {
            if (sfxSource != null && selectSound != null)
            {
                sfxSource.PlayOneShot(selectSound, 0.6f);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void AnimateTitlePulse()
        {
            if (titleTimeText == null) return;
            // Subtle breathing glow on the blood red '03:17'
            float pulse = (Mathf.Sin(Time.time * 2.8f) + 1f) * 0.5f;
            titleTimeText.color = Color.Lerp(timeRedDark, timeRedBright, pulse);
        }

        private IEnumerator FadeFromBlack(float duration)
        {
            if (screenFader == null) yield break;
            screenFader.color = Color.black;
            screenFader.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                screenFader.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            screenFader.gameObject.SetActive(false);
        }

        private IEnumerator FadeToBlack(float duration)
        {
            if (screenFader == null) yield break;
            screenFader.gameObject.SetActive(true);
            screenFader.color = new Color(0f, 0f, 0f, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                screenFader.color = new Color(0f, 0f, 0f, alpha);

                // Fade out ambient music
                if (ambientSource != null)
                {
                    ambientSource.volume = Mathf.Lerp(0.35f, 0f, alpha);
                }
                yield return null;
            }

            screenFader.color = Color.black;
        }

        private void ConfigureSettingsDefaults()
        {
            if (masterSlider != null)
            {
                masterSlider.value = AudioListener.volume;
                masterSlider.onValueChanged.AddListener((val) => AudioListener.volume = val);
            }

            if (ambienceSlider != null)
            {
                ambienceSlider.value = (ambientSource != null) ? ambientSource.volume : 0.4f;
                ambienceSlider.onValueChanged.AddListener((val) =>
                {
                    if (ambientSource != null) ambientSource.volume = val;
                });
            }

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener((isFull) => Screen.fullScreen = isFull);
            }

            if (settingsModal != null)
            {
                settingsModal.SetActive(false);
            }
        }

        private void GenerateProceduralAudio()
        {
            int sampleRate = 44100;

            // 1. Subtle UI Hover Click (Short resonant pulse)
            int hoverSamples = (int)(sampleRate * 0.045f);
            float[] hoverData = new float[hoverSamples];
            for (int i = 0; i < hoverSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                hoverData[i] = Mathf.Sin(2f * Mathf.PI * 520f * t) * env * 0.5f;
            }
            hoverSound = AudioClip.Create("UI_Hover", hoverSamples, 1, sampleRate, false);
            hoverSound.SetData(hoverData, 0);

            // 2. Button Confirm / Select (Low resonant horror tone)
            int selectSamples = (int)(sampleRate * 0.28f);
            float[] selectData = new float[selectSamples];
            for (int i = 0; i < selectSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9f);
                selectData[i] = (Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.3f) * env * 0.7f;
            }
            selectSound = AudioClip.Create("UI_Select", selectSamples, 1, sampleRate, false);
            selectSound.SetData(selectData, 0);

            // 3. Tension Transition Stinger
            int stingerSamples = (int)(sampleRate * 1.2f);
            float[] stingerData = new float[stingerSamples];
            for (int i = 0; i < stingerSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 2.2f);
                float f1 = Mathf.Sin(2f * Mathf.PI * 65f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 69.3f * t); // Dissonant minor second
                stingerData[i] = (f1 + f2) * 0.5f * env * 0.8f;
            }
            stingerSound = AudioClip.Create("UI_Stinger", stingerSamples, 1, sampleRate, false);
            stingerSound.SetData(stingerData, 0);

            // 4. Ambient Drone
            int droneSamples = sampleRate * 4;
            float[] droneData = new float[droneSamples];
            for (int i = 0; i < droneSamples; i++)
            {
                float t = (float)i / sampleRate;
                float w1 = Mathf.Sin(2f * Mathf.PI * 45f * t);
                float w2 = Mathf.Sin(2f * Mathf.PI * 47.5f * t) * 0.5f;
                float rumble = (Mathf.PerlinNoise(t * 1.5f, 0.5f) - 0.5f) * 0.2f;
                droneData[i] = (w1 + w2 + rumble) * 0.35f;
            }
            var droneClip = AudioClip.Create("Menu_AmbientDrone", droneSamples, 1, sampleRate, false);
            droneClip.SetData(droneData, 0);

            if (ambientSource == null) ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.clip = droneClip;
            ambientSource.volume = 0.35f;

            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        public void BuildRuntimeUIIfEmpty()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.035f, 0.045f, 1f);
                cam.orthographic = true;
                cam.orthographicSize = 5f;
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Background
            Sprite bgSprite = LoadMenuBackgroundSprite();
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = bgSprite;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Vignette Overlay
            var vigGo = new GameObject("Vignette_Overlay", typeof(RectTransform), typeof(Image));
            vigGo.transform.SetParent(canvas.transform, false);
            var vigRt = (RectTransform)vigGo.transform;
            vigRt.anchorMin = Vector2.zero;
            vigRt.anchorMax = Vector2.one;
            vigRt.offsetMin = Vector2.zero;
            vigRt.offsetMax = Vector2.zero;
            var vigImg = vigGo.GetComponent<Image>();
            vigImg.color = new Color(0f, 0f, 0f, 0.28f);
            vigImg.raycastTarget = false;

            // Menu Panel
            var menuPanelGo = new GameObject("Menu_Panel", typeof(RectTransform));
            menuPanelGo.transform.SetParent(canvas.transform, false);
            var menuPanelRt = (RectTransform)menuPanelGo.transform;
            menuPanelRt.anchorMin = new Vector2(0f, 0f);
            menuPanelRt.anchorMax = new Vector2(0f, 1f);
            menuPanelRt.pivot = new Vector2(0f, 0.5f);
            menuPanelRt.anchoredPosition = new Vector2(115f, 0f);
            menuPanelRt.sizeDelta = new Vector2(460f, 1080f);

            // Title Row: "ROOM 03:17"
            var titleRowGo = new GameObject("Title_Row", typeof(RectTransform));
            titleRowGo.transform.SetParent(menuPanelGo.transform, false);
            var titleRowRt = (RectTransform)titleRowGo.transform;
            titleRowRt.anchorMin = new Vector2(0f, 0.5f);
            titleRowRt.anchorMax = new Vector2(0f, 0.5f);
            titleRowRt.pivot = new Vector2(0f, 0.5f);
            titleRowRt.anchoredPosition = new Vector2(0f, 210f);
            titleRowRt.sizeDelta = new Vector2(440f, 85f);

            var roomTextGo = new GameObject("Text_Room", typeof(RectTransform), typeof(Text));
            roomTextGo.transform.SetParent(titleRowGo.transform, false);
            var roomTextRt = (RectTransform)roomTextGo.transform;
            roomTextRt.anchorMin = new Vector2(0f, 0f);
            roomTextRt.anchorMax = new Vector2(0f, 1f);
            roomTextRt.pivot = new Vector2(0f, 0.5f);
            roomTextRt.anchoredPosition = Vector2.zero;
            roomTextRt.sizeDelta = new Vector2(205f, 85f);
            var roomText = roomTextGo.GetComponent<Text>();
            roomText.font = font;
            roomText.text = "ROOM";
            roomText.fontSize = 68;
            roomText.fontStyle = FontStyle.Bold;
            roomText.color = new Color(0.93f, 0.91f, 0.88f, 1f);
            roomText.alignment = TextAnchor.MiddleLeft;

            var timeTextGo = new GameObject("Text_Time", typeof(RectTransform), typeof(Text));
            timeTextGo.transform.SetParent(titleRowGo.transform, false);
            var timeTextRt = (RectTransform)timeTextGo.transform;
            timeTextRt.anchorMin = new Vector2(0f, 0f);
            timeTextRt.anchorMax = new Vector2(0f, 1f);
            timeTextRt.pivot = new Vector2(0f, 0.5f);
            timeTextRt.anchoredPosition = new Vector2(215f, 0f);
            timeTextRt.sizeDelta = new Vector2(225f, 85f);
            titleTimeText = timeTextGo.GetComponent<Text>();
            titleTimeText.font = font;
            titleTimeText.text = "03:17";
            titleTimeText.fontSize = 68;
            titleTimeText.fontStyle = FontStyle.Bold;
            titleTimeText.color = new Color(0.85f, 0.22f, 0.22f, 1f);
            titleTimeText.alignment = TextAnchor.MiddleLeft;

            // Title Divider
            var titleDivGo = new GameObject("Title_Divider", typeof(RectTransform), typeof(Image));
            titleDivGo.transform.SetParent(menuPanelGo.transform, false);
            var titleDivRt = (RectTransform)titleDivGo.transform;
            titleDivRt.anchorMin = new Vector2(0f, 0.5f);
            titleDivRt.anchorMax = new Vector2(0f, 0.5f);
            titleDivRt.pivot = new Vector2(0f, 0.5f);
            titleDivRt.anchoredPosition = new Vector2(0f, 155f);
            titleDivRt.sizeDelta = new Vector2(400f, 2f);
            var titleDivImg = titleDivGo.GetComponent<Image>();
            titleDivImg.color = new Color(0.38f, 0.16f, 0.16f, 0.85f);
            titleDivImg.raycastTarget = false;

            // Buttons Container
            var buttonsContainerGo = new GameObject("Buttons_Container", typeof(RectTransform));
            buttonsContainerGo.transform.SetParent(menuPanelGo.transform, false);
            var buttonsContainerRt = (RectTransform)buttonsContainerGo.transform;
            buttonsContainerRt.anchorMin = new Vector2(0f, 0.5f);
            buttonsContainerRt.anchorMax = new Vector2(0f, 0.5f);
            buttonsContainerRt.pivot = new Vector2(0f, 0.5f);
            buttonsContainerRt.anchoredPosition = new Vector2(0f, -40f);
            buttonsContainerRt.sizeDelta = new Vector2(400f, 320f);

            // Highlight Plate
            var highlightPlateGo = new GameObject("Highlight_Plate", typeof(RectTransform), typeof(Image));
            highlightPlateGo.transform.SetParent(buttonsContainerGo.transform, false);
            highlightPlate = (RectTransform)highlightPlateGo.transform;
            highlightPlate.anchorMin = new Vector2(0f, 1f);
            highlightPlate.anchorMax = new Vector2(0f, 1f);
            highlightPlate.pivot = new Vector2(0f, 0.5f);
            highlightPlate.anchoredPosition = new Vector2(0f, -32f);
            highlightPlate.sizeDelta = new Vector2(400f, 58f);

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

            highlightGlowEdge = notchGo.GetComponent<Image>();
            highlightGlowEdge.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            highlightGlowEdge.raycastTarget = false;

            // Buttons creation
            string[] buttonLabels = { "Start Game", "Continue", "Settings", "Quit" };
            string[] buttonActions = { "Start", "Continue", "Settings", "Quit" };
            float startY = -32f;
            float stepY = 64f;

            menuButtons.Clear();
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                float btnY = startY - (i * stepY);
                var btnGo = new GameObject("Button_" + buttonActions[i], typeof(RectTransform), typeof(Button), typeof(Image));
                btnGo.transform.SetParent(buttonsContainerGo.transform, false);
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.anchorMin = new Vector2(0f, 1f);
                btnRt.anchorMax = new Vector2(0f, 1f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0f, btnY);
                btnRt.sizeDelta = new Vector2(400f, 56f);

                var raycastImg = btnGo.GetComponent<Image>();
                raycastImg.color = Color.clear;

                var btn = btnGo.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;

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
                lbl.fontSize = 28;
                lbl.color = (i == 0) ? Color.white : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.raycastTarget = false;

                menuButtons.Add(new MainMenuButtonEntry
                {
                    button = btn,
                    label = lbl,
                    rectTransform = btnRt,
                    actionName = buttonActions[i]
                });

                if (i < buttonLabels.Length - 1)
                {
                    var divGo = new GameObject("Divider_" + (i + 1), typeof(RectTransform), typeof(Image));
                    divGo.transform.SetParent(buttonsContainerGo.transform, false);
                    var divRt = (RectTransform)divGo.transform;
                    divRt.anchorMin = new Vector2(0f, 1f);
                    divRt.anchorMax = new Vector2(0f, 1f);
                    divRt.pivot = new Vector2(0f, 0.5f);
                    divRt.anchoredPosition = new Vector2(0f, btnY - 32f);
                    divRt.sizeDelta = new Vector2(400f, 1.5f);

                    var divImg = divGo.GetComponent<Image>();
                    divImg.color = new Color(0.24f, 0.16f, 0.16f, 0.65f);
                    divImg.raycastTarget = false;
                }
            }

            // Settings Modal
            settingsModal = BuildRuntimeSettingsModal(canvas.transform, font);
            settingsModal.SetActive(false);

            // Screen Fader
            var faderGo = new GameObject("Screen_Fader", typeof(RectTransform), typeof(Image));
            faderGo.transform.SetParent(canvas.transform, false);
            var faderRt = (RectTransform)faderGo.transform;
            faderRt.anchorMin = Vector2.zero;
            faderRt.anchorMax = Vector2.one;
            faderRt.offsetMin = Vector2.zero;
            faderRt.offsetMax = Vector2.zero;
            screenFader = faderGo.GetComponent<Image>();
            screenFader.color = Color.black;
            screenFader.raycastTarget = false;
        }

        private GameObject BuildRuntimeSettingsModal(Transform canvasParent, Font font)
        {
            var modalRoot = new GameObject("Settings_Modal", typeof(RectTransform), typeof(Image));
            modalRoot.transform.SetParent(canvasParent, false);
            var modalRt = (RectTransform)modalRoot.transform;
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            var bg = modalRoot.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.03f, 0.88f);

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(modalRoot.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(580f, 440f);
            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.07f, 0.10f, 0.96f);

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

            masterSlider = CreateRuntimeSliderRow(cardGo.transform, "Slider_Master", "Master Volume", -90f, font);
            ambienceSlider = CreateRuntimeSliderRow(cardGo.transform, "Slider_Ambience", "Ambience Volume", -170f, font);
            fullscreenToggle = CreateRuntimeToggleRow(cardGo.transform, "Toggle_Fullscreen", "Fullscreen Mode", -250f, font);

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
            closeLbl.color = Color.white;
            closeLbl.alignment = TextAnchor.MiddleCenter;

            settingsCloseButton = closeGo.GetComponent<Button>();
            return modalRoot;
        }

        private Slider CreateRuntimeSliderRow(Transform parent, string name, string labelText, float yOffset, Font font)
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

        private Toggle CreateRuntimeToggleRow(Transform parent, string name, string labelText, float yOffset, Font font)
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
            toggle.isOn = true;
            return toggle;
        }

        private Sprite LoadMenuBackgroundSprite()
        {
#if UNITY_EDITOR
            try
            {
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/MainMenu/main_menu_bg.png");
                if (s != null) return s;
                s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/MainMenu/main_menu_reference.jpg");
                if (s != null) return s;
            }
            catch { }
#endif
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "_Project/UI/MainMenu/main_menu_bg.png");
                if (!System.IO.File.Exists(path))
                {
                    path = System.IO.Path.Combine(Application.dataPath, "_Project/UI/MainMenu/main_menu_reference.jpg");
                }
                if (System.IO.File.Exists(path))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
