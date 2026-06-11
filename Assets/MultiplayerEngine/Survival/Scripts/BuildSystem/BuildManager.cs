using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Placement mode for the build system. Set by developer in Inspector.
    /// </summary>
    public enum BuildPlacementMode
    {
        /// <summary>Place tiles with materials consumed immediately (default).</summary>
        DirectPlace,
        /// <summary>Place transparent blueprint placeholders; fill with materials later.</summary>
        Blueprint
    }

    /// <summary>
    /// Unified BuildManager - handles ghost placement, network sync, and persistence.
    /// Uses data-driven sync (NetworkList) instead of NetworkObject per piece.
    /// </summary>
    public class BuildManager : NetworkBehaviour
    {
        public static BuildManager Instance { get; private set; }

        #region Inspector Fields

        [Header("Layers")]
        public LayerMask groundLayer;
        public LayerMask buildPieceLayer;
        public LayerMask ignoreLayers;

        [Header("Materials")]
        public Material validMaterial;
        public Material invalidMaterial;

        [Tooltip("Material used for blueprint placeholders (semi-transparent).")]
        public Material blueprintMaterial;

        [Tooltip("Material used for hover outline on placed/blueprint pieces.")]
        public Material outlineMaterial;

        [Header("Placement Mode")]
        [Tooltip("DirectPlace = consume resources immediately. Blueprint = place transparent placeholders first.")]
        [SerializeField] private BuildPlacementMode placementMode = BuildPlacementMode.DirectPlace;


        [Header("Settings")]
        public float maxSnapDistance = 0.5f;
        public float maxBuildDistance = 10f;

        [Tooltip("Radius for spherecast aiming (0 = thin raycast, 0.15 = forgiving aim).")]
        [SerializeField] private float sphereCastRadius = 0.15f;

        [Header("Structural Support")]
        [Tooltip("Minimum stability threshold (0.0 to 1.0). Pieces falling below this threshold will collapse. 0.1 = 10% stability.")]
        [SerializeField] private float collapseThreshold = 0.1f;

        [Tooltip("Delay between each wave of cascading destruction (seconds).")]
        [SerializeField] private float cascadeDelay = 0.1f;

        [Header("UI Reference")]
        [SerializeField] private BuildMenuUI buildMenuUI;

        [Header("Database")]
        [Tooltip("Build database for prefab lookup - required for network sync")]
        [SerializeField] private BuildDatabase buildDatabase;
        
        [Tooltip("Layer name for spawned build pieces")]
        [SerializeField] private string buildPieceLayerName = "BuildPiece";

        [Header("Save System")]
        [Tooltip("Auto-save delay after changes (seconds)")]
        [SerializeField] private float autoSaveDelay = 5f;

        #endregion

        #region Properties

        public float MaxSnapDistance => maxSnapDistance;
        public Material ValidMaterial => validMaterial;
        public Material InvalidMaterial => invalidMaterial;
        public Material BlueprintMaterial => blueprintMaterial;
        public BuildPlacementMode PlacementMode => placementMode;
        public BuildDatabase BuildDatabase => buildDatabase;
        public bool IsBuildModeActive => isBuildModeActive;
        public BuildPiece CurrentGhost => currentGhost;
        public BuildPieceEntry CurrentPiece => currentPiece;
        public BuildPiece HoveredPiece => hoveredPiece;

        #endregion

        #region Private State

        // Ghost & Build Mode
        private Camera mainCamera;
        private BuildPiece currentGhost;
        private BuildPieceEntry currentPiece;
        private bool isBuildModeActive;

        // Network Data
        private NetworkList<BuildPieceData> buildPieces;
        private Dictionary<int, GameObject> spawnedPieces = new Dictionary<int, GameObject>();
        private int nextPieceId = 0;

        // Structural support graph (server-side)
        private BuildSupportGraph supportGraph = new BuildSupportGraph();

        // Save System
        private float pendingSaveTimer = -1f;
        private bool hasPendingChanges;

        // Input
        private IInputManager inputManager;

        // Equipment references (for hammer check)
        private EquipmentController equipmentController;
        private IInventoryManager inventoryManager;

        // Hover outline
        private BuildPiece hoveredPiece;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            mainCamera = Camera.main;

            // Initialize NetworkList (must be in Awake before OnNetworkSpawn)
            buildPieces = new NetworkList<BuildPieceData>();
        }

        private void Update()
        {
            // Ghost preview (owner only)
            // Ghost preview & Input (Local Client)
            // Auto-cancel build if hammer was unequipped while ghost is active
            if (isBuildModeActive && currentGhost != null && !IsHammerEquipped())
            {
                CancelBuild();
            }

            HandleInput();
            UpdateHoverOutline();
            if (isBuildModeActive && currentGhost != null)
            {
                UpdateGhostPosition();
            }

            // Auto-save timer (server only)
            if (IsServer && hasPendingChanges && pendingSaveTimer > 0)
            {
                pendingSaveTimer -= Time.deltaTime;
                if (pendingSaveTimer <= 0)
                {
                    PerformSave();
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (IsServer && hasPendingChanges)
            {
                PerformSave();
            }
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Validate database
            if (buildDatabase == null)
            {
                Debug.LogError("[BuildManager] BuildDatabase is not assigned!");
            }

            // Subscribe to NetworkList changes
            buildPieces.OnListChanged += OnBuildPiecesChanged;

            // Subscribe to build menu events
            if (buildMenuUI != null)
            {
                buildMenuUI.OnPieceSelected += OnPieceSelected;
                buildMenuUI.OnPieceChanged += OnPieceChanged;
                buildMenuUI.OnMenuClosed += OnMenuClosed;
            }

            if (IsServer)
            {
                // Ensure SaveGameManager exists (auto-creates for testing without lobby)
                SaveGameManager.EnsureInstance();
                SaveGameManager.Instance.ActivateTestIdIfNeeded();

                // Server: Load saved buildings
                LoadBuildings();
                SaveGameManager.OnAutoSave += PerformSave;
            }
            else
            {
                // Client: Spawn all existing pieces (late joiner)
                SpawnAllExistingPieces();
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from events
            buildPieces.OnListChanged -= OnBuildPiecesChanged;

            if (buildMenuUI != null)
            {
                buildMenuUI.OnPieceSelected -= OnPieceSelected;
                buildMenuUI.OnPieceChanged -= OnPieceChanged;
                buildMenuUI.OnMenuClosed -= OnMenuClosed;
            }

            // Cleanup spawned pieces
            foreach (var kvp in spawnedPieces)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            spawnedPieces.Clear();

            if (IsServer)
            {
                SaveGameManager.OnAutoSave -= PerformSave;
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            if (inputManager == null || (inputManager as MonoBehaviour) == null)
                inputManager = LocalPlayerInstance.InputManager;

            if (inputManager == null) return;

            // Cache equipment references (BuildManager is a singleton, not on the player)
            if (equipmentController == null || (equipmentController as MonoBehaviour) == null)
                equipmentController = LocalPlayerInstance.EquipmentController;
            if (inventoryManager == null || (inventoryManager as MonoBehaviour) == null)
                inventoryManager = LocalPlayerInstance.InventoryManager;

            bool hammerEquipped = IsHammerEquipped();

            // Right-click hold opens build menu, release closes it (hammer required)
            if (inputManager.SecondaryActionDown && hammerEquipped)
            {
                if (buildMenuUI != null && !buildMenuUI.IsOpen)
                    buildMenuUI.Open();
            }
            if (inputManager.SecondaryActionUp && buildMenuUI != null && buildMenuUI.IsOpen)
            {
                buildMenuUI.Close();
            }

            // Cancel build mode via InputManager
            if (inputManager.Cancel && isBuildModeActive)
            {
                CancelBuild();
            }

            // Blueprint fill interaction — only when hammer equipped and NOT in build mode
            if (!isBuildModeActive && inputManager.Interact && hammerEquipped)
            {
                TryFillBlueprintAtCrosshair();
            }

            // Delete build piece — only when hammer equipped
            if (inputManager.DeleteBuild && hammerEquipped)
            {
                TryDeletePieceAtCrosshair();
            }

            if (!isBuildModeActive || currentGhost == null) return;

            // Rotation via InputManager
            if (inputManager.RotateLeft)
                currentGhost.RotateGhost(false);
            else if (inputManager.RotateRight)
                currentGhost.RotateGhost(true);

            // Place via InputManager primary action (skip if clicking on UI)
            if (inputManager.PrimaryActionDown && currentGhost.IsValid)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;
                PlacePiece();
            }
        }

        #endregion

        #region Build Menu Integration



        private void OnPieceSelected(BuildPieceEntry piece)
        {
            if (piece == null) return;

            currentPiece = piece;
            StartBuild(piece);
            // Menu stays open — it closes when right-click is released
        }

        private void OnPieceChanged(BuildPieceEntry piece)
        {
            if (isBuildModeActive && piece != null)
            {
                currentPiece = piece;
                SwapGhost(piece);
            }
        }

        private void OnMenuClosed()
        {
            // Menu closed without selection - do nothing
        }

        #endregion

        #region Ghost Operations

        public void StartBuild(BuildPieceEntry piece)
        {
            if (piece?.ghostPrefab == null) return;

            CancelBuild();

            currentGhost = Instantiate(piece.ghostPrefab).GetComponent<BuildPiece>();
            if (currentGhost != null)
            {
                currentGhost.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                var col = currentGhost.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                isBuildModeActive = true;
            }
        }

        private void SwapGhost(BuildPieceEntry piece)
        {
            if (piece?.ghostPrefab == null) return;

            Vector3 oldPos = currentGhost?.transform.position ?? Vector3.zero;
            Quaternion oldRot = currentGhost?.transform.rotation ?? Quaternion.identity;

            CancelBuild();

            currentGhost = Instantiate(piece.ghostPrefab, oldPos, oldRot).GetComponent<BuildPiece>();
            if (currentGhost != null)
            {
                currentGhost.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                isBuildModeActive = true;
            }
        }

        private void UpdateGhostPosition()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxBuildDistance, groundLayer | buildPieceLayer))
            {
                currentGhost.UpdateGhostPrefab(hit, ray, collapseThreshold);
            }
        }

        public void CancelBuild()
        {
            if (currentGhost != null)
            {
                Destroy(currentGhost.gameObject);
                currentGhost = null;
            }
            isBuildModeActive = false;
        }

        #endregion

        #region Place Piece (Client → Server)

        private void PlacePiece()
        {
            if (currentGhost == null || currentPiece == null) return;

            bool isBlueprint = placementMode == BuildPlacementMode.Blueprint;

            // In Direct Place mode, check resources before placing
            if (!isBlueprint && !HasRequiredResources(currentPiece.costs))
            {
                Debug.Log("Not enough resources to build!");
                return;
            }

            // Validate database
            if (buildDatabase == null)
            {
                Debug.LogError("BuildManager: BuildDatabase is not assigned!");
                return;
            }

            // Get prefab index
            int prefabIndex = buildDatabase.GetPrefabIndex(currentPiece.buildPrefab);
            if (prefabIndex < 0)
            {
                Debug.LogError($"BuildManager: Prefab '{currentPiece.buildPrefab.name}' not found in registry!");
                return;
            }

            // Get the snapped-to piece ID for support graph connection
            int snappedToPieceId = -1;
            if (currentGhost.SnappedToBuildPiece != null)
            {
                snappedToPieceId = currentGhost.SnappedToBuildPiece.pieceId;
            }

            // Send to server
            PlacePieceServerRpc(
                (ushort)prefabIndex,
                currentGhost.transform.position,
                currentGhost.transform.rotation,
                isBlueprint,
                (ushort)Mathf.Clamp(Mathf.RoundToInt(currentGhost.GhostStability * 10000f), 0, 10000),
                snappedToPieceId
            );

            // Deduct resources only in Direct Place mode
            if (!isBlueprint)
            {
                ConsumeResources(currentPiece.costs);
            }
        }

        /// <summary>
        /// Server RPC to place a new build piece.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void PlacePieceServerRpc(ushort prefabIndex, Vector3 position, Quaternion rotation, bool isBlueprint = false, ushort stabilityPacked = 10000, int snappedToPieceId = -1)
        {
            if (buildDatabase == null || buildDatabase.GetPrefab(prefabIndex) == null)
            {
                Debug.LogError($"[BuildManager] Invalid prefab index {prefabIndex}");
                return;
            }

            int newId = nextPieceId++;

            float stability = stabilityPacked / 10000f;

            BuildPieceData newPiece = new BuildPieceData(
                newId,
                prefabIndex,
                position,
                rotation,
                isBlueprint,
                stability
            );

            buildPieces.Add(newPiece);

            // Determine if it's a foundation piece
            var entry = buildDatabase.GetPieceByID(newId); // wait, ID is pieceId not prefab ID
            // Better to use prefabIndex
            var entryByPrefab = buildDatabase.Pieces[prefabIndex];
            bool isFoundation = entryByPrefab != null && entryByPrefab.isFoundation;

            // Simple heuristic for terrain: if not snapped and placed on ground layer, treat as grounded foundation
            bool isGrounded = isFoundation || (snappedToPieceId < 0 && stability >= 0.99f);

            // Register in support graph and automatically detect connections & recalculate support
            AutoDetectConnectionsAndRecalculate(newId, isGrounded);

            MarkDirty();

            Debug.Log($"[BuildManager] Placed piece {newPiece.pieceId} (prefab {prefabIndex}, blueprint={isBlueprint}, stability={stability:F2})");
        }

        /// <summary>
        /// Server RPC to fill a blueprint piece with materials, upgrading it to solid.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void FillBlueprintServerRpc(int pieceId)
        {
            for (int i = 0; i < buildPieces.Count; i++)
            {
                if (buildPieces[i].pieceId == pieceId && buildPieces[i].isBlueprint)
                {
                    // Update the piece data to non-blueprint
                    var updatedPiece = buildPieces[i];
                    updatedPiece.isBlueprint = false;
                    buildPieces[i] = updatedPiece;
                    MarkDirty();
                    Debug.Log($"[BuildManager] Filled blueprint piece {pieceId}");
                    return;
                }
            }
            Debug.LogWarning($"[BuildManager] Blueprint piece {pieceId} not found for filling");
        }

        /// <summary>
        /// Server RPC to remove a build piece.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RemovePieceServerRpc(int pieceId)
        {
            for (int i = 0; i < buildPieces.Count; i++)
            {
                if (buildPieces[i].pieceId == pieceId)
                {
                    bool wasSolid = !buildPieces[i].isBlueprint;
                    ushort prefabIdx = buildPieces[i].prefabIndex;
                    Vector3 pos = buildPieces[i].position;

                    buildPieces.RemoveAt(i);
                    MarkDirty();
                    Debug.Log($"[BuildManager] Removed piece {pieceId}");

                    if (wasSolid)
                    {
                        SpawnDroppedResources(prefabIdx, pos);
                    }

                    // Trigger cascading destruction and support recalculation
                    List<int> collapsed = supportGraph.OnPieceRemoved(pieceId, collapseThreshold);

                    // Sync updated stability levels of remaining pieces to clients
                    for (int j = 0; j < buildPieces.Count; j++)
                    {
                        float currentStab = supportGraph.GetStability(buildPieces[j].pieceId);
                        ushort packedStab = (ushort)Mathf.Clamp(Mathf.RoundToInt(currentStab * 10000f), 0, 10000);
                        if (packedStab != buildPieces[j].stabilityPacked)
                        {
                            var updated = buildPieces[j];
                            updated.stabilityPacked = packedStab;
                            buildPieces[j] = updated;
                        }
                    }

                    if (collapsed.Count > 0)
                    {
                        StartCoroutine(CascadeDestroyCoroutine(collapsed));
                    }

                    return;
                }
            }
            Debug.LogWarning($"[BuildManager] Piece {pieceId} not found for removal");
        }

        /// <summary>
        /// Coroutine that destroys collapsed pieces in waves for a cinematic cascade effect.
        /// </summary>
        private IEnumerator CascadeDestroyCoroutine(List<int> pieceIds)
        {
            foreach (int id in pieceIds)
            {
                // Remove from network list
                for (int i = 0; i < buildPieces.Count; i++)
                {
                    if (buildPieces[i].pieceId == id)
                    {
                        bool wasSolid = !buildPieces[i].isBlueprint;
                        ushort prefabIdx = buildPieces[i].prefabIndex;
                        Vector3 pos = buildPieces[i].position;

                        buildPieces.RemoveAt(i);
                        Debug.Log($"[BuildManager] Cascade destroyed piece {id}");

                        if (wasSolid)
                        {
                            SpawnDroppedResources(prefabIdx, pos);
                        }
                        break;
                    }
                }

                yield return new WaitForSeconds(cascadeDelay);
            }

            MarkDirty();
        }

        /// <summary>
        /// Spawns the resource drop items in the world at the given position.
        /// Only called on the server when a building is demolished or collapsed.
        /// </summary>
        private void SpawnDroppedResources(ushort prefabIndex, Vector3 position)
        {
            if (!IsServer || buildDatabase == null) return;

            if (prefabIndex >= buildDatabase.Pieces.Count) return;
            var entry = buildDatabase.Pieces[prefabIndex];
            if (entry == null || entry.costs == null) return;

            foreach (var cost in entry.costs)
            {
                if (cost.resource == null || cost.amount <= 0) continue;

                var itemData = cost.resource;
                if (itemData.networkPrefab == null) continue;

                // Random offset to disperse dropped items beautifully
                Vector3 spawnPos = position + new Vector3(
                    UnityEngine.Random.Range(-0.4f, 0.4f),
                    UnityEngine.Random.Range(0.2f, 0.6f),
                    UnityEngine.Random.Range(-0.4f, 0.4f)
                );

                var droppedObj = Instantiate(itemData.networkPrefab, spawnPos, Quaternion.identity);
                var netObj = droppedObj.GetComponent<NetworkObject>();

                if (netObj != null)
                {
                    netObj.Spawn();

                    var pickable = droppedObj.GetComponent<Pickable>();
                    if (pickable != null)
                    {
                        pickable.UpdateAmount(cost.amount);
                    }

                    // Apply outward physical impulse
                    var rb = droppedObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 force = new Vector3(
                            UnityEngine.Random.Range(-2f, 2f),
                            UnityEngine.Random.Range(2f, 4f),
                            UnityEngine.Random.Range(-2f, 2f)
                        );
                        rb.linearVelocity = force;
                        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
                    }
                }
                else
                {
                    Destroy(droppedObj);
                }
            }
        }

        /// <summary>
        /// Automatically detects all connections for a newly placed piece and recalculates the support graph.
        /// </summary>
        private void AutoDetectConnectionsAndRecalculate(int newId, bool isGrounded)
        {
            if (!spawnedPieces.TryGetValue(newId, out GameObject spawnedObj) || spawnedObj == null)
            {
                Debug.LogWarning($"[BuildManager] Spawned object for piece {newId} not found during auto-connection detection.");
                return;
            }

            var buildPiece = spawnedObj.GetComponent<BuildPiece>();
            if (buildPiece == null) return;

            // 1. Register the piece in the graph
            bool physicallyGrounded = isGrounded || buildPiece.isFoundation || buildPiece.IsTouchingGround();
            supportGraph.RegisterPiece(newId, physicallyGrounded);

            // 2. Query nearby snap points to automatically establish connections
            var mySnapPoints = spawnedObj.GetComponentsInChildren<SnapPoint>();
            foreach (var mySnap in mySnapPoints)
            {
                // Find nearby snap points in a small radius
                List<SnapPoint> nearbySnaps = SnapPointRegistry.Instance.GetNearby(mySnap.transform.position, 1);
                foreach (var otherSnap in nearbySnaps)
                {
                    if (otherSnap == null || otherSnap == mySnap) continue;

                    var otherPiece = otherSnap.GetComponentInParent<BuildPiece>();
                    if (otherPiece == null || otherPiece.pieceId == newId || otherPiece.pieceId < 0) continue;

                    // Check compatibility
                    var compat = mySnap.GetCompatibility(otherSnap.snapType);
                    if (compat == null) continue;

                    // Check distance to verify they are actually overlapping/touching
                    float distance = Vector3.Distance(mySnap.transform.position, otherSnap.transform.position);
                    if (distance <= 0.15f) // Snapping distance tolerance
                    {
                        // Found a valid connection! Add it to the support graph
                        supportGraph.AddConnection(newId, otherPiece.pieceId, compat.stabilityDecayFactor);
                        Debug.Log($"[BuildManager] Auto-detected connection between piece {newId} and {otherPiece.pieceId} (decay: {compat.stabilityDecayFactor})");
                    }
                }
            }

            // 3. Recalculate all stability levels using Dijkstra
            List<int> collapsed = supportGraph.RecalculateAll(collapseThreshold);

            // Update network piece stability levels so they are synchronized to clients!
            for (int i = 0; i < buildPieces.Count; i++)
            {
                float currentStab = supportGraph.GetStability(buildPieces[i].pieceId);
                ushort packedStab = (ushort)Mathf.Clamp(Mathf.RoundToInt(currentStab * 10000f), 0, 10000);
                if (packedStab != buildPieces[i].stabilityPacked)
                {
                    var updated = buildPieces[i];
                    updated.stabilityPacked = packedStab;
                    buildPieces[i] = updated; // This triggers client sync automatically!
                }
            }

            // 4. Handle collapsed pieces if any
            if (collapsed.Count > 0)
            {
                StartCoroutine(CascadeDestroyCoroutine(collapsed));
            }
        }

        /// <summary>
        /// Reconstructs all connections between all spawned pieces on the server and recalculates their stability.
        /// </summary>
        private void ReconstructAllConnectionsAndSupport()
        {
            supportGraph.Clear();

            // 1. Register all pieces in the support graph first
            foreach (var pieceData in buildPieces)
            {
                bool isGrounded = false;
                if (spawnedPieces.TryGetValue(pieceData.pieceId, out GameObject spawnedObj) && spawnedObj != null)
                {
                    var bp = spawnedObj.GetComponent<BuildPiece>();
                    if (bp != null)
                    {
                        isGrounded = bp.isFoundation || bp.IsTouchingGround();
                    }
                }

                if (!isGrounded)
                {
                    var entryByPrefab = buildDatabase.Pieces[pieceData.prefabIndex];
                    bool isFoundation = entryByPrefab != null && entryByPrefab.isFoundation;
                    isGrounded = isFoundation || pieceData.Stability >= 0.99f;
                }

                supportGraph.RegisterPiece(pieceData.pieceId, isGrounded);
            }

            // 2. Auto-detect connections for all pieces
            foreach (var pieceData in buildPieces)
            {
                if (!spawnedPieces.TryGetValue(pieceData.pieceId, out GameObject spawnedObj) || spawnedObj == null)
                    continue;

                var mySnapPoints = spawnedObj.GetComponentsInChildren<SnapPoint>();
                foreach (var mySnap in mySnapPoints)
                {
                    List<SnapPoint> nearbySnaps = SnapPointRegistry.Instance.GetNearby(mySnap.transform.position, 1);
                    foreach (var otherSnap in nearbySnaps)
                    {
                        if (otherSnap == null || otherSnap == mySnap) continue;

                        var otherPiece = otherSnap.GetComponentInParent<BuildPiece>();
                        if (otherPiece == null || otherPiece.pieceId == pieceData.pieceId || otherPiece.pieceId < 0) continue;

                        var compat = mySnap.GetCompatibility(otherSnap.snapType);
                        if (compat == null) continue;

                        float distance = Vector3.Distance(mySnap.transform.position, otherSnap.transform.position);
                        if (distance <= 0.15f)
                        {
                            // Add connection
                            supportGraph.AddConnection(pieceData.pieceId, otherPiece.pieceId, compat.stabilityDecayFactor);
                        }
                    }
                }
            }

            // 3. Run Dijkstra to recalculate all stability levels
            List<int> collapsed = supportGraph.RecalculateAll(collapseThreshold);

            // Update network piece stability levels
            for (int i = 0; i < buildPieces.Count; i++)
            {
                float currentStab = supportGraph.GetStability(buildPieces[i].pieceId);
                ushort packedStab = (ushort)Mathf.Clamp(Mathf.RoundToInt(currentStab * 10000f), 0, 10000);
                if (packedStab != buildPieces[i].stabilityPacked)
                {
                    var updated = buildPieces[i];
                    updated.stabilityPacked = packedStab;
                    buildPieces[i] = updated;
                }
            }

            // 4. Handle collapsed pieces
            if (collapsed.Count > 0)
            {
                StartCoroutine(CascadeDestroyCoroutine(collapsed));
            }
        }

        #endregion

        #region NetworkList Change Handler

        private void OnBuildPiecesChanged(NetworkListEvent<BuildPieceData> evt)
        {
            switch (evt.Type)
            {
                case NetworkListEvent<BuildPieceData>.EventType.Add:
                    SpawnLocalPiece(evt.Value);
                    break;

                case NetworkListEvent<BuildPieceData>.EventType.Remove:
                case NetworkListEvent<BuildPieceData>.EventType.RemoveAt:
                    DestroyLocalPiece(evt.Value.pieceId);
                    break;

                case NetworkListEvent<BuildPieceData>.EventType.Value:
                    // Handle blueprint → solid transition
                    UpgradeLocalPiece(evt.Value);
                    break;

                case NetworkListEvent<BuildPieceData>.EventType.Clear:
                    DestroyAllLocalPieces();
                    break;
            }
        }

        #endregion

        #region Local Spawning

        private void SpawnAllExistingPieces()
        {
            Debug.Log($"[BuildManager] Late joiner: spawning {buildPieces.Count} existing pieces");
            foreach (var pieceData in buildPieces)
            {
                SpawnLocalPiece(pieceData);
            }
        }

        private void SpawnLocalPiece(BuildPieceData data)
        {
            if (spawnedPieces.ContainsKey(data.pieceId))
            {
                return; // Already spawned
            }

            if (buildDatabase == null) return;

            GameObject prefab = buildDatabase.GetPrefab(data.prefabIndex);
            if (prefab == null)
            {
                Debug.LogError($"[BuildManager] No prefab for index {data.prefabIndex}");
                return;
            }

            GameObject piece = Instantiate(prefab, data.position, data.rotation);
            piece.name = $"{prefab.name}_{data.pieceId}";

            // Set layer
            int layer = LayerMask.NameToLayer(buildPieceLayerName);
            if (layer >= 0)
            {
                SetLayerRecursive(piece, layer);
            }

            spawnedPieces[data.pieceId] = piece;

            // Initialize the build piece
            var buildPiece = piece.GetComponent<BuildPiece>();
            if (buildPiece != null)
            {
                buildPiece.pieceId = data.pieceId;
                buildPiece.stability = data.Stability;

                if (data.isBlueprint)
                {
                    buildPiece.BuildTileAsBlueprint(blueprintMaterial);
                }
                else
                {
                    buildPiece.BuildTile();
                }
            }
        }

        /// <summary>
        /// Upgrades a blueprint piece to a solid piece locally (visual update).
        /// Called when NetworkList value changes (blueprint → solid).
        /// </summary>
        private void UpgradeLocalPiece(BuildPieceData data)
        {
            if (!spawnedPieces.TryGetValue(data.pieceId, out GameObject pieceObj)) return;
            if (pieceObj == null) return;

            var buildPiece = pieceObj.GetComponent<BuildPiece>();
            if (buildPiece != null && buildPiece.isBlueprintPiece && !data.isBlueprint)
            {
                buildPiece.UpgradeToSolid();
                Debug.Log($"[BuildManager] Upgraded piece {data.pieceId} from blueprint to solid");
            }
        }

        private void DestroyLocalPiece(int pieceId)
        {
            if (spawnedPieces.TryGetValue(pieceId, out GameObject piece))
            {
                if (piece != null) 
                {
                    var buildPiece = piece.GetComponent<BuildPiece>();
                    if (buildPiece != null)
                        buildPiece.TriggerPhysicalCollapse();
                    else
                        Destroy(piece);
                }
                spawnedPieces.Remove(pieceId);
            }
        }

        private void DestroyAllLocalPieces()
        {
            foreach (var kvp in spawnedPieces)
            {
                if (kvp.Value != null) 
                {
                    var buildPiece = kvp.Value.GetComponent<BuildPiece>();
                    if (buildPiece != null)
                        buildPiece.TriggerPhysicalCollapse();
                    else
                        Destroy(kvp.Value);
                }
            }
            spawnedPieces.Clear();
        }

        private void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        #endregion

        #region Save/Load System

        private const string BUILD_SAVE_FILENAME = "builds";

        private void MarkDirty()
        {
            hasPendingChanges = true;
            pendingSaveTimer = autoSaveDelay;
        }

        private void PerformSave()
        {
            SaveBuildings();
            hasPendingChanges = false;
            pendingSaveTimer = -1f;
        }

        private async void SaveBuildings()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.LogWarning("[BuildManager] No ActiveGameId — skipping save.");
                return;
            }

            try
            {
                BuildSaveData saveData = new BuildSaveData
                {
                    nextPieceId = nextPieceId,
                    pieces = new BuildPieceSaveEntry[buildPieces.Count]
                };

                for (int i = 0; i < buildPieces.Count; i++)
                {
                    saveData.pieces[i] = new BuildPieceSaveEntry(buildPieces[i]);
                }

                await SaveGameManager.Instance.SaveGameDataAsync(gameId, BUILD_SAVE_FILENAME, saveData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildManager] Save failed: {e.Message}");
            }
        }

        private async void LoadBuildings()
        {
            if (!IsServer) return;

            string gameId = SaveGameManager.Instance?.ActiveGameId;
            if (string.IsNullOrEmpty(gameId))
            {
                Debug.Log("[BuildManager] No ActiveGameId — starting fresh.");
                return;
            }

            try
            {
                BuildSaveData saveData = await SaveGameManager.Instance.LoadGameDataAsync<BuildSaveData>(gameId, BUILD_SAVE_FILENAME);

                if (saveData == null || saveData.pieces == null || saveData.pieces.Length == 0)
                {
                    Debug.Log("[BuildManager] No saved buildings found — starting fresh.");
                    return;
                }

                nextPieceId = saveData.nextPieceId;

                // Clear and rebuild support graph
                supportGraph.Clear();

                // 1. Add all pieces to Netcode list (this spawns the GameObjects and registers snap points)
                foreach (var entry in saveData.pieces)
                {
                    var data = entry.ToData();
                    buildPieces.Add(data);
                }

                // 2. Reconstruct connections and support graph for all pieces
                ReconstructAllConnectionsAndSupport();

                Debug.Log($"[BuildManager] Loaded {saveData.pieces.Length} pieces from save and auto-reconstructed support graph.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildManager] Load failed: {e.Message}");
            }
        }

        #endregion

        #region Resource Checking

        private bool HasRequiredResources(ResourceCost[] costs)
        {
            if (costs == null || costs.Length == 0) return true;

            var inventory = LocalPlayerInstance.InventoryManager;
            if (inventory == null) return false;

            foreach (var cost in costs)
            {
                if (cost.resource == null) continue;
                if (inventory.GetTotalCount(cost.resource.itemId) < cost.amount)
                    return false;
            }
            return true;
        }

        private void ConsumeResources(ResourceCost[] costs)
        {
            if (costs == null) return;

            var inventory = LocalPlayerInstance.InventoryManager;
            if (inventory == null) return;

            foreach (var cost in costs)
            {
                if (cost.resource == null) continue;
                inventory.RemoveItemByIDRpc(cost.resource.itemId, cost.amount);
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Log All Pieces")]
        private void LogAllPieces()
        {
            Debug.Log($"[BuildManager] Total: {buildPieces.Count} pieces");
            foreach (var p in buildPieces)
            {
                Debug.Log($"  ID:{p.pieceId} Prefab:{p.prefabIndex} Pos:{p.position} Blueprint:{p.isBlueprint}");
            }
        }

        #endregion

        #region Blueprint Helpers

        /// <summary>
        /// Gets the prefab index for a given piece ID by searching the network list.
        /// Used by BuildPiece.TryFillBlueprint to look up resource costs.
        /// </summary>
        public int GetPrefabIndexForPiece(int pieceId)
        {
            foreach (var piece in buildPieces)
            {
                if (piece.pieceId == pieceId)
                    return piece.prefabIndex;
            }
            return -1;
        }

        /// <summary>
        /// Raycasts from screen center to find a blueprint piece and tries to fill it.
        /// Called when the player presses interact while not in build mode.
        /// </summary>
        private void TryFillBlueprintAtCrosshair()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxBuildDistance, buildPieceLayer))
            {
                var buildPiece = hit.collider.GetComponentInParent<BuildPiece>();
                if (buildPiece != null && buildPiece.isBlueprintPiece)
                {
                    buildPiece.TryFillBlueprint();
                }
            }
        }

        /// <summary>
        /// Raycasts from screen center to find a placed build piece and deletes it.
        /// Works on both solid and blueprint pieces.
        /// </summary>
        private void TryDeletePieceAtCrosshair()
        {
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxBuildDistance, buildPieceLayer))
            {
                var buildPiece = hit.collider.GetComponentInParent<BuildPiece>();
                if (buildPiece != null && buildPiece.IsPlacedPiece)
                {
                    buildPiece.DestroyPiece();
                }
            }
        }

        /// <summary>
        /// Checks if the player currently has a hammer tool equipped.
        /// Looks for BuildHammer component on the equipped item's prefab.
        /// Public so BuildMenuUI can also check this.
        /// </summary>
        public bool IsHammerEquipped()
        {
            if (equipmentController == null || inventoryManager == null) return false;

            int equippedId = equipmentController.EquippedItemId;
            if (equippedId < 0) return false;

            var itemData = inventoryManager.GetItemData(equippedId);
            if (itemData == null || itemData.localPrefab == null) return false;

            return itemData.localPrefab.GetComponent<BuildHammer>() != null;
        }

        /// <summary>
        /// Per-frame hover outline update. Raycasts from screen center to highlight
        /// placed/blueprint pieces. Skipped when ghost is active or hammer not equipped.
        /// </summary>
        private void UpdateHoverOutline()
        {
            // No outline when hammer not equipped
            if (!IsHammerEquipped())
            {
                ClearHoveredPiece();
                return;
            }

            // Ghost active — outline the piece being snapped to
            if (currentGhost != null)
            {
                var snapTarget = currentGhost.SnappedToBuildPiece;
                if (snapTarget != null && snapTarget.IsPlacedPiece)
                {
                    if (snapTarget != hoveredPiece)
                    {
                        ClearHoveredPiece();
                        hoveredPiece = snapTarget;
                        hoveredPiece.ApplyOutline(outlineMaterial);
                    }
                }
                else
                {
                    // Ghost not snapping to anything — clear
                    ClearHoveredPiece();
                }
                return;
            }

            // No ghost — raycast from crosshair to outline hovered piece
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxBuildDistance, buildPieceLayer))
            {
                var piece = hit.collider.GetComponentInParent<BuildPiece>();
                if (piece != null && piece.IsPlacedPiece)
                {
                    if (piece != hoveredPiece)
                    {
                        ClearHoveredPiece();
                        hoveredPiece = piece;
                        hoveredPiece.ApplyOutline(outlineMaterial);
                    }
                    return;
                }
            }

            // Nothing valid hit — clear
            ClearHoveredPiece();
        }

        private void ClearHoveredPiece()
        {
            if (hoveredPiece != null)
            {
                hoveredPiece.RemoveOutline();
                hoveredPiece = null;
            }
        }

        #endregion
    }
}