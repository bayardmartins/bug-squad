using UnityEngine;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Pickable items that can be collected into player inventory.
    /// </summary>
    public class Pickable : Interactable
    {
        [Header("Item Data")]
        [SerializeField] private InventoryItemData inventoryItemData;
        [SerializeField] private int amount = 1;

        [Header("Durability (Tools/Weapons)")]
        [Tooltip("Current durability of the item. Only used for tools and weapons.")]
        [SerializeField] private int currentDurability = -1;  // -1 means use max from item data

        [Header("Loaded Ammo (Shooter Weapons)")]
        [Tooltip("Rounds currently loaded in the magazine. Only used for shooter weapons. -1 means not set.")]
        [SerializeField] private int loadedAmmo = -1;  // -1 means not a shooter weapon or fresh pickup

        public ObjectType ObjectType => InventoryItemData.objectType;
        public InventoryItemData InventoryItemData => inventoryItemData;
        public int Amount => amount;
        public int CurrentDurability => currentDurability;
        public int LoadedAmmo => loadedAmmo;

        /// <summary>
        /// Display name from inventory item data.
        /// </summary>
        public override string DisplayName => 
            inventoryItemData != null && !string.IsNullOrEmpty(inventoryItemData.itemName) 
                ? inventoryItemData.itemName 
                : base.DisplayName;

        /// <summary>
        /// Pickable items always use Pickup interaction type.
        /// </summary>
        public override InteractionType InteractionType => InteractionType.Pickup;

        /// <summary>
        /// Returns the item's description from InventoryItemData.
        /// </summary>
        public override string GetDescription() =>
            inventoryItemData != null && !string.IsNullOrEmpty(inventoryItemData.description)
                ? inventoryItemData.description
                : base.GetDescription();

        /// <summary>
        /// Returns the item's icon from InventoryItemData.
        /// </summary>
        public override Sprite GetIcon() =>
            inventoryItemData != null ? inventoryItemData.itemIcon : null;

        /// <summary>
        /// Returns the amount as interaction info (e.g., "x5").
        /// </summary>
        public override string GetInteractionInfo()
        {
            return amount > 1 ? $"x{amount}" : null;
        }

        /// <summary>
        /// Updates the amount for this pickable.
        /// </summary>
        public void UpdateAmount(int newAmount)
        {
            amount = newAmount;
        }

        /// <summary>
        /// Sets the current durability for tools/weapons.
        /// </summary>
        public void SetDurability(int durability)
        {
            currentDurability = durability;
        }

        /// <summary>
        /// Sets the loaded ammo count for shooter weapons.
        /// </summary>
        public void SetLoadedAmmo(int ammo)
        {
            loadedAmmo = ammo;
        }

        /// <summary>
        /// Gets the loaded ammo count. Returns -1 if not set (non-shooter or fresh item).
        /// </summary>
        public int GetLoadedAmmo()
        {
            return loadedAmmo;
        }

        /// <summary>
        /// Gets the actual durability to use (current or max from item data).
        /// </summary>
        public int GetActualDurability()
        {
            if (currentDurability >= 0)
                return currentDurability;
            
            if (inventoryItemData != null && inventoryItemData.HasDurability)
                return inventoryItemData.maxDurability;
            
            return 0;
        }

        public override void Interact()
        {
            base.Interact();

            if (IsServer)
                NetworkObject.Despawn(true);
        }
    }
}