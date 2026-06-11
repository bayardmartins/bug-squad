using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom property drawer for ResourceCost that shows a dropdown 
    /// of items from the ItemDatabase instead of requiring separate asset files.
    /// </summary>
    [CustomPropertyDrawer(typeof(ResourceCost))]
    public class ResourceCostDrawer : PropertyDrawer
    {
        // Cached database and items
        private static ItemDatabase cachedDatabase;
        private static string[] itemNames;
        private static InventoryItemData[] itemDataArray;
        private static bool cacheInitialized;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureCacheInitialized();

            EditorGUI.BeginProperty(position, label, property);

            var resourceProp = property.FindPropertyRelative("resource");
            var amountProp = property.FindPropertyRelative("amount");

            // Calculate rects
            float dropdownWidth = position.width * 0.65f;
            float amountWidth = position.width * 0.3f;
            float spacing = 5f;

            Rect dropdownRect = new Rect(position.x, position.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect amountRect = new Rect(position.x + dropdownWidth + spacing, position.y, amountWidth, EditorGUIUtility.singleLineHeight);

            if (cachedDatabase != null && itemNames != null && itemNames.Length > 0)
            {
                // Find current selection
                int currentIndex = 0;
                var currentResource = resourceProp.objectReferenceValue as InventoryItemData;
                if (currentResource != null)
                {
                    for (int i = 0; i < itemDataArray.Length; i++)
                    {
                        if (itemDataArray[i] == currentResource)
                        {
                            currentIndex = i + 1; // +1 because index 0 is "None"
                            break;
                        }
                    }
                }

                // Draw dropdown
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(dropdownRect, currentIndex, itemNames);
                if (EditorGUI.EndChangeCheck())
                {
                    if (newIndex == 0)
                        resourceProp.objectReferenceValue = null;
                    else
                        resourceProp.objectReferenceValue = itemDataArray[newIndex - 1];
                }

                // Draw amount field
                EditorGUI.PropertyField(amountRect, amountProp, GUIContent.none);
            }
            else
            {
                // Fallback to normal object field if no database found
                float halfWidth = position.width * 0.5f;
                Rect objRect = new Rect(position.x, position.y, halfWidth - spacing, EditorGUIUtility.singleLineHeight);
                Rect amtRect = new Rect(position.x + halfWidth, position.y, halfWidth, EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(objRect, resourceProp, GUIContent.none);
                EditorGUI.PropertyField(amtRect, amountProp, GUIContent.none);

                // Show help info
                Rect helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.HelpBox(helpRect, "Create an ItemDatabase for dropdown selection", MessageType.Info);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            EnsureCacheInitialized();
            if (cachedDatabase == null)
                return EditorGUIUtility.singleLineHeight * 2 + 4;
            return EditorGUIUtility.singleLineHeight;
        }

        private void EnsureCacheInitialized()
        {
            if (cacheInitialized && cachedDatabase != null) return;

            // Find ItemDatabase
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

                if (cachedDatabase != null && cachedDatabase.Items != null)
                {
                    // Build item list - only show Resource type items for costs
                    var resourceItems = new List<InventoryItemData>();
                    var names = new List<string>();
                    names.Add("(None)");

                    foreach (var item in cachedDatabase.Items)
                    {
                        if (item != null)
                        {
                            // Show all items but prefer resources first
                            resourceItems.Add(item);
                            string typeSuffix = item.objectType == ObjectType.Resource ? "" : $" [{item.objectType}]";
                            names.Add($"{item.itemName} (ID:{item.itemId}){typeSuffix}");
                        }
                    }

                    itemDataArray = resourceItems.ToArray();
                    itemNames = names.ToArray();
                }
            }

            cacheInitialized = true;
        }

        /// <summary>
        /// Call this to refresh the cache when ItemDatabase changes.
        /// </summary>
        public static void RefreshCache()
        {
            cacheInitialized = false;
            cachedDatabase = null;
            itemNames = null;
            itemDataArray = null;
        }
    }
}