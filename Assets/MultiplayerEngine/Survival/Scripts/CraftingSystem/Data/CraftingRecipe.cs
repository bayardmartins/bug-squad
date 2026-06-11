using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Defines a single crafting recipe with required ingredients and result.
    /// Can craft any item type (resources, consumables, tools, weapons).
    /// Stored as embedded data within CraftingRecipeDatabase.
    /// </summary>
    [System.Serializable]
    public class CraftingRecipe
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for this recipe")]
        public int recipeId;
        
        [Tooltip("Display name shown in the crafting UI")]
        public string recipeName;
        
        [TextArea(2, 4)]
        [Tooltip("Recipe description - shown in crafting UI")]
        public string description;
        
        [Tooltip("Icon shown in the recipe list")]
        public Sprite icon;

        [Header("Ingredients")]
        [Tooltip("Required ingredients to craft this recipe")]
        public CraftingIngredient[] ingredients;

        [Header("Result")]
        [Tooltip("The item produced when crafted")]
        public InventoryItemData resultItem;
        
        [Tooltip("Number of items produced")]
        public int resultAmount = 1;

        [Header("Crafting")]
        [Tooltip("Time in seconds to craft this recipe")]
        public float craftingTime = 5f;

        /// <summary>
        /// Check if a set of provided items meets the recipe requirements.
        /// </summary>
        public bool CanCraft(CraftingSlotItem[] providedItems)
        {
            if (ingredients == null) return false;
            
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || ingredient.item == null) continue;
                
                int provided = 0;
                foreach (var item in providedItems)
                {
                    if (item.itemId == ingredient.item.itemId)
                        provided += item.amount;
                }
                if (provided < ingredient.amount)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A single ingredient requirement for a recipe.
    /// </summary>
    [System.Serializable]
    public class CraftingIngredient
    {
        [Tooltip("The required item")]
        public InventoryItemData item;
        
        [Tooltip("Amount required")]
        public int amount = 1;
    }

    /// <summary>
    /// Network-serializable struct for items stored in crafting table slots.
    /// </summary>
    [System.Serializable]
    public struct CraftingSlotItem : Unity.Netcode.INetworkSerializable, System.IEquatable<CraftingSlotItem>
    {
        public int itemId;
        public int amount;

        public static CraftingSlotItem Empty => new CraftingSlotItem { itemId = -1, amount = 0 };
        public bool IsEmpty => itemId == -1 || amount <= 0;

        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer) where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref amount);
        }

        public bool Equals(CraftingSlotItem other) => itemId == other.itemId && amount == other.amount;
        
        public override bool Equals(object obj) => obj is CraftingSlotItem other && Equals(other);
        
        public override int GetHashCode() => System.HashCode.Combine(itemId, amount);
    }
}