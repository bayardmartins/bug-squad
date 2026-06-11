using System.Collections.Generic;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for querying and interacting with the player's inventory.
    /// Decouples systems like EquipmentController from the specific InventoryManager implementation.
    /// </summary>
    public interface IInventoryManager
    {
        /// <summary>
        /// Event fired when the inventory is changed.
        /// </summary>
        event System.Action<NetworkList<InventoryManager.InventoryItem>> OnInventoryChanged;

        /// <summary>
        /// The list of networked inventory slots.
        /// </summary>
        NetworkList<InventoryManager.InventoryItem> Slots { get; }
        
        /// <summary>
        /// Maximum number of items the inventory can hold.
        /// </summary>
        int MaxInventorySize { get; }

        /// <summary>
        /// Number of slots dedicated to quick bar/equipped items.
        /// </summary>
        int QuickSlotCount { get; }

        /// <summary>
        /// Retrieves the item data for a specific item ID.
        /// </summary>
        InventoryItemData GetItemData(int itemId);

        /// <summary>
        /// Checks if the inventory contains at least one of the specified item.
        /// </summary>
        bool HasItem(int itemId);

        /// <summary>
        /// Returns the total count of a specific item across all slots.
        /// </summary>
        int GetTotalCount(int itemId);

        /// <summary>
        /// Returns the item currently in the specified slot index.
        /// </summary>
        InventoryManager.InventoryItem GetItemAt(int index);

        /// <summary>
        /// Removes the item at the specified slot index (server only).
        /// </summary>
        void RemoveItemAt(int index);

        /// <summary>
        /// Finds the first available empty slot index. Returns -1 if full.
        /// </summary>
        int FindFirstEmptySlot();
        
        /// <summary>
        /// Checks if an item drop can be added to the inventory.
        /// </summary>
        bool TryAddToInventory(Pickable pickable);

        /// <summary>
        /// Tries to add an item by ID to the inventory.
        /// </summary>
        bool TryAddItem(int itemId, int amount = 1);

        void ConsumeItemRpc(int slotIndex, int amount = 1);
        void RemoveItemByIDRpc(int itemId, int amount = 1);
        void DropItemRpc(int slotIndex, int amount = -1);
        void SwapSlotsRpc(int fromIndex, int toIndex);
        void SplitStackRpc(int fromIndex, int toIndex, int amount);
        void ReduceDurabilityRpc(int slotIndex, int amount);
        void SetLoadedAmmoRpc(int slotIndex, int ammo);
    }
}
