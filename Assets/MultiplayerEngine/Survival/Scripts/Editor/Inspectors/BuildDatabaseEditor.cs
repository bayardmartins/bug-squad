using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Inspector Editor for BuildDatabase ScriptableObject.
    /// Prevents raw inspection/modification of pieces to protect sub-asset serialization and category structures.
    /// Provides detailed statistics, stability diagnostics, and a button to launch the dedicated editor window.
    /// </summary>
    [CustomEditor(typeof(BuildDatabase))]
    public class BuildDatabaseEditor : UnityEditor.Editor
    {
        private BuildDatabase database;

        private void OnEnable()
        {
            database = (BuildDatabase)target;
        }

        public override void OnInspectorGUI()
        {
            // 1. Initialize Styles & Background Theme
            MEEditorTheme.InitStyles();

            // Renders standard Multiplayer Engine Header styling inside the inspector
            MEEditorTheme.DrawHeader("Build Database", "Central Structural & Building Piece Sub-Asset Storage");

            GUILayout.Space(5);

            // 2. Purpose & Warning Card
            MEEditorTheme.BeginCard("System Overview");
            {
                EditorGUILayout.HelpBox(
                    "This ScriptableObject acts as a container database storing all structural building pieces (Floors, Walls, Roofs, Stairs, etc.) as embedded sub-assets.\n\n" +
                    "⚠ DIRECT RAW EDITING IN THE INSPECTOR IS DISABLED to prevent broken sub-asset serialization, desynchronized references, or stability key corruption.",
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
            MEEditorTheme.BeginCard("Building Pieces Statistics");
            {
                int totalCount = database.Count;
                int floorCount = 0;
                int wallCount = 0;
                int roofCount = 0;
                int stairCount = 0;
                int decorationCount = 0;
                int furnitureCount = 0;
                int foundationCount = 0;
                bool hasNulls = false;
                bool hasMissingPrefabs = false;
                bool hasDuplicateIDs = false;

                var idSet = new System.Collections.Generic.HashSet<int>();

                if (database.Pieces != null)
                {
                    for (int i = 0; i < database.Pieces.Count; i++)
                    {
                        var piece = database.Pieces[i];
                        if (piece == null)
                        {
                            hasNulls = true;
                            continue;
                        }

                        // Check for duplicate IDs
                        if (idSet.Contains(piece.pieceId))
                        {
                            hasDuplicateIDs = true;
                        }
                        else
                        {
                            idSet.Add(piece.pieceId);
                        }

                        // Check for missing prefabs
                        if (piece.buildPrefab == null)
                        {
                            hasMissingPrefabs = true;
                        }

                        // Count foundations
                        if (piece.isFoundation)
                        {
                            foundationCount++;
                        }

                        // Count by categories
                        switch (piece.category)
                        {
                            case BuildCategory.Floor:
                                floorCount++;
                                break;
                            case BuildCategory.Wall:
                                wallCount++;
                                break;
                            case BuildCategory.Roof:
                                roofCount++;
                                break;
                            case BuildCategory.Stair:
                                stairCount++;
                                break;
                            case BuildCategory.Decoration:
                                decorationCount++;
                                break;
                            case BuildCategory.Furniture:
                                furnitureCount++;
                                break;
                        }
                    }
                }

                // Render metrics
                DrawStatRow("Total Registered Pieces:", totalCount.ToString(), EditorStyles.boldLabel);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawStatRow("🧱 Foundations (Ground stable):", foundationCount.ToString(), EditorStyles.label);
                DrawStatRow("🟩 Floors / Foundations:", floorCount.ToString(), EditorStyles.label);
                DrawStatRow("🚪 Walls / Doorways:", wallCount.ToString(), EditorStyles.label);
                DrawStatRow("📐 Roofs / Slopes:", roofCount.ToString(), EditorStyles.label);
                DrawStatRow("🪜 Stairs / Pillars:", stairCount.ToString(), EditorStyles.label);
                DrawStatRow("🏺 Decorations:", decorationCount.ToString(), EditorStyles.label);
                DrawStatRow("🛋 Furniture / Utilities:", furnitureCount.ToString(), EditorStyles.label);

                // Diagnostics Check
                if (hasNulls || hasMissingPrefabs || hasDuplicateIDs)
                {
                    GUILayout.Space(10);
                    MEEditorTheme.DrawDivider();
                    GUILayout.Space(5);
                    GUILayout.Label("Diagnostics Status:", EditorStyles.boldLabel);

                    if (hasNulls)
                    {
                        EditorGUILayout.HelpBox("Warning: Database contains empty/null sub-asset entries! Run Cleanup via the database window.", MessageType.Warning);
                    }
                    if (hasMissingPrefabs)
                    {
                        EditorGUILayout.HelpBox("Warning: Some build pieces do not have an active Build Prefab configured!", MessageType.Warning);
                    }
                    if (hasDuplicateIDs)
                    {
                        EditorGUILayout.HelpBox("Critical Error: Duplicate piece ID keys detected! Fix immediately in the editor window.", MessageType.Error);
                    }
                }
            }
            MEEditorTheme.EndCard();

            // 4. Primary Editor Controls Card
            MEEditorTheme.BeginCard("Database Actions");
            {
                GUI.backgroundColor = MEEditorTheme.ColorAccent;
                if (GUILayout.Button(new GUIContent("🎯 Open Dedicated Build Database Window", EditorGUIUtility.IconContent("d_Prefab Icon").image), MEEditorTheme.StylePrimaryButton))
                {
                    BuildDatabaseWindow.Open();
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
