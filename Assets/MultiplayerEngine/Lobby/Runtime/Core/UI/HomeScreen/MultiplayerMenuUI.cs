using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Visibility options for lobby creation.
    /// </summary>
    public enum LobbyVisibility
    {
        Public,
        Private,
        Locked  // Requires password to join
    }

    /// <summary>
    /// Combined multiplayer menu handling both lobby creation and joining.
    /// Uses a side panel to show either Create Lobby or Lobby List sections.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MultiplayerMenuUI : MonoBehaviour
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private Button backButton;

        [Header("Join By Code")]
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinByCodeButton;

        [Header("Side Panel")]
        [SerializeField] private CanvasGroup createLobbyPanel;
        [SerializeField] private CanvasGroup lobbyListPanel;

        [Header("Create Lobby Settings")]
        [SerializeField] private TMP_InputField lobbyNameInput;
        [SerializeField] private Button maxPlayersButton;
        [SerializeField] private Button gameModeButton;
        [SerializeField] private Button visibilityButton;
        [SerializeField] private Button confirmCreateButton;
        [SerializeField] private TMP_InputField lobbyPasswordInput;

        [Header("Join Password Popup")]
        [SerializeField] private CanvasGroup joinPasswordPopup;
        [SerializeField] private TMP_InputField popupPasswordInput;
        [SerializeField] private Button popupJoinButton;
        [SerializeField] private Button popupCancelButton;

        [Header("Lobby List")]
        [SerializeField] private RectTransform lobbyListContainer;
        [SerializeField] private LobbyListItem lobbyListItemPrefab;
        [SerializeField] private Button refreshButton;

        private CanvasGroup canvasGroup;
        private int maxPlayers = 4;
        private LobbyVisibility visibility = LobbyVisibility.Public;
        private GameMode gameMode = GameMode.FreeForAll;
        private string pendingLobbyId; // Lobby ID waiting for password
        private string pendingJoinCode; // Join Code waiting for password

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            SetupButtons();

            // Subscribe to Locked Lobby clicks
            LobbyListItem.OnLockedLobbyClicked += ShowJoinPasswordPopup;

            Hide();
        }

        private void OnDestroy()
        {
            LobbyListItem.OnLockedLobbyClicked -= ShowJoinPasswordPopup;
        }

        private void SetupButtons()
        {
            // Navigation
            createLobbyButton?.onClick.AddListener(ShowCreateLobbyPanel);
            joinLobbyButton?.onClick.AddListener(ShowLobbyListPanel);
            backButton?.onClick.AddListener(() => MainMenuUI.Instance?.BackToMainMenu());

            // Join by code
            joinByCodeButton?.onClick.AddListener(OnJoinByCodeClicked);

            // Create lobby settings
            maxPlayersButton?.onClick.AddListener(CycleMaxPlayers);
            gameModeButton?.onClick.AddListener(CycleGameMode);
            visibilityButton?.onClick.AddListener(CycleVisibility);
            confirmCreateButton?.onClick.AddListener(OnCreateLobbyClicked);

            // Lobby list
            refreshButton?.onClick.AddListener(RefreshLobbyList);

            // Join Password Popup
            popupJoinButton?.onClick.AddListener(OnPopupJoinClicked);
            popupCancelButton?.onClick.AddListener(HideJoinPasswordPopup);

            UpdateSettingsTexts();
            UpdatePasswordPanelVisibility();
            HideJoinPasswordPopup();
        }

        #region Show/Hide

        public void Show()
        {
            SetCanvasGroup(canvasGroup, true);
            ShowCreateLobbyPanel();
        }

        public void Hide()
        {
            SetCanvasGroup(canvasGroup, false);
            HideAllPanels();
            HideJoinPasswordPopup();
        }

        private void ShowCreateLobbyPanel()
        {
            SetCanvasGroup(createLobbyPanel, true);
            SetCanvasGroup(lobbyListPanel, false);
            UpdatePasswordPanelVisibility();
        }

        private async void ShowLobbyListPanel()
        {
            SetCanvasGroup(createLobbyPanel, false);
            SetCanvasGroup(lobbyListPanel, true);
            await RefreshLobbyListAsync();
        }

        private void HideAllPanels()
        {
            SetCanvasGroup(createLobbyPanel, false);
            SetCanvasGroup(lobbyListPanel, false);
        }

        private void SetCanvasGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        #endregion

        #region Create Lobby

        private void CycleMaxPlayers()
        {
            maxPlayers = maxPlayers >= 8 ? 2 : maxPlayers + 1;
            UpdateSettingsTexts();
        }

        private void CycleGameMode()
        {
            gameMode = gameMode switch
            {
                GameMode.FreeForAll => GameMode.TeamDeathmatch,
                GameMode.TeamDeathmatch => GameMode.CaptureTheFlag,
                _ => GameMode.FreeForAll
            };
            UpdateSettingsTexts();
        }

        private void CycleVisibility()
        {
            visibility = visibility switch
            {
                LobbyVisibility.Public => LobbyVisibility.Private,
                LobbyVisibility.Private => LobbyVisibility.Locked,
                _ => LobbyVisibility.Public
            };
            UpdateSettingsTexts();
            UpdatePasswordPanelVisibility();
        }

        private void UpdatePasswordPanelVisibility()
        {
            bool showPassword = visibility == LobbyVisibility.Locked;
            lobbyPasswordInput?.gameObject.SetActive(showPassword);
        }

        private void UpdateSettingsTexts()
        {
            SetButtonText(maxPlayersButton, maxPlayers.ToString());
            SetButtonText(gameModeButton, gameMode.ToString());
            SetButtonText(visibilityButton, visibility.ToString());
        }

        private void SetButtonText(Button button, string text)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
        }

        private async void OnCreateLobbyClicked()
        {
            if (confirmCreateButton != null) confirmCreateButton.interactable = false;

            string lobbyName = string.IsNullOrEmpty(lobbyNameInput?.text) ? "New Lobby" : lobbyNameInput.text;
            string password = visibility == LobbyVisibility.Locked ? lobbyPasswordInput?.text ?? "" : "";
            bool isPrivate = visibility != LobbyVisibility.Public;

            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    var result = await lobbyService.CreateLobby(lobbyName, isPrivate, maxPlayers, gameMode, password);
                }
                else
                {
                    Debug.LogError("[MultiplayerMenuUI] No lobby service registered!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MultiplayerMenuUI] Exception during CreateLobby: {ex}");
            }

            if (confirmCreateButton != null) confirmCreateButton.interactable = true;
        }

        #endregion

        #region Join Lobby

        private void OnJoinByCodeClicked()
        {
            if (string.IsNullOrEmpty(joinCodeInput?.text)) return;

            // Show password popup for joining by code
            ShowJoinPasswordPopupForCode(joinCodeInput.text.Trim());
        }

        private async void RefreshLobbyList()
        {
            if (refreshButton != null) refreshButton.interactable = false;
            await RefreshLobbyListAsync();
            if (refreshButton != null) refreshButton.interactable = true;
        }

        private async System.Threading.Tasks.Task RefreshLobbyListAsync()
        {
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService == null || lobbyListContainer == null) return;

            foreach (Transform child in lobbyListContainer)
            {
                Destroy(child.gameObject);
            }

            var lobbies = await lobbyService.GetLobbyListAsync();
            if (lobbies != null && lobbyListItemPrefab != null)
            {
                foreach (var lobby in lobbies)
                {
                    var item = Instantiate(lobbyListItemPrefab, lobbyListContainer);
                    item.SetUp(lobby);
                }
            }
        }

        #endregion

        #region Join Password Popup

        private void ShowJoinPasswordPopup(string lobbyId)
        {
            pendingLobbyId = lobbyId;
            pendingJoinCode = null;
            if (popupPasswordInput != null) popupPasswordInput.text = "";
            SetCanvasGroup(joinPasswordPopup, true);
        }

        private void ShowJoinPasswordPopupForCode(string code)
        {
            pendingJoinCode = code;
            pendingLobbyId = null;
            if (popupPasswordInput != null) popupPasswordInput.text = "";
            SetCanvasGroup(joinPasswordPopup, true);
        }

        private void HideJoinPasswordPopup()
        {
            pendingLobbyId = null;
            pendingJoinCode = null;
            SetCanvasGroup(joinPasswordPopup, false);
        }

        private async void OnPopupJoinClicked()
        {
            if (popupJoinButton != null) popupJoinButton.interactable = false;

            string password = popupPasswordInput?.text ?? "";
            LobbyData result = null;

            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService != null)
            {
                if (!string.IsNullOrEmpty(pendingLobbyId))
                {
                    result = await lobbyService.JoinLobby(pendingLobbyId, password);
                }
                else if (!string.IsNullOrEmpty(pendingJoinCode))
                {
                    result = await lobbyService.JoinLobbyByCode(pendingJoinCode, password);
                }
            }

            // Only hide popup on success; keep it open on failure so user can retry or cancel
            if (result != null)
            {
                HideJoinPasswordPopup();
            }

            if (popupJoinButton != null) popupJoinButton.interactable = true;
        }

        #endregion
    }
}
