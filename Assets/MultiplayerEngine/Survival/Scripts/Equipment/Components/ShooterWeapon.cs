using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Attached to weapon visual prefabs to configure shooting.
    /// This is the single source of truth for all per-weapon shooting settings:
    /// aim range, projectile, hit effects, sounds, and muzzle flash.
    /// Handled by ShooterWeaponHandler to spawn projectiles and play effects.
    /// </summary>
    public class ShooterWeapon : MonoBehaviour
    {
        [Header("Transforms (Required for IK/Aiming)")]
        [Tooltip("The transform where the secondary hand (support hand) should grip the weapon.")]
        public Transform secondaryHandGrip;

        [Tooltip("The barrel tip / muzzle transform. Used for IK aim direction and as the projectile spawn point.")]
        public Transform aimReference;

        [Header("Aim Range")]
        [Tooltip("Layers that can be hit by the aim raycast for this weapon.")]
        public LayerMask aimLayer = ~0;

        [Tooltip("Minimum distance for the gun to aim at the actual hit point. " +
                 "Hits closer than this use the default aim distance instead.")]
        public float minAimRange = 2f;

        [Tooltip("Maximum distance for the gun to aim at the actual hit point. " +
                 "Hits beyond this use the default aim distance instead.")]
        public float maxAimRange = 50f;

        [Tooltip("Default aim distance from camera when no valid target is in range. " +
                 "The gun looks at this point on the camera ray when no valid target exists.")]
        public float defaultAimDistance = 50f;

        [Header("Projectile")]
        [Tooltip("The projectile prefab to spawn (must have Projectile component and be a registered NetworkPrefab).")]
        public GameObject projectilePrefab;

        [Tooltip("Speed at which the projectile travels.")]
        public float projectileSpeed = 40f;

        [Tooltip("Maximum range for the aim raycast / projectile travel.")]
        public float maxRange = 100f;

        [Header("Hit Effects")]
        [Tooltip("Hit particle prefab spawned on projectile impact. " +
                 "Overrides the projectile's default hit effect if set.")]
        public GameObject hitEffectPrefab;

        [Tooltip("Hit sound played on projectile impact. " +
                 "Overrides the projectile's default hit sound if set.")]
        public AudioClip hitSound;

        [Header("Muzzle Flash")]
        [Tooltip("Particle system on the weapon that plays when firing.")]
        public ParticleSystem muzzleFlash;

        [Header("Sounds")]
        [Tooltip("Sound played on fire.")]
        public AudioClip fireSound;

        [Tooltip("Sound played during reload.")]
        public AudioClip reloadSound;

        [Tooltip("Sound played when trying to fire with empty magazine.")]
        public AudioClip emptySound;

        
        /// <summary>
        /// Gets the shoot/spawn point (aimReference, falls back to this transform).
        /// </summary>
        public Transform GetShootPoint()
        {
            return aimReference != null ? aimReference : transform;
        }
    }
}