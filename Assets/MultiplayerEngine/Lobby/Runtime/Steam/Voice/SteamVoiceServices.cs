#if STEAM_SERVICES
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides Steam-based voice chat services for multiplayer lobbies.
    /// Features: speaking indicators, adjustable quality, stable connect/disconnect.
    /// </summary>
    public class SteamVoiceServices : IVoiceChat
    {
#region Constants

        // Speaking detection timeout in seconds
        private const float SPEAKING_TIMEOUT = 0.3f;

        // P2P Channels
        private const int VOICE_CHANNEL = 1;
        private const int POSITION_CHANNEL = 2;

        // Sample rates for each quality level
        private static readonly Dictionary<VoiceQuality, uint> QualitySampleRates = new()
        {
            { VoiceQuality.Low, 8000 },
            { VoiceQuality.Medium, 16000 },
            { VoiceQuality.High, 48000 }
        };

#endregion

#region Fields

        private bool isRecording;
        private bool isConnected;
        private VoiceQuality currentQuality = VoiceQuality.Medium;
        private uint currentSampleRate = 16000;

        private readonly byte[] voiceBuffer = new byte[1024 * 20];
        private readonly byte[] receiveBuffer = new byte[1024 * 20];
        private readonly byte[] pcmBuffer = new byte[96000]; // Larger buffer for higher quality

        private CSteamID lobbyID = CSteamID.Nil;
        private readonly Dictionary<CSteamID, AudioSource> playerSources = new();
        private readonly Dictionary<CSteamID, float> memberLastSpeakTime = new();
        private readonly Dictionary<CSteamID, bool> memberSpeakingState = new();
        private HashSet<CSteamID> trackedMembers = new();

        private Callback<LobbyChatMsg_t> lobbyChatMsgCallback;
        private Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> p2pSessionConnectFailCallback;

#endregion

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

#region Initialization

        /// <inheritdoc />
        public void Initialize()
        {
            // Register callbacks
            lobbyChatMsgCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
            p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
        }

#endregion

#region Connection Management

        /// <inheritdoc />
        public async Task<string> JoinVoiceChat(string channelId)
        {
            if (isConnected && lobbyID.m_SteamID.ToString() == channelId)
            {
                return await Task.FromResult(channelId);
            }

            // Disconnect from previous if connected
            if (isConnected)
            {
                await LeaveVoiceChat();
            }

            lobbyID = new CSteamID(ulong.Parse(channelId));

            // Accept P2P sessions from all lobby members
            AcceptLobbyP2PSessions();

            StartVoiceRecording();
            isConnected = true;

            ChannelJoined?.Invoke(channelId);

            return await Task.FromResult(channelId);
        }

        /// <inheritdoc />
        public Task LeaveVoiceChat()
        {
            if (!isConnected)
            {
                return Task.CompletedTask;
            }

            StopVoiceRecording();

            // Close P2P sessions with all members
            CloseLobbyP2PSessions();

            // Cleanup audio sources
            foreach (var kvp in playerSources)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.gameObject);
                }
            }
            playerSources.Clear();
            memberLastSpeakTime.Clear();
            memberSpeakingState.Clear();
            trackedMembers.Clear();

            lobbyID = CSteamID.Nil;
            isConnected = false;

            ChannelLeft?.Invoke();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<string> JoinPositionalChannel(string channelId)
        {
            await JoinVoiceChat(channelId);

            // Configure audio sources for 3D spatial audio
            foreach (var src in playerSources.Values)
            {
                ConfigurePositionalAudio(src);
            }

            PositionalChannelJoined?.Invoke(channelId);
            return await Task.FromResult(channelId);
        }

#endregion

#region Voice Recording & Transmission

        private void StartVoiceRecording()
        {
            if (lobbyID == CSteamID.Nil || isRecording) return;

            isRecording = true;
            SteamUser.StartVoiceRecording();
        }

        private void StopVoiceRecording()
        {
            if (!isRecording) return;

            isRecording = false;
            SteamUser.StopVoiceRecording();
        }

        /// <inheritdoc />
        public void Update()
        {
            if (!isConnected) return;

            CheckAndHandleMemberChanges();
            UpdateSpeakingStates();
            TransmitVoice();
        }

        private void TransmitVoice()
        {
            if (!isRecording) return;

            EVoiceResult result = SteamUser.GetAvailableVoice(out uint bytesAvailable);
            if (result != EVoiceResult.k_EVoiceResultOK || bytesAvailable == 0)
                return;

            result = SteamUser.GetVoice(true, voiceBuffer, (uint)voiceBuffer.Length, out uint bytesWritten);
            if (result != EVoiceResult.k_EVoiceResultOK || bytesWritten == 0)
                return;

            // Send voice data to all lobby members
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;

                SteamNetworking.SendP2PPacket(member, voiceBuffer, bytesWritten, EP2PSend.k_EP2PSendUnreliable, VOICE_CHANNEL);
            }
        }

        /// <inheritdoc />
        public void LateUpdate()
        {
            if (!isConnected) return;

            ReceiveVoicePackets();
            HandlePositionPackets();
        }

        private void ReceiveVoicePackets()
        {
            while (SteamNetworking.IsP2PPacketAvailable(out uint dataSize, VOICE_CHANNEL))
            {
                if (!SteamNetworking.ReadP2PPacket(receiveBuffer, (uint)receiveBuffer.Length, out uint bytesRead, out CSteamID remote, VOICE_CHANNEL))
                    continue;

                // Decompress voice using current sample rate
                EVoiceResult result = SteamUser.DecompressVoice(
                    receiveBuffer,
                    bytesRead,
                    pcmBuffer,
                    (uint)pcmBuffer.Length,
                    out uint bytesDecompressed,
                    currentSampleRate
                );

                if (result != EVoiceResult.k_EVoiceResultOK || bytesDecompressed == 0)
                    continue;

                // Update speaking state
                UpdateMemberSpeaking(remote, true);

                // Convert to float samples
                int sampleCount = (int)(bytesDecompressed / 2);
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = BitConverter.ToInt16(pcmBuffer, i * 2) / 32768f;
                }

                // Create and play audio clip
                AudioClip clip = AudioClip.Create("voice_" + remote.m_SteamID, sampleCount, 1, (int)currentSampleRate, false);
                clip.SetData(samples, 0);
                PlayVoice(remote, clip);
            }
        }

        private void PlayVoice(CSteamID speaker, AudioClip clip)
        {
            AudioSource source = GetOrCreateAudioSource(speaker);
            source.clip = clip;
            source.Play();
        }

#endregion

#region Speaking Detection

        private void UpdateMemberSpeaking(CSteamID member, bool isSpeaking)
        {
            memberLastSpeakTime[member] = Time.time;

            if (!memberSpeakingState.TryGetValue(member, out bool currentState) || !currentState)
            {
                memberSpeakingState[member] = true;
                MemberSpeaking?.Invoke(member.m_SteamID.ToString(), true);
            }
        }

        private void UpdateSpeakingStates()
        {
            float currentTime = Time.time;
            List<CSteamID> toUpdate = new();

            foreach (var kvp in memberLastSpeakTime)
            {
                bool wasSpeaking = memberSpeakingState.TryGetValue(kvp.Key, out bool speaking) && speaking;
                bool shouldBeSpeaking = (currentTime - kvp.Value) < SPEAKING_TIMEOUT;

                if (wasSpeaking && !shouldBeSpeaking)
                {
                    toUpdate.Add(kvp.Key);
                }
            }

            foreach (var member in toUpdate)
            {
                memberSpeakingState[member] = false;
                MemberSpeaking?.Invoke(member.m_SteamID.ToString(), false);
            }
        }

#endregion

#region Voice Quality

        /// <inheritdoc />
        public void SetVoiceQuality(VoiceQuality quality)
        {
            if (currentQuality == quality) return;

            currentQuality = quality;
            currentSampleRate = QualitySampleRates[quality];
        }

#endregion

#region Mute Controls

        /// <inheritdoc />
        public void MuteMic()
        {
            StopVoiceRecording();
            MicStatusUpdated?.Invoke(true);
        }

        /// <inheritdoc />
        public void UnmuteMic()
        {
            if (isConnected)
            {
                StartVoiceRecording();
                UnmuteSpeaker();
            }
            MicStatusUpdated?.Invoke(false);
            SpeakerStatusUpdated?.Invoke(false);
        }

        /// <inheritdoc />
        public void MuteSpeaker()
        {
            foreach (var src in playerSources.Values)
            {
                if (src != null) src.mute = true;
            }

            StopVoiceRecording();
            SpeakerStatusUpdated?.Invoke(true);
            MicStatusUpdated?.Invoke(true);
        }

        /// <inheritdoc />
        public void UnmuteSpeaker()
        {
            foreach (var src in playerSources.Values)
            {
                if (src != null) src.mute = false;
            }

            SpeakerStatusUpdated?.Invoke(false);
        }

        /// <inheritdoc />
        public void SetMicVolume(int volume)
        {
            // Steam doesn't expose mic volume directly, but we track it for UI
            MicVolumeUpdated?.Invoke(volume);
        }

        /// <inheritdoc />
        public void SetMemberVolume(int volume, string memberId)
        {
            CSteamID steamID = new CSteamID(ulong.Parse(memberId));
            if (playerSources.TryGetValue(steamID, out AudioSource src) && src != null)
            {
                src.volume = Mathf.Clamp01(volume / 100f);
            }
            MemberDataUpdated?.Invoke(CreateMemberSettings(steamID));
        }

        /// <inheritdoc />
        public void MuteMember(string playerId)
        {
            CSteamID steamID = new CSteamID(ulong.Parse(playerId));
            if (playerSources.TryGetValue(steamID, out AudioSource src) && src != null)
            {
                src.mute = true;
            }
            MemberDataUpdated?.Invoke(CreateMemberSettings(steamID));
        }

        /// <inheritdoc />
        public void UnmuteMember(string playerId)
        {
            CSteamID steamID = new CSteamID(ulong.Parse(playerId));
            if (playerSources.TryGetValue(steamID, out AudioSource src) && src != null)
            {
                src.mute = false;
            }
            MemberDataUpdated?.Invoke(CreateMemberSettings(steamID));
        }

#endregion

#region Positional Audio

        /// <inheritdoc />
        public void Set3DPosition(GameObject participantObject)
        {
            Vector3 pos = participantObject.transform.position;
            byte[] posData = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(pos.x), 0, posData, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.y), 0, posData, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.z), 0, posData, 8, 4);

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;
                SteamNetworking.SendP2PPacket(member, posData, (uint)posData.Length, EP2PSend.k_EP2PSendUnreliable, POSITION_CHANNEL);
            }
        }

        private void HandlePositionPackets()
        {
            while (SteamNetworking.IsP2PPacketAvailable(out uint dataSize, POSITION_CHANNEL))
            {
                byte[] posBuffer = new byte[12];
                if (SteamNetworking.ReadP2PPacket(posBuffer, (uint)posBuffer.Length, out uint bytesRead, out CSteamID remote, POSITION_CHANNEL) && bytesRead == 12)
                {
                    float x = BitConverter.ToSingle(posBuffer, 0);
                    float y = BitConverter.ToSingle(posBuffer, 4);
                    float z = BitConverter.ToSingle(posBuffer, 8);

                    if (playerSources.TryGetValue(remote, out AudioSource src) && src != null)
                    {
                        src.transform.position = new Vector3(x, y, z);
                    }
                }
            }
        }

        private void ConfigurePositionalAudio(AudioSource src)
        {
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1f;
            src.maxDistance = 20f;
        }

#endregion

#region P2P Session Management

        private void AcceptLobbyP2PSessions()
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;

                // This will cause Steam to initiate a P2P session
                SteamNetworking.AllowP2PPacketRelay(true);
            }
        }

        private void CloseLobbyP2PSessions()
        {
            foreach (CSteamID member in trackedMembers)
            {
                SteamNetworking.CloseP2PSessionWithUser(member);
            }
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            // Accept P2P sessions from lobby members
            CSteamID requester = callback.m_steamIDRemote;

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                if (member == requester)
                {
                    SteamNetworking.AcceptP2PSessionWithUser(requester);
                    return;
                }
            }

            Debug.LogWarning("[SteamVoice] Rejected P2P session from non-lobby member: " + requester.m_SteamID);
        }

        private void OnP2PSessionConnectFail(P2PSessionConnectFail_t callback)
        {
            Debug.LogWarning("[SteamVoice] P2P connection failed with: " + callback.m_steamIDRemote + " Error: " + callback.m_eP2PSessionError);
        }

#endregion

#region Member Management

        private void CheckAndHandleMemberChanges()
        {
            HashSet<CSteamID> currentMembers = new();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                if (member == SteamUser.GetSteamID()) continue;
                currentMembers.Add(member);
            }

            if (!currentMembers.SetEquals(trackedMembers))
            {
                OnLobbyMembersChanged(currentMembers);
                trackedMembers = currentMembers;
            }
        }

        private void OnLobbyMembersChanged(HashSet<CSteamID> currentMembers)
        {
            // Remove resources for members who left
            foreach (CSteamID member in trackedMembers)
            {
                if (!currentMembers.Contains(member))
                {
                    if (playerSources.TryGetValue(member, out AudioSource src) && src != null)
                    {
                        UnityEngine.Object.Destroy(src.gameObject);
                    }
                    playerSources.Remove(member);
                    memberLastSpeakTime.Remove(member);
                    memberSpeakingState.Remove(member);
                    SteamNetworking.CloseP2PSessionWithUser(member);
                }
            }

            // Add resources for new members
            foreach (CSteamID member in currentMembers)
            {
                if (!trackedMembers.Contains(member))
                {
                    GetOrCreateAudioSource(member);
                }
            }

            // Notify listeners
            List<VoiceMemeberSettings> memberSettingsList = new();
            foreach (CSteamID member in currentMembers)
            {
                memberSettingsList.Add(CreateMemberSettings(member));
            }
            MembersUpdated?.Invoke(memberSettingsList);
        }

        private AudioSource GetOrCreateAudioSource(CSteamID id)
        {
            if (!playerSources.TryGetValue(id, out AudioSource src) || src == null)
            {
                GameObject go = new GameObject($"Voice_{id.m_SteamID}");
                src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0; // 2D by default
                src.playOnAwake = false;
                playerSources[id] = src;
            }
            return src;
        }

        private VoiceMemeberSettings CreateMemberSettings(CSteamID steamID)
        {
            Sprite sprite = null;
            if (FriendsManager.Instance != null)
            {
                sprite = FriendsManager.Instance.FriendsList.Find(spriteData => spriteData.PlayerId == steamID.ToString())?.Avatar;
            }

            bool isSpeaking = memberSpeakingState.TryGetValue(steamID, out bool speaking) && speaking;

            return new VoiceMemeberSettings
            {
                MemberId = steamID.ToString(),
                IsMuted = VoiceManager.Instance?.IsMemberMuted(steamID.ToString()) ?? false,
                Volume = VoiceManager.Instance?.GetMemberVolume(steamID.ToString()) ?? 50,
                DisplayName = Steamworks.SteamFriends.GetFriendPersonaName(steamID),
                Sprite = sprite
            };
        }

#endregion

#region Text Chat

        /// <inheritdoc />
        public void SendMessage(string message)
        {
            if (lobbyID == CSteamID.Nil) return;
            SteamMatchmaking.SendLobbyChatMsg(lobbyID, System.Text.Encoding.UTF8.GetBytes(message), message.Length);
        }

        private void OnLobbyChatMessage(LobbyChatMsg_t callback)
        {
            byte[] data = new byte[4096];

            int length = SteamMatchmaking.GetLobbyChatEntry(
                new CSteamID(callback.m_ulSteamIDLobby),
                (int)callback.m_iChatID,
                out CSteamID user,
                data,
                data.Length,
                out EChatEntryType type
            );

            if (length > 0 && user != SteamUser.GetSteamID())
            {
                string message = System.Text.Encoding.UTF8.GetString(data, 0, length);
                OnMessageRecived?.Invoke(user.ToString(), message);
            }
        }

#endregion
    }
}
#endif