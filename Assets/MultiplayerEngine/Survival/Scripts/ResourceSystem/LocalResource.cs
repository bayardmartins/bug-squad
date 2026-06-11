using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Configuration for resource drops when destroyed.
    /// </summary>
    [System.Serializable]
    public struct ResourceDropConfig
    {
        [Tooltip("Item to drop - uses networkPrefab from this item data")]
        public InventoryItemData dropItem;
        public int minDrops;
        public int maxDrops;
    }

    /// <summary>
    /// Base class for all harvestable resources (trees, rocks, etc.).
    /// NOT a NetworkBehaviour - purely local. Syncs through ResourceManager.
    /// Resources must have a stable ID assigned by the editor to work in multiplayer.
    /// </summary>
    public abstract class LocalResource : MonoBehaviour, IHarvestable
    {
        // Stable ID - assigned by editor script, must be same on all clients
        [SerializeField, HideInInspector] protected int resourceId = 0;

        [Header("Resource Settings")]
        [SerializeField] protected int requiredTier = 1;
        [SerializeField] protected float maxHealth = 100f;

        [Header("Drops")]
        [SerializeField] protected ResourceDropConfig dropConfig;

        // Local state tracking (visual only - server is authoritative)
        protected bool isAlive = true;
        protected float localHealthPercent = 1f;

        #region Properties

        /// <summary>
        /// Unique identifier for this resource.
        /// </summary>
        public int ResourceId => resourceId;

        /// <summary>
        /// Maximum health of this resource.
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// If true, ResourceManager will NOT spawn drops immediately on destroy.
        /// The resource is responsible for requesting drops later via RequestDropsAtPosition().
        /// Used for trees that need to fall before spawning drops at the fallen position.
        /// </summary>
        public virtual bool DelaysDropSpawning => false;

        #endregion

        #region IHarvestable Implementation

        public abstract HarvestableType ResourceType { get; }
        public int RequiredTier => requiredTier;
        public bool IsAlive => isAlive;
        public float HealthPercentage => localHealthPercent;

        /// <summary>
        /// Called when a tool hits this resource. Routes to ResourceManager.
        /// </summary>
        public bool TakeHarvestDamage(float damage, Vector3 hitPoint, ulong attackerId)
        {
            if (!isAlive) return false;

            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.RequestDamage(
                    resourceId,
                    damage,
                    hitPoint,
                    attackerId,
                    ResourceType,
                    requiredTier
                );
                return true; // Request sent - actual success determined by server
            }

            return false;
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Validate ID was assigned by editor
            if (resourceId == 0)
            {
                Debug.LogError($"[LocalResource] {gameObject.name} has no resourceId assigned! Select It in hierarchy and it will auto-assign.", this);
            }
        }

        protected virtual void Start()
        {
            // Register with ResourceManager
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.RegisterResource(this);
            }
            else
            {
                Debug.LogWarning($"[LocalResource] ResourceManager not found! Resource {resourceId} cannot sync.");
            }
        }

        protected virtual void OnDestroy()
        {
            // Unregister from ResourceManager
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.UnregisterResource(resourceId);
            }
        }

        #endregion

        #region Abstract Methods (Implemented by derived classes)

        /// <summary>
        /// Called when this resource is hit. Play visual/audio effects.
        /// </summary>
        /// <param name="hitPoint">World position of the hit</param>
        /// <param name="healthPercent">Current health percentage (0-1)</param>
        public abstract void OnHit(Vector3 hitPoint, float healthPercent);

        /// <summary>
        /// Called when this resource is destroyed. Play destruction effects.
        /// </summary>
        /// <param name="fallDirection">Direction the resource should fall (for trees)</param>
        public abstract void OnDestroyed(Vector3 fallDirection);

        /// <summary>
        /// Called when this resource is reset/respawned.
        /// </summary>
        public abstract void OnReset();

        #endregion

        #region Public API

        /// <summary>
        /// Get the drop configuration for this resource.
        /// </summary>
        public ResourceDropConfig GetDropConfig() => dropConfig;

        /// <summary>
        /// Request drops to be spawned at a specific position.
        /// Used by resources with DelaysDropSpawning=true (like trees after they fall).
        /// </summary>
        protected void RequestDropsAtPosition(Vector3 position)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.SpawnDropsAtPosition(this, position);
            }
        }

        /// <summary>
        /// Request drops to be distributed across multiple spawn positions.
        /// Drops are round-robin assigned to the provided positions.
        /// Used by trees with configured spawn points along the trunk.
        /// </summary>
        protected void RequestDropsAtPositions(List<Vector3> positions)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.SpawnDropsAtPositions(this, positions);
            }
        }

        #endregion
    }
}