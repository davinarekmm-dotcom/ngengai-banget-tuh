using System;
using System.Collections.Generic;
using UnityEngine;

namespace Room0317.Apartment
{
    /// <summary>
    /// Represents one of the 5 rooms in the apartment.
    /// Manages its permanent furniture, changeable objects (State A vs B),
    /// room visual backdrops, interaction points, and detection trigger.
    /// </summary>
    public class ApartmentRoom : MonoBehaviour
    {
        [Header("Room Identity")]
        [SerializeField] private string roomId = "Room";
        [SerializeField] private string displayName = "Room";
        [SerializeField] private RoomState currentState = RoomState.A;

        [Header("Room Detection")]
        [SerializeField] private RoomTrigger roomTrigger;

        [Header("State A Objects (Normal)")]
        [SerializeField] private List<GameObject> stateAObjects = new List<GameObject>();

        [Header("State B Objects (Eerie)")]
        [SerializeField] private List<GameObject> stateBObjects = new List<GameObject>();

        [Header("Room Visuals (Optional overlays)")]
        [SerializeField] private SpriteRenderer visualStateA;
        [SerializeField] private SpriteRenderer visualStateB;

        [Header("Interaction Points")]
        [SerializeField] private List<InteractablePoint> interactionPoints = new List<InteractablePoint>();

        public string RoomId => roomId;
        public string DisplayName => displayName;
        public RoomState CurrentState => currentState;
        public RoomTrigger Trigger => roomTrigger;

        public event Action<ApartmentRoom, RoomState> OnRoomStateChanged;

        public void Initialize(string id, string name, RoomTrigger trigger)
        {
            roomId = id;
            displayName = name;
            roomTrigger = trigger;
            ApplyState(currentState);
        }

        public void RegisterStateObjects(List<GameObject> aObjs, List<GameObject> bObjs)
        {
            stateAObjects = aObjs ?? new List<GameObject>();
            stateBObjects = bObjs ?? new List<GameObject>();
            ApplyState(currentState);
        }

        public void RegisterVisuals(SpriteRenderer visualA, SpriteRenderer visualB)
        {
            visualStateA = visualA;
            visualStateB = visualB;
            ApplyState(currentState);
        }

        public void SetState(RoomState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            ApplyState(currentState);
            OnRoomStateChanged?.Invoke(this, currentState);
            Debug.Log($"<color=cyan>[ApartmentRoom] {displayName} transitioned to State {currentState}</color>");
        }

        public void ToggleState()
        {
            SetState(currentState == RoomState.A ? RoomState.B : RoomState.A);
        }

        private void ApplyState(RoomState state)
        {
            bool isA = state == RoomState.A;
            bool isB = state == RoomState.B;

            if (stateAObjects != null)
            {
                foreach (var obj in stateAObjects)
                {
                    if (obj != null) obj.SetActive(isA);
                }
            }

            if (stateBObjects != null)
            {
                foreach (var obj in stateBObjects)
                {
                    if (obj != null) obj.SetActive(isB);
                }
            }

            if (visualStateA != null) visualStateA.gameObject.SetActive(isA);
            if (visualStateB != null) visualStateB.gameObject.SetActive(isB);
        }
    }
}
