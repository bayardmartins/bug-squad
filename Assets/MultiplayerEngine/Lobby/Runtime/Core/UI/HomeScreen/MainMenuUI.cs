using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Main menu UI controller. Handles navigation between main menu and multiplayer menu.
    /// Uses CanvasGroups for smooth visibility control.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        [Header("Main Menu Buttons")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Header("Panels")]
        [Tooltip("The inner panel holding the main menu buttons.")]
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private MultiplayerMenuUI multiplayerMenu;

        [Header("Optional Components")]
        [SerializeField] private PlayerProfileCard playerProfileCard;

        // The CanvasGroup attached to THIS game object (Root)
        private CanvasGroup rootCanvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            rootCanvasGroup = GetComponent<CanvasGroup>();

            // Subscribe to auth completion
            AuthenticationManager.OnAuthCompleted += OnAuthCompleted;

            SetupButtons();

            // Initially hidden (Root level)
            SetCanvasGroup(rootCanvasGroup, false);
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnAuthCompleted -= OnAuthCompleted;
        }

        private void SetupButtons()
        {
            // Disable unassigned buttons
            if (singlePlayerButton != null)
            {
                singlePlayerButton.interactable = false; // Not implemented yet
            }

            if (settingsButton != null)
            {
                settingsButton.interactable = false; // Not implemented yet
            }

            // Multiplayer button
            if (multiplayerButton != null)
            {
                multiplayerButton.onClick.AddListener(OpenMultiplayerMenu);
            }

            // Exit button
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(() => Application.Quit());
            }
        }

        private void OnAuthCompleted()
        {
            // Make Root visible
            SetCanvasGroup(rootCanvasGroup, true);
            // Show the main menu panel
            ShowMainMenu();
            playerProfileCard?.Initialize();
        }

        #region Menu Navigation

        public void ShowMainMenu()
        {
            // Ensure root is visible
            SetCanvasGroup(rootCanvasGroup, true);
            // Show buttons panel
            SetCanvasGroup(mainMenuGroup, true);
            multiplayerMenu?.Hide();
        }

        public void Hide()
        {
            // Hide everything (Root level)
            SetCanvasGroup(rootCanvasGroup, false);
        }

        public void OpenMultiplayerMenu()
        {
            // Ensure root is visible
            SetCanvasGroup(rootCanvasGroup, true);
            // Hide buttons panel
            SetCanvasGroup(mainMenuGroup, false);
            multiplayerMenu?.Show();
        }

        public void BackToMainMenu()
        {
            ShowMainMenu();
        }

        private void SetCanvasGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        #endregion
    }
}
