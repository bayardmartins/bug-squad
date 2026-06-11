using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Voice quality settings that control sample rate and bandwidth usage.
    /// </summary>
    public enum VoiceQuality
    {
        /// <summary>8kHz sample rate - minimal bandwidth, basic quality</summary>
        Low,
        /// <summary>16kHz sample rate - balanced bandwidth and quality</summary>
        Medium,
        /// <summary>48kHz sample rate - highest quality, more bandwidth</summary>
        High
    }

    /// <summary>
    /// Defines the contract for voice chat functionality.
    /// </summary>
    public interface IVoiceChat
    {
        event Action<string> ChannelJoined;
        event Action<string> PositionalChannelJoined;
        event Action ChannelLeft;

        event Action<List<VoiceMemeberSettings>> MembersUpdated;
        event Action<VoiceMemeberSettings> MemberDataUpdated;

        /// <summary>Fires when a member starts or stops speaking. (memberId, isSpeaking)</summary>
        event Action<string, bool> MemberSpeaking;

        event Action<bool> MicStatusUpdated;
        event Action<bool> SpeakerStatusUpdated;
        event Action<int> MicVolumeUpdated;

        event Action<string, string> OnMessageRecived;

        /// <summary>
        /// Initializes the voice chat system.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Joins a voice chat channel.
        /// </summary>
        /// <param name="channelId">The identifier of the channel to join.</param>
        Task<string> JoinVoiceChat(string channelId);

        /// <summary>
        /// Leaves the current voice chat channel.
        /// </summary>
        Task LeaveVoiceChat();

        /// <summary>
        /// Mutes the local player.
        /// </summary>
        void MuteMic();

        void UnmuteMic();

        /// <summary>
        /// Unmutes the local player.
        /// </summary>
        void MuteSpeaker();

        void UnmuteSpeaker();

        /// <summary>
        /// Sets the microphone input volume.
        /// </summary>
        /// <param name="volume">Volume value between 0.0 and 1.0.</param>
        void SetMicVolume(int volume);

        /// <summary>
        /// Sets the volume for a specific member.
        /// </summary>
        /// <param name="volume">Volume value between 0.0 and 1.0.</param>
        /// <param name="memberId">The identifier of the member.</param>
        void SetMemberVolume(int volume, string memberId);

        /// <summary>
        /// Mutes a specific member.
        /// </summary>
        /// <param name="playerId">The identifier of the player to mute.</param>
        void MuteMember(string playerId);

        /// <summary>
        /// Unmutes a specific member.
        /// </summary>
        /// <param name="playerId">The identifier of the player to unmute.</param>
        void UnmuteMember(string playerId);

        /// <summary>
        /// Sets the voice chat type.
        /// </summary>
        /// <param name="type">The voice type (General or Proximity).</param>
        Task<string> JoinPositionalChannel(string channelId);

        /// <summary>
        /// Sets the player's position for proximity-based voice chat.
        /// </summary>
        /// <param name="position">The position of the player.</param>
        void Set3DPosition(GameObject participantObject);

        /// <summary>
        /// Updates the voice chat system.
        /// </summary>
        void Update();

        /// <summary>
        /// Performs late update operations for the voice chat system.
        /// </summary>
        void LateUpdate();

        void SendMessage(string message);

        /// <summary>
        /// Sets the voice quality/sample rate for recording.
        /// </summary>
        /// <param name="quality">The quality level to use.</param>
        void SetVoiceQuality(VoiceQuality quality);
    }
}