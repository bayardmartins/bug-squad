using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom Editor Window for managing BuildDatabase.
    /// Standardized to inherit from MEEditorWindow and styled via MEEditorTheme.
    /// Provides full CRUD operations for build pieces using Sub-Asset pattern.
    /// </summary>
    public class BuildDatabaseWindow : MEEditorWindow
    {
        // Database reference
        private BuildDatabase database;
        private SerializedObject serializedDatabase;

        // UI State
        private Vector2 leftPanelScroll;
        private Vector2 rightPanelScroll;
        private int selectedIndex = -1;
        private BuildPieceEntry selectedPiece;
        private UnityEditor.Editor selectedPieceEditor;

        // Search and filter
        private string searchFilter = "";
        private BuildCategory? categoryFilter = null;
        private SearchField searchField;
        
        // Mini Button Styles
        private GUIStyle styleMiniButtonActive;
        private GUIStyle styleMiniButtonInactive;

        // Layout
        private const float LEFT_PANEL_WIDTH = 270f;
        private const float ITEM_HEIGHT = 44f;

        protected override bool UseGlobalScrollView => false;
        protected override string WindowSubtitle => "Structure Pieces Sub-Asset & Saved Build Data Manager";

        [MenuItem("Tools/Multiplayer Engine/Build Database", false, 20)]
        public static void Open()
        {
            var window = GetWindow<BuildDatabaseWindow>();
            window.titleContent = new GUIContent("Build Database", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void OnEnable()
        {
            searchField = new SearchField();
            
            // Try to find existing database if none set
            if (database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:BuildDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    database = AssetDatabase.LoadAssetAtPath<BuildDatabase>(path);
                }
            }

            RefreshSerializedObject();
            titleContent = new GUIContent("Build Database", EditorGUIUtility.IconContent("d_Prefab Icon").image);
        }

        private void OnDisable()
        {
            if (selectedPieceEditor != null)
            {
                DestroyImmediate(selectedPieceEditor);
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
                // Left Section - Build Pieces List Card (Constrained Width)
                EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH), GUILayout.ExpandHeight(true));
                {
                    MEEditorTheme.BeginCard("Build Pieces List");
                    DrawLeftPanel();
                    MEEditorTheme.EndCard();
                }
                EditorGUILayout.EndVertical();

                // Right Section - Build Piece Details Card
                MEEditorTheme.BeginCard(selectedPiece != null ? $"Piece Details - {selectedPiece.pieceName}" : "Piece Details");
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
                MEEditorTheme.BeginCard("No Build Database Selected");

                GUILayout.Label("Select or create a Build Database ScriptableObject to manage building structure pieces.", MEEditorTheme.StyleHeaderSub);
                GUILayout.Space(15);

                // Database field
                EditorGUI.BeginChangeCheck();
                database = (BuildDatabase)EditorGUILayout.ObjectField("Database File", database, typeof(BuildDatabase), false);
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
                "Create Build Database",
                "BuildDatabase",
                "asset",
                "Choose a location to save the Build Database"
            );

            if (!string.IsNullOrEmpty(path))
            {
                database = ScriptableObject.CreateInstance<BuildDatabase>();
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
                    database = (BuildDatabase)EditorGUILayout.ObjectField(database, typeof(BuildDatabase), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshSerializedObject();
                        selectedIndex = -1;
                        selectedPiece = null;
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

                // 3. Category filter bar (custom modern buttons)
                EditorGUILayout.BeginHorizontal();
                {
                    bool isAll = categoryFilter == null;
                    if (GUILayout.Button("All", isAll ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        categoryFilter = null;

                    foreach (BuildCategory cat in System.Enum.GetValues(typeof(BuildCategory)))
                    {
                        bool isSelected = categoryFilter == cat;
                        string shortName = cat.ToString().Substring(0, System.Math.Min(4, cat.ToString().Length));
                        if (GUILayout.Button(shortName, isSelected ? styleMiniButtonActive : styleMiniButtonInactive, GUILayout.ExpandWidth(true)))
                        {
                            categoryFilter = isSelected ? null : (BuildCategory?)cat;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // 4. Actions (custom modern buttons)
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.backgroundColor = MEEditorTheme.ColorSuccess;
                    if (GUILayout.Button(new GUIContent(" Create", EditorGUIUtility.IconContent("d_CreateAddNew").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        CreateNewPiece();
                    }
                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(4);

                    GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f);
                    GUI.enabled = selectedPiece != null;
                    if (GUILayout.Button(new GUIContent(" Delete", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        DeleteSelectedPiece();
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(4);

                    GUI.backgroundColor = new Color(0.85f, 0.55f, 0.15f);
                    if (GUILayout.Button(new GUIContent(" Clear Save", EditorGUIUtility.IconContent("d_Refresh").image), GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    {
                        ClearCache();
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Piece count label
                int totalCount = database.Count;
                int filteredCount = GetFilteredPieces().Count;
                EditorGUILayout.LabelField($"Pieces: {filteredCount} / {totalCount}", EditorStyles.centeredGreyMiniLabel);

                // Piece list scroll panel - vertical scrollbar only, horizontal disabled
                leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                {
                    DrawPieceList();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private List<BuildPieceEntry> GetFilteredPieces()
        {
            var result = new List<BuildPieceEntry>();
            if (database == null || database.Pieces == null) return result;
            
            for (int i = 0; i < database.Pieces.Count; i++)
            {
                var piece = database.Pieces[i];
                if (piece == null) continue;

                // Category filter
                if (categoryFilter.HasValue && piece.category != categoryFilter.Value)
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    bool matchesName = piece.pieceName?.ToLower().Contains(searchFilter.ToLower()) ?? false;
                    bool matchesId = piece.pieceId.ToString().Contains(searchFilter);
                    if (!matchesName && !matchesId)
                        continue;
                }

                result.Add(piece);
            }

            return result;
        }

        private void DrawPieceList()
        {
            var filteredPieces = GetFilteredPieces();

            for (int i = 0; i < filteredPieces.Count; i++)
            {
                var piece = filteredPieces[i];
                bool isSelected = piece == selectedPiece;

                Rect itemRect = EditorGUILayout.BeginHorizontal(isSelected ? MEEditorTheme.StyleListItemSelected : MEEditorTheme.StyleListItem, 
                    GUILayout.Height(ITEM_HEIGHT));
                {
                    // Icon
                    if (piece.icon != null)
                    {
                        Rect iconRect = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36), GUILayout.Height(36));
                        DrawSpriteIcon(iconRect, piece.icon);
                    }
                    else
                    {
                        GUILayout.Label(EditorGUIUtility.IconContent("d_Prefab Icon").image, GUILayout.Width(36), GUILayout.Height(36));
                    }

                    GUILayout.Space(8);

                    // Name and info
                    EditorGUILayout.BeginVertical();
                    {
                        string displayName = string.IsNullOrEmpty(piece.pieceName) ? "(No Name)" : piece.pieceName;
                        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"ID: {piece.pieceId} | {piece.category}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                // Handle selection
                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    SelectPiece(piece);
                    Event.current.Use();
                }
            }
        }

        private void SelectPiece(BuildPieceEntry piece)
        {
            selectedPiece = piece;
            
            selectedIndex = -1;
            for (int i = 0; i < database.Pieces.Count; i++)
            {
                if (database.Pieces[i] == piece)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedPieceEditor != null)
            {
                DestroyImmediate(selectedPieceEditor);
            }

            if (selectedPiece != null)
            {
                selectedPieceEditor = UnityEditor.Editor.CreateEditor(selectedPiece);
            }

            Repaint();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            {
                if (selectedPiece == null)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Select a build structure piece from the left panel to inspect and edit.", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    // Header Toolbar
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        GUIStyle selectedHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                        selectedHeaderStyle.normal.textColor = MEEditorTheme.ColorTextNormal;
                        EditorGUILayout.LabelField($"Editing: {selectedPiece.pieceName}", selectedHeaderStyle);
                        
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        {
                            DuplicateSelectedPiece();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Piece inspector scroll body - vertical scrollbar only, horizontal disabled
                    rightPanelScroll = EditorGUILayout.BeginScrollView(rightPanelScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));
                    {
                        EditorGUILayout.Space(10);
                        
                        if (selectedPieceEditor != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            selectedPieceEditor.OnInspectorGUI();
                            if (EditorGUI.EndChangeCheck())
                            {
                                EditorUtility.SetDirty(selectedPiece);
                                EditorUtility.SetDirty(database);
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void CreateNewPiece()
        {
            GenericMenu menu = new GenericMenu();
            
            foreach (BuildCategory cat in System.Enum.GetValues(typeof(BuildCategory)))
            {
                BuildCategory capturedCat = cat;
                menu.AddItem(new GUIContent(cat.ToString()), false, () => CreateNewPieceWithCategory(capturedCat));
            }
            
            menu.ShowAsContext();
        }

        private void CreateNewPieceWithCategory(BuildCategory category)
        {
            Undo.RecordObject(database, "Create Build Piece");

            var newPiece = ScriptableObject.CreateInstance<BuildPieceEntry>();
            newPiece.name = "New " + category.ToString();
            newPiece.pieceName = "New " + category.ToString();
            newPiece.pieceId = database.GetNextAvailableID();
            newPiece.category = category;
            newPiece.requiredLevel = 1;

            AssetDatabase.AddObjectToAsset(newPiece, database);
            newPiece.hideFlags = HideFlags.HideInHierarchy;

            database.AddPiece(newPiece);

            EditorUtility.SetDirty(newPiece);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));
            
            RefreshSerializedObject();
            
            Debug.Log($"[BuildDatabase] Created {category} piece '{newPiece.pieceName}' (ID: {newPiece.pieceId})");

            SelectPiece(newPiece);
        }

        private void DeleteSelectedPiece()
        {
            if (selectedPiece == null) return;

            if (!EditorUtility.DisplayDialog("Delete Piece",
                $"Are you sure you want to delete '{selectedPiece.pieceName}'?\nThis cannot be undone.",
                "Delete", "Cancel"))
                return;

            Undo.RecordObject(database, "Delete Build Piece");

            database.RemovePiece(selectedPiece);
            AssetDatabase.RemoveObjectFromAsset(selectedPiece);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));

            selectedPiece = null;
            selectedIndex = -1;
            if (selectedPieceEditor != null)
            {
                DestroyImmediate(selectedPieceEditor);
                selectedPieceEditor = null;
            }
        }

        private void DuplicateSelectedPiece()
        {
            if (selectedPiece == null) return;

            Undo.RecordObject(database, "Duplicate Build Piece");

            var duplicate = ScriptableObject.Instantiate(selectedPiece);
            duplicate.name = selectedPiece.name + " (Copy)";
            duplicate.pieceName = selectedPiece.pieceName + " (Copy)";
            duplicate.pieceId = database.GetNextAvailableID();

            AssetDatabase.AddObjectToAsset(duplicate, database);
            duplicate.hideFlags = HideFlags.HideInHierarchy;

            database.AddPiece(duplicate);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(database));

            SelectPiece(duplicate);
        }

        private void ClearCache()
        {
            if (!EditorUtility.DisplayDialog("Clear In-Game Buildings",
                "This will:\n\u2022 Delete the saved building data file\n\u2022 Clear all in-game placed buildings (if playing)\n\nThis does NOT affect the database entries.\nContinue?",
                "Clear Buildings", "Cancel"))
                return;

            string saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, "BuildData");
            string savePath = System.IO.Path.Combine(saveDirectory, "world_buildings.json");

            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
                Debug.Log($"[BuildDatabase] Deleted save file: {savePath}");
            }
            else
            {
                Debug.Log($"[BuildDatabase] No save file found at: {savePath}");
            }

            if (Application.isPlaying)
            {
                var buildManager = UnityEngine.Object.FindFirstObjectByType<BuildManager>();
                if (buildManager != null)
                {
                    if (buildManager.IsServer)
                    {
                        var buildPiecesField = typeof(BuildManager).GetField("buildPieces", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (buildPiecesField != null)
                        {
                            var buildPieces = buildPiecesField.GetValue(buildManager);
                            if (buildPieces != null)
                            {
                                var clearMethod = buildPieces.GetType().GetMethod("Clear");
                                if (clearMethod != null)
                                {
                                    clearMethod.Invoke(buildPieces, null);
                                    Debug.Log("[BuildDatabase] Cleared runtime buildings from BuildManager.");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[BuildDatabase] Cannot clear runtime buildings - not the server.");
                    }
                }
                else
                {
                    Debug.Log("[BuildDatabase] BuildManager not found in scene.");
                }
            }

            Debug.Log("[BuildDatabase] In-game buildings cleared.");
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
