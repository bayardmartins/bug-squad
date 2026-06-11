using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides an abstraction for managing friends, friend requests, and lobby invites in a multiplayer environment.
    /// </summary>
    public interface IFriends
    {
        /// <summary>
        /// Raised when the friends list is updated.
        /// </summary>
        event Action<List<Friend>> OnFriendsListUpdated;

        /// <summary>
        /// Raised when a friend's data is updated.
        /// </summary>
        event Action<Friend> OnFriendDataUpdated;

        /// <summary>
        /// Raised when a friend's presence is updated.
        /// </summary>
        event Action<(string playerId, FriendPresence presence)> OnFriendPresenceUpdated;

        event Action<FriendPresence> OnLocalPlayerPresenceUpdated;

        /// <summary>
        /// Raised when a new friend is added.
        /// </summary>
        event Action<Friend> OnFriendAdded;

        /// <summary>
        /// Raised when a friend is removed.
        /// </summary>
        event Action<string> OnFriendRemoved;

        /// <summary>
        /// Raised when a new friend request is received.
        /// </summary>
        event Action<FriendRequest> OnFriendRequestReceived;

        /// <summary>
        /// Raised when a lobby invite is received.
        /// </summary>
        event Action<LobbyInvite> OnLobbyInviteReceived;

        /// <summary>
        /// Raised when someone accepts your join request (contains their lobby ID).
        /// </summary>
        event Action<LobbyInvite> OnJoinRequestAccepted;

        /// <summary>
        /// Sends a friend request to the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player to send a request to.</param>
        /// <returns>True if the request was sent successfully; otherwise, false.</returns>
        Task<bool> SendFriendRequest(string playerId);

        /// <summary>
        /// Cancels an outgoing friend request to the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be canceled.</param>
        /// <returns>True if the request was canceled successfully; otherwise, false.</returns>
        Task<bool> CancelOutgoingRequest(string playerId);

        /// <summary>
        /// Accepts a friend request from the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be accepted.</param>
        /// <returns>True if the request was accepted successfully; otherwise, false.</returns>
        Task<bool> AcceptFriendRequest(string playerId);

        /// <summary>
        /// Declines a friend request from the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be declined.</param>
        /// <returns>True if the request was declined successfully; otherwise, false.</returns>
        Task<bool> DeclineFriendRequest(string playerId);

        /// <summary>
        /// Initializes the friends system asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Removes a friend relationship with the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the friend to remove.</param>
        /// <returns>True if the friend was removed successfully; otherwise, false.</returns>
        Task<bool> Unfriend(string playerId);

        /// <summary>
        /// Retrieves the current list of friends asynchronously.
        /// </summary>
        /// <returns>A list of friends.</returns>
        Task<List<Friend>> GetFriendListAsync();

        /// <summary>
        /// Retrieves the current list of friend requests asynchronously.
        /// </summary>
        /// <returns>A list of friend requests.</returns>
        Task<List<FriendRequest>> GetFriendRequestsAsync();

        /// <summary>
        /// Sends a lobby invite to the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player to invite.</param>
        /// <param name="joinId">The join identifier for the lobby.</param>
        /// <returns>True if the invite was sent successfully; otherwise, false.</returns>
        Task<bool> SendInvite(string playerId, string joinId);

        /// <summary>
        /// Requests to join another player's lobby.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose lobby to join.</param>
        /// <returns>True if the join request was sent successfully; otherwise, false.</returns>
        Task<bool> AskToJoin(string playerId);

        /// <summary>
        /// Accepts a join request from another player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose join request to accept.</param>
        /// <param name="joinId">The join identifier for the lobby.</param>
        /// <returns>True if the join request was accepted successfully; otherwise, false.</returns>
        Task<bool> AcceptJoinRequest(string playerId, string joinId);

        Task SetPresence(FriendPresence presence);
    }
}