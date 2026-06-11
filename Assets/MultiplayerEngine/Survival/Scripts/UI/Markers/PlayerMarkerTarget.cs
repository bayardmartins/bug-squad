using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Attaches to the player prefab to show an on-screen/off-screen marker
    /// for other players (not the local owner). Displays player name, character icon,
    /// and health bar when in close proximity.
    /// 
    /// Automatically reads the player's display name and character icon from
    /// RuntimeSessionData / PlayerProfileManager and health from PlayerStatsManager.
    /// </summary>
    [RequireComponent(typeof(PlayerStatsManager))]
    public class PlayerMarkerTarget : NetworkBehaviour
    {
        [Header("Marker Settings")]
        [Tooltip("Vertical offset above the player's pivot (world units)")]
        [SerializeField] private float heightOffset = 2.2f;

        [Tooltip("Fallback icon if character icon can't be resolved")]
        [SerializeField] private Sprite fallbackIcon;

        /// <summary>
        /// Synced player display name — set by the owner, readable by all clients.
        /// </summary>
        private NetworkVariable<FixedString64Bytes> netDisplayName = new NetworkVariable<FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Synced character ID — set by the owner, used by other clients to look up the CharacterData icon.
        /// </summary>
        private NetworkVariable<FixedString64Bytes> netCharacterId = new NetworkVariable<FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private MarkerTarget markerTarget;
        private PlayerStatsManager statsManager;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            statsManager = GetComponent<PlayerStatsManager>();

            if (IsOwner)
            {
                // Set our display name from RuntimeSessionData
                string name = RuntimeSessionData.Instance?.DisplayName ?? "Player";
                netDisplayName.Value = name;

                // Set our character ID so other clients can look up the icon
                string charId = RuntimeSessionData.Instance?.SelectedCharacterId ?? "";
                netCharacterId.Value = charId;

                // Don't show marker for ourselves
                return;
            }

            // Non-owner: create a MarkerTarget to track this player
            CreateMarker();

            // Listen for name/character changes
            netDisplayName.OnValueChanged += OnNameChanged;
            netCharacterId.OnValueChanged += OnCharacterChanged;

            // Listen for health changes to update the marker
            if (statsManager != null)
                statsManager.OnStatsChanged += OnStatsChanged;
        }

        public override void OnNetworkDespawn()
        {
            // Cleanup
            netDisplayName.OnValueChanged -= OnNameChanged;
            netCharacterId.OnValueChanged -= OnCharacterChanged;

            if (statsManager != null)
                statsManager.OnStatsChanged -= OnStatsChanged;

            if (markerTarget != null)
            {
                if (MarkerManager.Instance != null)
                    MarkerManager.Instance.Unregister(markerTarget);

                Destroy(markerTarget);
            }

            base.OnNetworkDespawn();
        }

        private void CreateMarker()
        {
            // Add MarkerTarget directly to this player's GameObject
            markerTarget = gameObject.AddComponent<MarkerTarget>();

            string name = netDisplayName.Value.ToString();
            if (string.IsNullOrEmpty(name)) name = "Player";

            Sprite icon = ResolveCharacterIcon();

            markerTarget.SetPingData(name, icon, Color.white, heightOffset);

            // Set health data
            if (statsManager != null)
                markerTarget.HealthPercent = statsManager.HealthPercentage;

            // Permanent marker (no auto-expire)
            markerTarget.Lifetime = -1f;

            // Register with MarkerManager
            if (MarkerManager.Instance != null)
                MarkerManager.Instance.Register(markerTarget);
            else
                MarkerManager.RegisterPending(markerTarget);
        }

        /// <summary>
        /// Looks up the CharacterData icon from the synced character ID.
        /// </summary>
        private Sprite ResolveCharacterIcon()
        {
            string charId = netCharacterId.Value.ToString();

            if (!string.IsNullOrEmpty(charId) && PlayerProfileManager.Instance != null)
            {
                var charData = PlayerProfileManager.Instance.CharacterData?
                    .FirstOrDefault(c => c.CharacterId == charId);

                if (charData != null && charData.CharacterIcon != null)
                    return charData.CharacterIcon;
            }

            return fallbackIcon;
        }

        private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
        {
            if (markerTarget != null)
            {
                Sprite icon = ResolveCharacterIcon();
                markerTarget.SetPingData(newName.ToString(), icon, Color.white, heightOffset);
            }
        }

        private void OnCharacterChanged(FixedString64Bytes oldId, FixedString64Bytes newId)
        {
            if (markerTarget != null)
            {
                string name = netDisplayName.Value.ToString();
                if (string.IsNullOrEmpty(name)) name = "Player";

                Sprite icon = ResolveCharacterIcon();
                markerTarget.SetPingData(name, icon, Color.white, heightOffset);
            }
        }

        private void OnStatsChanged(PlayerStatsManager.PlayerStats stats)
        {
            if (markerTarget != null)
            {
                markerTarget.HealthPercent = stats.HealthPercent;
            }
        }
    }
}