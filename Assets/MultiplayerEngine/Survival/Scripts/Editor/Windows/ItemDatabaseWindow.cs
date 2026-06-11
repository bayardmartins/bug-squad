using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Editor Window for managing ItemDatabase.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// Provides full CRUD operations for items using Sub-Asset pattern.
    /// </summary>
    public class ItemDatabaseWindow : MEEditorWindow
    {
        // Database reference
        private ItemDatabase database;
        private SerializedObject serializedDatabase;

        // UI State
        private Vector2 leftPanelScroll;
        private Vector2 rightPanelScroll;
        private int selectedIndex = -1;
        private InventoryItemData selectedItem;
        private UnityEditor.Editor selectedItemEditor;

        // Search and filter
        private string searchFilter = "";
        private ObjectType? typeFilter = null;
        private SearchField searchField;
        
        // Mini Button Styles
        private GUIStyle styleMiniButtonActive;
        private GUIStyle styleMiniButtonInactive;

        // Layout
        private const float LEFT_PANEL_WIDTH = 270f;
        private const float ITEM_HEIGHT = 44f;

        protected override bool UseGlobalScrollView => false;
        protected override string WindowSubtitle => "Item Sub-Asset Database & Inventory Manager";

        [MenuItem("Tools/Multiplayer Engine/Item Database", false, 21)]
        public static void Open()
        {
            var window = GetWindow<ItemDatabaseWindow>();
            window.titleContent = new GUIContent("Item Database", EditorGUIUtility.IconContent("d_PreMatCube").image);
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void OnEnable()
        {
            searchField = new SearchField();
            
            // Try to find existing database if none set
            if (database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                }
            }

            RefreshSerializedObject();
            titleContent = new GUIContent("Item Database", EditorGUIUtility.IconContent("d_PreMatCube").image);
        }

        private void OnDisable()
        {
            if (selectedItemEditor != null)
            {
                DestroyImmediate(selectedItemEditor);
            }
        }

        private void RefreshSerializedObject()
        {
            if (database != null)
            {
                serializedDatabase = new SerializedObject(database);
            }
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
                // Left Section - Item List Card (Constrained Width)
                EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH), GUILayout.ExpandHeight(true));
                {
                    MEEditorTheme.BeginCard("Items List");
                    DrawLeftPanel();
                    MEEditorTheme.EndCard();
                }
                EditorGUILayout.EndVertical();

                // Right Section - Item Details Card
                MEEditorTheme.BeginCard(selectedItem != null ? $"Item Details - {selectedItem.itemName}" : "Item Details");
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
                MEEditorTheme.BeginCard("No Item Database Selected");

                GUILayout.Label("Select or create an Item Database ScriptableObject to manage inventory item sub-assets.", MEEditorTheme.StyleHeaderSub);
                GUILayout.Space(15);

                // Database field
                EditorGUI.BeginChangeCheck();
                database = (ItemDatabase)EditorGUILayout.ObjectField("Database File", database, typeof(ItemDatabase), false);
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
                "Create Item Database",
                "ItemDatabase",
                "asset",
                "Choose a location to save the Item Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
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
                    database = (ItemDatabase)EditorGUILayout.ObjectField(database, typeof(ItemDatabase), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshSerializedObject();
                        selectedIndex = -1;
                        selectedItem = null;
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
                    
                    bool isResource = typeFilter == ObjectType.Resource;
                    if (GUILayout.Button("Res", isResource ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        typeFilter = isResource ? null : ObjectType.Resource;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // 4. Create/Delete/Import actions (custom modern buttons)
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = MEEditorTheme.ColorSuccess;
                    if (GUILayout.Button(new GUIContent(" Create", EditorGUIUtility.IconContent("d_CreateAddNew").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        CreateNewItem();
                    }
                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(4);

                    if (GUILayout.Button(new GUIContent(" Import", EditorGUIUtility.IconContent("d_Import").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        ImportExistingItem();
                    }

                    GUILayout.Space(4);

                    GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f);
                    GUI.enabled = selectedItem != null;
                    if (GUILayout.Button(new GUIContent(" Delete", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        DeleteSelectedItem();
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Item count label
                int totalCount = database.Count;
                int filteredCount = GetFilteredItems().Count;
                EditorGUILayout.LabelField($"Items: {filteredCount} / {totalCount}", EditorStyles.centeredGreyMiniLabel);

                // Item list scroll panel - vertical scrollbar only, horizontal disabled
                leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                {
                    DrawItemList();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private List<InventoryItemData> GetFilteredItems()
        {
            var result = new List<InventoryItemData>();
            if (database == null || database.Items == null) return result;
            
            for (int i = 0; i < database.Items.Count; i++)
            {
                var item = database.Items[i];
                if (item == null) continue;

                // Type filter
                if (typeFilter.HasValue && item.objectType != typeFilter.Value)
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    bool matchesName = item.itemName?.ToLower().Contains(searchFilter.ToLower()) ?? false;
                    bool matchesId = item.itemId.ToString().Contains(searchFilter);
                    if (!matchesName && !matchesId)
                        continue;
                }

                result.Add(item);
            }

            return result;
        }

        private void DrawItemList()
        {
            var filteredItems = GetFilteredItems();

            for (int i = 0; i < filteredItems.Count; i++)
            {
                var item = filteredItems[i];
                bool isSelected = item == selectedItem;

                Rect itemRect = EditorGUILayout.BeginHorizontal(isSelected ? MEEditorTheme.StyleListItemSelected : MEEditorTheme.StyleListItem, 
                    GUILayout.Height(ITEM_HEIGHT));
                {
                    // Icon
                    if (item.itemIcon != null)
                    {
                        Rect iconRect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
                        DrawSpriteIcon(iconRect, item.itemIcon);
                    }
                    else
                    {
                        GUILayout.Label(EditorGUIUtility.IconContent("d_PreMatCube").image, GUILayout.Width(32), GUILayout.Height(32));
                    }

                    GUILayout.Space(8);

                    // Name and ID
                    EditorGUILayout.BeginVertical();
                    {
                        string displayName = string.IsNullOrEmpty(item.itemName) ? "(No Name)" : item.itemName;
                        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"ID: {item.itemId} | {item.objectType}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                // Handle selection
                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    SelectItem(item);
                    Event.current.Use();
                }
            }
        }

        private void SelectItem(InventoryItemData item)
        {
            selectedItem = item;
            
            selectedIndex = -1;
            for (int i = 0; i < database.Items.Count; i++)
            {
                if (database.Items[i] == item)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // Create new editor for selected item
            if (selectedItemEditor != null)
            {
                DestroyImmediate(selectedItemEditor);
            }

            if (selectedItem != null)
            {
                selectedItemEditor = UnityEditor.Editor.CreateEditor(selectedItem);
            }

            Repaint();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            {
                if (selectedItem == null)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Select an item from the left panel to inspect and edit.", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    // Header Toolbar
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        GUIStyle selectedHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                        selectedHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
                        EditorGUILayout.LabelField($"Editing: {selectedItem.itemName}", selectedHeaderStyle);
                        
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        {
                            DuplicateSelectedItem();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Item inspector scroll body - vertical scrollbar only, horizontal disabled
                    rightPanelScroll = EditorGUILayout.BeginScrollView(rightPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                    {
                        EditorGUILayout.Space(10);
                        
                        if (selectedItemEditor != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            selectedItemEditor.OnInspectorGUI();
                            if (EditorGUI.EndChangeCheck())
                            {
                                EditorUtility.SetDirty(selectedItem);
                                EditorUtility.SetDirty(database);
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void CreateNewItem()
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Weapon"), false, () => CreateNewItemWithType(ObjectType.Weapon));
            menu.AddItem(new GUIContent("Tool"), false, () => CreateNewItemWithType(ObjectType.Tools));
            menu.AddItem(new GUIContent("Consumable"), false, () => CreateNewItemWithType(ObjectType.Consumable));
            menu.AddItem(new GUIContent("Resource"), false, () => CreateNewItemWithType(ObjectType.Resource));
            
            menu.ShowAsContext();
        }

        private void CreateNewItemWithType(ObjectType objectType)
        {
            Undo.RecordObject(database, "Create Item");

            var newItem = ScriptableObject.CreateInstance<InventoryItemData>();
            newItem.name = "New Item";
            newItem.itemName = "New Item";
            newItem.itemId = database.GetNextAvailableID();
            newItem.maxStack = 1;
            newItem.objectType = objectType;

            AssetDatabase.AddObjectToAsset(newItem, database);
            newItem.hideFlags = HideFlags.HideInHierarchy;

            CreateEmbeddedDataForItem(newItem, objectType);

            EditorUtility.SetDirty(newItem);
            database.AddItem(newItem);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));
            
            RefreshSerializedObject();
            SelectItem(newItem);
        }

        private void CreateEmbeddedDataForItem(InventoryItemData item, ObjectType objectType)
        {
            string itemName = item.itemName;

            if (objectType == ObjectType.Weapon || objectType == ObjectType.Tools || objectType == ObjectType.Consumable)
            {
                var handOffset = ScriptableObject.CreateInstance<HandOffsetData>();
                handOffset.name = $"{itemName}_HandOffsetData";
                AssetDatabase.AddObjectToAsset(handOffset, database);
                handOffset.hideFlags = HideFlags.HideInHierarchy;
                item.handOffsetData = handOffset;
            }

            switch (objectType)
            {
                case ObjectType.Weapon:
                    break;

                case ObjectType.Tools:
                    var toolData = ScriptableObject.CreateInstance<ToolData>();
                    toolData.name = $"{itemName}_ToolData";
                    AssetDatabase.AddObjectToAsset(toolData, database);
                    toolData.hideFlags = HideFlags.HideInHierarchy;
                    item.toolData = toolData;
                    break;

                case ObjectType.Consumable:
                    var consumableData = ScriptableObject.CreateInstance<ConsumableData>();
                    consumableData.name = $"{itemName}_ConsumableData";
                    AssetDatabase.AddObjectToAsset(consumableData, database);
                    consumableData.hideFlags = HideFlags.HideInHierarchy;
                    item.consumableData = consumableData;
                    break;

                case ObjectType.Resource:
                    break;
            }
        }

        private void ImportExistingItem()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Import Existing Item",
                "Assets",
                new string[] { "ScriptableObject", "asset" }
            );

            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected file must be inside the Assets folder.", "OK");
                return;
            }

            var existingItem = AssetDatabase.LoadAssetAtPath<InventoryItemData>(path);
            if (existingItem == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a valid InventoryItemData asset.", "OK");
                return;
            }

            if (database.GetItemByID(existingItem.itemId) != null)
            {
                if (!EditorUtility.DisplayDialog("Duplicate ID",
                    $"An item with ID {existingItem.itemId} already exists. Assign a new ID?",
                    "Assign New ID", "Cancel"))
                    return;

                existingItem.itemId = database.GetNextAvailableID();
            }

            Undo.RecordObject(database, "Import Item");

            var importedItem = ScriptableObject.Instantiate(existingItem);
            importedItem.name = existingItem.name;

            AssetDatabase.AddObjectToAsset(importedItem, database);
            importedItem.hideFlags = HideFlags.HideInHierarchy;

            if (existingItem.meleeWeaponData != null && !AssetDatabase.IsSubAsset(existingItem.meleeWeaponData))
            {
                var meleeCopy = ScriptableObject.Instantiate(existingItem.meleeWeaponData);
                meleeCopy.name = importedItem.itemName + "_MeleeWeaponData";
                AssetDatabase.AddObjectToAsset(meleeCopy, database);
                meleeCopy.hideFlags = HideFlags.HideInHierarchy;
                importedItem.meleeWeaponData = meleeCopy;
            }

            if (existingItem.shooterWeaponData != null && !AssetDatabase.IsSubAsset(existingItem.shooterWeaponData))
            {
                var shooterCopy = ScriptableObject.Instantiate(existingItem.shooterWeaponData);
                shooterCopy.name = importedItem.itemName + "_ShooterWeaponData";
                AssetDatabase.AddObjectToAsset(shooterCopy, database);
                shooterCopy.hideFlags = HideFlags.HideInHierarchy;
                importedItem.shooterWeaponData = shooterCopy;
            }

            if (existingItem.chargedWeaponData != null && !AssetDatabase.IsSubAsset(existingItem.chargedWeaponData))
            {
                var chargedCopy = ScriptableObject.Instantiate(existingItem.chargedWeaponData);
                chargedCopy.name = importedItem.itemName + "_ChargedWeaponData";
                AssetDatabase.AddObjectToAsset(chargedCopy, database);
                chargedCopy.hideFlags = HideFlags.HideInHierarchy;
                importedItem.chargedWeaponData = chargedCopy;
            }

            if (existingItem.toolData != null && !AssetDatabase.IsSubAsset(existingItem.toolData))
            {
                var toolCopy = ScriptableObject.Instantiate(existingItem.toolData);
                toolCopy.name = importedItem.itemName + "_ToolData";
                AssetDatabase.AddObjectToAsset(toolCopy, database);
                toolCopy.hideFlags = HideFlags.HideInHierarchy;
                importedItem.toolData = toolCopy;
            }

            if (existingItem.consumableData != null && !AssetDatabase.IsSubAsset(existingItem.consumableData))
            {
                var consumeCopy = ScriptableObject.Instantiate(existingItem.consumableData);
                consumeCopy.name = importedItem.itemName + "_ConsumableData";
                AssetDatabase.AddObjectToAsset(consumeCopy, database);
                consumeCopy.hideFlags = HideFlags.HideInHierarchy;
                importedItem.consumableData = consumeCopy;
            }

            database.AddItem(importedItem);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));

            SelectItem(importedItem);

            EditorUtility.DisplayDialog("Success", 
                $"Imported '{importedItem.itemName}' into the database.\n\n" +
                "Note: The original asset was NOT deleted. You can remove it manually if desired.",
                "OK");
        }

        private void DeleteSelectedItem()
        {
            if (selectedItem == null) return;

            if (!EditorUtility.DisplayDialog("Delete Item",
                $"Are you sure you want to delete '{selectedItem.itemName}'?\nThis cannot be undone.",
                "Delete", "Cancel"))
                return;

            Undo.RecordObject(database, "Delete Item");

            if (selectedItem.toolData != null && AssetDatabase.IsSubAsset(selectedItem.toolData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.toolData);
            if (selectedItem.consumableData != null && AssetDatabase.IsSubAsset(selectedItem.consumableData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.consumableData);
            if (selectedItem.meleeWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.meleeWeaponData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.meleeWeaponData);
            if (selectedItem.shooterWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.shooterWeaponData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.shooterWeaponData);
            if (selectedItem.chargedWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.chargedWeaponData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.chargedWeaponData);
            if (selectedItem.handOffsetData != null && AssetDatabase.IsSubAsset(selectedItem.handOffsetData))
                AssetDatabase.RemoveObjectFromAsset(selectedItem.handOffsetData);

            database.RemoveItem(selectedItem);
            AssetDatabase.RemoveObjectFromAsset(selectedItem);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));

            selectedItem = null;
            selectedIndex = -1;
            if (selectedItemEditor != null)
            {
                DestroyImmediate(selectedItemEditor);
                selectedItemEditor = null;
            }
        }

        private void DuplicateSelectedItem()
        {
            if (selectedItem == null) return;

            Undo.RecordObject(database, "Duplicate Item");

            var duplicate = ScriptableObject.Instantiate(selectedItem);
            duplicate.name = selectedItem.name + " (Copy)";
            duplicate.itemName = selectedItem.itemName + " (Copy)";
            duplicate.itemId = database.GetNextAvailableID();

            AssetDatabase.AddObjectToAsset(duplicate, database);
            duplicate.hideFlags = HideFlags.HideInHierarchy;

            if (selectedItem.meleeWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.meleeWeaponData))
            {
                var meleeDupe = ScriptableObject.Instantiate(selectedItem.meleeWeaponData);
                meleeDupe.name = duplicate.itemName + "_MeleeWeaponData";
                AssetDatabase.AddObjectToAsset(meleeDupe, database);
                meleeDupe.hideFlags = HideFlags.HideInHierarchy;
                duplicate.meleeWeaponData = meleeDupe;
            }

            if (selectedItem.shooterWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.shooterWeaponData))
            {
                var shooterDupe = ScriptableObject.Instantiate(selectedItem.shooterWeaponData);
                shooterDupe.name = duplicate.itemName + "_ShooterWeaponData";
                AssetDatabase.AddObjectToAsset(shooterDupe, database);
                shooterDupe.hideFlags = HideFlags.HideInHierarchy;
                duplicate.shooterWeaponData = shooterDupe;
            }

            if (selectedItem.chargedWeaponData != null && AssetDatabase.IsSubAsset(selectedItem.chargedWeaponData))
            {
                var chargedDupe = ScriptableObject.Instantiate(selectedItem.chargedWeaponData);
                chargedDupe.name = duplicate.itemName + "_ChargedWeaponData";
                AssetDatabase.AddObjectToAsset(chargedDupe, database);
                chargedDupe.hideFlags = HideFlags.HideInHierarchy;
                duplicate.chargedWeaponData = chargedDupe;
            }

            if (selectedItem.toolData != null && AssetDatabase.IsSubAsset(selectedItem.toolData))
            {
                var toolDupe = ScriptableObject.Instantiate(selectedItem.toolData);
                toolDupe.name = duplicate.itemName + "_ToolData";
                AssetDatabase.AddObjectToAsset(toolDupe, database);
                toolDupe.hideFlags = HideFlags.HideInHierarchy;
                duplicate.toolData = toolDupe;
            }

            if (selectedItem.consumableData != null && AssetDatabase.IsSubAsset(selectedItem.consumableData))
            {
                var consumeDupe = ScriptableObject.Instantiate(selectedItem.consumableData);
                consumeDupe.name = duplicate.itemName + "_ConsumableData";
                AssetDatabase.AddObjectToAsset(consumeDupe, database);
                consumeDupe.hideFlags = HideFlags.HideInHierarchy;
                duplicate.consumableData = consumeDupe;
            }

            database.AddItem(duplicate);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));

            SelectItem(duplicate);
        }

        private void RefreshSelectedItemEditor()
        {
            if (selectedItemEditor != null)
            {
                DestroyImmediate(selectedItemEditor);
            }
            if (selectedItem != null)
            {
                selectedItemEditor = UnityEditor.Editor.CreateEditor(selectedItem);
            }
            Repaint();
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
