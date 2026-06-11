using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Provides the world-space aim target position via camera raycast.
    /// Used by ShooterIKController to align the weapon arm to the aim point.
    /// 
    /// Raycast from camera center forward, filtering self-hits and ignored tags.
    /// 
    /// Range-based aim behaviour:
    ///   - If the hit is within [minAimRange, maxAimRange], the gun aims directly at the hit point.
    ///   - If the hit is closer than minAimRange or farther than maxAimRange (or nothing is hit),
    ///     the gun aims at a default point at defaultAimDistance from the camera.
    ///   - Smooth blending at the range boundaries prevents snapping.
    /// </summary>
    public class AimTargetProvider : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [Tooltip("Layers that can be hit by the aim ray")]
        public LayerMask aimLayer = ~0;

        [Tooltip("Maximum raycast distance (how far the ray travels)")]
        public float maxDistance = 100f;

        [Tooltip("Tags to ignore (e.g., \"Player\")")]
        public List<string> ignoreTags = new List<string>();

        [Header("Aim Range")]
        [Tooltip("Minimum distance for the gun to aim at the actual hit point. " +
                 "Hits closer than this use the default aim distance instead.")]
        public float minAimRange = 2f;

        [Tooltip("Maximum distance for the gun to aim at the actual hit point. " +
                 "Hits beyond this use the default aim distance instead.")]
        public float maxAimRange = 50f;

        [Tooltip("Default aim distance from camera when nothing is in range. " +
                 "The gun looks at this point on the camera ray when no valid target exists.")]
        public float defaultAimDistance = 50f;

        [Tooltip("Size of the blend zone at range boundaries (in metres). " +
                 "The aim point smoothly transitions between the hit point and default point within this zone.")]
        public float rangeBlendZone = 2f;

        [Tooltip("Smoothing speed for aim position changes (higher = snappier)")]
        public float aimSmoothing = 15f;

        /// <summary>The current aim target world position.</summary>
        public Vector3 AimPosition { get; private set; }

        /// <summary>Whether the aim ray hit something (regardless of range).</summary>
        public bool HasHit { get; private set; }

        /// <summary>Whether the current aim is locked onto an actual hit (within range).</summary>
        public bool IsTargetInRange { get; private set; }

        /// <summary>The RaycastHit data from the last frame.</summary>
        public RaycastHit LastHit { get; private set; }

        private Camera mainCamera;
        private Vector3 smoothedAimPosition;
        private bool initialized;

        // Cached Inspector defaults (for restoring after per-weapon overrides)
        private LayerMask defaultAimLayer;
        private float defaultMinAimRange;
        private float defaultMaxAimRange;
        private float defaultDefaultAimDistance;

        private void Awake()
        {
            // Cache Inspector defaults so we can restore after per-weapon overrides
            defaultAimLayer = aimLayer;
            defaultMinAimRange = minAimRange;
            defaultMaxAimRange = maxAimRange;
            defaultDefaultAimDistance = defaultAimDistance;
        }

        private void Start()
        {
            mainCamera = Camera.main;

            // Add own tag to ignore list if not present
            if (!string.IsNullOrEmpty(gameObject.tag) && !ignoreTags.Contains(gameObject.tag))
                ignoreTags.Add(gameObject.tag);
        }

        /// <summary>
        /// Applies per-weapon aim settings. Called by ShooterWeaponHandler on equip.
        /// </summary>
        public void ApplyWeaponSettings(LayerMask layer, float min, float max, float defaultDist)
        {
            aimLayer = layer;
            minAimRange = min;
            maxAimRange = max;
            defaultAimDistance = defaultDist;
        }

        /// <summary>
        /// Restores the default aim settings from Inspector values. Called on unequip.
        /// </summary>
        public void ResetToDefaults()
        {
            aimLayer = defaultAimLayer;
            minAimRange = defaultMinAimRange;
            maxAimRange = defaultMaxAimRange;
            defaultAimDistance = defaultDefaultAimDistance;
        }

        private void LateUpdate()
        {
            UpdateAimPosition();
        }

        /// <summary>
        /// Performs the aim raycast and updates AimPosition with range-based logic.
        /// </summary>
        private void UpdateAimPosition()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // Ray from camera center
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // Default aim point (used when nothing is in range)
            Vector3 defaultPoint = ray.origin + ray.direction * defaultAimDistance;
            Vector3 targetAimPosition = defaultPoint;
            bool hitInRange = false;

            // SphereCast for more forgiving hit detection
            RaycastHit hit;
            if (Physics.SphereCast(ray, 0.01f, out hit, maxDistance, aimLayer))
            {
                if (!ShouldIgnoreHit(hit))
                {
                    HasHit = true;
                    LastHit = hit;

                    float hitDistance = hit.distance;

                    // Calculate blend factor based on where the hit falls relative to the range
                    float blend = GetRangeBlendFactor(hitDistance);

                    if (blend >= 1f)
                    {
                        // Fully inside the valid range — aim directly at hit
                        targetAimPosition = hit.point;
                        hitInRange = true;
                    }
                    else if (blend > 0f)
                    {
                        // In the blend zone — interpolate between hit point and default point
                        targetAimPosition = Vector3.Lerp(defaultPoint, hit.point, blend);
                        hitInRange = true;
                    }
                    // else blend == 0: fully outside range, use defaultPoint (already set)
                }
                else
                {
                    HasHit = false;
                }
            }
            else
            {
                HasHit = false;
            }

            IsTargetInRange = hitInRange;

            // Smooth the aim position to prevent jitter/snapping
            if (!initialized)
            {
                smoothedAimPosition = targetAimPosition;
                initialized = true;
            }
            else
            {
                smoothedAimPosition = Vector3.Lerp(
                    smoothedAimPosition, targetAimPosition,
                    aimSmoothing * Time.deltaTime
                );
            }

            AimPosition = smoothedAimPosition;
        }

        /// <summary>
        /// Returns a 0–1 blend factor for the given hit distance.
        ///   0 = fully outside range (use default point)
        ///   1 = fully inside range (use hit point)
        ///   In-between = blend zone at the boundaries
        /// </summary>
        private float GetRangeBlendFactor(float distance)
        {
            float halfBlend = rangeBlendZone * 0.5f;

            // Too close: blend out near minAimRange
            if (distance < minAimRange - halfBlend)
                return 0f;
            if (distance < minAimRange + halfBlend)
                return Mathf.InverseLerp(minAimRange - halfBlend, minAimRange + halfBlend, distance);

            // Too far: blend out near maxAimRange
            if (distance > maxAimRange + halfBlend)
                return 0f;
            if (distance > maxAimRange - halfBlend)
                return Mathf.InverseLerp(maxAimRange + halfBlend, maxAimRange - halfBlend, distance);

            // Fully inside range
            return 1f;
        }

        /// <summary>
        /// Checks if a raycast hit should be ignored based on tags.
        /// </summary>
        private bool ShouldIgnoreHit(RaycastHit hit)
        {
            if (hit.collider == null) return true;

            // Ignore hits on our own character hierarchy to prevent self-hits
            if (hit.collider.transform.root == transform.root) return true;

            string hitTag = hit.collider.gameObject.tag;
            for (int i = 0; i < ignoreTags.Count; i++)
            {
                if (ignoreTags[i] == hitTag) return true;
            }
            return false;
        }
    }
}