using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages the UI for editing the player's profile.
    /// Uses CanvasGroup for visibility control. Exists as a child of PlayerProfileUI.
    /// </summary>
    public class EditPlayerProfileUI : MonoBehaviour
    {
        [Header("Visibility")]
        [Tooltip("CanvasGroup on this GameObject for visibility control.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Input Fields")]
        [Tooltip("Input field for the username/display name.")]
        [SerializeField] private TMP_InputField usernameInput;

        [Tooltip("Input field for the player description/bio.")]
        [SerializeField] private TMP_InputField descriptionInput;

        [Header("Avatar Selection")]
        [Tooltip("Displays the currently selected avatar.")]
        [SerializeField] private Image avatarImage;

        [Tooltip("Parent container for profile icon buttons.")]
        [SerializeField] private RectTransform profileIconsHolder;

        [Header("Actions")]
        [Tooltip("Button to save the profile changes.")]
        [SerializeField] private Button saveButton;

        [Tooltip("Button to close the edit profile panel.")]
        [SerializeField] private Button closeButton;

        [Header("Feedback")]
        [Tooltip("Text field for displaying error messages.")]
        [SerializeField] private TMP_Text errorText;

        private int selectedAvatarIndex = 0;
        private bool isInitialized;

        private void Start()
        {
            SetupUI();
            // Start hidden
            SetVisible(false);
        }

        private void SetupUI()
        {
            if (isInitialized) return;
            isInitialized = true;

            // Validate profile manager and icons availability.
            if (PlayerProfileManager.Instance == null || PlayerProfileManager.Instance.PlayerIcons == null)
            {
                Debug.LogWarning("[EditPlayerProfileUI] PlayerProfileManager or PlayerIcons not set.");
                if (saveButton != null) saveButton.interactable = false;
                ShowError("Profile system unavailable.");
                return;
            }

            // Setup profile icons holder
            if (profileIconsHolder != null)
            {
                // Remove any existing avatar icon buttons.
                foreach (Transform child in profileIconsHolder)
                {
                    Destroy(child.gameObject);
                }

                // Dynamically create avatar icon buttons.
                for (int i = 0; i < PlayerProfileManager.Instance.PlayerIcons.Count; i++)
                {
                    Sprite sprite = PlayerProfileManager.Instance.PlayerIcons[i];
                    GameObject iconButtonObj = new GameObject($"IconButton_{i}", typeof(RectTransform), typeof(Button), typeof(Image));
                    iconButtonObj.transform.SetParent(profileIconsHolder, false);

                    Image iconImage = iconButtonObj.GetComponent<Image>();
                    iconImage.sprite = sprite;

                    Button iconButton = iconButtonObj.GetComponent<Button>();
                    int index = i;
                    iconButton.onClick.AddListener(() => SelectAvatar(index, sprite));
                }
            }

            // Setup save button
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSaveClicked);
            }

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            // Setup input validation
            if (usernameInput != null)
                usernameInput.onValueChanged.AddListener(_ => ValidateInputs());

            HideError();
        }

        /// <summary>
        /// Shows the edit profile panel and loads current data.
        /// </summary>
        public void Show()
        {
            SetupUI();
            LoadCurrentProfileData();
            SetVisible(true);
        }

        /// <summary>
        /// Hides the edit profile panel.
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
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
        /// Loads the current profile data into the input fields.
        /// </summary>
        private void LoadCurrentProfileData()
        {
            if (PlayerProfileManager.Instance == null || PlayerProfileManager.Instance.LocalPlayerStats == null)
                return;

            var stats = PlayerProfileManager.Instance.LocalPlayerStats;

            // Set current username
            if (usernameInput != null)
                usernameInput.text = stats.DisplayName ?? "";

            // Set current description
            if (descriptionInput != null && stats.CustomStats != null)
            {
                if (stats.CustomStats.TryGetValue("description", out string description))
                    descriptionInput.text = description;
                else
                    descriptionInput.text = "";
            }

            // Set current avatar
            if (avatarImage != null)
                avatarImage.sprite = stats.PlayerAvatar;

            // Determine current avatar index
            if (stats.PlayerAvatar != null && PlayerProfileManager.Instance.PlayerIcons != null)
            {
                int index = PlayerProfileManager.Instance.PlayerIcons.IndexOf(stats.PlayerAvatar);
                if (index >= 0)
                    selectedAvatarIndex = index;
            }

            ValidateInputs();
        }

        /// <summary>
        /// Called when an avatar icon is selected.
        /// </summary>
        private void SelectAvatar(int index, Sprite sprite)
        {
            selectedAvatarIndex = index;
            if (avatarImage != null)
                avatarImage.sprite = sprite;
            ValidateInputs();
        }

        /// <summary>
        /// Validates the input fields.
        /// </summary>
        private void ValidateInputs()
        {
            string name = usernameInput?.text?.Trim();

            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                if (saveButton != null) saveButton.interactable = false;
                return;
            }

            HideError();
            if (saveButton != null) saveButton.interactable = true;
        }

        /// <summary>
        /// Handles the save button click.
        /// </summary>
        private async void OnSaveClicked()
        {
            if (saveButton != null) saveButton.interactable = false;
            HideError();

            string displayName = usernameInput?.text?.Trim();
            string description = descriptionInput?.text?.Trim() ?? "";

            if (string.IsNullOrEmpty(displayName) || displayName.Length < 3)
            {
                ShowError("Username must be at least 3 characters.");
                if (saveButton != null) saveButton.interactable = true;
                return;
            }

            try
            {
                var results = await PlayerProfileManager.Instance.UpdatePlayerDataAsync(displayName, selectedAvatarIndex.ToString());

                if (results.userName && results.avatar)
                {
                    // Update description as custom stat
                    await PlayerProfileManager.Instance.SetCustomStatAsync("description", description);

                    HideError();
                    Hide();

                    // Refresh the player profile UI
                    if (PlayerProfileUI.Instance != null)
                        PlayerProfileUI.Instance.RefreshLocalProfile();
                }
                else if (!results.userName)
                {
                    ShowError("Error setting username. Please try again.");
                }
                else if (!results.avatar)
                {
                    ShowError("Error setting avatar. Please try again.");
                }
                else
                {
                    ShowError("Error setting profile. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EditPlayerProfileUI] Exception: {ex}");
                ShowError("Unexpected error. Please try again.");
            }
            finally
            {
                if (saveButton != null) saveButton.interactable = true;
            }
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }

        private void HideError()
        {
            if (errorText != null)
                errorText.gameObject.SetActive(false);
        }
    }
}
