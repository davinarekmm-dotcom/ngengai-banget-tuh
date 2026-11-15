using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.Apartment
{
    /// <summary>
    /// Interactable doorway component.
    /// When the player approaches within range, a floating prompt "[E] Enter [Destination]" appears.
    /// Pressing E initiates a room transition via ApartmentManager, teleporting the player to the
    /// destination room's entrance and focusing the camera exclusively on that room.
    /// </summary>
    public class DoorTransition : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField] private string targetRoomId;
        [SerializeField] private ApartmentRoom targetRoom;
        [SerializeField] private string destinationLabel = "Room";
        [SerializeField] private Transform targetSpawnPoint;
        [SerializeField] private AdrianController.Facing spawnFacing = AdrianController.Facing.Down;

        [Header("Settings")]
        [SerializeField] private float interactRadius = 1.4f;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string lockedReason = "The door is firmly locked.";

        [Header("Visual Prompt")]
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.8f, 0f);

        private GameObject promptVisual;
        private TextMesh promptTextMesh;
        private AdrianController player;
        private DialogueBox dialogueBox;
        private bool isPlayerInRange;

        public string TargetRoomId => targetRoomId;
        public ApartmentRoom TargetRoom => targetRoom;
        public string DestinationLabel => destinationLabel;
        public Transform TargetSpawnPoint => targetSpawnPoint;
        public AdrianController.Facing SpawnFacing => spawnFacing;
        public bool IsLocked => isLocked;

        public static event Action<DoorTransition> OnDoorInteracted;

        public void Configure(string targetId, ApartmentRoom room, string label, Transform spawnPoint, AdrianController.Facing facing, bool locked = false)
        {
            targetRoomId = targetId;
            targetRoom = room;
            destinationLabel = label;
            targetSpawnPoint = spawnPoint;
            spawnFacing = facing;
            isLocked = locked;
            UpdatePromptText();
        }

        public void SetTargetRoom(ApartmentRoom room)
        {
            targetRoom = room;
        }

        public void SetSpawnPoint(Transform spawnPoint)
        {
            targetSpawnPoint = spawnPoint;
        }

        public void SetLocked(bool locked, string reason = null)
        {
            isLocked = locked;
            if (!string.IsNullOrEmpty(reason)) lockedReason = reason;
            UpdatePromptText();
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            CreatePromptVisual();
        }

        private void CreatePromptVisual()
        {
            promptVisual = new GameObject("Prompt_" + gameObject.name);
            promptVisual.transform.SetParent(transform, false);
            promptVisual.transform.localPosition = promptOffset;

            promptTextMesh = promptVisual.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptTextMesh.font = font;
            promptTextMesh.fontSize = 44;
            promptTextMesh.characterSize = 0.024f;
            promptTextMesh.anchor = TextAnchor.MiddleCenter;
            promptTextMesh.alignment = TextAlignment.Center;
            promptTextMesh.color = new Color(1f, 0.95f, 0.65f, 0.95f);

            var mr = promptVisual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingLayerName = "Props_Front";
            mr.sortingOrder = 200;

            UpdatePromptText();
            promptVisual.SetActive(false);
        }

        private void UpdatePromptText()
        {
            if (promptTextMesh == null) return;
            promptTextMesh.text = isLocked ? "[E] Locked" : $"[E] Enter {destinationLabel}";
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<AdrianController>();
                if (player == null) return;
            }

            float dist = Vector2.Distance(transform.position, player.transform.position);
            bool inRange = dist <= interactRadius;

            bool blocked = Room0317.UI.InteractionBlocker.IsBlocked(dialogueBox);

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                UpdatePromptVisibility();
            }

            if (isPlayerInRange && !blocked)
            {
                if (promptVisual != null && !promptVisual.activeSelf) promptVisual.SetActive(true);

                // Gentle floating bob
                if (promptVisual != null && promptVisual.activeSelf)
                {
                    float y = promptOffset.y + Mathf.Sin(Time.time * 4f) * 0.06f;
                    promptVisual.transform.localPosition = new Vector3(promptOffset.x, y, promptOffset.z);
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

        private void UpdatePromptVisibility()
        {
            if (promptVisual == null) return;
            bool shouldShow = isPlayerInRange && !Room0317.UI.InteractionBlocker.IsBlocked(dialogueBox);
            promptVisual.SetActive(shouldShow);
        }

        private bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;

            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
                return true;

            return false;
        }

        public void TriggerInteraction()
        {
            if (isLocked)
            {
                if (Room0317.Audio.AudioManager.Instance != null)
                {
                    Room0317.Audio.AudioManager.Instance.PlayUiConfirm();
                }
                if (dialogueBox != null)
                {
                    dialogueBox.StartDialogue(new List<DialogueLine>
                    {
                        new DialogueLine("Adrian", "Neutral", lockedReason)
                    });
                }
                else
                {
                    Debug.Log($"[DoorTransition] {destinationLabel} is locked: {lockedReason}");
                }
                return;
            }

            OnDoorInteracted?.Invoke(this);

            if (Room0317.Audio.AudioManager.Instance != null)
            {
                Room0317.Audio.AudioManager.Instance.PlayDoorTransition();
            }

            if (ApartmentManager.Instance != null)
            {
                ApartmentManager.Instance.PerformDoorTransition(this);
            }
            else
            {
                Debug.LogWarning("[DoorTransition] ApartmentManager instance not found!");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRadius);

            if (targetSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetSpawnPoint.position);
                Gizmos.DrawWireSphere(targetSpawnPoint.position, 0.4f);
            }
        }
    }
}
