using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for ShooterWeaponData.
    /// Renders ammoItemId as a dropdown populated from ItemDatabase.
    /// </summary>
    [CustomEditor(typeof(ShooterWeaponData))]
    public class ShooterWeaponDataEditor : UnityEditor.Editor
    {
        private SerializedProperty ammoItemIdProp;
        private SerializedProperty magazineSizeProp;
        private SerializedProperty reloadTimeProp;
        private SerializedProperty alignArmToAimProp;
        private SerializedProperty alignHandToAimProp;
        private SerializedProperty fireRateProp;
        private SerializedProperty baseDamageProp;

        private void OnEnable()
        {
            ammoItemIdProp = serializedObject.FindProperty("ammoItemId");
            magazineSizeProp = serializedObject.FindProperty("magazineSize");
            reloadTimeProp = serializedObject.FindProperty("reloadTime");
            alignArmToAimProp = serializedObject.FindProperty("alignArmToAim");
            alignHandToAimProp = serializedObject.FindProperty("alignHandToAim");
            fireRateProp = serializedObject.FindProperty("fireRate");
            baseDamageProp = serializedObject.FindProperty("baseDamage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AmmoItemDropdownCache.DrawAmmoDropdown(ammoItemIdProp, "Ammo Item");
            EditorGUILayout.PropertyField(magazineSizeProp);
            EditorGUILayout.PropertyField(reloadTimeProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(alignArmToAimProp);
            EditorGUILayout.PropertyField(alignHandToAimProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(fireRateProp);
            EditorGUILayout.PropertyField(baseDamageProp);

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Custom editor for ChargedWeaponData.
    /// Renders ammoItemId as a dropdown populated from ItemDatabase.
    /// </summary>
    [CustomEditor(typeof(ChargedWeaponData))]
    public class ChargedWeaponDataEditor : UnityEditor.Editor
    {
        private SerializedProperty ammoItemIdProp;
        private SerializedProperty chargeTimeProp;
        private SerializedProperty drawDistanceProp;
        private SerializedProperty minDamageProp;
        private SerializedProperty maxDamageProp;
        private SerializedProperty minProjectileSpeedProp;
        private SerializedProperty maxProjectileSpeedProp;
        private SerializedProperty alignArmToAimProp;
        private SerializedProperty alignHandToAimProp;

        private void OnEnable()
        {
            ammoItemIdProp = serializedObject.FindProperty("ammoItemId");
            chargeTimeProp = serializedObject.FindProperty("chargeTime");
            drawDistanceProp = serializedObject.FindProperty("drawDistance");
            minDamageProp = serializedObject.FindProperty("minDamage");
            maxDamageProp = serializedObject.FindProperty("maxDamage");
            minProjectileSpeedProp = serializedObject.FindProperty("minProjectileSpeed");
            maxProjectileSpeedProp = serializedObject.FindProperty("maxProjectileSpeed");
            alignArmToAimProp = serializedObject.FindProperty("alignArmToAim");
            alignHandToAimProp = serializedObject.FindProperty("alignHandToAim");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AmmoItemDropdownCache.DrawAmmoDropdown(ammoItemIdProp, "Ammo Item");

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(chargeTimeProp);
            EditorGUILayout.PropertyField(drawDistanceProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(minDamageProp);
            EditorGUILayout.PropertyField(maxDamageProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(minProjectileSpeedProp);
            EditorGUILayout.PropertyField(maxProjectileSpeedProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(alignArmToAimProp);
            EditorGUILayout.PropertyField(alignHandToAimProp);

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Global static cache for the ammo item dropdown.
    /// Populated once and reused across all weapon data editors per domain reload.
    /// Only refreshed on explicit user request (refresh button).
    /// Safe for the CreateEditor/DestroyImmediate-per-frame pattern used by InventoryItemDataEditor.
    /// </summary>
    internal static class AmmoItemDropdownCache
    {
        private static bool isInitialized;
        private static ItemDatabase database;
        private static string[] displayNames;
        private static int[] itemIds;
        private static Dictionary<int, InventoryItemData> idToItemMap;

        /// <summary>
        /// Ensures the cache is populated. Only does work on first call after domain reload.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (isInitialized) return;
            Rebuild();
        }

        /// <summary>
        /// Forces a full rebuild of the cache from the ItemDatabase asset.
        /// </summary>
        private static void Rebuild()
        {
            isInitialized = true;
            idToItemMap = new Dictionary<int, InventoryItemData>();

            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length == 0)
            {
                database = null;
                displayNames = new string[] { "(No ItemDatabase found)" };
                itemIds = new int[] { -1 };
                return;
            }

            database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (database == null || database.Items == null || database.Items.Count == 0)
            {
                displayNames = new string[] { "(No items in database)" };
                itemIds = new int[] { -1 };
                return;
            }

            var names = new List<string>(database.Items.Count + 1) { "(None)" };
            var ids = new List<int>(database.Items.Count + 1) { -1 };

            for (int i = 0; i < database.Items.Count; i++)
            {
                var item = database.Items[i];
                if (item == null) continue;

                string label = !string.IsNullOrEmpty(item.itemName)
                    ? $"{item.itemName}  (ID: {item.itemId} | {item.objectType})"
                    : $"Unnamed  (ID: {item.itemId} | {item.objectType})";

                names.Add(label);
                ids.Add(item.itemId);
                idToItemMap[item.itemId] = item;
            }

            displayNames = names.ToArray();
            itemIds = ids.ToArray();
        }

        /// <summary>
        /// Draws the ammoItemId property as a Popup dropdown.
        /// Allocation-free on every frame — only lookups into cached arrays.
        /// </summary>
        public static void DrawAmmoDropdown(SerializedProperty ammoItemIdProp, string label)
        {
            EnsureInitialized();

            int currentId = ammoItemIdProp.intValue;

            // Find index in cached array
            int selectedIndex = 0;
            for (int i = 0; i < itemIds.Length; i++)
            {
                if (itemIds[i] == currentId) { selectedIndex = i; break; }
            }

            // Orphaned ID warning
            if (currentId >= 0 && selectedIndex == 0)
            {
                EditorGUILayout.HelpBox($"Ammo Item ID {currentId} not found in database.", MessageType.Warning);
            }

            // Dropdown row
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                ammoItemIdProp.intValue = itemIds[newIndex];
            }

            if (GUILayout.Button("R", EditorStyles.miniButtonRight, GUILayout.Width(22)))
            {
                Rebuild();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
