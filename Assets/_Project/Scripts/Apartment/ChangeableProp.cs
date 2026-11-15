using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Dialogue;
using Room0317.Inventory;
using Room0317.Player;

namespace Room0317.Apartment
{
    /// <summary>
    /// Interactive prop that alters its appearance between Room State A (Normal) and State B (Eerie).
    /// Used for objects such as the wall frames (normal_frame vs eerie_frame) and chairs.
    /// Can also be inspected with full-resolution close-up illustrations.
    /// </summary>
    public class ChangeableProp : MonoBehaviour
    {
        [Header("Room Link")]
        [SerializeField] private ApartmentRoom parentRoom;

        [Header("In-World Sprites")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite eerieSprite;
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Close-Up Inspection (Optional)")]
        [SerializeField] private bool isInspectable = true;
        [SerializeField] private string propName = "Framed Artwork";
        [SerializeField] private Sprite normalInspectArtwork;
        [SerializeField] private Sprite eerieInspectArtwork;
        [TextArea(2, 4)]
        [SerializeField] private string normalDescription = "A calm landscape painting in a dark wooden frame.";
        [TextArea(2, 4)]
        [SerializeField] private string eerieDescription = "The painting has changed! Dark distorted figures stand between the trees!";

        [Header("Range")]
        [SerializeField] private float interactRadius = 1.5f;

        private GameObject promptVisual;
        private TextMesh promptTextMesh;
        private AdrianController player;
        private DialogueBox dialogueBox;
        private bool isPlayerInRange;

        public void Configure(
            ApartmentRoom room,
            Sprite normal,
            Sprite eerie,
            Sprite normalInspect = null,
            Sprite eerieInspect = null,
            string name = null,
            string normalDesc = null,
            string eerieDesc = null)
        {
            parentRoom = room;
            normalSprite = normal;
            eerieSprite = eerie;
            normalInspectArtwork = normalInspect;
            eerieInspectArtwork = eerieInspect;
            if (!string.IsNullOrEmpty(name)) propName = name;
            if (!string.IsNullOrEmpty(normalDesc)) normalDescription = normalDesc;
            if (!string.IsNullOrEmpty(eerieDesc)) eerieDescription = eerieDesc;
            ApplyState(parentRoom != null ? parentRoom.CurrentState : RoomState.A);
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            if (parentRoom == null) parentRoom = GetComponentInParent<ApartmentRoom>();

            if (parentRoom != null)
            {
                parentRoom.OnRoomStateChanged += HandleRoomStateChanged;
                ApplyState(parentRoom.CurrentState);
            }
            else
            {
                ApplyState(RoomState.A);
            }

            if (isInspectable)
            {
                CreatePromptVisual();
            }
        }

        private void OnDestroy()
        {
            if (parentRoom != null)
            {
                parentRoom.OnRoomStateChanged -= HandleRoomStateChanged;
            }
        }

        private void HandleRoomStateChanged(ApartmentRoom room, RoomState newState)
        {
            ApplyState(newState);
        }

        public void ApplyState(RoomState state)
        {
            if (targetRenderer == null) return;
            targetRenderer.sprite = (state == RoomState.B && eerieSprite != null)
                ? eerieSprite
                : normalSprite;
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
            promptTextMesh.text = $"[E] Examine {propName}";
            promptTextMesh.fontSize = 40;
            promptTextMesh.characterSize = 0.024f;
            promptTextMesh.anchor = TextAnchor.MiddleCenter;
            promptTextMesh.alignment = TextAlignment.Center;
            promptTextMesh.color = new Color(1f, 0.95f, 0.65f, 0.95f);

            var mr = promptVisual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingLayerName = "Props_Front";
            mr.sortingOrder = 200;

            promptVisual.SetActive(false);
        }

        private void Update()
        {
            if (!isInspectable) return;

            if (player == null)
            {
                player = FindFirstObjectByType<AdrianController>();
                if (player == null) return;
            }

            float dist = Vector2.Distance(transform.position, player.transform.position);
            bool inRange = dist <= interactRadius;

            bool uiBlocked = Room0317.UI.InteractionBlocker.IsBlocked(dialogueBox);

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                if (promptVisual != null) promptVisual.SetActive(isPlayerInRange && !uiBlocked);
            }

            if (isPlayerInRange && !uiBlocked)
            {
                if (promptVisual != null && promptVisual.activeSelf)
                {
                    Vector3 lossy = transform.lossyScale;
                    float invY = Mathf.Abs(lossy.y) > 0.0001f ? 1f / lossy.y : 1f;
                    float y = (0.7f + Mathf.Sin(Time.time * 4f) * 0.05f) * invY;
                    promptVisual.transform.localPosition = new Vector3(0f, y, 0f);
                }

                if (InteractPressed())
                {
                    TriggerInspection();
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

        public void TriggerInspection()
        {
            RoomState state = parentRoom != null ? parentRoom.CurrentState : RoomState.A;
            bool isB = state == RoomState.B;

            Sprite inspectArt = isB ? eerieInspectArtwork : normalInspectArtwork;
            string desc = isB ? eerieDescription : normalDescription;

            if (inspectArt == null) inspectArt = isB ? eerieSprite : normalSprite;

            if (InspectionModal.Instance != null && inspectArt != null)
            {
                InspectionModal.Instance.Show(inspectArt, propName, desc, null, null);
            }
            else if (dialogueBox != null)
            {
                string expr = isB ? "Scared" : "Neutral";
                dialogueBox.StartDialogue(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", expr, desc)
                });
            }
        }
    }
}
