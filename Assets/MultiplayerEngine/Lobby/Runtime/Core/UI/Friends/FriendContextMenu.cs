using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Context menu popup for friend options. Spawns at click position and destroys when closed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FriendContextMenu : MonoBehaviour
    {
        [SerializeField] private Button showProfile;
        [SerializeField] private Button inviteToGame;
        [SerializeField] private Button askToJoin;
        [SerializeField] private Button removeFriend;

        private string friendId;
        private RectTransform rectTransform;
        private Canvas parentCanvas;

        public void Initialize(string friendId, Canvas canvas)
        {
            this.friendId = friendId;
            this.parentCanvas = canvas;
            rectTransform = GetComponent<RectTransform>();

            SetupButtons();
            PositionAtMouse();
            UpdateButtonStates();
        }

        private void SetupButtons()
        {
            showProfile?.onClick.AddListener(() =>
            {
                PlayerProfileUI.Instance?.ShowFriendProfileUI(friendId);
                DestroySelf();
            });

            inviteToGame?.onClick.AddListener(async () =>
            {
                inviteToGame.interactable = false;
                await FriendsManager.Instance.InviteToGame(friendId);
                DestroySelf();
            });

            askToJoin?.onClick.AddListener(async () =>
            {
                askToJoin.interactable = false;
                await FriendsManager.Instance.AskToJoin(friendId);
                DestroySelf();
            });

            removeFriend?.onClick.AddListener(async () =>
            {
                removeFriend.interactable = false;
                bool success = await FriendsManager.Instance.Unfriend(friendId);
                if (success) DestroySelf();
                else removeFriend.interactable = true;
            });
        }

        private void Update()
        {
            // Destroy when clicking outside
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, parentCanvas?.worldCamera))
                {
                    DestroySelf();
                }
            }

            // Destroy on escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DestroySelf();
            }
        }

        private void PositionAtMouse()
        {
            Vector2 pos = ClampToScreen(Input.mousePosition);
            rectTransform.position = pos;
        }

        private Vector2 ClampToScreen(Vector2 position)
        {
            Vector2 size = rectTransform.sizeDelta;
            Vector2 pivot = rectTransform.pivot;

            float minX = size.x * pivot.x;
            float maxX = Screen.width - size.x * (1 - pivot.x);
            float minY = size.y * pivot.y;
            float maxY = Screen.height - size.y * (1 - pivot.y);

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY)
            );
        }

        private void UpdateButtonStates()
        {
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            var lobbyAvailable = lobbyService != null && lobbyService.CurrentLobbyData != null;
            var friend = FriendsManager.Instance?.FriendsList.Find(f => f.PlayerId == friendId);

            if (inviteToGame != null)
                inviteToGame.interactable = lobbyAvailable && friend != null && friend.Presence != FriendPresence.Offline;

            if (askToJoin != null)
                askToJoin.interactable = friend != null && friend.Presence == FriendPresence.InLobby;
        }

        private void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}
