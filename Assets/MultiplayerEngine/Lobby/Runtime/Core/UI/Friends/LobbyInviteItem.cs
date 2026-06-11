using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// UI item for lobby invites and join requests.
    /// For Invites: Accept joins the lobby
    /// For Join Requests: Accept sends our lobby ID back to the requester
    /// </summary>
    public class LobbyInviteItem : MonoBehaviour
    {
        [SerializeField] private Button accept;
        [SerializeField] private Button decline;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text fromPlayerName;
        [SerializeField] private Image fromPlayerAvatar;

        public string LobbyId { get; private set; }
        public string FromPlayerId { get; private set; }
        public InviteType InviteType { get; private set; }

        public void SetInviteData(LobbyInvite invite)
        {
            FromPlayerId = invite.FromPlayerId;
            LobbyId = invite.LobbyId;
            InviteType = invite.InviteType;
            fromPlayerName.text = invite.FromPlayerName;

            if (invite.InviteType == InviteType.Invite)
                description.text = "invited you to join their lobby";
            else if (invite.InviteType == InviteType.RequestToJoin)
                description.text = "wants to join your lobby";
            else
                description.text = "sent you a lobby invite";

            if (fromPlayerAvatar != null && invite.FromAvatar != null)
                fromPlayerAvatar.sprite = invite.FromAvatar;
        }

        private void Awake()
        {
            accept.onClick.AddListener(async () =>
            {
                accept.interactable = false;

                if (InviteType == InviteType.Invite)
                {
                    // We received an invite - join their lobby
                    if (string.IsNullOrEmpty(LobbyId))
                    {
                        Debug.LogError("Cannot join lobby: LobbyId is null or empty");
                        accept.interactable = true;
                        return;
                    }

                    var lobbyService = ServiceLocator.Get<ILobbyService>();
                    if (lobbyService == null)
                    {
                        Debug.LogError("Cannot join lobby: No lobby service registered");
                        accept.interactable = true;
                        return;
                    }

                    await lobbyService.JoinLobby(LobbyId);
                    if (lobbyService.CurrentLobbyData != null)
                        Destroy(gameObject);
                    else
                        accept.interactable = true;
                }
                else if (InviteType == InviteType.RequestToJoin)
                {
                    // Someone is asking to join OUR lobby - accept and send them our lobby ID
                    var lobbyService = ServiceLocator.Get<ILobbyService>();
                    var myLobby = lobbyService?.CurrentLobbyData;
                    if (myLobby == null)
                    {
                        Debug.LogError("Cannot accept join request: You are not in a lobby");
                        accept.interactable = true;
                        return;
                    }

                    await FriendsManager.Instance.AcceptJoinRequest(FromPlayerId, myLobby.LobbyId);
                    Destroy(gameObject);
                }
            });

            decline.onClick.AddListener(() =>
            {
                Destroy(gameObject);
            });
        }
    }
}

