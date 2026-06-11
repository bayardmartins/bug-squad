using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Server-based lobby manager — persistent server that players join/leave freely.
    /// No ready checks; players can late-join mid-game.
    /// </summary>
    public class ServerLobbyManager : LobbyManagerBase
    {
        public static ServerLobbyManager Instance { get; private set; }

        public override LobbyType LobbyType => LobbyType.ServerBased;

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            base.Awake();
        }

        protected override void InitializePlatformService()
        {
#if UNITY_SERVICES
            lobbyService = new UnityServerLobby();
#elif STEAM_SERVICES
            lobbyService = new SteamServerLobby();
#endif
        }

        // ─── Server-Based Overrides ───

        /// <summary>
        /// Server-based lobbies auto-set players as ready on join (no ready check).
        /// </summary>
        public override async Task<LobbyData> JoinLobby(string lobbyId, string password = "")
        {
            if (!ValidateServiceAndAuth(out _)) return null;

            // Server-based: players join as ready immediately
            var playerData = BuildLocalPlayerData(isReady: true);
            var lobby = await lobbyService.JoinLobbyAsync(lobbyId, playerData);
            LobbyData = lobby;
            SyncLobbyToSessionData(lobby);
            return lobby;
        }

        /// <summary>
        /// Server-based lobbies auto-set players as ready on join by code.
        /// </summary>
        public override async Task<LobbyData> JoinLobbyByCode(string joinCode, string password = "")
        {
            if (!ValidateServiceAndAuth(out _)) return null;

            // Server-based: players join as ready immediately
            var playerData = BuildLocalPlayerData(isReady: true);
            var lobby = await lobbyService.JoinLobbyByCodeAsync(joinCode, playerData);
            LobbyData = lobby;
            SyncLobbyToSessionData(lobby);
            return lobby;
        }

        /// <summary>
        /// For server-based lobbies, "start game" can be called by the host at any time.
        /// The lobby remains open for late joiners (lobby is not locked).
        /// </summary>
        public override async Task StartGameAsync()
        {
            if (lobbyService == null)
            {
                Debug.LogError("[ServerLobbyManager] Lobby service not initialized.");
                return;
            }

            await lobbyService.StartGameAsync();
        }

        /// <summary>
        /// Ready state is not used in server-based lobbies.
        /// </summary>
        public override Task<LobbyData> UpdateReadyState(string readyState)
        {
            Debug.LogWarning("[ServerLobbyManager] Ready state is not applicable for server-based lobbies.");
            return Task.FromResult(LobbyData);
        }
    }
}
