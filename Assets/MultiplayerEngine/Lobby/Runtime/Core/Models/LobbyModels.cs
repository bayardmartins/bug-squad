using System.Collections.Generic;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents the data for a multiplayer lobby.
    /// </summary>
    public class LobbyData
    {
        public string LobbyId;
        public string JoinCode;
        public string LobbyName;
        public bool IsPrivate;
        public string HostId;
        public int MaxPlayers;
        public LobbyType LobbyType;
        public IDictionary<LobbyDataKeys, string> Data;
        public IList<PlayerData> Players;
    }

    /// <summary>
    /// Represents the data for a player in a lobby.
    /// </summary>
    public class PlayerData
    {
        public string PlayerId;
        public bool IsLobbyHost;
        public string PlayerName;
        public bool IsLocalPlayer;
        public IDictionary<PlayerDataKeys, string> Data;
    }

    /// <summary>
    /// Represents lobby list data for UI or matchmaking.
    /// </summary>
    public class LobbyListData
    {
        /// <summary>
        /// The name of the lobby.
        /// </summary>
        public string lobbyName;

        /// <summary>
        /// The unique identifier of the lobby.
        /// </summary>
        public string lobbyId;

        /// <summary>
        /// The current number of players in the lobby.
        /// </summary>
        public int currentPlayers;

        /// <summary>
        /// The maximum number of players allowed in the lobby.
        /// </summary>
        public int maxPlayers;

        /// <summary>
        /// The game mode of the lobby.
        /// </summary>
        public string gameMode;

        /// <summary>
        /// Whether the lobby requires a password to join.
        /// </summary>
        public bool isLocked;

        /// <summary>
        /// The type of lobby (General or ServerBased).
        /// </summary>
        public LobbyType lobbyType;
    }

    /// <summary>
    /// Keys for lobby-level metadata stored in the lobby.
    /// </summary>
    public enum LobbyDataKeys
    {
        GameMode,
        GameId,  // the ID use to connect to the game Host (In Unity : Relay allocation ID, in Steam : Host player ID)
        Password, // for locked lobbies
        LobbyType // General or ServerBased
    }

    /// <summary>
    /// Keys for player-specific metadata stored in the lobby.
    /// </summary>
    public enum PlayerDataKeys
    {
        PlayerName,
        PlayerCharacter,
        PlayerReady,
        PlayerID
    }

    /// <summary>
    /// Supported game modes for the lobby.
    /// </summary>
    public enum GameMode
    {
        FreeForAll,
        TeamDeathmatch,
        CaptureTheFlag
    }

    /// <summary>
    /// Defines the type of lobby system.
    /// </summary>
    public enum LobbyType
    {
        /// <summary>
        /// Classic lobby: character selection, ready check, host starts game, lobby destroyed after game.
        /// </summary>
        General,

        /// <summary>
        /// Persistent server: players join/leave freely, no ready check, late-join mid-game, lobby persists.
        /// </summary>
        ServerBased
    }
}
