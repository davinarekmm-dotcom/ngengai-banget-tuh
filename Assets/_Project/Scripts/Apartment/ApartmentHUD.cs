using UnityEngine;
using UnityEngine.UI;

namespace Room0317.Apartment
{
    /// <summary>
    /// Testing HUD showing real-time apartment state, active room detection,
    /// and hotkey instructions for testing the 5 rooms and 2 states.
    /// </summary>
    public class ApartmentHUD : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugHUD = false;

        [Header("UI References")]
        [SerializeField] private Text roomBannerText;
        [SerializeField] private Text statesListText;
        [SerializeField] private Text controlsHintText;

        public void Configure(Text banner, Text statesList, Text hint)
        {
            roomBannerText = banner;
            statesListText = statesList;
            controlsHintText = hint;
        }

        private void Awake()
        {
            if (!showDebugHUD)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            ApartmentManager.OnCurrentRoomChanged += HandleRoomChanged;
            ApartmentManager.OnAnyRoomStateChanged += HandleStateChanged;
            RefreshHUD();
        }

        private void OnDestroy()
        {
            ApartmentManager.OnCurrentRoomChanged -= HandleRoomChanged;
            ApartmentManager.OnAnyRoomStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            RefreshHUD();
        }

        private void HandleRoomChanged(ApartmentRoom room) => RefreshHUD();
        private void HandleStateChanged(ApartmentRoom room, RoomState state) => RefreshHUD();

        private void RefreshHUD()
        {
            var mgr = ApartmentManager.Instance;
            if (mgr == null) return;

            var cur = mgr.CurrentRoom;
            if (roomBannerText != null)
            {
                if (cur != null)
                {
                    string colorTag = cur.CurrentState == RoomState.A ? "<color=#8BE9FD>" : "<color=#FF5555>";
                    roomBannerText.text = $"CURRENT ROOM: <b>{cur.DisplayName.ToUpper()}</b>  |  STATE: {colorTag}<b>TYPE {cur.CurrentState}</b></color>";
                }
                else
                {
                    roomBannerText.text = "CURRENT ROOM: <b>ENTRANCE</b>";
                }
            }

            if (statesListText != null)
            {
                var rooms = mgr.Rooms;
                string b = GetStateTag(rooms, RoomIds.Bedroom, "[1] Bed");
                string l = GetStateTag(rooms, RoomIds.LivingRoom, "[2] Living");
                string k = GetStateTag(rooms, RoomIds.Kitchen, "[3] Kitchen");
                string ba = GetStateTag(rooms, RoomIds.Bathroom, "[4] Bath");
                string h = GetStateTag(rooms, RoomIds.Hallway, "[5] Hall");

                statesListText.text = $"{b}    {l}    {k}    {ba}    {h}    |    Camera: <b>Focused on {cur?.DisplayName ?? "Room"}</b>";
            }

            if (controlsHintText != null)
            {
                controlsHintText.text = "WASD: Move  |  E: Open Door / Inspect  |  TAB: Toggle All Rooms (A/B)  |  1-5: Toggle Room";
            }
        }

        private string GetStateTag(System.Collections.Generic.IReadOnlyDictionary<string, ApartmentRoom> dict, string id, string label)
        {
            if (dict != null && dict.TryGetValue(id, out var r))
            {
                string col = r.CurrentState == RoomState.A ? "#8BE9FD" : "#FF5555";
                string tag = $"{label}: <color={col}><b>{r.CurrentState}</b></color>";

                if (ChangingRoomManager.Instance != null)
                {
                    var tracker = ChangingRoomManager.Instance.GetTracker(id);
                    if (tracker != null && tracker.HasPendingChange)
                    {
                        tag += $" <color=#FFFF00>[->{tracker.PendingState}]</color>";
                    }
                }
                return tag;
            }
            return $"{label}: -";
        }
    }
}
