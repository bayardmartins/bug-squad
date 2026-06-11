using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Damage delivery mode.
    /// </summary>
    public enum DamageMode
    {
        Instant,   // Deal damage once on contact
        OverTime   // Deal damage repeatedly while inside trigger
    }

    /// <summary>
    /// Generic damage source that can be attached to any GameObject.
    /// Deals damage to IDamageable targets on contact (collision or trigger).
    /// 
    /// Usage:
    /// - Attach to fire pits, spike traps, lava floors, falling rocks, etc.
    /// - Configure damage, type, and mode in the Inspector.
    /// - Requires a Collider (set to Trigger for OverTime mode).
    /// - Damage is applied on the server only.
    /// </summary>
    public class DamageSource : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("Damage dealt per hit (Instant) or per tick (OverTime)")]
        [SerializeField] private float damage = 10f;

        [Tooltip("Type of damage dealt")]
        [SerializeField] private DamageType damageType = DamageType.Physical;

        [Header("Mode")]
        [Tooltip("Instant: damage once on contact. OverTime: damage repeatedly while inside")]
        [SerializeField] private DamageMode mode = DamageMode.Instant;

        [Tooltip("Seconds between damage ticks (OverTime mode only)")]
        [SerializeField] private float tickInterval = 1f;

        [Header("Detection")]
        [Tooltip("Layers that can receive damage")]
        [SerializeField] private LayerMask hitLayers = -1;

        // Track targets inside trigger for OverTime mode
        private Dictionary<IDamageable, float> targetsInside = new Dictionary<IDamageable, float>();
        private HashSet<IDamageable> instantHitTargets = new HashSet<IDamageable>();

        private bool IsServer
        {
            get
            {
                if (NetworkManager.Singleton == null) return true; // Offline / no network
                return NetworkManager.Singleton.IsServer;
            }
        }

        #region Collision Callbacks

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;
            if (!IsInHitLayers(collision.gameObject)) return;

            var damageable = collision.gameObject.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            if (mode == DamageMode.Instant)
            {
                Vector3 hitPoint = collision.contacts.Length > 0
                    ? collision.contacts[0].point
                    : collision.transform.position;
                Vector3 hitDir = (collision.transform.position - transform.position).normalized;

                ApplyDamage(damageable, hitPoint, hitDir);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (!IsInHitLayers(other.gameObject)) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            if (mode == DamageMode.Instant)
            {
                // Prevent double-hits from multiple colliders on the same target
                if (instantHitTargets.Contains(damageable)) return;
                instantHitTargets.Add(damageable);

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                ApplyDamage(damageable, hitPoint, hitDir);
            }
            else if (mode == DamageMode.OverTime)
            {
                // Start tracking for tick damage
                if (!targetsInside.ContainsKey(damageable))
                {
                    targetsInside[damageable] = 0f; // Apply first tick immediately
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                targetsInside.Remove(damageable);
                instantHitTargets.Remove(damageable);
            }
        }

        #endregion

        #region Update (OverTime ticking)

        private void Update()
        {
            if (!IsServer) return;
            if (mode != DamageMode.OverTime) return;
            if (targetsInside.Count == 0) return;

            // Iterate and tick damage
            List<IDamageable> toRemove = null;

            // Can't modify dictionary during iteration, use a temp list
            var keys = new List<IDamageable>(targetsInside.Keys);
            foreach (var target in keys)
            {
                if (target == null || !target.IsAlive)
                {
                    if (toRemove == null) toRemove = new List<IDamageable>();
                    toRemove.Add(target);
                    continue;
                }

                float elapsed = targetsInside[target] + Time.deltaTime;
                if (elapsed >= tickInterval)
                {
                    // Get position from the MonoBehaviour/Component
                    Vector3 hitPoint = transform.position;
                    Vector3 hitDir = Vector3.zero;
                    if (target is Component comp)
                    {
                        hitPoint = comp.transform.position;
                        hitDir = (comp.transform.position - transform.position).normalized;
                    }

                    ApplyDamage(target, hitPoint, hitDir);
                    elapsed -= tickInterval;
                }

                targetsInside[target] = elapsed;
            }

            // Clean up dead/null targets
            if (toRemove != null)
            {
                foreach (var t in toRemove)
                    targetsInside.Remove(t);
            }
        }

        #endregion

        #region Damage Application

        private void ApplyDamage(IDamageable target, Vector3 hitPoint, Vector3 hitDirection)
        {
            DamageInfo damageInfo = new DamageInfo(
                damage,
                damageType,
                hitPoint,
                0 // No specific attacker (environment source)
            ).WithDirection(hitDirection);

            target.TakeDamage(damageInfo);
        }

        #endregion

        #region Helpers

        private bool IsInHitLayers(GameObject obj)
        {
            return (hitLayers.value & (1 << obj.layer)) != 0;
        }

        private void OnDisable()
        {
            targetsInside.Clear();
            instantHitTargets.Clear();
        }

        #endregion
    }
}