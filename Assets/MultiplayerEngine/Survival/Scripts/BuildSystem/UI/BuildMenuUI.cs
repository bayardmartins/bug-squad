using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Build menu with 6 fixed category slots.
    /// Each slot cycles through its category's pieces from BuildDatabase.
    /// Center panel shows selected item info.
    /// </summary>
    public class BuildMenuUI : MonoBehaviour
    {
        [Header("Build Database")]
        [Tooltip("Reference to the BuildDatabase containing all build pieces")]
        [SerializeField] private BuildDatabase buildDatabase;

        [Header("Category Slots (6 Fixed)")]
        [Tooltip("Assign your 6 BuildUIItem slots directly from the hierarchy")]
        [SerializeField] private BuildUIItem[] categorySlots;

        [Header("Center Info Panel")]
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image resource1Icon;
        [SerializeField] private TMP_Text resource1Text;
        [SerializeField] private Image resource2Icon;
        [SerializeField] private TMP_Text resource2Text;

        [Header("Visual Settings")]
        public float hoverScale = 1.15f;
        public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public Color selectedColor = new Color(0.3f, 0.5f, 0.3f, 0.9f);
        public Color hoverColor = new Color(0.4f, 0.4f, 0.3f, 0.9f);

        private int selectedIndex = -1;
        private int hoveredIndex = -1;
        private bool isOpen;
        private IInventoryManager cachedInventory;
        private CanvasGroup canvasGroup;


        // Events
        public event Action<BuildPieceEntry> OnPieceSelected;
        public event Action<BuildPieceEntry> OnPieceChanged;
        public event Action OnMenuClosed;

        // Properties
        public bool IsOpen => isOpen;
        public BuildPieceEntry SelectedPiece => 
            selectedIndex >= 0 && selectedIndex < categorySlots.Length 
                ? categorySlots[selectedIndex].CurrentPiece 
                : null;

        private void Awake()
        {
            // Use CanvasGroup to hide without disabling Update
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            SetVisible(false);

            // Try to find database if not assigned
            if (buildDatabase == null)
            {
                buildDatabase = FindFirstObjectByType<BuildDatabase>();
            }
        }

        private void Start()
        {
            // Initialize all slots in Start so layout has time to set proper scales
            for (int i = 0; i < categorySlots.Length; i++)
            {
                if (categorySlots[i] != null)
                {
                    categorySlots[i].Initialize(this, i, buildDatabase);
                }
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            // Escape to close
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();

            // Number keys 1-6 for quick select
            for (int i = 0; i < 6 && i < categorySlots.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectSlot(i);
                }
            }

            // Arrow keys for piece cycling on hovered/selected slot
            int activeSlot = hoveredIndex >= 0 ? hoveredIndex : selectedIndex;
            if (activeSlot >= 0 && activeSlot < categorySlots.Length)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    categorySlots[activeSlot].PreviousPiece();
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                    categorySlots[activeSlot].NextPiece();
            }
        }

        #region Open/Close

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            
            SetVisible(true);

            if (cachedInventory == null || (cachedInventory as MonoBehaviour) == null)
                cachedInventory = LocalPlayerInstance.InventoryManager;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Show selected item info if any
            if (selectedIndex >= 0)
            {
                ShowInfo(SelectedPiece);
            }
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            
            SetVisible(false);

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            OnMenuClosed?.Invoke();
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        #endregion

        #region Slot Events (called by BuildUIItem)

        public void OnSlotHover(int index)
        {

            if (hoveredIndex == index) return;

            // Clear previous hover
            if (hoveredIndex >= 0 && hoveredIndex != selectedIndex)
            {
                categorySlots[hoveredIndex].SetHovered(false);
            }

            hoveredIndex = index;

            if (index >= 0 && index < categorySlots.Length)
            {
                categorySlots[index].SetHovered(true);
                ShowInfo(categorySlots[index].CurrentPiece);
            }
        }

        public void OnSlotUnhover(int index)
        {

            if (hoveredIndex != index) return;

            if (index >= 0 && index < categorySlots.Length && index != selectedIndex)
            {
                categorySlots[index].SetHovered(false);
            }

            hoveredIndex = -1;

            // Show selected info or clear
            if (selectedIndex >= 0)
                ShowInfo(SelectedPiece);
            else
                ClearInfo();
        }

        public void SelectSlot(int index)
        {

            // Deselect previous
            if (selectedIndex >= 0 && selectedIndex < categorySlots.Length)
            {
                categorySlots[selectedIndex].SetSelected(false);
            }

            selectedIndex = index;

            if (index >= 0 && index < categorySlots.Length)
            {
                categorySlots[index].SetSelected(true);
                var piece = categorySlots[index].CurrentPiece;
                ShowInfo(piece);
                OnPieceSelected?.Invoke(piece);
            }
        }

        public void NotifyPieceChanged(int index, BuildPieceEntry piece)
        {
            // Update info if this slot is hovered or selected
            if (index == hoveredIndex || index == selectedIndex)
            {
                ShowInfo(piece);
            }

            // If this is the selected slot, notify build system
            if (index == selectedIndex)
            {
                OnPieceChanged?.Invoke(piece);
            }
        }

        #endregion

        #region Info Panel

        private void ShowInfo(BuildPieceEntry piece)
        {
            if (piece == null)
            {
                ClearInfo();
                return;
            }

            if (itemNameText != null) 
                itemNameText.text = piece.pieceName;
            if (descriptionText != null) 
                descriptionText.text = piece.description;

            // Resource costs
            var costs = piece.costs;

            if (costs != null && costs.Length >= 1 && costs[0]?.resource != null)
            {
                if (resource1Icon != null)
                {
                    resource1Icon.sprite = costs[0].resource.itemIcon;
                    resource1Icon.gameObject.SetActive(true);
                }
                    int have = cachedInventory?.GetTotalCount(costs[0].resource.itemId) ?? 0;
                    resource1Text.text = $"{have}/{costs[0].amount}";
            }
            else
            {
                if (resource1Icon != null) resource1Icon.gameObject.SetActive(false);
                if (resource1Text != null) resource1Text.text = "";
            }

            if (costs != null && costs.Length >= 2 && costs[1]?.resource != null)
            {
                if (resource2Icon != null)
                {
                    resource2Icon.sprite = costs[1].resource.itemIcon;
                    resource2Icon.gameObject.SetActive(true);
                }
                    int have = cachedInventory?.GetTotalCount(costs[1].resource.itemId) ?? 0;
                    resource2Text.text = $"{have}/{costs[1].amount}";
            }
            else
            {
                if (resource2Icon != null) resource2Icon.gameObject.SetActive(false);
                if (resource2Text != null) resource2Text.text = "";
            }
        }

        private void ClearInfo()
        {
            if (itemNameText != null) itemNameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (resource1Icon != null) resource1Icon.gameObject.SetActive(false);
            if (resource2Icon != null) resource2Icon.gameObject.SetActive(false);
            if (resource1Text != null) resource1Text.text = "";
            if (resource2Text != null) resource2Text.text = "";
        }

        #endregion
    }
}
