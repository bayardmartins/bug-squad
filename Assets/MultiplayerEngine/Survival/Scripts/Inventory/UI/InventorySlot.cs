using UnityEngine;
using UnityEngine.EventSystems;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Main inventory slot - extends base with swap, split, and drop functionality.
    /// </summary>
    public class InventorySlot : InventorySlotBase
    {
        private InventoryUI inventoryUI;
        private IInputManager inputManager;

        public override bool IsEmpty => inventoryUI?.InventoryManager == null || 
            slotIndex >= inventoryUI.InventoryManager.Slots.Count ||
            inventoryUI.InventoryManager.Slots[slotIndex].IsEmpty;

        public override InventoryManager.InventoryItem CurrentItem => 
            inventoryUI?.InventoryManager?.GetItemAt(slotIndex) ?? InventoryManager.InventoryItem.Empty;

        protected override IInventoryManager GetInventoryManager() => inventoryUI?.InventoryManager;

        public void Initialize(InventoryUI ui, int index)
        {
            inventoryUI = ui;
            base.Initialize(index);
        }

        protected override void OnLeftClick()
        {
            // Clear all other selections first, then toggle this slot
            inventoryUI?.ClearAllSelections(this);
            SetSelected(!isSelected);
        }

        protected override void OnRightClick()
        {
            // Right-click is handled by drag (split-drag)
            // No action on just right-click release
        }

        public override void OnDrop(PointerEventData eventData)
        {
            if (!dragDropEnabled) return;

            // Handle drop from another inventory slot
            var fromSlot = eventData.pointerDrag?.GetComponent<InventorySlot>();
            if (fromSlot != null && fromSlot != this)
            {
                HandleInventorySwap(fromSlot);
                return;
            }

            // Handle drop from quick slot (copy reference)
            var quickSlot = eventData.pointerDrag?.GetComponent<QuickSlot>();
            if (quickSlot != null && !quickSlot.IsEmpty)
            {
                var manager = GetInventoryManager();
                int sourceIndex = quickSlot.SlotIndex;
                if (manager != null && sourceIndex != slotIndex)
                {
                    manager.SwapSlotsRpc(sourceIndex, slotIndex);
                }
            }
        }

        protected override void HandleDrop(InventorySlotBase fromSlot)
        {
            if (fromSlot is InventorySlot invSlot)
                HandleInventorySwap(invSlot);
        }

        private void HandleInventorySwap(InventorySlot fromSlot)
        {
            var manager = GetInventoryManager();
            if (manager == null) return;

            // Check if this is a split-drag operation (right-click drag)
            if (IsSplitDrag && !fromSlot.IsEmpty && fromSlot.CurrentItem.count > 1)
            {
                // Split the dragged amount to this slot
                manager.SplitStackRpc(fromSlot.SlotIndex, slotIndex, SplitDragAmount);
            }
            else
            {
                // Normal swap (left-click drag)
                manager.SwapSlotsRpc(fromSlot.SlotIndex, slotIndex);
            }
        }


    }
}
