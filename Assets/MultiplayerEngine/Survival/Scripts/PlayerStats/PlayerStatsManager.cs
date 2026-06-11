using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Unified player stats: Health, Stamina, Food, Water.
    /// Server-authoritative with network-optimized sync.
    /// </summary>
    public class PlayerStatsManager : NetworkBehaviour, IDamageable, IPlayerStatsManager
    {
        #region Serializable Stats Struct

        /// <summary>
        /// Network-serializable stats struct for efficient sync.
        /// </summary>
        [Serializable]
        public struct PlayerStats : INetworkSerializable, IEquatable<PlayerStats>
        {
            public float health;
            public float maxHealth;
            public float stamina;
            public float maxStamina;
            public float food;
            public float maxFood;
            public float water;
            public float maxWater;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref health);
                serializer.SerializeValue(ref maxHealth);
                serializer.SerializeValue(ref stamina);
                serializer.SerializeValue(ref maxStamina);
                serializer.SerializeValue(ref food);
                serializer.SerializeValue(ref maxFood);
                serializer.SerializeValue(ref water);
                serializer.SerializeValue(ref maxWater);
            }

            public bool Equals(PlayerStats other) =>
                Mathf.Approximately(health, other.health) &&
                Mathf.Approximately(stamina, other.stamina) &&
                Mathf.Approximately(food, other.food) &&
                Mathf.Approximately(water, other.water);

            public static PlayerStats Default => new PlayerStats
            {
                health = 100f, maxHealth = 100f,
                stamina = 100f, maxStamina = 100f,
                food = 100f, maxFood = 100f,
                water = 100f, maxWater = 100f
            };

            // Percentage helpers
            public float HealthPercent => maxHealth > 0 ? health / maxHealth : 0;
            public float StaminaPercent => maxStamina > 0 ? stamina / maxStamina : 0;
            public float FoodPercent => maxFood > 0 ? food / maxFood : 0;
            public float WaterPercent => maxWater > 0 ? water / maxWater : 0;
        }

        #endregion

        #region Configuration

        [Header("Base Stats")]
        [SerializeField] private float baseMaxHealth = 100f;
        [SerializeField] private float baseMaxStamina = 100f;
        [SerializeField] private float baseMaxFood = 100f;
        [SerializeField] private float baseMaxWater = 100f;

        [Header("Regeneration")]
        [SerializeField] private float healthRegenRate = 2f;
        [SerializeField] private float healthRegenRateFast = 4f;  // Faster regen when food/water >90%
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float regenDelay = 2f;
        
        [Header("Health Regen Thresholds")]
        [Tooltip("Food/Water must be above this percentage for health to regenerate")]
        [SerializeField] private float healthRegenThreshold = 0.75f;  // 75%
        [Tooltip("Food/Water must be above this percentage for faster health regen")]
        [SerializeField] private float healthRegenFastThreshold = 0.90f;  // 90%

        [Header("Drain Rates (per second)")]
        [SerializeField] private float foodDrainRate = 0.5f;
        [SerializeField] private float waterDrainRate = 0.7f;
        [SerializeField] private float runningStaminaDrain = 10f;  // Stamina drain while running

        [Header("Starvation/Dehydration")]
        [SerializeField] private float lowThreshold = 20f;  // Below this = penalties
        [SerializeField] private float starvationDamage = 1f;  // Damage per second at 0 food/water

        [Header("Network Optimization")]
        [SerializeField] private float syncThreshold = 1f;  // Min change to trigger sync
        [SerializeField] private float syncInterval = 0.5f; // Max time between syncs

        [Header("Hit Reaction")]
        [Tooltip("Duration of invincibility after taking a hit (prevents stunlock)")]
        [SerializeField] private float invincibilityDuration = 0.5f;
        [Tooltip("Custom hit particle effect (e.g. blood splash) spawned when this player takes damage")]
        [SerializeField] private GameObject bloodHitEffectPrefab;
        public GameObject BloodHitEffectPrefab => bloodHitEffectPrefab;

        [Header("Debug")]
        [Tooltip("Toggle to test running stamina drain")]
        [SerializeField] private bool debugRunning = false;

        #endregion

        #region Network State

        private NetworkVariable<PlayerStats> networkStats = new NetworkVariable<PlayerStats>(
            PlayerStats.Default, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server);

        // Local prediction for stamina (responsive feel)
        private float localStamina;
        private float lastStaminaUseTime;

        // Server-side tracking
        private PlayerStats serverStats;
        private PlayerStats lastSyncedStats;
        private float lastSyncTime;
        private float lastDamageTime;
        private float lastHitTime; // For invincibility frames

        // Server tracks if player is running (to prevent regen during sprint)
        private bool serverIsRunning;

        // Cached references for hit reaction / death
        private Animator _animator;
        private PlayerController _playerController;
        private CharacterController _characterController;
        private bool _isDead;

        // Animator parameter hashes
        private static readonly int AnimGetHit = Animator.StringToHash("GetHit");
        private static readonly int AnimHitDirX = Animator.StringToHash("HitDirectionX");
        private static readonly int AnimHitDirY = Animator.StringToHash("HitDirectionY");
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");

        #endregion

        #region Events

        public event Action<PlayerStats> OnStatsChanged;
        public event Action OnDeath;

        #endregion

        #region Properties

        public PlayerStats Stats => IsOwner ? GetLocalStats() : networkStats.Value;
        public bool IsAlive => networkStats.Value.health > 0;
        public float HealthPercentage => Stats.HealthPercent;

        // Stamina - use local for owner (responsive)
        public float CurrentStamina => IsOwner ? localStamina : networkStats.Value.stamina;
        public float MaxStamina => networkStats.Value.maxStamina;
        public bool CanAct => CurrentStamina >= 5f;

        private PlayerStats GetLocalStats()
        {
            var stats = networkStats.Value;
            stats.stamina = localStamina; // Override with local prediction
            return stats;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Cache references for hit reaction and death
            _animator = GetComponentInChildren<Animator>();
            _playerController = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
        }

        private NetworkVariable<Unity.Collections.FixedString64Bytes> netPlayerId = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Assign defaults immediately
                serverStats = CreateDefaultStats();
                networkStats.Value = serverStats;
                lastSyncedStats = serverStats;

                SaveGameManager.OnAutoSave += SaveStatsAuto;
            }

            localStamina = networkStats.Value.stamina;
            networkStats.OnValueChanged += OnNetworkStatsChanged;

            // Owner auto-connects to the UI
            if (IsOwner)
            {
                ConnectToUI();
                
                // Owner sends their PlayerID to the server
                if (PlayerProfileManager.Instance?.LocalPlayerStats != null)
                {
                    SetPlayerIdServerRpc(PlayerProfileManager.Instance.LocalPlayerStats.PlayerId);
                }
            }
            
            // If we already have a valid ID on spawn (host or rejoin), load
            if (IsServer && !string.IsNullOrEmpty(netPlayerId.Value.ToString()))
            {
                LoadStatsAndApplyAsync();
            }
        }

        [Rpc(SendTo.Server)]
        private void SetPlayerIdServerRpc(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            
            netPlayerId.Value = playerId;
            
            LoadStatsAndApplyAsync();
        }

        /// <summary>
        /// Finds and connects to PlayerStatsUI (owner only).
        /// </summary>
        private void ConnectToUI()
        {
            var statsUI = FindFirstObjectByType<PlayerStatsUI>();
            if (statsUI != null)
            {
                statsUI.SetStatsManager(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                SaveGameManager.OnAutoSave -= SaveStatsAuto;
                SaveStats(serverStats);
            }

            networkStats.OnValueChanged -= OnNetworkStatsChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                UpdateServerStats();
            }
            
            if (IsOwner)
            {
                UpdateLocalStamina();
            }
        }

        #endregion

        #region Server Update

        private void UpdateServerStats()
        {
            float dt = Time.deltaTime;
            bool changed = false;

            // Food drain
            if (serverStats.food > 0)
            {
                serverStats.food = Mathf.Max(0, serverStats.food - foodDrainRate * dt);
                changed = true;
            }

            // Water drain
            if (serverStats.water > 0)
            {
                serverStats.water = Mathf.Max(0, serverStats.water - waterDrainRate * dt);
                changed = true;
            }

            // Starvation/Dehydration damage
            if (serverStats.food <= 0 || serverStats.water <= 0)
            {
                float damage = starvationDamage * dt;
                serverStats.health = Mathf.Max(0, serverStats.health - damage);
                changed = true;

                if (serverStats.health <= 0)
                {
                    HandleDeath();
                }
            }
            else
            {
                // Health regeneration based on food/water levels
                // Only regenerate if not at max health
                if (serverStats.health < serverStats.maxHealth)
                {
                    float foodPercent = serverStats.FoodPercent;
                    float waterPercent = serverStats.WaterPercent;
                    
                    // Both food AND water must be above 75% for health to regenerate
                    if (foodPercent > healthRegenThreshold && waterPercent > healthRegenThreshold)
                    {
                        float regenRate;
                        
                        // Faster regeneration if both are above 90%
                        if (foodPercent > healthRegenFastThreshold && waterPercent > healthRegenFastThreshold)
                        {
                            regenRate = healthRegenRateFast;
                        }
                        else
                        {
                            regenRate = healthRegenRate;
                        }
                        
                        serverStats.health = Mathf.Min(serverStats.maxHealth, serverStats.health + regenRate * dt);
                        changed = true;
                    }
                }
            }

            // NOTE: Stamina always regenerates on client-side for responsiveness. Server tracks drain via RPC.

            // Sync if changed and threshold met
            if (changed)
            {
                TrySyncStats();
            }
        }

        private void TrySyncStats()
        {
            float timeSinceSync = Time.time - lastSyncTime;
            
            // Check if change is significant or enough time passed
            float healthDiff = Mathf.Abs(serverStats.health - lastSyncedStats.health);
            float staminaDiff = Mathf.Abs(serverStats.stamina - lastSyncedStats.stamina);
            float foodDiff = Mathf.Abs(serverStats.food - lastSyncedStats.food);
            float waterDiff = Mathf.Abs(serverStats.water - lastSyncedStats.water);

            bool significantChange = healthDiff >= syncThreshold || 
                                    staminaDiff >= syncThreshold ||
                                    foodDiff >= syncThreshold || 
                                    waterDiff >= syncThreshold;

            if (significantChange || timeSinceSync >= syncInterval)
            {
                networkStats.Value = serverStats;
                lastSyncedStats = serverStats;
                lastSyncTime = Time.time;
            }
        }

        #endregion

        #region Local Stamina (Client Prediction)

        // Running state
        private bool isRunning;

        private void UpdateLocalStamina()
        {
            // Check if running (via code or debug toggle)
            bool running = isRunning || debugRunning;
            
            // If running, drain stamina
            if (running && localStamina > 0)
            {
                float drainAmount = runningStaminaDrain * Time.deltaTime;
                localStamina = Mathf.Max(0, localStamina - drainAmount);
                lastStaminaUseTime = Time.time;
                OnStatsChanged?.Invoke(GetLocalStats());
                
                // Notify server periodically (not every frame)
                if (Time.frameCount % 10 == 0)
                {
                    SyncStaminaToServerRpc(localStamina);
                }
                return;
            }

            // Regenerate locally for responsive feel (only after delay)
            if (Time.time > lastStaminaUseTime + regenDelay && localStamina < MaxStamina)
            {
                float waterMult = networkStats.Value.water > lowThreshold ? 1f : 0.3f;
                localStamina = Mathf.Min(MaxStamina, localStamina + staminaRegenRate * waterMult * Time.deltaTime);
                OnStatsChanged?.Invoke(GetLocalStats());
                
                // Sync regen to server periodically
                if (Time.frameCount % 15 == 0)
                {
                    SyncStaminaToServerRpc(localStamina);
                }
            }
        }

        /// <summary>
        /// Set running state. Call from CharacterController when player starts/stops running.
        /// </summary>
        public void SetRunning(bool running)
        {
            if (!IsOwner) return;
            
            // Notify server of running state change
            if (isRunning != running)
            {
                SetRunningServerRpc(running);
            }
            
            isRunning = running;
        }

        /// <summary>
        /// Check if player can run (has stamina).
        /// </summary>
        public bool CanRun => localStamina > 5f;

        /// <summary>
        /// Try to use stamina (client-predicted, server-verified).
        /// </summary>
        public bool TryUseStamina(float amount)
        {
            if (!IsOwner) return false;
            if (localStamina < amount) return false;

            localStamina = Mathf.Max(0, localStamina - amount);
            lastStaminaUseTime = Time.time;
            
            SyncStaminaToServerRpc(localStamina);
            OnStatsChanged?.Invoke(GetLocalStats());
            
            return true;
        }

        public bool HasStamina(float amount) => CurrentStamina >= amount;

        /// <summary>
        /// Syncs the client's local stamina to the server (authoritative update).
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SyncStaminaToServerRpc(float staminaValue)
        {
            serverStats.stamina = Mathf.Clamp(staminaValue, 0, serverStats.maxStamina);
            
            // Sync to network
            networkStats.Value = serverStats;
            lastSyncedStats = serverStats;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// Notifies server of running state (for validation/logging).
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SetRunningServerRpc(bool running)
        {
            serverIsRunning = running;
        }

        #endregion

        #region IDamageable

        public bool TakeDamage(DamageInfo damage)
        {
            Debug.Log($"[PlayerStatsManager] TakeDamage called on player '{gameObject.name}' (Attacker ID: {damage.attackerId}, Amount: {damage.amount}, Type: {damage.type})");
            
            if (!IsServer)
            {
                Debug.LogWarning($"[PlayerStatsManager] TakeDamage on '{gameObject.name}' ignored: Client is not the Server.");
                return false;
            }
            if (!IsAlive)
            {
                Debug.LogWarning($"[PlayerStatsManager] TakeDamage on '{gameObject.name}' ignored: Player is already dead.");
                return false;
            }

            // Invincibility frame check
            if (Time.time < lastHitTime + invincibilityDuration)
            {
                Debug.Log($"[PlayerStatsManager] TakeDamage on '{gameObject.name}' ignored: inside invincibility frame duration. Current time: {Time.time}, last hit: {lastHitTime}, duration: {invincibilityDuration}");
                return false;
            }

            float previousHealth = serverStats.health;
            serverStats.health = Mathf.Max(0, serverStats.health - damage.amount);
            Debug.Log($"[PlayerStatsManager] Player '{gameObject.name}' took damage! Health reduced from {previousHealth} to {serverStats.health}");
            
            lastDamageTime = Time.time;
            lastHitTime = Time.time;

            // Immediate sync for damage
            networkStats.Value = serverStats;
            lastSyncedStats = serverStats;
            lastSyncTime = Time.time;

            if (serverStats.health <= 0)
            {
                Debug.Log($"[PlayerStatsManager] Player '{gameObject.name}' health reached 0. Triggering death.");
                HandleDeath();
            }
            else
            {
                // Play hit reaction on all clients
                Debug.Log($"[PlayerStatsManager] Triggering hit reaction PlayHitReactionRpc on all clients with hitDirection: {damage.hitDirection}");
                PlayHitReactionRpc(damage.hitDirection);
            }

            return true;
        }

        #endregion

        #region Hit Reaction

        /// <summary>
        /// Plays hit reaction animation on all clients.
        /// Converts world-space hit direction to local-space for directional blend tree.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PlayHitReactionRpc(Vector3 hitDirection)
        {
            if (_animator == null) return;

            // Convert world hit direction to local space for directional reactions
            if (hitDirection.sqrMagnitude > 0.01f)
            {
                Vector3 localDir = transform.InverseTransformDirection(hitDirection).normalized;
                _animator.SetFloat(AnimHitDirX, localDir.x);
                _animator.SetFloat(AnimHitDirY, localDir.z);
            }
            else
            {
                _animator.SetFloat(AnimHitDirX, 0f);
                _animator.SetFloat(AnimHitDirY, 0f);
            }

            _animator.SetTrigger(AnimGetHit);
        }

        #endregion

        #region Stat Modification (Consumables, etc.)

        /// <summary>
        /// Restore stats from consumable (called from ConsumableHandler).
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RestoreStatsRpc(float health, float stamina, float food, float water)
        {
            if (!IsServer) return;

            if (health > 0)
                serverStats.health = Mathf.Min(serverStats.maxHealth, serverStats.health + health);
            if (stamina > 0)
                serverStats.stamina = Mathf.Min(serverStats.maxStamina, serverStats.stamina + stamina);
            if (food > 0)
                serverStats.food = Mathf.Min(serverStats.maxFood, serverStats.food + food);
            if (water > 0)
                serverStats.water = Mathf.Min(serverStats.maxWater, serverStats.water + water);

            // Immediate sync for consumable effects
            networkStats.Value = serverStats;
            lastSyncedStats = serverStats;
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// Heal health directly (server only).
        /// </summary>
        public void Heal(float amount)
        {
            if (!IsServer) return;
            serverStats.health = Mathf.Min(serverStats.maxHealth, serverStats.health + amount);
        }

        #endregion

        #region Death

        private void HandleDeath()
        {
            if (!IsServer) return;

            // Notify all clients to play death animation and disable movement
            OnDeathRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void OnDeathRpc()
        {
            _isDead = true;

            // Play death animation
            if (_animator != null)
            {
                _animator.SetBool(AnimIsDead, true);
            }

            // Disable movement
            if (_playerController != null)
                _playerController.enabled = false;
            if (_characterController != null)
                _characterController.enabled = false;

            OnDeath?.Invoke();
        }

        /// <summary>
        /// Respawn with full stats (server only).
        /// Re-enables components and resets death state on all clients.
        /// </summary>
        public void Respawn()
        {
            if (!IsServer) return;

            serverStats = CreateDefaultStats();
            networkStats.Value = serverStats;
            lastSyncedStats = serverStats;

            OnRespawnRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void OnRespawnRpc()
        {
            _isDead = false;

            // Reset death animation
            if (_animator != null)
            {
                _animator.SetBool(AnimIsDead, false);
            }

            // Re-enable movement
            if (_playerController != null)
                _playerController.enabled = true;
            if (_characterController != null)
                _characterController.enabled = true;
        }

        /// <summary>
        /// Debug button: Reset all stats to max.
        /// Click the gear icon on the component in Inspector → Reset Stats
        /// </summary>
        [ContextMenu("Reset Stats")]
        public void ResetStatsDebug()
        {
            if (IsOwner)
            {
                // Client: request reset from server
                ResetStatsRpc();
                localStamina = baseMaxStamina;
                OnStatsChanged?.Invoke(GetLocalStats());
            }
        }

        [Rpc(SendTo.Server)]
        private void ResetStatsRpc()
        {
            serverStats = CreateDefaultStats();
            networkStats.Value = serverStats;
            lastSyncedStats = serverStats;
            lastSyncTime = Time.time;
            Debug.Log("[PlayerStatsManager] Stats reset to defaults");
        }

        #endregion

        #region Save/Load

        [Serializable]
        private class StatsSaveData
        {
            public string ownerId;
            public PlayerStats stats;
            public long timestamp;
        }

        private PlayerStats CreateDefaultStats()
        {
            return new PlayerStats
            {
                health = baseMaxHealth, maxHealth = baseMaxHealth,
                stamina = baseMaxStamina, maxStamina = baseMaxStamina,
                food = baseMaxFood, maxFood = baseMaxFood,
                water = baseMaxWater, maxWater = baseMaxWater
            };
        }

        private async void SaveStats(PlayerStats stats)
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId)) return; // No active game — skip save

            try
            {
                string ownerId = GetOwnerId();
                if (string.IsNullOrEmpty(ownerId)) return;

                var saveData = new StatsSaveData
                {
                    ownerId = ownerId,
                    stats = stats,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await SaveGameManager.Instance.SavePlayerSubsystemToHostAsync(gameId, ownerId, "stats", saveData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save player stats: {e.Message}");
            }
        }

        private void SaveStatsAuto()
        {
            if (IsServer) SaveStats(serverStats);
        }

        private async void LoadStatsAndApplyAsync()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId)) return; // No active game — fresh stats

            string ownerId = GetOwnerId();
            if (string.IsNullOrEmpty(ownerId)) return;

            PlayerStats? loadedStats = null;

            try
            {
                var data = await SaveGameManager.Instance.LoadPlayerSubsystemFromHostAsync<StatsSaveData>(gameId, ownerId, "stats");
                loadedStats = data?.stats;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load player stats: {e.Message}");
            }

            if (loadedStats.HasValue)
            {
                serverStats = loadedStats.Value;
                networkStats.Value = serverStats;
                lastSyncedStats = serverStats;
            }
        }

        /// <summary>
        /// Resolves the owner key for save data.
        /// </summary>
        private string GetOwnerId()
        {
            string playerId = GetPlayerId();
            if (string.IsNullOrEmpty(playerId)) return null;

            return SaveGameManager.Instance?.GetOwnerKey(playerId) ?? playerId;
        }

        private string GetPlayerId()
        {
            if (!string.IsNullOrEmpty(netPlayerId.Value.ToString()))
            {
                return netPlayerId.Value.ToString();
            }

            // Use player profile if available (fallback)
            if (PlayerProfileManager.Instance?.LocalPlayerStats != null &&
                NetworkManager.Singleton.LocalClientId == OwnerClientId)
            {
                return PlayerProfileManager.Instance.LocalPlayerStats.PlayerId;
            }
            // Fallback to client ID
            return OwnerClientId.ToString();
        }

        #endregion

        #region Event Handlers

        private void OnNetworkStatsChanged(PlayerStats previous, PlayerStats current)
        {
            if (IsOwner)
            {
                // Don't overwrite local stamina prediction
                var localStats = current;
                localStats.stamina = localStamina;
                OnStatsChanged?.Invoke(localStats);
            }
            else
            {
                OnStatsChanged?.Invoke(current);
            }
        }

        #endregion
    }
}