#if STEAM_SERVICES
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    public class SteamFriends : IFriends
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

        public Task<bool> AcceptFriendRequest(string playerId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelOutgoingRequest(string playerId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeclineFriendRequest(string playerId)
        {
            throw new NotImplementedException();
        }

        public Task SetPresence(FriendPresence presence)
        {
            Steamworks.SteamFriends.SetRichPresence("presence", presence.ToString());
            OnLocalPlayerPresenceUpdated?.Invoke(presence);
            return Task.CompletedTask;
        }

        public Task<List<Friend>> GetFriendListAsync()
        {
            int friendCount = Steamworks.SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            var friendsList = new List<Friend>();

            for (int i = 0; i < friendCount; i++)
            {
                CSteamID friendID = Steamworks.SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                string friendName = Steamworks.SteamFriends.GetFriendPersonaName(friendID);
                int friendAvatar = Steamworks.SteamFriends.GetSmallFriendAvatar(friendID);
                EPersonaState state = Steamworks.SteamFriends.GetFriendPersonaState(friendID);
                string presence = Steamworks.SteamFriends.GetFriendRichPresence(friendID, "presence");

                FriendPresence stateString = (state, presence) switch
                {
                    (EPersonaState.k_EPersonaStateOnline, var s) when !string.IsNullOrEmpty(s) =>
                        s.ToLower() switch
                        {
                            "inGame" => FriendPresence.InGame,
                            "inLobby" => FriendPresence.Online,
                            "away" => FriendPresence.Away,
                            "hide" => FriendPresence.Offline,
                            _ => FriendPresence.Online
                        },
                    (EPersonaState.k_EPersonaStateOffline, _) => FriendPresence.Offline,
                    (EPersonaState.k_EPersonaStateOnline, _) => FriendPresence.Online,
                    (EPersonaState.k_EPersonaStateAway, _) => FriendPresence.Away,
                    (EPersonaState.k_EPersonaStateInvisible, _) => FriendPresence.Offline,
                    (EPersonaState.k_EPersonaStateBusy, _) => FriendPresence.Away,
                    (EPersonaState.k_EPersonaStateSnooze, _) => FriendPresence.Away,
                    (EPersonaState.k_EPersonaStateLookingToTrade, _) => FriendPresence.Online,
                    (EPersonaState.k_EPersonaStateLookingToPlay, _) => FriendPresence.Online,
                    _ => FriendPresence.Offline
                };

                Friend friendData = new Friend
                {
                    PlayerId = friendID.ToString(),
                    DisplayName = friendName,
                    Avatar = GetFriendAvatar(friendID),
                    Presence = stateString

                };

                friendsList.Add(friendData);
            }
            OnFriendsListUpdated?.Invoke(friendsList);
            return Task.FromResult(friendsList);
        }

        public Task<List<FriendRequest>> GetFriendRequestsAsync()
        {
            throw new NotImplementedException();
        }

        public Task InitializeAsync()
        {
            GetFriendListAsync();

            Callback<FriendRichPresenceUpdate_t>.Create(OnFriendRichPresenceUpdate);
            Callback<LobbyInvite_t>.Create((param) =>
            {
                CSteamID lobbyId = new CSteamID(param.m_ulSteamIDLobby);
                CSteamID friendId = new CSteamID(param.m_ulSteamIDUser);
                string friendName = Steamworks.SteamFriends.GetFriendPersonaName(friendId);
                LobbyInvite invite = new LobbyInvite
                {
                    LobbyId = lobbyId.ToString(),
                    FromPlayerId = friendId.ToString(),
                    FromPlayerName = friendName,
                    InviteType = InviteType.Invite,
                    FromAvatar = GetFriendAvatar(friendId)
                };
                OnLobbyInviteReceived?.Invoke(invite);
            });
            Callback<GameRichPresenceJoinRequested_t>.Create(AskToJoin);

            SetPresence(FriendPresence.Online);

            return Task.CompletedTask;
        }
        private void AskToJoin(GameRichPresenceJoinRequested_t param)
        {
            CSteamID friendId = param.m_steamIDFriend;
            string joinString = param.m_rgchConnect;

            OnLobbyInviteReceived?.Invoke(new LobbyInvite
            {
                FromPlayerId = friendId.ToString(),
                FromPlayerName = Steamworks.SteamFriends.GetFriendPersonaName(friendId),
                LobbyId = joinString,
                InviteType = InviteType.RequestToJoin,
                FromAvatar = GetFriendAvatar(friendId)
            });
        }

        private void OnFriendRichPresenceUpdate(FriendRichPresenceUpdate_t param)
        {
            CSteamID friendID = param.m_steamIDFriend;
            EPersonaState state = Steamworks.SteamFriends.GetFriendPersonaState(friendID);
            string presence = Steamworks.SteamFriends.GetFriendRichPresence(friendID, "presence");
            FriendPresence stateString = (state, presence) switch
            {
                (EPersonaState.k_EPersonaStateOnline, var s) when !string.IsNullOrEmpty(s) =>
                    s.ToLower() switch
                    {
                        "inGame" => FriendPresence.InGame,
                        "inLobby" => FriendPresence.Online,
                        "away" => FriendPresence.Away,
                        "hide" => FriendPresence.Offline,
                        _ => FriendPresence.Online
                    },
                (EPersonaState.k_EPersonaStateOffline, _) => FriendPresence.Offline,
                (EPersonaState.k_EPersonaStateOnline, _) => FriendPresence.Online,
                (EPersonaState.k_EPersonaStateAway, _) => FriendPresence.Away,
                (EPersonaState.k_EPersonaStateInvisible, _) => FriendPresence.Offline,
                (EPersonaState.k_EPersonaStateBusy, _) => FriendPresence.Away,
                (EPersonaState.k_EPersonaStateSnooze, _) => FriendPresence.Away,
                (EPersonaState.k_EPersonaStateLookingToTrade, _) => FriendPresence.Online,
                (EPersonaState.k_EPersonaStateLookingToPlay, _) => FriendPresence.Online,
                _ => FriendPresence.Offline
            };
            OnFriendPresenceUpdated?.Invoke((friendID.ToString(), stateString));
        }

        public Task<bool> SendFriendRequest(string playerId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Unfriend(string playerId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SendInvite(string playerId, string joinId)
        {
            SteamMatchmaking.InviteUserToLobby(new CSteamID(ulong.Parse(joinId)), new CSteamID(ulong.Parse(playerId)));
            return Task.FromResult(true);
        }

        public Task<bool> AskToJoin(string playerId)
        {
            return Task.FromResult(false);
        }

        public Task<bool> AcceptJoinRequest(string playerId, string joinId)
        {
            return Task.FromResult(false);
        }

        public static Sprite GetFriendAvatar(CSteamID steamId)
        {
            // Choose size: 0 = small, 1 = medium, 2 = large
            int imageId = -1;
            imageId = Steamworks.SteamFriends.GetMediumFriendAvatar(steamId);


            if (imageId == -1) return null; // not yet loaded

            uint width, height;
            if (!SteamUtils.GetImageSize(imageId, out width, out height))
                return null;

            byte[] image = new byte[width * height * 4]; // RGBA
            if (!SteamUtils.GetImageRGBA(imageId, image, image.Length))
                return null;

            // Create texture
            Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(image);
            texture.Apply();

            // Convert to Sprite
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
#endif
