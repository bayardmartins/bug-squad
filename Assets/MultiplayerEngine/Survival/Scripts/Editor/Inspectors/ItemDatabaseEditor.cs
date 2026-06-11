using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Inspector Editor for ItemDatabase ScriptableObject.
    /// Prevents raw inspection/modification of items to secure sub-asset consistency and ID integrity.
    /// Provides detailed statistics, diagnostic warnings, and a button to launch the dedicated editor window.
    /// </summary>
    [CustomEditor(typeof(ItemDatabase))]
    public class ItemDatabaseEditor : UnityEditor.Editor
    {
        private ItemDatabase database;

        private void OnEnable()
        {
            database = (ItemDatabase)target;
        }

        public override void OnInspectorGUI()
        {
            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Renders standard Multiplayer Engine Header styling inside the inspector
            MEEditorTheme.DrawHeader("Item Database", "Central Inventory & Item Sub-Asset Storage");

            GUILayout.Space(5);

            // 2. Purpose & Warning Card
            MEEditorTheme.BeginCard("System Overview");
            {
                EditorGUILayout.HelpBox(
                    "This ScriptableObject acts as a container database that stores all gameplay items (Weapons, Tools, Consumables, and Resources) as embedded sub-assets.\n\n" +
                    "⚠ DIRECT RAW EDITING IN THE INSPECTOR IS DISABLED to prevent broken sub-asset serialization, desynchronized references, or duplicate ID keys.",
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
            MEEditorTheme.BeginCard("Database Statistics");
            {
                int totalCount = database.Count;
                int weaponCount = 0;
                int toolCount = 0;
                int consumableCount = 0;
                int resourceCount = 0;
                bool hasNulls = false;
                bool hasDuplicateIDs = false;

                var idSet = new System.Collections.Generic.HashSet<int>();

                if (database.Items != null)
                {
                    for (int i = 0; i < database.Items.Count; i++)
                    {
                        var item = database.Items[i];
                        if (item == null)
                        {
                            hasNulls = true;
                            continue;
                        }

                        // Check for duplicate IDs
                        if (idSet.Contains(item.itemId))
                        {
                            hasDuplicateIDs = true;
                        }
                        else
                        {
                            idSet.Add(item.itemId);
                        }

                        // Count by types
                        switch (item.objectType)
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
                        }
                    }
                }

                // Render metrics
                DrawStatRow("Total Registered Items:", totalCount.ToString(), EditorStyles.boldLabel);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawStatRow("⚔ Weapons:", weaponCount.ToString(), EditorStyles.label);
                DrawStatRow("🛠 Tools / Melee:", toolCount.ToString(), EditorStyles.label);
                DrawStatRow("🍎 Consumables:", consumableCount.ToString(), EditorStyles.label);
                DrawStatRow("🪵 Resources / Craftables:", resourceCount.ToString(), EditorStyles.label);

                // Diagnostics Check
                if (hasNulls || hasDuplicateIDs)
                {
                    GUILayout.Space(10);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(5);
                    GUILayout.Label("Diagnostics Status:", EditorStyles.boldLabel);
                    
                    if (hasNulls)
                    {
                        EditorGUILayout.HelpBox("Warning: Database contains empty/null items! Run Cleanup Null References via the database window.", MessageType.Warning);
                    }
                    if (hasDuplicateIDs)
                    {
                        EditorGUILayout.HelpBox("Critical Error: Duplicate Item IDs detected! Please correct them immediately in the database editor window.", MessageType.Error);
                    }
                }
            }
            MEEditorTheme.EndCard();

            // 4. Primary Editor Controls Card
            MEEditorTheme.BeginCard("Database Actions");
            {
                GUI.backgroundColor = MEEditorTheme.ColorAccent;
                if (GUILayout.Button(new GUIContent("🎯 Open Dedicated Item Database Window", EditorGUIUtility.IconContent("d_PreMatCube").image), MEEditorTheme.StylePrimaryButton))
                {
                    ItemDatabaseWindow.Open();
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
