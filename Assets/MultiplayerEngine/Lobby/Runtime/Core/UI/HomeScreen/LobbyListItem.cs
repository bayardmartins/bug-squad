using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Represents a single lobby item in the lobby list.
    /// Shows lock icon for password-protected lobbies.
    /// </summary>
    public class LobbyListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text lobbyName;
        [SerializeField] private TMP_Text playerCount;
        [SerializeField] private TMP_Text gameMode;
        [SerializeField] private Button joinLobby;
        [SerializeField] private GameObject lockIcon;

        private string lobbyId;
        private bool isLocked;

        /// <summary>
        /// Event invoked when a locked lobby is clicked. Parent UI should show password popup.
        /// </summary>
        public static event Action<string> OnLockedLobbyClicked;

        public void SetUp(LobbyListData lobbyListData)
        {
            lobbyId = lobbyListData.lobbyId;
            isLocked = lobbyListData.isLocked;

            if (lobbyName != null) lobbyName.text = lobbyListData.lobbyName;
            if (playerCount != null) playerCount.text = $"{lobbyListData.currentPlayers}/{lobbyListData.maxPlayers}";
            if (gameMode != null) gameMode.text = lobbyListData.gameMode;

            // Show/hide lock icon
            if (lockIcon != null) lockIcon.SetActive(isLocked);

            joinLobby?.onClick.RemoveAllListeners();
            joinLobby?.onClick.AddListener(OnJoinClicked);
        }

        private async void OnJoinClicked()
        {
            if (isLocked)
            {
                // Notify parent UI to show password popup
                OnLockedLobbyClicked?.Invoke(lobbyId);
            }
            else
            {
                // Direct join for unlocked lobbies
                if (joinLobby != null) joinLobby.interactable = false;
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.JoinLobby(lobbyId);
                }
                if (joinLobby != null) joinLobby.interactable = true;
            }
        }
    }
}
