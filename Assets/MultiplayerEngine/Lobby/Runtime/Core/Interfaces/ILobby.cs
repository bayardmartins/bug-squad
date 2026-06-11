using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides an abstraction for lobby management in a multiplayer environment.
    /// Supports creating, joining, leaving, and updating lobbies and player data.
    /// </summary>
    public interface ILobby
    {
        // Lobby lifecycle events
        event Action<LobbyData> LobbyCreated;
        event Action<LobbyData> LobbyJoined;
        event Action LobbyLeft;
        event Action<LobbyData> LobbyUpdated;
        event Action<LobbyData> LobbyPlayerDataUpdated;
        event Action<LobbyData> GameStarted;

        // Initialization
        /// <summary>
        /// Initializes the lobby system asynchronously.
        /// </summary>
        /// <returns>True if initialization succeeded; otherwise, false.</returns>
        Task<bool> InitializeAsync();

        // Lobby creation and joining
        /// <summary>
        /// Creates a new lobby with the specified parameters.
        /// </summary>
        Task<LobbyData> CreateLobbyAsync(string lobbyName, bool isPrivate, int maxPlayers, IDictionary<LobbyDataKeys, string> lobbyData, IDictionary<PlayerDataKeys, string> playerData);

        /// <summary>
        /// Joins an existing lobby using a join code.
        /// </summary>
        Task<LobbyData> JoinLobbyByCodeAsync(string joinCode, IDictionary<PlayerDataKeys, string> playerData);

        /// <summary>
        /// Joins an existing lobby using a lobby ID.
        /// </summary>
        Task<LobbyData> JoinLobbyAsync(string lobbyId, IDictionary<PlayerDataKeys, string> playerData);

        /// <summary>
        /// Leaves the currently joined lobby.
        /// </summary>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> LeaveLobbyAsync();

        /// <summary>
        /// Starts the game from the lobby.
        /// </summary>
        Task StartGameAsync();

        // Data updates
        /// <summary>
        /// Updates the current player's data in the lobby.
        /// </summary>
        Task<LobbyData> UpdatePlayerDataAsync(IDictionary<PlayerDataKeys, string> playerData);

        /// <summary>
        /// Updates the lobby's metadata.
        /// </summary>
        Task<LobbyData> UpdateLobbyDataAsync(IDictionary<LobbyDataKeys, string> lobbyData);

        /// <summary>
        /// Updates the lobby's privacy setting.
        /// </summary>
        Task<LobbyData> UpdateLobbyPrivacyAsync(bool isPrivate);

        // Queries
        /// <summary>
        /// Returns a list of available lobbies.
        /// </summary>
        Task<List<LobbyListData>> GetLobbyListAsync();

        // Admin actions
        /// <summary>
        /// Kicks a player from the lobby.
        /// </summary>
        Task<LobbyData> KickPlayerAsync(string playerId);
    }
}