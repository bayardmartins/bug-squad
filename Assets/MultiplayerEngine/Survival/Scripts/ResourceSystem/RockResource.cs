using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Rock-specific resource implementation.
    /// Handles rock-specific effects: hit debris and destruction particles.
    /// Drops are handled by ResourceManager using the inherited dropConfig.
    /// </summary>
    public class RockResource : LocalResource
    {
        // RockResource is always HarvestableType.Rock
        public override HarvestableType ResourceType => HarvestableType.Rock;

        [Header("Hit Effects")]
        [SerializeField] private ParticleSystem hitEffectPrefab;
        [SerializeField] private AudioClip hitSound;

        [Header("Destruction Effects")]
        [SerializeField] private ParticleSystem destroyEffect;
        [SerializeField] private AudioClip destroySound;

        // State
        private Vector3 originalPosition;
        private Quaternion originalRotation;

        protected override void Awake()
        {
            base.Awake();

            // Store original transform
            originalPosition = transform.position;
            originalRotation = transform.rotation;
        }

        #region LocalResource Implementation

        public override void OnHit(Vector3 hitPoint, float healthPercent)
        {
            if (!isAlive) return;

            // Update local health tracking
            localHealthPercent = healthPercent;

            // Calculate hit normal locally using a local raycast against this resource's colliders
            Vector3 hitNormal = Vector3.up;
            Collider[] colliders = GetComponentsInChildren<Collider>();
            bool hitFound = false;
            Transform hitTransform = transform; // default fallback

            foreach (var col in colliders)
            {
                if (col.enabled && col.gameObject.activeInHierarchy)
                {
                    Vector3 center = col.bounds.center;
                    Vector3 dir = (hitPoint - center).normalized;
                    if (dir == Vector3.zero) dir = Vector3.forward;

                    // Start raycast slightly outside the collider
                    Vector3 rayStart = hitPoint + dir * 0.5f;
                    Ray ray = new Ray(rayStart, -dir);

                    if (col.Raycast(ray, out RaycastHit hit, 1.0f))
                    {
                        hitNormal = hit.normal;
                        hitFound = true;
                        hitTransform = col.transform;
                        break;
                    }
                }
            }

            if (!hitFound)
            {
                // Fallback to outward direction from first active collider, or transform position if none found
                if (colliders.Length > 0 && colliders[0] != null)
                {
                    hitNormal = (hitPoint - colliders[0].bounds.center).normalized;
                    hitTransform = colliders[0].transform;
                }
                else
                {
                    hitNormal = (hitPoint - transform.position).normalized;
                }
                hitNormal.y = 0; // keep it horizontal
                if (hitNormal == Vector3.zero) hitNormal = Vector3.forward;
            }

            // Spawn hit effect (small debris/drizzle)
            if (hitEffectPrefab != null)
            {
                var effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
                
                // Reparent decal child to the hit transform so it's destroyed when the resource disappears
                foreach (Transform child in effect.transform)
                {
                    if (child.name.Contains("Decal"))
                    {
                        child.SetParent(hitTransform, true);
                    }
                }
                
                Destroy(effect.gameObject, 2f);
            }

            // Play hit sound
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, hitPoint);
            }
        }

        public override void OnDestroyed(Vector3 fallDirection)
        {
            if (!isAlive) return;

            isAlive = false;
            localHealthPercent = 0f;

            // Spawn destruction effect
            if (destroyEffect != null)
            {
                destroyEffect.transform.parent = null;
                destroyEffect.Play();
                Destroy(destroyEffect.gameObject, destroyEffect.main.duration + 0.5f);
            }

            // Play destroy sound
            if (destroySound != null)
            {
                AudioSource.PlayClipAtPoint(destroySound, transform.position);
            }

            // NOTE: Drops are spawned by ResourceManager using dropConfig
            // No need to spawn locally - it's handled server-side for networking

            // Hide the rock
            gameObject.SetActive(false);

            // Destroy after a short delay
            Destroy(gameObject, 1f);
        }

        public override void OnReset()
        {
            // Reset state
            isAlive = true;
            localHealthPercent = 1f;

            // Reset transform
            transform.position = originalPosition;
            transform.rotation = originalRotation;

            // Ensure game object is visible
            gameObject.SetActive(true);
        }

        #endregion
    }
}