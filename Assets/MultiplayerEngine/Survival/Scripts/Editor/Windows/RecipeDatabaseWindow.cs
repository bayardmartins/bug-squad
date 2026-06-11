using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Editor Window for managing CraftingRecipeDatabase.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// Provides full CRUD operations for recipes using embedded data pattern.
    /// </summary>
    public class RecipeDatabaseWindow : MEEditorWindow
    {
        // Database references
        private CraftingRecipeDatabase database;
        private ItemDatabase itemDatabase;
        private SerializedObject serializedDatabase;
        private SerializedProperty recipesProperty;

        // UI State
        private Vector2 leftPanelScroll;
        private Vector2 rightPanelScroll;
        private int selectedIndex = -1;
        private CraftingRecipe selectedRecipe;
        
        // Item database cache for dropdowns
        private string[] itemNames;
        private InventoryItemData[] itemDataCache;

        // Search and filter
        private string searchFilter = "";
        private ObjectType? typeFilter = null;
        private SearchField searchField;
        
        // Mini Button Styles
        private GUIStyle styleMiniButtonActive;
        private GUIStyle styleMiniButtonInactive;

        // Layout
        private const float LEFT_PANEL_WIDTH = 290f;
        private const float ITEM_HEIGHT = 50f;

        protected override bool UseGlobalScrollView => false;
        protected override string WindowSubtitle => "Crafting Recipe Database & Ingredient Manager";

        [MenuItem("Tools/Multiplayer Engine/Recipe Database", false, 22)]
        public static void Open()
        {
            var window = GetWindow<RecipeDatabaseWindow>();
            window.titleContent = new GUIContent("Recipe Database", EditorGUIUtility.IconContent("d_CustomTool").image);
            window.minSize = new Vector2(850, 500);
            window.Show();
        }

        private void OnEnable()
        {
            searchField = new SearchField();
            
            // Try to find existing recipe database if none set
            if (database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:CraftingRecipeDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    database = AssetDatabase.LoadAssetAtPath<CraftingRecipeDatabase>(path);
                }
            }

            // Try to find ItemDatabase for item selection dropdown
            if (itemDatabase == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                }
            }

            RefreshSerializedObject();
            RefreshItemCache();
            titleContent = new GUIContent("Recipe Database", EditorGUIUtility.IconContent("d_CustomTool").image);
        }

        private void OnDisable()
        {
            // Save any pending changes
            if (database != null)
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
        }

        private void RefreshSerializedObject()
        {
            if (database != null)
            {
                serializedDatabase = new SerializedObject(database);
                recipesProperty = serializedDatabase.FindProperty("allRecipes");
            }
        }

        private void RefreshItemCache()
        {
            if (itemDatabase == null || itemDatabase.Items == null)
            {
                itemNames = new string[] { "(No Item Database)" };
                itemDataCache = new InventoryItemData[0];
                return;
            }

            var items = itemDatabase.Items.Where(i => i != null).ToList();
            itemNames = new string[items.Count + 1];
            itemDataCache = new InventoryItemData[items.Count + 1];

            itemNames[0] = "(None)";
            itemDataCache[0] = null;

            for (int i = 0; i < items.Count; i++)
            {
                itemNames[i + 1] = $"{items[i].itemName} (ID: {items[i].itemId})";
                itemDataCache[i + 1] = items[i];
            }
        }

        private int GetItemIndex(InventoryItemData item)
        {
            if (item == null || itemDataCache == null) return 0;
            
            for (int i = 0; i < itemDataCache.Length; i++)
            {
                if (itemDataCache[i] == item)
                    return i;
            }
            return 0;
        }

        private void InitMiniButtonStyles()
        {
            if (styleMiniButtonActive != null) return;

            styleMiniButtonActive = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 22,
                alignment = TextAnchor.MiddleCenter
            };
            styleMiniButtonActive.normal.background = MEEditorTheme.GetTexture(MEEditorTheme.ColorAccent);
            styleMiniButtonActive.normal.textColor = Color.white;

            styleMiniButtonInactive = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fixedHeight = 22,
                alignment = TextAnchor.MiddleCenter
            };
            styleMiniButtonInactive.normal.background = MEEditorTheme.GetTexture(MEEditorTheme.ColorWindowBg);
            styleMiniButtonInactive.normal.textColor = MEEditorTheme.ColorTextMuted;
            styleMiniButtonInactive.hover.background = MEEditorTheme.GetTexture(MEEditorTheme.ColorBorder);
            styleMiniButtonInactive.hover.textColor = Color.white;
        }

        protected override void DrawBody()
        {
            // Check for database
            if (database == null)
            {
                DrawNoDatabaseUI();
                return;
            }

            serializedDatabase?.Update();

            EditorGUILayout.BeginHorizontal();
            {
                // Left Section - Recipe List Card (Constrained Width)
                EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH), GUILayout.ExpandHeight(true));
                {
                    MEEditorTheme.BeginCard("Recipes List");
                    DrawLeftPanel();
                    MEEditorTheme.EndCard();
                }
                EditorGUILayout.EndVertical();

                // Right Section - Recipe Details Card
                MEEditorTheme.BeginCard(selectedRecipe != null ? $"Recipe Details - {selectedRecipe.recipeName}" : "Recipe Details");
                DrawRightPanel();
                MEEditorTheme.EndCard();
            }
            EditorGUILayout.EndHorizontal();

            serializedDatabase?.ApplyModifiedProperties();
        }

        private void DrawNoDatabaseUI()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            {
                MEEditorTheme.BeginCard("No Recipe Database Selected");

                GUILayout.Label("Select or create a Crafting Recipe Database ScriptableObject to manage recipes.", MEEditorTheme.StyleHeaderSub);
                GUILayout.Space(15);

                // Database field
                EditorGUI.BeginChangeCheck();
                database = (CraftingRecipeDatabase)EditorGUILayout.ObjectField("Database File", database, typeof(CraftingRecipeDatabase), false);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshSerializedObject();
                }

                GUILayout.Space(15);

                if (GUILayout.Button("Create New Database", MEEditorTheme.StylePrimaryButton))
                {
                    CreateNewDatabase();
                }

                MEEditorTheme.EndCard();
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Recipe Database",
                "CraftingRecipeDatabase",
                "asset",
                "Choose a location to save the Recipe Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                database = ScriptableObject.CreateInstance<CraftingRecipeDatabase>();
                AssetDatabase.CreateAsset(database, path);
                AssetDatabase.SaveAssets();
                RefreshSerializedObject();
                EditorGUIUtility.PingObject(database);
            }
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            {
                InitMiniButtonStyles();

                // 1. Database reference selector (styled card style)
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Label("DB File:", MEEditorTheme.StyleLabelMuted, GUILayout.Width(50));
                    EditorGUI.BeginChangeCheck();
                    database = (CraftingRecipeDatabase)EditorGUILayout.ObjectField(database, typeof(CraftingRecipeDatabase), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshSerializedObject();
                        selectedIndex = -1;
                        selectedRecipe = null;
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(6);

                // 2. Search bar (styled card style)
                EditorGUILayout.BeginHorizontal();
                {
                    searchFilter = searchField.OnGUI(searchFilter, GUILayout.ExpandWidth(true));
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // 3. Type filter bar (custom modern buttons)
                EditorGUILayout.BeginHorizontal();
                {
                    bool isAll = typeFilter == null;
                    if (GUILayout.Button("All", isAll ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        typeFilter = null;

                    bool isWeapon = typeFilter == ObjectType.Weapon;
                    if (GUILayout.Button("Wpn", isWeapon ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        typeFilter = isWeapon ? null : ObjectType.Weapon;
                    
                    bool isTool = typeFilter == ObjectType.Tools;
                    if (GUILayout.Button("Tool", isTool ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        typeFilter = isTool ? null : ObjectType.Tools;
                    
                    bool isConsumable = typeFilter == ObjectType.Consumable;
                    if (GUILayout.Button("Cons", isConsumable ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        typeFilter = isConsumable ? null : ObjectType.Consumable;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // 4. Create/Delete actions (custom modern buttons)
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = MEEditorTheme.ColorSuccess;
                    if (GUILayout.Button(new GUIContent(" Create", EditorGUIUtility.IconContent("d_CreateAddNew").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        CreateNewRecipe();
                    }
                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(8);

                    GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f);
                    GUI.enabled = selectedRecipe != null;
                    if (GUILayout.Button(new GUIContent(" Delete", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        DeleteSelectedRecipe();
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Recipe count label
                int totalCount = database.Count;
                int filteredCount = GetFilteredRecipes().Count;
                EditorGUILayout.LabelField($"Recipes: {filteredCount} / {totalCount}", EditorStyles.centeredGreyMiniLabel);

                // Recipe list scroll panel - vertical scrollbar only, horizontal disabled
                leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                {
                    DrawRecipeList();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private List<CraftingRecipe> GetFilteredRecipes()
        {
            var result = new List<CraftingRecipe>();
            if (database == null || database.Recipes == null) return result;
            
            for (int i = 0; i < database.Recipes.Count; i++)
            {
                var recipe = database.Recipes[i];
                if (recipe == null) continue;

                // Type filter (based on result item)
                if (typeFilter.HasValue)
                {
                    if (recipe.resultItem == null || recipe.resultItem.objectType != typeFilter.Value)
                        continue;
                }

                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    bool matchesName = recipe.recipeName?.ToLower().Contains(searchFilter.ToLower()) ?? false;
                    bool matchesId = recipe.recipeId.ToString().Contains(searchFilter);
                    bool matchesResult = recipe.resultItem?.itemName?.ToLower().Contains(searchFilter.ToLower()) ?? false;
                    if (!matchesName && !matchesId && !matchesResult)
                        continue;
                }

                result.Add(recipe);
            }

            return result;
        }

        private void DrawRecipeList()
        {
            var filteredRecipes = GetFilteredRecipes();

            for (int i = 0; i < filteredRecipes.Count; i++)
            {
                var recipe = filteredRecipes[i];
                bool isSelected = recipe == selectedRecipe;

                Rect itemRect = EditorGUILayout.BeginHorizontal(isSelected ? MEEditorTheme.StyleListItemSelected : MEEditorTheme.StyleListItem, 
                    GUILayout.Height(ITEM_HEIGHT));
                {
                    // Icon
                    Sprite icon = recipe.icon ?? recipe.resultItem?.itemIcon;
                    if (icon != null)
                    {
                        Rect iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
                        DrawSpriteIcon(iconRect, icon);
                    }
                    else
                    {
                        GUILayout.Label(EditorGUIUtility.IconContent("d_CustomTool").image, GUILayout.Width(40), GUILayout.Height(40));
                    }

                    GUILayout.Space(8);

                    // Recipe info
                    EditorGUILayout.BeginVertical();
                    {
                        string displayName = string.IsNullOrEmpty(recipe.recipeName) ? "(No Name)" : recipe.recipeName;
                        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                        
                        string resultName = recipe.resultItem != null ? recipe.resultItem.itemName : "(No Result)";
                        string resultType = recipe.resultItem != null ? recipe.resultItem.objectType.ToString() : "";
                        EditorGUILayout.LabelField($"ID: {recipe.recipeId} | Result: {resultName} ({resultType})", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                // Handle selection
                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    SelectRecipe(recipe, filteredRecipes.IndexOf(recipe));
                    Event.current.Use();
                }
            }
        }

        private void SelectRecipe(CraftingRecipe recipe, int index)
        {
            selectedRecipe = recipe;
            selectedIndex = index;
            
            for (int i = 0; i < database.Recipes.Count; i++)
            {
                if (database.Recipes[i] == recipe)
                {
                    selectedIndex = i;
                    break;
                }
            }

            Repaint();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            {
                if (selectedRecipe == null)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Select a recipe from the left panel to inspect and edit.", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    // Header Toolbar
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
                    {
                        Sprite icon = selectedRecipe.icon ?? selectedRecipe.resultItem?.itemIcon;
                        if (icon != null)
                        {
                            Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
                            DrawSpriteIcon(iconRect, icon);
                            GUILayout.Space(5);
                        }
                        
                        GUIStyle selectedHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                        selectedHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
                        string displayName = string.IsNullOrEmpty(selectedRecipe.recipeName) ? "(No Name)" : selectedRecipe.recipeName;
                        EditorGUILayout.LabelField(displayName, selectedHeaderStyle);
                        
                        GUILayout.FlexibleSpace();
                        
                        // Recipe ID badge
                        GUI.enabled = false;
                        EditorGUILayout.LabelField($"ID: {selectedRecipe.recipeId}", EditorStyles.miniLabel, GUILayout.Width(50));
                        GUI.enabled = true;
                        
                        if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        {
                            DuplicateSelectedRecipe();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Recipe editor scroll body - vertical scrollbar only, horizontal disabled
                    rightPanelScroll = EditorGUILayout.BeginScrollView(rightPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                    {
                        EditorGUILayout.Space(10);
                        DrawRecipeEditor();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRecipeEditor()
        {
            if (selectedRecipe == null)
                return;

            EditorGUI.BeginChangeCheck();
            
            // =========================================================================
            // BASIC INFO SECTION
            // =========================================================================
            DrawSectionHeader("Basic Info");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Recipe Name", EditorStyles.miniLabel);
                selectedRecipe.recipeName = EditorGUILayout.TextField(selectedRecipe.recipeName, EditorStyles.textField);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
                selectedRecipe.description = EditorGUILayout.TextArea(selectedRecipe.description, GUILayout.Height(50));
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Recipe Custom Icon (Optional)", GUILayout.Width(170));
                selectedRecipe.icon = (Sprite)EditorGUILayout.ObjectField(selectedRecipe.icon, typeof(Sprite), false, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(15);
            
            // =========================================================================
            // ITEM DATABASE SECTION
            // =========================================================================
            DrawSectionHeader("Item Database Link");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                itemDatabase = (ItemDatabase)EditorGUILayout.ObjectField(itemDatabase, typeof(ItemDatabase), false);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshItemCache();
                }
                if (GUILayout.Button("Refresh Items", GUILayout.Width(100)))
                {
                    RefreshItemCache();
                }
                EditorGUILayout.EndHorizontal();
                
                if (itemDatabase == null)
                {
                    EditorGUILayout.HelpBox("Assign your Item Database here to populate the ingredient and result item selector dropdowns.", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(15);
            
            // =========================================================================
            // INGREDIENTS SECTION
            // =========================================================================
            DrawSectionHeader("Required Ingredients");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                DrawIngredientsEditor();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(15);
            
            // =========================================================================
            // RESULT SECTION
            // =========================================================================
            DrawSectionHeader("Crafted Result Output");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Crafted Item Output", EditorStyles.miniLabel);
                int currentResultIndex = GetItemIndex(selectedRecipe.resultItem);
                int newResultIndex = EditorGUILayout.Popup(currentResultIndex, itemNames);
                if (newResultIndex != currentResultIndex && newResultIndex < itemDataCache.Length)
                {
                    selectedRecipe.resultItem = itemDataCache[newResultIndex];
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Amount Produced", GUILayout.Width(120));
                selectedRecipe.resultAmount = EditorGUILayout.IntField(selectedRecipe.resultAmount, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(15);
            
            // =========================================================================
            // CRAFTING SETTINGS SECTION
            // =========================================================================
            DrawSectionHeader("Crafting Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Crafting Duration", GUILayout.Width(110));
                selectedRecipe.craftingTime = EditorGUILayout.FloatField(selectedRecipe.craftingTime, GUILayout.Width(60));
                EditorGUILayout.LabelField("seconds", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(database);
            }
        }

        private void DrawSectionHeader(string title)
        {
            GUIStyle sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(2, 2, 2, 5)
            };
            sectionHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
            EditorGUILayout.LabelField(title, sectionHeaderStyle);
        }

        private void DrawIngredientsEditor()
        {
            if (selectedRecipe.ingredients == null)
            {
                selectedRecipe.ingredients = new CraftingIngredient[0];
            }
            
            if (selectedRecipe.ingredients.Length == 0)
            {
                EditorGUILayout.LabelField("No crafting ingredients added yet.", EditorStyles.centeredGreyMiniLabel);
            }
            
            for (int i = 0; i < selectedRecipe.ingredients.Length; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                var ingredient = selectedRecipe.ingredients[i];
                if (ingredient == null)
                {
                    ingredient = new CraftingIngredient();
                    selectedRecipe.ingredients[i] = ingredient;
                }
                
                EditorGUILayout.LabelField($"{i + 1}.", EditorStyles.boldLabel, GUILayout.Width(22));
                
                int currentIndex = GetItemIndex(ingredient.item);
                int newIndex = EditorGUILayout.Popup(currentIndex, itemNames, GUILayout.MinWidth(180));
                if (newIndex != currentIndex && newIndex < itemDataCache.Length)
                {
                    ingredient.item = itemDataCache[newIndex];
                }
                
                EditorGUILayout.LabelField("Qty:", GUILayout.Width(25));
                ingredient.amount = EditorGUILayout.IntField(ingredient.amount, GUILayout.Width(40));
                
                GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f);
                if (GUILayout.Button("Delete", GUILayout.Width(50)))
                {
                    var list = selectedRecipe.ingredients.ToList();
                    list.RemoveAt(i);
                    selectedRecipe.ingredients = list.ToArray();
                    EditorUtility.SetDirty(database);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(5);
            
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.3f);
            if (GUILayout.Button("+ Add Ingredient", GUILayout.Height(25)))
            {
                var list = selectedRecipe.ingredients.ToList();
                list.Add(new CraftingIngredient { amount = 1 });
                selectedRecipe.ingredients = list.ToArray();
                EditorUtility.SetDirty(database);
            }
            GUI.backgroundColor = Color.white;
        }

        private void CreateNewRecipe()
        {
            Undo.RecordObject(database, "Create Recipe");

            var newRecipe = new CraftingRecipe
            {
                recipeId = database.GetNextAvailableID(),
                recipeName = "New Recipe",
                resultAmount = 1,
                craftingTime = 5f,
                ingredients = new CraftingIngredient[0]
            };

            database.AddRecipe(newRecipe);

            EditorUtility.SetDirty(database);
            RefreshSerializedObject();

            Debug.Log($"[RecipeDatabase] Created recipe '{newRecipe.recipeName}' (ID: {newRecipe.recipeId})");

            SelectRecipe(newRecipe, database.Count - 1);
        }

        private void DeleteSelectedRecipe()
        {
            if (selectedRecipe == null) return;

            if (!EditorUtility.DisplayDialog("Delete Recipe",
                $"Are you sure you want to delete '{selectedRecipe.recipeName}'?\nThis cannot be undone.",
                "Delete", "Cancel"))
                return;

            Undo.RecordObject(database, "Delete Recipe");

            database.RemoveRecipe(selectedRecipe);

            EditorUtility.SetDirty(database);
            RefreshSerializedObject();

            selectedRecipe = null;
            selectedIndex = -1;
        }

        private void DuplicateSelectedRecipe()
        {
            if (selectedRecipe == null) return;

            Undo.RecordObject(database, "Duplicate Recipe");

            var duplicate = new CraftingRecipe
            {
                recipeId = database.GetNextAvailableID(),
                recipeName = selectedRecipe.recipeName + " (Copy)",
                description = selectedRecipe.description,
                icon = selectedRecipe.icon,
                resultItem = selectedRecipe.resultItem,
                resultAmount = selectedRecipe.resultAmount,
                craftingTime = selectedRecipe.craftingTime
            };

            if (selectedRecipe.ingredients != null)
            {
                duplicate.ingredients = new CraftingIngredient[selectedRecipe.ingredients.Length];
                for (int i = 0; i < selectedRecipe.ingredients.Length; i++)
                {
                    var src = selectedRecipe.ingredients[i];
                    duplicate.ingredients[i] = new CraftingIngredient
                    {
                        item = src.item,
                        amount = src.amount
                    };
                }
            }

            database.AddRecipe(duplicate);

            EditorUtility.SetDirty(database);
            RefreshSerializedObject();

            SelectRecipe(duplicate, database.Count - 1);
        }

        private void DrawSpriteIcon(Rect position, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            Texture2D tex = sprite.texture;
            Rect spriteRect = sprite.textureRect;
            
            Rect texCoords = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = position.width / position.height;
            
            Rect drawRect = position;
            if (spriteAspect > rectAspect)
            {
                float newHeight = position.width / spriteAspect;
                drawRect.y += (position.height - newHeight) / 2f;
                drawRect.height = newHeight;
            }
            else
            {
                float newWidth = position.height * spriteAspect;
                drawRect.x += (position.width - newWidth) / 2f;
                drawRect.width = newWidth;
            }

            GUI.DrawTextureWithTexCoords(drawRect, tex, texCoords);
        }
    }
}
