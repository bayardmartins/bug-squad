using System;
using System.Collections.Generic;
using System.Linq;

#if UNITY_SERVICES || STEAM_SERVICES
using System.Threading.Tasks;
using Unity.Netcode;
#if UNITY_SERVICES
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages multiplayer session lifecycle, including scene loading and player readiness.
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Session Settings")]
        [Tooltip("Load scenes asynchronously.")]
        public bool loadAsync = false;

        [Tooltip("Maximum wait time for players to load (seconds).")]
        public int waitTime = 45;

        private int playersLoaded = 0;
        private int totalPlayers = 0;

        // Authoritative server-side cache of validated player data
        public readonly Dictionary<ulong, PlayerSessionData> ServerSessionCache = new();



        public static event Action OnAllPlayersLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            Instance = this;
        }

        private void OnEnable()
        {
            // Subscribe to static event - no need to check Instance
            LobbyManagerBase.OnLobbyUpdated += OnLobbyUpdated;
        }

        private void OnDisable()
        {
            LobbyManagerBase.OnLobbyUpdated -= OnLobbyUpdated;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsServer)
            {
                ServerSessionCache.Remove(clientId);
                Debug.Log($"[SessionManager] Cleaned up Client {clientId} from ServerSessionCache.");
            }
        }

        private bool hasGameStarted;

        private async void OnLobbyUpdated(LobbyData data)
        {
            if (hasGameStarted) return;

            if (data == null)
            {
                Debug.LogError("LobbyData is null in OnLobbyUpdated.");
                return;
            }

            if (!data.Data.TryGetValue(LobbyDataKeys.GameId, out var gameId) || string.IsNullOrEmpty(gameId))
                return;

            hasGameStarted = true;

            LoadingScreen.Instance?.ShowLoading("Connecting to server...");

            var localPlayer = data.Players.FirstOrDefault(p => p.IsLocalPlayer);
            if (localPlayer == null)
            {
                Debug.LogError("Local player not found in lobby.");
                return;
            }

            totalPlayers = data.Players.Count;

            localPlayer.Data.TryGetValue(PlayerDataKeys.PlayerCharacter, out var characterId);
            if (string.IsNullOrEmpty(characterId))
            {
                var fallback = ServiceLocator.Get<IProfileService>()?.CharacterData;
                characterId = fallback != null && fallback.Count > 0 ? fallback[0].CharacterId : null;
            }
            RuntimeSessionData.Instance.SetSelectedCharacter(characterId);

#if UNITY_SERVICES
            if (data.HostId != RuntimeSessionData.Instance.PlayerId)
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(gameId);
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));
            }
#elif STEAM_SERVICES
            NetworkManager.Singleton.GetComponent<SteamNetworkingSocketsTransport>().ConnectToSteamID = ulong.Parse(gameId);
#endif

            // ─── Set Connection Payload ───
            var payload = new ConnectionPayload
            {
                PlayerId = RuntimeSessionData.Instance.PlayerId,
                DisplayName = RuntimeSessionData.Instance.DisplayName,
                SelectedCharacterId = RuntimeSessionData.Instance.SelectedCharacterId
            };
            string jsonPayload = JsonUtility.ToJson(payload);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            if (data.HostId == RuntimeSessionData.Instance.PlayerId)
            {
                LoadingScreen.Instance?.UpdateStatus("Starting host...");
                NetworkManager.Singleton.StartHost();

                // test

                // Pre-populate host session data since host doesn't go through standard approval flow
                ServerSessionCache[NetworkManager.ServerClientId] = new PlayerSessionData
                {
                    ClientId = NetworkManager.ServerClientId,
                    PlayerId = RuntimeSessionData.Instance.PlayerId,
                    DisplayName = RuntimeSessionData.Instance.DisplayName,
                    SelectedCharacterId = RuntimeSessionData.Instance.SelectedCharacterId
                };

                await WaitForPlayersToLoad();
            }
            else
            {
                LoadingScreen.Instance?.UpdateStatus("Joining game...");
                await Task.Delay(2000); // Small delay to ensure transport is ready
                NetworkManager.Singleton.StartClient();
                LoadingScreen.Instance?.UpdateStatus("Waiting for host...");
            }
        }

        private async Task WaitForPlayersToLoad()
        {
            var timeout = TimeSpan.FromSeconds(waitTime);
            var startTime = DateTime.UtcNow;

            while (playersLoaded < totalPlayers && (DateTime.UtcNow - startTime) < timeout)
            {
                await Task.Delay(1000);

                playersLoaded = NetworkManager.Singleton.ConnectedClientsList.Count;
                float progress = totalPlayers > 0 ? (float)playersLoaded / totalPlayers : 0f;
                LoadingScreen.Instance?.UpdateStatus($"Waiting for players... {playersLoaded}/{totalPlayers}");
                LoadingScreen.Instance?.SetProgress(progress * 0.5f); // First 50% is player connection
            }

            if (playersLoaded < totalPlayers)
            {
                Debug.LogWarning("Timeout reached. Not all players loaded, proceeding.");
            }

            await Task.Delay(1000); // Ensure all clients are ready

            LoadingScreen.Instance?.UpdateStatus("Loading game...");
            LoadingScreen.Instance?.SetProgress(0.6f); // Scene loading starts at 60%
            LoadNewScene("Game"); // Replace with dynamic scene selection if needed (e.g., from lobby data > GameMode)
        }

        private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            // Unsubscribe to prevent multiple calls
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;

            if (IsServer)
            {
                playersLoaded = clientsCompleted.Count;
                totalPlayers = NetworkManager.ConnectedClients.Count;

                if (playersLoaded >= totalPlayers)
                {
                    HideLoadingRpc();
                }
            }
        }

        public void LoadNewScene(string sceneName)
        {
            if (!IsServer)
            {
                return;
            }

            NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        [Rpc(SendTo.Everyone)]
        private void HideLoadingRpc()
        {
            OnAllPlayersLoaded?.Invoke();
            LoadingScreen.Instance?.HideLoading();

            playersLoaded = 0;
            totalPlayers = 0;
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ulong clientId = request.ClientNetworkId;

            // Host is always approved, even if payload is missing or empty
            if (clientId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                response.CreatePlayerObject = false; // We spawn manually via SpawnManager

                string playerId = RuntimeSessionData.Instance.PlayerId;
                string displayName = RuntimeSessionData.Instance.DisplayName;
                string selectedCharId = RuntimeSessionData.Instance.SelectedCharacterId;

                if (request.Payload != null && request.Payload.Length > 0)
                {
                    try
                    {
                        string json = System.Text.Encoding.UTF8.GetString(request.Payload);
                        var payload = JsonUtility.FromJson<ConnectionPayload>(json);
                        if (payload != null)
                        {
                            playerId = payload.PlayerId;
                            displayName = payload.DisplayName;
                            selectedCharId = payload.SelectedCharacterId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SessionManager] Could not parse host payload: {ex.Message}");
                    }
                }

                ServerSessionCache[clientId] = new PlayerSessionData
                {
                    ClientId = clientId,
                    PlayerId = playerId,
                    DisplayName = displayName,
                    SelectedCharacterId = selectedCharId
                };
                return;
            }

            if (request.Payload == null || request.Payload.Length == 0)
            {
                response.Approved = false;
                response.Reason = "Missing connection payload.";
                return;
            }

            try
            {
                string json = System.Text.Encoding.UTF8.GetString(request.Payload);
                var payload = JsonUtility.FromJson<ConnectionPayload>(json);

                bool isValid = false;

                // Cross-reference with LobbyData from LobbyManager
                var lobbyPlayer = LobbyManager.Instance?.CurrentLobbyData?.Players
                    .FirstOrDefault(p => p.PlayerId == payload.PlayerId);

                if (lobbyPlayer != null)
                {
                    // Retrieve their chosen character directly from the secure LobbyData, NOT the payload!
                    lobbyPlayer.Data.TryGetValue(PlayerDataKeys.PlayerCharacter, out var lobbyCharId);
                    
                    // Set the selected character to the official one from the LobbyData
                    payload.SelectedCharacterId = lobbyCharId;
                    isValid = true;
                }
                else
                {
                    Debug.LogWarning($"[Security] Client {clientId} with PlayerId {payload.PlayerId} is not in the Lobby list.");
                }

                if (isValid)
                {
                    response.Approved = true;
                    response.CreatePlayerObject = false; // We spawn manually via SpawnManager

                    ServerSessionCache[clientId] = new PlayerSessionData
                    {
                        ClientId = clientId,
                        PlayerId = payload.PlayerId,
                        DisplayName = payload.DisplayName,
                        SelectedCharacterId = payload.SelectedCharacterId
                    };
                }
                else
                {
                    response.Approved = false;
                    response.Reason = "Identity verification failed.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionManager] Error in ApprovalCheck: {ex.Message}");
                response.Approved = false;
                response.Reason = "Malformed connection payload.";
            }
        }
    }

    [System.Serializable]
    public class ConnectionPayload
    {
        public string PlayerId;
        public string DisplayName;
        public string SelectedCharacterId;
    }

    public class PlayerSessionData
    {
        public ulong ClientId;
        public string PlayerId;
        public string DisplayName;
        public string SelectedCharacterId;
    }
}
#endif