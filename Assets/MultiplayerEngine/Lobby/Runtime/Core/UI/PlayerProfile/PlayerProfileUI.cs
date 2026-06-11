using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages the player profile UI, including local and friend profiles.
    /// Uses CanvasGroup for visibility control.
    /// </summary>
    public class PlayerProfileUI : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of PlayerProfileUI.
        /// </summary>
        public static PlayerProfileUI Instance { get; private set; }

        [Header("Visibility")]
        [Tooltip("CanvasGroup on this GameObject for visibility control.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Profile Display")]
        [Tooltip("Displays the player's avatar/icon.")]
        [SerializeField] private Image playerIcon;

        [Tooltip("Displays the player's display name.")]
        [SerializeField] private TMP_Text displayName;

        [Tooltip("Displays the player's description/bio.")]
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("Displays the number of games played.")]
        [SerializeField] private TMP_Text playedGamesCountText;

        [Header("Actions")]
        [Tooltip("Button to open the edit profile panel.")]
        [SerializeField] private Button editProfileButton;

        [Tooltip("Button to close/hide the profile panel.")]
        [SerializeField] private Button closeButton;

        [Header("Edit Profile (Child Panel)")]
        [Tooltip("Reference to the child edit profile UI.")]
        [SerializeField] private EditPlayerProfileUI editProfileUI;


        private static readonly List<string> CustomStatsKeys = new() { "gamesPlayed", "description" };
        private bool isInitialized;

        private void Awake()
        {
            // Auto-initialize on Awake
            Initialize();
        }

        /// <summary>
        /// Initializes the profile UI, sets up singleton and button listeners.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            isInitialized = true;

            // Start hidden
            SetVisible(false);

#if STEAM_SERVICES
            if (editProfileButton != null)
                editProfileButton.onClick.AddListener(ShowEditProfilePanel);
#endif

            if (closeButton != null)
                closeButton.onClick.AddListener(HideProfilePanel);
        }

        /// <summary>
        /// Sets visibility using CanvasGroup.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Displays the local player's profile UI and loads stats asynchronously.
        /// </summary>
        public async Task ShowPlayerProfileUI()
        {
            SetVisible(true);
            try
            {
                var stats = await PlayerProfileManager.Instance.GetLocalPlayerStatsAsync(CustomStatsKeys);
                UpdateProfileUI(stats, isLocalPlayer: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading local player profile: {ex}");
                SetVisible(false);
            }
        }

        /// <summary>
        /// Displays a friend's profile UI and loads stats asynchronously.
        /// </summary>
        /// <param name="friendId">The friend's player ID.</param>
        public async void ShowFriendProfileUI(string friendId)
        {
            SetVisible(true);
            try
            {
                var stats = await PlayerProfileManager.Instance.GetRemotePlayerStatsAsync(friendId, CustomStatsKeys);
                UpdateProfileUI(stats, isLocalPlayer: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading friend profile ({friendId}): {ex}");
                SetVisible(false);
            }
        }

        /// <summary>
        /// Updates the profile UI elements with the provided stats.
        /// </summary>
        private void UpdateProfileUI(PlayerStats stats, bool isLocalPlayer)
        {
            if (stats == null)
            {
                if (displayName != null) displayName.text = "Unknown Player";
                if (playerIcon != null) playerIcon.sprite = null;
                if (descriptionText != null) descriptionText.text = "";
                if (playedGamesCountText != null) playedGamesCountText.text = "0";
                if (editProfileButton != null) editProfileButton.gameObject.SetActive(false);
                return;
            }

            // Update display name
            if (displayName != null)
                displayName.text = stats.DisplayName;

            // Update player icon/avatar
            if (playerIcon != null)
                playerIcon.sprite = stats.PlayerAvatar;

            // Update description
            if (descriptionText != null)
            {
                if (stats.CustomStats != null && stats.CustomStats.TryGetValue("description", out string description))
                    descriptionText.text = description;
                else
                    descriptionText.text = "";
            }

            // Update games played count
            if (playedGamesCountText != null)
            {
                if (stats.CustomStats != null && stats.CustomStats.TryGetValue("gamesPlayed", out string gamesPlayed))
                    playedGamesCountText.text = gamesPlayed;
                else
                    playedGamesCountText.text = "0";
            }

            // Only show edit button for local player
            if (editProfileButton != null)
                editProfileButton.gameObject.SetActive(isLocalPlayer);
        }

        /// <summary>
        /// Hides the profile UI panel.
        /// </summary>
        public void HideProfilePanel()
        {
            // Also hide edit panel if open
            if (editProfileUI != null)
                editProfileUI.SetVisible(false);

            SetVisible(false);
        }

        /// <summary>
        /// Shows the edit profile panel (child).
        /// </summary>
        private void ShowEditProfilePanel()
        {
            if (editProfileUI != null)
                editProfileUI.Show();
        }

        /// <summary>
        /// Refreshes the local player's profile display.
        /// </summary>
        public async void RefreshLocalProfile()
        {
            try
            {
                var stats = await PlayerProfileManager.Instance.GetLocalPlayerStatsAsync(CustomStatsKeys);
                UpdateProfileUI(stats, isLocalPlayer: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error refreshing local player profile: {ex}");
            }
        }
    }
}
