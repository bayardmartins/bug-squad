using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Standalone voice control UI for lobby screen.
    /// Controls local player's mic, speaker, volume, and voice connection.
    /// VoiceManager does NOT depend on this - it subscribes to VoiceManager events.
    /// </summary>
    public class LobbyVoiceControlUI : MonoBehaviour
    {
        #region UI References

        [Header("Microphone Controls")]
        [SerializeField] private Button micButton;
        [SerializeField] private Image micIcon;
        [SerializeField] private Slider micVolumeSlider;
        [SerializeField] private Sprite micActiveSprite;
        [SerializeField] private Sprite micMutedSprite;

        [Header("Speaker Controls")]
        [SerializeField] private Button speakerButton;
        [SerializeField] private Image speakerIcon;
        [SerializeField] private Sprite speakerActiveSprite;
        [SerializeField] private Sprite speakerMutedSprite;

        [Header("Connection Controls")]
        [SerializeField] private Button connectionButton;
        [SerializeField] private Image connectionIcon;
        [SerializeField] private Sprite connectedSprite;
        [SerializeField] private Sprite disconnectedSprite;

        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.cyan;
        [SerializeField] private Color mutedColor = Color.yellow;
        [SerializeField] private Color disconnectedColor = Color.red;

        #endregion

        #region State

        private bool isMicMuted;
        private bool isSpeakerMuted;
        private bool isConnected;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            SetupButtonListeners();
            SubscribeToVoiceEvents();
            LoadInitialState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromVoiceEvents();
        }

        #endregion

        #region Setup

        private void SetupButtonListeners()
        {
            if (micButton != null)
                micButton.onClick.AddListener(OnMicButtonClicked);

            if (speakerButton != null)
                speakerButton.onClick.AddListener(OnSpeakerButtonClicked);

            if (connectionButton != null)
                connectionButton.onClick.AddListener(OnConnectionButtonClicked);

            if (micVolumeSlider != null)
            {
                micVolumeSlider.minValue = 0;
                micVolumeSlider.maxValue = 100;
                micVolumeSlider.wholeNumbers = true;
                micVolumeSlider.onValueChanged.AddListener(OnMicVolumeChanged);
            }
        }

        private void SubscribeToVoiceEvents()
        {
            VoiceManager.ChannelJoined += OnChannelJoined;
            VoiceManager.PositionalChannelJoined += OnChannelJoined;
            VoiceManager.ChannelLeft += OnChannelLeft;
            VoiceManager.MicStatusUpdated += OnMicStatusUpdated;
            VoiceManager.SpeakerStatusUpdated += OnSpeakerStatusUpdated;
            VoiceManager.MicVolumeUpdated += OnMicVolumeUpdated;
        }

        private void UnsubscribeFromVoiceEvents()
        {
            VoiceManager.ChannelJoined -= OnChannelJoined;
            VoiceManager.PositionalChannelJoined -= OnChannelJoined;
            VoiceManager.ChannelLeft -= OnChannelLeft;
            VoiceManager.MicStatusUpdated -= OnMicStatusUpdated;
            VoiceManager.SpeakerStatusUpdated -= OnSpeakerStatusUpdated;
            VoiceManager.MicVolumeUpdated -= OnMicVolumeUpdated;
        }

        private void LoadInitialState()
        {
            if (VoiceManager.Instance == null) return;

            var settings = VoiceManager.Instance.GetSavedSettings();

            // Mic state
            isMicMuted = settings.micMuted;
            UpdateMicUI();

            // Speaker state
            isSpeakerMuted = settings.speakerMuted;
            UpdateSpeakerUI();

            // Volume
            if (micVolumeSlider != null)
                micVolumeSlider.SetValueWithoutNotify(settings.micVolume);

            // Connection state (default disconnected)
            isConnected = !string.IsNullOrEmpty(VoiceManager.Instance.CurrentChannelId);
            UpdateConnectionUI();
        }

        #endregion

        #region Button Handlers

        private void OnMicButtonClicked()
        {
            if (VoiceManager.Instance == null) return;

            if (isMicMuted)
                VoiceManager.Instance.UnmuteMic();
            else
                VoiceManager.Instance.MuteMic();
        }

        private void OnSpeakerButtonClicked()
        {
            if (VoiceManager.Instance == null) return;

            if (isSpeakerMuted)
                VoiceManager.Instance.UnmuteSpeaker();
            else
                VoiceManager.Instance.MuteSpeaker();
        }

        private async void OnConnectionButtonClicked()
        {
            if (VoiceManager.Instance == null) return;

            if (connectionButton != null)
                connectionButton.interactable = false;

            try
            {
                if (isConnected)
                {
                    await VoiceManager.Instance.LeaveVoiceChat();
                }
                else
                {
                    // Get lobby ID from LobbyManager
                    string lobbyId = ServiceLocator.Get<ILobbyService>()?.CurrentLobbyData?.LobbyId;
                    if (string.IsNullOrEmpty(lobbyId))
                    {
                        Debug.LogWarning("[LobbyVoiceControlUI] Cannot connect - not in a lobby");
                        return;
                    }
                    await VoiceManager.Instance.JoinVoiceChat(lobbyId);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyVoiceControlUI] Connection toggle failed: {ex.Message}");
            }
            finally
            {
                if (connectionButton != null)
                    connectionButton.interactable = true;
            }
        }

        private void OnMicVolumeChanged(float value)
        {
            VoiceManager.Instance?.SetMicVolume(value);
        }

        #endregion

        #region VoiceManager Event Handlers

        private void OnChannelJoined(string channelId)
        {
            isConnected = true;
            UpdateConnectionUI();
        }

        private void OnChannelLeft()
        {
            isConnected = false;
            UpdateConnectionUI();
        }

        private void OnMicStatusUpdated(bool isMuted)
        {
            isMicMuted = isMuted;
            UpdateMicUI();
        }

        private void OnSpeakerStatusUpdated(bool isMuted)
        {
            isSpeakerMuted = isMuted;
            UpdateSpeakerUI();
        }

        private void OnMicVolumeUpdated(int volume)
        {
            if (micVolumeSlider != null)
                micVolumeSlider.SetValueWithoutNotify(volume);
        }

        #endregion

        #region UI Updates

        private void UpdateMicUI()
        {
            if (micIcon != null)
            {
                micIcon.sprite = isMicMuted ? micMutedSprite : micActiveSprite;
                micIcon.color = isMicMuted ? mutedColor : activeColor;
            }
        }

        private void UpdateSpeakerUI()
        {
            if (speakerIcon != null)
            {
                speakerIcon.sprite = isSpeakerMuted ? speakerMutedSprite : speakerActiveSprite;
                speakerIcon.color = isSpeakerMuted ? mutedColor : activeColor;
            }
        }

        private void UpdateConnectionUI()
        {
            if (connectionIcon != null)
            {
                connectionIcon.sprite = isConnected ? connectedSprite : disconnectedSprite;
                connectionIcon.color = isConnected ? activeColor : disconnectedColor;
            }
        }

        #endregion
    }
}
