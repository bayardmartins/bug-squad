using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents a friend in the multiplayer system.
    /// </summary>
    public class Friend
    {
        /// <summary>
        /// The display name of the friend.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// The unique player ID of the friend.
        /// </summary>
        public string PlayerId;

        /// <summary>
        /// The current presence status of the friend.
        /// </summary>
        public FriendPresence Presence;

        /// <summary>
        /// The avatar image of the friend.
        /// </summary>
        public Sprite Avatar;
    }

    /// <summary>
    /// Represents a friend request in the multiplayer system.
    /// </summary>
    public class FriendRequest
    {
        /// <summary>
        /// The display name of the player who sent or received the request.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// The unique player ID of the request sender or receiver.
        /// </summary>
        public string PlayerId;

        /// <summary>
        /// The avatar image of the player.
        /// </summary>
        public Sprite Avatar;

        /// <summary>
        /// The type of the friend request (incoming or outgoing).
        /// </summary>
        public FriendRequestType RequestType;
    }

    /// <summary>
    /// Represents a lobby invite in the multiplayer system.
    /// </summary>
    public class LobbyInvite
    {
        /// <summary>
        /// The player ID of the sender.
        /// </summary>
        public string FromPlayerId;

        /// <summary>
        /// The display name of the sender.
        /// </summary>
        public string FromPlayerName;

        /// <summary>
        /// The lobby ID for the invite.
        /// </summary>
        public string LobbyId;

        /// <summary>
        /// The avatar image of the sender.
        /// </summary>
        public Sprite FromAvatar;

        /// <summary>
        /// The type of the invite.
        /// </summary>
        public InviteType InviteType;
    }

    /// <summary>
    /// Specifies the type of a lobby invite.
    /// </summary>
    public enum InviteType
    {
        /// <summary>
        /// Standard invite to join a lobby.
        /// </summary>
        Invite,

        /// <summary>
        /// Request to join a lobby.
        /// </summary>
        RequestToJoin,

        /// <summary>
        /// Accepted join request.
        /// </summary>
        AcceptedRequest
    }

    /// <summary>
    /// Specifies the type of a friend request.
    /// </summary>
    public enum FriendRequestType
    {
        /// <summary>
        /// Incoming friend request.
        /// </summary>
        Incoming,

        /// <summary>
        /// Outgoing friend request.
        /// </summary>
        Outgoing
    }

    /// <summary>
    /// Specifies the presence status of a friend.
    /// </summary>
    public enum FriendPresence
    {
        /// <summary>
        /// The friend is away.
        /// </summary>
        Away,

        /// <summary>
        /// The friend is online.
        /// </summary>
        Online,

        /// <summary>
        /// The friend is offline.
        /// </summary>
        Offline,

        /// <summary>
        /// The friend is in a game.
        /// </summary>
        InGame,

        /// <summary>
        /// The friend is in Lobby.
        /// </summary>
        InLobby
    }
}
