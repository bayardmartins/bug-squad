using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for RockResource.
    /// Shows dropdown for selecting drop item from ItemDatabase.
    /// Extends LocalResourceEditor for auto-ID assignment.
    /// </summary>
    [CustomEditor(typeof(RockResource))]
    [CanEditMultipleObjects]
    public class RockResourceEditor : LocalResourceEditor
    {
        // Drop config properties
        private SerializedProperty dropConfigProp;
        private SerializedProperty requiredTierProp;
        private SerializedProperty maxHealthProp;

        // Hit effects
        private SerializedProperty hitEffectPrefabProp;
        private SerializedProperty hitSoundProp;

        // Destruction effects
        private SerializedProperty destroyEffectProp;
        private SerializedProperty destroySoundProp;

        // Database cache
        private static ItemDatabase cachedDatabase;
        private static string[] itemNames;
        private static InventoryItemData[] itemDataArray;

        protected override void OnEnable()
        {
            base.OnEnable(); // This handles auto-ID assignment

            dropConfigProp = serializedObject.FindProperty("dropConfig");
            requiredTierProp = serializedObject.FindProperty("requiredTier");
            maxHealthProp = serializedObject.FindProperty("maxHealth");

            hitEffectPrefabProp = serializedObject.FindProperty("hitEffectPrefab");
            hitSoundProp = serializedObject.FindProperty("hitSound");

            destroyEffectProp = serializedObject.FindProperty("destroyEffect");
            destroySoundProp = serializedObject.FindProperty("destroySound");

            RefreshDatabaseCache();
        }

        private void RefreshDatabaseCache()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

                if (cachedDatabase != null && cachedDatabase.Items != null)
                {
                    var items = cachedDatabase.Items.Where(i => i != null).ToList();
                    itemDataArray = items.ToArray();

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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Resource ID (from base class)
            var resourceIdProp = serializedObject.FindProperty("resourceId");
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(resourceIdProp, new GUIContent("Resource ID (Auto)"));
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Regenerate ID", GUILayout.Width(100)))
            {
                resourceIdProp.intValue = 0;
                serializedObject.ApplyModifiedProperties();
                // Trigger re-selection to regenerate
                Selection.activeObject = null;
                EditorApplication.delayCall += () => Selection.activeObject = target;
            }

            EditorGUILayout.Space(10);

            // Resource Settings
            EditorGUILayout.LabelField("Resource Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(requiredTierProp);
            EditorGUILayout.PropertyField(maxHealthProp);

            EditorGUILayout.Space(10);

            // Drops (from dropConfig - spawned by ResourceManager)
            EditorGUILayout.LabelField("Drops (Spawned by Server)", EditorStyles.boldLabel);
            DrawDropConfig();

            EditorGUILayout.Space(10);

            // Hit Effects
            EditorGUILayout.LabelField("Hit Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hitEffectPrefabProp);
            EditorGUILayout.PropertyField(hitSoundProp);

            EditorGUILayout.Space(10);

            // Destruction Effects
            EditorGUILayout.LabelField("Destruction Effects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(destroyEffectProp);
            EditorGUILayout.PropertyField(destroySoundProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDropConfig()
        {
            var dropItemProp = dropConfigProp.FindPropertyRelative("dropItem");
            var minDropsProp = dropConfigProp.FindPropertyRelative("minDrops");
            var maxDropsProp = dropConfigProp.FindPropertyRelative("maxDrops");

            // Item dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Drop Item", GUILayout.Width(EditorGUIUtility.labelWidth - 4));

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

            // Find current selection
            int currentIndex = 0;
            var currentItem = dropItemProp.objectReferenceValue as InventoryItemData;
            if (currentItem != null)
            {
                for (int i = 0; i < itemDataArray.Length; i++)
                {
                    if (itemDataArray[i] == currentItem)
                    {
                        currentIndex = i + 1;
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
                dropItemProp.objectReferenceValue = newIndex == 0 ? null : itemDataArray[newIndex - 1];
            }

            // Show item preview
            if (currentItem != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                if (currentItem.itemIcon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                    DrawSpriteIcon(iconRect, currentItem.itemIcon);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(currentItem.itemName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Type: {currentItem.objectType}");
                
                // Show warning if networkPrefab is missing
                if (currentItem.networkPrefab == null)
                {
                    EditorGUILayout.HelpBox("⚠ networkPrefab is NULL! Drops won't spawn.", MessageType.Warning);
                }
                
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            // Min/Max drops
            EditorGUILayout.PropertyField(minDropsProp);
            EditorGUILayout.PropertyField(maxDropsProp);
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

            GUI.DrawTextureWithTexCoords(position, tex, texCoords);
        }
    }
}