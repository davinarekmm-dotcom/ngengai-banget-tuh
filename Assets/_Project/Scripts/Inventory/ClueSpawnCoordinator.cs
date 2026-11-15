using System.Collections.Generic;
using UnityEngine;
using Room0317.Apartment;
using Room0317.Objectives;

namespace Room0317.Inventory
{
    /// <summary>
    /// Narrative Clue Spawn Coordinator.
    /// Ensures that clue items (Tarnished Key, Torn Diary Page, Hidden Note) exist in the world
    /// according to story progression if the designer has not placed them manually.
    /// If prefabs are already placed in the scene, this coordinator leaves them untouched.
    /// </summary>
    public class ClueSpawnCoordinator : MonoBehaviour
    {
        public static ClueSpawnCoordinator Instance { get; private set; }

        private const string PrefabsFolder = "Assets/_Project/Prefabs/Objects";

        [Header("Settings")]
        [SerializeField] private bool coordinatorEnabled = false;

        [Header("Clue Prefab Overrides (Optional)")]
        [SerializeField] private GameObject keyPrefab;
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private GameObject hiddenNotePrefab;

        public bool CoordinatorEnabled { get => coordinatorEnabled; set => coordinatorEnabled = value; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            coordinatorEnabled = false;
        }

        private void Start()
        {
            // Fallback spawner completely disabled for Phase 5 Story Progression
            coordinatorEnabled = false;
        }

        private void OnDestroy()
        {
            ChangingRoomManager.OnRoomShifted -= HandleRoomShifted;
        }

        private void EnsureInitialCluesPlaced()
        {
            // Disabled: StoryProgressionCoordinator controls all story clues (Key, Second Chair, Kitchen Note)
        }

        private void HandleRoomShifted(ApartmentRoom room, RoomState newState)
        {
            // When Living Room shifts to State B, spawn the Key on the Coffee Table if not already in world/inventory
            if (room != null && room.RoomId == RoomIds.LivingRoom && newState == RoomState.B)
            {
                EnsureKeySpawnedOnCoffeeTable(room);
            }
        }

        private void EnsureKeySpawnedOnCoffeeTable(ApartmentRoom livingRoom)
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem("key_brass"))
            {
                return; // Already in inventory
            }

            var existingItems = FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in existingItems)
            {
                if (item.ItemData != null && item.ItemData.id == "key_brass")
                {
                    if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                    return; // Already in scene
                }
            }

            // Spawn key on coffee table (living room center offset)
            Vector3 coffeeTablePos = livingRoom.transform.position + new Vector3(-0.35f, -0.85f, 0f);
            SpawnClueInstance("Item_TarnishedKey", "key_brass", "Tarnished Brass Key",
                "An old brass key found in the apartment. Its teeth are worn and dark stains mark the barrel.",
                coffeeTablePos, new Vector3(0.08f, 0.08f, 1f));
        }

        private void SpawnClueInstance(string prefabName, string itemId, string displayName, string desc, Vector3 position, Vector3 localScale)
        {
            GameObject prefab = null;
            if (prefabName == "Item_TarnishedKey") prefab = keyPrefab;
            else if (prefabName == "Item_TornDiaryPage") prefab = notePrefab;
            else if (prefabName == "Item_HiddenNote") prefab = hiddenNotePrefab;

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{prefabName}.prefab");
            }
#endif
            if (prefab != null)
            {
                var instance = Instantiate(prefab, position, Quaternion.identity);
                instance.name = prefabName;
                return;
            }

            // Runtime procedural fallback if prefab asset not found at path
            var go = new GameObject(prefabName);
            go.transform.position = position;
            go.transform.localScale = localScale;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 4.0f;

            var item = go.AddComponent<CollectibleItem>();
            var data = new InventoryItem(itemId, displayName, desc, null, null, true);
            item.Configure(data, 1.4f);
        }

        private ApartmentRoom FindRoom(string roomId)
        {
            if (ApartmentManager.Instance != null && ApartmentManager.Instance.Rooms.TryGetValue(roomId, out var r))
            {
                return r;
            }

            var all = FindObjectsByType<ApartmentRoom>(FindObjectsSortMode.None);
            foreach (var room in all)
            {
                if (room.RoomId == roomId) return room;
            }
            return null;
        }
    }
}
