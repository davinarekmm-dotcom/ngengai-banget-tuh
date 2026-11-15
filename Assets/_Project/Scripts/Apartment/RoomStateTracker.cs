using System;
using UnityEngine;

namespace Room0317.Apartment
{
    /// <summary>
    /// Tracks state, visit count, and pending re-entry changes for a single room.
    /// Ensures that visual transitions happen during doorway fades rather than
    /// popping in front of the player's view.
    /// </summary>
    [Serializable]
    public class RoomStateTracker
    {
        [SerializeField] private string roomId;
        [SerializeField] private ApartmentRoom room;
        [SerializeField] private int visitCount = 0;
        [SerializeField] private bool isPlayerInside = false;
        [SerializeField] private bool changeOnNextEntry = false;
        [SerializeField] private RoomState pendingState = RoomState.A;

        public string RoomId => roomId;
        public ApartmentRoom Room => room;
        public int VisitCount => visitCount;
        public bool IsPlayerInside => isPlayerInside;
        public bool HasPendingChange => changeOnNextEntry;
        public RoomState PendingState => pendingState;

        public event Action<RoomStateTracker, int> OnRoomEntered;
        public event Action<RoomStateTracker> OnRoomExited;
        public event Action<RoomStateTracker, RoomState> OnStateCommitted;

        public RoomStateTracker(string id, ApartmentRoom roomRef)
        {
            roomId = id;
            room = roomRef;
            if (room != null)
            {
                pendingState = room.CurrentState;
            }
        }

        /// <summary>
        /// Queues a room state change to be applied the next time the player enters this room.
        /// </summary>
        public void QueueStateChange(RoomState nextState)
        {
            pendingState = nextState;
            changeOnNextEntry = true;
            Debug.Log($"<color=yellow>[RoomStateTracker] Queued {roomId} to change to State {nextState} on next entry.</color>");
        }

        /// <summary>
        /// Called by ChangingRoomManager when the player enters this room through a door.
        /// Applies any pending state change during the fade.
        /// </summary>
        public void NotifyEntered()
        {
            visitCount++;
            isPlayerInside = true;

            if (changeOnNextEntry)
            {
                changeOnNextEntry = false;
                if (room != null && room.CurrentState != pendingState)
                {
                    room.SetState(pendingState);
                    OnStateCommitted?.Invoke(this, pendingState);
                    Debug.Log($"<color=red><b>[ChangingRoomSystem]</b> {roomId} shifted to State {pendingState} upon re-entry (Visit #{visitCount})!</color>");
                }
            }

            OnRoomEntered?.Invoke(this, visitCount);
        }

        /// <summary>
        /// Called when the player leaves this room.
        /// </summary>
        public void NotifyExited()
        {
            isPlayerInside = false;
            OnRoomExited?.Invoke(this);
        }

        /// <summary>
        /// Forces an immediate state change (bypasses pending queue).
        /// </summary>
        public void ForceSetState(RoomState state)
        {
            pendingState = state;
            changeOnNextEntry = false;
            if (room != null)
            {
                room.SetState(state);
            }
            OnStateCommitted?.Invoke(this, state);
        }
    }
}
