#if UNITY_SERVICES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Unity lobby management using Unity Services.
    /// </summary>
    public class UnityLobby : ILobby
    {
        public event Action<LobbyData> LobbyCreated;
        public event Action<LobbyData> LobbyJoined;
        public event Action LobbyLeft;
        public event Action<LobbyData> LobbyUpdated;
        public event Action<LobbyData> LobbyPlayerDataUpdated;
        public event Action<LobbyData> GameStarted;

        private Lobby currentLobby;
        private ILobbyEvents currentLobbyEvents;

        /// <inheritdoc />
        public Task<bool> InitializeAsync()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public async Task<LobbyData> CreateLobbyAsync(
        string lobbyName,
        bool isPrivate,
        int maxPlayers,
        IDictionary<LobbyDataKeys, string> lobbyData,
        IDictionary<PlayerDataKeys, string> playerData)
        {
            try
            {
                // Convert enum keys to string for Unity Lobby API
                var player = new Player
                {
                    Data = playerData?.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, kvp.Value)
                    )
                };

                var options = new CreateLobbyOptions
                {
                    Player = player,
                    IsPrivate = isPrivate,
                    Data = lobbyData?.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => new DataObject(DataObject.VisibilityOptions.Member, kvp.Value)
                    )
                };

                var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);

                // Fire the event BEFORE subscribing to updates to prevent 
                // OnLobbyChanged from adding duplicate players during initialization
                LobbyCreated?.Invoke(lobbyDataResult);

                await SubscribeToLobbyEventsAsync(lobby.Id);

                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"CreateLobbyAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> JoinLobbyAsync(string lobbyId, IDictionary<PlayerDataKeys, string> playerData)
        {
            try
            {
                // Convert enum keys to string for Unity Lobby API
                var joinOptions = new JoinLobbyByIdOptions
                {
                    Player = new Player
                    {
                        Data = playerData != null
                            ? playerData.ToDictionary(
                                kvp => kvp.Key.ToString(),
                                kvp => new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, kvp.Value))
                            : null
                    }
                };

                var lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);

                // Fire the event BEFORE subscribing to updates to prevent 
                // OnLobbyChanged from adding duplicate players during initialization
                LobbyJoined?.Invoke(lobbyDataResult);

                await SubscribeToLobbyEventsAsync(lobby.Id);

                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"JoinLobbyAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> JoinLobbyByCodeAsync(string joinCode, IDictionary<PlayerDataKeys, string> playerData)
        {
            try
            {
                var joinOptions = new JoinLobbyByCodeOptions
                {
                    Player = new Player
                    {
                        Data = playerData?.ToDictionary(
                            kvp => kvp.Key.ToString(),
                            kvp => new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, kvp.Value)
                        )
                    }
                };

                var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode, joinOptions);
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);

                // Fire the event BEFORE subscribing to updates to prevent 
                // OnLobbyChanged from adding duplicate players during initialization
                LobbyJoined?.Invoke(lobbyDataResult);

                await SubscribeToLobbyEventsAsync(lobby.Id);

                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"JoinLobbyByCodeAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> LeaveLobbyAsync()
        {
            try
            {
                if (currentLobby == null)
                    return false;

                // Unsubscribe from lobby events first
                await UnsubscribeFromLobbyEventsAsync();

                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                currentLobby = null;

                NetworkManager.Singleton.Shutdown();

                LobbyLeft?.Invoke();
                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"LeaveLobbyAsync failed: {e}");
                currentLobby = null; // Ensure cleanup even on error
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> UpdatePlayerDataAsync(IDictionary<PlayerDataKeys, string> playerData)
        {
            try
            {
                if (currentLobby == null)
                {
                    Debug.LogError("UpdatePlayerDataAsync failed: No current lobby.");
                    throw new InvalidOperationException("No current lobby to update player data.");
                }

                if (playerData == null || playerData.Count == 0)
                {
                    Debug.LogWarning("UpdatePlayerDataAsync called with null or empty playerData.");
                    return MapLobbyToLobbyData(currentLobby);
                }

                // Convert enum keys to string for Unity Lobby API
                var options = new UpdatePlayerOptions
                {
                    Data = playerData.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, kvp.Value)
                    )
                };

                var lobby = await LobbyService.Instance.UpdatePlayerAsync(
                    currentLobby.Id,
                    AuthenticationService.Instance.PlayerId,
                    options
                );
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);
                LobbyPlayerDataUpdated?.Invoke(lobbyDataResult);
                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UpdatePlayerDataAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> UpdateLobbyDataAsync(IDictionary<LobbyDataKeys, string> lobbyData)
        {
            try
            {
                var options = new UpdateLobbyOptions
                {
                    Data = lobbyData.ToDictionary(
                        kvp => kvp.Key.ToString(), // Convert enum to string
                        kvp => new DataObject(DataObject.VisibilityOptions.Member, kvp.Value)
                    )
                };

                var lobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);
                LobbyUpdated?.Invoke(lobbyDataResult);
                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UpdateLobbyDataAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<LobbyListData>> GetLobbyListAsync()
        {
            try
            {
                var queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
                return queryResponse.Results
                    .Select(lobby =>
                    {
                        // Parse lobby type from data
                        var lobbyTypeStr = lobby.Data != null && lobby.Data.ContainsKey(LobbyDataKeys.LobbyType.ToString())
                            ? lobby.Data[LobbyDataKeys.LobbyType.ToString()].Value
                            : LobbyType.General.ToString();
                        Enum.TryParse<LobbyType>(lobbyTypeStr, out var parsedType);

                        return new LobbyListData
                        {
                            lobbyId = lobby.Id,
                            lobbyName = lobby.Name,
                            currentPlayers = lobby.Players.Count,
                            maxPlayers = lobby.MaxPlayers,
                            gameMode = lobby.Data != null && lobby.Data.ContainsKey(LobbyDataKeys.GameMode.ToString()) ? lobby.Data[LobbyDataKeys.GameMode.ToString()].Value : "FreeForAll",
                            isLocked = lobby.IsLocked,
                            lobbyType = parsedType
                        };
                    })
                    .Where(l => l.currentPlayers > 0) // Filter out empty/phantom lobbies
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetLobbyListAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> UpdateLobbyPrivacyAsync(bool isPrivate)
        {
            try
            {
                var lobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                {
                    IsPrivate = isPrivate
                });
                currentLobby = lobby;

                var lobbyDataResult = MapLobbyToLobbyData(lobby);
                LobbyUpdated?.Invoke(lobbyDataResult);
                return lobbyDataResult;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UpdateLobbyPrivacy failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<LobbyData> KickPlayerAsync(string playerId)
        {
            try
            {
                if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    var lobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                    return MapLobbyToLobbyData(lobby);
                }

                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
                var updatedLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                return MapLobbyToLobbyData(updatedLobby);
            }
            catch (Exception ex)
            {
                Debug.LogError($"KickPlayerAsync failed: {ex}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task StartGameAsync()
        {
            try
            {


                if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                {

                    currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                    {
                        IsLocked = true
                    });

                    var allocation = await RelayService.Instance.CreateAllocationAsync(currentLobby.MaxPlayers);

                    if (NetworkManager.Singleton == null)
                    {
                        Debug.LogError("StartGameAsync failed: NetworkManager.Singleton is null.");
                        throw new InvalidOperationException("NetworkManager.Singleton is not initialized.");
                    }

                    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    if (transport == null)
                    {
                        Debug.LogError("StartGameAsync failed: UnityTransport component not found on NetworkManager.");
                        throw new InvalidOperationException("UnityTransport component is missing from NetworkManager.");
                    }

                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

                    string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                    var options = new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                        {
                            { LobbyDataKeys.GameId.ToString(), new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                        }
                    };

                    await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, options);

                    GameStarted?.Invoke(MapLobbyToLobbyData(currentLobby));
                }
                else
                {
                    Debug.LogWarning("UnityLobby: StartGameAsync called but player is not the host!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"StartGameAsync failed: {ex}");
                throw;
            }
        }

        private async Task SubscribeToLobbyEventsAsync(string lobbyId)
        {
            // Unsubscribe from any existing subscription first
            await UnsubscribeFromLobbyEventsAsync();

            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += OnLobbyChanged;
            currentLobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobbyId, callbacks);
        }

        private async Task UnsubscribeFromLobbyEventsAsync()
        {
            if (currentLobbyEvents != null)
            {
                try
                {
                    await currentLobbyEvents.UnsubscribeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"UnsubscribeFromLobbyEventsAsync: {ex.Message}");
                }
                finally
                {
                    currentLobbyEvents = null;
                }
            }
        }

        private void OnLobbyChanged(ILobbyChanges changes)
        {
            changes.ApplyToLobby(currentLobby);

            var lobbyDataResult = MapLobbyToLobbyData(currentLobby);

            if (changes.Data.Changed || changes.IsPrivate.Changed || changes.HostId.Changed)
            {
                LobbyUpdated?.Invoke(lobbyDataResult);
            }

            // Fire player data updated for player joins, leaves, or data changes
            if (changes.PlayerJoined.Changed || changes.PlayerLeft.Changed || changes.PlayerData.Changed)
                LobbyPlayerDataUpdated?.Invoke(lobbyDataResult);
        }

        /// <summary>
        /// Maps a Unity Lobby object to the LobbyData abstraction.
        /// </summary>
        private LobbyData MapLobbyToLobbyData(Lobby lobby)
        {
            // Parse LobbyType from data
            var lobbyType = LobbyType.General; // Default for this implementation
            if (lobby.Data != null && lobby.Data.TryGetValue(LobbyDataKeys.LobbyType.ToString(), out var typeObj))
            {
                Enum.TryParse<LobbyType>(typeObj.Value, out lobbyType);
            }

            var result = new LobbyData
            {
                LobbyId = lobby.Id,
                LobbyName = lobby.Name,
                JoinCode = lobby.LobbyCode,
                IsPrivate = lobby.IsPrivate,
                HostId = lobby.HostId,
                MaxPlayers = lobby.MaxPlayers,
                LobbyType = lobbyType,
                // Ensure correct enum key usage for Data dictionary
                Data = lobby.Data != null
                    ? lobby.Data.ToDictionary(
                        kvp => Enum.TryParse<LobbyDataKeys>(kvp.Key, out var key) ? key : LobbyDataKeys.GameMode, // fallback to a default
                        kvp => kvp.Value?.Value ?? string.Empty)
                    : new Dictionary<LobbyDataKeys, string>(),
                Players = lobby.Players != null
                    ? lobby.Players.Select(p => new PlayerData
                    {
                        PlayerId = p.Id,
                        IsLobbyHost = p.Id == lobby.HostId,
                        IsLocalPlayer = p.Id == AuthenticationService.Instance.PlayerId,
                        // Map PlayerName if available
                        PlayerName = p.Data != null && p.Data.TryGetValue(nameof(PlayerDataKeys.PlayerName), out var nameObj)
                            ? nameObj?.Value ?? string.Empty
                            : string.Empty,
                        Data = p.Data != null
                            ? p.Data.ToDictionary(
                                kvp => Enum.TryParse<PlayerDataKeys>(kvp.Key, out var key) ? key : PlayerDataKeys.PlayerID, // fallback to a default
                                kvp => kvp.Value?.Value ?? string.Empty)
                            : new Dictionary<PlayerDataKeys, string>()
                    }).ToList()
                    : new List<PlayerData>()
            };
            return result;
        }
    }
}
#endif