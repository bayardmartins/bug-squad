using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Drop zone UI component - drag items here to drop them into the world.
    /// Shows visual feedback when an item is being dragged over.
    /// </summary>
    public class InventoryDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image dropZoneImage;
        [SerializeField] private Image dropIcon;

        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        [SerializeField] private Color hoverColor = new Color(0.8f, 0.3f, 0.2f, 0.9f);
        [SerializeField] private Color iconNormalColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color iconHoverColor = new Color(1f, 1f, 1f, 1f);

        private bool isHovering;

        private void Start()
        {
            ResetVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Only show hover state if we're dragging an item
            if (InventorySlotBase.IsDragging)
            {
                isHovering = true;
                ApplyHoverVisuals();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            ResetVisuals();
        }

        public void OnDrop(PointerEventData eventData)
        {
            isHovering = false;
            ResetVisuals();

            // Handle drop from inventory slot
            var inventorySlot = eventData.pointerDrag?.GetComponent<InventorySlot>();
            if (inventorySlot != null && !inventorySlot.IsEmpty)
            {
                DropItem(inventorySlot);
                return;
            }

            // Handle drop from quick slot
            var quickSlot = eventData.pointerDrag?.GetComponent<QuickSlot>();
            if (quickSlot != null && !quickSlot.IsEmpty)
            {
                DropFromQuickSlot(quickSlot);
            }
        }

        private void DropItem(InventorySlot slot)
        {
            var manager = InventoryUI.Instance?.InventoryManager;
            if (manager == null) return;

            // If it's a split-drag, only drop the split amount
            int dropAmount = InventorySlotBase.IsSplitDrag ? InventorySlotBase.SplitDragAmount : -1;
            manager.DropItemRpc(slot.SlotIndex, dropAmount);
        }

        private void DropFromQuickSlot(QuickSlot slot)
        {
            var manager = QuickInventoryUI.Instance?.InventoryManager;
            if (manager == null) return;

            // If it's a split-drag, only drop the split amount
            int dropAmount = InventorySlotBase.IsSplitDrag ? InventorySlotBase.SplitDragAmount : -1;
            manager.DropItemRpc(slot.SlotIndex, dropAmount);
        }

        private void ApplyHoverVisuals()
        {
            if (dropZoneImage != null)
                dropZoneImage.color = hoverColor;

            if (dropIcon != null)
                dropIcon.color = iconHoverColor;
        }

        private void ResetVisuals()
        {
            if (dropZoneImage != null)
                dropZoneImage.color = normalColor;

            if (dropIcon != null)
                dropIcon.color = iconNormalColor;
        }

        private void Update()
        {
            // Continuously check drag state to update visuals
            if (isHovering && !InventorySlotBase.IsDragging)
            {
                isHovering = false;
                ResetVisuals();
            }
        }
    }
}
