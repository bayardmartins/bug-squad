using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages the lobby UI elements and interactions.
    /// Handles player list, game mode, privacy, and button events.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LobbyUI : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of the LobbyUI.
        /// </summary>
        public static LobbyUI Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("Button to leave the lobby.")]
        [SerializeField] private Button leaveButton;

        [Tooltip("Button for the host to start the game.")]
        [SerializeField] private Button startGameButton;

        [Tooltip("Button for players to mark themselves as ready.")]
        [SerializeField] private Button readyGameButton;

        [Tooltip("Button to toggle lobby privacy between public and private.")]
        [SerializeField] private Button privateButton;

        [Tooltip("Button to change the game mode.")]
        [SerializeField] private Button gameModeButton;

        [Tooltip("Displays the lobby join code.")]
        [SerializeField] private TMP_Text joinCode;

        [Tooltip("Displays the lobby name.")]
        [SerializeField] private TMP_Text lobbyNameText;

        [Tooltip("Displays current/max players.")]
        [SerializeField] private TMP_Text playerCountText;

        [Tooltip("Button to copy lobby code.")]
        [SerializeField] private Button copyCodeButton;

        [Header("Lobby Components")]
        [Tooltip("Handles character selection in the lobby. Attach character selection panel if there is any character selection panels avalable in lobby, other than you can skip this")]
        [SerializeField] private LobbyCharacterSelection characterSelection;

        [Tooltip("Prefab for displaying each player in the lobby.")]
        [SerializeField] private LobbyPlayerItem lobbyPlayerItemPrefab;

        [Header("Invite Slots")]
        [Tooltip("Enable or disable invite placeholder slots for empty player spots.")]
        [SerializeField] private bool enableInviteSlots = true;

        [Tooltip("Prefab for displaying invite placeholder in empty slots.")]
        [SerializeField] private LobbyEmptySlotItem lobbyEmptySlotItemPrefab;

        [Tooltip("UI container for player items.")]
        [SerializeField]
        private RectTransform playerItemHolder;

        private bool isLobbyPrivate = true;
        private GameMode currentGameMode = GameMode.FreeForAll;

        private readonly Dictionary<string, LobbyPlayerItem> lobbyPlayers = new();
        private readonly List<LobbyEmptySlotItem> emptySlotItems = new();

        private const string PrivateText = "Private";
        private const string PublicText = "Public";
        private const string JoinCodePrefix = "LOBBY ID : ";

        private CanvasGroup canvasGroup;

        /// <summary>
        /// Ensures singleton instance and destroys duplicate objects.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();

            // Subscribe to lobby events early
            SubscribeLobbyEvents();

            // Setup button listeners
            SetupButtonListeners();

            // Initialize sub-components (VoiceControlUI, CharacterSelection, etc.)
            Initialize();

            // Initially hidden
            SetCanvasGroup(false);
        }

        /// <summary>
        /// Initializes sub-components of the lobby UI.
        /// </summary>
        public void Initialize()
        {
            characterSelection?.Initialize();
        }

        /// <summary>
        /// Validates that all required UI references are set in the inspector.
        /// </summary>
        /// <returns>True if all references are valid; otherwise, false.</returns>
        private bool ValidateUIReferences()
        {
            if (leaveButton == null || startGameButton == null || readyGameButton == null ||
                privateButton == null || gameModeButton == null || joinCode == null ||
                lobbyPlayerItemPrefab == null || playerItemHolder == null)
            {
                Debug.LogError("LobbyUI: One or more UI references are not set in the inspector.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sets up button click listeners for lobby actions.
        /// </summary>
        private void SetupButtonListeners()
        {
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            readyGameButton.onClick.AddListener(OnReadyGameButtonClicked);
            gameModeButton.onClick.AddListener(OnGameModeButtonClicked);
            privateButton.onClick.AddListener(OnPrivateButtonClicked);
            copyCodeButton?.onClick.AddListener(OnCopyCodeButtonClicked);
        }

        /// <summary>
        /// Subscribes to lobby-related events for UI updates.
        /// </summary>
        private void SubscribeLobbyEvents()
        {
            LobbyManagerBase.OnLobbyCreated += InitializeLobby;
            LobbyManagerBase.OnLobbyJoined += InitializeLobby;
            LobbyManagerBase.OnLobbyUpdated += UpdateLobbyData;
            LobbyManagerBase.OnLobbyPlayerDataUpdated += UpdatePlayerData;
        }

        /// <summary>
        /// Unsubscribes from lobby-related events to prevent memory leaks.
        /// </summary>
        private void UnsubscribeLobbyEvents()
        {
            LobbyManagerBase.OnLobbyCreated -= InitializeLobby;
            LobbyManagerBase.OnLobbyJoined -= InitializeLobby;
            LobbyManagerBase.OnLobbyUpdated -= UpdateLobbyData;
            LobbyManagerBase.OnLobbyPlayerDataUpdated -= UpdatePlayerData;
        }

        /// <summary>
        /// Called when the object is destroyed; unsubscribes from events.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeLobbyEvents();
        }

        /// <summary>
        /// Handles the leave button click event and leaves the lobby.
        /// </summary>
        private async void OnLeaveButtonClicked()
        {
            leaveButton.interactable = false;

            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.LeaveLobby();
                }
                else
                {
                    Debug.LogError("No lobby service registered.");
                }
                SetCanvasGroup(false);
                MainMenuUI.Instance?.ShowMainMenu();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error leaving lobby: {ex}");
            }
            finally
            {
                leaveButton.interactable = true;
            }
        }

        /// <summary>
        /// Handles the start game button click event and starts the game.
        /// </summary>
        private async void OnStartGameButtonClicked()
        {
            startGameButton.enabled = false;

            // Show loading screen immediately
            LoadingScreen.Instance?.ShowLoading("Starting game...");

            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.StartGameAsync();
                }
                else
                {
                    Debug.LogError("No lobby service registered.");
                    LoadingScreen.Instance?.HideLoading();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error starting game: {ex}");
                LoadingScreen.Instance?.HideLoading();
                startGameButton.enabled = true;
            }
        }

        /// <summary>
        /// Handles the ready button click event and updates the player's ready state.
        /// </summary>
        private async void OnReadyGameButtonClicked()
        {
            readyGameButton.interactable = false;
            try
            {
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.UpdateReadyState("true");
                }
                else
                {
                    Debug.LogWarning("No lobby service registered.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error updating player ready state: {ex}");
            }
        }

        /// <summary>
        /// Handles the privacy button click event and toggles lobby privacy.
        /// </summary>
        private async void OnPrivateButtonClicked()
        {
            privateButton.interactable = false;
            try
            {
                isLobbyPrivate = !isLobbyPrivate;
                UpdatePrivateButtonText();
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.UpdateLobbyPrivacy(isLobbyPrivate);
                }
                else
                {
                    Debug.LogWarning("No lobby service registered.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error updating lobby privacy: {ex}");
            }
            finally
            {
                privateButton.interactable = true;
            }
        }

        /// <summary>
        /// Handles the game mode button click event and cycles through available game modes.
        /// </summary>
        private async void OnGameModeButtonClicked()
        {
            gameModeButton.interactable = false;
            try
            {
                currentGameMode = GetNextGameMode(currentGameMode);
                UpdateGameModeButtonText();
                var lobbyService = ServiceLocator.Get<ILobbyService>();
                if (lobbyService != null)
                {
                    await lobbyService.UpdateGameMode(currentGameMode);
                }
                else
                {
                    Debug.LogWarning("No lobby service registered.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error updating game mode: {ex}");
            }
            finally
            {
                gameModeButton.interactable = true;
            }
        }

        private void OnCopyCodeButtonClicked()
        {
            var lobbyService = ServiceLocator.Get<ILobbyService>();
            if (lobbyService != null && lobbyService.CurrentLobbyData != null)
            {
                GUIUtility.systemCopyBuffer = lobbyService.CurrentLobbyData.JoinCode;
            }
        }

        /// <summary>
        /// Returns the next game mode in the cycle.
        /// </summary>
        /// <param name="mode">Current game mode.</param>
        /// <returns>Next game mode.</returns>
        private static GameMode GetNextGameMode(GameMode mode)
        {
            return mode switch
            {
                GameMode.TeamDeathmatch => GameMode.CaptureTheFlag,
                GameMode.CaptureTheFlag => GameMode.FreeForAll,
                GameMode.FreeForAll => GameMode.TeamDeathmatch,
                _ => GameMode.FreeForAll
            };
        }

        /// <summary>
        /// Initializes the lobby UI with the provided lobby data.
        /// </summary>
        /// <param name="lobbyData">Lobby data to initialize UI.</param>
        private void InitializeLobby(LobbyData lobbyData)
        {
            // Only respond to General lobby events
            if (lobbyData?.LobbyType != LobbyType.General) return;

            SetCanvasGroup(true);
            MainMenuUI.Instance?.Hide();

            if (lobbyData == null) return;

            ClearPlayerItems();

            UpdateLobbyData(lobbyData);
            UpdatePlayerData(lobbyData);
        }

        private void SetCanvasGroup(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Updates the player list UI based on the latest lobby data.
        /// Adds, updates, or removes player items as needed.
        /// </summary>
        /// <param name="data">Latest lobby data.</param>
        /// <summary>
        /// Updates the player list UI based on the latest lobby data.
        /// Adds, updates, or removes player items as needed while maintaining proper ordering.
        /// </summary>
        /// <param name="data">Latest lobby data.</param>
        private void UpdatePlayerData(LobbyData data)
        {
            if (data?.Players == null || data.LobbyType != LobbyType.General) return;

            if (lobbyPlayerItemPrefab == null)
            {
                Debug.LogError("LobbyUI: lobbyPlayerItemPrefab is not assigned in the Inspector!");
                return;
            }

            if (playerItemHolder == null)
            {
                Debug.LogError("LobbyUI: playerItemHolder is not assigned in the Inspector!");
                return;
            }

            // Sort players by join order or ensure host is first
            var sortedPlayers = data.Players.OrderBy(p => p.PlayerId == data.HostId ? 0 : 1).ToList();

            foreach (var player in sortedPlayers)
            {
                // Skip players with null or empty PlayerId to prevent duplicate entries
                if (string.IsNullOrEmpty(player.PlayerId))
                {
                    Debug.LogWarning("LobbyUI: Skipping player with null or empty PlayerId");
                    continue;
                }

                if (!lobbyPlayers.ContainsKey(player.PlayerId))
                {
                    // Find the correct insertion point by replacing the first empty slot
                    int insertIndex = GetInsertionIndex();

                    var newPlayerItem = Instantiate(lobbyPlayerItemPrefab, playerItemHolder);
                    newPlayerItem.UpdatePlayer(player, data);

                    // Set the correct sibling index to maintain order
                    newPlayerItem.transform.SetSiblingIndex(insertIndex);

                    lobbyPlayers.Add(player.PlayerId, newPlayerItem);

                    // Remove one empty slot since a player took its place
                    RemoveOneEmptySlot();
                }
                else
                {
                    lobbyPlayers[player.PlayerId].UpdatePlayer(player, data);
                }
            }

            var playerIdsToRemove = lobbyPlayers.Keys
                .Where(id => !data.Players.Any(p => p.PlayerId == id))
                .ToList();

            foreach (var playerId in playerIdsToRemove)
            {
                // Get the index of the leaving player to add empty slot at same position
                int playerIndex = lobbyPlayers[playerId].transform.GetSiblingIndex();

                Destroy(lobbyPlayers[playerId].gameObject);
                lobbyPlayers.Remove(playerId);

                // Add an empty slot at the same position if invite slots are enabled
                if (enableInviteSlots && lobbyEmptySlotItemPrefab != null)
                {
                    AddEmptySlotAtIndex(playerIndex);
                }
            }

            if (playerCountText != null)
            {
                playerCountText.text = $"PLAYERS : <color=#00FFFF>{data.Players.Count}/{data.MaxPlayers}</color>";
            }

            CheckAllPlayersReady(data);

            // Update invite slots for remaining empty player spots
            UpdateInviteSlots(data);
        }

        /// <summary>
        /// Gets the insertion index for a new player by finding the first empty slot position.
        /// </summary>
        /// <returns>The index where the new player should be inserted.</returns>
        private int GetInsertionIndex()
        {
            if (emptySlotItems.Count > 0)
            {
                // Return the index of the first empty slot
                return emptySlotItems[0].transform.GetSiblingIndex();
            }

            // If no empty slots, add at the end
            return playerItemHolder.childCount;
        }

        /// <summary>
        /// Removes one empty slot item (typically the first one).
        /// </summary>
        private void RemoveOneEmptySlot()
        {
            if (emptySlotItems.Count > 0)
            {
                var slotToRemove = emptySlotItems[0];
                emptySlotItems.RemoveAt(0);
                Destroy(slotToRemove.gameObject);
            }
        }

        /// <summary>
        /// Adds an empty slot at the specified index.
        /// </summary>
        /// <param name="index">The index where to insert the empty slot.</param>
        private void AddEmptySlotAtIndex(int index)
        {
            if (lobbyEmptySlotItemPrefab == null) return;

            var newEmptySlot = Instantiate(lobbyEmptySlotItemPrefab, playerItemHolder);
            newEmptySlot.transform.SetSiblingIndex(index);
            emptySlotItems.Insert(0, newEmptySlot); // Insert at beginning for easier management
        }

        /// <summary>
        /// Updates the invite placeholder slots based on available space in the lobby.
        /// Now maintains proper positioning and only adjusts the total count.
        /// </summary>
        /// <param name="data">Latest lobby data.</param>
        private void UpdateInviteSlots(LobbyData data)
        {
            if (!enableInviteSlots || lobbyEmptySlotItemPrefab == null || playerItemHolder == null)
            {
                ClearEmptySlotItems();
                return;
            }

            int currentPlayerCount = data?.Players?.Count ?? 0;
            int maxPlayers = data?.MaxPlayers ?? 0;
            int requiredEmptySlots = maxPlayers - currentPlayerCount;

            // Add additional empty slots if needed
            while (emptySlotItems.Count < requiredEmptySlots)
            {
                var newEmptySlot = Instantiate(lobbyEmptySlotItemPrefab, playerItemHolder);
                // Add new empty slots at the end
                emptySlotItems.Add(newEmptySlot);
            }

            // Remove excess empty slots from the end
            while (emptySlotItems.Count > requiredEmptySlots)
            {
                int lastIndex = emptySlotItems.Count - 1;
                Destroy(emptySlotItems[lastIndex].gameObject);
                emptySlotItems.RemoveAt(lastIndex);
            }
        }

        /// <summary>
        /// Clears all empty slot placeholder items.
        /// </summary>
        private void ClearEmptySlotItems()
        {
            foreach (var slotItem in emptySlotItems)
            {
                if (slotItem != null)
                    Destroy(slotItem.gameObject);
            }
            emptySlotItems.Clear();
        }

        /// <summary>
        /// Updates the lobby UI elements (join code, privacy, game mode, button states) based on lobby data.
        /// </summary>
        /// <param name="lobbyData">Latest lobby data.</param>
        private void UpdateLobbyData(LobbyData lobbyData)
        {
            if (lobbyData == null || lobbyData.LobbyType != LobbyType.General) return;

            joinCode.text = $"{JoinCodePrefix}{lobbyData.JoinCode}";
            if (lobbyNameText != null) lobbyNameText.text = $"LOBBY : <color=#00FFFF>{lobbyData.LobbyName.ToUpper()}</color>";

            isLobbyPrivate = lobbyData.IsPrivate;
            currentGameMode = ParseGameModeFromLobbyData(lobbyData);

            UpdatePrivateButtonText();
            UpdateGameModeButtonText();

            var profileService = ServiceLocator.Get<IProfileService>();
            if (lobbyData.HostId == profileService?.LocalPlayerStats?.PlayerId)
                SetHostUIState();
            else
                SetPlayerUIState();
        }

        /// <summary>
        /// Checks if all non-host players are ready and enables the start game button if so.
        /// </summary>
        /// <param name="lobbyData">Lobby data to check readiness.</param>
        private void CheckAllPlayersReady(LobbyData lobbyData)
        {
            var profileService = ServiceLocator.Get<IProfileService>();
            if (lobbyData == null || profileService == null) return;

            var localPlayerId = profileService.LocalPlayerStats?.PlayerId;
            if (lobbyData.HostId == localPlayerId)
            {
                bool allReady = lobbyData.Players
                    .Where(p => p.PlayerId != lobbyData.HostId)
                    .All(p => p.Data.ContainsKey(PlayerDataKeys.PlayerReady) && p.Data[PlayerDataKeys.PlayerReady] == "true");

                startGameButton.interactable = allReady;
            }
        }

        /// <summary>
        /// Updates the privacy button text to reflect the current lobby privacy state.
        /// </summary>
        private void UpdatePrivateButtonText()
        {
            var textComponent = privateButton.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = isLobbyPrivate ? PrivateText : PublicText;
        }

        /// <summary>
        /// Updates the game mode button text to reflect the current game mode.
        /// </summary>
        private void UpdateGameModeButtonText()
        {
            var textComponent = gameModeButton.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = currentGameMode.ToString();
        }

        /// <summary>
        /// Removes all player items from the player item holder UI.
        /// </summary>
        private void ClearPlayerItems()
        {
            foreach (Transform child in playerItemHolder)
            {
                // Immediately disable to prevent same-frame visibility issues
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            lobbyPlayers.Clear();
            emptySlotItems.Clear();
        }

        /// <summary>
        /// Parses the game mode from the lobby data dictionary.
        /// </summary>
        /// <param name="lobbyData">Lobby data containing game mode info.</param>
        /// <returns>Parsed GameMode value, or FreeForAll if parsing fails.</returns>
        private static GameMode ParseGameModeFromLobbyData(LobbyData lobbyData)
        {
            if (lobbyData.Data != null && lobbyData.Data.TryGetValue(LobbyDataKeys.GameMode, out var modeStr))
            {
                return Enum.TryParse(modeStr, out GameMode mode) ? mode : GameMode.FreeForAll;
            }
            return GameMode.FreeForAll;
        }

        /// <summary>
        /// Sets the UI state for the host (enables host controls).
        /// </summary>
        private void SetHostUIState()
        {
            gameModeButton.interactable = true;
            privateButton.interactable = true;
            readyGameButton.gameObject.SetActive(false);
            startGameButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Sets the UI state for non-host players (enables ready controls).
        /// </summary>
        private void SetPlayerUIState()
        {
            gameModeButton.interactable = false;
            privateButton.interactable = false;
            readyGameButton.gameObject.SetActive(true);
            startGameButton.gameObject.SetActive(false);
        }


    }
}