using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Server-side state for a resource.
    /// </summary>
    public struct ResourceState
    {
        public float CurrentHealth;
        public float MaxHealth;
        public bool IsAlive;

        public ResourceState(float maxHealth)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            IsAlive = true;
        }
    }

    /// <summary>
    /// Central manager for all harvestable resources in the game.
    /// Single NetworkBehaviour that handles all resource networking.
    /// Individual resources (trees, rocks, etc.) register with this manager
    /// and are purely local MonoBehaviours.
    /// </summary>
    public class ResourceManager : NetworkBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Drop Settings")]
        [SerializeField] private float dropSpreadRadius = 2f;
        [SerializeField] private float dropSpawnHeight = 1f;

        // Registered resources by ID
        private Dictionary<int, LocalResource> resources = new Dictionary<int, LocalResource>();

        // Server-side state tracking
        private Dictionary<int, ResourceState> resourceStates = new Dictionary<int, ResourceState>();

        // Event for when a resource is destroyed
        public event Action<int, ulong> OnResourceDestroyed; // resourceId, lastAttackerId

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #region Resource Registration

        /// <summary>
        /// Register a resource with the manager. Called by LocalResource.Start()
        /// </summary>
        public void RegisterResource(LocalResource resource)
        {
            if (resource == null) return;

            int id = resource.ResourceId;
            if (!resources.ContainsKey(id))
            {
                resources.Add(id, resource);

                // Initialize server state if we're the server
                if (IsServer)
                {
                    resourceStates[id] = new ResourceState(resource.MaxHealth);
                }
            }
        }

        /// <summary>
        /// Unregister a resource. Called when resource is destroyed.
        /// </summary>
        public void UnregisterResource(int resourceId)
        {
            resources.Remove(resourceId);
            resourceStates.Remove(resourceId);
        }

        /// <summary>
        /// Get a resource by ID.
        /// </summary>
        public LocalResource GetResource(int resourceId)
        {
            return resources.TryGetValue(resourceId, out var resource) ? resource : null;
        }

        #endregion

        #region Damage Handling

        /// <summary>
        /// Request damage to a resource. Called by tools/weapons locally.
        /// Routes to server via RPC.
        /// </summary>
        public void RequestDamage(int resourceId, float damage, Vector3 hitPoint, ulong attackerId, HarvestableType expectedType, int toolTier)
        {
            // Send to server for validation
            RequestDamageRpc(resourceId, damage, hitPoint, attackerId, (int)expectedType, toolTier);
        }

        [Rpc(SendTo.Server)]
        private void RequestDamageRpc(int resourceId, float damage, Vector3 hitPoint, ulong attackerId, int expectedType, int toolTier)
        {
            
            // Validate resource exists
            if (!resources.TryGetValue(resourceId, out var resource))
            {
                Debug.LogWarning($"[ResourceManager] Resource {resourceId} not found! Registered IDs: {string.Join(", ", resources.Keys)}");
                return;
            }
            if (!resourceStates.TryGetValue(resourceId, out var state))
            {
                // Initialize state now if it wasn't set during registration (timing issue with IsServer)
                state = new ResourceState(resource.MaxHealth);
                resourceStates[resourceId] = state;
            }
            if (!state.IsAlive)
            {
                return;
            }

            // Validate tool can harvest this type
            if (resource.ResourceType != (HarvestableType)expectedType)
            {
                return;
            }

            // Validate tier requirement
            if (toolTier < resource.RequiredTier)
            {
                return;
            }


            // Apply damage
            state.CurrentHealth = Mathf.Max(0, state.CurrentHealth - damage);
            state.IsAlive = state.CurrentHealth > 0;
            resourceStates[resourceId] = state;

            // Calculate hit direction for fall
            Vector3 hitDirection = Vector3.zero;
            if (resource != null)
            {
                hitDirection = (resource.transform.position - hitPoint).normalized;
                hitDirection.y = 0;
            }

            // Broadcast hit to all clients
            OnResourceHitRpc(resourceId, hitPoint, state.CurrentHealth / state.MaxHealth);

            // Check for destruction
            if (!state.IsAlive)
            {
                // Spawn drops (unless resource handles its own drop timing)
                if (!resource.DelaysDropSpawning)
                {
                    SpawnDrops(resource, resource.transform.position);
                }

                // Broadcast destruction
                OnResourceDestroyedRpc(resourceId, hitDirection, attackerId);

                // Fire event
                OnResourceDestroyed?.Invoke(resourceId, attackerId);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void OnResourceHitRpc(int resourceId, Vector3 hitPoint, float healthPercent)
        {
            if (resources.TryGetValue(resourceId, out var resource))
            {
                resource.OnHit(hitPoint, healthPercent);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void OnResourceDestroyedRpc(int resourceId, Vector3 fallDirection, ulong lastAttackerId)
        {
            if (resources.TryGetValue(resourceId, out var resource))
            {
                resource.OnDestroyed(fallDirection);
            }
        }

        #endregion

        #region Drop Spawning

        private void SpawnDrops(LocalResource resource, Vector3 spawnPosition)
        {
            if (!IsServer) return;

            var dropConfig = resource.GetDropConfig();
            if (dropConfig.dropItem == null || dropConfig.dropItem.networkPrefab == null) return;

            int dropCount = UnityEngine.Random.Range(dropConfig.minDrops, dropConfig.maxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * dropSpreadRadius;
                Vector3 spawnPos = spawnPosition + new Vector3(offset.x, dropSpawnHeight, offset.y);

                var drop = Instantiate(dropConfig.dropItem.networkPrefab, spawnPos, Quaternion.identity);
                var netObj = drop.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
        }

        /// <summary>
        /// Spawn drops at a specific position. Called by resources that delay drop spawning
        /// (like trees that need to fall first).
        /// </summary>
        public void SpawnDropsAtPosition(LocalResource resource, Vector3 position)
        {
            if (resource == null) return;
            
            // Only server spawns drops, but clients can request via RPC
            if (IsServer)
            {
                SpawnDrops(resource, position);
            }
            else
            {
                // Client requests server to spawn drops
                RequestDropsAtPositionRpc(resource.ResourceId, position);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestDropsAtPositionRpc(int resourceId, Vector3 position)
        {
            if (resources.TryGetValue(resourceId, out var resource))
            {
                SpawnDrops(resource, position);
            }
        }

        /// <summary>
        /// Spawn drops distributed across multiple positions. Drops are round-robin
        /// assigned to the provided positions with a small spread around each.
        /// Used by trees with configured spawn points along the trunk.
        /// </summary>
        public void SpawnDropsAtPositions(LocalResource resource, List<Vector3> positions)
        {
            if (resource == null || positions == null || positions.Count == 0) return;

            if (IsServer)
            {
                SpawnDropsDistributed(resource, positions);
            }
            else
            {
                // Client requests server to spawn drops - send as array for RPC serialization
                RequestDropsAtPositionsRpc(resource.ResourceId, positions.ToArray());
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestDropsAtPositionsRpc(int resourceId, Vector3[] positions)
        {
            if (resources.TryGetValue(resourceId, out var resource))
            {
                SpawnDropsDistributed(resource, new List<Vector3>(positions));
            }
        }

        /// <summary>
        /// Spawns drops distributed round-robin across the provided positions.
        /// Each drop gets a small random offset around its assigned spawn point.
        /// </summary>
        private void SpawnDropsDistributed(LocalResource resource, List<Vector3> positions)
        {
            if (!IsServer) return;

            var dropConfig = resource.GetDropConfig();
            if (dropConfig.dropItem == null || dropConfig.dropItem.networkPrefab == null) return;

            int dropCount = UnityEngine.Random.Range(dropConfig.minDrops, dropConfig.maxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                // Round-robin assign drops to spawn points
                Vector3 basePosition = positions[i % positions.Count];

                // Spawn exactly at the point, just slightly above so it drops to ground
                Vector3 spawnPos = basePosition + new Vector3(0f, dropSpawnHeight, 0f);

                var drop = Instantiate(dropConfig.dropItem.networkPrefab, spawnPos, Quaternion.identity);
                var netObj = drop.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get current health percentage for a resource (for UI).
        /// </summary>
        public float GetHealthPercentage(int resourceId)
        {
            if (resourceStates.TryGetValue(resourceId, out var state))
            {
                return state.CurrentHealth / state.MaxHealth;
            }
            return 1f;
        }

        /// <summary>
        /// Check if a resource is alive.
        /// </summary>
        public bool IsResourceAlive(int resourceId)
        {
            if (resourceStates.TryGetValue(resourceId, out var state))
            {
                return state.IsAlive;
            }
            // If not on server, check local resource
            if (resources.TryGetValue(resourceId, out var resource))
            {
                return resource.IsAlive;
            }
            return false;
        }

        /// <summary>
        /// Reset a resource to full health (for respawning).
        /// </summary>
        public void ResetResource(int resourceId)
        {
            if (!IsServer) return;

            if (resources.TryGetValue(resourceId, out var resource))
            {
                resourceStates[resourceId] = new ResourceState(resource.MaxHealth);
                resource.OnReset();
            }
        }

        #endregion
    }
}