using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for CraftingTableUI to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Provides component validations and playmode runtime monitoring.
    /// </summary>
    [CustomEditor(typeof(CraftingTableUI))]
    public class CraftingTableUIEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "HUD Crafting Table UI Panel";

        // Search & Filter Properties
        private SerializedProperty searchInputProp;
        private SerializedProperty categoryButtonContainerProp;
        private SerializedProperty categoryButtonPrefabProp;

        // Recipe List Properties
        private SerializedProperty recipeListContainerProp;
        private SerializedProperty recipeSlotPrefabProp;

        // Selected Recipe Info Properties
        private SerializedProperty craftingItemNameTextProp;
        private SerializedProperty craftingItemDescTextProp;
        private SerializedProperty craftingItemIconProp;
        private SerializedProperty craftingTimeTextProp;

        // Ingredient Properties
        private SerializedProperty ingredientSlotsContainerProp;
        private SerializedProperty ingredientSlotPrefabProp;

        // Actions & Buttons Properties
        private SerializedProperty craftButtonProp;
        private SerializedProperty craftButtonTextProp;
        private SerializedProperty closeButtonProp;

        // Progress Properties
        private SerializedProperty progressPanelProp;
        private SerializedProperty progressBarProp;
        private SerializedProperty progressTextProp;

        // Theme Properties
        private SerializedProperty selectedFilterColorProp;
        private SerializedProperty normalFilterColorProp;

        // Playmode Reflection Fields
        private FieldInfo currentTableField;
        private FieldInfo selectedRecipeField;

        private void OnEnable()
        {
            // Search & Filter
            searchInputProp = serializedObject.FindProperty("searchInput");
            categoryButtonContainerProp = serializedObject.FindProperty("categoryButtonContainer");
            categoryButtonPrefabProp = serializedObject.FindProperty("categoryButtonPrefab");

            // Recipe List
            recipeListContainerProp = serializedObject.FindProperty("recipeListContainer");
            recipeSlotPrefabProp = serializedObject.FindProperty("recipeSlotPrefab");

            // Selected Item Details
            craftingItemNameTextProp = serializedObject.FindProperty("craftingItemNameText");
            craftingItemDescTextProp = serializedObject.FindProperty("craftingItemDescText");
            craftingItemIconProp = serializedObject.FindProperty("craftingItemIcon");
            craftingTimeTextProp = serializedObject.FindProperty("craftingTimeText");

            // Dynamic Ingredients
            ingredientSlotsContainerProp = serializedObject.FindProperty("ingredientSlotsContainer");
            ingredientSlotPrefabProp = serializedObject.FindProperty("ingredientSlotPrefab");

            // Actions & Close
            craftButtonProp = serializedObject.FindProperty("craftButton");
            craftButtonTextProp = serializedObject.FindProperty("craftButtonText");
            closeButtonProp = serializedObject.FindProperty("closeButton");

            // Progress Fills
            progressPanelProp = serializedObject.FindProperty("progressPanel");
            progressBarProp = serializedObject.FindProperty("progressBar");
            progressTextProp = serializedObject.FindProperty("progressText");

            // Filter Colors
            selectedFilterColorProp = serializedObject.FindProperty("selectedFilterColor");
            normalFilterColorProp = serializedObject.FindProperty("normalFilterColor");

            // Cache playmode reflection info
            currentTableField = typeof(CraftingTableUI).GetField("currentTable", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            selectedRecipeField = typeof(CraftingTableUI).GetField("selectedRecipe", 
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: Search & Filter Settings ──
            BeginCard("Search & Categories Settings");
            {
                DrawProperty(searchInputProp, "Search Input Field", "TMP InputField used by the player to search recipes by name.");
                DrawProperty(categoryButtonContainerProp, "Category Button parent", "Transform container where category filter buttons are spawned.");
                DrawProperty(categoryButtonPrefabProp, "Category Button Prefab", "Prefab instantiated for each unique item category category filter.");
                if (categoryButtonPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("Category Button Prefab is unassigned! Filters cannot be dynamically constructed.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 2: Recipe List Configuration ──
            BeginCard("Recipe Scroll Container Settings");
            {
                DrawProperty(recipeListContainerProp, "Recipe Scroll Content", "Transform container where available recipe row elements are listed.");
                DrawProperty(recipeSlotPrefabProp, "Recipe Slot Prefab", "Row prefab instantiated for each crafting recipe available in database.");
                if (recipeSlotPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("Recipe Slot Prefab is unassigned! Recipes cannot be listed.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 3: Selected Recipe Info Elements ──
            BeginCard("Selected Recipe Details Labels");
            {
                DrawProperty(craftingItemNameTextProp, "Item Name Label (TMP)", "HUD Text displaying the selected recipe item name.");
                DrawProperty(craftingItemDescTextProp, "Item Desc Label (TMP)", "HUD Text displaying the selected recipe description metadata.");
                DrawProperty(craftingItemIconProp, "Item Icon Display (Image)", "UI Image displaying the selected recipe target sprite.");
                DrawProperty(craftingTimeTextProp, "Item Duration Label (TMP)", "HUD Text displaying duration metrics needed for crafting.");
            }
            EndCard();

            // ── Card 4: Dynamic Ingredient Layout ──
            BeginCard("Dynamic Ingredients Settings");
            {
                DrawProperty(ingredientSlotsContainerProp, "Ingredients Container", "Transform container where dynamically required material slots are listed.");
                DrawProperty(ingredientSlotPrefabProp, "Ingredient Slot Prefab", "Material card slot prefab instantiated for each required item ingredient.");
                if (ingredientSlotPrefabProp.objectReferenceValue == null)
                {
                    DrawMessage("Ingredient Slot Prefab is unassigned! Crafting requirements cannot be displayed.", MessageType.Error);
                }
            }
            EndCard();

            // ── Card 5: Panel Actions ──
            BeginCard("HUD Actions & Buttons Setup");
            {
                DrawProperty(craftButtonProp, "Craft Action Button", "HUD Button triggered to initiate crafting.");
                DrawProperty(craftButtonTextProp, "Craft Button Text (TMP)", "Label overlaying the Craft Button (triggers state changes e.g. Add Items vs Craft).");
                DrawProperty(closeButtonProp, "Panel Close Button", "HUD Button used to safely exit the crafting table overlay.");
            }
            EndCard();

            // ── Card 6: Progress Indicators ──
            BeginCard("Craft Progress Overlay Settings");
            {
                DrawProperty(progressPanelProp, "Progress Panel Root", "GameObject root active exclusively during crafting operations.");
                DrawProperty(progressBarProp, "Progress Fill Slider", "Slider tracking the active crafting progress bar percentage.");
                DrawProperty(progressTextProp, "Progress Label Text (TMP)", "HUD Text displaying percentage digits for crafting progress.");
            }
            EndCard();

            // ── Card 7: Visual Themes (Filter Colors) ──
            BeginCard("Visual Filter Colors Theme");
            {
                // Stacked vertically to prevent clipping
                DrawProperty(selectedFilterColorProp, "Selected Category Color", "Tint color applied to active/selected category filter buttons.");
                DrawProperty(normalFilterColorProp, "Normal Category Color", "Tint color applied to inactive category filter buttons.");
            }
            EndCard();

            // ── Card 8: Playmode Live Debug Monitor ──
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeMonitor();
            }
        }

        /// <summary>
        /// Renders live variables from the active instance in Play Mode.
        /// </summary>
        private void DrawRuntimeMonitor()
        {
            BeginCard("Live Crafting UI Monitor");
            {
                var ui = (CraftingTableUI)target;
                
                // Live state properties
                GUILayout.Label($"<b>Crafting Menu Open</b>: {(ui.IsOpen ? "<color=#66CD00>TRUE</color>" : "<color=#CD2626>FALSE</color>")}", new GUIStyle(EditorStyles.label) { richText = true });

                var currentTable = currentTableField?.GetValue(ui) as CraftingTable;
                GUILayout.Label($"<b>Associated Table Table</b>: {(currentTable != null ? $"<color=#5CACEE>{currentTable.name}</color>" : "<i>None</i>")}", new GUIStyle(EditorStyles.label) { richText = true });

                var selectedRecipe = selectedRecipeField?.GetValue(ui) as CraftingRecipe;
                GUILayout.Label($"<b>Selected Recipe</b>: {(selectedRecipe != null ? $"<color=#9B30FF>{selectedRecipe.recipeName}</color> (ID: {selectedRecipe.recipeId})" : "<i>None Selected</i>")}", new GUIStyle(EditorStyles.label) { richText = true });

                if (currentTable != null && currentTable.IsCrafting)
                {
                    GUILayout.Space(8);
                    
                    // Show progress bar representation
                    float progress = currentTable.CraftingProgress;
                    Rect progressRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(progressRect, new Color(0.12f, 0.14f, 0.16f));
                    
                    Rect fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
                    EditorGUI.DrawRect(fillRect, new Color(0.33f, 0.41f, 0.92f));

                    GUIStyle progressStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    progressStyle.normal.textColor = Color.white;
                    GUI.Label(progressRect, $"Crafting Progress: {progress * 100:F0}%", progressStyle);
                }

                GUILayout.Space(10);

                // Manual close action
                EditorGUI.BeginDisabledGroup(!ui.IsOpen);
                if (GUILayout.Button("Force Close HUD Overlay", MEEditorTheme.StylePrimaryButton))
                {
                    ui.Close();
                    Debug.Log("[CraftingTableUIEditor] Forcibly closed Crafting HUD Overlay.");
                }
                EditorGUI.EndDisabledGroup();

                // Repaint to keep progress bars animating smoothly
                if (ui.IsOpen)
                {
                    Repaint();
                }
            }
            EndCard();
        }
    }
}