using System;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Manages player stamina for actions like attacking, blocking, harvesting.
    /// Server-authoritative with client prediction for responsiveness.
    /// </summary>
    public class StaminaManager : NetworkBehaviour, IStaminaManager
    {
        [Header("Configuration")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenRate = 10f;          // Per second
        [SerializeField] private float regenDelay = 1.5f;        // Delay after using stamina
        [SerializeField] private float minStaminaToAct = 5f;     // Minimum needed to start action

        // Network synced current stamina
        private NetworkVariable<float> currentStamina = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Local state for prediction
        private float localStamina;
        private float lastUseTime;
        private bool isRegenerating;

        // Events
        public event Action<float, float> OnStaminaChanged; // current, max

        #region Properties

        public float MaxStamina => maxStamina;
        public float CurrentStamina => IsOwner ? localStamina : currentStamina.Value;
        public float StaminaPercentage => CurrentStamina / maxStamina;
        public bool CanAct => CurrentStamina >= minStaminaToAct;

        #endregion

        #region Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                currentStamina.Value = maxStamina;
            }

            localStamina = currentStamina.Value;
            currentStamina.OnValueChanged += OnServerStaminaChanged;
        }

        public override void OnNetworkDespawn()
        {
            currentStamina.OnValueChanged -= OnServerStaminaChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // Handle regeneration
            if (IsOwner)
            {
                UpdateLocalRegeneration();
            }
            else if (IsServer)
            {
                UpdateServerRegeneration();
            }
        }

        #endregion

        #region Stamina Usage

        /// <summary>
        /// Attempts to use stamina for an action.
        /// Returns true if successful (enough stamina available).
        /// </summary>
        /// <param name="amount">Amount of stamina to use</param>
        /// <returns>True if stamina was consumed</returns>
        public bool TryUseStamina(float amount)
        {
            if (!IsOwner) return false;

            // Check if enough stamina (use local for responsiveness)
            if (localStamina < amount)
            {
                return false;
            }

            // Consume locally for instant feedback
            localStamina = Mathf.Max(0, localStamina - amount);
            lastUseTime = Time.time;
            isRegenerating = false;

            // Notify server
            UseStaminaRpc(amount);

            // Fire event for UI
            OnStaminaChanged?.Invoke(localStamina, maxStamina);

            return true;
        }

        /// <summary>
        /// Quickly checks if stamina is available without consuming.
        /// </summary>
        public bool HasStamina(float amount)
        {
            return (IsOwner ? localStamina : currentStamina.Value) >= amount;
        }

        [Rpc(SendTo.Server)]
        private void UseStaminaRpc(float amount)
        {
            if (!IsServer) return;

            currentStamina.Value = Mathf.Max(0, currentStamina.Value - amount);
        }

        #endregion

        #region Regeneration

        private void UpdateLocalRegeneration()
        {
            // Wait for delay after using stamina
            if (Time.time < lastUseTime + regenDelay)
            {
                return;
            }

            // Regenerate
            if (localStamina < maxStamina)
            {
                localStamina = Mathf.Min(maxStamina, localStamina + regenRate * Time.deltaTime);
                OnStaminaChanged?.Invoke(localStamina, maxStamina);
            }
        }

        private void UpdateServerRegeneration()
        {
            // Server regeneration (authoritative)
            if (currentStamina.Value < maxStamina)
            {
                // Note: Server doesn't track lastUseTime per-player in this simple impl
                // For more accuracy, use a separate tracker or trust the client delay
                currentStamina.Value = Mathf.Min(maxStamina, currentStamina.Value + regenRate * Time.deltaTime);
            }
        }

        private void OnServerStaminaChanged(float previous, float current)
        {
            // Sync local stamina if too far off from server (reconciliation)
            if (IsOwner)
            {
                float diff = Mathf.Abs(localStamina - current);
                if (diff > 10f) // Threshold for correction
                {
                    localStamina = current;
                    OnStaminaChanged?.Invoke(localStamina, maxStamina);
                }
            }
            else
            {
                // Non-owner just uses server value
                OnStaminaChanged?.Invoke(current, maxStamina);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Instantly restores stamina (e.g., from consumable).
        /// </summary>
        public void RestoreStamina(float amount)
        {
            if (IsOwner)
            {
                RestoreStaminaRpc(amount);
                localStamina = Mathf.Min(maxStamina, localStamina + amount);
                OnStaminaChanged?.Invoke(localStamina, maxStamina);
            }
        }

        [Rpc(SendTo.Server)]
        private void RestoreStaminaRpc(float amount)
        {
            if (!IsServer) return;
            currentStamina.Value = Mathf.Min(maxStamina, currentStamina.Value + amount);
        }

        /// <summary>
        /// Sets max stamina (for upgrades/buffs).
        /// </summary>
        public void SetMaxStamina(float newMax)
        {
            if (IsServer)
            {
                maxStamina = newMax;
                currentStamina.Value = Mathf.Min(currentStamina.Value, newMax);
            }
        }

        #endregion
    }
}