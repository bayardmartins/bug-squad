using System;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Central runtime data store for multiplayer session state.
    /// Auto-created at runtime, auto-destroyed on application quit.
    /// Managers read/write via RuntimeSessionData.Instance — no Inspector wiring needed.
    /// </summary>
    public class RuntimeSessionData : ScriptableObject
    {
        // ─── Singleton (runtime-created) ───

        private static RuntimeSessionData _instance;

        /// <summary>
        /// Singleton instance. Auto-creates on first access, auto-cleans on app quit.
        /// </summary>
        public static RuntimeSessionData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<RuntimeSessionData>();
                    _instance.hideFlags = HideFlags.DontSave; // don't persist to disk
                    Debug.Log("[RuntimeSessionData] Created runtime instance.");
                }
                return _instance;
            }
        }

        /// <summary>
        /// Returns true if the instance exists (without creating one).
        /// </summary>
        public static bool Exists => _instance != null;

        /// <summary>
        /// Destroys the runtime instance. Call on app quit or full cleanup.
        /// </summary>
        public static void DestroyInstance()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance);
                _instance = null;
                Debug.Log("[RuntimeSessionData] Destroyed runtime instance.");
            }
        }

        // ─── Player Identity (written by PlayerProfileManager) ───

        private string _playerId;
        private string _displayName;
        private string _avatarId;
        private Sprite _playerAvatar;

        // ─── Session State (written by LobbyManager / SessionManager) ───

        private string _currentLobbyId;
        private string _joinCode;
        private string _hostId;
        private bool _isHost;
        private string _selectedCharacterId;
        private LobbyType _lobbyType;

        // ─── Connection Flags ───

        private bool _isAuthenticated;
        private bool _isInLobby;
        private bool _isInGame;

        // ─── Public Properties ───

        /// <summary>Current player's unique ID.</summary>
        public string PlayerId => _playerId;

        /// <summary>Current player's display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Current player's avatar ID.</summary>
        public string AvatarId => _avatarId;

        /// <summary>Current player's avatar sprite.</summary>
        public Sprite PlayerAvatar => _playerAvatar;

        /// <summary>Current lobby ID, or null if not in a lobby.</summary>
        public string CurrentLobbyId => _currentLobbyId;

        /// <summary>Current lobby join code.</summary>
        public string JoinCode => _joinCode;

        /// <summary>Host player ID of the current lobby.</summary>
        public string HostId => _hostId;

        /// <summary>Whether the local player is the host.</summary>
        public bool IsHost => _isHost;

        /// <summary>Currently selected character ID.</summary>
        public string SelectedCharacterId => _selectedCharacterId;

        /// <summary>Whether the player is authenticated.</summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>Whether the player is currently in a lobby.</summary>
        public bool IsInLobby => _isInLobby;

        /// <summary>Whether the player is currently in a game.</summary>
        public bool IsInGame => _isInGame;

        /// <summary>The type of lobby the player is currently in.</summary>
        public LobbyType LobbyType => _lobbyType;

        // ─── Events ───

        /// <summary>Fired when player identity data changes (PlayerId, DisplayName, Avatar).</summary>
        public event Action OnPlayerIdentityChanged;

        /// <summary>Fired when session state changes (lobby, host, game).</summary>
        public event Action OnSessionStateChanged;

        // ─── Write Methods (called by managers) ───

        /// <summary>
        /// Sets the local player's identity. Called by PlayerProfileManager after fetching profile.
        /// </summary>
        public void SetPlayerIdentity(string id, string name, string avatarIndex, Sprite avatar)
        {
            _playerId = id;
            _displayName = name;
            _avatarId = avatarIndex;
            _playerAvatar = avatar;
            _isAuthenticated = !string.IsNullOrEmpty(id);
            OnPlayerIdentityChanged?.Invoke();
        }

        /// <summary>
        /// Sets the current lobby session data. Called by LobbyManager on create/join.
        /// </summary>
        public void SetLobbySession(string lobbyId, string code, string host, bool isLocalHost, LobbyType lobbyType = LobbyType.General)
        {
            _currentLobbyId = lobbyId;
            _joinCode = code;
            _hostId = host;
            _isHost = isLocalHost;
            _lobbyType = lobbyType;
            _isInLobby = !string.IsNullOrEmpty(lobbyId);
            OnSessionStateChanged?.Invoke();
        }

        /// <summary>
        /// Updates the host ID (e.g. when host migration happens). Called by LobbyManager.
        /// </summary>
        public void UpdateHost(string host, bool isLocalHost)
        {
            _hostId = host;
            _isHost = isLocalHost;
            OnSessionStateChanged?.Invoke();
        }

        /// <summary>
        /// Sets the selected character ID. Called by SessionManager or LobbyManager.
        /// </summary>
        public void SetSelectedCharacter(string characterId)
        {
            _selectedCharacterId = characterId;
        }

        /// <summary>
        /// Sets the in-game flag. Called by SessionManager when game starts/ends.
        /// </summary>
        public void SetInGame(bool inGame)
        {
            _isInGame = inGame;
            OnSessionStateChanged?.Invoke();
        }

        /// <summary>
        /// Clears lobby/session data. Called by LobbyManager on leave.
        /// </summary>
        public void ClearSession()
        {
            _currentLobbyId = null;
            _joinCode = null;
            _hostId = null;
            _isHost = false;
            _isInLobby = false;
            _isInGame = false;
            _selectedCharacterId = null;
            _lobbyType = LobbyType.General;
            OnSessionStateChanged?.Invoke();
        }

        /// <summary>
        /// Clears all runtime data. Called on sign-out or cleanup.
        /// </summary>
        public void ClearAll()
        {
            _playerId = null;
            _displayName = null;
            _avatarId = null;
            _playerAvatar = null;
            _isAuthenticated = false;
            ClearSession();
            OnPlayerIdentityChanged?.Invoke();
        }
    }
}
