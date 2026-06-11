#if UNITY_SERVICES
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Unity Friends Service implementation for multiplayer functionality.
    /// </summary>
    public class UnityFriends : IFriends
    {
        public event Action<List<Friend>> OnFriendsListUpdated;
        public event Action<Friend> OnFriendDataUpdated;
        public event Action<(string playerId, FriendPresence presence)> OnFriendPresenceUpdated;

        public event Action<Friend> OnFriendAdded;
        public event Action<string> OnFriendRemoved;
        public event Action<FriendRequest> OnFriendRequestReceived;
        public event Action<LobbyInvite> OnLobbyInviteReceived;
        public event Action<LobbyInvite> OnJoinRequestAccepted;
        public event Action<FriendPresence> OnLocalPlayerPresenceUpdated;

        private List<Friend> currentFriends = new List<Friend>();

        public async Task InitializeAsync()
        {
            await FriendsService.Instance.InitializeAsync();

            // Subscribe to events
            FriendsService.Instance.RelationshipAdded += HandleRelationshipAdded;
            FriendsService.Instance.RelationshipDeleted += HandleRelationshipDeleted;
            FriendsService.Instance.PresenceUpdated += HandlePresenceUpdated;
            FriendsService.Instance.MessageReceived += HandleMessageReceived;

            await SetPresence(FriendPresence.Online);
            await GetFriendListAsync();
        }

        public async Task<bool> SendFriendRequest(string playerName)
        {
            try
            {
                var relationship = await FriendsService.Instance.AddFriendByNameAsync(playerName);
                return relationship.Type == RelationshipType.FriendRequest;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AcceptFriendRequest(string memberId)
        {
            try
            {
                var relationship = await FriendsService.Instance.AddFriendAsync(memberId);
                if (relationship.Type == RelationshipType.Friend)
                {
                    _ = GetFriendListAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CancelOutgoingRequest(string memberId)
        {
            try
            {
                await FriendsService.Instance.DeleteOutgoingFriendRequestAsync(memberId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeclineFriendRequest(string memberId)
        {
            try
            {
                await FriendsService.Instance.DeleteIncomingFriendRequestAsync(memberId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> Unfriend(string memberId)
        {
            try
            {
                await FriendsService.Instance.DeleteFriendAsync(memberId);
                _ = GetFriendListAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<Friend>> GetFriendListAsync()
        {
            var friends = FriendsService.Instance.Friends;
            List<Friend> friendList = new List<Friend>();

            if (friends == null) return friendList;

            foreach (var friend in friends)
            {
                FriendPresence presence = MapPresence(friend.Member.Presence);

                var newFriend = new Friend
                {
                    PlayerId = friend.Member.Id,
                    DisplayName = friend.Member.Profile.Name,
                    Avatar = await GetProfilePictureAsync(friend.Member.Id),
                    Presence = presence
                };

                friendList.Add(newFriend);
            }

            currentFriends = friendList;
            OnFriendsListUpdated?.Invoke(currentFriends);
            return currentFriends;
        }

        public async Task<List<FriendRequest>> GetFriendRequestsAsync()
        {
            var friendRequests = new List<FriendRequest>();

            // Incoming requests
            if (FriendsService.Instance.IncomingFriendRequests != null)
            {
                foreach (var request in FriendsService.Instance.IncomingFriendRequests)
                {
                    friendRequests.Add(new FriendRequest
                    {
                        PlayerId = request.Member.Id,
                        DisplayName = request.Member.Profile.Name,
                        Avatar = await GetProfilePictureAsync(request.Member.Id),
                        RequestType = FriendRequestType.Incoming
                    });
                }
            }

            // Outgoing requests
            if (FriendsService.Instance.OutgoingFriendRequests != null)
            {
                foreach (var request in FriendsService.Instance.OutgoingFriendRequests)
                {
                    friendRequests.Add(new FriendRequest
                    {
                        PlayerId = request.Member.Id,
                        DisplayName = request.Member.Profile.Name,
                        Avatar = await GetProfilePictureAsync(request.Member.Id),
                        RequestType = FriendRequestType.Outgoing
                    });
                }
            }

            return friendRequests;
        }

        private async Task<Sprite> GetProfilePictureAsync(string playerId)
        {
            try
            {
                var playerData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { "avatarId" },
                    new LoadOptions(new PublicReadAccessClassOptions(playerId))
                );

                if (playerData.TryGetValue("avatarId", out var playerIconValue))
                {
                    int playerIconId = playerIconValue.Value.GetAs<int>();
                    return PlayerProfileManager.Instance.GetIconById(playerIconId.ToString());
                }
            }
            catch { }
            return PlayerProfileManager.Instance.GetIconById("0");
        }

        private FriendPresence MapPresence(Presence presence)
        {
            if (presence == null)
                return FriendPresence.Offline;

            if (presence.Availability == Availability.Offline)
                return FriendPresence.Offline;

            if (presence.Availability == Availability.Away)
                return FriendPresence.Away;

            return presence.GetActivity<Activity>()?.friendPresence ?? FriendPresence.Online;
        }

        public async Task SetPresence(FriendPresence presence)
        {
            var availability = Availability.Offline;
            var activity = new Activity { friendPresence = presence };

            switch (presence)
            {
                case FriendPresence.Offline:
                    availability = Availability.Offline;
                    break;
                case FriendPresence.Online:
                case FriendPresence.InGame:
                case FriendPresence.InLobby:
                    availability = Availability.Online;
                    break;
                case FriendPresence.Away:
                    availability = Availability.Away;
                    break;
            }

            await FriendsService.Instance.SetPresenceAsync(availability, activity);
            OnLocalPlayerPresenceUpdated?.Invoke(presence);
        }

        #region Event Handlers

        private async void HandleRelationshipAdded(IRelationshipAddedEvent evt)
        {
            if (evt.Relationship.Type == RelationshipType.Friend)
            {
                OnFriendAdded?.Invoke(new Friend
                {
                    PlayerId = evt.Relationship.Member.Id,
                    DisplayName = evt.Relationship.Member.Profile.Name,
                    Avatar = await GetProfilePictureAsync(evt.Relationship.Member.Id),
                    Presence = MapPresence(evt.Relationship.Member.Presence)
                });

                _ = GetFriendListAsync();
            }
            else if (evt.Relationship.Type == RelationshipType.FriendRequest)
            {
                // Only trigger for incoming requests (Member.Role == Source means they sent it to us)
                if (evt.Relationship.Member.Role == MemberRole.Source)
                {
                    OnFriendRequestReceived?.Invoke(new FriendRequest
                    {
                        PlayerId = evt.Relationship.Member.Id,
                        DisplayName = evt.Relationship.Member.Profile.Name,
                        Avatar = await GetProfilePictureAsync(evt.Relationship.Member.Id),
                        RequestType = FriendRequestType.Incoming
                    });
                }
            }
        }

        private void HandleRelationshipDeleted(IRelationshipDeletedEvent evt)
        {
            // Just refresh the entire friends list when any relationship is deleted
            _ = GetFriendListAsync();
        }

        private void HandlePresenceUpdated(IPresenceUpdatedEvent evt)
        {
            FriendPresence presence = MapPresence(evt.Presence);
            OnFriendPresenceUpdated?.Invoke((evt.ID, presence));
        }

        private async void HandleMessageReceived(IMessageReceivedEvent @event)
        {
            InviteData inviteData = @event.GetAs<InviteData>();
            if (inviteData != null)
            {
                var friend = currentFriends.Find(f => f.PlayerId == @event.UserId);
                string playerName = friend?.DisplayName ?? "Unknown";
                Sprite avatar = friend?.Avatar ?? await GetProfilePictureAsync(@event.UserId);

                if (inviteData.Type == InviteType.Invite || inviteData.Type == InviteType.RequestToJoin)
                {
                    OnLobbyInviteReceived?.Invoke(new LobbyInvite
                    {
                        FromPlayerId = @event.UserId,
                        FromPlayerName = playerName,
                        FromAvatar = avatar,
                        LobbyId = inviteData.JoinId,
                        InviteType = inviteData.Type
                    });
                }
                else if (inviteData.Type == InviteType.AcceptedRequest)
                {
                    OnJoinRequestAccepted?.Invoke(new LobbyInvite
                    {
                        FromPlayerId = @event.UserId,
                        FromPlayerName = playerName,
                        FromAvatar = avatar,
                        LobbyId = inviteData.JoinId,
                        InviteType = inviteData.Type
                    });
                }
            }
        }

        #endregion

        #region Lobby Invites

        public class InviteData
        {
            public string JoinId;
            public InviteType Type;
        }

        public async Task<bool> SendInvite(string playerId, string joinId)
        {
            try
            {
                var inviteData = new InviteData
                {
                    JoinId = joinId,
                    Type = InviteType.Invite
                };
                await FriendsService.Instance.MessageAsync(playerId, inviteData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AskToJoin(string playerId)
        {
            try
            {
                var inviteData = new InviteData
                {
                    JoinId = "",
                    Type = InviteType.RequestToJoin
                };
                await FriendsService.Instance.MessageAsync(playerId, inviteData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AcceptJoinRequest(string playerId, string joinId)
        {
            try
            {
                var inviteData = new InviteData
                {
                    JoinId = joinId,
                    Type = InviteType.AcceptedRequest
                };
                await FriendsService.Instance.MessageAsync(playerId, inviteData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        private class Activity
        {
            public FriendPresence friendPresence;
        }
    }
}
#endif
