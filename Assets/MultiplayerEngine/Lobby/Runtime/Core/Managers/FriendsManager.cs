using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages friends, friend requests, and lobby invites for multiplayer functionality.
    /// Provides methods to interact with the friends system and raises events for UI updates.
    /// </summary>
    public class FriendsManager : MonoBehaviour
    {

        /// <summary>
        /// Singleton instance of the FriendsManager.
        /// </summary>
        public static FriendsManager Instance { get; private set; }

        /// <summary>
        /// Raised when the friends list is updated.
        /// </summary>
        public static event System.Action<List<Friend>> OnFriendsListUpdated;

        /// <summary>
        /// Raised when a friend's data is updated.
        /// </summary>
        public static event System.Action<Friend> OnFriendDataUpdated;

        /// <summary>
        /// Raised when a friend's presence is updated.
        /// </summary>
        public static event System.Action<(string friendId, FriendPresence presence)> OnFriendPresenceUpdated;

        /// <summary>
        /// Raised when a lobby invite is received.
        /// </summary>
        public static event System.Action<LobbyInvite> OnLobbyInviteReceived;

        /// <summary>
        /// Raised when localPlayer's presence is updated.
        /// </summary>
        public static event System.Action<FriendPresence> OnLocalPlayerPresenceUpdated;

        public static event System.Action<FriendRequest> OnFriendRequestReceived;

        /// <summary>
        /// Raised when friend requests list is updated (after accept/decline/cancel).
        /// </summary>
        public static event System.Action OnFriendRequestsUpdated;

        /// <summary>
        /// Raised when friends system authentication/initialization completes.
        /// </summary>
        public static event System.Action OnFriendsAuthenticated;

        public static event System.Action<Friend> OnFriendAdded;

        /// <summary>
        /// Raised when someone accepts your join request (contains their lobby ID).
        /// </summary>
        public static event System.Action<LobbyInvite> OnJoinRequestAccepted;

        public List<Friend> FriendsList { get; private set; } = new List<Friend>();

        /// <summary>
        /// Indicates if the friends service has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        private IFriends friendsService;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_SERVICES
            friendsService = new UnityFriends();
#elif STEAM_SERVICES
            friendsService = new SteamFriends();
#endif

            // Register friends service with ServiceLocator
            ServiceLocator.Register<IFriends>(friendsService);

            AuthenticationManager.OnProfileSetupCompleted += OnSignInCompletedHandler;

            // Subscribe to decoupled presence update events from LobbyManager
            LobbyManagerBase.OnPresenceUpdateRequested += HandlePresenceUpdateRequest;

            friendsService.OnFriendsListUpdated += (list) =>
            {
                FriendsList = list;
                OnFriendsListUpdated?.Invoke(list);
            };

            friendsService.OnFriendDataUpdated += (friend) =>
            {
                // Update the specific friend in FriendsList
                var index = FriendsList.FindIndex(f => f.PlayerId == friend.PlayerId);
                if (index >= 0)
                {
                    FriendsList[index] = friend;
                }
                else
                {
                    FriendsList.Add(friend);
                }
                OnFriendDataUpdated?.Invoke(friend);
            };

            friendsService.OnFriendPresenceUpdated += (data) =>
            {
                // Update presence for the specific friend
                var (friendId, presence) = data;
                var friend = FriendsList.Find(f => f.PlayerId == friendId);
                if (friend != null)
                {
                    friend.Presence = presence;
                }
                OnFriendPresenceUpdated?.Invoke(data);
            };
            friendsService.OnLobbyInviteReceived += (invite) => OnLobbyInviteReceived?.Invoke(invite);
            friendsService.OnFriendRequestReceived += (request) => OnFriendRequestReceived?.Invoke(request);
            friendsService.OnLocalPlayerPresenceUpdated += (presence) => OnLocalPlayerPresenceUpdated?.Invoke(presence);
            friendsService.OnFriendAdded += (friend) => OnFriendAdded?.Invoke(friend);
            friendsService.OnJoinRequestAccepted += HandleJoinRequestAccepted;
        }

        private async void OnSignInCompletedHandler(bool success)
        {
            if (!success) return;

            await friendsService.InitializeAsync();
            IsInitialized = true;
            OnFriendsAuthenticated?.Invoke();
        }

        /// <summary>
        /// Handles presence update requests from other managers (decoupled pattern).
        /// </summary>
        private async void HandlePresenceUpdateRequest(PresenceUpdateRequest request)
        {
            if (friendsService == null) return;
            await friendsService.SetPresence(request.RequestedPresence);
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            LobbyManagerBase.OnPresenceUpdateRequested -= HandlePresenceUpdateRequest;
            AuthenticationManager.OnProfileSetupCompleted -= OnSignInCompletedHandler;

            // Unregister from ServiceLocator
            ServiceLocator.Unregister<IFriends>();
        }

        /// <summary>
        /// Retrieves the current list of friends asynchronously.
        /// </summary>
        /// <returns>A list of friends.</returns>
        public async Task<List<Friend>> GetFriendsList()
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return new List<Friend>();
            }
            FriendsList = await friendsService.GetFriendListAsync();
            return FriendsList;
        }

        /// <summary>
        /// Retrieves the current list of friend requests asynchronously.
        /// </summary>
        /// <returns>A list of friend requests.</returns>
        public async Task<List<FriendRequest>> GetFriendRequests()
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return new List<FriendRequest>();
            }
            return await friendsService.GetFriendRequestsAsync();
        }

        /// <summary>
        /// Sends a friend request to the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player to send a request to.</param>
        /// <returns>True if the request was sent successfully; otherwise, false.</returns>
        public async Task<bool> SendFriendRequest(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.SendFriendRequest(playerId);
        }

        /// <summary>
        /// Cancels an outgoing friend request to the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be canceled.</param>
        /// <returns>True if the request was canceled successfully; otherwise, false.</returns>
        public async Task<bool> CancelOutgoingRequest(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.CancelOutgoingRequest(playerId);
        }

        /// <summary>
        /// Accepts a friend request from the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be accepted.</param>
        /// <returns>True if the request was accepted successfully; otherwise, false.</returns>
        public async Task<bool> AcceptFriendRequest(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.AcceptFriendRequest(playerId);
        }

        /// <summary>
        /// Declines a friend request from the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player whose request should be declined.</param>
        /// <returns>True if the request was declined successfully; otherwise, false.</returns>
        public async Task<bool> DeclineFriendRequest(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.DeclineFriendRequest(playerId);
        }

        /// <summary>
        /// Removes a friend relationship with the specified player.
        /// </summary>
        /// <param name="playerId">The unique identifier of the friend to remove.</param>
        /// <returns>True if the friend was removed successfully; otherwise, false.</returns>
        public async Task<bool> Unfriend(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.Unfriend(playerId);
        }

        /// <summary>
        /// Invites a player to the current lobby.
        /// </summary>
        /// <param name="playerId">The player to invite.</param>
        /// <param name="lobbyId">The lobby ID to invite to. If null, attempts to get from LobbyManager.</param>
        /// <returns>True if invite was sent successfully.</returns>
        public async Task<bool> InviteToGame(string playerId, string lobbyId = null)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }

            // Read lobby ID from RuntimeSessionData
            if (string.IsNullOrEmpty(lobbyId))
            {
                if (RuntimeSessionData.Exists && !string.IsNullOrEmpty(RuntimeSessionData.Instance.CurrentLobbyId))
                {
                    lobbyId = RuntimeSessionData.Instance.CurrentLobbyId;
                }
                else
                {
                    Debug.LogError("No lobby ID provided and no active lobby in session data.");
                    return false;
                }
            }

            return await friendsService.SendInvite(playerId, lobbyId);
        }

        public async Task SetPresence(FriendPresence presence)
        {
            await friendsService.SetPresence(presence);
        }

        /// <summary>
        /// Requests to join a friend's lobby.
        /// </summary>
        /// <param name="playerId">The player to request to join.</param>
        /// <returns>True if request was sent successfully.</returns>
        public async Task<bool> AskToJoin(string playerId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.AskToJoin(playerId);
        }

        /// <summary>
        /// Accepts a join request from a player and sends them our lobby ID.
        /// </summary>
        public async Task<bool> AcceptJoinRequest(string playerId, string lobbyId)
        {
            if (friendsService == null)
            {
                Debug.LogError("Friends service not initialized.");
                return false;
            }
            return await friendsService.AcceptJoinRequest(playerId, lobbyId);
        }

        /// <summary>
        /// Handles when someone accepts your join request - auto-joins their lobby.
        /// </summary>
        private async void HandleJoinRequestAccepted(LobbyInvite invite)
        {
            OnJoinRequestAccepted?.Invoke(invite);

            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (!string.IsNullOrEmpty(invite.LobbyId) && lobbyService != null)
            {
                await lobbyService.JoinLobby(invite.LobbyId);
            }
        }
    }
}