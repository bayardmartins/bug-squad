using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manager-level interface for lobby operations.
    /// Any lobby system (Unity, Steam, custom) implements this so other
    /// managers and UI can call it without knowing the concrete type.
    /// </summary>
    public interface ILobbyService
    {
        /// <summary>Current lobby data, or null if not in a lobby.</summary>
        LobbyData CurrentLobbyData { get; }

        /// <summary>The type of lobby this service manages.</summary>
        LobbyType LobbyType { get; }

        Task<LobbyData> CreateLobby(string lobbyName, bool isPrivate, int maxPlayers, GameMode gameMode, string password = "");
        Task<LobbyData> JoinLobby(string lobbyId, string password = "");
        Task<LobbyData> JoinLobbyByCode(string joinCode, string password = "");
        Task<bool> LeaveLobby(LobbyData lobbyData = null);
        Task<LobbyData> UpdatePlayerData(IDictionary<PlayerDataKeys, string> playerData);
        Task<LobbyData> UpdateLobbyPrivacy(bool isPrivate);
        Task<LobbyData> UpdateLobbyData(IDictionary<LobbyDataKeys, string> lobbyData);
        Task<LobbyData> UpdateGameMode(GameMode gameMode);
        Task<LobbyData> UpdateCharacter(string characterId);
        Task<LobbyData> UpdateReadyState(string readyState);
        Task<LobbyData> KickPlayer(string playerId);
        Task<List<LobbyListData>> GetLobbyListAsync();
        Task StartGameAsync();
    }
}
