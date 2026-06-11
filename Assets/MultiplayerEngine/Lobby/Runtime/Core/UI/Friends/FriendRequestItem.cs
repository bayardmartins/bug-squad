using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine 
{
    /// <summary>
    /// Represents a UI item for a friend request.
    /// </summary>
    public class FriendRequestItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text displaynameText;
        [SerializeField] private Button accept;
        [SerializeField] private Button decline;
        [SerializeField] private Button cancel;
        [SerializeField] private Button send;
        [SerializeField] private Image avatar;

        public string playerId { get; private set; }
        public FriendRequestType RequestType { get; private set; }

        public void SetRequest(FriendRequest request)
        {
            displaynameText.text = request.DisplayName;
            avatar.sprite = request.Avatar;
            RequestType = request.RequestType;
            accept.gameObject.SetActive(request.RequestType == FriendRequestType.Incoming);
            decline.gameObject.SetActive(request.RequestType == FriendRequestType.Incoming);
            cancel.gameObject.SetActive(request.RequestType == FriendRequestType.Outgoing);
            send.gameObject.SetActive(false);
            playerId = request.PlayerId;
        }

        private void Awake()
        {
            accept.onClick.AddListener(AcceptRequest);
            decline.onClick.AddListener(DeclineRequest);
            cancel.onClick.AddListener(CancelRequest);
        }

        private async void AcceptRequest()
        {
            await HandleRequest(FriendsManager.Instance.AcceptFriendRequest, accept);
        }

        private async void DeclineRequest()
        {
            await HandleRequest(FriendsManager.Instance.DeclineFriendRequest, decline);
        }

        private async void CancelRequest()
        {
            await HandleRequest(FriendsManager.Instance.CancelOutgoingRequest, cancel);
        }

        private async void SendRequest()
        {
            await HandleRequest(FriendsManager.Instance.SendFriendRequest, send);
        }

        private async Task HandleRequest(Func<string, Task<bool>> requestAction, Button button)
        {
            button.interactable = false;
            bool success = await requestAction(playerId);
            if (success)
            {
                Destroy(gameObject);
            }
            else
            {
                button.interactable = true;
            }
        }
    }
}