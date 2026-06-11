#if UNITY_SERVICES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Vivox-based voice chat services with speaking indicators and quality settings.
    /// </summary>
    public class VivoxServices : IVoiceChat
    {
        #region Events

        public event Action<string> ChannelJoined;
        public event Action<string> PositionalChannelJoined;
        public event Action ChannelLeft;
        public event Action<List<VoiceMemeberSettings>> MembersUpdated;
        public event Action<VoiceMemeberSettings> MemberDataUpdated;
        public event Action<string, bool> MemberSpeaking;
        public event Action<bool> MicStatusUpdated;
        public event Action<bool> SpeakerStatusUpdated;
        public event Action<int> MicVolumeUpdated;
        public event Action<string, string> OnMessageRecived;

        #endregion

        #region Fields

        private string currentChannelId;
        private List<VoiceMemeberSettings> voiceMembers = new();
        private Dictionary<string, bool> memberSpeakingStates = new();

        private bool isLoggedIn = false;
        private string queuedChannelId = null;
        private bool joinCancelled = false;

        private VoiceQuality currentQuality = VoiceQuality.Medium;
        private bool isPositionalChannel = false;

        // Quality settings map to input volume adjustments
        private static readonly Dictionary<VoiceQuality, int> QualityVolumeMap = new()
        {
            { VoiceQuality.Low, -30 },    // Lower input = less data
            { VoiceQuality.Medium, 0 },   // Default
            { VoiceQuality.High, 20 }     // Higher input = better clarity
        };

        #endregion

        #region Initialization

        /// <inheritdoc />
        public async void Initialize()
        {
            try
            {
                await VivoxService.Instance.InitializeAsync();

                // Logout first if already logged in (e.g., from a previous session or sign-up flow)
                if (VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LogoutAsync();
                    isLoggedIn = false;
                }

                LoginOptions loginOptions = new LoginOptions
                {
                    PlayerId = AuthenticationService.Instance.PlayerId,
                    DisplayName = AuthenticationService.Instance.PlayerName,
                };

                await VivoxService.Instance.LoginAsync(loginOptions);
                isLoggedIn = true;

                // Process queued join if any and not cancelled
                if (queuedChannelId != null && !joinCancelled)
                {
                    await JoinVoiceChat(queuedChannelId);
                    queuedChannelId = null;
                }

                // Subscribe to participant events
                VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;

                // Subscribe to text message events
                VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Initialization failed: {ex.Message}");
            }
        }

        #endregion

        #region Channel Management

        /// <inheritdoc />
        public async Task<string> JoinVoiceChat(string channelId)
        {
            if (!isLoggedIn)
            {
                queuedChannelId = channelId;
                joinCancelled = false;
                return null;
            }

            try
            {
                if (VivoxService.Instance.ActiveChannels.Count != 0)
                {
                    await VivoxService.Instance.LeaveAllChannelsAsync();
                    ClearMemberData();
                }

                await VivoxService.Instance.JoinGroupChannelAsync(channelId, ChatCapability.TextAndAudio);
                currentChannelId = channelId;

                // Apply quality settings
                ApplyQualitySettings();

                // Ensure audio devices are unmuted after joining (Vivox may start muted by default)
                VivoxService.Instance.UnmuteInputDevice();
                VivoxService.Instance.UnmuteOutputDevice();

                ChannelJoined?.Invoke(channelId);
                isPositionalChannel = false;
                return channelId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Failed to join voice chat: {ex.Message}");
            }
            return null;
        }

        /// <inheritdoc />
        public async Task LeaveVoiceChat()
        {
            if (!isLoggedIn && queuedChannelId != null)
            {
                joinCancelled = true;
                queuedChannelId = null;
                return;
            }

            try
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                ClearMemberData();
                currentChannelId = null;
                isPositionalChannel = false;
                ChannelLeft?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Failed to leave voice chat: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<string> JoinPositionalChannel(string channelId)
        {
            if (!isLoggedIn)
            {
                queuedChannelId = channelId;
                joinCancelled = false;
                return null;
            }

            try
            {
                if (VivoxService.Instance.ActiveChannels.Count != 0)
                {
                    await VivoxService.Instance.LeaveAllChannelsAsync();
                    ClearMemberData();
                }

                var channel3DProperties = new Channel3DProperties();
                await VivoxService.Instance.JoinPositionalChannelAsync(channelId, ChatCapability.TextAndAudio, channel3DProperties);
                currentChannelId = channelId;

                ApplyQualitySettings();

                // Ensure audio devices are unmuted after joining
                VivoxService.Instance.UnmuteInputDevice();
                VivoxService.Instance.UnmuteOutputDevice();

                PositionalChannelJoined?.Invoke(channelId);
                isPositionalChannel = true;
                return channelId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Failed to join positional channel: {ex.Message}");
            }
            return null;
        }

        private void ClearMemberData()
        {
            voiceMembers.Clear();
            memberSpeakingStates.Clear();
        }

        #endregion

        #region Speaking Detection

        private void HandleParticipantSpeechDetected(VivoxParticipant participant)
        {
            if (participant == null || participant.PlayerId == AuthenticationService.Instance.PlayerId)
                return;

            bool isSpeaking = participant.SpeechDetected;
            string memberId = participant.PlayerId;

            // Only fire event if state changed
            if (!memberSpeakingStates.TryGetValue(memberId, out bool wasActive) || wasActive != isSpeaking)
            {
                memberSpeakingStates[memberId] = isSpeaking;
                MemberSpeaking?.Invoke(memberId, isSpeaking);
            }
        }

        #endregion

        #region Participant Management

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant.PlayerId == AuthenticationService.Instance.PlayerId)
                return;

            var existingMember = voiceMembers.Find(m => m.MemberId == participant.PlayerId);
            if (existingMember != null)
                return;

            // Subscribe to this participant's speech detection event
            participant.ParticipantSpeechDetected += () => HandleParticipantSpeechDetected(participant);

            Sprite sprite = null;
            if (FriendsManager.Instance != null)
            {
                sprite = FriendsManager.Instance.FriendsList
                    .Find(spriteData => spriteData.PlayerId == participant.PlayerId)?.Avatar;
            }

            var newMember = new VoiceMemeberSettings
            {
                MemberId = participant.PlayerId,
                Volume = VoiceManager.Instance?.GetMemberVolume(participant.PlayerId) ?? 50,
                IsMuted = VoiceManager.Instance?.IsMemberMuted(participant.PlayerId) ?? false,
                Sprite = sprite,
                DisplayName = participant.DisplayName
            };

            voiceMembers.Add(newMember);
            memberSpeakingStates[participant.PlayerId] = false;
            MembersUpdated?.Invoke(voiceMembers);
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            var memberToRemove = voiceMembers.Find(m => m.MemberId == participant.PlayerId);
            if (memberToRemove != null)
            {
                voiceMembers.Remove(memberToRemove);
                memberSpeakingStates.Remove(participant.PlayerId);
                MembersUpdated?.Invoke(voiceMembers);
            }
        }

        #endregion

        #region Voice Quality

        /// <inheritdoc />
        public void SetVoiceQuality(VoiceQuality quality)
        {
            currentQuality = quality;
            ApplyQualitySettings();
        }

        private void ApplyQualitySettings()
        {
            if (!isLoggedIn) return;

            int volumeAdjustment = QualityVolumeMap[currentQuality];
            VivoxService.Instance.SetInputDeviceVolume(volumeAdjustment);
        }

        #endregion

        #region Mute Controls

        /// <inheritdoc />
        public void MuteMic()
        {
            VivoxService.Instance.MuteInputDevice();
            MicStatusUpdated?.Invoke(true);
        }

        /// <inheritdoc />
        public void UnmuteMic()
        {
            VivoxService.Instance.UnmuteInputDevice();
            VivoxService.Instance.UnmuteOutputDevice();
            MicStatusUpdated?.Invoke(false);
            SpeakerStatusUpdated?.Invoke(false);
        }

        /// <inheritdoc />
        public void MuteSpeaker()
        {
            VivoxService.Instance.MuteInputDevice();
            VivoxService.Instance.MuteOutputDevice();
            SpeakerStatusUpdated?.Invoke(true);
            MicStatusUpdated?.Invoke(true);
        }

        /// <inheritdoc />
        public void UnmuteSpeaker()
        {
            VivoxService.Instance.UnmuteOutputDevice();
            SpeakerStatusUpdated?.Invoke(false);
        }

        /// <inheritdoc />
        public void SetMicVolume(int volume)
        {
            // Volume range: -50 to 50, where 0 is default
            VivoxService.Instance.SetInputDeviceVolume(volume - 50);
            MicVolumeUpdated?.Invoke(volume);
        }

        /// <inheritdoc />
        public void MuteMember(string playerId)
        {
            if (string.IsNullOrEmpty(currentChannelId) || string.IsNullOrEmpty(playerId))
                return;

            var channel = VivoxService.Instance.ActiveChannels.GetValueOrDefault(currentChannelId);
            var participant = channel?.FirstOrDefault(p => p.PlayerId == playerId);
            participant?.MutePlayerLocally();

            MemberDataUpdated?.Invoke(GetMemberSettings(playerId));
        }

        /// <inheritdoc />
        public void UnmuteMember(string playerId)
        {
            if (string.IsNullOrEmpty(currentChannelId) || string.IsNullOrEmpty(playerId))
                return;

            var channel = VivoxService.Instance.ActiveChannels.GetValueOrDefault(currentChannelId);
            var participant = channel?.FirstOrDefault(p => p.PlayerId == playerId);
            participant?.UnmutePlayerLocally();

            MemberDataUpdated?.Invoke(GetMemberSettings(playerId));
        }

        /// <inheritdoc />
        public void SetMemberVolume(int volume, string memberId)
        {
            if (string.IsNullOrEmpty(currentChannelId) || string.IsNullOrEmpty(memberId))
                return;

            var channel = VivoxService.Instance.ActiveChannels.GetValueOrDefault(currentChannelId);
            var participant = channel?.FirstOrDefault(p => p.PlayerId == memberId);
            // Volume range: -50 to 50
            participant?.SetLocalVolume(volume - 50);

            MemberDataUpdated?.Invoke(GetMemberSettings(memberId));
        }

        private VoiceMemeberSettings GetMemberSettings(string playerId)
        {
            return voiceMembers.Find(m => m.MemberId == playerId) ?? new VoiceMemeberSettings
            {
                MemberId = playerId,
                Volume = 50,
                IsMuted = false
            };
        }

        #endregion

        #region 3D Position

        /// <inheritdoc />
        public void Set3DPosition(GameObject participantObject)
        {
            // Only update position if we're in a positional channel
            if (participantObject == null || string.IsNullOrEmpty(currentChannelId) || !isPositionalChannel)
                return;

            try
            {
                VivoxService.Instance.Set3DPosition(participantObject, currentChannelId);
            }
            catch (Exception)
            {
                // Silently ignore - channel may not be ready yet
            }
        }

        #endregion

        #region Update Methods

        /// <inheritdoc />
        public void Update() { }

        /// <inheritdoc />
        public void LateUpdate() { }

        #endregion

        #region Text Chat

        private void OnChannelMessageReceived(VivoxMessage args)
        {
            if (args.FromSelf) return;
            OnMessageRecived?.Invoke(args.SenderPlayerId, args.MessageText);
        }

        /// <inheritdoc />
        public async void SendMessage(string message)
        {
            if (string.IsNullOrEmpty(currentChannelId) || string.IsNullOrEmpty(message))
                return;

            try
            {
                await VivoxService.Instance.SendChannelTextMessageAsync(currentChannelId, message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Failed to send text message: {ex.Message}");
            }
        }

        #endregion
    }
}
#endif