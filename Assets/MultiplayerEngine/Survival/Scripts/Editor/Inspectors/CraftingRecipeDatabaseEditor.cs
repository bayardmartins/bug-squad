using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Inspector Editor for CraftingRecipeDatabase ScriptableObject.
    /// Prevents raw inspection/modification of recipes in the standard inspector to maintain integrity.
    /// Provides detailed statistics, diagnostic warnings, and a button to launch the dedicated editor window.
    /// </summary>
    [CustomEditor(typeof(CraftingRecipeDatabase))]
    public class CraftingRecipeDatabaseEditor : UnityEditor.Editor
    {
        private CraftingRecipeDatabase database;

        private void OnEnable()
        {
            database = (CraftingRecipeDatabase)target;
        }

        public override void OnInspectorGUI()
        {
            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Renders standard Multiplayer Engine Header styling inside the inspector
            MEEditorTheme.DrawHeader("Recipe Database", "Central Crafting Recipe & Ingredient Matrix Storage");

            GUILayout.Space(5);

            // 2. Purpose & Warning Card
            MEEditorTheme.BeginCard("System Overview");
            {
                EditorGUILayout.HelpBox(
                    "This ScriptableObject acts as a container database storing all crafting recipes, cooking requirements, and material ingredients as embedded data.\n\n" +
                    "⚠ DIRECT RAW EDITING IN THE INSPECTOR IS DISABLED to prevent broken serialization, lost ingredient keys, or recipe corruption.",
                    MessageType.Info
                );

                GUILayout.Space(8);

                string assetPath = AssetDatabase.GetAssetPath(database);
                EditorGUILayout.LabelField("Asset Path:", assetPath, MEEditorTheme.StyleLabelMuted);

                if (File.Exists(assetPath))
                {
                    FileInfo fileInfo = new FileInfo(assetPath);
                    string sizeText = (fileInfo.Length / 1024f).ToString("F2") + " KB";
                    EditorGUILayout.LabelField("Database File Size:", sizeText, MEEditorTheme.StyleLabelMuted);
                }
            }
            MEEditorTheme.EndCard();

            // 3. Database Statistics Card
            MEEditorTheme.BeginCard("Recipes Statistics");
            {
                int totalCount = database.Count;
                int weaponCount = 0;
                int toolCount = 0;
                int consumableCount = 0;
                int resourceCount = 0;
                int otherCount = 0;
                bool hasNulls = false;
                bool hasInvalidIngredients = false;
                bool hasDuplicateIDs = false;

                var idSet = new System.Collections.Generic.HashSet<int>();

                if (database.Recipes != null)
                {
                    for (int i = 0; i < database.Recipes.Count; i++)
                    {
                        var recipe = database.Recipes[i];
                        if (recipe == null)
                        {
                            hasNulls = true;
                            continue;
                        }

                        // Check for duplicate IDs
                        if (idSet.Contains(recipe.recipeId))
                        {
                            hasDuplicateIDs = true;
                        }
                        else
                        {
                            idSet.Add(recipe.recipeId);
                        }

                        // Check for valid ingredients
                        if (recipe.ingredients == null || recipe.ingredients.Length == 0)
                        {
                            hasInvalidIngredients = true;
                        }
                        else
                        {
                            foreach (var ingredient in recipe.ingredients)
                            {
                                if (ingredient == null || ingredient.item == null || ingredient.amount <= 0)
                                {
                                    hasInvalidIngredients = true;
                                    break;
                                }
                            }
                        }

                        // Categorize based on result item
                        if (recipe.resultItem != null)
                        {
                            switch (recipe.resultItem.objectType)
                            {
                                case ObjectType.Weapon:
                                    weaponCount++;
                                    break;
                                case ObjectType.Tools:
                                    toolCount++;
                                    break;
                                case ObjectType.Consumable:
                                    consumableCount++;
                                    break;
                                case ObjectType.Resource:
                                    resourceCount++;
                                    break;
                                default:
                                    otherCount++;
                                    break;
                            }
                        }
                        else
                        {
                            otherCount++;
                        }
                    }
                }

                // Render metrics
                DrawStatRow("Total Registered Recipes:", totalCount.ToString(), EditorStyles.boldLabel);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawStatRow("⚔ Weapon Recipes:", weaponCount.ToString(), EditorStyles.label);
                DrawStatRow("🛠 Tool Recipes:", toolCount.ToString(), EditorStyles.label);
                DrawStatRow("🍎 Consumable Recipes:", consumableCount.ToString(), EditorStyles.label);
                DrawStatRow("🪵 Resource / Material Recipes:", resourceCount.ToString(), EditorStyles.label);
                DrawStatRow("❓ Custom / Misconfigured Output:", otherCount.ToString(), EditorStyles.label);

                // Diagnostics Check
                if (hasNulls || hasInvalidIngredients || hasDuplicateIDs)
                {
                    GUILayout.Space(10);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(5);
                    GUILayout.Label("Diagnostics Status:", EditorStyles.boldLabel);

                    if (hasNulls)
                    {
                        EditorGUILayout.HelpBox("Warning: Database contains empty/null recipe entries! Clean them via the recipe database window.", MessageType.Warning);
                    }
                    if (hasInvalidIngredients)
                    {
                        EditorGUILayout.HelpBox("Warning: Some recipes have missing ingredients or invalid counts of 0!", MessageType.Warning);
                    }
                    if (hasDuplicateIDs)
                    {
                        EditorGUILayout.HelpBox("Critical Error: Duplicate recipe ID keys detected! Fix immediately in the editor window.", MessageType.Error);
                    }
                }
            }
            MEEditorTheme.EndCard();

            // 4. Primary Editor Controls Card
            MEEditorTheme.BeginCard("Database Actions");
            {
                GUI.backgroundColor = MEEditorTheme.ColorAccent;
                if (GUILayout.Button(new GUIContent("🎯 Open Dedicated Recipe Database Window", EditorGUIUtility.IconContent("d_CustomTool").image), MEEditorTheme.StylePrimaryButton))
                {
                    RecipeDatabaseWindow.Open();
                }
                GUI.backgroundColor = Color.white;

                GUILayout.Space(8);

                if (GUILayout.Button("Find Asset in Project Explorer", MEEditorTheme.StyleSecondaryButton))
                {
                    EditorGUIUtility.PingObject(database);
                }
            }
            MEEditorTheme.EndCard();

            // 5. Standard Footer branding
            MEEditorTheme.DrawFooter();
        }

        private void DrawStatRow(string label, string value, GUIStyle valueStyle)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200));
            EditorGUILayout.LabelField(value, valueStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
    }
}