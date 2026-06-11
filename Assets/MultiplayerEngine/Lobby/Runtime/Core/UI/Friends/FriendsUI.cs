using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Lightweight friends list UI. Optimized for performance.
    /// </summary>
    public class FriendsUI : MonoBehaviour
    {
        public static FriendsUI Instance { get; private set; }

        [Header("Friend List")]
        [SerializeField] private FriendListItem friendListItemPrefab;
        [SerializeField] private Transform friendListHolder;
        [SerializeField] private FriendContextMenu contextMenuPrefab;

        [Header("Lobby Invites")]
        [SerializeField] private LobbyInviteItem lobbyInviteItemPrefab;
        [SerializeField] private Transform lobbyInviteHolder;

        [Header("Search")]
        [SerializeField] private Button clearSearchButton;
        [SerializeField] private TMP_InputField searchInputField;

        [Header("Add Friend")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private AddFriends addFriendsPrefab;

        [Header("Panel Animation")]
        [SerializeField] private UITransformMove panelMover;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Count Displays")]
        [SerializeField] private TMP_Text friendsCountText;
        [SerializeField] private TMP_Text invitesCountText;

        private readonly Dictionary<string, FriendListItem> friendItems = new();
        private readonly Dictionary<string, LobbyInviteItem> inviteItems = new();
        private readonly List<FriendListItem> sortedFriendList = new();

        private Color defaultAddFriendColor;
        private Canvas rootCanvas;
        private FriendContextMenu activeContextMenu;
        private AddFriends activeAddFriendsPanel;

        private void Start()
        {
            Initialize();
        }
        public void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Hide until authentication completes
            SetVisible(false);

            SetupAddFriendButton();
            SetupSearchListeners();
            SubscribeToEvents();

            // Subscribe to panel hide event
            if (panelMover != null)
                panelMover.OnHide += OnPanelHide;

            // Subscribe to authentication event
            if (FriendsManager.Instance != null)
            {
                FriendsManager.OnFriendsAuthenticated += OnFriendsAuthenticated;
                if (FriendsManager.Instance.IsInitialized)
                {
                    OnFriendsAuthenticated();
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (panelMover != null)
                panelMover.OnHide -= OnPanelHide;

            if (FriendsManager.Instance != null)
                FriendsManager.OnFriendsAuthenticated -= OnFriendsAuthenticated;
        }

        /// <summary>
        /// Called when FriendsManager authentication completes.
        /// </summary>
        private async void OnFriendsAuthenticated()
        {
            SetVisible(true);

            // Fetch initial friends list
            if (FriendsManager.Instance != null)
            {
                var friends = await FriendsManager.Instance.GetFriendsList();
                OnFriendsListUpdated(friends);
            }
        }

        /// <summary>
        /// Sets the visibility of the friends UI using CanvasGroup.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Called when the friends panel starts hiding.
        /// </summary>
        private void OnPanelHide()
        {
            // Destroy AddFriends popup when panel hides
            if (activeAddFriendsPanel != null)
            {
                if (panelMover != null)
                    panelMover.RemoveIgnoreArea(activeAddFriendsPanel.GetComponent<RectTransform>());

                Destroy(activeAddFriendsPanel.gameObject);
                activeAddFriendsPanel = null;
            }
        }

        private void SetupAddFriendButton()
        {
#if UNITY_SERVICES
            if (addFriendButton != null)
            {
                defaultAddFriendColor = addFriendButton.image.color;
                addFriendButton.onClick.AddListener(ToggleAddFriendsPanel);
            }
            else
            {
                Debug.LogError("FriendsUI: addFriendButton is NULL - not assigned in Inspector!");
            }
#elif STEAM_SERVICES
            if (addFriendButton != null)
                addFriendButton.gameObject.SetActive(false);
#else
            Debug.LogWarning("FriendsUI: Neither UNITY_SERVICES nor STEAM_SERVICES defined!");
#endif
        }

        private void SetupSearchListeners()
        {
            clearSearchButton?.onClick.AddListener(ClearSearch);
            searchInputField?.onValueChanged.AddListener(FilterFriends);
        }

        private void SubscribeToEvents()
        {
            FriendsManager.OnFriendDataUpdated += OnFriendDataUpdated;
            FriendsManager.OnFriendsListUpdated += OnFriendsListUpdated;
            FriendsManager.OnLobbyInviteReceived += OnLobbyInviteReceived;
            FriendsManager.OnFriendRequestReceived += OnFriendRequestReceived;
            FriendsManager.OnFriendPresenceUpdated += OnPresenceUpdate;
            FriendsManager.OnFriendRequestsUpdated += RefreshInvitesCount;
        }

        private void UnsubscribeFromEvents()
        {
            FriendsManager.OnFriendDataUpdated -= OnFriendDataUpdated;
            FriendsManager.OnFriendsListUpdated -= OnFriendsListUpdated;
            FriendsManager.OnLobbyInviteReceived -= OnLobbyInviteReceived;
            FriendsManager.OnFriendRequestReceived -= OnFriendRequestReceived;
            FriendsManager.OnFriendPresenceUpdated -= OnPresenceUpdate;
            FriendsManager.OnFriendRequestsUpdated -= RefreshInvitesCount;
        }

        #region Button Actions

        private void ToggleAddFriendsPanel()
        {
            // Destroy if already open
            if (activeAddFriendsPanel != null)
            {
                // Unregister from ignore list before destroying
                if (panelMover != null)
                    panelMover.RemoveIgnoreArea(activeAddFriendsPanel.GetComponent<RectTransform>());

                Destroy(activeAddFriendsPanel.gameObject);
                activeAddFriendsPanel = null;
                return;
            }

            // Validate prefab
            if (addFriendsPrefab == null)
            {
                Debug.LogError("FriendsUI: AddFriends prefab is not assigned in Inspector!");
                return;
            }

            // Cache canvas on first use
            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            if (rootCanvas == null)
            {
                Debug.LogError("FriendsUI: Could not find root Canvas!");
                return;
            }

            // Spawn new panel
            activeAddFriendsPanel = Instantiate(addFriendsPrefab, rootCanvas.transform);
            activeAddFriendsPanel.Initialize();

            if (addFriendButton != null)
                addFriendButton.image.color = defaultAddFriendColor;

            // Register to ignore list so clicking it doesn't close friends panel
            if (panelMover != null)
                panelMover.AddIgnoreArea(activeAddFriendsPanel.GetComponent<RectTransform>());
        }

        private void ClearSearch()
        {
            if (searchInputField != null)
                searchInputField.text = string.Empty;

            // Show all friends
            foreach (var item in sortedFriendList)
                item.gameObject.SetActive(true);
        }

        private void FilterFriends(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                foreach (var item in sortedFriendList)
                    item.gameObject.SetActive(true);
                return;
            }

            string lowerSearch = searchText.ToLowerInvariant();
            foreach (var item in sortedFriendList)
            {
                bool match = item.FriendName.ToLowerInvariant().Contains(lowerSearch);
                item.gameObject.SetActive(match);
            }
        }

        #endregion

        #region Event Handlers

        private void OnFriendRequestReceived(FriendRequest request)
        {
            if (addFriendButton != null)
                addFriendButton.image.color = Color.yellow;
            RefreshInvitesCount();
        }

        private void OnPresenceUpdate((string friendId, FriendPresence presence) data)
        {
            if (friendItems.TryGetValue(data.friendId, out var item))
            {
                item.UpdatePresence(data.presence);
                SortFriendList();
            }
        }

        private void OnFriendsListUpdated(List<Friend> list)
        {
            // Track which friends are still valid
            var validIds = new HashSet<string>();

            foreach (var friend in list)
            {
                validIds.Add(friend.PlayerId);

                if (friendItems.TryGetValue(friend.PlayerId, out var existingItem))
                {
                    existingItem.SetFriendData(friend);
                }
                else
                {
                    var newItem = Instantiate(friendListItemPrefab, friendListHolder);
                    newItem.SetFriendData(friend);
                    friendItems[friend.PlayerId] = newItem;
                    sortedFriendList.Add(newItem);
                }
            }

            // Remove stale items
            var toRemove = new List<string>();
            foreach (var kvp in friendItems)
            {
                if (!validIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    sortedFriendList.Remove(kvp.Value);
                    Destroy(kvp.Value.gameObject);
                }
            }
            foreach (var id in toRemove)
                friendItems.Remove(id);

            SortFriendList();
            UpdateFriendsCount();
        }

        private void OnFriendDataUpdated(Friend friend)
        {
            if (friendItems.TryGetValue(friend.PlayerId, out var item))
                item.SetFriendData(friend);
        }

        private void OnLobbyInviteReceived(LobbyInvite invite)
        {
            if (!inviteItems.ContainsKey(invite.FromPlayerId))
            {
                var newItem = Instantiate(lobbyInviteItemPrefab, lobbyInviteHolder);
                newItem.SetInviteData(invite);
                inviteItems[invite.FromPlayerId] = newItem;
            }
        }

        #endregion

        #region Friend List Management

        private void SortFriendList()
        {
            sortedFriendList.Sort((a, b) => GetPresencePriority(a.Presence).CompareTo(GetPresencePriority(b.Presence)));

            for (int i = 0; i < sortedFriendList.Count; i++)
                sortedFriendList[i].transform.SetSiblingIndex(i);
        }

        private static int GetPresencePriority(FriendPresence presence) => presence switch
        {
            FriendPresence.Online => 0,
            FriendPresence.InLobby => 1,
            FriendPresence.InGame => 2,
            FriendPresence.Away => 3,
            FriendPresence.Offline => 5,
            _ => 4,
        };

        public void ShowExpandOptionsPanel(string friendId)
        {
            // Destroy any existing context menu
            if (activeContextMenu != null)
                Destroy(activeContextMenu.gameObject);

            // Cache canvas on first use
            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            // Spawn new context menu
            activeContextMenu = Instantiate(contextMenuPrefab, rootCanvas.transform);
            activeContextMenu.Initialize(friendId, rootCanvas);
        }

        #endregion

        #region Count Updates

        private void UpdateFriendsCount()
        {
            if (friendsCountText != null)
                friendsCountText.text = friendItems.Count.ToString();
        }

        private async void RefreshInvitesCount()
        {
            if (invitesCountText == null) return;

            try
            {
                var requests = await FriendsManager.Instance.GetFriendRequests();
                int count = 0;
                foreach (var r in requests)
                {
                    if (r.RequestType == FriendRequestType.Incoming)
                        count++;
                }
                invitesCountText.text = count.ToString();
                invitesCountText.gameObject.SetActive(count > 0);
            }
            catch { /* Silently ignore - count update is non-critical */ }
        }

        #endregion
    }
}

