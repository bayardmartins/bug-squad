using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents a friend item in the friends list. Supports click and right-click for options.
    /// </summary>
    public class FriendListItem : MonoBehaviour, IPointerClickHandler
    {
        public string FriendName { get; private set; }
        public string FriendId { get; private set; }
        public FriendPresence Presence { get; private set; }

        [SerializeField] private TMP_Text friendNameText;
        [SerializeField] private TMP_Text precenseText;
        [SerializeField] private Image profileImage;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button moreButton;
        [SerializeField] private Image precenseIcon;

        private void Start()
        {
            profileButton.onClick.AddListener(() =>
            {
                if (PlayerProfileUI.Instance != null)
                {
                    PlayerProfileUI.Instance.ShowFriendProfileUI(FriendId);
                }
            });

            moreButton.onClick.AddListener(() =>
            {
                FriendsUI.Instance?.ShowExpandOptionsPanel(FriendId);
            });
        }

        /// <summary>
        /// Handles pointer click events. Right-click opens context menu.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                FriendsUI.Instance?.ShowExpandOptionsPanel(FriendId);
            }
        }

        public void SetFriendData(Friend friend)
        {
            FriendName = friend.DisplayName;
            FriendId = friend.PlayerId;
            friendNameText.text = FriendName;
            profileImage.sprite = friend.Avatar != null ? friend.Avatar : null;
            UpdatePresence(friend.Presence);
        }

        public void UpdatePresence(FriendPresence presence)
        {
            precenseText.text = presence.ToString();
            Presence = presence;
            switch (presence)
            {
                case FriendPresence.Online:
                    precenseIcon.color = Color.green;
                    precenseText.color = Color.green;
                    break;
                case FriendPresence.Away:
                    precenseText.color = Color.red;
                    break;
                case FriendPresence.Offline:
                    precenseText.color = Color.grey;
                    break;
                case FriendPresence.InLobby:
                    precenseIcon.color = Color.purple;
                    precenseText.color = Color.purple;
                    break;
                case FriendPresence.InGame:
                    precenseIcon.color = Color.cyan;
                    precenseText.color = Color.cyan;
                    break;
                default:
                    precenseIcon.color = Color.gray;
                    precenseText.color = Color.gray;
                    break;
            }
        }
    }
}

