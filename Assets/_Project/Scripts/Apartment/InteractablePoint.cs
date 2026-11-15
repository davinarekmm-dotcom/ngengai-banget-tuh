using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.Apartment
{
    /// <summary>
    /// Interaction point in a room with state-aware dialogue (different text/expressions for State A vs State B).
    /// Displays a floating prompt when the player is within range.
    /// </summary>
    public class InteractablePoint : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pointName = "Interactable";
        [SerializeField] private ApartmentRoom parentRoom;

        [Header("State A Dialogue (Normal)")]
        [SerializeField] private List<DialogueLine> stateALines = new List<DialogueLine>();

        [Header("State B Dialogue (Eerie)")]
        [SerializeField] private List<DialogueLine> stateBLines = new List<DialogueLine>();

        [Header("Range")]
        [SerializeField] private float triggerRadius = 1.0f;

        private bool isPlayerInRange;
        private GameObject promptVisual;
        private DialogueBox dialogueBox;
        private AdrianController player;
        private float nextAllowedInteractTime = 0f;

        public string PointName => pointName;
        public ApartmentRoom ParentRoom => parentRoom;

        public static event Action<InteractablePoint> OnObjectInspected;

        public void Configure(string name, ApartmentRoom room, List<DialogueLine> aLines, List<DialogueLine> bLines, float radius = 1.0f)
        {
            pointName = name;
            parentRoom = room;
            stateALines = aLines;
            stateBLines = bLines;
            triggerRadius = radius;
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            CreatePromptVisual();
        }

        private void CreatePromptVisual()
        {
            promptVisual = new GameObject("Prompt_" + pointName);
            promptVisual.transform.SetParent(transform, false);
            promptVisual.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var tm = promptVisual.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = "[E] Inspect";
            tm.fontSize = 48;
            tm.characterSize = 0.025f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.7f, 0.95f);

            var mr = promptVisual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 100;

            promptVisual.SetActive(false);
        }

        private void Update()
        {
            if (Room0317.UI.InteractionBlocker.IsBlocked(dialogueBox))
            {
                nextAllowedInteractTime = Time.unscaledTime + 0.4f;
                if (promptVisual != null && promptVisual.activeSelf) promptVisual.SetActive(false);
                return;
            }

            if (Time.unscaledTime < nextAllowedInteractTime)
            {
                if (promptVisual != null && promptVisual.activeSelf) promptVisual.SetActive(false);
                return;
            }

            if (player == null)
            {
                player = FindFirstObjectByType<AdrianController>();
                if (player == null) return;
            }

            float dist = Vector2.Distance(transform.position, player.transform.position);
            bool inRange = dist <= triggerRadius;

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                if (promptVisual != null) promptVisual.SetActive(isPlayerInRange && (dialogueBox == null || !dialogueBox.IsOpen));
            }

            if (isPlayerInRange && (dialogueBox == null || !dialogueBox.IsOpen))
            {
                // Bobbing prompt animation
                if (promptVisual != null && promptVisual.activeSelf)
                {
                    float yOffset = Mathf.Sin(Time.time * 4f) * 0.05f;
                    promptVisual.transform.localPosition = new Vector3(0f, 0.6f + yOffset, 0f);
                }

                if (InteractPressed())
                {
                    TriggerInteraction();
                }
            }
            else if (promptVisual != null && promptVisual.activeSelf)
            {
                promptVisual.SetActive(false);
            }
        }

        private static bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonWest.wasPressedThisFrame;
        }

        public void TriggerInteraction()
        {
            if (dialogueBox == null) dialogueBox = FindFirstObjectByType<DialogueBox>();
            if (dialogueBox == null || dialogueBox.IsOpen) return;

            nextAllowedInteractTime = Time.unscaledTime + 0.35f;
            if (promptVisual != null) promptVisual.SetActive(false);
            if (Room0317.Audio.AudioManager.Instance != null) Room0317.Audio.AudioManager.Instance.PlayUiConfirm();

            RoomState current = parentRoom != null ? parentRoom.CurrentState : RoomState.A;
            List<DialogueLine> linesToPlay = (current == RoomState.B && stateBLines != null && stateBLines.Count > 0)
                ? stateBLines
                : stateALines;

            if (linesToPlay == null || linesToPlay.Count == 0)
            {
                linesToPlay = new List<DialogueLine> { new DialogueLine("Adrian", "Neutral", "Nothing unusual here.") };
            }

            if (player != null) player.CanMove = false;

            OnObjectInspected?.Invoke(this);

            dialogueBox.OnDialogueFinished += HandleFinished;
            dialogueBox.StartDialogue(linesToPlay);
        }

        private void HandleFinished()
        {
            nextAllowedInteractTime = Time.unscaledTime + 0.35f;
            if (dialogueBox != null) dialogueBox.OnDialogueFinished -= HandleFinished;
            if (player != null) player.CanMove = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
