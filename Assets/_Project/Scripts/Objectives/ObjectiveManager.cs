using System;
using System.Collections.Generic;
using UnityEngine;
using Room0317.Apartment;
using Room0317.Audio;
using Room0317.Dialogue;
using Room0317.Inventory;
using Room0317.UI;

namespace Room0317.Objectives
{
    /// <summary>
    /// Master narrative coordinator for ROOM 03:17 (Step 8: Simple Objective Progression).
    /// Tracks active objectives, chronological progression toward 03:17 AM,
    /// and seamlessly triggers story milestones when clues are gathered,
    /// props are inspected, and room shifts occur.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Objectives")]
        [SerializeField] private List<ObjectiveData> objectives = new List<ObjectiveData>();
        [SerializeField] private int activeObjectiveIndex = 0;

        public ObjectiveData CurrentObjective => (activeObjectiveIndex >= 0 && activeObjectiveIndex < objectives.Count)
            ? objectives[activeObjectiveIndex]
            : null;

        public IReadOnlyList<ObjectiveData> AllObjectives => objectives;
        public int CurrentIndex => activeObjectiveIndex;
        public bool IsAllCompleted => activeObjectiveIndex >= objectives.Count;

        public static event Action<ObjectiveData> OnObjectiveActivated;
        public static event Action<ObjectiveData> OnObjectiveCompleted;
        public static event Action OnAllObjectivesCompleted;

        private DialogueBox dialogueBox;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultObjectives();
        }

        private void Start()
        {
            dialogueBox = FindFirstObjectByType<DialogueBox>();

            // Subscribe to game systems
            InteractablePoint.OnObjectInspected += HandleObjectInspected;
            CollectibleItem.OnCollectiblePickedUp += HandleCollectiblePickedUp;
            ChangingRoomManager.OnRoomShifted += HandleRoomShifted;
            DoorTransition.OnDoorInteracted += HandleDoorInteracted;

            // Activate initial objective
            if (CurrentObjective != null)
            {
                SyncClockWithObjective(CurrentObjective);
                OnObjectiveActivated?.Invoke(CurrentObjective);
            }
        }

        private void OnDestroy()
        {
            InteractablePoint.OnObjectInspected -= HandleObjectInspected;
            CollectibleItem.OnCollectiblePickedUp -= HandleCollectiblePickedUp;
            ChangingRoomManager.OnRoomShifted -= HandleRoomShifted;
            DoorTransition.OnDoorInteracted -= HandleDoorInteracted;
        }

        private void InitializeDefaultObjectives()
        {
            if (objectives != null && objectives.Count > 0) return;

            objectives = new List<ObjectiveData>
            {
                new ObjectiveData(
                    "obj_clock",
                    "Check the Bedside Alarm Clock",
                    "Examine the red glowing digital clock beside your bed to confirm the time.",
                    "Press [E] facing the digital alarm clock on the bedside nightstand.",
                    "03:12 AM"
                ),
                new ObjectiveData(
                    "obj_key",
                    "Find the Wardrobe Key",
                    "The bedroom wardrobe is locked. Search the apartment to locate the tarnished brass key.",
                    "Explore the Living Room and investigate the furniture.",
                    "03:13 AM"
                ),
                new ObjectiveData(
                    "obj_wardrobe",
                    "Unlock the Bedroom Wardrobe",
                    "Use the tarnished brass key to unlock and investigate your bedroom wardrobe.",
                    "Return to the Bedroom and press [E] in front of the tall wardrobe.",
                    "03:14 AM"
                ),
                new ObjectiveData(
                    "obj_living",
                    "Investigate the Living Room",
                    "A chilling shudder echoes through the apartment. Something in the living room has changed.",
                    "Enter the Living Room and inspect the framed artwork on the wall.",
                    "03:15 AM"
                ),
                new ObjectiveData(
                    "obj_note",
                    "Find the Hidden Evidence",
                    "Look behind the living room furniture to uncover the source of the scratching noise.",
                    "Search closely for a crumpled note tucked near the edges of the room.",
                    "03:16 AM"
                ),
                new ObjectiveData(
                    "obj_escape",
                    "03:17 AM - Escape the Apartment",
                    "The clock strikes 03:17. The apartment exit deadbolts are unsealed. Escape immediately!",
                    "Run to the Hallway and open the apartment front door.",
                    "03:17 AM"
                )
            };
        }

        public void ConfigureObjectives(List<ObjectiveData> newObjectives)
        {
            if (newObjectives == null || newObjectives.Count == 0) return;
            objectives = newObjectives;
            activeObjectiveIndex = 0;
            if (CurrentObjective != null)
            {
                SyncClockWithObjective(CurrentObjective);
                OnObjectiveActivated?.Invoke(CurrentObjective);
            }
        }

        public void AdvanceToNextObjective()
        {
            if (CurrentObjective == null) return;

            var finished = CurrentObjective;
            finished.MarkComplete();
            OnObjectiveCompleted?.Invoke(finished);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayObjectiveChime();
            }

            activeObjectiveIndex++;

            if (CurrentObjective != null)
            {
                SyncClockWithObjective(CurrentObjective);
                OnObjectiveActivated?.Invoke(CurrentObjective);
            }
            else
            {
                OnAllObjectivesCompleted?.Invoke();
            }
        }

        public void CompleteObjective(string objectiveId)
        {
            if (CurrentObjective != null && CurrentObjective.Id == objectiveId)
            {
                AdvanceToNextObjective();
            }
        }

        private void SyncClockWithObjective(ObjectiveData obj)
        {
            if (ApartmentClock.Instance != null && !string.IsNullOrEmpty(obj.TimeString))
            {
                ApartmentClock.Instance.SetTimeString(obj.TimeString);
            }
        }

        // ====================================================================
        // AUTOMATED GAMEPLAY EVENT HANDLERS
        // ====================================================================

        private void HandleObjectInspected(InteractablePoint point)
        {
            if (CurrentObjective == null || point == null) return;

            string objId = CurrentObjective.Id;

            // Objective 0: Inspecting Bedside Clock
            if (objId == "obj_clock" && point.PointName.Contains("Clock"))
            {
                AdvanceToNextObjective();
            }
            // Objective 2: Inspecting Wardrobe
            else if (objId == "obj_wardrobe" && point.PointName.Contains("Wardrobe"))
            {
                bool hasKey = InventoryManager.Instance != null && InventoryManager.Instance.HasItem("key_brass");
                if (hasKey)
                {
                    // Trigger unlock sequence & advance
                    if (dialogueBox != null)
                    {
                        dialogueBox.StartDialogue(new List<DialogueLine>
                        {
                            new DialogueLine("Adrian", "Nervous", "The tarnished key turns with a sharp, heavy CLACK."),
                            new DialogueLine("Adrian", "Scared", "Inside... between the coats... a diary page is pinned to the back wall!")
                        });
                    }

                    // Queue Living Room shift to Eerie (Type B)
                    if (ChangingRoomManager.Instance != null)
                    {
                        ChangingRoomManager.Instance.QueueRoomChange(RoomIds.LivingRoom, RoomState.B);
                    }

                    AdvanceToNextObjective();
                }
                else
                {
                    if (dialogueBox != null)
                    {
                        dialogueBox.StartDialogue(new List<DialogueLine>
                        {
                            new DialogueLine("Adrian", "Neutral", "The wardrobe is firmly locked. I need to find the key.")
                        });
                    }
                }
            }
            // Objective 3: Inspecting Framed Artwork in Living Room
            else if (objId == "obj_living" && point.PointName.Contains("Painting"))
            {
                AdvanceToNextObjective();
            }
        }

        private void HandleCollectiblePickedUp(CollectibleItem item)
        {
            if (CurrentObjective == null || item == null || item.ItemData == null) return;

            string objId = CurrentObjective.Id;
            string itemId = item.ItemData.id;

            // Objective 1: Finding Key
            if (objId == "obj_key" && itemId == "key_brass")
            {
                AdvanceToNextObjective();
            }
            // Objective 4: Finding Hidden Note
            else if (objId == "obj_note" && (itemId == "note_hidden" || itemId == "note_diary"))
            {
                AdvanceToNextObjective();
            }
        }

        private void HandleRoomShifted(ApartmentRoom room, RoomState newState)
        {
            if (CurrentObjective == null || room == null) return;

            // If living room shifted to B during objective 3, advance
            if (CurrentObjective.Id == "obj_living" && room.RoomId == RoomIds.LivingRoom && newState == RoomState.B)
            {
                AdvanceToNextObjective();
            }
        }

        private void HandleDoorInteracted(DoorTransition door)
        {
            if (door == null) return;

            // Climax: Player opens front exit door at 03:17 AM
            if (CurrentObjective != null && CurrentObjective.Id == "obj_escape" && door.DestinationLabel.Contains("Exit"))
            {
                door.SetLocked(false);
                TriggerGameEnding();
            }
        }

        private void TriggerGameEnding()
        {
            if (dialogueBox != null)
            {
                dialogueBox.StartDialogue(new List<DialogueLine>
                {
                    new DialogueLine("Adrian", "Scared", "The deadbolt opens with a deafening rattle! The cold air from outside rushes in..."),
                    new DialogueLine("Adrian", "Neutral", "I step out into the stairwell. Behind me, the clock inside clicks to 03:18 AM."),
                    new DialogueLine("Adrian", "Neutral", "For the first time in years... the apartment is silent.")
                });
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(0f);
                AudioManager.Instance.PlayObjectiveChime();
            }

            AdvanceToNextObjective();
        }
    }
}
