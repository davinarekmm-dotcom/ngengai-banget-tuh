using System;
using UnityEngine;

namespace Room0317.Apartment
{
    /// <summary>
    /// Invisible trigger collider covering a room's interior.
    /// Detects when player enters or leaves the room.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomTrigger : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField] private ApartmentRoom room;

        public string RoomId => roomId;
        public ApartmentRoom Room => room;

        public static event Action<ApartmentRoom> OnPlayerEnteredRoom;
        public static event Action<ApartmentRoom> OnPlayerExitedRoom;

        public void Initialize(string id, ApartmentRoom roomRef)
        {
            roomId = id;
            room = roomRef;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Room0317.Player.AdrianController>() != null)
            {
                OnPlayerEnteredRoom?.Invoke(room);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Room0317.Player.AdrianController>() != null)
            {
                OnPlayerExitedRoom?.Invoke(room);
            }
        }
    }
}
