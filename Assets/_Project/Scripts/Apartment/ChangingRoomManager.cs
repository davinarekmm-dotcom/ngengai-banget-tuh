using System;
using System.Collections.Generic;
using UnityEngine;

namespace Room0317.Apartment
{
    /// <summary>
    /// Master manager for the Changing Room System (Step 3).
    /// Tracks player room entry and exit history, evaluates room change rules,
    /// and seamlessly applies pending room state shifts (Type A Normal -> Type B Eerie)
    /// during doorway transitions while the screen is black.
    /// </summary>
    public class ChangingRoomManager : MonoBehaviour
    {
        public static ChangingRoomManager Instance { get; private set; }

        [Header("Rules Configuration")]
        [SerializeField] private List<RoomChangeRule> rules = new List<RoomChangeRule>();

        private readonly Dictionary<string, RoomStateTracker> trackers = new Dictionary<string, RoomStateTracker>();

        public IReadOnlyDictionary<string, RoomStateTracker> Trackers => trackers;

        public static event Action<ApartmentRoom, RoomState> OnRoomShifted;
        public static event Action<ApartmentRoom, int> OnRoomEnteredWithVisitCount;
        public static event Action<ApartmentRoom, ApartmentRoom> OnDoorwayTransitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeTrackers();
            SetupDefaultRulesIfEmpty();
        }

        private void Start()
        {
            InteractablePoint.OnObjectInspected += HandleObjectInspected;

            // Initialize tracker for starting room (Bedroom)
            if (ApartmentManager.Instance != null && ApartmentManager.Instance.CurrentRoom != null)
            {
                var cur = ApartmentManager.Instance.CurrentRoom;
                if (trackers.TryGetValue(cur.RoomId, out var t))
                {
                    t.NotifyEntered();
                    OnRoomEnteredWithVisitCount?.Invoke(cur, t.VisitCount);
                }
            }
        }

        private void OnDestroy()
        {
            InteractablePoint.OnObjectInspected -= HandleObjectInspected;
        }

        private void InitializeTrackers()
        {
            trackers.Clear();
            var allRooms = FindObjectsByType<ApartmentRoom>(FindObjectsSortMode.None);
            foreach (var r in allRooms)
            {
                var tracker = new RoomStateTracker(r.RoomId, r);
                tracker.OnStateCommitted += (t, state) =>
                {
                    OnRoomShifted?.Invoke(t.Room, state);
                };
                trackers[r.RoomId] = tracker;
            }
        }

        private void SetupDefaultRulesIfEmpty()
        {
            if (rules != null && rules.Count > 0) return;

            rules = new List<RoomChangeRule>
            {
                // Rule 1: Inspecting the coffee table photograph queues Living Room to turn Eerie
                new RoomChangeRule
                {
                    ruleName = "Coffee Table Photo -> Living Room Shifts to Eerie",
                    triggerType = RoomChangeTriggerType.OnObjectInspected,
                    triggerObjectName = "Inspect_CoffeeTable",
                    targetRoomId = RoomIds.LivingRoom,
                    targetState = RoomState.B,
                    isOneShot = true
                },
                // Rule 2: Leaving Living Room queues Bedroom to turn Eerie on next return
                new RoomChangeRule
                {
                    ruleName = "Exit Living Room -> Bedroom Shifts to Eerie",
                    triggerType = RoomChangeTriggerType.OnRoomExit,
                    triggerRoomId = RoomIds.LivingRoom,
                    targetRoomId = RoomIds.Bedroom,
                    targetState = RoomState.B,
                    isOneShot = true
                },
                // Rule 3: Leaving Kitchen queues Bathroom to turn Eerie on next return
                new RoomChangeRule
                {
                    ruleName = "Exit Kitchen -> Bathroom Shifts to Eerie",
                    triggerType = RoomChangeTriggerType.OnRoomExit,
                    triggerRoomId = RoomIds.Kitchen,
                    targetRoomId = RoomIds.Bathroom,
                    targetState = RoomState.B,
                    isOneShot = true
                }
            };
        }

        // ====================================================================
        // DOORWAY HOOK: Called by ApartmentManager during the black screen fade
        // ====================================================================
        public void NotifyDoorTransition(ApartmentRoom fromRoom, ApartmentRoom toRoom)
        {
            OnDoorwayTransitioning?.Invoke(fromRoom, toRoom);

            // 1. Notify exit of previous room
            if (fromRoom != null && trackers.TryGetValue(fromRoom.RoomId, out var fromTracker))
            {
                fromTracker.NotifyExited();
                EvaluateExitRules(fromRoom.RoomId);
            }

            // 2. Notify entry of target room (commits pending state change during black fade)
            if (toRoom != null && trackers.TryGetValue(toRoom.RoomId, out var toTracker))
            {
                toTracker.NotifyEntered();
                OnRoomEnteredWithVisitCount?.Invoke(toRoom, toTracker.VisitCount);
                EvaluateReentryRules(toRoom.RoomId, toTracker.VisitCount);
            }
        }

        // ====================================================================
        // RULE EVALUATION
        // ====================================================================
        private void HandleObjectInspected(InteractablePoint pt)
        {
            if (pt == null) return;
            string objName = pt.PointName;

            foreach (var rule in rules)
            {
                if (!rule.CanTrigger()) continue;
                if (rule.triggerType == RoomChangeTriggerType.OnObjectInspected && rule.triggerObjectName == objName)
                {
                    ExecuteRuleAction(rule);
                }
            }
        }

        private void EvaluateExitRules(string exitedRoomId)
        {
            foreach (var rule in rules)
            {
                if (!rule.CanTrigger()) continue;
                if (rule.triggerType == RoomChangeTriggerType.OnRoomExit && rule.triggerRoomId == exitedRoomId)
                {
                    ExecuteRuleAction(rule);
                }
            }
        }

        private void EvaluateReentryRules(string enteredRoomId, int visitCount)
        {
            foreach (var rule in rules)
            {
                if (!rule.CanTrigger()) continue;
                if (rule.triggerType == RoomChangeTriggerType.OnRoomReentry &&
                    rule.triggerRoomId == enteredRoomId &&
                    visitCount >= rule.requiredVisitCount)
                {
                    ExecuteRuleAction(rule);
                }
            }
        }

        private void ExecuteRuleAction(RoomChangeRule rule)
        {
            rule.MarkTriggered();
            QueueRoomChange(rule.targetRoomId, rule.targetState);
            Debug.Log($"<color=magenta><b>[ChangingRoomManager]</b> Rule Triggered: '{rule.ruleName}'. Queued {rule.targetRoomId} -> {rule.targetState}.</color>");
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================
        /// <summary>
        /// Queues a room state change to be applied the next time the player enters that room.
        /// </summary>
        public void QueueRoomChange(string roomId, RoomState targetState)
        {
            if (trackers.TryGetValue(roomId, out var t))
            {
                t.QueueStateChange(targetState);
            }
            else
            {
                Debug.LogWarning($"[ChangingRoomManager] Cannot queue room change. RoomId '{roomId}' not found.");
            }
        }

        /// <summary>
        /// Forces an immediate room state change without waiting for re-entry.
        /// </summary>
        public void ForceRoomState(string roomId, RoomState targetState)
        {
            if (trackers.TryGetValue(roomId, out var t))
            {
                t.ForceSetState(targetState);
            }
        }

        public RoomStateTracker GetTracker(string roomId)
        {
            trackers.TryGetValue(roomId, out var t);
            return t;
        }

        public int GetVisitCount(string roomId)
        {
            return trackers.TryGetValue(roomId, out var t) ? t.VisitCount : 0;
        }

        public bool IsRoomStateB(string roomId)
        {
            return trackers.TryGetValue(roomId, out var t) && t.Room != null && t.Room.CurrentState == RoomState.B;
        }

        public void AddRule(RoomChangeRule rule)
        {
            if (rule != null) rules.Add(rule);
        }
    }
}
