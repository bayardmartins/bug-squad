using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Simplified QuickInventoryUI - manages the hotbar (first N inventory slots).
    /// No separate reference system - quick slots ARE inventory slots 0 to N-1.
    /// </summary>
    public class QuickInventoryUI : MonoBehaviour
    {
        public static QuickInventoryUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject inventoryModePanel;  // Shown when inventory is open
        [SerializeField] private Transform inventoryModeSlotHolder;
        [SerializeField] private GameObject gameplayModePanel;   // Always shown in gameplay
        [SerializeField] private Transform gameplayModeSlotHolder;

        [Header("Prefab")]
        [SerializeField] private QuickSlot quickSlotPrefab;

        private IInventoryManager inventoryManager;
        private IInputManager inputManager;
        private List<QuickSlot> inventoryModeSlots = new List<QuickSlot>();
        private List<QuickSlot> gameplayModeSlots = new List<QuickSlot>();
        private int selectedSlotIndex = 0;
        private bool isInventoryOpen;
        private bool uiDirty;

        // Properties
        public IInventoryManager InventoryManager => inventoryManager;
        public int SelectedSlotIndex => selectedSlotIndex;
        public int QuickSlotCount => inventoryManager?.QuickSlotCount ?? 8;
        public bool IsInventoryOpen => isInventoryOpen;

        // Events
        public event Action<int> OnSlotSelected;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            SetInventoryMode(false);
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
                RefreshAllSlots();
            }
        }

        private void HandleInput()
        {
            if (inputManager == null || (inputManager as MonoBehaviour) == null)
            {
                inputManager = LocalPlayerInstance.InputManager;
            }

            if (inputManager == null) return;

            // Quick slot keys via InputManager
            if (inputManager.QuickSlotPressed >= 0 && inputManager.QuickSlotPressed < QuickSlotCount)
            {
                SelectSlot(inputManager.QuickSlotPressed);
            }

            // Mouse scroll (only in gameplay mode)
            if (!isInventoryOpen)
            {
                float scroll = inputManager.ScrollWheel;
                if (Mathf.Abs(scroll) > 0.1f)
                {
                    int direction = scroll > 0 ? -1 : 1;
                    int newIndex = (selectedSlotIndex + direction + QuickSlotCount) % QuickSlotCount;
                    SelectSlot(newIndex);
                }
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
                RefreshAllSlots();
            }
        }

        private void SetupSlots()
        {
            ClearSlots(inventoryModeSlots);
            ClearSlots(gameplayModeSlots);

            int slotCount = QuickSlotCount;

            // Create inventory mode slots (drag/drop enabled)
            if (inventoryModeSlotHolder != null)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    var slot = Instantiate(quickSlotPrefab, inventoryModeSlotHolder);
                    slot.Initialize(this, i);
                    slot.SetDragDropEnabled(true);
                    inventoryModeSlots.Add(slot);
                }
            }

            // Create gameplay mode slots (selection only)
            if (gameplayModeSlotHolder != null)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    var slot = Instantiate(quickSlotPrefab, gameplayModeSlotHolder);
                    slot.Initialize(this, i);
                    slot.SetDragDropEnabled(false);
                    gameplayModeSlots.Add(slot);
                }
            }
        }

        private void ClearSlots(List<QuickSlot> slotList)
        {
            foreach (var slot in slotList)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            slotList.Clear();
        }

        private void OnInventoryChanged(NetworkList<InventoryManager.InventoryItem> items)
        {
            uiDirty = true;
        }

        public void RefreshAllSlots()
        {
            foreach (var slot in inventoryModeSlots)
                slot.UpdateVisuals();
            foreach (var slot in gameplayModeSlots)
                slot.UpdateVisuals();
        }

        /// <summary>
        /// Clears selection state from all inventory-mode quick slots.
        /// Called when a main inventory slot is clicked to prevent multi-selection.
        /// </summary>
        public void ClearAllSelections()
        {
            foreach (var slot in inventoryModeSlots)
                slot.SetSelected(false);
        }

        public void SetInventoryMode(bool inventoryOpen)
        {
            isInventoryOpen = inventoryOpen;

            if (inventoryModePanel != null)
                inventoryModePanel.SetActive(inventoryOpen);

            if (gameplayModePanel != null)
                gameplayModePanel.SetActive(!inventoryOpen);

            RefreshSelection();
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= QuickSlotCount)
                return;

            selectedSlotIndex = index;
            RefreshSelection();
            OnSlotSelected?.Invoke(selectedSlotIndex);
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < QuickSlotCount; i++)
            {
                bool isSelected = (i == selectedSlotIndex);

                if (i < inventoryModeSlots.Count)
                    inventoryModeSlots[i].SetSelected(isSelected);

                if (i < gameplayModeSlots.Count)
                    gameplayModeSlots[i].SetSelected(isSelected);
            }
        }

        public InventoryManager.InventoryItem GetSelectedItem()
        {
            if (inventoryManager == null || selectedSlotIndex < 0)
                return MultiplayerEngine.InventoryManager.InventoryItem.Empty;

            return inventoryManager.GetItemAt(selectedSlotIndex);
        }

        public int GetSelectedInventorySlotIndex() => selectedSlotIndex;

        public QuickSlot GetSlot(int index, bool fromInventoryMode = true)
        {
            var list = fromInventoryMode ? inventoryModeSlots : gameplayModeSlots;
            return index >= 0 && index < list.Count ? list[index] : null;
        }



        private void OnDestroy()
        {
            if (inventoryManager != null)
                inventoryManager.OnInventoryChanged -= OnInventoryChanged;
        }
    }
}