using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for Pickable that:
    /// 1. Inherits from the universal base class MEEditorInspector.
    /// 2. Hides InteractionType field (always Pickup).
    /// 3. Shows dropdown list of items from ItemDatabase.
    /// 4. Provides a premium styled layout.
    /// </summary>
    [CustomEditor(typeof(Pickable))]
    [CanEditMultipleObjects]
    public class PickableEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Collectable Inventory Item Spawner";

        // Serialized properties
        private SerializedProperty inventoryItemDataProp;
        private SerializedProperty amountProp;
        private SerializedProperty currentDurabilityProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;

        // Cached database reference
        private static ItemDatabase cachedDatabase;
        private static string[] itemNames;
        private static InventoryItemData[] itemDataArray;

        private void OnEnable()
        {
            inventoryItemDataProp = serializedObject.FindProperty("inventoryItemData");
            amountProp = serializedObject.FindProperty("amount");
            currentDurabilityProp = serializedObject.FindProperty("currentDurability");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");

            // Refresh database cache
            RefreshDatabaseCache();
        }

        private void RefreshDatabaseCache()
        {
            // Find ItemDatabase in project
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

                if (cachedDatabase != null && cachedDatabase.Items != null)
                {
                    var items = cachedDatabase.Items.Where(i => i != null).ToList();
                    itemDataArray = items.ToArray();
                    
                    // Build display names for dropdown
                    var names = new List<string> { "(None)" };
                    foreach (var item in items)
                    {
                        string displayName = !string.IsNullOrEmpty(item.itemName) 
                            ? $"{item.itemName} (ID: {item.itemId})" 
                            : $"Unnamed (ID: {item.itemId})";
                        names.Add(displayName);
                    }
                    itemNames = names.ToArray();
                }
            }
        }

        protected override void DrawInspectorBody()
        {
            // Info banner explaining component role
            DrawMessage("Pickable items are automatically collected into the player's inventory upon interaction.", MessageType.Info);
            GUILayout.Space(2);

            // ── Card 1: Inventory Item Mapping ──
            BeginCard("Item Association");
            {
                DrawItemDropdown();
            }
            EndCard();

            var currentItem = inventoryItemDataProp.objectReferenceValue as InventoryItemData;

            // ── Card 2: Stack & Durability Settings ──
            if (currentItem != null)
            {
                // For Tools and Weapons - show durability
                if (currentItem.objectType == ObjectType.Tools || currentItem.objectType == ObjectType.Weapon)
                {
                    BeginCard("Durability Configurations");
                    {
                        int maxDur = currentItem.maxDurability > 0 ? currentItem.maxDurability : 100;
                        int currentDur = currentDurabilityProp.intValue;
                        
                        // If -1, show "Use Max" indicator
                        if (currentDur < 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("Current Durability", GUILayout.Width(EditorGUIUtility.labelWidth - 4));
                            GUILayout.Label($"Max ({maxDur})", EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Set Custom Value", EditorStyles.miniButton, GUILayout.Width(120)))
                            {
                                currentDurabilityProp.intValue = maxDur;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            currentDurabilityProp.intValue = EditorGUILayout.IntSlider("Current Durability", currentDur, 0, maxDur);
                            if (GUILayout.Button("Use Max", EditorStyles.miniButton, GUILayout.Width(75)))
                            {
                                currentDurabilityProp.intValue = -1;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        
                        // Don't show amount for tools/weapons (always 1)
                        amountProp.intValue = 1;
                    }
                    EndCard();
                }
                // For Consumables and Resources - show stack settings
                else if (currentItem.objectType == ObjectType.Consumable || currentItem.objectType == ObjectType.Resource)
                {
                    BeginCard("Stack Configurations");
                    {
                        EditorGUILayout.PropertyField(amountProp, new GUIContent("Stack Amount"));
                        
                        // Clamp to max stack
                        if (amountProp.intValue > currentItem.maxStack)
                            amountProp.intValue = currentItem.maxStack;
                        if (amountProp.intValue < 1)
                            amountProp.intValue = 1;
                        
                        // Show info about drop behavior from item data
                        string dropMode = currentItem.dropAsStack ? "Drops as a single stack." : "Drops as individual physical items.";
                        DrawMessage($"{dropMode} (Configured in global Item Database)", MessageType.Info);
                    }
                    EndCard();
                }
                else
                {
                    // Default - just show amount
                    BeginCard("Stack Settings");
                    {
                        EditorGUILayout.PropertyField(amountProp);
                    }
                    EndCard();
                }
            }
            else
            {
                BeginCard("Stack Settings");
                {
                    EditorGUILayout.PropertyField(amountProp);
                }
                EndCard();
            }

            // ── Card 3: Display Overrides ──
            BeginCard("Prompt Display Overrides");
            {
                DrawMessage("These values override the default item name/description on HUD overlays. Leave blank to inherit from Item Database.", MessageType.None);
                GUILayout.Space(4);

                DrawProperty(displayNameProp, "Override Name", "Overrides the HUD name prompt.");
                DrawProperty(descriptionProp, "Override Description", "Overrides the HUD description prompt.");
            }
            EndCard();
        }

        private void DrawItemDropdown()
        {
            // Dropdown row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Item Database Target", "The inventory item referenced by this pickable."), GUILayout.Width(EditorGUIUtility.labelWidth - 4));

            if (cachedDatabase == null || itemDataArray == null || itemDataArray.Length == 0)
            {
                EditorGUILayout.LabelField("No ItemDatabase found!", EditorStyles.helpBox);
                if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                {
                    RefreshDatabaseCache();
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            // Find current selection index
            int currentIndex = 0;
            var currentItem = inventoryItemDataProp.objectReferenceValue as InventoryItemData;
            if (currentItem != null)
            {
                for (int i = 0; i < itemDataArray.Length; i++)
                {
                    if (itemDataArray[i] == currentItem)
                    {
                        currentIndex = i + 1; // +1 because of "(None)" option
                        break;
                    }
                }
            }

            // Draw dropdown
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(currentIndex, itemNames);
            
            if (GUILayout.Button("↻", GUILayout.Width(25)))
            {
                RefreshDatabaseCache();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == 0)
                {
                    inventoryItemDataProp.objectReferenceValue = null;
                }
                else
                {
                    inventoryItemDataProp.objectReferenceValue = itemDataArray[newIndex - 1];
                }
            }

            // Show item preview if selected
            if (currentItem != null)
            {
                GUILayout.Space(8);
                
                GUIStyle previewBoxStyle = new GUIStyle(GUI.skin.box);
                previewBoxStyle.normal.background = MEEditorTheme.GetTexture(new Color(0.14f, 0.15f, 0.18f));
                previewBoxStyle.padding = new RectOffset(12, 12, 8, 8);

                EditorGUILayout.BeginVertical(previewBoxStyle);
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    // Show icon
                    if (currentItem.itemIcon != null)
                    {
                        var iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                        DrawSpriteIcon(iconRect, currentItem.itemIcon);
                        GUILayout.Space(8);
                    }
                    
                    EditorGUILayout.BeginVertical();
                    {
                        GUIStyle boldLabel = new GUIStyle(EditorStyles.boldLabel);
                        boldLabel.normal.textColor = MEEditorTheme.ColorTextNormal;
                        EditorGUILayout.LabelField(currentItem.itemName, boldLabel);

                        GUIStyle infoLabel = new GUIStyle(EditorStyles.miniLabel);
                        infoLabel.normal.textColor = MEEditorTheme.ColorTextMuted;
                        EditorGUILayout.LabelField($"Type: {currentItem.objectType}   |   Max Stack: {currentItem.maxStack}", infoLabel);
                    }
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.EndHorizontal();
                    
                    if (!string.IsNullOrEmpty(currentItem.description))
                    {
                        GUILayout.Space(6);
                        MEEditorTheme.DrawDivider();
                        GUILayout.Space(4);

                        GUIStyle descLabel = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
                        descLabel.normal.textColor = MEEditorTheme.ColorTextMuted;
                        EditorGUILayout.LabelField(currentItem.description, descLabel);
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        // Helper to draw sprites perfectly inside GUI Rects
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