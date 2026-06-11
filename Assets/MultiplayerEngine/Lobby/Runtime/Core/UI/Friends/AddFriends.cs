using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Add friend panel. Spawns at runtime and destroys on close.
    /// </summary>
    public class AddFriends : MonoBehaviour
    {
        [SerializeField] private FriendRequestItem friendRequestItemPrefab;
        [SerializeField] private Transform friendRequestHolder;

        [SerializeField] private TMP_InputField friendNameInputField;
        [SerializeField] private Button addFriendButton;
        [SerializeField] private RectTransform addFriendSection;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button sendRequest;
        [SerializeField] private Button receiveRequest;
        [SerializeField] private Button closeButton;

        private readonly List<FriendRequestItem> friendRequestItems = new();

        public void Initialize()
        {
            SetupButtons();
            SubscribeToEvents();
            _ = RefreshRequests();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SetupButtons()
        {
            sendRequest?.onClick.AddListener(ShowOnlySentRequests);
            receiveRequest?.onClick.AddListener(ShowOnlyReceivedRequests);
            closeButton?.onClick.AddListener(Close);

            addFriendButton?.onClick.AddListener(async () =>
            {
                var playerName = friendNameInputField.text;
                if (string.IsNullOrEmpty(playerName))
                {
                    ShowStatus("Enter a player name", Color.yellow);
                    return;
                }

                addFriendButton.interactable = false;
                ShowStatus("Sending...", Color.white);

                bool success = await FriendsManager.Instance.SendFriendRequest(playerName);

                if (success)
                {
                    ShowStatus("Request sent!", Color.green);
                    friendNameInputField.text = string.Empty;
                    await RefreshRequests();
                }
                else
                {
                    ShowStatus("Failed to send request", Color.red);
                }

                addFriendButton.interactable = true;
            });
        }

        private void ShowStatus(string message, Color color)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = color;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void SubscribeToEvents()
        {
            FriendsManager.OnFriendRequestReceived += OnFriendRequestReceived;
            FriendsManager.OnFriendAdded += OnFriendAdded;
        }

        private void UnsubscribeFromEvents()
        {
            FriendsManager.OnFriendRequestReceived -= OnFriendRequestReceived;
            FriendsManager.OnFriendAdded -= OnFriendAdded;
        }

        private async void OnFriendRequestReceived(FriendRequest request) => await RefreshRequests();
        private async void OnFriendAdded(Friend friend) => await RefreshRequests();

        private async Task RefreshRequests()
        {
            // Clear existing items
            foreach (var item in friendRequestItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            friendRequestItems.Clear();

            // Fetch and create new items
            var requests = await FriendsManager.Instance.GetFriendRequests();
            foreach (var request in requests)
            {
                var item = Instantiate(friendRequestItemPrefab, friendRequestHolder);
                item.SetRequest(request);
                friendRequestItems.Add(item);
            }
        }

        private void ShowOnlyReceivedRequests()
        {
            // Hide add friend section for received requests
            if (addFriendSection != null)
                addFriendSection.gameObject.SetActive(false);

            foreach (var item in friendRequestItems)
                item.gameObject.SetActive(item.RequestType == FriendRequestType.Incoming);
        }

        private void ShowOnlySentRequests()
        {
            // Show add friend section for sending requests
            if (addFriendSection != null)
                addFriendSection.gameObject.SetActive(true);

            foreach (var item in friendRequestItems)
                item.gameObject.SetActive(item.RequestType == FriendRequestType.Outgoing);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}

