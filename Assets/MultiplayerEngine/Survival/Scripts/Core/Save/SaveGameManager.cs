using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_SERVICES
using Unity.Services.CloudSave;
#endif

#if STEAM_SERVICES
using Steamworks;
#endif

namespace Ignitives.MultiplayerEngine
{

    /// <summary>
    /// Represents the local player's role in a saved game.
    /// Used to distinguish between games you created (Owner) and games you joined (Participant).
    /// </summary>
    public enum SaveRole
    {
        /// <summary>I created/hosted this game world.</summary>
        Owner,
        /// <summary>I joined this game as a client.</summary>
        Participant
    }

    /// <summary>
    /// Manages game saving and loading, supporting local storage and cloud synchronization 
    /// (Steam Cloud or Unity Cloud Save).
    /// </summary>
    public class SaveGameManager : MonoBehaviour
    {
        public static SaveGameManager Instance { get; private set; }

        public bool IsDedicatedServer { get; set; } = false;
        public bool IsClientConnectingToDedicated { get; set; } = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private string Encrypt(string plainText)
        {
            if (!enableEncryption || string.IsNullOrEmpty(plainText)) return plainText;
            try
            {
                byte[] iv = new byte[16]; // Padding default IV of zeros for simplicity or could generate one and prepend it.
                byte[] array;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
                    aes.IV = iv;
                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                            {
                                streamWriter.Write(plainText);
                            }
                            array = memoryStream.ToArray();
                        }
                    }
                }
                return Convert.ToBase64String(array);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Encryption failed: {e.Message}");
                return plainText;
            }
        }

        private string Decrypt(string cipherText)
        {
            if (!enableEncryption || string.IsNullOrEmpty(cipherText)) return cipherText;

            // Simple check to bypass obvious unencrypted JSON strings safely
            if (cipherText.TrimStart().StartsWith("{") || cipherText.TrimStart().StartsWith("[")) return cipherText;

            try
            {
                byte[] iv = new byte[16];
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
                    aes.IV = iv;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream memoryStream = new MemoryStream(buffer))
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If it fails (e.g., base64 invalid or wrong key), just return original assuming it's unencrypted text
                return cipherText;
            }
        }

        #region Public API

        public enum SaveLocation
        {
            Local,
            Cloud
        }

        public enum AutoSaveInterval
        {
            Off = 0,
            FiveMinutes = 5,
            TenMinutes = 10,
            FifteenMinutes = 15
        }

        [Header("Settings")]
        [SerializeField] private SaveLocation saveLocation = SaveLocation.Cloud;
        [SerializeField] private AutoSaveInterval autoSaveInterval = AutoSaveInterval.Off;

        [Header("Security")]
        [Tooltip("Encrypts save files locally and in the cloud.")]
        [SerializeField] private bool enableEncryption = true;
        [Tooltip("The secret key used for encryption (must be exactly 16, 24, or 32 bytes for valid AES pad check, we auto-pad up to 32 usually).")]
        [SerializeField] private string encryptionKey = "multiplayer_engine_s3cr3t_key_!!";


        [Header("Debug / Testing")]
        [Tooltip("When enabled, auto-sets ActiveGameId to the test value below if no lobby session set it. Allows testing directly from the Game scene.")]
        [SerializeField] private bool useTestGameId = false;
        [SerializeField] private string testGameId = "debug_test_session";

        /// <summary>
        /// The SaveGameId of the currently active game session.
        /// Set by SessionManager when a session is initialized.
        /// When null/empty, per-game saves are disabled (fresh/unsaved game).
        /// </summary>
        public string ActiveGameId { get; set; }

        /// <summary>
        /// Call this from any system (e.g., SpawnManager, SessionManager) to ensure
        /// SaveGameManager exists. If it doesn't, one is auto-created for testing.
        /// </summary>
        public static void EnsureInstance()
        {
            if (Instance != null) return;

            Debug.LogWarning("[SaveGameManager] No instance found — creating a runtime test instance.");
            var go = new GameObject("[SaveGameManager] (Auto-Created)");
            var mgr = go.AddComponent<SaveGameManager>();
            mgr.useTestGameId = true;
            mgr.testGameId = "debug_test_session";
            // Awake() handles singleton + DontDestroyOnLoad
        }

        /// <summary>
        /// Activates the test GameId if no ActiveGameId has been set (i.e., no lobby flow).
        /// Call this when the game scene starts, or it runs automatically on first access.
        /// </summary>
        public void ActivateTestIdIfNeeded()
        {
            if (useTestGameId && string.IsNullOrEmpty(ActiveGameId))
            {
                ActiveGameId = testGameId;
                Debug.Log($"[SaveGameManager] TEST MODE: ActiveGameId set to '{testGameId}'");
            }
        }


        /// <summary>
        /// Event raised when auto-save is triggered.
        /// </summary>
        public static event Action OnAutoSave;

        private float autoSaveTimer;

        private void Update()
        {
            if (autoSaveInterval != AutoSaveInterval.Off)
            {
                autoSaveTimer += Time.deltaTime;
                float intervalSeconds = (int)autoSaveInterval * 60f;

                if (autoSaveTimer >= intervalSeconds)
                {
                    autoSaveTimer = 0f;
                    TriggerAutoSave();
                }
            }
        }

        public void TriggerAutoSave()
        {
            Debug.Log($"[SaveGameManager] Auto-save triggered.");
            OnAutoSave?.Invoke();
        }

        /// <summary>
        /// Saves data to local storage and attempts to sync with the active cloud service.
        /// </summary>
        /// <typeparam name="T">The type of data to save.</typeparam>
        /// <param name="key">Unique key/filename for the data.</param>
        /// <param name="data">The data object to serialize.</param>
        public async Task SaveDataAsync<T>(string key, T data)
        {
            if (IsClientConnectingToDedicated)
            {
                Debug.Log($"[SaveGameManager] Bypassed local save for '{key}' because client is connected to a dedicated server.");
                return;
            }

            string json = JsonUtility.ToJson(data, true);
            json = Encrypt(json);
            
            // 1. Local Save
            string localPath = GetLocalPath(key);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                await File.WriteAllTextAsync(localPath, json);
                Debug.Log($"[SaveGameManager] Local save successful: {localPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Local save failed: {e.Message}");
            }

            // 2. Cloud Sync (Only if enabled)
            if (saveLocation == SaveLocation.Cloud)
            {
#if STEAM_SERVICES
                SaveToSteam(key, json);
#elif UNITY_SERVICES
                await SaveToUnityCloud(key, json);
#endif
            }
        }

        /// <summary>
        /// Loads data, preferring cloud versions if available and newer (logic simplified for now).
        /// </summary>
        /// <typeparam name="T">The type of data to load.</typeparam>
        /// <param name="key">Unique key/filename for the data.</param>
        /// <returns>The loaded data, or default if not found.</returns>
        public async Task<T> LoadDataAsync<T>(string key)
        {
            string json = null;

            // 1. Try Load from Cloud
#if STEAM_SERVICES
            json = LoadFromSteam(key);
#elif UNITY_SERVICES
            json = await LoadFromUnityCloud<T>(key);
#endif

            // 2. If no cloud data, load from Local
            if (string.IsNullOrEmpty(json))
            {
                string localPath = GetLocalPath(key);
                if (File.Exists(localPath))
                {
                    json = await File.ReadAllTextAsync(localPath);
                    Debug.Log($"[SaveGameManager] Loaded from local storage: {localPath}");
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                json = Decrypt(json);
                return JsonUtility.FromJson<T>(json);
            }

            Debug.LogWarning($"[SaveGameManager] No save data found for key: {key}");
            return default;
        }

        /// <summary>
        /// Deletes data from local and cloud storage.
        /// </summary>
        public async Task DeleteDataAsync(string key)
        {
            // Local Delete
            string localPath = GetLocalPath(key);
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                Debug.Log($"[SaveGameManager] Deleted local file: {localPath}");
            }

            // Cloud Delete
#if STEAM_SERVICES
            if (SteamRemoteStorage.FileExists(key))
            {
                SteamRemoteStorage.FileDelete(key);
                Debug.Log($"[SaveGameManager] Deleted Steam Cloud file: {key}");
            }
#elif UNITY_SERVICES
            await DeleteFromUnityCloud(key);
#endif
        }

        #endregion

        #region Internal Helpers

        private string GetLocalPath(string key)
        {
            return Path.Combine(Application.persistentDataPath, "Saves", key + ".json");
        }

        #endregion

        #region GameID-Scoped Save API

        /// <summary>
        /// Returns the local file path for a game-scoped save file.
        /// Layout: PersistentDataPath/Saves/{gameId}/{filename}.json
        /// </summary>
        private string GetGameSavePath(string gameId, string filename)
        {
            return Path.Combine(Application.persistentDataPath, "Saves", gameId, filename + ".json");
        }

        public string GetOwnerKey(string playerId)
        {
            return playerId;
        }

        /// <summary>
        /// Saves data to a game-scoped file. Also syncs to cloud if enabled.
        /// </summary>
        /// <typeparam name="T">The type of data to save.</typeparam>
        /// <param name="gameId">The SaveGameId for this session.</param>
        /// <param name="filename">Filename without extension (e.g. "builds", "inventory_player123").</param>
        /// <param name="data">The data object to serialize.</param>
        public async Task SaveGameDataAsync<T>(string gameId, string filename, T data)
        {
            if (IsClientConnectingToDedicated)
            {
                Debug.Log($"[SaveGameManager] Bypassed game data save for '{filename}' because client is connected to a dedicated server.");
                return;
            }

            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot save game data: no ActiveGameId set.");
                return;
            }

            string json = JsonUtility.ToJson(data, true);
            json = Encrypt(json);
            string localPath = GetGameSavePath(gameId, filename);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                await File.WriteAllTextAsync(localPath, json);
                Debug.Log($"[SaveGameManager] Game data saved: {localPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Game data save failed: {e.Message}");
            }

            // Cloud sync
            if (saveLocation == SaveLocation.Cloud)
            {
                string cloudKey = $"{gameId}/{filename}";
#if STEAM_SERVICES
                SaveToSteam(cloudKey, json);
#elif UNITY_SERVICES
                await SaveToUnityCloud(cloudKey, json);
#endif
            }
        }

        /// <summary>
        /// Loads data from a game-scoped file.
        /// Returns default(T) if the file does not exist (fresh game).
        /// </summary>
        public async Task<T> LoadGameDataAsync<T>(string gameId, string filename)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot load game data: no ActiveGameId set.");
                return default;
            }

            string json = null;

            // Try cloud first
            string cloudKey = $"{gameId}/{filename}";
#if STEAM_SERVICES
            json = LoadFromSteam(cloudKey);
#elif UNITY_SERVICES
            json = await LoadFromUnityCloud<T>(cloudKey);
#endif

            // Fallback to local
            if (string.IsNullOrEmpty(json))
            {
                string localPath = GetGameSavePath(gameId, filename);
                if (File.Exists(localPath))
                {
                    json = await File.ReadAllTextAsync(localPath);
                    Debug.Log($"[SaveGameManager] Game data loaded from local: {localPath}");
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                json = Decrypt(json);
                return JsonUtility.FromJson<T>(json);
            }

            // No save found — this is a new game, start fresh
            return default;
        }

        /// <summary>
        /// Deletes a game-scoped save file from local and cloud storage.
        /// </summary>
        public async Task DeleteGameDataAsync(string gameId, string filename)
        {
            if (string.IsNullOrEmpty(gameId)) return;

            string localPath = GetGameSavePath(gameId, filename);
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                Debug.Log($"[SaveGameManager] Deleted game data: {localPath}");
            }

            string cloudKey = $"{gameId}/{filename}";
#if STEAM_SERVICES
            if (SteamRemoteStorage.FileExists(cloudKey))
            {
                SteamRemoteStorage.FileDelete(cloudKey);
                Debug.Log($"[SaveGameManager] Deleted Steam Cloud game data: {cloudKey}");
            }
#elif UNITY_SERVICES
            await DeleteFromUnityCloud(cloudKey);
#endif
        }

        #region Unified Host Player Data

        [Serializable]
        public class SubsystemData
        {
            public string SubsystemName;
            public string JsonData;
        }

        [Serializable]
        public class HostPlayerEntry
        {
            public string PlayerId;
            public List<SubsystemData> Subsystems = new List<SubsystemData>();
        }

        [Serializable]
        public class HostPlayersFile
        {
            public List<HostPlayerEntry> Players = new List<HostPlayerEntry>();
        }

        private readonly System.Threading.SemaphoreSlim playersFileLock = new System.Threading.SemaphoreSlim(1, 1);

        public async Task SavePlayerSubsystemToHostAsync<T>(string gameId, string playerId, string subsystem, T data)
        {
            if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId)) return;
            
            await playersFileLock.WaitAsync();
            try
            {
                HostPlayersFile fileData = null;
                string localPath = GetGameSavePath(gameId, "players");
                
                if (File.Exists(localPath))
                {
                    string existingJson = await File.ReadAllTextAsync(localPath);
                    existingJson = Decrypt(existingJson);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        try
                        {
                            fileData = JsonUtility.FromJson<HostPlayersFile>(existingJson);
                        }
                        catch (Exception parseEx)
                        {
                            Debug.LogWarning($"[SaveGameManager] Corrupt players file detected, starting fresh. Parse error: {parseEx.Message}");
                            File.Delete(localPath);
                            fileData = null;
                        }
                    }
                }
                if (fileData == null) fileData = new HostPlayersFile();

                var playerEntry = fileData.Players.Find(p => p.PlayerId == playerId);
                if (playerEntry == null)
                {
                    playerEntry = new HostPlayerEntry { PlayerId = playerId };
                    fileData.Players.Add(playerEntry);
                }

                var subEntry = playerEntry.Subsystems.Find(s => s.SubsystemName == subsystem);
                if (subEntry == null)
                {
                    subEntry = new SubsystemData { SubsystemName = subsystem };
                    playerEntry.Subsystems.Add(subEntry);
                }

                subEntry.JsonData = JsonUtility.ToJson(data);

                string newJson = JsonUtility.ToJson(fileData, true);
                newJson = Encrypt(newJson);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                await File.WriteAllTextAsync(localPath, newJson);
                Debug.Log($"[SaveGameManager] Saved unified player data for {playerId} subsystem: {subsystem}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Unified save failed: {e.Message}");
            }
            finally
            {
                playersFileLock.Release();
            }
        }

        public async Task<T> LoadPlayerSubsystemFromHostAsync<T>(string gameId, string playerId, string subsystem)
        {
            if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId)) return default;

            await playersFileLock.WaitAsync();
            try
            {
                string localPath = GetGameSavePath(gameId, "players");
                if (File.Exists(localPath))
                {
                    string existingJson = await File.ReadAllTextAsync(localPath);
                    existingJson = Decrypt(existingJson);
                    if (string.IsNullOrWhiteSpace(existingJson)) return default;
                    HostPlayersFile fileData;
                    try
                    {
                        fileData = JsonUtility.FromJson<HostPlayersFile>(existingJson);
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogWarning($"[SaveGameManager] Corrupt players file on load, returning default. Parse error: {parseEx.Message}");
                        return default;
                    }
                    if (fileData != null)
                    {
                        var playerEntry = fileData.Players.Find(p => p.PlayerId == playerId);
                        if (playerEntry != null)
                        {
                            var subEntry = playerEntry.Subsystems.Find(s => s.SubsystemName == subsystem);
                            if (subEntry != null && !string.IsNullOrEmpty(subEntry.JsonData))
                            {
                                return JsonUtility.FromJson<T>(subEntry.JsonData);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Unified load failed: {e.Message}");
            }
            finally
            {
                playersFileLock.Release();
            }
            return default;
        }

        #endregion

        #endregion

        #region Saved Games Index

        [Serializable]
        public class SavedGameEntry
        {
            public string GameId;
            public string GameName;
            public List<string> PlayerIds = new List<string>();
            public long CreatedTimestamp;
            public long LastUpdatedTimestamp;
            public bool IsPrivate;
            public string GameMode;
            public string LobbyPassword; // New field for restoring lobby settings
            public string OwnerPlayerId; // PlayerId of the original host/creator
            public SaveRole Role = SaveRole.Owner; // This player's role in the game

            public DateTime GetCreatedDate() => DateTimeOffset.FromUnixTimeSeconds(CreatedTimestamp).LocalDateTime;
            public DateTime GetLastUpdatedDate() => DateTimeOffset.FromUnixTimeSeconds(LastUpdatedTimestamp).LocalDateTime;
        }

        [Serializable]
        private class SavedGameIndex
        {
            public List<SavedGameEntry> Entries = new List<SavedGameEntry>();
        }

        private const string INDEX_KEY = "SavedGames";
        private SavedGameIndex cachedIndex;

        /// <summary>
        /// Retrieves the list of all saved games from the index.
        /// </summary>
        public async Task<List<SavedGameEntry>> GetAllSavesAsync()
        {
            if (cachedIndex == null)
            {
                cachedIndex = await LoadDataAsync<SavedGameIndex>(INDEX_KEY);
                if (cachedIndex == null) cachedIndex = new SavedGameIndex();
            }
            return cachedIndex.Entries;
        }

        /// <summary>
        /// Creates a new game entry in the index.
        /// </summary>
        public async Task<string> CreateNewSaveAsync(string gameName, string hostPlayerId, bool isPrivate, string gameMode, string password = "", SaveRole role = SaveRole.Owner)
        {
            await GetAllSavesAsync(); // Ensure cache is loaded

            string newGameId = Guid.NewGuid().ToString();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var newEntry = new SavedGameEntry
            {
                GameId = newGameId,
                GameName = gameName,
                PlayerIds = new List<string> { hostPlayerId },
                CreatedTimestamp = now,
                LastUpdatedTimestamp = now,
                IsPrivate = isPrivate,
                GameMode = gameMode,
                LobbyPassword = password,
                OwnerPlayerId = hostPlayerId,
                Role = role
            };

            cachedIndex.Entries.Add(newEntry);
            await SaveDataAsync(INDEX_KEY, cachedIndex);
            
            return newGameId;
        }

        /// <summary>
        /// Updates an existing save entry (timestamp and player list).
        /// </summary>
        public async Task UpdateSaveEntryAsync(string gameId, List<string> currentPlayerIds, string password = null)
        {
            await GetAllSavesAsync();

            var entry = cachedIndex.Entries.Find(e => e.GameId == gameId);
            if (entry != null)
            {
                entry.LastUpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (password != null) entry.LobbyPassword = password;
                
                
                // Merge player IDs
                if (currentPlayerIds != null)
                {
                    foreach (var pid in currentPlayerIds)
                    {
                        if (!entry.PlayerIds.Contains(pid))
                        {
                            entry.PlayerIds.Add(pid);
                        }
                    }
                }

                await SaveDataAsync(INDEX_KEY, cachedIndex);
            }
        }

        /// <summary>
        /// Adds a new save entry or updates an existing one.
        /// Called by both host (Owner) and client (Participant) when a game starts.
        /// If the entry already exists, updates the timestamp and merges player IDs.
        /// If not, creates a new entry with the given role and owner info.
        /// </summary>
        public async Task AddOrUpdateSaveEntryAsync(
            string gameId, string gameName, string localPlayerId,
            string hostPlayerId, bool isPrivate, string gameMode,
            SaveRole role, string password = "")
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot add save entry: no GameId provided.");
                return;
            }

            await GetAllSavesAsync();

            var existing = cachedIndex.Entries.Find(e => e.GameId == gameId);
            if (existing != null)
            {
                // Update existing entry
                existing.LastUpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!string.IsNullOrEmpty(gameName)) existing.GameName = gameName;
                if (!string.IsNullOrEmpty(password)) existing.LobbyPassword = password;

                if (!string.IsNullOrEmpty(localPlayerId) && !existing.PlayerIds.Contains(localPlayerId))
                {
                    existing.PlayerIds.Add(localPlayerId);
                }

                // Update Role to reflect current session status (e.g., if rejoining as client, update from Owner to Participant)
                existing.Role = role;

                Debug.Log($"[SaveGameManager] Updated existing save entry: {gameId} (Role: {existing.Role})");
            }
            else
            {
                // Create new entry
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var newEntry = new SavedGameEntry
                {
                    GameId = gameId,
                    GameName = gameName ?? "Unnamed Game",
                    PlayerIds = new List<string> { localPlayerId },
                    CreatedTimestamp = now,
                    LastUpdatedTimestamp = now,
                    IsPrivate = isPrivate,
                    GameMode = gameMode ?? "",
                    LobbyPassword = password ?? "",
                    OwnerPlayerId = hostPlayerId,
                    Role = role
                };

                cachedIndex.Entries.Add(newEntry);
                Debug.Log($"[SaveGameManager] Created new save entry: {gameId} (Role: {role})");
            }

            await SaveDataAsync(INDEX_KEY, cachedIndex);
        }

        /// <summary>
        /// Deletes a save game entry and its index.
        /// Note: Caller is responsible for deleting the actual game data files using the GameId.
        /// </summary>
        public async Task DeleteSaveEntryAsync(string gameId)
        {
            await GetAllSavesAsync();

            int removed = cachedIndex.Entries.RemoveAll(e => e.GameId == gameId);
            if (removed > 0)
            {
                await SaveDataAsync(INDEX_KEY, cachedIndex);
            }
        }

        #endregion

        #region Steam Cloud Implementation
#if STEAM_SERVICES
        private void SaveToSteam(string key, string json)
        {
            if (!SteamManager.Initialized) return;

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            if (SteamRemoteStorage.FileWrite(key, bytes, bytes.Length))
            {
                Debug.Log($"[SaveGameManager] Synced to Steam Cloud: {key}");
            }
            else
            {
                Debug.LogError($"[SaveGameManager] Steam Cloud sync failed: {key}");
            }
        }

        private string LoadFromSteam(string key)
        {
            if (!SteamManager.Initialized) return null;

            if (SteamRemoteStorage.FileExists(key))
            {
                int fileSize = SteamRemoteStorage.GetFileSize(key);
                if (fileSize > 0)
                {
                    byte[] bytes = new byte[fileSize];
                    int read = SteamRemoteStorage.FileRead(key, bytes, fileSize);
                    if (read > 0)
                    {
                        Debug.Log($"[SaveGameManager] Loaded from Steam Cloud: {key}");
                        return System.Text.Encoding.UTF8.GetString(bytes);
                    }
                }
            }
            return null;
        }
#endif
        #endregion

        #region Unity Cloud Implementation
#if UNITY_SERVICES
        private async Task SaveToUnityCloud(string key, string json)
        {
            try
            {
                var dataToSave = new Dictionary<string, object> { { key, json } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(dataToSave);
                Debug.Log($"[SaveGameManager] Synced to Unity Cloud: {key}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Unity Cloud sync failed: {e.Message}");
            }
        }

        private async Task<string> LoadFromUnityCloud<T>(string key)
        {
            try
            {
                var results = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
                if (results.TryGetValue(key, out var item))
                {
                    Debug.Log($"[SaveGameManager] Loaded from Unity Cloud: {key}");
                    try 
                    {
                        string str = item.Value.GetAs<string>();
                        if (!string.IsNullOrEmpty(str)) 
                            return str;
                    }
                    catch { }
                    
                    // Fallback to previous object behavior if not string
                    return JsonUtility.ToJson(item.Value.GetAs<T>());
                }
            }
            catch (Exception e)
            {
                // Cloud load failed or key doesn't exist - strict fail is acceptable, fallback to local
                Debug.LogWarning($"[SaveGameManager] Unity Cloud load warning: {e.Message}");
            }
            return null;
        }

        private async Task DeleteFromUnityCloud(string key)
        {
            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(key);
                Debug.Log($"[SaveGameManager] Deleted from Unity Cloud: {key}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveGameManager] Unity Cloud delete failed: {e.Message}");
            }
        }
#endif
        #endregion

        #region Debug Hooks (Inspector)

        [ContextMenu("Test Save")]
        public async void TestSave()
        {
            var testData = new TestSaveObject { message = "Hello Cloud", timestamp = DateTime.Now.ToString() };
            await SaveDataAsync("test_save", testData);
        }

        [ContextMenu("Test Load")]
        public async void TestLoad()
        {
            var data = await LoadDataAsync<TestSaveObject>("test_save");
            if (data != null)
            {
                Debug.Log($"Loaded Test Data: {data.message} at {data.timestamp}");
            }
        }

        [ContextMenu("Test Index")]
        public async void TestIndex()
        {
            string id = await CreateNewSaveAsync("My New World", "Player123", false, "FreeForAll");
            Debug.Log($"Created new save: {id}");

            await UpdateSaveEntryAsync(id, new List<string> { "Player123", "Player456" });
            Debug.Log("Updated save with new player.");
            
            var saves = await GetAllSavesAsync();
            Debug.Log($"Total Saves: {saves.Count}");
            foreach(var s in saves)
            {
                Debug.Log($" - {s.GameName} ({s.GameId}) Mode: {s.GameMode} Private: {s.IsPrivate}");
            }
        }

        [Serializable]
        private class TestSaveObject
        {
            public string message;
            public string timestamp;
        }

        #endregion

        #region Debug Delete (Inspector — Testing Only)

        /// <summary>
        /// Deletes the build data file for a specific game (local + cloud).
        /// </summary>
        public async Task DeleteBuildDataAsync(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot delete build data: no GameId.");
                return;
            }

            await DeleteGameDataAsync(gameId, "builds");
            Debug.Log($"[SaveGameManager] ✓ Deleted build data for game: {gameId}");
        }

        /// <summary>
        /// Deletes the unified players file (contains all player inventory + stats subsystems) for a game (local + cloud).
        /// </summary>
        public async Task DeletePlayersFileAsync(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot delete players file: no GameId.");
                return;
            }

            await DeleteGameDataAsync(gameId, "players");
            Debug.Log($"[SaveGameManager] ✓ Deleted players file (inventory + stats) for game: {gameId}");
        }

        /// <summary>
        /// Deletes ALL game data for a specific game: builds, players file, and the index entry.
        /// Also cleans up the local game directory if empty.
        /// </summary>
        public async Task DeleteAllGameDataAsync(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[SaveGameManager] Cannot delete game data: no GameId.");
                return;
            }

            // Delete individual data files
            await DeleteBuildDataAsync(gameId);
            await DeletePlayersFileAsync(gameId);

            // Remove from index
            await DeleteSaveEntryAsync(gameId);

            // Clean up local game directory
            string gameDir = Path.Combine(Application.persistentDataPath, "Saves", gameId);
            if (Directory.Exists(gameDir))
            {
                try
                {
                    Directory.Delete(gameDir, true);
                    Debug.Log($"[SaveGameManager] ✓ Deleted local game directory: {gameDir}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveGameManager] Could not delete game directory: {e.Message}");
                }
            }

            Debug.Log($"[SaveGameManager] ✓ Deleted ALL data for game: {gameId}");
        }

        /// <summary>
        /// Nuclear option: deletes ALL local save files, clears cloud data for known keys, and wipes the index.
        /// </summary>
        public async Task DeleteEverythingAsync()
        {
            // 1. Delete all known game data via index
            var saves = await GetAllSavesAsync();
            foreach (var save in saves)
            {
                await DeleteBuildDataAsync(save.GameId);
                await DeletePlayersFileAsync(save.GameId);

                // Clean up local game directory
                string gameDir = Path.Combine(Application.persistentDataPath, "Saves", save.GameId);
                if (Directory.Exists(gameDir))
                {
                    try { Directory.Delete(gameDir, true); }
                    catch (Exception e) { Debug.LogWarning($"[SaveGameManager] Could not delete directory: {e.Message}"); }
                }
            }

            // 2. Delete the index itself
            await DeleteDataAsync(INDEX_KEY);
            cachedIndex = null;

            // 3. Wipe the entire local Saves folder
            string savesRoot = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(savesRoot))
            {
                try
                {
                    Directory.Delete(savesRoot, true);
                    Debug.Log($"[SaveGameManager] ✓ Deleted entire Saves directory: {savesRoot}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveGameManager] Could not delete Saves root: {e.Message}");
                }
            }

            Debug.Log("[SaveGameManager] ✓ NUCLEAR DELETE COMPLETE — all save data wiped.");
        }

        // ── ContextMenu Wrappers (use ActiveGameId) ──────────────────────────

        [ContextMenu("Debug/Delete Build Data")]
        public async void DebugDeleteBuildData()
        {
            ActivateTestIdIfNeeded();
            string gameId = ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogError("[SaveGameManager] No ActiveGameId set. Set one or enable useTestGameId.");
                return;
            }
            await DeleteBuildDataAsync(gameId);
        }

        [ContextMenu("Debug/Delete Player Data (Inventory + Stats)")]
        public async void DebugDeletePlayerData()
        {
            ActivateTestIdIfNeeded();
            string gameId = ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogError("[SaveGameManager] No ActiveGameId set. Set one or enable useTestGameId.");
                return;
            }
            await DeletePlayersFileAsync(gameId);
        }

        [ContextMenu("Debug/Delete ALL Game Data")]
        public async void DebugDeleteAllGameData()
        {
            ActivateTestIdIfNeeded();
            string gameId = ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogError("[SaveGameManager] No ActiveGameId set. Set one or enable useTestGameId.");
                return;
            }
            await DeleteAllGameDataAsync(gameId);
        }

        [ContextMenu("Debug/Delete Save Index")]
        public async void DebugDeleteSaveIndex()
        {
            await DeleteDataAsync(INDEX_KEY);
            cachedIndex = null;
            Debug.Log("[SaveGameManager] ✓ Save index deleted.");
        }

        [ContextMenu("Debug/Delete Everything (Nuclear)")]
        public async void DebugDeleteEverything()
        {
            await DeleteEverythingAsync();
        }

        #endregion
    }
}
