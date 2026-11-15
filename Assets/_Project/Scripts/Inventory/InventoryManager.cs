using System;
using System.Collections.Generic;
using UnityEngine;

namespace Room0317.Inventory
{
    /// <summary>
    /// Master coordinator for player inventory and collected clues.
    /// Manages items, dispatches collection events, and allows querying collected clues.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Initial / Debug Items")]
        [SerializeField] private List<InventoryItem> collectedItems = new List<InventoryItem>();

        public IReadOnlyList<InventoryItem> Items => collectedItems;

        public static event Action<InventoryItem> OnItemCollected;
        public static event Action<InventoryItem> OnItemUsed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AddItem(InventoryItem item)
        {
            if (item == null) return;
            if (HasItem(item.id)) return;

            collectedItems.Add(item);
            OnItemCollected?.Invoke(item);
            Debug.Log($"<color=green><b>[Inventory]</b> Collected: {item.displayName} ({item.id})</color>");
        }

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return collectedItems.Exists(i => i != null && i.id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        }

        public InventoryItem GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return collectedItems.Find(i => i != null && i.id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        }

        public bool RemoveItem(string itemId)
        {
            var item = GetItem(itemId);
            if (item != null)
            {
                collectedItems.Remove(item);
                OnItemUsed?.Invoke(item);
                Debug.Log($"<color=yellow><b>[Inventory]</b> Used/Removed: {item.displayName}</color>");
                return true;
            }
            return false;
        }

        public void Clear()
        {
            collectedItems.Clear();
        }
    }
}
