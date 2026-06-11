using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Displays individual player information in the lobby player list.
    /// Includes voice controls for other players (not local player).
    /// </summary>
    public class LobbyPlayerItem : MonoBehaviour
    {
        [Header("Player Info")]
        [SerializeField] private Image characterImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text characterNameText;

        [Header("Status Indicators")]
        [SerializeField] private Image ownerImage;
        [SerializeField] private Image readyImage;
        [SerializeField] private TMP_Text readyText;

        [Header("Actions")]
        [SerializeField] private Button kickPlayer;

        [Header("Voice Controls (For Other Players)")]
        [SerializeField] private GameObject voiceControlsContainer;
        [SerializeField] private Image speakingIndicator;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button muteButton;
        [SerializeField] private Image muteIcon;
        [SerializeField] private Sprite mutedSprite;
        [SerializeField] private Sprite unmutedSprite;
        [SerializeField] private Color speakingColor = Color.green;
        [SerializeField] private Color silentColor = Color.gray;

        [Header("Colors")]
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.yellow;

        private string playerId;
        private bool isLocalPlayer;
        private bool isMuted;
        private bool isVoiceConnected;

        /// <summary>
        /// Gets the player ID associated with this item.
        /// </summary>
        public string PlayerId => playerId;

        private void Awake()
        {
            SetupKickButton();
            SetupVoiceControls();
            SubscribeToVoiceEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromVoiceEvents();
        }

        #region Setup

        private void SetupKickButton()
        {
            if (kickPlayer != null)
            {
                kickPlayer.onClick.AddListener(async () =>
                {
                    kickPlayer.interactable = false;
                    var lobbyService = ServiceLocator.Get<ILobbyService>();
                    if (lobbyService != null)
                        await lobbyService.KickPlayer(playerId);
                    kickPlayer.interactable = true;
                });
            }
        }

        private void SetupVoiceControls()
        {
            if (voiceControlsContainer == null || VoiceManager.Instance == null) return;

            // Hide voice controls by default (shown only for other players when connected)
            if (voiceControlsContainer != null)
                voiceControlsContainer.SetActive(false);

            // Setup volume slider
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0;
                volumeSlider.maxValue = 100;
                volumeSlider.wholeNumbers = true;
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            // Setup mute button
            if (muteButton != null)
                muteButton.onClick.AddListener(OnMuteButtonClicked);
        }

        private void SubscribeToVoiceEvents()
        {
            VoiceManager.ChannelJoined += OnVoiceChannelJoined;
            VoiceManager.PositionalChannelJoined += OnVoiceChannelJoined;
            VoiceManager.ChannelLeft += OnVoiceChannelLeft;
            VoiceManager.MemberSpeaking += OnMemberSpeaking;
        }

        private void UnsubscribeFromVoiceEvents()
        {
            VoiceManager.ChannelJoined -= OnVoiceChannelJoined;
            VoiceManager.PositionalChannelJoined -= OnVoiceChannelJoined;
            VoiceManager.ChannelLeft -= OnVoiceChannelLeft;
            VoiceManager.MemberSpeaking -= OnMemberSpeaking;
        }

        #endregion

        #region Public Methods

        public void UpdatePlayer(PlayerData playerData, LobbyData lobbyData)
        {
            // Cache player ID and local player status
            playerId = playerData.PlayerId;
            isLocalPlayer = playerData.IsLocalPlayer;

            // Player Name
            if (playerNameText != null)
                playerNameText.text = playerData.Data.ContainsKey(PlayerDataKeys.PlayerName)
                    ? playerData.Data[PlayerDataKeys.PlayerName]
                    : "Unknown";

            // Host Indicator
            if (ownerImage != null)
                ownerImage.gameObject.SetActive(playerData.IsLobbyHost);

            // Ready State
            bool isReady = playerData.Data.ContainsKey(PlayerDataKeys.PlayerReady)
                && playerData.Data[PlayerDataKeys.PlayerReady] == "true";

            if (readyImage != null)
                readyImage.color = isReady ? readyColor : notReadyColor;

            if (readyText != null)
                readyText.text = isReady ? "Ready" : "Not Ready";

            // Kick Button (only visible to host for non-local players)
            if (kickPlayer != null)
            {
                bool isHost = lobbyData.HostId == ServiceLocator.Get<IProfileService>()?.LocalPlayerStats?.PlayerId;
                kickPlayer.gameObject.SetActive(!playerData.IsLocalPlayer && isHost);
            }

            // Character Data
            if (playerData.Data.ContainsKey(PlayerDataKeys.PlayerCharacter))
            {
                var characterDataList = ServiceLocator.Get<IProfileService>()?.CharacterData;
                if (characterDataList != null)
                {
                    var selectedCharacterId = playerData.Data[PlayerDataKeys.PlayerCharacter];
                    var charData = characterDataList.Find(c => c != null && c.CharacterId == selectedCharacterId);
                    if (charData != null)
                    {
                        if (characterImage != null) characterImage.sprite = charData.CharacterIcon;
                        if (characterNameText != null) characterNameText.text = charData.CharacterName;
                    }
                }
            }

            // Voice controls - only for other players, hidden for local player
            UpdateVoiceControlsVisibility();
            LoadVoiceSettings();
        }

        #endregion

        #region Voice Controls

        private void UpdateVoiceControlsVisibility()
        {
            // Check current voice connection state (not just event-driven)
            isVoiceConnected = VoiceManager.Instance != null
                && !string.IsNullOrEmpty(VoiceManager.Instance.CurrentChannelId);

            // Hide for local player, show for others only when voice is connected
            bool shouldShow = !isLocalPlayer && isVoiceConnected;

            if (voiceControlsContainer != null)
                voiceControlsContainer.SetActive(shouldShow);
        }

        private void LoadVoiceSettings()
        {
            if (isLocalPlayer || string.IsNullOrEmpty(playerId)) return;
            if (VoiceManager.Instance == null) return;

            // Load saved volume
            if (volumeSlider != null)
            {
                int savedVolume = VoiceManager.Instance.GetMemberVolume(playerId);
                volumeSlider.SetValueWithoutNotify(savedVolume);
            }

            // Load saved mute state
            isMuted = VoiceManager.Instance.IsMemberMuted(playerId);
            UpdateMuteUI();
        }

        private void OnVolumeChanged(float value)
        {
            if (isLocalPlayer || string.IsNullOrEmpty(playerId)) return;
            VoiceManager.Instance?.SetMemberVolume(value, playerId);
        }

        private void OnMuteButtonClicked()
        {
            if (isLocalPlayer || string.IsNullOrEmpty(playerId)) return;
            if (VoiceManager.Instance == null) return;

            if (isMuted)
                VoiceManager.Instance.UnmuteMember(playerId);
            else
                VoiceManager.Instance.MuteMember(playerId);

            isMuted = !isMuted;
            UpdateMuteUI();
        }

        private void UpdateMuteUI()
        {
            if (muteIcon == null) return;
            muteIcon.sprite = isMuted ? mutedSprite : unmutedSprite;
        }

        #endregion

        #region Voice Event Handlers

        private void OnVoiceChannelJoined(string channelId)
        {
            isVoiceConnected = true;
            UpdateVoiceControlsVisibility();
        }

        private void OnVoiceChannelLeft()
        {
            isVoiceConnected = false;
            UpdateVoiceControlsVisibility();

            // Reset speaking indicator
            if (speakingIndicator != null)
                speakingIndicator.color = silentColor;
        }

        private void OnMemberSpeaking(string memberId, bool isSpeaking)
        {
            // Only update if this is for our player
            if (memberId != playerId) return;
            if (speakingIndicator == null) return;

            speakingIndicator.color = isSpeaking ? speakingColor : silentColor;
        }

        #endregion
    }
}
