using UnityEngine;
using UnityEditor;
using System.IO;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Custom editor for SaveGameManager to provide a premium, consistent visual inspector.
    /// Inherits from the universal base class MEEditorInspector.
    /// Includes high-contrast encryption warnings, playmode debug controls, 
    /// and a comprehensive administrative Save File Explorer (Edit & Play Mode).
    /// </summary>
    [CustomEditor(typeof(SaveGameManager))]
    public class SaveGameManagerEditor : MEEditorInspector
    {
        protected override string InspectorSubtitle => "Game Saving & Loading System";

        // Serialized properties
        private SerializedProperty saveLocationProp;
        private SerializedProperty autoSaveIntervalProp;
        private SerializedProperty enableEncryptionProp;
        private SerializedProperty encryptionKeyProp;
        private SerializedProperty useTestGameIdProp;
        private SerializedProperty testGameIdProp;

        // Foldouts for individual save game sessions
        private System.Collections.Generic.Dictionary<string, bool> sessionFoldouts = 
            new System.Collections.Generic.Dictionary<string, bool>();

        private void OnEnable()
        {
            saveLocationProp = serializedObject.FindProperty("saveLocation");
            autoSaveIntervalProp = serializedObject.FindProperty("autoSaveInterval");
            enableEncryptionProp = serializedObject.FindProperty("enableEncryption");
            encryptionKeyProp = serializedObject.FindProperty("encryptionKey");
            useTestGameIdProp = serializedObject.FindProperty("useTestGameId");
            testGameIdProp = serializedObject.FindProperty("testGameId");
        }

        protected override void DrawInspectorBody()
        {
            // ── Card 1: Core Save Settings ──
            BeginCard("Storage & Auto-Save Settings");
            {
                DrawProperty(saveLocationProp, "Save Location", "Where saves are stored. Local keeps them strictly on-disk, Cloud syncs to Steam or Unity services.");

                // Helper description depending on selection
                int locationVal = saveLocationProp.enumValueIndex;
                GUIStyle pathLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                pathLabelStyle.normal.textColor = MEEditorTheme.ColorTextMuted;

                if (locationVal == 0) // Local
                {
                    string localPath = Path.Combine(Application.persistentDataPath, "Saves");
                    GUILayout.Label($"Local Storage Path: {localPath}", pathLabelStyle);
                }
                else // Cloud
                {
                    GUILayout.Label("Saves are stored locally and synchronized to remote Steam Cloud or Unity Cloud Storage.", pathLabelStyle);
                }

                GUILayout.Space(6);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                DrawProperty(autoSaveIntervalProp, "Auto-Save Interval", "Frequency of periodic save game cycles during active play.");
            }
            EndCard();

            // ── Card 2: Security & Encryption ──
            BeginCard("Security & Encryption");
            {
                DrawProperty(enableEncryptionProp, "Enable Encryption", "If true, encrypts all game saves locally and on the cloud using AES-256.");

                if (enableEncryptionProp.boolValue)
                {
                    DrawProperty(encryptionKeyProp, "Encryption Key", "16, 24, or 32 character key used for AES encoding. Auto-pads up to 32 bytes.");

                    string keyStr = encryptionKeyProp.stringValue;
                    if (string.IsNullOrEmpty(keyStr))
                    {
                        DrawMessage("Encryption key cannot be empty! Save operations will fail.", MessageType.Error);
                    }
                    else if (keyStr != "multiplayer_engine_s3cr3t_key_!!")
                    {
                        DrawMessage("Warning: Altering the encryption key makes existing save files unreadable (they will fail to decrypt and fall back to default empty states).", MessageType.Warning);
                    }
                }
                else
                {
                    DrawMessage("Encryption is disabled. Saves will be stored in plain-text JSON format.", MessageType.Info);
                }
            }
            EndCard();

            // ── Card 3: Standalone Testing & Debugging ──
            BeginCard("Standalone Scene Testing");
            {
                DrawProperty(useTestGameIdProp, "Use Test Game ID", "Enables setting a mock Game ID when testing individual scenes directly in Unity Play Mode.");

                if (useTestGameIdProp.boolValue)
                {
                    DrawProperty(testGameIdProp, "Test Game ID", "Mock Game ID folder name used to store scene test saves.");
                    if (string.IsNullOrEmpty(testGameIdProp.stringValue))
                    {
                        DrawMessage("Test Game ID cannot be empty!", MessageType.Warning);
                    }
                }
                else
                {
                    GUIStyle noteStyle = new GUIStyle(EditorStyles.miniLabel);
                    noteStyle.normal.textColor = MEEditorTheme.ColorTextMuted;
                    noteStyle.wordWrap = true;
                    GUILayout.Label("Note: Standalone scene test saves will be disabled. ActiveGameId must be set dynamically by lobby sessions.", noteStyle);
                }
            }
            EndCard();

            // ── Card 4: Administrative Save File Explorer (Edit & Play Mode) ──
            DrawSaveFileExplorer();

            // ── Card 5: Play Mode Live State Debugger ──
            if (EditorApplication.isPlaying)
            {
                BeginCard("Live Save Debugger");
                {
                    if (SaveGameManager.Instance != null)
                    {
                        string gameId = SaveGameManager.Instance.ActiveGameId;
                        bool hasId = !string.IsNullOrEmpty(gameId);

                        // Session details
                        GUILayout.Label($"<b>Active Game ID</b>: {(hasId ? $"<color=#5CACEE>{gameId}</color>" : "<i>None (Unsaved/Standalone)</i>")}", new GUIStyle(EditorStyles.label) { richText = true });
                        GUILayout.Label($"<b>Dedicated Server</b>: {SaveGameManager.Instance.IsDedicatedServer}");
                        GUILayout.Label($"<b>Connected Client</b>: {SaveGameManager.Instance.IsClientConnectingToDedicated}");

                        GUILayout.Space(10);

                        // Trigger manual save broadcast
                        if (GUILayout.Button("Trigger Auto-Save", MEEditorTheme.StylePrimaryButton))
                        {
                            SaveGameManager.Instance.TriggerAutoSave();
                            Debug.Log("[SaveGameManagerEditor] Triggered manual auto-save broadcast.");
                        }
                    }
                    else
                    {
                        DrawMessage("SaveGameManager instance is inactive or disabled.", MessageType.Warning);
                    }
                }
                EndCard();
            }
        }

        /// <summary>
        /// Draws a comprehensive file system explorer for active save data.
        /// </summary>
        private void DrawSaveFileExplorer()
        {
            BeginCard("Save File Explorer & Settings");
            {
                string savesDir = Path.Combine(Application.persistentDataPath, "Saves");

                if (!Directory.Exists(savesDir))
                {
                    GUILayout.Label("<i>No save directory found on disk (no save files created yet).</i>", new GUIStyle(EditorStyles.label) { richText = true });
                    EndCard();
                    return;
                }

                GUIStyle boldHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
                boldHeader.normal.textColor = MEEditorTheme.ColorTextNormal;

                GUIStyle itemLabelStyle = new GUIStyle(EditorStyles.label);
                itemLabelStyle.normal.textColor = MEEditorTheme.ColorTextNormal;

                GUIStyle mutedLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                mutedLabelStyle.normal.textColor = MEEditorTheme.ColorTextMuted;

                GUIStyle sessionHeaderStyle = new GUIStyle(GUI.skin.box);
                sessionHeaderStyle.normal.background = MEEditorTheme.GetTexture(new Color(0.18f, 0.20f, 0.24f));
                sessionHeaderStyle.padding = new RectOffset(8, 8, 6, 6);

                GUIStyle fileRowStyle = new GUIStyle(GUI.skin.box);
                fileRowStyle.normal.background = MEEditorTheme.GetTexture(new Color(0.14f, 0.15f, 0.18f));
                fileRowStyle.padding = new RectOffset(6, 6, 4, 4);

                // ── 1. The Global Index File ──
                string indexPath = Path.Combine(savesDir, "SavedGames.json");
                if (File.Exists(indexPath))
                {
                    GUILayout.Label("Global Save Index File", boldHeader);
                    
                    GUILayout.BeginHorizontal(sessionHeaderStyle);
                    {
                        long indexSize = new FileInfo(indexPath).Length;
                        string sizeStr = FormatFileSize(indexSize);
                        
                        string cloudStatus = GetCloudSyncStatus("SavedGames");

                        GUILayout.Label($"📄 <b>SavedGames.json</b> ({sizeStr}) - <color=#87CEFA>{cloudStatus}</color>", new GUIStyle(itemLabelStyle) { richText = true });
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Delete Index", EditorStyles.miniButton, GUILayout.Width(90)))
                        {
                            if (EditorUtility.DisplayDialog("Delete Index File",
                                "Are you sure you want to delete the SavedGames.json index file? This clears the list of game sessions in UI menus but keeps individual game save directories intact.",
                                "Yes, Delete", "Cancel"))
                            {
                                File.Delete(indexPath);
                                DeleteCloudFile("SavedGames");
                                Debug.Log("[SaveGameManagerEditor] Deleted global index file SavedGames.json");
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }

                // ── 2. Individual Game Session Subdirectories ──
                GUILayout.Label("Active Game Sessions", boldHeader);

                string[] subDirs = Directory.GetDirectories(savesDir);
                if (subDirs.Length > 0)
                {
                    foreach (string dirPath in subDirs)
                    {
                        string gameId = Path.GetFileName(dirPath);
                        long dirSize = GetDirectorySize(dirPath);
                        string dirSizeStr = FormatFileSize(dirSize);

                        // Track foldout state
                        if (!sessionFoldouts.ContainsKey(gameId))
                            sessionFoldouts[gameId] = false;

                        GUILayout.BeginVertical(sessionHeaderStyle);
                        {
                            GUILayout.BeginHorizontal();
                            {
                                sessionFoldouts[gameId] = EditorGUILayout.Foldout(sessionFoldouts[gameId], 
                                    $"📁 <b>Session: {gameId}</b> ({dirSizeStr})", true, 
                                    new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, richText = true });
                                
                                GUILayout.FlexibleSpace();

                                GUI.backgroundColor = MEEditorTheme.ColorWarning;
                                if (GUILayout.Button("Delete Session", EditorStyles.miniButton, GUILayout.Width(100)))
                                {
                                    if (EditorUtility.DisplayDialog("Wipe Game Session",
                                        $"Are you sure you want to wipe game session directory '{gameId}'?\n\nThis permanently deletes all builds, player inventories, and local configs for this world.",
                                        "Delete Session", "Cancel"))
                                    {
                                        Directory.Delete(dirPath, true);
                                        // Wipe all associated cloud keys too
                                        WipeCloudDirectory(gameId);
                                        Debug.Log($"[SaveGameManagerEditor] Successfully wiped local save session: {gameId}");
                                        GUIUtility.ExitGUI();
                                    }
                                }
                                GUI.backgroundColor = Color.white;
                            }
                            GUILayout.EndHorizontal();

                            // Render individual save files under this session folder
                            if (sessionFoldouts[gameId])
                            {
                                GUILayout.Space(6);
                                string[] files = Directory.GetFiles(dirPath, "*.json");
                                if (files.Length > 0)
                                {
                                    foreach (string filePath in files)
                                    {
                                        string filename = Path.GetFileName(filePath);
                                        long fileSize = new FileInfo(filePath).Length;
                                        string fileSizeStr = FormatFileSize(fileSize);

                                        string relativeKey = $"{gameId}/{Path.GetFileNameWithoutExtension(filePath)}";
                                        string fileCloud = GetCloudSyncStatus(relativeKey);

                                        GUILayout.BeginHorizontal(fileRowStyle);
                                        {
                                            GUILayout.Label($"   📄 <b>{filename}</b> ({fileSizeStr}) - <color=#87CEFA>{fileCloud}</color>", new GUIStyle(itemLabelStyle) { richText = true });
                                            GUILayout.FlexibleSpace();

                                            if (GUILayout.Button("Wipe File", EditorStyles.miniButton, GUILayout.Width(75)))
                                            {
                                                if (EditorUtility.DisplayDialog("Delete Save File",
                                                    $"Are you sure you want to delete file '{filename}' inside session '{gameId}'?",
                                                    "Delete", "Cancel"))
                                                {
                                                    File.Delete(filePath);
                                                    DeleteCloudFile(relativeKey);
                                                    Debug.Log($"[SaveGameManagerEditor] Deleted save file {filename}");
                                                    GUIUtility.ExitGUI();
                                                }
                                            }
                                        }
                                        GUILayout.EndHorizontal();
                                    }
                                }
                                else
                                {
                                    GUILayout.Label("   <i>No files found inside session.</i>", new GUIStyle(EditorStyles.label) { richText = true });
                                }
                            }
                        }
                        GUILayout.EndVertical();
                        GUILayout.Space(4);
                    }
                }
                else
                {
                    GUILayout.Label("<i>No active game session directories found.</i>", mutedLabelStyle);
                }

                GUILayout.Space(10);
                MEEditorTheme.DrawDivider();
                GUILayout.Space(6);

                // Global Wipe All Saves Shortcut
                GUI.backgroundColor = MEEditorTheme.ColorWarning;
                if (GUILayout.Button("Wipe Entire Saves Folder", MEEditorTheme.StyleDynamicButton, GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Wipe Entire Save Directory",
                        "This will delete ALL local saves, directories, and session index tables from disk.\n\nThis cannot be undone. Are you sure?",
                        "Wipe Disk Directory", "Cancel"))
                    {
                        try
                        {
                            Directory.Delete(savesDir, true);
                            Debug.Log("[SaveGameManagerEditor] ✓ Wiped entire Saves directory from disk.");
                            GUIUtility.ExitGUI();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[SaveGameManagerEditor] Failed to wipe Saves folder: {ex.Message}");
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            EndCard();
        }

        // Helper to format file size in human-readable notation
        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024f * 1024f):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        // Helper to get total folder directory size
        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long size = 0;
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                size += new FileInfo(file).Length;
            }
            return size;
        }

        // Helper to resolve Steam Cloud synchronization status
        private string GetCloudSyncStatus(string key)
        {
#if STEAM_SERVICES
            if (Steamworks.SteamRemoteStorage.FileExists(key))
                return "Steam Cloud Synced";
#endif
            // Fallback when cloud services are inactive/unloaded
            return saveLocationProp.enumValueIndex == 1 ? "Cloud Eligible" : "Local Save";
        }

        // Helper to delete steam cloud file representation cleanly
        private void DeleteCloudFile(string key)
        {
#if STEAM_SERVICES
            if (Steamworks.SteamRemoteStorage.FileExists(key))
            {
                Steamworks.SteamRemoteStorage.FileDelete(key);
                Debug.Log($"[SaveGameManagerEditor] Deleted Steam Cloud file: {key}");
            }
#endif
        }

        // Helper to recursively wipe session files from Steam Cloud representation
        private void WipeCloudDirectory(string gameId)
        {
#if STEAM_SERVICES
            // SteamRemoteStorage doesn't support directory deletions directly, 
            // so we wipe common known files or list matches
            DeleteCloudFile($"{gameId}/builds");
            DeleteCloudFile($"{gameId}/players");
            DeleteCloudFile($"{gameId}/inventory");
            DeleteCloudFile($"{gameId}/quests");
            DeleteCloudFile($"{gameId}/world");
#endif
        }
    }
}
