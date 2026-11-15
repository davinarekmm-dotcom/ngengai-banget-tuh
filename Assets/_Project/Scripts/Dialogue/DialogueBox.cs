using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Room0317.Apartment;
using Room0317.Audio;

namespace Room0317.Dialogue
{
    /// <summary>One expression = a row of 4 portrait frames that loop while the character is talking.</summary>
    [Serializable]
    public class PortraitExpression
    {
        public string name;
        public Sprite[] frames;
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker = "Adrian";
        public string expression = "Neutral";
        [TextArea(2, 4)] public string text;

        public DialogueLine() { }
        public DialogueLine(string speaker, string expression, string text)
        {
            this.speaker = speaker;
            this.expression = expression;
            this.text = text;
        }
    }

    /// <summary>
    /// Visual-novel style dialogue box: portrait sits above the box on its left side,
    /// name tag on the box's top-left edge, typewriter text, blinking continue arrow.
    /// The portrait cycles its expression frames while text is being typed.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject root;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Graphic continueIndicator;

        [Header("Portraits - Adrian")]
        [SerializeField] private List<PortraitExpression> expressions = new List<PortraitExpression>();
        [SerializeField] private float talkFrameRate = 8f;

        [Header("Portraits - Ghost Girl / Unknown Voice")]
        [SerializeField] private List<PortraitExpression> ghostGirlExpressions = new List<PortraitExpression>();
        [SerializeField] private Sprite[] ghostGirlFrames;

        [Header("Typing")]
        [SerializeField] private float charactersPerSecond = 40f;

        public static DialogueBox ActiveInstance { get; private set; }

        public bool IsOpen { get; private set; }
        public bool IsClosedRecently => (Time.unscaledTime - lastClosedTime) < 0.35f;
        public event Action OnDialogueFinished;

        private readonly Queue<DialogueLine> queue = new Queue<DialogueLine>();
        private Coroutine typingRoutine;
        private bool isTyping;
        private string fullText;
        private PortraitExpression currentExpression;
        private string currentExpressionName = "Neutral";
        private bool currentSpeakerUnknown;
        private float openedTime = -10f;
        private float lastClosedTime = -10f;
        private float lastBlipTime = -10f;
        private bool didDuckAmbient;

        private CanvasGroup rootCanvasGroup;
        private RectTransform rootRt;
        private Vector2 rootBaseAnchoredPos;
        private bool rootBasePosCaptured;

        private RectTransform bodyRt;
        private Vector2 bodyBaseAnchoredPos;
        private bool bodyBasePosCaptured;

        private Color baseNameColor = Color.white;
        private bool baseNameColorCaptured;

        private Coroutine boxAppearRoutine;
        private Coroutine scaredTremorRoutine;

        public void Configure(GameObject rootObj, Image portrait, Text nameLabel, Text body, Graphic indicator, List<PortraitExpression> exprs)
        {
            root = rootObj;
            portraitImage = portrait;
            nameText = nameLabel;
            bodyText = body;
            continueIndicator = indicator;
            expressions = exprs;
        }

        private void Awake()
        {
            ActiveInstance = this;
            if (root != null) root.SetActive(false);
            LoadGhostGirlPortraits();
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        public void StartDialogue(IEnumerable<DialogueLine> lines)
        {
            queue.Clear();
            foreach (var l in lines) queue.Enqueue(l);
            openedTime = Time.unscaledTime;
            IsOpen = true;
            didDuckAmbient = false;

            EnsureJuiceSetup();
            if (root != null) root.SetActive(true);

            if (boxAppearRoutine != null) StopCoroutine(boxAppearRoutine);
            boxAppearRoutine = StartCoroutine(AppearRoutine());

            ShowNext();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (continueIndicator != null)
            {
                continueIndicator.enabled = !isTyping && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
            }

            if (Time.unscaledTime - openedTime < 0.18f) return;
            if (Room0317.UI.PauseMenu.Instance != null && Room0317.UI.PauseMenu.Instance.IsPaused) return;
            if (Room0317.UI.PhoneNotificationUI.Instance != null && Room0317.UI.PhoneNotificationUI.Instance.IsShowing) return;
            if (Room0317.Inventory.InspectionModal.Instance != null && Room0317.Inventory.InspectionModal.Instance.IsOpen) return;

            if (AdvancePressed())
            {
                if (isTyping) CompleteTyping();
                else ShowNext();
            }
        }

        private static bool AdvancePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        private void ShowNext()
        {
            if (queue.Count == 0)
            {
                Close();
                return;
            }

            var line = queue.Dequeue();
            currentSpeakerUnknown = IsGhostOrUnknownSpeaker(line.speaker);
            currentExpressionName = string.IsNullOrEmpty(line.expression) ? "Neutral" : line.expression;

            if (nameText != null)
            {
                if (!baseNameColorCaptured)
                {
                    baseNameColor = nameText.color;
                    baseNameColorCaptured = true;
                }
                nameText.text = line.speaker;
                nameText.color = currentSpeakerUnknown
                    ? new Color(0.95f, 0.46f, 0.48f, 1f)
                    : baseNameColor;
            }

            if (currentSpeakerUnknown && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAmbientDuck(0.65f, 0.25f);
                didDuckAmbient = true;
            }

            currentExpression = FindExpression(line.speaker, line.expression);
            SetPortraitFrame(0);

            if (scaredTremorRoutine != null)
            {
                StopCoroutine(scaredTremorRoutine);
                scaredTremorRoutine = null;
            }
            if (bodyRt != null && bodyBasePosCaptured)
            {
                bodyRt.anchoredPosition = bodyBaseAnchoredPos;
            }

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = StartCoroutine(TypeLine(line.text));

            if (string.Equals(currentExpressionName, "Scared", StringComparison.OrdinalIgnoreCase))
            {
                scaredTremorRoutine = StartCoroutine(ScaredTremorRoutine());
            }
        }

        private IEnumerator TypeLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                fullText = string.Empty;
                CompleteTyping();
                yield break;
            }

            isTyping = true;
            fullText = text;
            if (bodyText != null) bodyText.text = string.Empty;

            float baseSpeed = charactersPerSecond * GetExpressionSpeedMultiplier(currentExpressionName);
            bool isNervous = string.Equals(currentExpressionName, "Nervous", StringComparison.OrdinalIgnoreCase);

            float charTimer = 0f;
            float frameTimer = 0f;
            float pauseTimer = 0f;
            int shown = 0;
            int frame = 0;

            while (shown < text.Length)
            {
                if (pauseTimer > 0f)
                {
                    pauseTimer -= Time.unscaledDeltaTime;
                }
                else
                {
                    float speed = baseSpeed;
                    if (isNervous)
                    {
                        float pulse = Mathf.Sin(Time.unscaledTime * 11f);
                        speed *= pulse < -0.45f ? 0.68f : 1.05f;
                    }

                    charTimer += Time.unscaledDeltaTime * speed;
                    while (charTimer >= 1f && shown < text.Length)
                    {
                        charTimer -= 1f;
                        shown++;

                        char ch = text[shown - 1];
                        if (char.IsLetterOrDigit(ch) && Time.unscaledTime - lastBlipTime >= 0.055f)
                        {
                            lastBlipTime = Time.unscaledTime;
                            if (AudioManager.Instance != null)
                            {
                                AudioManager.Instance.PlayDialogueBlip(currentSpeakerUnknown);
                            }
                        }

                        float punctPause = GetPunctuationPause(text, shown - 1);
                        if (punctPause > 0f)
                        {
                            pauseTimer = punctPause;
                            charTimer = 0f;
                            break;
                        }
                    }
                    if (bodyText != null) bodyText.text = text.Substring(0, shown);
                }

                // Talking animation: loop the expression frames.
                frameTimer += Time.unscaledDeltaTime * talkFrameRate;
                if (frameTimer >= 1f)
                {
                    frameTimer -= 1f;
                    frame++;
                    SetPortraitFrame(frame);
                }
                yield return null;
            }

            CompleteTyping();
        }

        private void CompleteTyping()
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            if (scaredTremorRoutine != null)
            {
                StopCoroutine(scaredTremorRoutine);
                scaredTremorRoutine = null;
            }
            if (bodyRt != null && bodyBasePosCaptured)
            {
                bodyRt.anchoredPosition = bodyBaseAnchoredPos;
            }
            if (bodyText != null) bodyText.text = fullText;
            isTyping = false;
            SetPortraitFrame(0); // rest on the first frame of the expression
        }

        private void SetPortraitFrame(int frame)
        {
            if (portraitImage == null) return;

            if (currentExpression == null || currentExpression.frames == null || currentExpression.frames.Length == 0)
            {
                portraitImage.gameObject.SetActive(false);
                return;
            }

            portraitImage.gameObject.SetActive(true);
            portraitImage.sprite = currentExpression.frames[frame % currentExpression.frames.Length];
        }

        private PortraitExpression FindExpression(string speaker, string expressionName)
        {
            if (IsGhostOrUnknownSpeaker(speaker))
            {
                // Ensure portraits are loaded
                if ((ghostGirlFrames == null || ghostGirlFrames.Length == 0) && (ghostGirlExpressions == null || ghostGirlExpressions.Count == 0))
                {
                    LoadGhostGirlPortraits();
                }

                if (ghostGirlExpressions != null && ghostGirlExpressions.Count > 0)
                {
                    foreach (var e in ghostGirlExpressions)
                        if (string.Equals(e.name, expressionName, StringComparison.OrdinalIgnoreCase))
                            return e;
                    return ghostGirlExpressions[0];
                }

                if (ghostGirlFrames != null && ghostGirlFrames.Length > 0)
                {
                    return new PortraitExpression { name = "Ghost", frames = ghostGirlFrames };
                }
            }

            // Default to Adrian expressions
            foreach (var e in expressions)
                if (string.Equals(e.name, expressionName, StringComparison.OrdinalIgnoreCase))
                    return e;
            return expressions.Count > 0 ? expressions[0] : null;
        }

        private PortraitExpression FindExpression(string expressionName)
        {
            return FindExpression("Adrian", expressionName);
        }

        public static bool IsGhostOrUnknownSpeaker(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return false;
            string s = speaker.Trim().ToLowerInvariant();
            return s.Contains("unknown") || s.Contains("ghost") || s.Contains("voice") || s.Contains("roommate") || s.Contains("???");
        }

        public void LoadGhostGirlPortraits()
        {
            if ((ghostGirlFrames == null || ghostGirlFrames.Length == 0) && (ghostGirlExpressions == null || ghostGirlExpressions.Count == 0))
            {
#if UNITY_EDITOR
                try
                {
                    var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Ghost Girl Portrait.png");
                    var sprites = new List<Sprite>();
                    if (assets != null)
                    {
                        foreach (var a in assets)
                        {
                            if (a is Sprite s)
                            {
                                // Filter out accidental small slices
                                if (s.rect.width > 100 && s.rect.height > 100)
                                {
                                    sprites.Add(s);
                                }
                            }
                        }
                    }

                    if (sprites.Count > 0)
                    {
                        sprites.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                        ghostGirlFrames = sprites.ToArray();
                    }
                }
                catch { }
#endif
                if (ghostGirlFrames == null || ghostGirlFrames.Length == 0)
                {
                    var res = Resources.LoadAll<Sprite>("Characters/Ghost Girl Portrait");
                    if (res != null && res.Length > 0) ghostGirlFrames = res;
                }
            }

            if ((ghostGirlExpressions == null || ghostGirlExpressions.Count == 0) && ghostGirlFrames != null && ghostGirlFrames.Length > 0)
            {
                ghostGirlExpressions = new List<PortraitExpression>
                {
                    new PortraitExpression { name = "Neutral", frames = ghostGirlFrames },
                    new PortraitExpression { name = "Default", frames = ghostGirlFrames }
                };
            }
        }

        private void Close()
        {
            lastClosedTime = Time.unscaledTime;
            IsOpen = false;
            if (typingRoutine != null)
            {
                StopCoroutine(typingRoutine);
                typingRoutine = null;
            }
            if (boxAppearRoutine != null)
            {
                StopCoroutine(boxAppearRoutine);
                boxAppearRoutine = null;
            }
            if (scaredTremorRoutine != null)
            {
                StopCoroutine(scaredTremorRoutine);
                scaredTremorRoutine = null;
            }
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
            if (rootRt != null && rootBasePosCaptured) rootRt.anchoredPosition = rootBaseAnchoredPos;
            if (bodyRt != null && bodyBasePosCaptured) bodyRt.anchoredPosition = bodyBaseAnchoredPos;
            if (nameText != null && baseNameColorCaptured) nameText.color = baseNameColor;
            if (didDuckAmbient && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetAmbientDuck(1.0f, 0.45f);
            }
            didDuckAmbient = false;

            isTyping = false;
            if (root != null) root.SetActive(false);
            OnDialogueFinished?.Invoke();
        }

        private void EnsureJuiceSetup()
        {
            if (root != null)
            {
                if (rootCanvasGroup == null)
                {
                    rootCanvasGroup = root.GetComponent<CanvasGroup>();
                    if (rootCanvasGroup == null) rootCanvasGroup = root.AddComponent<CanvasGroup>();
                }
                if (rootRt == null) rootRt = root.GetComponent<RectTransform>();
                if (rootRt != null && !rootBasePosCaptured)
                {
                    rootBaseAnchoredPos = rootRt.anchoredPosition;
                    rootBasePosCaptured = true;
                }
            }

            if (bodyText != null)
            {
                if (bodyRt == null) bodyRt = bodyText.rectTransform;
                if (bodyRt != null && !bodyBasePosCaptured)
                {
                    bodyBaseAnchoredPos = bodyRt.anchoredPosition;
                    bodyBasePosCaptured = true;
                }
            }
        }

        private IEnumerator AppearRoutine()
        {
            if (rootCanvasGroup == null) yield break;

            float duration = 0.15f;
            float elapsed = 0f;
            Vector2 startPos = rootBaseAnchoredPos + new Vector2(0f, -10f);

            rootCanvasGroup.alpha = 0f;
            if (rootRt != null && rootBasePosCaptured) rootRt.anchoredPosition = startPos;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                rootCanvasGroup.alpha = ease;
                if (rootRt != null && rootBasePosCaptured)
                {
                    rootRt.anchoredPosition = Vector2.Lerp(startPos, rootBaseAnchoredPos, ease);
                }
                yield return null;
            }

            rootCanvasGroup.alpha = 1f;
            if (rootRt != null && rootBasePosCaptured) rootRt.anchoredPosition = rootBaseAnchoredPos;
            boxAppearRoutine = null;
        }

        private IEnumerator ScaredTremorRoutine()
        {
            if (bodyRt == null || !bodyBasePosCaptured) yield break;

            while (isTyping)
            {
                if (ApartmentManager.ScreenShakeEnabled)
                {
                    Vector2 jitter = new Vector2(
                        UnityEngine.Random.Range(-1.2f, 1.2f),
                        UnityEngine.Random.Range(-0.9f, 0.9f));
                    bodyRt.anchoredPosition = bodyBaseAnchoredPos + jitter;
                }
                else
                {
                    bodyRt.anchoredPosition = bodyBaseAnchoredPos;
                }
                yield return new WaitForSecondsRealtime(0.055f);
            }

            bodyRt.anchoredPosition = bodyBaseAnchoredPos;
            scaredTremorRoutine = null;
        }

        private static float GetExpressionSpeedMultiplier(string expression)
        {
            if (string.Equals(expression, "Exhausted", StringComparison.OrdinalIgnoreCase)) return 0.72f;
            if (string.Equals(expression, "Scared", StringComparison.OrdinalIgnoreCase)) return 1.08f;
            if (string.Equals(expression, "Nervous", StringComparison.OrdinalIgnoreCase)) return 0.95f;
            return 1.0f;
        }

        private static float GetPunctuationPause(string text, int index)
        {
            if (index < 0 || index >= text.Length) return 0f;
            char ch = text[index];
            bool nextIsSamePunct = index + 1 < text.Length && text[index + 1] == ch;
            if (nextIsSamePunct) return 0f;

            if (ch == ',' || ch == ';') return 0.055f;
            if (ch == '.' || ch == '?' || ch == '!') return 0.12f;
            return 0f;
        }
    }
}
