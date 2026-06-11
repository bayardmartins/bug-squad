using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Base class for all inventory slots (main inventory and quick slots).
    /// Handles common functionality: visuals, drag/drop, selection.
    /// </summary>
    public abstract class InventorySlotBase : MonoBehaviour, 
        IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI References")]
        [SerializeField] protected Image itemIcon;
        [SerializeField] protected TextMeshProUGUI countText;
        [SerializeField] protected Image background;
        [SerializeField] protected Slider durabilitySlider;
        [SerializeField] protected TextMeshProUGUI slotNumberText;

        [Header("Visual Settings")]
        [SerializeField] protected Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] protected Color filledSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] protected Color hoverColor = new Color(0.4f, 0.4f, 0.2f, 0.8f);
        [SerializeField] protected Color selectedColor = new Color(0.2f, 0.5f, 0.2f, 0.9f);
        [SerializeField] protected Color dropTargetColor = new Color(0.2f, 0.4f, 0.5f, 0.9f);

        // State
        protected int slotIndex;
        protected bool isSelected;
        protected bool isDragging;
        protected bool dragDropEnabled = true;
        protected CanvasGroup canvasGroup;

        // Static drag state (shared across all slots)
        protected static GameObject draggedItemVisual;
        protected static InventorySlotBase draggedSlot;
        protected static bool isSplitDrag;  // True when right-click dragging (split mode)
        protected static int splitDragAmount;  // Amount being dragged in split mode

        // Properties
        public int SlotIndex => slotIndex;
        public bool IsSelected => isSelected;
        public abstract bool IsEmpty { get; }
        public abstract InventoryManager.InventoryItem CurrentItem { get; }
        protected abstract IInventoryManager GetInventoryManager();

        /// <summary>
        /// True if any slot is currently being dragged.
        /// </summary>
        public static bool IsDragging => draggedSlot != null;

        /// <summary>
        /// True if the current drag is a split operation (right-click drag).
        /// </summary>
        public static bool IsSplitDrag => isSplitDrag;

        /// <summary>
        /// Amount being dragged when in split mode.
        /// </summary>
        public static int SplitDragAmount => splitDragAmount;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public virtual void Initialize(int index)
        {
            slotIndex = index;
            if (slotNumberText != null)
                slotNumberText.text = (index + 1).ToString();
            UpdateVisuals();
        }

        public void SetDragDropEnabled(bool enabled) => dragDropEnabled = enabled;

        #region Visual Updates

        public virtual void UpdateVisuals()
        {
            if (itemIcon == null) return;

            var item = CurrentItem;
            var manager = GetInventoryManager();

            if (IsEmpty || item.IsEmpty)
            {
                ShowEmptySlot();
            }
            else
            {
                ShowFilledSlot(item, manager);
            }

            ApplySelectionColor();
        }

        protected virtual void ShowEmptySlot()
        {
            itemIcon.sprite = null;
            itemIcon.color = Color.clear;

            if (countText != null)
            {
                countText.text = "";
                countText.gameObject.SetActive(false);
            }

            if (durabilitySlider != null)
                durabilitySlider.gameObject.SetActive(false);

            if (background != null)
                background.color = emptySlotColor;
        }

        protected virtual void ShowFilledSlot(InventoryManager.InventoryItem item, IInventoryManager manager)
        {
            var itemData = manager?.GetItemData(item.itemId);
            if (itemData == null) return;

            // Icon
            itemIcon.sprite = itemData.itemIcon;
            itemIcon.color = Color.white;

            // Count
            if (countText != null)
            {
                bool showCount = item.count > 1;
                countText.text = showCount ? item.count.ToString() : "";
                countText.gameObject.SetActive(showCount);
            }

            // Durability
            if (durabilitySlider != null)
            {
                bool hasDurability = itemData.HasDurability && item.durability > 0;
                durabilitySlider.gameObject.SetActive(hasDurability);
                
                if (hasDurability)
                {
                    float percentage = item.GetDurabilityPercentage(itemData.maxDurability);
                    durabilitySlider.value = percentage;

                    var fillImage = durabilitySlider.fillRect?.GetComponent<Image>();
                    if (fillImage != null)
                        fillImage.color = itemData.GetDurabilityColor(percentage);
                }
            }

            if (background != null)
                background.color = filledSlotColor;
        }

        protected virtual void ApplySelectionColor()
        {
            if (isSelected && background != null)
                background.color = selectedColor;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisuals();
        }

        public void SetDropTarget(bool isDropTarget)
        {
            if (background != null)
            {
                if (isDropTarget)
                {
                    background.color = dropTargetColor;
                }
                else
                {
                    if (isSelected)
                        background.color = selectedColor;
                    else
                        background.color = IsEmpty ? emptySlotColor : filledSlotColor;
                }
            }
        }

        #endregion

        #region Pointer Events

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnLeftClick();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick();
            }
        }

        protected virtual void OnLeftClick()
        {
            // Override in derived classes
        }

        protected virtual void OnRightClick()
        {
            // Override in derived classes for drop/context menu
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsDragging && background != null && !isSelected)
                background.color = hoverColor;

            if (draggedSlot != null && draggedSlot != this && dragDropEnabled)
                SetDropTarget(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsDragging && !isSelected)
                UpdateVisuals();

            if (draggedSlot != null && draggedSlot != this)
                SetDropTarget(false);
        }

        #endregion

        #region Drag and Drop

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (!dragDropEnabled || IsEmpty)
                return;

            // Only allow left-click (move) or right-click (split) drag
            bool isLeftClick = eventData.button == PointerEventData.InputButton.Left;
            bool isRightClick = eventData.button == PointerEventData.InputButton.Right;
            
            if (!isLeftClick && !isRightClick)
                return;

            // Right-click drag = split mode (only if stack has more than 1)
            isSplitDrag = isRightClick && CurrentItem.count > 1;
            
            // Can't split a single item
            if (isRightClick && CurrentItem.count <= 1)
                return;

            // Calculate split amount (half the stack)
            splitDragAmount = isSplitDrag ? Mathf.Max(1, CurrentItem.count / 2) : CurrentItem.count;

            isDragging = true;
            draggedSlot = this;

            CreateDragVisual(eventData, isSplitDrag ? splitDragAmount : CurrentItem.count);
            canvasGroup.alpha = 0.5f;
        }

        protected void CreateDragVisual(PointerEventData eventData, int displayCount)
        {
            draggedItemVisual = new GameObject("DraggedItem");
            draggedItemVisual.transform.SetParent(transform.root, false);
            draggedItemVisual.transform.SetAsLastSibling();

            var dragImage = draggedItemVisual.AddComponent<Image>();
            dragImage.sprite = itemIcon.sprite;
            dragImage.color = new Color(1, 1, 1, 0.8f);
            dragImage.raycastTarget = false;

            var item = CurrentItem;
            if (displayCount > 1)
            {
                var textObj = new GameObject("Count");
                textObj.transform.SetParent(draggedItemVisual.transform, false);
                var dragText = textObj.AddComponent<TextMeshProUGUI>();
                dragText.text = displayCount.ToString();
                dragText.fontSize = 14;
                dragText.color = Color.white;
                dragText.alignment = TextAlignmentOptions.BottomRight;
                dragText.raycastTarget = false;
            }

            draggedItemVisual.GetComponent<RectTransform>().sizeDelta = GetComponent<RectTransform>().sizeDelta;
            PositionDragVisual(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (draggedItemVisual != null)
                PositionDragVisual(eventData);
        }

        private void PositionDragVisual(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform.root as RectTransform, eventData.position, eventData.pressEventCamera, out var position))
            {
                draggedItemVisual.transform.localPosition = position;
            }
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.alpha = 1f;

            if (draggedItemVisual != null)
            {
                Destroy(draggedItemVisual);
                draggedItemVisual = null;
            }

            draggedSlot = null;
            isSplitDrag = false;
            splitDragAmount = 0;

            UpdateVisuals();
        }

        public virtual void OnDrop(PointerEventData eventData)
        {
            if (!dragDropEnabled || draggedSlot == null || draggedSlot == this)
                return;

            HandleDrop(draggedSlot);
        }

        protected abstract void HandleDrop(InventorySlotBase fromSlot);

        public static void CancelAllDragOperations()
        {
            if (draggedItemVisual != null)
            {
                Destroy(draggedItemVisual);
                draggedItemVisual = null;
            }

            if (draggedSlot != null)
            {
                draggedSlot.isDragging = false;
                draggedSlot.canvasGroup.alpha = 1f;
                draggedSlot = null;
            }
        }

        #endregion

        protected virtual void OnDestroy()
        {
            if (draggedSlot == this)
            {
                if (draggedItemVisual != null)
                    Destroy(draggedItemVisual);
                draggedSlot = null;
                draggedItemVisual = null;
            }
        }
    }
}
