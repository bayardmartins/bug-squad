using Unity.Netcode;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Projectile component for ranged weapons.
    /// Handles collision detection and damage application.
    /// Spawned via EquipmentController.FireProjectileRpc on the server.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : NetworkBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private LayerMask hitLayers = -1;
        
        [Header("Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private AudioClip hitSound;

        // Projectile data (set by Initialize)
        private ulong ownerId;
        private float damage;
        private DamageType damageType;
        private int weaponTier;
        private bool hasHit;

        /// <summary>
        /// Initialize the projectile with damage data and optional per-weapon hit overrides.
        /// Called by EquipmentController.FireProjectileRpc after spawning.
        /// </summary>
        /// <param name="ownerId">NetworkObject ID of the attacker</param>
        /// <param name="damage">Damage amount to deal</param>
        /// <param name="damageType">Type of damage</param>
        /// <param name="tier">Weapon tier for tier-locked resources</param>
        /// <param name="hitEffectOverride">Optional weapon-specific hit particle prefab (overrides projectile default)</param>
        /// <param name="hitSoundOverride">Optional weapon-specific hit sound (overrides projectile default)</param>
        public void Initialize(ulong ownerId, float damage, DamageType damageType, int tier,
                               GameObject hitEffectOverride = null, AudioClip hitSoundOverride = null)
        {
            this.ownerId = ownerId;
            this.damage = damage;
            this.damageType = damageType;
            this.weaponTier = tier;

            // Apply per-weapon overrides (weapon-specific effects take priority)
            if (hitEffectOverride != null) hitEffectPrefab = hitEffectOverride;
            if (hitSoundOverride != null) hitSound = hitSoundOverride;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Destroy after lifetime
            if (IsServer)
            {
                Invoke(nameof(DestroyProjectile), lifetime);
            }
        }


        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;
            Debug.Log($"[Projectile] OnCollisionEnter with: {collision.gameObject.name} on layer {LayerMask.LayerToName(collision.gameObject.layer)}");
            if (hasHit)
            {
                Debug.Log($"[Projectile] Collision ignored: projectile already hit another target.");
                return;
            }

            // Check if we hit something in hit layers
            if ((hitLayers.value & (1 << collision.gameObject.layer)) == 0)
            {
                Debug.Log($"[Projectile] Collision ignored: layer {LayerMask.LayerToName(collision.gameObject.layer)} not in hitLayers ({hitLayers.value}).");
                return;
            }

            hasHit = true;

            // Get hit point
            Vector3 hitPoint = collision.contacts.Length > 0 
                ? collision.contacts[0].point 
                : transform.position;

            Debug.Log($"[Projectile] Valid collision. Applying damage to {collision.gameObject.name} at point {hitPoint}");

            // Try to apply damage
            ApplyDamage(collision.gameObject, hitPoint);

            // Spawn hit effect
            SpawnHitEffectClientRpc(hitPoint, collision.contacts.Length > 0 
                ? collision.contacts[0].normal 
                : -transform.forward);

            // Destroy the projectile
            DestroyProjectile();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            Debug.Log($"[Projectile] OnTriggerEnter with: {other.gameObject.name} on layer {LayerMask.LayerToName(other.gameObject.layer)}");
            if (hasHit)
            {
                Debug.Log($"[Projectile] Trigger ignored: projectile already hit another target.");
                return;
            }

            // Check if we hit something in hit layers
            if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                Debug.Log($"[Projectile] Trigger ignored: layer {LayerMask.LayerToName(other.gameObject.layer)} not in hitLayers ({hitLayers.value}).");
                return;
            }

            hasHit = true;

            // Get hit point
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            Debug.Log($"[Projectile] Valid trigger. Applying damage to {other.gameObject.name} at point {hitPoint}");

            // Try to apply damage
            ApplyDamage(other.gameObject, hitPoint);

            // Spawn hit effect
            SpawnHitEffectClientRpc(hitPoint, -transform.forward);

            // Destroy the projectile
            DestroyProjectile();
        }

        private void ApplyDamage(GameObject target, Vector3 hitPoint)
        {
            // Create damage info
            DamageInfo damageInfo = new DamageInfo(
                damage,
                damageType,
                hitPoint,
                ownerId,
                weaponTier
            ).WithDirection(transform.forward);

            // Try to damage entities with IDamageable interface
            var damageable = target.GetComponentInParent<IDamageable>();
            Debug.Log($"[Projectile] ApplyDamage: Target {target.name} has IDamageable: {damageable != null}, IsAlive: {damageable?.IsAlive}");
            if (damageable != null && damageable.IsAlive)
            {
                Debug.Log($"[Projectile] Calling TakeDamage on target {target.name} for damage amount: {damage}");
                damageable.TakeDamage(damageInfo);
            }
        }

        [ClientRpc]
        private void SpawnHitEffectClientRpc(Vector3 position, Vector3 normal)
        {
            // Spawn hit particle effect
            if (hitEffectPrefab != null)
            {
                var effect = Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
                Destroy(effect, 2f);
            }

            // Play hit sound
            if (hitSound != null)
            {
                AudioPool.Play(hitSound, position);
            }
        }

        private void DestroyProjectile()
        {
            if (!IsServer) return;

            // Disable trail before despawn to avoid visual artifacts
            if (trailRenderer != null)
            {
                trailRenderer.enabled = false;
            }

            NetworkObject.Despawn(true);
        }
    }
}