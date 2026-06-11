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
    /// Steam implementation of ILobby for server-based persistent lobbies.
    /// The lobby remains joinable while the game is running (late-join support).
    /// </summary>
    public class SteamServerLobby : ILobby
    {
        public event Action<LobbyData> LobbyCreated;
        public event Action<LobbyData> LobbyJoined;
        public event Action LobbyLeft;
        public event Action<LobbyData> LobbyUpdated;
        public event Action<LobbyData> LobbyPlayerDataUpdated;
        public event Action<LobbyData> GameStarted;

        private CSteamID lobbyId;
        private LobbyData currentLobbyData;

        private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
        private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
        private Callback<LobbyMatchList_t> lobbyMatchListCallback;

        /// <summary>
        /// Initializes Steam lobby callbacks for server-based lobbies.
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
                Debug.LogError($"[SteamServerLobby] Initialization failed: {ex}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Creates a new Steam server lobby.
        /// </summary>
        public async Task<LobbyData> CreateLobbyAsync(string lobbyName, bool isPrivate, int maxPlayers, IDictionary<LobbyDataKeys, string> lobbyData, IDictionary<PlayerDataKeys, string> playerData)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[SteamServerLobby] CreateLobbyAsync failed: SteamManager is not initialized!");
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

                        SteamMatchmaking.SetLobbyData(lobbyId, "lobbyName", lobbyName);
                        SetLobbyData(lobbyId, lobbyData);
                        SetLobbyMemberData(lobbyId, playerData);

                        currentLobbyData = BuildLobbyData(lobbyId);

                        LobbyCreated?.Invoke(currentLobbyData);
                        tcs.TrySetResult(currentLobbyData);
                    }
                    else
                    {
                        Debug.LogError($"[SteamServerLobby] Failed to create lobby: {callback.m_eResult}");
                        tcs.TrySetResult(null);
                    }
                    lobbyCreatedCallback?.Dispose();
                });

                SteamMatchmaking.CreateLobby(isPrivate ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypePublic, maxPlayers);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamServerLobby] Exception during CreateLobbyAsync: {ex}");
                tcs.TrySetResult(null);
                lobbyCreatedCallback?.Dispose();
            }

            return await tcs.Task;
        }

        /// <summary>
        /// Joins a lobby by code (same as JoinLobbyAsync for Steam).
        /// </summary>
        public Task<LobbyData> JoinLobbyByCodeAsync(string joinCode, IDictionary<PlayerDataKeys, string> playerData)
        {
            return JoinLobbyAsync(joinCode, playerData);
        }

        /// <summary>
        /// Joins a server lobby.
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
                    if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    {
                        SetLobbyMemberData(lobbyId, playerData);
                        currentLobbyData = BuildLobbyData(lobbyId);

                        LobbyJoined?.Invoke(currentLobbyData);
                        tcs.SetResult(currentLobbyData);
                    }
                    else
                    {
                        Debug.LogError($"[SteamServerLobby] Failed to join lobby: {callback.m_EChatRoomEnterResponse}");
                        tcs.SetResult(null);
                    }
                    lobbyEnterCallback.Dispose();
                });

                SteamMatchmaking.JoinLobby(lobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamServerLobby] Exception during JoinLobbyAsync: {ex}");
                tcs.SetResult(null);
                lobbyEnterCallback?.Dispose();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Leaves the server lobby.
        /// </summary>
        public Task<bool> LeaveLobbyAsync()
        {
            try
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                LobbyLeft?.Invoke();

                lobbyDataUpdateCallback?.Dispose();
                lobbyChatUpdateCallback?.Dispose();

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamServerLobby] Exception during LeaveLobbyAsync: {ex}");
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
        /// Starts the game. Unlike the General lobby, the server-based lobby
        /// stays joinable (no SetLobbyJoinable(false)) for late joiners.
        /// </summary>
        public Task StartGameAsync()
        {
            if (lobbyId == CSteamID.Nil)
                return Task.FromResult(false);

            // Server-based: do NOT set lobby as unjoinable (allow late joiners)
            NetworkManager.Singleton.StartHost();
            SteamMatchmaking.SetLobbyData(lobbyId, LobbyDataKeys.GameId.ToString(), SteamUser.GetSteamID().ToString());
            GameStarted?.Invoke(currentLobbyData);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Kicks a player from the lobby.
        /// </summary>
        public Task<LobbyData> KickPlayerAsync(string playerId)
        {
            Debug.LogError("[SteamServerLobby] KickPlayerAsync is not implemented for Steam.");
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
                        CSteamID lid = SteamMatchmaking.GetLobbyByIndex(i);
                        string name = SteamMatchmaking.GetLobbyData(lid, "lobbyName");
                        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lid);
                        int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lid);
                        string gameMode = SteamMatchmaking.GetLobbyData(lid, LobbyDataKeys.GameMode.ToString());

                        // Parse lobby type
                        string lobbyTypeStr = SteamMatchmaking.GetLobbyData(lid, LobbyDataKeys.LobbyType.ToString());
                        Enum.TryParse<LobbyType>(lobbyTypeStr, out var parsedType);

                        lobbyList.Add(new LobbyListData
                        {
                            lobbyId = lid.ToString(),
                            lobbyName = name,
                            currentPlayers = currentPlayers,
                            maxPlayers = maxPlayers,
                            gameMode = gameMode,
                            lobbyType = parsedType
                        });
                    }

                    tcs.SetResult(lobbyList);
                    lobbyMatchListCallback.Dispose();
                });

                SteamMatchmaking.RequestLobbyList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SteamServerLobby] Exception during GetLobbyListAsync: {ex}");
                tcs.SetResult(new List<LobbyListData>());
                lobbyMatchListCallback?.Dispose();
            }

            return tcs.Task;
        }

        // ─── Helpers ───

        private void SetLobbyData(CSteamID lid, IDictionary<LobbyDataKeys, string> data)
        {
            foreach (var kvp in data)
                SteamMatchmaking.SetLobbyData(lid, kvp.Key.ToString(), kvp.Value);
        }

        private void SetLobbyMemberData(CSteamID lid, IDictionary<PlayerDataKeys, string> data)
        {
            foreach (var kvp in data)
                SteamMatchmaking.SetLobbyMemberData(lid, kvp.Key.ToString(), kvp.Value);
        }

        private Dictionary<LobbyDataKeys, string> GetLobbyData(CSteamID lid)
        {
            var data = Enum.GetValues(typeof(LobbyDataKeys))
                .Cast<LobbyDataKeys>().ToDictionary(key => key, key => string.Empty);

            foreach (var key in data.Keys.ToList())
            {
                string val = SteamMatchmaking.GetLobbyData(lid, key.ToString());
                if (!string.IsNullOrEmpty(val))
                    data[key] = val;
            }
            return data;
        }

        private PlayerData GetPlayerData(CSteamID lid, CSteamID memberId)
        {
            var data = Enum.GetValues(typeof(PlayerDataKeys))
                .Cast<PlayerDataKeys>().ToDictionary(key => key, key => string.Empty);

            foreach (var key in data.Keys.ToList())
            {
                string value = SteamMatchmaking.GetLobbyMemberData(lid, memberId, key.ToString());
                if (!string.IsNullOrEmpty(value))
                    data[key] = value;
            }

            return new PlayerData
            {
                PlayerId = memberId.ToString(),
                IsLobbyHost = SteamMatchmaking.GetLobbyOwner(lid) == memberId,
                IsLocalPlayer = memberId == SteamUser.GetSteamID(),
                PlayerName = Steamworks.SteamFriends.GetFriendPersonaName(memberId),
                Data = data
            };
        }

        private LobbyData BuildLobbyData(CSteamID lid)
        {
            var data = GetLobbyData(lid);

            // Parse lobby type
            var lobbyType = LobbyType.ServerBased;
            if (data.TryGetValue(LobbyDataKeys.LobbyType, out var typeStr) && !string.IsNullOrEmpty(typeStr))
            {
                Enum.TryParse<LobbyType>(typeStr, out lobbyType);
            }

            var lobbyData = new LobbyData
            {
                LobbyId = lid.ToString(),
                LobbyName = SteamMatchmaking.GetLobbyData(lid, "lobbyName"),
                IsPrivate = true,
                JoinCode = lid.ToString(),
                HostId = SteamMatchmaking.GetLobbyOwner(lid).ToString(),
                MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lid),
                LobbyType = lobbyType,
                Data = data,
                Players = new List<PlayerData>()
            };

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lid);
            for (int i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(lid, i);
                lobbyData.Players.Add(GetPlayerData(lid, memberId));
            }

            return lobbyData;
        }

        // ─── Callbacks ───

        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            CSteamID updatedLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID updatedMemberID = new CSteamID(callback.m_ulSteamIDMember);

            if (currentLobbyData == null) return;

            if (updatedMemberID == updatedLobbyID)
            {
                currentLobbyData.JoinCode = lobbyId.ToString();
                currentLobbyData.LobbyName = SteamMatchmaking.GetLobbyData(updatedLobbyID, "lobbyName");
                currentLobbyData.IsPrivate = true;
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
            // Can handle player join/leave chat notifications here if needed
        }
    }
}
#endif
