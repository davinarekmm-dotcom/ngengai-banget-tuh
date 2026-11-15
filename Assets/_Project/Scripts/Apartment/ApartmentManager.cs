using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Room0317.Player;

namespace Room0317.Apartment
{
    /// <summary>
    /// Master coordinator for the cohesive apartment scene.
    /// Manages the 5 isolated rooms, executes door transitions with smooth screen fades,
    /// locks the camera to each room separately (never showing neighboring rooms or the whole house),
    /// and coordinates room state toggles (Type A Normal / Type B Eerie).
    /// </summary>
    public class ApartmentManager : MonoBehaviour
    {
        public static ApartmentManager Instance { get; private set; }

        [Header("Rooms")]
        [SerializeField] private ApartmentRoom livingRoom;
        [SerializeField] private ApartmentRoom kitchen;
        [SerializeField] private ApartmentRoom bathroom;
        [SerializeField] private ApartmentRoom hallway;
        [SerializeField] private ApartmentRoom bedroom;

        [Header("State")]
        [SerializeField] private ApartmentRoom currentRoom;

        [Header("Camera")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float cameraOrthoSize = 4.5f; // Perfect 16:9 view of 16x9 room
        [SerializeField] private float cameraSmoothSpeed = 10f;
        [SerializeField] private bool allowSubtleTracking = false; // When true, tiny clamped offset following player

        [Header("Audio")]
        [SerializeField] private AudioClip mainIngameBacksound;

        [Header("Debug")]
        [SerializeField] private bool enableDebugHotkeys = false;

        private AdrianController player;
        private ScreenFader screenFader;
        private bool isTransitioning = false;
        private readonly Dictionary<string, ApartmentRoom> roomLookup = new Dictionary<string, ApartmentRoom>();
        private Vector3 cameraBasePos;
        private float shakeTimer = 0f;
        private float shakeDuration = 0.25f;
        private float shakeIntensity = 0f;

        public static bool ScreenShakeEnabled { get; set; } = true;

        public ApartmentRoom CurrentRoom => currentRoom;
        public IReadOnlyDictionary<string, ApartmentRoom> Rooms => roomLookup;
        public bool IsTransitioning => isTransitioning;

        public static event Action<ApartmentRoom> OnCurrentRoomChanged;
        public static event Action<ApartmentRoom, RoomState> OnAnyRoomStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GetComponent<ChangingRoomManager>() == null)
            {
                gameObject.AddComponent<ChangingRoomManager>();
            }

            var audioMgr = GetComponent<Room0317.Audio.AudioManager>();
            if (audioMgr == null)
            {
                audioMgr = gameObject.AddComponent<Room0317.Audio.AudioManager>();
            }
            if (mainIngameBacksound != null && audioMgr != null)
            {
                audioMgr.SetAmbientBacksound(mainIngameBacksound);
            }

            if (GetComponent<Room0317.UI.ApartmentClock>() == null)
            {
                gameObject.AddComponent<Room0317.UI.ApartmentClock>();
            }

            if (GetComponent<Room0317.Objectives.ObjectiveManager>() == null)
            {
                gameObject.AddComponent<Room0317.Objectives.ObjectiveManager>();
            }

            if (FindFirstObjectByType<Room0317.UI.ObjectiveHUD>() == null)
            {
                gameObject.AddComponent<Room0317.UI.ObjectiveHUD>();
            }

            if (GetComponent<Room0317.Horror.HorrorEventManager>() == null)
            {
                gameObject.AddComponent<Room0317.Horror.HorrorEventManager>();
            }

            if (GetComponent<Room0317.Inventory.InventoryManager>() == null)
            {
                gameObject.AddComponent<Room0317.Inventory.InventoryManager>();
            }

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var uiMod = esGo.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (uiMod != null) uiMod.AssignDefaultActions();
            }

            if (FindFirstObjectByType<Room0317.Inventory.InspectionModal>() == null)
            {
                gameObject.AddComponent<Room0317.Inventory.InspectionModal>();
            }

            if (FindFirstObjectByType<Room0317.Inventory.InventoryHUD>() == null)
            {
                gameObject.AddComponent<Room0317.Inventory.InventoryHUD>();
            }

            if (FindFirstObjectByType<Room0317.UI.PauseMenu>() == null)
            {
                gameObject.AddComponent<Room0317.UI.PauseMenu>();
            }

            if (FindFirstObjectByType<Room0317.UI.GameOverScreen>() == null)
            {
                gameObject.AddComponent<Room0317.UI.GameOverScreen>();
            }

            if (FindFirstObjectByType<Room0317.UI.EndingScreen>() == null)
            {
                gameObject.AddComponent<Room0317.UI.EndingScreen>();
            }

            if (FindFirstObjectByType<Room0317.UI.PhoneNotificationUI>() == null)
            {
                gameObject.AddComponent<Room0317.UI.PhoneNotificationUI>();
            }

            // Note: ClueSpawnCoordinator is omitted here because StoryProgressionCoordinator
            // manages all narrative clue spawning, positions, and visibility cleanly.

            if (GetComponent<Room0317.Story.StoryProgressionCoordinator>() == null)
            {
                gameObject.AddComponent<Room0317.Story.StoryProgressionCoordinator>();
            }

            ResolveRooms();
        }

        private void Start()
        {
            player = FindFirstObjectByType<AdrianController>();
            screenFader = FindFirstObjectByType<ScreenFader>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera != null)
            {
                mainCamera.orthographic = true;
                mainCamera.orthographicSize = cameraOrthoSize;
            }

            RoomTrigger.OnPlayerEnteredRoom += HandlePlayerEnteredRoom;

            // Default initial room: Bedroom
            if (currentRoom == null && bedroom != null)
            {
                currentRoom = bedroom;
            }

            if (currentRoom != null)
            {
                SnapCameraToRoom(currentRoom);
                OnCurrentRoomChanged?.Invoke(currentRoom);
            }
        }

        private void OnDestroy()
        {
            RoomTrigger.OnPlayerEnteredRoom -= HandlePlayerEnteredRoom;
        }

        private void ResolveRooms()
        {
            roomLookup.Clear();
            var all = FindObjectsByType<ApartmentRoom>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                roomLookup[r.RoomId] = r;
                r.OnRoomStateChanged += HandleRoomStateChanged;

                switch (r.RoomId)
                {
                    case RoomIds.LivingRoom: livingRoom = r; break;
                    case RoomIds.Kitchen: kitchen = r; break;
                    case RoomIds.Bathroom: bathroom = r; break;
                    case RoomIds.Hallway: hallway = r; break;
                    case RoomIds.Bedroom: bedroom = r; break;
                }
            }
        }

        private void Update()
        {
            HandleHotkeys();
        }

        private void LateUpdate()
        {
            UpdateCamera();
        }

        // ====================================================================
        // DOOR TRANSITION LOGIC
        // ====================================================================
        public void PerformDoorTransition(DoorTransition door)
        {
            if (isTransitioning || door == null) return;
            StartCoroutine(DoorTransitionRoutine(door));
        }

        private IEnumerator DoorTransitionRoutine(DoorTransition door)
        {
            isTransitioning = true;
            if (player != null) player.CanMove = false;

            // 1. Fade screen out to black
            if (screenFader == null) screenFader = FindFirstObjectByType<ScreenFader>();
            if (screenFader != null)
            {
                yield return screenFader.FadeOut(0.18f);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }

            // 2. Resolve target destination room
            ApartmentRoom target = door.TargetRoom;
            if (target == null && !string.IsNullOrEmpty(door.TargetRoomId))
            {
                roomLookup.TryGetValue(door.TargetRoomId, out target);
            }

            ApartmentRoom previous = currentRoom;

            // Notify ChangingRoomManager: evaluates exit/re-entry rules and commits pending shifts in darkness
            if (ChangingRoomManager.Instance != null)
            {
                ChangingRoomManager.Instance.NotifyDoorTransition(previous, target);
            }

            // 3. Teleport Adrian to doorway inside target room
            Vector2 spawnPos = Vector2.zero;
            if (door.TargetSpawnPoint != null)
            {
                spawnPos = door.TargetSpawnPoint.position;
            }
            else if (target != null)
            {
                spawnPos = target.transform.position;
            }

            if (player != null)
            {
                player.Teleport(spawnPos, door.SpawnFacing);
            }

            // 4. Update Current Room & snap camera to target room center immediately
            if (target != null)
            {
                SetCurrentRoom(target);
                SnapCameraToRoom(target);
            }

            // Small dramatic pause in darkness
            yield return new WaitForSeconds(0.08f);

            // 5. Fade screen back in
            if (screenFader != null)
            {
                yield return screenFader.FadeIn(0.18f);
            }

            if (player != null) player.CanMove = true;
            isTransitioning = false;
        }

        public void SetCurrentRoom(ApartmentRoom room)
        {
            if (room != null && currentRoom != room)
            {
                currentRoom = room;
                Debug.Log($"<color=green>[ApartmentManager] Entered: {currentRoom.DisplayName} ({currentRoom.CurrentState})</color>");
                OnCurrentRoomChanged?.Invoke(currentRoom);
            }
        }

        public void TriggerCameraMicroShake(float intensity = 0.04f, float duration = 0.28f)
        {
            if (!ScreenShakeEnabled) return;
            shakeIntensity = Mathf.Clamp(intensity, 0f, 0.07f);
            shakeDuration = Mathf.Max(0.05f, duration);
            shakeTimer = shakeDuration;
        }

        public void SnapCameraToRoom(ApartmentRoom room)
        {
            if (mainCamera == null || room == null) return;
            Vector3 center = room.transform.position;
            cameraBasePos = new Vector3(center.x, center.y, -10f);
            mainCamera.transform.position = cameraBasePos;
            mainCamera.orthographicSize = cameraOrthoSize;
        }

        private void UpdateCamera()
        {
            if (mainCamera == null || currentRoom == null) return;

            Vector3 roomCenter = currentRoom.transform.position;
            Vector3 targetCamPos = new Vector3(roomCenter.x, roomCenter.y, -10f);

            if (allowSubtleTracking && player != null)
            {
                // Very subtle tracking clamped to small radius so adjacent areas are NEVER shown
                Vector2 diff = (Vector2)player.transform.position - (Vector2)roomCenter;
                Vector2 clampedOffset = Vector2.ClampMagnitude(diff, 0.4f);
                targetCamPos += new Vector3(clampedOffset.x, clampedOffset.y, 0f);
            }

            if (cameraBasePos == Vector3.zero && mainCamera.transform.position.z < -1f)
            {
                cameraBasePos = mainCamera.transform.position;
            }

            // Smoothly ease base framing without compounding shake offset
            cameraBasePos = Vector3.Lerp(cameraBasePos, targetCamPos, Time.deltaTime * cameraSmoothSpeed);
            Vector3 finalPos = cameraBasePos;

            if (shakeTimer > 0f && ScreenShakeEnabled)
            {
                shakeTimer = Mathf.Max(0f, shakeTimer - Time.unscaledDeltaTime);
                float falloff = shakeDuration > 0f ? (shakeTimer / shakeDuration) : 0f;
                float mag = shakeIntensity * falloff;
                finalPos += new Vector3(
                    UnityEngine.Random.Range(-mag, mag),
                    UnityEngine.Random.Range(-mag, mag),
                    0f
                );
            }

            mainCamera.transform.position = finalPos;
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, cameraOrthoSize, Time.deltaTime * cameraSmoothSpeed);
        }

        private void HandlePlayerEnteredRoom(ApartmentRoom room)
        {
            if (room != null && currentRoom != room)
            {
                SetCurrentRoom(room);
            }
        }

        private void HandleRoomStateChanged(ApartmentRoom room, RoomState state)
        {
            OnAnyRoomStateChanged?.Invoke(room, state);
        }

        // ====================================================================
        // HOTKEYS & STATE MANAGEMENT
        // ====================================================================
        private void HandleHotkeys()
        {
            if (!enableDebugHotkeys) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Tab: Toggle ALL rooms
            if (kb.tabKey.wasPressedThisFrame)
            {
                ToggleAllRoomsState();
            }

            // 1-5: Individual room toggles
            if (kb.digit1Key.wasPressedThisFrame && bedroom != null) bedroom.ToggleState();
            if (kb.digit2Key.wasPressedThisFrame && livingRoom != null) livingRoom.ToggleState();
            if (kb.digit3Key.wasPressedThisFrame && kitchen != null) kitchen.ToggleState();
            if (kb.digit4Key.wasPressedThisFrame && bathroom != null) bathroom.ToggleState();
            if (kb.digit5Key.wasPressedThisFrame && hallway != null) hallway.ToggleState();
        }

        public void SetAllRoomsState(RoomState state)
        {
            foreach (var r in roomLookup.Values)
            {
                r.SetState(state);
            }
        }

        public void ToggleAllRoomsState()
        {
            bool anyIsA = false;
            foreach (var r in roomLookup.Values)
            {
                if (r.CurrentState == RoomState.A) { anyIsA = true; break; }
            }
            SetAllRoomsState(anyIsA ? RoomState.B : RoomState.A);
        }

        public void SetRoomState(string roomId, RoomState state)
        {
            if (roomLookup.TryGetValue(roomId, out var room))
            {
                room.SetState(state);
            }
        }
    }
}
