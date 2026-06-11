using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Main UI panel for the crafting table.
    /// Features:
    /// - Search bar to filter recipes by name
    /// - Auto-generated category buttons based on recipe result ObjectTypes
    /// - Scrollable recipe list
    /// </summary>
    public class CraftingTableUI : MonoBehaviour
    {
        public static CraftingTableUI Instance { get; private set; }

        #region Serialized Fields

        [Header("Search & Filter")]
        [Tooltip("Input field for searching recipes by name")]
        [SerializeField] private TMP_InputField searchInput;
        
        [Tooltip("Container for auto-generated category filter buttons")]
        [SerializeField] private Transform categoryButtonContainer;
        
        [Tooltip("Prefab for category filter buttons")]
        [SerializeField] private Button categoryButtonPrefab;

        [Header("Recipe List")]
        [Tooltip("Container for recipe slots (use ScrollRect)")]
        [SerializeField] private Transform recipeListContainer;
        [SerializeField] private CraftingRecipeSlot recipeSlotPrefab;


        [Header("Crafting Item Info")]
        [SerializeField] private TMP_Text craftingItemNameText;
        [SerializeField] private TMP_Text craftingItemDescText;
        [SerializeField] private Image craftingItemIcon;
        [SerializeField] private TMP_Text craftingTimeText;

        [Header("Ingredient Slots")]
        [Tooltip("Container for dynamically generated ingredient slots")]
        [SerializeField] private Transform ingredientSlotsContainer;
        [Tooltip("Prefab for ingredient slots")]
        [SerializeField] private CraftingIngredientSlot ingredientSlotPrefab;

        [Header("Actions")]
        [SerializeField] private Button craftButton;
        [SerializeField] private TMP_Text craftButtonText;
        [SerializeField] private Button closeButton;

        [Header("Progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text progressText;

        [Header("Visual Settings")]
        [SerializeField] private Color selectedFilterColor = new Color(0.3f, 0.6f, 0.3f);
        [SerializeField] private Color normalFilterColor = new Color(0.25f, 0.25f, 0.25f);

        #endregion

        #region Private Fields

        private CraftingTable currentTable;
        private CraftingRecipe selectedRecipe;
        private List<CraftingRecipeSlot> activeRecipeSlots = new List<CraftingRecipeSlot>();
        private List<CraftingIngredientSlot> activeIngredientSlots = new List<CraftingIngredientSlot>();
        private List<Button> activeCategoryButtons = new List<Button>();
        private IInventoryManager localInventory;
        private CanvasGroup canvasGroup;
        private bool isOpen;

        // Filter state
        private string searchText = "";
        private ObjectType? selectedCategory = null; // null = show all

        #endregion

        #region Properties

        public bool IsOpen => isOpen;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            SetVisible(false);
        }

        private void Start()
        {
            if (craftButton != null)
                craftButton.onClick.AddListener(OnCraftClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            // Setup search input
            if (searchInput != null)
            {
                searchInput.onValueChanged.AddListener(OnSearchChanged);
            }
        }

        private void OnEnable()
        {
            CraftingTable.OnTableStateChanged += OnTableStateChanged;
        }

        private void OnDisable()
        {
            CraftingTable.OnTableStateChanged -= OnTableStateChanged;
        }

        private void Update()
        {
            if (!isOpen) return;

            // Escape to close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }

            // Update progress display
            if (currentTable != null && currentTable.IsCrafting)
            {
                UpdateProgressDisplay();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Open the UI for a specific crafting table.
        /// </summary>
        public void Open(CraftingTable table)
        {
            if (table == null) return;

            currentTable = table;
            currentTable.SetLocalUI(this);
            isOpen = true;

            // Reset filters
            searchText = "";
            selectedCategory = null;
            if (searchInput != null)
                searchInput.text = "";

            // Find local inventory
            localInventory = LocalPlayerInstance.InventoryManager;

            SetVisible(true);

            // Unlock cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Check if table has a selected recipe
            if (currentTable.SelectedRecipeId >= 0 && currentTable.RecipeDatabase != null)
            {
                selectedRecipe = currentTable.RecipeDatabase.GetRecipeById(currentTable.SelectedRecipeId);
            }

            // Build category buttons from available recipes
            BuildCategoryButtons();
            RefreshUI();
        }

        /// <summary>
        /// Close the UI.
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;

            // Tell server we're done
            if (currentTable != null && currentTable.IsLocalPlayerInteracting())
            {
                currentTable.CloseUIRpc();
            }

            isOpen = false;
            currentTable = null;
            selectedRecipe = null;

            SetVisible(false);

            // Lock cursor
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        #endregion

        #region Category Buttons (Auto-Generated)

        private void BuildCategoryButtons()
        {
            // Clear existing buttons
            foreach (var btn in activeCategoryButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            activeCategoryButtons.Clear();

            if (currentTable?.RecipeDatabase == null || categoryButtonContainer == null || categoryButtonPrefab == null)
                return;

            // Find all unique ObjectTypes from recipes
            var categories = new HashSet<ObjectType>();
            foreach (var recipe in currentTable.RecipeDatabase.GetAllRecipes())
            {
                if (recipe.resultItem != null)
                    categories.Add(recipe.resultItem.objectType);
            }

            // Create "All" button first
            CreateCategoryButton("All", null);

            // Create buttons for each category
            foreach (var category in categories)
            {
                CreateCategoryButton(category.ToString(), category);
            }

            UpdateCategoryButtonVisuals();
        }

        private void CreateCategoryButton(string label, ObjectType? category)
        {
            var btn = Instantiate(categoryButtonPrefab, categoryButtonContainer);
            
            // Set button text
            var text = btn.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = label;

            // Store category in button name for identification
            btn.name = category.HasValue ? category.Value.ToString() : "All";

            // Add click listener
            btn.onClick.AddListener(() => OnCategorySelected(category));
            
            activeCategoryButtons.Add(btn);
        }

        private void OnCategorySelected(ObjectType? category)
        {
            selectedCategory = category;
            UpdateCategoryButtonVisuals();
            PopulateRecipeList();
        }

        private void UpdateCategoryButtonVisuals()
        {
            foreach (var btn in activeCategoryButtons)
            {
                if (btn == null) continue;

                bool isSelected = (btn.name == "All" && !selectedCategory.HasValue) ||
                                  (selectedCategory.HasValue && btn.name == selectedCategory.Value.ToString());

                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = isSelected ? selectedFilterColor : normalFilterColor;
            }
        }

        #endregion

        #region Search

        private void OnSearchChanged(string text)
        {
            searchText = text.Trim().ToLower();
            PopulateRecipeList();
        }

        #endregion

        #region Recipe List

        private void PopulateRecipeList()
        {
            // Clear existing slots
            foreach (var slot in activeRecipeSlots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            activeRecipeSlots.Clear();

            if (currentTable?.RecipeDatabase == null || recipeSlotPrefab == null || recipeListContainer == null)
                return;

            // Get filtered recipes
            var allRecipes = currentTable.RecipeDatabase.GetAllRecipes();
            var filteredRecipes = new List<CraftingRecipe>();

            foreach (var recipe in allRecipes)
            {
                // Apply category filter
                if (selectedCategory.HasValue)
                {
                    if (recipe.resultItem == null || recipe.resultItem.objectType != selectedCategory.Value)
                        continue;
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matchesName = recipe.recipeName.ToLower().Contains(searchText);
                    bool matchesResult = recipe.resultItem != null && 
                                         recipe.resultItem.itemName.ToLower().Contains(searchText);
                    
                    if (!matchesName && !matchesResult)
                        continue;
                }

                filteredRecipes.Add(recipe);
            }

            // Create slots for filtered recipes
            foreach (var recipe in filteredRecipes)
            {
                var slot = Instantiate(recipeSlotPrefab, recipeListContainer);
                slot.Setup(recipe, localInventory, OnRecipeSelected);
                slot.SetSelected(recipe == selectedRecipe);
                activeRecipeSlots.Add(slot);
            }
        }

        #endregion

        #region Recipe Selection

        private void OnRecipeSelected(CraftingRecipe recipe)
        {
            selectedRecipe = recipe;

            // Update slot visuals
            foreach (var slot in activeRecipeSlots)
            {
                slot.SetSelected(slot.Recipe == recipe);
            }

            // Tell server
            if (currentTable != null)
            {
                currentTable.SelectRecipeRpc(recipe.recipeId);
            }

            ShowRecipeInfo(recipe);
            UpdateIngredientSlots();
            UpdateCraftButton();
        }

        private void ShowRecipeInfo(CraftingRecipe recipe)
        {
            if (recipe == null || recipe.resultItem == null)
            {
                ClearRecipeInfo();
                return;
            }

            var resultItem = recipe.resultItem;

            // Show crafting item name
            if (craftingItemNameText != null)
                craftingItemNameText.text = resultItem.itemName;

            // Show crafting item icon
            if (craftingItemIcon != null)
            {
                craftingItemIcon.sprite = resultItem.itemIcon;
                craftingItemIcon.gameObject.SetActive(resultItem.itemIcon != null);
            }

            // Show crafting item description
            if (craftingItemDescText != null)
                craftingItemDescText.text = resultItem.description;

            // Show crafting time
            if (craftingTimeText != null)
                craftingTimeText.text = $"Time: {recipe.craftingTime}s";
        }

        private void ClearRecipeInfo()
        {
            if (craftingItemNameText != null)
                craftingItemNameText.text = "Select a recipe";

            if (craftingItemDescText != null)
                craftingItemDescText.text = "";

            if (craftingItemIcon != null)
                craftingItemIcon.gameObject.SetActive(false);

            if (craftingTimeText != null)
                craftingTimeText.text = "";
        }

        #endregion

        #region Ingredient Slots

        private void UpdateIngredientSlots()
        {
            // Clear existing dynamic slots
            foreach (var slot in activeIngredientSlots)
            {
                if (slot != null)
                    Destroy(slot.gameObject);
            }
            activeIngredientSlots.Clear();

            if (selectedRecipe == null || ingredientSlotsContainer == null || ingredientSlotPrefab == null)
                return;

            var storedItems = currentTable?.GetAllStoredItems() ?? new CraftingSlotItem[4];

            // Create a slot for each ingredient in the recipe
            for (int i = 0; i < selectedRecipe.ingredients.Length; i++)
            {
                var ingredient = selectedRecipe.ingredients[i];
                if (ingredient == null || ingredient.item == null) continue;

                // Find matching stored amount
                int storedAmount = 0;
                foreach (var item in storedItems)
                {
                    if (item.itemId == ingredient.item.itemId)
                        storedAmount += item.amount;
                }

                int inventoryAmount = localInventory != null
                    ? localInventory.GetTotalCount(ingredient.item.itemId)
                    : 0;

                // Instantiate and setup the slot
                var slot = Instantiate(ingredientSlotPrefab, ingredientSlotsContainer);
                int slotIndex = i;
                slot.Setup(
                    ingredient,
                    storedAmount,
                    inventoryAmount,
                    slotIndex,
                    OnAddIngredient,
                    OnRemoveIngredient
                );
                activeIngredientSlots.Add(slot);
            }
        }

        private void OnAddIngredient(int slotIndex)
        {
            if (currentTable == null || selectedRecipe == null) return;
            if (slotIndex < 0 || slotIndex >= selectedRecipe.ingredients.Length) return;

            var ingredient = selectedRecipe.ingredients[slotIndex];
            
            // Add one at a time
            currentTable.AddIngredientRpc(ingredient.item.itemId, 1);
        }

        private void OnRemoveIngredient(int slotIndex)
        {
            if (currentTable == null || selectedRecipe == null) return;
            if (slotIndex < 0 || slotIndex >= selectedRecipe.ingredients.Length) return;

            var ingredient = selectedRecipe.ingredients[slotIndex];

            // Find the actual slot with this item
            var storedItems = currentTable.GetAllStoredItems();
            for (int i = 0; i < storedItems.Length; i++)
            {
                if (storedItems[i].itemId == ingredient.item.itemId && storedItems[i].amount > 0)
                {
                    currentTable.RemoveIngredientRpc(i, 1);
                    break;
                }
            }
        }

        #endregion

        #region Crafting

        private void OnCraftClicked()
        {
            if (currentTable == null || selectedRecipe == null) return;

            // Check if can craft from table or inventory
            var storedItems = currentTable.GetAllStoredItems();
            bool canCraftFromTable = selectedRecipe.CanCraft(storedItems);
            bool canCraftFromInventory = CanCraftFromInventory();

            if (!canCraftFromTable && !canCraftFromInventory) return;

            // Use direct craft RPC which handles both table and inventory consumption
            currentTable.StartCraftingDirectRpc();
        }

        /// <summary>
        /// Check if player can craft using combined table + inventory ingredients.
        /// </summary>
        private bool CanCraftFromInventory()
        {
            if (selectedRecipe == null || localInventory == null) return false;

            var storedItems = currentTable?.GetAllStoredItems() ?? new CraftingSlotItem[4];

            foreach (var ingredient in selectedRecipe.ingredients)
            {
                if (ingredient?.item == null) continue;

                // Calculate stored amount
                int storedAmount = 0;
                foreach (var item in storedItems)
                {
                    if (item.itemId == ingredient.item.itemId)
                        storedAmount += item.amount;
                }

                // Calculate inventory amount
                int inventoryAmount = localInventory.GetTotalCount(ingredient.item.itemId);

                // Check if combined total is enough
                if (storedAmount + inventoryAmount < ingredient.amount)
                    return false;
            }
            return true;
        }

        private void UpdateCraftButton()
        {
            if (craftButton == null) return;

            bool canCraft = false;
            if (selectedRecipe != null && currentTable != null && !currentTable.IsCrafting)
            {
                // Only allow crafting when ingredients have been added to the table
                var storedItems = currentTable.GetAllStoredItems();
                canCraft = selectedRecipe.CanCraft(storedItems);
            }

            craftButton.interactable = canCraft;

            if (craftButtonText != null)
            {
                if (currentTable?.IsCrafting == true)
                    craftButtonText.text = "Crafting...";
                else
                    craftButtonText.text = canCraft ? "Craft" : "Add Items";
            }
        }

        private void UpdateProgressDisplay()
        {
            if (currentTable == null) return;

            bool isCrafting = currentTable.IsCrafting;

            if (progressPanel != null)
                progressPanel.SetActive(isCrafting);

            if (progressBar != null)
                progressBar.value = currentTable.CraftingProgress;

            if (progressText != null)
            {
                int percent = Mathf.RoundToInt(currentTable.CraftingProgress * 100);
                progressText.text = $"{percent}%";
            }
        }

        #endregion

        #region UI Helpers

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        private void RefreshUI()
        {
            PopulateRecipeList();
            
            if (selectedRecipe != null)
                ShowRecipeInfo(selectedRecipe);
            else
                ClearRecipeInfo();

            UpdateIngredientSlots();
            UpdateCraftButton();
            UpdateProgressDisplay();
        }

        private void OnTableStateChanged(CraftingTable table)
        {
            if (table != currentTable) return;

            // Refresh ingredient display and craft button
            UpdateIngredientSlots();
            UpdateCraftButton();
            UpdateProgressDisplay();
        }

        #endregion
    }
}