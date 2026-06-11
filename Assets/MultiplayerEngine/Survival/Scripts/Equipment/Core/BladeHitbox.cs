using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Component attached to weapon/tool prefabs for blade-based hit detection.
    /// Uses Physics.CapsuleCast between two transform points (blade base to tip)
    /// for accurate hit detection along the weapon's edge.
    /// </summary>
    public class BladeHitbox : MonoBehaviour
    {
        [Header("Blade Points")]
        [Tooltip("Transform at the base of the blade (near handle)")]
        public Transform bladeBase;
        
        [Tooltip("Transform at the tip of the blade")]
        public Transform bladeTip;

        [Header("Detection Settings")]
        [Tooltip("Radius of the capsule cast")]
        public float hitRadius = 0.1f;
        
        [Tooltip("Layers to detect hits on")]
        public LayerMask hitLayers = -1;

        [Header("Debug")]
        [Tooltip("Draw gizmos to visualize blade hitbox")]
        public bool showDebugGizmos = true;

        // Callback for when a hit is detected
        public System.Action<RaycastHit> OnHitDetected;

        // Track objects hit during current swing to prevent double-hits
        private HashSet<Collider> hitThisSwing = new HashSet<Collider>();
        private bool isDetecting = false;

        /// <summary>
        /// Performs a single hit detection using capsule cast between blade base and tip.
        /// Should be called by animation event at the impact frame.
        /// </summary>
        /// <returns>All hits detected along the blade edge</returns>
        public RaycastHit[] PerformHitDetection()
        {
            if (bladeBase == null || bladeTip == null)
            {
                Debug.LogWarning($"[BladeHitbox] Missing blade points on {gameObject.name}");
                return new RaycastHit[0];
            }

            Vector3 basePos = bladeBase.position;
            Vector3 tipPos = bladeTip.position;
            Vector3 direction = (tipPos - basePos).normalized;
            float distance = Vector3.Distance(basePos, tipPos);

            // Use OverlapCapsule instead of CapsuleCast for stationary detection
            // This detects anything currently overlapping the blade
            Collider[] overlaps = Physics.OverlapCapsule(basePos, tipPos, hitRadius, hitLayers);
            
            if (overlaps.Length > 0)
            {
                Debug.Log($"[BladeHitbox] PerformHitDetection on {gameObject.name}: Overlapped {overlaps.Length} colliders in layers {hitLayers.value}");
            }
            
            List<RaycastHit> validHits = new List<RaycastHit>();
            
            foreach (var collider in overlaps)
            {
                // Skip self
                if (collider.transform.IsChildOf(transform.root))
                {
                    Debug.Log($"[BladeHitbox] Skipping collider {collider.name}: is child of root ({transform.root.name})");
                    continue;
                }
                    
                // Skip already hit this swing
                if (hitThisSwing.Contains(collider))
                {
                    Debug.Log($"[BladeHitbox] Skipping collider {collider.name}: already hit this swing");
                    continue;
                }

                // Mark as hit
                hitThisSwing.Add(collider);
                
                Debug.Log($"[BladeHitbox] Attempting to hit collider: {collider.name} on layer {LayerMask.LayerToName(collider.gameObject.layer)}");

                // Create a RaycastHit-like result by finding closest point
                Vector3 closestPoint = collider.ClosestPoint((basePos + tipPos) / 2f);
                
                // Raycast to get proper hit info
                Vector3 toCenter = collider.bounds.center - basePos;
                if (Physics.Raycast(basePos, toCenter.normalized, out RaycastHit hit, toCenter.magnitude + 1f, hitLayers))
                {
                    if (hit.collider == collider)
                    {
                        Debug.Log($"[BladeHitbox] Primary Raycast Hit successful on {collider.name} at point {hit.point}");
                        validHits.Add(hit);
                        OnHitDetected?.Invoke(hit);
                    }
                    else
                    {
                        Debug.LogWarning($"[BladeHitbox] Primary Raycast hit other collider {hit.collider.name} instead of target {collider.name}");
                    }
                }
                else
                {
                    // Fallback: Since overlap detection succeeded, we definitely hit the collider.
                    // To get a valid RaycastHit on the collider's surface without starting inside it,
                    // we cast inward from the outside towards the closest point on the collider.
                    Vector3 bladeCenter = (basePos + tipPos) / 2f;
                    Vector3 toCollider = (closestPoint - bladeCenter).normalized;
                    if (toCollider == Vector3.zero) toCollider = transform.forward;

                    Vector3 startPointOutside = closestPoint + toCollider * 0.2f;
                    Vector3 directionInward = -toCollider;

                    if (Physics.Raycast(startPointOutside, directionInward, out RaycastHit fallbackHit, 0.4f, hitLayers))
                    {
                        Debug.Log($"[BladeHitbox] Fallback Raycast Hit successful on {collider.name} at point {fallbackHit.point}");
                        validHits.Add(fallbackHit);
                        OnHitDetected?.Invoke(fallbackHit);
                    }
                    else
                    {
                        // Ultimate fallback: do a raycast from the weapon owner to the target's center
                        Vector3 ownerPos = transform.root.position;
                        Vector3 targetPos = collider.bounds.center;
                        Vector3 toTarget = (targetPos - ownerPos).normalized;
                        if (Physics.Raycast(ownerPos, toTarget, out RaycastHit ultimateHit, Vector3.Distance(ownerPos, targetPos) + 1f, hitLayers))
                        {
                            Debug.Log($"[BladeHitbox] Ultimate Fallback Raycast Hit successful on {collider.name} at point {ultimateHit.point}");
                            validHits.Add(ultimateHit);
                            OnHitDetected?.Invoke(ultimateHit);
                        }
                        else
                        {
                            Debug.LogWarning($"[BladeHitbox] Overlap succeeded on {collider.name} but all raycast fallbacks failed!");
                        }
                    }
                }
            }

            return validHits.ToArray();
        }

        /// <summary>
        /// Clears the hit tracking. Call at the start of each swing.
        /// </summary>
        public void ResetHitTracking()
        {
            hitThisSwing.Clear();
        }

        /// <summary>
        /// Enables continuous hit detection (for weapons that sweep).
        /// </summary>
        public void StartContinuousDetection()
        {
            isDetecting = true;
            ResetHitTracking();
        }

        /// <summary>
        /// Disables continuous hit detection.
        /// </summary>
        public void StopContinuousDetection()
        {
            isDetecting = false;
        }

        private void Update()
        {
            if (isDetecting)
            {
                PerformHitDetection();
            }
        }

        /// <summary>
        /// Gets the length of the blade (distance from base to tip).
        /// </summary>
        public float GetBladeLength()
        {
            if (bladeBase == null || bladeTip == null)
                return 0f;
            return Vector3.Distance(bladeBase.position, bladeTip.position);
        }

        /// <summary>
        /// Gets the center point of the blade.
        /// </summary>
        public Vector3 GetBladeCenter()
        {
            if (bladeBase == null || bladeTip == null)
                return transform.position;
            return (bladeBase.position + bladeTip.position) / 2f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || bladeBase == null || bladeTip == null)
                return;

            // Draw blade line
            Gizmos.color = isDetecting ? Color.red : Color.cyan;
            Gizmos.DrawLine(bladeBase.position, bladeTip.position);

            // Draw spheres at base and tip
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bladeBase.position, hitRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(bladeTip.position, hitRadius);

            // Draw capsule approximation
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            DrawWireCapsule(bladeBase.position, bladeTip.position, hitRadius);
        }

        private void DrawWireCapsule(Vector3 point1, Vector3 point2, float radius)
        {
            // Simplified capsule visualization
            Vector3 direction = (point2 - point1).normalized;
            Vector3 up = Vector3.Cross(direction, Vector3.right);
            if (up.magnitude < 0.01f)
                up = Vector3.Cross(direction, Vector3.forward);
            up.Normalize();
            Vector3 right = Vector3.Cross(direction, up);

            // Draw circles at each end
            DrawWireCircle(point1, direction, radius);
            DrawWireCircle(point2, direction, radius);

            // Draw connecting lines
            Gizmos.DrawLine(point1 + up * radius, point2 + up * radius);
            Gizmos.DrawLine(point1 - up * radius, point2 - up * radius);
            Gizmos.DrawLine(point1 + right * radius, point2 + right * radius);
            Gizmos.DrawLine(point1 - right * radius, point2 - right * radius);
        }

        private void DrawWireCircle(Vector3 center, Vector3 normal, float radius)
        {
            Vector3 from = Vector3.Cross(normal, Vector3.right);
            if (from.magnitude < 0.01f)
                from = Vector3.Cross(normal, Vector3.forward);
            from = from.normalized * radius;

            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (i / (float)segments) * Mathf.PI * 2f;
                float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector3 p1 = center + Quaternion.AngleAxis(angle1 * Mathf.Rad2Deg, normal) * from;
                Vector3 p2 = center + Quaternion.AngleAxis(angle2 * Mathf.Rad2Deg, normal) * from;
                Gizmos.DrawLine(p1, p2);
            }
        }
#endif
    }
}