using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages voice chat functionality within the lobby.
    /// </summary>
    public class VoiceManager : MonoBehaviour
    {
        public static VoiceManager Instance { get; private set; }

        // Voice chat events
        public static event Action<string> ChannelJoined;
        public static event Action<string> PositionalChannelJoined;
        public static event Action ChannelLeft;

        public static event Action<List<VoiceMemeberSettings>> MembersUpdated;
        public static event Action<VoiceMemeberSettings> MemberDataUpdated;

        public static event Action<bool> MicStatusUpdated;
        public static event Action<bool> SpeakerStatusUpdated;
        public static event Action<int> MicVolumeUpdated;

        // Text chat events
        public static event Action<string, string> OnMessageReceived;

        // Speaking indicator event (memberId, isSpeaking)
        public static event Action<string, bool> MemberSpeaking;

        private IVoiceChat voiceChatService;

        public string CurrentChannelId { get; private set; }

        // Remembers the last channel ID for reconnection after leaving
        private string lastChannelId;

        // PlayerPrefs keys (centralized for consistency)
        private const string MicMutedKey = "LocalPlayer_MicMuted";
        private const string MicVolumeKey = "LocalPlayer_MicVolume";
        private const string SpeakerMutedKey = "LocalPlayer_SpeakerMuted";
        private const string VoiceQualityKey = "LocalPlayer_VoiceQuality";

        private void Awake()
        {
            // Ensure singleton instance persists across scenes
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if STEAM_SERVICES
            voiceChatService = new SteamVoiceServices();
#elif UNITY_SERVICES
            voiceChatService = new VivoxServices();
#endif

            // Register with ServiceLocator
            ServiceLocator.Register<IVoiceChat>(voiceChatService);

            // Subscribe to voice chat service events
            if (voiceChatService != null)
            {
                voiceChatService.ChannelJoined += (channelId) =>
                {
                    CurrentChannelId = channelId;
                    ChannelJoined?.Invoke(channelId);
                };
                voiceChatService.PositionalChannelJoined += (channelId) =>
                {
                    CurrentChannelId = channelId;
                    PositionalChannelJoined?.Invoke(channelId);
                };
                voiceChatService.ChannelLeft += () =>
                {
                    CurrentChannelId = null;
                    ChannelLeft?.Invoke();
                };
                voiceChatService.MembersUpdated += (members) => MembersUpdated?.Invoke(members);
                voiceChatService.MemberDataUpdated += (member) => MemberDataUpdated?.Invoke(member);
                voiceChatService.MicStatusUpdated += (isMuted) => MicStatusUpdated?.Invoke(isMuted);
                voiceChatService.SpeakerStatusUpdated += (isMuted) => SpeakerStatusUpdated?.Invoke(isMuted);
                voiceChatService.MicVolumeUpdated += (volume) => MicVolumeUpdated?.Invoke(volume);
                voiceChatService.OnMessageRecived += (senderId, message) => OnMessageReceived?.Invoke(senderId, message);
                voiceChatService.MemberSpeaking += (memberId, isSpeaking) => MemberSpeaking?.Invoke(memberId, isSpeaking);
            }

            AuthenticationManager.OnProfileSetupCompleted += (boolean) =>
            {
                voiceChatService?.Initialize();
                ApplySavedQuality();
            };

            // Subscribe to lobby events for auto voice connection
            LobbyManagerBase.OnLobbyCreated += OnLobbyJoinedOrCreated;
            LobbyManagerBase.OnLobbyJoined += OnLobbyJoinedOrCreated;
            LobbyManagerBase.OnLobbyLeft += OnLobbyLeftHandler;

#if UNITY_SERVICES || STEAM_SERVICES
            // Subscribe to game session events for proximity voice
            SessionManager.OnAllPlayersLoaded += OnGameSceneLoaded;
#endif
        }

        private void OnLobbyJoinedOrCreated(LobbyData lobbyData)
        {
            if (lobbyData != null && !string.IsNullOrEmpty(lobbyData.LobbyId))
            {
                _ = JoinVoiceChat(lobbyData.LobbyId);
            }
        }

        private async void OnLobbyLeftHandler()
        {
            await LeaveVoiceChat();
            ClearChannelCache();
        }

        /// <summary>
        /// Called when all players have loaded the game scene.
        /// Automatically switches from lobby voice to proximity voice.
        /// </summary>
        private async void OnGameSceneLoaded()
        {
            // Get channel ID before leaving (use current or cached)
            string channelId = CurrentChannelId ?? lastChannelId;

            if (string.IsNullOrEmpty(channelId))
            {
                Debug.LogWarning("[VoiceManager] No channel ID available for proximity voice.");
                return;
            }

            // Leave current lobby voice channel
            if (!string.IsNullOrEmpty(CurrentChannelId))
            {
                await LeaveVoiceChat();
            }

            // Join proximity/positional channel with same ID
            await JoinPositionalChannel(channelId);
        }

        private void OnDestroy()
        {
            // Unsubscribe from lobby events
            LobbyManagerBase.OnLobbyCreated -= OnLobbyJoinedOrCreated;
            LobbyManagerBase.OnLobbyJoined -= OnLobbyJoinedOrCreated;
            LobbyManagerBase.OnLobbyLeft -= OnLobbyLeftHandler;

            #if UNITY_SERVICES || STEAM_SERVICES
            // Unsubscribe from game session events
            SessionManager.OnAllPlayersLoaded -= OnGameSceneLoaded;
#endif

            // Unregister from ServiceLocator
            ServiceLocator.Unregister<IVoiceChat>();
        }

        private void Update()
        {
            voiceChatService?.Update();
        }

        private void LateUpdate()
        {
            voiceChatService?.LateUpdate();
        }

        /// <summary>
        /// Joins the voice chat channel for the specified lobby.
        /// </summary>
        /// <param name="lobbyId">The lobby identifier.</param>
        public async Task JoinVoiceChat(string lobbyId)
        {
            if (voiceChatService == null) return;

            // If empty string is passed, try to use CurrentChannelId or lastChannelId for reconnection
            if (string.IsNullOrEmpty(lobbyId))
            {
                if (!string.IsNullOrEmpty(CurrentChannelId))
                    lobbyId = CurrentChannelId;
                else if (!string.IsNullOrEmpty(lastChannelId))
                    lobbyId = lastChannelId;
                else
                {
                    Debug.LogWarning("VoiceManager: No channel ID available for reconnection.");
                    return;
                }
            }

            CurrentChannelId = await voiceChatService.JoinVoiceChat(lobbyId);

            // Remember this channel for potential reconnection
            if (!string.IsNullOrEmpty(CurrentChannelId))
                lastChannelId = CurrentChannelId;

            ApplySavedSettings();
        }

        public async Task JoinPositionalChannel(string lobbyId)
        {
            if (voiceChatService == null) return;

            // If null/empty is passed, try to use CurrentChannelId or lastChannelId for reconnection
            if (string.IsNullOrEmpty(lobbyId))
            {
                if (!string.IsNullOrEmpty(CurrentChannelId))
                    lobbyId = CurrentChannelId;
                else if (!string.IsNullOrEmpty(lastChannelId))
                    lobbyId = lastChannelId;
                else
                {
                    Debug.LogWarning("VoiceManager: No channel ID available for reconnection.");
                    return;
                }
            }

            CurrentChannelId = await voiceChatService.JoinPositionalChannel(lobbyId);

            // Remember this channel for potential reconnection
            if (!string.IsNullOrEmpty(CurrentChannelId))
                lastChannelId = CurrentChannelId;

            ApplySavedSettings();
        }

        /// <summary>
        /// Applies saved audio settings from PlayerPrefs.
        /// </summary>
        private void ApplySavedSettings()
        {
            // Apply mic mute state (1 = muted, 0 = unmuted)
            bool isMicMuted = PlayerPrefs.GetInt(MicMutedKey, 0) == 1;
            if (isMicMuted)
                voiceChatService.MuteMic();
            else
                voiceChatService.UnmuteMic();

            // Apply mic volume
            int micVolume = PlayerPrefs.GetInt(MicVolumeKey, 50);
            voiceChatService.SetMicVolume(micVolume);

            // Apply speaker mute state (1 = muted, 0 = unmuted)
            bool isSpeakerMuted = PlayerPrefs.GetInt(SpeakerMutedKey, 0) == 1;
            if (isSpeakerMuted)
                voiceChatService.MuteSpeaker();
            else
                voiceChatService.UnmuteSpeaker();
        }

        /// <summary>
        /// Gets the saved settings for UI initialization.
        /// </summary>
        public (bool micMuted, bool speakerMuted, int micVolume) GetSavedSettings()
        {
            bool micMuted = PlayerPrefs.GetInt(MicMutedKey, 0) == 1;
            bool speakerMuted = PlayerPrefs.GetInt(SpeakerMutedKey, 0) == 1;
            int micVolume = PlayerPrefs.GetInt(MicVolumeKey, 50);
            return (micMuted, speakerMuted, micVolume);
        }

        public async Task LeaveVoiceChat()
        {
            if (CurrentChannelId == null || voiceChatService == null) return;

            // Save the channel ID before clearing so we can reconnect later
            lastChannelId = CurrentChannelId;

            await voiceChatService.LeaveVoiceChat();
            CurrentChannelId = null;
        }

        /// <summary>
        /// Clears the cached channel ID. Call this when leaving a lobby entirely
        /// to prevent reconnection to an old lobby's voice channel.
        /// </summary>
        public void ClearChannelCache()
        {
            lastChannelId = null;
            CurrentChannelId = null;
        }

        public void MuteMic()
        {
            voiceChatService?.MuteMic();
            PlayerPrefs.SetInt(MicMutedKey, 1);
            PlayerPrefs.Save();
        }

        public void UnmuteMic()
        {
            voiceChatService?.UnmuteMic();
            PlayerPrefs.SetInt(MicMutedKey, 0);
            PlayerPrefs.Save();
        }

        public void SetMicVolume(float volume)
        {
            voiceChatService?.SetMicVolume((int)volume);
            PlayerPrefs.SetInt(MicVolumeKey, (int)volume);
            PlayerPrefs.Save();
        }

        public void MuteSpeaker()
        {
            voiceChatService?.MuteSpeaker();
            PlayerPrefs.SetInt(SpeakerMutedKey, 1);
            PlayerPrefs.Save();
        }

        public void UnmuteSpeaker()
        {
            voiceChatService?.UnmuteSpeaker();
            PlayerPrefs.SetInt(SpeakerMutedKey, 0);
            PlayerPrefs.Save();
        }

        public void SetMemberVolume(float volume, string playerId)
        {
            voiceChatService?.SetMemberVolume((int)volume, playerId);
            PlayerPrefs.SetInt($"VoiceMember_{playerId}_Volume", (int)volume);
            PlayerPrefs.Save();
        }

        public void MuteMember(string playerId)
        {
            voiceChatService?.MuteMember(playerId);
            PlayerPrefs.SetInt($"VoiceMember_{playerId}_Muted", 1);
            PlayerPrefs.Save();
        }

        public void UnmuteMember(string playerId)
        {
            voiceChatService?.UnmuteMember(playerId);
            PlayerPrefs.SetInt($"VoiceMember_{playerId}_Muted", 0);
            PlayerPrefs.Save();
        }

        public int GetMemberVolume(string memberId)
        {
            return PlayerPrefs.GetInt($"VoiceMember_{memberId}_Volume", 50);
        }

        public bool IsMemberMuted(string memberId)
        {
            return PlayerPrefs.GetInt($"VoiceMember_{memberId}_Muted", 0) == 1;
        }

        /// <summary>
        /// Sets the voice quality (Low, Medium, High).
        /// </summary>
        public void SetVoiceQuality(VoiceQuality quality)
        {
            voiceChatService?.SetVoiceQuality(quality);
            PlayerPrefs.SetInt(VoiceQualityKey, (int)quality);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gets the current voice quality setting.
        /// </summary>
        public VoiceQuality GetVoiceQuality()
        {
            return (VoiceQuality)PlayerPrefs.GetInt(VoiceQualityKey, (int)VoiceQuality.Medium);
        }

        private void ApplySavedQuality()
        {
            VoiceQuality quality = GetVoiceQuality();
            voiceChatService?.SetVoiceQuality(quality);
        }

        /// <summary>
        /// Sends a text chat message to the current channel.
        /// </summary>
        public void SendTextMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || voiceChatService == null) return;
            voiceChatService.SendMessage(message);
        }
    }
}