namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Interface for all item handlers (weapons, tools, consumables, resources).
    /// Implemented by specific handler classes to provide item-type behavior.
    /// </summary>
    public interface IItemHandler
    {
        /// <summary>
        /// Called when item is equipped/selected by the player.
        /// </summary>
        void OnEquip(EquipmentController controller, InventoryItemData itemData, int inventorySlot);

        /// <summary>
        /// Called when item is unequipped/deselected.
        /// </summary>
        void OnUnequip();

        /// <summary>
        /// Primary action (left mouse button).
        /// </summary>
        /// <param name="pressed">True when pressed, false when released</param>
        void OnPrimaryAction(bool pressed);

        /// <summary>
        /// Secondary action (right mouse button).
        /// </summary>
        /// <param name="pressed">True when pressed, false when released</param>
        void OnSecondaryAction(bool pressed);

        /// <summary>
        /// Called every frame while item is equipped.
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Clean up resources when handler is destroyed.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Whether this item type shows a held object.
        /// </summary>
        bool HasVisualModel { get; }

        void OnBladeEnable();
        void OnBladeDisable();
        void OnComboWindowStart();
        void OnComboWindowEnd();
        void OnAttackExitEnd();
        void OnReloadComplete();
        void OnGrabArrow();
        void OnNockArrow();

        /// <summary>
        /// Whether this item is currently equipped.
        /// </summary>
        bool IsEquipped { get; }
    }
}
