using System;
using UnityEngine;

namespace Room0317.Inventory
{
    /// <summary>
    /// Represents a clue or collectible item in ROOM 03:17.
    /// Supports an in-world/HUD icon and a high-resolution close-up inspection sprite.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        [Header("Identity")]
        public string id = "item_id";
        public string displayName = "Item Name";

        [TextArea(2, 4)]
        public string description = "Description of the item.";

        [Header("Visuals")]
        public Sprite icon;             // Small icon for HUD / inventory slot
        public Sprite inspectionSprite; // Full-resolution illustration for Inspection Modal

        [Header("Type")]
        public bool isClue = true;
        public bool canBeUsed = true;

        public InventoryItem() { }

        public InventoryItem(string id, string name, string desc, Sprite icon, Sprite inspectSprite, bool isClue = true)
        {
            this.id = id;
            this.displayName = name;
            this.description = desc;
            this.icon = icon;
            this.inspectionSprite = inspectSprite;
            this.isClue = isClue;
        }
    }
}
