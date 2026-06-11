using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Unified build piece component. Uses configurable snap point rules for alignment and rotation.
    /// No longer abstract - all piece types use this component with different snap point configurations.
    /// </summary>
    public class BuildPiece : MonoBehaviour
    {
        [Header("Network Piece ID")]
        [Tooltip("Unique ID assigned by BuildDataManager. -1 means ghost/unplaced.")]
        [HideInInspector] public int pieceId = -1;
        
        protected List<SnapPoint> snapPoints = new List<SnapPoint>();
        protected Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
        protected BuildManager buildManager;
        protected int area = 3;
        protected SnapPoint currentSnappedPoint;
        protected SnapPoint currentMySnap;
        protected SnapCompatibility currentCompatibility;
        protected bool isValid = false;

        /// <summary>
        /// True if this is a blueprint piece (transparent placeholder awaiting materials).
        /// </summary>
        [HideInInspector] public bool isBlueprintPiece = false;

        /// <summary>
        /// Structural stability (0.0 = no support, 1.0 = grounded/foundation).
        /// </summary>
        [HideInInspector] public float stability = 0f;

        // Ghost-only: calculated stability during preview
        private float ghostStability = 0f;

        /// <summary>
        /// The calculated stability for this ghost piece. Used by BuildManager for validation.
        /// </summary>
        public float GhostStability => ghostStability;

        // Rotation state - tracks user rotation so it isn't overwritten by alignment each frame
        private SnapPoint lastTargetSnap = null;
        private float userRotationOffset = 0f;
        public bool canPlaceOnGround = true;

        [Header("Foundation Options")]
        [Tooltip("If true, this piece acts as a grounded foundation (stability = 100%) when placed on terrain/ground layer.")]
        public bool isFoundation = false;

        [Header("Rotation Settings")]
        [Tooltip("Optional center pivot point for rotation when RotationPivotMode is CenterPoint. If null, uses transform center.")]
        public Transform rotationPivot;

        [Tooltip("Overlap detection tolerance - smaller values are more strict")]
        public float overlapTolerance = 0.1f;
        private BoxCollider boxCollider;
        public bool IsValid => isValid;
        
        /// <summary>
        /// Returns true if this is a placed piece (not a ghost).
        /// </summary>
        public bool IsPlacedPiece => pieceId >= 0;

        /// <summary>
        /// Returns the BuildPiece that this ghost is currently snapped to, or null if free-floating.
        /// </summary>
        public BuildPiece SnappedToBuildPiece =>
            currentSnappedPoint != null
                ? currentSnappedPoint.GetComponentInParent<BuildPiece>()
                : null;

        private void Awake()
        {
            buildManager = BuildManager.Instance;

            snapPoints = new List<SnapPoint>(GetComponentsInChildren<SnapPoint>());

            // Store original materials
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                originalMaterials.Add(rend, rend.sharedMaterial);
            }

            // Only apply ghost preview materials if this is a ghost (BuildManager exists)
            // Network-spawned pieces skip this - they keep original materials
            if (buildManager != null && buildManager.InvalidMaterial != null)
            {
                foreach (Renderer rend in originalMaterials.Keys)
                {
                    rend.material = buildManager.InvalidMaterial;
                }
            }

            boxCollider = GetComponent<BoxCollider>();

            // Disable colliders initially (will be enabled by BuildTile for placed pieces)
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
                col.isTrigger = false;
            }
        }

        public void UpdateGhostPrefab(RaycastHit hit, Ray ray, float collapseThreshold)
        {
            var result = FindBestSnap(ray, hit.point);

            if (result.targetSnap != null)
            {
                bool targetChanged = result.targetSnap != lastTargetSnap;

                if (targetChanged)
                {
                    // New snap target - accept auto-selected snap point and reset user rotation
                    currentSnappedPoint = result.targetSnap;
                    currentMySnap = result.mySnap;
                    currentCompatibility = result.compatibility;
                    lastTargetSnap = result.targetSnap;
                    userRotationOffset = 0f;
                }
                // When target is unchanged, keep currentMySnap/currentCompatibility
                // from the user's rotation choices (CycleSnapPoints etc.)

                // Align using the current snap point choice
                AlignWithRule(currentMySnap, currentSnappedPoint, currentCompatibility.matchingRule);

                // Re-apply user rotation offset
                if (Mathf.Abs(userRotationOffset) > 0.01f)
                {
                    // Always rotate around the snap point's Y axis (up direction)
                    Vector3 rotAxis = currentSnappedPoint.transform.up;

                    if (currentCompatibility.rotationPivotMode == RotationPivotMode.CenterPoint)
                    {
                        // CenterPoint: piece spins in place around its own center
                        Vector3 pivotPos = rotationPivot != null ? rotationPivot.position : transform.position;
                        transform.RotateAround(pivotPos, rotAxis, userRotationOffset);
                        // Do NOT re-seat — the piece stays at center, snap points orbit around it
                    }
                    else
                    {
                        // SnapPoint: piece orbits around the snap connection point
                        transform.RotateAround(currentSnappedPoint.transform.position, rotAxis, userRotationOffset);
                        
                        // Re-seat so snap points stay perfectly aligned
                        Vector3 snapOffset = currentMySnap.transform.position - transform.position;
                        transform.position = currentSnappedPoint.transform.position - snapOffset;
                    }
                }

                // Calculate ghost stability from target piece and other adjacent pieces
                // Using multiplicative decay: parentStability * decayFactor
                float bestStability = 0f;
                BuildPiece targetPiece = null;

                if (isFoundation || IsTouchingGround())
                {
                    bestStability = 1.0f;
                }
                else
                {
                    targetPiece = currentSnappedPoint.GetComponentInParent<BuildPiece>();
                    if (targetPiece != null)
                    {
                        bestStability = targetPiece.stability * currentCompatibility.stabilityDecayFactor;
                    }
                    else
                    {
                        bestStability = 1.0f; // Snapping to something without a BuildPiece = ground level
                    }
                }

                // Query all other snap points to find other touching pieces that might provide better support
                if (SnapPointRegistry.Instance != null)
                {
                    foreach (var mySnap in snapPoints)
                    {
                        List<SnapPoint> nearbySnaps = SnapPointRegistry.Instance.GetNearby(mySnap.transform.position, 1);
                        foreach (var otherSnap in nearbySnaps)
                        {
                            if (otherSnap == null || otherSnap == mySnap) continue;

                            var otherPiece = otherSnap.GetComponentInParent<BuildPiece>();
                            if (otherPiece == null || !otherPiece.IsPlacedPiece || otherPiece == targetPiece) continue;

                            var compat = mySnap.GetCompatibility(otherSnap.snapType);
                            if (compat == null) continue;

                            float distance = Vector3.Distance(mySnap.transform.position, otherSnap.transform.position);
                            if (distance <= 0.15f) // Snap tolerance
                            {
                                float stabilityThroughOther = otherPiece.stability * compat.stabilityDecayFactor;
                                if (stabilityThroughOther > bestStability)
                                {
                                    bestStability = stabilityThroughOther;
                                }
                            }
                        }
                    }
                }

                ghostStability = bestStability;

                // Check if stability is too low to place
                if (ghostStability < collapseThreshold)
                {
                    SetMaterial(buildManager.invalidMaterial);
                    isValid = false;
                    return;
                }

                // Check for overlaps even when snapped
                if (CheckForOverlaps())
                {
                    SetMaterial(buildManager.invalidMaterial);
                    isValid = false;
                    return;
                }

                // Valid snap — apply Valheim-style stability color feedback
                UpdateStabilityColor(ghostStability);
                isValid = true;
                return;
            }

            // No snap point found - reset snap state and allow free movement
            lastTargetSnap = null;
            currentSnappedPoint = null;
            currentMySnap = null;
            currentCompatibility = null;
            userRotationOffset = 0f;
            ghostStability = 1.0f; // Ground placement = foundation (100%)

            transform.position = hit.point;

            // Check ground angle for validity
            if (canPlaceOnGround && IsGroundAngleValid(hit.normal))
            {
                isValid = true;
                UpdateStabilityColor(ghostStability);
            }
            else
            {
                isValid = false;
                SetMaterial(buildManager.invalidMaterial);
            }
        }

        private bool CheckForOverlaps()
        {
            if (boxCollider == null)
                return false;

            // Get the box collider's center and size in world space
            Vector3 center = transform.TransformPoint(boxCollider.center);
            Vector3 size = Vector3.Scale(boxCollider.size, transform.lossyScale);

            // Reduce the size slightly to account for tolerance
            size -= Vector3.one * overlapTolerance;

            // Use Physics.OverlapBox to detect overlapping colliders on the buildPiece layer
            Collider[] overlapping = Physics.OverlapBox(
                center,
                size * 0.5f, // OverlapBox expects half-extents
                transform.rotation,
                buildManager.buildPieceLayer
            );

            // Check if we found any overlapping build pieces
            foreach (var overlappingCollider in overlapping)
            {
                // Make sure we're not detecting ourselves (shouldn't happen since our collider is disabled)
                if (overlappingCollider.transform != transform)
                {
                    return true; // Found overlap
                }
            }

            return false; // No overlaps found
        }

        /// <summary>
        /// Checks if the bottom of this build piece physically touches/intersects the ground layer.
        /// </summary>
        public bool IsTouchingGround()
        {
            if (boxCollider == null || buildManager == null)
                return false;

            Vector3 center = transform.TransformPoint(boxCollider.center);
            Vector3 size = Vector3.Scale(boxCollider.size, transform.lossyScale);

            // Shift the center to the bottom of the box collider
            float bottomExtent = size.y * 0.5f;
            Vector3 bottomCenter = center - transform.up * bottomExtent;

            // Check a small flat box centered on the bottom of the piece (0.2m thick)
            Vector3 halfExtents = new Vector3(size.x * 0.5f, 0.1f, size.z * 0.5f);

            Collider[] groundColliders = Physics.OverlapBox(
                bottomCenter,
                halfExtents,
                transform.rotation,
                buildManager.groundLayer
            );

            return groundColliders.Length > 0;
        }

        private bool IsGroundAngleValid(Vector3 groundNormal)
        {
            // Calculate the angle between the ground normal and up vector
            float angle = Vector3.Angle(groundNormal, Vector3.up);

            // Return true if angle is less than or equal to 45 degrees
            return angle <= 45f;
        }

        private void SetMaterial(Material material)
        {
            foreach (var rend in originalMaterials.Keys)
            {
                // Use instance material (not sharedMaterial) to avoid affecting other objects
                rend.material = material;
            }
        }

        /// <summary>
        /// Valheim-style color-coded stability feedback on the ghost preview.
        /// Cyan = grounded, Green = strong, Yellow = medium, Orange = weak, Red = critical.
        /// </summary>
        private void UpdateStabilityColor(float stab)
        {
            Color feedbackColor;
            if (stab >= 0.99f)
                feedbackColor = Color.cyan;                         // Foundation / grounded
            else if (stab >= 0.80f)
                feedbackColor = Color.green;                        // Strong
            else if (stab >= 0.50f)
                feedbackColor = Color.yellow;                       // Medium
            else if (stab >= 0.25f)
                feedbackColor = new Color(1f, 0.5f, 0f, 1f);       // Orange - weak
            else
                feedbackColor = Color.red;                          // Critical

            foreach (var rend in originalMaterials.Keys)
            {
                Material mat = rend.material;
                // Try URP _BaseColor, then fall back to _Color
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", feedbackColor);
                else if (mat.HasProperty("_Color"))
                    mat.color = feedbackColor;
            }
        }

        /// <summary>
        /// Triggers physical collapse if BuildPhysicalCollapse component exists.
        /// Otherwise falls back to instant destruction.
        /// Called by BuildManager during cascading collapse.
        /// </summary>
        public void TriggerPhysicalCollapse()
        {
            var collapser = GetComponent<BuildPhysicalCollapse>();
            if (collapser != null)
            {
                collapser.TriggerCollapse();
            }
            else
            {
                // No collapse component — instant destroy
                Destroy(gameObject);
            }
        }

         /// <summary>
        /// Finalizes the build piece: registers snap points, restores materials, enables colliders.
        /// Called when a ghost is placed OR when BuildDataManager spawns a networked piece.
        /// </summary>
        public void BuildTile()
        {
            // Register snap points for snapping
            foreach (SnapPoint snap in snapPoints)
            {
                snap.RegisterSnapPoint();
            }

            // Restore original materials (for ghosts that had preview materials)
            foreach (var rend in originalMaterials.Keys)
            {
                rend.material = originalMaterials[rend];
            }

            // Enable colliders for physics
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                col.enabled = true;
                col.isTrigger = false;
            }

            // Disable the main overlap detection collider
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            isBlueprintPiece = false;
        }

        /// <summary>
        /// Spawns as a blueprint: registers snap points and enables colliders,
        /// but applies the transparent blueprint material instead of original materials.
        /// </summary>
        public void BuildTileAsBlueprint(Material blueprintMaterial)
        {
            // Register snap points for snapping
            foreach (SnapPoint snap in snapPoints)
            {
                snap.RegisterSnapPoint();
            }

            // Apply blueprint (transparent) material
            if (blueprintMaterial != null)
            {
                foreach (var rend in originalMaterials.Keys)
                {
                    rend.material = blueprintMaterial;
                }
            }

            // Enable colliders for physics
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                col.enabled = true;
                col.isTrigger = false;
            }

            // Disable the main overlap detection collider
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            isBlueprintPiece = true;
        }

        /// <summary>
        /// Upgrades a blueprint piece to a solid piece by restoring original materials.
        /// </summary>
        public void UpgradeToSolid()
        {
            foreach (var rend in originalMaterials.Keys)
            {
                rend.material = originalMaterials[rend];
            }
            isBlueprintPiece = false;
        }

        #region Hover Outline

        private bool isOutlined = false;

        /// <summary>
        /// Applies an outline material as a second material pass on all renderers.
        /// </summary>
        public void ApplyOutline(Material outlineMaterial)
        {
            if (isOutlined || outlineMaterial == null) return;

            foreach (var rend in originalMaterials.Keys)
            {
                var mats = rend.sharedMaterials;
                var newMats = new Material[mats.Length + 1];
                mats.CopyTo(newMats, 0);
                newMats[mats.Length] = outlineMaterial;
                rend.materials = newMats;
            }
            isOutlined = true;
        }

        /// <summary>
        /// Removes the outline material, restoring single-material renderers.
        /// </summary>
        public void RemoveOutline()
        {
            if (!isOutlined) return;

            foreach (var rend in originalMaterials.Keys)
            {
                var mats = rend.sharedMaterials;
                if (mats.Length > 1)
                {
                    var newMats = new Material[mats.Length - 1];
                    System.Array.Copy(mats, newMats, mats.Length - 1);
                    rend.materials = newMats;
                }
            }
            isOutlined = false;
        }

        #endregion

        /// <summary>
        /// Player interaction: try to fill this blueprint with required resources.
        /// Checks inventory and sends fill request to server.
        /// </summary>
        public void TryFillBlueprint()
        {
            if (!isBlueprintPiece || pieceId < 0) return;

            if (BuildManager.Instance != null)
            {
                // Get the piece entry to check costs
                var db = BuildManager.Instance.BuildDatabase;
                if (db == null) return;

                // Find which BuildPieceEntry this piece uses
                var prefabIndex = GetPrefabIndexFromManager();
                if (prefabIndex < 0) return;

                var entry = db.Pieces[prefabIndex];
                if (entry == null) return;

                // Check resources
                var inventory = LocalPlayerInstance.InventoryManager;
                if (inventory == null)
                {
                    Debug.Log("No inventory found!");
                    return;
                }

                if (entry.costs != null)
                {
                    foreach (var cost in entry.costs)
                    {
                        if (cost.resource == null) continue;
                        if (inventory.GetTotalCount(cost.resource.itemId) < cost.amount)
                        {
                            Debug.Log("Not enough resources to fill blueprint!");
                            return;
                        }
                    }

                    // Consume resources
                    foreach (var cost in entry.costs)
                    {
                        if (cost.resource == null) continue;
                        inventory.RemoveItemByIDRpc(cost.resource.itemId, cost.amount);
                    }
                }

                // Send fill request to server
                BuildManager.Instance.FillBlueprintServerRpc(pieceId);
            }
        }

        /// <summary>
        /// Gets the prefab index for this piece from the BuildManager's spawned pieces.
        /// </summary>
        private int GetPrefabIndexFromManager()
        {
            if (BuildManager.Instance == null) return -1;

            // Search the network list for our piece ID to get the prefab index
            return BuildManager.Instance.GetPrefabIndexForPiece(pieceId);
        }

        public void RotateGhost(bool clockwise)
        {
            if (currentSnappedPoint != null && currentCompatibility != null)
            {
                // Snapped: use the rotation step from the active snap compatibility
                float step = currentCompatibility.rotationStep;
                float angle = clockwise ? step : -step;
                userRotationOffset += angle;
            }
            else
            {
                // Not snapped: free rotation with 15° steps around pivot
                float angle = clockwise ? 15f : -15f;
                Vector3 pivot = rotationPivot != null ? rotationPivot.position : transform.position;
                transform.RotateAround(pivot, Vector3.up, angle);
            }
        }

        /// <summary>
        /// Result of snap point search.
        /// </summary>
        private struct SnapResult
        {
            public SnapPoint targetSnap;
            public SnapPoint mySnap;
            public SnapCompatibility compatibility;
        }

        /// <summary>
        /// Find the best snap point match using ray-center priority.
        /// Snap points closer to the camera's forward ray are prioritized over simple 3D distance.
        /// </summary>
        private SnapResult FindBestSnap(Ray ray, Vector3 hitPoint)
        {
            SnapResult result = new SnapResult();

            // Check if registry exists
            if (SnapPointRegistry.Instance == null)
                return result;

            // Get nearby snap points using the registry
            List<SnapPoint> possible = SnapPointRegistry.Instance.GetNearby(hitPoint, area);

            SnapPoint closestSnap = null;
            float closestRayDist = float.MaxValue;

            // Find the snap point closest to the camera ray (perpendicular distance)
            // This prioritizes what the player is LOOKING AT, not just the closest in 3D space
            foreach (var otherSnap in possible)
            {
                if (otherSnap == null) continue;

                // Calculate perpendicular distance from snap point to the camera ray
                Vector3 toSnap = otherSnap.transform.position - ray.origin;
                Vector3 cross = Vector3.Cross(ray.direction, toSnap);
                float perpendicularDist = cross.magnitude;

                // Also factor in the distance along the ray to avoid selecting points behind the player
                float alongRay = Vector3.Dot(toSnap, ray.direction);
                if (alongRay < 0) continue; // Behind the camera

                if (perpendicularDist < closestRayDist)
                {
                    closestRayDist = perpendicularDist;
                    closestSnap = otherSnap;
                }
            }

            if (closestSnap == null)
                return result;

            // Find our own snap point closest to the found snap point that is compatible
            SnapPoint myClosestSnap = null;
            SnapCompatibility myCompatibility = null;
            float myClosestDist = float.MaxValue;
            
            foreach (var mySnap in snapPoints)
            {
                // Check if this snap point can connect to the target
                var compat = mySnap.GetCompatibility(closestSnap.snapType);
                if (compat == null) continue;

                float dist = Vector3.Distance(closestSnap.transform.position, mySnap.transform.position);
                if (dist < myClosestDist)
                {
                    myClosestDist = dist;
                    myClosestSnap = mySnap;
                    myCompatibility = compat;
                }
            }

            // Return the best match (without aligning - caller handles alignment)
            if (myClosestSnap != null && myCompatibility != null)
            {
                result.targetSnap = closestSnap;
                result.mySnap = myClosestSnap;
                result.compatibility = myCompatibility;
            }

            return result;
        }

        /// <summary>
        /// Align this object using the specified matching rule.
        /// </summary>
        protected void AlignWithRule(SnapPoint mySnap, SnapPoint targetSnap, MatchingRule rule)
        {
            switch (rule)
            {
                case MatchingRule.Face2Face:
                    AlignFace2Face(mySnap, targetSnap);
                    break;

                case MatchingRule.Face2Object:
                    AlignFace2Object(mySnap, targetSnap);
                    break;
                    
                case MatchingRule.Object2Face:
                    AlignObject2Face(mySnap, targetSnap);
                    break;
            }
        }

        /// <summary>
        /// Face-to-face alignment: Aligns this object so that 'mySnap' forward faces opposite to 'targetSnap' forward.
        /// Used for edge-to-edge connections like floor tiles.
        /// </summary>
        protected void AlignFace2Face(SnapPoint mySnap, SnapPoint targetSnap)
        {
            // Standard face-to-face alignment (opposite directions)
            Vector3 targetForward = targetSnap.transform.forward;
            Vector3 targetUp = targetSnap.transform.up;

            // Desired orientation: Look opposite to target, but keep Up aligned
            Quaternion targetLookRotation = Quaternion.LookRotation(-targetForward, targetUp);

            // Calculate the difference between my snap point's rotation and the desired rotation
            Quaternion deltaRotation = targetLookRotation * Quaternion.Inverse(mySnap.transform.localRotation);
            
            transform.rotation = deltaRotation;

            // Position Alignment: offset so snap points overlap
            Vector3 offset = mySnap.transform.position - transform.position;
            transform.position = targetSnap.transform.position - offset;
        }

        /// <summary>
        /// Face-to-object alignment: Aligns this object so that 'mySnap' forward OPPOSES the target BUILD PIECE's forward.
        /// Used for wall-bottom-to-floor connections.
        /// </summary>
        protected void AlignFace2Object(SnapPoint mySnap, SnapPoint targetSnap)
        {
            // Get the target build piece's forward (the parent object containing the snap point)
            BuildPiece targetPiece = targetSnap.GetComponentInParent<BuildPiece>();
            Vector3 objectForward = targetPiece != null ? targetPiece.transform.forward : targetSnap.transform.forward;

            // Our snap point's forward should OPPOSE target object's forward
            // Calculate the rotation needed for our snap point to face -objectForward
            Quaternion desiredSnapRotation = Quaternion.LookRotation(-objectForward, Vector3.up);
            
            // Apply the rotation considering our snap point's local rotation
            Quaternion deltaRotation = desiredSnapRotation * Quaternion.Inverse(mySnap.transform.localRotation);
            transform.rotation = deltaRotation;

            // Position: align snap points
            Vector3 offset = mySnap.transform.position - transform.position;
            transform.position = targetSnap.transform.position - offset;
        }

        /// <summary>
        /// Object-to-face alignment: Aligns OUR OBJECT's forward to OPPOSE TARGET SNAP POINT's forward.
        /// Used when our whole piece should face opposite direction of target's snap point.
        /// </summary>
        protected void AlignObject2Face(SnapPoint mySnap, SnapPoint targetSnap)
        {
            // Our object's forward should OPPOSE target snap point's forward
            Vector3 targetSnapForward = targetSnap.transform.forward;
            
            // Rotate our object to face opposite of target snap point's forward direction
            transform.rotation = Quaternion.LookRotation(-targetSnapForward, Vector3.up);

            // Position: align snap points
            Vector3 offset = mySnap.transform.position - transform.position;
            transform.position = targetSnap.transform.position - offset;
        }

        /// <summary>
        /// Destroy this build piece. Sends removal request to server.
        /// Only works on placed pieces (not ghosts).
        /// </summary>
        public void DestroyPiece()
        {
            if (pieceId < 0)
            {
                Debug.LogWarning("DestroyPiece called on ghost piece (no pieceId)");
                return;
            }

            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.RemovePieceServerRpc(pieceId);
            }
            else
            {
                Debug.LogError("BuildManager.Instance is null, cannot destroy piece!");
            }
        }

        /// <summary>
        /// Gets the BuildPieceEntry corresponding to this placed piece.
        /// </summary>
        public BuildPieceEntry GetBuildPieceEntry()
        {
            if (pieceId < 0 || BuildManager.Instance == null || BuildManager.Instance.BuildDatabase == null) return null;
            int idx = GetPrefabIndexFromManager();
            if (idx >= 0 && idx < BuildManager.Instance.BuildDatabase.Pieces.Count)
            {
                return BuildManager.Instance.BuildDatabase.Pieces[idx];
            }
            return null;
        }
    }
}
