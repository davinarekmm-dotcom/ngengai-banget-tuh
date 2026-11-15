using System;
using UnityEngine;

namespace Room0317.Apartment
{
    public enum RoomChangeTriggerType
    {
        OnRoomExit,         // Triggers when player leaves triggerRoomId
        OnRoomReentry,      // Triggers when player re-enters triggerRoomId (at visit count >= requiredVisitCount)
        OnObjectInspected,  // Triggers when player inspects triggerObjectName
        ManualTrigger       // Triggered explicitly via code
    }

    /// <summary>
    /// Configurable rule that dictates when and how a room shifts states.
    /// Can be configured in the Inspector or created dynamically via code.
    /// </summary>
    [Serializable]
    public class RoomChangeRule
    {
        [Header("Identity")]
        public string ruleName = "New Rule";
        public bool isEnabled = true;
        public bool isOneShot = true;

        [Header("Trigger Condition")]
        public RoomChangeTriggerType triggerType = RoomChangeTriggerType.OnRoomExit;
        public string triggerRoomId = RoomIds.LivingRoom;
        public string triggerObjectName = "";
        public int requiredVisitCount = 2;

        [Header("Target Action")]
        public string targetRoomId = RoomIds.LivingRoom;
        public RoomState targetState = RoomState.B;

        [NonSerialized] private bool hasTriggered = false;

        public bool HasTriggered => hasTriggered;

        public bool CanTrigger()
        {
            if (!isEnabled) return false;
            if (isOneShot && hasTriggered) return false;
            return true;
        }

        public void MarkTriggered()
        {
            hasTriggered = true;
        }

        public void Reset()
        {
            hasTriggered = false;
        }
    }
}
