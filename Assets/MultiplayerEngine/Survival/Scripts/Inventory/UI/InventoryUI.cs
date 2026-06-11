using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Main inventory UI controller - handles the full inventory panel.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Transform slotHolder;
        [SerializeField] private InventorySlot slotPrefab;
        [SerializeField] private UnityEngine.UI.Button backButton;

        private IInventoryManager inventoryManager;
        private IInputManager inputManager;
        private List<InventorySlot> inventorySlots = new List<InventorySlot>();
        private CanvasGroup canvasGroup;
        private bool isOpen;
        private bool uiDirty;

        // Properties
        public bool IsInventoryOpen => isOpen;
        public IInventoryManager InventoryManager => inventoryManager;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            // Get or add CanvasGroup for visibility control without disabling
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            // Hide panel but keep script running for input detection
            SetPanelVisible(false);

            // Setup back button if assigned
            if (backButton != null)
                backButton.onClick.AddListener(CloseInventory);
        }

        private void Update()
        {
            HandleInput();
        }

        private void LateUpdate()
        {
            if (uiDirty)
            {
                uiDirty = false;
                RefreshUI();
            }
        }

        private void HandleInput()
        {
            if (inputManager == null || (inputManager as MonoBehaviour) == null)
            {
                inputManager = LocalPlayerInstance.InputManager;
            }

            if (inputManager == null) return;

            // Toggle inventory via InputManager
            if (inputManager.ToggleInventory)
            {
                ToggleInventory();
            }

            // Close with cancel (Escape) via InputManager
            if (inputManager.Cancel && IsInventoryOpen)
            {
                CloseInventory();
            }
        }

        public void Initialize(InventoryManager manager)
        {
            if (inventoryManager != null)
                inventoryManager.OnInventoryChanged -= OnInventoryChanged;

            inventoryManager = manager;

            if (inventoryManager != null)
            {
                inputManager = manager.GetComponent<IInputManager>();
                inventoryManager.OnInventoryChanged += OnInventoryChanged;
                SetupSlots();
                RefreshUI();

                // Initialize quick inventory as well
                QuickInventoryUI.Instance?.Initialize(manager);
            }
        }

        private void SetupSlots()
        {
            // Clear existing
            foreach (var slot in inventorySlots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            inventorySlots.Clear();

            // Create slots (skip quick slots since they're in the quick bar)
            // Start from quickSlotCount if you want to separate them, or from 0 for all
            int startIndex = inventoryManager.QuickSlotCount; // Skip quick slots
            for (int i = startIndex; i < inventoryManager.MaxInventorySize; i++)
            {
                var slot = Instantiate(slotPrefab, slotHolder);
                slot.Initialize(this, i);
                inventorySlots.Add(slot);
            }
        }

        private void OnInventoryChanged(NetworkList<InventoryManager.InventoryItem> items)
        {
            uiDirty = true;
        }

        public void RefreshUI()
        {
            if (inventoryManager == null) return;

            foreach (var slot in inventorySlots)
            {
                slot.UpdateVisuals();
            }

            QuickInventoryUI.Instance?.RefreshAllSlots();
        }

        /// <summary>
        /// Clears selection from all slots except the specified one.
        /// </summary>
        public void ClearAllSelections(InventorySlot exceptSlot = null)
        {
            foreach (var slot in inventorySlots)
            {
                if (slot != exceptSlot)
                    slot.SetSelected(false);
            }

            // Also clear quick slot selections
            QuickInventoryUI.Instance?.ClearAllSelections();
        }

        public void ToggleInventory()
        {
            if (IsInventoryOpen)
                CloseInventory();
            else
                OpenInventory();
        }

        public void OpenInventory()
        {
            if (isOpen) return;
            isOpen = true;
            
            SetPanelVisible(true);
            RefreshUI();

            // Switch quick inventory to drag/drop mode
            QuickInventoryUI.Instance?.SetInventoryMode(true);

            // Show cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;
            
            SetPanelVisible(false);

            // Cancel any ongoing drags
            InventorySlotBase.CancelAllDragOperations();

            // Clear selections
            foreach (var slot in inventorySlots)
                slot.SetSelected(false);

            // Switch quick inventory to gameplay mode
            QuickInventoryUI.Instance?.SetInventoryMode(false);

            // Hide cursor
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void SetPanelVisible(bool visible)
        {
            // Use inventoryPanel if it's a child, otherwise use CanvasGroup
            if (inventoryPanel != null && inventoryPanel != gameObject)
            {
                inventoryPanel.SetActive(visible);
            }
            else if (canvasGroup != null)
            {
                // Panel is this GameObject - use CanvasGroup to hide without disabling
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        public InventorySlot GetSlot(int index)
        {
            // Adjust for quick slot offset
            int adjustedIndex = index - inventoryManager.QuickSlotCount;
            if (adjustedIndex >= 0 && adjustedIndex < inventorySlots.Count)
                return inventorySlots[adjustedIndex];
            return null;
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
                inventoryManager.OnInventoryChanged -= OnInventoryChanged;

            if (Instance == this)
                Instance = null;
        }
    }
}