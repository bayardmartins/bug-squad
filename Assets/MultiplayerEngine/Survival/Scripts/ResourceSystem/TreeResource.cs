using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Tree-specific resource implementation with 3-model slot system.
    /// Slots: Full Tree (shown when alive), Upper Part (falls when cut), Stump (shown when cut).
    /// Uses velocity-based ground detection (no collisions) - when velocity drops below threshold, tree has settled.
    /// </summary>
    public class TreeResource : LocalResource
    {
        // TreeResource is always HarvestableType.Tree
        public override HarvestableType ResourceType => HarvestableType.Tree;

        // Trees delay drop spawning until the upper part falls and settles
        public override bool DelaysDropSpawning => true;

        [Header("Tree Model Slots")]
        [Tooltip("The full tree model shown when tree is alive")]
        [SerializeField] private GameObject fullTreeSlot;
        [Tooltip("The upper part of tree that falls when cut - should have Rigidbody attached")]
        [SerializeField] private GameObject upperPartSlot;
        [Tooltip("The stump that appears when tree is cut")]
        [SerializeField] private GameObject stumpSlot;

        [Header("Hit Effects")]
        [Tooltip("ParticleSystem already attached to tree - moves to hit point and plays")]
        [SerializeField] private ParticleSystem hitVFX;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private float shakeIntensity = 0.1f;
        [SerializeField] private float shakeDuration = 0.15f;

        [Header("Fall Settings")]
        [Tooltip("Initial push force applied to upper part when tree is cut")]
        [SerializeField] private float fallPushForce = 3f;
        [Tooltip("Local offset from upper part pivot where force is applied")]
        [SerializeField] private Vector3 forcePointOffset = new Vector3(0f, 3f, 0f);
        [Tooltip("Direction the tree will fall (in local space). Set to (0,0,0) to use incoming hit direction.")]
        [SerializeField] private Vector3 fallDirection = new Vector3(1f, 0f, 0f);
        [SerializeField] private AudioClip fallSound;
        [Tooltip("ParticleSystem already attached to tree - enabled and played when tree falls")]
        [SerializeField] private ParticleSystem fallImpactVFX;

        [Header("Gizmo Settings")]
        [SerializeField] private bool showForceGizmo = true;
        [SerializeField] private float gizmoSphereRadius = 0.2f;
        [SerializeField] private float gizmoArrowLength = 2f;

        [Header("Ground Detection")]
        [Tooltip("Deceleration magnitude (m/s²) that counts as a ground hit")]
        [SerializeField] private float impactDecelerationThreshold = 15f;

        [Header("Drop Spawn Points")]
        [Tooltip("Transforms where resource drops will spawn when tree falls. Place these as children of the Upper Part so they move with the falling tree. Drops are distributed evenly across these points. If empty, spawns at upper part center.")]
        [SerializeField] private List<Transform> dropSpawnPoints = new List<Transform>();

        [Header("After Fall")]
        [Tooltip("Delay after tree settles before spawning wood and destroying upper part")]
        [SerializeField] private float destroyDelay = 1.5f;
        [SerializeField] private float stumpLifetime = 30f;

        // State
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float shakeTimer;
        private bool isFalling;
        private bool hasSettled;
        private bool isCheckingVelocity;
        private int groundHitCount;         // 1st hit = impact VFX, 2nd hit = settled
        private Vector3 previousVelocity;
        private Rigidbody upperPartRb;
        private AudioSource audioSource;

        protected override void Awake()
        {
            base.Awake();

            // Store original transform
            originalPosition = transform.position;
            originalRotation = transform.rotation;

            // Initial state: show full tree, hide stump and upper part
            SetModelState(showFullTree: true, showStump: false, showUpperPart: false);

            // Cache the AudioSource on this tree
            audioSource = GetComponentInChildren<AudioSource>();

            // Ensure VFX are disabled at start
            if (hitVFX != null) hitVFX.gameObject.SetActive(false);
            if (fallImpactVFX != null) fallImpactVFX.gameObject.SetActive(false);

            // Cache the rigidbody from upper part (should already be attached)
            if (upperPartSlot != null)
            {
                upperPartRb = upperPartSlot.GetComponent<Rigidbody>();
                if (upperPartRb != null)
                {
                    // Ensure rigidbody is kinematic initially (no physics until cut)
                    upperPartRb.isKinematic = true;
                }
            }
        }

        private void Update()
        {
            // Handle shake effect
            if (shakeTimer > 0 && !isFalling)
            {
                shakeTimer -= Time.deltaTime;
                if (shakeTimer <= 0)
                {
                    transform.position = originalPosition;
                }
                else
                {
                    Vector3 shakeOffset = Random.insideUnitSphere * shakeIntensity;
                    shakeOffset.y = 0;
                    transform.position = originalPosition + shakeOffset;
                }
            }

            // Ground hit detection via deceleration
            // 1st ground hit = play impact VFX, 2nd ground hit = tree settled
            if (isCheckingVelocity && upperPartRb != null && !hasSettled)
            {
                Vector3 velocity = upperPartRb.linearVelocity;
                float dt = Time.deltaTime;

                if (dt > 0f && previousVelocity.sqrMagnitude > 0.001f)
                {
                    // Detect ground hit: deceleration spike or vertical bounce
                    Vector3 acceleration = (velocity - previousVelocity) / dt;
                    bool decelerationHit = acceleration.magnitude > impactDecelerationThreshold 
                        && velocity.magnitude < previousVelocity.magnitude;
                    bool verticalBounce = previousVelocity.y < -1f && velocity.y >= 0f;

                    if (decelerationHit || verticalBounce)
                    {
                        groundHitCount++;

                        if (groundHitCount == 1)
                        {
                            // First hit: play impact VFX
                            PlayFallImpactVFX();
                        }
                        else if (groundHitCount >= 2)
                        {
                            // Second hit: tree has bounced and landed again — settled
                            OnTreeSettled();
                        }
                    }
                }

                previousVelocity = velocity;
            }
        }

        #region Model State Management

        /// <summary>
        /// Controls visibility of the 3 model slots
        /// </summary>
        private void SetModelState(bool showFullTree, bool showStump, bool showUpperPart)
        {
            if (fullTreeSlot != null)
                fullTreeSlot.SetActive(showFullTree);

            if (stumpSlot != null)
                stumpSlot.SetActive(showStump);

            if (upperPartSlot != null)
                upperPartSlot.SetActive(showUpperPart);
        }

        #endregion

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

            // Spawn hit VFX (instantiate a world-space copy to avoid parent scale/shake issues)
            if (hitVFX != null)
            {
                var effect = Instantiate(hitVFX, hitPoint, Quaternion.LookRotation(hitNormal));
                
                // Reparent decal child to the hit transform so it's destroyed when the resource disappears
                foreach (Transform child in effect.transform)
                {
                    if (child.name.Contains("Decal"))
                    {
                        child.SetParent(hitTransform, true);
                    }
                }
                
                effect.gameObject.SetActive(true);
                effect.Play();
                Destroy(effect.gameObject, effect.main.duration + 0.5f);
            }

            // Play hit sound via attached AudioSource
            if (hitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hitSound);
            }

            // Start shake
            shakeTimer = shakeDuration;
            originalPosition = transform.position;
        }

        public override void OnDestroyed(Vector3 fallDirection)
        {
            if (!isAlive || isFalling) return;

            isAlive = false;
            isFalling = true;
            hasSettled = false;
            groundHitCount = 0;
            isCheckingVelocity = false;
            localHealthPercent = 0f;
            previousVelocity = Vector3.zero;

            // Instantly swap models: hide full tree, show stump + upper part
            SetModelState(showFullTree: false, showStump: true, showUpperPart: true);

            // Start the fall physics on upper part
            StartFall(fallDirection);
        }

        public override void OnReset()
        {
            // Reset state
            isAlive = true;
            isFalling = false;
            hasSettled = false;
            groundHitCount = 0;
            isCheckingVelocity = false;
            localHealthPercent = 1f;
            previousVelocity = Vector3.zero;

            // Reset transform
            transform.position = originalPosition;
            transform.rotation = originalRotation;

            // Reset upper part if exists
            if (upperPartSlot != null)
            {
                upperPartSlot.transform.localPosition = Vector3.zero;
                upperPartSlot.transform.localRotation = Quaternion.identity;

                if (upperPartRb != null)
                {
                    upperPartRb.isKinematic = true;
                    upperPartRb.linearVelocity = Vector3.zero;
                    upperPartRb.angularVelocity = Vector3.zero;
                }
            }

            // Reset model states: show full tree, hide stump and upper part
            SetModelState(showFullTree: true, showStump: false, showUpperPart: false);

            // Ensure game object is visible
            gameObject.SetActive(true);
        }

        #endregion

        #region Fall Physics

        private void StartFall(Vector3 incomingFallDirection)
        {
            if (upperPartRb == null)
            {
                Debug.LogWarning($"[TreeResource] Upper part slot has no Rigidbody! Cannot fall. Tree: {gameObject.name}");
                return;
            }

            // Enable physics on upper part
            upperPartRb.isKinematic = false;

            // Determine fall direction: use configured direction if set, otherwise use incoming direction
            Vector3 actualFallDir;
            if (fallDirection.sqrMagnitude > 0.01f)
            {
                // Use configured local fall direction, convert to world space
                actualFallDir = transform.TransformDirection(fallDirection.normalized);
            }
            else
            {
                // Use incoming direction from hit
                actualFallDir = incomingFallDirection.normalized;
            }

            // Calculate world position where force is applied
            Vector3 forcePoint = upperPartSlot != null 
                ? upperPartSlot.transform.TransformPoint(forcePointOffset)
                : transform.TransformPoint(forcePointOffset);

            // Apply force at the specified point to create natural tipping
            // Using AddForceAtPosition creates both linear and angular momentum
            Vector3 forceVector = actualFallDir * fallPushForce;
            upperPartRb.AddForceAtPosition(forceVector, forcePoint, ForceMode.Impulse);

            // Play fall sound via attached AudioSource
            if (fallSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(fallSound);
            }

            // Start ground hit detection immediately
            isCheckingVelocity = true;
            previousVelocity = Vector3.zero;
            groundHitCount = 0;
        }

        /// <summary>
        /// Plays fall impact VFX at the first ground contact.
        /// Called on the 1st detected ground hit.
        /// </summary>
        private void PlayFallImpactVFX()
        {
            // Enable and play fall impact VFX at upper part's current position
            if (fallImpactVFX != null && upperPartSlot != null)
            {
                fallImpactVFX.transform.parent = null;
                Destroy(fallImpactVFX.gameObject, fallImpactVFX.main.duration + 0.5f);
                fallImpactVFX.gameObject.SetActive(true);
                fallImpactVFX.Play();
            }
        }

        /// <summary>
        /// Called on the 2nd ground hit — tree has bounced and landed.
        /// Spawns drops and starts cleanup.
        /// </summary>
        private void OnTreeSettled()
        {
            if (hasSettled) return;

            hasSettled = true;
            isCheckingVelocity = false;

            // After delay, destroy upper part and spawn wood
            Invoke(nameof(FinalizeTreeFall), destroyDelay);
        }

        private void FinalizeTreeFall()
        {
            // Spawn drops distributed across spawn points (or at center if none configured)
            if (dropSpawnPoints != null && dropSpawnPoints.Count > 0)
            {
                // Collect valid spawn point positions (they've moved with the fallen upper part)
                List<Vector3> positions = new List<Vector3>();
                foreach (var point in dropSpawnPoints)
                {
                    if (point != null)
                        positions.Add(point.position);
                }

                if (positions.Count > 0)
                {
                    RequestDropsAtPositions(positions);
                }
                else
                {
                    // All spawn point references were null, fallback to upper part position
                    Vector3 fallback = upperPartSlot != null ? upperPartSlot.transform.position : transform.position;
                    RequestDropsAtPosition(fallback);
                }
            }
            else
            {
                // No spawn points configured, use upper part center (legacy behavior)
                Vector3 dropPosition = upperPartSlot != null 
                    ? upperPartSlot.transform.position 
                    : transform.position;
                RequestDropsAtPosition(dropPosition);
            }

            // Destroy/hide the upper part
            if (upperPartSlot != null)
            {
                upperPartSlot.SetActive(false);
            }

            // Schedule stump removal
            if (stumpSlot != null)
            {
                Invoke(nameof(RemoveStump), stumpLifetime);
            }

            // Destroy the entire tree object after everything is done
            // Or keep it for pooling - uncomment below if you want full destruction
            // Destroy(gameObject, stumpLifetime + 1f);
        }

        private void RemoveStump()
        {
            if (stumpSlot != null)
            {
                stumpSlot.SetActive(false);
            }

            // Optionally destroy the whole tree object now
            Destroy(gameObject, 1f);
        }



        #endregion

        #region Editor Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!showForceGizmo) return;

            // Calculate force point position
            Vector3 forcePoint;
            if (upperPartSlot != null)
            {
                forcePoint = upperPartSlot.transform.TransformPoint(forcePointOffset);
            }
            else
            {
                forcePoint = transform.TransformPoint(forcePointOffset);
            }

            // Draw force application point (yellow sphere)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(forcePoint, gizmoSphereRadius);
            Gizmos.DrawSphere(forcePoint, gizmoSphereRadius * 0.3f);

            // Draw fall direction arrow (red)
            if (fallDirection.sqrMagnitude > 0.01f)
            {
                Vector3 worldFallDir = transform.TransformDirection(fallDirection.normalized);
                Vector3 arrowEnd = forcePoint + worldFallDir * gizmoArrowLength;

                Gizmos.color = Color.red;
                Gizmos.DrawLine(forcePoint, arrowEnd);

                // Draw arrowhead
                Vector3 right = Vector3.Cross(worldFallDir, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(worldFallDir, Vector3.forward).normalized;

                Vector3 arrowHeadSize = worldFallDir * 0.3f;
                Gizmos.DrawLine(arrowEnd, arrowEnd - arrowHeadSize + right * 0.15f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - arrowHeadSize - right * 0.15f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - arrowHeadSize + Vector3.up * 0.15f);
                Gizmos.DrawLine(arrowEnd, arrowEnd - arrowHeadSize - Vector3.up * 0.15f);
            }
            else
            {
                // If no direction set, show a "?" indicator
                Gizmos.color = Color.gray;
#if UNITY_EDITOR
                UnityEditor.Handles.Label(forcePoint + Vector3.up * 0.5f, "Dir: From Hit");
#endif
            }

            // Draw drop spawn points (green spheres with labels)
            if (dropSpawnPoints != null)
            {
                for (int i = 0; i < dropSpawnPoints.Count; i++)
                {
                    if (dropSpawnPoints[i] == null) continue;

                    Vector3 spawnPos = dropSpawnPoints[i].position;
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(spawnPos, 0.25f);
                    Gizmos.DrawSphere(spawnPos, 0.08f);

                    // Connect spawn points with a line to show distribution
                    if (i > 0 && dropSpawnPoints[i - 1] != null)
                    {
                        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
                        Gizmos.DrawLine(dropSpawnPoints[i - 1].position, spawnPos);
                    }

#if UNITY_EDITOR
                    UnityEditor.Handles.Label(spawnPos + Vector3.up * 0.35f, $"Drop {i + 1}");
#endif
                }
            }
        }

        #endregion
    }
}