using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Apartment;
using Room0317.Dialogue;
using Room0317.Player;

namespace Room0317.Inventory
{
    /// <summary>
    /// Interactive collectible item placed in world space (e.g. Key, Note, Hidden Clue).
    /// When Adrian approaches, a floating prompt appears. Pressing E opens the close-up
    /// inspection viewer (showing high-res illustration), adds it to inventory,
    /// and triggers optional dialogue or story progression.
    /// </summary>
    public class CollectibleItem : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private InventoryItem itemData;

        [Header("Interaction Settings")]
        [SerializeField] private float interactRadius = 1.3f;
        [SerializeField] private bool openInspectionFirst = true;
        [SerializeField] private bool destroyOnCollect = false;

        [Header("Post-Pickup Dialogue (Optional)")]
        [SerializeField] private List<DialogueLine> pickupDialogue = new List<DialogueLine>();

        [Header("Story Trigger (Optional)")]
        [SerializeField] private string queueRoomShiftId = "";
        [SerializeField] private RoomState queueRoomShiftState = RoomState.B;

        private GameObject promptVisual;
        private TextMesh promptTextMesh;
        private AdrianController player;
        private DialogueBox dialogueBox;
        private bool isPlayerInRange;
        private bool isCollected = false;
        private float nextAllowedInteractTime = 0f;

        public InventoryItem ItemData => itemData;
        public bool IsCollected => isCollected;

        public static event Action<CollectibleItem> OnCollectiblePickedUp;

        public void Configure(InventoryItem data, float radius = 1.3f)
        {
            itemData = data;
            interactRadius = radius;
            UpdatePrompt();
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();

            // Setup in-world sprite if not already configured
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null && itemData != null && itemData.icon != null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = itemData.icon;
                sr.sortingLayerName = "Props_Back";
                sr.sortingOrder = 5;
            }

            CreatePromptVisual();
        }

        private void CreatePromptVisual()
        {
            promptVisual = new GameObject("Prompt_" + gameObject.name);
            promptVisual.transform.SetParent(transform, false);

            Vector3 lossy = transform.lossyScale;
            float invX = Mathf.Abs(lossy.x) > 0.0001f ? 1f / lossy.x : 1f;
            float invY = Mathf.Abs(lossy.y) > 0.0001f ? 1f / lossy.y : 1f;
            promptVisual.transform.localScale = new Vector3(invX, invY, 1f);
            promptVisual.transform.localPosition = new Vector3(0f, 0.7f * invY, 0f);

            promptTextMesh = promptVisual.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptTextMesh.font = font;
            promptTextMesh.fontSize = 40;
            promptTextMesh.characterSize = 0.024f;
            promptTextMesh.anchor = TextAnchor.MiddleCenter;
            promptTextMesh.alignment = TextAlignment.Center;
            promptTextMesh.color = new Color(1f, 0.95f, 0.65f, 0.95f);

            var mr = promptVisual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingLayerName = "Props_Front";
            mr.sortingOrder = 200;

            UpdatePrompt();
            promptVisual.SetActive(false);
        }

        private void UpdatePrompt()
        {
            if (promptTextMesh != null && itemData != null)
            {
                promptTextMesh.text = $"[E] Examine {itemData.displayName}";
            }
        }

        private void Update()
        {
            if (isCollected) return;

            if (player == null)
            {
                player = FindFirstObjectByType<AdrianController>();
                if (player == null) return;
            }

            float dist = Vector2.Distance(transform.position, player.transform.position);
            bool inRange = dist <= interactRadius;

            bool overlayActive = Room0317.UI.InteractionBlocker.IsBlocked(dialogueBox);
            bool isBlocked = overlayActive || Time.unscaledTime < nextAllowedInteractTime;

            if (overlayActive)
            {
                nextAllowedInteractTime = Time.unscaledTime + 0.4f;
            }

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                UpdatePromptVisibility(!isBlocked);
            }

            if (isPlayerInRange && !isBlocked)
            {
                // Bobbing prompt
                if (promptVisual != null && promptVisual.activeSelf)
                {
                    Vector3 lossy = transform.lossyScale;
                    float invY = Mathf.Abs(lossy.y) > 0.0001f ? 1f / lossy.y : 1f;
                    float y = (0.7f + Mathf.Sin(Time.time * 4f) * 0.05f) * invY;
                    promptVisual.transform.localPosition = new Vector3(0f, y, 0f);
                }

                if (InteractPressed())
                {
                    nextAllowedInteractTime = Time.unscaledTime + 0.45f;
                    TriggerInteraction();
                }
            }
            else if (promptVisual != null && promptVisual.activeSelf)
            {
                promptVisual.SetActive(false);
            }
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (promptVisual != null)
            {
                promptVisual.SetActive(isPlayerInRange && visible);
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
            if (isCollected) return;

            if (openInspectionFirst && InspectionModal.Instance != null && itemData != null)
            {
                InspectionModal.Instance.ShowItem(
                    itemData,
                    onCollect: ExecutePickup,
                    onClose: null
                );
            }
            else
            {
                ExecutePickup();
            }
        }

        private void ExecutePickup()
        {
            isCollected = true;
            if (promptVisual != null) promptVisual.SetActive(false);

            if (InventoryManager.Instance != null && itemData != null)
            {
                InventoryManager.Instance.AddItem(itemData);
            }

            if (Room0317.Audio.AudioManager.Instance != null && itemData != null)
            {
                if (itemData.id.Contains("key"))
                {
                    Room0317.Audio.AudioManager.Instance.PlayKeyPickup();
                }
                else
                {
                    Room0317.Audio.AudioManager.Instance.PlayNotePickup();
                }
            }

            // Optional story trigger
            if (!string.IsNullOrEmpty(queueRoomShiftId) && ChangingRoomManager.Instance != null)
            {
                ChangingRoomManager.Instance.QueueRoomChange(queueRoomShiftId, queueRoomShiftState);
            }

            OnCollectiblePickedUp?.Invoke(this);

            // Optional pickup dialogue
            if (pickupDialogue != null && pickupDialogue.Count > 0 && dialogueBox != null)
            {
                dialogueBox.StartDialogue(pickupDialogue);
            }

            if (destroyOnCollect)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
