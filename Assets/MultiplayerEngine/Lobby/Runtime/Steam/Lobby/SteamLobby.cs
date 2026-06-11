#if STEAM_SERVICES
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Steam implementation of ILobby for multiplayer lobby management.
    /// </summary>
    public class SteamLobby : ILobby
    {
        public event Action<LobbyData> LobbyCreated;
        public event Action<LobbyData> LobbyJoined;
        public event Action LobbyLeft;
        public event Action<LobbyData> LobbyUpdated;
        public event Action<LobbyData> LobbyPlayerDataUpdated;
        public event Action<LobbyData> GameStarted;

        private CSteamID lobbyId;
        private LobbyData currentLobbyData;

        // Steam callback handles
        private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
        private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
        private Callback<LobbyChatMsg_t> lobbyChatMessageCallback;
        private Callback<LobbyMatchList_t> lobbyMatchListCallback;

        /// <summary>
        /// Initializes Steam lobby callbacks.
        /// </summary>
        public Task<bool> InitializeAsync()
        {
            try
            {
                lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
                lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SteamLobby initialization failed: {ex}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Creates a new Steam lobby.
        /// </summary>
        public async Task<LobbyData> CreateLobbyAsync(string lobbyName, bool isPrivate, int maxPlayers, IDictionary<LobbyDataKeys, string> lobbyData, IDictionary<PlayerDataKeys, string> playerData)
        {
            // Check if SteamManager is initialized
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[SteamLobby] CreateLobbyAsync failed: SteamManager is not initialized!");
                return null;
            }

            var tcs = new TaskCompletionSource<LobbyData>();
            Callback<LobbyCreated_t> lobbyCreatedCallback = null;

            try
            {
                lobbyCreatedCallback = Callback<LobbyCreated_t>.Create((LobbyCreated_t callback) =>
                {
                    if (callback.m_eResult == EResult.k_EResultOK)
                    {
                        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

                        // Set lobby name first (not part of LobbyDataKeys enum, stored as plain string)
                        SteamMatchmaking.SetLobbyData(lobbyId, "lobbyName", lobbyName);
                        SetLobbyData(lobbyId, lobbyData);
                        SetLobbyMemberData(lobbyId, playerData);

                        currentLobbyData = BuildLobbyData(lobbyId);

                        LobbyCreated?.Invoke(currentLobbyData);
                        tcs.TrySetResult(currentLobbyData);
                    }
                    else
                    {
                        Debug.LogError($"[SteamLobby] Failed to create lobby: {callback.m_eResult}");
                        tcs.TrySetResult(null);
                    }
                    lobbyCreatedCallback?.Dispose();
                });

                SteamMatchmaking.CreateLobby(isPrivate ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypePublic, maxPlayers);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamLobby] Exception during CreateLobbyAsync: {ex}");
                tcs.TrySetResult(null);
                lobbyCreatedCallback?.Dispose();
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Joins a lobby by code.
        /// </summary>
        public Task<LobbyData> JoinLobbyByCodeAsync(string joinCode, IDictionary<PlayerDataKeys, string> playerData)
        {
            return JoinLobbyAsync(joinCode, playerData);
        }

        /// <summary>
        /// Joins a lobby.
        /// </summary>
        public Task<LobbyData> JoinLobbyAsync(string joinCode, IDictionary<PlayerDataKeys, string> playerData)
        {
            var tcs = new TaskCompletionSource<LobbyData>();
            lobbyId = new CSteamID(ulong.Parse(joinCode));
            Callback<LobbyEnter_t> lobbyEnterCallback = null;

            try
            {
                lobbyEnterCallback = Callback<LobbyEnter_t>.Create((LobbyEnter_t callback) =>
                {
                    CSteamID enteredLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

                    if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    {
                        SetLobbyMemberData(lobbyId, playerData);
                        currentLobbyData = BuildLobbyData(lobbyId);

                        LobbyJoined?.Invoke(currentLobbyData);
                        tcs.SetResult(currentLobbyData);
                    }
                    else
                    {
                        Debug.LogError($"Failed to join lobby: {callback.m_EChatRoomEnterResponse}");
                        tcs.SetResult(null);
                    }
                    lobbyEnterCallback.Dispose();
                });

                SteamMatchmaking.JoinLobby(lobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during JoinLobbyAsync: {ex}");
                tcs.SetResult(null);
                lobbyEnterCallback?.Dispose();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Leaves the current lobby.
        /// </summary>
        public Task<bool> LeaveLobbyAsync()
        {
            try
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                LobbyLeft?.Invoke();

                lobbyDataUpdateCallback?.Dispose();
                lobbyChatUpdateCallback?.Dispose();
                lobbyChatMessageCallback?.Dispose();

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during LeaveLobbyAsync: {ex}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Updates lobby metadata.
        /// </summary>
        public Task<LobbyData> UpdateLobbyDataAsync(IDictionary<LobbyDataKeys, string> lobbyData)
        {
            if (lobbyId == CSteamID.Nil || currentLobbyData == null)
                return Task.FromResult<LobbyData>(null);

            SetLobbyData(lobbyId, lobbyData);
            currentLobbyData.Data = GetLobbyData(lobbyId);
            return Task.FromResult(currentLobbyData);
        }

        /// <summary>
        /// Updates local player data in the lobby.
        /// </summary>
        public Task<LobbyData> UpdatePlayerDataAsync(IDictionary<PlayerDataKeys, string> playerData)
        {
            if (lobbyId == CSteamID.Nil || currentLobbyData == null)
                return Task.FromResult<LobbyData>(null);

            SetLobbyMemberData(lobbyId, playerData);

            var localPlayerId = SteamUser.GetSteamID().ToString();
            var localPlayer = currentLobbyData?.Players?.FirstOrDefault(p => p.PlayerId == localPlayerId);

            if (localPlayer != null)
            {
                foreach (var kvp in playerData)
                {
                    string value = SteamMatchmaking.GetLobbyMemberData(lobbyId, SteamUser.GetSteamID(), kvp.Key.ToString());
                    if (!string.IsNullOrEmpty(value))
                    {
                        localPlayer.Data[kvp.Key] = value;
                    }
                }
            }

            return Task.FromResult(currentLobbyData);
        }

        /// <summary>
        /// Updates lobby privacy.
        /// </summary>
        public Task<LobbyData> UpdateLobbyPrivacyAsync(bool isPrivate)
        {
            if (lobbyId == CSteamID.Nil || currentLobbyData == null)
                return Task.FromResult<LobbyData>(null);

            SteamMatchmaking.SetLobbyType(lobbyId, isPrivate ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypePublic);
            currentLobbyData.IsPrivate = isPrivate;
            return Task.FromResult(currentLobbyData);
        }

        /// <summary>
        /// Starts the game from the lobby.
        /// </summary>
        public Task StartGameAsync()
        {
            if (lobbyId == CSteamID.Nil)
                return Task.FromResult(false);

            SteamMatchmaking.SetLobbyJoinable(lobbyId, false);
            NetworkManager.Singleton.StartHost();
            SteamMatchmaking.SetLobbyData(lobbyId, LobbyDataKeys.GameId.ToString(), SteamUser.GetSteamID().ToString());
            GameStarted?.Invoke(currentLobbyData);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Not implemented: Kick a player from the lobby.
        /// </summary>
        public Task<LobbyData> KickPlayerAsync(string playerId)
        {
            Debug.LogError("KickPlayerAsync is not implemented for SteamLobby.");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a list of available lobbies.
        /// </summary>
        public Task<List<LobbyListData>> GetLobbyListAsync()
        {
            var tcs = new TaskCompletionSource<List<LobbyListData>>();

            try
            {
                lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
                {
                    var lobbyList = new List<LobbyListData>();
                    for (int i = 0; i < callback.m_nLobbiesMatching; i++)
                    {
                        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                        string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyDataKeys.GameMode.ToString());
                        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                        int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
                        string gameMode = SteamMatchmaking.GetLobbyData(lobbyId, LobbyDataKeys.GameMode.ToString());

                        lobbyList.Add(new LobbyListData
                        {
                            lobbyId = lobbyId.ToString(),
                            lobbyName = lobbyName,
                            currentPlayers = currentPlayers,
                            maxPlayers = maxPlayers,
                            gameMode = gameMode,
                            lobbyType = LobbyType.General
                        });
                    }

                    tcs.SetResult(lobbyList);
                    lobbyMatchListCallback.Dispose();
                });

                SteamMatchmaking.RequestLobbyList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during GetLobbyListAsync: {ex}");
                tcs.SetResult(new List<LobbyListData>());
                lobbyMatchListCallback?.Dispose();
            }

            return tcs.Task;
        }

        private void SetLobbyData(CSteamID lobbyId, IDictionary<LobbyDataKeys, string> data)
        {
            // Set lobby data for all keys in the dictionary
            foreach (var kvp in data)
            {
                SteamMatchmaking.SetLobbyData(lobbyId, kvp.Key.ToString(), kvp.Value);
            }
        }

        private void SetLobbyMemberData(CSteamID lobbyId, IDictionary<PlayerDataKeys, string> data)
        {
            // Set member data for all keys in the dictionary
            foreach (var kvp in data)
            {
                SteamMatchmaking.SetLobbyMemberData(lobbyId, kvp.Key.ToString(), kvp.Value);
            }
        }

        private Dictionary<LobbyDataKeys, string> GetLobbyData(CSteamID lobbyId)
        {
            var lobbyData = Enum.GetValues(typeof(LobbyDataKeys))
                .Cast<LobbyDataKeys>().ToDictionary(key => key, key => string.Empty);

            // Iterate over a copy of the keys to avoid modifying during enumeration
            foreach (var key in lobbyData.Keys.ToList())
            {
                string lobbyDataValue = SteamMatchmaking.GetLobbyData(lobbyId, key.ToString());
                if (!string.IsNullOrEmpty(lobbyDataValue))
                {
                    lobbyData[key] = lobbyDataValue;
                }
            }

            return lobbyData;
        }

        private PlayerData GetPlayerData(CSteamID lobbyId, CSteamID memberId)
        {
            var playerData = Enum.GetValues(typeof(PlayerDataKeys))
                .Cast<PlayerDataKeys>().ToDictionary(key => key, key => string.Empty);

            foreach (var key in playerData.Keys.ToList())
            {
                string value = SteamMatchmaking.GetLobbyMemberData(lobbyId, memberId, key.ToString());
                if (!string.IsNullOrEmpty(value))
                {
                    playerData[key] = value;
                }
            }

            return new PlayerData
            {
                PlayerId = memberId.ToString(),
                IsLobbyHost = SteamMatchmaking.GetLobbyOwner(lobbyId) == memberId,
                IsLocalPlayer = memberId == SteamUser.GetSteamID(),
                PlayerName = Steamworks.SteamFriends.GetFriendPersonaName(memberId),
                Data = playerData
            };
        }

        private LobbyData BuildLobbyData(CSteamID lobbyId)
        {
            var data = GetLobbyData(lobbyId);

            // Parse lobby type
            var lobbyType = LobbyType.General;
            if (data.TryGetValue(LobbyDataKeys.LobbyType, out var typeStr) && !string.IsNullOrEmpty(typeStr))
            {
                Enum.TryParse<LobbyType>(typeStr, out lobbyType);
            }

            var lobbyData = new LobbyData
            {
                LobbyId = lobbyId.ToString(),
                LobbyName = SteamMatchmaking.GetLobbyData(lobbyId, "lobbyName"),
                IsPrivate = true,
                JoinCode = lobbyId.ToString(),
                HostId = SteamMatchmaking.GetLobbyOwner(lobbyId).ToString(),
                MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId),
                LobbyType = lobbyType,
                Data = data,
                Players = new List<PlayerData>()
            };

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            for (int i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                lobbyData.Players.Add(GetPlayerData(lobbyId, memberId));
            }

            return lobbyData;
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            CSteamID updatedLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID updatedMemberID = new CSteamID(callback.m_ulSteamIDMember);

            if (currentLobbyData == null) return;

            if (updatedMemberID == updatedLobbyID)
            {
                currentLobbyData.JoinCode = lobbyId.ToString();
                currentLobbyData.LobbyName = SteamMatchmaking.GetLobbyData(updatedLobbyID, "lobbyName");
                currentLobbyData.IsPrivate = true;  // Steamworks does not provide a direct way to get lobby privacy status
                currentLobbyData.LobbyId = lobbyId.ToString();
                currentLobbyData.HostId = SteamMatchmaking.GetLobbyOwner(lobbyId).ToString();
                currentLobbyData.MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
                currentLobbyData.Data = GetLobbyData(lobbyId);
                LobbyUpdated?.Invoke(currentLobbyData);
            }
            else
            {
                var existingPlayer = currentLobbyData.Players.FirstOrDefault(p => p.PlayerId == updatedMemberID.ToString());

                if (existingPlayer != null)
                {
                    var updatedPlayer = GetPlayerData(lobbyId, updatedMemberID);
                    int idx = currentLobbyData.Players.IndexOf(existingPlayer);
                    currentLobbyData.Players[idx] = updatedPlayer;
                }
                else
                {
                    currentLobbyData.Players.Add(GetPlayerData(lobbyId, updatedMemberID));
                }

                LobbyPlayerDataUpdated?.Invoke(currentLobbyData);
            }
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID changedUser = new CSteamID(callback.m_ulSteamIDUserChanged);
        }
    }
}
#endif