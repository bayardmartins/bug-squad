using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Database of all crafting recipes.
    /// Recipes are stored as embedded data within this database asset.
    /// Similar pattern to ItemDatabase storing InventoryItemData.
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingRecipeDatabase", menuName = "Multiplayer Engine/Crafting/Recipe Database", order = 0)]
    public class CraftingRecipeDatabase : ScriptableObject
    {
        [Header("Recipes")]
        [Tooltip("All crafting recipes in the game")]
        [SerializeField]
        private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

        /// <summary>
        /// Read-only access to all recipes in the database.
        /// </summary>
        public IReadOnlyList<CraftingRecipe> Recipes => allRecipes;

        /// <summary>
        /// Total count of recipes in the database.
        /// </summary>
        public int Count => allRecipes.Count;

        /// <summary>
        /// Get all valid recipes.
        /// </summary>
        public List<CraftingRecipe> GetAllRecipes()
        {
            var result = new List<CraftingRecipe>();
            foreach (var recipe in allRecipes)
            {
                if (recipe != null)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>
        /// Get recipes filtered by the result item's ObjectType.
        /// Useful for optional filtering in the UI.
        /// </summary>
        public List<CraftingRecipe> GetRecipesByObjectType(ObjectType type)
        {
            var result = new List<CraftingRecipe>();
            foreach (var recipe in allRecipes)
            {
                if (recipe != null && recipe.resultItem != null && recipe.resultItem.objectType == type)
                    result.Add(recipe);
            }
            return result;
        }

        /// <summary>
        /// Find a recipe by its ID.
        /// </summary>
        public CraftingRecipe GetRecipeById(int recipeId)
        {
            foreach (var recipe in allRecipes)
            {
                if (recipe != null && recipe.recipeId == recipeId)
                    return recipe;
            }
            return null;
        }

        /// <summary>
        /// Find a recipe by its name (case-insensitive).
        /// </summary>
        public CraftingRecipe GetRecipeByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (var recipe in allRecipes)
            {
                if (recipe != null && 
                    string.Equals(recipe.recipeName, name, System.StringComparison.OrdinalIgnoreCase))
                    return recipe;
            }
            return null;
        }

        /// <summary>
        /// Checks if a recipe with the given ID exists in the database.
        /// </summary>
        public bool ContainsID(int id)
        {
            return GetRecipeById(id) != null;
        }

        /// <summary>
        /// Gets the next available unique ID for a new recipe.
        /// </summary>
        public int GetNextAvailableID()
        {
            int maxId = 0;
            foreach (var recipe in allRecipes)
            {
                if (recipe != null && recipe.recipeId > maxId)
                    maxId = recipe.recipeId;
            }
            return maxId + 1;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Adds a recipe to the database. Editor-only.
        /// </summary>
        public void AddRecipe(CraftingRecipe recipe)
        {
            if (recipe != null && !allRecipes.Contains(recipe))
            {
                allRecipes.Add(recipe);
            }
        }

        /// <summary>
        /// Removes a recipe from the database. Editor-only.
        /// </summary>
        public void RemoveRecipe(CraftingRecipe recipe)
        {
            if (recipe != null)
            {
                allRecipes.Remove(recipe);
            }
        }

        /// <summary>
        /// Cleans up any null references in the recipes list. Editor-only.
        /// </summary>
        public void CleanupNullReferences()
        {
            allRecipes.RemoveAll(recipe => recipe == null);
        }
#endif
    }
}