using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages the UI for setting up a new player's profile.
    /// Only functional in UNITY_SERVICES builds.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SetPlayerProfileUI : MonoBehaviour
    {
        [Header("Avatar Selection")]
        [SerializeField] private Image profileIcon;
        [SerializeField] private RectTransform profileIconHolder;

        [Header("User Input")]
        [SerializeField] private TMP_InputField userName;

        [Header("Actions")]
        [SerializeField] private Button save;

        [Header("Feedback")]
        [SerializeField] private TMP_Text error;

        private CanvasGroup canvasGroup;

#if UNITY_SERVICES
        private int selectedAvatarIndex = 0;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            var profileService = ServiceLocator.Get<IProfileService>();
            if (profileService == null || profileService.PlayerIcons == null)
            {
                Debug.LogWarning("[SetPlayerProfileUI] Profile service or PlayerIcons not available.");
                if (save != null) save.interactable = false;
                ShowError("Profile system unavailable.");
                return;
            }

            // Clear existing icons
            if (profileIconHolder != null)
            {
                foreach (Transform child in profileIconHolder)
                {
                    Destroy(child.gameObject);
                }

                // Create avatar buttons
                for (int i = 0; i < profileService.PlayerIcons.Count; i++)
                {
                    Sprite sprite = profileService.PlayerIcons[i];
                    var iconObj = new GameObject($"Avatar_{i}", typeof(RectTransform), typeof(Button), typeof(Image));
                    iconObj.transform.SetParent(profileIconHolder, false);

                    var iconImage = iconObj.GetComponent<Image>();
                    iconImage.sprite = sprite;

                    var iconButton = iconObj.GetComponent<Button>();
                    int index = i;
                    iconButton.onClick.AddListener(() => SelectAvatar(index, sprite));
                }
            }

            save?.onClick.RemoveAllListeners();
            save?.onClick.AddListener(OnSaveClicked);
            userName?.onValueChanged.AddListener(_ => ValidateInputs());

            HideError();
            if (save != null) save.interactable = false;
        }

        public void Show()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        private void SelectAvatar(int index, Sprite sprite)
        {
            selectedAvatarIndex = index;
            if (profileIcon != null) profileIcon.sprite = sprite;
            ValidateInputs();
        }

        private void ValidateInputs()
        {
            string name = userName?.text?.Trim();

            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 20)
            {
                if (save != null) save.interactable = false;
                return;
            }

            HideError();
            if (save != null) save.interactable = true;
        }

        private async void OnSaveClicked()
        {
            if (save != null) save.interactable = false;
            HideError();

            var profileService = ServiceLocator.Get<IProfileService>();
            if (profileService == null)
            {
                ShowError("Profile system unavailable.");
                if (save != null) save.interactable = true;
                return;
            }

            string displayName = userName?.text?.Trim() ?? "";

            try
            {
                var results = await profileService.UpdatePlayerDataAsync(displayName, selectedAvatarIndex.ToString());

                if (results.userName && results.avatar)
                {
                    Hide();
                    var authService = ServiceLocator.Get<IAuthService>();
                    if (authService != null)
                    {
                        await authService.OnProfileSetupSuccess();
                    }
                }
                else
                {
                    ShowError("Failed to save profile. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SetPlayerProfileUI] Exception: {ex}");
                ShowError("Unexpected error. Please try again.");
            }
            finally
            {
                if (save != null) save.interactable = true;
            }
        }

        private void ShowError(string message)
        {
            if (error != null)
            {
                error.text = message;
                error.gameObject.SetActive(true);
            }
        }

        private void HideError()
        {
            if (error != null) error.gameObject.SetActive(false);
        }
#else
        public void Show() { }
        public void Hide() { }
#endif
    }
}