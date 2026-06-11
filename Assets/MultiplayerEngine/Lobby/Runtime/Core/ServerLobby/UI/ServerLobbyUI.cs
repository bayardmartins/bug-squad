using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// UI for server-based lobbies.
    /// No ready/start buttons — shows a "Join Game" button for players to connect directly.
    /// Shows server status and player list without ready indicators.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ServerLobbyUI : MonoBehaviour
    {
        public static ServerLobbyUI Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("Button to leave the server lobby.")]
        [SerializeField] private Button leaveButton;

        [Tooltip("Button to join the running game directly.")]
        [SerializeField] private Button joinGameButton;

        [Tooltip("Displays the lobby join code.")]
        [SerializeField] private TMP_Text joinCode;

        [Tooltip("Displays the server/lobby name.")]
        [SerializeField] private TMP_Text serverNameText;

        [Tooltip("Displays current/max players.")]
        [SerializeField] private TMP_Text playerCountText;

        [Tooltip("Displays the server status (Waiting / In Game).")]
        [SerializeField] private TMP_Text serverStatusText;

        [Tooltip("Button to copy lobby code.")]
        [SerializeField] private Button copyCodeButton;

        [Header("Lobby Components")]
        [Tooltip("Handles character selection in the lobby.")]
        [SerializeField] private LobbyCharacterSelection characterSelection;

        [Tooltip("Prefab for displaying each player in the lobby.")]
        [SerializeField] private LobbyPlayerItem lobbyPlayerItemPrefab;

        [Tooltip("UI container for player items.")]
        [SerializeField] private RectTransform playerItemHolder;

        private readonly Dictionary<string, LobbyPlayerItem> lobbyPlayers = new();
        private CanvasGroup canvasGroup;

        private const string JoinCodePrefix = "SERVER ID : ";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();

            SubscribeEvents();
            SetupButtons();
            SetCanvasGroup(false);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            LobbyManagerBase.OnLobbyCreated += InitializeLobby;
            LobbyManagerBase.OnLobbyJoined += InitializeLobby;
            LobbyManagerBase.OnLobbyUpdated += UpdateLobbyData;
            LobbyManagerBase.OnLobbyPlayerDataUpdated += UpdatePlayerData;
        }

        private void UnsubscribeEvents()
        {
            LobbyManagerBase.OnLobbyCreated -= InitializeLobby;
            LobbyManagerBase.OnLobbyJoined -= InitializeLobby;
            LobbyManagerBase.OnLobbyUpdated -= UpdateLobbyData;
            LobbyManagerBase.OnLobbyPlayerDataUpdated -= UpdatePlayerData;
        }

        private void SetupButtons()
        {
            leaveButton?.onClick.AddListener(OnLeaveButtonClicked);
            joinGameButton?.onClick.AddListener(OnJoinGameButtonClicked);
            copyCodeButton?.onClick.AddListener(OnCopyCodeButtonClicked);
        }

        // ─── Button Handlers ───

        private async void OnLeaveButtonClicked()
        {
            if (leaveButton != null) leaveButton.interactable = false;

            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.LeaveLobby();
                }
                SetCanvasGroup(false);
                MainMenuUI.Instance?.ShowMainMenu();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerLobbyUI] Error leaving server lobby: {ex}");
            }
            finally
            {
                if (leaveButton != null) leaveButton.interactable = true;
            }
        }

        private async void OnJoinGameButtonClicked()
        {
            if (joinGameButton != null) joinGameButton.interactable = false;

            LoadingScreen.Instance?.ShowLoading("Joining game...");

            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.StartGameAsync();
                }
                else
                {
                    Debug.LogError("[ServerLobbyUI] No lobby service registered.");
                    LoadingScreen.Instance?.HideLoading();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerLobbyUI] Error joining game: {ex}");
                LoadingScreen.Instance?.HideLoading();
                if (joinGameButton != null) joinGameButton.interactable = true;
            }
        }

        private void OnCopyCodeButtonClicked()
        {
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService?.CurrentLobbyData != null)
            {
                GUIUtility.systemCopyBuffer = lobbyService.CurrentLobbyData.JoinCode;
            }
        }

        // ─── Lobby Data Handlers ───

        private void InitializeLobby(LobbyData lobbyData)
        {
            // Only respond to ServerBased lobby events
            if (lobbyData?.LobbyType != LobbyType.ServerBased) return;

            SetCanvasGroup(true);
            MainMenuUI.Instance?.Hide();

            if (lobbyData == null) return;

            ClearPlayerItems();
            UpdateLobbyData(lobbyData);
            UpdatePlayerData(lobbyData);
        }

        private void UpdateLobbyData(LobbyData lobbyData)
        {
            if (lobbyData == null || lobbyData.LobbyType != LobbyType.ServerBased) return;

            if (joinCode != null) joinCode.text = $"{JoinCodePrefix}{lobbyData.JoinCode}";
            if (serverNameText != null) serverNameText.text = $"SERVER : <color=#00FF88>{lobbyData.LobbyName.ToUpper()}</color>";

            // Update server status based on whether game has started
            UpdateServerStatus(lobbyData);

            // Host sees "Join Game" button (to start hosting), others see it too (to connect)
            if (joinGameButton != null)
            {
                bool hasGameId = lobbyData.Data != null
                    && lobbyData.Data.TryGetValue(LobbyDataKeys.GameId, out var gameId)
                    && !string.IsNullOrEmpty(gameId);

                // If game is running, show "Join Game"; if not and player is host, show "Start Server"
                var profileService = ServiceLocator.Get<IProfileService>();
                bool isHost = lobbyData.HostId == profileService?.LocalPlayerStats?.PlayerId;

                if (hasGameId)
                {
                    SetButtonText(joinGameButton, "Join Game");
                    joinGameButton.gameObject.SetActive(true);
                }
                else if (isHost)
                {
                    SetButtonText(joinGameButton, "Start Server");
                    joinGameButton.gameObject.SetActive(true);
                }
                else
                {
                    joinGameButton.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateServerStatus(LobbyData lobbyData)
        {
            if (serverStatusText == null) return;

            bool hasGameId = lobbyData.Data != null
                && lobbyData.Data.TryGetValue(LobbyDataKeys.GameId, out var gameId)
                && !string.IsNullOrEmpty(gameId);

            serverStatusText.text = hasGameId
                ? "STATUS : <color=#00FF88>IN GAME</color>"
                : "STATUS : <color=#FFAA00>WAITING</color>";
        }

        private void UpdatePlayerData(LobbyData data)
        {
            if (data?.Players == null || data.LobbyType != LobbyType.ServerBased) return;

            if (lobbyPlayerItemPrefab == null || playerItemHolder == null) return;

            // Sort: host first
            var sortedPlayers = data.Players.OrderBy(p => p.PlayerId == data.HostId ? 0 : 1).ToList();

            foreach (var player in sortedPlayers)
            {
                if (string.IsNullOrEmpty(player.PlayerId)) continue;

                if (!lobbyPlayers.ContainsKey(player.PlayerId))
                {
                    var newPlayerItem = Instantiate(lobbyPlayerItemPrefab, playerItemHolder);
                    newPlayerItem.UpdatePlayer(player, data);
                    lobbyPlayers.Add(player.PlayerId, newPlayerItem);
                }
                else
                {
                    lobbyPlayers[player.PlayerId].UpdatePlayer(player, data);
                }
            }

            // Remove players who left
            var playerIdsToRemove = lobbyPlayers.Keys
                .Where(id => !data.Players.Any(p => p.PlayerId == id))
                .ToList();

            foreach (var playerId in playerIdsToRemove)
            {
                Destroy(lobbyPlayers[playerId].gameObject);
                lobbyPlayers.Remove(playerId);
            }

            if (playerCountText != null)
            {
                playerCountText.text = $"PLAYERS : <color=#00FF88>{data.Players.Count}/{data.MaxPlayers}</color>";
            }
        }

        // ─── Helpers ───

        private void ClearPlayerItems()
        {
            foreach (Transform child in playerItemHolder)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            lobbyPlayers.Clear();
        }

        private void SetCanvasGroup(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void SetButtonText(Button button, string text)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
        }
    }
}
