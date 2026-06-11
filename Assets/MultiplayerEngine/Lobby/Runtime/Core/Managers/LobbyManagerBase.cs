using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Abstract base class for lobby managers.
    /// Contains shared lobby lifecycle logic (events, ILobby wiring, session sync, presence).
    /// Subclasses define lobby-type-specific behavior (ready checks, game start, etc.).
    /// </summary>
    public abstract class LobbyManagerBase : MonoBehaviour, ILobbyService
    {
        // ─── Abstract ───

        /// <summary>
        /// The type of lobby this manager handles. Defined by each subclass.
        /// </summary>
        public abstract LobbyType LobbyType { get; }

        // ─── Events ───

        public static event Action<LobbyData> OnLobbyCreated;
        public static event Action<LobbyData> OnLobbyJoined;
        public static event Action OnLobbyLeft;
        public static event Action<LobbyData> OnLobbyUpdated;
        public static event Action<LobbyData> OnLobbyPlayerDataUpdated;
        public static event Action<bool> OnAllPlayersReady;
        public static event Action<LobbyData> OnGameStarted;

        /// <summary>
        /// Raised when presence should be updated. Decoupled from FriendsManager for modularity.
        /// </summary>
        public static event Action<PresenceUpdateRequest> OnPresenceUpdateRequested;

        // ─── State ───

        public LobbyData LobbyData { get; protected set; }

        /// <summary>
        /// ILobbyService implementation — exposes current lobby data via interface.
        /// </summary>
        public LobbyData CurrentLobbyData => LobbyData;

        protected ILobby lobbyService;

        // ─── Lifecycle ───

        protected virtual void Awake()
        {
            InitializePlatformService();
            ServiceLocator.Register<ILobbyService>(this);
            AuthenticationManager.OnProfileSetupCompleted += HandleProfileCompleted;
        }

        protected virtual void OnDestroy()
        {
            AuthenticationManager.OnProfileSetupCompleted -= HandleProfileCompleted;
            ServiceLocator.Unregister<ILobbyService>();
            UnsubscribeLobbyServiceEvents();
        }

        protected virtual void Start()
        {
            if (lobbyService == null)
            {
                Debug.LogError($"[{GetType().Name}] No lobby service implementation found.");
                return;
            }
            SubscribeLobbyServiceEvents();
        }

        /// <summary>
        /// Initializes the platform-specific ILobby implementation.
        /// Override to provide a custom ILobby for your lobby type.
        /// </summary>
        protected abstract void InitializePlatformService();

        private void HandleProfileCompleted(bool success)
        {
            _ = lobbyService?.InitializeAsync();
        }

        // ─── ILobby Event Wiring ───

        protected virtual void SubscribeLobbyServiceEvents()
        {
            if (lobbyService == null) return;
            lobbyService.LobbyCreated += HandleLobbyCreated;
            lobbyService.LobbyJoined += HandleLobbyJoined;
            lobbyService.LobbyLeft += HandleLobbyLeft;
            lobbyService.LobbyUpdated += HandleLobbyUpdated;
            lobbyService.LobbyPlayerDataUpdated += RaiseOnLobbyPlayerDataUpdated;
            lobbyService.GameStarted += RaiseOnGameStarted;
        }

        protected virtual void UnsubscribeLobbyServiceEvents()
        {
            if (lobbyService == null) return;
            lobbyService.LobbyCreated -= HandleLobbyCreated;
            lobbyService.LobbyJoined -= HandleLobbyJoined;
            lobbyService.LobbyLeft -= HandleLobbyLeft;
            lobbyService.LobbyUpdated -= HandleLobbyUpdated;
            lobbyService.LobbyPlayerDataUpdated -= RaiseOnLobbyPlayerDataUpdated;
            lobbyService.GameStarted -= RaiseOnGameStarted;
        }

        // ─── Event Handlers (virtual — subclasses can override) ───

        protected virtual void HandleLobbyCreated(LobbyData lobbyData)
        {
            LobbyData = lobbyData;
            SyncLobbyToSessionData(lobbyData);
            OnLobbyCreated?.Invoke(lobbyData);
            SetPresenceBasedOnPrivacy(lobbyData);
        }

        protected virtual void HandleLobbyJoined(LobbyData lobbyData)
        {
            LobbyData = lobbyData;
            SyncLobbyToSessionData(lobbyData);
            OnLobbyJoined?.Invoke(lobbyData);
            SetPresenceBasedOnPrivacy(lobbyData);
        }

        protected virtual void HandleLobbyLeft()
        {
            LobbyData = null;
            RuntimeSessionData.Instance.ClearSession();
            OnLobbyLeft?.Invoke();
            OnPresenceUpdateRequested?.Invoke(new PresenceUpdateRequest(FriendPresence.Online, "Left lobby"));
        }

        protected virtual void HandleLobbyUpdated(LobbyData lobbyData)
        {
            LobbyData = lobbyData;
            SyncLobbyToSessionData(lobbyData);
            OnLobbyUpdated?.Invoke(lobbyData);
            SetPresenceBasedOnPrivacy(lobbyData);
        }

        protected void RaiseOnLobbyPlayerDataUpdated(LobbyData data)
        {
            OnLobbyPlayerDataUpdated?.Invoke(data);
        }

        protected void RaiseOnGameStarted(LobbyData data)
        {
            OnGameStarted?.Invoke(data);
        }

        protected void RaiseOnAllPlayersReady(bool allReady)
        {
            OnAllPlayersReady?.Invoke(allReady);
        }

        // ─── Presence ───

        protected void SetPresenceBasedOnPrivacy(LobbyData lobbyData)
        {
            if (lobbyData == null) return;
            var presence = lobbyData.IsPrivate ? FriendPresence.Online : FriendPresence.InLobby;
            OnPresenceUpdateRequested?.Invoke(new PresenceUpdateRequest(presence, "Lobby privacy changed"));
        }

        // ─── Session Data Sync ───

        protected void SyncLobbyToSessionData(LobbyData lobby)
        {
            if (lobby == null) return;
            var session = RuntimeSessionData.Instance;
            bool isLocalHost = lobby.HostId == session.PlayerId;
            session.SetLobbySession(lobby.LobbyId, lobby.JoinCode, lobby.HostId, isLocalHost, LobbyType);
        }

        // ─── Shared Helpers ───

        /// <summary>
        /// Builds default player data from RuntimeSessionData for lobby create/join.
        /// </summary>
        protected Dictionary<PlayerDataKeys, string> BuildLocalPlayerData(bool isReady)
        {
            var session = RuntimeSessionData.Instance;
            return new Dictionary<PlayerDataKeys, string>
            {
                [PlayerDataKeys.PlayerName] = session.DisplayName,
                [PlayerDataKeys.PlayerID] = session.PlayerId,
                [PlayerDataKeys.PlayerCharacter] = string.Empty,
                [PlayerDataKeys.PlayerReady] = isReady ? "true" : "false"
            };
        }

        /// <summary>
        /// Builds default lobby data dictionary.
        /// </summary>
        protected Dictionary<LobbyDataKeys, string> BuildLobbyMetadata(GameMode gameMode, string password = "")
        {
            var lobbyData = new Dictionary<LobbyDataKeys, string>
            {
                [LobbyDataKeys.GameMode] = gameMode.ToString(),
                [LobbyDataKeys.LobbyType] = LobbyType.ToString()
            };

            if (!string.IsNullOrEmpty(password))
            {
                lobbyData[LobbyDataKeys.Password] = password;
            }

            return lobbyData;
        }

        protected bool ValidateServiceAndAuth(out string error)
        {
            if (lobbyService == null)
            {
                error = "Lobby service not initialized.";
                Debug.LogError($"[{GetType().Name}] {error}");
                return false;
            }

            if (!RuntimeSessionData.Instance.IsAuthenticated)
            {
                error = "Player profile not available (RuntimeSessionData).";
                Debug.LogError($"[{GetType().Name}] {error}");
                return false;
            }

            error = null;
            return true;
        }

        // ─── ILobbyService Implementation ───

        public virtual async Task<LobbyData> CreateLobby(string lobbyName, bool isPrivate, int maxPlayers, GameMode gameMode, string password = "")
        {
            if (!ValidateServiceAndAuth(out _)) return null;

            var playerData = BuildLocalPlayerData(isReady: true);
            var lobbyMetadata = BuildLobbyMetadata(gameMode, password);

            var lobby = await lobbyService.CreateLobbyAsync(lobbyName, isPrivate, maxPlayers, lobbyMetadata, playerData);
            LobbyData = lobby;
            SyncLobbyToSessionData(lobby);
            SetPresenceBasedOnPrivacy(lobby);
            return lobby;
        }

        public virtual async Task<LobbyData> JoinLobby(string lobbyId, string password = "")
        {
            if (!ValidateServiceAndAuth(out _)) return null;

            var playerData = BuildLocalPlayerData(isReady: false);
            var lobby = await lobbyService.JoinLobbyAsync(lobbyId, playerData);
            LobbyData = lobby;
            SyncLobbyToSessionData(lobby);
            return lobby;
        }

        public virtual async Task<LobbyData> JoinLobbyByCode(string joinCode, string password = "")
        {
            if (!ValidateServiceAndAuth(out _)) return null;

            var playerData = BuildLocalPlayerData(isReady: false);
            var lobby = await lobbyService.JoinLobbyByCodeAsync(joinCode, playerData);
            LobbyData = lobby;
            SyncLobbyToSessionData(lobby);
            return lobby;
        }

        public virtual async Task<bool> LeaveLobby(LobbyData lobbyData = null)
        {
            if (lobbyService == null) return false;

            var result = await lobbyService.LeaveLobbyAsync();
            if (result)
            {
                OnLobbyLeft?.Invoke();
                OnPresenceUpdateRequested?.Invoke(new PresenceUpdateRequest(FriendPresence.Online, "Left lobby"));
            }
            return result;
        }

        public virtual async Task<LobbyData> UpdatePlayerData(IDictionary<PlayerDataKeys, string> playerData)
        {
            if (lobbyService == null) return null;

            var lobby = await lobbyService.UpdatePlayerDataAsync(playerData);
            LobbyData = lobby;
            OnLobbyPlayerDataUpdated?.Invoke(lobby);
            return lobby;
        }

        public virtual async Task<LobbyData> UpdateLobbyPrivacy(bool isPrivate)
        {
            if (lobbyService == null) return null;
            return await lobbyService.UpdateLobbyPrivacyAsync(isPrivate);
        }

        public virtual async Task<LobbyData> UpdateLobbyData(IDictionary<LobbyDataKeys, string> lobbyData)
        {
            if (lobbyService == null) return null;

            var lobby = await lobbyService.UpdateLobbyDataAsync(lobbyData);
            LobbyData = lobby;
            OnLobbyUpdated?.Invoke(lobby);
            return lobby;
        }

        public virtual async Task<LobbyData> UpdateGameMode(GameMode gameMode)
        {
            if (lobbyService == null) return null;

            var lobby = await UpdateLobbyData(new Dictionary<LobbyDataKeys, string>
            {
                { LobbyDataKeys.GameMode, gameMode.ToString() }
            });
            LobbyData = lobby;
            return lobby;
        }

        public virtual async Task<LobbyData> UpdateCharacter(string characterId)
        {
            if (lobbyService == null) return null;

            var lobby = await UpdatePlayerData(new Dictionary<PlayerDataKeys, string>
            {
                { PlayerDataKeys.PlayerCharacter, characterId }
            });
            LobbyData = lobby;
            return lobby;
        }

        public virtual async Task<LobbyData> KickPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || lobbyService == null) return null;

            var result = await lobbyService.KickPlayerAsync(playerId);
            LobbyData = result;
            return result;
        }

        public virtual async Task<List<LobbyListData>> GetLobbyListAsync()
        {
            if (lobbyService == null) return null;
            return await lobbyService.GetLobbyListAsync();
        }

        public virtual async Task<LobbyData> UpdateReadyState(string readyState)
        {
            if (lobbyService == null) return null;

            var lobby = await UpdatePlayerData(new Dictionary<PlayerDataKeys, string>
            {
                { PlayerDataKeys.PlayerReady, readyState }
            });
            LobbyData = lobby;
            return lobby;
        }

        public virtual async Task StartGameAsync()
        {
            if (lobbyService == null) return;
            await lobbyService.StartGameAsync();
        }
    }
}
