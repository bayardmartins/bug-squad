using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Central database for all inventory items. Uses Unity's Sub-Asset pattern
    /// to store all items as hidden sub-assets within a single database file.
    /// Similar to Invector's vItemListData pattern.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Multiplayer Engine/Inventory/Item Database", order = 0)]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField]
        private List<InventoryItemData> items = new List<InventoryItemData>();

        /// <summary>
        /// Read-only access to all items in the database.
        /// </summary>
        public IReadOnlyList<InventoryItemData> Items => items;

        /// <summary>
        /// Total count of items in the database.
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// Gets an item by its unique ID.
        /// </summary>
        /// <param name="id">The item ID to search for.</param>
        /// <returns>The item if found, null otherwise.</returns>
        public InventoryItemData GetItemByID(int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == id)
                    return items[i];
            }
            return null;
        }

        /// <summary>
        /// Gets an item by its name (case-insensitive).
        /// </summary>
        /// <param name="name">The item name to search for.</param>
        /// <returns>The item if found, null otherwise.</returns>
        public InventoryItemData GetItemByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && 
                    string.Equals(items[i].itemName, name, System.StringComparison.OrdinalIgnoreCase))
                    return items[i];
            }
            return null;
        }

        /// <summary>
        /// Gets all items of a specific type.
        /// </summary>
        /// <param name="type">The ObjectType to filter by.</param>
        /// <returns>List of items matching the type.</returns>
        public List<InventoryItemData> GetItemsByType(ObjectType type)
        {
            var result = new List<InventoryItemData>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].objectType == type)
                    result.Add(items[i]);
            }
            return result;
        }

        /// <summary>
        /// Checks if an item with the given ID exists in the database.
        /// </summary>
        public bool ContainsID(int id)
        {
            return GetItemByID(id) != null;
        }

        /// <summary>
        /// Gets the next available unique ID for a new item.
        /// </summary>
        /// <returns>The next available ID.</returns>
        public int GetNextAvailableID()
        {
            int maxId = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId > maxId)
                    maxId = items[i].itemId;
            }
            return maxId + 1;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Adds an item to the database. Editor-only.
        /// The item should already be added as a sub-asset before calling this.
        /// </summary>
        public void AddItem(InventoryItemData item)
        {
            if (item != null && !items.Contains(item))
            {
                items.Add(item);
            }
        }

        /// <summary>
        /// Removes an item from the database. Editor-only.
        /// This only removes from the list - caller must handle asset deletion.
        /// </summary>
        public void RemoveItem(InventoryItemData item)
        {
            if (item != null)
            {
                items.Remove(item);
            }
        }

        /// <summary>
        /// Cleans up any null references in the items list. Editor-only.
        /// </summary>
        public void CleanupNullReferences()
        {
            items.RemoveAll(item => item == null);
        }
#endif
    }
}
