using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// UI slot displaying a single ingredient requirement in the crafting UI.
    /// Shows current/required amounts with availability colors and add/remove buttons.
    /// Supports multiplayer co-op where different players can contribute ingredients.
    /// </summary>
    public class CraftingIngredientSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button addButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text addButtonText;

        [Header("Visual Settings")]
        [Tooltip("Color when total available (stored + inventory) meets requirement")]
        [SerializeField] private Color availableColor = new Color(0.3f, 0.8f, 0.3f);
        [Tooltip("Color when have some but not enough")]
        [SerializeField] private Color partialColor = new Color(1f, 0.7f, 0.2f);
        [Tooltip("Color when have nothing available")]
        [SerializeField] private Color unavailableColor = new Color(0.8f, 0.3f, 0.3f);
        [Tooltip("Background color when requirement is met")]
        [SerializeField] private Color completeBgColor = new Color(0.2f, 0.4f, 0.2f, 0.5f);
        [Tooltip("Background color when requirement is not met")]
        [SerializeField] private Color incompleteBgColor = new Color(0.3f, 0.2f, 0.2f, 0.5f);

        private int slotIndex;
        private Action<int> onAdd;
        private Action<int> onRemove;

        private void Awake()
        {
            if (addButton != null)
                addButton.onClick.AddListener(() => onAdd?.Invoke(slotIndex));

            if (removeButton != null)
                removeButton.onClick.AddListener(() => onRemove?.Invoke(slotIndex));
        }

        /// <summary>
        /// Setup the ingredient slot with availability information.
        /// </summary>
        /// <param name="ingredient">The ingredient requirement (null to hide)</param>
        /// <param name="storedAmount">Amount currently in crafting table</param>
        /// <param name="inventoryAmount">Amount available in player inventory</param>
        /// <param name="slotIndex">Index of this slot</param>
        /// <param name="onAdd">Callback when add is clicked</param>
        /// <param name="onRemove">Callback when remove is clicked</param>
        public void Setup(
            CraftingIngredient ingredient,
            int storedAmount,
            int inventoryAmount,
            int slotIndex,
            Action<int> onAdd,
            Action<int> onRemove)
        {
            this.slotIndex = slotIndex;
            this.onAdd = onAdd;
            this.onRemove = onRemove;

            if (ingredient == null || ingredient.item == null)
            {
                // Hide the slot
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            int required = ingredient.amount;
            int totalAvailable = storedAmount + inventoryAmount;
            bool hasEnoughTotal = totalAvailable >= required;
            bool hasEnoughStored = storedAmount >= required;

            // Set icon
            if (iconImage != null)
            {
                iconImage.sprite = ingredient.item.itemIcon;
                iconImage.gameObject.SetActive(ingredient.item.itemIcon != null);
            }

            // Set name
            if (nameText != null)
                nameText.text = ingredient.item.itemName;

            // Set amount text (currentAdded / total need) with color based on availability
            if (amountText != null)
            {
                amountText.text = $"{storedAmount}/{required}";
                
                // Color based on total availability (stored + inventory)
                if (hasEnoughTotal)
                    amountText.color = availableColor;
                else if (totalAvailable > 0)
                    amountText.color = partialColor;
                else
                    amountText.color = unavailableColor;
            }

            // Update add button - interactable when player has items in inventory
            // and still needs more in the table
            if (addButton != null)
            {
                bool canAdd = inventoryAmount > 0 && storedAmount < required;
                addButton.interactable = canAdd;
                
                // Update add button visual to show availability
                var addButtonImage = addButton.GetComponent<Image>();
                if (addButtonImage != null)
                {
                    addButtonImage.color = inventoryAmount > 0 ? availableColor : unavailableColor;
                }
            }

            // Update add button text to show total resources we have in inventory
            if (addButtonText != null)
            {
                addButtonText.text = $"+({inventoryAmount})";
            }

            // Update remove button - can remove if there are items stored
            if (removeButton != null)
            {
                removeButton.interactable = storedAmount > 0;
            }

            // Background color based on whether stored amount meets requirement
            if (backgroundImage != null)
            {
                backgroundImage.color = hasEnoughStored ? completeBgColor : incompleteBgColor;
            }
        }
    }
}