using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// UI slot displaying a single recipe in the crafting recipe list.
    /// </summary>
    public class CraftingRecipeSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image canCraftIndicator;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button selectButton;

        [Header("Visual Settings")]
        [SerializeField] private Color canCraftColor = new Color(0.2f, 0.7f, 0.2f);
        [SerializeField] private Color cannotCraftColor = new Color(0.7f, 0.2f, 0.2f);
        [SerializeField] private Color selectedBgColor = new Color(0.3f, 0.5f, 0.3f, 0.9f);
        [SerializeField] private Color normalBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        private CraftingRecipe recipe;
        private Action<CraftingRecipe> onSelected;
        private bool isSelected;

        public CraftingRecipe Recipe => recipe;

        private void Awake()
        {
            if (selectButton != null)
                selectButton.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// Setup the slot with recipe data.
        /// </summary>
        public void Setup(CraftingRecipe recipe, IInventoryManager inventory, Action<CraftingRecipe> onSelected)
        {
            this.recipe = recipe;
            this.onSelected = onSelected;

            if (iconImage != null)
            {
                iconImage.sprite = recipe.icon;
                iconImage.gameObject.SetActive(recipe.icon != null);
            }

            if (nameText != null)
                nameText.text = recipe.recipeName;

            // Check if player has ingredients in inventory
            bool canCraft = true;
            if (inventory != null)
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    if (inventory.GetTotalCount(ingredient.item.itemId) < ingredient.amount)
                    {
                        canCraft = false;
                        break;
                    }
                }
            }

            if (canCraftIndicator != null)
                canCraftIndicator.color = canCraft ? canCraftColor : cannotCraftColor;
        }

        /// <summary>
        /// Set selection state.
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (backgroundImage != null)
                backgroundImage.color = selected ? selectedBgColor : normalBgColor;
        }

        private void OnClicked()
        {
            onSelected?.Invoke(recipe);
        }
    }
}