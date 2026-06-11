using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Attached to bow weapon visual prefabs to configure bow-specific transforms,
    /// projectile, and effects. Handled by BowWeaponHandler.
    /// </summary>
    public class BowWeapon : MonoBehaviour
    {
        [Header("Transforms (Required for IK/Aiming)")]
        [Tooltip("The transform where the secondary hand (support hand) grips the bow body.")]
        public Transform secondaryHandGrip;

        [Tooltip("The aim reference transform (arrow tip direction when nocked). Used for IK aim alignment.")]
        public Transform aimReference;

        [Header("Bow String")]
        [Tooltip("The point on the string where the arrow nocks and the draw hand grips. Moves backwards during draw.")]
        public Transform stringNockPoint;

        [Tooltip("Local position of stringNockPoint at rest (auto-captured on Awake if zero).")]
        public Vector3 stringRestLocalPosition;

        [Header("Arrow")]
        [Tooltip("Visual-only arrow prefab (shown in hand/on string, destroyed on fire). Not networked.")]
        public GameObject localArrowPrefab;

        [Tooltip("Local position offset for the arrow when held in the drawing hand before nocking.")]
        public Vector3 arrowHandPositionOffset;

        [Tooltip("Local rotation offset for the arrow when held in the drawing hand before nocking.")]
        public Vector3 arrowHandRotationOffset;



        [Header("Projectile")]
        [Tooltip("The networked projectile prefab spawned on fire (must have Projectile + NetworkObject components).")]
        public GameObject projectilePrefab;

        [Header("Aim Range")]
        [Tooltip("Layers that can be hit by the aim raycast for this weapon.")]
        public LayerMask aimLayer = ~0;

        [Tooltip("Minimum distance for aiming at the actual hit point.")]
        public float minAimRange = 2f;

        [Tooltip("Maximum distance for aiming at the actual hit point.")]
        public float maxAimRange = 50f;

        [Tooltip("Default aim distance from camera when no valid target is in range.")]
        public float defaultAimDistance = 50f;

        [Header("Hit Effects")]
        [Tooltip("Hit particle prefab spawned on projectile impact.")]
        public GameObject hitEffectPrefab;

        [Tooltip("Hit sound played on projectile impact.")]
        public AudioClip hitSound;

        [Header("Sounds")]
        [Tooltip("Sound played when drawing the string.")]
        public AudioClip drawSound;

        [Tooltip("Sound played when releasing/firing.")]
        public AudioClip releaseSound;

        [Tooltip("Sound played when trying to fire with no arrows.")]
        public AudioClip emptySound;

        private void Awake()
        {
            // Auto-capture rest position if not set
            if (stringNockPoint != null && stringRestLocalPosition == Vector3.zero)
            {
                stringRestLocalPosition = stringNockPoint.localPosition;
            }
        }

        /// <summary>
        /// Sets the string draw amount. t=0 is rest, t=1 is full draw.
        /// Uses aimReference to dynamically calculate pull direction based on drawDistance.
        /// </summary>
        public void SetStringDraw(float t, float drawDistance)
        {
            if (stringNockPoint == null) return;

            // Dynamically calculate the pull direction based on aimReference
            // This ensures the string always pulls perfectly backward regardless of bone orientation.
            Transform refTransform = aimReference != null ? aimReference : transform;
            
            if (stringNockPoint.parent != null)
            {
                // Multiply the backward direction by the desired draw distance
                Vector3 pullDeltaWorld = -refTransform.forward * drawDistance;
                // Convert the world delta into local space, correctly accounting for parent scale!
                Vector3 pullDeltaLocal = stringNockPoint.parent.InverseTransformVector(pullDeltaWorld);
                stringNockPoint.localPosition = stringRestLocalPosition + (pullDeltaLocal * Mathf.Clamp01(t));
            }
        }

        /// <summary>
        /// Resets string to rest position.
        /// </summary>
        public void ResetString()
        {
            if (stringNockPoint == null) return;
            stringNockPoint.localPosition = stringRestLocalPosition;
        }

        /// <summary>
        /// Gets the shoot/spawn point (aimReference, falls back to this transform).
        /// </summary>
        public Transform GetShootPoint()
        {
            return aimReference != null ? aimReference : transform;
        }
    }
}