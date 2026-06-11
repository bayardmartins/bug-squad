using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Holds character-related data for multiplayer functionality.
    /// </summary>
    public class PlayerProfileCard : MonoBehaviour
    {
        [SerializeField] private Image profilePicture;
        [SerializeField] private Image precenseImage;
        [SerializeField] private TMP_Text displayName;
        [SerializeField] private TMP_Text precense;
        [SerializeField] private Button hideShowPrecense;
        [SerializeField] private Button showLocalPlayerProfile;

        private FriendPresence currentPresence = FriendPresence.Online;
        private bool isInitialized;

        private void Start()
        {
            // Auto-initialize if not already done
            if (!isInitialized)
                Initialize();
        }

        private void OnEnable()
        {
            // Refresh data when enabled (handles second card scenario)
            if (isInitialized)
                UpdateLocalPlayerProfile();
        }

        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            PlayerProfileManager.OnLocalPlayerUpdated += UpdateLocalPlayerProfile;
            FriendsManager.OnLocalPlayerPresenceUpdated += OnLocalPlayerPresenceUpdated;
            if (hideShowPrecense != null)
            {
                hideShowPrecense.onClick.AddListener(async () =>
                {
                    hideShowPrecense.interactable = false;
                    await FriendsManager.Instance.SetPresence(currentPresence == FriendPresence.Online ? FriendPresence.Away : FriendPresence.Online);
                    hideShowPrecense.interactable = true;
                });
            }
            if (showLocalPlayerProfile != null)
            {
                showLocalPlayerProfile.onClick.AddListener(async () =>
                {
                    if (PlayerProfileUI.Instance == null)
                    {
                        Debug.LogWarning("[PlayerProfileCard] PlayerProfileUI.Instance is null.");
                        return;
                    }
                    showLocalPlayerProfile.interactable = false;
                    await PlayerProfileUI.Instance.ShowPlayerProfileUI();
                    showLocalPlayerProfile.interactable = true;
                });
            }
            // Update initial state
            UpdateLocalPlayerProfile();
        }

        private void OnDestroy()
        {
            PlayerProfileManager.OnLocalPlayerUpdated -= UpdateLocalPlayerProfile;
            FriendsManager.OnLocalPlayerPresenceUpdated -= OnLocalPlayerPresenceUpdated;
        }

        private void OnLocalPlayerPresenceUpdated(FriendPresence presence)
        {
            Color statusColor = Color.green;

            switch (presence)
            {
                case FriendPresence.Offline:
                    statusColor = Color.gray;
                    break;
                case FriendPresence.Online:
                    statusColor = Color.green;
                    break;
                case FriendPresence.Away:
                    statusColor = Color.red;
                    break;
                case FriendPresence.InGame:
                    statusColor = Color.cyan;
                    break;
                case FriendPresence.InLobby:
                    statusColor = Color.purple;
                    break;
            }

            if (precenseImage != null) precenseImage.color = statusColor;
            if (precense != null)
            {
                precense.color = statusColor;
                precense.text = presence.ToString();
            }
            currentPresence = presence;
        }

        private async void UpdateLocalPlayerProfile()
        {
            if (PlayerProfileManager.Instance == null) return;

            // Use cached data first, fetch if not available
            PlayerStats stats = PlayerProfileManager.Instance.LocalPlayerStats;

            // If no cached stats, fetch them
            if (stats == null)
            {
                try
                {
                    stats = await PlayerProfileManager.Instance.GetLocalPlayerStatsAsync(null);
                }
                catch
                {
                    return;
                }
            }

            // Update UI with whatever stats we have
            if (stats != null)
            {
                if (displayName != null) displayName.text = stats.DisplayName ?? "";
                if (profilePicture != null) profilePicture.sprite = stats.PlayerAvatar;
            }
        }
    }
}
