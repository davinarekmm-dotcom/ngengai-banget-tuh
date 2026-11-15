using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Room0317.UI;

namespace Room0317.Editor
{
    /// <summary>
    /// Builder script that creates the AAA Main Menu scene (00_MainMenu.unity)
    /// styled precisely after the reference art:
    /// - Atmospheric hallway backdrop with dresser lamp and bloody door
    /// - Elegant "ROOM 03:17" logo with pulsing crimson red timestamp
    /// - Interactive navigation buttons (Start Game, Continue, Settings, Quit)
    /// - Glowing crimson selector notch and highlight bar
    /// - Settings modal (audio sliders, fullscreen toggle)
    /// - Cinematic scene transition to 03_Room0317_Game
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/00_MainMenu.unity";
        public const string BgPath = "Assets/_Project/UI/MainMenu/main_menu_bg.png";
        public const string RefPath = "Assets/_Project/UI/MainMenu/main_menu_reference.jpg";

        static MainMenuSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
                if (!File.Exists(ScenePath))
                {
                    BuildMainMenuScene();
                }
            };
        }

        [MenuItem("ROOM 03:17/Build Main Menu Scene (Matches Reference)")]
        public static void BuildMainMenuScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MainMenuSceneBuilder] Cannot build scenes during play mode.");
                return;
            }

            // 1. Configure texture import settings
            EnsureSpriteImportSettings(BgPath);
            EnsureSpriteImportSettings(RefPath);
            AssetDatabase.Refresh();

            // 2. Create and open new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Main Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.035f, 0.045f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;

            // 4. EventSystem
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var uiMod = esGo.GetComponent<InputSystemUIInputModule>();
            if (uiMod != null) uiMod.AssignDefaultActions();

            // 5. Canvas
            var canvasGo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 6. Font
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 7. Background Image
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BgPath);
            if (bgSprite == null) bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RefPath);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = bgSprite;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // 8. Subtle Horror Vignette Overlay
            var vigGo = new GameObject("Vignette_Overlay", typeof(RectTransform), typeof(Image));
            vigGo.transform.SetParent(canvasGo.transform, false);
            var vigRt = (RectTransform)vigGo.transform;
            vigRt.anchorMin = Vector2.zero;
            vigRt.anchorMax = Vector2.one;
            vigRt.offsetMin = Vector2.zero;
            vigRt.offsetMax = Vector2.zero;

            var vigImg = vigGo.GetComponent<Image>();
            vigImg.color = new Color(0f, 0f, 0f, 0.28f);
            vigImg.raycastTarget = false;

            // 9. Menu Root (Positioned precisely on the left wall to match reference)
            var menuPanelGo = new GameObject("Menu_Panel", typeof(RectTransform));
            menuPanelGo.transform.SetParent(canvasGo.transform, false);
            var menuPanelRt = (RectTransform)menuPanelGo.transform;
            menuPanelRt.anchorMin = new Vector2(0f, 0f);
            menuPanelRt.anchorMax = new Vector2(0f, 1f);
            menuPanelRt.pivot = new Vector2(0f, 0.5f);
            // Left margin 115px, vertically centered
            menuPanelRt.anchoredPosition = new Vector2(115f, 0f);
            menuPanelRt.sizeDelta = new Vector2(460f, 1080f);

            // 10. Title Container: "ROOM 03:17"
            var titleRowGo = new GameObject("Title_Row", typeof(RectTransform));
            titleRowGo.transform.SetParent(menuPanelGo.transform, false);
            var titleRowRt = (RectTransform)titleRowGo.transform;
            titleRowRt.anchorMin = new Vector2(0f, 0.5f);
            titleRowRt.anchorMax = new Vector2(0f, 0.5f);
            titleRowRt.pivot = new Vector2(0f, 0.5f);
            titleRowRt.anchoredPosition = new Vector2(0f, 210f);
            titleRowRt.sizeDelta = new Vector2(440f, 85f);

            // "ROOM " Text
            var roomTextGo = new GameObject("Text_Room", typeof(RectTransform), typeof(Text));
            roomTextGo.transform.SetParent(titleRowGo.transform, false);
            var roomTextRt = (RectTransform)roomTextGo.transform;
            roomTextRt.anchorMin = new Vector2(0f, 0f);
            roomTextRt.anchorMax = new Vector2(0f, 1f);
            roomTextRt.pivot = new Vector2(0f, 0.5f);
            roomTextRt.anchoredPosition = new Vector2(0f, 0f);
            roomTextRt.sizeDelta = new Vector2(205f, 85f);

            var roomText = roomTextGo.GetComponent<Text>();
            roomText.font = font;
            roomText.text = "ROOM";
            roomText.fontSize = 68;
            roomText.fontStyle = FontStyle.Bold;
            roomText.color = new Color(0.93f, 0.91f, 0.88f, 1f); // Crisp off-white
            roomText.alignment = TextAnchor.MiddleLeft;

            // "03:17" Text
            var timeTextGo = new GameObject("Text_Time", typeof(RectTransform), typeof(Text));
            timeTextGo.transform.SetParent(titleRowGo.transform, false);
            var timeTextRt = (RectTransform)timeTextGo.transform;
            timeTextRt.anchorMin = new Vector2(0f, 0f);
            timeTextRt.anchorMax = new Vector2(0f, 1f);
            timeTextRt.pivot = new Vector2(0f, 0.5f);
            timeTextRt.anchoredPosition = new Vector2(215f, 0f);
            timeTextRt.sizeDelta = new Vector2(225f, 85f);

            var timeText = timeTextGo.GetComponent<Text>();
            timeText.font = font;
            timeText.text = "03:17";
            timeText.fontSize = 68;
            timeText.fontStyle = FontStyle.Bold;
            timeText.color = new Color(0.85f, 0.22f, 0.22f, 1f); // Ominous Crimson Red
            timeText.alignment = TextAnchor.MiddleLeft;

            // 11. Title Divider Line
            var titleDivGo = new GameObject("Title_Divider", typeof(RectTransform), typeof(Image));
            titleDivGo.transform.SetParent(menuPanelGo.transform, false);
            var titleDivRt = (RectTransform)titleDivGo.transform;
            titleDivRt.anchorMin = new Vector2(0f, 0.5f);
            titleDivRt.anchorMax = new Vector2(0f, 0.5f);
            titleDivRt.pivot = new Vector2(0f, 0.5f);
            titleDivRt.anchoredPosition = new Vector2(0f, 155f);
            titleDivRt.sizeDelta = new Vector2(400f, 2f);

            var titleDivImg = titleDivGo.GetComponent<Image>();
            titleDivImg.color = new Color(0.38f, 0.16f, 0.16f, 0.85f); // Dark subtle red border
            titleDivImg.raycastTarget = false;

            // 12. Buttons Container
            var buttonsContainerGo = new GameObject("Buttons_Container", typeof(RectTransform));
            buttonsContainerGo.transform.SetParent(menuPanelGo.transform, false);
            var buttonsContainerRt = (RectTransform)buttonsContainerGo.transform;
            buttonsContainerRt.anchorMin = new Vector2(0f, 0.5f);
            buttonsContainerRt.anchorMax = new Vector2(0f, 0.5f);
            buttonsContainerRt.pivot = new Vector2(0f, 0.5f);
            buttonsContainerRt.anchoredPosition = new Vector2(0f, -40f);
            buttonsContainerRt.sizeDelta = new Vector2(400f, 320f);

            // 13. Highlight Plate (With dark crimson plate and bright red glowing left notch)
            var highlightPlateGo = new GameObject("Highlight_Plate", typeof(RectTransform), typeof(Image));
            highlightPlateGo.transform.SetParent(buttonsContainerGo.transform, false);
            var highlightPlateRt = (RectTransform)highlightPlateGo.transform;
            highlightPlateRt.anchorMin = new Vector2(0f, 1f);
            highlightPlateRt.anchorMax = new Vector2(0f, 1f);
            highlightPlateRt.pivot = new Vector2(0f, 0.5f);
            highlightPlateRt.anchoredPosition = new Vector2(0f, -32f);
            highlightPlateRt.sizeDelta = new Vector2(400f, 58f);

            var plateImg = highlightPlateGo.GetComponent<Image>();
            plateImg.color = new Color(0.25f, 0.08f, 0.08f, 0.82f); // Dark crimson translucent plate
            plateImg.raycastTarget = false;

            // Glowing Left Notch
            var notchGo = new GameObject("Glow_Notch", typeof(RectTransform), typeof(Image));
            notchGo.transform.SetParent(highlightPlateGo.transform, false);
            var notchRt = (RectTransform)notchGo.transform;
            notchRt.anchorMin = new Vector2(0f, 0f);
            notchRt.anchorMax = new Vector2(0f, 1f);
            notchRt.pivot = new Vector2(0f, 0.5f);
            notchRt.anchoredPosition = Vector2.zero;
            notchRt.sizeDelta = new Vector2(4f, 0f);

            var notchImg = notchGo.GetComponent<Image>();
            notchImg.color = new Color(0.95f, 0.22f, 0.22f, 1f); // Vibrant glowing red notch
            notchImg.raycastTarget = false;

            // 14. Create Buttons: Start Game, Continue, Settings, Quit
            string[] buttonLabels = { "Start Game", "Continue", "Settings", "Quit" };
            string[] buttonActions = { "Start", "Continue", "Settings", "Quit" };
            float startY = -32f;
            float stepY = 64f;

            var buttonEntries = new List<MainMenuController.MainMenuButtonEntry>();

            for (int i = 0; i < buttonLabels.Length; i++)
            {
                float btnY = startY - (i * stepY);

                // Button GameObject
                var btnGo = new GameObject("Button_" + buttonActions[i], typeof(RectTransform), typeof(Button));
                btnGo.transform.SetParent(buttonsContainerGo.transform, false);
                var btnRt = (RectTransform)btnGo.transform;
                btnRt.anchorMin = new Vector2(0f, 1f);
                btnRt.anchorMax = new Vector2(0f, 1f);
                btnRt.pivot = new Vector2(0f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0f, btnY);
                btnRt.sizeDelta = new Vector2(400f, 56f);

                var btn = btnGo.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;

                // Transparent background image for raycast detection
                var raycastImg = btnGo.AddComponent<Image>();
                raycastImg.color = Color.clear;

                // Button Label Text
                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(btnGo.transform, false);
                var lblRt = (RectTransform)lblGo.transform;
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = new Vector2(24f, 0f); // Indent slightly to clear the notch
                lblRt.offsetMax = new Vector2(-10f, 0f);

                var lbl = lblGo.GetComponent<Text>();
                lbl.font = font;
                lbl.text = buttonLabels[i];
                lbl.fontSize = 28;
                lbl.fontStyle = FontStyle.Normal;
                lbl.color = (i == 0) ? Color.white : new Color(0.85f, 0.82f, 0.78f, 0.95f);
                lbl.alignment = TextAnchor.MiddleLeft;

                buttonEntries.Add(new MainMenuController.MainMenuButtonEntry
                {
                    button = btn,
                    label = lbl,
                    rectTransform = btnRt,
                    actionName = buttonActions[i]
                });

                // Divider Line beneath button (except after the last one)
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
                    divImg.color = new Color(0.24f, 0.16f, 0.16f, 0.65f); // Subtle dark line
                    divImg.raycastTarget = false;
                }
            }

            // 15. Settings Modal (Initially Inactive)
            var settingsModalGo = BuildSettingsModal(canvasGo.transform, font);
            settingsModalGo.SetActive(false);

            // 16. Screen Fader (Full-screen black overlay)
            var faderGo = new GameObject("Screen_Fader", typeof(RectTransform), typeof(Image));
            faderGo.transform.SetParent(canvasGo.transform, false);
            var faderRt = (RectTransform)faderGo.transform;
            faderRt.anchorMin = Vector2.zero;
            faderRt.anchorMax = Vector2.one;
            faderRt.offsetMin = Vector2.zero;
            faderRt.offsetMax = Vector2.zero;

            var faderImg = faderGo.GetComponent<Image>();
            faderImg.color = Color.black;
            faderImg.raycastTarget = false;

            // 17. MainMenuController Script & Audio Sources
            var managerGo = new GameObject("--- MAIN MENU CONTROLLER ---");
            var menuController = managerGo.AddComponent<MainMenuController>();

            var ambientSrc = managerGo.AddComponent<AudioSource>();
            ambientSrc.playOnAwake = false;
            ambientSrc.loop = true;

            var sfxSrc = managerGo.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.loop = false;

            // Wire Serialized Properties
            var so = new SerializedObject(menuController);
            so.FindProperty("gameSceneName").stringValue = "03_Room0317_Game";
            so.FindProperty("titleTimeText").objectReferenceValue = timeText;
            so.FindProperty("highlightPlate").objectReferenceValue = highlightPlateRt;
            so.FindProperty("highlightGlowEdge").objectReferenceValue = notchImg;
            so.FindProperty("screenFader").objectReferenceValue = faderImg;
            so.FindProperty("ambientSource").objectReferenceValue = ambientSrc;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSrc;
            so.FindProperty("settingsModal").objectReferenceValue = settingsModalGo;

            // Wire buttons list
            var buttonsProp = so.FindProperty("menuButtons");
            buttonsProp.arraySize = buttonEntries.Count;
            for (int i = 0; i < buttonEntries.Count; i++)
            {
                var elem = buttonsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("button").objectReferenceValue = buttonEntries[i].button;
                elem.FindPropertyRelative("label").objectReferenceValue = buttonEntries[i].label;
                elem.FindPropertyRelative("rectTransform").objectReferenceValue = buttonEntries[i].rectTransform;
                elem.FindPropertyRelative("actionName").stringValue = buttonEntries[i].actionName;
            }

            // Wire settings modal components
            var masterSlider = settingsModalGo.transform.Find("Card/Slider_Master")?.GetComponent<Slider>();
            var ambienceSlider = settingsModalGo.transform.Find("Card/Slider_Ambience")?.GetComponent<Slider>();
            var fullscreenToggle = settingsModalGo.transform.Find("Card/Toggle_Fullscreen")?.GetComponent<Toggle>();
            var settingsCloseBtn = settingsModalGo.transform.Find("Card/Button_Close")?.GetComponent<Button>();

            if (masterSlider != null) so.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            if (ambienceSlider != null) so.FindProperty("ambienceSlider").objectReferenceValue = ambienceSlider;
            if (fullscreenToggle != null) so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
            if (settingsCloseBtn != null) so.FindProperty("settingsCloseButton").objectReferenceValue = settingsCloseBtn;

            so.ApplyModifiedProperties();

            // 18. Save Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);

            // 19. Ensure Scene is registered in EditorBuildSettings as Scene 0
            RegisterSceneInBuildSettings(ScenePath);

            Debug.Log($"<color=green><b>[ROOM 03:17]</b> Successfully constructed AAA Main Menu Scene at {ScenePath}!</color>");
        }

        private static GameObject BuildSettingsModal(Transform canvasParent, Font font)
        {
            var modalRoot = new GameObject("Settings_Modal", typeof(RectTransform), typeof(Image));
            modalRoot.transform.SetParent(canvasParent, false);
            var modalRt = (RectTransform)modalRoot.transform;
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;

            var bg = modalRoot.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.03f, 0.88f); // Frosted dark horror backdrop

            // Modal Card Box
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(modalRoot.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(580f, 440f);

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.08f, 0.07f, 0.10f, 0.96f);

            // Title
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

            // Master Volume Slider Row
            CreateSliderRow(cardGo.transform, "Slider_Master", "Master Volume", -90f, font);
            // Ambience Volume Slider Row
            CreateSliderRow(cardGo.transform, "Slider_Ambience", "Ambience Volume", -170f, font);
            // Fullscreen Toggle Row
            CreateToggleRow(cardGo.transform, "Toggle_Fullscreen", "Fullscreen Mode", -250f, font);

            // Close / Back Button
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
            closeLblRt.offsetMin = Vector2.zero;
            closeLblRt.offsetMax = Vector2.zero;

            var closeLbl = closeLblGo.GetComponent<Text>();
            closeLbl.font = font;
            closeLbl.text = "Back to Menu";
            closeLbl.fontSize = 20;
            closeLbl.fontStyle = FontStyle.Bold;
            closeLbl.color = Color.white;
            closeLbl.alignment = TextAnchor.MiddleCenter;

            return modalRoot;
        }

        private static void CreateSliderRow(Transform parent, string name, string labelText, float yOffset, Font font)
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

            // Label
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(rowGo.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0f, 0.5f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.anchoredPosition = new Vector2(0f, 0f);
            lblRt.sizeDelta = new Vector2(180f, 32f);

            var lbl = lblGo.GetComponent<Text>();
            lbl.font = font;
            lbl.text = labelText;
            lbl.fontSize = 20;
            lbl.color = new Color(0.85f, 0.82f, 0.78f);
            lbl.alignment = TextAnchor.MiddleLeft;

            // Slider Background bar
            var barGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(rowGo.transform, false);
            var barRt = (RectTransform)barGo.transform;
            barRt.anchorMin = new Vector2(0.42f, 0.35f);
            barRt.anchorMax = new Vector2(1f, 0.65f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;

            var barImg = barGo.GetComponent<Image>();
            barImg.color = new Color(0.18f, 0.16f, 0.20f, 0.9f);

            // Fill Area
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
            fillImg.color = new Color(0.85f, 0.25f, 0.25f, 1f); // Crimson Fill

            slider.fillRect = fillRt;
        }

        private static void CreateToggleRow(Transform parent, string name, string labelText, float yOffset, Font font)
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

            // Label
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(rowGo.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0f, 0.5f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.anchoredPosition = new Vector2(0f, 0f);
            lblRt.sizeDelta = new Vector2(240f, 32f);

            var lbl = lblGo.GetComponent<Text>();
            lbl.font = font;
            lbl.text = labelText;
            lbl.fontSize = 20;
            lbl.color = new Color(0.85f, 0.82f, 0.78f);
            lbl.alignment = TextAnchor.MiddleLeft;

            // Toggle Box
            var boxGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(rowGo.transform, false);
            var boxRt = (RectTransform)boxGo.transform;
            boxRt.anchorMin = new Vector2(1f, 0.5f);
            boxRt.anchorMax = new Vector2(1f, 0.5f);
            boxRt.pivot = new Vector2(1f, 0.5f);
            boxRt.anchoredPosition = Vector2.zero;
            boxRt.sizeDelta = new Vector2(28f, 28f);

            var boxImg = boxGo.GetComponent<Image>();
            boxImg.color = new Color(0.18f, 0.16f, 0.20f, 0.95f);

            // Checkmark
            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(boxGo.transform, false);
            var checkRt = (RectTransform)checkGo.transform;
            checkRt.anchorMin = new Vector2(0.2f, 0.2f);
            checkRt.anchorMax = new Vector2(0.8f, 0.8f);
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;

            var checkImg = checkGo.GetComponent<Image>();
            checkImg.color = new Color(0.95f, 0.25f, 0.25f, 1f);

            toggle.graphic = checkImg;
            toggle.targetGraphic = boxImg;
            toggle.isOn = true;
        }

        private static void EnsureSpriteImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }
                if (importer.maxTextureSize < 2048)
                {
                    importer.maxTextureSize = 2048;
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Remove existing duplicate if present
            scenes.RemoveAll(s => s.path == scenePath);

            // Insert 00_MainMenu as Scene 0
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));

            // Ensure 03_Room0317_Game is also registered right after
            string gameScene = "Assets/_Project/Scenes/03_Room0317_Game.unity";
            if (!scenes.Exists(s => s.path == gameScene))
            {
                scenes.Add(new EditorBuildSettingsScene(gameScene, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
