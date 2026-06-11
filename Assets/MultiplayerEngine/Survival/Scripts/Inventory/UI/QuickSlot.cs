using UnityEngine;
using UnityEngine.EventSystems;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Quick inventory slot (hotbar). 
    /// Quick slots are mapped to the first N slots of the main inventory.
    /// </summary>
    public class QuickSlot : InventorySlotBase
    {
        private QuickInventoryUI quickInventoryUI;
        private IInputManager inputManager;

        // Quick slots map directly to inventory indices
        private int InventoryIndex => slotIndex;

        public override bool IsEmpty => 
            quickInventoryUI?.InventoryManager == null ||
            InventoryIndex >= quickInventoryUI.InventoryManager.Slots.Count ||
            quickInventoryUI.InventoryManager.Slots[InventoryIndex].IsEmpty;

        public override InventoryManager.InventoryItem CurrentItem =>
            quickInventoryUI?.InventoryManager?.GetItemAt(InventoryIndex) ?? InventoryManager.InventoryItem.Empty;

        protected override IInventoryManager GetInventoryManager() => quickInventoryUI?.InventoryManager;

        public void Initialize(QuickInventoryUI ui, int index)
        {
            quickInventoryUI = ui;
            base.Initialize(index);
        }

        protected override void OnLeftClick()
        {
            // Select this quick slot
            quickInventoryUI?.SelectSlot(slotIndex);
        }

        protected override void OnRightClick()
        {
            // Right-click is handled by drag (split-drag)
            // No action on just right-click release
        }

        public override void OnDrop(PointerEventData eventData)
        {
            if (!dragDropEnabled) return;

            // Handle drop from main inventory slot
            var invSlot = eventData.pointerDrag?.GetComponent<InventorySlot>();
            if (invSlot != null && !invSlot.IsEmpty)
            {
                var manager = GetInventoryManager();
                if (manager != null)
                {
                    manager.SwapSlotsRpc(invSlot.SlotIndex, InventoryIndex);
                }
                return;
            }

            // Handle drop from another quick slot
            var quickSlot = eventData.pointerDrag?.GetComponent<QuickSlot>();
            if (quickSlot != null && quickSlot != this)
            {
                var manager = GetInventoryManager();
                if (manager != null)
                {
                    manager.SwapSlotsRpc(quickSlot.InventoryIndex, InventoryIndex);
                }
            }
        }

        protected override void HandleDrop(InventorySlotBase fromSlot)
        {
            if (fromSlot is QuickSlot qs && qs != this)
            {
                var manager = GetInventoryManager();
                manager?.SwapSlotsRpc(qs.InventoryIndex, InventoryIndex);
            }
        }
    }
}
