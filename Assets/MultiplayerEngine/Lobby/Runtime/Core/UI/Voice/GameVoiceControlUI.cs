using UnityEngine;
using UnityEngine.UI;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Voice control UI for the game scene with proximity voice chat support.
    /// Auto-initializes on enable and can reconnect to proximity voice channel.
    /// </summary>
    public class GameVoiceControlUI : MonoBehaviour
    {
        #region UI References

        [Header("Microphone Controls")]
        [SerializeField] private Button micButton;
        [SerializeField] private Image micIcon;
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
        private VoiceManager cachedVoiceManager;

        /// <summary>
        /// Gets the VoiceManager instance, using cached reference or FindAnyObjectByType as fallback.
        /// This handles async scene loading where the singleton may not be immediately accessible.
        /// </summary>
        private VoiceManager Voice
        {
            get
            {
                if (cachedVoiceManager != null)
                    return cachedVoiceManager;

                cachedVoiceManager = VoiceManager.Instance;

                if (cachedVoiceManager == null)
                {
                    cachedVoiceManager = FindAnyObjectByType<VoiceManager>();
                }

                return cachedVoiceManager;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            SetupButtonListeners();
            SubscribeToVoiceEvents();

            // Try to load state immediately, or start coroutine to wait for VoiceManager
            if (VoiceManager.Instance != null)
            {
                LoadInitialState();
            }
            else
            {
                StartCoroutine(WaitForVoiceManager());
            }
        }

        private System.Collections.IEnumerator WaitForVoiceManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (Voice == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (Voice != null)
            {
                LoadInitialState();
            }
            else
            {
                Debug.LogWarning("[GameVoiceControlUI] VoiceManager not found after timeout. Voice controls will not function.");
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
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
        }

        private void SubscribeToVoiceEvents()
        {
            VoiceManager.ChannelJoined += OnChannelJoined;
            VoiceManager.PositionalChannelJoined += OnChannelJoined;
            VoiceManager.ChannelLeft += OnChannelLeft;
            VoiceManager.MicStatusUpdated += OnMicStatusUpdated;
            VoiceManager.SpeakerStatusUpdated += OnSpeakerStatusUpdated;
        }

        private void UnsubscribeFromVoiceEvents()
        {
            VoiceManager.ChannelJoined -= OnChannelJoined;
            VoiceManager.PositionalChannelJoined -= OnChannelJoined;
            VoiceManager.ChannelLeft -= OnChannelLeft;
            VoiceManager.MicStatusUpdated -= OnMicStatusUpdated;
            VoiceManager.SpeakerStatusUpdated -= OnSpeakerStatusUpdated;

            // Remove button listeners
            if (micButton != null)
                micButton.onClick.RemoveListener(OnMicButtonClicked);
            if (speakerButton != null)
                speakerButton.onClick.RemoveListener(OnSpeakerButtonClicked);
            if (connectionButton != null)
                connectionButton.onClick.RemoveListener(OnConnectionButtonClicked);
        }

        private void LoadInitialState()
        {
            if (Voice == null) return;

            var settings = Voice.GetSavedSettings();

            // Mic state
            isMicMuted = settings.micMuted;
            UpdateMicUI();

            // Speaker state
            isSpeakerMuted = settings.speakerMuted;
            UpdateSpeakerUI();

            // Connection state
            isConnected = !string.IsNullOrEmpty(Voice.CurrentChannelId);
            UpdateConnectionUI();
        }

        #endregion

        #region Button Handlers

        private void OnMicButtonClicked()
        {
            if (Voice == null)
            {
                Debug.LogWarning("[GameVoiceControlUI] Mic button clicked but VoiceManager not found!");
                return;
            }

            if (isMicMuted)
                Voice.UnmuteMic();
            else
                Voice.MuteMic();
        }

        private void OnSpeakerButtonClicked()
        {
            if (Voice == null)
            {
                Debug.LogWarning("[GameVoiceControlUI] Speaker button clicked but VoiceManager not found!");
                return;
            }

            if (isSpeakerMuted)
                Voice.UnmuteSpeaker();
            else
                Voice.MuteSpeaker();
        }

        private async void OnConnectionButtonClicked()
        {
            if (Voice == null) return;

            if (connectionButton != null)
                connectionButton.interactable = false;

            try
            {
                if (isConnected)
                {
                    await Voice.LeaveVoiceChat();
                }
                else
                {
                    // Reconnect to proximity channel using cached channel ID
                    await Voice.JoinPositionalChannel(string.Empty);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameVoiceControlUI] Connection toggle failed: {ex.Message}");
            }
            finally
            {
                if (connectionButton != null)
                    connectionButton.interactable = true;
            }
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
